"""地球・月を、船内の物（破れたパイプ・警報ランプ、あればクルーとコンソール）と並べた一覧の画像を出す。
「全部並べて同じゲームの物に見えるか」（目で見る10項目の 10）と「地球・月の継ぎ目」（9）を見る用。

書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14

  modeling/core/run.sh modeling/bodies/render_lineup.py
出力：modeling/bodies/renders/bodies_lineup.png（960×600）と bodies_lineup_320.png
読むのは各フォルダの export/*.glb だけ（書き出し済みの物。ほかの人のフォルダには書かない）。
カメラ・光・背景・床は core の render_check.py と同じ（ゲームと同じ角度：縦の画角 30°、見下ろし 50°、正面から）。
月は見やすさのために地球の 0.55 倍で置く（実際の比は 0.273。モデル自体は半径 1）。
"""
import sys, os
sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
MODELING = os.path.dirname(HERE)
sys.path.insert(0, os.path.join(MODELING, "core"))
import bpy
from mathutils import Vector
import export as _export
import render_check as rc

OUT = os.path.join(HERE, "renders", "bodies_lineup.png")
# (glb, 位置, 拡大率, なくてもよいか)
LINEUP = [
    ("bodies/export/Body_Earth.glb", (-1.45, 1.5, 1.0), 1.0, False),
    ("bodies/export/Body_Moon.glb", (1.35, 1.7, 0.55), 0.55, False),
    ("fx/export/Fx_BrokenPipe.glb", (-0.55, -1.2, 0), 1.0, False),
    ("fx/export/Fx_Alarm.glb", (1.15, -1.15, 0), 1.0, False),
    ("console/export/Console_Pilot.glb", (0.75, 0.15, 0), 1.0, True),     # 比べる用（ほかの担当の物。あれば）
    ("crew/export/Crew_Pilot.glb", (-0.55, -0.15, 0), 1.0, True),
]


def load(path, loc, scale):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    tops = [o for o in new if o.parent is None]
    for o in tops:
        o.location = Vector(loc) + o.location
        o.scale = (scale,) * 3
    return new


def main():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    objs, used = [], []
    for rel, loc, scale, optional in LINEUP:
        path = os.path.join(MODELING, rel)
        if not os.path.exists(path):
            assert optional, f"glb がない: {rel}（先に build を流す）"
            continue
        objs += load(path, loc, scale)
        used.append(os.path.basename(rel)[:-4])
    bpy.context.view_layer.update()
    lo, hi = _export.world_bounds(objs)
    has_crew = any(u.startswith("Crew_") for u in used)
    scn = bpy.context.scene
    made, cam, dummy_pts = rc._helpers(lo, hi, True, not has_crew)
    world = rc._world()
    scn.render.engine = "BLENDER_EEVEE"
    scn.render.resolution_x, scn.render.resolution_y = rc.RES
    scn.render.resolution_percentage = 100
    scn.render.image_settings.file_format = "PNG"
    scn.view_settings.view_transform = "Standard"; scn.view_settings.look = "None"
    rc._aim(cam, rc._corners(lo, hi) + dummy_pts, 0, 50, margin=1.0)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    rc._render(OUT)
    small = rc._small(OUT)
    print("[lineup]", ", ".join(used), "→", os.path.relpath(OUT, _export.REPO), os.path.relpath(small, _export.REPO))


main()
