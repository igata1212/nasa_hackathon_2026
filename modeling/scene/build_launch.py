"""Scene_LaunchPad・Scene_Ground：出発の場面（storyboard.md の場面1）の発射台と地面（#52）。

書いた人：Taiga（作成：Fable 5.2）／ 最終更新：2026-11-14

  modeling/core/run.sh modeling/scene/build_launch.py
出力：unity/Assets/Models/Scene/Scene_LaunchPad.fbx・Scene_Ground.fbx、modeling/scene/export/、
      modeling/scene/renders/（各3枚＋320px 版、組み合わせた launch_scene.png と launch_scene_320.png）、
      modeling/scene/parts.json（腕3本）

決めたこと（#52、Taiga、2026-11-14）：出発の場面は 3D で作る。発射台と地面だけ。ロケットの断面は作らない
  （#47 のロケットを立てて外から見せる）。

大きさ：#47 の大型ロケット（Ship_RocketLarge、高さ 約110m・直径 10.1m）に合わせた実寸。Unity 側で縮めてよい。
  形は Saturn V の移動式発射台（台＋横の塔＋腕）を思い出して箱と太い柱だけで組んだ。寸法は出典と突き合わせていない。
  細い鉄骨・旗・文字・記章なし。
向き：正面は −Y。塔はロケットの右（+X）に立つ。腕は塔からロケット（−X）へ伸びる。
ベベル：実寸で 100m を超えるので、標準の 2〜4cm では角の光が見えない。大きな箱は 0.25m にした（ルールからの例外）。

Scene_LaunchPad（原点＝台の脚の底の中心。ロケットの真下。底が z=0）：
  Legs          台を支える太い脚6本（1つのメッシュ）。gray_dark
  Platform      台（gray_light）＋ふちの帯（navy）＋ロケットの下の噴射口（gray_dark）。1つのメッシュ
  HoldDowns     ロケットの根元を押さえる爪4個（1つのメッシュ）。orange
  Tower         塔：太い柱4本（orange）＋床9枚（gray_dark）＋中の箱（gray_light）。1つのメッシュ
  Crane         塔のてっぺんのクレーン（gray_light と orange）
  Lamp_Beacon   てっぺんの灯（lamp_red。発光は Unity）
  Move_Arm_1〜3 塔からロケットへ伸びる腕（下から。3 がクルーの乗る腕で、先に白い部屋）。
                Z まわりに回る。0＝ロケットに付いた姿、−90＝奥（+Y）へ逃がした姿。原点は塔の面
  Slot_Rocket   ロケット（Ship_Rocket* の原点＝ノズル出口）を置く位置。台の上面
Scene_Ground（原点＝地面の底の中心。底が z=0）：
  Land          弧を描く地面（球の一部。直径 800m、中央が 40m 高い）。cream
  Hardstand     発射台の下の平らな舗装（直径 120m）。gray_light
  Road          舗装から奥（+Y）へ伸びる道（ロケットを運んできた道）。gray
  Slot_Pad      Scene_LaunchPad の原点を置く位置（舗装の上面の中心）
"""
import sys, os, math
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "core"))
import bpy, bmesh
from mathutils import Vector
import corekit as ck
import render_check as rc

KIND = "scene"
BV = 0.25                 # 大きな箱のベベル

# ---- 発射台 ----
LEG_H = 6.0
PLAT_H = 8.0
PLAT_TOP = LEG_H + PLAT_H             # 台の上面＝ロケットのノズル出口の高さ
PLAT_X = (-18.0, 30.0)                # 台の左右（塔の分だけ右に長い）
PLAT_Y = 17.0                         # 台の奥行の半分
TOWER_X = 23.0                        # 塔の中心
TOWER_W = 9.0                         # 塔の幅（正方形）
TOWER_H = 104.0                       # 塔の高さ（台の上から）
TOWER_FACE = TOWER_X - TOWER_W / 2    # 塔のロケット側の面
# 腕：(台の上からの高さ, ロケットの半径)。#47 の大型：1段 5.05、2段 5.05、司令船 1.95（高さ 約103〜107m）
ARMS = [(30.0, 5.05), (62.0, 5.05), (98.5, 3.3)]
# ---- 地面 ----
G_R, G_H = 400.0, 40.0                # 地面の半径、中央の高さ
G_SPH = (G_R * G_R + G_H * G_H) / (2 * G_H)     # 球の半径
HARD_R = 60.0
HARD_TOP = G_H + 0.3


def ground_z(r):
    return math.sqrt(G_SPH * G_SPH - r * r) - (G_SPH - G_H)


def mesh_obj(name, bm):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    return o


def build_pad():
    root = ck.asset("Scene_LaunchPad")
    px = (PLAT_X[0] + PLAT_X[1]) / 2
    pw = PLAT_X[1] - PLAT_X[0]

    # 脚6本
    legs = []
    for i, x in enumerate((PLAT_X[0] + 4, px, PLAT_X[1] - 4)):
        for j, y in enumerate((-PLAT_Y + 4, PLAT_Y - 4)):
            legs.append(ck.box(f"Leg{i}{j}", (x, y, LEG_H / 2 + 0.2), (5.0, 5.0, LEG_H + 0.4), "gray_dark", bevel=BV))
    ck.join(legs, "Legs")

    # 台＋帯＋噴射口
    deck = ck.box("PlatDeck", (px, 0, LEG_H + PLAT_H / 2), (pw, 2 * PLAT_Y, PLAT_H), "gray_light", bevel=0.4)
    trim = ck.box("PlatTrim", (px, 0, LEG_H + PLAT_H - 2.0), (pw + 0.6, 2 * PLAT_Y + 0.6, 1.6), "navy", bevel=BV)
    hole = ck.box("PlatHole", (0, 0, PLAT_TOP - 0.3), (15.0, 15.0, 1.0), "gray_dark", bevel=BV)
    ck.join([deck, trim, hole], "Platform")

    # 爪4個（安定翼は 0・90・180・270° なので、その間の 45° に置く）
    hd = []
    for i, az in enumerate((45, 135, 225, 315)):
        a = math.radians(az)
        hd.append(ck.box(f"Hold{i}", (7.6 * math.cos(a), 7.6 * math.sin(a), PLAT_TOP + 1.5), (2.6, 2.6, 3.4), "orange",
                         bevel=BV, rot=(0, 0, az)))
    ck.join(hd, "HoldDowns")

    # 塔：柱4本＋床＋中の箱
    tw = []
    c = TOWER_W / 2 - 0.9
    for i, (sx, sy) in enumerate(((-1, -1), (1, -1), (1, 1), (-1, 1))):
        tw.append(ck.box(f"Col{i}", (TOWER_X + sx * c, sy * c, PLAT_TOP + TOWER_H / 2), (1.8, 1.8, TOWER_H), "orange",
                         bevel=BV))
    n_floor = 9
    for i in range(n_floor):
        z = PLAT_TOP + 8.0 + i * (TOWER_H - 8.0) / (n_floor - 1)
        tw.append(ck.box(f"Floor{i}", (TOWER_X, 0, z - 0.7), (TOWER_W + 0.8, TOWER_W + 0.8, 1.4), "gray_dark", bevel=BV))
    tw.append(ck.box("Core", (TOWER_X + 0.6, 0.6, PLAT_TOP + TOWER_H / 2 - 1.0), (4.6, 4.6, TOWER_H - 2.0), "gray_light",
                     bevel=BV))
    ck.join(tw, "Tower")

    # クレーン
    top = PLAT_TOP + TOWER_H
    cab = ck.box("CraneCab", (TOWER_X, 0, top + 2.5), (7.0, 6.0, 5.0), "gray_light", bevel=0.4)
    boom = ck.box("CraneBoom", (TOWER_X - 9.0, 0, top + 6.2), (30.0, 2.4, 2.4), "orange", bevel=BV)
    weight = ck.box("CraneWeight", (TOWER_X + 5.0, 0, top + 5.6), (4.0, 3.6, 3.6), "gray_dark", bevel=BV)
    ck.join([cab, boom, weight], "Crane")
    ck.cyl("Lamp_Beacon", (TOWER_X, 0, top + 8.6), 0.9, 1.8, "lamp_red", bevel=0.2)

    # 腕3本
    parts = []
    for i, (h, r) in enumerate(ARMS, 1):
        z = PLAT_TOP + h
        tip_x = r + 0.2
        length = TOWER_FACE - tip_x
        last = i == len(ARMS)
        beam = ck.box(f"ArmBeam{i}", (tip_x + length / 2 + 0.5, 0, z), (length + 1.0, 3.0, 2.6), "orange", bevel=BV)
        if last:      # クルーが乗り込む白い部屋
            tip = ck.box(f"ArmTip{i}", (tip_x + 2.2, 0, z + 0.3), (4.4, 4.4, 4.2), "white", bevel=0.4)
        else:
            tip = ck.box(f"ArmTip{i}", (tip_x + 1.0, 0, z), (2.0, 3.8, 3.4), "gray_light", bevel=BV)
        arm = ck.join([beam, tip], f"Move_Arm_{i}")
        ck.set_origin(arm, (TOWER_FACE, 0, z))
        parts.append({"name": f"Move_Arm_{i}", "type": "rotate", "axis": "Z", "min": -90, "max": 0, "default": 0,
                      "note": "0＝ロケットに付いた姿、−90＝奥（+Y）へ逃がした姿。打ち上げの直前に逃がす"
                              + ("。先の白い箱がクルーの乗る部屋" if last else "")})

    ck.slot("Slot_Rocket", (0, 0, PLAT_TOP))
    ck.end_asset()
    return root, parts


def build_ground():
    root = ck.asset("Scene_Ground")
    seg, rings = 32, 8
    bm = bmesh.new()
    top = bm.verts.new((0, 0, G_H))
    loops = []
    for k in range(1, rings + 1):
        r = G_R * k / rings
        loops.append([bm.verts.new((r * math.cos(2 * math.pi * i / seg), r * math.sin(2 * math.pi * i / seg), ground_z(r)))
                      for i in range(seg)])
    for i in range(seg):
        bm.faces.new((top, loops[0][i], loops[0][(i + 1) % seg]))
    for a, b in zip(loops, loops[1:]):
        for i in range(seg):
            j = (i + 1) % seg
            bm.faces.new((a[i], b[i], b[j], a[j]))
    bm.faces.new(loops[-1][::-1])
    ck._fin(mesh_obj("Land", bm), "cream", "Land")

    ck.cyl("Hardstand", (0, 0, HARD_TOP - 1.5), HARD_R, 3.0, "gray_light", verts=24, bevel=BV)

    # 道：舗装から奥（+Y）へ。地面の丸みに沿わせる
    bm = bmesh.new()
    half, n = 14.0, 8
    rows = []
    for k in range(n + 1):
        y = HARD_R - 4.0 + (G_R - 6.0 - HARD_R) * k / n
        z = ground_z(math.hypot(half, y))
        rows.append([bm.verts.new((sx * half, y, z + dz)) for sx in (-1, 1) for dz in (-1.5, 0.35)])
    for a, b in zip(rows, rows[1:]):     # 各行：左下・左上・右下・右上
        bm.faces.new((a[1], a[3], b[3], b[1]))          # 上
        bm.faces.new((a[0], b[0], b[2], a[2]))          # 下
        bm.faces.new((a[0], a[1], b[1], b[0]))          # 左
        bm.faces.new((a[2], b[2], b[3], a[3]))          # 右
    bm.faces.new((rows[0][0], rows[0][2], rows[0][3], rows[0][1]))
    bm.faces.new((rows[-1][0], rows[-1][1], rows[-1][3], rows[-1][2]))
    ck._fin(mesh_obj("Road", bm), "gray", "Road", smooth=False)

    ck.slot("Slot_Pad", (0, 0, HARD_TOP))
    ck.end_asset()
    return root


def scene_render(pad, ground, path):
    """地面・発射台・ロケット（#47 の glb）を組み合わせた1枚。腕は付いた姿。320px 版も出す。"""
    for o in ck.tree(pad) + ck.tree(ground):
        o.hide_render = False
    pad.location = (0, 0, HARD_TOP)
    glb = os.path.join(ck._export.MODELING, "ship", "export", "Ship_RocketLarge.glb")
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=glb)
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        if o.parent is None:
            o.location = (0, 0, HARD_TOP + PLAT_TOP)
    bpy.context.view_layer.update()
    lo = Vector((-70, -70, HARD_TOP - 6)); hi = Vector((70, 70, HARD_TOP + PLAT_TOP + 116))
    rc._world()
    made, cam, _ = rc._helpers(lo, hi, False, False)
    scn = bpy.context.scene
    scn.render.engine = "BLENDER_EEVEE"
    scn.render.resolution_x, scn.render.resolution_y = rc.RES
    scn.render.resolution_percentage = 100
    scn.view_settings.view_transform = "Standard"; scn.view_settings.look = "None"
    cam.data.clip_end = 5000
    rc._aim(cam, rc._corners(lo, hi), 20, 6, margin=1.0)
    rc._render(path)
    rc._small(path)
    print("[scene]", path)


if __name__ == "__main__":
    ck.clear_scene()
    pad, parts = build_pad()
    ck.write_parts(KIND, pad, parts)
    ck.export_all(pad, KIND)
    ck.render_check(pad, KIND, [dict(p, max=p["min"]) for p in parts], dummy=False)   # 3枚目は腕を逃がした姿
    print(f"[build] {pad.name}: {ck.tri_count(pad)} 三角")
    ground = build_ground()
    ck.export_all(ground, KIND)
    ck.render_check(ground, KIND, dummy=False)
    print(f"[build] {ground.name}: {ck.tri_count(ground)} 三角")
    scene_render(pad, ground, os.path.join(ck._export.MODELING, KIND, "renders", "launch_scene.png"))
