from typing import TypedDict, Optional
from .schemas import TaskProgress, AIAnalysis, ReportOutput


class ProgressReportState(TypedDict):
    company_id: str
    manager_id: str
    manager_email: str
    manager_name: str
    run_date: str
    tasks_data: list[TaskProgress]
    is_valid: bool
    analysis: Optional[AIAnalysis]
    report: Optional[ReportOutput]
    email_subject: str
    email_body: str
    email_sent: bool
    error: Optional[str]