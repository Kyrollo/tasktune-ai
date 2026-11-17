"""Dependency-free API key validation shared by agent middleware and tests."""

from __future__ import annotations

import hmac


def is_valid_service_key(expected: str, supplied: str) -> bool:
    """Require a strong configured key and compare without timing-sensitive equality."""
    return (
        len(expected.encode("utf-8")) >= 32
        and bool(supplied)
        and hmac.compare_digest(expected, supplied)
    )
