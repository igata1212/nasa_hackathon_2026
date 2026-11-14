"""Console_*：席のコンソール台（1種の形を4席分）。操作部品は載せない（取り付け位置 .Slot_1〜6 だけ）。

書いた人：iizuka（作成：Opus 5.5「Opus-3」）／ 最終更新：2026-11-14

  modeling/core/run.sh modeling/console/build_console.py
出力：unity/Assets/Models/Console/Console_<席>.fbx、modeling/console/export/、modeling/console/renders/
      （各席の3枚＋320px 版、一覧 console_lineup.png と console_lineup_320.png）
下敷き：midori の room.py の Station_Pilot（机 0.8m・天板・斜めの画面）の寸法感だけ借りた。メッシュは新規。

形（正面＝操作する人の側が −Y）：
  Body       本体（横から見た形を押し出した物。手前の下に足先の逃げ）。cream
  Panel      手前に 20° 傾いたパネル。後ろの縁が z=0.80、手前の縁が z≈0.70。gray
  Top        パネルの後ろの平らな天板。上面 z=0.80（check.py が見る）。gray_light
  Rim        パネルの左右の縁＋手前の手すり（浮いているクルーがつかまる棒）。席の色
  Board      後ろに立つ席アイコンの板（後ろへ 12° 倒す）。席の色。クルーが前にいても頭の上から見える
  Screen_L/R 板の左右の画面（文字は入れない。Unity で絵を貼れる）。teal
  Icon       席アイコン（▲●■◆）。navy（4色どれの上でも読める濃さ）
  Slot_1〜6  操作部品の取り付け位置。左（−X）から 0.2m 間隔。原点＝台座の底の中心、ローカル +Z＝パネルの法線
"""
import sys, os, math
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "core"))
import bpy, bmesh
from mathutils import Matrix, Vector
import corekit as ck
import render_check as rc

KIND = "console"

# 席：名前、席の色、アイコン（色だけに頼らないための形。クルーと同じ）
SEATS = [
    ("Console_Pilot", "seat_red", "triangle"),           # ▲
    ("Console_LifeSupport", "seat_green", "circle"),     # ●
    ("Console_CommsPower", "seat_yellow", "square"),     # ■
    ("Console_MissionPlanner", "seat_blue", "diamond"),  # ◆
]

# 寸法（m）
W = 1.28              # 全幅（取り付け位置 6×0.2m ＋ 左右の縁 0.04）
SLOT_W = 0.2          # 取り付け位置の間隔＝操作部品の台座
N_SLOT = 6
TOP_Z = 0.80          # 天板とパネルの後ろの縁の高さ
TILT = math.radians(20)   # パネルの傾き（手前が低い）
L = 0.30              # パネルの斜面の長さ（台座 0.2m ＋ 前後 0.05m）
Y_BACK = 0.32         # 本体の背面
Y_B = -0.008          # パネルの後ろの縁（ここから天板）
PLATE_T = 0.03        # パネルの板の厚み
RIM_IN = W / 2 - 0.03     # 縁の内側（パネルの板の幅は 2×これ）
BOARD_Y, BOARD_H, BOARD_W, BOARD_T = 0.20, 0.32, 1.10, 0.04
BOARD_TILT = math.radians(12)
ICON_ZC = 0.16        # 板の上での席アイコンの中心の高さ

U = Vector((-math.cos(TILT), -math.sin(TILT)))   # 斜面の向き（後ろ→手前、(y, z)）
N = Vector((-math.sin(TILT), math.cos(TILT)))    # パネルの法線（(y, z)）


def P(s, h=0.0):
    """パネル上の点 (y, z)。s：後ろの縁からの斜面の距離、h：パネル上面からの高さ（法線向き）。"""
    return Vector((Y_B, TOP_Z)) + U * s + N * h


def prism(name, pts, depth, color, mat=None, bevel=None):
    """2D の多角形 pts（ローカル XY）をローカル +Z へ depth だけ押し出し、mat で置く。"""
    me = bpy.data.meshes.new("__prism__")
    bm = bmesh.new()
    bot = [bm.verts.new((x, y, 0)) for x, y in pts]
    top = [bm.verts.new((x, y, depth)) for x, y in pts]
    bm.faces.new(bot)
    bm.faces.new(top)
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((bot[i], bot[j], top[j], top[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if mat is not None:
        bmesh.ops.transform(bm, matrix=mat, verts=bm.verts[:])
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new("__prism__", me)
    bpy.context.scene.collection.objects.link(o)
    xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
    thin = min(depth, max(xs) - min(xs), max(ys) - min(ys))
    return ck._fin(o, color, name, bevel=ck._bevel(bevel, thin))


def box_m(name, center, size, color, mat, bevel=None):
    """mat の座標系の中に置く直方体（center・size はその座標系で）。"""
    o = ck.box(name, (0, 0, 0), size, color, bevel=bevel)
    o.data.transform(mat @ Matrix.Translation(center))
    return o


def icon_pts(kind):
    """席アイコンの形（中心が原点、上が +Y）。面積がだいたいそろう大きさ。"""
    if kind == "triangle":
        r = 0.125
        return [(r * math.cos(math.radians(a)), r * math.sin(math.radians(a)) - 0.018) for a in (90, 210, 330)]
    if kind == "circle":
        r = 0.098
        return [(r * math.cos(2 * math.pi * i / 16), r * math.sin(2 * math.pi * i / 16)) for i in range(16)]
    if kind == "square":
        h = 0.086
        return [(-h, -h), (h, -h), (h, h), (-h, h)]
    if kind == "diamond":
        r = 0.12
        return [(0, -r), (r, 0), (0, r), (-r, 0)]
    raise ValueError(kind)


def build(name, color, icon):
    root = ck.asset(name)

    # 本体：横から見た形（y, z）を X 方向に押し出す。手前の下に足先の逃げ（奥行き 0.05、高さ 0.10）
    f_hi = P(L, -0.02)                  # 斜面の手前の端（パネルの板の中に少しもぐらせる）
    prof = [(Y_BACK, 0.0), (Y_BACK, TOP_Z - 0.04), (Y_B + 0.02, TOP_Z - 0.04), (f_hi.x, f_hi.y),
            (f_hi.x, 0.10), (f_hi.x + 0.05, 0.10), (f_hi.x + 0.05, 0.0)]
    to_x = Matrix(((0, 0, 1, 0), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))   # (a, b, c) → (x=c, y=a, z=b)
    prism("Body", prof, W, "cream", Matrix.Translation((-W / 2, 0, 0)) @ to_x, bevel=0.03)

    # 傾いたパネル（上面が P(s, 0)）と、後ろの平らな天板（上面 0.80）
    rot = (math.degrees(TILT), 0, 0)
    c = P(L / 2, -PLATE_T / 2)
    ck.box("Panel", (0, c.x, c.y), (2 * RIM_IN, L, PLATE_T), "gray", bevel=0.008, rot=rot)
    ck.box("Top", (0, (Y_B + Y_BACK) / 2, TOP_Z - 0.02), (W, Y_BACK - Y_B, 0.04), "gray_light", bevel=0.012)

    # 縁（席の色）：左右のレール＋手前の手すり。レールの外の面は本体の横の面より 3mm 外（同じ面に重ねるとちらつく）
    rails = []
    for sx in (-1, 1):
        c = P(L / 2 + 0.01, -0.01)
        rails.append(ck.box(f"RimSide{'L' if sx < 0 else 'R'}", (sx * (W / 2 - 0.012), c.x, c.y),
                            (0.03, L + 0.02, 0.06), color, bevel=0.007, rot=rot))
    h = P(L, -0.004)
    rails.append(ck.cyl("RimRail", (0, h.x - 0.012, h.y), 0.028, W, color, rot=(0, 90, 0), bevel=0.012))
    ck.join(rails, "Rim")

    # 後ろの板：前の面の下の縁が (y=BOARD_Y, z=0.80)。ローカル x 右・z 上・−y が見る人の側
    mb = Matrix.Translation((0, BOARD_Y, TOP_Z)) @ Matrix.Rotation(-BOARD_TILT, 4, "X")
    box_m("Board", (0, BOARD_T / 2, BOARD_H / 2), (BOARD_W, BOARD_T, BOARD_H), color, mb, bevel=0.015)
    face = Matrix.Rotation(math.radians(90), 4, "X")          # 押し出し（ローカル +Z）を板の手前（−y）へ
    for side, sx in (("L", -1), ("R", 1)):
        sc = [(-0.15, -0.09), (0.15, -0.09), (0.15, 0.09), (-0.15, 0.09)]
        prism(f"Screen_{side}", sc, 0.02, "teal", mb @ Matrix.Translation((sx * 0.36, 0, ICON_ZC)) @ face, bevel=0.005)
    prism("Icon", icon_pts(icon), 0.02, "navy", mb @ Matrix.Translation((0, 0, ICON_ZC)) @ face, bevel=0.005)

    # 取り付け位置：パネルの斜面の中央に 0.2m 間隔で6つ。ローカル +Z＝パネルの法線（X まわりに +20°）
    s = P(L / 2, 0)
    for i in range(N_SLOT):
        x = (i - (N_SLOT - 1) / 2) * SLOT_W
        sl = ck.slot(f"Slot_{i + 1}", (x, s.x, s.y))
        sl.rotation_euler = (TILT, 0, 0)
    ck.end_asset()
    return root


def lineup(roots, path, gap):
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
    rc._aim(cam, rc._corners(lo, hi) + dummy, 0, 50, margin=0.72)   # 横長なので外接球より詰める
    rc._render(path)
    rc._small(path)
    print("[lineup]", path)


if __name__ == "__main__":
    ck.clear_scene()
    roots = []
    for name, color, icon in SEATS:
        for o in list(bpy.data.objects):          # 前のアセットはもう書き出したので隠すだけ
            o.hide_render = True
        root = build(name, color, icon)
        ck.export_all(root, KIND)
        ck.render_check(root, KIND)
        print(f"[build] {name}: {ck.tri_count(root)} 三角")
        roots.append(root)
    for o in bpy.data.objects:
        o.hide_render = False
    lineup(roots, os.path.join(ck._export.MODELING, KIND, "renders", "console_lineup.png"), gap=1.55)
