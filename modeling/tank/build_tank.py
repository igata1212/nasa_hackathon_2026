"""Tank_*：酸素タンク。通常2つ（Tank_Oxygen1・Tank_Oxygen2）と、漏れる Tank 2 の破れ版（Tank_Oxygen2_Broken）。

書いた人：iizuka（作成：Opus 5.5「Opus-3」）／ 最終更新：2026-11-14
更新：Taiga（2026-11-14、Fable 5.2）#46 build() に spare を足した（予備のタンクは build_tank_spare.py が作る）

  modeling/core/run.sh modeling/tank/build_tank.py
出力：unity/Assets/Models/Tank/Tank_*.fbx、modeling/tank/export/、modeling/tank/renders/（各3枚＋320px 版、
      一覧 tank_lineup.png と tank_lineup_320.png）
参考：アポロ機械船の酸素タンクは球形（中身は液体と気体の間の状態の酸素）。形だけ借りて、大きさはゲームに合わせて直径 0.5m。
      メッシュは新規（kit の球・円柱・箱）。

形（正面は −Y）：
  Plinth     台座の板。gray_dark
  Cradle     球を受ける皿。gray
  Shell      球（直径 0.5m）。white。破れ版は穴が開いている
  Band       赤道の帯。teal
  Neck・Pad  てっぺんの首と、バルブを載せる台。gray / gray_light
  Label      番号の札（球の左前、さいころの目の板）。teal（帯と同じ色。暗い色だと破れ版で穴と並んで「両目」に見えるので避けた）
  Dot_1〜2   番号＝点の数（文字は使わない）。さいころと同じ並び（1つは中央、2つは斜め）。white
             横に2つ並べると「目」に見えて顔になるので、斜めにして札に載せた
  Slot_Valve バルブ（操作部品）の取り付け位置。原点＝台座の底の中心、ローカル +Z が上
破れ版だけ：
  Inside     穴から見える暗い内側（少し小さい球）。visor
  Petals     外へめくれた板（6枚を1つのメッシュに）。gray_light
  Slot_Leak  ガスの噴き出し口（Unity の粒子をここから出す）。ローカル +Z＝噴き出す向き
"""
import sys, os, math, random
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "core"))
import bpy, bmesh
from mathutils import Matrix, Vector
import corekit as ck
import render_check as rc

KIND = "tank"
TANKS = [("Tank_Oxygen1", 1, False), ("Tank_Oxygen2", 2, False), ("Tank_Oxygen2_Broken", 2, True)]

R = 0.25                  # 球の半径（直径 0.5m）
C = Vector((0, 0, 0.325))  # 球の中心
TOP = C.z + 0.32           # バルブ台の上面（取り付け位置の高さ）
LABEL_AZ, LABEL_EL = -42, 33   # 番号の札の向き（正面から左へ、上へ、度）。帯にかからない高さ
LABEL_W = 0.17                 # 札の大きさ（正方形）
PIP = 0.04                    # 2つのときの目の位置（札の中心から斜めに）
HOLE_AZ, HOLE_EL = 32, 35      # 穴の向き（正面から右（+X）へ、上へ、度）。ゲームのカメラから正面に見える
HOLE_A = math.radians(22)      # 穴の大きさ（中心から縁までの角度）


def direction(az, el):
    """正面（−Y）から右（+X）へ az 度、上へ el 度の向き。"""
    a, e = math.radians(az), math.radians(el)
    return Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))


def frame(d):
    """ローカル +Z を d に、ローカル +Y をなるべく上（+Z）に向ける回転（3×3）。札が傾かないように。"""
    x = Vector((0, 0, 1)).cross(d).normalized()
    return Matrix((x, d.cross(x), d)).transposed()


def euler_to(d):
    """ローカル +Z を d に向ける回転（度）。"""
    return tuple(math.degrees(v) for v in d.to_track_quat("Z", "Y").to_euler())


def plate3d(name, pts, normal, thick, color, bevel=0.004):
    """3D の多角形 pts を -normal の向きに thick だけ厚くした板。"""
    me = bpy.data.meshes.new("__plate__")
    bm = bmesh.new()
    a = [bm.verts.new(p) for p in pts]
    b = [bm.verts.new(p - normal * thick) for p in pts]
    bm.faces.new(a); bm.faces.new(b)
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new("__plate__", me)
    bpy.context.scene.collection.objects.link(o)
    return ck._fin(o, color, name, bevel=min(bevel, thick * 0.25) if bevel else 0)


def holed_sphere(name, d, alpha, color):
    """球の d の向きに、中心から alpha の角度の丸い穴を開ける（ふたはしない）。"""
    o = ck.sph(name, tuple(C), R, color)
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=d * (R * math.cos(alpha)),
                           plane_no=d, clear_outer=True)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)
    bmesh.ops.dissolve_degenerate(bm, edges=bm.edges[:], dist=1e-6)
    bm.to_mesh(o.data); bm.free()
    for p in o.data.polygons:
        p.use_smooth = True
    return ck.paint(o, color)


def petals(d, alpha):
    """穴の縁から外へめくれた板（6枚）。縁を蝶番にして、穴の中心側から外へ 100〜140° 起こした形。"""
    e1 = d.cross(Vector((0, 0, 1))).normalized()
    e2 = d.cross(e1).normalized()
    rnd = random.Random(34)
    out = []
    n = 6
    for k in range(n):
        phi = 2 * math.pi * k / n + rnd.uniform(-0.25, 0.25)
        w = math.radians(rnd.uniform(26, 32))
        ln = R * math.sin(alpha) * rnd.uniform(0.6, 0.85)
        beta = math.radians(rnd.uniform(95, 125))

        def rim(p):
            radial = math.cos(p) * e1 + math.sin(p) * e2
            return C + R * (math.cos(alpha) * d + math.sin(alpha) * radial)
        radial = math.cos(phi) * e1 + math.sin(phi) * e2
        t_out = -math.sin(alpha) * d + math.cos(alpha) * radial          # 縁で、穴の外へ向かう面の向き
        n_rim = (rim(phi) - C).normalized()
        tipdir = math.cos(beta) * (-t_out) + math.sin(beta) * n_rim
        b1, b2 = rim(phi - w), rim(phi + w)
        tip = rim(phi) + tipdir * ln + (b2 - b1) * rnd.uniform(-0.15, 0.15)
        nrm = (b2 - b1).cross(tip - b1).normalized()
        if nrm.dot(tipdir.cross(b2 - b1)) < 0:
            nrm = -nrm
        if nrm.dot(n_rim) < 0:                   # 外側の面を表に
            nrm = -nrm
        out.append(plate3d(f"Petal{k}", [b1 - n_rim * 0.004, b2 - n_rim * 0.004, tip], nrm, 0.02, "gray_light",
                           bevel=0))                       # 小さい板なのでベベルなし（三角数を抑える）
    return ck.join(out, "Petals")


def build(name, number, broken, spare=False):
    """spare=True は予備のタンク（#46）：帯と札が orange、帯は上下2本。メインは teal の帯1本。"""
    mark = "orange" if spare else "teal"
    root = ck.asset(name)
    ck.box("Plinth", (0, 0, 0.04), (0.46, 0.46, 0.08), "gray_dark", bevel=0.02)
    ck.cyl("Cradle", (0, 0, 0.12), 0.15, 0.08, "gray", r2=0.19, bevel=0.012)
    hole = direction(HOLE_AZ, HOLE_EL)
    if broken:
        holed_sphere("Shell", hole, HOLE_A, "white")
        ck.sph("Inside", tuple(C), R - 0.016, "visor")
        petals(hole, HOLE_A)
    else:
        ck.sph("Shell", tuple(C), R, "white")
    if spare:
        # 赤道をはさんで上下に2本（色が見分けにくくても、本数でメインと区別できる）。球の丸みに合わせて半径を少し縮める
        bz = 0.06
        br = math.sqrt(R * R - bz * bz) + 0.012
        ck.join([ck.cyl("BandA", (0, 0, C.z + bz), br, 0.045, mark, bevel=0.01),
                 ck.cyl("BandB", (0, 0, C.z - bz), br, 0.045, mark, bevel=0.01)], "Band")
    else:
        ck.cyl("Band", tuple(C), R + 0.012, 0.06, mark, bevel=0.012)
    ck.cyl("Neck", (0, 0, C.z + R - 0.01), 0.08, 0.09, "gray", bevel=0.012)
    ck.box("Pad", (0, 0, TOP - 0.02), (0.22, 0.22, 0.04), "gray_light", bevel=0.01)
    # 番号の札：球に接する平らな板（角が球にもぐる深さにする）と、さいころの目
    dl = direction(LABEL_AZ, LABEL_EL)
    rq = frame(dl)
    tangent = C + dl * R
    lab = ck.box("Label", (0, 0, 0), (LABEL_W, LABEL_W, 0.05), mark, bevel=0.012)       # 表は球の面から 13mm 浮く
    lab.data.transform(Matrix.Translation(tangent) @ rq.to_4x4() @ Matrix.Translation((0, 0, -0.012)))
    pips = [(0, 0)] if number == 1 else [(-PIP, -PIP), (PIP, PIP)]
    for i, (px, py) in enumerate(pips):
        # 低い円錐台（ベベルの代わりに斜めの側面で光を受ける。三角数を抑える）
        ck.cyl(f"Dot_{i + 1}", tuple(tangent + rq @ Vector((px, py, 0.019))), 0.037, 0.02, "white",
               rot=tuple(math.degrees(v) for v in rq.to_euler()), bevel=0, r2=0.029)
    ck.slot("Slot_Valve", (0, 0, TOP))
    if broken:
        sl = ck.slot("Slot_Leak", tuple(C + hole * R * math.cos(HOLE_A)))
        sl.rotation_euler = hole.to_track_quat("Z", "Y").to_euler()
    ck.end_asset()
    return root


def lineup(roots, path, gap, margin):
    """全部を横に並べて、ゲームと同じ角度（画角 30°、見下ろし 50°、正面から）で1枚。320px 版も出す。"""
    n = len(roots)
    for i, r in enumerate(roots):
        r.location = ((i - (n - 1) / 2) * gap, 0, 0)
    bpy.context.view_layer.update()
    ms = [m for r in roots for m in ck.meshes(r)]
    lo, hi = ck._export.world_bounds(ms)
    rc._world()
    made, cam, dummy = rc._helpers(lo, hi, False, True)
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 0, -0.001))
    fl = bpy.context.object; fl.scale = (hi.x - lo.x + 3, hi.y - lo.y + 3, 1)
    fl.data.materials.append(rc._mat("__lineup_floor", "#3D4B6E"))
    scn = bpy.context.scene
    scn.render.engine = "BLENDER_EEVEE"
    scn.render.resolution_x, scn.render.resolution_y = rc.RES
    scn.render.resolution_percentage = 100
    scn.render.image_settings.file_format = "PNG"
    scn.view_settings.view_transform = "Standard"; scn.view_settings.look = "None"
    rc._aim(cam, rc._corners(lo, hi) + dummy, 0, 50, margin=margin)
    rc._render(path)
    rc._small(path)
    print("[lineup]", path)


if __name__ == "__main__":
    ck.clear_scene()
    roots = []
    for name, number, broken in TANKS:
        root = build(name, number, broken)
        ck.export_all(root, KIND)
        ck.render_check(root, KIND)
        print(f"[build] {name}: {ck.tri_count(root)} 三角")
        roots.append(root)
    lineup(roots, os.path.join(ck._export.MODELING, KIND, "renders", "tank_lineup.png"), gap=0.85, margin=0.85)
