"""Provider-reported token usage helpers for LangChain message responses."""

from __future__ import annotations

from collections.abc import Mapping
from typing import Any


def extract_token_usage(raw_message: Any) -> dict[str, int] | None:
    """Extract actual provider usage; return None when the provider omitted it."""
    if raw_message is None:
        return None
    usage = getattr(raw_message, "usage_metadata", None)
    if not isinstance(usage, Mapping):
        metadata = getattr(raw_message, "response_metadata", None)
        if isinstance(metadata, Mapping):
            usage = metadata.get("token_usage") or metadata.get("usage")
    if not isinstance(usage, Mapping):
        return None

    def valid_count(value: Any) -> int | None:
        if isinstance(value, bool) or not isinstance(value, int) or value < 0:
            return None
        return value

    input_tokens = valid_count(usage.get("input_tokens", usage.get("prompt_tokens")))
    output_tokens = valid_count(usage.get("output_tokens", usage.get("completion_tokens")))
    total_tokens = valid_count(usage.get("total_tokens"))
    if input_tokens is None or output_tokens is None:
        return None
    if total_tokens is None:
        total_tokens = input_tokens + output_tokens
    return {
        "input_tokens": input_tokens,
        "output_tokens": output_tokens,
        "total_tokens": total_tokens,
    }


def merge_token_usage(
    current: Mapping[str, int] | None,
    incoming: Mapping[str, int] | None,
) -> dict[str, int]:
    """Sum token counts from multiple LLM calls."""
    if not current and not incoming:
        return {}
    current = current or {}
    incoming = incoming or {}
    keys = ("input_tokens", "output_tokens", "total_tokens")
    return {key: int(current.get(key, 0)) + int(incoming.get(key, 0)) for key in keys}


def invoke_structured_with_usage(llm: Any, schema: Any, prompt: Any) -> tuple[Any, dict[str, int] | None]:
    """Invoke structured output and retain the raw provider message."""
    response = llm.with_structured_output(schema, include_raw=True).invoke(prompt)
    if not isinstance(response, Mapping):
        raise TypeError("Structured LLM response did not contain raw/parsed fields.")
    parsed = response.get("parsed")
    if parsed is None:
        error = response.get("parsing_error")
        if error is not None:
            raise error
        raise ValueError("Structured LLM response could not be parsed.")
    return parsed, extract_token_usage(response.get("raw"))
