"""Node functions for Agent 5 email recommendations."""

from __future__ import annotations

import json

from langchain_groq import ChatGroq
from langchain_core.messages import HumanMessage, SystemMessage
from pydantic import BaseModel, Field

from .state import EmailRecommendationState

import os
MODEL_NAME = os.getenv("GROQ_MODEL", "qwen/qwen3.8-27b")


class EmailRecommendationResult(BaseModel):
    subject: str
    body: str
    cc: list[str] = Field(default_factory=list)
    send_to: list[str] = Field(default_factory=list)
    rationale: str = ""


def _get_llm(request) -> ChatGroq:
    config = request.agent_config if isinstance(request.agent_config, dict) else {}
    kwargs = {
        "model": config.get("model") or MODEL_NAME,
        "temperature": config.get("temperature", 0.3),
    }

    if config.get("api_key"):
        kwargs["api_key"] = config["api_key"]

    if config.get("max_tokens"):
        kwargs["max_tokens"] = config["max_tokens"]

    return ChatGroq(**kwargs)


def generate_recommendation(state: EmailRecommendationState) -> dict:
    request = state["input"]
    recipients = [recipient.model_dump() for recipient in request.recipients]
    template = request.template.model_dump() if request.template else None

    system_prompt = (
        "You are TaskTune Agent 5, an internal email recommendation assistant.\n"
        "Create professional internal communications.\n"
        "Use the actual recipients and template provided.\n"
        "Keep the email practical, polished, and ready to send.\n"
        "Return only structured data."
    )
    user_prompt = (
        f"Sender:\n{json.dumps(request.sender.model_dump(), ensure_ascii=False, indent=2)}\n\n"
        f"Recipient type: {request.recipient_type}\n"
        f"Communication type: {request.communication_type}\n"
        f"Recipients:\n{json.dumps(recipients, ensure_ascii=False, indent=2)}\n\n"
        f"Selected template:\n{json.dumps(template, ensure_ascii=False, indent=2)}\n\n"
        f"Current subject:\n{request.current_subject}\n\n"
        f"Current body:\n{request.current_body}\n\n"
        f"User instructions:\n{request.prompt}\n\n"
        "Return subject, body, cc, send_to, and a short rationale."
    )

    llm = _get_llm(request).with_structured_output(EmailRecommendationResult)
    result: EmailRecommendationResult = llm.invoke([
        SystemMessage(content=system_prompt),
        HumanMessage(content=user_prompt),
    ])

    send_to = result.send_to or [recipient.email for recipient in request.recipients if recipient.email]
    cc = [email for email in result.cc if email and email not in send_to]

    return {
        "subject": result.subject.strip(),
        "body": result.body.strip(),
        "cc": cc,
        "send_to": send_to,
        "rationale": result.rationale.strip(),
    }
