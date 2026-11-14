"""Part_SolarPanel・Part_FuelTank・Part_RadarSounder：設計画面で選ぶ部品のうち「余裕があれば」の3つ（#48）。

書いた人：Taiga（作成：Fable 5.2）／ 最終更新：2026-11-14

  modeling/core/run.sh modeling/part/build_parts_extra.py
出力：unity/Assets/Models/Part/Part_*.fbx、modeling/part/export/、modeling/part/renders/（各3枚＋320px 版、
      3つを並べた一覧 parts_extra_lineup.png と parts_extra_lineup_320.png）
どれも原点＝底の中心、正面 −Y、可動部なし。メッシュは新規。

Part_SolarPanel（ソーラーパネル。板 2×1m）：
  Mount   付け根の台と太い柱。gray
  Panel   gray_light の枠の板（厚み 5cm）に、navy のセル8枚を載せた1つのメッシュ。正面へ 25° 傾けてある
Part_FuelTank（追加の燃料タンク。カプセル形）：
  Plinth・Cradle  台（酸素タンクと同じ作り）。gray_dark / gray
  Shell   縦長のカプセル（直径 0.44m、高さ 0.8m）。orange（球形で white の酸素タンクと、形と色で見分ける）
  Band1・Band2  胴の帯2本。gray_dark
  Cap     てっぺんの口。gray_light
Part_RadarSounder（レーダーサウンダー）：
  Body    箱。cream
  Stripe  正面の帯。teal
  Antenna 左右に出る太いアンテナ棒2本（太さ 5cm）と先の玉。gray_light と cream を1つのメッシュに
  Dish    上の傾いた皿＋受信棒＋先の玉（スイッチ Ctrl_Instr_Radar のアイコンと同じ形・同じ色）。teal / gray_light / cream
          カメラ（レンズ）・分光計とは、皿とアンテナ棒で見分ける
"""
import sys, os, math
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "core"))
import bpy
from mathutils import Matrix, Vector
import corekit as ck
import render_check as rc

KIND = "part"


def build_solar():
    root = ck.asset("Part_SolarPanel")
    ck.join([ck.box("MountBase", (0, 0.18, 0.05), (0.40, 0.30, 0.10), "gray", bevel=0.02),
             ck.cyl("MountPost", (0, 0.18, 0.215), 0.06, 0.27, "gray", bevel=0.01, verts=12)], "Mount")
    # 板はまず水平に作り、まとめてから傾ける
    pw, pd, pt = 2.0, 1.0, 0.05
    pieces = [ck.box("Frame", (0, 0, 0), (pw, pd, pt), "gray_light", bevel=0.0125)]
    nx, ny, gap = 4, 2, 0.05
    cw, cd = (pw - gap * (nx + 1)) / nx, (pd - gap * (ny + 1)) / ny
    for i in range(nx):
        for j in range(ny):
            x = -pw / 2 + gap + cw / 2 + i * (cw + gap)
            y = -pd / 2 + gap + cd / 2 + j * (cd + gap)
            pieces.append(ck.box(f"Cell{i}{j}", (x, y, pt / 2 + 0.005), (cw, cd, 0.03), "navy", bevel=0.006))
    panel = ck.join(pieces, "Panel")
    tilt = math.radians(25)
    h = 0.5 * math.sin(tilt) + 0.08                       # 手前の辺が床から 8cm 浮く高さ
    panel.data.transform(Matrix.Translation((0, 0, h)) @ Matrix.Rotation(tilt, 4, "X"))
    ck.end_asset()
    return root


def build_fuel():
    root = ck.asset("Part_FuelTank")
    r, cyl_h, z0 = 0.22, 0.36, 0.14
    ck.box("Plinth", (0, 0, 0.04), (0.46, 0.46, 0.08), "gray_dark", bevel=0.02)
    ck.cyl("Cradle", (0, 0, 0.12), 0.15, 0.08, "gray", r2=0.19, bevel=0.012)
    zc = z0 + r + cyl_h / 2
    body = ck.cyl("ShellMid", (0, 0, zc), r, cyl_h, "orange", bevel=0)
    top = ck.dome("ShellTop", (0, 0, zc + cyl_h / 2), r, "orange")
    bot = ck.dome("ShellBot", (0, 0, zc - cyl_h / 2), r, "orange", rot=(180, 0, 0))
    shell = ck.join([body, top, bot], "Shell")
    for p in shell.data.polygons:
        p.use_smooth = True
    ck.cyl("Band1", (0, 0, zc + cyl_h / 2 - 0.03), r + 0.012, 0.05, "gray_dark", bevel=0.012)
    ck.cyl("Band2", (0, 0, zc - cyl_h / 2 + 0.03), r + 0.012, 0.05, "gray_dark", bevel=0.012)
    ck.cyl("Cap", (0, 0, zc + cyl_h / 2 + r + 0.02), 0.07, 0.08, "gray_light", bevel=0.012, verts=12)
    ck.end_asset()
    return root


def build_radar():
    root = ck.asset("Part_RadarSounder")
    bw, bd, bh = 0.44, 0.32, 0.26
    ck.box("Body", (0, 0, bh / 2), (bw, bd, bh), "cream", bevel=0.03)
    ck.box("Stripe", (0, -bd / 2 - 0.004, bh / 2), (bw - 0.10, 0.03, 0.07), "teal", bevel=0.0075)
    rods = []
    for s in (-1, 1):
        x0 = s * bw / 2
        rods.append(ck.cyl(f"Rod{s + 1}", (x0 + s * 0.30, 0, bh * 0.6), 0.025, 0.64, "gray_light", rot=(0, 90, 0),
                           bevel=0.006, verts=10))
        rods.append(ck.sph(f"RodTip{s + 1}", (x0 + s * 0.63, 0, bh * 0.6), 0.045, "cream", seg=10))
    ck.join(rods, "Antenna")
    # 皿：Ctrl_Instr_Radar のアイコン（傾いた teal の皿＋受信棒＋ cream の玉）を大きくした形
    loc = Vector((0, 0, bh + 0.07))
    d = Vector((0, -math.sin(math.radians(35)), math.cos(math.radians(35))))      # 皿の向き（正面の上）
    rot = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
    stem = ck.cyl("DishStem", (0, 0, bh + 0.035), 0.035, 0.09, "gray_light", bevel=0.008, verts=10)
    # 平らな面を正面の上（d）へ向け、ふくらみを背中側にする（アイコンと同じ向き）
    dish = ck.dome("DishBowl", (0, 0, 0), (0.15, 0.15, 0.07), "teal")
    dish.data.transform(Matrix.Translation(loc + d * 0.07) @ rot @ Matrix.Rotation(math.pi, 4, "X"))
    feed = ck.cyl("DishFeed", (0, 0, 0), 0.025, 0.12, "gray_light", bevel=0.005, verts=8)
    feed.data.transform(Matrix.Translation(loc + d * 0.13) @ rot)
    tip = ck.sph("DishTip", tuple(loc + d * 0.20), 0.04, "cream", seg=10)
    ck.join([stem, dish, feed, tip], "Dish")
    ck.end_asset()
    return root


def lineup(roots, path, gap):
    """横に並べて、ゲームと同じ角度で1枚。320px 版も出す（tank/build_tank.py の lineup と同じやり方）。"""
    n = len(roots)
    for i, r in enumerate(roots):
        r.location = ((i - (n - 1) / 2) * gap, 0, 0)
    bpy.context.view_layer.update()
    lo, hi = ck._export.world_bounds([m for r in roots for m in ck.meshes(r)])
    rc._world()
    made, cam, dummy = rc._helpers(lo, hi, False, True)
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 0, -0.001))
    fl = bpy.context.object; fl.scale = (hi.x - lo.x + 3, hi.y - lo.y + 3, 1)
    fl.data.materials.append(rc._mat("__lineup_floor", "#3D4B6E"))
    scn = bpy.context.scene
    scn.render.engine = "BLENDER_EEVEE"
    scn.render.resolution_x, scn.render.resolution_y = rc.RES
    scn.render.resolution_percentage = 100
    scn.view_settings.view_transform = "Standard"; scn.view_settings.look = "None"
    rc._aim(cam, rc._corners(lo, hi) + dummy, 0, 50, margin=0.85)
    rc._render(path)
    rc._small(path)
    print("[lineup]", path)


if __name__ == "__main__":
    ck.clear_scene()
    roots = []
    for build in (build_solar, build_fuel, build_radar):
        root = build()
        ck.export_all(root, KIND)
        ck.render_check(root, KIND)
        print(f"[build] {root.name}: {ck.tri_count(root)} 三角")
        roots.append(root)
    lineup(roots, os.path.join(ck._export.MODELING, KIND, "renders", "parts_extra_lineup.png"), gap=1.6)
