# Chibi crew v1: astronaut (suit) + civilian crew member, rendered side by side.
import bpy, math, os

ROOT = "/Users/tomokiashizawa/Claude/アプリ作成/PhysDraft-SpaceCrew3D"

# ---------- reset ----------
scn = bpy.context.scene
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.lights, bpy.data.cameras):
    for d in list(coll):
        coll.remove(d)

def mat(name, color, rough=0.5, metal=0.0, emit=None, emit_str=0.0, coat=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
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

def finish(o, m, parent, smooth=True, bevel=0.0):
    o.data.materials.append(m)
    if smooth:
        o.data.shade_smooth()
    if bevel:
        bv = o.modifiers.new("bevel", "BEVEL"); bv.width = bevel; bv.segments = 4
    o.parent = parent
    return o

def sphere(name, loc, scale, m, parent, r=1.0):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=24, radius=r, location=loc)
    o = bpy.context.object; o.name = name; o.scale = scale
    return finish(o, m, parent)

def cyl(name, loc, r, depth, m, parent, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=40, radius=r, depth=depth, location=loc, rotation=rot)
    o = bpy.context.object; o.name = name
    return finish(o, m, parent, bevel=0.03)

def box(name, loc, size, m, parent, bevel=0.06):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.object; o.name = name; o.scale = size
    bpy.ops.object.transform_apply(scale=True)
    return finish(o, m, parent, bevel=bevel)

def torus(name, loc, R, r, m, parent, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, location=loc, rotation=rot,
                                     major_segments=64, minor_segments=16)
    o = bpy.context.object; o.name = name
    return finish(o, m, parent)

def empty(name, loc):
    o = bpy.data.objects.new(name, None); o.location = loc
    scn.collection.objects.link(o); return o

# ---------- palette ----------
WHITE  = mat("SuitWhite",  (0.86, 0.87, 0.90), rough=0.55)
ORANGE = mat("SafetyOrange", (0.95, 0.33, 0.06), rough=0.45)
GREY   = mat("SuitGrey",   (0.25, 0.27, 0.31), rough=0.6)
VISOR  = mat("Visor",      (0.02, 0.05, 0.12), rough=0.05, metal=0.6, coat=1.0)
GOLD   = mat("VisorRim",   (0.85, 0.62, 0.22), rough=0.3, metal=1.0)
LED_G  = mat("LedGreen",   (0.1, 1.0, 0.4), emit=(0.1, 1.0, 0.4), emit_str=4)
LED_R  = mat("LedRed",     (1.0, 0.15, 0.1), emit=(1.0, 0.15, 0.1), emit_str=4)
SKIN   = mat("Skin",       (0.93, 0.62, 0.46), rough=0.6)
HAIR   = mat("Hair",       (0.10, 0.07, 0.06), rough=0.45)
EYE    = mat("Eye",        (0.02, 0.02, 0.03), rough=0.2)
SHINE  = mat("EyeShine",   (1, 1, 1), emit=(1, 1, 1), emit_str=2)
CHEEK  = mat("Cheek",      (1.0, 0.45, 0.45), rough=0.7)
NAVY   = mat("CrewNavy",   (0.06, 0.12, 0.30), rough=0.6)
SHOE   = mat("Shoe",       (0.12, 0.12, 0.14), rough=0.5)

# ---------- shared chibi body ----------
def limbs(p, x, sleeve, hand, leg, boot, stocky=1.0):
    # arms: slightly outward, stubby
    for s in (-1, 1):
        a = sphere(f"Arm_{s}", (x + s * 0.48 * stocky, 0, 0.80), (0.14 * stocky, 0.14 * stocky, 0.26), sleeve, p)
        a.rotation_euler = (0, s * math.radians(-28), 0)
        sphere(f"Hand_{s}", (x + s * 0.60 * stocky, -0.02, 0.56), (0.12 * stocky,) * 3, hand, p)
        cyl(f"Leg_{s}", (x + s * 0.17, 0, 0.30), 0.13 * stocky, 0.32, leg, p)
        sphere(f"Boot_{s}", (x + s * 0.18, -0.05, 0.10), (0.17 * stocky, 0.22 * stocky, 0.11), boot, p)

def astronaut(x):
    p = empty("Astronaut", (0, 0, 0))
    sphere("Torso", (x, 0, 0.76), (0.42, 0.36, 0.42), WHITE, p)
    box("ChestPanel", (x, -0.33, 0.80), (0.30, 0.10, 0.20), GREY, p, bevel=0.04)
    sphere("LedA", (x - 0.08, -0.385, 0.84), (0.035,) * 3, LED_G, p)
    sphere("LedB", (x + 0.06, -0.385, 0.84), (0.035,) * 3, LED_R, p)
    box("Belt", (x, 0, 0.52), (0.70, 0.62, 0.07), ORANGE, p, bevel=0.03)
    box("Backpack", (x, 0.40, 0.88), (0.62, 0.28, 0.62), WHITE, p, bevel=0.10)
    box("BackpackStripe", (x, 0.545, 0.88), (0.64, 0.02, 0.10), ORANGE, p, bevel=0.0)
    torus("NeckRing", (x, 0, 1.10), 0.34, 0.07, GREY, p)
    sphere("Helmet", (x, 0, 1.62), (0.62, 0.60, 0.58), WHITE, p)
    sphere("Visor", (x, -0.30, 1.62), (0.46, 0.34, 0.36), VISOR, p)
    torus("VisorRim", (x, -0.45, 1.62), 0.40, 0.035, GOLD, p, rot=(math.radians(90), 0, 0)).scale = (1.12, 1, 0.80)
    sphere("VisorShine", (x - 0.18, -0.62, 1.74), (0.08, 0.02, 0.05), SHINE, p)
    cyl("Antenna", (x + 0.36, 0.10, 2.20), 0.025, 0.40, GREY, p)
    sphere("AntennaTip", (x + 0.36, 0.10, 2.42), (0.06,) * 3, ORANGE, p)
    limbs(p, x, WHITE, ORANGE, WHITE, GREY, stocky=1.15)
    return p

def civilian(x):
    p = empty("Civilian", (0, 0, 0))
    sphere("Torso", (x, 0, 0.74), (0.34, 0.28, 0.38), NAVY, p)
    torus("Collar", (x, 0, 1.06), 0.20, 0.06, ORANGE, p)
    box("Badge", (x + 0.14, -0.27, 0.86), (0.12, 0.04, 0.08), ORANGE, p, bevel=0.02)
    box("Belt", (x, 0, 0.52), (0.56, 0.50, 0.06), SHOE, p, bevel=0.025)
    sphere("Head", (x, 0, 1.58), (0.58, 0.55, 0.55), SKIN, p)
    sphere("Hair", (x, 0.10, 1.74), (0.63, 0.55, 0.50), HAIR, p)
    sphere("Bang", (x - 0.12, -0.38, 1.88), (0.34, 0.16, 0.14), HAIR, p).rotation_euler = (0, math.radians(-14), 0)
    for s in (-1, 1):
        sphere(f"Ear_{s}", (x + s * 0.56, 0.02, 1.55), (0.08, 0.06, 0.11), SKIN, p)
        sphere(f"Eye_{s}", (x + s * 0.19, -0.50, 1.55), (0.075, 0.04, 0.11), EYE, p)
        sphere(f"EyeShine_{s}", (x + s * 0.19 - 0.025, -0.535, 1.60), (0.025, 0.01, 0.03), SHINE, p)
        sphere(f"Cheek_{s}", (x + s * 0.32, -0.44, 1.42), (0.08, 0.03, 0.05), CHEEK, p)
    sphere("Mouth", (x, -0.545, 1.40), (0.05, 0.015, 0.022), EYE, p)
    limbs(p, x, NAVY, SKIN, NAVY, SHOE, stocky=0.9)
    return p

astronaut(-0.95)
civilian(0.95)

# ---------- stage ----------
bpy.ops.mesh.primitive_cylinder_add(vertices=96, radius=2.6, depth=0.08, location=(0, 0.2, -0.04))
floor = bpy.context.object; floor.name = "Deck"
floor.data.materials.append(mat("Deck", (0.20, 0.22, 0.27), rough=0.35, metal=0.3))
bpy.ops.mesh.primitive_torus_add(major_radius=2.6, minor_radius=0.03, location=(0, 0.2, 0.0))
bpy.context.object.data.materials.append(mat("DeckGlow", (0.2, 0.7, 1.0), emit=(0.2, 0.7, 1.0), emit_str=3))

world = bpy.data.worlds.new("World"); scn.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.012, 0.016, 0.04, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.0

def light(name, kind, loc, rot, energy, color=(1, 1, 1), size=3):
    d = bpy.data.lights.new(name, kind); d.energy = energy; d.color = color
    if kind == "AREA": d.size = size
    o = bpy.data.objects.new(name, d); o.location = loc
    o.rotation_euler = [math.radians(a) for a in rot]
    scn.collection.objects.link(o)

light("Key",  "AREA", (-3.0, -4.0, 4.5), (50, 0, -35), 600)
light("Fill", "AREA", (4.0, -3.0, 2.0), (70, 0, 50), 300, color=(0.6, 0.75, 1.0))
light("Rim",  "AREA", (0.0, 4.0, 3.5), (-55, 0, 180), 700, color=(0.5, 0.8, 1.0))

cam_d = bpy.data.cameras.new("Cam"); cam_d.lens = 55
cam = bpy.data.objects.new("Cam", cam_d); scn.collection.objects.link(cam)
cam.location = (0.6, -8.6, 2.1)
cam.rotation_euler = (math.radians(85), 0, math.radians(4))
scn.camera = cam

for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
    try:
        scn.render.engine = eng; break
    except TypeError:
        pass
scn.render.resolution_x, scn.render.resolution_y = 1600, 1000
scn.view_settings.view_transform = "Standard"
scn.view_settings.look = "None"
scn.render.filepath = os.path.join(ROOT, "renders", "crew_v2.png")
bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "blend", "crew_v2.blend"))
print("rendered", scn.render.filepath, scn.render.engine)
