# Rig the 6 crew characters in the open crew_v5 scene: armature per character,
# rigid bone-parenting of parts, a looping "Walk" action, preview render, GLB export.
import bpy, math, os, re
from mathutils import Matrix, Vector, Quaternion

ROOT = "/Users/tomokiashizawa/Claude/アプリ作成/PhysDraft-SpaceCrew3D"
SRC = os.path.join(ROOT, "blend", "crew_v5.blend")
if os.path.basename(bpy.data.filepath) != "crew_v5.blend" or "Astronaut_Nav_Rig" in bpy.data.objects:
    bpy.ops.wm.open_mainfile(filepath=SRC)

scn = bpy.context.scene
vl = bpy.context.view_layer
WALK_LEN = 20          # frames per 2 steps (24 fps -> ~0.83 s)
LEG_SWING, ARM_SWING = 26, 32
CHARS = [o for o in bpy.data.objects if o.type == "EMPTY" and re.match(r"(Astronaut|Civilian)_", o.name)]

def base(name):
    return re.sub(r"\.\d{3}$", "", name)

def ensure_object_mode():
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")

def world_rot_to_local(bone, axis, deg):
    R_rest = bone.matrix_local.to_3x3()
    R_w = Matrix.Rotation(math.radians(deg), 3, axis)
    return (R_rest.inverted() @ R_w @ R_rest).to_quaternion()

def rig(root):
    vl.update()
    origin = root.matrix_world.translation.copy()
    kids = {base(c.name): c for c in root.children}
    W = lambda o: o.matrix_world.translation - origin
    child = lambda o, b: next(c for c in o.children if base(c.name) == b)
    joints = {
        "Hips": W(kids["Torso"]),
        "Neck": W(kids["Head"]),
        "Shoulder.L": W(kids["Arm.L"]), "Hand.L": W(child(kids["Arm.L"], "Hand.L")),
        "Shoulder.R": W(kids["Arm.R"]), "Hand.R": W(child(kids["Arm.R"], "Hand.R")),
        "Hip.L": W(kids["Leg.L"]), "Ankle.L": W(child(kids["Leg.L"], "Boot.L")),
        "Hip.R": W(kids["Leg.R"]), "Ankle.R": W(child(kids["Leg.R"], "Boot.R")),
    }
    top = joints["Neck"] + Vector((0, 0, 0.5))

    ensure_object_mode()
    data = bpy.data.armatures.new(f"{root.name}_Armature")
    rig_obj = bpy.data.objects.new(f"{root.name}_Rig", data)
    scn.collection.objects.link(rig_obj)
    rig_obj.parent = root
    rig_obj.show_in_front = True
    bpy.ops.object.select_all(action="DESELECT")
    vl.objects.active = rig_obj; rig_obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = data.edit_bones
    def bone(name, head, tail, parent=None):
        b = eb.new(name); b.head = head; b.tail = tail; b.roll = 0
        if parent: b.parent = eb[parent]; b.use_connect = False
        return b
    hips = joints["Hips"]
    bone("Hips", hips, hips + Vector((0, 0, 0.12)))
    bone("Spine", hips + Vector((0, 0, 0.12)), joints["Neck"], "Hips")
    bone("Head", joints["Neck"], top, "Spine")
    for s in "LR":
        bone(f"Arm.{s}", joints[f"Shoulder.{s}"], joints[f"Hand.{s}"], "Spine")
        bone(f"Leg.{s}", joints[f"Hip.{s}"], joints[f"Ankle.{s}"], "Hips")
    bpy.ops.object.mode_set(mode="OBJECT")
    vl.update()

    # rigid-bind every direct part to a bone (children of arms/legs/head follow their parent)
    target = {"Arm.L": "Arm.L", "Arm.R": "Arm.R", "Leg.L": "Leg.L", "Leg.R": "Leg.R", "Head": "Head"}
    for name, o in kids.items():
        mw = o.matrix_world.copy()
        o.parent = rig_obj; o.parent_type = "BONE"; o.parent_bone = target.get(name, "Spine")
        o.matrix_parent_inverse = Matrix.Identity(4)
        vl.update()
        o.matrix_world = mw
    # readable, unique part names for the game
    for o in rig_obj.children_recursive:
        o.name = f"{root.name}.{base(o.name)}"
    return rig_obj

def walk(rig_obj):
    rig_obj.animation_data_create()
    act = bpy.data.actions.new(f"{rig_obj.parent.name}_Walk")
    rig_obj.animation_data.action = act
    pb, bones = rig_obj.pose.bones, rig_obj.data.bones
    for p in pb:
        p.rotation_mode = "QUATERNION"
    X, Y = Vector((1, 0, 0)), Vector((0, 1, 0))
    # phase k: 0 -> left leg forward, 0.5 -> right leg forward
    for f, k in ((0, 0), (5, 0.25), (10, 0.5), (15, 0.75), (20, 1.0)):
        s = math.cos(2 * math.pi * k)                 # +1 left forward, -1 right forward
        up = abs(math.sin(2 * math.pi * k))           # passing pose = highest
        keys = {
            "Leg.L": (X, -LEG_SWING * s), "Leg.R": (X, LEG_SWING * s),
            "Arm.L": (X, ARM_SWING * s), "Arm.R": (X, -ARM_SWING * s),
            "Spine": (Y, 4 * s), "Head": (Y, -3 * s),
        }
        for name, (axis, deg) in keys.items():
            pb[name].rotation_quaternion = world_rot_to_local(bones[name], axis, deg)
            pb[name].keyframe_insert("rotation_quaternion", frame=f)
        pb["Hips"].location = (0, -0.02 + 0.05 * up, 0)  # bone Y == world up for the vertical hip bone
        pb["Hips"].keyframe_insert("location", frame=f)
    return act

rigs = [rig(r) for r in CHARS]
for r in rigs:
    walk(r)

scn.render.fps = 24
scn.frame_start, scn.frame_end = 0, WALK_LEN - 1
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "blend", "crew_v6_rigged.blend"))

# ---------- preview frames ----------
out = os.path.join(ROOT, "renders", "walk")
os.makedirs(out, exist_ok=True)
scn.camera = bpy.data.objects["Cam34"]
scn.render.resolution_x, scn.render.resolution_y = 960, 600
scn.render.image_settings.file_format = "PNG"
scn.render.filepath = os.path.join(out, "f_")
bpy.ops.render.render(animation=True)

# ---------- GLB per character (moved to origin for export) ----------
exp = os.path.join(ROOT, "export"); os.makedirs(exp, exist_ok=True)
for r in rigs:
    root = r.parent
    keep = root.location.copy(); root.location = (0, 0, 0); vl.update()
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for o in root.children_recursive:
        o.select_set(True)
    vl.objects.active = r
    bpy.ops.export_scene.gltf(filepath=os.path.join(exp, f"{root.name}.glb"), export_format="GLB",
                              use_selection=True, export_apply=True, export_animations=True,
                              export_animation_mode="ACTIONS")
    root.location = keep
vl.update()
print("rigs", [r.name for r in rigs], "bones", [b.name for b in rigs[0].data.bones])
