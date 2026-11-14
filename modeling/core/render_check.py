"""確認用のレンダー。どのアセットも同じカメラ・同じ光で PNG を出す。

書いた人：iizuka（作成：Fable 5.2、PNG の軽量化と parts_*.json：Opus-5）／ 最終更新：2026-11-14

出すもの（960×600、<名前>_*.png）：
  _1_game.png      ゲームと同じ角度（縦の画角 30°、見下ろし 50°、正面から）。アセットと 1.2m のダミーが入る距離
  _1_game_320.png  上の 320px 版（小さくても見分けられるかを見る）
  _2_front.png     斜め前の寄り（左前 30°、見下ろし 25°）
  _3_move.png      可動部を最大まで動かした姿（parts がないアセットは斜め後ろから）
光は暖色の平行光1灯（#FFE2B8）＋環境光、背景は濃紺、仮の床（直径 6m）と 1.2m のダミー。
カメラの距離はいつもアセットの外接箱から決める（大きさに関係なく画面に収まる）。
床は、アセットが直径 6m に収まらないとき（例：実寸の Ship_Stack）は自動で外す。ダミーは大きさの比較用に残る。

使い方：
  build_*.py の中から   ck.render_check(root, "fx", parts)
  単体で（glb を読む） modeling/core/run.sh modeling/core/render_check.py modeling/fx/export/Fx_Alarm.glb [出力フォルダ]
                        オプション：--floor / --no-floor（床を必ず出す／外す。省くと自動）、--no-dummy（ダミーを外す）、
                        --fit（外接箱に合わせる。いつもそうなので付けても同じ）
"""
import sys
sys.dont_write_bytecode = True
import bpy, json, math, os
from mathutils import Matrix, Vector

CORE = os.path.dirname(os.path.abspath(__file__))
if CORE not in sys.path:
    sys.path.insert(0, CORE)
import export as _export          # noqa: E402

RES = (960, 600)
FOV = math.radians(30)            # 縦の画角（Unity の FOV と同じ意味）
BG = "#1D2D50"
SUN = "#FFE2B8"
FLOOR_D = 6.0
DUMMY_H = 1.2
HELPER = "__check__"


def _srgb(hexstr):
    c = [int(hexstr[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    return tuple((v / 12.92) if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in c)


def _mat(name, hexcol):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*_srgb(hexcol), 1)
    b.inputs["Roughness"].default_value = 0.8
    return m


def _world():
    scn = bpy.context.scene
    w = bpy.data.worlds.new(HELPER); w.use_nodes = True; scn.world = w
    nt = w.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputWorld")
    mix = nt.nodes.new("ShaderNodeMixShader")
    lp = nt.nodes.new("ShaderNodeLightPath")
    amb = nt.nodes.new("ShaderNodeBackground")      # 環境光（少し青い）
    amb.inputs["Color"].default_value = (0.62, 0.68, 0.85, 1); amb.inputs["Strength"].default_value = 0.55
    bg = nt.nodes.new("ShaderNodeBackground")       # カメラに見える背景
    bg.inputs["Color"].default_value = (*_srgb(BG), 1); bg.inputs["Strength"].default_value = 1.0
    nt.links.new(lp.outputs["Is Camera Ray"], mix.inputs["Fac"])
    nt.links.new(amb.outputs["Background"], mix.inputs[1])
    nt.links.new(bg.outputs["Background"], mix.inputs[2])
    nt.links.new(mix.outputs["Shader"], out.inputs["Surface"])
    return w


def _helpers(lo, hi, floor, dummy):
    """床・ダミー・光・カメラを置く。返り値は (作った物のリスト, カメラ, ダミーの点)。"""
    scn = bpy.context.scene
    made = []
    if floor == "auto":       # 直径 6m の床に乗るときだけ出す（少しのはみ出しは許す）
        floor = max(abs(lo.x), abs(hi.x), abs(lo.y), abs(hi.y)) <= FLOOR_D / 2 and (hi.z - lo.z) <= FLOOR_D
    if floor:
        bpy.ops.mesh.primitive_cylinder_add(vertices=64, radius=FLOOR_D / 2, depth=0.02, location=(0, 0, -0.0105))
        fl = bpy.context.object; fl.name = HELPER + "floor"; fl.data.materials.append(_mat(HELPER + "floor", "#3D4B6E"))
        made.append(fl)
    # 1.2m のダミー（胴＋頭）。アセットの右（+X）に置く
    dx = hi.x + 0.45
    dummy_pts = []
    if dummy:
        dm = _mat(HELPER + "dummy", "#AEB6C6")
        bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=0.17, depth=0.72, location=(dx, 0, 0.36))
        body = bpy.context.object; body.name = HELPER + "dummy_body"; body.data.materials.append(dm); made.append(body)
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=0.25, location=(dx, 0, DUMMY_H - 0.25))
        head = bpy.context.object; head.name = HELPER + "dummy_head"; head.data.materials.append(dm); made.append(head)
        for o in (body, head):
            for p in o.data.polygons:
                p.use_smooth = True
        dummy_pts = [Vector((dx - 0.25, -0.25, 0)), Vector((dx + 0.25, 0.25, DUMMY_H))]
    sd = bpy.data.lights.new(HELPER + "sun", "SUN")
    sd.energy = 3.2; sd.color = _srgb(SUN); sd.angle = math.radians(8)
    sun = bpy.data.objects.new(HELPER + "sun", sd)
    sun.rotation_euler = (math.radians(48), 0, math.radians(-35))     # 左前の上から
    scn.collection.objects.link(sun); made.append(sun)
    cd = bpy.data.cameras.new(HELPER + "cam"); cd.sensor_fit = "VERTICAL"; cd.angle_y = FOV
    cd.clip_start = 0.05; cd.clip_end = 500
    cam = bpy.data.objects.new(HELPER + "cam", cd); scn.collection.objects.link(cam); made.append(cam)
    scn.camera = cam
    cd.clip_end = max(500, (hi - lo).length * 20)
    return made, cam, dummy_pts


def _aim(cam, pts, azim_deg, elev_deg, margin=1.12):
    """pts（世界座標）が全部入る距離にカメラを置く。azim 0 が正面（−Y 側）から、正の値で左（−X）へ回る。"""
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    c = (lo + hi) / 2
    r = max((hi - lo).length / 2, 0.05)
    d = r / math.sin(FOV / 2) * margin
    a, e = math.radians(azim_deg), math.radians(elev_deg)
    to_cam = Vector((-math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
    cam.location = c + to_cam * d
    cam.rotation_euler = (-to_cam).to_track_quat("-Z", "Y").to_euler()


def _corners(lo, hi):
    return [Vector((x, y, z)) for x in (lo.x, hi.x) for y in (lo.y, hi.y) for z in (lo.z, hi.z)]


def output_settings(scn=None):
    """PNG を軽くする保存設定（1枚 約520KB → 約70KB）。ディザのノイズを切り、RGB（透明なし）・最大圧縮で保存する。
    見た目はほぼ同じ。_render() が毎回呼ぶので、build_*.py の一覧画像も同じ設定になる。"""
    scn = scn or bpy.context.scene
    scn.render.dither_intensity = 0
    scn.render.image_settings.file_format = "PNG"
    scn.render.image_settings.color_mode = "RGB"
    scn.render.image_settings.color_depth = "8"
    scn.render.image_settings.compression = 100
    return scn


def _render(path):
    scn = bpy.context.scene
    output_settings(scn)
    scn.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


def _small(src, width=320):
    img = bpy.data.images.load(src)
    h = round(img.size[1] * width / img.size[0])
    img.scale(width, h)
    dst = src[:-4] + f"_{width}.png"
    img.filepath_raw = dst; img.file_format = "PNG"; img.save()
    bpy.data.images.remove(img)
    return dst


def pose(root, parts, t=1.0):
    """parts.json の部品を動かす。t=1 で最大、t=0 で初期値。返り値は元に戻すための関数。"""
    saved = []
    for p in parts:
        name = p["name"] if p["name"].startswith(root.name + ".") else f"{root.name}.{p['name']}"
        o = bpy.data.objects.get(name)
        if o is None:
            print("[render_check] parts の部品がない:", name); continue
        saved.append((o, o.location.copy(), o.rotation_euler.copy()))
        v = p.get("default", 0) + (p["max"] - p.get("default", 0)) * t
        i = "XYZ".index(p["axis"])
        if p["type"] == "rotate":
            o.rotation_euler[i] += math.radians(v - p.get("default", 0))
        else:
            axis = Vector((0, 0, 0)); axis[i] = 1
            o.location += o.matrix_basis.to_3x3() @ axis * (v - p.get("default", 0))
    bpy.context.view_layer.update()

    def restore():
        for o, loc, rot in saved:
            o.location = loc; o.rotation_euler = rot
        bpy.context.view_layer.update()
    return restore


def render_all(root, out_dir, parts=(), floor="auto", dummy=True):
    """root を写した PNG を out_dir に出す。ほかのアセットは写らないように隠し、終わったら元に戻す。
    floor："auto"（直径 6m に収まるときだけ）/ True / False。dummy：1.2m のダミーを置くか。"""
    scn = bpy.context.scene
    os.makedirs(out_dir, exist_ok=True)
    mine = set(_export.tree(root))
    hidden = [(o, o.hide_render) for o in bpy.data.objects if o not in mine]
    for o, _ in hidden:
        o.hide_render = True
    keep_loc = root.location.copy(); root.location = (0, 0, 0)
    lo, hi = _export.world_bounds(_export.meshes(root))
    old_world, old_cam = scn.world, scn.camera
    made, cam, dummy_pts = _helpers(lo, hi, floor, dummy)
    world = _world()
    scn.render.engine = "BLENDER_EEVEE"
    scn.render.resolution_x, scn.render.resolution_y = RES
    scn.render.resolution_percentage = 100
    scn.render.film_transparent = False
    output_settings(scn)
    scn.view_settings.view_transform = "Standard"; scn.view_settings.look = "None"
    base = os.path.join(out_dir, root.name)
    out = []
    pts = _corners(lo, hi)
    _aim(cam, pts + dummy_pts, 0, 50)
    out.append(_render(base + "_1_game.png"))
    out.append(_small(out[0]))
    _aim(cam, pts, 30, 25)
    out.append(_render(base + "_2_front.png"))
    if parts:
        restore = pose(root, parts, 1.0)
        lo2, hi2 = _export.world_bounds(_export.meshes(root))
        _aim(cam, pts + _corners(lo2, hi2), 30, 25)
        out.append(_render(base + "_3_move.png"))
        restore()
    else:
        _aim(cam, pts, 150, 25)
        out.append(_render(base + "_3_move.png"))
    for o in made:
        data = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if data and data.users == 0:
            getattr(bpy.data, {"MESH": "meshes", "LIGHT": "lights", "CAMERA": "cameras"}[data.id_type]).remove(data)
    bpy.data.worlds.remove(world)
    scn.world, scn.camera = old_world, old_cam
    for o, h in hidden:
        o.hide_render = h
    root.location = keep_loc
    bpy.context.view_layer.update()
    print("[render_check]", ", ".join(os.path.relpath(p, _export.REPO) for p in out))
    return out


def load_parts(kind_dir):
    """modeling/<種類>/ の parts.json と parts_*.json（例：操作部品の parts_a.json・parts_b.json）を全部読んで1つにする。"""
    data = {}
    if not os.path.isdir(kind_dir):
        return data
    for f in sorted(os.listdir(kind_dir)):
        if f == "parts.json" or (f.startswith("parts_") and f.endswith(".json")):
            with open(os.path.join(kind_dir, f), encoding="utf-8") as fh:
                for k, v in json.load(fh).items():
                    data.setdefault(k, v)
    return data


def _main(argv):
    floor = True if "--floor" in argv else False if "--no-floor" in argv else "auto"
    dummy = "--no-dummy" not in argv
    argv = [a for a in argv if not a.startswith("--")]
    if not argv:
        print("使い方: run.sh render_check.py <glb> [出力フォルダ] [--floor|--no-floor] [--no-dummy]"); sys.exit(2)
    glb = os.path.abspath(argv[0])
    kind_dir = os.path.dirname(os.path.dirname(glb))
    out_dir = os.path.abspath(argv[1]) if len(argv) > 1 else os.path.join(kind_dir, "renders")
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.import_scene.gltf(filepath=glb)
    name = os.path.splitext(os.path.basename(glb))[0]
    root = bpy.data.objects.get(name) or next(o for o in bpy.data.objects if o.parent is None)
    for o in bpy.data.objects:
        o.rotation_mode = "XYZ"          # glb は四元数で入るので、pose() が回せるように直す
    parts = load_parts(kind_dir).get(name, [])
    render_all(root, out_dir, parts, floor=floor, dummy=dummy)


if __name__ == "__main__":
    _main(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
