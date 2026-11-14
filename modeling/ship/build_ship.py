"""宇宙船の外観：Ship_CM（司令船）、Ship_SM（機械船）、Ship_LM（月着陸船）、Ship_Stack（3つをつないだ姿）。

書いた人：iizuka（作成：Fable 5.2）／ 最終更新：2026-11-14

  modeling/core/run.sh modeling/ship/build_ship.py
出力：unity/Assets/Models/Ship/Ship_{CM,SM,LM,Stack}.fbx、modeling/ship/export/*.glb・*.stats.json、
      modeling/ship/renders/*.png、modeling/ship/parts.json

大きさは実物の公表値（記憶。確かさは dimensions.md の 4章）、形と部品の配置は NASA の 3D モデルを測った値
（dimensions.md の 1〜2章）から。NASA のメッシュは入っていない。旗・文字・記章なし。
向き：Z が上、正面 −Y。原点はどれも底面の中心（Ship_SM と Ship_Stack はノズル出口の中心、長軸 +Z、司令船の先端が +Z）。
Ship_Stack では月着陸船を上下逆にして司令船の先端につなぐ（アポロ13号の配置。脚は開いた状態）。

可動部：Ship_SM.Move_Panel4（事故で吹き飛ぶ外板。−Y 側の 60°）。外すと中の棚とタンクが見える。
"""
import sys, os
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "core"))
import json, math
import bpy, bmesh
from mathutils import Matrix
import corekit as ck

KIND = "ship"
SEG = 24            # 大きな胴（直径 3.9m）は 24 分割。16 だと 60° の外板が分割の境目に合わない

# ---------------------------------------------------------------- 寸法（m）
# 機械船（単体の座標。ノズル出口が z=0）
NOZ_LEN, NOZ_R_EXIT, NOZ_R_THROAT = 2.8, 1.25, 0.33
SM_R = 1.95                         # 直径 3.9
SM_Z0 = NOZ_LEN                     # 後ろの遮熱板の下面
SM_SKIN_Z0, SM_SKIN_Z1 = 2.95, 7.30
SM_TOP = 7.50                       # 円筒の上端（= 2.8 + 4.7）
SKIN_T = 0.09
PANEL_A0, PANEL_A1 = -120.0, -60.0  # 外板4の範囲（度。−90° が −Y＝正面）
PANEL_OPEN = 120.0                  # 下の辺を軸に外へ開く角度（度）
RCS_Z = SM_TOP - 1.2
# 司令船（単体の座標。遮熱板のふくらみの底が z=0）
CM_BULGE = 0.35
CM_R = 1.955
CM_TOP = CM_BULGE + 3.10            # ドッキング部の先
# 月着陸船（単体の座標。足の皿の底が z=0、はしご側が −Y）
LM_PAD_SPAN, LM_PAD_R = 9.4, 0.45
LM_DESC_Z0, LM_DESC_H, LM_DESC_FLAT = 1.5, 1.7, 4.2
LM_ASC_Z0 = LM_DESC_Z0 + LM_DESC_H  # 3.2
LM_TOP = 6.3                        # ドッキング部の先
# つないだ姿
STACK_CM_Z = SM_TOP - CM_BULGE      # Ship_CM の原点（ふくらみは機械船の中に隠れる）
STACK_DOCK_Z = SM_TOP + 3.10 - 0.05  # 司令船の先端のすぐ下（月着陸船のドッキング部を 5cm 差し込む）


# ---------------------------------------------------------------- 形を作る小道具
def crisp(o):
    """ベベルなしの部品用。40° より急な角を「鋭い辺」にして、面がなめらかにつながって見えないようにする。"""
    o.data.set_sharp_from_angle(angle=math.radians(40))
    return o


def lathe(name, prof, color, seg=SEG, closed=False, bevel=0.03, loc_z=0.0):
    """回転体。prof は (半径, 高さ) の列。closed=True なら列の最後と最初をつないで輪にする（二重壁のノズル用）。
    closed=False なら両端をふたでふさぐ（半径 0 の端はとがらせる）。"""
    bm = bmesh.new()
    rings = []
    for r, z in prof:
        if r <= 1e-6:
            rings.append([bm.verts.new((0, 0, z))])
        else:
            rings.append([bm.verts.new((r * math.cos(2 * math.pi * i / seg), r * math.sin(2 * math.pi * i / seg), z))
                          for i in range(seg)])
    pairs = list(zip(rings, rings[1:])) + ([(rings[-1], rings[0])] if closed else [])
    for a, b in pairs:
        for i in range(seg):
            j = (i + 1) % seg
            if len(a) == 1 and len(b) == 1:
                continue
            if len(a) == 1:
                bm.faces.new((a[0], b[i], b[j]))
            elif len(b) == 1:
                bm.faces.new((a[i], a[j], b[0]))
            else:
                bm.faces.new((a[i], a[j], b[j], b[i]))
    if not closed:                      # 面の向きは作る順で決まる（列は下から上へ＝外向き）
        if len(rings[0]) > 1:
            bm.faces.new(rings[0][::-1])
        if len(rings[-1]) > 1:
            bm.faces.new(rings[-1])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    o.location = (0, 0, loc_z)
    o = ck._fin(o, color, name, bevel=bevel)
    return o if bevel else crisp(o)


def arc(name, a0, a1, r_in, r_out, z0, z1, color, bevel=0.02):
    """円筒の壁の一部（角度 a0〜a1 度）。厚みのある板。分割は SEG に合わせる（15° ごと）。"""
    n = max(1, round((a1 - a0) / (360.0 / SEG)))
    bm = bmesh.new()
    cols = []
    for i in range(n + 1):
        a = math.radians(a0 + (a1 - a0) * i / n)
        c, s = math.cos(a), math.sin(a)
        cols.append([bm.verts.new((r_in * c, r_in * s, z0)), bm.verts.new((r_out * c, r_out * s, z0)),
                     bm.verts.new((r_out * c, r_out * s, z1)), bm.verts.new((r_in * c, r_in * s, z1))])
    for p, q in zip(cols, cols[1:]):
        for k in range(4):
            m = (k + 1) % 4
            bm.faces.new((p[k], q[k], q[m], p[m]))
    bm.faces.new(cols[0]); bm.faces.new(cols[-1][::-1])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    return ck._fin(o, color, name, bevel=bevel)


def polar(r, deg, z):
    a = math.radians(deg)
    return (r * math.cos(a), r * math.sin(a), z)


def sub_asset(name, parent):
    """入れ子のアセット。parent が None なら単体のルート。"""
    o = ck.asset(name)
    if parent is not None:
        o.parent = parent
    return o


# ---------------------------------------------------------------- 機械船
def build_sm(parent=None):
    sm = sub_asset("Ship_SM", parent)

    # ノズル（二重壁の鐘。出口が z=0、内側も見える）
    t = 0.05
    outer = [(NOZ_R_EXIT, 0.0), (1.12, 0.60), (0.80, 1.60), (NOZ_R_THROAT, NOZ_LEN)]
    inner = [(NOZ_R_THROAT - t, NOZ_LEN), (0.80 - t, 1.60), (1.12 - t, 0.60), (NOZ_R_EXIT - t, 0.0)]
    # 内側の奥（のど）をふさぐため、内壁の列の先頭に中心の点を足す
    lathe("Nozzle", outer + [(0.0, NOZ_LEN - 0.02)] + inner[1:], "gray_dark", seg=16, closed=True, bevel=0)
    crisp(ck.cyl("NozzleMount", (0, 0, NOZ_LEN - 0.12), 0.55, 0.36, "gray", bevel=0))

    # 後ろの遮熱板、中の柱、上の輪（外板4を外したときに中がふさがって見えるように）
    lathe("AftShield", [(1.55, SM_Z0), (SM_R - 0.02, SM_Z0 + 0.15), (SM_R - 0.02, SKIN_Z0_IN)], "gray_dark", bevel=0)
    ck.cyl("Core", (0, 0, (SM_SKIN_Z0 + SM_SKIN_Z1) / 2), 0.62, SM_SKIN_Z1 - SM_SKIN_Z0 + 0.1, "navy", bevel=0)
    lathe("TopRing", [(SM_R, SM_SKIN_Z1), (SM_R, SM_TOP - 0.06), (SM_R - 0.10, SM_TOP)], "gray", bevel=0)

    # 外板：外板4（−120°〜−60°）と、残りの 300°
    arc("Skin", PANEL_A1, PANEL_A0 + 360.0, SM_R - SKIN_T, SM_R, SM_SKIN_Z0, SM_SKIN_Z1, "gray_light")
    panel = arc("Move_Panel4", PANEL_A0 + 0.4, PANEL_A1 - 0.4, SM_R - SKIN_T, SM_R, SM_SKIN_Z0 + 0.03,
                SM_SKIN_Z1 - 0.03, "gray_light")
    ck.set_origin(panel, (0, -SM_R, SM_SKIN_Z0 + 0.03))      # 下の辺の外側。ここを軸（X）に外へ開く

    # 外板4の中（第4区画）：仕切りの壁2枚、棚、酸素タンク2つ、燃料電池3つ、水素タンク1つ
    wall_len = SM_R - SKIN_T - 0.55
    wall_h = SM_SKIN_Z1 - SM_SKIN_Z0 + 0.06
    zc = (SM_SKIN_Z0 + SM_SKIN_Z1) / 2
    bay = []
    for i, a in enumerate((PANEL_A0, PANEL_A1)):
        bay.append(ck.box(f"BayWall{i}", polar(0.55 + wall_len / 2, a, zc), (wall_len, 0.06, wall_h), "navy",
                          rot=(0, 0, a), bevel=0))
        crisp(bay[-1])
    for i, z in enumerate((4.35, 5.75)):
        bay.append(ck.box(f"BayShelf{i}", (0, -1.22, z), (0.92, 0.95, 0.06), "gray", bevel=0))
        crisp(bay[-1])
    for i, x in enumerate((-0.36, 0.36)):
        bay.append(ck.sph(f"BayO2{i}", (x, -1.27, 5.08), 0.32, "cream", seg=8))
    for i, (x, y) in enumerate(((0, -1.45), (-0.38, -1.12), (0.38, -1.12))):
        bay.append(ck.cyl(f"BayCell{i}", (x, y, 6.40), 0.19, 0.95, "gray", verts=6, bevel=0))
        crisp(bay[-1])
    bay.append(ck.sph("BayH2", (0, -1.30, 3.72), 0.36, "gray_light", seg=8))
    ck.join(bay, "Bay4")

    # 小型噴射器 4か所（45°・135°・225°・315°）。箱＋噴射口4つ
    for k in range(4):
        th = 45.0 + 90.0 * k
        rz = Matrix.Rotation(math.radians(th), 4, "Z")

        def at(x, y, z):
            v = rz @ Matrix.Translation((x, y, z)).translation
            return (v.x, v.y, v.z)

        quad = [ck.box(f"RcsBox{k}", at(SM_R + 0.07, 0, RCS_Z), (0.20, 0.34, 0.42), "gray", rot=(0, 0, th), bevel=0)]
        r_out = SM_R + 0.20
        for j, (dy, dz, rx) in enumerate(((0, 0.30, 0), (0, -0.30, 180), (0.27, 0, -90), (-0.27, 0, 90))):
            quad.append(ck.cyl(f"RcsJet{k}{j}", at(r_out, dy, RCS_Z + dz), 0.05, 0.22, "gray_dark", r2=0.11,
                               rot=(rx, 0, th), verts=6, bevel=0))
        for q in quad:
            crisp(q)
        ck.join(quad, f"Rcs{k + 1}")
    ck.end_asset()
    return sm


SKIN_Z0_IN = SM_SKIN_Z0     # 遮熱板の上面＝外板の下端


# ---------------------------------------------------------------- 司令船
def build_cm(parent=None):
    cm = sub_asset("Ship_CM", parent)
    b = CM_BULGE
    # 遮熱板（底のふくらみ）。つないだ姿では機械船の中に隠れる
    lathe("HeatShield", [(0.0, 0.0), (0.95, 0.07), (1.55, 0.21), (1.80, b), (1.80, b + 0.02)], "gray_dark", bevel=0)
    # 円錐。側面は軸から約 33°
    lathe("Cone", [(1.80, b - 0.01), (CM_R, b + 0.12), (0.62, b + 2.18), (0.45, b + 2.72)], "white", bevel=0.04)
    crisp(ck.cyl("DockRing", (0, 0, b + 2.72 + 0.19), 0.40, 0.38, "gray", bevel=0))
    # 正面（−Y）の窓2つとハッチ。側面の傾きに合わせて寝かせる
    slope = math.degrees(math.atan2(CM_R - 0.62, 2.06))          # 約 33°
    # 正面のハッチ（まん中に丸窓）と、左右の細い窓
    ck.box("Hatch", (0, -1.33, b + 1.10), (0.70, 0.08, 0.78), "gray_light", rot=(-slope, 0, 0), bevel=0.02)
    crisp(ck.cyl("HatchWindow", (0, -1.36, b + 1.17), 0.14, 0.06, "visor", rot=(90 - slope, 0, 0), verts=8, bevel=0))
    for i, (x, a) in enumerate(((-0.78, -32), (0.78, 32))):
        crisp(ck.box(f"Window{i}", (x, -0.93, b + 1.42), (0.16, 0.08, 0.40), "visor", rot=(-slope, 0, a), bevel=0))
    ck.end_asset()
    return cm


# ---------------------------------------------------------------- 月着陸船
def build_lm(parent=None):
    lm = sub_asset("Ship_LM", parent)
    z0, h = LM_DESC_Z0, LM_DESC_H
    r_oct = LM_DESC_FLAT / 2 / math.cos(math.radians(22.5))
    # 下段：八角形の箱（面が ±X・±Y を向く）とエンジンのノズル
    ck.cyl("Descent", (0, 0, z0 + h / 2), r_oct, h, "orange", verts=8, rot=(0, 0, 22.5), bevel=0.04)
    ck.cyl("DescentNozzle", (0, 0, z0 - 0.33), 0.75, 0.70, "gray_dark", r2=0.32, bevel=0)
    crisp(bpy.data.objects["Ship_LM.DescentNozzle"])
    crisp(ck.box("Porch", (0, -2.45, z0 + h - 0.06), (0.95, 0.75, 0.10), "gray", bevel=0))

    # 脚4本（±X・±Y）。主脚＋支え2本＋皿。皿の外縁〜外縁が 9.4
    pad_c = LM_PAD_SPAN / 2 - LM_PAD_R
    legs, pads = [], []
    for k in range(4):
        th = 90.0 * k - 90.0                       # −Y から
        rz = Matrix.Rotation(math.radians(th), 4, "Z")

        def at(x, y, z):
            v = rz @ Matrix.Translation((x, y, z)).translation
            return (v.x, v.y, v.z)

        top, foot = (2.05, 0, z0 + h - 0.15), (pad_c, 0, 0.16)
        knee = tuple(top[i] + (foot[i] - top[i]) * 0.55 for i in range(3))
        legs.append(ck.tube(f"LegMain{k}", [at(*top), at(*foot)], 0.085, "gray_light"))
        for j, s in enumerate((-1, 1)):
            legs.append(ck.tube(f"LegBrace{k}{j}", [at(2.05, 0.85 * s, z0 + 0.12), at(*knee)], 0.055, "gray_light"))
        pads.append(ck.cyl(f"Pad{k}", at(pad_c, 0, 0.09), 0.30, 0.18, "orange", r2=LM_PAD_R, verts=10, bevel=0))
        crisp(pads[-1])
    ck.join(legs, "Legs")
    ck.join(pads, "Pads")

    # 上段：前の丸い操縦室（軸は Y）、まん中の箱、左右のタンク、後ろの機器箱、上のドッキング部
    za = LM_ASC_Z0
    ck.cyl("Cabin", (0, -0.78, za + 1.32), 1.17, 1.55, "gray_light", rot=(90, 0, 0), bevel=0.04)
    ck.box("Mid", (0, 0.40, za + 1.30), (2.55, 1.40, 2.40), "gray_light", bevel=0.04)
    for i, x in enumerate((-1.45, 1.45)):
        ck.sph(f"Tank{i}", (x, 0.35, za + 1.00), (0.55, 0.70, 0.62), "gray", seg=12)
    ck.box("AftBay", (0, 1.38, za + 1.45), (1.80, 0.62, 1.55), "gray", bevel=0.04)
    ck.cyl("DockTunnel", (0, 0, (za + 2.45 + LM_TOP) / 2), 0.42, LM_TOP - (za + 2.45), "gray", bevel=0)
    crisp(bpy.data.objects["Ship_LM.DockTunnel"])
    # 前面の窓2つとハッチ
    for i, x in enumerate((-0.52, 0.52)):
        crisp(ck.box(f"Window{i}", (x, -1.56, za + 1.78), (0.46, 0.08, 0.40), "visor",
                     rot=(0, 18 if x < 0 else -18, 0), bevel=0))
    ck.box("Hatch", (0, -1.56, za + 0.70), (0.66, 0.08, 0.66), "gray_dark", bevel=0.02)
    # 姿勢制御の噴射器（四すみの箱）と、皿のアンテナ1つ
    jets = [crisp(ck.box(f"Jet{i}", (x, y, za + 2.05), (0.26, 0.26, 0.30), "gray_dark", rot=(0, 0, 45), bevel=0))
            for i, (x, y) in enumerate(((-1.40, -0.55), (1.40, -0.55), (-1.40, 1.25), (1.40, 1.25)))]
    ck.join(jets, "Jets")
    dish = [crisp(ck.cyl("DishMast", (1.05, 0.55, za + 2.52), 0.04, 0.44, "gray_dark", verts=6, bevel=0)),
            crisp(ck.cyl("DishBowl", (1.05, 0.47, za + 2.78), 0.10, 0.14, "white", r2=0.30, rot=(60, 0, 0), verts=10,
                         bevel=0))]
    ck.join(dish, "Dish")
    ck.end_asset()
    return lm


def flip_lm_for_stack(lm):
    """月着陸船を上下逆にして、ドッキング部の先を原点（= Ship_LM の位置）に合わせる。
    Y軸まわりに 180° なので、窓とはしごのある面は −Y（正面）のまま。回転は finalize がメッシュに焼く。"""
    m = Matrix.Translation((0, 0, LM_TOP)) @ Matrix.Rotation(math.pi, 4, "Y")
    for o in lm.children:
        o.matrix_basis = m @ o.matrix_basis
    bpy.context.view_layer.update()


# ---------------------------------------------------------------- 書き出し
PANEL_PART = {"name": "Move_Panel4", "type": "rotate", "axis": "X", "min": 0, "max": PANEL_OPEN, "default": 0,
              "note": "事故で吹き飛ぶ外板（機械船の第4区画、−Y 側の 60°）。原点は下の辺の外側で、+ で外（−Y）へ開く。"
                      "吹き飛ばすときは Unity で子から外して −Y 向きに飛ばす"}


def write_stack_parts(root, sub_parts):
    """Ship_Stack の可動部の表を書く。ck.write_parts は「<ルート>.<部品>」の名前しか受けないので、
    入れ子の部品（Ship_SM.Move_Panel4）は同じ形式でここから足す。"""
    path = os.path.join(ck._export.MODELING, KIND, "parts.json")
    with open(path, encoding="utf-8") as f:
        data = json.load(f)
    out = []
    for p in sub_parts:
        q = dict(p)
        q["origin"] = [round(v, 5) for v in bpy.data.objects[p["name"]].matrix_world.translation]
        out.append(q)
    data[root.name] = out
    with open(path, "w", encoding="utf-8") as f:
        json.dump(dict(sorted(data.items())), f, ensure_ascii=False, indent=1); f.write("\n")
    return out


def finish(root, parts, dummy=True):
    ck.export_all(root, KIND)
    ck.render_check(root, KIND, parts, dummy=dummy)
    print(f"[build] {root.name}: {ck.tri_count(root)} 三角")


# 単体3つ
ck.clear_scene()
root = build_cm()
finish(root, [])

ck.clear_scene()
root = build_sm()
sm_parts = ck.write_parts(KIND, root, [PANEL_PART])
finish(root, sm_parts)

ck.clear_scene()
root = build_lm()
finish(root, [])

# つないだ姿
ck.clear_scene()
stack = ck.asset("Ship_Stack")
ck.end_asset()
sm = build_sm(stack)
cm = build_cm(stack)
cm.location = (0, 0, STACK_CM_Z)
lm = build_lm(stack)
lm.location = (0, 0, STACK_DOCK_Z)
flip_lm_for_stack(lm)
bpy.context.view_layer.update()
ck.finalize(stack)
write_stack_parts(stack, sm_parts)
# render_check も「<ルート>.<部品>」の名前しか動かせないので、つないだ姿の3枚目は後ろからの姿になる
# （外板4を開いた姿は Ship_SM_3_move.png）
finish(stack, [])
