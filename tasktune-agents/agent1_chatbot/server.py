"""FastAPI server for the TaskTune chatbot agent."""

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

from agent1_chatbot.graph import run_chatbot
from agent1_chatbot.models import ChatbotRequest, ChatbotResponse

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
    title="TaskTune AI — Chatbot Agent",
    description="Role-aware enterprise chatbot.",
    version="1.0.0",
)

@app.post("/api/chat", response_model=ChatbotResponse, tags=["Chatbot"], dependencies=[Depends(require_agent_service_token)])
async def chat(request: ChatbotRequest):
    try:
        result = run_chatbot(request)
        return ChatbotResponse(**result)
    except Exception as exc:
        logger.exception("Chatbot request failed; returning a service error")
        raise HTTPException(status_code=503, detail="Chatbot analysis is temporarily unavailable.") from exc


@app.get("/api/health", tags=["Health"])
async def health():
    return {"status": "ok", "agent": "chatbot"}


if __name__ == "__main__":
    import uvicorn
    port = int(os.environ.get("AGENT_PORT", 8001))
    print(f"🚀 Chatbot Agent running on port {port}")
    uvicorn.run(app, host="0.0.0.0", port=port)
