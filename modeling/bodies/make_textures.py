"""地球・月の画像を、デフォルメの船内と並べて浮かない絵に加工する（Blender は使わない。ふつうの python3 で流す）。

書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14

  python3 -I modeling/bodies/make_textures.py            → modeling/bodies/textures/ と unity/Assets/Models/Bodies/ に出す
  python3 -I modeling/bodies/make_textures.py <出力先>    → 試し書き（そのフォルダにだけ出す）

元：modeling/source/textures/ の NASA の画像（出典は modeling/source/README.md と CREDITS.md）。元のファイルは変えない。
出すもの：earth_1024.png・moon_1024.png（1024×512、正距円筒、左端＝経度 −180°、上＝北）。
FBX のマテリアル（M_Body_Earth・M_Body_Moon）はこの名前で画像を探すので、名前を変えるときは build_bodies.py も直す。

やること（両方とも同じ順番）
  1. 縮める（左右をつないだまま。経度 180° の継ぎ目で色がずれないように、左右に折り返しを足してから縮めて切る）
  2. 極の近くの行を、その行の平均色に寄せる（球の極で絵が1点に集まって「つぶれ」が見えるのを消す）
  3. ぼかしてから色数を減らす（細かい点々が残らず、塗り分けの絵になる）。ディザなし。
     ぼかしは「球の上で同じ幅」（極に近い行ほど横に強く。sphere_blur）。ふつうにぼかすと、極のまわりで
     減らした色の境目が放射状のトゲになる
  4. 減らした色ごとに、彩度を少し上げ、明るさの幅を船内の明るい色に寄せる
     地球は 海・陸・氷 に分けてから別々に減らす（暗い熱帯雨林と深い海が同じ色にまとめられないように）。
     海は明るい青にする（背景の濃紺 #1D2D50 に海が沈まないように）。氷はパレットの白 #FBF7EE に寄せる
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
SRC = os.path.join(REPO, "modeling", "source", "textures")
SIZE = (1024, 512)
PAD = 32            # 左右の折り返し（px、縮めた後の大きさで）

EARTH = dict(src="earth_bmng_5400x2700.jpg", out="earth_1024.png",
             ocean_colors=4,       # 海・陸・氷を別々に減らす。それぞれの色数
             land_colors=8,
             ice_colors=2,
             blur=1.6,             # 減らす前のぼかし（px）
             ocean_blur=4.0,       # 海だけ、もっとぼかす（海底の筋で縞だらけにならないように）
             pole_rows=0.07,       # 上下この割合の行を平均色に寄せる
             land_sat=1.40,        # 陸の彩度の倍率
             land_v=(0.44, 0.98),  # 陸の明るさ（HSV の V）を詰め直す幅
             ocean_hue=(0.565, 0.615),  # 海の色相の幅（青。パレットの seat_blue #3A86FF は 0.60）
             ocean_sat=(0.52, 0.68),    # 海の彩度（深い→浅い）
             ocean_v=(0.60, 0.87),      # 海の明るさ（深い→浅い）。背景の濃紺 #1D2D50（V 0.31）より十分明るく
             ice_v=(0.86, 0.98))        # 氷の明るさ。色はパレットの white #FBF7EE に寄せる
MOON = dict(src="moon_lroc_color_poles_1k.jpg", out="moon_1024.png",
            blur=2.4, pole_rows=0.06, sat=1.10, v_lo=0.54, v_hi=0.94,
            cuts=(17, 45, 78, 96))   # 明るさの段の境（画素の数の百分位）。5段になる
MOON_TINT = (0.98, 0.99, 1.0)    # 月はほんの少しだけ青みに（ゲームの光が暖色 #FFE2B8 なので、画像まで暖色にすると茶色い星に見える）


def wrap_resize(img, size, pad=PAD):
    """左右をつないだまま縮める。元の左右に折り返しを足して縮め、真ん中を切り出す。"""
    w, h = img.size
    ps = round(pad * w / size[0])
    wide = Image.new(img.mode, (w + 2 * ps, h))
    wide.paste(img.crop((w - ps, 0, w, h)), (0, 0))
    wide.paste(img, (ps, 0))
    wide.paste(img.crop((0, 0, ps, h)), (w + ps, 0))
    small = wide.resize((size[0] + 2 * pad, size[1]), Image.LANCZOS)
    return small.crop((pad, 0, pad + size[0], size[1]))


def wrap_filter(img, flt, pad=PAD):
    """左右をつないだままフィルタをかける（継ぎ目で色がずれないように）。"""
    w, h = img.size
    wide = Image.new(img.mode, (w + 2 * pad, h))
    wide.paste(img.crop((w - pad, 0, w, h)), (0, 0))
    wide.paste(img, (pad, 0))
    wide.paste(img.crop((0, 0, pad, h)), (w + pad, 0))
    return wide.filter(flt).crop((pad, 0, pad + w, h))


def sphere_blur(img, sigma):
    """球の上でどこも同じくらいにぼかす。正距円筒では極に近い行ほど横に引き伸ばされているので、
    行ごとに横だけ σ·tan(緯度) を足してぼかし（左右はつながったまま。FFT で輪のまま畳み込む）、
    そのあと全体を σ でぼかす。極のまわりで模様が放射状のトゲ（極のつぶれ）になるのを防ぐ。"""
    arr = np.asarray(img).astype(np.float64)
    h, w = arr.shape[:2]
    lat = (0.5 - (np.arange(h) + 0.5) / h) * np.pi
    sig = np.minimum(sigma * np.abs(np.tan(lat)), w / 2)               # 行ごとの横の σ（px）
    f = np.fft.rfftfreq(w)                                               # 周期／px
    gain = np.exp(-2 * (np.pi * f[None, :] * sig[:, None]) ** 2)        # ガウスの周波数応答
    spec = np.fft.rfft(arr, axis=1) * gain[:, :, None]
    arr = np.fft.irfft(spec, n=w, axis=1)
    out = Image.fromarray(np.clip(arr + 0.5, 0, 255).astype(np.uint8))
    return wrap_filter(out, ImageFilter.GaussianBlur(sigma))


def flatten_poles(arr, frac):
    """上下 frac の行を、行の平均色に寄せる（極で 1 に、frac の境目で 0 になる重み）。"""
    h = arr.shape[0]
    n = max(1, round(h * frac))
    out = arr.copy()
    for i in range(n):
        t = 1 - i / n                    # 極で 1
        wgt = t * t * (3 - 2 * t)        # なめらかに
        for r in (i, h - 1 - i):
            out[r] = arr[r] * (1 - wgt) + arr[r].mean(axis=0) * wgt
    return out


def rgb_to_hsv(c):
    r, g, b = c
    mx, mn = max(c), min(c)
    v = mx
    s = 0 if mx == 0 else (mx - mn) / mx
    if mx == mn:
        hue = 0.0
    elif mx == r:
        hue = ((g - b) / (mx - mn)) % 6
    elif mx == g:
        hue = (b - r) / (mx - mn) + 2
    else:
        hue = (r - g) / (mx - mn) + 4
    return hue / 6, s, v


def hsv_to_rgb(hue, s, v):
    i = int(hue * 6) % 6
    f = hue * 6 - int(hue * 6)
    p, q, t = v * (1 - s), v * (1 - f * s), v * (1 - (1 - f) * s)
    return [(v, t, p), (q, v, p), (p, v, t), (p, q, v), (t, p, v), (v, p, q)][i]


def _lerp(a, b, t):
    return a + (b - a) * min(1.0, max(0.0, t))


def restyle_moon(color, t, cfg):
    """月：段の色。明るさは段の順に v_lo〜v_hi へ等間隔に並べ直し、色み（灰色からのずれ）は sat 倍して残す。
    最後に MOON_TINT でほんの少し暖かくする。t は 0（いちばん暗い段）〜1。"""
    c = np.asarray(color, np.float64) / 255
    v = _lerp(cfg["v_lo"], cfg["v_hi"], t)
    rgb = np.clip(v + (c - c.mean()) * cfg["sat"], 0, 1) * MOON_TINT
    return [round(x * 255) for x in np.clip(rgb, 0, 1)]


def restyle_earth(color, cls, cfg):
    """地球：減らした後の1色を、海・陸・氷ごとに直す。海は元の明るさ（深さ）の順を保ったまま明るい青に、
    陸は彩度を上げて明るく、氷はパレットの白に寄せる。"""
    hue, s, v = rgb_to_hsv([x / 255 for x in color])
    if cls == 0:                                   # 海：元の V（0.1〜0.6 くらい）を 0〜1 に伸ばして使う
        t = (v - 0.10) / 0.50
        hue = _lerp(cfg["ocean_hue"][1], cfg["ocean_hue"][0], t)   # 浅いほど少し青緑へ
        s = _lerp(*cfg["ocean_sat"][::-1], t)
        v = _lerp(*cfg["ocean_v"], t)
    elif cls == 1:
        s = min(0.85, s * cfg["land_sat"])
        v = _lerp(*cfg["land_v"], v)
    else:
        hue, s, v = 0.12, 0.05, _lerp(*cfg["ice_v"], (v - 0.55) / 0.45)
    return [round(c * 255) for c in hsv_to_rgb(hue, s, v)]


def _quant(pixels, n):
    """pixels（N×3）を n 色に減らし、その色の表（n×3、0〜255）を返す。"""
    side = int(np.ceil(np.sqrt(len(pixels))))
    buf = np.zeros((side * side, 3), np.uint8)
    buf[:len(pixels)] = pixels
    buf[len(pixels):] = pixels[0]
    q = Image.fromarray(buf.reshape(side, side, 3), "RGB").quantize(
        colors=n, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    return np.array(q.getpalette()[:n * 3], np.float64).reshape(-1, 3)


def _nearest(pixels, table):
    d = ((pixels[:, None, :] - table[None, :, :]) ** 2).sum(-1)
    return d.argmin(1)


def earth_classes(arr):
    """ぼかした地球の画素を 海・陸・氷 に分ける（0=海、1=陸、2=氷）。"""
    r, g, b = arr[..., 0], arr[..., 1], arr[..., 2]
    mx, mn = arr.max(-1), arr.min(-1)
    sat = (mx - mn) / np.maximum(mx, 1)
    cls = np.ones(arr.shape[:2], np.int64)
    cls[(b > r + 12) & (b >= g)] = 0
    cls[(mx > 150) & (sat < 0.25)] = 2
    return cls


def make(cfg, earth):
    img = Image.open(os.path.join(SRC, cfg["src"])).convert("RGB")
    if img.size != SIZE:
        img = wrap_resize(img, SIZE)
    arr = flatten_poles(np.asarray(img).astype(np.float64), cfg["pole_rows"])
    src = Image.fromarray(np.clip(arr + 0.5, 0, 255).astype(np.uint8))
    img = sphere_blur(src, cfg["blur"])
    arr = np.asarray(img).astype(np.float64)
    flat = arr.reshape(-1, 3)
    if earth:
        # 海・陸・氷を別々に減らす（暗い森と深い海が同じ色にまとめられないように）
        cls = wrap_filter(Image.fromarray(earth_classes(arr).astype(np.uint8)), ImageFilter.ModeFilter(5))
        cls = np.asarray(cls).reshape(-1)
        # 海は海底の細かい筋で縞だらけにならないよう、もっとぼかした色で分ける
        sea = np.asarray(sphere_blur(src, cfg["ocean_blur"])).astype(np.float64).reshape(-1, 3)
        idx = np.zeros(len(flat), np.int64)
        table = []
        for c, n in ((0, cfg["ocean_colors"]), (1, cfg["land_colors"]), (2, cfg["ice_colors"])):
            m = cls == c
            px = (sea if c == 0 else flat)[m]
            t = _quant(px.astype(np.uint8), n)
            idx[m] = _nearest(px, t) + len(table)
            table += [restyle_earth(col, c, cfg) for col in t]
    else:
        # 月は明るさの段だけで分ける（色みはほぼ灰色なので、色で分けると似た灰色が何段もできる）。
        # 段の境は画素の数の分位（各段がほぼ同じ面積になる）。段の色はその段の画素の平均
        lum = flat.mean(1)
        cuts = np.percentile(lum, cfg["cuts"])
        idx = np.searchsorted(cuts, lum)
        t = np.array([flat[idx == k].mean(0) for k in range(len(cuts) + 1)])
        table = [restyle_moon(col, k / len(cuts), cfg) for k, col in enumerate(t)]
    idx = idx.reshape(SIZE[1], SIZE[0]).astype(np.uint8)
    # 小さな飛び地（1〜2px の点）を消す：色の番号の上で多数決のフィルタを1回（RGB でかけると色が混ざる）
    idx = wrap_filter(Image.fromarray(idx), ImageFilter.ModeFilter(3))
    lut = np.array(table, dtype=np.uint8).reshape(-1, 3)
    return Image.fromarray(lut[np.asarray(idx)], "RGB")


def main(argv):
    if argv:
        outs = [os.path.abspath(argv[0])]
    else:
        outs = [os.path.join(HERE, "textures"), os.path.join(REPO, "unity", "Assets", "Models", "Bodies")]
    for cfg, earth in ((EARTH, True), (MOON, False)):
        img = make(cfg, earth)
        assert img.size == SIZE
        ncol = len(img.getcolors(1 << 16))
        for d in outs:
            os.makedirs(d, exist_ok=True)
            p = os.path.join(d, cfg["out"])
            img.save(p, optimize=True)
            print(f"[textures] {os.path.relpath(p, REPO)}  {img.size[0]}×{img.size[1]}  {ncol} 色  "
                  f"{os.path.getsize(p) / 1024:.0f}KB")


if __name__ == "__main__":
    main(sys.argv[1:])
