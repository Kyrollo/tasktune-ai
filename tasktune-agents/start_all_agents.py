"""
Start ALL TaskTune AI agents with a single command.

Usage:
    python start_all_agents.py

This launches each FastAPI agent in its own subprocess and keeps
them running until you press Ctrl+C.
"""

import subprocess
import sys
import signal
import os
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

# ── Agent definitions ──────────────────────────────────────────────────
AGENTS = [
    {
        "name": "Chatbot",
        "module": "agent1_chatbot.server:app",
        "port": 8001,
    },
    {
        "name": "Task Guidance",
        "module": "agent2_task_guidance.server:app",
        "port": 5050,
    },
    {
        "name": "Progress Tracking",
        "module": "agent3_progress_tracking.server:app",
        "port": 8003,
    },
    {
        "name": "Report Agent",
        "module": "agent4_Report_Agent.server:app",
        "port": 8004,
    },
    {
        "name": "Email Recommendation",
        "module": "agent5_email.server:app",
        "port": 8005,
    },
]

WORKDIR = Path(__file__).resolve().parent


def main():
    processes = []

    print("=" * 60)
    print("  🚀  TaskTune AI — Starting All Agents")
    print("=" * 60)

    for agent in AGENTS:
        cmd = [
            sys.executable, "-m", "uvicorn",
            agent["module"],
            "--host", "0.0.0.0",
            "--port", str(agent["port"]),
        ]
        print(f"\n  ▸ {agent['name']:20s}  →  http://localhost:{agent['port']}")
        print(f"    docs: http://localhost:{agent['port']}/docs")

        proc = subprocess.Popen(
            cmd,
            cwd=str(WORKDIR),
            # Let output flow to the same terminal
            stdout=sys.stdout,
            stderr=sys.stderr,
        )
        processes.append((agent["name"], proc))

    print("\n" + "=" * 60)
    print("  ✅  All agents started. Press Ctrl+C to stop all.")
    print("=" * 60 + "\n")

    # Wait for Ctrl+C, then terminate everything
    try:
        for _, proc in processes:
            proc.wait()
    except KeyboardInterrupt:
        print("\n\n  🛑  Shutting down all agents...")
        for name, proc in processes:
            proc.terminate()
        for name, proc in processes:
            proc.wait()
            print(f"    ✓ {name} stopped")
        print("  Done.\n")


if __name__ == "__main__":
    main()
