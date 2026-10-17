"""LangGraph state definition for the Task Guidance Agent."""

from __future__ import annotations

import operator
from typing import Annotated, Any

from typing_extensions import TypedDict


class TaskInput(TypedDict, total=False):
    """Raw task data coming from the caller / API."""

    task_name: str
    description: str
    subtasks: list[dict[str, Any]]
    definition_of_done: str
    due_date: str
    dependencies: list[dict[str, Any]]
    industry: str
    user_role: str
    completion_percentage: int


class TaskGuidanceState(TypedDict, total=False):
    """Full state flowing through the LangGraph.

    *  ``task`` is written once at invocation time.
    *  ``complexity`` is set by the ``analyze_task`` node.
    *  ``guidance_text`` and the list fields are populated by guidance nodes.
    *  ``risk_factors`` is populated by the risk-assessment node.
    *  ``final_output`` is the compiled JSON-ready dict returned to the caller.
    *  ``messages`` keeps an append-only log useful for debugging.
    """

    task: TaskInput
    complexity: str  # "simple" | "complex"
    guidance_text: str
    suggested_steps: list[str]
    task_breakdown: list[dict[str, Any]]
    estimated_time: str
    resources: list[str]
    risk_factors: list[str]
    success_criteria: dict[str, Any]
    final_output: dict[str, Any]
    messages: Annotated[list[str], operator.add]
