# 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
#
# 酸素ガスの煙（Unity のパーティクル GasLeak.prefab）の画像を作る（#55 LookDev）。
# 元：Kenney「Smoke Particles」の White puff から丸くて柔らかい4枚（CC0）。元画像はコミットしない。
# 4枚を 2×2 のアトラス（512×512、1マス 256）にまとめる（Texture Sheet Animation でランダムに1枚選ぶ）。
# 元画像は半透明の縁の RGB が暗く、アルファで重ねると黒い縁が出るので、RGB は全部白にしてアルファだけ残す。
#
#   python3 art/make_smoke.py [元フォルダ] [出力]
#   既定：tmp/space_assets/07-vfx/kenney-smoke-particles/extracted/PNG/White puff → unity/Assets/Art/VFX/Smoke_Puff_2x2.png
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "tmp/space_assets/07-vfx/kenney-smoke-particles/extracted/PNG/White puff"
DST = ROOT / "unity/Assets/Art/VFX/Smoke_Puff_2x2.png"
PICK = ["whitePuff01.png", "whitePuff10.png", "whitePuff12.png", "whitePuff18.png"]   # 丸くて形のそろった物
CELL = 256
MARGIN = 10   # マスの縁はアルファ 0 を残す（隣のマスがにじまない）


def main(src=SRC, dst=DST):
    src = Path(src)
    atlas = np.zeros((CELL * 2, CELL * 2, 4), dtype=np.uint8)
    for i, name in enumerate(PICK):
        im = Image.open(src / name).convert("RGBA")
        im.thumbnail((CELL - 2 * MARGIN, CELL - 2 * MARGIN), Image.LANCZOS)
        cell = Image.new("RGBA", (CELL, CELL), (255, 255, 255, 0))
        cell.paste(im, ((CELL - im.width) // 2, (CELL - im.height) // 2))
        a = np.asarray(cell).copy()
        a[..., :3] = 255
        r, c = divmod(i, 2)
        atlas[r * CELL:(r + 1) * CELL, c * CELL:(c + 1) * CELL] = a
    dst = Path(dst)
    dst.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(atlas, "RGBA").save(dst, optimize=True)
    print(f"{src} {PICK} → {dst} {atlas.shape[1]}×{atlas.shape[0]} {dst.stat().st_size // 1024}KB")


if __name__ == "__main__":
    main(*sys.argv[1:3])
