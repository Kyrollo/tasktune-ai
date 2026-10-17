"""Build and compile the Task Guidance LangGraph."""

from __future__ import annotations

from typing import Any, Literal

from langgraph.graph import END, START, StateGraph

from .nodes import (
    analyze_task,
    assess_risks,
    compile_output,
    define_success_criteria,
    detailed_guidance,
    quick_guidance,
)
from .state import TaskGuidanceState, TaskInput


def _route_by_complexity(state: TaskGuidanceState) -> Literal["quick_guidance", "detailed_guidance"]:
    """Conditional edge: pick the guidance depth based on task complexity."""
    if state.get("complexity") == "simple":
        return "quick_guidance"
    return "detailed_guidance"


def build_task_guidance_graph() -> StateGraph:
    """Construct and compile the Task Guidance state graph.

    Graph topology::

        START
          │
          ▼
        analyze_task
          │
          ├──(simple)──► quick_guidance ──┐
          │                               │
          └──(complex)─► detailed_guidance─┤
                                          │
                                          ▼
                                    assess_risks
                                          │
                                          ▼
                              define_success_criteria
                                          │
                                          ▼
                                    compile_output
                                          │
                                          ▼
                                         END
    """
    builder = StateGraph(TaskGuidanceState)

    builder.add_node("analyze_task", analyze_task)
    builder.add_node("quick_guidance", quick_guidance)
    builder.add_node("detailed_guidance", detailed_guidance)
    builder.add_node("assess_risks", assess_risks)
    builder.add_node("define_success_criteria", define_success_criteria)
    builder.add_node("compile_output", compile_output)

    builder.add_edge(START, "analyze_task")

    builder.add_conditional_edges(
        "analyze_task",
        _route_by_complexity,
        {
            "quick_guidance": "quick_guidance",
            "detailed_guidance": "detailed_guidance",
        },
    )

    builder.add_edge("quick_guidance", "assess_risks")
    builder.add_edge("detailed_guidance", "assess_risks")
    builder.add_edge("assess_risks", "define_success_criteria")
    builder.add_edge("define_success_criteria", "compile_output")
    builder.add_edge("compile_output", END)

    return builder.compile()


def run_task_guidance(task_input: dict[str, Any]) -> dict[str, Any]:
    """High-level helper: build the graph, run it, return the final output.

    Parameters
    ----------
    task_input:
        Dict matching the ``TaskInput`` schema (task_name, description, …).

    Returns
    -------
    dict
        The ``final_output`` field from the completed state, matching the PRD
        contract: ``{guidance_text, suggested_subtasks, estimated_time,
        resources, risk_factors}``.
    """
    graph = build_task_guidance_graph()
    result = graph.invoke({"task": TaskInput(**task_input)})
    return result["final_output"]
