"""R01 事後レポートのスコアの内訳（SPEC 8章）

合計は reference_sim の Sim.result() の score と一致する（tests/test_report.py で確認）。
式を変えるときは reference_sim と両方を直すこと。
"""
from .sim_bridge import LIMITS, P


def score_breakdown(sim, result, cost):
    if sim is None or result is None:
        return None
    survived = result["ending"] in ("ED-A", "ED-B")
    parts = {
        "ending": {"ED-A": P["score_ed_a"], "ED-B": P["score_ed_b"], "ED-C": 0}[result["ending"]],
        "science": result["science"] * P["score_per_science"],
        "resources": ((max(0, sim.power) + max(0, sim.oxygen) + max(0, sim.fuel)) * P["score_per_resource"]
                      if survived else 0),
        "teamwork": (min(sim.swaps, P["co2_filters"]) + (1 if sim.first_try else 0)) * P["score_teamwork"],
        "budget": max(0, LIMITS["budget"] - cost) * P["score_per_budget"],
        "all_requirements": P["score_all_requirements"] if all(result["requirements"].values()) else 0,
    }
    return {k: round(v, 1) for k, v in parts.items()}


