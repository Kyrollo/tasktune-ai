from sqlalchemy.ext.asyncio import create_async_engine, AsyncSession
from sqlalchemy.orm import sessionmaker
from sqlalchemy import text
from datetime import date, datetime
from agent4_Report_Agent.models.schemas import TaskProgress
from agent4_Report_Agent.config import get_settings
import uuid

settings = get_settings()

engine = create_async_engine(
    settings.database_url,
    echo=False,
)

AsyncSessionLocal = sessionmaker(
    bind=engine,
    class_=AsyncSession,
    expire_on_commit=False,
)


async def fetch_tasks_for_company(company_id: str, run_date: date) -> list[TaskProgress]:
    query = text("""
        SELECT
            t.task_id,
            t.task_name,
            t.project_id,
            p.project_name,
            u.user_id AS employee_id,
            u.first_name || ' ' || u.last_name AS employee_name,
            t.completion_percentage,
            t.due_date,
            t.status,
            t.submitted_at,
            (
                SELECT tp.blockers
                FROM task_progress tp
                WHERE tp.task_id = t.task_id
                ORDER BY tp.submitted_at DESC
                LIMIT 1
            ) AS blockers
        FROM tasks t
        JOIN users u ON u.user_id = t.assigned_to
        JOIN projects p ON p.project_id = t.project_id
        WHERE
            p.company_id = :company_id
            AND t.status IN ('not-started', 'in-progress')
            AND p.status = 'active'
        ORDER BY u.user_id, t.due_date
    """)

    async with AsyncSessionLocal() as session:
        result = await session.execute(query, {"company_id": company_id})
        rows = result.mappings().all()

    tasks = []
    for row in rows:
        tasks.append(TaskProgress(
            task_id               = row["task_id"],
            task_name             = row["task_name"],
            project_id            = row["project_id"],
            project_name          = row["project_name"],
            employee_id           = row["employee_id"],
            employee_name         = row["employee_name"],
            completion_percentage = row["completion_percentage"],
            due_date              = row["due_date"],
            status                = row["status"],
            blockers              = row["blockers"],
            submitted_at          = row["submitted_at"],
        ))

    return tasks


async def fetch_manager_info(manager_id: str) -> dict:
    query = text("""
        SELECT
            user_id,
            first_name || ' ' || last_name AS full_name,
            email
        FROM users
        WHERE user_id = :manager_id
        AND is_active = 1
    """)

    async with AsyncSessionLocal() as session:
        result = await session.execute(query, {"manager_id": manager_id})
        row = result.mappings().first()

    if not row:
        raise ValueError(f"Manager {manager_id} not found or inactive")

    return {
        "manager_id":    row["user_id"],
        "manager_name":  row["full_name"],
        "manager_email": row["email"],
    }


async def save_report_to_db(
    company_id: str,
    manager_id: str,
    run_date: date,
    ai_insights: str,
    report_period_start: date,
    report_period_end: date,
) -> str:
    report_id = str(uuid.uuid4())
    query = text("""
        INSERT INTO reports (
            report_id,
            company_id,
            generated_by,
            report_type,
            report_period_start,
            report_period_end,
            ai_insights,
            generated_at,
            metadata_json
        ) VALUES (
            :report_id,
            :company_id,
            :manager_id,
            'performance',
            :period_start,
            :period_end,
            :ai_insights,
            :generated_at,
            :metadata
        )
    """)

    async with AsyncSessionLocal() as session:
        await session.execute(query, {
            "report_id":    report_id,
            "company_id":   company_id,
            "manager_id":   manager_id,
            "period_start": str(report_period_start),
            "period_end":   str(report_period_end),
            "ai_insights":  ai_insights,
            "generated_at": str(datetime.now()),
            "metadata":     '{"source": "progress_agent", "version": "1.0"}',
        })
        await session.commit()

    return report_id


async def save_communication_log(
    company_id: str,
    sender_id: str,
    recipient_id: str,
    subject: str,
    body: str,
) -> None:
    query = text("""
        INSERT INTO communication_log (
            communication_id,
            company_id,
            sent_by,
            recipient_user_id,
            communication_type,
            subject,
            body,
            ai_generated,
            sent_at
        ) VALUES (
            :communication_id,
            :company_id,
            :sender_id,
            :recipient_id,
            'announcement',
            :subject,
            :body,
            1,
            :sent_at
        )
    """)

    async with AsyncSessionLocal() as session:
        await session.execute(query, {
            "communication_id": str(uuid.uuid4()),
            "company_id":       company_id,
            "sender_id":        sender_id,
            "recipient_id":     recipient_id,
            "subject":          subject,
            "body":             body,
            "sent_at":          str(datetime.now()),
        })
        await session.commit()


async def save_notification(
    user_id: str,
    title: str,
    message: str,
    action_url: str,
) -> None:
    query = text("""
        INSERT INTO notifications (
            notification_id,
            user_id,
            notification_type,
            title,
            message,
            action_url,
            is_read,
            sent_at
        ) VALUES (
            :notification_id,
            :user_id,
            'system',
            :title,
            :message,
            :action_url,
            0,
            :sent_at
        )
    """)

    async with AsyncSessionLocal() as session:
        await session.execute(query, {
            "notification_id": str(uuid.uuid4()),
            "user_id":         user_id,
            "title":           title,
            "message":         message,
            "action_url":      action_url,
            "sent_at":         str(datetime.now()),
        })
        await session.commit()