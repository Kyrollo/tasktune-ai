from langchain_groq import ChatGroq
from langchain_core.messages import HumanMessage, SystemMessage
from agent4_Report_Agent.models.state import ProgressReportState
from agent4_Report_Agent.models.schemas import AIAnalysis, EmployeeSummary
from agent4_Report_Agent.config import get_settings
from datetime import date
import json
settings = get_settings()


def _get_llm(state: ProgressReportState) -> ChatGroq:
    config = state.get("agent_config", {}) or {}
    return ChatGroq(
        model=config.get("model") or settings.ai_model,
        api_key=config.get("api_key") or settings.groq_api_key,
        max_tokens=config.get("max_tokens") or settings.max_tokens,
        temperature=config.get("temperature", 0.2),
    )


def _build_employee_summaries(state: ProgressReportState) -> list[EmployeeSummary]:
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


def _build_prompt(state: ProgressReportState, summaries: list[EmployeeSummary]) -> str:
    today = date.today()
    tasks = state["tasks_data"]

    tasks_text = ""
    for t in tasks:
        overdue = f"OVERDUE by {(today - t.due_date).days} days" if t.due_date < today else f"due in {(t.due_date - today).days} days"
        blocker = f"| Blocker: {t.blockers}" if t.blockers else ""
        tasks_text += f"- [{t.completion_percentage}%] {t.employee_name} | {t.task_name} | {overdue} {blocker}\n"

    summary_text = ""
    for s in summaries:
        risk = "⚠️ AT RISK" if s.at_risk else "✅ OK"
        summary_text += f"- {s.employee_name}: avg {s.avg_completion}% | {s.late_tasks} late tasks | {risk}\n"

    return f"""
You are analyzing employee task progress for a manager's daily report.
Today: {today}
Report date: {state['run_date']}

EMPLOYEE SUMMARIES:
{summary_text}

TASK DETAILS:
{tasks_text}

Your job:
1. Write a clear executive summary (2-3 sentences)
2. List employees who are at risk (overdue or below 50% with deadline soon)
3. Predict when each at-risk employee will finish their tasks
4. Give 3 actionable recommendations for the manager

Respond ONLY with a valid JSON object in this exact format:
{{
    "summary": "...",
    "at_risk_employees": ["name1", "name2"],
    "predictions": {{
        "employee name": "expected completion date or note"
    }},
    "recommendations": ["action 1", "action 2", "action 3"],
    "overall_health": "good" or "warning" or "critical"
}}
"""


async def analyze_with_ai(state: ProgressReportState) -> ProgressReportState:
    print("🤖 [analyze_with_ai] Sending data to Groq for analysis...")

    try:
        summaries = _build_employee_summaries(state)
        prompt    = _build_prompt(state, summaries)

        messages = [
            SystemMessage(content="You are a professional project management assistant. Always respond with valid JSON only, no extra text."),
            HumanMessage(content=prompt),
        ]

        response = await _get_llm(state).ainvoke(messages)
        raw = response.content.strip()

        # Clean response if wrapped in ```json
        if raw.startswith("```"):
            raw = raw.split("```")[1]
            if raw.startswith("json"):
                raw = raw[4:]
        raw = raw.strip()

        data = json.loads(raw)

        analysis = AIAnalysis(
            summary           = data["summary"],
            at_risk_employees = data["at_risk_employees"],
            predictions       = data["predictions"],
            recommendations   = data["recommendations"],
            overall_health    = data["overall_health"],
        )

        print(f"✅ [analyze_with_ai] Analysis done — overall health: {analysis.overall_health}")
        print(f"   At risk: {analysis.at_risk_employees}")

        return {**state, "analysis": analysis, "error": None}

    except json.JSONDecodeError as e:
        print(f"❌ [analyze_with_ai] JSON parse error: {e}")
        return {**state, "error": f"AI response parse error: {e}"}

    except Exception as e:
        print(f"❌ [analyze_with_ai] Error: {e}")
        return {**state, "error": str(e)}
