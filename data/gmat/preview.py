"""route_a.csv / route_b.csv から確認用の図 preview.svg を作る（標準ライブラリだけ）。

書いた人：Taiga ／ 最終更新：2026-11-14
使い方： python data/gmat/preview.py

地球と月を結ぶ線と一緒に回る座標（EarthMoonRot）で描くので、月は右側にほぼ止まって見える。
"""
import csv
from pathlib import Path

HERE = Path(__file__).resolve().parent
W, H, PAD = 1000, 520, 40
X_MIN, X_MAX = -40000.0, 440000.0
SCALE = (W - 2 * PAD) / (X_MAX - X_MIN)
R_EARTH, R_MOON = 6378.14, 1737.4


def load(name):
    with open(HERE / name, newline="") as f:
        rows = list(csv.reader(f))
    head = [h.strip() for h in rows[0]]
    ix = head.index(next(h for h in head if h.endswith("EarthMoonRot.X")))
    iy = head.index(next(h for h in head if h.endswith("EarthMoonRot.Y")))
    im = head.index(next(h for h in head if h.endswith("Luna.Altitude")))
    data = [[float(v) for v in r] for r in rows[1:] if len(r) == len(head)]
    return [(r[0], r[ix], r[iy], r[im]) for r in data]


def px(x, y):
    return PAD + (x - X_MIN) * SCALE, H / 2 - y * SCALE


def path(points):
    return " ".join(
        ("M" if i == 0 else "L") + "%.1f,%.1f" % px(x, y) for i, (_, x, y, _) in enumerate(points)
    )


a, b = load("route_a.csv"), load("route_b.csv")
i_moon = min(range(len(a)), key=lambda i: a[i][3])
moon_x = a[i_moon][1] - (R_MOON + a[i_moon][3])
days_a, days_b = a[-1][0] - a[0][0], b[-1][0] - b[0][0]

ex, ey = px(0, 0)
mx, my = px(moon_x, 0)
svg = f"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" font-family="sans-serif" font-size="14">
<rect width="{W}" height="{H}" fill="#0b1020"/>
<path d="{path(b)}" fill="none" stroke="#5bc0ff" stroke-width="2"/>
<path d="{path(a)}" fill="none" stroke="#ffb347" stroke-width="2"/>
<circle cx="{ex:.1f}" cy="{ey:.1f}" r="{max(4, R_EARTH * SCALE):.1f}" fill="#3b82f6"/>
<circle cx="{mx:.1f}" cy="{my:.1f}" r="{max(3, R_MOON * SCALE):.1f}" fill="#cbd5e1"/>
<text x="{ex:.1f}" y="{ey + 34:.1f}" fill="#e2e8f0" text-anchor="middle">Earth</text>
<text x="{mx:.1f}" y="{my + 30:.1f}" fill="#e2e8f0" text-anchor="middle">Moon</text>
<text x="{PAD}" y="30" fill="#ffb347">Route A  (launch to entry {days_a:.2f} days)</text>
<text x="{PAD}" y="52" fill="#5bc0ff">Route B  (launch to entry {days_b:.2f} days)</text>
<text x="{PAD}" y="{H - 16}" fill="#94a3b8">Earth-Moon rotating frame, GMAT R2026a. Both routes share the outbound leg.</text>
</svg>
"""
(HERE / "preview.svg").write_text(svg, encoding="utf-8")
print("wrote preview.svg  A %.3f d  B %.3f d  moon_x %.0f km" % (days_a, days_b, moon_x))
ys = [p[2] for p in a + b]
print("y range %.0f .. %.0f km" % (min(ys), max(ys)))
