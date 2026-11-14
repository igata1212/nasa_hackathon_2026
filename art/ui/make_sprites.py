# 書いた人：iizuka（作成：Haiku 5.5）／ 最終更新：2026-11-14
#
# UI の白い形を作る（UI ガイドライン 2・3・5 章）：角丸の四角（9-slice）、ピル、吹き出しの ▼、危険の斜め縞。
# 色は付けない（白＋アルファだけ）。Unity の Image の Color で色を付ける。
# 4 倍で描いてから縮小するので、角がなめらか。
#
#   uv run --with pillow art/ui/make_sprites.py
#
# 出力：unity/Assets/Art/UI/Sprites/（9-slice の数字と使い方は Sprites/README.md）
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
DST = ROOT / "unity/Assets/Art/UI/Sprites"
SS = 4  # 描く倍率


def mask(w, h):
    return Image.new("L", (w * SS, h * SS), 0)


def save(m, name, w, h, alpha=255):
    """SS 倍の白黒マスクを w×h に縮めて、白の RGBA にする（alpha は最大の不透明度）"""
    small = m.resize((w, h), Image.Resampling.BOX)
    if alpha != 255:
        small = small.point(lambda v: v * alpha // 255)
    out = Image.new("RGBA", (w, h), (255, 255, 255, 0))
    out.putalpha(small)
    out.save(DST / name, optimize=True)
    print(f"{name}  {w}×{h}")


def rounded(r):
    """3r×3r の白い角丸の四角（角丸の半径 r）。9-slice の border は r"""
    s = 3 * r
    m = mask(s, s)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, s * SS - 1, s * SS - 1), radius=r * SS, fill=255)
    save(m, f"round{r}.png", s, s)


def pill():
    """128×128 の白い円（9-slice の border は 63）。角の半径は Image の Pixels Per Unit Multiplier で決める"""
    m = mask(128, 128)
    ImageDraw.Draw(m).ellipse((0, 0, 128 * SS - 1, 128 * SS - 1), fill=255)
    save(m, "pill.png", 128, 128)


def caret_down():
    """吹き出しの ▼（24×14）"""
    m = mask(24, 14)
    ImageDraw.Draw(m).polygon([(0, 0), (24 * SS, 0), (12 * SS, 14 * SS)], fill=255)
    save(m, "caret_down.png", 24, 14)


def stripes():
    """危険の斜め縞（64×64 のタイル、白の 40%）。45°、周期 32、縞の幅 16。64 は 32 の倍数なので端がつながる"""
    tile, period, width = 64, 32, 16
    m = mask(tile, tile)
    px = m.load()
    for y in range(tile * SS):
        for x in range(tile * SS):
            if (x + y) % (period * SS) < width * SS:
                px[x, y] = 255
    save(m, "stripes64.png", tile, tile, alpha=102)


def main():
    DST.mkdir(parents=True, exist_ok=True)
    for r in (16, 28, 40):
        rounded(r)
    pill()
    caret_down()
    stripes()


if __name__ == "__main__":
    main()
