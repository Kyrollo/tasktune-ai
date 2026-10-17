from langgraph.graph import StateGraph, END
from models.state import ProgressReportState
from agents.nodes.fetch_data   import fetch_data
from agents.nodes.validate     import validate_data, should_continue
from agents.nodes.analyze      import analyze_with_ai
from agents.nodes.build_report import build_report
from agents.email_agent        import build_email_subgraph


# ── Error node ────────────────────────────────────────────────────────────────

async def log_error(state: ProgressReportState) -> ProgressReportState:
    """
    Terminal node: called when validation fails.
    """
    print(f"🚨 [log_error] Flow stopped — reason: {state.get('error')}")
    return state


# ── In-app notification node ──────────────────────────────────────────────────

async def send_in_app_notification(state: ProgressReportState) -> ProgressReportState:
    """
    Node: Save a notification inside TaskTune for the manager.
    """
    print("🔔 [notification] Sending in-app notification...")

    try:
        from services.db_service import save_notification

        report = state["report"]
        health = state["analysis"].overall_health.upper()

        await save_notification(
            user_id    = state["manager_id"],
            title      = f"Daily Progress Report — {report.run_date}",
            message    = f"Overall health: {health} | {report.total_tasks} tasks | avg {report.avg_completion}%",
            action_url = "/dashboards/executive",
        )

        print("✅ [notification] In-app notification sent")
        return state

    except Exception as e:
        print(f"⚠️  [notification] Warning: {e}")
        return state


# ── Save report node ──────────────────────────────────────────────────────────

async def save_report(state: ProgressReportState) -> ProgressReportState:
    """
    Node: Save the final report to the reports table.
    """
    print("💾 [save_report] Saving report to database...")

    try:
        from services.db_service import save_report_to_db
        from datetime import date

        report_id = await save_report_to_db(
            company_id          = state["company_id"],
            manager_id          = state["manager_id"],
            run_date            = date.today(),
            ai_insights         = state["analysis"].summary,
            report_period_start = date.today(),
            report_period_end   = date.today(),
        )

        print(f"✅ [save_report] Report saved — ID: {report_id}")
        return state

    except Exception as e:
        print(f"⚠️  [save_report] Warning: {e}")
        return state


# ── Build the main graph ──────────────────────────────────────────────────────

def build_progress_agent():
    """
    Builds and returns the full Progress Agent graph.
    """
    graph = StateGraph(ProgressReportState)

    # ── Add nodes ──
    graph.add_node("fetch_data",fetch_data)
    graph.add_node("validate_data",validate_data)
    graph.add_node("analyze_with_ai",analyze_with_ai)
    graph.add_node("build_report",build_report)
    graph.add_node("email_agent",build_email_subgraph())
    graph.add_node("send_in_app_notification",send_in_app_notification)
    graph.add_node("save_report",save_report)
    graph.add_node("log_error",log_error)

    # ── Entry point ──
    graph.set_entry_point("fetch_data")

    # ── Edges ──
    graph.add_edge("fetch_data", "validate_data")

    # Conditional: valid → continue | invalid → log_error
    graph.add_conditional_edges(
        "validate_data",
        should_continue,
        {
            "analyze_with_ai": "analyze_with_ai",
            "log_error":"log_error",
        }
    )

    graph.add_edge("analyze_with_ai","build_report")
    graph.add_edge("build_report","email_agent")
    graph.add_edge("email_agent","send_in_app_notification")
    graph.add_edge("send_in_app_notification","save_report")
    graph.add_edge("save_report",END)
    graph.add_edge("log_error",END)

    return graph.compile()