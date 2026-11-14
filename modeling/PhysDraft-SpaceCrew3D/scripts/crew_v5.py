# Chibi crew v3: 3 heads tall, A-pose, built from concepts/proportions.svg (C).
# Each moving part is its own object with its origin at the joint (shoulder/hip/neck)
# and Blender-style .L/.R names, so it can be bound to an armature later.
import bpy, math, os
from mathutils import Vector

ROOT = "/Users/tomokiashizawa/Claude/アプリ作成/PhysDraft-SpaceCrew3D"
VER = "crew_v5"

scn = bpy.context.scene
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.lights, bpy.data.cameras):
    for d in list(coll):
        coll.remove(d)

# ---------- proportions (meters-ish) ----------
H = 1.5                 # total height (top of head)
HEADS = 3.0
h = H / HEADS           # head height 0.5
body_h = H - h          # 1.0
torso_h = body_h * 0.52 # 0.52
NECK_Z = body_h         # 1.0
HIP_Z = NECK_Z - torso_h * 0.86
HEAD_Z = H - h / 2      # 1.25
ARM_DEG = 50            # A-pose: degrees below horizontal

def mat(name, color, rough=0.5, metal=0.0, emit=None, emit_str=0.0, coat=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*color, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if coat:
        b.inputs["Coat Weight"].default_value = coat
    if emit:
        b.inputs["Emission Color"].default_value = (*emit, 1)
        b.inputs["Emission Strength"].default_value = emit_str
    return m

def srgb(hexstr):
    c = [int(hexstr[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    return tuple((v / 12.92) if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in c)

WHITE  = mat("SuitWhite", srgb("#eef1f6"), rough=0.55)
ORANGE = mat("SafetyOrange", srgb("#f2661c"), rough=0.45)
GREY   = mat("SuitGrey", srgb("#596173"), rough=0.6)
VISOR  = mat("Visor", srgb("#14254a"), rough=0.05, metal=0.5, coat=1.0)
GOLD   = mat("VisorRim", srgb("#d9a441"), rough=0.3, metal=1.0)
LED_G  = mat("LedGreen", (0.1, 1, 0.4), emit=(0.1, 1, 0.4), emit_str=5)
LED_R  = mat("LedRed", (1, 0.15, 0.1), emit=(1, 0.15, 0.1), emit_str=5)
SKIN   = mat("Skin", srgb("#f2b48f"), rough=0.6)
HAIR   = mat("Hair", srgb("#3a2a24"), rough=0.45)
EYE    = mat("Eye", srgb("#1d2333"), rough=0.2)
SHINE  = mat("EyeShine", (1, 1, 1), emit=(1, 1, 1), emit_str=2)
CHEEK  = mat("Cheek", srgb("#ff8a8a"), rough=0.7)
NAVY   = mat("CrewNavy", srgb("#2f4f9c"), rough=0.6)
# --- roles: main color for suit accents / crew jumpsuit ---
ROLES = [
    # key,  label,   main,      hair,      skin
    ("Nav",   "航法", "#2f7de1", "#3a2a24", "#f2b48f"),
    ("Pilot", "操縦", "#e2433a", "#16141a", "#e9a27a"),
    ("Sys",   "機体", "#f2b417", "#7a3b1e", "#c98a62"),
]
ACC = MAIN = ORANGE  # overwritten per character

SHOE   = mat("Shoe", srgb("#2b2d33"), rough=0.5)

# ---------- helpers ----------
def _finish(o, m, parent, bevel=0.0, smooth=True):
    o.data.materials.append(m)
    if smooth:
        o.data.shade_smooth()
    if bevel:
        bv = o.modifiers.new("bevel", "BEVEL"); bv.width = bevel; bv.segments = 6
        bv.limit_method = "NONE"
    o.parent = parent
    return o

def set_origin(o, point):
    bpy.context.scene.cursor.location = point
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.context.view_layer.update()

def sphere(name, loc, scale, m, parent):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=24, radius=1, location=loc)
    o = bpy.context.object; o.name = name; o.scale = scale
    bpy.ops.object.transform_apply(scale=True)
    return _finish(o, m, parent)

def rbox(name, loc, size, m, parent, round_=0.4):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.object; o.name = name; o.scale = size
    bpy.ops.object.transform_apply(scale=True)
    return _finish(o, m, parent, bevel=min(size) * round_)

def capsule(name, a, b, r, m, parent):
    """Cylinder from a to b with rounded ends; origin at a (the joint)."""
    a, b = Vector(a), Vector(b)
    d = b - a
    bpy.ops.mesh.primitive_cylinder_add(vertices=40, radius=r, depth=d.length, location=(a + b) / 2)
    o = bpy.context.object; o.name = name
    o.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    bpy.ops.object.transform_apply(rotation=True)
    _finish(o, m, None, bevel=r * 0.95)
    set_origin(o, a)
    o.parent = parent
    return o

def torus(name, loc, R, r, m, parent, rot=(0, 0, 0), scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, location=loc, rotation=rot,
                                     major_segments=64, minor_segments=16)
    o = bpy.context.object; o.name = name; o.scale = scale
    bpy.ops.object.transform_apply(scale=True, rotation=True)
    return _finish(o, m, parent)

def root(name, x):
    o = bpy.data.objects.new(name, None); o.location = (x, 0, 0)
    o.empty_display_type = "PLAIN_AXES"
    scn.collection.objects.link(o)
    return o

def body(p, suit):
    """Shared torso + limbs. All coordinates relative to root p (built at x=0, then moved)."""
    tw = h * (0.80 if suit else 0.66)       # torso width
    td = tw * (0.78 if suit else 0.72)      # torso depth
    lr = h * (0.15 if suit else 0.11)       # limb radius
    clothes = WHITE if suit else MAIN
    torso = rbox("Torso", (0, 0, NECK_Z - torso_h / 2), (tw, td, torso_h), clothes, p, round_=0.42)
    set_origin(torso, (0, 0, HIP_Z))
    # arms (A-pose)
    arm_len = torso_h * 0.82
    for side, d in (("L", 1), ("R", -1)):
        sh = Vector((d * tw * 0.40, 0, NECK_Z - torso_h * 0.20))
        hand = sh + Vector((d * math.cos(math.radians(ARM_DEG)), 0, -math.sin(math.radians(ARM_DEG)))) * arm_len
        arm = capsule(f"Arm.{side}", sh, hand, lr, clothes, p)
        if suit:  # role-colored shoulder pad, readable from above
            pad = sphere(f"Shoulder.{side}", sh + Vector((d * lr * 0.2, 0, lr * 0.25)), (lr * 1.25, lr * 1.2, lr * 0.9), ACC, p)
            pad.parent = arm; pad.matrix_parent_inverse = arm.matrix_world.inverted()
        hr = lr * 1.30
        hd = sphere(f"Hand.{side}", hand + Vector((d * hr * 0.3, 0, -hr * 0.3)), (hr, hr, hr), ACC if suit else SKIN_, p)
        set_origin(hd, hand); hd.parent = arm; hd.matrix_parent_inverse = arm.matrix_world.inverted()
    # legs
    for side, d in (("L", 1), ("R", -1)):
        hip = Vector((d * tw * 0.24, 0, HIP_Z + 0.04))
        ankle = Vector((d * tw * 0.24, 0, lr * 0.9))
        leg = capsule(f"Leg.{side}", hip, ankle, lr * 1.05, clothes, p)
        bt = sphere(f"Boot.{side}", (d * tw * 0.25, -lr * 0.35, lr * 0.62), (lr * 1.35, lr * 1.7, lr * 0.75),
                    GREY if suit else SHOE, p)
        set_origin(bt, ankle); bt.parent = leg; bt.matrix_parent_inverse = leg.matrix_world.inverted()
    return tw, td

def astronaut(x, key="A"):
    p = root(f"Astronaut_{key}", 0)
    tw, td = body(p, True)
    front = -td / 2
    rbox("ChestPanel", (0, front - 0.01, NECK_Z - torso_h * 0.36), (tw * 0.45, 0.06, torso_h * 0.20), GREY, p, round_=0.3)
    sphere("Led.G", (-tw * 0.09, front - 0.045, NECK_Z - torso_h * 0.36), (0.022,) * 3, LED_G, p)
    sphere("Led.R", (tw * 0.09, front - 0.045, NECK_Z - torso_h * 0.36), (0.022,) * 3, LED_R, p)
    torus("Belt", (0, 0, NECK_Z - torso_h * 0.80), 1, 0.06, ACC, p, scale=(tw * 0.52, td * 0.54, 0.45))
    rbox("Backpack", (0, td / 2 + 0.07, NECK_Z - torso_h * 0.42), (tw * 0.85, 0.16, torso_h * 0.72), WHITE, p, round_=0.35)
    rbox("BackpackStripe", (0, td / 2 + 0.15, NECK_Z - torso_h * 0.42), (tw * 0.86, 0.02, 0.05), ACC, p, round_=0.2)
    torus("NeckRing", (0, 0, NECK_Z + 0.005), tw * 0.36, 0.035, GREY, p)
    hr = h * 0.5
    helmet = sphere("Head", (0, 0, HEAD_Z), (hr * 1.06, hr, hr), WHITE, p)
    set_origin(helmet, (0, 0, NECK_Z))
    parts = [
        sphere("HelmetBand", (0, hr * 0.12, HEAD_Z + hr * 0.02), (hr * 0.34, hr * 1.0, hr * 1.04), ACC, p),
        sphere("Visor", (0, -hr * 0.42, HEAD_Z + hr * 0.02), (hr * 0.80, hr * 0.62, hr * 0.64), VISOR, p),
        torus("VisorRim", (0, -hr * 0.80, HEAD_Z + hr * 0.02), hr * 0.66, 0.022, GOLD, p,
              rot=(math.radians(90), 0, 0), scale=(1.12, 0.92, 1)),
        sphere("VisorShine", (-hr * 0.32, -hr * 1.01, HEAD_Z + hr * 0.24), (hr * 0.13, 0.008, hr * 0.07), SHINE, p),
        capsule("Antenna", (hr * 0.6, 0, HEAD_Z + hr * 0.62), (hr * 0.6, 0, HEAD_Z + hr * 1.35), 0.012, GREY, p),
        sphere("AntennaTip", (hr * 0.6, 0, HEAD_Z + hr * 1.40), (0.035,) * 3, ACC, p),
    ]
    for o in parts:   # head details follow the head
        o.parent = helmet; o.matrix_parent_inverse = helmet.matrix_world.inverted()
    p.location.x = x
    return p

def civilian(x, key="C"):
    p = root(f"Civilian_{key}", 0)
    tw, td = body(p, False)
    front = -td / 2
    rbox("Badge", (tw * 0.20, front - 0.005, NECK_Z - torso_h * 0.28), (tw * 0.2, 0.03, torso_h * 0.09), WHITE, p, round_=0.3)
    torus("Belt", (0, 0, NECK_Z - torso_h * 0.80), 1, 0.05, SHOE, p, scale=(tw * 0.52, td * 0.55, 0.40))
    torus("Collar", (0, 0, NECK_Z + 0.005), tw * 0.30, 0.03, WHITE, p)
    hr = h * 0.5 * 0.98
    head = sphere("Head", (0, 0, HEAD_Z), (hr, hr * 0.95, hr * 0.95), SKIN_, p)
    set_origin(head, (0, 0, NECK_Z))
    parts = [
        sphere("Hair", (0, hr * 0.10, HEAD_Z + hr * 0.28), (hr * 1.06, hr * 1.0, hr * 0.80), HAIR_, p),
        sphere("Fringe", (-hr * 0.22, -hr * 0.70, HEAD_Z + hr * 0.42), (hr * 0.62, hr * 0.26, hr * 0.20), HAIR_, p),
        sphere("Mouth", (0, -hr * 0.93, HEAD_Z - hr * 0.40), (hr * 0.09, 0.01, hr * 0.03), EYE, p),
    ]
    for side, d in (("L", 1), ("R", -1)):
        parts += [
            sphere(f"Ear.{side}", (d * hr * 0.98, 0.02, HEAD_Z), (hr * 0.13, hr * 0.10, hr * 0.19), SKIN_, p),
            sphere(f"Eye.{side}", (d * hr * 0.36, -hr * 0.86, HEAD_Z - hr * 0.08), (hr * 0.11, hr * 0.06, hr * 0.17), EYE, p),
            sphere(f"EyeShine.{side}", (d * hr * 0.36 - hr * 0.04, -hr * 0.93, HEAD_Z), (hr * 0.04, 0.01, hr * 0.05), SHINE, p),
            sphere(f"Cheek.{side}", (d * hr * 0.58, -hr * 0.74, HEAD_Z - hr * 0.30), (hr * 0.13, 0.03, hr * 0.07), CHEEK, p),
        ]
    for o in parts:
        o.parent = head; o.matrix_parent_inverse = head.matrix_world.inverted()
    p.location.x = x
    return p

HAIR_, SKIN_ = HAIR, SKIN
for i, (key, label, main, hair, skin) in enumerate(ROLES):
    MAIN = ACC = mat(f"Role_{key}", srgb(main), rough=0.5)
    HAIR_ = mat(f"Hair_{key}", srgb(hair), rough=0.45)
    SKIN_ = mat(f"Skin_{key}", srgb(skin), rough=0.6)
    cx = (i - 1) * 2.0
    astronaut(cx - 0.44, key)
    civilian(cx + 0.44, key)

# ---------- stage / lights / cameras ----------
bpy.ops.mesh.primitive_cylinder_add(vertices=96, radius=3.7, depth=0.05, location=(0, 0.2, -0.025))
deck = bpy.context.object; deck.name = "Deck"
deck.data.materials.append(mat("Deck", srgb("#3a4150"), rough=0.35, metal=0.3))
bpy.ops.mesh.primitive_torus_add(major_radius=3.7, minor_radius=0.022, location=(0, 0.2, 0))
bpy.context.object.name = "DeckGlow"
bpy.context.object.data.materials.append(mat("DeckGlow", (0.2, 0.7, 1), emit=(0.2, 0.7, 1), emit_str=3))

world = bpy.data.worlds.get("World") or bpy.data.worlds.new("World"); scn.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (*srgb("#1e2235"), 1)

def light(name, loc, rot, energy, color=(1, 1, 1), size=3):
    d = bpy.data.lights.new(name, "AREA"); d.energy = energy; d.color = color; d.size = size
    o = bpy.data.objects.new(name, d); o.location = loc
    o.rotation_euler = [math.radians(a) for a in rot]
    scn.collection.objects.link(o)

light("Key", (-3.5, -4.2, 4.5), (50, 0, -38), 650, size=5)
light("Fill", (4.0, -3.5, 2.0), (70, 0, 50), 280, color=(0.65, 0.78, 1.0))
light("Rim", (0, 4.2, 3.4), (-55, 0, 180), 650, color=(0.5, 0.8, 1.0))

def camera(name, loc, rot, lens):
    cd = bpy.data.cameras.new(name); cd.lens = lens
    c = bpy.data.objects.new(name, cd); c.location = loc
    c.rotation_euler = [math.radians(a) for a in rot]
    scn.collection.objects.link(c); return c

cam_front = camera("CamFront", (0, -9.6, 0.80), (90, 0, 0), 50)
cam_34 = camera("Cam34", (4.2, -8.2, 2.6), (79, 0, 26), 50)
cam_top = camera("CamTop", (0, -6.4, 6.0), (49, 0, 0), 50)

scn.render.engine = "BLENDER_EEVEE"
scn.render.resolution_x, scn.render.resolution_y = 1600, 1000
scn.view_settings.view_transform = "Standard"
scn.view_settings.look = "None"
for cam, tag in ((cam_front, "front"), (cam_34, "34"), (cam_top, "top")):
    scn.camera = cam
    scn.render.filepath = os.path.join(ROOT, "renders", f"{VER}_{tag}.png")
    bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "blend", f"{VER}.blend"))
print("ok", [o.name for o in bpy.data.objects if o.parent and o.parent.name in ("Astronaut", "Civilian")][:40])
