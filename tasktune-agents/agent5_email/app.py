import os
from dotenv import load_dotenv
load_dotenv()  # must run before any agent imports
 
import streamlit as st
from agent.graph import build_graph
from agent.email_client import fetch_inbox, send_email, mark_as_read
 
@st.cache_resource
def get_agent():
    return build_graph()
 
# ─────────────────────────────────────────
#  Page config
# ─────────────────────────────────────────
st.set_page_config(
    page_title="Email Agent",
    page_icon="✉️",
    layout="wide",
    initial_sidebar_state="expanded",
)
 
# ─────────────────────────────────────────
#  Custom CSS
# ─────────────────────────────────────────
st.markdown("""
<style>
    [data-testid="stSidebar"] { background-color: #f8f8f6; }
    [data-testid="stSidebar"] .block-container { padding-top: 1rem; }
    .email-card { padding: 12px 14px; border: 1px solid #e8e8e4; border-radius: 8px; margin-bottom: 8px; background: white; }
    .email-card.unread { border-left: 3px solid #378ADD; }
    .sender-name { font-weight: 600; font-size: 14px; color: #1a1a1a; }
    .email-subject { font-size: 13px; color: #333; margin: 2px 0; }
    .email-preview { font-size: 12px; color: #888; }
    .email-time { font-size: 11px; color: #aaa; float: right; }
    .tag { display: inline-block; padding: 2px 9px; border-radius: 20px; font-size: 11px; font-weight: 500; margin-right: 4px; }
    .tag-HR         { background:#CECBF6; color:#3C3489; }
    .tag-IT         { background:#B5D4F4; color:#185FA5; }
    .tag-Cross-dept { background:#F5C4B3; color:#712B13; }
    .tag-General    { background:#C0DD97; color:#3B6D11; }
    .tag-Finance    { background:#FAC775; color:#633806; }
    .metric-row { display: flex; gap: 12px; margin-bottom: 1rem; }
    .metric-box { flex: 1; background: #f8f8f6; border: 1px solid #e8e8e4; border-radius: 8px; padding: 14px 16px; text-align: center; }
    .metric-num { font-size: 28px; font-weight: 600; color: #1a1a1a; }
    .metric-label { font-size: 12px; color: #888; margin-top: 2px; }
    #MainMenu, footer { visibility: hidden; }
    .block-container { padding-top: 1.5rem; padding-bottom: 1rem; }
</style>
""", unsafe_allow_html=True)
 
# ─────────────────────────────────────────
#  Session state init
# ─────────────────────────────────────────
if "inbox" not in st.session_state:
    st.session_state.inbox = []
if "inbox_loaded" not in st.session_state:
    st.session_state.inbox_loaded = False
if "sent" not in st.session_state:
    st.session_state.sent = []
if "selected_email" not in st.session_state:
    st.session_state.selected_email = None
if "view" not in st.session_state:
    st.session_state.view = "inbox"
if "ai_reply" not in st.session_state:
    st.session_state.ai_reply = {}
if "ai_category" not in st.session_state:
    st.session_state.ai_category = {}
if "compose_draft" not in st.session_state:
    st.session_state.compose_draft = ""
if "fetch_error" not in st.session_state:
    st.session_state.fetch_error = None
 
# ─────────────────────────────────────────
#  Helpers
# ─────────────────────────────────────────
def tag_html(tag):
    return f'<span class="tag tag-{tag}">{tag}</span>'
 
def unread_count():
    return sum(1 for e in st.session_state.inbox if not e["read"])
 
def load_inbox():
    try:
        with st.spinner("Fetching emails from Gmail..."):
            emails = fetch_inbox(max_emails=20)
        st.session_state.inbox = emails
        st.session_state.inbox_loaded = True
        st.session_state.fetch_error = None
    except Exception as e:
        st.session_state.fetch_error = str(e)
        st.session_state.inbox_loaded = True
 
def run_auto_reply(email_item):
    with st.spinner("Agent thinking..."):
        result = get_agent().invoke({
            "email": email_item,
            "compose_prompt": None,
            "compose_to": None,
            "compose_subject": None,
            "task": None,
            "category": None,
            "category_reason": None,
            "draft": None,
            "reply": None,
            "error": None,
        })
    return result
 
def run_compose(prompt, to, subject):
    with st.spinner("Drafting email..."):
        result = get_agent().invoke({
            "email": None,
            "compose_prompt": prompt,
            "compose_to": to,
            "compose_subject": subject,
            "task": None,
            "category": None,
            "category_reason": None,
            "draft": None,
            "reply": None,
            "error": None,
        })
    return result
 
# ─────────────────────────────────────────
#  Sidebar
# ─────────────────────────────────────────
with st.sidebar:
    st.markdown(
    '<h2 style="color:black;">✉️ Email Agent</h2>', 
    unsafe_allow_html=True
)
    st.divider()
 
    if st.button("✏️  Compose new email", use_container_width=True):
        st.session_state.view = "compose"
        st.session_state.compose_draft = ""
        st.rerun()
 
    st.markdown(' <h3 style="color:black;">Mailbox</h3>', unsafe_allow_html=True)
    unread = unread_count()
    inbox_label = f"📥  Inbox {'🔴' if unread > 0 else ''} ({unread} unread)"
 
    if st.button(inbox_label, use_container_width=True):
        st.session_state.view = "inbox"
        st.session_state.selected_email = None
        st.rerun()
 
    if st.button("🔄  Refresh inbox", use_container_width=True):
        st.session_state.inbox_loaded = False
        st.session_state.selected_email = None
        st.session_state.view = "inbox"
        st.rerun()
 
    if st.button(f"📤  Sent ({len(st.session_state.sent)})", use_container_width=True):
        st.session_state.view = "sent"
        st.session_state.selected_email = None
        st.rerun()
 
    st.divider()
    st.markdown(' <h3 style="color:black;"> Agent stats</h3>', unsafe_allow_html=True)
    st.markdown(f'<p style="color:black;">- Emails processed: <b>{len(st.session_state.ai_reply)}</b></p>', unsafe_allow_html=True)
    st.markdown(f'<p style="color:black;">- Drafts sent: <b>{len(st.session_state.sent)}</b></p>', unsafe_allow_html=True)
    st.markdown(f'<p style="color:black;">- Unread: <b>{unread}</b></p>', unsafe_allow_html=True)
 
    st.divider()
    gmail_user = os.getenv("GMAIL_USER", "")
    if gmail_user:
        st.caption(f"📬 Logged in as:\n{gmail_user}")
    else:
        st.warning("GMAIL_USER not set in .env")
 
# ─────────────────────────────────────────
#  Auto-load inbox on first visit
# ─────────────────────────────────────────
if not st.session_state.inbox_loaded and st.session_state.view == "inbox":
    load_inbox()
 
# ─────────────────────────────────────────
#  INBOX VIEW
# ─────────────────────────────────────────
if st.session_state.view == "inbox":
 
    if st.session_state.fetch_error:
        st.error(f"Could not connect to Gmail: {st.session_state.fetch_error}")
        st.info(
            "**Setup checklist:**\n"
            "1. Set `GMAIL_USER=you@gmail.com` in `.env`\n"
            "2. Set `GMAIL_APP_PASSWORD=xxxx xxxx xxxx xxxx` in `.env`\n"
            "3. Enable IMAP: Gmail → Settings → See all settings → Forwarding and POP/IMAP → Enable IMAP\n"
            "4. Generate App Password: myaccount.google.com/apppasswords (requires 2FA enabled)"
        )
        st.stop()
 
    col_list, col_detail = st.columns([1, 1.4], gap="large")
 
    with col_list:
        st.markdown("### Inbox")
        total = len(st.session_state.inbox)
        unread = unread_count()
        processed = len(st.session_state.ai_reply)
 
        st.markdown(f"""
        <div class="metric-row">
            <div class="metric-box"><div class="metric-num">{total}</div><div class="metric-label">Total</div></div>
            <div class="metric-box"><div class="metric-num">{unread}</div><div class="metric-label">Unread</div></div>
            <div class="metric-box"><div class="metric-num">{processed}</div><div class="metric-label">Processed</div></div>
        </div>
        """, unsafe_allow_html=True)
 
        if not st.session_state.inbox:
            st.info("Inbox is empty.")
        else:
            for email_item in st.session_state.inbox:
                unread_cls = "unread" if not email_item["read"] else ""
                preview = email_item["body"].replace("\n", " ")[:60] + "..."
                st.markdown(f"""
                <div class="email-card {unread_cls}">
                    <span class="email-time">{email_item['time']}</span>
                    <div class="sender-name">{email_item['from_name']}</div>
                    <div class="email-subject">{email_item['subject']}</div>
                    <div class="email-preview">{preview}</div>
                    {tag_html(email_item['tag'])}
                </div>
                """, unsafe_allow_html=True)
                if st.button("Open", key=f"open_{email_item['id']}", use_container_width=True):
                    st.session_state.selected_email = email_item["id"]
                    email_item["read"] = True
                    mark_as_read(email_item["id"])
                    st.rerun()
 
    with col_detail:
        if st.session_state.selected_email is None:
            st.markdown("### Email detail")
            st.info("Select an email from the inbox to view it.")
        else:
            email_item = next(
                (e for e in st.session_state.inbox if e["id"] == st.session_state.selected_email), None
            )
            if email_item:
                st.markdown(f"### {email_item['subject']}")
                st.caption(f"From: **{email_item['from_name']}** &lt;{email_item['from_email']}&gt; · {email_item['time']}")
                st.markdown(tag_html(email_item["tag"]), unsafe_allow_html=True)
                st.divider()
                st.text(email_item["body"])
                st.divider()
 
                st.markdown("#### 🤖 AI Agent reply")
                eid = email_item["id"]
 
                if eid not in st.session_state.ai_reply:
                    if st.button("✨ Generate auto-reply", type="primary", use_container_width=True):
                        result = run_auto_reply(email_item)
                        st.session_state.ai_reply[eid] = result.get("reply", "")
                        st.session_state.ai_category[eid] = {
                            "category": result.get("category", ""),
                            "reason": result.get("category_reason", ""),
                        }
                        st.rerun()
                else:
                    cat = st.session_state.ai_category.get(eid, {})
                    if cat.get("category"):
                        st.markdown(f"**Classified as:** {tag_html(cat['category'])}", unsafe_allow_html=True)
                        st.caption(cat.get("reason", ""))
 
                    reply_text = st.text_area(
                        "Edit reply before sending",
                        value=st.session_state.ai_reply[eid],
                        height=180,
                        key=f"reply_edit_{eid}",
                    )
 
                    c1, c2 = st.columns(2)
                    with c1:
                        if st.button("🔄 Regenerate", use_container_width=True):
                            del st.session_state.ai_reply[eid]
                            st.rerun()
                    with c2:
                        if st.button("📨 Send reply", type="primary", use_container_width=True):
                            result = send_email(
                                to=email_item["from_email"],
                                subject=f"Re: {email_item['subject']}",
                                body=reply_text,
                            )
                            if result["success"]:
                                st.session_state.sent.append({
                                    "to": email_item["from_name"],
                                    "to_email": email_item["from_email"],
                                    "subject": f"Re: {email_item['subject']}",
                                    "body": reply_text,
                                    "time": "Just now",
                                })
                                st.success(f"Reply sent to {email_item['from_name']} ({email_item['from_email']})")
                            else:
                                st.error(f"Failed to send: {result['error']}")
 
# ─────────────────────────────────────────
#  COMPOSE VIEW
# ─────────────────────────────────────────
elif st.session_state.view == "compose":
    st.markdown("### ✏️ Compose email")
    st.caption("Describe what you want to say — the agent will write it for you.")
    st.divider()
 
    col1, col2 = st.columns(2)
    with col1:
        to = st.text_input("To", placeholder="recipient@gmail.com")
    with col2:
        subject = st.text_input("Subject", placeholder="Email subject...")
 
    prompt = st.text_area(
        "What should the email say?",
        placeholder="e.g. Remind all staff about the Friday payroll cut-off...",
        height=100,
    )
 
    if st.button("✨ Generate draft", type="primary", use_container_width=True):
        if not prompt.strip():
            st.warning("Please enter a prompt.")
        else:
            result = run_compose(prompt, to, subject)
            st.session_state.compose_draft = result.get("draft", "")
 
    if st.session_state.compose_draft:
        st.divider()
        st.markdown("**Generated draft — edit before sending:**")
        draft_text = st.text_area(
            "Draft",
            value=st.session_state.compose_draft,
            height=220,
            label_visibility="collapsed",
        )
        c1, c2, c3 = st.columns(3)
        with c1:
            if st.button("🔄 Regenerate", use_container_width=True):
                st.session_state.compose_draft = ""
                st.rerun()
        with c2:
            if st.button("🗑️ Clear", use_container_width=True):
                st.session_state.compose_draft = ""
                st.rerun()
        with c3:
            if st.button("📨 Send email", type="primary", use_container_width=True):
                if not to or not subject:
                    st.warning("Please fill in To and Subject fields.")
                else:
                    result = send_email(to=to, subject=subject, body=draft_text)
                    if result["success"]:
                        st.session_state.sent.append({
                            "to": to, "to_email": to,
                            "subject": subject, "body": draft_text, "time": "Just now",
                        })
                        st.success(f"Email sent to {to}!")
                        st.session_state.compose_draft = ""
                        st.session_state.view = "sent"
                        st.rerun()
                    else:
                        st.error(f"Failed to send: {result['error']}")
 
# ─────────────────────────────────────────
#  SENT VIEW
# ─────────────────────────────────────────
elif st.session_state.view == "sent":
    st.markdown("### 📤 Sent emails")
    st.divider()
    if not st.session_state.sent:
        st.info("No emails sent this session.")
    else:
        for sent_email in reversed(st.session_state.sent):
            with st.expander(f"To: {sent_email['to']} — {sent_email['subject']} · {sent_email['time']}"):
                st.text(sent_email["body"])