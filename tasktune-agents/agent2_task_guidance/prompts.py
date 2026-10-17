"""Prompt templates for each node in the Task Guidance graph."""

from langchain_core.prompts import ChatPromptTemplate

LANGUAGE_INSTRUCTION = (
    "IMPORTANT: Detect the language of the task name and description."
    "You MUST respond in the SAME language. If the task is written in"
    "Arabic, respond entirely in Arabic. If in English, respond in English."
    "Match the user's language exactly.\n\n"
)

# ── Analyze Task Complexity ─────────────────────────────────────────────

ANALYZE_TASK_PROMPT = ChatPromptTemplate.from_messages(
    [
        (
            "system",
            "You are a task-analysis AI for a {industry} company. "
            "Given a task description, determine whether it is **simple** or "
            "**complex** so the right level of guidance can be generated.\n\n"
            "Criteria for SIMPLE:\n"
            "- 0-3 subtasks\n"
            "- No blocking dependencies\n"
            "- Deadline is more than 3 days away\n"
            "- Straightforward scope\n\n"
            "Criteria for COMPLEX:\n"
            "- 4+ subtasks, or\n"
            "- Has blocking dependencies, or\n"
            "- Deadline is within 3 days, or\n"
            "- Requires cross-team coordination\n\n"
            "Respond ONLY with the requested JSON structure.",
        ),
        (
            "human",
            "Task: {task_name}\n"
            "Description: {description}\n"
            "Subtasks: {subtasks}\n"
            "Dependencies: {dependencies}\n"
            "Due Date: {due_date}\n"
            "Completion: {completion_percentage}%",
        ),
    ]
)

# ── Quick Guidance (simple tasks) ───────────────────────────────────────

QUICK_GUIDANCE_PROMPT = ChatPromptTemplate.from_messages(
    [
        (
            "system",
            "You are a task guidance AI for a {industry} company. "
            "The user is a **{user_role}**. Provide concise, actionable "
            "guidance for this straightforward task. "
            "Keep it practical and to-the-point.\n\n"
            "You MUST include a task_breakdown: a nested list where each item "
            "is a main work area with its own subtasks and priority (high / "
            "medium / low). Break down what the person actually needs to DO.\n\n"
            + LANGUAGE_INSTRUCTION +
            "Respond ONLY with the requested JSON structure.",
        ),
        (
            "human",
            "Task: {task_name}\n"
            "Description: {description}\n"
            "Subtasks: {subtasks}\n"
            "Definition of Done: {definition_of_done}\n"
            "Due Date: {due_date}\n"
            "Industry: {industry}",
        ),
    ]
)

# ── Detailed Guidance (complex tasks) ───────────────────────────────────

DETAILED_GUIDANCE_PROMPT = ChatPromptTemplate.from_messages(
    [
        (
            "system",
            "You are a senior task guidance AI for a {industry} company. "
            "The user is a **{user_role}**. Provide thorough, structured "
            "guidance for this complex task. Consider the subtasks, "
            "dependencies, deadline pressure, and the definition of done. "
            "Break the work into a clear execution plan with time estimates "
            "and resource recommendations.\n\n"
            "You MUST include a task_breakdown: a nested list where each item "
            "is a main work area with its own subtasks and priority (high / "
            "medium / low). For complex tasks, provide 4-8 main areas with "
            "detailed subtasks under each.\n\n"
            + LANGUAGE_INSTRUCTION +
            "Respond ONLY with the requested JSON structure.",
        ),
        (
            "human",
            "Task: {task_name}\n"
            "Description: {description}\n"
            "Subtasks: {subtasks}\n"
            "Definition of Done: {definition_of_done}\n"
            "Dependencies: {dependencies}\n"
            "Due Date: {due_date}\n"
            "Current Completion: {completion_percentage}%\n"
            "Industry: {industry}",
        ),
    ]
)

# ── Risk Assessment ─────────────────────────────────────────────────────

RISK_ASSESSMENT_PROMPT = ChatPromptTemplate.from_messages(
    [
        (
            "system",
            "You are a risk-assessment AI for a {industry} company. "
            "Analyze the task below and identify concrete risk factors. "
            "For each risk, provide a brief mitigation tip. "
            "Consider: deadline pressure, dependency blockers, skill gaps, "
            "scope creep, resource availability, and external factors.\n\n"
            + LANGUAGE_INSTRUCTION +
            "Respond ONLY with the requested JSON structure.",
        ),
        (
            "human",
            "Task: {task_name}\n"
            "Description: {description}\n"
            "Subtasks: {subtasks}\n"
            "Dependencies: {dependencies}\n"
            "Due Date: {due_date}\n"
            "Current Completion: {completion_percentage}%\n"
            "Complexity: {complexity}\n"
            "Industry: {industry}",
        ),
    ]
)

# ── Success Criteria ────────────────────────────────────────────────────

SUCCESS_CRITERIA_PROMPT = ChatPromptTemplate.from_messages(
    [
        (
            "system",
            "You are a quality-assurance AI for a {industry} company. "
            "Given a task and its definition of done, produce clear success "
            "criteria so the assignee knows exactly what to deliver, how to "
            "judge quality, and what to check before marking the task done.\n\n"
            "Include:\n"
            "- **deliverables**: concrete outputs to hand over\n"
            "- **quality_indicators**: measurable or observable signs that "
            "the work is done well\n"
            "- **completion_checklist**: final checks before closing the task\n\n"
            + LANGUAGE_INSTRUCTION +
            "Respond ONLY with the requested JSON structure.",
        ),
        (
            "human",
            "Task: {task_name}\n"
            "Description: {description}\n"
            "Definition of Done: {definition_of_done}\n"
            "Subtasks: {subtasks}\n"
            "Due Date: {due_date}\n"
            "Complexity: {complexity}\n"
            "Industry: {industry}",
        ),
    ]
)