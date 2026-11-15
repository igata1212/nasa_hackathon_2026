"""部屋を Redis に置く WebSocket の部屋サーバー（Vercel 用。#222）。ロジックは room.Game のまま。

Vercel は接続ごとに別のインスタンスへ届けることがある。メモリの部屋（server.Server）だと
「同じ合言葉なのに部屋が無い」になるので、部屋を Redis に置いて、どのインスタンスからでも読めるようにする。

  mr:room:<合言葉>   Game を pickle したもの（1時間さわらなければ消える）
  mr:lock:<合言葉>   部屋を読んで書く間の鍵（1つのインスタンスだけが触る）
  mr:out:<合言葉>    pub/sub。{"due": 次に時計を進める実時刻, "out": [[宛先 player_id か null, メッセージ], …]}

時計：部屋を触ったインスタンスは次の時刻（due）を知る。due が来たら、つながっている接続のどれか1つが
鍵を取って Game.update を呼ぶ（取れなかった接続は何もしない）。時刻は time.time()（インスタンスをまたいで同じ）。

接続は MAX_CONN_S 秒でこちらから閉じる（Vercel が関数の最大時間で黙って切る前に、退出をきちんと記録するため）。
クライアントは player_id で同じ席に戻る。
"""
import asyncio
import json
import logging
import pickle
import random
import secrets
import time

from websockets.exceptions import ConnectionClosed

from .room import Game, GameError
from .server import SocketClosed

log = logging.getLogger("mission_return")

ROOM_TTL_S = 3600
LOCK_MS = 5000
CLOSED = (ConnectionClosed, SocketClosed)


def _due(game):
    """次に Game.update を呼ぶ実時刻（無ければ None）"""
    if game.phase == "flight":
        return game.next_tick
    if game.phase in ("practice", "stir", "ending"):
        return game.deadline
    return None


class _Conn:
    def __init__(self):
        self.code = self.pid = None
        self.due = None
        self.wake = asyncio.Event()
        self.pubsub = None


class RedisServer:
    def __init__(self, redis, speed=1.0, difficulty=1, max_conn_s=None):
        self.r = redis
        self.speed = speed
        self.difficulty = difficulty
        self.max_conn_s = max_conn_s

    # ------------------------------------------------------------ Redis
    async def _lock(self, code, wait):
        token = secrets.token_hex(8)
        key = "mr:lock:" + code
        for _ in range(150 if wait else 1):
            if await self.r.set(key, token, nx=True, px=LOCK_MS):
                return token
            if wait:
                await asyncio.sleep(0.02)
        return None

    async def _unlock(self, code, token):
        key = "mr:lock:" + code
        held = await self.r.get(key)
        if held is not None and held.decode() == token:
            await self.r.delete(key)

    async def mutate(self, code, fn, conn=None, wait=True):
        """鍵を取り、部屋を読み、fn(game か None) → (game, 戻り値) を呼び、書き戻して outbox を配る。
        鍵が取れなければ (False, None)。fn が GameError を出しても、そこまでの変更は書き戻す（server.Server と同じ）"""
        token = await self._lock(code, wait)
        if token is None:
            if wait:
                raise GameError("busy", "部屋が混んでいる")
            return False, None
        game = None
        try:
            raw = await self.r.get("mr:room:" + code)
            game = pickle.loads(raw) if raw else None
            game, result = fn(game)
            return True, result
        finally:
            try:
                if game is not None:
                    out = game.take_outbox()
                    due = _due(game)
                    await self.r.set("mr:room:" + code, pickle.dumps(game), ex=ROOM_TTL_S)
                    if conn is not None and due != conn.due:
                        conn.due = due
                        conn.wake.set()
                    await self.r.publish("mr:out:" + code,
                                         json.dumps({"due": due, "out": out}, ensure_ascii=False))
            finally:
                await self._unlock(code, token)

    # ------------------------------------------------------------ 接続
    async def _send(self, ws, msg):
        await ws.send(json.dumps(msg, ensure_ascii=False))

    async def handler(self, ws):
        conn = _Conn()
        tasks = []
        try:
            if self.max_conn_s:
                await asyncio.wait_for(self._read(ws, conn, tasks), self.max_conn_s)
            else:
                await self._read(ws, conn, tasks)
        except asyncio.TimeoutError:
            pass   # こちらから閉じる。クライアントがつなぎ直す
        except CLOSED:
            pass
        finally:
            for t in tasks:
                t.cancel()
            if conn.pubsub is not None:
                try:
                    await conn.pubsub.aclose()
                except Exception:
                    pass
            if conn.pid:
                try:
                    await self.mutate(conn.code, lambda g: (g, g.disconnect(conn.pid) if g else None), conn)
                except Exception as e:
                    log.warning("disconnect を記録できない: %s", e)

    async def _read(self, ws, conn, tasks):
        async for raw in ws:
            try:
                msg = json.loads(raw)
                if not isinstance(msg, dict):
                    raise GameError("bad_json")
            except (ValueError, GameError):
                await self._send(ws, {"type": "error", "code": "bad_json"})
                continue
            now = time.time()
            try:
                if conn.pid is None:
                    await self._enter(ws, conn, msg, now)
                    tasks.append(asyncio.create_task(self._pump(ws, conn)))
                    tasks.append(asyncio.create_task(self._clock(conn)))
                else:
                    def fn(game):
                        if game is None:
                            raise GameError("no_such_room", conn.code)
                        game.handle(conn.pid, msg, now)
                        game.update(now)
                        return game, None
                    await self.mutate(conn.code, fn, conn)
            except GameError as e:
                await self._send(ws, {"type": "error", "code": e.code, "detail": e.detail, "echo": msg})

    async def _enter(self, ws, conn, msg, now):
        kind = msg.get("type")
        if kind == "create":
            while True:
                code = "%04d" % random.randrange(10000)
                if not await self.r.exists("mr:room:" + code):
                    break
        elif kind == "join":
            code = str(msg.get("room", ""))
        else:
            raise GameError("not_in_room", "まず create か join")

        # 先に聞き始める（部屋に入った直後の知らせを取りこぼさない）
        pubsub = self.r.pubsub()
        await pubsub.subscribe("mr:out:" + code)

        def fn(game):
            if kind == "create":
                game = Game(code, speed=self.speed, difficulty=msg.get("difficulty", self.difficulty))
            elif game is None:
                raise GameError("no_such_room", code)
            if msg.get("player_id"):
                p = game.reconnect(msg["player_id"])
            else:
                p = game.add_player(msg.get("name"), now, seats=msg.get("seats"), bot=msg.get("bot", False))
            game.update(now)
            direct = [{"type": "welcome", "room": code, "player_id": p.id, "seats": p.seats,
                       "speed": self.speed}] + game.sync_msgs()
            return game, (p.id, direct)

        try:
            _, (pid, direct) = await self.mutate(code, fn, conn)
        except BaseException:
            await pubsub.aclose()
            raise
        conn.code, conn.pid, conn.pubsub = code, pid, pubsub
        for m in direct:
            await self._send(ws, m)

    async def _pump(self, ws, conn):
        """pub/sub に来た知らせのうち、全員宛てと自分宛てを送る"""
        try:
            async for item in conn.pubsub.listen():
                if item.get("type") != "message":
                    continue
                data = json.loads(item["data"])
                if data.get("due") != conn.due:
                    conn.due = data.get("due")
                    conn.wake.set()
                for to, msg in data.get("out", []):
                    if to is None or to == conn.pid:
                        await self._send(ws, msg)
        except CLOSED:
            pass

    async def _clock(self, conn):
        """due が来たら鍵を取って時計を進める。ほかの接続が先に進めたら、新しい due まで待つ"""
        jitter = random.random() * 0.03   # 同時に鍵を取りに行かない
        while True:
            conn.wake.clear()
            due = conn.due
            wait = None if due is None else due + jitter - time.time()
            if wait is None or wait > 0:
                try:
                    await asyncio.wait_for(conn.wake.wait(), wait)
                except asyncio.TimeoutError:
                    pass
                continue
            try:
                def fn(game):
                    if game is None:
                        conn.due = None
                    else:
                        game.update(time.time())
                    return game, None
                ok, _ = await self.mutate(conn.code, fn, conn, wait=False)
            except Exception as e:
                log.warning("時計を進められない: %s", e)
                ok = False
            if not ok or (conn.due is not None and conn.due <= time.time()):
                await asyncio.sleep(0.05)
