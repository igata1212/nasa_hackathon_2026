# 書いた人：iizuka（作成：Haiku 5.5）／ 最終更新：2026-11-14
#
# UI のアイコンを作る（UI ガイドライン 4 章の割り当て表）。
# Ionicons（filled、MIT）の SVG を、白（#FFFFFF）の塗りの PNG 128×128（透明背景）にする。
# 色は Unity の Image の Color で付ける。stroke のある SVG は stroke も白にする。
# 酸素は文字の O2 のチップにするので画像は作らない。
# 元：tmp/ui_ref/icons/ionicons-filled/（コミットしない）
#
#   uv run --with resvg-py --with pillow art/ui/make_icons.py
#
# 変換は resvg（resvg-py）。cairosvg は uv の Python から Homebrew の libcairo を読めなかったので使わない。
# 出力：unity/Assets/Art/UI/Icons/<概念名>.png と LICENSE-ionicons.txt
import shutil
import xml.etree.ElementTree as ET
from pathlib import Path

import resvg_py
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "tmp/ui_ref/icons/ionicons-filled"
DST = ROOT / "unity/Assets/Art/UI/Icons"
SIZE = 128
WHITE = "#FFFFFF"

# 概念名（ファイル名）→ Ionicons の名前。並びはガイドライン 4 章の表どおり
ICONS = {
    "power": "flash",              # 電力
    "battery": "battery-half",     # 電池の残り
    "temperature": "thermometer",  # 温度
    "co2": "cloud",                # CO2
    "fuel": "water",               # 燃料
    "ignition": "flame",           # 点火
    "comms": "radio",              # 通信
    "comms_lost": "cloud-offline", # 通信途絶
    "alarm": "notifications",      # 警報
    "caution": "alert-circle",     # 注意
    "danger": "warning",           # 危険
    "switch": "toggle",            # スイッチ
    "lock": "lock-closed",         # 押せない（鍵）
    "rocket": "rocket",            # ロケット
    "earth": "earth",              # 地球
    "moon": "moon",                # 月
    "sun": "sunny",                # 太陽
    "heat_shield": "shield",       # 熱シールド
    "attitude": "compass",         # 姿勢（向き）
    "camera": "camera",            # カメラ
    "clock": "time",               # 時計
    "timer": "timer",              # 残り時間
    "check": "checkmark-circle",   # チェック
    "cross": "close-circle",       # バツ
    "seat": "person",              # 席
    "crew": "people",              # 人数
    "send": "send",                # 送信
    "route": "git-branch",         # ルート
    "science": "flask",            # 科学
    "score": "trophy",             # スコア
    "mass": "barbell",             # 質量
    "cost": "cash",                # コスト
    "enter": "enter",              # 部屋に入る
    "create": "add-circle",        # 作る
    # 6 章のワイヤーフレーム（#72）で使うもの。4 章の表には無い
    "room_code": "key",            # 合言葉の入力欄
    "settings": "settings",        # 開発用のパネル（DEV）を開く
    # Ionicons に無いものの代用（ガイドライン 4 章）
    "valve": "aperture",           # バルブ
    "spectrometer": "prism",       # 分光計
    "radar": "scan",               # レーダー
    "filter": "funnel",            # フィルター
}


def to_white(svg_text):
    """stroke と fill を白にする（none は残す）。ルートに fill が無ければ白にして、子に継承させる。"""
    ET.register_namespace("", "http://www.w3.org/2000/svg")
    root = ET.fromstring(svg_text)
    for el in root.iter():
        for key in ("fill", "stroke"):
            value = el.get(key)
            if value is not None and value != "none":
                el.set(key, WHITE)
    if root.get("fill") is None:
        root.set("fill", WHITE)
    return ET.tostring(root, encoding="unicode")


def check(path):
    """大きさと、不透明な所が白かを確かめる。戻り値は塗られている割合（%）"""
    raw = Image.open(path).convert("RGBA").tobytes()
    pixels = [raw[i:i + 4] for i in range(0, len(raw), 4)]
    assert len(pixels) == SIZE * SIZE, path
    assert all(p[:3] == b"\xff\xff\xff" for p in pixels if p[3] == 255), path
    return 100.0 * sum(1 for p in pixels if p[3] > 0) / (SIZE * SIZE)


def main():
    missing = [name for name in ICONS.values() if not (SRC / "svg" / f"{name}.svg").exists()]
    if missing:
        raise SystemExit(f"Ionicons に無い名前: {missing}")
    DST.mkdir(parents=True, exist_ok=True)
    for concept, name in ICONS.items():
        svg = (SRC / "svg" / f"{name}.svg").read_text(encoding="utf-8")
        png = bytes(resvg_py.svg_to_bytes(svg_string=to_white(svg), width=SIZE, height=SIZE))
        out = DST / f"{concept}.png"
        out.write_bytes(png)
        coverage = check(out)
        print(f"{concept:14s} <- {name:18s} 塗り {coverage:5.1f}%")
    shutil.copyfile(SRC / "LICENSE", DST / "LICENSE-ionicons.txt")
    print(f"{len(ICONS)} 個を {DST} に置いた")


if __name__ == "__main__":
    main()
