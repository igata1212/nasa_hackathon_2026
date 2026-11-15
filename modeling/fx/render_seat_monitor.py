"""席の計器モニター（Fx_SeatMonitor_*）を船室に置いた確認図（#167）。

書いた人：Taiga（作成：Fable 5.2）／ 最終更新：2026-11-15

  modeling/core/run.sh modeling/fx/render_seat_monitor.py
出力（modeling/fx/renders/、960×600 と 320px 版）：
  seat_monitor_room_cutaway.png        横からの断面 15°（今の Slot_Cam_Cutaway）。4席とも入る
  seat_monitor_room_topdown.png        見下ろし 50°・席に寄せる（今のゲームのカメラ）
  seat_monitor_room_shoulder_<席>.png  肩ごしのカメラ（#167 の「近い三人称」の案）。Pilot（端の席）と LifeSupport（まん中の席）
読むのは各フォルダの export/*.glb だけ（先に build_seat_monitor.py を流す）。場面の組み立ては interior/render_assembled.py を借りる。
肩ごしのカメラの位置は、ここで仮に決めたもの（SHOULDER）。ゲームのカメラは Unity 側で決める。
"""
import sys, os, math
sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "core"))
sys.path.insert(0, os.path.join(HERE, "..", "interior"))
import bpy
from mathutils import Vector
import export as _export
import render_check as rc
import render_assembled as asm

OUT = os.path.join(_export.MODELING, "fx", "renders")
asm.RES = (960, 600)      # 容量の上限（check.py の合計 30MB）に収めるため、組み立て図より小さく撮る
# 肩ごしのカメラ（コンソールのローカル座標、m）：位置と、見る点。縦の画角は FOV_SHOULDER
SHOULDER = {"pos": (0.55, -2.05, 1.55), "look": (0.0, 0.28, 1.20)}
FOV_SHOULDER = math.radians(42)
SHIFT = {"Pilot": 0.0, "LifeSupport": -0.26, "CommsPower": 0.26, "MissionPlanner": 0.0}   # build_seat_monitor.py と同じ


def scene():
    room = asm.scene(players=4, accident=True)
    for seat in asm.SEATS:
        asm.put(asm.load(f"Fx_SeatMonitor_{seat}"), asm.child(room, f"Slot_Console_{seat}"))
    return room


def shoulder(room, seat, path):
    for n in asm.TOPDOWN_HIDE:
        asm.hide(asm.child(room, n))
    lo, hi = _export.world_bounds([o for o in bpy.data.objects if o.type == "MESH" and not o.hide_render])
    made, cam, _ = rc._helpers(lo, hi, False, False)
    rc._world()
    m = asm.child(room, f"Slot_Console_{seat}").matrix_world
    sx = 1 if SHIFT[seat] <= 0 else -1           # カメラは、画面をずらした向きと反対の肩ごしに置く
    px, py, pz = SHOULDER["pos"]; lx, ly, lz = SHOULDER["look"]
    pos = m @ Vector((sx * px, py, pz))
    look = m @ Vector((SHIFT[seat] * 0.5 + lx, ly, lz))
    view = (look - pos).normalized()
    asm.earth_behind_window(room, pos, view)
    cam.location = pos
    cam.rotation_euler = view.to_track_quat("-Z", "Y").to_euler()
    cam.data.angle_y = FOV_SHOULDER
    scn = bpy.context.scene
    scn.render.engine = "BLENDER_EEVEE"
    scn.render.resolution_x, scn.render.resolution_y = asm.RES
    scn.render.resolution_percentage = 100
    scn.view_settings.view_transform = "Standard"; scn.view_settings.look = "None"
    p = rc._render(path)
    rc._small(p)
    print("[seat_monitor]", os.path.relpath(p, _export.REPO))


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = argv[argv.index("--only") + 1] if "--only" in argv else None
    shots = [("cutaway", lambda r, p: asm.shoot(r, "Slot_Cam_Cutaway", asm.CUTAWAY_HIDE, p)),
             ("topdown", lambda r, p: asm.shoot(r, "Slot_Cam_Topdown_Close", asm.TOPDOWN_HIDE, p)),
             ("shoulder_Pilot", lambda r, p: shoulder(r, "Pilot", p)),
             ("shoulder_LifeSupport", lambda r, p: shoulder(r, "LifeSupport", p))]
    for name, fn in shots:
        if only and only != name:
            continue
        fn(scene(), os.path.join(OUT, f"seat_monitor_room_{name}.png"))
