from models.state import ProgressReportState
from models.schemas import ReportOutput, EmployeeSummary
from datetime import date


def _build_employee_summaries(state: ProgressReportState) -> list[EmployeeSummary]:
    """
    Group tasks by employee and calculate stats.
    """
    tasks = state["tasks_data"]
    today = date.today()

    employees: dict[str, list] = {}
    for task in tasks:
        emp_id = task.employee_id
        if emp_id not in employees:
            employees[emp_id] = []
        employees[emp_id].append(task)

    summaries = []
    for emp_id, emp_tasks in employees.items():
        avg_completion = sum(t.completion_percentage for t in emp_tasks) / len(emp_tasks)
        late_tasks     = sum(1 for t in emp_tasks if t.due_date < today)
        on_time_tasks  = len(emp_tasks) - late_tasks
        at_risk        = avg_completion < 50 or late_tasks > 0

        summaries.append(EmployeeSummary(
            employee_id    = emp_id,
            employee_name  = emp_tasks[0].employee_name,
            total_tasks    = len(emp_tasks),
            avg_completion = round(avg_completion, 1),
            on_time_tasks  = on_time_tasks,
            late_tasks     = late_tasks,
            at_risk        = at_risk,
        ))

    return summaries


async def build_report(state: ProgressReportState) -> ProgressReportState:
    """
    Node 4: Build the final report object
    from tasks data + AI analysis.
    """
    print("📝 [build_report] Building final report...")

    try:
        tasks    = state["tasks_data"]
        analysis = state["analysis"]

        summaries = _build_employee_summaries(state)

        total_tasks     = len(tasks)
        total_employees = len(summaries)
        avg_completion  = round(
            sum(t.completion_percentage for t in tasks) / total_tasks, 1
        )

        report = ReportOutput(
            title             = f"Daily Progress Report — {state['run_date']}",
            generated_at      = str(date.today()),
            run_date          = state["run_date"],
            total_tasks       = total_tasks,
            total_employees   = total_employees,
            avg_completion    = avg_completion,
            employees_summary = summaries,
            ai_analysis       = analysis,
        )

        print(f"✅ [build_report] Report built successfully")
        print(f"   Tasks: {total_tasks} | Employees: {total_employees} | Avg: {avg_completion}%")

        return {**state, "report": report, "error": None}

    except Exception as e:
        print(f"❌ [build_report] Error: {e}")
        return {**state, "error": str(e)}