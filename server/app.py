"""Vercel（と uvicorn）用の入口。部屋のロジックは game.server.Server をそのまま使う（#222）。

  wss://<host>/api/ws    … ゲームの WebSocket（メッセージは README.md と同じ）
  https://<host>/api/health … 動いているか・部屋の数・インスタンスの名前

手元： uv run uvicorn app:app --port 8766   → ws://localhost:8766/api/ws
main.py（Cloudflare Tunnel 用の素の WebSocket サーバー）もそのまま使える。

部屋はこのプロセスのメモリにある。Vercel は新しい接続を同じインスタンスに届けるとは限らない
（https://vercel.com/docs/functions/websockets）。人が少ない間は同じインスタンスに集まるが、保証はない。
/api/health の instance が人によって違えば、部屋が分かれている。
"""
import os
import secrets

import fastapi

from game.server import Server, SocketClosed

SPEED = float(os.environ.get("MISSION_RETURN_SPEED", "1"))
INSTANCE = secrets.token_hex(3)

app = fastapi.FastAPI(title="Mission Return")
server = Server(speed=SPEED)


class _Socket:
    """game.server.Server が使う形（async for で受信、send(str) で送信）に合わせる"""

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


@app.get("/api/health")
async def health():
    return {"ok": True, "instance": INSTANCE, "rooms": len(server.games), "speed": SPEED}
