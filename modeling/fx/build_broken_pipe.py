"""Fx_BrokenPipe：まん中で折れて口が開いたパイプ。midori の props.py の broken_pipe() を core で組み直したもの。

書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14

  modeling/core/run.sh modeling/fx/build_broken_pipe.py
出力：unity/Assets/Models/Fx/Fx_BrokenPipe.fbx、modeling/fx/export/Fx_BrokenPipe.glb・.stats.json、
      modeling/fx/renders/Fx_BrokenPipe_*.png、modeling/fx/parts.json（Fx_Alarm の分は残して Fx_BrokenPipe を足す）

midori の形（長さ 1.8m、太さ 半径 7cm、まん中に 16cm のすき間、両端にフランジ、右半分が少し傾く）はそのまま。変えたこと：
  - 煙（Steam）と火花（Spark）は外した。Unity の粒子で出す。出す位置は .Slot_Leak（すき間のまん中）
  - 割れ目の輪（Crack のトーラス 2つ）の代わりに、折れ口に「めくれた板」3枚と「暗い穴」を付けた（遠くからでも折れたと分かるように）
  - 円柱は 16 分割（パイプ・フランジ）、ボルトは 6 角。midori の tube（曲線）は使わず円柱にした（ベベルが付く）
  - 管を少し太くした（半径 7→8cm、フランジ 10→11.5cm）。ゲームの見下ろしのカメラで細い棒に見えたため
  - 床置きのルールに合わせて、いちばん下（フランジの下の端）が z=0。midori は高さ 0.9m に浮いていた
  - 右半分は .Move_PipeR（原点＝右のフランジの中心、Y 軸まわりに 0〜6° 揺れる）。ガスが噴き出す勢いで揺らす用
部品：PipeL（左半分：管・フランジ・ボルト・帯・めくれた板・穴）、Move_PipeR（右半分：同じ）、Slot_Leak（すき間の中心）
色：管 gray_light、フランジ gray、ボルトと穴の奥 gray_dark・visor、注意の帯 orange（船内の色）
"""
import sys, os
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "core"))
import bpy, math
from mathutils import Matrix, Vector
import corekit as ck

KIND, NAME = "fx", "Fx_BrokenPipe"
if "--trial" in sys.argv:
    ck.trial()

L = 0.9              # 中心から端のフランジまで（midori と同じ）
R = 0.08             # 管の半径（midori は 0.07。見下ろしのカメラで細く見えたので少し太く）
GAP = 0.08           # 中心から折れ口まで（すき間は 2×GAP = 16cm、midori と同じ）
FL_R, FL_T = 0.115, 0.04    # フランジの半径・厚み（midori は 0.10。管に合わせて太く）
ZC = FL_R            # 管の中心の高さ。フランジの下の端が z=0 になる
BOLT_R = (R + FL_R) / 2     # ボルトの並ぶ円の半径
PETALS = {-1: [(20, 38, 0.075), (140, 28, 0.06), (255, 45, 0.07)],     # 左：(縁の角度 0°=上, 反り返り 度, 長さ m)
          +1: [(60, 38, 0.075), (230, 28, 0.06), (310, 45, 0.07)]}     # 右
TILT = 1.3           # 右半分の傾き（度）。midori は 0.92→0.88 で約 2.7° 下がっていたが、揺れの分を残して小さめに


def sharp(o, deg=50):
    """ベベルなしの部品は、角（面どうしが deg 度より大きく折れる辺）を「シャープ」にする。
    しないと、finalize の重み付き法線が大きい面に引っぱられて、帯の横に黒いしみが出る（レンダーで出た）。"""
    import bmesh
    bm = bmesh.new(); bm.from_mesh(o.data)
    lim = math.radians(deg)
    sharp_ids = {e.index for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(0) > lim}
    bm.free()
    for e in o.data.edges:
        if e.index in sharp_ids:
            e.use_edge_sharp = True
    return o


def pipe_half(side):
    """side=-1 が左半分、+1 が右半分。端のフランジから折れ口までを、色ごとの部品で作って返す（join する前）。"""
    s = "L" if side < 0 else "R"
    end = side * L                     # フランジの中心
    brk = side * GAP                   # 折れ口
    length = abs(end - brk)
    mid = (end + brk) / 2
    parts = []
    parts.append(ck.cyl(f"Tube{s}", (mid, 0, ZC), R, length, "gray_light", rot=(0, 90, 0), bevel=0.02))
    parts.append(ck.cyl(f"Flange{s}", (end, 0, ZC), FL_R, FL_T, "gray", rot=(0, 90, 0), bevel=0.012))
    # フランジのボルト 4本（6角）。フランジの内側の面から少し出す
    for i in range(4):
        a = math.radians(45 + 90 * i)
        x = end - side * (FL_T / 2 + 0.01)
        parts.append(ck.cyl(f"Bolt{s}{i}", (x, BOLT_R * math.cos(a), ZC + BOLT_R * math.sin(a)), 0.014, 0.02,
                            "gray_dark", rot=(0, 90, 0), verts=6, bevel=0))
        sharp(parts[-1])
    # 注意の帯（フランジの近く。船内のオレンジ）
    parts.append(ck.cyl(f"Band{s}", (end - side * 0.12, 0, ZC), R + 0.008, 0.06, "orange", rot=(0, 90, 0),
                        bevel=0))         # 8mm しか出ないのでベベルなし（三角数の節約）
    sharp(parts[-1])
    # 折れ口の穴（管の中の暗さ）。口の面より 4mm 外に出して、面のちらつきを避ける
    parts.append(ck.cyl(f"Hole{s}", (brk + side * 0.006, 0, ZC), R - 0.018, 0.02, "visor", rot=(0, 90, 0), bevel=0))
    sharp(parts[-1])
    # めくれた板 3枚：折れ口の縁から外へ反り返る（向きは左右でずらす）。真下（180°）の近くには付けない
    # （床置きの底 z=0 より下に出る。右半分は傾けるので、さらに下がる）
    for i, (deg, bend, ln) in enumerate(PETALS[side]):
        parts.append(petal(f"Petal{s}{i}", brk, side, deg, bend, ln))
    return parts


def petal(name, brk, side, deg, bend, length, width=0.05, thick=0.02):
    """折れ口の縁（角度 deg、0° が上）から、管の外へ bend 度反り返った板。"""
    th = math.radians(deg)
    d = Vector((0, math.sin(th), math.cos(th)))          # 管の軸から縁へ向かう向き
    ax = Vector((-side, 0, 0))                           # 折れ口から外（すき間の側）へ向かう軸の向き
    b = math.radians(bend)
    lv = (ax * math.cos(b) + d * math.sin(b)).normalized()   # 板の長さの向き
    tv = ax.cross(d).normalized()                            # 板の幅の向き（縁に沿う）
    nv = lv.cross(tv).normalized()                           # 板の厚みの向き
    p0 = Vector((brk, 0, ZC)) + d * (R - thick / 2)          # 縁の上の付け根
    c = p0 + lv * (length / 2 - 0.01)                        # 付け根を 1cm 管に食い込ませる
    o = ck.box(name, (0, 0, 0), (length, width, thick), "gray_light", bevel=0)   # ちぎれた板なので角は立てたまま
    m = Matrix((lv, tv, nv)).transposed().to_4x4()
    m.translation = c
    o.matrix_world = m
    return sharp(o)


ck.clear_scene()
root = ck.asset(NAME)
left = ck.join(pipe_half(-1), "PipeL")
ck.set_origin(left, (-L, 0, ZC))                 # 左のフランジの中心（動かないが、右と同じ考え方にそろえる）
right = ck.join(pipe_half(+1), "Move_PipeR")
# 右半分を少し下へ傾ける（右のフランジの中心が支点）
ck.set_origin(right, (L, 0, ZC))
right.rotation_euler = (0, math.radians(-TILT), 0)
bpy.context.view_layer.update()
ck._apply(right)                                 # 傾きを形に焼く（回転 0・原点は支点のまま）
ck.slot("Slot_Leak", (0, 0, ZC))
ck.end_asset()
lo, hi = ck._export.world_bounds(ck.meshes(root))
assert abs(lo.z) < 0.0005, f"底が z=0 にない: {lo.z:.4f}（めくれた板が下に出ていないか）"

parts = [{"name": "Move_PipeR", "type": "rotate", "axis": "Y", "min": 0, "max": 6, "default": 0,
          "note": "ガスが噴き出している間、0〜6° で小刻みに揺らす（+ で折れ口が上がる）。支点は右のフランジの中心。"
                  "下（−）へは振らない：折れ口が床の高さ（z=0）より下に出るため"}]
ck.write_parts(KIND, root, parts)
ck.export_all(root, KIND)
ck.render_check(root, KIND, parts)
print(f"[build] {NAME}: {ck.tri_count(root)} 三角")
