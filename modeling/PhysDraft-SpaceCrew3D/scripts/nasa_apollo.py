# Apollo 13-style stack from NASA 3D Resources (github.com/nasa/NASA-3D-Resources):
#   - "Apollo Soyuz.glb": keep only the Apollo Command + Service Module (drop the ASTP docking module and Soyuz)
#   - "Apollo Lunar Module.glb": docked nose-to-nose to the CM, as flown on Apollo 13 (LM = lifeboat)
# Exports CSM / LM / Stack as GLB + FBX (Unity) to export/nasa/, renders a preview. Writes blend/nasa_apollo.blend.
import sys, importlib, math, os
sys.path.insert(0, "/Users/tomokiashizawa/Claude/アプリ作成/PhysDraft-SpaceCrew3D/scripts")
import kit; importlib.reload(kit)
import bpy, bmesh
from mathutils import Vector, Matrix

ROOT = kit.ROOT
SRC = os.path.join(ROOT, "nasa_source")
OUT = os.path.join(ROOT, "export", "nasa")
kit.clear_scene()
for img in list(bpy.data.images):
    if img.users == 0:
        bpy.data.images.remove(img)
vl = bpy.context.view_layer

def import_glb(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    return [o for o in bpy.data.objects if o not in before]

def bake_into_one(objs, name):
    """Apply transforms and join all meshes into one object (keeps materials/UVs)."""
    meshes = [o for o in objs if o.type == "MESH"]
    empty_names = [o.name for o in objs if o.type == "EMPTY"]   # names only: join() frees the others
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    vl.objects.active = meshes[0]
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(meshes) > 1:
        bpy.ops.object.join()
    o = vl.objects.active; o.name = name
    for n in empty_names:
        if n in bpy.data.objects:
            bpy.data.objects.remove(bpy.data.objects[n], do_unlink=True)
    return o

# ---------- Command + Service Module ----------
astp = bake_into_one(import_glb(os.path.join(SRC, "Apollo Soyuz.glb")), "Apollo_CSM")
# Apollo side spans x in [-12.3, -0.4]; the ASTP docking module starts at x ~ -0.74 with its own materials.
DOCKING_MATS = {"blinn6SG", "blinn5SG", "blinn2SG", "blinn7SG", "blinn3SG"}
bm = bmesh.new(); bm.from_mesh(astp.data)
mats = astp.data.materials
drop = []
for f in bm.faces:
    x = f.calc_center_median().x
    mname = mats[f.material_index].name.split(".")[0] if mats[f.material_index] else ""
    if x > -0.42 or mname in DOCKING_MATS:
        drop.append(f)
bmesh.ops.delete(bm, geom=drop, context="FACES")
bm.to_mesh(astp.data); bm.free()
astp.data.update()

def bounds(o):
    pts = [o.matrix_world @ v.co for v in o.data.vertices]
    return Vector(map(min, zip(*pts))), Vector(map(max, zip(*pts)))

mn, mx = bounds(astp)
apex_x = mx.x
axis_y, axis_z = (mn.y + mx.y) / 2, (mn.z + mx.z) / 2
print("CSM bounds", tuple(round(v, 2) for v in mn), tuple(round(v, 2) for v in mx))

# ---------- Lunar Module, docked to the CM apex ----------
lm = bake_into_one(import_glb(os.path.join(SRC, "Apollo Lunar Module.glb")), "Apollo_LM")
LM_SCALE = 1.4          # NASA LM model is ~5 m tall; the real LM is ~7 m with legs deployed
lmn, lmx = bounds(lm)
lm_center_xy = Vector(((lmn.x + lmx.x) / 2, (lmn.y + lmx.y) / 2, 0))
# the docking tunnel is the highest point *near the centre axis* (antennas stick up higher off-axis)
tunnel_top = max(v.co.z for v in lm.data.vertices
                 if math.hypot(v.co.x - lm_center_xy.x, v.co.y - lm_center_xy.y) < 0.45)
# move LM so its docking tunnel is at the origin, then rotate +Z -> -X (top faces the CM)
lm.data.transform(Matrix.Translation(-Vector((lm_center_xy.x, lm_center_xy.y, tunnel_top))))
lm.data.transform(Matrix.Scale(LM_SCALE, 4))
lm.data.transform(Matrix.Rotation(math.radians(-90), 4, "Y"))
lm.location = (apex_x + 0.05, axis_y, axis_z)
vl.update()

# ---------- put both on a common origin: stack centre, long axis = X ----------
for o in (astp, lm):
    o.data.transform(Matrix.Translation(o.location))
    o.location = (0, 0, 0)
smn = Vector(map(min, zip(bounds(astp)[0], bounds(lm)[0])))
smx = Vector(map(max, zip(bounds(astp)[1], bounds(lm)[1])))
centre = (smn + smx) / 2
for o in (astp, lm):
    o.data.transform(Matrix.Translation(-centre))
vl.update()
print("stack length", round(smx.x - smn.x, 2), "m")

stack = bpy.data.objects.new("Apollo13_Stack", None); bpy.context.scene.collection.objects.link(stack)
for o in (astp, lm):
    o.parent = stack

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "blend", "nasa_apollo.blend"))

# ---------- preview render ----------
cam = kit.studio((13.0, -21.0, 7.5), (73, 0, 32), lens=35, bg="#04060d")
for l in bpy.data.lights:
    l.energy *= 0.6
kit.render(os.path.join(ROOT, "renders", "nasa_apollo13_stack.png"))

# ---------- export: GLB (web/three.js, Unity glTFast) + FBX (Unity default importer) ----------
os.makedirs(OUT, exist_ok=True)
def export(objs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    vl.objects.active = objs[0]
    bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, name + ".glb"), export_format="GLB",
                              use_selection=True, export_apply=True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"), use_selection=True,
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
                             path_mode="COPY", embed_textures=True, bake_space_transform=True)
    return name

export([stack, astp, lm], "Apollo13_Stack")
export([astp], "Apollo_CSM")
export([lm], "Apollo_LM")
print(sorted((f, os.path.getsize(os.path.join(OUT, f))) for f in os.listdir(OUT)))
