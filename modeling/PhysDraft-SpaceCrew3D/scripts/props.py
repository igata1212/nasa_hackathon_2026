# Carry items (5) + incident props (5). Builds blend/props.blend, renders a catalog, exports GLBs.
import sys, importlib, math, os
sys.path.insert(0, "/Users/tomokiashizawa/Claude/アプリ作成/PhysDraft-SpaceCrew3D/scripts")
import kit; importlib.reload(kit)
from kit import *
import bpy

clear_scene()
C = P()
built = []

# ---------------- carry items (sized for a 1.5 m-tall crew member) ----------------
def battery():
    a = asset("Item_Battery")
    box("Body", (0, 0, 0.19), (0.30, 0.22, 0.34), C["dark"], bevel=0.04)
    box("Band", (0, 0, 0.19), (0.31, 0.23, 0.10), C["yellow"], bevel=0.02)
    box("Charge", (0, -0.118, 0.19), (0.16, 0.01, 0.045), C["led_g"], bevel=0.0)
    for x, m in ((-0.07, C["red"]), (0.07, C["metal"])):
        cyl("Terminal", (x, 0, 0.385), 0.03, 0.05, m)
    box("Handle", (0, 0, 0.40), (0.16, 0.035, 0.03), C["grey"], bevel=0.01)
    return a

def comms():
    a = asset("Item_Comms")
    box("Body", (0, 0, 0.13), (0.34, 0.24, 0.24), C["white"], bevel=0.05)
    box("Face", (0, -0.121, 0.15), (0.24, 0.01, 0.12), C["screen"], bevel=0.0)
    for x, m in ((-0.10, C["led_g"]), (-0.06, C["led_y"])):
        sph("Led", (x, -0.125, 0.06), 0.016, m, seg=12)
    cyl("Mast", (0.10, 0.05, 0.33), 0.012, 0.18, C["grey"], bevel=0)
    cyl("Dish", (0.10, 0.05, 0.44), 0.11, 0.03, C["white"], r2=0.02, rot=(-30, 0, 0))
    sph("Feed", (0.10, 0.0, 0.47), 0.02, C["blue"], seg=12)
    box("Stripe", (0, 0, 0.255), (0.345, 0.245, 0.03), C["blue"], bevel=0.01)
    return a

def toolbox():
    a = asset("Item_Toolbox")
    box("Body", (0, 0, 0.11), (0.42, 0.20, 0.20), C["orange"], bevel=0.03)
    box("Lid", (0, 0, 0.225), (0.43, 0.21, 0.04), C["red"], bevel=0.015)
    box("Latch", (0, -0.103, 0.20), (0.06, 0.01, 0.04), C["metal"], bevel=0.005)
    tube("Handle", [(-0.12, 0, 0.245), (-0.12, 0, 0.31), (0.12, 0, 0.31), (0.12, 0, 0.245)], 0.014, C["dark"])
    # wrench lying on top
    box("WrenchBar", (0.02, 0.04, 0.255), (0.22, 0.03, 0.012), C["metal"], bevel=0.004, rot=(0, 0, 18))
    cyl("WrenchHead", (0.125, 0.075, 0.255), 0.03, 0.012, C["metal"], bevel=0.003)
    return a

def extinguisher():
    a = asset("Item_Extinguisher")
    cyl("Body", (0, 0, 0.22), 0.09, 0.40, C["red"], bevel=0.04)
    sph("Top", (0, 0, 0.42), (0.09, 0.09, 0.05), C["red"])
    box("Label", (0, -0.088, 0.22), (0.10, 0.01, 0.12), C["white"], bevel=0.0)
    cyl("Valve", (0, 0, 0.48), 0.03, 0.07, C["dark"])
    box("Lever", (0.04, 0, 0.53), (0.12, 0.03, 0.015), C["metal"], bevel=0.005, rot=(0, -10, 0))
    tube("Hose", [(-0.02, 0, 0.50), (-0.09, 0, 0.50), (-0.12, 0, 0.40), (-0.11, 0, 0.30)], 0.012, C["dark"])
    cyl("Nozzle", (-0.11, 0, 0.27), 0.022, 0.06, C["dark"], r2=0.012)
    return a

def oxygen():
    a = asset("Item_Oxygen")
    cyl("Body", (0, 0, 0.26), 0.085, 0.46, C["cyan"], bevel=0.04)
    sph("Dome", (0, 0, 0.49), (0.085, 0.085, 0.06), C["cyan"])
    tor("Band", (0, 0, 0.30), 0.088, 0.012, C["white"])
    tor("Band2", (0, 0, 0.16), 0.088, 0.012, C["white"])
    cyl("Neck", (0, 0, 0.56), 0.025, 0.05, C["metal"])
    cyl("ValveWheel", (0, 0, 0.60), 0.045, 0.015, C["dark"])
    cyl("Gauge", (0, -0.05, 0.56), 0.025, 0.015, C["white"], rot=(90, 0, 0))
    text("O2", (0, -0.087, 0.24), 0.07, C["dark"], extrude=0.004)
    return a

# ---------------- incident props ----------------
def broken_pipe():
    a = asset("Incident_BrokenPipe")
    tube("PipeA", [(-0.9, 0, 0.9), (-0.08, 0, 0.9)], 0.07, C["metal"])
    tube("PipeB", [(0.08, 0, 0.92), (0.9, 0, 0.88)], 0.07, C["metal"])
    for x in (-0.9, 0.9):
        cyl("Flange", (x, 0, 0.9), 0.1, 0.04, C["grey"], rot=(0, 90, 0))
    for x in (-0.05, 0.05):
        tor("Crack", (x, 0, 0.9), 0.07, 0.012, C["dark"], rot=(0, 90, 0))
    for i, (x, z, r) in enumerate([(0.0, 1.0, 0.09), (0.05, 1.15, 0.12), (-0.04, 1.32, 0.15), (0.03, 1.5, 0.11)]):
        sph(f"Steam{i}", (x, -0.02, z), r, C["smoke"], seg=16)
    for i, (x, z) in enumerate([(-0.12, 0.82), (0.14, 0.8), (0.0, 0.76), (0.2, 0.95)]):
        ico(f"Spark{i}", (x, -0.05, z), 0.025, C["fire2"], sub=1)
    return a

def hull_breach():
    a = asset("Incident_HullBreach")
    box("Panel", (0, 0, 0.75), (1.2, 0.08, 1.2), C["wall"], bevel=0.04)
    for i, x in enumerate((-0.45, -0.15, 0.15, 0.45)):
        box(f"Hazard{i}", (x, -0.045, 0.28), (0.28, 0.01, 0.10), C["yellow"] if i % 2 == 0 else C["dark"], bevel=0)
    ico("Hole", (0.05, -0.02, 0.85), 0.26, C["space"], sub=1, noise=0.25, seed=3, scale=(1.1, 0.25, 0.9))
    for i, (x, z, rz) in enumerate([(-0.18, 1.05, 20), (0.3, 0.95, -35), (0.2, 0.62, 60), (-0.2, 0.7, -70)]):
        box(f"Shard{i}", (x, -0.07, z), (0.14, 0.02, 0.05), C["metal"], bevel=0.0, rot=(0, rz, 0))
    for i, (x, z) in enumerate([(0.0, 0.9), (0.12, 0.78), (-0.07, 0.8)]):
        sph(f"StarPeek{i}", (x, -0.05, z), 0.012, C["star"], seg=8)
    return a

def alarm():
    a = asset("Incident_Alarm")
    cyl("Base", (0, 0, 0.05), 0.12, 0.10, C["dark"])
    sph("Dome", (0, 0, 0.12), (0.10, 0.10, 0.12), C["led_r"])
    box("Reflector", (0, 0, 0.14), (0.02, 0.15, 0.10), C["metal"], bevel=0.0)
    tor("Cage", (0, 0, 0.12), 0.105, 0.008, C["dark"])
    return a

def debris():
    a = asset("Incident_Debris")
    ico("Chunk0", (0, 0, 0.25), 0.18, C["grey"], sub=1, noise=0.3, seed=1)
    ico("Chunk1", (0.35, 0.1, 0.45), 0.10, C["metal"], sub=1, noise=0.3, seed=2)
    ico("Chunk2", (-0.3, -0.05, 0.55), 0.08, C["dark"], sub=1, noise=0.3, seed=5)
    box("Plate", (0.15, -0.2, 0.7), (0.24, 0.02, 0.16), C["wall"], bevel=0.0, rot=(25, 40, 10))
    cyl("Bolt", (-0.15, 0.2, 0.85), 0.025, 0.09, C["metal"], verts=6, rot=(40, 20, 0), bevel=0)
    return a

def fire():
    a = asset("Incident_Fire")
    cyl("Scorch", (0, 0, 0.005), 0.42, 0.01, C["dark"], bevel=0)
    for i, (x, y, r, h, m) in enumerate([(0, 0, 0.22, 0.65, C["fire"]), (0.15, 0.08, 0.14, 0.45, C["fire"]),
                                        (-0.14, -0.05, 0.13, 0.40, C["fire"]), (0.02, -0.02, 0.12, 0.42, C["fire2"]),
                                        (-0.05, 0.1, 0.08, 0.28, C["fire2"])]):
        cyl(f"Flame{i}", (x, y, h / 2), r, h, m, r2=0.0, verts=16, bevel=0)
    for i, (x, z, r) in enumerate([(0.05, 0.85, 0.12), (-0.08, 1.05, 0.16)]):
        sph(f"Smoke{i}", (x, 0, z), r, C["smoke"], seg=16)
    return a

items = [battery, comms, toolbox, extinguisher, oxygen]
incidents = [broken_pipe, hull_breach, alarm, debris, fire]
for row, group in enumerate((items, incidents)):
    for i, fn in enumerate(group):
        a = fn(); end_asset()
        spacing = 0.9 if row == 0 else 1.75
        a.location = ((i - 2) * spacing, row * 2.2, 0)
        built.append(a)

bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "blend", "props.blend"))

# catalog renders: items close-up, incidents
bpy.context.scene.render.resolution_x = 1600
studio((0, -3.4, 1.1), (78, 0, 0), lens=32, floor_r=8, floor_loc=(0, 1, 0))
render(os.path.join(ROOT, "renders", "props_items.png"))
cam = bpy.context.scene.camera
cam.location = (0, -5.2, 3.6); cam.rotation_euler = (math.radians(66), 0, 0); cam.data.lens = 30
render(os.path.join(ROOT, "renders", "props_incidents.png"))

out = []
for a in built:
    folder = os.path.join(ROOT, "export", "items" if a.name.startswith("Item_") else "incidents")
    out.append((a.name, export_asset(a, folder)[1]))
print(out)
