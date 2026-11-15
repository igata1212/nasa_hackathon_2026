"""Fx_SeatMonitor_*：席ごとの計器盤（#167）。コンソールの後ろに立つ、針のメーター5つの盤。4席分。

書いた人：Taiga（作成：Fable 5.2）／ 最終更新：2026-11-15 ／ 更新：Taiga（Opus 5.5、2026-11-15、画面からメーターに替えた）

  modeling/core/run.sh modeling/fx/build_seat_monitor.py
出力：unity/Assets/Models/Fx/Fx_SeatMonitor_<席>.fbx、modeling/fx/export/、modeling/fx/renders/、modeling/fx/parts.json
      （Pilot の3枚＋320px 版、4席の一覧 seat_monitor_lineup.png。船室に置いた図は render_seat_monitor.py）

ねらい（#167）：酸素・電力などの数値を、画面の上の表示（HUD）ではなく、船内の計器で読む。本物の宇宙船のように、針のメーターで読む。
  席ごとに 0.80×0.50m の計器盤をクルーの目の高さに立てる。メーターは5つ（電力・酸素・温度・CO2・燃料）、直径 0.21m。
  右下の小さい画面にヒューストン（先生）の顔、左下にスピーカー。

置き方（Unity）：Ship_Interior の .Slot_Console_<席> の子に、位置0・回転0 で置く（コンソールと同じ Slot）。
  原点＝コンソールの原点（床の上）。正面＝操作する人の側が −Y。盤はまっすぐ立てる（針の回転軸を盤の法線にそろえるため）。
  まん中の2席（Life Support・Comms & Power）は、警報ランプの箱（JBox）をよけて、盤を外側へ 0.26m ずらしてある。

部品：
  Frame        枠（gray_light）＋計器の面（navy）＋スピーカーの溝＋ヒューストンの画面の縁
  Dials        メーター5つの縁・白い面・危ない側のオレンジの帯（1つのメッシュ。動かない）。
               帯は CO2 だけ右（多いほど悪い）、ほかは左（少ないほど悪い）。帯の幅は目安で、閾値そのものではない
  Move_Needle_Power / _Oxygen / _Temp / _Co2 / _Fuel   針。原点＝メーターのまん中。ローカル Y まわりに −120〜＋120°（parts.json）。
               0 が真上、＋が見る人から見て時計回り。値 0 で −120°（左下）、満タンで ＋120°（右下）。Unity では localEulerAngles.z
  MonScreen    ヒューストンの小さい画面（teal、0.24×0.15m）。Unity の ScreenGlow が光らせる
  Hood         上のひさし（席の色）
  Lamp_Green / Lamp_Yellow / Lamp_Red   下の帯の状態ランプ
  Stand        床に立つ柱＋足＋腕＋席の色の輪
  Slot_Dial_<名前>  各メーターのまん中（白い面のすぐ手前、針の奥）。数字とアイコンの Canvas を載せる
  Slot_Screen  ヒューストンの画面のまん中。管制室の映像の Canvas を載せる（幅 0.24m・高さ 0.15m）
  Slot_Panel   計器の面のまん中（カメラが見る点）
  Slot はどれも、Unity でローカル +X＝見る人から見て右、+Y＝上、+Z＝面の奥。world-space の Canvas を子に位置0・回転0 で置けば表が見る人を向く
"""
import sys, os, math
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "core"))
import bpy, bmesh
from mathutils import Matrix
import corekit as ck
import render_check as rc

KIND = "fx"

# 席：名前、席の色、盤を横にずらす量（m。コンソールのローカル X）
JBOX_SHIFT = 0.26     # まん中の2席：警報ランプの箱（ローカル x 0.25〜0.63）をよける
SEATS = [
    ("Fx_SeatMonitor_Pilot", "seat_red", 0.0),
    ("Fx_SeatMonitor_LifeSupport", "seat_green", -JBOX_SHIFT),     # 箱は +X 側（部屋のまん中寄り）
    ("Fx_SeatMonitor_CommsPower", "seat_yellow", JBOX_SHIFT),      # 箱は −X 側
    ("Fx_SeatMonitor_MissionPlanner", "seat_blue", 0.0),
]

# 寸法（m）。高さは床から
PANEL_W, PANEL_H = 0.80, 0.50        # 計器の面（枠の内側）
FRAME_W, FRAME_H, FRAME_D = 0.90, 0.68, 0.09
PANEL_UP = 0.05                      # 枠のまん中から計器の面のまん中まで（下に帯の場所を空ける）
CHIN_Z = -0.275                      # 下の帯のまん中（枠のまん中から）
CENTER_Y, CENTER_Z = 0.28, 1.50      # 枠のまん中。下の端 1.16 はコンソールの板（1.11）の上
POLE_Y, POLE_T = 0.40, 0.08          # 柱（コンソールの背面 0.32 と壁の間）
POLE_TOP = 1.34
LAMP_R, LAMP_D = 0.032, 0.03

# メーター：(名前, 面の上の x, z（計器の面のまん中から）, 危ない側)
DIAL_R = 0.105
SWEEP = 120.0
DIALS = [
    ("Power", -0.27, 0.115, "low"), ("Oxygen", 0.0, 0.115, "low"), ("Temp", 0.27, 0.115, "low"),
    ("Co2", -0.27, -0.135, "high"), ("Fuel", 0.0, -0.135, "low"),
]
SCREEN_X, SCREEN_Z, SCREEN_W, SCREEN_H = 0.27, -0.135, 0.24, 0.15   # ヒューストンの小さい画面（右下）



def box_m(name, center, size, color, mat, bevel=None):
    """mat の座標系の中に置く直方体（center・size はその座標系で）。"""
    o = ck.box(name, (0, 0, 0), size, color, bevel=bevel)
    o.data.transform(mat @ Matrix.Translation(center))
    return o


def disc_m(name, center, r, depth, color, mat, verts=12):
    """軸が盤の法線（ローカル Y）を向く円柱。"""
    o = ck.cyl(name, (0, 0, 0), r, depth, color, rot=(90, 0, 0), verts=verts, bevel=0)
    o.data.transform(mat @ Matrix.Translation(center))
    return o


def arc_m(name, center, r0, r1, a0, a1, depth, color, mat, seg=4):
    """メーターの色の帯（角度 a0〜a1 度、0 が真上、＋が見る人から見て時計回り）。盤の面に沿う板で、厚み depth は −Y（手前）へ。"""
    pts = []
    for k in range(seg + 1):
        a = math.radians(a0 + (a1 - a0) * k / seg)
        pts.append((r1 * math.sin(a), r1 * math.cos(a)))
    for k in range(seg, -1, -1):
        a = math.radians(a0 + (a1 - a0) * k / seg)
        pts.append((r0 * math.sin(a), r0 * math.cos(a)))
    me = bpy.data.meshes.new("__arc__")
    bm = bmesh.new()
    cx, cy, cz = center
    bot = [bm.verts.new((cx + x, cy, cz + z)) for x, z in pts]
    top = [bm.verts.new((cx + x, cy - depth, cz + z)) for x, z in pts]
    bm.faces.new(bot); bm.faces.new(top)
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((bot[i], bot[j], top[j], top[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bmesh.ops.transform(bm, matrix=mat, verts=bm.verts[:])
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new("__arc__", me)
    bpy.context.scene.collection.objects.link(o)
    return ck._fin(o, color, name, bevel=0)


def unity_slot(name, pos):
    """取り付け位置。Unity で +X＝見る人から見て右、+Y＝上、+Z＝盤の奥になる向き（Blender では Z まわり 180°）。"""
    sl = ck.slot(name, tuple(pos))
    sl.rotation_euler = Matrix.Rotation(math.pi, 4, "Z").to_euler()
    return sl


def build(name, color, shift):
    root = ck.asset(name)
    # 盤の座標系：x 右（見る人から見て）、z 上、−y が見る人の側
    M = Matrix.Translation((shift, CENTER_Y, CENTER_Z))
    front = -FRAME_D / 2
    X = lambda x: x

    # 枠：本体＋計器の面（濃紺）＋スピーカーの溝＋ヒューストンの画面の縁
    parts = [box_m("FrameBody", (0, 0, 0), (FRAME_W, FRAME_D, FRAME_H), "gray_light", M, bevel=0.03)]
    parts.append(box_m("Face", (0, front - 0.004, PANEL_UP), (PANEL_W + 0.05, 0.02, PANEL_H + 0.05), "navy", M, bevel=0))
    for i in range(2):                                   # スピーカー（ヒューストンの声が出る所）。左下
        parts.append(box_m(f"Grille{i}", (X(-0.29), front - 0.004, CHIN_Z - 0.018 + i * 0.036),
                           (0.22, 0.02, 0.014), "gray_dark", M, bevel=0))
    parts.append(box_m("ScreenRim", (X(SCREEN_X), front - 0.014, PANEL_UP + SCREEN_Z),
                       (SCREEN_W + 0.03, 0.02, SCREEN_H + 0.03), "gray_light", M, bevel=0))
    ck.join(parts, "Frame")

    # ヒューストンの小さい画面（独立した部品。Unity の ScreenGlow が光らせる）
    box_m("MonScreen", (X(SCREEN_X), front - 0.026, PANEL_UP + SCREEN_Z), (SCREEN_W, 0.02, SCREEN_H), "teal", M, bevel=0)
    unity_slot("Slot_Screen", M @ Matrix.Translation((X(SCREEN_X), front - 0.037, PANEL_UP + SCREEN_Z)).translation)
    unity_slot("Slot_Panel", M @ Matrix.Translation((0, front - 0.016, PANEL_UP)).translation)

    # メーター5つ：縁・白い面・危ない側の帯を1つのメッシュに。針は1本ずつ別の部品
    y_face = front - 0.014                                # 計器の面の手前の端
    dial_parts, moving = [], []
    for key, dx, dz, bad in DIALS:
        cx, cz = X(dx), PANEL_UP + dz
        dial_parts.append(disc_m(f"Bezel{key}", (cx, y_face - 0.010, cz), DIAL_R, 0.020, "gray_light", M))
        dial_parts.append(disc_m(f"Dial{key}", (cx, y_face - 0.014, cz), DIAL_R - 0.014, 0.020, "white", M))
        a0, a1 = (-SWEEP, -SWEEP + 70) if bad == "low" else (SWEEP - 70, SWEEP)
        dial_parts.append(arc_m(f"Zone{key}", (cx, y_face - 0.024, cz), DIAL_R - 0.040, DIAL_R - 0.018, a0, a1, 0.004, "orange", M))
        # 針：まん中の軸＋棒。真上（0°）を向けて作り、原点を軸に置く
        hub = disc_m(f"Hub{key}", (cx, y_face - 0.036, cz), 0.016, 0.022, "navy", M, verts=8)
        bar = box_m(f"Bar{key}", (cx, y_face - 0.036, cz + 0.034), (0.012, 0.010, 0.085), "navy", M, bevel=0)
        needle = ck.join([hub, bar], f"Move_Needle_{key}")
        ck.set_origin(needle, tuple(M @ Matrix.Translation((cx, y_face - 0.036, cz)).translation))
        moving.append({"name": f"Move_Needle_{key}", "type": "rotate", "axis": "Y", "min": -SWEEP, "max": SWEEP, "default": 0,
                       "note": "0 が真上。値 0 で −120°（左下）、満タンで ＋120°（右下）。Unity では localEulerAngles.z"})
        unity_slot(f"Slot_Dial_{key}", M @ Matrix.Translation((cx, y_face - 0.0245, cz)).translation)
    ck.join(dial_parts, "Dials")

    # ひさし（席の色）
    box_m("Hood", (0, front - 0.02, FRAME_H / 2 + 0.012), (FRAME_W + 0.06, FRAME_D + 0.16, 0.045), color, M, bevel=0)

    # 状態ランプ（下の帯の右）
    for i, (lamp, col) in enumerate((("Lamp_Green", "lamp_green"), ("Lamp_Yellow", "lamp_yellow"), ("Lamp_Red", "lamp_red"))):
        o = ck.cyl(lamp, (0, 0, 0), LAMP_R, LAMP_D, col, rot=(90, 0, 0), verts=12, bevel=0)
        o.data.transform(M @ Matrix.Translation((X(0.17 + i * 0.095), front - 0.006, CHIN_Z)))

    # 柱＋足＋腕＋席の色の輪
    stand = [ck.box("Pole", (shift, POLE_Y, POLE_TOP / 2), (POLE_T, POLE_T, POLE_TOP), "gray", bevel=0),
             ck.box("Foot", (shift, POLE_Y, 0.025), (0.26, 0.14, 0.05), "gray_dark", bevel=0),
             ck.box("Arm", (shift, (CENTER_Y + POLE_Y) / 2 + 0.02, POLE_TOP - 0.07), (0.14, POLE_Y - CENTER_Y + 0.06, 0.12), "gray", bevel=0),
             ck.box("Collar", (shift, POLE_Y, 1.02), (POLE_T + 0.03, POLE_T + 0.03, 0.06), color, bevel=0)]
    ck.join(stand, "Stand")
    ck.end_asset()
    ck.write_parts(KIND, root, moving)
    return root, moving


def lineup(roots, path, gap):
    """4つを横に並べて、正面の少し上から1枚（盤が読める角度）。320px 版も出す。"""
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
    rc._aim(cam, rc._corners(lo, hi) + dummy, 0, 20, margin=0.62)
    rc._render(path)
    rc._small(path)
    print("[lineup]", path)


if __name__ == "__main__":
    ck.clear_scene()
    roots = []
    for name, color, shift in SEATS:
        for o in list(bpy.data.objects):          # 前のアセットはもう書き出したので隠すだけ
            o.hide_render = True
        root, moving = build(name, color, shift)
        ck.export_all(root, KIND)
        if not roots:                             # 単体の3枚は1席分だけ（4席は色とずらす量が違うだけ。容量の上限 30MB のため。4席は一覧で見る）
            ck.render_check(root, KIND, moving)
        print(f"[build] {name}: {ck.tri_count(root)} 三角")
        roots.append(root)
    for o in bpy.data.objects:
        o.hide_render = False
    lineup(roots, os.path.join(ck._export.MODELING, KIND, "renders", "seat_monitor_lineup.png"), gap=1.4)
