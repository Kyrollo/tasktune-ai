"""
Run Agent 4 (Report Agent) server:
    python -m agent4_Report_Agent
"""

import uvicorn
import os
import sys
from pathlib import Path

# Make sure parent dir is on path
_agents_dir = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(_agents_dir))
sys.path.insert(0, str(Path(__file__).resolve().parent))

from agent4_Report_Agent.server import app  # noqa: E402

if __name__ == "__main__":
    port = int(os.environ.get("AGENT_PORT", 8004))
    print(f"🚀 Report Agent API running on http://localhost:{port}")
    uvicorn.run(app, host="0.0.0.0", port=port)
