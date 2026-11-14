"""ルートを原点に置いて FBX（Unity 用）と glb（予備）を書き出す。書き出した時点の数値も <名前>.stats.json に残す。

書いた人：iizuka（作成：Fable 5.2、FBX の画像パスをファイル名だけに：Opus-5、Unity の差し替えの注記：Opus 5.5）／ 最終更新：2026-11-14

ふつうは build_*.py から `ck.export_all(root, "fx")` で呼ぶ（corekit がここを使う）。
FBX の設定は FBX_SETTINGS の1か所だけ。Unity 6000.6.9f1 に見本（Fx_Alarm）を入れて確かめた値（README「Unity での向き」）。
"""
import sys
sys.dont_write_bytecode = True
import bpy, json, os
from mathutils import Vector

CORE = os.path.dirname(os.path.abspath(__file__))
MODELING = os.path.dirname(CORE)
REPO = os.path.dirname(MODELING)
UNITY_MODELS = os.path.join(REPO, "unity", "Assets", "Models")
KINDS = {"controls": "Controls", "console": "Console", "crew": "Crew", "tank": "Tank",
         "ship": "Ship", "bodies": "Bodies", "fx": "Fx", "props": "Props",
         "interior": "Ship"}      # 船室（Ship_Interior）。FBX は外観と同じ Ship/ に、元データは modeling/interior/ に置く（#40）

# Unity で確かめた設定。変えるときは build_axis_check.py のモデルを入れ直して ModelImportReport の出力を見ること。
# bake_space_transform は使わない：Blender 5.1.2 の FBX 書き出しは、これを True にすると「孫」（子の子）の位置と
# 回転が壊れる（Unity で 90° 回って入った）。代わりに export_fbx() が書き出しの間だけ、自分で軸を Y-up に直す。
FBX_SETTINGS = dict(
    use_selection=True,
    object_types={"EMPTY", "MESH"},
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS",
    bake_space_transform=False,
    axis_forward="-Z",
    axis_up="Y",
    use_mesh_modifiers=True,
    mesh_smooth_type="OFF",       # 法線だけ書く（重み付き法線をそのまま渡す）
    use_tspace=False,
    use_custom_props=False,
    add_leaf_bones=False,
    bake_anim=False,
    path_mode="STRIP",            # 画像は名前（palette.png）だけ書く。Unity が Assets 内から探す
    embed_textures=False,
)


def set_out(modeling_dir, unity_models_dir):
    """書き出し先を差し替える（試し書き用。corekit の ck.trial() が使う）。"""
    global MODELING, UNITY_MODELS
    MODELING, UNITY_MODELS = modeling_dir, unity_models_dir


def tree(root):
    return [root] + list(root.children_recursive)


def meshes(root):
    return [o for o in tree(root) if o.type == "MESH"]


def tri_count(o):
    o.data.calc_loop_triangles()
    return len(o.data.loop_triangles)


def world_bounds(objs):
    """メッシュの頂点から世界座標の最小・最大を出す。"""
    bpy.context.view_layer.update()
    lo, hi = Vector((1e9,) * 3), Vector((-1e9,) * 3)
    for o in objs:
        if o.type != "MESH":
            continue
        mw = o.matrix_world
        for v in o.data.vertices:
            p = mw @ v.co
            lo = Vector(map(min, lo, p)); hi = Vector(map(max, hi, p))
    return lo, hi


def stats(root):
    """三角数・寸法・各部品の原点（世界座標、Blender の軸）。check.py が FBX の読み直しと比べる。"""
    bpy.context.view_layer.update()
    ms = meshes(root)
    lo, hi = world_bounds(ms)
    r = lambda v: [round(x, 5) for x in v]
    return {
        "name": root.name,
        "tris": sum(tri_count(o) for o in ms),
        "min": r(lo), "max": r(hi), "size": r(hi - lo),
        "objects": {o.name: {"origin": r(o.matrix_world.translation),
                             "tris": tri_count(o) if o.type == "MESH" else 0} for o in tree(root)},
    }


def _select(root):
    bpy.ops.object.select_all(action="DESELECT")
    for o in tree(root):
        o.hide_set(False)
        o.select_set(True)
    bpy.context.view_layer.objects.active = root


def _axis_matrix():
    """Blender（Z 上・正面 −Y）→ FBX（Y 上）の回転。(x, y, z) → (x, z, −y)。"""
    from bpy_extras.io_utils import axis_conversion
    return axis_conversion(to_forward=FBX_SETTINGS["axis_forward"], to_up=FBX_SETTINGS["axis_up"]).to_4x4()


def _to_fbx_space(root, G):
    """ルート以下を「Y-up に直した形」に組み替える。見た目（世界座標）は変わらない。
    子・孫の行列は G·M·G⁻¹、メッシュの頂点は G·v、ルートは M·G⁻¹（書き出しが掛ける G と打ち消し合って回転 0 になる）。"""
    Gi = G.inverted()
    done = set()
    for o in tree(root):
        o.matrix_basis = o.matrix_parent_inverse @ o.matrix_basis
        o.matrix_parent_inverse.identity()
    for o in tree(root)[1:]:
        o.matrix_basis = G @ o.matrix_basis @ Gi
        if o.type == "MESH" and o.data.name not in done:
            o.data.transform(G); o.data.update(); done.add(o.data.name)
    root.matrix_basis = root.matrix_basis @ Gi
    bpy.context.view_layer.update()


def _no_abs_image_paths(fbx_path):
    """FBX の書き出しは、画像の「FileName」に必ず絶対パス（/Users/...）を書く（path_mode="STRIP" でも
    RelativeFilename だけが名前になる）。書き出しの間だけ、マシンに依らないパスに差し替える。
      - FBX と同じフォルダに同じ名前の画像があれば、両方ともファイル名だけ（例：earth_1024.png）
      - なければ unity/Assets/Models/Core/ の画像を指す：FileName は Unity のプロジェクトからのパス
        （Assets/Models/Core/palette.png）、RelativeFilename は FBX からの相対パス（../Core/palette.png）
    注意（2026-11-14、Unity 6000.6.9f1 で確かめた）：Unity はどちらのパスでも Core/palette.png を貼らない。
    同じフォルダか、上の階層の Textures/ フォルダ（例：Assets/Models/Textures/palette.png）にあれば貼る。
    そこで Unity 側で、M_Core_Palette を共通の Assets/Models/Core/M_Core_Palette.mat に差し替えている
    （unity/Assets/Editor/CorePaletteImport.cs、#38）。README「Unity での向き」の色の段落を参照。返り値は元に戻す関数。"""
    import io_scene_fbx.export_fbx_bin as fb
    orig = fb._gen_vid_path
    fbx_dir = os.path.dirname(os.path.abspath(fbx_path))

    def rel_only(img, scene_data):
        _abs, rel = orig(img, scene_data)
        name = os.path.basename(rel.replace("\\", "/"))
        core = os.path.join(UNITY_MODELS, "Core", name)
        if not os.path.exists(os.path.join(fbx_dir, name)) and os.path.exists(core):
            rel = os.path.relpath(core, fbx_dir).replace(os.sep, "/")
            proj = os.path.relpath(core, os.path.dirname(os.path.dirname(UNITY_MODELS))).replace(os.sep, "/")
            return proj, rel
        return name, name
    fb._gen_vid_path = rel_only

    def restore():
        fb._gen_vid_path = orig
    return restore


def export_fbx(root, path):
    """FBX を書く。Unity で、ルートも子も孫も回転 0・スケール 1、位置は Blender (x, y, z) → Unity (−x, z, −y) で入る。
    画像のパスは FBX からの相対パス（../Core/palette.png など）。マシンの絶対パスは入らない。"""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    G = _axis_matrix()
    keep = [(o, o.matrix_basis.copy()) for o in tree(root)]
    _to_fbx_space(root, G)
    restore_paths = _no_abs_image_paths(path)
    try:
        _select(root)
        bpy.ops.export_scene.fbx(filepath=path, **FBX_SETTINGS)
    finally:                      # 元の Z-up に戻す
        restore_paths()
        Gi = G.inverted()
        done = set()
        for o, m in keep:
            o.matrix_basis = m
            if o is not root and o.type == "MESH" and o.data.name not in done:
                o.data.transform(Gi); o.data.update(); done.add(o.data.name)
        bpy.context.view_layer.update()
    return path


def export_glb(root, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    _select(root)
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_apply=True,
                              export_yup=True, export_animations=False)
    return path


def export_all(root, kind):
    """FBX → unity/Assets/Models/<種類>/、glb と stats.json → modeling/<種類>/export/。ルートは原点に戻してから出す。"""
    assert kind in KINDS, f"種類は {sorted(KINDS)} のどれか: {kind}"
    root.location = (0, 0, 0); root.rotation_euler = (0, 0, 0); root.scale = (1, 1, 1)
    bpy.context.view_layer.update()
    exp = os.path.join(MODELING, kind, "export")
    fbx = export_fbx(root, os.path.join(UNITY_MODELS, KINDS[kind], root.name + ".fbx"))
    glb = export_glb(root, os.path.join(exp, root.name + ".glb"))
    st = stats(root)
    with open(os.path.join(exp, root.name + ".stats.json"), "w", encoding="utf-8") as f:
        json.dump(st, f, ensure_ascii=False, indent=1); f.write("\n")
    print(f"[export] {root.name}: {st['tris']} 三角, 寸法 {st['size']} m, "
          f"FBX {os.path.getsize(fbx) / 1024:.0f}KB → {os.path.relpath(fbx, REPO)}, "
          f"glb {os.path.getsize(glb) / 1024:.0f}KB → {os.path.relpath(glb, REPO)}")
    return fbx, glb, st
