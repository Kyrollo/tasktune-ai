import streamlit as st
import asyncio
import sys
from datetime import date
from config import get_settings
from services.db_service import fetch_tasks_for_company
from models.state import ProgressReportState

settings = get_settings()

if sys.platform == "win32":
    asyncio.set_event_loop_policy(asyncio.WindowsSelectorEventLoopPolicy())

# ── Page config ───────────────────────────────────────────────────────────────
st.set_page_config(
    page_title="TaskTune AI — Progress Agent",
    page_icon="⚡",
    layout="wide",
    initial_sidebar_state="expanded",
)

# ── Custom CSS ────────────────────────────────────────────────────────────────
st.markdown("""
<style>
@import url('https://fonts.googleapis.com/css2?family=Syne:wght@400;600;700;800&family=DM+Sans:wght@300;400;500&display=swap');

html, body, [class*="css"] {
    font-family: 'DM Sans', sans-serif;
}

/* Background */
.stApp {
    background: #0a0a0f;
    color: #e8e6f0;
}

/* Sidebar */
[data-testid="stSidebar"] {
    background: #0f0f1a !important;
    border-right: 1px solid #1e1e30;
}

/* Hide default header */
header[data-testid="stHeader"] { display: none; }

/* Custom header */
.tt-header {
    display: flex;
    align-items: center;
    gap: 16px;
    padding: 28px 0 32px;
    border-bottom: 1px solid #1e1e30;
    margin-bottom: 32px;
}
.tt-logo {
    width: 44px; height: 44px;
    background: linear-gradient(135deg, #7c5cbf, #4e9af1);
    border-radius: 12px;
    display: flex; align-items: center; justify-content: center;
    font-size: 20px;
}
.tt-title {
    font-family: 'Syne', sans-serif;
    font-size: 22px;
    font-weight: 800;
    color: #ffffff;
    letter-spacing: -0.5px;
    margin: 0;
}
.tt-subtitle {
    font-size: 12px;
    color: #6b6880;
    margin: 2px 0 0;
    letter-spacing: 0.5px;
    text-transform: uppercase;
}

/* KPI Cards */
.kpi-card {
    background: #111118;
    border: 1px solid #1e1e30;
    border-radius: 16px;
    padding: 24px;
    position: relative;
    overflow: hidden;
}
.kpi-card::before {
    content: '';
    position: absolute;
    top: 0; left: 0; right: 0;
    height: 2px;
}
.kpi-card.purple::before { background: linear-gradient(90deg, #7c5cbf, transparent); }
.kpi-card.blue::before   { background: linear-gradient(90deg, #4e9af1, transparent); }
.kpi-card.green::before  { background: linear-gradient(90deg, #3ecf8e, transparent); }
.kpi-card.amber::before  { background: linear-gradient(90deg, #f59e0b, transparent); }
.kpi-card.red::before    { background: linear-gradient(90deg, #ef4444, transparent); }

.kpi-value {
    font-family: 'Syne', sans-serif;
    font-size: 36px;
    font-weight: 800;
    color: #ffffff;
    line-height: 1;
    margin: 8px 0 4px;
}
.kpi-label {
    font-size: 12px;
    color: #6b6880;
    text-transform: uppercase;
    letter-spacing: 0.8px;
}
.kpi-icon {
    font-size: 24px;
    margin-bottom: 4px;
}

/* Employee cards */
.emp-card {
    background: #111118;
    border: 1px solid #1e1e30;
    border-radius: 14px;
    padding: 20px;
    margin-bottom: 12px;
    transition: border-color 0.2s;
}
.emp-card:hover { border-color: #2e2e45; }
.emp-card.at-risk { border-color: #3d1f1f; background: #130f0f; }
.emp-name {
    font-family: 'Syne', sans-serif;
    font-size: 15px;
    font-weight: 700;
    color: #ffffff;
    margin-bottom: 4px;
}
.emp-meta {
    font-size: 12px;
    color: #6b6880;
    margin-bottom: 12px;
}
.progress-bar-bg {
    background: #1e1e30;
    border-radius: 100px;
    height: 6px;
    overflow: hidden;
    margin: 8px 0;
}
.progress-bar-fill {
    height: 100%;
    border-radius: 100px;
    transition: width 0.5s ease;
}
.badge {
    display: inline-block;
    padding: 3px 10px;
    border-radius: 100px;
    font-size: 11px;
    font-weight: 500;
    letter-spacing: 0.3px;
}
.badge-risk    { background: #3d1515; color: #f87171; border: 1px solid #5a1f1f; }
.badge-ok      { background: #0f2d1f; color: #3ecf8e; border: 1px solid #1a4a33; }
.badge-warning { background: #2d2000; color: #f59e0b; border: 1px solid #4a3500; }

/* Task rows */
.task-row {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 12px 16px;
    border-radius: 10px;
    margin-bottom: 6px;
    background: #0f0f18;
    border: 1px solid #1a1a28;
}
.task-pct {
    font-family: 'Syne', sans-serif;
    font-size: 13px;
    font-weight: 700;
    min-width: 42px;
    text-align: right;
}
.task-name { font-size: 13px; color: #c8c5d8; flex: 1; }
.task-emp  { font-size: 11px; color: #6b6880; min-width: 110px; }
.task-due  { font-size: 11px; min-width: 90px; text-align: right; }

/* AI Analysis card */
.ai-card {
    background: #0d0d1f;
    border: 1px solid #2a1f4f;
    border-radius: 16px;
    padding: 24px;
    position: relative;
}
.ai-card::before {
    content: '⚡ AI ANALYSIS';
    position: absolute;
    top: -10px; left: 20px;
    background: linear-gradient(135deg, #7c5cbf, #4e9af1);
    color: white;
    font-size: 10px;
    font-weight: 700;
    letter-spacing: 1px;
    padding: 3px 10px;
    border-radius: 100px;
}
.ai-summary {
    font-size: 14px;
    color: #c8c5d8;
    line-height: 1.7;
    margin-bottom: 20px;
}
.rec-item {
    display: flex;
    gap: 10px;
    padding: 10px 14px;
    background: #111125;
    border-radius: 8px;
    margin-bottom: 8px;
    font-size: 13px;
    color: #c8c5d8;
    border-left: 3px solid #7c5cbf;
}
.rec-num {
    font-family: 'Syne', sans-serif;
    font-weight: 700;
    color: #7c5cbf;
    min-width: 20px;
}

/* Run button */
.stButton > button {
    background: linear-gradient(135deg, #7c5cbf, #4e9af1) !important;
    color: white !important;
    border: none !important;
    border-radius: 12px !important;
    padding: 14px 28px !important;
    font-family: 'Syne', sans-serif !important;
    font-weight: 700 !important;
    font-size: 14px !important;
    letter-spacing: 0.5px !important;
    width: 100% !important;
    cursor: pointer !important;
    transition: opacity 0.2s !important;
}
.stButton > button:hover { opacity: 0.9 !important; }

/* Section titles */
.section-title {
    font-family: 'Syne', sans-serif;
    font-size: 13px;
    font-weight: 700;
    color: #6b6880;
    text-transform: uppercase;
    letter-spacing: 1.5px;
    margin: 28px 0 16px;
    display: flex;
    align-items: center;
    gap: 8px;
}
.section-title::after {
    content: '';
    flex: 1;
    height: 1px;
    background: #1e1e30;
}

/* Health badge */
.health-good     { color: #3ecf8e; }
.health-warning  { color: #f59e0b; }
.health-critical { color: #ef4444; }

/* Log output */
.log-box {
    background: #0a0a10;
    border: 1px solid #1e1e30;
    border-radius: 12px;
    padding: 16px 20px;
    font-family: 'DM Mono', monospace;
    font-size: 12px;
    color: #6b6880;
    max-height: 280px;
    overflow-y: auto;
    line-height: 1.8;
}
.log-success { color: #3ecf8e; }
.log-error   { color: #ef4444; }
.log-info    { color: #4e9af1; }
.log-warning { color: #f59e0b; }
</style>
""", unsafe_allow_html=True)


# ── Helpers ───────────────────────────────────────────────────────────────────

def health_color(health: str) -> str:
    return {"good": "#3ecf8e", "warning": "#f59e0b", "critical": "#ef4444"}.get(health, "#6b6880")

def health_icon(health: str) -> str:
    return {"good": "✅", "warning": "⚠️", "critical": "🚨"}.get(health, "❓")

def pct_color(pct: int) -> str:
    if pct >= 70: return "#3ecf8e"
    if pct >= 40: return "#f59e0b"
    return "#ef4444"

def due_label(due_date) -> tuple[str, str]:
    today = date.today()
    if isinstance(due_date, str):
        due_date = date.fromisoformat(due_date)
    diff = (due_date - today).days
    if diff < 0:   return f"{abs(diff)}d overdue", "#ef4444"
    if diff == 0:  return "Due today",             "#f59e0b"
    if diff <= 2:  return f"Due in {diff}d",       "#f59e0b"
    return f"Due in {diff}d", "#6b6880"


async def run_agent(state: ProgressReportState) -> tuple[ProgressReportState, list[str]]:
    from agents.progress_agent import build_progress_agent
    logs = []

    # Capture print output
    import io, sys
    old_stdout = sys.stdout
    sys.stdout = buffer = io.StringIO()

    agent = build_progress_agent()
    final_state = await agent.ainvoke(state)

    sys.stdout = old_stdout
    logs = buffer.getvalue().strip().split("\n")

    return final_state, logs


async def get_backend_snapshot(company_id: str, run_date: date) -> dict:
    tasks = await fetch_tasks_for_company(company_id, run_date)
    employees = {t.employee_id for t in tasks}
    projects = {t.project_id for t in tasks}
    overdue = sum(1 for t in tasks if t.due_date < run_date)
    return {
        "tasks": len(tasks),
        "employees": len(employees),
        "projects": len(projects),
        "overdue": overdue,
    }


# ── Sidebar ───────────────────────────────────────────────────────────────────

with st.sidebar:
    st.markdown("""
    <div class="tt-header">
        <div class="tt-logo">⚡</div>
        <div>
            <div class="tt-title">TaskTune AI</div>
            <div class="tt-subtitle">Progress Agent</div>
        </div>
    </div>
    """, unsafe_allow_html=True)

    st.markdown('<div class="section-title">Configuration</div>', unsafe_allow_html=True)

    manager_name  = st.text_input("Manager Name",  value=settings.manager_name)
    manager_email = st.text_input("Manager Email", value=settings.manager_email)
    run_date      = st.date_input("Report Date",   value=date.today())

    st.markdown('<div class="section-title">Backend Data</div>', unsafe_allow_html=True)
    if settings.company_id:
        try:
            snapshot = asyncio.run(get_backend_snapshot(settings.company_id, run_date))
            st.markdown(f"""
            <div style="font-size:13px; color:#6b6880; line-height:2;">
                📋 <b style="color:#c8c5d8">{snapshot["tasks"]}</b> tasks loaded<br>
                👥 <b style="color:#c8c5d8">{snapshot["employees"]}</b> employees<br>
                📁 <b style="color:#c8c5d8">{snapshot["projects"]}</b> projects<br>
                🔴 <b style="color:#ef4444">{snapshot["overdue"]}</b> overdue tasks
            </div>
            """, unsafe_allow_html=True)
        except Exception as e:
            st.markdown(
                f'<div style="font-size:12px;color:#ef4444;">Could not load backend snapshot: {e}</div>',
                unsafe_allow_html=True,
            )
    else:
        st.markdown(
            '<div style="font-size:12px;color:#f59e0b;">Set COMPANY_ID in .env to load backend data.</div>',
            unsafe_allow_html=True,
        )

    st.markdown('<div class="section-title">Run Agent</div>', unsafe_allow_html=True)
    run_btn = st.button("⚡ Run Progress Agent")

    st.markdown("""
    <div style="position:absolute; bottom:24px; left:24px; right:24px; font-size:11px; color:#3a3850; text-align:center;">
        TaskTune AI v1.0 · LangGraph + Groq
    </div>
    """, unsafe_allow_html=True)


# ── Main Content ──────────────────────────────────────────────────────────────

# Header
st.markdown(f"""
<div class="tt-header">
    <div class="tt-logo">⚡</div>
    <div>
        <div class="tt-title">Daily Progress Report</div>
        <div class="tt-subtitle">AI-Powered Workforce Analytics · {run_date.strftime('%B %d, %Y')}</div>
    </div>
</div>
""", unsafe_allow_html=True)


# ── Run agent ─────────────────────────────────────────────────────────────────

if run_btn:
    if not settings.company_id or not settings.manager_id:
        st.error("Set COMPANY_ID and MANAGER_ID in .env before running.")
        st.stop()

    initial_state = ProgressReportState(
        company_id    = settings.company_id,
        manager_id    = settings.manager_id,
        manager_email = manager_email,
        manager_name  = manager_name,
        run_date      = str(run_date),
        tasks_data    = [],
        is_valid      = False,
        analysis      = None,
        report        = None,
        email_subject = "",
        email_body    = "",
        email_sent    = False,
        error         = None,
    )

    with st.spinner("⚡ Running AI analysis..."):
        final_state, logs = asyncio.run(run_agent(initial_state))

    st.session_state["result"] = final_state
    st.session_state["logs"]   = logs


# ── Display results ───────────────────────────────────────────────────────────

if "result" in st.session_state:
    result = st.session_state["result"]
    logs   = st.session_state["logs"]

    if result.get("error") and not result.get("report"):
        st.error(f"❌ Flow failed: {result['error']}")
    else:
        report   = result["report"]
        analysis = result["analysis"]

        # ── KPI Row ──
        st.markdown('<div class="section-title">Overview</div>', unsafe_allow_html=True)

        c1, c2, c3, c4, c5 = st.columns(5)

        with c1:
            st.markdown(f"""
            <div class="kpi-card purple">
                <div class="kpi-icon">📋</div>
                <div class="kpi-value">{report.total_tasks}</div>
                <div class="kpi-label">Total Tasks</div>
            </div>""", unsafe_allow_html=True)

        with c2:
            st.markdown(f"""
            <div class="kpi-card blue">
                <div class="kpi-icon">👥</div>
                <div class="kpi-value">{report.total_employees}</div>
                <div class="kpi-label">Employees</div>
            </div>""", unsafe_allow_html=True)

        with c3:
            st.markdown(f"""
            <div class="kpi-card {'green' if report.avg_completion >= 60 else 'amber'}">
                <div class="kpi-icon">📊</div>
                <div class="kpi-value">{report.avg_completion}%</div>
                <div class="kpi-label">Avg Completion</div>
            </div>""", unsafe_allow_html=True)

        with c4:
            at_risk_count = len(analysis.at_risk_employees)
            st.markdown(f"""
            <div class="kpi-card {'red' if at_risk_count > 0 else 'green'}">
                <div class="kpi-icon">⚠️</div>
                <div class="kpi-value">{at_risk_count}</div>
                <div class="kpi-label">At Risk</div>
            </div>""", unsafe_allow_html=True)

        with c5:
            h = analysis.overall_health
            st.markdown(f"""
            <div class="kpi-card {'green' if h=='good' else 'amber' if h=='warning' else 'red'}">
                <div class="kpi-icon">{health_icon(h)}</div>
                <div class="kpi-value" style="font-size:22px;color:{health_color(h)}">{h.upper()}</div>
                <div class="kpi-label">Overall Health</div>
            </div>""", unsafe_allow_html=True)

        st.markdown("<br>", unsafe_allow_html=True)

        # ── Two columns: Employees + AI Analysis ──
        left, right = st.columns([1, 1], gap="large")

        with left:
            st.markdown('<div class="section-title">Employee Status</div>', unsafe_allow_html=True)

            for emp in report.employees_summary:
                color    = pct_color(int(emp.avg_completion))
                badge    = '<span class="badge badge-risk">AT RISK</span>' if emp.at_risk else '<span class="badge badge-ok">ON TRACK</span>'
                card_cls = "emp-card at-risk" if emp.at_risk else "emp-card"

                st.markdown(f"""
                <div class="{card_cls}">
                    <div style="display:flex;justify-content:space-between;align-items:flex-start;">
                        <div>
                            <div class="emp-name">{emp.employee_name}</div>
                            <div class="emp-meta">{emp.total_tasks} tasks · {emp.late_tasks} late</div>
                        </div>
                        {badge}
                    </div>
                    <div style="display:flex;justify-content:space-between;align-items:center;margin-top:4px;">
                        <div class="progress-bar-bg" style="flex:1;margin-right:12px;">
                            <div class="progress-bar-fill" style="width:{emp.avg_completion}%;background:{color};"></div>
                        </div>
                        <span style="font-family:'Syne',sans-serif;font-size:14px;font-weight:700;color:{color};">{emp.avg_completion}%</span>
                    </div>
                    {'<div style="font-size:11px;color:#ef4444;margin-top:6px;">🔴 Predicted: ' + analysis.predictions.get(emp.employee_name, '—') + '</div>' if emp.at_risk else ''}
                </div>
                """, unsafe_allow_html=True)

        with right:
            st.markdown('<div class="section-title">AI Analysis</div>', unsafe_allow_html=True)

            st.markdown(f"""
            <div class="ai-card">
                <div class="ai-summary">{analysis.summary}</div>
                <div style="font-size:11px;color:#6b6880;text-transform:uppercase;letter-spacing:1px;margin-bottom:10px;">Recommendations</div>
                {''.join(f'<div class="rec-item"><span class="rec-num">{i+1}</span>{r}</div>' for i, r in enumerate(analysis.recommendations))}
            </div>
            """, unsafe_allow_html=True)

            # Email status
            st.markdown("<br>", unsafe_allow_html=True)
            if result.get("email_sent"):
                st.markdown(f"""
                <div style="background:#0f2d1f;border:1px solid #1a4a33;border-radius:12px;padding:16px 20px;display:flex;gap:12px;align-items:center;">
                    <span style="font-size:20px;">📧</span>
                    <div>
                        <div style="font-size:13px;color:#3ecf8e;font-weight:500;">Email sent successfully</div>
                        <div style="font-size:11px;color:#6b6880;margin-top:2px;">{result.get('email_subject','')}</div>
                    </div>
                </div>
                """, unsafe_allow_html=True)
            else:
                st.markdown(f"""
                <div style="background:#1a0f0f;border:1px solid #3d1515;border-radius:12px;padding:16px 20px;">
                    <div style="font-size:13px;color:#f87171;">⚠️ Email not sent</div>
                    <div style="font-size:11px;color:#6b6880;margin-top:2px;">Check SMTP credentials in .env</div>
                </div>
                """, unsafe_allow_html=True)

    

        # ── Agent Logs ──
        st.markdown('<div class="section-title">Agent Logs</div>', unsafe_allow_html=True)

        log_html = ""
        for line in logs:
            if "✅" in line:  cls = "log-success"
            elif "❌" in line: cls = "log-error"
            elif "📥" in line or "🤖" in line or "📝" in line: cls = "log-info"
            elif "⚠️" in line: cls = "log-warning"
            else: cls = ""
            log_html += f'<div class="{cls}">{line}</div>'

        st.markdown(f'<div class="log-box">{log_html}</div>', unsafe_allow_html=True)

else:
    # Empty state
    st.markdown("""
    <div style="text-align:center;padding:80px 40px;">
        <div style="font-size:64px;margin-bottom:24px;">⚡</div>
        <div style="font-family:'Syne',sans-serif;font-size:24px;font-weight:800;color:#ffffff;margin-bottom:12px;">
            Ready to Analyze
        </div>
        <div style="font-size:14px;color:#6b6880;max-width:400px;margin:0 auto;line-height:1.7;">
            Configure your settings in the sidebar and click
            <b style="color:#7c5cbf;">Run Progress Agent</b> to generate
            an AI-powered report for your team.
        </div>
    </div>
    """, unsafe_allow_html=True)