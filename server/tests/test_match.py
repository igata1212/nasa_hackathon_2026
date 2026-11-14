"""一致テスト：サーバーのゲームを直接進めた結果が reference_sim.run() と完全に同じ"""
import pytest

from game.sim_bridge import rs
from helpers import play

CASES = {name[:2]: (design, events) for name, design, events in rs.TESTS}


@pytest.mark.parametrize("case", ["T1", "T2", "T4", "T5"])
@pytest.mark.parametrize("n_players", [1, 2, 3, 4])
def test_same_as_reference(case, n_players):
    design, events = CASES[case]
    g = play(design, events, n_players)
    assert g.phase == "ending"
    assert g.result == rs.run(design, events)


def test_spec_table_values():
    """SPEC 10章の表（結末・電力・酸素・燃料・温度・科学・スコア）"""
    table = {
        "T1": ("ED-A", 51.1, 32.0, 30, 13.6, 3.375, 1260.6),
        "T2": ("ED-B", 13.1, 32.0, 45, 13.6, 1.0, 690.1),
        "T4": ("ED-C", 0, 38.3, 75, 22, 0, 100),
        "T5": ("ED-B", 39.1, 3.0, 45, 11.4, 3.375, 984.6),
    }
    for case, want in table.items():
        r = play(*CASES[case]).result
        got = (r["ending"], r["power"], r["oxygen"], r["fuel"], r["temp"], r["science"], r["score"])
        assert got == want, case


def test_t3_over_mass_stays_in_design():
    from game.room import Game
    g = Game("0000")
    p = g.add_player("a", 0)
    g.handle(p.id, {"type": "start"}, 0)
    design = CASES["T3"][0]
    for pid, n in design["parts"].items():
        g.handle(p.id, {"type": "design", "part": pid, "n": n}, 0)
    g.handle(p.id, {"type": "design", "trajectory": "science", "rocket": "small"}, 0)
    g.take_outbox()
    g.handle(p.id, {"type": "launch"}, 0)
    out = [m for _, m in g.take_outbox()]
    assert g.phase == "design"
    launch = [m for m in out if m.get("kind") == "launch"][0]
    assert launch["status"] == "over_mass" and launch["mass"] == 111
    assert any(m.get("id") == "H01" for m in out)
