"""
FastAPI application exposing the Progress Tracking Agent as an HTTP endpoint.

Run with:
    cd tasktune-agents
    uvicorn agent3_progress_tracking.main:app --reload --port 8003
"""

from __future__ import annotations

import logging

from dotenv import load_dotenv
from fastapi import FastAPI, HTTPException

from .graph import run_progress_tracking
from .models import ProgressTrackingRequest, ProgressTrackingResponse

load_dotenv()

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s  %(levelname)-8s  %(name)s  %(message)s",
)

app = FastAPI(
    title="TaskTune AI — Progress Tracking Agent",
    description=(
        "Analyzes task progress data and returns predictive insights "
        "including status assessment, completion prediction, risk analysis, "
        "and actionable recommendations.  Supports English and Arabic."
    ),
    version="1.0.0",
)


@app.post(
    "/api/ai/progress-tracking",
    response_model=ProgressTrackingResponse,
    summary="Analyze task progress",
    tags=["Progress Tracking"],
)
async def progress_tracking(request: ProgressTrackingRequest):
    """Accept a progress‑tracking request and return AI‑powered insights."""
    try:
        result = await run_progress_tracking(request.model_dump())
        return ProgressTrackingResponse(**result)
    except Exception as exc:
        logging.getLogger(__name__).exception("Progress tracking failed")
        raise HTTPException(status_code=500, detail=str(exc))


@app.get("/health", tags=["Health"])
async def health():
    """Simple liveness probe."""
    return {"status": "ok", "agent": "progress_tracking"}