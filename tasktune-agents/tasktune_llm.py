"""Tenant-configured LLM adapter shared by the five TaskTune agents.

Provider API credentials stay in the ASP.NET configuration store and are sent
only over the authenticated API-to-agent channel. This module never invents a
mock response when a provider is unavailable.
"""
from __future__ import annotations

from typing import Any


def _as_dict(config: Any) -> dict[str, Any]:
    if config is None:
        return {}
    if isinstance(config, dict):
        return config
    dump = getattr(config, "model_dump", None)
    if callable(dump):
        return dump()
    return {}


def build_chat_model(
    config: Any,
    *,
    fallback_model: str,
    temperature: float = 0.2,
    max_tokens: int = 1024,
    fallback_api_key: str | None = None,
):
    """Build a real LangChain chat model for Groq, OpenAI, Anthropic or an OpenAI-compatible API."""
    values = _as_dict(config)
    provider = str(values.get("provider") or "groq").strip().lower()
    model = str(values.get("model") or fallback_model).strip()
    api_key = values.get("api_key") or (fallback_api_key if provider == "groq" else None)
    provider_base_url = str(values.get("provider_base_url") or "").strip() or None
    configured_temperature = values.get("temperature")
    configured_tokens = values.get("max_tokens")
    resolved_temperature = temperature if configured_temperature is None else float(configured_temperature)
    resolved_tokens = max_tokens if configured_tokens is None else int(configured_tokens)
    common = {"temperature": resolved_temperature, "max_tokens": resolved_tokens}

    if provider == "groq":
        from langchain_groq import ChatGroq
        kwargs = {**common, "model": model}
        if api_key:
            kwargs["api_key"] = api_key
        return ChatGroq(**kwargs)

    if provider == "openai":
        from langchain_openai import ChatOpenAI
        kwargs = {**common, "model": model}
        if api_key:
            kwargs["api_key"] = api_key
        if provider_base_url:
            kwargs["base_url"] = provider_base_url
        return ChatOpenAI(**kwargs)

    if provider == "anthropic":
        from langchain_anthropic import ChatAnthropic
        kwargs = {**common, "model": model}
        if api_key:
            kwargs["api_key"] = api_key
        if provider_base_url:
            kwargs["base_url"] = provider_base_url
        return ChatAnthropic(**kwargs)

    if provider in {"openai-compatible", "custom"}:
        if not provider_base_url:
            raise ValueError("provider_base_url is required for an OpenAI-compatible provider.")
        from langchain_openai import ChatOpenAI
        kwargs = {**common, "model": model, "base_url": provider_base_url}
        if api_key:
            kwargs["api_key"] = api_key
        return ChatOpenAI(**kwargs)

    raise ValueError(
        f"Unsupported AI provider '{provider}'. Use groq, openai, anthropic, or openai-compatible."
    )
