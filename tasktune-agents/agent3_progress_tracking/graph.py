"""Build and compile the Progress Tracking LangGraph."""

from __future__ import annotations

from typing import Any

from langgraph.graph import END, StateGraph

from .nodes import (
    analyze_progress,
    assess_risk,
    compile_output,
    generate_recommendations,
    predict_completion,
)
from .state import ProgressTrackingState, ProgressTrackingInput


def build_progress_tracking_graph() -> StateGraph:
    """Construct and compile the Progress Tracking state graph.

    Graph topology::

        START
          │
          ▼
        analyze_progress
          │
          ▼
        predict_completion
          │
          ▼
        assess_risk
          │
          ▼
        generate_recommendations
          │
          ▼
        compile_output
          │
          ▼
         END
    """
    builder = StateGraph(ProgressTrackingState)

    builder.add_node("analyze_progress", analyze_progress)
    builder.add_node("predict_completion", predict_completion)
    builder.add_node("assess_risk", assess_risk)
    builder.add_node("generate_recommendations", generate_recommendations)
    builder.add_node("compile_output", compile_output)

    builder.set_entry_point("analyze_progress")
    builder.add_edge("analyze_progress", "predict_completion")
    builder.add_edge("predict_completion", "assess_risk")
    builder.add_edge("assess_risk", "generate_recommendations")
    builder.add_edge("generate_recommendations", "compile_output")
    builder.add_edge("compile_output", END)

    return builder.compile()


def run_progress_tracking(input_data: dict[str, Any]) -> dict[str, Any]:
    """High-level helper: build the graph, run it, return the final output.

    Parameters
    ----------
    input_data:
        Dict matching the ``ProgressTrackingInput`` schema.

    Returns
    -------
    dict
        The ``final_output`` field from the completed state, matching
        ``{progress_summary, completion_prediction, blockers_analysis,
        recommendations, risk_level}``.
    """
    graph = build_progress_tracking_graph()
    result = graph.invoke({"input": input_data, "messages": []})
    return result["final_output"]