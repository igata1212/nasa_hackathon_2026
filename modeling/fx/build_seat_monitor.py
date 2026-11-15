"""Fx_SeatMonitor_*：席ごとの計器モニター（#167）。コンソールの後ろに立つ大きい画面。4席分。

書いた人：Taiga（作成：Fable 5.2）／ 最終更新：2026-11-15

  modeling/core/run.sh modeling/fx/build_seat_monitor.py
出力：unity/Assets/Models/Fx/Fx_SeatMonitor_<席>.fbx、modeling/fx/export/、modeling/fx/renders/
      （各席の3枚＋320px 版、一覧 seat_monitor_lineup.png）

ねらい（#167）：酸素・電力などの数値を、画面の上の表示（HUD）ではなく、船内の計器で読む。
  今の壁の画面（0.54×0.30m）とコンソールの板の画面（0.30×0.18m）は、数字を読ませるには小さい。
  そこで、席ごとに 0.80×0.50m（16:10）の画面を、クルーの頭より上に立てる。

置き方（Unity）：Ship_Interior の .Slot_Console_<席> の子に、位置0・回転0 で置く（コンソールと同じ Slot）。
  原点＝コンソールの原点（床の上）。正面＝操作する人の側が −Y。
  まん中の2席（Life Support・Comms & Power）は、警報ランプの箱（JBox）をよけて、画面を外側へ 0.26m ずらしてある。

部品：
  Frame        画面の枠（gray_light）＋内側の縁（navy）＋下の帯のスピーカーの溝（gray_dark）
  Hood         上のひさし（席の色）。見下ろしのカメラからも、どの席の画面か分かる
  MonScreen    画面（teal、0.80×0.50m）。独立した部品。名前が MonScreen で始まるので Unity の ScreenGlow が光らせる
  Lamp_Green / Lamp_Yellow / Lamp_Red   下の帯の状態ランプ（安全・注意・危険）。Unity の LampGlow で光らせる
  Stand        床に立つ柱＋足＋画面を支える腕（gray・gray_dark）＋席の色の輪
  Slot_Screen  画面のまん中（画面の表の面の上）。ローカル +Z＝画面の法線（見る人の側）、+X＝右、+Y＝上。
               world-space の Canvas をここに置く（幅 0.80m・高さ 0.50m）
"""
import sys, os, math
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "core"))
import bpy
from mathutils import Matrix
import corekit as ck
import render_check as rc

KIND = "fx"

# 席：名前、席の色、画面を横にずらす量（m。コンソールのローカル X）
JBOX_SHIFT = 0.26     # まん中の2席：警報ランプの箱（ローカル x 0.25〜0.63）をよける
SEATS = [
    ("Fx_SeatMonitor_Pilot", "seat_red", 0.0),
    ("Fx_SeatMonitor_LifeSupport", "seat_green", -JBOX_SHIFT),     # 箱は +X 側（部屋のまん中寄り）
    ("Fx_SeatMonitor_CommsPower", "seat_yellow", JBOX_SHIFT),      # 箱は −X 側
    ("Fx_SeatMonitor_MissionPlanner", "seat_blue", 0.0),
]

# 寸法（m）。高さは床から
SCREEN_W, SCREEN_H = 0.80, 0.50      # 画面（16:10）
FRAME_W, FRAME_H, FRAME_D = 0.90, 0.68, 0.09
SCREEN_UP = 0.05                     # 枠のまん中から画面のまん中まで（下に帯の場所を空ける）
CHIN_Z = -0.275                      # 下の帯のまん中（枠のまん中から）
CENTER_Y, CENTER_Z = 0.28, 1.50      # 枠のまん中。下の端 1.16 はコンソールの板（1.11）の上、画面は 1.30〜1.80（クルーの頭 1.48 より上が半分以上）
TILT = math.radians(8)               # 後ろへ倒す（上が奥）。肩ごしのカメラと見下ろしのカメラの両方に向く
POLE_Y, POLE_T = 0.40, 0.08          # 柱（コンソールの背面 0.32 と壁の間）
POLE_TOP = 1.34
LAMP_R, LAMP_D = 0.032, 0.03


def box_m(name, center, size, color, mat, bevel=None):
    """mat の座標系の中に置く直方体（center・size はその座標系で）。"""
    o = ck.box(name, (0, 0, 0), size, color, bevel=bevel)
    o.data.transform(mat @ Matrix.Translation(center))
    return o


def build(name, color, shift):
    root = ck.asset(name)
    # 画面の座標系：x 右、z 上、−y が見る人の側。枠の表の面は y = −FRAME_D/2
    M = Matrix.Translation((shift, CENTER_Y, CENTER_Z)) @ Matrix.Rotation(-TILT, 4, "X")
    front = -FRAME_D / 2

    # 枠：本体＋内側の縁（画面のまわりの濃い帯）＋スピーカーの溝
    parts = [box_m("FrameBody", (0, 0, 0), (FRAME_W, FRAME_D, FRAME_H), "gray_light", M, bevel=0.03)]
    parts.append(box_m("Bezel", (0, front - 0.004, SCREEN_UP), (SCREEN_W + 0.05, 0.02, SCREEN_H + 0.05), "navy", M, bevel=0))
    for i in range(4):                                   # スピーカー（ヒューストンの声が出る所）。左下
        parts.append(box_m(f"Grille{i}", (-0.29 + 0.0, front - 0.004, CHIN_Z - 0.036 + i * 0.024),
                           (0.22, 0.02, 0.012), "gray_dark", M, bevel=0))
    ck.join(parts, "Frame")

    # 画面（独立した部品。縁より 4mm 手前）
    box_m("MonScreen", (0, front - 0.008, SCREEN_UP), (SCREEN_W, 0.02, SCREEN_H), "teal", M, bevel=0)

    # ひさし（席の色）。枠の上に載り、手前へ 0.10 出る
    box_m("Hood", (0, front - 0.02, FRAME_H / 2 + 0.012), (FRAME_W + 0.06, FRAME_D + 0.16, 0.045), color, M, bevel=0.011)

    # 状態ランプ（右下）。円柱の軸を画面の法線に向ける
    for i, (lamp, col) in enumerate((("Lamp_Green", "lamp_green"), ("Lamp_Yellow", "lamp_yellow"), ("Lamp_Red", "lamp_red"))):
        o = ck.cyl(lamp, (0, 0, 0), LAMP_R, LAMP_D, col, rot=(90, 0, 0), bevel=0.006)
        o.data.transform(M @ Matrix.Translation((0.17 + i * 0.095, front - 0.006, CHIN_Z)))

    # 柱＋足＋腕＋席の色の輪（まっすぐ立つ。画面の傾きとは別）
    stand = [ck.box("Pole", (shift, POLE_Y, POLE_TOP / 2), (POLE_T, POLE_T, POLE_TOP), "gray", bevel=0.015),
             ck.box("Foot", (shift, POLE_Y, 0.025), (0.26, 0.14, 0.05), "gray_dark", bevel=0.012),
             ck.box("Arm", (shift, (CENTER_Y + POLE_Y) / 2 + 0.02, POLE_TOP - 0.07), (0.14, POLE_Y - CENTER_Y + 0.06, 0.12), "gray", bevel=0.02),
             ck.box("Collar", (shift, POLE_Y, 1.02), (POLE_T + 0.03, POLE_T + 0.03, 0.06), color, bevel=0.012)]
    ck.join(stand, "Stand")

    # 画面のまん中（表の面の上）。+Z＝法線（見る人の側）、+X＝右、+Y＝上
    sl = ck.slot("Slot_Screen", M @ Matrix.Translation((0, front - 0.019, SCREEN_UP)).translation)
    sl.rotation_euler = (math.radians(90) - TILT, 0, 0)
    ck.end_asset()
    return root


def lineup(roots, path, gap):
    """4つを横に並べて、正面の少し上から1枚（画面が読める角度）。320px 版も出す。"""
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
        root = build(name, color, shift)
        ck.export_all(root, KIND)
        ck.render_check(root, KIND)
        print(f"[build] {name}: {ck.tri_count(root)} 三角")
        roots.append(root)
    for o in bpy.data.objects:
        o.hide_render = False
    lineup(roots, os.path.join(ck._export.MODELING, KIND, "renders", "seat_monitor_lineup.png"), gap=1.4)
