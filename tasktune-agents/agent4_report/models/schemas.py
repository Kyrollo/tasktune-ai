from pydantic import BaseModel
from datetime import date, datetime
from typing import Optional


class TaskProgress(BaseModel):
    task_id: str
    task_name: str
    project_id: str
    project_name: str
    employee_id: str
    employee_name: str
    completion_percentage: int
    due_date: date
    status: str
    blockers: Optional[str] = None
    submitted_at: Optional[datetime] = None


class EmployeeSummary(BaseModel):
    employee_id: str
    employee_name: str
    total_tasks: int
    avg_completion: float
    on_time_tasks: int
    late_tasks: int
    at_risk: bool


class AIAnalysis(BaseModel):
    summary: str
    at_risk_employees: list[str]
    predictions: dict[str, str]
    recommendations: list[str]
    overall_health: str


class ReportOutput(BaseModel):
    title: str
    generated_at: str
    run_date: str
    total_tasks: int
    total_employees: int
    avg_completion: float
    employees_summary: list[EmployeeSummary]
    ai_analysis: AIAnalysis