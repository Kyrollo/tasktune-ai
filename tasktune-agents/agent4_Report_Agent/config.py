from pydantic_settings import BaseSettings
from functools import lru_cache
from dotenv import load_dotenv


class Settings(BaseSettings):
    # Groq
    groq_api_key: str

    # Email
    smtp_host: str = "smtp.gmail.com"
    smtp_port: int = 587
    smtp_user: str = ""
    smtp_password: str = ""
    email_from: str = "ya805817@tasktune.ai"

    # Agent settings
    ai_model: str = "qwen/qwen3.8-27b"
    max_tokens: int = 2000

    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"


@lru_cache
def get_settings() -> Settings:
    return Settings()