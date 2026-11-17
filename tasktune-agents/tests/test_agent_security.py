from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from agent_security import is_valid_service_key


class AgentServiceKeyTests(unittest.TestCase):
    def test_accepts_matching_strong_key(self) -> None:
        key = "a" * 48
        self.assertTrue(is_valid_service_key(key, key))

    def test_rejects_missing_or_short_configured_key(self) -> None:
        self.assertFalse(is_valid_service_key("", ""))
        self.assertFalse(is_valid_service_key("short", "short"))

    def test_rejects_missing_or_wrong_supplied_key(self) -> None:
        key = "a" * 48
        self.assertFalse(is_valid_service_key(key, ""))
        self.assertFalse(is_valid_service_key(key, "b" * 48))


if __name__ == "__main__":
    unittest.main()
