"""FastAPI server for the Task Guidance Agent."""

from __future__ import annotations

import logging
import os
import sys
from pathlib import Path

from dotenv import load_dotenv
import hmac
from fastapi import Depends, FastAPI, Header, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field

_agents_dir = Path(__file__).resolve().parent.parent
load_dotenv(_agents_dir / ".env")

if __package__ is None or __package__ == "":
    sys.path.insert(0, str(_agents_dir))

from agent2_task_guidance.graph import run_task_guidance

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



class GuidanceRequest(BaseModel):
    task_name: str
    description: str = ""
    subtasks: list[dict] = Field(default_factory=list)
    definition_of_done: str = ""
    due_date: str = "N/A"
    dependencies: list[str] = Field(default_factory=list)
    industry: str = "General"
    user_role: str = ""
    completion_percentage: int = 0
    agent_config: dict = Field(default_factory=dict)


class GuidanceResponse(BaseModel):
    guidance_text: str = ""
    suggested_subtasks: list[str] = Field(default_factory=list)
    task_breakdown: list[dict] = Field(default_factory=list)
    estimated_time: str = ""
    resources: list[str] = Field(default_factory=list)
    risk_factors: list[str] = Field(default_factory=list)
    success_criteria: dict = Field(default_factory=dict)


app = FastAPI(
    title="TaskTune AI — Task Guidance Agent",
    description="Analyzes tasks and returns AI-powered guidance.",
    version="1.0.0",
)

@app.post("/api/guidance", response_model=GuidanceResponse, summary="Generate task guidance", dependencies=[Depends(require_agent_service_token)])
async def generate_guidance(request: GuidanceRequest):
    try:
        result = run_task_guidance(request.model_dump())
        return GuidanceResponse(**result)
    except Exception as exc:
        logger.exception("Task guidance generation failed")
        raise HTTPException(status_code=503, detail="Task guidance is temporarily unavailable.") from exc


@app.get("/api/health")
async def health():
    return {"status": "ok", "agent": "task_guidance"}


if __name__ == "__main__":
    import uvicorn
    port = int(os.environ.get("AGENT_PORT", 5050))
    print(f"🚀 Task Guidance Agent API running on port {port}")
    uvicorn.run(app, host="0.0.0.0", port=port)
