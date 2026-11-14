#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Mission Return 参照シミュレーション（1秒刻み）

Unity (C#) に移植するための基準実装。SPEC.md の疑似コードと同じ内容。
数値はすべて parameters.json / parts.json から読む（コードに数値を書かない）。
時刻 t は「事故（M05）からの経過秒」。事故より前はリソースを消費しない。

使い方:  python3 reference_sim.py
"""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))


def _load(name):
    with open(os.path.join(HERE, name), encoding="utf-8") as f:
        return json.load(f)


P = {p["key"]: p["value"] for p in _load("parameters.json")["params"]}
_PARTS = _load("parts.json")
PART = {p["id"]: p for p in _PARTS["parts"]}
LIMITS = _PARTS["limits"]

INSTRUMENTS = ("camera", "spectrometer", "radar")
SWITCHES = ("heater", "lights", "nav", "comms") + INSTRUMENTS
ATTITUDES = ("earth", "moon", "sun", "burn", "heatshield")


# ---------------------------------------------------------------- 設計フェーズ
def launch_check(design):
    """M02: 質量と予算の検証。戻り値 (mass, cost, status)"""
    mass, cost = PART["bus"]["mass"], PART["bus"]["cost"]
    for pid, n in design["parts"].items():
        part = PART[pid]
        assert part["min"] <= n <= part["max"], "個数が範囲外: %s" % pid
        mass += part["mass"] * n
        cost += part["cost"] * n
    cost += PART["rocket_" + design["rocket"]]["cost"]
    limit = LIMITS["rocket_%s_mass" % design["rocket"]]
    if mass > limit:
        status = "over_mass"
    elif cost > LIMITS["budget"]:
        status = "over_budget"
    else:
        status = "ok"
    return mass, cost, status


# ---------------------------------------------------------------- 運用フェーズ
class Sim:
    def __init__(self, design, difficulty=1):
        self.design = design
        self.mult = P["difficulty_%d" % difficulty]

        # --- リソース（事故直後の値）
        self.capacity = P["power_per_battery"] * self.n("battery")
        self.power = self.capacity - P["power_accident_loss"]
        self.oxygen = P["oxygen_start"] + P["oxygen_per_spare_tank"] * self.n("spare_o2_tank")
        self.fuel = P["fuel_start"] + P["fuel_per_extra_tank"] * self.n("extra_fuel_tank")
        self.temp = P["temp_start"]
        self.co2 = P["co2_start"]
        self.filters = P["co2_filters"]

        # --- 機器の状態
        self.leak_open = True
        self.leaked = 0.0
        self.sw = {k: k in ("heater", "lights", "nav", "comms") for k in SWITCHES}
        self.attitude = "earth"
        self.transmit = False

        # --- 科学データ
        self.observed_s = {i: 0 for i in INSTRUMENTS}
        self.data_collected = 0.0
        self.data_sent = 0.0

        # --- 軌道まわり
        self.correction_done = design["trajectory"] != "science"
        self.route = None
        self.burn_done = False
        self.first_try = None
        self.strict = False  # 噴射窓を逃した／再突入の準備不足

        self.swaps = 0
        self.t = 0
        self.ending = None

        # --- タイムライン（秒）
        self.T_LOS = P["time_p2_accident_min"] * 60
        self.T_AOS = self.T_LOS + P["time_p3_farside_min"] * 60
        self.T_BURN_OPEN = self.T_AOS + P["burn_window_open_after_aos_s"]
        self.T_BURN_CLOSE = self.T_BURN_OPEN + P["burn_window_s"]

    def n(self, pid):
        return self.design["parts"].get(pid, 0)

    def t_reentry(self):
        route = self.route or "b"
        return self.T_AOS + P["time_p4_return_%s_min" % route] * 60

    def in_farside(self):
        return self.T_LOS <= self.t < self.T_AOS

    def has_signal(self):
        return self.t < self.T_LOS or self.T_AOS <= self.t < self.t_reentry()

    # ------------------------------------------------------------ コマンド
    def close_valve(self):
        """Life Support: 漏れているタンクのバルブを閉じる"""
        self.leak_open = False

    def switch(self, name, on):
        """Comms & Power / Mission Planner: 各システムの ON/OFF"""
        if name in INSTRUMENTS and self.n(name) == 0:
            return False
        if name == "nav" and on and not self.sw["nav"]:
            self.power -= P["power_nav_restart"]
        self.sw[name] = on
        return True

    def set_attitude(self, mode):
        """Pilot: 姿勢の変更（変更ごとに燃料を消費）"""
        assert mode in ATTITUDES
        if mode != self.attitude:
            self.fuel -= P["fuel_attitude_change"]
            self.attitude = mode

    def correction_burn(self):
        """Pilot: 自由帰還軌道に戻す噴射（観測向き軌道のみ）"""
        if self.correction_done or not self.sw["nav"]:
            return False
        self.fuel -= P["fuel_correction_burn"]
        if self.t >= self.T_LOS:  # 月の裏側に入る前に済ませなかった
            self.fuel -= P["fuel_burn_retry"]
        self.correction_done = True
        return True

    def set_route(self, route):
        """Mission Planner: 帰還ルート（点火まで変更可）"""
        if not self.burn_done:
            self.route = route

    def swap_filter(self):
        """Life Support + もう1人: CO2フィルター交換（同時押しは成立済みとして扱う）"""
        if self.filters <= 0:
            return False
        self.filters -= 1
        self.swaps += 1
        self.co2 = P["co2_start"]
        return True

    def set_transmit(self, on):
        """Comms & Power: データ送信の開始／停止"""
        self.transmit = on

    def ignite(self, together=True):
        """Pilot + Comms & Power: 帰還噴射。together=False は同時押しの失敗"""
        if self.burn_done or not (self.T_BURN_OPEN <= self.t < self.T_BURN_CLOSE):
            return False
        if not (self.sw["nav"] and self.attitude == "burn" and self.correction_done):
            return False
        if self.route is None:
            self.route = "a" if self.fuel >= P["fuel_burn_a"] else "b"
        need = P["fuel_burn_%s" % self.route]
        if self.fuel < need:
            return False
        if not together:
            self.fuel -= P["fuel_burn_retry"]
            self.first_try = False
            return False
        self.fuel -= need
        self.burn_done = True
        if self.first_try is None:
            self.first_try = True
        return True

    # ------------------------------------------------------------ 1秒ごとの更新
    def step(self):
        if self.t == self.T_BURN_CLOSE and not self.burn_done:
            self._missed_burn()
            if self.ending:
                return
        if self.t == self.t_reentry() + P["reentry_ready_s"]:
            if not (self.attitude == "heatshield" and self.sw["nav"]):
                self.strict = True
        if self.t >= self.t_reentry() + P["time_p5_reentry_min"] * 60:
            self._splashdown()
            return
        self._tick()

    def _tick(self):
        dt = 1.0 / 60.0  # 1秒を「分」で表したもの（レートは /min）

        # 1) 酸素漏れ
        if self.leak_open and self.leaked < P["oxygen_leak_max"]:
            x = min(P["oxygen_leak_per_s"], P["oxygen_leak_max"] - self.leaked)
            self.oxygen -= x
            self.leaked += x

        # 2) 電力
        load = P["power_life_support"]
        for k in SWITCHES:
            if self.sw[k]:
                load += P["power_" + k]
        self.power -= self.mult * load * dt
        gen = self.n("solar_panel") * (P["solar_sun"] if self.attitude == "sun" else P["solar_other"])
        self.power = min(self.capacity, self.power + gen * dt)

        # 3) 温度
        if self.sw["heater"]:
            self.temp = min(P["temp_start"], self.temp + P["temp_rise"] * dt)
        else:
            self.temp -= P["temp_drop"] * dt

        # 4) CO2
        self.co2 = min(100.0, self.co2 + P["co2_rise"] * dt)
        co2_high = self.co2 >= P["co2_slow"]

        # 5) 酸素（呼吸）
        breathing = P["oxygen_breathing"] + (P["oxygen_co2_extra"] if co2_high else 0)
        self.oxygen -= self.mult * breathing * dt

        # 6) 観測（裏側の窓 + 月向き + スイッチON）
        if self.in_farside() and self.attitude == "moon":
            for i in INSTRUMENTS:
                if self.sw[i]:
                    self.observed_s[i] += 1
                    self.data_collected += P["data_" + i] * dt

        # 7) データ送信（通信可 + 通信ON + 地球向き + 送信ON）
        if self.transmit and self.has_signal() and self.sw["comms"] and self.attitude == "earth":
            rate = P["tx_high_gain"] if self.n("high_gain_antenna") else P["tx_standard"]
            self.data_sent += min(rate * dt, self.data_collected - self.data_sent)

        self.t += 1

        # 8) 失敗判定
        if self.power <= 0 or self.oxygen <= 0:
            self.ending = "ED-C"

    def _missed_burn(self):
        """噴射窓を逃した: ルートBで強制帰還し、判定を厳しくする（提案仕様）"""
        self.strict = True
        self.first_try = False
        self.route = "b"
        need = P["fuel_burn_b"]
        if not self.correction_done:
            need += P["fuel_correction_burn"] + P["fuel_burn_retry"]
        if self.fuel < need:
            self.ending = "ED-C"
            return
        self.fuel -= need
        self.correction_done = True
        self.burn_done = True

    def _splashdown(self):
        margin = P["end_strict_margin"] if self.strict else 0
        ok = (self.oxygen >= P["end_oxygen_min"] + margin
              and self.power >= P["end_power_min"] + margin
              and self.temp >= P["end_temp_min"])
        self.ending = "ED-A" if ok else "ED-B"

    # ------------------------------------------------------------ 結果とスコア
    def result(self, cost):
        survived = self.ending in ("ED-A", "ED-B")
        share = self.data_sent / self.data_collected if self.data_collected > 0 else 0.0
        window_s = P["time_p3_farside_min"] * 60
        base = sum(P["sci_" + i] * self.observed_s[i] / window_s for i in INSTRUMENTS)
        traj = P["sci_trajectory_mult"] if self.design["trajectory"] == "science" else 1
        carried = (1 - share) * P["sci_carried_home"] if survived else 0
        science = base * traj * (share + carried)

        req = {
            "req1_crew_alive": survived,
            "req2_science": science >= P["req_science_min"],
            "req3_mass": True,
            "req4_budget": True,
        }
        score = {"ED-A": P["score_ed_a"], "ED-B": P["score_ed_b"], "ED-C": 0}[self.ending]
        score += science * P["score_per_science"]
        if survived:
            score += (max(0, self.power) + max(0, self.oxygen) + max(0, self.fuel)) * P["score_per_resource"]
        teamwork = min(self.swaps, P["co2_filters"]) + (1 if self.first_try else 0)
        score += teamwork * P["score_teamwork"]
        score += max(0, LIMITS["budget"] - cost) * P["score_per_budget"]
        if all(req.values()):
            score += P["score_all_requirements"]

        return {
            "launch": "ok", "ending": self.ending, "t_end": self.t, "route": self.route,
            "power": round(max(0.0, self.power), 1), "oxygen": round(max(0.0, self.oxygen), 1),
            "fuel": round(self.fuel, 1), "temp": round(self.temp, 1), "co2": round(self.co2, 1),
            "data_collected": round(self.data_collected, 1), "data_sent": round(self.data_sent, 1),
            "science": round(science, 3), "requirements": req, "score": round(score, 1),
        }


def run(design, events, difficulty=1):
    """events: [(t, コマンド名, 引数タプル), ...]  t は事故からの秒"""
    mass, cost, status = launch_check(design)
    if status != "ok":
        return {"launch": status, "mass": mass, "cost": cost, "score": 0}
    sim = Sim(design, difficulty)
    queue = sorted(events, key=lambda e: e[0])
    while sim.ending is None:
        while queue and queue[0][0] <= sim.t:
            _, name, args = queue.pop(0)
            getattr(sim, name)(*args)
        sim.step()
    out = sim.result(cost)
    out["mass"], out["cost"] = mass, cost
    return out


# ---------------------------------------------------------------- テストシナリオ
DESIGN_SCIENCE = {  # バッテリー3、カメラ+分光計、観測向き軌道、大型ロケット
    "parts": {"battery": 3, "camera": 1, "spectrometer": 1},
    "trajectory": "science", "rocket": "large",
}
DESIGN_CHEAP = {  # バッテリー2、カメラのみ、自由帰還、小型ロケット
    "parts": {"battery": 2, "camera": 1},
    "trajectory": "free_return", "rocket": "small",
}

PLAY_GOOD = [
    (0, "switch", ("lights", False)),
    (0, "switch", ("heater", False)),
    (10, "close_valve", ()),
    (20, "set_route", ("a",)),
    (25, "correction_burn", ()),
    (30, "switch", ("nav", False)),
    (88, "set_attitude", ("moon",)),
    (90, "switch", ("camera", True)),
    (90, "switch", ("spectrometer", True)),
    (150, "switch", ("heater", True)),
    (160, "swap_filter", ()),
    (190, "switch", ("heater", False)),
    (210, "switch", ("camera", False)),
    (210, "switch", ("spectrometer", False)),
    (210, "set_attitude", ("earth",)),
    (210, "switch", ("nav", True)),
    (210, "set_transmit", (True,)),
    (275, "set_attitude", ("burn",)),
    (278, "ignite", (True,)),
    (280, "set_attitude", ("earth",)),
    (285, "set_transmit", (False,)),
    (298, "set_attitude", ("heatshield",)),
]

TESTS = [
    ("T1 良い設計 + 良い操作", DESIGN_SCIENCE, PLAY_GOOD),
    ("T2 最安の設計 + 良い操作", DESIGN_CHEAP, PLAY_GOOD),
    ("T3 T1の設計を小型ロケットで打ち上げ", dict(DESIGN_SCIENCE, rocket="small"), PLAY_GOOD),
    ("T4 最安の設計 + バルブを閉じるだけ", DESIGN_CHEAP, [(10, "close_valve", ())]),
    ("T5 良い設計 + 反応が遅い（バルブ25秒・ルートB・点火1回失敗）", DESIGN_SCIENCE,
     [e for e in PLAY_GOOD if e[1] not in ("close_valve", "set_route", "ignite")]
     + [(25, "close_valve", ()), (20, "set_route", ("b",)),
        (276, "ignite", (False,)), (278, "ignite", (True,)),
        (358, "set_attitude", ("heatshield",))]),
]

if __name__ == "__main__":
    for name, design, play in TESTS:
        print(name)
        print("  ", json.dumps(run(design, play), ensure_ascii=False))
