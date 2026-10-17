"""Entry point for Agent 1 chatbot."""

from __future__ import annotations

import os

import uvicorn


if __name__ == "__main__":
    uvicorn.run("agent1_chatbot.server:app", host="0.0.0.0", port=int(os.environ.get("AGENT_PORT", 8001)))
