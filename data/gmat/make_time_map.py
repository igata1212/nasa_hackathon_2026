"""route_a.csv / route_b.csv から time_map.json（ゲームの t ↔ GMAT の経過時間）を作る。

書いた人：Claude（#65）／ 最終更新：2026-11-14
使い方： python data/gmat/make_time_map.py
決め方は data/README.md の「ゲーム時間との対応」を参照。
hours = 各 CSV の最初の行（UTCModJulian の先頭）からの経過時間（時間）。
ゲームの t は SPEC 4章：事故 0、月の裏側 120〜300、噴射 330〜390（中央 360）、再突入 A 420 / B 510。
"""
import csv, json
from pathlib import Path

HERE = Path(__file__).resolve().parent
R_MOON = 1737.4
ACCIDENT_BEFORE_LOS_H = 21.227   # アポロ13号：事故 GET 55:54:53 から LOS 約 77:08:30 まで（月まで約21時間の所に事故を置く）


def load(name):
    with open(HERE / name, newline="") as f:
        rows = list(csv.reader(f))
    head = [h.strip() for h in rows[0]]
    col = lambda suf: next(i for i, h in enumerate(head) if h.endswith(suf))
    ix, iy, ia, im = col("EarthMoonRot.X"), col("EarthMoonRot.Y"), col("Earth.Altitude"), col("Luna.Altitude")
    d = [[float(v) for v in r] for r in rows[1:] if len(r) == len(head)]
    return d, ix, iy, im


a, ix, iy, im = load("route_a.csv")
b = load("route_b.csv")[0]
t0 = a[0][0]
hrs = lambda row: (row[0] - t0) * 24
k = min(range(len(a)), key=lambda i: a[i][im])
moon_x = a[k][ix] - (R_MOON + a[k][im])
behind = [r for r in a if r[ix] > moon_x and abs(r[iy]) < R_MOON]
los, aos = hrs(behind[0]), hrs(behind[-1])
burn = next(hrs(x) for x, y in zip(a, b) if abs(x[0] - y[0]) < 1e-9 and abs(x[ix] - y[ix]) > 1)
end_a, end_b = hrs(a[-1]), hrs((b[-1]))
b_t0_ok = abs(b[0][0] - t0) < 1e-9
assert b_t0_ok

def knots(end_hours, t_end):
    return [
        {"t": 0, "h": round(los - ACCIDENT_BEFORE_LOS_H, 4), "label": "accident"},
        {"t": 120, "h": round(los, 4), "label": "los"},
        {"t": 300, "h": round(aos, 4), "label": "aos"},
        {"t": 360, "h": round(burn, 4), "label": "burn"},
        {"t": t_end, "h": round(end_hours, 4), "label": "reentry"},
    ]

out = {
    "about": "game t (s since accident) <-> GMAT hours since first CSV row. Piecewise linear between knots. See data/README.md.",
    "origin_mjd": t0,
    "moon_center_x_km": round(moon_x, 1),
    "routes": [
        {"id": "a", "knots": knots(end_a, 420)},
        {"id": "b", "knots": knots(end_b, 510)},
    ],
}
(HERE / "time_map.json").write_text(json.dumps(out, indent=1) + "\n", encoding="utf-8")
print(json.dumps(out)[:600])
