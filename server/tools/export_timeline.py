"""1回遊んだ記録を JSON に書き出す（Unity のオフライン再生用。サーバーなしで画面を作るため）

  uv run python tools/export_timeline.py            # data/sim/timeline_t1_good.json と timeline_t4_fail.json

中身は、サーバーが WebSocket で送るのと同じ形のメッセージを、届く順に並べたもの：
  {"meta": {...}, "messages": [{"at": 実時間の秒, "to": null か player_id, "msg": {...}}, ...]}
- at：部屋を作ってからの秒（speed=1 の本番と同じ間隔）。Unity はこの時刻どおりに msg を流せばよい
- to：null は全員宛て。player_id のときはその人だけに届くもの（welcome とエラー）
- 事故より前（ロビー・設計・打ち上げ判定・練習・かき混ぜ）は、ありそうな操作を仮に入れてある
- 4人プレイ。視点は bot0（Pilot）。welcome は bot0 の分だけ入れている
"""
import argparse
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.abspath(os.path.join(HERE, "..")))

from game.room import Game  # noqa: E402
from game.seats import COMMS, LIFE, PILOT, PLANNER  # noqa: E402
from game.sim_bridge import rs  # noqa: E402
from tools.bot_client import PRACTICE, to_msgs  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_DIR = os.path.join(REPO, "data", "sim")

CASES = {
    "timeline_t1_good.json": ("T1 良い設計 + 良い操作（PLAY_GOOD）", rs.DESIGN_SCIENCE, rs.PLAY_GOOD),
    "timeline_t4_fail.json": ("T4 最安の設計 + バルブを閉じるだけ（t=400 で電力0、ED-C）",
                              rs.DESIGN_CHEAP, [(10, "close_valve", ())]),
}


class Recorder:
    def __init__(self):
        self.game = Game("1234", speed=1.0)
        self.rows = []
        self.now = 0.0

    def flush(self):
        for to, msg in self.game.take_outbox():
            self.rows.append({"at": round(self.now, 3), "to": to, "msg": msg})

    def at(self, now):
        self.now = now
        self.game.update(now)
        self.flush()

    def do(self, now, pid, msg):
        self.now = now
        self.game.update(now)
        self.game.handle(pid, msg, now)
        self.flush()


def record(design, events):
    r = Recorder()
    g = r.game
    # --- ロビー：4人が入る
    ps = []
    for i in range(4):
        r.now = float(i)
        ps.append(g.add_player("bot%d" % i, r.now))
        if i == 0:
            r.rows.append({"at": 0.0, "to": ps[0].id,
                           "msg": {"type": "welcome", "room": g.code, "player_id": ps[0].id,
                                   "seats": [PILOT], "speed": 1.0}})
        r.flush()
    owner = {s: p.id for p in ps for s in p.seats}
    host = ps[0].id
    # --- M01 設計：部品を1つずつ足していく
    r.do(5.0, host, {"type": "start"})
    now = 8.0
    for pid, n in design["parts"].items():
        for _ in range(n - g.design["parts"].get(pid, 0)):
            r.do(now, host, {"type": "design", "part": pid, "delta": 1})
            now += 1.5
    r.do(now, host, {"type": "design", "trajectory": design["trajectory"]})
    r.do(now + 1.5, host, {"type": "design", "rocket": design["rocket"]})
    # --- M02 打ち上げ判定 → M03 練習：各席が1回ずつ操作
    now += 5.0
    r.do(now, host, {"type": "launch"})
    for i, s in enumerate((PILOT, COMMS, LIFE, PLANNER)):
        r.do(now + 6.0 + 3.0 * i, owner[s], dict(PRACTICE[s], seat=s))
    # --- M04 かき混ぜ → M05 事故
    t0 = now + 6.0 + 12.0 + 8.0
    r.do(t0, owner[LIFE], {"cmd": "stir", "seat": LIFE})
    # --- 事故のあと：時刻 k のコマンドは tick k-1 と tick k の間に届く
    queue = sorted(events, key=lambda e: e[0])
    while g.phase == "flight":
        while queue and queue[0][0] <= g.sim.t:
            _, name, args = queue.pop(0)
            for m in to_msgs(name, args):
                r.do(t0 + g.sim.t + 0.1, owner[m["seat"]], m)
        r.at(t0 + g.sim.t + 1.0)
    # --- Ending → Report（自動で進む時間まで待つ）
    end = r.now
    while g.phase == "ending":
        end += 1.0
        r.at(end)
    return r, ps


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=OUT_DIR)
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    for fname, (title, design, events) in CASES.items():
        r, ps = record(design, events)
        ref = rs.run(design, events)
        assert r.game.result == ref, "reference_sim と合わない: %s" % fname
        doc = {
            "meta": {
                "title": title,
                "generated_by": "server/tools/export_timeline.py",
                "design": design,
                "events": [[t, n, list(args)] for t, n, args in events],
                "players": [p.info() for p in ps],
                "viewer": ps[0].id,
                "result": r.game.result,
                "note": "msg はサーバーが WebSocket で送るものと同じ形（server/README.md）。at は部屋を作ってからの実時間の秒",
            },
            "messages": r.rows,
        }
        path = os.path.join(a.out, fname)
        with open(path, "w", encoding="utf-8") as f:
            json.dump(doc, f, ensure_ascii=False, indent=None, separators=(",", ":"))
            f.write("\n")
        print("%s  %d messages  %.0f KB  ending=%s" % (path, len(r.rows), os.path.getsize(path) / 1024,
                                                       ref["ending"]))


if __name__ == "__main__":
    main()
