# 書いた人：iizuka（作成：Haiku 5.5）／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-15、#190 あつ森に寄せた形）
#
# UI の白い形を作る（UI ガイドライン 2・3・5 章）：角丸の四角（9-slice）、ピル、吹き出しの ▼、危険の斜め縞。
# #190：波打つ形（blob_*）・波の縁・角の丸い ▼・パネルの横縞・宇宙服のグローブの指さし。
# 形はすべて式で作る（基準の画像をなぞらない。ガイドライン 7 章）。
# 色は付けない（白＋アルファだけ。glove_point だけ色つき）。Unity の Image の Color で色を付ける。
# 4 倍で描いてから縮小するので、角がなめらか。
#
#   uv run --with pillow art/ui/make_sprites.py              # sprite を作る
#   uv run --with pillow art/ui/make_sprites.py --preview    # ＋見本の絵 art/ui/preview_acnh.png
#
# 出力：unity/Assets/Art/UI/Sprites/（9-slice の数字と使い方は Sprites/README.md と art/ui/README.md）
import math
import sys
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


# ------------------------------------------------------------------ #190 あつ森に寄せた形（ガイドライン 1 章の原則 1・3・4・5・6、5 章）

def poly(w, h, pts):
    """sprite の px の座標の輪郭（閉じた折れ線）を SS 倍のマスクに塗る"""
    m = mask(w, h)
    ImageDraw.Draw(m).polygon([(x * SS, y * SS) for x, y in pts], fill=255)
    return m


def fit(pts, w, h, margin=1.0):
    """輪郭の外接の四角を [margin, w−margin]×[margin, h−margin] に合わせる（形が枠いっぱいに来る）"""
    xs, ys = [p[0] for p in pts], [p[1] for p in pts]
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    sx, sy = (w - 2 * margin) / (x1 - x0), (h - 2 * margin) / (y1 - y0)
    return [(margin + (x - x0) * sx, margin + (y - y0) * sy) for x, y in pts]


def se(c, s, n):
    """超楕円の向き（cos, sin を指数 n の超楕円に）"""
    return math.copysign(abs(c) ** (2 / n), c), math.copysign(abs(s) ** (2 / n), s)


def smooth01(u):
    """0→1 を両端の傾き 0 でつなぐ（9-slice の境目で輪郭が折れない）"""
    u = min(max(u, 0.0), 1.0)
    return u * u * (3 - 2 * u)


def blob_bubble():
    """会話の吹き出し（384×256、9-slice の border 左右 120・上下 96。【12】）
    超楕円（指数 3.2）。半径に波（1周に 3 つ＋5 つ、位相をずらす）。下の辺だけ波を 4 つ。
    plan の値は ±1.5%・±3px。【12】と並べると波が弱く、ただの角丸に見えたので ±2.5%（1.4%＋1.1%）・±4px に上げた。
    下の波は周期 96 で、9-slice の境目（中心から ±72）がちょうど山と谷になる（傾き 0 なので伸ばしても折れない）"""
    w, h, n, k = 384, 256, 3.2, 1440
    a, b = w / 2, h / 2
    pts = []
    for i in range(k):
        t = 2 * math.pi * i / k
        u, v = se(math.cos(t), math.sin(t), n)
        f = 1 + 0.014 * math.sin(3 * t + 0.7) + 0.011 * math.sin(5 * t + 2.1)
        pts.append((a + a * u * f, b + b * v * f))
    pts = fit(pts, w, h - 8, 1.5)  # 下の波 4px ぶんを空けておく
    out = []
    for x, y in pts:
        wt = max(0.0, (y - (h - 8) / 2) / ((h - 8) / 2 - 1.5)) ** 6  # 下の辺だけ（横の辺には効かない）
        out.append((x, y + 4 + 4 * wt * math.sin(2 * math.pi * (x - w / 2) / 96)))
    save(poly(w, h, out), "blob_bubble.png", w, h)


def corner_pts(cx, cy, r, sx, sy, n, rev, k=90):
    """超楕円（指数 n）の角。中心 (cx, cy)、半径 r、向き (sx, sy)。rev で順を逆に"""
    ts = [math.pi / 2 * i / k for i in range(k + 1)]
    if rev:
        ts.reverse()
    out = []
    for t in ts:
        u, v = abs(math.cos(t)) ** (2 / n), abs(math.sin(t)) ** (2 / n)
        out.append((cx + sx * r * u, cy + sy * r * v))
    return out


def blob_panel():
    """パネル・選択肢・盆・計器（192×192、9-slice の border 72。【10】【05】）
    角の半径 左上 52・右上 64・右下 56・左下 68、角は超楕円（指数 2.6）。border 72 は一番大きい角 68 より大きい。
    4 つの辺は真ん中（9-slice で伸びる所、72〜120）だけ 3px ふくらませる（sin²。境目で傾き 0 なので折れない）。
    plan には無いが、辺がまっすぐだと【10】と並べてただの角丸の四角に見えたので、原則 1「辺がふくらむ」に合わせて足した。
    伸ばすと、辺全体がゆるくふくらんだ形になる（原則 1。【10】の豆形）"""
    w = h = 192
    m, n, bul, bd = 1.0, 2.6, 3.0, 72
    tl, tr, br, bl = 52, 64, 56, 68
    e = m + bul  # 角と、ふくらんでいない辺の位置

    def edge(a, b, k=96):  # 9-slice の真ん中（bd〜w−bd）のふくらみ。a→b の順に
        out = []
        for i in range(1, k):
            t = a + (b - a) * i / k
            u = (t - bd) / (w - 2 * bd)
            out.append((t, bul * math.sin(math.pi * u) ** 2 if 0 <= u <= 1 else 0.0))
        return out

    pts = []
    pts += corner_pts(e + tl, e + tl, tl, -1, -1, n, False)                       # 左の辺 → 上の辺
    pts += [(x, e - d) for x, d in edge(e + tl, w - e - tr)]                      # 上の辺
    pts += corner_pts(w - e - tr, e + tr, tr, 1, -1, n, True)                     # 上の辺 → 右の辺
    pts += [(w - e + d, y) for y, d in edge(e + tr, h - e - br)]                  # 右の辺
    pts += corner_pts(w - e - br, h - e - br, br, 1, 1, n, False)                 # 右の辺 → 下の辺
    pts += [(x, h - e + d) for x, d in edge(w - e - br, e + bl)]                  # 下の辺
    pts += corner_pts(e + bl, h - e - bl, bl, -1, 1, n, True)                     # 下の辺 → 左の辺
    pts += [(e - d, y) for y, d in edge(h - e - bl, e + tl)]                      # 左の辺
    save(poly(w, h, pts), "blob_panel.png", w, h)


def blob_pill():
    """名前タグ・品名の札・チップ・ボタン（192×96、9-slice の border 左右 48・上下 0。【12】【09】）
    両端は指数 2.5 の超楕円（半円より少し四角い）。上下の辺を中央で 2px ふくらませる。
    左端の高さ 92・右端 88（左を 4px 太らせる）。中の辺は両端で傾き 0 なので、横に伸ばしても折れない"""
    w, h = 192, 96
    m, n, cap = 1.0, 2.5, 48
    cy = h / 2
    hl, hr, bulge = 46.0, 44.0, 2.0     # 左右の端の半分の高さ、ふくらみ
    k = 120

    def half(x):  # 中の辺の半分の高さ
        u = (x - cap) / (w - 2 * cap)
        return hl + (hr - hl) * smooth01(u) + bulge * math.sin(math.pi * u) ** 2

    top, bot = [], []
    for i in range(k + 1):  # 左端（左 → 上へ）
        t = math.pi / 2 * i / k
        u, v = abs(math.cos(t)) ** (2 / n), abs(math.sin(t)) ** (2 / n)
        top.append((cap - (cap - m) * u, cy - hl * v))
    for i in range(1, k):   # 中の上の辺
        x = cap + (w - 2 * cap) * i / k
        top.append((x, cy - half(x)))
    for i in range(k + 1):  # 右端（上 → 右へ）
        t = math.pi / 2 * (k - i) / k
        u, v = abs(math.cos(t)) ** (2 / n), abs(math.sin(t)) ** (2 / n)
        top.append((w - cap + (cap - m) * u, cy - hr * v))
    bot = [(x, 2 * cy - y) for x, y in reversed(top)]
    save(poly(w, h, top + bot), "blob_pill.png", w, h)


def blob_disc():
    """アイコンの台・丸い枠・タブの帯のこぶ（128×128、Simple。【01】【10】）
    指数 2.4 の超楕円に ±2% の波（3 つ）"""
    w = h = 128
    k, n = 1080, 2.4
    pts = []
    for i in range(k):
        t = 2 * math.pi * i / k
        u, v = se(math.cos(t), math.sin(t), n)
        f = 1 + 0.02 * math.sin(3 * t + 0.4)
        pts.append((u * f, v * f))
    save(poly(w, h, fit(pts, w, h, 1.0)), "blob_disc.png", w, h)


def wave_edge():
    """面の端の波の縁（96×24、横に Repeat。【04】【11】）
    下半分（12px）は白。上に半径 24 の円の頭のこぶを 2 つ（周期 48、こぶの高さ 12）。
    谷は円と下半分の境目の角を半径 4 ほどで丸めた（smooth min）。左右の端はどちらも谷なので、横につなげても切れない"""
    w, h, r, base, kk = 96, 24, 24, 12.0, 4.0
    pts = []
    steps = w * 8
    for i in range(steps + 1):
        x = w * i / steps
        y = 1e9
        for c in (-24, 24, 72, 120):
            d = x - c
            if abs(d) < r:
                y = min(y, h - math.sqrt(r * r - d * d))
        # 円の頭（y）と下半分の上端（base）の小さい方を、角を丸めて取る
        hh = max(kk - abs(y - base), 0.0) / kk
        pts.append((x, min(y, base) - hh * hh * kk / 4))
    pts = [(x, max(y, 0.5)) for x, y in pts]
    save(poly(w, h, pts + [(w, h), (0, h)]), "wave_edge.png", w, h)


def caret_soft():
    """吹き出しの ▼（32×22、角の半径 5。【12】）
    三角を内側に 5 縮めてから 5 ふくらませる（角だけが丸くなり、外形は 32×22 のまま）"""
    w, h, r = 32, 22, 5.0
    tri = [(0.0, 0.0), (w, 0.0), (w / 2, h)]
    side = math.hypot(w / 2, h)
    area = w * h / 2
    rho = area / ((w + 2 * side) / 2)            # 内接円の半径
    ix, iy = w / 2, rho                          # 内心
    sc = (rho - r) / rho
    inner = [(ix + (x - ix) * sc, iy + (y - iy) * sc) for x, y in tri]
    m = mask(w, h)
    d = ImageDraw.Draw(m)
    d.polygon([(x * SS, y * SS) for x, y in inner], fill=255)
    for i in range(3):
        (x0, y0), (x1, y1) = inner[i], inner[(i + 1) % 3]
        d.line([(x0 * SS, y0 * SS), (x1 * SS, y1 * SS)], fill=255, width=int(2 * r * SS))
    for x, y in inner:
        d.ellipse(((x - r) * SS, (y - r) * SS, (x + r) * SS, (y + r) * SS), fill=255)
    save(m, "caret_soft.png", w, h)


def bands():
    """パネルの中の横縞（4×128、縦に Repeat。上 64 が透明・下 64 が白。【05】）。Image の Color を panelBand に"""
    out = Image.new("RGBA", (4, 128), (255, 255, 255, 0))
    out.paste((255, 255, 255, 255), (0, 64, 4, 128))
    out.save(DST / "bands64.png", optimize=True)
    print("bands64.png  4×128")


# glove_point の色（ガイドライン 2 章のトークン。palette.json と UI 専用の色）
PANEL = (0xFB, 0xF7, 0xEE)
TEXT = (0x5C, 0x4A, 0x3A)
ACCENT = (0xF2, 0x8C, 0x28)


def glove_point():
    """指さしの手（144×144、色つき。右向き。【09】【10】は役目と大きさだけの基準）
    宇宙服のグローブ：手首の輪（accent）→ 丸い手のひら → 人さし指のカプセル（長さ 56・太さ 30）
    → 上に親指のこぶ、下ににぎった指のこぶ 2 つ。面 panel、輪郭 6px text。4本指の白手袋にしない。
    部品ごとに「輪郭（6 ふくらませた形）→ 面」を奥から順に重ねるので、重なりに線が残る。
    指の先は (136, 56) あたり。左上向きは 135° 回して使う"""
    w = h = 144
    ol = 6.0
    img = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    def ell(cx, cy, rx, ry, col, grow=0.0):
        d.ellipse(((cx - rx - grow) * SS, (cy - ry - grow) * SS, (cx + rx + grow) * SS, (cy + ry + grow) * SS), fill=col)

    def cap(x0, y0, x1, y1, r, col, grow=0.0):  # カプセル（両端が半円の太い線）
        rr = r + grow
        d.line([(x0 * SS, y0 * SS), (x1 * SS, y1 * SS)], fill=col, width=int(2 * rr * SS))
        for x, y in ((x0, y0), (x1, y1)):
            d.ellipse(((x - rr) * SS, (y - rr) * SS, (x + rr) * SS, (y + rr) * SS), fill=col)

    def part(fn, *args, col=PANEL):
        fn(*args, TEXT, ol)
        fn(*args, col)

    ox, oy = 8, -6  # 全体を枠の真ん中に寄せる
    part(ell, 50 + ox, 84 + oy, 28, 30)                   # 手のひら
    part(cap, 64 + ox, 104 + oy, 76 + ox, 104 + oy, 11)   # にぎった指 2（下。手のひらの前に重ね、指の線を見せる）
    part(cap, 68 + ox, 83 + oy, 82 + ox, 83 + oy, 12)     # にぎった指 1（人さし指のすぐ下）
    part(cap, 18 + ox, 68 + oy, 18 + ox, 100 + oy, 11, col=ACCENT)  # 手首の輪
    part(cap, 81 + ox, 62 + oy, 107 + ox, 62 + oy, 15)   # 人さし指（長さ 56＝中心の間 26＋太さ 30）。ほぼ全部が手のひらの外に出る
    part(cap, 56 + ox, 52 + oy, 68 + ox, 50 + oy, 10)    # 親指のこぶ（人さし指の付け根の上に重ねる）
    pm = img.convert("RGBa").resize((w, h), Image.Resampling.BOX).convert("RGBA")
    pm.save(DST / "glove_point.png", optimize=True)
    print(f"glove_point.png  {w}×{h}")


# ------------------------------------------------------------------ 見本の絵（--preview）
# 全部の sprite を cream と濃紺の上に並べ、Unity での使い方（9-slice・色・重ね方）をまねて組んだ見本を描く。
# 基準の画像（docs/reference/art-ref/acnh/）と横に並べて見比べるための物。ゲームには入れない。
# 値はガイドライン 2・3 章と tmp/handoff/190-plan.md の 2 章（UiTokens に足す予定の値）

PREVIEW = ROOT / "art/ui/preview_acnh.png"
FONTS = ROOT / "unity/Assets/Art/UI/Fonts"
C = {k: tuple(int(v[i:i + 2], 16) for i in (0, 2, 4)) for k, v in dict(
    bg="F2E3C6", bgDeep="E7D4AE", panel="FBF7EE", panelBand="F4EDDC", choice="FBEFC0", text="5C4A3A",
    textSub="705C49", ink="1D2D50", go="2A9D8F", goLight="4DB8AA", accent="F28C28", plum="8A6BBE",
    danger="C81E4A", line="C8CCD4", seatRed="E4572E", seatBlue="3A86FF").items()}


def font(name, size):
    from PIL import ImageFont
    return ImageFont.truetype(str(FONTS / name), size)


def tint(spr, rgb, a=1.0):
    """白＋アルファの sprite に色を付ける（Image の Color と同じ）"""
    out = Image.new("RGBA", spr.size, rgb + (0,))
    out.putalpha(spr.getchannel("A").point(lambda v: int(v * a)))
    return out


def slice9(spr, border, size, scale=1.0):
    """Image の Sliced をまねる。border＝(左, 上, 右, 下)、scale＝1／Pixels Per Unit Multiplier"""
    l, t, r, b = border
    w, h = spr.size
    W, H = size
    L, T, R, B = (round(v * scale) for v in border)
    xs, Xs = [0, l, w - r, w], [0, L, W - R, W]
    ys, Ys = [0, t, h - b, h], [0, T, H - B, H]
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for i in range(3):
        for j in range(3):
            src = (xs[i], ys[j], xs[i + 1], ys[j + 1])
            dw, dh = Xs[i + 1] - Xs[i], Ys[j + 1] - Ys[j]
            if src[2] > src[0] and src[3] > src[1] and dw > 0 and dh > 0:
                piece = spr.crop(src).convert("RGBa").resize((dw, dh), Image.Resampling.LANCZOS).convert("RGBA")
                out.paste(piece, (Xs[i], Ys[j]))
    return out


def tiled(spr, size, offset=(0, 0)):
    W, H = size
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    w, h = spr.size
    for y in range(-(offset[1] % h), H, h):
        for x in range(-(offset[0] % w), W, w):
            out.paste(spr, (x, y))
    return out


def clip(img, shape):
    """img の不透明度を shape のアルファでかける（Mask のまね）"""
    from PIL import ImageChops
    out = img.copy()
    out.putalpha(ImageChops.multiply(img.getchannel("A"), shape.getchannel("A")))
    return out


def put(canvas, img, xy, angle=0.0, center=False):
    if angle:
        cx, cy = (xy[0] + img.width / 2, xy[1] + img.height / 2) if not center else xy
        img = img.rotate(angle, resample=Image.Resampling.BICUBIC, expand=True)
        xy = (round(cx - img.width / 2), round(cy - img.height / 2))
    elif center:
        xy = (round(xy[0] - img.width / 2), round(xy[1] - img.height / 2))
    canvas.alpha_composite(img, (int(xy[0]), int(xy[1])))


def text_on(canvas, xy, text, f, rgb, anchor="la"):
    ImageDraw.Draw(canvas).text(xy, text, font=f, fill=rgb, anchor=anchor)


def pill_with_text(sp, text, f, face, ink, h, pad=28, rim=None, rimw=0, stripes=None):
    """blob_pill のピル（高さ h）に文字。rim＝ふちの色（外のピルを rim、rimw 内側に face）。stripes＝(縞, 色, 不透明度)"""
    tw = ImageDraw.Draw(Image.new("L", (1, 1))).textlength(text, font=f)
    W = int(tw + 2 * pad)
    out = Image.new("RGBA", (W, h), (0, 0, 0, 0))
    border = (48, 0, 48, 0)
    if rim:
        out.alpha_composite(tint(slice9(sp, border, (W, h), h / 96), rim))
        inner = slice9(sp, border, (W - 2 * rimw, h - 2 * rimw), (h - 2 * rimw) / 96)
        face_img = tint(inner, face)
        if stripes:
            st, col, a = stripes
            face_img.alpha_composite(clip(tint(tiled(st, inner.size), col, a), inner))
        out.alpha_composite(face_img, (rimw, rimw))
    else:
        out.alpha_composite(tint(slice9(sp, border, (W, h), h / 96), face))
    text_on(out, (W / 2, h / 2), text, f, ink, "mm")
    return out


def preview():
    S = {n: Image.open(DST / f"{n}.png").convert("RGBA") for n in (
        "blob_bubble", "blob_panel", "blob_pill", "blob_disc", "wave_edge", "caret_soft", "bands64",
        "glove_point", "stripes64")}
    icon = lambda n, d, rgb: tint(Image.open(ROOT / f"unity/Assets/Art/UI/Icons/{n}.png").convert("RGBA")
                                  .resize((d, d), Image.Resampling.LANCZOS), rgb)
    disc = lambda d, rgb: tint(S["blob_disc"].resize((d, d), Image.Resampling.LANCZOS), rgb)
    W, H, navy_y = 1600, 1800, 1010
    cv = Image.new("RGBA", (W, H), C["bg"] + (255,))
    cv.paste(C["ink"] + (255,), (0, navy_y, W, H))
    fXB, fB, fBal = "Nunito-ExtraBold.ttf", "Nunito-Bold.ttf", "Baloo2-ExtraBold.ttf"
    if not (FONTS / fXB).exists():
        fXB = fB
    f18, f32, f36 = font(fB, 18), font(fXB, 32), font(fB, 36)
    d = ImageDraw.Draw(cv)
    hand = S["glove_point"].resize((72, 72), Image.Resampling.LANCZOS)                     # 右向き
    hand_ul = S["glove_point"].rotate(135, resample=Image.Resampling.BICUBIC).resize((72, 72), Image.Resampling.LANCZOS)  # 左上向き
    B9 = dict(blob_bubble=(120, 96, 120, 96), blob_panel=(72, 72, 72, 72), blob_pill=(48, 0, 48, 0))
    stripes = (S["stripes64"], C["goLight"], 0.35 / 0.4)  # stripes64 は白の 40%。goLight 35% にする

    def check(xy):  # チェック【09】：go の丸 40 に白の check
        cv.alpha_composite(disc(40, C["go"]), xy)
        cv.alpha_composite(icon("check", 26, C["panel"]), (xy[0] + 7, xy[1] + 7))

    # ---- sprite そのもの（cream の上は面の色、濃紺の上は panel）。点線＝9-slice の border
    def raw_row(y, dark):
        x = 32
        for name, col, wdt in (("blob_bubble", "panel", 384), ("blob_panel", "panel", 192), ("blob_pill", "go", 192),
                               ("blob_disc", "accent", 128), ("wave_edge", "panel", 96), ("caret_soft", "accent", 60),
                               ("bands64", "panelBand", 60), ("glove_point", None, 144)):
            sp = S[name] if name != "bands64" else tiled(S[name], (32, 192))
            img = sp if col is None else tint(sp, C["panel"] if dark and col in ("panel", "panelBand") else C[col])
            if name == "bands64":
                base = Image.new("RGBA", sp.size, C["panel"] + (255,))
                base.alpha_composite(tint(sp, C["panelBand"]))
                img = base
            cv.alpha_composite(img, (x, y))
            if name in B9:
                l, t, r, b = B9[name]
                gc = C["accent"] if dark else C["text"]
                for gx in (x + l, x + sp.width - r):
                    for yy in range(y, y + sp.height, 8):
                        d.line([(gx, yy), (gx, yy + 3)], fill=gc, width=1)
                if t:
                    for gy in (y + t, y + sp.height - b):
                        for xx in range(x, x + sp.width, 8):
                            d.line([(xx, gy), (xx + 3, gy)], fill=gc, width=1)
            lc = C["panel"] if dark else C["textSub"]
            text_on(cv, (x, y + 262), name, f18, lc)
            sz = f"{S[name].width}×{S[name].height}" + (" b{}/{}".format(B9[name][0], B9[name][1]) if name in B9 else "")
            text_on(cv, (x, y + 284), sz, f18, lc)
            x += wdt + 40

    raw_row(24, False)
    raw_row(navy_y + 24, True)

    # ---- 会話の吹き出し【12】（1040×260、名前タグ、▼。固有名詞は go、数字は accent）
    bx, by = 32, 380
    cv.alpha_composite(tint(slice9(S["blob_bubble"], B9["blob_bubble"], (1040, 260)), C["panel"]), (bx, by))
    def say(x, y, parts):
        for word, col in parts:
            text_on(cv, (x, y), word, f36, C[col])
            x += d.textlength(word, font=f36)
    say(bx + 72, by + 52, [("Good morning, crew! You have ", "text"), ("500", "accent"), (" to build your ship.", "text")])
    say(bx + 72, by + 106, [("Tap a part to put it on ", "text"), ("Orion", "go"), (".", "text")])
    put(cv, pill_with_text(S["blob_pill"], "Houston", font(fXB, 26), C["go"], C["panel"], 48, pad=26), (bx + 36, by - 26), angle=6)
    car = tint(S["caret_soft"], C["accent"])
    cv.alpha_composite(car, (bx + 520 - 16, by + 260 - 11))

    # ---- 選択肢の吹き出し【10】（choice の blob_panel、行 80、マーカー mark、右向きの手）
    cx, cy, cw, ch = 1150, 392, 400, 2 * 80 + 64
    # Pixels Per Unit Multiplier 0.62（角を 1.6 倍にして、角が高さの半分近くになる豆形に。【10】）
    cv.alpha_composite(tint(slice9(S["blob_panel"], B9["blob_panel"], (cw, ch), 1 / 0.62), C["choice"]), (cx, cy))
    for i, word in enumerate(("Small rocket", "Large rocket")):
        ty = cy + 32 + 80 * i + 40
        if i == 1:
            tw = d.textlength(word, font=f32)
            mk = tint(slice9(S["blob_pill"], B9["blob_pill"], (int(tw + 32), 24), 24 / 96), C["accent"], 0.45)
            cv.alpha_composite(mk, (int(cx + cw / 2 - tw / 2 - 16), ty + 2))
            cv.alpha_composite(hand, (int(cx + cw / 2 - tw / 2 - 16 - 78), ty - 38))
        text_on(cv, (cx + cw / 2, ty), word, f32, C["text"], "mm")

    # ---- 主ボタン【05】・副ボタン・数のチップ【09】
    ry = 720
    main = pill_with_text(S["blob_pill"], "Ready for launch!", f32, C["go"], C["panel"], 96, pad=56,
                          rim=C["goLight"], rimw=5, stripes=stripes)
    cv.alpha_composite(main, (32, ry))
    cv.alpha_composite(hand_ul, (32 + main.width - 46, ry + 50))
    sub_b = pill_with_text(S["blob_pill"], "Back", font(fXB, 30), C["panel"], C["text"], 72, pad=40, rim=C["bgDeep"], rimw=5)
    cv.alpha_composite(sub_b, (32, ry + 150))
    chx, chy = 520, ry + 30
    chip = pill_with_text(S["blob_pill"], "420", font(fBal, 40), C["text"], C["panel"], 88, pad=48)
    cv.alpha_composite(chip, (chx, chy))
    cv.alpha_composite(disc(48, C["accent"]), (chx + chip.width // 2 - 24, chy - 28))
    cv.alpha_composite(icon("cost", 30, C["panel"]), (chx + chip.width // 2 - 15, chy - 19))
    unit = pill_with_text(S["blob_pill"], "left", font(fXB, 22), C["panel"], C["text"], 36, pad=16)
    put(cv, unit, (chx + chip.width - unit.width + 16, chy + 88 - 24), angle=4)
    # ヒントのピル（右下。panel 90%、text の丸に白いアイコン）
    hint = pill_with_text(S["blob_pill"], "    Skip", font(fB, 26), C["panel"], C["text"], 56, pad=24)
    cv.alpha_composite(hint, (chx, ry + 160))
    cv.alpha_composite(disc(36, C["text"]), (chx + 14, ry + 170))
    cv.alpha_composite(icon("enter", 22, C["panel"]), (chx + 21, ry + 177))

    # ---- タブの帯【09】（blob_disc 112 を 16 ずつ重ねる、面 bgDeep。選んだタブは accent の台 80＋名前の札）
    # 16 ずつ重ねるだけだと丸どうしの谷が深く、別々の丸に見える。後ろに blob_pill（高さ 84）を敷いて1本の帯にした
    tx, ty = 760, ry + 40
    cv.alpha_composite(tint(slice9(S["blob_pill"], B9["blob_pill"], (4 * 96 + 112 - 16, 84), 84 / 96), C["bgDeep"]), (tx + 8, ty + 14))
    for i in range(5):
        cv.alpha_composite(disc(112, C["bgDeep"]), (tx + i * 96, ty))
    cv.alpha_composite(disc(80, C["accent"]), (tx + 2 * 96 + 16, ty + 16))
    for i, n_ in enumerate(("rocket", "power", "camera", "science", "mass")):
        col = C["panel"] if i == 2 else C["textSub"]
        cv.alpha_composite(icon(n_, 56 if i != 2 else 48, col), (tx + i * 96 + (28 if i != 2 else 32), ty + (28 if i != 2 else 32)))
    put(cv, pill_with_text(S["blob_pill"], "Science", font(fXB, 22), C["accent"], C["panel"], 40, pad=18),
        (tx + 2 * 96 + 56, ty - 4), center=True)

    # ---- グリッドの選択【09】【10】（丸い枠 bgDeep の blob_disc 96、品名の札 −3°、チェック、左上向きの手）
    gx, gy = 760, ry + 190
    for i, (n_, col) in enumerate((("battery", "go"), ("camera", "plum"), ("radar", "accent"), ("science", "ink"))):
        cv.alpha_composite(disc(96, C["bgDeep"]), (gx + i * 120, gy))
        cv.alpha_composite(disc(64, C[col]), (gx + i * 120 + 16, gy + 16))
        cv.alpha_composite(icon(n_, 38, C["panel"]), (gx + i * 120 + 29, gy + 29))
    check((gx + 120 + 66, gy - 6))
    cv.alpha_composite(hand_ul, (gx + 120 + 58, gy + 56))

    # ---- パネル【05】（外＝白 55%、内＝panel を 12 内側、中に横縞 bands64＝panelBand）
    px_, py_, pw, ph = 1290, 690, 280, 300
    cv.alpha_composite(tint(slice9(S["blob_panel"], B9["blob_panel"], (pw, ph)), (255, 255, 255), 0.55), (px_, py_))
    inner = slice9(S["blob_panel"], B9["blob_panel"], (pw - 24, ph - 24))
    face = tint(inner, C["panel"])
    face.alpha_composite(clip(tint(tiled(S["bands64"], inner.size, (0, 64 - 20)), C["panelBand"]), inner))
    cv.alpha_composite(face, (px_ + 12, py_ + 12))
    text_on(cv, (px_ + 36, py_ + 36), "Launch check", font(fXB, 30), C["text"])
    for i, (ic, col, word) in enumerate((("check", "go", "Power"), ("check", "go", "Mass"), ("cross", "danger", "Cost"))):
        yy = py_ + 100 + i * 60
        cv.alpha_composite(disc(40, C[col]), (px_ + 36, yy))
        cv.alpha_composite(icon(ic, 26, C["panel"]), (px_ + 43, yy + 7))
        text_on(cv, (px_ + 92, yy + 20), word, font(fB, 28), C["text"], "lm")

    # ---- 濃紺：船内の吹き出し（880×180）・小さな吹き出し【08】
    ny = navy_y + 380
    cv.alpha_composite(tint(slice9(S["blob_bubble"], B9["blob_bubble"], (880, 180), 0.75), C["panel"]), (640, ny))
    say(640 + 72, ny + 36, [("Burn is go. Fire when ", "text"), ("ready", "accent"), ("!", "text")])
    put(cv, pill_with_text(S["blob_pill"], "Houston", font(fXB, 26), C["go"], C["panel"], 48, pad=26), (640 + 36, ny - 26), angle=6)
    cv.alpha_composite(car, (640 + 440 - 16, ny + 180 - 11))
    sb = pill_with_text(S["blob_pill"], "Aki", font(fXB, 26), C["panel"], C["text"], 56, pad=28)
    cv.alpha_composite(sb, (360, ny + 30))
    cv.alpha_composite(disc(18, C["panel"]), (360 + 26, ny + 30 + 58))
    cv.alpha_composite(disc(10, C["panel"]), (360 + 18, ny + 30 + 80))

    # ---- 船内の盆【04】【06】（panel の面＋上端に wave_edge、席の名前タグ、丸い枠の選択、主ボタン）
    tray_y = navy_y + 600
    cv.paste(C["panel"] + (255,), (0, tray_y + 12, W, H))
    cv.alpha_composite(tint(tiled(S["wave_edge"], (W, 24)), C["panel"]), (0, tray_y))
    put(cv, pill_with_text(S["blob_pill"], "Pilot", font(fXB, 26), C["seatRed"], C["panel"], 48, pad=26), (40, tray_y - 6), angle=6)
    for i, (n_, col) in enumerate((("earth", "go"), ("moon", "ink"), ("ignition", "accent"), ("heat_shield", "plum"))):
        cv.alpha_composite(disc(96, C["bgDeep"]), (60 + i * 120, tray_y + 56))
        cv.alpha_composite(disc(64, C[col]), (76 + i * 120, tray_y + 72))
        cv.alpha_composite(icon(n_, 38, C["panel"]), (89 + i * 120, tray_y + 85))
    check((60 + 120 + 66, tray_y + 50))
    cv.alpha_composite(hand_ul, (60 + 120 + 58, tray_y + 112))
    # 品名の札【09】【10】（go、高さ 64、白文字 32、下に小さな三角、3° 右上がり）
    tagm = pill_with_text(S["blob_pill"], "Moon", f32, C["go"], C["panel"], 64, pad=30)
    tag2 = Image.new("RGBA", (tagm.width, tagm.height + 12), (0, 0, 0, 0))
    tag2.alpha_composite(tint(S["caret_soft"].resize((24, 16), Image.Resampling.LANCZOS), C["go"]), (26, tagm.height - 4))
    tag2.alpha_composite(tagm)
    put(cv, tag2, (60 + 120 + 6, tray_y - 44), angle=3)
    ign = pill_with_text(S["blob_pill"], "Ignite!", f32, C["go"], C["panel"], 96, pad=60, rim=C["goLight"], rimw=5, stripes=stripes)
    cv.alpha_composite(ign, (W - ign.width - 80, tray_y + 50))
    cv.alpha_composite(hand_ul, (W - 80 - 46, tray_y + 100))

    cv.convert("RGB").save(PREVIEW, optimize=True)
    print(f"preview: {PREVIEW.relative_to(ROOT)}  {W}×{H}")


def main():
    DST.mkdir(parents=True, exist_ok=True)
    for r in (16, 28, 40):
        rounded(r)
    pill()
    caret_down()
    stripes()
    blob_bubble()
    blob_panel()
    blob_pill()
    blob_disc()
    wave_edge()
    caret_soft()
    bands()
    glove_point()
    if "--preview" in sys.argv:
        preview()


if __name__ == "__main__":
    main()
