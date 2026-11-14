# Shared crew animations: Idle / Walk / Carry / Work.
# All 6 rigs have the same bone names and rest orientations, so one action set drives everyone.
# Usage: bl.py reload_rig.py && bl.py anims.py   (writes blend/crew_v7_anim.blend)
import bpy, math, os
from mathutils import Matrix, Vector

ROOT = "/Users/tomokiashizawa/Claude/アプリ作成/PhysDraft-SpaceCrew3D"
assert os.path.basename(bpy.data.filepath) == "crew_v6_rigged.blend", "run reload_rig.py first"
rigs = [o for o in bpy.data.objects if o.type == "ARMATURE"]
REF = rigs[0].data.bones
BONES = ["Hips", "Spine", "Head", "Arm.L", "Arm.R", "Leg.L", "Leg.R"]
X, Y = Vector((1, 0, 0)), Vector((0, 1, 0))

for a in list(bpy.data.actions):
    bpy.data.actions.remove(a)

def rest(b):
    return REF[b].matrix_local.to_3x3()

def local_q(b, R_world):
    Rr = rest(b)
    return (Rr.inverted() @ R_world @ Rr).to_quaternion()

def rot(axis, deg):
    return Matrix.Rotation(math.radians(deg), 3, axis)

def aim(b, target):
    """World rotation turning bone b's rest direction toward target direction."""
    d = (REF[b].tail_local - REF[b].head_local).normalized()
    return d.rotation_difference(Vector(target).normalized()).to_matrix()

def mirror(v):
    return (-v[0], v[1], v[2])

def bake(name, length, pose_fn, step):
    """pose_fn(t in [0,1)) -> {bone: 3x3 world rot}, hips_up. Keys every `step` frames, loops."""
    act = bpy.data.actions.new(name); act.use_fake_user = True
    r = rigs[0]
    r.animation_data_create(); r.animation_data.action = act
    for p in r.pose.bones:
        p.rotation_mode = "QUATERNION"
    for f in list(range(0, length, step)) + [length]:
        t = (f % length) / length
        rots, up = pose_fn(t)
        for b in BONES:
            pb = r.pose.bones[b]
            pb.rotation_quaternion = local_q(b, rots.get(b, Matrix.Identity(3)))
            pb.keyframe_insert("rotation_quaternion", frame=f)
        hp = r.pose.bones["Hips"]; hp.location = (0, up, 0); hp.keyframe_insert("location", frame=f)
    return act

TAU = 2 * math.pi

def legs_walk(t, swing=26):
    s = math.cos(TAU * t)
    return {"Leg.L": rot(X, -swing * s), "Leg.R": rot(X, swing * s)}, s

def idle(t):
    b = math.sin(TAU * t)
    return {
        "Spine": rot(X, 1.5 * b),
        "Head": rot(Y, 3 * math.sin(TAU * t + 1.2)) @ rot(X, -1.0 * b),
        "Arm.L": rot(X, 3 * b) @ rot(Y, 2 * b),
        "Arm.R": rot(X, 3 * b) @ rot(Y, -2 * b),
    }, -0.006 + 0.008 * b

def walk(t):
    legs, s = legs_walk(t)
    up = abs(math.sin(TAU * t))
    legs.update({
        "Arm.L": rot(X, 32 * s), "Arm.R": rot(X, -32 * s),
        "Spine": rot(Y, 4 * s), "Head": rot(Y, -3 * s),
    })
    return legs, -0.02 + 0.05 * up

CARRY_L = (0.12, -0.85, -0.35)   # hands in front of chest, holding a box
def carry(t):
    legs, s = legs_walk(t, swing=20)
    up = abs(math.sin(TAU * t))
    legs.update({
        "Arm.L": rot(X, 3 * s) @ aim("Arm.L", CARRY_L),
        "Arm.R": rot(X, 3 * s) @ aim("Arm.R", mirror(CARRY_L)),
        "Spine": rot(X, -4) @ rot(Y, 3 * s),          # lean back slightly under the load
        "Head": rot(X, 3),
    })
    return legs, -0.03 + 0.035 * up

def work(t):
    # hammer/wrench loop: right arm strikes, left arm steadies, body leans in
    k = 0.5 - 0.5 * math.cos(TAU * t)        # 0 = raised, 1 = strike
    up_dir, down_dir = Vector((-0.20, -0.80, 0.25)), Vector((-0.20, -0.75, -0.60))
    r_dir = up_dir.lerp(down_dir, k)
    return {
        "Spine": rot(X, 10 + 3 * k),
        "Head": rot(X, 6),
        "Arm.L": aim("Arm.L", (0.25, -0.75, -0.55)),
        "Arm.R": aim("Arm.R", r_dir),
        "Leg.L": rot(X, -8), "Leg.R": rot(X, 6),
    }, -0.03 - 0.015 * k

acts = {
    "Idle": bake("Idle", 40, idle, 5),
    "Walk": bake("Walk", 20, walk, 5),
    "Carry": bake("Carry", 24, carry, 3),
    "Work": bake("Work", 16, work, 2),
}
for r in rigs:
    r.animation_data_create()
    r.animation_data.action = acts["Walk"]

scn = bpy.context.scene
scn.frame_start, scn.frame_end = 0, 19
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "blend", "crew_v7_anim.blend"))
print("actions", list(acts))
