from __future__ import annotations

import unittest

from usage_metadata import extract_token_usage, merge_token_usage


class FakeMessage:
    def __init__(self, usage_metadata=None, response_metadata=None):
        self.usage_metadata = usage_metadata
        self.response_metadata = response_metadata or {}


class UsageMetadataTests(unittest.TestCase):
    def test_extracts_standard_usage_metadata(self):
        message = FakeMessage({"input_tokens": 120, "output_tokens": 40, "total_tokens": 160})
        self.assertEqual(
            extract_token_usage(message),
            {"input_tokens": 120, "output_tokens": 40, "total_tokens": 160},
        )

    def test_extracts_provider_response_metadata_fallback(self):
        message = FakeMessage(
            response_metadata={"token_usage": {"prompt_tokens": 50, "completion_tokens": 20, "total_tokens": 70}}
        )
        self.assertEqual(
            extract_token_usage(message),
            {"input_tokens": 50, "output_tokens": 20, "total_tokens": 70},
        )

    def test_missing_usage_stays_unreported(self):
        self.assertIsNone(extract_token_usage(FakeMessage()))
        self.assertIsNone(extract_token_usage(None))

    def test_merge_sums_multiple_calls(self):
        self.assertEqual(
            merge_token_usage(
                {"input_tokens": 10, "output_tokens": 5, "total_tokens": 15},
                {"input_tokens": 20, "output_tokens": 7, "total_tokens": 27},
            ),
            {"input_tokens": 30, "output_tokens": 12, "total_tokens": 42},
        )


if __name__ == "__main__":
    unittest.main()
