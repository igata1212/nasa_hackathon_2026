"""ヒューストンの状況に反応する台詞（SPEC 9章、houston_lines.json の reactive_lines）

実装済み：critical と high すべて（H01 H02 H07 H08 H09 H11 H12 H14 H16 H18 H24 H25）と
         H27（通信 ON に戻したとき。SPEC 9章のルール）。
閾値だけで決まる medium（H10 H13 H15 H17 H26）も書いてあるが、config.HOUSTON_PRIORITIES で
今は止めている（今日は critical と high だけ。足すなら "medium" を加える）。
未実装は README の TODO を見る。
"""
from . import config as C
from .seats import SHIP_SWITCHES
from .sim_bridge import P, REACTIVE_LINES

RANK = {"critical": 0, "high": 1, "medium": 2, "low": 3}


def line_event(lid, t=None, kind="houston", **extra):
    src = REACTIVE_LINES[lid]
    ev = {"type": "event", "kind": kind, "id": lid, "priority": src["priority"],
          "text": src["line"], "text_ja": src.get("line_ja", "")}
    if t is not None:
        ev["t"] = t
    ev.update(extra)
    return ev


def thresholds(sim):
    """閾値で決まる台詞：ID → 今その状態か"""
    cap = sim.capacity
    return {
        "H10": sim.power < cap * C.H_POWER_HALF,
        "H11": sim.power < cap * C.H_POWER_LOW,
        "H12": sim.power < cap * C.H_POWER_CRIT,
        "H13": sim.oxygen < C.H_OXYGEN_40,
        "H14": sim.oxygen < C.H_OXYGEN_20,
        "H15": sim.temp < P["temp_slow_c"],
        "H16": sim.temp < P["temp_slower_c"],
        "H17": sim.co2 > P["co2_warn"],
        "H18": sim.co2 >= P["co2_slow"],
        "H26": sim.fuel < C.H_FUEL_LOW,
    }


class Houston:
    """1秒ごとに on_tick(sim) を呼ぶ。戻り値は送るイベントのリスト"""

    def __init__(self, sim):
        self.prev = thresholds(sim)
        self.prev_comms = sim.sw["comms"]
        self.last = {}          # ID → 最後に声で流した t
        self.queue = []         # [(rank, 順番, ID, 積んだ t)]
        self.seq = 0
        self.extra = []         # コマンドから来た台詞（H08 など）

    def push_event(self, lid):
        self.extra.append(lid)

    def _cool(self, lid, t):
        wait = P["houston_leak_repeat_s"] if lid == "H07" else P["houston_cooldown_s"]
        return lid not in self.last or t - self.last[lid] >= wait

    def _enqueue(self, lid, t, front=False):
        if any(q[2] == lid for q in self.queue):
            return
        rank = -1 if front else RANK[REACTIVE_LINES[lid]["priority"]]
        self.seq += 1
        self.queue.append((rank, self.seq, lid, t))

    def on_tick(self, sim):
        t = sim.t
        out = []
        now = thresholds(sim)
        cands = [lid for lid, v in now.items() if v and not self.prev[lid]]
        self.prev = now

        # H07：漏れが続いている。houston_leak_repeat_s ごとに繰り返す
        # （leak_open でも漏れた量が oxygen_leak_max に達したら減らないので出さない）
        if sim.leak_open and sim.leaked < P["oxygen_leak_max"] and t >= P["houston_leak_repeat_s"]:
            cands.append("H07")
        # H09：事故から30秒、船のスイッチがすべて ON のまま
        if t == C.H09_AFTER_S and all(sim.sw[k] for k in SHIP_SWITCHES):
            cands.append("H09")
        # H24・H25：噴射窓の30秒前
        if t == sim.T_BURN_OPEN - C.WARN_BEFORE_BURN_S:
            if not sim.sw["nav"]:
                cands.append("H24")
            if sim.route is None:
                cands.append("H25")
        cands += self.extra
        self.extra = []
        cands = [lid for lid in cands if REACTIVE_LINES[lid]["priority"] in C.HOUSTON_PRIORITIES]

        voice_ok = sim.has_signal() and sim.sw["comms"]
        if not voice_ok:
            reason = "no_signal" if not sim.has_signal() else "comms_off"
            for lid in cands:
                out.append(line_event(lid, t, kind="warning", reason=reason))
            self.queue = []
        else:
            # 通信を ON に戻した：H27、続けていちばん優先度の高い警告
            if not self.prev_comms:
                self._enqueue("H27", t, front=True)
                active = [lid for lid, v in now.items()
                          if v and REACTIVE_LINES[lid]["priority"] in C.HOUSTON_PRIORITIES]
                if sim.leak_open and sim.leaked < P["oxygen_leak_max"]:
                    active.append("H07")
                if active:
                    best = min(active, key=lambda x: RANK[REACTIVE_LINES[x]["priority"]])
                    self.last.pop(best, None)  # 直後に流すのでクールダウンを外す
                    self._enqueue(best, t)
            for lid in cands:
                if self._cool(lid, t):
                    self._enqueue(lid, t)
            # 古くなったもの（クールダウンより前に積んだもの）は捨てる
            self.queue = [q for q in self.queue if t - q[3] <= P["houston_cooldown_s"]]
            # 一度に流すのは1本。優先度の高い順
            if self.queue:
                self.queue.sort()
                _, _, lid, _ = self.queue.pop(0)
                if lid == "H27" or self._cool(lid, t):
                    self.last[lid] = t
                    out.append(line_event(lid, t))
        self.prev_comms = sim.sw["comms"]
        return out
