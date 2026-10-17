from __future__ import annotations

from .models import ChatbotRequest
from .nodes import generate_reply


def run_chatbot(request: ChatbotRequest) -> dict[str, str]:
    state = {"input": request}
    return generate_reply(state)
