"""Ctrl_Gauge：針が動く計器（酸素タンクの圧力計。同じ形で酸素・CO2・温度の計器にも使える）（#51）。

書いた人：Taiga（作成：Fable 5.2）／ 最終更新：2026-11-14

  modeling/core/run.sh modeling/controls/build_gauge.py
出力：unity/Assets/Models/Controls/Ctrl_Gauge.fbx、modeling/controls/export/、modeling/controls/renders/、
      modeling/controls/parts.json（Ctrl_Gauge の行）

決めたこと（#51、Taiga、2026-11-14）：計器は 3D のモデルで見せる。
  破れたタンク（Tank_Oxygen2_Broken）は見れば分かる形のまま残す。圧力計は「どれだけ減ったか」を見せる物。

形：0.2×0.2m の共通台座（コンソールの取り付け位置にも、タンクの Slot_Valve にも載る）の上に、
    上（ローカル +Z）を向いた丸い文字盤（直径 0.19m。実物の約3倍）。見下ろしのカメラからそのまま読める。
    文字は入れない。目盛りは太い印4個。帯は塗りの色3つ（ランプ色は使わない）。
  左（−X）＝空、右（+X）＝満タン。操作する人は −Y 側にいて、文字盤の「上」は +Y。
    危険の帯 seat_red   左下〜左   （針の角度 +120〜+60）
    注意の帯 orange     左〜上     （+60〜0）
    安全の帯 teal       上〜右下   （0〜−120）
部品：
  Base         共通台座（gray_dark）
  Housing      文字盤の胴（cream）＋ふち（gray_dark）
  Dial         文字盤（white）＋帯3色＋目盛り4個（gray_dark）。1つのメッシュ
  Move_Needle  針（navy）＋中心の丸（gray_light）。Z まわりに回る。原点は文字盤の中心
針の角度（Blender の Z まわり、度）：0 で真上（+Y）を指す。＋で左（空のほう）、−で右（満タンのほう）。
  値 0〜1（0＝空、1＝満タン）→ 角度 = 120 − 240 × 値。Unity では localEulerAngles.y = −角度（parts.json の unity_sign）。
"""
import sys, os, math
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "core"))
import bpy, bmesh
import corekit as ck

KIND, NAME = "controls", "Ctrl_Gauge"
BASE_TOP = 0.04
R_OUT = 0.095            # 胴の半径
HOUSING_H = 0.05
FACE_Z = BASE_TOP + HOUSING_H          # 文字盤の面の高さ
R_FACE = 0.082
SWEEP = 120              # 針の振れ幅（±度）


def direction(deg):
    """針の角度（+Y から反時計回り、度）→ XY の向き。"""
    a = math.radians(deg)
    return -math.sin(a), math.cos(a)


def sector(name, a0, a1, r0, r1, z0, h, color, steps=6):
    """輪の一部（角度 a0〜a1、半径 r0〜r1）を z0 から h だけ押し出した板。"""
    bm = bmesh.new()
    ring = []
    for k in range(steps + 1):
        dx, dy = direction(a0 + (a1 - a0) * k / steps)
        ring.append((dx, dy))
    def layer(z):
        return ([bm.verts.new((dx * r0, dy * r0, z)) for dx, dy in ring],
                [bm.verts.new((dx * r1, dy * r1, z)) for dx, dy in ring])
    bi, bo = layer(z0)
    ti, to = layer(z0 + h)
    for k in range(steps):
        bm.faces.new((bi[k], bi[k + 1], bo[k + 1], bo[k]))
        bm.faces.new((ti[k], to[k], to[k + 1], ti[k + 1]))
        bm.faces.new((bo[k], bo[k + 1], to[k + 1], to[k]))
        bm.faces.new((bi[k], ti[k], ti[k + 1], bi[k + 1]))
    bm.faces.new((bi[0], bo[0], to[0], ti[0]))
    bm.faces.new((bi[-1], ti[-1], to[-1], bo[-1]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    return ck._fin(o, color, name, smooth=False)


ck.clear_scene()
root = ck.asset(NAME)
ck.ctrl_base(0.2, 0.2)

# 胴とふち
body = ck.cyl("HousingBody", (0, 0, BASE_TOP + HOUSING_H / 2), R_OUT, HOUSING_H, "cream", bevel=0.01)
rim = ck.tor("HousingRim", (0, 0, FACE_Z), R_OUT - 0.012, 0.012, "gray_dark", seg=(24, 6))
ck.join([body, rim], "Housing")

# 文字盤：白い面＋帯3色＋目盛り
face = ck.cyl("DialFace", (0, 0, FACE_Z), R_FACE, 0.02, "white", bevel=0)
band_z, band_h = FACE_Z + 0.004, 0.012
bands = [
    sector("BandDanger", SWEEP, 60, 0.052, 0.078, band_z, band_h, "seat_red", steps=4),
    sector("BandCaution", 60, 0, 0.052, 0.078, band_z, band_h, "orange", steps=4),
    sector("BandSafe", 0, -SWEEP, 0.052, 0.078, band_z, band_h, "teal", steps=8),
]
ticks = [sector(f"Tick{i}", a + 3.5, a - 3.5, 0.050, 0.081, band_z, band_h + 0.003, "gray_dark", steps=1)
         for i, a in enumerate((SWEEP, 40, -40, -SWEEP))]
ck.join([face] + bands + ticks, "Dial")

# 針：+Y を指す太い矢（先を細く）＋中心の丸。原点は文字盤の中心
needle_z = FACE_Z + 0.024
bm = bmesh.new()
pts = [(-0.013, -0.028), (0.013, -0.028), (0.013, 0.046), (0.0, 0.074), (-0.013, 0.046)]
bot = [bm.verts.new((x, y, needle_z)) for x, y in pts]
top = [bm.verts.new((x, y, needle_z + 0.02)) for x, y in pts]
bm.faces.new(bot[::-1]); bm.faces.new(top)
for i in range(len(pts)):
    j = (i + 1) % len(pts)
    bm.faces.new((bot[i], bot[j], top[j], top[i]))
me = bpy.data.meshes.new("NeedleArrow"); bm.to_mesh(me); bm.free()
arrow = bpy.data.objects.new("NeedleArrow", me)
bpy.context.scene.collection.objects.link(arrow)
ck._fin(arrow, "navy", "NeedleArrow", bevel=0.004, smooth=False)
hub = ck.cyl("NeedleHub", (0, 0, needle_z + 0.013), 0.02, 0.026, "gray_light", bevel=0.006)
needle = ck.join([arrow, hub], "Move_Needle")
ck.set_origin(needle, (0, 0, FACE_Z))
ck.end_asset()

parts = [{"name": "Move_Needle", "type": "rotate", "axis": "Z", "min": -SWEEP, "max": SWEEP, "default": -SWEEP,
          "note": "0 で真上（+Y）。+120＝空（左、赤の帯の端）、−120＝満タン（右、teal の帯の端）。"
                  "値 0〜1（0＝空）→ 角度 = 120 − 240×値。帯：+120〜+60 危険（赤）、+60〜0 注意（orange）、0〜−120 安全（teal）"}]
ck.write_parts(KIND, root, parts)
ck.export_all(root, KIND)
ck.render_check(root, KIND, parts)
print(f"[build] {NAME}: {ck.tri_count(root)} 三角")
