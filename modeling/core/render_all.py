"""全部のアセットを並べた一覧の画像（目で見る10項目を判定する用）。

書いた人：iizuka（作成：Opus-5）／ 最終更新：2026-11-14

  modeling/core/run.sh modeling/core/render_all.py
出力：
  modeling/renders/all_lineup.png（1600×1000）と all_lineup_320.png
      ゲームと同じ角度（縦の画角 30°、見下ろし 50°、正面から）。奥にコンソール4台、その傾いたパネルの取り付け位置
      （.Slot_1〜6 の位置と向き）に操作部品を載せる。手前にクルー4人（正面＝カメラ向き。目は3種を1つずつ見せる）。
      右にタンク3つ（Tank_Oxygen2 の .Slot_Valve にバルブ）、左に警報ランプと破れたパイプ。
  modeling/renders/all_exterior.png（1600×1000）と all_exterior_320.png
      宇宙船（Ship_Stack）と地球・月。大きさと距離は見栄え優先（実際の比ではない）。
読むのは各フォルダの export/*.glb だけ（書き出し済みの物）。先に全部の build_*.py を流すこと。
カメラ以外（光・背景・保存設定）は render_check.py と同じ。
"""
import sys
sys.dont_write_bytecode = True
import bpy, math, os
from mathutils import Matrix, Vector

CORE = os.path.dirname(os.path.abspath(__file__))
if CORE not in sys.path:
    sys.path.insert(0, CORE)
import export as _export          # noqa: E402
import render_check as rc         # noqa: E402

MODELING = _export.MODELING
OUT_DIR = os.path.join(MODELING, "renders")
RES = (1600, 1000)

KIND_OF = {"Ctrl": "controls", "Console": "console", "Crew": "crew", "Tank": "tank", "Ship": "ship",
           "Body": "bodies", "Fx": "fx"}

# コンソールと、そのパネルに載せる操作部品（取り付け位置の番号 → 操作部品）
CONSOLES = [
    ("Console_Pilot", {1: "Ctrl_Toggle_Nav", 2: "Ctrl_Ignite", 4: "Ctrl_AttitudeStick", 6: "Ctrl_Button"}),
    ("Console_LifeSupport", {2: "Ctrl_Stir", 4: "Ctrl_Toggle_Heater"}),
    ("Console_CommsPower", {1: "Ctrl_Toggle_Comms", 2: "Ctrl_Toggle_Lights", 4: "Ctrl_RouteLever", 6: "Ctrl_Transmit"}),
    ("Console_MissionPlanner", {2: "Ctrl_Instr_Camera", 3: "Ctrl_Instr_Spectrometer", 5: "Ctrl_Instr_Radar"}),
]
CONSOLE_GAP = 1.5
# クルー（コンソールの手前に並べる。見せる目）
CREW = [("Crew_Pilot", "Eyes_Normal"), ("Crew_LifeSupport", "Eyes_Panic"),
        ("Crew_CommsPower", "Eyes_Relief"), ("Crew_MissionPlanner", "Eyes_Normal")]
CREW_Y = -0.95
# 横に置く物：(名前, 位置, Z まわりの角度)
PROPS = [
    ("Tank_Oxygen1", (3.45, 0.35, 0), 0),
    ("Tank_Oxygen2", (4.15, 0.35, 0), 0),
    ("Tank_Oxygen2_Broken", (3.8, -0.6, 0), 0),
    ("Fx_Alarm", (-3.45, 0.3, 0), 0),
    ("Fx_BrokenPipe", (-3.7, -0.75, 0), 0),
]
VALVE_ON = ("Tank_Oxygen2", "Slot_Valve", "Ctrl_Valve")


def _clear():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.lights, bpy.data.cameras,
                 bpy.data.worlds):
        for d in list(coll):
            coll.remove(d)


def load(name):
    """export/<name>.glb を読み、ルートを返す。"""
    path = os.path.join(MODELING, KIND_OF[name.split("_")[0]], "export", name + ".glb")
    assert os.path.exists(path), f"glb がない: {path}（先に build を流す）"
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    root = bpy.data.objects.get(name) or next(o for o in new if o.parent is None)
    for o in new:
        o.rotation_mode = "XYZ"
    return root


def place(root, loc=(0, 0, 0), rot_z=0.0, scale=1.0, rot=None):
    root.matrix_world = (Matrix.Translation(loc) @ (rot.to_4x4() if rot is not None else Matrix.Rotation(math.radians(rot_z), 4, "Z"))
                         @ Matrix.Scale(scale, 4))
    bpy.context.view_layer.update()


def on_slot(ctrl, host, slot):
    """操作部品のルート（台座の底の中心）を、host の取り付け位置（Empty）の位置と向きに合わせる。"""
    bpy.context.view_layer.update()
    s = bpy.data.objects[f"{host.name}.{slot}"]
    ctrl.matrix_world = s.matrix_world.normalized()
    bpy.context.view_layer.update()


def all_meshes():
    return [o for o in bpy.data.objects if o.type == "MESH" and not o.hide_render]


def _floor(lo, hi, pad=2.5):
    bpy.ops.mesh.primitive_plane_add(size=1, location=((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, -0.001))
    fl = bpy.context.object; fl.name = rc.HELPER + "floor_all"
    fl.scale = (hi.x - lo.x + 2 * pad, hi.y - lo.y + 2 * pad, 1)
    fl.data.materials.append(rc._mat(rc.HELPER + "floor_all", "#3D4B6E"))
    return fl


def _setup(cam_pts, azim, elev, margin, lo, hi):
    scn = bpy.context.scene
    made, cam, _ = rc._helpers(lo, hi, False, False)
    rc._world()
    scn.render.engine = "BLENDER_EEVEE"
    scn.render.resolution_x, scn.render.resolution_y = RES
    scn.render.resolution_percentage = 100
    scn.render.film_transparent = False
    scn.view_settings.view_transform = "Standard"; scn.view_settings.look = "None"
    rc._aim(cam, cam_pts, azim, elev, margin=margin)
    return cam


def lineup():
    _clear()
    consoles = []
    n = len(CONSOLES)
    for i, (cname, ctrls) in enumerate(CONSOLES):
        con = load(cname)
        place(con, ((i - (n - 1) / 2) * CONSOLE_GAP, 0, 0))
        for slot_no, ctrl_name in ctrls.items():
            on_slot(load(ctrl_name), con, f"Slot_{slot_no}")
        consoles.append(con)
    for i, (crew, eyes) in enumerate(CREW):
        r = load(crew)
        place(r, ((i - (n - 1) / 2) * CONSOLE_GAP, CREW_Y, 0))
        for o in r.children_recursive:
            if ".Eyes_" in o.name:
                o.hide_render = not o.name.endswith(eyes)
    hosts = {}
    for name, loc, rz in PROPS:
        r = load(name)
        place(r, loc, rz)
        hosts[name] = r
    host, slot, ctrl = VALVE_ON
    on_slot(load(ctrl), hosts[host], slot)
    lo, hi = _export.world_bounds(all_meshes())
    _floor(lo, hi)
    pts = rc._corners(lo, hi)
    _setup(pts, 0, 50, 0.62, lo, hi)
    os.makedirs(OUT_DIR, exist_ok=True)
    p = rc._render(os.path.join(OUT_DIR, "all_lineup.png"))
    s = rc._small(p)
    print("[render_all]", os.path.relpath(p, _export.REPO), os.path.relpath(s, _export.REPO))


def exterior():
    _clear()
    ship = load("Ship_Stack")
    # 長軸（+Z）を右上へ倒し、月へ向かう向きに。少しこちらへ振る
    rot = (Matrix.Rotation(math.radians(-18), 3, "Y") @ Matrix.Rotation(math.radians(-25), 3, "Z")
           @ Matrix.Rotation(math.radians(90), 3, "Y"))
    place(ship, (1.0, -2.0, -1.0), rot=rot)
    earth = load("Body_Earth")
    place(earth, (-17, 30, -3), rot_z=25, scale=11)
    moon = load("Body_Moon")
    place(moon, (20, 30, 11), rot_z=0, scale=4.0)
    s_lo, s_hi = _export.world_bounds(_export.meshes(ship))
    lo, hi = _export.world_bounds(all_meshes())
    # カメラは宇宙船を中心に、地球・月の手前の面が入るように
    pts = rc._corners(s_lo, s_hi) + [Vector((-28, 30, -3)), Vector((24, 30, 11))]
    _setup(pts, 0, 12, 0.62, lo, hi)
    p = rc._render(os.path.join(OUT_DIR, "all_exterior.png"))
    s = rc._small(p)
    print("[render_all]", os.path.relpath(p, _export.REPO), os.path.relpath(s, _export.REPO))


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not argv or "lineup" in argv:
        lineup()
    if not argv or "exterior" in argv:
        exterior()
