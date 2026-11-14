"""Shared Blender helpers for PhysDraft SpaceCrew props (imported inside Blender).
Every asset is an Empty root ("asset") with mesh parts parented to it; export_asset() writes a GLB."""
import bpy, math, os, random
from mathutils import Vector

ROOT = "/Users/tomokiashizawa/Claude/アプリ作成/PhysDraft-SpaceCrew3D"
_cur = [None]   # current parent root

def srgb(hexstr):
    c = [int(hexstr[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    return tuple((v / 12.92) if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in c)

def clear_scene():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.lights, bpy.data.cameras,
                 bpy.data.curves, bpy.data.armatures, bpy.data.actions):
        for d in list(coll):
            coll.remove(d)

def mat(name, hexcol, rough=0.5, metal=0.0, emit=0.0, alpha=1.0, coat=0.0):
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    col = srgb(hexcol)
    b.inputs["Base Color"].default_value = (*col, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if coat:
        b.inputs["Coat Weight"].default_value = coat
    if emit:
        b.inputs["Emission Color"].default_value = (*col, 1)
        b.inputs["Emission Strength"].default_value = emit
    if alpha < 1:
        b.inputs["Alpha"].default_value = alpha
        m.surface_render_method = "BLENDED"
    return m

# palette shared by all props
def P():
    return dict(
        white=mat("K_White", "#eef1f6", 0.5), grey=mat("K_Grey", "#596173", 0.55),
        dark=mat("K_Dark", "#2b2f3a", 0.5), metal=mat("K_Metal", "#b8c0cc", 0.3, 0.8),
        orange=mat("K_Orange", "#f2661c", 0.45), red=mat("K_Red", "#e2433a", 0.45),
        yellow=mat("K_Yellow", "#f2b417", 0.45), blue=mat("K_Blue", "#2f7de1", 0.45),
        cyan=mat("K_Cyan", "#3fd0e0", 0.4), green=mat("K_Green", "#3bd16f", 0.45),
        glass=mat("K_Glass", "#9fd8ff", 0.05, 0.0, 0, 0.35),
        screen=mat("K_Screen", "#38e0ff", 0.3, 0, 3.0), led_g=mat("K_LedG", "#3bff8a", 0.3, 0, 5.0),
        led_r=mat("K_LedR", "#ff2a1f", 0.3, 0, 2.2), led_y=mat("K_LedY", "#ffd23b", 0.3, 0, 6.0),
        fire=mat("K_Fire", "#ff5a14", 0.6, 0, 2.2), fire2=mat("K_Fire2", "#ffc23b", 0.6, 0, 2.8),
        smoke=mat("K_Smoke", "#c9d0da", 0.9, 0, 0, 0.55), space=mat("K_Space", "#0b1022", 1.0, 0, 0.0),
        star=mat("K_Star", "#ffffff", 0.3, 0, 6.0), holo=mat("K_Holo", "#5ff0ff", 0.2, 0, 4.0, 0.45),
        moon=mat("K_Moon", "#9a9a98", 0.95), moon2=mat("K_Moon2", "#6e6e6c", 0.95),
        rock=mat("K_Rock", "#7a6a5c", 0.9), floor=mat("K_Floor", "#3a4150", 0.4, 0.3),
        floor2=mat("K_Floor2", "#2e3441", 0.45, 0.3), wall=mat("K_Wall", "#d9dee7", 0.5),
        solar=mat("K_Solar", "#1d3a7a", 0.2, 0.6), gold=mat("K_Gold", "#d9a441", 0.3, 1.0),
    )

def asset(name):
    o = bpy.data.objects.new(name, None)
    o.empty_display_type = "PLAIN_AXES"; o["asset"] = True
    bpy.context.scene.collection.objects.link(o)
    _cur[0] = o
    return o

def _fin(o, m, name, bevel=0.0, seg=3, smooth=True):
    o.name = f"{_cur[0].name}.{name}" if _cur[0] else name
    if m:
        o.data.materials.append(m)
    if smooth:
        o.data.shade_smooth()
    if bevel:
        bv = o.modifiers.new("bevel", "BEVEL"); bv.width = bevel; bv.segments = seg; bv.limit_method = "ANGLE"
    if _cur[0]:          # assets are always built with the root at the origin, then moved
        o.parent = _cur[0]
    return o

def _apply(o, scale=True, rot=True):
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=False, rotation=rot, scale=scale)

def box(name, loc, size, m, bevel=None, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=[math.radians(a) for a in rot])
    o = bpy.context.object; o.scale = size; _apply(o)
    return _fin(o, m, name, bevel=min(size) * 0.25 if bevel is None else bevel, smooth=True)

def cyl(name, loc, r, depth, m, rot=(0, 0, 0), verts=32, bevel=None, r2=None):
    if r2 is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc,
                                            rotation=[math.radians(a) for a in rot])
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=r2, depth=depth, location=loc,
                                        rotation=[math.radians(a) for a in rot])
    o = bpy.context.object; _apply(o)
    return _fin(o, m, name, bevel=min(r, depth) * 0.2 if bevel is None else bevel)

def sph(name, loc, scale, m, seg=32, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=seg // 2, radius=1, location=loc,
                                         rotation=[math.radians(a) for a in rot])
    o = bpy.context.object; o.scale = scale if hasattr(scale, "__len__") else (scale,) * 3; _apply(o)
    return _fin(o, m, name)

def ico(name, loc, r, m, sub=2, noise=0.0, seed=0, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub, radius=r, location=loc)
    o = bpy.context.object; o.scale = scale; _apply(o)
    if noise:
        rnd = random.Random(seed)
        for v in o.data.vertices:
            v.co *= 1 + rnd.uniform(-noise, noise)
    return _fin(o, m, name, smooth=sub > 1)

def tor(name, loc, R, r, m, rot=(0, 0, 0), scale=(1, 1, 1), seg=48):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, location=loc,
                                     rotation=[math.radians(a) for a in rot], major_segments=seg, minor_segments=12)
    o = bpy.context.object; o.scale = scale; _apply(o)
    return _fin(o, m, name)

def plane(name, loc, size, m, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_plane_add(size=1, location=loc, rotation=[math.radians(a) for a in rot])
    o = bpy.context.object; o.scale = (size[0], size[1], 1); _apply(o)
    return _fin(o, m, name, smooth=False)

def tube(name, pts, r, m, res=12):
    """Pipe through points (bezier poly) converted to mesh."""
    cu = bpy.data.curves.new(name, "CURVE"); cu.dimensions = "3D"
    cu.bevel_depth = r; cu.bevel_resolution = 3; cu.use_fill_caps = True
    sp = cu.splines.new("POLY"); sp.points.add(len(pts) - 1)
    for p, c in zip(sp.points, pts):
        p.co = (*c, 1)
    o = bpy.data.objects.new(name, cu); bpy.context.scene.collection.objects.link(o)
    bpy.ops.object.select_all(action="DESELECT"); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.convert(target="MESH")
    o = bpy.context.object
    return _fin(o, m, name)

def text(body, loc, size, m, rot=(90, 0, 0), extrude=0.01):
    cu = bpy.data.curves.new("txt", "FONT"); cu.body = body; cu.size = size; cu.extrude = extrude
    cu.align_x = "CENTER"; cu.align_y = "CENTER"
    o = bpy.data.objects.new("txt", cu); o.location = loc
    o.rotation_euler = [math.radians(a) for a in rot]
    bpy.context.scene.collection.objects.link(o)
    bpy.ops.object.select_all(action="DESELECT"); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.convert(target="MESH")
    return _fin(bpy.context.object, m, "Label", smooth=False)

def end_asset():
    _cur[0] = None

def export_asset(root, folder, decimate=None):
    """Write <folder>/<root.name>.glb with the asset at the origin."""
    vl = bpy.context.view_layer
    os.makedirs(folder, exist_ok=True)
    keep, keep_rot = root.location.copy(), root.rotation_euler.copy()
    root.location = (0, 0, 0); root.rotation_euler = (0, 0, 0); vl.update()
    added = []
    if decimate:
        for o in root.children_recursive:
            if o.type == "MESH":
                m = o.modifiers.new("web_decimate", "DECIMATE"); m.ratio = decimate; added.append((o, m))
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for o in root.children_recursive:
        o.select_set(True)
    vl.objects.active = root
    path = os.path.join(folder, f"{root.name}.glb")
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_apply=True,
                              export_animations=False)
    for o, m in added:
        o.modifiers.remove(m)
    root.location = keep; root.rotation_euler = keep_rot
    return path, os.path.getsize(path)

def studio(cam_loc, cam_rot, lens=50, res=(1600, 1000), bg="#1e2235", floor_r=None, floor_loc=(0, 0, 0)):
    scn = bpy.context.scene
    w = bpy.data.worlds.get("World") or bpy.data.worlds.new("World"); scn.world = w; w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (*srgb(bg), 1)
    w.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.0
    def light(name, loc, rot, energy, col=(1, 1, 1), size=5):
        d = bpy.data.lights.new(name, "AREA"); d.energy = energy; d.color = col; d.size = size
        o = bpy.data.objects.new(name, d); o.location = loc; o.rotation_euler = [math.radians(a) for a in rot]
        scn.collection.objects.link(o)
    light("Key", (-4, -5, 6), (45, 0, -38), 900)
    light("Fill", (5, -4, 3), (65, 0, 50), 350, (0.65, 0.78, 1.0))
    light("Rim", (0, 6, 5), (-55, 0, 180), 700, (0.5, 0.8, 1.0))
    cd = bpy.data.cameras.new("Cam"); cd.lens = lens
    c = bpy.data.objects.new("Cam", cd); c.location = cam_loc
    c.rotation_euler = [math.radians(a) for a in cam_rot]; scn.collection.objects.link(c); scn.camera = c
    if floor_r:
        bpy.ops.mesh.primitive_cylinder_add(vertices=96, radius=floor_r, depth=0.05, location=(floor_loc[0], floor_loc[1], -0.025))
        f = bpy.context.object; f.name = "StudioFloor"; f.data.materials.append(mat("Studio", "#3a4150", 0.4, 0.3))
    scn.render.engine = "BLENDER_EEVEE"
    scn.render.resolution_x, scn.render.resolution_y = res
    scn.view_settings.view_transform = "Standard"; scn.view_settings.look = "None"
    scn.render.image_settings.file_format = "PNG"
    return c

def render(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
