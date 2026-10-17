from __future__ import annotations

from .models import EmailRecommendationRequest
from .nodes import generate_recommendation


def run_email_recommendation(request: EmailRecommendationRequest) -> dict:
    state = {"input": request}
    return generate_recommendation(state)
