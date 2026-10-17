from __future__ import annotations

from typing import Any

from pydantic import BaseModel, ConfigDict, Field


class ChatbotUser(BaseModel):
    id: int
    name: str
    email: str = ""
    role: str = "Employee"
    department: str = "Unassigned"
    position: str = "N/A"


class ChatbotThread(BaseModel):
    id: int
    title: str = "New Chat"
    related_task_id: int | None = None
    related_project_id: int | None = None


class ChatbotHistoryItem(BaseModel):
    role: str
    content: str
    sent_at: str | None = None


class ChatbotAgentConfig(BaseModel):
    provider: str = "groq"
    model: str = "llama-3.3-70b-versatile"
    api_key: str | None = None
    temperature: float | None = None
    max_tokens: int | None = None


class ChatbotSqlContext(BaseModel):
    phase: str | None = None
    view_schema: str | None = Field(default=None, alias="viewSchema")
    role_rules: str | None = Field(default=None, alias="roleRules")
    generated_sql: str | None = Field(default=None, alias="generatedSql")
    executed_sql: str | None = Field(default=None, alias="executedSql")
    sql_error: str | None = Field(default=None, alias="sqlError")
    rows: list[dict[str, Any]] = Field(default_factory=list)

    model_config = ConfigDict(populate_by_name=True)


class ChatbotRequest(BaseModel):
    message: str
    user: ChatbotUser
    thread: ChatbotThread
    history: list[ChatbotHistoryItem] = Field(default_factory=list)
    scope: dict[str, Any] = Field(default_factory=dict)
    focus: dict[str, Any] = Field(default_factory=dict)
    available_data: dict[str, Any] = Field(default_factory=dict)
    sql_context: ChatbotSqlContext | None = None
    agent_config: ChatbotAgentConfig | None = None


class ChatbotResponse(BaseModel):
    reply: str
    sql_query: str | None = Field(default=None, alias="sqlQuery")
    mode_used: str | None = Field(default=None, alias="modeUsed")

    model_config = ConfigDict(populate_by_name=True)
