"""Entry point for Agent 5 email recommendations."""

from __future__ import annotations

import os

import uvicorn


if __name__ == "__main__":
    uvicorn.run("agent5_email.server:app", host="0.0.0.0", port=int(os.environ.get("AGENT_PORT", 8005)))
