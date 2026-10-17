import smtplib
import imaplib
import email
from email.mime.text import MIMEText
from email.mime.multipart import MIMEMultipart
from email.header import decode_header
from email.utils import parsedate_to_datetime
from datetime import datetime
import os
 
def _cfg():
    return os.getenv("GMAIL_USER", ""), os.getenv("GMAIL_APP_PASSWORD", "")
 
def _decode(value: str) -> str:
    parts = decode_header(value or "")
    out = []
    for part, charset in parts:
        if isinstance(part, bytes):
            out.append(part.decode(charset or "utf-8", errors="replace"))
        else:
            out.append(str(part))
    return "".join(out)
 
def _body(msg) -> str:
    if msg.is_multipart():
        for part in msg.walk():
            if part.get_content_type() == "text/plain" and "attachment" not in str(part.get("Content-Disposition", "")):
                return part.get_payload(decode=True).decode(part.get_content_charset() or "utf-8", errors="replace")
    else:
        return msg.get_payload(decode=True).decode(msg.get_content_charset() or "utf-8", errors="replace")
    return ""
 
def _fmt_time(date_str: str) -> str:
    try:
        dt = parsedate_to_datetime(date_str)
        diff = (datetime.now(dt.tzinfo) - dt).days
        if diff == 0: return dt.strftime("%I:%M %p")
        if diff == 1: return "Yesterday"
        if diff < 7:  return dt.strftime("%A")
        return dt.strftime("%b %d")
    except Exception:
        return date_str or ""
 
def _tag(subject: str, body: str) -> str:
    text = (subject + " " + body).lower()
    if any(w in text for w in ["leave", "vacation", "payroll", "salary", "holiday"]): return "HR"
    if any(w in text for w in ["access", "laptop", "vpn", "software", "password", "it support"]): return "IT"
    if any(w in text for w in ["invoice", "payment", "finance", "budget", "expense"]): return "Finance"
    if any(w in text for w in ["team", "cross", "sync", "meeting", "all staff", "department"]): return "Cross-dept"
    return "General"
 
def send_email(to: str, subject: str, body: str) -> dict:
    user, pwd = _cfg()
    if not user or not pwd:
        return {"success": False, "error": "GMAIL_USER or GMAIL_APP_PASSWORD not set in .env"}
    try:
        msg = MIMEMultipart("alternative")
        msg["Subject"] = subject
        msg["From"] = user
        msg["To"] = to
        msg.attach(MIMEText(body, "plain", "utf-8"))
        with smtplib.SMTP("smtp.gmail.com", 587) as s:
            s.ehlo(); s.starttls(); s.login(user, pwd)
            s.sendmail(user, to, msg.as_string())
        return {"success": True}
    except smtplib.SMTPAuthenticationError:
        return {"success": False, "error": "Auth failed — use a Gmail App Password, not your regular password. Generate at: myaccount.google.com/apppasswords"}
    except Exception as e:
        return {"success": False, "error": str(e)}
 
def fetch_inbox(max_emails: int = 4) -> list:
    user, pwd = _cfg()
    if not user or not pwd:
        return []
    mail = imaplib.IMAP4_SSL("imap.gmail.com", 993)
    try:
        mail.login(user, pwd)
    except imaplib.IMAP4.error as e:
        raise ConnectionError(f"IMAP login failed: {e}")
    mail.select("INBOX")
    _, data = mail.search(None, "ALL")
    ids = data[0].split()[-max_emails:][::-1]
    emails = []
    for mid in ids:
        _, msg_data = mail.fetch(mid, "(RFC822 FLAGS)")
        if not msg_data or not msg_data[0]: continue
        flags = msg_data[0][0].decode()
        is_read = "\\Seen" in flags
        msg = email.message_from_bytes(msg_data[0][1])
        raw_from = _decode(msg.get("From", ""))
        if "<" in raw_from:
            from_name  = raw_from[:raw_from.index("<")].strip().strip('"')
            from_email = raw_from[raw_from.index("<")+1:raw_from.index(">")].strip()
        else:
            from_name = from_email = raw_from
        subject = _decode(msg.get("Subject", "(No subject)"))
        body    = _body(msg).strip()
        emails.append({
            "id":         int(mid),
            "from_name":  from_name or from_email,
            "from_email": from_email,
            "subject":    subject,
            "body":       body,
            "tag":        _tag(subject, body),
            "read":       is_read,
            "time":       _fmt_time(msg.get("Date", "")),
        })
    mail.logout()
    return emails
 
def mark_as_read(mail_id: int) -> None:
    user, pwd = _cfg()
    if not user or not pwd: return
    try:
        mail = imaplib.IMAP4_SSL("imap.gmail.com", 993)
        mail.login(user, pwd)
        mail.select("INBOX")
        mail.store(str(mail_id), "+FLAGS", "\\Seen")
        mail.logout()
    except Exception:
        pass