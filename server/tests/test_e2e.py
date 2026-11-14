"""通しのテスト：サーバーを別プロセスで起動し、ボット4つで部屋→設計→打ち上げ→事故→着水→レポート"""
import asyncio
import os
import socket
import subprocess
import sys
import time

import pytest

from game.sim_bridge import rs
import json

import websockets

from tools.bot_client import Bot, case, run_bots

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SPEED = 40   # 着水まで 360 秒 → 約9秒


def free_port():
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]


@pytest.fixture(scope="module")
def server():
    port = free_port()
    proc = subprocess.Popen([sys.executable, "main.py", "--port", str(port),
                             "--speed", str(SPEED)], cwd=HERE,
                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    for _ in range(100):
        try:
            socket.create_connection(("127.0.0.1", port), 0.1).close()
            break
        except OSError:
            time.sleep(0.05)
    yield "ws://localhost:%d" % port
    proc.terminate()
    proc.wait(5)


@pytest.mark.parametrize("n,case", [(4, "T1"), (3, "T5"), (2, "T2"), (1, "T4")])
def test_full_game(server, n, case):
    shared, bots = asyncio.run(run_bots(server, n, case, timeout=90))
    design, events = {name[:2]: (d, e) for name, d, e in rs.TESTS}[case]
    ref = rs.run(design, events)
    r = shared["result"]
    assert r["ending"] == ref["ending"]
    # ローカルなら毎秒のコマンドは次の tick までに届くので、数値まで一致するはず
    assert r == ref
    assert len(shared["reports"]) == n
    rep = shared["reports"][0]
    assert rep["history"][-1]["t"] == r["t_end"]
    phases = [m["phase"] for m in bots[0].log if m.get("type") == "room"]
    for ph in ("lobby", "design", "practice", "stir", "flight", "ending", "report"):
        assert ph in phases


def test_fill_seats_with_bot(server):
    """bot_client --seats：ボットが pilot と comms_power だけを持ち、残りの席を「人」（ここでは別のボット）が持つ"""
    design, events = case("T1")
    shared = {"created": asyncio.Event()}
    filler = Bot(0, server, design, events, 2, shared, want_seats=["pilot", "comms_power"], drive=True)
    human = Bot(1, server, design, events, 2, shared, drive=False)

    async def go():
        await asyncio.wait_for(asyncio.gather(filler.run(), human.run()), 90)
    asyncio.run(go())
    assert filler.seats == ["pilot", "comms_power"]
    assert sorted(human.seats) == ["life_support", "mission_planner"]
    assert shared["result"] == rs.run(design, events)


def test_ipv4_and_ipv6(server):
    """Unity の ClientWebSocket が localhost を ::1 にしても 127.0.0.1 にしても届く"""
    port = server.rsplit(":", 1)[1]

    async def hello(url):
        async with websockets.connect(url) as ws:
            await ws.send(json.dumps({"type": "create", "name": "x"}))
            return json.loads(await ws.recv())["type"]
    for host in ("127.0.0.1", "[::1]"):
        assert asyncio.run(hello("ws://%s:%s" % (host, port))) == "welcome"
