"""Part_Battery・Part_Camera・Part_Spectrometer・Part_HighGainAntenna：設計画面を 3D にするとき（#168）に足りない部品4つ。

書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14

  modeling/core/run.sh modeling/part/build_parts_design.py
出力：unity/Assets/Models/Part/Part_*.fbx、modeling/part/export/、modeling/part/renders/（各3枚＋320px 版、
      4つを並べた parts_design_lineup.png、既存の3つも入れた7つの一覧 parts_all_lineup.png。どちらも 320px 版つき）
どれも原点＝底の中心、底が船への取り付け面、+Z が船の外向き、可動部なし。寸法と色は tmp/handoff/168-plan.md 5章。

Part_Battery（電池。台の上に立つ teal の箱）：
  Plinth    台 0.56×0.36×0.06。gray_dark
  Body      本体の箱 0.50×0.30×0.55（ベベル 4cm）。teal
  Terminals 上の端子2本（r 0.05、h 0.08）。gray_light
  Bolt      稲妻の板（厚み 3cm）を正面（−Y）と背面（+Y）に1枚ずつ。cream（船への付け方で裏が見えても読めるように）
Part_Camera（カメラ。外向きの鏡筒）：
  Plinth  台 0.40×0.40×0.06。gray
  Body    本体 0.36×0.36×0.28。cream
  Barrel  +Z 向きの鏡筒（r 0.14、h 0.22）。gray_dark
  Rim     鏡筒の口の輪。orange
  Lens    レンズの丸いふくらみ（r 0.11）。visor
Part_Spectrometer（分光計。円柱の上に三角柱）：
  Plinth  台 0.46×0.46×0.06。gray
  Body    +Z 向きの円柱（r 0.20、h 0.42）。cream
  Band    胴の帯。navy
  Prism   上の三角柱（辺 0.30、厚み 0.16）。orange。三角の面が +Z を向く（スイッチ Ctrl_Instr_Spectrometer と同じ向き）。
          丸いレンズのカメラとは形で見分ける
Part_HighGainAntenna（アポロの4枚皿のアンテナ）：
  Foot    付け根の丸い台。gray_dark
  Post    柱（r 0.05、h 0.45）。gray
  Arms    柱の先の十字の腕と、真ん中の玉。gray
  Dishes  皿4枚（r 0.26、2×2、くぼみが +Z）。white
  Feeds   各皿の中心の棒と玉。orange
"""
import sys, os, math
sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "core"))
import bpy
import bmesh
from mathutils import Matrix
import corekit as ck
import render_check as rc

KIND = "part"


def plate(name, pts, y0, t, color, bevel=None):
    """XZ 平面の多角形 pts（(x, z) の列。凹んでいてもよい）を、y0 から −Y へ厚み t だけ押し出した板。"""
    me = bpy.data.meshes.new("tmp_plate")
    bm = bmesh.new()
    back = [bm.verts.new((x, y0, z)) for x, z in pts]
    front = [bm.verts.new((x, y0 - t, z)) for x, z in pts]
    n = len(pts)
    bm.faces.new(back)
    bm.faces.new(front)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((back[i], back[j], front[j], front[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new("tmp_plate", me)
    bpy.context.scene.collection.objects.link(o)
    return ck._fin(o, color, name, bevel=ck._bevel(bevel, t))


def prism_z(name, pts, z0, h, color, bevel=None):
    """XY 平面の凸多角形 pts を z0 から高さ h だけ押し出した柱（controls/build_controls_a.py の prism と同じ作り）。"""
    me = bpy.data.meshes.new("tmp_prism")
    bm = bmesh.new()
    bot = [bm.verts.new((x, y, z0)) for x, y in pts]
    top = [bm.verts.new((x, y, z0 + h)) for x, y in pts]
    n = len(pts)
    bm.faces.new(bot)
    bm.faces.new(top)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((bot[i], bot[j], top[j], top[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new("tmp_prism", me)
    bpy.context.scene.collection.objects.link(o)
    xs, ys = [p[0] for p in pts], [p[1] for p in pts]
    thin = min(h, max(xs) - min(xs), max(ys) - min(ys))
    return ck._fin(o, color, name, bevel=ck._bevel(bevel, thin))


# 稲妻の形（(x, z)。中心 0,0、高さ約 0.36）。上の太い斜めの帯 → 段 → 下の尖り
BOLT = [(0.035, 0.18), (-0.085, 0.005), (-0.005, 0.005), (-0.055, -0.18),
        (0.085, 0.03), (0.005, 0.03), (0.085, 0.18)]


def build_battery():
    root = ck.asset("Part_Battery")
    ck.box("Plinth", (0, 0, 0.03), (0.56, 0.36, 0.06), "gray_dark", bevel=0.02)
    bw, bd, bh, z0 = 0.50, 0.30, 0.55, 0.06
    ck.box("Body", (0, 0, z0 + bh / 2), (bw, bd, bh), "teal", bevel=0.04)
    terms = [ck.cyl(f"Term{i}", (s * 0.13, 0, z0 + bh + 0.04 - 0.005), 0.05, 0.08, "gray_light", bevel=0.015, verts=12)
             for i, s in enumerate((-1, 1))]
    ck.join(terms, "Terminals")
    zc = z0 + bh / 2
    pts = [(x, z + zc) for x, z in BOLT]
    front = plate("BoltFront", pts, -bd / 2 + 0.005, 0.03, "cream", bevel=0.0075)
    back = plate("BoltBack", [(-x, z) for x, z in pts], bd / 2 + 0.025, 0.03, "cream", bevel=0.0075)
    ck.join([front, back], "Bolt")
    ck.end_asset()
    return root


def build_camera():
    root = ck.asset("Part_Camera")
    ck.box("Plinth", (0, 0, 0.03), (0.40, 0.40, 0.06), "gray", bevel=0.02)
    ck.box("Body", (0, 0, 0.06 + 0.14), (0.36, 0.36, 0.28), "cream", bevel=0.04)
    z1 = 0.34                                   # 本体の上面
    ck.cyl("Barrel", (0, 0, z1 + 0.11 - 0.01), 0.14, 0.22, "gray_dark", bevel=0.02)
    ztop = z1 + 0.22 - 0.01
    ck.tor("Rim", (0, 0, ztop - 0.005), 0.135, 0.028, "orange")
    ck.dome("Lens", (0, 0, ztop - 0.03), (0.11, 0.11, 0.09), "visor")
    # レンズの光り（白い点。マンガの「きらっ」。暗いレンズでも遠目にレンズと分かるように）
    ck.sph("Glint", (-0.04, -0.035, ztop + 0.045), (0.028, 0.028, 0.02), "white", seg=8)
    # シャッターのボタン（本体の上の角。カメラらしさ）
    ck.cyl("Shutter", (0.12, -0.12, z1 + 0.025 - 0.005), 0.04, 0.05, "orange", bevel=0.012, verts=12)
    ck.end_asset()
    return root


def build_spectrometer():
    root = ck.asset("Part_Spectrometer")
    ck.cyl("Plinth", (0, 0, 0.03), 0.24, 0.06, "gray", bevel=0.02)
    r, h, z0 = 0.20, 0.42, 0.06
    ck.cyl("Body", (0, 0, z0 + h / 2), r, h, "cream", bevel=0.03)
    ck.cyl("Band", (0, 0, z0 + h * 0.42), r + 0.015, 0.07, "navy", bevel=0.015)
    side = 0.30
    R = side / math.sqrt(3)                     # 外接円の半径
    # 頂点が正面（−Y）を向く正三角形（Ctrl_Instr_Spectrometer のつまみと同じ向き）
    pts = [(R * math.sin(math.radians(a)), -R * math.cos(math.radians(a))) for a in (0, 120, 240)]
    prism_z("Prism", pts, z0 + h - 0.01, 0.16, "orange", bevel=0.03)
    ck.end_asset()
    return root


def build_antenna():
    root = ck.asset("Part_HighGainAntenna")
    ck.cyl("Foot", (0, 0, 0.03), 0.13, 0.06, "gray_dark", bevel=0.02, verts=12)
    ph = 0.45
    ck.cyl("Post", (0, 0, 0.06 + ph / 2 - 0.01), 0.05, ph, "gray", bevel=0.012, verts=12)
    zc = 0.06 + ph - 0.01                       # 腕の高さ（柱の先）
    off = 0.29                                  # 皿の中心（±off, ±off）
    arms = []
    for i, a in enumerate((45, 135)):           # 斜めの十字＝4枚の皿の中心を結ぶ
        arms.append(ck.box(f"Arm{i}", (0, 0, zc), (2 * off * math.sqrt(2), 0.06, 0.05), "gray", bevel=0.012, rot=(0, 0, a)))
    arms.append(ck.sph("Hub", (0, 0, zc), 0.075, "gray", seg=12))
    ck.join(arms, "Arms")
    dishes, feeds = [], []
    dz, hollow = 0.11, 0.075                    # 皿の厚み（ふくらみ）と、くぼみの深さ
    for i, (sx, sy) in enumerate(((-1, -1), (1, -1), (-1, 1), (1, 1))):
        c = (sx * off, sy * off, zc + 0.01)
        # 皿：半球を浅くつぶし、底をすり鉢にくぼませてから裏返す（くぼみが +Z、ふくらみが柱の側）
        d = ck.dome(f"Dish{i}", (0, 0, 0), (0.26, 0.26, dz), "white", seg=16)
        dish_hollow(d, hollow)
        d.data.transform(Matrix.Translation((c[0], c[1], c[2] + dz)) @ Matrix.Rotation(math.pi, 4, "X"))
        dishes.append(d)
        z_bottom = c[2] + dz - hollow           # くぼみの底
        feeds.append(ck.cyl(f"Feed{i}", (c[0], c[1], z_bottom + 0.095), 0.022, 0.20, "orange", bevel=0, verts=6))
        feeds.append(ck.sph(f"FeedTip{i}", (c[0], c[1], z_bottom + 0.20), 0.045, "orange", seg=8))
    ck.join(dishes, "Dishes")
    ck.join(feeds, "Feeds")
    ck.end_asset()
    return root


def dish_hollow(d, depth):
    """ck.dome の平らな底（z=0）の真ん中を depth だけ押し込み、浅いすり鉢にする（裏返すと皿のくぼみになる）。"""
    bm = bmesh.new(); bm.from_mesh(d.data)
    cap = max(bm.faces, key=lambda f: len(f.verts))
    res = bmesh.ops.poke(bm, faces=[cap])
    for v in res["verts"]:
        v.co.z += depth
    for f in res["faces"]:
        f.smooth = False                         # すり鉢の面は角を立てて、くぼみが光と影で見えるように
    bm.to_mesh(d.data); bm.free()
    ck.paint(d, "white")


def load_glb(name):
    """modeling/part/export/<name>.glb を読む（既存の部品を一覧に並べる用）。"""
    path = os.path.join(ck._export.MODELING, KIND, "export", name + ".glb")
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    return bpy.data.objects.get(name) or next(o for o in new if o.parent is None)


def _meshes(root):
    return [o for o in [root] + list(root.children_recursive) if o.type == "MESH"]


def lineup(roots, path, pad=0.35):
    """幅の違う物を外接箱の幅で詰めて横に並べ、ゲームと同じ角度で1枚（build_parts_extra.py の lineup と同じ光・床・カメラ）。
    320px 版も出す。並べた物以外は写さず、終わったら置いた物を片付けて元の位置に戻す。"""
    spans = []
    for r in roots:
        r.location = (0, 0, 0)
    for r in roots:
        lo, hi = ck._export.world_bounds(_meshes(r))
        spans.append((lo.x, hi.x))
    total = sum(h - l for l, h in spans) + pad * (len(roots) - 1)
    x = -total / 2
    for r, (l, h) in zip(roots, spans):
        r.location = (x - l, 0, 0)
        x += (h - l) + pad
    mine = {o for r in roots for o in [r] + list(r.children_recursive)}
    hidden = [(o, o.hide_render) for o in bpy.data.objects if o not in mine]
    for o, _ in hidden:
        o.hide_render = True
    for o in mine:
        o.hide_render = False
    bpy.context.view_layer.update()
    lo, hi = ck._export.world_bounds([m for r in roots for m in _meshes(r)])
    scn = bpy.context.scene
    old_world, old_cam = scn.world, scn.camera
    world = rc._world()
    made, cam, dummy = rc._helpers(lo, hi, False, True)
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 0, -0.001))
    fl = bpy.context.object; fl.scale = (hi.x - lo.x + 3, hi.y - lo.y + 3, 1)
    fl.data.materials.append(rc._mat("__lineup_floor", "#3D4B6E"))
    made.append(fl)
    scn.render.engine = "BLENDER_EEVEE"
    scn.render.resolution_x, scn.render.resolution_y = rc.RES
    scn.render.resolution_percentage = 100
    scn.view_settings.view_transform = "Standard"; scn.view_settings.look = "None"
    rc._aim(cam, rc._corners(lo, hi) + dummy, 0, 50, margin=0.85)
    rc._render(path)
    rc._small(path)
    for o in made:
        bpy.data.objects.remove(o, do_unlink=True)
    scn.world, scn.camera = old_world, old_cam
    if world.users == 0:
        bpy.data.worlds.remove(world)
    for o, h in hidden:
        o.hide_render = h
    for r in roots:
        r.location = (0, 0, 0)
    bpy.context.view_layer.update()
    print("[lineup]", path)


if __name__ == "__main__":
    ck.clear_scene()
    roots = []
    for build in (build_battery, build_camera, build_spectrometer, build_antenna):
        root = build()
        ck.export_all(root, KIND)
        ck.render_check(root, KIND)
        print(f"[build] {root.name}: {ck.tri_count(root)} 三角")
        roots.append(root)
    renders = os.path.join(ck._export.MODELING, KIND, "renders")
    lineup(roots, os.path.join(renders, "parts_design_lineup.png"))
    # 設計画面のタブの順（POWER → AIR & FUEL → SCIENCE）に既存の3つも入れて7つ並べる
    old = {n: load_glb(n) for n in ("Part_SolarPanel", "Part_FuelTank", "Part_RadarSounder")}
    new = {r.name: r for r in roots}
    order = [new["Part_Battery"], old["Part_SolarPanel"], old["Part_FuelTank"], new["Part_Camera"],
             new["Part_Spectrometer"], old["Part_RadarSounder"], new["Part_HighGainAntenna"]]
    lineup(order, os.path.join(renders, "parts_all_lineup.png"))
