# 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
#
# Unity の空（Skybox/Panoramic）の画像を作る（#55 LookDev）。
# 元：Kenney「Skyboxes Space」の skybox-space-galaxy.png（CC0、4096×2048、2:1 の正距円筒）。元画像はコミットしない。
# 紫が強いので、明るさを残したまま色をパレットの濃紺 navy #1D2D50 寄りの色の帯に置き換え、彩度を落として、2048×1024 に縮める。
#
#   python3 art/make_sky.py [元画像] [出力]
#   既定：tmp/space_assets/04-cc0-skybox/kenney_unzipped/Skyboxes/skybox-space-galaxy.png → unity/Assets/Art/Sky/Sky_Space_Navy.png
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "tmp/space_assets/04-cc0-skybox/kenney_unzipped/Skyboxes/skybox-space-galaxy.png"
DST = ROOT / "unity/Assets/Art/Sky/Sky_Space_Navy.png"
SIZE = (2048, 1024)

# 明るさ（0〜1、元画像の範囲に合わせて伸ばしたもの）→ 色。暗い所は濃紺、明るい所（銀河の芯・星）は少し暖かい白
RAMP = [
    (0.00, "#080D1A"),
    (0.30, "#141F3A"),
    (0.55, "#1D2D50"),   # navy
    (0.80, "#344B78"),
    (0.92, "#5A72A2"),
    (1.00, "#C9CCD2"),
]
KEEP_ORIGINAL = 0.18   # 元の色をどれだけ残すか（0＝全部置き換え）


def hex2rgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], dtype=np.float32) / 255.0


def ramp(t):
    xs = np.array([p for p, _ in RAMP], dtype=np.float32)
    cs = np.stack([hex2rgb(c) for _, c in RAMP])
    return np.stack([np.interp(t, xs, cs[:, i]) for i in range(3)], axis=-1)


def main(src=SRC, dst=DST):
    im = Image.open(src).convert("RGB").resize(SIZE, Image.LANCZOS)
    a = np.asarray(im, dtype=np.float32) / 255.0
    lum = a @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
    lo, hi = np.percentile(lum, 1), np.percentile(lum, 99.97)
    t = np.clip((lum - lo) / max(hi - lo, 1e-6), 0, 1)
    out = ramp(t)
    gray = lum[..., None].repeat(3, axis=-1)
    orig = 0.5 * a + 0.5 * gray                         # 元の色も彩度を半分に落としてから混ぜる
    out = (1 - KEEP_ORIGINAL) * out + KEEP_ORIGINAL * orig
    dst = Path(dst)
    dst.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.clip(out * 255 + 0.5, 0, 255).astype(np.uint8)).save(dst, optimize=True)
    print(f"{src} → {dst} {SIZE} {dst.stat().st_size // 1024}KB  平均色={np.round(out.reshape(-1, 3).mean(0) * 255).astype(int)}")


if __name__ == "__main__":
    main(*sys.argv[1:3])
