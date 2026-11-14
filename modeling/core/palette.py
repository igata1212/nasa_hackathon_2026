"""パレット16色の定義。palette.png（256×256、4×4マス）と palette.json（色名→マス中心の UV）を作る。

書いた人：iizuka（作成：Fable 5.2）／ 最終更新：2026-11-14

使い方（Blender なしで動く）：  python3 modeling/core/palette.py
Unity 側の `unity/Assets/Models/Core/palette.png` にも同じ画像を書く（FBX のマテリアルがこの名前の画像を探す）。
色を変えるときは下の COLORS だけを直して、このスクリプトと各 build_*.py を流し直す。
"""
import sys
sys.dont_write_bytecode = True
import json, os, struct, zlib

CORE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(CORE))
SIZE, GRID = 256, 4

# (色名, 日本語名, 16進, 用途)。左上から右へ、上の行から下の行へ並ぶ
COLORS = [
    ("cream",       "クリーム",       "#F2E3C6", "船内"),
    ("orange",      "オレンジ",       "#F28C28", "船内"),
    ("teal",        "青緑",           "#2A9D8F", "船内"),
    ("navy",        "濃紺",           "#1D2D50", "船内"),
    ("seat_red",    "席の赤",         "#E4572E", "席（仮：Pilot）"),
    ("seat_blue",   "席の青",         "#3A86FF", "席（仮：Mission Planner）"),
    ("seat_yellow", "席の黄",         "#FFC857", "席（仮：Comms & Power）"),
    ("seat_green",  "席の緑",         "#6BBF59", "席（仮：Life Support）"),
    ("white",       "白",             "#FBF7EE", "無彩色（提案）"),
    ("gray_light",  "明るい灰",       "#C8CCD4", "無彩色（提案）"),
    ("gray",        "灰",             "#8B93A1", "無彩色（提案）"),
    ("gray_dark",   "暗い灰",         "#4B5263", "無彩色（提案）"),
    ("visor",       "バイザー黒紺",   "#0E1630", "無彩色（提案）"),
    ("lamp_green",  "ランプの緑",     "#2ECC71", "ランプ専用（提案）。Lamp_ の部品だけ"),
    ("lamp_yellow", "ランプの黄",     "#FFD60A", "ランプ専用（提案）。Lamp_ の部品だけ"),
    ("lamp_red",    "ランプの赤",     "#FF3B30", "ランプ専用（提案）。Lamp_ の部品だけ"),
]
LAMP_COLORS = ("lamp_green", "lamp_yellow", "lamp_red")


def table():
    """色名 → {hex, ja, use, cell:[列,行], uv:[u,v]}。行0が画像の上。UV は左下が原点。"""
    out = {}
    for i, (name, ja, hx, use) in enumerate(COLORS):
        col, row = i % GRID, i // GRID
        out[name] = {"hex": hx, "ja": ja, "use": use, "cell": [col, row],
                     "uv": [(col + 0.5) / GRID, 1 - (row + 0.5) / GRID]}
    return out


def _png(path, rows):
    raw = b"".join(b"\x00" + bytes(v for px in r for v in px) for r in rows)
    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", SIZE, SIZE, 8, 2, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def main():
    assert len(COLORS) == GRID * GRID
    cell = SIZE // GRID
    rgb = [tuple(int(hx[i:i + 2], 16) for i in (1, 3, 5)) for _, _, hx, _ in COLORS]
    rows = [[rgb[(y // cell) * GRID + x // cell] for x in range(SIZE)] for y in range(SIZE)]
    unity_core = os.path.join(REPO, "unity", "Assets", "Models", "Core")
    os.makedirs(unity_core, exist_ok=True)
    for p in (os.path.join(CORE, "palette.png"), os.path.join(unity_core, "palette.png")):
        _png(p, rows)
        print("wrote", p)
    with open(os.path.join(CORE, "palette.json"), "w", encoding="utf-8") as f:
        json.dump({"size": SIZE, "grid": GRID, "material": "M_Core_Palette", "lamp_colors": list(LAMP_COLORS),
                   "colors": table()}, f, ensure_ascii=False, indent=1)
        f.write("\n")
    print("wrote", os.path.join(CORE, "palette.json"))


if __name__ == "__main__":
    main()
