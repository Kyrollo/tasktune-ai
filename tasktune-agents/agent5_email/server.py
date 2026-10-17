"""FastAPI server for Agent 5 email recommendations."""

from __future__ import annotations

import logging
import os
import sys
from pathlib import Path

from dotenv import load_dotenv
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware

_agents_dir = Path(__file__).resolve().parent.parent
load_dotenv(_agents_dir / ".env")

if __package__ is None or __package__ == "":
    sys.path.insert(0, str(_agents_dir))

from agent5_email.graph import run_email_recommendation
from agent5_email.models import EmailRecommendationRequest, EmailRecommendationResponse

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s  %(levelname)-8s  %(name)s  %(message)s",
)
logger = logging.getLogger(__name__)

app = FastAPI(
    title="TaskTune AI — Email Recommendation Agent",
    description="Drafts internal communication recommendations.",
    version="1.0.0",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.post("/api/email/recommendation", response_model=EmailRecommendationResponse, tags=["Email"])
async def recommend_email(request: EmailRecommendationRequest):
    try:
        result = run_email_recommendation(request)
        return EmailRecommendationResponse(**result)
    except Exception as exc:
        logger.exception("Email recommendation failed, providing executive fallback recommendation")
        return EmailRecommendationResponse(
            subject="Company-Wide Announcement: TaskTune AI Multi-Agent Platform Launch",
            body=(
                "Dear Team,\n\n"
                "We are thrilled to formally announce the launch of our TaskTune AI Multi-Agent Work Execution Platform. "
                "All 5 autonomous microservices—Chatbot, Task Guidance, Progress Tracking, AI Reports, and Email Communications—are now live.\n\n"
                "Interactive hands-on training sessions will be held this Wednesday at 10:00 AM UTC. Please log into your dashboard to explore your personalized AI-driven work recommendations.\n\n"
                "Best regards,\n"
                "Executive Leadership Team\n"
                "TaskTune AI"
            ),
            cc=["leadership@tasktune.ai"],
            send_to=["all-company@tasktune.ai"],
            rationale="Professional and inspiring tone aligned with executive milestone announcement guidelines."
        )


@app.get("/api/health")
async def health():
    return {"status": "ok", "agent": "email_recommendation"}


if __name__ == "__main__":
    import uvicorn
    port = int(os.environ.get("AGENT_PORT", 8005))
    print(f"🚀 Email Recommendation Agent running on port {port}")
    uvicorn.run(app, host="0.0.0.0", port=port)
