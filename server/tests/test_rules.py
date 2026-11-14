"""同時押し・席の権限・台詞・レポートなど"""
import pytest

from game.room import Game, GameError
from game.seats import COMMS, LIFE, PILOT, PLANNER
from game.sim_bridge import P, rs
from game.report import score_breakdown
from helpers import new_game_in_flight, play

DESIGN = rs.DESIGN_SCIENCE


def advance_to(g, T0, t):
    while g.sim.t < t:
        g.update(T0 + g.sim.t + 1.0)


def ready_for_burn(g, owner, T0):
    """噴射できる状態（軌道修正・噴射姿勢・航法 ON）にして噴射窓まで進める"""
    advance_to(g, T0, P["m06_delay_s"])
    g.handle(owner[PILOT], {"cmd": "correction_burn", "seat": PILOT}, T0 + g.sim.t + 0.1)
    g.handle(owner[COMMS], {"cmd": "close_valve", "seat": LIFE, "tank": 2}, T0 + g.sim.t + 0.1) \
        if owner[COMMS] == owner[LIFE] else g.handle(owner[LIFE], {"cmd": "close_valve", "seat": LIFE, "tank": 2}, T0 + g.sim.t + 0.1)
    g.handle(owner[PILOT], {"cmd": "set_attitude", "seat": PILOT, "mode": "burn"}, T0 + g.sim.t + 0.1)
    advance_to(g, T0, g.sim.T_BURN_OPEN + 1)


def events(g, kind):
    return [m for _, m in g.take_outbox() if m.get("kind") == kind]


# ---------------------------------------------------------------- 同時押し
def test_ignite_together_within_window():
    g, owner, T0 = new_game_in_flight(DESIGN, 4)
    ready_for_burn(g, owner, T0)
    fuel = g.sim.fuel
    now = T0 + g.sim.t + 0.1
    g.take_outbox()
    g.handle(owner[PILOT], {"cmd": "ignite"}, now)
    g.handle(owner[COMMS], {"cmd": "ignite"}, now + P["press_window_s"])  # ちょうど幅いっぱい
    assert [e["status"] for e in events(g, "press")] == ["waiting", "together"]
    g.update(T0 + g.sim.t + 1.0)
    assert g.sim.burn_done
    assert g.sim.fuel == fuel - P["fuel_burn_" + g.sim.route]


def test_ignite_too_far_apart_costs_retry():
    g, owner, T0 = new_game_in_flight(DESIGN, 4)
    ready_for_burn(g, owner, T0)
    fuel = g.sim.fuel
    now = T0 + g.sim.t + 0.1
    g.handle(owner[PILOT], {"cmd": "ignite"}, now)
    g.handle(owner[COMMS], {"cmd": "ignite"}, now + P["press_window_s"] + 0.01)
    # 1回目（Pilot）は失敗として fuel_burn_retry、2回目（Comms）は待ちのまま次の tick で失敗
    g.update(now + 0.9)
    assert not g.sim.burn_done
    assert g.sim.fuel == fuel - P["fuel_burn_retry"]
    assert g.sim.first_try is False
    g.update(now + 1.9)
    assert g.sim.fuel == fuel - 2 * P["fuel_burn_retry"]


def test_ignite_alone_outside_window_no_penalty():
    g, owner, T0 = new_game_in_flight(DESIGN, 4)
    advance_to(g, T0, 50)
    fuel = g.sim.fuel
    g.handle(owner[PILOT], {"cmd": "ignite"}, T0 + g.sim.t + 0.1)
    advance_to(g, T0, 53)
    assert g.sim.fuel == fuel  # 噴射窓の外は reference_sim でも何も起きない


def test_swap_filter_together_and_alone():
    g, owner, T0 = new_game_in_flight(DESIGN, 4)
    advance_to(g, T0, 100)
    now = T0 + g.sim.t + 0.1
    g.handle(owner[PLANNER], {"cmd": "swap_filter"}, now)
    g.handle(owner[LIFE], {"cmd": "swap_filter"}, now + 0.2)
    g.update(T0 + g.sim.t + 1.0)
    assert g.sim.swaps == 1 and g.sim.filters == P["co2_filters"] - 1
    # Life Support だけ → 不成立、何も起きない
    now = T0 + g.sim.t + 0.1
    g.handle(owner[LIFE], {"cmd": "swap_filter"}, now)
    g.update(T0 + g.sim.t + 1.0)
    assert g.sim.swaps == 1
    # Life Support 以外の2人だけでも不成立
    now = T0 + g.sim.t + 0.1
    g.handle(owner[PILOT], {"cmd": "swap_filter"}, now)
    g.handle(owner[COMMS], {"cmd": "swap_filter"}, now)
    g.update(T0 + g.sim.t + 1.0)
    assert g.sim.swaps == 1


def test_solo_player_presses_both_seats():
    g, owner, T0 = new_game_in_flight(DESIGN, 1)
    ready_for_burn(g, owner, T0)
    now = T0 + g.sim.t + 0.1
    g.handle(owner[PILOT], {"cmd": "ignite", "seat": PILOT}, now)
    g.handle(owner[PILOT], {"cmd": "ignite", "seat": COMMS}, now)
    g.update(T0 + g.sim.t + 1.0)
    assert g.sim.burn_done


# ---------------------------------------------------------------- 席
def test_seat_assignment_by_player_count():
    for n, want in {1: [[PILOT, COMMS, LIFE, PLANNER]],
                    2: [[PILOT, PLANNER], [LIFE, COMMS]],
                    3: [[PILOT], [COMMS, PLANNER], [LIFE]],
                    4: [[PILOT], [COMMS], [LIFE], [PLANNER]]}.items():
        g = Game("0000")
        ps = [g.add_player("p", 0) for _ in range(n)]
        assert [p.seats for p in ps] == want
    with pytest.raises(GameError) as e:
        g.add_player("p5", 0)
    assert e.value.code == "room_full"


@pytest.mark.parametrize("seat,msg,code", [
    (COMMS, {"cmd": "set_attitude", "mode": "sun"}, "seat_not_allowed"),
    (PILOT, {"cmd": "switch", "name": "heater", "on": False}, "seat_not_allowed"),
    (COMMS, {"cmd": "switch", "name": "camera", "on": True}, "seat_not_allowed"),
    (PLANNER, {"cmd": "switch", "name": "nav", "on": False}, "seat_not_allowed"),
    (PILOT, {"cmd": "close_valve", "tank": 2}, "seat_not_allowed"),
    (LIFE, {"cmd": "ignite"}, "seat_not_allowed"),
    (PLANNER, {"cmd": "set_transmit", "on": True}, "seat_not_allowed"),
    (PILOT, {"cmd": "set_route", "route": "a"}, "seat_not_allowed"),
])
def test_permissions(seat, msg, code):
    g, owner, T0 = new_game_in_flight(DESIGN, 4)
    with pytest.raises(GameError) as e:
        g.handle(owner[seat], msg, T0 + 0.1)
    assert e.value.code == code


def test_not_your_seat_and_locked():
    g, owner, T0 = new_game_in_flight(DESIGN, 4)
    with pytest.raises(GameError) as e:
        g.handle(owner[PILOT], {"cmd": "switch", "name": "nav", "on": False, "seat": COMMS}, T0 + 0.1)
    assert e.value.code == "not_your_seat"
    with pytest.raises(GameError) as e:
        g.handle(owner[PLANNER], {"cmd": "set_route", "route": "a"}, T0 + 0.1)
    assert e.value.code == "locked"     # M08 より前
    advance_to(g, T0, P["m06_delay_s"])
    g.handle(owner[PLANNER], {"cmd": "set_route", "route": "a"}, T0 + g.sim.t + 0.1)


# ---------------------------------------------------------------- 台詞
def lines(g):
    return [(m["kind"], m["id"], m.get("t")) for _, m in g.take_outbox()
            if m.get("type") == "event" and m.get("kind") in ("houston", "warning")]


def test_houston_leak_repeats_and_milestones():
    g, owner, T0 = new_game_in_flight(DESIGN, 4)
    g.take_outbox()
    got = []
    while g.phase == "flight":
        g.update(T0 + g.sim.t + 1.0)
        got += lines(g)
    h07 = [t for k, i, t in got if i == "H07" and k == "houston"]
    rep = P["houston_leak_repeat_s"]
    assert h07[0] == rep and all(b - a == rep for a, b in zip(h07, h07[1:]))
    ids = [i for k, i, t in got if k == "houston"]
    for mid in ("M05", "M06", "M07", "M08", "M09"):
        assert mid in ids or mid == "M05"
    # 通信途絶中は声ではなく warning
    assert all(not (g.sim.T_LOS <= t < g.sim.T_AOS) for k, i, t in got if k == "houston" and i.startswith("H"))


def test_houston_comms_off_then_h27():
    g, owner, T0 = new_game_in_flight(DESIGN, 4)
    g.handle(owner[COMMS], {"cmd": "switch", "name": "comms", "on": False}, T0 + 0.1)
    g.take_outbox()
    got = []
    advance_to(g, T0, 10)
    got += lines(g)
    assert not [x for x in got if x[0] == "houston" and x[1].startswith("H")]
    assert ("warning", "H07", rs.P["houston_leak_repeat_s"]) in got
    g.handle(owner[COMMS], {"cmd": "switch", "name": "comms", "on": True}, T0 + g.sim.t + 0.1)
    advance_to(g, T0, 13)
    got = [x for x in lines(g) if x[0] == "houston" and x[1].startswith("H")]
    assert [x[1] for x in got[:2]] == ["H27", "H07"]


def test_houston_one_line_per_tick():
    g, owner, T0 = new_game_in_flight(DESIGN, 4)
    g.take_outbox()
    while g.phase == "flight":
        g.update(T0 + g.sim.t + 1.0)
        voiced = [x for x in lines(g) if x[0] == "houston" and x[1].startswith("H")]
        assert len(voiced) <= 1


# ---------------------------------------------------------------- 状態・レポート
def test_state_has_timeline_and_slow():
    g, owner, T0 = new_game_in_flight(DESIGN, 4)
    st = [m for _, m in g.take_outbox() if m.get("type") == "state"][-1]["state"]
    for k in ("route", "t", "t_los", "t_aos", "t_burn_open", "t_reentry", "slow_pct", "sw", "attitude"):
        assert k in st
    assert st["route"] is None and st["t_los"] == g.sim.T_LOS


def test_report_breakdown_sums_to_score_and_again():
    for name, design, ev in rs.TESTS:
        if name.startswith("T3"):
            continue
        g = play(design, ev)
        b = score_breakdown(g.sim, g.result, g.cost)
        assert round(sum(b.values()), 1) == g.result["score"], name
    g.handle(next(iter(g.players)), {"type": "next"}, 9999)
    assert g.phase == "report"
    rep = [m for _, m in g.take_outbox() if m.get("type") == "report"][0]
    assert len(rep["history"]) == g.sim.t + 1 and rep["commands"]
    g.handle(next(iter(g.players)), {"type": "again"}, 9999)
    assert g.phase == "design" and g.sim is None


def test_practice_times_out_and_stir_repeats():
    g = Game("0000", speed=1)
    p = g.add_player("a", 0)
    g.handle(p.id, {"type": "start"}, 0)
    g.handle(p.id, {"type": "launch"}, 0)
    assert g.phase == "practice"
    g.update(89.9)
    assert g.phase == "practice"
    g.update(90.0)
    assert g.phase == "stir"
    g.take_outbox()
    g.update(110.0)
    assert any(m.get("id") == "M04" and m.get("repeat") for _, m in g.take_outbox())


def test_requested_seats():
    g = Game("0000")
    human = g.add_player("human", 0)
    bot = g.add_player("bot", 0, seats=["pilot", "comms_power"], bot=True)
    assert bot.seats == [PILOT, COMMS] and sorted(human.seats) == [LIFE, PLANNER]
    h2 = g.add_player("human2", 0)
    assert sorted(human.seats + h2.seats) == [LIFE, PLANNER] and human.seats and h2.seats
    with pytest.raises(GameError) as e:
        g.add_player("bot2", 0, seats=["pilot"])
    assert e.value.code == "seat_taken"
    g2 = Game("0001")
    g2.add_player("bot", 0, seats=list((PILOT, COMMS, LIFE, PLANNER)))
    with pytest.raises(GameError) as e:
        g2.add_player("human", 0)
    assert e.value.code == "no_free_seat"
