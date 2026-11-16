"""FastAPI server for the Progress Tracking Agent."""

from __future__ import annotations

import logging
import os
import sys
from pathlib import Path

from dotenv import load_dotenv
import hmac
from fastapi import Depends, FastAPI, Header, HTTPException
from fastapi.middleware.cors import CORSMiddleware

_agents_dir = Path(__file__).resolve().parent.parent
load_dotenv(_agents_dir / ".env")

if __package__ is None or __package__ == "":
    sys.path.insert(0, str(_agents_dir))

from agent3_progress_tracking.graph import run_progress_tracking
from agent3_progress_tracking.models import ProgressTrackingRequest, ProgressTrackingResponse

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s  %(levelname)-8s  %(name)s  %(message)s",
)
logger = logging.getLogger(__name__)

_AGENT_SERVICE_TOKEN = os.environ.get("AGENT_SERVICE_TOKEN", "")

async def require_agent_service_token(x_agent_internal_token: str = Header(default="")):
    if not _AGENT_SERVICE_TOKEN:
        raise HTTPException(status_code=503, detail="Agent service authentication is not configured.")
    if not hmac.compare_digest(x_agent_internal_token, _AGENT_SERVICE_TOKEN):
        raise HTTPException(status_code=401, detail="Unauthorized agent service caller.")


app = FastAPI(
    title="TaskTune AI — Progress Tracking Agent",
    description="Analyzes task progress data and returns predictive insights.",
    version="1.0.0",
)

@app.post("/api/ai/progress-tracking", response_model=ProgressTrackingResponse, tags=["Progress Tracking"], dependencies=[Depends(require_agent_service_token)])
async def progress_tracking(request: ProgressTrackingRequest):
    try:
        result = run_progress_tracking(request.model_dump())
        return ProgressTrackingResponse(**result)
    except Exception as exc:
        logger.exception("Progress tracking failed")
        raise HTTPException(status_code=503, detail="Progress analysis is temporarily unavailable.") from exc


@app.get("/api/health")
async def health():
    return {"status": "ok", "agent": "progress_tracking"}


if __name__ == "__main__":
    import uvicorn
    port = int(os.environ.get("AGENT_PORT", 8003))
    print(f"🚀 Progress Tracking Agent running on port {port}")
    uvicorn.run(app, host="0.0.0.0", port=port)
