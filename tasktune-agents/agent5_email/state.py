from __future__ import annotations

from typing import TypedDict

from .models import EmailRecommendationRequest


class EmailRecommendationState(TypedDict, total=False):
    input: EmailRecommendationRequest
    subject: str
    body: str
    cc: list[str]
    send_to: list[str]
    rationale: str
