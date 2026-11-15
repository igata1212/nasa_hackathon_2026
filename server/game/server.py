"""WebSocket の部屋サーバー。ロジックは room.Game、ここは接続と時計だけ"""
import asyncio
import json
import logging
import random
import time

from websockets.asyncio.server import serve
from websockets.exceptions import ConnectionClosed

from .room import Game, GameError

log = logging.getLogger("mission_return")


class SocketClosed(Exception):
    """app.py（ASGI）が、相手が切れたときに出す。ConnectionClosed と同じ扱い"""


class Server:
    def __init__(self, speed=1.0, difficulty=1):
        self.speed = speed
        self.difficulty = difficulty
        self.games = {}      # 合言葉 → Game
        self.conns = {}      # 合言葉 → {player_id: websocket}
        self.tasks = {}

    def _new_code(self):
        while True:
            code = "%04d" % random.randrange(10000)
            if code not in self.games:
                return code

    async def flush(self, code):
        game = self.games.get(code)
        if game is None:
            return
        conns = self.conns.get(code, {})
        for to, msg in game.take_outbox():
            data = json.dumps(msg, ensure_ascii=False)
            targets = [conns[to]] if to in conns else ([] if to else list(conns.values()))
            for ws in targets:
                try:
                    await ws.send(data)
                except (ConnectionClosed, SocketClosed):
                    pass

    async def _clock(self, code):
        """部屋ごとの時計。ゲーム内1秒（実時間 1/speed）より細かく update を呼ぶ"""
        step = min(0.05, 0.25 / self.speed)
        while code in self.games:
            game = self.games[code]
            game.update(time.monotonic())
            await self.flush(code)
            if not game.any_connected() and game.phase == "lobby" and not game.players:
                break
            await asyncio.sleep(step)
        self.games.pop(code, None)
        self.conns.pop(code, None)
        self.tasks.pop(code, None)

    async def _send(self, ws, msg):
        await ws.send(json.dumps(msg, ensure_ascii=False))

    async def handler(self, ws):
        code = pid = None
        try:
            async for raw in ws:
                try:
                    msg = json.loads(raw)
                    if not isinstance(msg, dict):
                        raise GameError("bad_json")
                except (ValueError, GameError):
                    await self._send(ws, {"type": "error", "code": "bad_json"})
                    continue
                now = time.monotonic()
                try:
                    if pid is None:
                        code, pid = await self._enter(ws, msg, now)
                    else:
                        self.games[code].handle(pid, msg, now)
                except GameError as e:
                    await self._send(ws, {"type": "error", "code": e.code, "detail": e.detail,
                                          "echo": msg})
                if code:
                    await self.flush(code)
        except (ConnectionClosed, SocketClosed):
            pass
        finally:
            if code in self.games and pid:
                if self.conns.get(code, {}).get(pid) is ws:
                    self.conns[code].pop(pid, None)
                    self.games[code].disconnect(pid)
                    await self.flush(code)

    async def _enter(self, ws, msg, now):
        kind = msg.get("type")
        if kind == "create":
            code = self._new_code()
            self.games[code] = Game(code, speed=self.speed, difficulty=msg.get("difficulty", self.difficulty))
            self.conns[code] = {}
            self.tasks[code] = asyncio.create_task(self._clock(code))
        elif kind == "join":
            code = str(msg.get("room", ""))
            if code not in self.games:
                raise GameError("no_such_room", code)
        else:
            raise GameError("not_in_room", "まず create か join")
        game = self.games[code]
        if msg.get("player_id"):
            p = game.reconnect(msg["player_id"])
        else:
            p = game.add_player(msg.get("name"), now, seats=msg.get("seats"), bot=msg.get("bot", False))
        self.conns[code][p.id] = ws
        await self._send(ws, {"type": "welcome", "room": code, "player_id": p.id, "seats": p.seats,
                              "speed": self.speed})
        for m in game.sync_msgs():
            await self._send(ws, m)
        return code, p.id


async def run(host=None, port=8765, speed=1.0, difficulty=1, ready=None):
    srv = Server(speed=speed, difficulty=difficulty)
    async with serve(srv.handler, host, port) as ws_server:
        log.info("listening on ws://%s:%d (speed x%s)", host, port, speed)
        if ready is not None:
            ready.set()
        await ws_server.serve_forever()
