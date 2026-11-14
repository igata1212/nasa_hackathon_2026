"""Scene_MoonSurface：月の表面。NASA の Apollo 16 着陸地点（デカルト高地）の地形から作る（#54）。

書いた人：Taiga（作成：Fable 5.2）／ 最終更新：2026-11-14

  modeling/core/run.sh modeling/scene/build_moon_surface.py            （--trial で tmp/modeling_trial/ に試し書き）
出力：unity/Assets/Models/Scene/Scene_MoonSurface.fbx、modeling/scene/export/、modeling/scene/renders/Scene_MoonSurface_*.png

決めたこと（#54、Taiga、2026-11-14）：月に目・口の顔は付けない。代わりに本物の地形を月の表面として使う。

元データ：modeling/source/nasa/Apollo16_Landing_Site.stl（NASA 3D Resources。出典は modeling/source/README.md）。
  30km 四方の高さの地図で、高さは NASA が 35 倍に誇張してある。四角い板（約14万三角）。
作り方：STL の上の面の高さを 256×256 のマスに読む → 少しぼかす（元データのタイルの継ぎ目の段を弱める）
  → 格子の板にする → 三角を TARGET_TRIS まで減らす（平らな所は大きな三角、クレーターのまわりに三角が残る）
  → 面ごとに灰色を塗る（パレットの4色）。
大きさ：一辺 SIZE（20m）の四角い板。本物の 1/1500。Unity 側で好きな大きさに拡大する。
  高さは元データ（35 倍）にさらに EXAGGERATE（1.8 倍）を掛けた。合わせて本物の 63 倍。
向き：元データの +Y（北）が奥（+Y、Unity の −Z）。原点＝板の底の中心（底が z=0）。
部品：Scene_MoonSurface.Ground（1つのメッシュ。可動部なし）。面は平らに塗る（ローポリの見た目）。ベベルなし（地形なので）。
色：gray_light（平地）／white（まわりより高い所。クレーターのふち・丘）／gray（まわりより低い所と急な斜面）／gray_dark（板の横と底）。
"""
import sys, os
sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "core"))
import bpy
import numpy as np
import corekit as ck

KIND, NAME = "scene", "Scene_MoonSurface"
SRC = os.path.join(HERE, "..", "source", "nasa", "Apollo16_Landing_Site.stl")
SIZE = 20.0            # 板の一辺（m）
BASE = 0.30            # いちばん低い所の下に残す厚み（m）
CELLS = 256            # 高さを読むマスの数（一辺）
GRID = 128             # 減らす前の格子（一辺のマスの数）
BLUR = 2               # ぼかしの回数（3×3 の平均）
TARGET_TRIS = 5200     # 減らしたあとの三角数の目安（Scene_ の上限は 6,000）
STEEP = 0.80           # 面の法線の z がこれより小さければ「急な斜面」
EXAGGERATE = 1.8       # 高さをさらに誇張する倍率（元データの 35 倍に掛ける。見た目のため）
LOCAL_BLUR = 30        # 「まわり」の広さ（ぼかしの回数）
LOW_PCT, HIGH_PCT = 14, 88   # まわりより低い側・高い側の何 % を塗り分けるか
if "--trial" in sys.argv:
    ck.trial()


def read_heights():
    """STL（バイナリ）の頂点を読み、マスごとにいちばん高い z を取る。戻り値：(CELLS×CELLS の高さ, 元の一辺の長さ)。
    行 0 が北（+Y）、列 0 が西（−X）。"""
    with open(SRC, "rb") as f:
        f.seek(80)
        n = int(np.frombuffer(f.read(4), dtype="<u4")[0])
        rec = np.frombuffer(f.read(50 * n), dtype=np.dtype([("n", "<3f4"), ("v", "<9f4"), ("a", "<u2")]))
    v = rec["v"].reshape(-1, 3).astype(np.float64)
    lo, hi = v[:, :2].min(), v[:, :2].max()
    span = hi - lo
    c = np.clip(((v[:, 0] - lo) / span * (CELLS - 1) + 0.5).astype(int), 0, CELLS - 1)
    r = np.clip(((hi - v[:, 1]) / span * (CELLS - 1) + 0.5).astype(int), 0, CELLS - 1)
    h = np.zeros((CELLS, CELLS))
    np.maximum.at(h, (r, c), v[:, 2])
    for _ in range(8):                       # 頂点が入らなかったマスを、まわりの値で埋める
        if (h > 0).all():
            break
        p = np.pad(h, 1, mode="edge")
        nb = np.max([p[i:i + CELLS, j:j + CELLS] for i in range(3) for j in range(3)], axis=0)
        h = np.where(h > 0, h, nb)
    assert (h > 0).all(), "高さを読めなかったマスがある"
    return h, span


def blur(h, times):
    for _ in range(times):
        p = np.pad(h, 1, mode="edge")
        h = sum(p[i:i + h.shape[0], j:j + h.shape[1]] for i in range(3) for j in range(3)) / 9.0
    return h


def resample(h, n):
    """CELLS×CELLS → n×n（双一次）。"""
    x = np.linspace(0, h.shape[0] - 1, n)
    i0 = np.floor(x).astype(int).clip(0, h.shape[0] - 2)
    t = x - i0
    rows = h[i0] * (1 - t)[:, None] + h[i0 + 1] * t[:, None]
    return rows[:, i0] * (1 - t)[None, :] + rows[:, i0 + 1] * t[None, :]


def slab(z):
    """高さの格子 z（(GRID+1)²。行 0 が北）から、横と底のある板のメッシュを作る。"""
    n = z.shape[0]
    xs = np.linspace(-SIZE / 2, SIZE / 2, n)
    verts = [(xs[c], -xs[r], float(z[r, c])) for r in range(n) for c in range(n)]
    top = lambda r, c: r * n + c
    faces = [(top(r, c), top(r + 1, c), top(r + 1, c + 1), top(r, c + 1)) for r in range(n - 1) for c in range(n - 1)]
    ring = [(0, c) for c in range(n)] + [(r, n - 1) for r in range(1, n)] \
        + [(n - 1, c) for c in range(n - 2, -1, -1)] + [(r, 0) for r in range(n - 2, 0, -1)]
    b0 = len(verts)
    verts += [(xs[c], -xs[r], 0.0) for r, c in ring]
    m = len(ring)
    for i in range(m):                       # 横の面（外向き）
        j = (i + 1) % m
        faces.append((top(*ring[i]), top(*ring[j]), b0 + j, b0 + i))
    faces.append(tuple(b0 + i for i in range(m - 1, -1, -1)))     # 底（下向き）
    me = bpy.data.meshes.new("ground")
    me.from_pydata(verts, [], faces)
    me.update()
    o = bpy.data.objects.new("ground", me)
    bpy.context.scene.collection.objects.link(o)
    return o


def reduce(o, target):
    me = o.data
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    d = o.modifiers.new("dec", "DECIMATE")
    d.decimate_type = "COLLAPSE"
    d.ratio = min(1.0, target / tris)
    d.use_collapse_triangulate = True
    ck._apply_modifiers(o)


def colour(o, local):
    """面ごとに灰色を塗る（UV をパレットのマス中心へ）。local は「まわりより高いか低いか」の地図（CELLS×CELLS、行 0 が北）。
    まわりより低い所（クレーターの中）と急な斜面は gray、まわりより高い所（ふち・丘）は white。"""
    me = o.data
    uv = me.uv_layers.active.data
    low, high = np.percentile(local, LOW_PCT), np.percentile(local, HIGH_PCT)
    count = {}
    for p in me.polygons:
        c = int(np.clip((p.center.x / SIZE + 0.5) * (CELLS - 1), 0, CELLS - 1))
        r = int(np.clip((0.5 - p.center.y / SIZE) * (CELLS - 1), 0, CELLS - 1))
        if p.normal.z <= 0.3:
            name = "gray_dark"                # 板の横と底
        elif p.normal.z < STEEP or local[r, c] < low:
            name = "gray"
        elif local[r, c] > high:
            name = "white"
        else:
            name = "gray_light"
        count[name] = count.get(name, 0) + 1
        u, v = ck.PALETTE[name]["uv"]
        for li in p.loop_indices:
            uv[li].uv = (u, v)
    return count


ck.clear_scene()
root = ck.asset(NAME)
h, span = read_heights()
h = blur(h, BLUR)
local = h - blur(h, LOCAL_BLUR)
z = BASE + (resample(h, GRID + 1) - h.min()) * (SIZE / span) * EXAGGERATE
g = slab(z)
reduce(g, TARGET_TRIS)
ck._fin(g, "gray_light", "Ground", bevel=0, smooth=False)
print("[build] 色ごとの面の数:", colour(g, local))
ck.end_asset()

ck.export_all(root, KIND)
ck.render_check(root, KIND, [], floor=False, dummy=False)
print(f"[build] {NAME}: {ck.tri_count(root)} 三角、高さ {z.min():.2f}〜{z.max():.2f} m")
