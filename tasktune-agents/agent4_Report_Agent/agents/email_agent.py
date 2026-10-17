from langchain_groq import ChatGroq
from langchain_core.messages import HumanMessage, SystemMessage
from langgraph.graph import StateGraph, END
from agent4_Report_Agent.models.state import ProgressReportState
from agent4_Report_Agent.config import get_settings
import smtplib
from email.mime.text import MIMEText
from email.mime.multipart import MIMEMultipart

settings = get_settings()


def _get_llm(state: ProgressReportState) -> ChatGroq:
    config = state.get("agent_config", {}) or {}
    return ChatGroq(
        model=config.get("model") or settings.ai_model,
        api_key=config.get("api_key") or settings.groq_api_key,
        max_tokens=config.get("max_tokens") or settings.max_tokens,
        temperature=config.get("temperature", 0.3),
    )


# ── Node 1: Generate email content with AI ────────────────────────────────────

async def generate_email_content(state: ProgressReportState) -> ProgressReportState:
    """
    Node 1: Use Groq to write a professional email
    based on the report data.
    """
    print("✍️  [email_agent] Generating email content...")

    try:
        report   = state.get("report")
        analysis = state.get("analysis")

        if not report or not analysis:
            print("⚠️  [email_agent] No report/analysis data — skipping email generation")
            return {**state, "email_subject": "Report Generation Failed", "email_body": "Report data was not available."}

        # Build employees details for the prompt
        employees_text = ""
        for emp in report.employees_summary:
            status = "⚠️ AT RISK" if emp.at_risk else "✅ OK"
            employees_text += (
                f"- {emp.employee_name}: {emp.avg_completion}% avg completion | "
                f"{emp.late_tasks} late tasks | {status}\n"
            )

        prompt = f"""
Write a professional daily progress report email from the system to the manager.

Report Data:
- Date: {report.run_date}
- Total Tasks: {report.total_tasks}
- Total Employees: {report.total_employees}
- Average Completion: {report.avg_completion}%
- Overall Health: {analysis.overall_health.upper()}

Employee Status:
{employees_text}

AI Summary:
{analysis.summary}

At Risk Employees: {', '.join(analysis.at_risk_employees) if analysis.at_risk_employees else 'None'}

Recommendations:
{chr(10).join(f'- {r}' for r in analysis.recommendations)}

Write the email with:
1. A clear subject line
2. Professional greeting
3. Quick summary paragraph
4. Employee status table (text format)
5. Recommendations section
6. Professional closing

Respond ONLY in this exact format:
SUBJECT: <subject here>
BODY:
<full email body here>
"""

        messages = [
            SystemMessage(content="You are a professional business communication assistant. Write clear, concise emails."),
            HumanMessage(content=prompt),
        ]

        response = await _get_llm(state).ainvoke(messages)
        raw = response.content.strip()

        # Parse subject and body
        lines        = raw.split("\n")
        subject_line = next((l for l in lines if l.startswith("SUBJECT:")), None)
        subject      = subject_line.replace("SUBJECT:", "").strip() if subject_line else f"Daily Progress Report — {report.run_date}"

        body_start = raw.find("BODY:")
        body       = raw[body_start + 5:].strip() if body_start != -1 else raw

        print(f"✅ [email_agent] Email content generated")
        print(f"   Subject: {subject}")

        return {**state, "email_subject": subject, "email_body": body}

    except Exception as e:
        print(f"❌ [email_agent] Generate error: {e}")
        return {**state, "error": str(e)}


# ── Node 2: Send the email ─────────────────────────────────────────────────────

async def send_email(state: ProgressReportState) -> ProgressReportState:
    """
    Node 2: Send the generated email to the manager.
    Gracefully skips if SMTP credentials are not configured.
    """
    # Skip if SMTP credentials are not configured
    if not settings.smtp_user or not settings.smtp_password:
        print("⚠️  [email_agent] SMTP credentials not configured — skipping email send")
        return {**state, "email_sent": False}

    print(f"📧 [email_agent] Sending email to {state['manager_email']}...")

    try:
        msg = MIMEMultipart("alternative")
        msg["Subject"] = state["email_subject"]
        msg["From"]    = settings.email_from
        msg["To"]      = state["manager_email"]

        body_part = MIMEText(state["email_body"], "plain", "utf-8")
        msg.attach(body_part)

        with smtplib.SMTP(settings.smtp_host, settings.smtp_port) as server:
            server.starttls()
            server.login(settings.smtp_user, settings.smtp_password)
            server.sendmail(
                settings.email_from,
                [state["manager_email"]],
                msg.as_bytes(),
            )

        print(f"✅ [email_agent] Email sent successfully")
        return {**state, "email_sent": True}

    except Exception as e:
        print(f"❌ [email_agent] Send error: {e}")
        return {**state, "email_sent": False, "error": str(e)}
# ── Node 3: Log the communication ─────────────────────────────────────────────

async def log_communication(state: ProgressReportState) -> ProgressReportState:
    """
    Node 3: Save email log to communication_log table.
    """
    print("💾 [email_agent] Logging communication...")

    try:
        from agent4_Report_Agent.services.db_service import save_communication_log

        await save_communication_log(
            company_id   = state["company_id"],
            sender_id    = state["manager_id"],
            recipient_id = state["manager_id"],
            subject      = state["email_subject"],
            body         = state["email_body"],
        )

        print("✅ [email_agent] Communication logged")
        return state

    except Exception as e:
        # Logging failure should not stop the flow
        print(f"⚠️  [email_agent] Log warning: {e}")
        return state


# ── Build Email Subgraph ───────────────────────────────────────────────────────

def build_email_subgraph():
    """
    Builds and returns the Email Agent as a compiled subgraph.
    """
    graph = StateGraph(ProgressReportState)

    # Add nodes
    graph.add_node("generate_email_content", generate_email_content)
    graph.add_node("send_email",             send_email)
    graph.add_node("log_communication",      log_communication)

    # Add edges
    graph.set_entry_point("generate_email_content")
    graph.add_edge("generate_email_content", "send_email")
    graph.add_edge("send_email",             "log_communication")
    graph.add_edge("log_communication",      END)

    return graph.compile()
