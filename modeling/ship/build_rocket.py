"""Ship_RocketSmall・Ship_RocketLarge：設計画面で選ぶロケット2種（#47）。

書いた人：Taiga（作成：Fable 5.2）／ 最終更新：2026-11-14

  modeling/core/run.sh modeling/ship/build_rocket.py
出力：unity/Assets/Models/Ship/Ship_Rocket*.fbx、modeling/ship/export/、modeling/ship/renders/（各3枚＋320px 版、
      2つを並べた一覧 rocket_lineup.png と rocket_lineup_320.png）

大きさ（実寸。1 unit = 1m。設計画面では Unity 側で縮めて見せる）：
  大型 Ship_RocketLarge  高さ 約110m・直径 10.1m。Saturn V を下敷きにした3段（太い1・2段＋細い3段）。エンジン5基
  小型 Ship_RocketSmall  高さ 約70m・直径 6.6m。架空の2段（同じ太さ）。エンジン4基
  Saturn V の寸法（全高 110.6m、1・2段の直径 10.1m、3段の直径 6.6m）は Fable 5.2 の記憶から。出典と突き合わせていない。
  メッシュは新規（回転体と板）。NASA のメッシュは使っていない。
宇宙船と同じ決まり：原点＝ノズル出口の中心、長軸 +Z、底が z=0。旗・文字・記章なし。可動部なし。

2つの見分け方（色だけに頼らない）：
  高さと太さ／大型は途中で細くなる（3段目）、小型はまっすぐ／帯は大型 navy、小型 teal／エンジンは大型5基、小型4基

部品（下から）：
  Engines     ノズル（1つのメッシュ）。gray_dark
  Skirt       エンジンの付け根の板。gray_dark
  Stage1      1段目の胴。white
  Fairings    外側のエンジンを覆う円錐（大型だけ、4つを1つのメッシュ）。gray_light
  Fins        安定翼4枚（1つのメッシュ）。大型 navy、小型 teal
  Band1〜     胴を巻く帯。大型 navy、小型 teal
  Ring1〜     段の継ぎ目。gray_dark
  Stage2      2段目の胴。white
  Taper       細くなる継ぎ目（大型だけ）。gray_light
  Stage3      3段目の胴（大型だけ）。white
  Adapter     宇宙船を載せる円錐。cream
  Payload_SM・Payload_CM・Payload_Tower
              先端に見える宇宙船（機械船・司令船・脱出ロケット）の簡単な形。gray_light / cream / orange
              Unity で Ship_Stack の本物を載せたいときは、この3つを隠して Slot_Ship に置く
  Slot_Ship   宇宙船（Ship_Stack・Ship_SM の原点＝ノズル出口）を置く位置。ノズル 2.8m は Adapter の中に隠れる
"""
import sys, os, math
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "core"))
import bpy, bmesh
from mathutils import Vector
import corekit as ck
import render_check as rc

KIND = "ship"
SEG = 24                 # 胴の分割数（build_ship.py の大きな胴と同じ）
SHIP_R = 1.95            # 宇宙船（機械船・司令船）の半径。build_ship.py の SM_R と同じ
SM_NOZZLE = 2.8          # 機械船のノズルが外に出ている長さ（Adapter の中に隠す）
BEVEL = 0.04


def lathe(name, prof, color, seg=SEG, bevel=BEVEL):
    """回転体（build_ship.py の lathe と同じ作り）。prof は下から上への (半径, 高さ) の列。両端はふたでふさぐ。"""
    bm = bmesh.new()
    rings = []
    for r, z in prof:
        if r <= 1e-6:
            rings.append([bm.verts.new((0, 0, z))])
        else:
            rings.append([bm.verts.new((r * math.cos(2 * math.pi * i / seg), r * math.sin(2 * math.pi * i / seg), z))
                          for i in range(seg)])
    for a, b in zip(rings, rings[1:]):
        for i in range(seg):
            j = (i + 1) % seg
            if len(a) == 1:
                bm.faces.new((a[0], b[i], b[j]))
            elif len(b) == 1:
                bm.faces.new((a[i], a[j], b[0]))
            else:
                bm.faces.new((a[i], a[j], b[j], b[i]))
    if len(rings[0]) > 1:
        bm.faces.new(rings[0][::-1])
    if len(rings[-1]) > 1:
        bm.faces.new(rings[-1])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    return ck._fin(o, color, name, bevel=bevel)


def fin(name, r0, span, z0, root_h, tip_h, thick, az, color):
    """安定翼1枚。胴の半径 r0 から外へ span、付け根の高さ root_h、先の高さ tip_h（下の辺は水平）。az は度。"""
    a = math.radians(az)
    out = Vector((math.cos(a), math.sin(a), 0))
    side = Vector((-math.sin(a), math.cos(a), 0))
    pts = [(r0 - 0.3, z0), (r0 + span, z0), (r0 + span, z0 + tip_h), (r0 - 0.3, z0 + root_h)]
    bm = bmesh.new()
    fa = [bm.verts.new(out * r + side * (thick / 2) + Vector((0, 0, z))) for r, z in pts]
    fb = [bm.verts.new(out * r - side * (thick / 2) + Vector((0, 0, z))) for r, z in pts]
    bm.faces.new(fa); bm.faces.new(fb[::-1])
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new((fa[i], fb[i], fb[j], fa[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    return ck._fin(o, color, name, bevel=BEVEL, smooth=False)


def nozzle(name, x, y, r_exit, length, seg=10):
    """エンジンのノズル1基（出口が z=0）。のどは出口の 0.35 倍。"""
    o = lathe(name, [(r_exit, 0), (r_exit * 0.62, length * 0.55), (r_exit * 0.35, length), (r_exit * 0.5, length + 0.6)],
              "gray_dark", seg=seg)
    o.data.transform(__import__("mathutils").Matrix.Translation((x, y, 0)))
    return o


def band(name, r, z, h, color):
    return lathe(name, [(r + 0.08, z), (r + 0.08, z + h)], color)


def payload(z, tower_len):
    """先端の宇宙船の簡単な形。z は機械船の下端。返り値はてっぺんの高さ。"""
    sm_h, cm_h = 4.0, 3.1
    lathe("Payload_SM", [(SHIP_R, z), (SHIP_R, z + sm_h)], "gray_light")
    lathe("Payload_CM", [(SHIP_R, z + sm_h), (0.55, z + sm_h + cm_h - 0.35), (0.42, z + sm_h + cm_h)], "cream")
    t0 = z + sm_h + cm_h
    # 脱出ロケット：太い柱と、付け根の広がり、先のとがり（細い骨組みは作らない）
    lathe("Payload_Tower", [(0.75, t0 - 0.1), (0.38, t0 + 0.9), (0.38, t0 + tower_len - 1.2), (0.05, t0 + tower_len)],
          "orange", seg=10)
    ck.slot("Slot_Ship", (0, 0, z - SM_NOZZLE))
    return t0 + tower_len


def build_large():
    root = ck.asset("Ship_RocketLarge")
    R, R3 = 5.05, 3.30
    eng_len, eng_r = 5.0, 1.85
    ck.join([nozzle(f"E{i}", x, y, eng_r, eng_len) for i, (x, y) in
             enumerate([(0, 0), (3.4, 0), (-3.4, 0), (0, 3.4), (0, -3.4)])], "Engines")
    z = eng_len
    lathe("Skirt", [(R - 0.25, z), (R, z + 0.8)], "gray_dark")
    lathe("Stage1", [(R, z + 0.8), (R, 46.0)], "white")
    # 外側のエンジンを覆う円錐と、その外の安定翼（Saturn V の1段目の形）。エンジン4基と同じ向き
    fair = []
    for i, az in enumerate((0, 90, 180, 270)):
        a = math.radians(az)
        c = lathe(f"F{i}", [(1.9, z + 0.4), (1.9, z + 2.5), (0.25, z + 10.5)], "gray_light", seg=12)
        c.data.transform(__import__("mathutils").Matrix.Translation((math.cos(a) * 4.35, math.sin(a) * 4.35, 0)))
        fair.append(c)
    ck.join(fair, "Fairings")
    ck.join([fin(f"Fin{i}", R + 0.9, 3.2, z + 0.4, 7.0, 2.6, 0.45, az, "navy")
             for i, az in enumerate((0, 90, 180, 270))], "Fins")
    band("Band1", R, 20.0, 4.0, "navy")
    band("Band2", R, 36.0, 4.0, "navy")
    lathe("Ring1", [(R + 0.12, 46.0), (R + 0.12, 49.5)], "gray_dark")
    lathe("Stage2", [(R, 49.5), (R, 74.0)], "white")
    band("Band3", R, 68.0, 3.0, "navy")
    lathe("Taper", [(R, 74.0), (R3, 79.5)], "gray_light")
    lathe("Stage3", [(R3, 79.5), (R3, 92.0)], "white")
    band("Band4", R3, 83.0, 2.5, "navy")
    lathe("Ring2", [(R3 + 0.1, 92.0), (R3 + 0.1, 93.0)], "gray_dark")
    lathe("Adapter", [(R3, 93.0), (SHIP_R, 99.6)], "cream")
    top = payload(99.6, 4.0)
    ck.end_asset()
    print(f"[build] Ship_RocketLarge 高さ {top:.1f}m")
    return root


def build_small():
    root = ck.asset("Ship_RocketSmall")
    R = 3.30
    eng_len, eng_r = 3.6, 1.25
    d = 1.75
    ck.join([nozzle(f"E{i}", x, y, eng_r, eng_len) for i, (x, y) in
             enumerate([(d, d), (-d, d), (-d, -d), (d, -d)])], "Engines")
    z = eng_len
    lathe("Skirt", [(R - 0.2, z), (R, z + 0.7)], "gray_dark")
    lathe("Stage1", [(R, z + 0.7), (R, 36.0)], "white")
    ck.join([fin(f"Fin{i}", R, 2.6, z + 0.3, 6.0, 2.0, 0.4, az, "teal")
             for i, az in enumerate((45, 135, 225, 315))], "Fins")
    band("Band1", R, 24.0, 4.0, "teal")
    lathe("Ring1", [(R + 0.1, 36.0), (R + 0.1, 38.5)], "gray_dark")
    lathe("Stage2", [(R, 38.5), (R, 53.0)], "white")
    band("Band2", R, 46.0, 3.0, "teal")
    lathe("Ring2", [(R + 0.1, 53.0), (R + 0.1, 54.0)], "gray_dark")
    lathe("Adapter", [(R, 54.0), (SHIP_R, 59.4)], "cream")
    top = payload(59.4, 3.5)
    ck.end_asset()
    print(f"[build] Ship_RocketSmall 高さ {top:.1f}m")
    return root


def lineup(roots, path, gap):
    """2つを横に並べて、ほぼ真横（少し上）から1枚。大きさの違いを見るための画像。320px 版も出す。"""
    n = len(roots)
    for i, r in enumerate(roots):
        r.location = ((i - (n - 1) / 2) * gap, 0, 0)
    bpy.context.view_layer.update()
    lo, hi = ck._export.world_bounds([m for r in roots for m in ck.meshes(r)])
    rc._world()
    made, cam, _ = rc._helpers(lo, hi, False, False)
    scn = bpy.context.scene
    scn.render.engine = "BLENDER_EEVEE"
    scn.render.resolution_x, scn.render.resolution_y = rc.RES
    scn.render.resolution_percentage = 100
    scn.view_settings.view_transform = "Standard"; scn.view_settings.look = "None"
    rc._aim(cam, rc._corners(lo, hi), 25, 8, margin=1.0)
    rc._render(path)
    rc._small(path)
    print("[lineup]", path)


if __name__ == "__main__":
    ck.clear_scene()
    roots = []
    for build in (build_small, build_large):
        root = build()
        ck.export_all(root, KIND)
        ck.render_check(root, KIND, dummy=False)
        print(f"[build] {root.name}: {ck.tri_count(root)} 三角")
        roots.append(root)
    lineup(roots, os.path.join(ck._export.MODELING, KIND, "renders", "rocket_lineup.png"), gap=22)
