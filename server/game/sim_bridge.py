"""docs/design/reference_sim.py を import する（複製しない。計算の正はあちら）"""
import json
import os
import sys

_HERE = os.path.dirname(__file__)
# Vercel bundle (tools/vercel/make_bundle.py) puts a copy in server/design. The repo keeps the source in docs/design.
_BUNDLED = os.path.abspath(os.path.join(_HERE, "..", "design"))
DESIGN_DIR = _BUNDLED if os.path.isdir(_BUNDLED) else os.path.abspath(os.path.join(_HERE, "..", "..", "docs", "design"))
if DESIGN_DIR not in sys.path:
    sys.path.insert(0, DESIGN_DIR)

import reference_sim as rs  # noqa: E402

P = rs.P
PART = rs.PART
LIMITS = rs.LIMITS
INSTRUMENTS = rs.INSTRUMENTS
SWITCHES = rs.SWITCHES
ATTITUDES = rs.ATTITUDES
Sim = rs.Sim
launch_check = rs.launch_check

with open(os.path.join(DESIGN_DIR, "houston_lines.json"), encoding="utf-8") as _f:
    _H = json.load(_f)
MILESTONE_LINES = {x["id"]: x for x in _H["milestone_lines"]}
REACTIVE_LINES = {x["id"]: x for x in _H["reactive_lines"]}

# 設計で個数を選べる部品（bus と軌道・ロケットを除く）
DESIGN_PARTS = [p["id"] for p in rs._PARTS["parts"] if "min" in p and p["id"] != "bus"]
TRAJECTORIES = [p["id"][len("trajectory_"):] for p in rs._PARTS["parts"] if p["id"].startswith("trajectory_")]
ROCKETS = [p["id"][len("rocket_"):] for p in rs._PARTS["parts"] if p["id"].startswith("rocket_")]
