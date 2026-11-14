"""1つの部屋のゲーム進行（WebSocket に依存しない）

段階：lobby → design → practice → stir → flight → ending → report → design …
（SPEC 2章・7章。M02 の打ち上げ判定は design の中の "launch" メッセージで行う）

時刻 now は実時間の秒（time.monotonic など）。ゲーム内の1秒 = 実時間 1/speed 秒。
同時押しの受付幅 press_window_s もゲーム内の秒として扱う（実時間 press_window_s / speed）。
speed=1 の本番では実時間 0.5 秒。早送りのテストでは幅も縮む（そうしないと点火の失敗が遅れて届き、
reference_sim と合わなくなる）。
"""
import copy
import secrets

from . import config as C
from .houston import Houston, line_event as reactive_event
from .report import score_breakdown
from .seats import DUAL, SEATS, assign, seats_for
from .sim_bridge import (ATTITUDES, DESIGN_PARTS, LIMITS, MILESTONE_LINES, P, PART,
                         ROCKETS, SWITCHES, TRAJECTORIES, Sim, launch_check, rs)

MAX_PLAYERS = 4
FLIGHT_CMDS = ("set_attitude", "correction_burn", "ignite", "close_valve", "swap_filter",
               "switch", "set_transmit", "set_route")


class GameError(Exception):
    def __init__(self, code, detail=""):
        super().__init__(code)
        self.code, self.detail = code, detail


def milestone_event(mid, t=None, **extra):
    src = MILESTONE_LINES[mid]
    ev = {"type": "event", "kind": "houston", "id": mid, "priority": "milestone",
          "text": src["line"], "text_ja": src.get("line_ja", "")}
    if t is not None:
        ev["t"] = t
    ev.update(extra)
    return ev


def default_design():
    parts = {pid: PART[pid]["min"] for pid in DESIGN_PARTS if PART[pid]["min"] > 0}
    return {"parts": parts, "trajectory": TRAJECTORIES[0], "rocket": ROCKETS[0]}


def design_totals(design):
    mass, cost, status = launch_check(design)
    n = design["parts"].get
    mass_limit, budget = LIMITS["rocket_%s_mass" % design["rocket"]], LIMITS["budget"]
    return {
        "mass": mass, "cost": cost, "status": status,
        "mass_limit": mass_limit, "budget": budget,
        # 残り（UI は引き算しない）。負なら超過
        "mass_left": mass_limit - mass, "budget_left": budget - cost,
        "over_mass": mass > mass_limit, "over_budget": cost > budget,
        "science_possible": any(n(pid, 0) > 0 for pid in rs.INSTRUMENTS),
        "capacity": P["power_per_battery"] * n("battery", 0),
        "oxygen": P["oxygen_start"] + P["oxygen_per_spare_tank"] * n("spare_o2_tank", 0),
        "fuel": P["fuel_start"] + P["fuel_per_extra_tank"] * n("extra_fuel_tank", 0),
    }


def slow_pct(sim):
    """SPEC 6章「操作が遅くなる」：max(寒さ, CO2) の割合（%）。長押しの時間は Unity 側"""
    cold = 0
    if sim.temp < P["temp_slower_c"]:
        cold = P["slow_colder_pct"]
    elif sim.temp < P["temp_slow_c"]:
        cold = P["slow_cold_pct"]
    co2 = P["slow_co2_pct"] if sim.co2 >= P["co2_slow"] else 0
    return max(cold, co2)


def thresholds():
    """UI のゲージ・警告の閾値。parameters.json の値と config.py の値をそのまま集める（Unity に数字を書かせない）。
    power_frac は容量に対する割合、ほかは単位つきの値（oxygen・fuel は量、co2 は %、temp は ℃）"""
    return {
        "power_frac": {"half": C.H_POWER_HALF, "low": C.H_POWER_LOW, "crit": C.H_POWER_CRIT},
        "oxygen": {"mid": C.H_OXYGEN_40, "low": C.H_OXYGEN_20},
        "fuel": {"low": C.H_FUEL_LOW},
        "co2": {"warn": P["co2_warn"], "slow": P["co2_slow"]},
        "temp": {"slow": P["temp_slow_c"], "slower": P["temp_slower_c"]},
        "end_min": {"oxygen": P["end_oxygen_min"], "power": P["end_power_min"], "temp": P["end_temp_min"]},
        "strict_margin": P["end_strict_margin"],
        "req_science_min": P["req_science_min"],
        "reentry_ready_s": P["reentry_ready_s"],
    }


def route_times(sim):
    """ルート A・B それぞれの再突入・着水の時刻（route を選ぶ前でも両方わかる）"""
    out = {}
    for r in ("a", "b"):
        re = sim.T_AOS + P["time_p4_return_%s_min" % r] * 60
        out["t_reentry_" + r] = re
        out["t_splashdown_" + r] = re + P["time_p5_reentry_min"] * 60
    return out


def science_now(sim, cost):
    """Req 2 の今の見込み：ここで観測と送信をやめて、無事に帰ってきたとしたときの science。
    result() を無事帰還の状態のコピーに使うので、計算は reference_sim のまま"""
    tmp = copy.copy(sim)
    tmp.ending = "ED-A"
    return tmp.result(cost)["science"]


def snapshot(sim, strict_reason=None, cost=None):
    out = {
        "t": sim.t, "capacity": sim.capacity, "power": sim.power, "oxygen": sim.oxygen,
        "fuel": sim.fuel, "temp": sim.temp, "co2": sim.co2, "filters": sim.filters,
        "leak_open": sim.leak_open, "leaked": sim.leaked, "sw": dict(sim.sw),
        "attitude": sim.attitude, "transmit": sim.transmit, "observed_s": dict(sim.observed_s),
        "data_collected": sim.data_collected, "data_sent": sim.data_sent,
        "correction_done": sim.correction_done, "route": sim.route, "burn_done": sim.burn_done,
        "first_try": sim.first_try, "strict": sim.strict, "swaps": sim.swaps, "ending": sim.ending,
        "has_signal": sim.has_signal(), "in_farside": sim.in_farside(),
        "t_los": sim.T_LOS, "t_aos": sim.T_AOS, "t_burn_open": sim.T_BURN_OPEN,
        "t_burn_close": sim.T_BURN_CLOSE, "t_reentry": sim.t_reentry(),
        "t_splashdown": sim.t_reentry() + P["time_p5_reentry_min"] * 60,
        "slow_pct": slow_pct(sim),
        "strict_reason": strict_reason,
        "thresholds": thresholds(),
    }
    out.update(route_times(sim))
    if cost is not None:
        out["science_now"] = science_now(sim, cost)
    return out


def history_row(sim):
    return {"t": sim.t, "power": round(sim.power, 2), "oxygen": round(sim.oxygen, 2),
            "fuel": round(sim.fuel, 2), "temp": round(sim.temp, 2), "co2": round(sim.co2, 2),
            "data_collected": round(sim.data_collected, 2), "data_sent": round(sim.data_sent, 2),
            "attitude": sim.attitude}


class Player:
    def __init__(self, pid, name):
        self.id, self.name = pid, name
        self.seats = []
        self.want = None     # 指定した席（bot_client --seats）
        self.bot = False
        self.connected = True

    def info(self):
        return {"id": self.id, "name": self.name, "seats": list(self.seats), "connected": self.connected,
                "bot": self.bot}


class Game:
    def __init__(self, code, speed=1.0, difficulty=1):
        self.code = code
        self.speed = float(speed)
        self.difficulty = difficulty
        self.players = {}            # 入室順
        self.phase = "lobby"
        self.design = default_design()
        self.outbox = []             # [(宛先 player_id か None=全員, メッセージ)]
        self._reset_flight()

    # ------------------------------------------------------------ 送信
    def send(self, msg, to=None):
        self.outbox.append((to, msg))

    def take_outbox(self):
        out, self.outbox = self.outbox, []
        return out

    def room_msg(self):
        return {"type": "room", "room": self.code, "phase": self.phase,
                "players": [p.info() for p in self.players.values()]}

    def design_msg(self):
        return {"type": "design", "design": copy.deepcopy(self.design), "totals": design_totals(self.design)}

    def sync_msgs(self):
        """入室・再接続したときにその人へ送るもの"""
        msgs = [self.room_msg(), self.design_msg()]
        if self.sim is not None:
            msgs.append({"type": "state", "t": self.sim.t, "state": self._snapshot()})
        if self.phase in ("ending", "report") and self.result:
            msgs.append(self.result_msg())
        if self.phase == "report":
            msgs.append(self.report_msg())
        return msgs

    def _gs(self, s):
        """ゲーム内の秒 → 実時間の秒"""
        return s / self.speed

    # ------------------------------------------------------------ 入退室
    def add_player(self, name, now, seats=None, bot=False):
        """seats を指定すると、その席だけを持つ（bot_client --seats で人数の足りない席を埋める用）。
        指定のない人には、残りの席を SPEC 6章「人数と席」の割り当てで配る"""
        if self.phase != "lobby":
            raise GameError("game_started", "途中からは入れない（player_id で再接続はできる）")
        if len(self.players) >= MAX_PLAYERS:
            raise GameError("room_full")
        if seats:
            seats = list(dict.fromkeys(seats))
            if any(s not in SEATS for s in seats):
                raise GameError("bad_args", "seats")
            taken = {x for p in self.players.values() if p.want for x in p.want}
            if taken & set(seats):
                raise GameError("seat_taken", ",".join(sorted(taken & set(seats))))
        free = [s for s in SEATS if s not in {x for p in self.players.values() if p.want for x in p.want}
                and s not in (seats or [])]
        if not seats and not free:
            raise GameError("no_free_seat")
        if seats and not free and any(not p.want for p in self.players.values()):
            raise GameError("no_free_seat", "ほかの人の席がなくなる")
        pid = secrets.token_hex(4)
        p = Player(pid, name or "player%d" % (len(self.players) + 1))
        p.want = seats or None
        p.bot = bool(bot)
        self.players[pid] = p
        self._assign_seats()
        self.send(self.room_msg())
        return p

    def reconnect(self, pid):
        p = self.players.get(pid)
        if p is None:
            raise GameError("unknown_player")
        p.connected = True
        self.send(self.room_msg())
        return p

    def disconnect(self, pid):
        p = self.players.get(pid)
        if p is None:
            return
        if self.phase == "lobby":
            del self.players[pid]
            self._assign_seats()
        else:
            p.connected = False  # 席は残す（同じ player_id で戻れる）
        self.send(self.room_msg())

    def _assign_seats(self):
        fixed = [p for p in self.players.values() if p.want]
        rest = [p for p in self.players.values() if not p.want]
        for p in fixed:
            p.seats = list(p.want)
        free = [s for s in SEATS if not any(s in p.want for p in fixed)]
        if not rest:
            return
        plan = [[s for s in seats if s in free] for seats in assign(len(rest))]
        if any(not x for x in plan) or sorted(sum(plan, [])) != sorted(free):
            plan = [free[i::len(rest)] for i in range(len(rest))]  # 割り当て表で配れないときは順に配る
        for p, seats in zip(rest, plan):
            p.seats = list(seats)

    def any_connected(self):
        return any(p.connected for p in self.players.values())

    # ------------------------------------------------------------ 段階
    def _set_phase(self, phase):
        self.phase = phase
        self.send(self.room_msg())

    def _reset_flight(self):
        self.sim = None
        self.houston = None
        self.cost = self.mass = None
        self.queue = []              # 次の tick で Sim に渡すコマンド（届いた順）
        self.pending = {c: {} for c in DUAL}   # 同時押しの片方：cmd → {seat: (実時刻, player_id)}
        self.next_tick = None
        self.history = []
        self.commands = []
        self.fired = set()
        self.result = None
        self.strict_reason = None
        self.practiced = set()
        self.deadline = None
        self.last_design_status = None

    def handle(self, pid, msg, now):
        """クライアントからのメッセージ1つ。エラーは GameError"""
        p = self.players.get(pid)
        if p is None:
            raise GameError("unknown_player")
        if "cmd" in msg:
            return self._on_cmd(p, msg, now)
        kind = msg.get("type")
        if kind == "start":
            self._expect("lobby")
            self._set_phase("design")
            self.send(milestone_event("M01"))
            self.send(self.design_msg())
        elif kind == "design":
            self._expect("design")
            self._on_design(msg)
        elif kind == "launch":
            self._expect("design")
            self._on_launch(now)
        elif kind == "next":
            self._expect("ending")
            self._to_report()
        elif kind == "again":
            self._expect("report")
            self._reset_flight()
            self._set_phase("design")
            self.send(milestone_event("M01"))
            self.send(self.design_msg())
        elif kind == "ping":
            self.send({"type": "pong", "t": msg.get("t")}, to=pid)
        else:
            raise GameError("unknown_type", str(kind))

    def _expect(self, phase):
        if self.phase != phase:
            raise GameError("wrong_phase", "now=%s need=%s" % (self.phase, phase))

    # ------------------------------------------------------------ M01・M02
    def _on_design(self, msg):
        d = self.design
        if "part" in msg:
            pid = msg["part"]
            if pid not in DESIGN_PARTS:
                raise GameError("bad_args", "part")
            n = d["parts"].get(pid, 0)
            if "n" in msg:
                n = int(msg["n"])
            else:
                n += int(msg.get("delta", 0))
            n = max(PART[pid]["min"], min(PART[pid]["max"], n))
            if n:
                d["parts"][pid] = n
            else:
                d["parts"].pop(pid, None)
        if "trajectory" in msg:
            if msg["trajectory"] not in TRAJECTORIES:
                raise GameError("bad_args", "trajectory")
            d["trajectory"] = msg["trajectory"]
        if "rocket" in msg:
            if msg["rocket"] not in ROCKETS:
                raise GameError("bad_args", "rocket")
            d["rocket"] = msg["rocket"]
        dm = self.design_msg()
        self.send(dm)
        # H01・H02：オーバーになった瞬間に1回
        status = dm["totals"]["status"]
        if status != self.last_design_status:
            if status == "over_mass":
                self.send(reactive_event("H01"))
            elif status == "over_budget":
                self.send(reactive_event("H02"))
        self.last_design_status = status

    def _on_launch(self, now):
        mass, cost, status = launch_check(self.design)
        self.send({"type": "event", "kind": "launch", "status": status, "mass": mass, "cost": cost})
        if status != "ok":
            self.send(reactive_event("H01" if status == "over_mass" else "H02"))
            return
        self.mass, self.cost = mass, cost
        self.send(milestone_event("M02"))
        self._set_phase("practice")
        self.send(milestone_event("M03"))
        self.practiced = set()
        self.deadline = now + self._gs(C.PRACTICE_MAX_S)

    def _to_stir(self, now):
        self._set_phase("stir")
        self.send(milestone_event("M04"))
        self.deadline = now + self._gs(C.STIR_REPEAT_S)

    def _start_flight(self, now):
        self.sim = Sim(self.design, self.difficulty)
        self.houston = Houston(self.sim)
        self._set_phase("flight")
        self.send(milestone_event("M05", t=0))
        self.next_tick = now + self._gs(1)
        self.history = [history_row(self.sim)]
        self._broadcast_state()

    # ------------------------------------------------------------ コマンド
    def _pick_seat(self, p, cmd, args, msg):
        allowed = seats_for(cmd, args)
        seat = msg.get("seat")
        if seat is not None:
            if seat not in SEATS:
                raise GameError("bad_args", "seat")
            if seat not in p.seats:
                raise GameError("not_your_seat", seat)
            if seat not in allowed:
                raise GameError("seat_not_allowed", "%s cannot %s" % (seat, cmd))
            return seat
        for s in p.seats:
            if s in allowed:
                return s
        raise GameError("seat_not_allowed", cmd)

    @staticmethod
    def _args(cmd, msg):
        try:
            if cmd == "set_attitude":
                if msg["mode"] not in ATTITUDES:
                    raise ValueError
                return {"mode": msg["mode"]}
            if cmd == "switch":
                if msg["name"] not in SWITCHES or not isinstance(msg["on"], bool):
                    raise ValueError
                return {"name": msg["name"], "on": msg["on"]}
            if cmd == "set_transmit":
                if not isinstance(msg["on"], bool):
                    raise ValueError
                return {"on": msg["on"]}
            if cmd == "set_route":
                if msg["route"] not in ("a", "b"):
                    raise ValueError
                return {"route": msg["route"]}
            if cmd == "close_valve":
                if msg["tank"] not in C.TANKS:
                    raise ValueError
                return {"tank": msg["tank"]}
        except (KeyError, ValueError, TypeError):
            raise GameError("bad_args", cmd)
        return {}

    def _on_cmd(self, p, msg, now):
        cmd = msg["cmd"]
        if cmd == "stir":
            self._expect("stir")
            self._pick_seat(p, cmd, {}, msg)
            self._start_flight(now)
            return
        if cmd not in FLIGHT_CMDS:
            raise GameError("unknown_cmd", str(cmd))
        args = self._args(cmd, msg)
        seat = self._pick_seat(p, cmd, args, msg)
        if self.phase == "practice":
            # M03：練習。リソースは減らさない。各席が1回ずつ使えば次へ
            self.practiced.add(seat)
            self.send({"type": "event", "kind": "practice", "seat": seat, "cmd": cmd,
                       "done": sorted(self.practiced)})
            if self.practiced >= set(SEATS):
                self._to_stir(now)
            return
        self._expect("flight")
        if cmd in ("correction_burn", "set_route") and self.sim.t < P["m06_delay_s"]:
            raise GameError("locked", "%s は M06 のあと" % cmd)  # M07・M08 で解禁
        if cmd in DUAL:
            self._press(cmd, seat, p.id, now)
        else:
            self.queue.append((seat, p.id, cmd, args))

    def _press(self, cmd, seat, pid, now):
        """同時押し：2人の押下が届いた時刻の差が press_window_s 以内なら成立（_expire で幅の外を落とす）"""
        self._expire(now)
        need, partners = DUAL[cmd]
        pend = self.pending[cmd]
        mates = [s for s in (partners if seat == need else (need,)) if s in pend and s != seat]
        if mates:
            mate = mates[0]
            _, mate_pid = pend.pop(mate)
            pend.clear()
            args = {"together": True} if cmd == "ignite" else {}
            self.queue.append(("%s+%s" % (need, mate if seat == need else seat), pid, cmd, args))
            self.send({"type": "event", "kind": "press", "cmd": cmd, "status": "together",
                       "seats": sorted([seat, mate])})
        else:
            pend[seat] = (now, pid)
            self.send({"type": "event", "kind": "press", "cmd": cmd, "status": "waiting",
                       "seats": [seat], "window_s": P["press_window_s"]})

    def _expire(self, now):
        for cmd, pend in self.pending.items():
            gone = [s for s, (t0, _) in pend.items() if now - t0 > self._gs(P["press_window_s"])]
            if not gone:
                continue
            for s in gone:
                _, pid = pend.pop(s)
            self.send({"type": "event", "kind": "press", "cmd": cmd, "status": "failed",
                       "seats": sorted(gone)})
            if cmd == "ignite":  # 成立しなかった点火：reference_sim の ignite(False)（fuel_burn_retry）
                self.queue.append((gone[0], pid, cmd, {"together": False}))

    def _apply(self, seat, pid, cmd, args):
        sim = self.sim
        ok = None
        if cmd == "close_valve":
            if args["tank"] == C.LEAKING_TANK:
                sim.close_valve()
                ok = True
            else:
                self.houston.push_event("H08")
                ok = False
        elif cmd == "ignite":
            ok = sim.ignite(args["together"])
        else:
            fn = getattr(sim, cmd)
            if cmd == "switch":
                ok = fn(args["name"], args["on"])
            elif cmd in ("set_transmit",):
                fn(args["on"])
            elif cmd == "set_route":
                fn(args["route"])
            elif cmd == "set_attitude":
                fn(args["mode"])
            else:
                ok = fn()
        rec = {"t": sim.t, "seat": seat, "player": pid, "cmd": cmd, "args": args, "ok": ok}
        self.commands.append(rec)
        self.send(dict(rec, type="event", kind="cmd"))

    # ------------------------------------------------------------ 時間
    def update(self, now):
        if self.phase == "practice" and now >= self.deadline:
            self._to_stir(now)
        elif self.phase == "stir" and now >= self.deadline:
            self.send(milestone_event("M04", repeat=True))
            self.deadline = now + self._gs(C.STIR_REPEAT_S)
        elif self.phase == "flight":
            while self.phase == "flight" and now >= self.next_tick:
                tick_at = self.next_tick
                self.next_tick += self._gs(1)
                self._tick(tick_at)
        elif self.phase == "ending" and now >= self.deadline:
            self._to_report()

    def _tick(self, now):
        sim = self.sim
        self._expire(now)
        queue, self.queue = self.queue, []
        for item in queue:
            self._apply(*item)
        strict_before, t_before = sim.strict, sim.t
        sim.step()
        if sim.strict and not strict_before:  # sim は理由を持たないので、strict になった瞬間の時刻で見分ける
            self.strict_reason = "missed_burn" if t_before == sim.T_BURN_CLOSE else "reentry_not_ready"
        self._milestones()
        for ev in self.houston.on_tick(sim):
            self.send(ev)
        if self.history[-1]["t"] != sim.t:  # 着水の step は t を進めない
            self.history.append(history_row(sim))
        self._broadcast_state()
        if sim.ending:
            self._finish(now)

    def _broadcast_state(self):
        self.send({"type": "state", "t": self.sim.t, "state": self._snapshot()})

    def _snapshot(self):
        return snapshot(self.sim, self.strict_reason, self.cost)

    def _milestones(self):
        sim, t = self.sim, self.sim.t
        want = []
        if t >= P["m06_delay_s"]:
            want.append("M06")
            if self.design["trajectory"] == "science":
                want.append("M07")
            want.append("M08")
        if t >= sim.T_LOS - C.M09_LEAD_S:
            want.append("M09")
        if t >= sim.T_AOS:
            want.append("M10")
        if t >= sim.T_BURN_OPEN:
            want.append("M11")
        if t >= sim.t_reentry():
            want.append("M12")
        for mid in want:
            if mid not in self.fired:
                self.fired.add(mid)
                self.send(milestone_event(mid, t=t))

    def _finish(self, now):
        out = self.sim.result(self.cost)
        out["mass"], out["cost"] = self.mass, self.cost
        self.result = out
        self.next_tick = None
        self._set_phase("ending")
        self.send(milestone_event(out["ending"], t=self.sim.t))
        self.send(self.result_msg())
        self.deadline = now + self._gs(C.ENDING_HOLD_S)

    def result_msg(self):
        """result は reference_sim の result() のまま。strict は外に並べる（噴射窓を逃した／再突入の準備不足）"""
        return {"type": "result", "result": self.result, "strict": self.sim.strict,
                "strict_reason": self.strict_reason}

    def report_msg(self):
        return {"type": "report", "result": self.result, "strict": self.sim.strict,
                "strict_reason": self.strict_reason, "design": copy.deepcopy(self.design),
                "score_breakdown": score_breakdown(self.sim, self.result, self.cost),
                "history": self.history, "commands": self.commands}

    def _to_report(self):
        self._set_phase("report")
        self.send(milestone_event("R01"))
        self.send(self.report_msg())
