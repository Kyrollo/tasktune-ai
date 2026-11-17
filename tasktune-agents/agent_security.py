"""Shared authentication boundary for private TaskTune agent services."""

from __future__ import annotations

import hmac
import os

from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse


def is_valid_service_key(expected: str, supplied: str) -> bool:
    """Validate configured/supplied keys without timing-sensitive equality."""
    return (
        len(expected.encode("utf-8")) >= 32
        and bool(supplied)
        and hmac.compare_digest(expected, supplied)
    )


def add_agent_security(app: FastAPI) -> None:
    """Require the API-to-agent shared secret for every route except health."""

    @app.middleware("http")
    async def require_internal_service_key(request: Request, call_next):
        if request.method == "GET" and request.url.path == "/api/health":
            return await call_next(request)

        expected = os.environ.get("TASKTUNE_SERVICE_API_KEY", "")
        if len(expected.encode("utf-8")) < 32:
            return JSONResponse(
                status_code=503,
                content={"detail": "Agent service authentication is not configured."},
            )

        supplied = request.headers.get("X-TaskTune-Service-Key", "")
        if not is_valid_service_key(expected, supplied):
            return JSONResponse(
                status_code=401,
                content={"detail": "Unauthorized agent service request."},
            )

        return await call_next(request)
