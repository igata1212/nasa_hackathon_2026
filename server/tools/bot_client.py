"""テスト用のクライアント。操作は reference_sim の TESTS（T1 = PLAY_GOOD）を、席ごとに担当が送る。

全員ボットで1回遊ぶ（部屋→設計→打ち上げ→練習→事故→着水→レポート）：
  uv run python tools/bot_client.py --bots 4 --case T1

人数の足りない席をボットで埋める（人は Unity から同じ部屋に入る）：
  uv run python tools/bot_client.py --seats pilot,comms_power              # 部屋を作って合言葉を出す
  uv run python tools/bot_client.py --seats life_support --room 1234       # 人が作った部屋に入る
  --drive を付けると start・設計・打ち上げ・next もボットが送る（--players 人そろったら start）
ボットは PLAY_GOOD の時刻どおりに自分の席の操作だけを送る。同時押しの相手が人の席なら、
人が 0.5 秒以内に押せば成立する。レポートのあとも切らずに次のゲームを待つ（Ctrl+C で終わる）。
"""
import argparse
import asyncio
import json
import os
import sys

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

import websockets  # noqa: E402

from game.seats import COMMS, LIFE, PILOT, PLANNER, seats_for  # noqa: E402
from game.sim_bridge import rs  # noqa: E402

PRACTICE = {
    PILOT: {"cmd": "set_attitude", "mode": "sun"},
    COMMS: {"cmd": "set_transmit", "on": True},
    LIFE: {"cmd": "close_valve", "tank": 2},
    PLANNER: {"cmd": "set_route", "route": "a"},
}


def to_msgs(name, args):
    """reference_sim のイベント1つ → 席つきのメッセージのリスト（同時押しは2つ）"""
    if name == "switch":
        a = {"name": args[0], "on": args[1]}
    elif name == "set_attitude":
        a = {"mode": args[0]}
    elif name == "set_route":
        a = {"route": args[0]}
    elif name == "set_transmit":
        a = {"on": args[0]}
    elif name == "close_valve":
        a = {"tank": 2}  # 漏れているのは Tank 2
    else:
        a = {}
    if name == "ignite":
        seats = [PILOT, COMMS] if args[0] else [PILOT]  # together=False は片方だけ押す
    elif name == "swap_filter":
        seats = [LIFE, COMMS]
    else:
        seats = [seats_for(name, a)[0]]
    return [dict(a, cmd=name, seat=s) for s in seats]


class Bot:
    """1つの接続（=1人のプレイヤー）。

    want_seats：席を指定して入る（人数の足りない席を埋める用）。None なら人数の割り当てに任せる
    drive：start・設計・打ち上げ・next を自分で送る
    stay：レポートのあとも切らずに次のゲームを待つ（人と一緒に遊ぶとき）
    """

    def __init__(self, idx, url, design, events, n, shared, want_seats=None, drive=None, stay=False,
                 room=None):
        self.idx, self.url, self.design, self.events, self.n = idx, url, design, events, n
        self.shared = shared          # {"room": 合言葉, "created": Event, "result", "reports"}
        self.want_seats, self.stay, self.room = want_seats, stay, room
        self.drive = (idx == 0) if drive is None else drive
        self.plan = []
        self.seats = []
        self.pid = None
        self.phase = None
        self.log = []

    def make_plan(self):
        self.plan = sorted(((t, m) for t, name, args in self.events for m in to_msgs(name, args)),
                           key=lambda x: x[0])

    async def send(self, ws, msg):
        await ws.send(json.dumps(msg))

    async def run(self):
        async with websockets.connect(self.url) as ws:
            hello = {"name": "bot%d" % self.idx, "bot": True}
            if self.want_seats:
                hello["seats"] = self.want_seats
            if self.room is None and self.idx == 0:
                await self.send(ws, dict(hello, type="create"))
            else:
                if self.room is None:
                    await self.shared["created"].wait()
                await self.send(ws, dict(hello, type="join", room=self.room or self.shared["room"]))
            async for raw in ws:
                msg = json.loads(raw)
                self.log.append(msg)
                if await self.on(ws, msg):
                    return

    async def on(self, ws, msg):
        typ = msg.get("type")
        if typ == "error":
            if self.stay:  # 人と遊んでいるときは落ちない
                print("bot%d error: %s" % (self.idx, msg), flush=True)
                return False
            raise RuntimeError("bot%d error: %s" % (self.idx, msg))
        if typ == "welcome":
            self.pid, self.seats = msg["player_id"], msg["seats"]
            if "created" in self.shared and not self.shared["created"].is_set():
                self.shared["room"] = msg["room"]
                self.shared["created"].set()
            if self.stay:
                print("room %s  player %s  seats %s" % (msg["room"], self.pid, msg["seats"]), flush=True)
        elif typ == "room":
            me = [p for p in msg["players"] if p["id"] == self.pid]
            if me:
                self.seats = me[0]["seats"]
            old, self.phase = self.phase, msg["phase"]
            if self.drive and msg["phase"] == "lobby" and len(msg["players"]) >= self.n \
                    and not self.shared.get("started"):
                self.shared["started"] = True
                await self.send(ws, {"type": "start"})
            if old != self.phase:
                await self.on_phase(ws)
        elif typ == "state" and self.phase == "flight":
            t = msg["t"]
            while self.plan and self.plan[0][0] <= t:
                _, m = self.plan.pop(0)
                if m["seat"] in self.seats:
                    await self.send(ws, m)
        elif typ == "result":
            self.shared["result"] = msg["result"]
            if self.stay:
                r = msg["result"]
                print("result %s  score %s" % (r["ending"], r["score"]), flush=True)
            if self.drive:
                await self.send(ws, {"type": "next"})
        elif typ == "report":
            self.shared.setdefault("reports", []).append(msg)
            return not self.stay
        return False

    async def on_phase(self, ws):
        if self.phase == "design" and self.drive:
            for pid, n in self.design["parts"].items():
                await self.send(ws, {"type": "design", "part": pid, "n": n})
            await self.send(ws, {"type": "design", "trajectory": self.design["trajectory"],
                                 "rocket": self.design["rocket"]})
            await self.send(ws, {"type": "launch"})
        elif self.phase == "practice":
            for s in self.seats:
                await self.send(ws, dict(PRACTICE[s], seat=s))
        elif self.phase == "stir" and LIFE in self.seats:
            await self.send(ws, {"cmd": "stir", "seat": LIFE})
        elif self.phase == "flight":
            self.make_plan()


def case(name):
    return {n[:2]: (d, e) for n, d, e in rs.TESTS}[name]


async def run_bots(url, n=4, case_name="T1", timeout=120):
    """テスト用：n 人ぶんつないで1回遊ぶ"""
    design, events = case(case_name)
    shared = {"created": asyncio.Event()}
    bots = [Bot(i, url, design, events, n, shared) for i in range(n)]
    await asyncio.wait_for(asyncio.gather(*(b.run() for b in bots)), timeout)
    return shared, bots


async def fill_seats(url, seats, room=None, case_name="T1", drive=False, players=2):
    """人と一緒に遊ぶ用：指定した席だけを自動で操作する1人として入り、切れるまで居続ける"""
    design, events = case(case_name)
    shared = {"created": asyncio.Event()}
    bot = Bot(0 if room is None else 9, url, design, events, players, shared,
              want_seats=seats, drive=drive, stay=True, room=room)
    await bot.run()


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--url", default="ws://localhost:8765")
    ap.add_argument("--case", default="T1", choices=["T1", "T2", "T4", "T5"],
                    help="使う設計と操作（reference_sim の TESTS。T1 = PLAY_GOOD）")
    ap.add_argument("--bots", type=int, default=4, help="全員ボットで1回遊ぶときの人数")
    ap.add_argument("--seats", help="この席だけを自動で操作する（例 pilot,comms_power）。人と一緒に遊ぶ用")
    ap.add_argument("--room", help="--seats のとき：入る部屋の合言葉。省略すると部屋を作って合言葉を出す")
    ap.add_argument("--drive", action="store_true",
                    help="--seats のとき：start・設計・打ち上げ・next もボットが送る")
    ap.add_argument("--players", type=int, default=2, help="--drive のとき：この人数そろったら start")
    a = ap.parse_args()
    if a.seats:
        seats = [s.strip() for s in a.seats.split(",") if s.strip()]
        try:
            asyncio.run(fill_seats(a.url, seats, a.room, a.case, a.drive, a.players))
        except KeyboardInterrupt:
            pass
        return
    shared, _ = asyncio.run(run_bots(a.url, a.bots, a.case))
    print(json.dumps(shared["result"], ensure_ascii=False))
    ref = rs.run(*case(a.case))
    print("reference_sim と一致" if shared["result"] == ref else "reference_sim と不一致: %s" % ref)


if __name__ == "__main__":
    main()
