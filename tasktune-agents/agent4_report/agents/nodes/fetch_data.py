from models.state import ProgressReportState
from mock_data import get_mock_tasks


async def fetch_data(state: ProgressReportState) -> ProgressReportState:
    """
    Node 1: Fetch tasks data.
    In production, this will query the database.
    For now, it uses mock data.
    """
    print("📥 [fetch_data] Fetching tasks data...")

    try:
        tasks = get_mock_tasks()

        print(f"✅ [fetch_data] Successfully fetched {len(tasks)} tasks")
        return {**state, "tasks_data": tasks, "error": None}

    except Exception as e:
        print(f"❌ [fetch_data] Error: {e}")
        return {**state, "tasks_data": [], "error": str(e)}