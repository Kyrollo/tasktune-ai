"""
LangChain prompt templates for each Progress Tracking node.

Design principles
─────────────────
1. Every prompt requests **valid JSON only** — no markdown, no code fences.
2. Enum / status fields (progress_summary, risk_level) are **always English**.
3. Free‑text fields (explanations, analysis, recommendations) follow the
   language detected from the user's task description, notes, and blockers
   — supporting **English and Arabic** out of the box.
"""

from langchain_core.prompts import ChatPromptTemplate

# ── Shared instruction fragments ───────────────────────────────────

LANGUAGE_INSTRUCTION = (
    "IMPORTANT — LANGUAGE RULES:\n"
    "1. Detect the language used in the task title, description, progress notes, "
    "and blocker descriptions.\n"
    "2. For ALL free‑text / explanation fields in your JSON response, you MUST "
    "reply in the SAME language as the input.  If the input is in Arabic, write "
    "explanations and recommendations in Arabic.  If in English, respond in "
    "English.  Match the user's language exactly.\n"
    "3. HOWEVER, the following fields must ALWAYS use their exact English enum "
    "values regardless of input language:\n"
    '   • "progress_summary" → one of "On track", "At risk", "Delayed"\n'
    '   • "risk_level"       → one of "low", "medium", "high"\n\n'
)

JSON_INSTRUCTION = (
    "RESPONSE FORMAT:\n"
    "You MUST respond with a single valid JSON object.  "
    "No markdown code fences, no commentary, no extra text — just raw JSON.\n\n"
)

# ── 1. Analyze Progress ────────────────────────────────────────────

ANALYZE_PROGRESS_PROMPT = ChatPromptTemplate.from_messages([
    (
        "system",
        "You are a project‑management AI specialized in progress analysis.\n\n"
        + LANGUAGE_INSTRUCTION
        + JSON_INSTRUCTION
        + "Analyze the task data below and determine whether the task is "
        "'On track', 'At risk', or 'Delayed'.\n\n"
        "Consider:\n"
        "• Current completion percentage vs. time remaining until the due date.\n"
        "• The velocity / rate of progress shown in the progress updates.\n"
        "• The number and severity of current blockers.\n"
        "• The user's historical on‑time delivery rate (if available).\n\n"
        "Return a JSON object with exactly these keys:\n"
        '{{\n'
        '  "progress_summary": "<On track | At risk | Delayed>",\n'
        '  "reason": "<brief explanation in the user\'s language>"\n'
        '}}'
    ),
    (
        "human",
        "Task ID: {task_id}\n"
        "Task title: {task_title}\n"
        "Task description: {task_description}\n"
        "Current completion: {current_completion_percentage}%\n"
        "Start date: {start_date}\n"
        "Due date: {due_date}\n"
        "Progress updates: {progress_updates}\n"
        "Current blockers: {current_blockers}\n"
        "User performance history: {user_performance_history}"
    ),
])

# ── 2. Predict Completion Date ─────────────────────────────────────

PREDICT_COMPLETION_PROMPT = ChatPromptTemplate.from_messages([
    (
        "system",
        "You are a project‑management AI specialized in schedule prediction.\n\n"
        + LANGUAGE_INSTRUCTION
        + JSON_INSTRUCTION
        + "Estimate the most likely completion date for this task.\n\n"
        "Consider:\n"
        "• The progress velocity derived from the updates.\n"
        "• Remaining work (100% minus current completion).\n"
        "• Impact of current blockers on future velocity.\n"
        "• The user's historical on‑time delivery rate.\n"
        "• The progress summary from prior analysis.\n\n"
        "If you cannot make a reasonable estimate, set completion_prediction to null.\n\n"
        "Return a JSON object with exactly these keys:\n"
        '{{\n'
        '  "completion_prediction": "<YYYY-MM-DD or null>",\n'
        '  "reasoning": "<explanation in the user\'s language>"\n'
        '}}'
    ),
    (
        "human",
        "Task ID: {task_id}\n"
        "Task title: {task_title}\n"
        "Current completion: {current_completion_percentage}%\n"
        "Start date: {start_date}\n"
        "Due date: {due_date}\n"
        "Progress summary so far: {progress_summary}\n"
        "Progress updates: {progress_updates}\n"
        "Current blockers: {current_blockers}\n"
        "User performance history: {user_performance_history}"
    ),
])

# ── 3. Assess Risk ─────────────────────────────────────────────────

RISK_ASSESSMENT_PROMPT = ChatPromptTemplate.from_messages([
    (
        "system",
        "You are a project‑management AI specialized in risk assessment.\n\n"
        + LANGUAGE_INSTRUCTION
        + JSON_INSTRUCTION
        + "Assess the overall risk level for this task: low, medium, or high.\n"
        "Provide a detailed analysis of blockers and risk factors.\n\n"
        "Consider:\n"
        "• Current blockers — severity, duration, dependencies.\n"
        "• Gap between predicted completion and the due date.\n"
        "• Progress trend — accelerating, steady, or decelerating.\n"
        "• User's track record and reliability.\n\n"
        "Return a JSON object with exactly these keys:\n"
        '{{\n'
        '  "risk_level": "<low | medium | high>",\n'
        '  "blockers_analysis": "<detailed narrative in the user\'s language>",\n'
        '  "risk_factors": ["<factor 1>", "<factor 2>", "..."]\n'
        '}}'
    ),
    (
        "human",
        "Task ID: {task_id}\n"
        "Task title: {task_title}\n"
        "Current completion: {current_completion_percentage}%\n"
        "Start date: {start_date}\n"
        "Due date: {due_date}\n"
        "Progress summary: {progress_summary}\n"
        "Predicted completion date: {completion_prediction}\n"
        "Progress updates: {progress_updates}\n"
        "Current blockers: {current_blockers}\n"
        "User performance history: {user_performance_history}"
    ),
])

# ── 4. Generate Recommendations ────────────────────────────────────

RECOMMENDATIONS_PROMPT = ChatPromptTemplate.from_messages([
    (
        "system",
        "You are a project‑management AI specialized in actionable advice.\n\n"
        + LANGUAGE_INSTRUCTION
        + JSON_INSTRUCTION
        + "Based on the full analysis below, provide 2–4 concrete, actionable "
        "recommendations to improve progress or mitigate risks.\n\n"
        "Each recommendation should be specific and directly tied to the "
        "identified blockers, risk factors, or progress trends.\n\n"
        "Return a JSON object with exactly this key:\n"
        '{{\n'
        '  "recommendations": ["<recommendation 1>", "<recommendation 2>", "..."]\n'
        '}}'
    ),
    (
        "human",
        "Task ID: {task_id}\n"
        "Task title: {task_title}\n"
        "Current completion: {current_completion_percentage}%\n"
        "Due date: {due_date}\n"
        "Progress summary: {progress_summary}\n"
        "Predicted completion date: {completion_prediction}\n"
        "Risk level: {risk_level}\n"
        "Blockers analysis: {blockers_analysis}\n"
        "Current blockers: {current_blockers}"
    ),
])