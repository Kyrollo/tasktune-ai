"""LangGraph state definition for the Progress Tracking Agent."""

from __future__ import annotations

import operator
from typing import Annotated, Any, Optional

from typing_extensions import TypedDict


class ProgressTrackingInput(TypedDict, total=False):
    """Raw progress data coming from the caller / API."""

    task_id: str
    task_title: str
    task_description: str
    current_completion_percentage: int
    start_date: str
    due_date: str
    progress_updates: list[dict[str, Any]]
    current_blockers: list[str]
    user_performance_history: list[dict[str, Any]]


class ProgressTrackingState(TypedDict, total=False):
    """Full state flowing through the LangGraph.

    *  ``input`` is written once at invocation time.
    *  ``progress_summary`` is set by the ``analyze_progress`` node.
    *  ``completion_prediction`` is set by the ``predict_completion`` node.
    *  ``blockers_analysis`` and ``risk_level`` are set by ``assess_risk``.
    *  ``recommendations`` is set by ``generate_recommendations``.
    *  ``final_output`` is the compiled JSON-ready dict returned to the caller.
    *  ``messages`` keeps an append-only log useful for debugging.
    """

    input: ProgressTrackingInput
    progress_summary: str                # "On track" | "At risk" | "Delayed"
    completion_prediction: Optional[str] # ISO date (YYYY-MM-DD) or None
    blockers_analysis: str               # Detailed blocker / risk narrative
    recommendations: list[str]           # 2-4 actionable suggestions
    risk_level: str                      # "low" | "medium" | "high"
    final_output: dict[str, Any]
    messages: Annotated[list[str], operator.add]