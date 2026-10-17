from typing import TypedDict, Optional, Literal
 
 
class Email(TypedDict):
    id: int
    from_name: str
    from_email: str
    subject: str
    body: str
    tag: str
    read: bool
    time: str
 
 
class AgentState(TypedDict):
    # Input
    email: Optional[Email]
    compose_prompt: Optional[str]
    compose_to: Optional[str]
    compose_subject: Optional[str]
 
    # Routing
    task: Optional[Literal["auto_reply", "compose", "classify"]]
 
    # Outputs
    category: Optional[str]
    category_reason: Optional[str]
    draft: Optional[str]
    reply: Optional[str]
    error: Optional[str]
 