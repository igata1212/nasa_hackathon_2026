"""Vercel に置くフォルダ（tmp/vercel/）を作る。書いた人：Taiga（Claude） ／ 最終更新：2026-11-15（#222）

  python tools/vercel/make_bundle.py          # サーバーだけ
  python tools/vercel/make_bundle.py --webgl  # unity/Builds/WebGL も public/ に入れる（ブラウザ版）
  npx vercel deploy tmp/vercel --prod

入れるもの：
  server/app.py・game/・pyproject.toml・uv.lock  … ゲームのサーバー（/api/ws）
  docs/design の reference_sim.py と JSON          … design/ に写す（計算の正。game/sim_bridge.py が読む）
  unity/Builds/WebGL                               … public/ に写す（--webgl のとき）
tmp/ はコミットされない。毎回作り直す。
"""
import argparse
import os
import shutil

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "tmp", "vercel")
DESIGN_FILES = ["reference_sim.py", "parameters.json", "parts.json", "houston_lines.json"]


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--webgl", action="store_true", help="unity/Builds/WebGL を public/ に入れる")
    a = ap.parse_args()

    keep = os.path.join(OUT, ".vercel")   # vercel link の結果（どのプロジェクトか）は残す
    saved = None
    if os.path.isdir(keep):
        saved = os.path.join(ROOT, "tmp", ".vercel_keep")
        shutil.rmtree(saved, ignore_errors=True)
        shutil.move(keep, saved)
    shutil.rmtree(OUT, ignore_errors=True)
    os.makedirs(OUT)
    if saved:
        shutil.move(saved, keep)

    server = os.path.join(ROOT, "server")
    for name in ["app.py", "pyproject.toml", "uv.lock"]:
        shutil.copy(os.path.join(server, name), OUT)
    shutil.copytree(os.path.join(server, "game"), os.path.join(OUT, "game"),
                    ignore=shutil.ignore_patterns("__pycache__"))
    os.makedirs(os.path.join(OUT, "design"))
    for name in DESIGN_FILES:
        shutil.copy(os.path.join(ROOT, "docs", "design", name), os.path.join(OUT, "design"))
    shutil.copy(os.path.join(os.path.dirname(__file__), "vercel.json"), OUT)

    if a.webgl:
        build = os.path.join(ROOT, "unity", "Builds", "WebGL")
        if not os.path.isfile(os.path.join(build, "index.html")):
            raise SystemExit("WebGL のビルドが無い: %s（Unity の Tools/Build/WebGL (Vercel)）" % build)
        shutil.copytree(build, os.path.join(OUT, "public"), ignore=shutil.ignore_patterns("vercel.json"))
    print(OUT)


if __name__ == "__main__":
    main()
