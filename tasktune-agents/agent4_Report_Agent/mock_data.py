import uuid
from datetime import date, datetime, timedelta
from agent4_Report_Agent.models.schemas import TaskProgress
from agent4_Report_Agent.models.state import ProgressReportState

# ── Fixed UUIDs ────────────────────────────────────────────
COMPANY_ID    = "11111111-1111-1111-1111-111111111111"
MANAGER_ID    = "22222222-2222-2222-2222-222222222222"
MANAGER_EMAIL = "ya7942147@gmail.com"
MANAGER_NAME  = "Ahmed Hassan"

EMPLOYEES = [
    {"id": "aaaa0001-0000-0000-0000-000000000001", "name": "Sara Mohamed",  "email": "sara@tasktune.ai"},
    {"id": "aaaa0002-0000-0000-0000-000000000002", "name": "Omar Khaled",   "email": "omar@tasktune.ai"},
    {"id": "aaaa0003-0000-0000-0000-000000000003", "name": "Nour Ibrahim",  "email": "nour@tasktune.ai"},
    {"id": "aaaa0004-0000-0000-0000-000000000004", "name": "Karim Adel",    "email": "karim@tasktune.ai"},
]

PROJECTS = [
    {"id": "bbbb0001-0000-0000-0000-000000000001", "name": "Website Redesign"},
    {"id": "bbbb0002-0000-0000-0000-000000000002", "name": "Mobile App v2"},
    {"id": "bbbb0003-0000-0000-0000-000000000003", "name": "HR Portal"},
]

TODAY = date.today()

RAW_TASKS = [
    # Sara — on track
    {"task_id": "cccc0001-0000-0000-0000-000000000001", "task_name": "Design new landing page",      "project_id": PROJECTS[0]["id"], "project_name": PROJECTS[0]["name"], "employee_id": EMPLOYEES[0]["id"], "employee_name": EMPLOYEES[0]["name"], "completion_percentage": 80, "due_date": TODAY + timedelta(days=3),  "status": "in-progress",  "blockers": None},
    {"task_id": "cccc0002-0000-0000-0000-000000000002", "task_name": "Write UI component tests",     "project_id": PROJECTS[0]["id"], "project_name": PROJECTS[0]["name"], "employee_id": EMPLOYEES[0]["id"], "employee_name": EMPLOYEES[0]["name"], "completion_percentage": 60, "due_date": TODAY + timedelta(days=5),  "status": "in-progress",  "blockers": None},
    # Omar — at risk
    {"task_id": "cccc0003-0000-0000-0000-000000000003", "task_name": "Build authentication module",  "project_id": PROJECTS[1]["id"], "project_name": PROJECTS[1]["name"], "employee_id": EMPLOYEES[1]["id"], "employee_name": EMPLOYEES[1]["name"], "completion_percentage": 30, "due_date": TODAY - timedelta(days=2),  "status": "in-progress",  "blockers": "Waiting for API keys from client"},
    {"task_id": "cccc0004-0000-0000-0000-000000000004", "task_name": "Integrate push notifications", "project_id": PROJECTS[1]["id"], "project_name": PROJECTS[1]["name"], "employee_id": EMPLOYEES[1]["id"], "employee_name": EMPLOYEES[1]["name"], "completion_percentage": 10, "due_date": TODAY + timedelta(days=1),  "status": "not-started", "blockers": "Blocked by authentication module"},
    # Nour — average
    {"task_id": "cccc0005-0000-0000-0000-000000000005", "task_name": "Setup employee onboarding",    "project_id": PROJECTS[2]["id"], "project_name": PROJECTS[2]["name"], "employee_id": EMPLOYEES[2]["id"], "employee_name": EMPLOYEES[2]["name"], "completion_percentage": 50, "due_date": TODAY + timedelta(days=7),  "status": "in-progress",  "blockers": "Need final approval on HR policy doc"},
    {"task_id": "cccc0006-0000-0000-0000-000000000006", "task_name": "Design leave request form",    "project_id": PROJECTS[2]["id"], "project_name": PROJECTS[2]["name"], "employee_id": EMPLOYEES[2]["id"], "employee_name": EMPLOYEES[2]["name"], "completion_percentage": 70, "due_date": TODAY + timedelta(days=4),  "status": "in-progress",  "blockers": None},
    # Karim — late
    {"task_id": "cccc0007-0000-0000-0000-000000000007", "task_name": "Refactor database queries",    "project_id": PROJECTS[0]["id"], "project_name": PROJECTS[0]["name"], "employee_id": EMPLOYEES[3]["id"], "employee_name": EMPLOYEES[3]["name"], "completion_percentage": 0,  "due_date": TODAY - timedelta(days=5),  "status": "not-started", "blockers": "Was on sick leave"},
    {"task_id": "cccc0008-0000-0000-0000-000000000008", "task_name": "Write API documentation",      "project_id": PROJECTS[1]["id"], "project_name": PROJECTS[1]["name"], "employee_id": EMPLOYEES[3]["id"], "employee_name": EMPLOYEES[3]["name"], "completion_percentage": 40, "due_date": TODAY + timedelta(days=6),  "status": "in-progress",  "blockers": None},
]


def get_mock_tasks() -> list[TaskProgress]:
    return [
        TaskProgress(
            task_id               = t["task_id"],
            task_name             = t["task_name"],
            project_id            = t["project_id"],
            project_name          = t["project_name"],
            employee_id           = t["employee_id"],
            employee_name         = t["employee_name"],
            completion_percentage = t["completion_percentage"],
            due_date              = t["due_date"],
            status                = t["status"],
            blockers              = t.get("blockers"),
        )
        for t in RAW_TASKS
    ]


def get_mock_state() -> ProgressReportState:
    return ProgressReportState(
        company_id    = COMPANY_ID,
        manager_id    = MANAGER_ID,
        manager_email = MANAGER_EMAIL,
        manager_name  = MANAGER_NAME,
        run_date      = str(TODAY),
        tasks_data    = get_mock_tasks(),
        is_valid      = True,
        analysis      = None,
        report        = None,
        email_subject = "",
        email_body    = "",
        email_sent    = False,
        error         = None,
    )


def setup_sqlite(db_path: str = "test_tasktune.db") -> None:
    import sqlite3, json

    conn = sqlite3.connect(db_path)
    c = conn.cursor()
    c.executescript("""
        CREATE TABLE IF NOT EXISTS companies (company_id TEXT PRIMARY KEY, company_name TEXT, settings_json TEXT);
        CREATE TABLE IF NOT EXISTS users (user_id TEXT PRIMARY KEY, company_id TEXT, first_name TEXT, last_name TEXT, email TEXT, user_type TEXT, is_active INTEGER DEFAULT 1);
        CREATE TABLE IF NOT EXISTS projects (project_id TEXT PRIMARY KEY, company_id TEXT, project_name TEXT, status TEXT);
        CREATE TABLE IF NOT EXISTS tasks (task_id TEXT PRIMARY KEY, project_id TEXT, task_name TEXT, status TEXT, assigned_to TEXT, completion_percentage INTEGER DEFAULT 0, due_date TEXT, submitted_at TEXT);
        CREATE TABLE IF NOT EXISTS task_progress (progress_id TEXT PRIMARY KEY, task_id TEXT, submitted_by TEXT, completion_percentage INTEGER, notes TEXT, blockers TEXT, submitted_at TEXT);
        CREATE TABLE IF NOT EXISTS reports (report_id TEXT PRIMARY KEY, company_id TEXT, generated_by TEXT, report_type TEXT, report_period_start TEXT, report_period_end TEXT, ai_insights TEXT, generated_at TEXT, metadata_json TEXT);
        CREATE TABLE IF NOT EXISTS communication_log (communication_id TEXT PRIMARY KEY, company_id TEXT, sent_by TEXT, recipient_user_id TEXT, communication_type TEXT, subject TEXT, body TEXT, ai_generated INTEGER DEFAULT 0, sent_at TEXT);
        CREATE TABLE IF NOT EXISTS notifications (notification_id TEXT PRIMARY KEY, user_id TEXT, notification_type TEXT, title TEXT, message TEXT, action_url TEXT, is_read INTEGER DEFAULT 0, sent_at TEXT);
    """)

    c.execute("INSERT OR REPLACE INTO companies VALUES (?,?,?)", (COMPANY_ID, "TaskTune Demo Co.", json.dumps({"ai_enabled": True})))
    c.execute("INSERT OR REPLACE INTO users VALUES (?,?,?,?,?,?,?)", (MANAGER_ID, COMPANY_ID, "Ahmed", "Hassan", MANAGER_EMAIL, "management", 1))

    for emp in EMPLOYEES:
        n = emp["name"].split()
        c.execute("INSERT OR REPLACE INTO users VALUES (?,?,?,?,?,?,?)", (emp["id"], COMPANY_ID, n[0], n[1], emp["email"], "employee", 1))

    for proj in PROJECTS:
        c.execute("INSERT OR REPLACE INTO projects VALUES (?,?,?,?)", (proj["id"], COMPANY_ID, proj["name"], "active"))

    for t in RAW_TASKS:
        c.execute("INSERT OR REPLACE INTO tasks VALUES (?,?,?,?,?,?,?,?)", (
            t["task_id"], t["project_id"], t["task_name"], t["status"],
            t["employee_id"], t["completion_percentage"], str(t["due_date"]), None,
        ))
        if t.get("blockers"):
            c.execute("INSERT OR REPLACE INTO task_progress VALUES (?,?,?,?,?,?,?)", (
                str(uuid.uuid4()), t["task_id"], t["employee_id"],
                t["completion_percentage"], "Progress update", t["blockers"], str(datetime.now()),
            ))

    conn.commit()
    conn.close()
    print(f"✅ SQLite DB created: {db_path}")


if __name__ == "__main__":
    print("📊 Mock Tasks Preview:")
    print("-" * 65)
    for t in get_mock_tasks():
        overdue = "🔴 OVERDUE" if t.due_date < TODAY else "🟢        "
        print(f"{overdue} [{t.completion_percentage:3}%] {t.employee_name:<15} | {t.task_name}")
        if t.blockers:
            print(f"          ⚠️  Blocker: {t.blockers}")
    print("-" * 65)
    setup_sqlite()