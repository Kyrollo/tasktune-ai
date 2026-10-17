"""LangGraph node functions for the Progress Tracking Agent.

Each function takes the full ``ProgressTrackingState`` and returns a partial
state update dict that LangGraph merges back into the state.

Mirrors the agent2_task_guidance structure: synchronous calls with
structured output parsing from the notebook logic.
"""

from __future__ import annotations

import json
import logging
from typing import Any

from langchain_groq import ChatGroq

from .prompts import (
    ANALYZE_PROGRESS_PROMPT,
    PREDICT_COMPLETION_PROMPT,
    RECOMMENDATIONS_PROMPT,
    RISK_ASSESSMENT_PROMPT,
)
from .state import ProgressTrackingState

logger = logging.getLogger(__name__)

import os
MODEL_NAME = os.getenv("GROQ_MODEL", "qwen/qwen3.8-27b")


def _get_agent_config(state: ProgressTrackingState) -> dict[str, Any]:
    config = state.get("input", {}).get("agent_config", {})
    return config if isinstance(config, dict) else {}


def _get_llm(state: ProgressTrackingState, temperature: float = 0.2) -> ChatGroq:
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


def _clean_json_text(text: str) -> str:
    """Strip markdown code fences that some models add despite instructions."""
    text = text.strip()
    if text.startswith("```json"):
        text = text[7:]
    elif text.startswith("```"):
        text = text[3:]
    if text.endswith("```"):
        text = text[:-3]
    return text.strip()


def _parse_json_response(text: str) -> dict[str, Any]:
    """Parse a JSON string from the LLM response, tolerating code fences."""
    try:
        return json.loads(_clean_json_text(text))
    except json.JSONDecodeError as e:
        logger.error(f"Failed to parse JSON: {text[:200]}... Error: {e}")
        return {}


def _input_vars(state: ProgressTrackingState) -> dict[str, str]:
    """Build the common template variables from the input block."""
    inp = state.get("input", {})
    return {
        "task_id": inp.get("task_id", "N/A"),
        "task_title": inp.get("task_title", ""),
        "task_description": inp.get("task_description", ""),
        "current_completion_percentage": str(inp.get("current_completion_percentage", 0)),
        "start_date": inp.get("start_date", "N/A"),
        "due_date": inp.get("due_date", "N/A"),
        "progress_updates": json.dumps(
            inp.get("progress_updates", []), ensure_ascii=False, indent=2
        ),
        "current_blockers": ", ".join(inp.get("current_blockers", [])) or "None",
        "user_performance_history": json.dumps(
            inp.get("user_performance_history", []), ensure_ascii=False, indent=2
        ),
    }


# ── Node 1: Analyze Progress ───────────────────────────────────────

def analyze_progress(state: ProgressTrackingState) -> dict:
    """Determine whether the task is On track, At risk, or Delayed."""
    try:
        llm = _get_llm(state, temperature=0.2)
        variables = _input_vars(state)
        chain = ANALYZE_PROGRESS_PROMPT | llm
        response = chain.invoke(variables)

        result = _parse_json_response(response.content)
        summary = result.get("progress_summary", "At risk")
        reason = result.get("reason", "")

        # Validate enum value
        if summary not in {"On track", "At risk", "Delayed"}:
            summary = "At risk"

        return {
            "progress_summary": summary,
            "messages": [f"[analyze_progress] Summary: {summary} — {reason}"],
        }
    except Exception as exc:
        logger.exception("analyze_progress failed")
        return {
            "progress_summary": "At risk",
            "messages": [f"[analyze_progress] ERROR: {exc}"],
        }


# ── Node 2: Predict Completion ─────────────────────────────────────

def predict_completion(state: ProgressTrackingState) -> dict:
    """Estimate the most likely completion date."""
    try:
        llm = _get_llm(state, temperature=0.3)
        variables = {
            **_input_vars(state),
            "progress_summary": state.get("progress_summary", "Unknown"),
        }
        chain = PREDICT_COMPLETION_PROMPT | llm
        response = chain.invoke(variables)

        result = _parse_json_response(response.content)
        prediction = result.get("completion_prediction")
        reasoning = result.get("reasoning", "")

        # Normalize "null" string to None
        if prediction and str(prediction).lower() in {"null", "none", "n/a", ""}:
            prediction = None

        return {
            "completion_prediction": prediction,
            "messages": [f"[predict_completion] Prediction: {prediction} — {reasoning}"],
        }
    except Exception as exc:
        logger.exception("predict_completion failed")
        return {
            "completion_prediction": None,
            "messages": [f"[predict_completion] ERROR: {exc}"],
        }


# ── Node 3: Assess Risk ────────────────────────────────────────────

def assess_risk(state: ProgressTrackingState) -> dict:
    """Assess risk level and analyze blockers in detail."""
    try:
        llm = _get_llm(state, temperature=0.3)
        variables = {
            **_input_vars(state),
            "progress_summary": state.get("progress_summary", "Unknown"),
            "completion_prediction": state.get("completion_prediction") or "N/A",
        }
        chain = RISK_ASSESSMENT_PROMPT | llm
        response = chain.invoke(variables)

        result = _parse_json_response(response.content)
        risk_level = result.get("risk_level", "medium").lower()
        blockers_analysis = result.get("blockers_analysis", "No analysis available.")
        risk_factors = result.get("risk_factors", [])

        if risk_level not in {"low", "medium", "high"}:
            risk_level = "medium"

        return {
            "risk_level": risk_level,
            "blockers_analysis": blockers_analysis,
            "messages": [
                f"[assess_risk] Risk: {risk_level} | Factors: {risk_factors}"
            ],
        }
    except Exception as exc:
        logger.exception("assess_risk failed")
        return {
            "risk_level": "medium",
            "blockers_analysis": "Analysis unavailable due to an internal error.",
            "messages": [f"[assess_risk] ERROR: {exc}"],
        }


# ── Node 4: Generate Recommendations ──────────────────────────────

def generate_recommendations(state: ProgressTrackingState) -> dict:
    """Produce 2-4 actionable recommendations."""
    try:
        llm = _get_llm(state, temperature=0.4)
        variables = {
            **_input_vars(state),
            "progress_summary": state.get("progress_summary", "Unknown"),
            "completion_prediction": state.get("completion_prediction") or "N/A",
            "risk_level": state.get("risk_level", "medium"),
            "blockers_analysis": state.get("blockers_analysis", ""),
        }
        chain = RECOMMENDATIONS_PROMPT | llm
        response = chain.invoke(variables)

        result = _parse_json_response(response.content)
        recommendations = result.get("recommendations", [])

        if not isinstance(recommendations, list):
            recommendations = [str(recommendations)]

        # Ensure we have at least 1 recommendation
        if not recommendations:
            recommendations = ["Review current blockers and escalate if needed."]

        return {
            "recommendations": recommendations,
            "messages": [
                f"[generate_recommendations] {len(recommendations)} recommendation(s) generated"
            ],
        }
    except Exception as exc:
        logger.exception("generate_recommendations failed")
        return {
            "recommendations": ["Review current blockers and escalate if needed."],
            "messages": [f"[generate_recommendations] ERROR: {exc}"],
        }


# ── Node 5: Compile Output ─────────────────────────────────────────

def compile_output(state: ProgressTrackingState) -> dict:
    """Assemble the final API-ready response from the accumulated state."""
    # Normalize progress_summary
    summary = state.get("progress_summary", "At risk")
    valid_summaries = {"On track", "At risk", "Delayed"}
    if summary not in valid_summaries:
        matched = False
        for vs in valid_summaries:
            if summary.strip().lower() == vs.lower():
                summary = vs
                matched = True
                break
        if not matched:
            summary = "At risk"

    # Normalize risk_level
    risk = state.get("risk_level", "medium").strip().lower()
    if risk not in {"low", "medium", "high"}:
        risk = "medium"

    final: dict[str, Any] = {
        "progress_summary": summary,
        "completion_prediction": state.get("completion_prediction"),
        "blockers_analysis": state.get("blockers_analysis", "No analysis available."),
        "recommendations": state.get("recommendations", ["Review current blockers and escalate if needed."]),
        "risk_level": risk,
    }

    return {
        "final_output": final,
        "messages": ["[compile_output] Final output assembled."],
    }
