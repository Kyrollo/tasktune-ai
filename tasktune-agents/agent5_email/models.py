from __future__ import annotations

from pydantic import BaseModel, Field


class EmailRecipient(BaseModel):
    userId: int | None = None
    name: str
    email: str = ""
    department: str = "Unassigned"


class EmailTemplate(BaseModel):
    key: str
    label: str
    subject: str
    body: str


class EmailSender(BaseModel):
    name: str
    email: str = ""
    role: int | str
    department: str = "Unassigned"


class EmailRecommendationRequest(BaseModel):
    communication_type: str = "Email"
    recipient_type: str = "Individual"
    template: EmailTemplate | None = None
    current_subject: str = ""
    current_body: str = ""
    prompt: str = ""
    sender: EmailSender
    recipients: list[EmailRecipient] = Field(default_factory=list)
    agent_config: dict = Field(default_factory=dict)


class EmailRecommendationResponse(BaseModel):
    subject: str
    body: str
    cc: list[str] = Field(default_factory=list)
    send_to: list[str] = Field(default_factory=list)
    rationale: str = ""
