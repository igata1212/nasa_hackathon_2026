"""Fx_Parachute：着水のパラシュート。司令船（Ship_CM）の上に開く主傘3つ（#50）。

書いた人：Taiga（作成：Fable 5.2）／ 最終更新：2026-11-14

  modeling/core/run.sh modeling/fx/build_parachute.py
出力：unity/Assets/Models/Fx/Fx_Parachute.fbx、modeling/fx/export/、modeling/fx/renders/Fx_Parachute_*.png

原点＝ひもの集まる点（底、z=0）。Unity で司令船の先端（Ship_CM の原点から +Z へ 3.45m、ドッキング部の先）に合わせる。
大きさはデフォルメ：傘の直径 8m（実物は約 25m。司令船は直径 3.9m）、全体の高さ 約 13m。
アポロの着水は主傘3つ・オレンジと白の縞。形だけ借りた。メッシュは新規。

部品：
  Riser       ひもを束ねる太い柱（z=0〜1.2）。gray
  Canopy_1〜3 傘。12 枚の布を orange と white で交互に塗った厚みのある椀（1つのメッシュに2色）。
              原点は傘の口（縁の輪）の中心。Unity で少し揺らすならここを回す
  Lines_1〜3  ひも。傘1つにつき4本の太い帯（太さ 14cm）。white
  Vent_1〜3   てっぺんのふた。orange
可動部はなし（開く動きは作らない。Unity でスケールか位置で出す）。透明なし。
"""
import sys, os, math
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "core"))
import bpy, bmesh
from mathutils import Matrix, Vector
import corekit as ck

KIND, NAME = "fx", "Fx_Parachute"

RC = 4.0            # 傘の口の半径（直径 8m）
DEPTH = 2.7         # 傘の深さ
THICK = 0.25        # 布の厚み（見た目用。2cm 以上）
GORES = 12          # 布の枚数（色が交互）
LEN = 10.5          # 原点から傘の口の中心までの距離
TILT = 24           # 傘の軸の傾き（鉛直から外へ、度）
RISER = 1.2
LINE_R = 0.07       # ひもの半径（太さ 14cm）


def canopy(name):
    """ローカル座標（口の中心が原点、軸 +Z）で傘を作る。面ごとに orange / white の UV を入れる。"""
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("UVMap")
    cols = [ck.PALETTE["orange"]["uv"], ck.PALETTE["white"]["uv"]]

    def shell(rc, depth, z0):
        rings = []
        for t in (0, 32, 62):
            a = math.radians(t)
            rings.append([Vector((rc * math.cos(a) * math.cos(2 * math.pi * i / GORES),
                                  rc * math.cos(a) * math.sin(2 * math.pi * i / GORES), z0 + depth * math.sin(a)))
                          for i in range(GORES)])
        top = Vector((0, 0, z0 + depth))
        return rings, top

    def face(vs, gore, flip=False):
        vs = [bm.verts.new(v) for v in (vs[::-1] if flip else vs)]
        f = bm.faces.new(vs)
        for lp in f.loops:
            lp[uvl].uv = cols[gore % 2]
        f.smooth = True

    outer, otop = shell(RC, DEPTH, 0.0)
    inner, itop = shell(RC - THICK, DEPTH - THICK, 0.0)
    for i in range(GORES):
        j = (i + 1) % GORES
        for a, b in zip(outer, outer[1:]):
            face([a[i], a[j], b[j], b[i]], i)
        face([outer[-1][i], outer[-1][j], otop], i)
        for a, b in zip(inner, inner[1:]):
            face([a[i], a[j], b[j], b[i]], i, flip=True)
        face([inner[-1][i], inner[-1][j], itop], i, flip=True)
        face([outer[0][i], inner[0][i], inner[0][j], outer[0][j]], i)       # 縁
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    o.name = ck._full_name(name); me.name = o.name
    me.materials.append(ck.palette_mat())
    o.parent = ck._cur[0]
    me.set_sharp_from_angle(angle=math.radians(50))
    return o


ck.clear_scene()
root = ck.asset(NAME)
ck.cyl("Riser", (0, 0, RISER / 2), 0.22, RISER, "gray", bevel=0.04, verts=12)
for k, az in enumerate((90, 210, 330)):
    a, t = math.radians(az), math.radians(TILT)
    d = Vector((math.cos(a) * math.sin(t), math.sin(a) * math.sin(t), math.cos(t)))
    c = d * LEN                                                # 傘の口の中心
    rot = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
    mw = Matrix.Translation(c) @ rot
    cv = canopy(f"Canopy_{k + 1}")
    cv.data.transform(mw)
    ck.set_origin(cv, tuple(c))
    v = ck.cyl(f"Vent_{k + 1}", (0, 0, 0), 0.55, 0.2, "orange", bevel=0.04, verts=12)
    v.data.transform(mw @ Matrix.Translation((0, 0, DEPTH - 0.02)))
    lines = []
    for q in range(4):
        ph = 2 * math.pi * (q + 0.5) / 4
        rim = mw @ Vector(((RC - THICK / 2) * math.cos(ph), (RC - THICK / 2) * math.sin(ph), 0.05))
        lines.append(ck.tube(f"L{k}{q}", [(0, 0, RISER - 0.25), tuple(rim)], LINE_R, "white"))
    ck.attach(ck.join(lines, f"Lines_{k + 1}"), root)
ck.end_asset()

ck.export_all(root, KIND)
ck.render_check(root, KIND)
print(f"[build] {NAME}: {ck.tri_count(root)} 三角")
