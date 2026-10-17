# TaskTune AI — Agent 3: Progress Tracking

> Analyzes task progress data and delivers predictive insights: status assessment, completion‑date prediction, risk analysis, and actionable recommendations.

---

## Features

| Capability | Description |
|---|---|
| **Progress Analysis** | Classifies tasks as *On track*, *At risk*, or *Delayed* |
| **Completion Prediction** | Estimates the most likely finish date (YYYY‑MM‑DD) |
| **Risk Assessment** | Rates risk as *low / medium / high* with a detailed narrative |
| **Recommendations** | Generates 2–4 concrete, actionable next steps |
| **Bilingual Support** | Responds in the same language as the input — **English** and **Arabic** |

---

## Tech Stack

| Layer | Technology |
|---|---|
| LLM | Groq LLaMA 3.3 70B (`llama-3.3-70b-versatile`) via `langchain-groq` |
| Orchestration | LangGraph (state‑graph workflow) |
| Prompts | LangChain Core prompt templates |
| Validation | Pydantic v2 |
| API | FastAPI + Uvicorn |
| Config | python‑dotenv |

---

## Prerequisites

- **Python 3.10+**
- A **Groq** API key from [console.groq.com](https://console.groq.com/keys)

---

## Setup

```bash
# 1. Navigate to the agents root
cd tasktune-agents

# 2. Create & activate a virtual environment
python -m venv .venv
source .venv/bin/activate        # Linux / macOS
# .venv\Scripts\activate         # Windows

# 3. Install dependencies
pip install -r agent3_progress_tracking/requirements.txt

# 4. Configure environment
cp agent3_progress_tracking/.env.example agent3_progress_tracking/.env
# Edit .env and set your GROQ_API_KEY
```

---

## Usage

### Run the Demo

```bash
cd tasktune-agents
python -m agent3_progress_tracking.demo
```

The demo runs two examples (English + Arabic) and prints the JSON results.

### Start the API Server

```bash
cd tasktune-agents
uvicorn agent3_progress_tracking.main:app --reload --port 8003
```

Interactive docs are available at **http://localhost:8003/docs**.

### Example API Call

```bash
curl -X POST http://localhost:8003/api/ai/progress-tracking \
  -H "Content-Type: application/json" \
  -d '{
    "task_id": "TASK-301",
    "task_title": "Implement User Dashboard",
    "task_description": "Build the main dashboard with charts.",
    "current_completion_percentage": 45,
    "start_date": "2026-03-01",
    "due_date": "2026-04-15",
    "progress_updates": [
      {
        "submitted_at": "2026-03-10T10:00:00Z",
        "completion_percentage": 20,
        "notes": "Base components done.",
        "blockers": ""
      }
    ],
    "current_blockers": ["Client feedback pending"],
    "user_performance_history": []
  }'
```

### Example Response

```json
{
  "progress_summary": "At risk",
  "completion_prediction": "2026-04-22",
  "blockers_analysis": "The pending client feedback creates a dependency that could delay the chart design finalization...",
  "recommendations": [
    "Schedule a follow-up meeting with the client to obtain feedback within 2 days.",
    "Proceed with a default chart design that can be adjusted later.",
    "Allocate a buffer of 3 extra days in the project plan."
  ],
  "risk_level": "medium"
}
```

---

## Project Structure

```text
agent3_progress_tracking/
├── __init__.py          # Package exports
├── demo.py              # Smoke‑test with English + Arabic examples
├── graph.py             # LangGraph state‑graph definition
├── main.py              # FastAPI application
├── models.py            # Pydantic request / response models
├── nodes.py             # Individual LangGraph node functions
├── prompts.py           # LangChain prompt templates
├── state.py             # TypedDict state definitions
├── .env.example         # Environment variable template
├── .gitignore           # Git ignore rules
├── requirements.txt     # Python dependencies
└── README.md            # This file
```

---

## Configuration

| Variable | Default | Description |
|---|---|---|
| `GROQ_API_KEY` | *(required)* | Your Groq API key |
| `GROQ_MODEL` | `llama-3.3-70b-versatile` | Override the Groq model name |

---

## Arabic / Multilingual Support

The agent **automatically detects** the language of the input (task title, description, progress notes, blocker text) and responds in the **same language** for all free‑text fields.

Structured enum fields are always in English for machine readability:

| Field | Values |
|---|---|
| `progress_summary` | `On track` · `At risk` · `Delayed` |
| `risk_level` | `low` · `medium` · `high` |

All JSON serialization uses `ensure_ascii=False` to properly encode Arabic (and other non‑ASCII) characters.

---

## Error Handling

Every LangGraph node is wrapped in a `try / except` block:

- **Malformed LLM response** → the node logs the error and returns safe defaults (`progress_summary: "At risk"`, `risk_level: "medium"`).
- **Individual node failure** does **not** crash the pipeline — downstream nodes receive default values and continue.

---

## Testing

You can test individual nodes by mocking the LLM:

```python
from unittest.mock import AsyncMock, patch
import pytest

@pytest.mark.asyncio
async def test_analyze_progress_on_track():
    mock_response = AsyncMock()
    mock_response.content = '{"progress_summary": "On track", "reason": "Good velocity."}'

    with patch("agent3_progress_tracking.nodes.llm") as mock_llm:
        mock_chain = AsyncMock()
        mock_chain.ainvoke.return_value = mock_response
        mock_llm.__or__ = lambda self, other: mock_chain

        from agent3_progress_tracking.nodes import analyze_progress
        state = {"input": {
            "task_id": "T1",
            "current_completion_percentage": 80,
            "due_date": "2026-05-01",
        }, "messages": []}

        result = await analyze_progress(state)
        assert result["progress_summary"] == "On track"
```

---

## Integration with TaskTune Backend

The ASP.NET Core backend calls this agent via HTTP:

```
POST http://<agent-host>:8003/api/ai/progress-tracking
Content-Type: application/json
```

In production, place the agent behind a reverse proxy or use Docker Compose networking to connect it with the main backend.

---