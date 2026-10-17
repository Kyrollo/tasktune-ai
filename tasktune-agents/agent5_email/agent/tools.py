from langchain_core.tools import tool
 
 
@tool
def get_leave_policy() -> str:
    """Returns the company leave policy details."""
    return (
        "Annual leave: 21 days per year. "
        "Sick leave: 10 days per year. "
        "Leave requests must be submitted at least 3 days in advance via HR portal. "
        "Unused leave can be carried over up to 5 days to next year."
    )
 
 
@tool
def get_payroll_schedule() -> str:
    """Returns the payroll processing schedule."""
    return (
        "Payroll is processed on the last working day of each month. "
        "Cut-off for expense claims and overtime is 3 days before month end. "
        "Late submissions are processed in the following month's cycle."
    )
 
 
@tool
def get_remote_work_policy() -> str:
    """Returns the remote work / WFH policy."""
    return (
        "Employees may work remotely up to 3 days per week. "
        "Tuesday and Thursday are mandatory in-office days. "
        "Remote exceptions must be requested 48 hours in advance. "
        "All remote work must be approved by line manager."
    )
 
 
@tool
def get_it_onboarding_checklist() -> str:
    """Returns the IT onboarding checklist for new hires."""
    return (
        "New hire IT checklist: "
        "1. Corporate email account setup. "
        "2. Slack workspace invitation. "
        "3. VPN credentials and setup guide. "
        "4. Software licenses (role-dependent). "
        "5. Laptop configuration and MDM enrollment. "
        "6. Access permissions per department matrix."
    )
 
 
@tool
def get_hr_contacts() -> str:
    """Returns HR department contacts for escalation."""
    return (
        "HR Director: hr.director@company.com | ext. 101. "
        "Payroll: payroll@company.com | ext. 102. "
        "IT Support: it.support@company.com | ext. 103. "
        "People Ops: people@company.com | ext. 104."
    )
 
 
# All tools available to the agent
ALL_TOOLS = [
    get_leave_policy,
    get_payroll_schedule,
    get_remote_work_policy,
    get_it_onboarding_checklist,
    get_hr_contacts,
]
 
TOOLS_BY_NAME = {t.name: t for t in ALL_TOOLS}
 