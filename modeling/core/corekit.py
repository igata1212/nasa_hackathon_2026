"""#34 の共通キット。Blender の中で import して使う（`import corekit as ck`）。

元：midori の kit.py（modeling/PhysDraft-SpaceCrew3D/scripts/kit.py）。asset / box / cyl / sph / ico / tor / tube の
作り方はそのまま借りて、次を変えた。
  - パスは __file__ 基準（作者のマシンの絶対パスをやめた）
  - 分割数を下げた（円柱・球 16、ヘルメット 24、トーラス 24×8、ベベル 2段）
  - マテリアルは M_Core_Palette の1つだけ。色は paint(obj, "色名") で UV をパレットのマス中心に寄せる
  - set_origin()、ctrl_base(w, d)、join()、finalize()、parts.json、FBX/glb 書き出し、三角数の集計を足した
  - 文字・ガラス・発光・煙のマテリアルは外した（旗・文字なし、透明なし、発光は Unity 側）

書いた人：iizuka（作成：Fable 5.2、_fin などの注記：Opus-5）／ 最終更新：2026-11-14
更新：Taiga（2026-11-14、Fable 5.2）#48 ルートの名前に Part_（設計画面の部品）を足した
更新：Taiga（2026-11-14、Fable 5.2）#52 #53 ルートの名前に Scene_（場面の背景）を足した
ルールは同じフォルダの README.md。1 unit = 1m、Z が上、正面は −Y。
"""
import sys
sys.dont_write_bytecode = True
import bpy, json, math, os, random, re
from mathutils import Matrix, Vector

CORE = os.path.dirname(os.path.abspath(__file__))
if CORE not in sys.path:
    sys.path.insert(0, CORE)
import export as _export          # noqa: E402
import palette as _palette        # noqa: E402

REPO = _export.REPO
KINDS = _export.KINDS

MAT_NAME = "M_Core_Palette"
EXTRA_MATS = ("M_Body_Earth", "M_Body_Moon")       # 地球・月だけの例外
PALETTE = _palette.table()
LAMP_COLORS = _palette.LAMP_COLORS

SEG_CYL = 16          # 円柱・円錐
SEG_SPH = 16          # 球（リングは半分）
SEG_HELMET = 24       # ヘルメットだけ
SEG_TOR = (24, 8)     # トーラス（大円×小円）
BEVEL_SEG = 2         # ベベルの段数
BEVEL = 0.02          # ベベルの幅の標準（2〜4cm）。薄い部品は厚みの 1/4 まで自動で縮める
MIN_THICK = 0.02      # 一番薄いところ

ROOT_RE = re.compile(r"^(Ctrl|Console|Crew|Tank|Ship|Body|Fx|Part|Prop|Scene)_[A-Z][A-Za-z0-9_]*$")
PART_RE = re.compile(r"^[A-Z][A-Za-z0-9_]*$")

# Blender の軸 → Unity の軸（見本 Fx_Alarm と軸の確認用モデルを Unity 6000.6.9f1 に入れて確かめた。README 参照）
#   位置：Blender (x, y, z) → Unity (−x, z, −y)。正面（Blender −Y）は Unity の +Z
#   回転：Blender の X軸まわり +θ → Unity の X軸まわり +θ、Y → Z で +θ、Z → Y で −θ（Inspector の値）
UNITY_AXIS = {"X": ("X", -1), "Y": ("Z", -1), "Z": ("Y", 1)}      # 位置・移動：(Unity の軸, 符号)
UNITY_ROT = {"X": ("X", 1), "Y": ("Z", 1), "Z": ("Y", -1)}       # 回転：(Unity の軸, 符号)

_cur = [None]   # いま作っているアセットのルート


def trial(folder=None):
    """試し書き。これを呼ぶと、以後の書き出し・parts.json・レンダーが全部 folder（省くと tmp/modeling_trial/）の下に出て、
    modeling/ と unity/ には何も書かない。雛形の動作確認や実験に使う。"""
    folder = folder or os.path.join(REPO, "tmp", "modeling_trial")
    _export.set_out(folder, os.path.join(folder, "unity"))
    return folder


# ---------- 場面 ----------
def clear_scene():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.lights, bpy.data.cameras,
                 bpy.data.curves, bpy.data.images):
        for d in list(coll):
            coll.remove(d)
    _cur[0] = None


def srgb(hexstr):
    c = [int(hexstr[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    return tuple((v / 12.92) if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in c)


def palette_mat():
    """全メッシュ共通のマテリアル。palette.png を「最近傍」で貼るだけ。"""
    m = bpy.data.materials.get(MAT_NAME)
    if m:
        return m
    m = bpy.data.materials.new(MAT_NAME)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    b.inputs["Roughness"].default_value = 0.6
    b.inputs["Metallic"].default_value = 0.0
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(os.path.join(CORE, "palette.png"), check_existing=True)
    tex.interpolation = "Closest"
    tex.location = (-320, 300)
    nt.links.new(tex.outputs["Color"], b.inputs["Base Color"])
    return m


# ---------- アセットと部品 ----------
def asset(name):
    """アセットのルート（Empty）。以後に作る部品はこの子になり、名前は <ルート>.<部品>。"""
    assert ROOT_RE.match(name), f"ルートの名前がルール外: {name}"
    o = bpy.data.objects.new(name, None)
    o.empty_display_type = "PLAIN_AXES"
    o.empty_display_size = 0.1
    bpy.context.scene.collection.objects.link(o)
    _cur[0] = o
    return o


def end_asset():
    _cur[0] = None


def use_asset(root):
    """別のアセットの続きを作るときに、いまのルートを切り替える。"""
    _cur[0] = root


def _full_name(part):
    assert _cur[0] is not None, "先に ck.asset('Fx_Name') を呼ぶ"
    assert PART_RE.match(part), f"部品の名前がルール外（大文字始まり、英数字と _ だけ）: {part}"
    full = f"{_cur[0].name}.{part}"
    assert full not in bpy.data.objects, f"同じ名前の部品がもうある: {full}（.001 が付くので不可）"
    return full


def paint(obj, color):
    """obj の全部の面の UV を、パレットの「色名」のマス中心に寄せる。色は1部品につき1色。
    1つのメッシュに2色以上ほしいときは、色ごとに部品を作って join() でまとめる。"""
    assert color in PALETTE, f"パレットにない色: {color}（{', '.join(PALETTE)}）"
    if color in LAMP_COLORS:
        assert ".Lamp_" in obj.name, f"ランプ色 {color} は .Lamp_ の部品だけ: {obj.name}"
    me = obj.data
    if not me.materials:
        me.materials.append(palette_mat())
    uv = me.uv_layers.active or me.uv_layers.new(name="UVMap")
    u, v = PALETTE[color]["uv"]
    for d in uv.data:
        d.uv = (u, v)
    obj["color"] = color
    return obj


# ---------- 自作の形の仕上げ（_fin・_apply・_bevel） ----------
# 名前は「_」始まりだが、build_*.py が自分の形（押し出しの prism() など）を作るときに使っている
# （controls/build_controls_a.py、console/build_console.py、tank/build_tank.py、ship/build_ship.py、fx/build_broken_pipe.py）。
# 名前と引数を変えないこと。
#   _fin(o, 色名, 部品名, bevel=幅, smooth=True)   bpy で作ったメッシュを部品にする（名前・UV・ベベル・親）
#   _apply(o)                                      回転・スケールを形に焼く
#   _bevel(希望の幅 or None, いちばん薄い寸法)      ベベル幅（標準 2cm、厚みの 1/4 まで）
def _fin(o, color, name, bevel=0.0, smooth=True):
    o.name = _full_name(name)
    o.data.name = o.name
    while len(o.data.uv_layers) > 1:
        o.data.uv_layers.remove(o.data.uv_layers[-1])
    paint(o, color)
    for p in o.data.polygons:
        p.use_smooth = smooth
    if bevel:
        bv = o.modifiers.new("bevel", "BEVEL")
        bv.width = bevel; bv.segments = BEVEL_SEG; bv.limit_method = "ANGLE"; bv.angle_limit = math.radians(40)
    o.parent = _cur[0]
    return o


def _apply(o, scale=True, rot=True):
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=False, rotation=rot, scale=scale)


def _bevel(requested, thinnest):
    """ベベル幅。None なら標準 2cm。厚みの 1/4 を超えない（薄い板がつぶれないように）。0 でベベルなし。"""
    b = BEVEL if requested is None else requested
    return min(b, thinnest * 0.25)


def _rad(rot):
    return [math.radians(a) for a in rot]


def box(name, loc, size, color, bevel=None, rot=(0, 0, 0)):
    """直方体。loc は中心、size は (幅X, 奥行Y, 高さZ)、rot は度。"""
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=_rad(rot))
    o = bpy.context.object; o.scale = size; _apply(o)
    return _fin(o, color, name, bevel=_bevel(bevel, min(size)))


def cyl(name, loc, r, depth, color, rot=(0, 0, 0), verts=SEG_CYL, bevel=None, r2=None):
    """円柱（r2 を渡すと円錐台。r が下、r2 が上）。loc は中心、軸は Z。"""
    if r2 is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc, rotation=_rad(rot))
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=r2, depth=depth, location=loc,
                                        rotation=_rad(rot))
    o = bpy.context.object; _apply(o)
    return _fin(o, color, name, bevel=_bevel(bevel, min(2 * r, depth)))


def sph(name, loc, scale, color, seg=SEG_SPH, rot=(0, 0, 0)):
    """球。scale は半径（数1つ）か (X, Y, Z) の半径。ヘルメットは seg=ck.SEG_HELMET。"""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=seg // 2, radius=1, location=loc, rotation=_rad(rot))
    o = bpy.context.object; o.scale = scale if hasattr(scale, "__len__") else (scale,) * 3; _apply(o)
    return _fin(o, color, name)


def dome(name, loc, scale, color, seg=SEG_SPH, rot=(0, 0, 0)):
    """半球（下半分なし、底はふさぐ）。loc は底面の中心。"""
    o = sph(name, loc, scale, color, seg=seg, rot=rot)
    import bmesh
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, 0), plane_no=(0, 0, -1),
                           clear_outer=True)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)      # 切り口に重なった頂点が残ると面積0の三角ができる
    bmesh.ops.dissolve_degenerate(bm, edges=bm.edges[:], dist=1e-6)
    edges = [e for e in bm.edges if e.is_boundary]
    if edges:
        bmesh.ops.holes_fill(bm, edges=edges)
    bm.to_mesh(o.data); bm.free()
    for p in o.data.polygons:
        p.use_smooth = True
    return paint(o, color)


def ico(name, loc, r, color, sub=1, noise=0.0, seed=0, scale=(1, 1, 1)):
    """多面体の球（岩・破片むき）。"""
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub, radius=r, location=loc)
    o = bpy.context.object; o.scale = scale; _apply(o)
    if noise:
        rnd = random.Random(seed)
        for v in o.data.vertices:
            v.co *= 1 + rnd.uniform(-noise, noise)
    return _fin(o, color, name, smooth=sub > 1)


def tor(name, loc, R, r, color, rot=(0, 0, 0), scale=(1, 1, 1), seg=SEG_TOR):
    """トーラス。R は大円の半径、r は管の半径（r は 1cm 以上＝太さ 2cm 以上）。"""
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, location=loc, rotation=_rad(rot),
                                     major_segments=seg[0], minor_segments=seg[1])
    o = bpy.context.object; o.scale = scale; _apply(o)
    return _fin(o, color, name)


def tube(name, pts, r, color, res=1):
    """点列を通るパイプ（r は 1cm 以上）。res=1 で断面は8角形。"""
    cu = bpy.data.curves.new(name, "CURVE"); cu.dimensions = "3D"
    cu.bevel_depth = r; cu.bevel_resolution = res; cu.use_fill_caps = True
    sp = cu.splines.new("POLY"); sp.points.add(len(pts) - 1)
    for p, c in zip(sp.points, pts):
        p.co = (*c, 1)
    o = bpy.data.objects.new(name, cu); bpy.context.scene.collection.objects.link(o)
    bpy.ops.object.select_all(action="DESELECT"); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.convert(target="MESH")
    return _fin(bpy.context.object, color, name)


def ctrl_base(w=0.2, d=0.2, color="gray_dark", h=0.04):
    """操作部品の共通台座。w・d は 0.2m の倍数。底が z=0、中心が原点。部品名は必ず <ルート>.Base。"""
    for v in (w, d):
        assert abs(v / 0.2 - round(v / 0.2)) < 1e-6 and v > 0, f"台座は 0.2m の倍数: {v}"
    return box("Base", (0, 0, h / 2), (w, d, h), color, bevel=0.01)


# ---------- 原点・親子 ----------
def set_origin(obj, point=None):
    """obj の原点を世界座標の point に移す（形は動かない）。point を省くと底面の中心。
    可動部は回転軸の上の点を渡す。子があっても子の位置は変わらない。"""
    vl = bpy.context.view_layer
    vl.update()
    if point is None:
        lo, hi = _export.world_bounds([obj])
        point = ((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z)
    p = Vector(point)
    kids = [(c, c.matrix_world.copy()) for c in obj.children]
    mw = obj.matrix_world.copy()
    obj.data.transform(Matrix.Translation(-(mw.inverted() @ p)))
    mw.translation = p
    obj.matrix_world = mw
    vl.update()
    for c, m in kids:
        c.matrix_world = m
    vl.update()
    return obj


def attach(child, parent):
    """child を parent の子にする（見た目の位置は変えない）。可動部と一緒に動く部品に使う。"""
    vl = bpy.context.view_layer
    vl.update()
    mw = child.matrix_world.copy()
    child.parent = parent
    child.matrix_parent_inverse = Matrix.Identity(4)
    vl.update()
    child.matrix_world = mw
    vl.update()
    return child


def slot(name, loc):
    """取り付け位置（Empty）。名前は Slot_〜。"""
    assert name.startswith("Slot_"), f"取り付け位置は Slot_ で始める: {name}"
    o = bpy.data.objects.new(_full_name(name), None)
    o.empty_display_type = "PLAIN_AXES"; o.empty_display_size = 0.05
    o.location = loc
    bpy.context.scene.collection.objects.link(o)
    o.parent = _cur[0]
    return o


def _apply_modifiers(o):
    if not o.modifiers:
        return
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True); bpy.context.view_layer.objects.active = o
    for m in list(o.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)


def join(objs, name):
    """色の違う部品を1つのメッシュにまとめる（色＝UV はそのまま残る）。名前は <ルート>.<name>、原点は objs[0] の原点。
    まとめる前にベベルを確定するので、色の境目で UV がにじまない。"""
    objs = list(objs)
    for o in objs:
        _apply_modifiers(o)
    first = objs[0]
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = first
    if len(objs) > 1:
        bpy.ops.object.join()
    first.name = "__joined__"
    first.name = _full_name(name)
    first.data.name = first.name
    return first


# ---------- 仕上げ・集計 ----------
def tree(root):
    return _export.tree(root)


def meshes(root):
    return _export.meshes(root)


def tri_count(root):
    """ルート以下の三角数の合計（finalize の後に呼ぶ）。"""
    return sum(_export.tri_count(o) for o in meshes(root))


def finalize(root):
    """書き出し前の仕上げ。回転・スケールを適用し、ベベルを確定、三角にして、重み付き法線を付ける。
    名前とマテリアルもここで確かめる。何度呼んでもよい。"""
    vl = bpy.context.view_layer
    assert ROOT_RE.match(root.name), f"ルートの名前がルール外: {root.name}"
    for o in tree(root)[1:]:
        ok = ROOT_RE.match(o.name) or (o.name.count(".") == 1 and PART_RE.match(o.name.split(".")[1])
                                       and ROOT_RE.match(o.name.split(".")[0]))
        assert ok, f"名前がルール外: {o.name}"
    for o in meshes(root):
        if o.get("finalized"):
            continue
        if any(abs(s - 1) > 1e-6 for s in o.scale) or any(abs(a) > 1e-6 for a in o.rotation_euler):
            kids = [(c, c.matrix_world.copy()) for c in o.children]
            _apply(o)
            vl.update()
            for c, m in kids:
                c.matrix_world = m
        _apply_modifiers(o)
        tri = o.modifiers.new("tri", "TRIANGULATE"); tri.keep_custom_normals = True
        wn = o.modifiers.new("wn", "WEIGHTED_NORMAL"); wn.keep_sharp = True; wn.weight = 50
        _apply_modifiers(o)
        names = [m.name for m in o.data.materials]
        assert len(names) == 1 and (names[0] == MAT_NAME or names[0] in EXTRA_MATS), f"マテリアルがルール外: {o.name} {names}"
        o["finalized"] = True
    vl.update()
    return root


def write_parts(kind, root, parts):
    """modeling/<種類>/parts.json に root の可動部の表を書く（ほかのアセットの分は残す）。
    parts は dict のリスト：
      {"name": "Move_Lever", "type": "rotate", "axis": "X", "min": -30, "max": 30, "default": 0}   角度は度
      {"name": "Move_Button", "type": "translate", "axis": "Z", "min": -0.01, "max": 0, "default": 0}  距離は m
    axis は部品のローカル軸（Blender）。Unity での軸と符号、原点（Blender の世界座標）は自動で足す。"""
    bpy.context.view_layer.update()
    path = os.path.join(_export.MODELING, kind, "parts.json")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    data = {}
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            data = json.load(f)
    out = []
    for p in parts:
        full = p["name"] if p["name"].startswith(root.name + ".") else f"{root.name}.{p['name']}"
        o = bpy.data.objects.get(full)
        assert o is not None and o in root.children_recursive, f"parts.json の部品がない: {full}"
        assert p["type"] in ("rotate", "translate") and p["axis"] in "XYZ", p
        ua, us = (UNITY_ROT if p["type"] == "rotate" else UNITY_AXIS)[p["axis"]]
        q = dict(p); q["name"] = full
        q["origin"] = [round(v, 5) for v in o.matrix_world.translation]
        q["unity_axis"] = ua; q["unity_sign"] = us
        q["unit"] = "deg" if p["type"] == "rotate" else "m"
        out.append(q)
    data[root.name] = out
    with open(path, "w", encoding="utf-8") as f:
        json.dump(dict(sorted(data.items())), f, ensure_ascii=False, indent=1); f.write("\n")
    return out


def export_all(root, kind, anim=None):
    """仕上げてから FBX（unity/Assets/Models/<種類>/）と glb・stats.json（modeling/<種類>/export/）を出す。
    anim を渡すと FBX にアニメーションも入る（export.export_fbx を参照。#120）。"""
    finalize(root)
    return _export.export_all(root, kind, anim=anim)


def render_check(root, kind, parts=None, floor="auto", dummy=True):
    """確認用の PNG を modeling/<種類>/renders/ に出す（3枚＋1枚目の 320px 版）。
    カメラの距離はアセットの外接箱から自動で決まる。floor は "auto"（直径 6m に収まるときだけ床を出す）/ True / False、
    dummy=False で 1.2m のダミーを外す。"""
    import render_check as rc
    return rc.render_all(root, os.path.join(_export.MODELING, kind, "renders"), parts or [], floor=floor, dummy=dummy)
