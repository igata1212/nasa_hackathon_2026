"""船室の組み立て図。Ship_Interior の .Slot_〜 に、できているモデル（glb）を置いて、ゲームのカメラ2通りで撮る。

書いた人：iizuka（作成：Fable 5.2）／ 最終更新：2026-11-14

  modeling/core/run.sh modeling/interior/render_assembled.py
出力（modeling/interior/renders/、1600×1000 と 320px 版）：
  interior_assembled_topdown.png        見下ろし 50°、床の全体（.Slot_Cam_Topdown）。屋根なし。4人・事故のあと（破れたタンクとパイプ、警報ランプ）
  interior_assembled_topdown_close.png  見下ろし 50°、席に寄せる（.Slot_Cam_Topdown_Close）。同じ場面
  interior_assembled_cutaway.png        横からの断面 15°（.Slot_Cam_Cutaway）。奥の屋根あり。同じ場面
  interior_assembled_3p_topdown.png     3人・事故の前（Mission Planner の席が空き、Comms & Power のクルーが2台の間）
窓の向こうの地球（Body_Earth）は場面の確認用に置いただけ（ゲームでの位置は Unity 側で決める）。
読むのは各フォルダの export/*.glb だけ（先に build を流す）。光・背景・保存設定は core/render_check.py と同じ。
試すとき：-- --out <フォルダ> で出力先、--elev <度> で断面カメラの見下ろし角、--only <名前> で1枚だけ。
"""
import sys, os, math, re
sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "core"))
sys.path.insert(0, HERE)
import bpy
from mathutils import Matrix, Vector
import export as _export
import render_check as rc
import render_all as ra            # コンソールに載せる操作部品の表（CONSOLES）を借りる

MODELING = _export.MODELING
RES = (1600, 1000)
INTERIOR = "Ship_Interior"
SEATS = ["Pilot", "LifeSupport", "CommsPower", "MissionPlanner"]
EYES = {"Pilot": "Eyes_Normal", "LifeSupport": "Eyes_Panic", "CommsPower": "Eyes_Normal", "MissionPlanner": "Eyes_Relief"}
CTRLS = {name.split("_", 1)[1]: ctrls for name, ctrls in ra.CONSOLES}


def load(name, kind=None):
    """export/<name>.glb を読み、そのルートを返す（同じ glb を2回読んでもよい）。"""
    kind = kind or ra.KIND_OF[name.split("_")[0]]
    path = os.path.join(MODELING, kind, "export", name + ".glb")
    assert os.path.exists(path), f"glb がない: {path}（先に build を流す）"
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        o.rotation_mode = "XYZ"
    return next(o for o in new if o.parent is None)


def child(root, part):
    """root の下の <ルート>.<part>（2回読んだときの .001 も拾う）。"""
    for o in [root] + list(root.children_recursive):
        if re.sub(r"\.\d{3}$", "", o.name).endswith("." + part):
            return o
    raise KeyError(f"{root.name} に {part} がない")


def put(obj, slot):
    bpy.context.view_layer.update()
    obj.matrix_world = slot.matrix_world.normalized()
    bpy.context.view_layer.update()
    return obj


def hide(o, flag=True):
    for x in [o] + list(o.children_recursive):
        x.hide_render = flag


def scene(players=4, accident=True):
    """船室に全部置く。返り値は船室のルート。"""
    ra._clear()
    room = load(INTERIOR, "interior")
    S = lambda n: child(room, n)
    for seat in SEATS:
        con = put(load(f"Console_{seat}"), S(f"Slot_Console_{seat}"))
        for no, ctrl in CTRLS[seat].items():
            put(load(ctrl), child(con, f"Slot_{no}"))
        if players == 3 and seat == "MissionPlanner":
            continue                                    # 空いた席：コンソールと床の印だけ残る
        slot = "Slot_Crew_Dual_CommsPlanner" if (players == 3 and seat == "CommsPower") else f"Slot_Crew_{seat}"
        crew = put(load(f"Crew_{seat}"), S(slot))
        for o in crew.children_recursive:
            if ".Eyes_" in o.name:
                o.hide_render = EYES[seat] not in o.name
    t1 = put(load("Tank_Oxygen1"), S("Slot_Tank_1"))
    t2 = put(load("Tank_Oxygen2_Broken" if accident else "Tank_Oxygen2"), S("Slot_Tank_2"))
    for t in (t1, t2):
        put(load("Ctrl_Valve"), child(t, "Slot_Valve"))
    for k in (1, 2):
        put(load("Fx_Alarm"), S(f"Slot_Alarm_{k}"))
    if accident:
        put(load("Fx_BrokenPipe"), S("Slot_Pipe"))
        hide(S("Pipe_Intact"))
    return room


def earth_behind_window(room, pos, view):
    """窓の向こうに地球を置く（窓から地球の縁が見える位置と大きさ）。場面の確認用で、ゲームの配置ではない。"""
    w = child(room, "Slot_Window").matrix_world.translation
    right = view.cross(Vector((0, 0, 1))).normalized()
    up = right.cross(view).normalized()
    target = w + right * EARTH_AT[0] + up * EARTH_AT[1]        # 窓の面の上で、地球の中心が見える位置
    k = ((w - pos).length + EARTH_FAR) / (w - pos).length
    earth = load("Body_Earth")
    earth.matrix_world = (Matrix.Translation(pos + (target - pos) * k) @ Matrix.Rotation(math.radians(200), 4, "Z")
                          @ Matrix.Scale(EARTH_AT[2] * k, 4))
    bpy.context.view_layer.update()
    return earth


def shoot(room, cam_slot, hidden, path, elev=None):
    scn = bpy.context.scene
    for n in hidden:
        hide(child(room, n))
    lo, hi = _export.world_bounds([o for o in bpy.data.objects if o.type == "MESH" and not o.hide_render])
    made, cam, _ = rc._helpers(lo, hi, False, False)
    rc._world()
    if elev is None:
        m = child(room, cam_slot).matrix_world
        pos, view = m.translation.copy(), (m.to_3x3() @ Vector((0, -1, 0))).normalized()
    else:                                   # 試し：見下ろし角を変えて、同じ合わせ方でカメラを置き直す
        import build_interior as bi
        base = bi.rim_points(bi.R_OUT, 0, 0, 360, 48) + bi.rim_points(bi.R_OUT, bi.BASE_H, 0, 360, 48)
        pts = base + bi.rim_points(bi.R_OUT, bi.WALL_TOP, -90, 90)
        if f"Roof_Back" not in hidden:
            pts += bi.rim_points(bi.R_TOP, bi.TUNNEL_TOP + 0.15, -90, 90) + [Vector((0, -bi.HATCH_R, bi.TUNNEL_TOP + 0.15))]
        pos, view, _ = bi.fit_camera(pts, elev)
    earth_behind_window(room, pos, view)
    cam.location = pos
    cam.rotation_euler = view.to_track_quat("-Z", "Y").to_euler()
    scn.render.engine = "BLENDER_EEVEE"
    scn.render.resolution_x, scn.render.resolution_y = RES
    scn.render.resolution_percentage = 100
    scn.render.film_transparent = False
    scn.view_settings.view_transform = "Standard"; scn.view_settings.look = "None"
    os.makedirs(os.path.dirname(path), exist_ok=True)
    p = rc._render(path)
    s = rc._small(p)
    print("[assembled]", os.path.relpath(p, _export.REPO), os.path.relpath(s, _export.REPO))


EARTH_AT = (0.30, -0.62, 0.85)     # 窓のまん中から見た地球の中心（右, 上）と見かけの半径（m、窓の位置での大きさ）
EARTH_FAR = 40.0                   # 窓から地球までの距離
TOPDOWN_HIDE = ["Wall_Front", "Roof_Front", "Roof_Back"]
CUTAWAY_HIDE = ["Wall_Front", "Roof_Front"]

if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = lambda k, d=None: argv[argv.index(k) + 1] if k in argv else d
    out = os.path.abspath(opt("--out", os.path.join(MODELING, "interior", "renders")))
    elev = float(opt("--elev")) if opt("--elev") else None
    only = opt("--only")
    shots = [
        ("interior_assembled_topdown", dict(players=4, accident=True), "Slot_Cam_Topdown", TOPDOWN_HIDE, None),
        ("interior_assembled_topdown_close", dict(players=4, accident=True), "Slot_Cam_Topdown_Close", TOPDOWN_HIDE, None),
        ("interior_assembled_cutaway", dict(players=4, accident=True), "Slot_Cam_Cutaway", CUTAWAY_HIDE, elev),
        ("interior_assembled_3p_topdown", dict(players=3, accident=False), "Slot_Cam_Topdown_Close", TOPDOWN_HIDE, None),
    ]
    for name, kw, cam, hidden, e in shots:
        if only and only != name:
            continue
        shoot(scene(**kw), cam, hidden, os.path.join(out, name + ".png"), e)
