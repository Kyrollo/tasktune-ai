import json
from functools import lru_cache
from langchain_core.messages import HumanMessage, SystemMessage, ToolMessage
from agent.state import AgentState
from agent.tools import ALL_TOOLS, TOOLS_BY_NAME
 
# ─────────────────────────────────────────
#  Lazy LLM — only instantiated on first use
#  inside Streamlit runtime, not at import time
# ─────────────────────────────────────────
@lru_cache(maxsize=1)
def _get_llm():
    import os
    from langchain_groq import ChatGroq
    return ChatGroq(model=os.getenv("GROQ_MODEL", "qwen/qwen3.8-27b"), temperature=0)
 
 
@lru_cache(maxsize=1)
def _get_llm_with_tools():
    return _get_llm().bind_tools(ALL_TOOLS)
 
 
# ─────────────────────────────────────────
#  Router node
# ─────────────────────────────────────────
def router_node(state: AgentState) -> AgentState:
    """Determine which task the agent should perform."""
    if state.get("compose_prompt"):
        return {**state, "task": "compose"}
    elif state.get("email"):
        return {**state, "task": "auto_reply"}
    else:
        return {**state, "task": "classify", "error": "No input provided."}
 
 
def route_decision(state: AgentState) -> str:
    """LangGraph conditional edge — returns next node name."""
    return state.get("task", "classify")
 
 
# ─────────────────────────────────────────
#  Node 1 — Classify incoming email
# ─────────────────────────────────────────
def classify_node(state: AgentState) -> AgentState:
    """Classify an email into a department category with a reason."""
    email = state.get("email")
    if not email:
        return {**state, "error": "No email to classify."}
 
    system = SystemMessage(content=(
        "You are an email classification agent for a company's internal communications.\n"
        "Classify the email into exactly one of: HR, IT, Finance, Cross-dept, General.\n"
        "Respond ONLY with valid JSON in this format:\n"
        '{"category": "HR", "reason": "one sentence explanation"}'
    ))
    human = HumanMessage(content=(
        f"From: {email['from_name']} <{email['from_email']}>\n"
        f"Subject: {email['subject']}\n\n"
        f"{email['body']}"
    ))
 
    try:
        response = _get_llm().invoke([system, human])
        parsed = json.loads(response.content)
        return {
            **state,
            "category": parsed.get("category", "General"),
            "category_reason": parsed.get("reason", ""),
        }
    except Exception as e:
        return {**state, "category": "General", "category_reason": str(e)}
 
 
# ─────────────────────────────────────────
#  Node 2 — Auto-reply with tool use
# ─────────────────────────────────────────
def auto_reply_node(state: AgentState) -> AgentState:
    """Generate a reply to an incoming email, using tools if needed."""
    email = state.get("email")
    if not email:
        return {**state, "error": "No email to reply to."}
 
    system = SystemMessage(content=(
        "You are a professional HR and employee communications agent.\n"
        "Reply to the employee's email in a warm, clear, and helpful tone.\n"
        "Use the available tools to look up accurate policy information when relevant.\n"
        "Keep replies concise (under 100 words). Plain text only — no markdown.\n"
        "Sign off as: HR Agent"
    ))
    human = HumanMessage(content=(
        f"Reply to this email:\n\n"
        f"From: {email['from_name']} <{email['from_email']}>\n"
        f"Subject: {email['subject']}\n\n"
        f"{email['body']}"
    ))
 
    messages = [system, human]
 
    for _ in range(3):
        response = _get_llm_with_tools().invoke(messages)
        messages.append(response)
 
        if not response.tool_calls:
            return {**state, "reply": response.content}
 
        for tool_call in response.tool_calls:
            tool_fn = TOOLS_BY_NAME.get(tool_call["name"])
            if tool_fn:
                result = tool_fn.invoke(tool_call["args"])
                messages.append(
                    ToolMessage(content=str(result), tool_call_id=tool_call["id"])
                )
 
    last_text = next(
        (m.content for m in reversed(messages) if hasattr(m, "content") and isinstance(m.content, str)),
        "I'll look into this and get back to you shortly.\n\nHR Agent"
    )
    return {**state, "reply": last_text}
 
 
# ─────────────────────────────────────────
#  Node 3 — Compose a new email from prompt
# ─────────────────────────────────────────
def compose_node(state: AgentState) -> AgentState:
    """Write a full email from a short natural-language prompt."""
    prompt = state.get("compose_prompt", "")
    to = state.get("compose_to", "")
    subject = state.get("compose_subject", "")
 
    system = SystemMessage(content=(
        "You are a professional business email writer for internal company communications.\n"
        "Write clear, concise, and professional emails based on the prompt.\n"
        "Output ONLY the email body text — no subject line, no 'To:' header.\n"
        "Keep it under 120 words. Sign off as: HR Communications Team"
    ))
 
    context_parts = []
    if to:
        context_parts.append(f"To: {to}")
    if subject:
        context_parts.append(f"Subject: {subject}")
    context_parts.append(f"Prompt: {prompt}")
 
    human = HumanMessage(content="\n".join(context_parts))
 
    try:
        response = _get_llm().invoke([system, human])
        return {**state, "draft": response.content}
    except Exception as e:
        return {**state, "error": str(e), "draft": ""}
 