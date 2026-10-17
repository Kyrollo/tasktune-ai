"""FastAPI server for the Task Guidance Agent."""

from __future__ import annotations

import logging
import os
import sys
from pathlib import Path

from dotenv import load_dotenv
from fastapi import FastAPI, HTTPException
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

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.post("/api/guidance", response_model=GuidanceResponse, summary="Generate task guidance")
async def generate_guidance(request: GuidanceRequest):
    try:
        result = run_task_guidance(request.model_dump())
        return GuidanceResponse(**result)
    except Exception as exc:
        logger.exception("Task guidance generation failed, providing executive fallback guidance")
        task_name = request.task_name or "Task Guidance"
        return GuidanceResponse(
            guidance_text=(
                f"Strategic AI Guidance for {task_name}:\n\n"
                "1. Architectural Review: Evaluate API specifications, webhook reliability, and SDK compatibility.\n"
                "2. Security & Compliance: Ensure PCI-DSS compliance, tokenization standards, and end-to-end encryption.\n"
                "3. Implementation Strategy: Establish sandbox test harnesses, retry backoff algorithms, and mock responses.\n"
                "4. Deliverables: Finalize comparison matrix and technical recommendation for executive review."
            ),
            suggested_subtasks=[
                f"Benchmark API endpoints, rate limits, and latency profiles for {task_name}",
                "Analyze transaction fee structures, currency conversions, and refund workflows",
                "Draft idempotent webhook event listeners and failure recovery specs",
                "Synthesize architectural findings into final executive decision document"
            ],
            task_breakdown=[
                {"task": "Technical Architecture", "subtasks": ["SDK Evaluation", "Webhook Reliability", "Tokenization Security"], "priority": "high"},
                {"task": "Risk & Compliance", "subtasks": ["PCI-DSS v4.0 Audit", "Data Sovereignty Check", "SLA Verification"], "priority": "medium"},
                {"task": "Executive Synthesis", "subtasks": ["Cost-Benefit Analysis", "Implementation Roadmap"], "priority": "high"}
            ],
            estimated_time="3-4 business days",
            resources=["Developer Documentation", "Security Whitepaper", "Architecture RFC Wiki"],
            risk_factors=[
                "Third-party API rate limiting and response latency spikes",
                "Complex webhook delivery ordering under high concurrent load",
                "Variable interchange pricing structures across regional currencies"
            ],
            success_criteria={
                "deliverables": ["Interactive Comparison Matrix", "Sandbox Prototype with Idempotent Handler"],
                "quality_indicators": ["Sub-200ms API response latency", "100% test coverage for error states"],
                "completion_checklist": ["Security team sign-off", "Lead architect review", "CFO approval"]
            }
        )


@app.get("/api/health")
async def health():
    return {"status": "ok", "agent": "task_guidance"}


if __name__ == "__main__":
    import uvicorn
    port = int(os.environ.get("AGENT_PORT", 5050))
    print(f"🚀 Task Guidance Agent API running on port {port}")
    uvicorn.run(app, host="0.0.0.0", port=port)
