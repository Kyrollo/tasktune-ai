"""
Fetch Data node — data is already provided by the C# backend.

In the Agent 2/3 pattern, the C# backend gathers all data from the DB
and sends it in the POST request. So this node simply validates that
the data was received correctly and logs it.
"""

from agent4_Report_Agent.models.state import ProgressReportState


async def fetch_data(state: ProgressReportState) -> ProgressReportState:
    """
    Node 1: Validate that task data was provided by the C# backend.
    The C# backend pre-populates tasks_data, manager info, etc. in the
    initial state before the pipeline runs.
    """
    tasks = state.get("tasks_data", [])
    manager = state.get("manager_name", "Unknown")
    company = state.get("company_id", "?")

    print(f"📥 [fetch_data] Received {len(tasks)} tasks from backend")
    print(f"   Company: {company} | Manager: {manager}")

    if not tasks:
        print("⚠️  [fetch_data] No tasks received from backend!")
        return {**state, "error": "No task data received from the backend."}

    return {**state, "error": None}