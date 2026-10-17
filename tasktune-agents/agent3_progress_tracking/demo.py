#!/usr/bin/env python3
"""Interactive demo for the Progress Tracking Agent (Agent 3).

Run from the tasktune-agents directory:
    python -m agent3_progress_tracking.demo              # uses pre-built sample tasks
    python -m agent3_progress_tracking.demo --custom     # enter your own task details

Or run directly:
    cd agent3_progress_tracking
    python demo.py

Requires:
    GROQ_API_KEY set in environment or in a .env file in the tasktune-agents/ dir.
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import textwrap
from pathlib import Path

from dotenv import load_dotenv

# Load .env from the tasktune-agents/ parent directory
_agents_dir = Path(__file__).resolve().parent.parent
load_dotenv(_agents_dir / ".env")

# Allow running this file directly (python demo.py) from inside the package
if __package__ is None or __package__ == "":
    sys.path.insert(0, str(_agents_dir))

from agent3_progress_tracking.graph import run_progress_tracking

# ── Sample Tasks ─────────────────────────────────────────────────────────

SAMPLE_TASKS = [
    {
        "label": "English — Implement User Dashboard",
        "task_id": "TASK-301",
        "task_title": "Implement User Dashboard",
        "task_description": "Build the main dashboard page with charts and KPI cards.",
        "current_completion_percentage": 45,
        "start_date": "2026-03-01",
        "due_date": "2026-04-15",
        "progress_updates": [
            {
                "submitted_at": "2026-03-10T10:00:00Z",
                "completion_percentage": 20,
                "notes": "Set up project structure and base components.",
                "blockers": "",
            },
            {
                "submitted_at": "2026-03-20T14:30:00Z",
                "completion_percentage": 45,
                "notes": "Charts integrated, KPI cards in progress.",
                "blockers": "Waiting for final KPI definitions from product team.",
            },
        ],
        "current_blockers": [
            "Client feedback pending on chart design",
            "KPI definitions not finalized",
        ],
        "user_performance_history": [
            {
                "evaluation_date": "2025-12-15",
                "average_score": 4.1,
                "on_time_delivery_rate": 0.82,
            }
        ],
    },
    {
        "label": "Arabic — تطوير صفحة تسجيل الدخول",
        "task_id": "TASK-302",
        "task_title": "تطوير صفحة تسجيل الدخول",
        "task_description": "بناء صفحة تسجيل الدخول مع التحقق من البيانات ودعم تعدد اللغات.",
        "current_completion_percentage": 30,
        "start_date": "2026-03-05",
        "due_date": "2026-04-10",
        "progress_updates": [
            {
                "submitted_at": "2026-03-12T09:00:00Z",
                "completion_percentage": 15,
                "notes": "تم إعداد هيكل الصفحة الأساسي.",
                "blockers": "",
            },
            {
                "submitted_at": "2026-03-22T11:00:00Z",
                "completion_percentage": 30,
                "notes": "تم إضافة نموذج تسجيل الدخول، بانتظار تصميم واجهة المستخدم النهائي.",
                "blockers": "التصميم النهائي لم يُعتمد بعد من فريق التصميم.",
            },
        ],
        "current_blockers": [
            "التصميم النهائي لم يُعتمد بعد",
            "مواصفات API للمصادقة غير مكتملة",
        ],
        "user_performance_history": [
            {
                "evaluation_date": "2025-11-20",
                "average_score": 3.8,
                "on_time_delivery_rate": 0.75,
            }
        ],
    },
    {
        "label": "Custom — Deploy ML Model to Production",
        "task_id": "CUSTOM-001",
        "task_title": "Deploy ML Model to Production",
        "task_description": "Containerize the model, set up API endpoint, and deploy to cloud",
        "current_completion_percentage": 75,
        "start_date": "2026-03-10",
        "due_date": "2026-03-28",
        "progress_updates": [
            {
                "submitted_at": "2026-03-12T08:00:00Z",
                "completion_percentage": 30,
                "notes": "Model containerization complete",
                "blockers": "",
            },
            {
                "submitted_at": "2026-03-18T15:00:00Z",
                "completion_percentage": 75,
                "notes": "API endpoints implemented, waiting for security review",
                "blockers": "Security review team unavailable this week",
            },
        ],
        "current_blockers": [
            "Security review delayed",
            "Cloud resource quota pending approval",
        ],
        "user_performance_history": [
            {
                "evaluation_date": "2026-02-01",
                "average_score": 4.5,
                "on_time_delivery_rate": 0.95,
            }
        ],
    },
]


# ── Display Helpers ──────────────────────────────────────────────────────

DIVIDER = "=" * 60


def print_header(text: str) -> None:
    print(f"\n{DIVIDER}")
    print(f"  {text}")
    print(DIVIDER)


def print_section(title: str, content: str | list[str]) -> None:
    print(f"\n>> {title}")
    if isinstance(content, list):
        for i, item in enumerate(content, 1):
            wrapped = textwrap.fill(item, width=70, subsequent_indent="     ")
            print(f"   {i}. {wrapped}")
    else:
        for line in textwrap.wrap(content, width=70):
            print(f"   {line}")


def display_result(result: dict) -> None:
    print_section("Progress Summary", result.get("progress_summary", "N/A"))
    print_section("Completion Prediction", result.get("completion_prediction") or "Not available")
    print_section("Risk Level", result.get("risk_level", "N/A").upper())
    print_section("Blockers Analysis", result.get("blockers_analysis", "N/A"))
    print_section("Recommendations", result.get("recommendations", []))

    print(f"\n{'─' * 60}")
    print("Raw JSON output:")
    print(json.dumps(result, indent=2, ensure_ascii=False))


# ── Custom Task Input ────────────────────────────────────────────────────

def get_custom_task() -> dict:
    print_header("Enter Custom Progress Data")

    task_id = input("\nTask ID: ").strip() or "CUSTOM-001"
    task_title = input("Task title: ").strip() or "My Custom Task"
    task_description = input("Description: ").strip() or "No description."
    completion = input("Current completion % [50]: ").strip() or "50"
    start_date = input("Start date (YYYY-MM-DD): ").strip() or "2026-03-01"
    due_date = input("Due date (YYYY-MM-DD): ").strip() or "2026-04-15"
    blockers = input("Blockers (comma-separated) []: ").strip()

    return {
        "task_id": task_id,
        "task_title": task_title,
        "task_description": task_description,
        "current_completion_percentage": int(completion),
        "start_date": start_date,
        "due_date": due_date,
        "progress_updates": [],
        "current_blockers": [b.strip() for b in blockers.split(",") if b.strip()] if blockers else [],
        "user_performance_history": [],
    }


# ── Main ─────────────────────────────────────────────────────────────────

def main() -> None:
    if not os.getenv("GROQ_API_KEY"):
        sys.exit(
            "ERROR: GROQ_API_KEY is not set.\n"
            "Get a free key from https://console.groq.com/keys\n"
            "Then: set GROQ_API_KEY=your-key  (Windows)\n"
            "  or: export GROQ_API_KEY=your-key  (Linux/Mac)"
        )

    parser = argparse.ArgumentParser(description="Progress Tracking Agent Demo")
    parser.add_argument(
        "--custom",
        action="store_true",
        help="Enter your own task details instead of using samples",
    )
    args = parser.parse_args()

    print_header("TaskTune AI — Agent 3: Progress Tracking Demo")
    print("  Model  : Groq LLaMA 3.3 70B (llama-3.3-70b-versatile)")
    print("  Stack  : LangChain + LangGraph")
    print("  Graph  : analyze -> predict -> risk -> recommend -> output")

    if args.custom:
        task_data = get_custom_task()
        tasks_to_run = [(task_data["task_title"], task_data)]
    else:
        print("\nAvailable sample tasks:")
        for i, t in enumerate(SAMPLE_TASKS, 1):
            print(f"  [{i}] {t['label']}")
        print("  [a] Run all samples")

        choice = input("\nSelect (1-3 or a) [a]: ").strip().lower() or "a"

        if choice == "a":
            tasks_to_run = [(t["label"], t) for t in SAMPLE_TASKS]
        elif choice in ("1", "2", "3"):
            idx = int(choice) - 1
            t = SAMPLE_TASKS[idx]
            tasks_to_run = [(t["label"], t)]
        else:
            print("Invalid choice, running all samples.")
            tasks_to_run = [(t["label"], t) for t in SAMPLE_TASKS]

    for label, task_data in tasks_to_run:
        clean_data = {k: v for k, v in task_data.items() if k != "label"}

        print_header(f"Running: {label}")
        print("Calling LangGraph pipeline...")

        try:
            result = run_progress_tracking(clean_data)
            display_result(result)
        except Exception as exc:
            print(f"\n  ERROR: {exc}")
            print("  Make sure your GROQ_API_KEY is valid and has access to llama-3.3-70b-versatile.")

    print_header("Demo Complete")


if __name__ == "__main__":
    main()