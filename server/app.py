"""Vercel（と uvicorn）用の入口（#222）。部屋のロジックは game.room.Game のまま。

  wss://<host>/api/ws       … ゲームの WebSocket（メッセージは README.md と同じ）
  https://<host>/api/health … 動いているか・部屋の置き場所・インスタンスの名前

部屋の置き場所：
  REDIS_URL（か KV_URL）がある … Redis（game/redis_server.py）。Vercel はこちら。接続ごとに別のインスタンスへ
                                  届くことがあるので、メモリの部屋では「同じ合言葉なのに部屋が無い」になる
  無い                          … このプロセスのメモリ（game/server.py）。手元で試す用

手元： uv run uvicorn app:app --port 8766   → ws://localhost:8766/api/ws
main.py（Cloudflare Tunnel 用の素の WebSocket サーバー）もそのまま使える。
"""
import os
import secrets

import fastapi

from game.server import Server, SocketClosed

SPEED = float(os.environ.get("MISSION_RETURN_SPEED", "1"))
REDIS_URL = os.environ.get("REDIS_URL") or os.environ.get("KV_URL")
# Vercel は関数の最大時間で接続を黙って切る。その前にこちらから閉じる（クライアントは同じ席に戻る）
MAX_CONN_S = float(os.environ.get("MISSION_RETURN_MAX_CONN_S", "270" if os.environ.get("VERCEL") else "0"))
INSTANCE = secrets.token_hex(3)

app = fastapi.FastAPI(title="Mission Return")

if REDIS_URL:
    import redis.asyncio as aioredis

    from game.redis_server import RedisServer

    server = RedisServer(aioredis.from_url(REDIS_URL), speed=SPEED, max_conn_s=MAX_CONN_S or None)
    STORE = "redis"
else:
    server = Server(speed=SPEED)
    STORE = "memory"


class _Socket:
    """サーバーが使う形（async for で受信、send(str) で送信）に合わせる"""

    def __init__(self, ws):
        self.ws = ws

    async def send(self, data):
        try:
            await self.ws.send_text(data)
        except (fastapi.WebSocketDisconnect, RuntimeError):
            raise SocketClosed()

    def __aiter__(self):
        return self

    async def __anext__(self):
        try:
            return await self.ws.receive_text()
        except (fastapi.WebSocketDisconnect, RuntimeError):
            raise StopAsyncIteration


@app.websocket("/api/ws")
async def ws_endpoint(websocket: fastapi.WebSocket):
    await websocket.accept()
    await server.handler(_Socket(websocket))
    try:
        await websocket.close()
    except RuntimeError:
        pass


@app.get("/api/health")
async def health():
    out = {"ok": True, "instance": INSTANCE, "store": STORE, "speed": SPEED, "max_conn_s": MAX_CONN_S}
    if STORE == "redis":
        try:
            out["redis"] = bool(await server.r.ping())
        except Exception as e:   # つながらない理由を見えるようにする
            out["ok"], out["redis"] = False, str(e)[:200]
    return out
