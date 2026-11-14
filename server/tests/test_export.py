"""Unity のオフライン再生用の記録（tools/export_timeline.py）が reference_sim と合う"""
from game.sim_bridge import rs
from tools.export_timeline import CASES, record


def test_export_matches_reference():
    for fname, (_, design, events) in CASES.items():
        r, _ = record(design, events)
        assert r.game.result == rs.run(design, events), fname
        types = [row["msg"]["type"] for row in r.rows]
        assert types[0] == "welcome" and types[-1] == "report"
        ats = [row["at"] for row in r.rows]
        assert ats == sorted(ats)
