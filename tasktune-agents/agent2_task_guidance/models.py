"""Pydantic models for structured LLM outputs."""

from __future__ import annotations

from pydantic import BaseModel, Field


class TaskComplexityAnalysis(BaseModel):
    """Output of the analyze_task node."""

    complexity: str = Field(
        description="Either 'simple' or 'complex'. Simple = few subtasks, no "
        "blocking dependencies, short timeline. Complex = many subtasks, "
        "dependencies, or long timeline."
    )
    reasoning: str = Field(
        description="One-sentence explanation for the complexity rating."
    )


class TaskBreakdownItem(BaseModel):
    """A main task with its nested subtasks."""

    task: str = Field(description="Name of the main task or work area.")
    subtasks: list[str] = Field(
        description="Specific subtasks that must be completed under this task."
    )
    priority: str = Field(
        description="Priority level: 'high', 'medium', or 'low'."
    )


class QuickGuidance(BaseModel):
    """Concise guidance for straightforward tasks."""

    guidance_text: str = Field(
        description="Brief, actionable guidance paragraph for the task."
    )
    suggested_steps: list[str] = Field(
        description="3-5 concrete action steps the assignee should follow."
    )
    task_breakdown: list[TaskBreakdownItem] = Field(
        description="Nested breakdown of main tasks, each with its own "
        "subtasks and priority. 2-4 items for simple tasks."
    )
    estimated_time: str = Field(
        description="Human-readable estimate, e.g. '2-3 hours' or '1 day'."
    )
    resources: list[str] = Field(
        description="Helpful links, tools, or internal references."
    )


class DetailedGuidance(BaseModel):
    """In-depth guidance for complex, multi-faceted tasks."""

    guidance_text: str = Field(
        description="Detailed guidance covering approach, methodology, and "
        "key considerations for the task."
    )
    suggested_steps: list[str] = Field(
        description="5-10 detailed action steps, ordered by recommended "
        "execution sequence."
    )
    task_breakdown: list[TaskBreakdownItem] = Field(
        description="Nested breakdown of main tasks, each with its own "
        "subtasks and priority. 4-8 items for complex tasks."
    )
    estimated_time: str = Field(
        description="Human-readable estimate with breakdown, e.g. "
        "'3-5 days (research: 1d, implementation: 2-3d, review: 1d)'."
    )
    resources: list[str] = Field(
        description="Comprehensive list of tools, documentation, templates, "
        "or team contacts that will help."
    )


class RiskAssessment(BaseModel):
    """Risk factors identified for the task."""

    risk_factors: list[str] = Field(
        description="Specific risks like deadline pressure, dependency "
        "blockers, skill gaps, or scope creep."
    )
    mitigation_tips: list[str] = Field(
        description="One mitigation tip per identified risk."
    )


class SuccessCriteria(BaseModel):
    """What the person should deliver and how to know they did well."""

    deliverables: list[str] = Field(
        description="Concrete outputs the person must hand over when done."
    )
    quality_indicators: list[str] = Field(
        description="How to judge if the work is done well — measurable or "
        "observable signs of quality."
    )
    completion_checklist: list[str] = Field(
        description="Final checks the person should run before marking the "
        "task as complete."
    )


class TaskGuidanceOutput(BaseModel):
    """Final compiled output returned to the caller."""

    guidance_text: str
    suggested_subtasks: list[str]
    task_breakdown: list[dict]
    estimated_time: str
    resources: list[str]
    risk_factors: list[str]
    success_criteria: dict
