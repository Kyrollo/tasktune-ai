from pydantic_settings import BaseSettings
from functools import lru_cache
from dotenv import load_dotenv
class Settings(BaseSettings):
    # Groq
    groq_api_key: str 

    # Database
    database_url: str = "sqlite+aiosqlite:///test_tasktune.db"

    # Email
    smtp_host: str = "smtp.gmail.com"
    smtp_port: int = 587
    smtp_user: str = ""
    smtp_password: str = ""
    email_from: str = "ya805817@tasktune.ai"

    # Agent settings
    ai_model: str = "llama-3.3-70b-versatile"
    max_tokens: int = 2000
    schedule_hour: int = 8
    schedule_minute: int = 0

    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"


@lru_cache
def get_settings() -> Settings:
    return Settings()