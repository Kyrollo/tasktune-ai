from __future__ import annotations

from typing import TypedDict

from .models import ChatbotRequest


class ChatbotState(TypedDict, total=False):
    input: ChatbotRequest
    reply: str
