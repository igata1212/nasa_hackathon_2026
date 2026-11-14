"""テスト用：WebSocket を通さずに Game を直接進める"""
from game.room import Game
from game.seats import COMMS, DUAL, LIFE, PILOT, PLANNER, seats_for
from game.sim_bridge import rs

from tools.bot_client import to_msgs  # noqa: F401


def new_game_in_flight(design, n_players=1, speed=1.0):
    g = Game("0000", speed=speed)
    ps = [g.add_player("p%d" % i, 0.0) for i in range(n_players)]
    owner = {s: p.id for p in ps for s in p.seats}
    g.handle(ps[0].id, {"type": "start"}, 0.0)
    for pid, n in design["parts"].items():
        g.handle(ps[0].id, {"type": "design", "part": pid, "n": n}, 0.0)
    g.handle(ps[0].id, {"type": "design", "trajectory": design["trajectory"], "rocket": design["rocket"]}, 0.0)
    g.handle(ps[0].id, {"type": "launch"}, 0.0)
    assert g.phase == "practice", g.phase
    for s in (PILOT, COMMS, LIFE, PLANNER):
        g.handle(owner[s], {"cmd": "set_attitude" if s == PILOT else
                            {"comms_power": "set_transmit", "life_support": "close_valve",
                             "mission_planner": "set_route"}[s],
                            "seat": s, "mode": "sun", "on": True, "tank": 2, "route": "a"}, 0.0)
    assert g.phase == "stir", g.phase
    T0 = 100.0
    g.handle(owner[LIFE], {"cmd": "stir", "seat": LIFE}, T0)
    assert g.phase == "flight"
    return g, owner, T0


def play(design, events, n_players=1):
    """events は reference_sim と同じ形。時刻 k のコマンドは、tick k-1 と tick k の間に届く"""
    g, owner, T0 = new_game_in_flight(design, n_players)
    queue = sorted(events, key=lambda e: e[0])
    k = 0
    while g.phase == "flight":
        while queue and queue[0][0] <= g.sim.t:
            _, name, args = queue.pop(0)
            for m in to_msgs(name, args):
                g.handle(owner[m["seat"]], m, T0 + g.sim.t + 0.1)
        g.update(T0 + g.sim.t + 1.0)
        k += 1
        assert k < 2000
    return g


def reference(design, events):
    return rs.run(design, events)
