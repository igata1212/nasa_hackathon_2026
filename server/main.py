"""Mission Return ゲームサーバーの起動： uv run python main.py --port 8765"""
import argparse
import asyncio
import logging

from game.server import run


def main():
    ap = argparse.ArgumentParser(description="Mission Return WebSocket game server")
    ap.add_argument("--host", default=None, help="省略すると IPv4・IPv6 の全部で待つ（localhost が ::1 になっても届く）")
    ap.add_argument("--port", type=int, default=8765)
    ap.add_argument("--speed", type=float, default=1.0, help="早送りの倍率（テスト用。1 = 実時間）")
    ap.add_argument("--difficulty", type=int, default=1, choices=(1, 2, 3))
    a = ap.parse_args()
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(message)s")
    print("ws://%s:%d  speed x%s" % (a.host or "localhost", a.port, a.speed), flush=True)
    asyncio.run(run(a.host, a.port, a.speed, a.difficulty))


if __name__ == "__main__":
    main()
