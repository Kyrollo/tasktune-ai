"""Entry point for subprocess invocation: python -m agent3_progress_tracking

The C# backend sends progress data JSON on stdin and reads the agent's
final_output JSON from stdout.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

from dotenv import load_dotenv

# Load .env from the tasktune-agents/ directory (parent of agent3_progress_tracking/)
_agents_dir = Path(__file__).resolve().parent.parent
load_dotenv(_agents_dir / ".env")

# Fix import path so agent3_progress_tracking package is found
if str(_agents_dir) not in sys.path:
    sys.path.insert(0, str(_agents_dir))

from agent3_progress_tracking.graph import run_progress_tracking


def main() -> None:
    raw = sys.stdin.read()
    if not raw.strip():
        json.dump({"error": "No input received"}, sys.stdout)
        sys.exit(1)

    try:
        task_input = json.loads(raw)
    except json.JSONDecodeError as exc:
        json.dump({"error": f"Invalid JSON: {exc}"}, sys.stdout)
        sys.exit(1)

    result = run_progress_tracking(task_input)
    json.dump(result, sys.stdout, ensure_ascii=False)


if __name__ == "__main__":
    main()
