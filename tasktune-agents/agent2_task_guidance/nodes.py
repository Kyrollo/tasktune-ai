"""LangGraph node functions for the Task Guidance Agent.

Each function takes the full ``TaskGuidanceState`` and returns a partial
state update dict that LangGraph merges back into the state.
"""

from __future__ import annotations

import json
from typing import Any

from langchain_groq import ChatGroq

from .models import (
    DetailedGuidance,
    QuickGuidance,
    RiskAssessment,
    SuccessCriteria,
    TaskComplexityAnalysis,
)
from .prompts import (
    ANALYZE_TASK_PROMPT,
    DETAILED_GUIDANCE_PROMPT,
    QUICK_GUIDANCE_PROMPT,
    RISK_ASSESSMENT_PROMPT,
    SUCCESS_CRITERIA_PROMPT,
)
from .state import TaskGuidanceState

import os
MODEL_NAME = os.getenv("GROQ_MODEL", "qwen/qwen3.8-27b")


def _get_agent_config(state: TaskGuidanceState) -> dict[str, Any]:
    task = state.get("task", {})
    config = task.get("agent_config", {})
    return config if isinstance(config, dict) else {}


def _get_llm(state: TaskGuidanceState, temperature: float = 0.4) -> ChatGroq:
    config = _get_agent_config(state)
    kwargs: dict[str, Any] = {
        "model": config.get("model") or MODEL_NAME,
        "temperature": config.get("temperature", temperature),
    }

    if config.get("api_key"):
        kwargs["api_key"] = config["api_key"]

    if config.get("max_tokens"):
        kwargs["max_tokens"] = config["max_tokens"]

    return ChatGroq(**kwargs)


def _task_vars(state: TaskGuidanceState) -> dict[str, Any]:
    """Extract prompt variables from state, serialising lists to JSON."""
    task = state["task"]
    return {
        "task_name": task.get("task_name", "Untitled Task"),
        "description": task.get("description", "No description provided."),
        "subtasks": json.dumps(task.get("subtasks", []), indent=2),
        "definition_of_done": task.get("definition_of_done", "N/A"),
        "due_date": task.get("due_date", "N/A"),
        "dependencies": json.dumps(task.get("dependencies", []), indent=2),
        "industry": task.get("industry", "General"),
        "user_role": task.get("user_role", "Employee"),
        "completion_percentage": task.get("completion_percentage", 0),
    }


# ── Node: Analyze Task ──────────────────────────────────────────────────

def analyze_task(state: TaskGuidanceState) -> dict[str, Any]:
    """Determine whether the task is *simple* or *complex*."""
    llm = _get_llm(state, temperature=0.2)
    structured_llm = llm.with_structured_output(TaskComplexityAnalysis)
    chain = ANALYZE_TASK_PROMPT | structured_llm

    result: TaskComplexityAnalysis = chain.invoke(_task_vars(state))

    return {
        "complexity": result.complexity.lower(),
        "messages": [f"[analyze] complexity={result.complexity}: {result.reasoning}"],
    }


# ── Node: Quick Guidance (simple path) ──────────────────────────────────

def quick_guidance(state: TaskGuidanceState) -> dict[str, Any]:
    """Generate concise guidance for a simple task."""
    llm = _get_llm(state)
    structured_llm = llm.with_structured_output(QuickGuidance)
    chain = QUICK_GUIDANCE_PROMPT | structured_llm

    result: QuickGuidance = chain.invoke(_task_vars(state))

    breakdown = [item.model_dump() for item in result.task_breakdown]

    return {
        "guidance_text": result.guidance_text,
        "suggested_steps": result.suggested_steps,
        "task_breakdown": breakdown,
        "estimated_time": result.estimated_time,
        "resources": result.resources,
        "messages": [f"[quick_guidance] steps={len(result.suggested_steps)}, breakdown={len(breakdown)}"],
    }


# ── Node: Detailed Guidance (complex path) ──────────────────────────────

def detailed_guidance(state: TaskGuidanceState) -> dict[str, Any]:
    """Generate in-depth guidance for a complex task."""
    llm = _get_llm(state, temperature=0.5)
    structured_llm = llm.with_structured_output(DetailedGuidance)
    chain = DETAILED_GUIDANCE_PROMPT | structured_llm

    result: DetailedGuidance = chain.invoke(_task_vars(state))

    breakdown = [item.model_dump() for item in result.task_breakdown]

    return {
        "guidance_text": result.guidance_text,
        "suggested_steps": result.suggested_steps,
        "task_breakdown": breakdown,
        "estimated_time": result.estimated_time,
        "resources": result.resources,
        "messages": [f"[detailed_guidance] steps={len(result.suggested_steps)}, breakdown={len(breakdown)}"],
    }


# ── Node: Risk Assessment ───────────────────────────────────────────────

def assess_risks(state: TaskGuidanceState) -> dict[str, Any]:
    """Identify risk factors and mitigation tips."""
    llm = _get_llm(state, temperature=0.3)
    structured_llm = llm.with_structured_output(RiskAssessment)

    variables = _task_vars(state)
    variables["complexity"] = state.get("complexity", "unknown")

    chain = RISK_ASSESSMENT_PROMPT | structured_llm
    result: RiskAssessment = chain.invoke(variables)

    combined = [
        f"{risk} — Mitigation: {tip}"
        for risk, tip in zip(result.risk_factors, result.mitigation_tips)
    ]
    # If lengths don't match, append any extra risks without tips
    if len(result.risk_factors) > len(result.mitigation_tips):
        combined.extend(result.risk_factors[len(result.mitigation_tips) :])

    return {
        "risk_factors": combined,
        "messages": [f"[assess_risks] found {len(result.risk_factors)} risk(s)"],
    }


# ── Node: Success Criteria ───────────────────────────────────────────────

def define_success_criteria(state: TaskGuidanceState) -> dict[str, Any]:
    """Define what to deliver and how to know the work is done well."""
    llm = _get_llm(state, temperature=0.3)
    structured_llm = llm.with_structured_output(SuccessCriteria)

    variables = _task_vars(state)
    variables["complexity"] = state.get("complexity", "unknown")

    chain = SUCCESS_CRITERIA_PROMPT | structured_llm
    result: SuccessCriteria = chain.invoke(variables)

    return {
        "success_criteria": result.model_dump(),
        "messages": [
            f"[success_criteria] deliverables={len(result.deliverables)}, "
            f"checks={len(result.completion_checklist)}"
        ],
    }


# ── Node: Compile Output ────────────────────────────────────────────────

def compile_output(state: TaskGuidanceState) -> dict[str, Any]:
    """Assemble the final JSON-ready output."""
    output = {
        "guidance_text": state.get("guidance_text", ""),
        "suggested_subtasks": state.get("suggested_steps", []),
        "task_breakdown": state.get("task_breakdown", []),
        "estimated_time": state.get("estimated_time", ""),
        "resources": state.get("resources", []),
        "risk_factors": state.get("risk_factors", []),
        "success_criteria": state.get("success_criteria", {}),
    }
    return {
        "final_output": output,
        "messages": ["[compile_output] done"],
    }
