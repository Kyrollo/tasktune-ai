from agent4_Report_Agent.models.state import ProgressReportState


async def validate_data(state: ProgressReportState) -> ProgressReportState:
    """
    Node 2: Validate data before sending to AI.
    Checks that tasks exist and all required fields are present.
    """
    print("🔍 [validate_data] Validating data...")

    tasks = state.get("tasks_data", [])

    # Check 1: Are there any tasks?
    if not tasks:
        print("⚠️  [validate_data] No tasks found!")
        return {**state, "is_valid": False, "error": "No tasks found"}

    # Check 2: Does every task have the required fields?
    for task in tasks:
        if not task.employee_name:
            return {**state, "is_valid": False, "error": f"Task {task.task_id} is missing employee name"}
        if task.completion_percentage < 0 or task.completion_percentage > 100:
            return {**state, "is_valid": False, "error": f"Task {task.task_id} has invalid completion percentage"}

    # Check 3: Is the manager present?
    if not state.get("manager_id"):
        return {**state, "is_valid": False, "error": "No manager_id provided"}

    print(f"✅ [validate_data] Data is valid — {len(tasks)} tasks ready")
    return {**state, "is_valid": True, "error": None}


def should_continue(state: ProgressReportState) -> str:
    """
    Conditional edge: decide which node to go to next.
    Returns the name of the next node.
    """
    if state.get("is_valid"):
        return "analyze_with_ai"
    else:
        return "log_error"