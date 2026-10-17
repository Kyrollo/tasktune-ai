#!/usr/bin/env python3
"""Interactive demo for the Task Guidance Agent (Agent 2).

Run from the tasktune-agents directory:
    python -m agent2_task_guidance.demo              # uses pre-built sample tasks
    python -m agent2_task_guidance.demo --custom     # enter your own task details

Or run directly:
    cd agent2_task_guidance
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

from agent2_task_guidance.graph import run_task_guidance

# ── Sample Tasks ─────────────────────────────────────────────────────────

SAMPLE_TASKS = [
    {
        "label": "Simple — Fix login button styling",
        "task_name": "Fix Login Button Styling",
        "description": (
            "The login button on the landing page has incorrect padding "
            "and the hover state color doesn't match the design system. "
            "Update the Tailwind classes to match the Figma spec."
        ),
        "subtasks": [
            {"name": "Update button padding", "status": "NotStarted"},
            {"name": "Fix hover color", "status": "NotStarted"},
        ],
        "definition_of_done": "Button matches Figma design on desktop and mobile.",
        "due_date": "2026-03-20",
        "dependencies": [],
        "industry": "Technology",
        "user_role": "Employee",
        "completion_percentage": 0,
    },
    {
        "label": "Complex — Build real-time notification system",
        "task_name": "Implement Real-time Notification System",
        "description": (
            "Design and implement a real-time notification system using "
            "SignalR. Notifications should push to all connected clients "
            "when tasks are assigned, deadlines approach, or evaluations "
            "are submitted. Include a notification bell with unread count "
            "badge, a dropdown list, and mark-as-read functionality."
        ),
        "subtasks": [
            {"name": "Set up SignalR hub on backend", "status": "NotStarted"},
            {"name": "Create NotificationService", "status": "NotStarted"},
            {"name": "Build frontend SignalR client hook", "status": "NotStarted"},
            {"name": "Implement notification bell component", "status": "NotStarted"},
            {"name": "Add unread count badge", "status": "NotStarted"},
            {"name": "Build notification dropdown list", "status": "NotStarted"},
            {"name": "Add mark-as-read API + UI", "status": "NotStarted"},
            {"name": "Write integration tests", "status": "NotStarted"},
        ],
        "definition_of_done": (
            "Real-time push works for task assignment, deadline reminders, "
            "and evaluation events. Bell icon shows unread count. Users can "
            "view and dismiss notifications. Integration tests pass."
        ),
        "due_date": "2026-03-17",
        "dependencies": [
            {"task": "Backend Auth System", "status": "Completed"},
            {"task": "Task CRUD API", "status": "InProgress"},
        ],
        "industry": "Technology",
        "user_role": "Employee",
        "completion_percentage": 10,
    },
    {
        "label": "Medium — Create quarterly performance report template",
        "task_name": "Design Quarterly Performance Report Template",
        "description": (
            "Create a reusable quarterly performance report template for "
            "department managers. The report should include KPI summaries, "
            "team member evaluations overview, project completion rates, "
            "and AI-generated insights. Output as PDF via QuestPDF."
        ),
        "subtasks": [
            {"name": "Define report data schema", "status": "NotStarted"},
            {"name": "Design PDF layout in QuestPDF", "status": "NotStarted"},
            {"name": "Build chart rendering for KPIs", "status": "NotStarted"},
            {"name": "Integrate AI insights section", "status": "NotStarted"},
            {"name": "Add report generation API endpoint", "status": "NotStarted"},
        ],
        "definition_of_done": (
            "Manager can generate a PDF report from the Reports page that "
            "includes KPIs, team evaluations, project stats, and AI insights."
        ),
        "due_date": "2026-03-28",
        "dependencies": [
            {"task": "Evaluation API", "status": "Completed"},
            {"task": "Dashboard KPI Service", "status": "InProgress"},
        ],
        "industry": "Technology",
        "user_role": "Manager",
        "completion_percentage": 0,
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


def display_breakdown(breakdown: list[dict]) -> None:
    """Pretty-print the nested task breakdown."""
    print(f"\n>> Task Breakdown")
    for i, item in enumerate(breakdown, 1):
        priority_tag = f"[{item.get('priority', 'medium').upper()}]"
        print(f"   {i}. {priority_tag} {item['task']}")
        for j, sub in enumerate(item.get("subtasks", []), 1):
            print(f"      {i}.{j} {sub}")


def display_success_criteria(criteria: dict) -> None:
    """Pretty-print the success criteria section."""
    print(f"\n>> Success Criteria")

    deliverables = criteria.get("deliverables", [])
    if deliverables:
        print("   Deliverables:")
        for i, d in enumerate(deliverables, 1):
            wrapped = textwrap.fill(d, width=66, subsequent_indent="        ")
            print(f"     {i}. {wrapped}")

    quality = criteria.get("quality_indicators", [])
    if quality:
        print("   Quality Indicators:")
        for i, q in enumerate(quality, 1):
            wrapped = textwrap.fill(q, width=66, subsequent_indent="        ")
            print(f"     {i}. {wrapped}")

    checklist = criteria.get("completion_checklist", [])
    if checklist:
        print("   Completion Checklist:")
        for i, c in enumerate(checklist, 1):
            wrapped = textwrap.fill(c, width=66, subsequent_indent="        ")
            print(f"     {i}. {wrapped}")


def display_result(result: dict) -> None:
    print_section("Guidance", result["guidance_text"])
    print_section("Suggested Steps", result["suggested_subtasks"])
    display_breakdown(result.get("task_breakdown", []))
    print_section("Estimated Time", result["estimated_time"])
    print_section("Resources", result["resources"])
    print_section("Risk Factors", result["risk_factors"])
    display_success_criteria(result.get("success_criteria", {}))

    print(f"\n{'─' * 60}")
    print("Raw JSON output:")
    print(json.dumps(result, indent=2, ensure_ascii=False))


# ── Custom Task Input ────────────────────────────────────────────────────

def get_custom_task() -> dict:
    print_header("Enter Custom Task Details")

    task_name = input("\nTask name: ").strip() or "My Custom Task"
    description = input("Description: ").strip() or "No description."
    definition = input("Definition of Done: ").strip() or "Task is done."
    due_date = input("Due date (YYYY-MM-DD): ").strip() or "2026-03-25"
    industry = input("Industry [Technology]: ").strip() or "Technology"
    role = input("Your role [Employee]: ").strip() or "Employee"
    completion = input("Current completion % [0]: ").strip() or "0"

    num_subtasks = input("Number of subtasks [0]: ").strip() or "0"
    subtasks = []
    for i in range(int(num_subtasks)):
        name = input(f"  Subtask {i + 1} name: ").strip()
        if name:
            subtasks.append({"name": name, "status": "NotStarted"})

    return {
        "task_name": task_name,
        "description": description,
        "subtasks": subtasks,
        "definition_of_done": definition,
        "due_date": due_date,
        "dependencies": [],
        "industry": industry,
        "user_role": role,
        "completion_percentage": int(completion),
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

    parser = argparse.ArgumentParser(description="Task Guidance Agent Demo")
    parser.add_argument(
        "--custom",
        action="store_true",
        help="Enter your own task instead of using samples",
    )
    args = parser.parse_args()

    print_header("TaskTune AI — Agent 2: Task Guidance Demo")
    print("  Model  : Groq LLaMA 3.3 70B (llama-3.3-70b-versatile)")
    print("  Stack  : LangChain + LangGraph")
    print("  Graph  : analyze -> [simple|complex] guidance -> risks -> output")

    if args.custom:
        task_data = get_custom_task()
        tasks_to_run = [(task_data["task_name"], task_data)]
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
            result = run_task_guidance(clean_data)
            display_result(result)
        except Exception as exc:
            print(f"\n  ERROR: {exc}")
            print("  Make sure your GROQ_API_KEY is valid and has access to llama-3.3-70b-versatile.")

    print_header("Demo Complete")


if __name__ == "__main__":
    main()
