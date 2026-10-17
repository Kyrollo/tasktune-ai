from langgraph.graph import StateGraph, END
from agent.state import AgentState
from agent.nodes import (
    router_node,
    route_decision,
    classify_node,
    auto_reply_node,
    compose_node,
)
 
 
def build_graph():
    """Build and compile the email agent LangGraph workflow."""
 
    graph = StateGraph(AgentState)
 
    graph.add_node("router", router_node)
    graph.add_node("classify", classify_node)
    graph.add_node("auto_reply", auto_reply_node)
    graph.add_node("compose", compose_node)
 
    graph.set_entry_point("router")
 
    graph.add_conditional_edges(
        "router",
        route_decision,
        {
            "auto_reply": "auto_reply",
            "compose": "compose",
            "classify": "classify",
        },
    )
 
    graph.add_edge("auto_reply", "classify")
    graph.add_edge("classify", END)
    graph.add_edge("compose", END)
 
    return graph.compile()