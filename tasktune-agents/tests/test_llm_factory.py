import sys
import types
import unittest
from unittest.mock import patch

sys.path.insert(0, __import__("os").path.dirname(__file__) + "/..")
from tasktune_llm import build_chat_model


class LlmFactoryTests(unittest.TestCase):
    def test_groq_adapter_uses_tenant_model_and_key(self):
        fake = types.ModuleType("langchain_groq")
        fake.ChatGroq = lambda **kwargs: kwargs
        with patch.dict(sys.modules, {"langchain_groq": fake}):
            result = build_chat_model(
                {"provider": "groq", "model": "tenant-model", "api_key": "tenant-secret", "temperature": 0.7, "max_tokens": 333},
                fallback_model="fallback",
            )
        self.assertEqual(result["model"], "tenant-model")
        self.assertEqual(result["api_key"], "tenant-secret")
        self.assertEqual(result["temperature"], 0.7)
        self.assertEqual(result["max_tokens"], 333)

    def test_openai_compatible_provider_uses_custom_base_url(self):
        fake = types.ModuleType("langchain_openai")
        fake.ChatOpenAI = lambda **kwargs: kwargs
        with patch.dict(sys.modules, {"langchain_openai": fake}):
            result = build_chat_model(
                {"provider": "openai-compatible", "model": "local-model", "provider_base_url": "https://llm.example/v1", "api_key": "custom-key"},
                fallback_model="fallback",
            )
        self.assertEqual(result["base_url"], "https://llm.example/v1")
        self.assertEqual(result["model"], "local-model")
        self.assertEqual(result["api_key"], "custom-key")

    def test_openai_compatible_requires_base_url(self):
        with self.assertRaisesRegex(ValueError, "provider_base_url"):
            build_chat_model({"provider": "openai-compatible"}, fallback_model="fallback")

    def test_unsupported_provider_fails_closed(self):
        with self.assertRaisesRegex(ValueError, "Unsupported AI provider"):
            build_chat_model({"provider": "unknown"}, fallback_model="fallback")

    def test_empty_agent_config_uses_default_provider_and_model(self):
        fake = types.ModuleType("langchain_groq")
        fake.ChatGroq = lambda **kwargs: kwargs
        with patch.dict(sys.modules, {"langchain_groq": fake}):
            result = build_chat_model({}, fallback_model="default-model", temperature=0.1)
        self.assertEqual(result["model"], "default-model")
        self.assertEqual(result["temperature"], 0.1)


if __name__ == "__main__":
    unittest.main()
