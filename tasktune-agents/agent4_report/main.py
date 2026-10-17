import asyncio
import sys
from datetime import date
from agents.progress_agent import build_progress_agent
from mock_data import COMPANY_ID, MANAGER_ID, MANAGER_EMAIL, MANAGER_NAME


async def run():
    print("🚀 Starting Progress Report Agent...")
    print("-" * 50)

    initial_state = {
        "company_id":    COMPANY_ID,
        "manager_id":    MANAGER_ID,
        "manager_email": MANAGER_EMAIL,
        "manager_name":  MANAGER_NAME,
        "run_date":      str(date.today()),
        "tasks_data":    [],
        "is_valid":      False,
        "analysis":      None,
        "report":        None,
        "email_subject": "",
        "email_body":    "",
        "email_sent":    False,
        "error":         None,
    }

    agent = build_progress_agent()
    final_state = await agent.ainvoke(initial_state)

    print("-" * 50)
    if final_state.get("error"):
        print(f"❌ Flow failed: {final_state['error']}")
    else:
        report = final_state["report"]
        print(f"✅ Done! Report: {report.title}")
        print(f"   Health: {final_state['analysis'].overall_health.upper()}")
        print(f"   Email sent: {final_state['email_sent']}")


if __name__ == "__main__":
    # Fix for Windows event loop issue
    if sys.platform == "win32":
        asyncio.set_event_loop_policy(asyncio.WindowsSelectorEventLoopPolicy())

    asyncio.run(run())