"""部屋を Redis に置くサーバー（game/redis_server.py。Vercel 用）。

2つの RedisServer（＝別々のインスタンス）が同じ Redis を見ている状態で、ボットを交互につなぐ。
同じ合言葉で同じ部屋に入れて、最後まで遊べること。接続を途中でサーバーから切っても戻れること。
"""
import asyncio

import fakeredis
import pytest
from websockets.asyncio.server import serve

from game.redis_server import RedisServer
from game.sim_bridge import rs
from tools.bot_client import case, run_bots

SPEED = 20


async def _play(n, case_name, max_conn_s=None):
    backend = fakeredis.FakeServer()
    servers = [RedisServer(fakeredis.FakeAsyncRedis(server=backend), speed=SPEED, max_conn_s=max_conn_s)
               for _ in range(2)]
    async with serve(servers[0].handler, "127.0.0.1", 0) as a, serve(servers[1].handler, "127.0.0.1", 0) as b:
        urls = ",".join("ws://127.0.0.1:%d" % s.sockets[0].getsockname()[1] for s in (a, b))
        return await run_bots(urls, n, case_name, timeout=90)


@pytest.mark.parametrize("n", [4, 2])
def test_two_instances_share_a_room(n):
    shared, bots = asyncio.run(_play(n, "T1"))
    ref = rs.run(*case("T1"))
    assert shared["result"]["ending"] == ref["ending"]
    assert shared["result"]["requirements"] == ref["requirements"]
    assert len(shared["reports"]) == n


def test_server_closes_connection_and_players_come_back():
    shared, bots = asyncio.run(_play(4, "T1", max_conn_s=4))
    assert shared["result"]["ending"] == rs.run(*case("T1"))["ending"]
    assert sum(b.reconnects for b in bots) >= 4
