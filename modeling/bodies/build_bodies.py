"""Body_Earth・Body_Moon：地球と月。UV 球（48×24、半径 1）に、NASA の画像を加工した絵を1枚ずつ貼る。

書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14

  python3 -I modeling/bodies/make_textures.py      先に画像を作る（画像を変えたときだけ）
  modeling/core/run.sh modeling/bodies/build_bodies.py
出力：unity/Assets/Models/Bodies/Body_Earth.fbx・Body_Moon.fbx（画像 earth_1024.png・moon_1024.png は同じフォルダ）、
      modeling/bodies/export/*.glb・*.stats.json、modeling/bodies/renders/Body_*_*.png
部品：<ルート>.Globe（球。原点は球の中心＝ルートと同じ）。可動部なし（自転は Unity でルートか Globe を回す）

向き：北極が +Z（Unity の +Y）。経度 0° が正面 −Y（Unity の +Z）、東経 90° が +X（Unity の −X）。
      画像の継ぎ目（経度 180°）は背中側 +Y（Unity の −Z）。月は経度 0° が地球に向く面（表側）なので、正面に表側が来る。
大きさ：半径 1m。Unity 側で拡大する（実際の比は 月／地球 ＝ 0.273）。
マテリアル：例外の M_Body_Earth・M_Body_Moon（各1枚の画像）。M_Core_Palette は使わない。月に顔は付けない（#54 の決定。月の表面の地形は modeling/scene/build_moon_surface.py）。
"""
import sys, os
sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "core"))
import bpy, math
import corekit as ck

KIND = "bodies"
SEG, RINGS = 48, 24          # UV 球の分割（経度 × 緯度）
RADIUS = 1.0
TEX_DIR = os.path.join(HERE, "textures")
BODIES = [  # (ルート名, マテリアル名, 画像)
    ("Body_Earth", "M_Body_Earth", "earth_1024.png"),
    ("Body_Moon", "M_Body_Moon", "moon_1024.png"),
]
TRIAL = "--trial" in sys.argv
if TRIAL:
    ck.trial()


def body_mat(name, image):
    """画像1枚を貼るだけのマテリアル（透明なし・金属なし）。FBX には画像の名前だけが入り、
    Unity は同じフォルダの unity/Assets/Models/Bodies/<image> を貼る。"""
    path = os.path.join(TEX_DIR, image)
    assert os.path.exists(path), f"画像がない: {path}（先に python3 -I modeling/bodies/make_textures.py）"
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    b.inputs["Roughness"].default_value = 0.85
    b.inputs["Metallic"].default_value = 0.0
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(path, check_existing=True)
    tex.interpolation = "Linear"
    tex.extension = "REPEAT"          # 経度 180° の継ぎ目で左右がつながるように
    tex.location = (-320, 300)
    nt.links.new(tex.outputs["Color"], b.inputs["Base Color"])
    return m


def globe(root, mat):
    """UV 球。primitive_uv_sphere_add の UV は u=0.5 が +X、u が増えると東（上から見て反時計回り）、v=1 が +Z。
    これを Z まわりに −90° 回して、経度 0°（u=0.5）を正面 −Y に向ける。"""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=SEG, ring_count=RINGS, radius=RADIUS, location=(0, 0, 0),
                                         rotation=(0, 0, math.radians(-90)), calc_uvs=True)
    o = bpy.context.object
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    o.name = f"{root.name}.Globe"
    o.data.name = o.name
    o.data.materials.append(mat)
    for p in o.data.polygons:
        p.use_smooth = True
    o.parent = root
    return o


def radial_normals(o):
    """法線を球の中心から外向きにそろえる（finalize の重み付き法線は極の近くで少し傾くので、上書きする）。"""
    me = o.data
    me.normals_split_custom_set_from_vertices([v.co.normalized() for v in me.vertices])
    me.update()


def check_uv(o):
    """念のため：正面（−Y）の赤道の点が u≈0.5、+X の赤道の点が u≈0.75（東経 90°）、北極が v=1 になっているか。"""
    me = o.data
    uv = me.uv_layers.active.data
    want = {(0, -1, 0): 0.5, (1, 0, 0): 0.75}
    for d, u_want in want.items():
        got = [uv[li].uv for p in me.polygons for li in p.loop_indices
               if (me.vertices[me.loops[li].vertex_index].co - ck.Vector(d)).length < 1e-4]
        assert got and all(abs(u[0] - u_want) < 1e-3 and abs(u[1] - 0.5) < 1e-3 for u in got), (d, got)
    top = [uv[li].uv[1] for p in me.polygons for li in p.loop_indices if me.vertices[me.loops[li].vertex_index].co.z > 0.999]
    assert top and all(abs(v - 1) < 1e-6 for v in top), top


ck.clear_scene()
for name, mat_name, image in BODIES:
    root = ck.asset(name)
    g = globe(root, body_mat(mat_name, image))
    check_uv(g)
    ck.end_asset()
    ck.finalize(root)            # 三角化と名前・マテリアルの確認（export_all の中でも呼ばれるが、2回目は何もしない）
    radial_normals(g)
    ck.export_all(root, KIND)
    ck.render_check(root, KIND, [], floor=False, dummy=False)
    print(f"[build] {name}: {ck.tri_count(root)} 三角")
    root.hide_render = True
    for o in root.children_recursive:
        o.hide_render = True
