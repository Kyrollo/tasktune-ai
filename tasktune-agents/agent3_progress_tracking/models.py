"""
Pydantic models for API request / response validation.

These models define the contract between the FastAPI endpoint and consumers
(e.g. the ASP.NET Core backend).  Enum‑like fields (progress_summary,
risk_level) are always in English; free‑text fields adapt to the caller's
language.
"""

from __future__ import annotations

from typing import List, Optional

from pydantic import BaseModel, Field


# ── Request models ──────────────────────────────────────────────────

class ProgressUpdateModel(BaseModel):
    """A single progress update submitted by a user."""
    submitted_at: str
    completion_percentage: int = Field(..., ge=0, le=100)
    notes: str = ""
    blockers: str = ""


class UserPerformanceModel(BaseModel):
    """Historical performance snapshot."""
    evaluation_date: str
    average_score: Optional[float] = None
    on_time_delivery_rate: Optional[float] = None


class ProgressTrackingRequest(BaseModel):
    """Payload accepted by POST /api/ai/progress-tracking."""
    task_id: str
    task_title: str = ""
    task_description: str = ""
    current_completion_percentage: int = Field(..., ge=0, le=100)
    start_date: str = ""
    due_date: str
    progress_updates: List[ProgressUpdateModel] = []
    current_blockers: List[str] = []
    user_performance_history: List[UserPerformanceModel] = []
    agent_config: dict = Field(default_factory=dict)


# ── Response model ──────────────────────────────────────────────────

class ProgressTrackingResponse(BaseModel):
    """Payload returned by the Progress Tracking Agent."""
    progress_summary: str = Field(
        ...,
        description="One of: On track, At risk, Delayed",
    )
    completion_prediction: Optional[str] = Field(
        None,
        description="Predicted completion date in YYYY-MM-DD format, or null",
    )
    blockers_analysis: str = Field(
        ...,
        description="Narrative analysis of blockers and risk factors (matches input language)",
    )
    recommendations: List[str] = Field(
        ...,
        description="2–4 actionable recommendations (matches input language)",
    )
    risk_level: str = Field(
        ...,
        description="One of: low, medium, high",
    )
