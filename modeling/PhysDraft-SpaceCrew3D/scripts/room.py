# Ship interior: room shell + role stations. Composes a playable-looking bridge with crew and props,
# renders game-view stills, exports each station + the shell as GLB. Writes blend/room.blend.
import sys, importlib, math, os, random
sys.path.insert(0, "/Users/tomokiashizawa/Claude/アプリ作成/PhysDraft-SpaceCrew3D/scripts")
import kit; importlib.reload(kit)
from kit import *
import bpy
from mathutils import Vector

clear_scene()
C = P()
ROLE = dict(nav=mat("K_RoleNav", "#2f7de1", 0.45), pilot=mat("K_RolePilot", "#e2433a", 0.45),
            sys=mat("K_RoleSys", "#f2b417", 0.45))
W, D = 10.0, 7.0          # room width (x) and depth (y)
BACK_H, SIDE_H = 2.4, 1.0

# ---------------- room shell ----------------
def room_shell():
    a = asset("Room_Shell")
    box("Floor", (0, 0, -0.05), (W, D, 0.1), C["floor"], bevel=0.02)
    for i in range(1, int(W)):
        box(f"GridX{i}", (-W / 2 + i, 0, 0.002), (0.025, D - 0.2, 0.004), C["floor2"], bevel=0)
    for j in range(1, int(D)):
        box(f"GridY{j}", (0, -D / 2 + j, 0.002), (W - 0.2, 0.025, 0.004), C["floor2"], bevel=0)
    t = 0.2
    # back wall with a big window (x in [-2, 2], z in [0.7, 2.0])
    by = D / 2 + t / 2
    box("BackL", (-(W / 2 + 2) / 2 - 0.0, by, BACK_H / 2), ((W / 2 - 2), t, BACK_H), C["wall"], bevel=0.03)
    box("BackR", ((W / 2 + 2) / 2, by, BACK_H / 2), ((W / 2 - 2), t, BACK_H), C["wall"], bevel=0.03)
    box("BackLow", (0, by, 0.35), (4.0, t, 0.7), C["wall"], bevel=0.03)
    box("BackTop", (0, by, 2.2), (4.0, t, 0.4), C["wall"], bevel=0.03)
    box("WinFrameB", (0, by - 0.12, 0.72), (4.1, 0.08, 0.06), C["grey"], bevel=0.01)
    box("WinFrameT", (0, by - 0.12, 1.98), (4.1, 0.08, 0.06), C["grey"], bevel=0.01)
    for x in (-2.02, -0.67, 0.67, 2.02):
        box("WinMullion", (x, by - 0.12, 1.35), (0.06, 0.08, 1.3), C["grey"], bevel=0.01)
    plane("Glass", (0, by, 1.35), (4.0, 1.3), C["glass"], rot=(90, 0, 0))
    # space backdrop + moon behind the window
    plane("Space", (0, by + 3.0, 1.35), (14, 8), C["space"], rot=(90, 0, 0))
    rnd = random.Random(7)
    for i in range(60):
        sph(f"Star{i}", (rnd.uniform(-6, 6), by + 2.9, rnd.uniform(-1.5, 4.5)), rnd.uniform(0.01, 0.035), C["star"], seg=8)
    sph("Moon", (2.6, by + 2.6, 1.1), 1.1, C["moon"], seg=48)
    for i, (x, z, r) in enumerate([(2.2, 1.5, 0.22), (3.0, 0.8, 0.16), (2.5, 0.7, 0.1), (3.2, 1.5, 0.12)]):
        sph(f"Crater{i}", (x, by + 1.55, z), (r, 0.05, r), C["moon2"], seg=16)
    # side walls (low so the top-down camera sees in) with trim
    for s, name in ((-1, "WallL"), (1, "WallR")):
        x = s * (W / 2 + t / 2)
        box(name, (x, 0, SIDE_H / 2), (t, D + 2 * t, SIDE_H), C["wall"], bevel=0.03)
        box(name + "Trim", (x, 0, SIDE_H + 0.02), (t + 0.04, D + 2 * t + 0.04, 0.05), C["grey"], bevel=0.01)
    box("BackTrim", (0, by, BACK_H + 0.02), (W + 0.44, t + 0.04, 0.05), C["grey"], bevel=0.01)
    # front lip (no wall: camera side)
    box("FrontLip", (0, -D / 2 - 0.05, 0.05), (W + 0.4, 0.1, 0.1), C["grey"], bevel=0.02)
    # airlock zone hazard stripes on the floor near the right wall
    for i in range(6):
        box(f"FloorHazard{i}", (W / 2 - 0.35, -1.25 + i * 0.5, 0.004), (0.5, 0.22, 0.006),
            C["yellow"] if i % 2 == 0 else C["dark"], bevel=0)
    return a

def airlock():
    a = asset("Station_Airlock")
    for y in (-0.75, 0.75):
        box("Jamb", (0, y, 1.05), (0.4, 0.2, 2.1), C["grey"], bevel=0.04)
    box("Header", (0, 0, 2.15), (0.4, 1.7, 0.3), C["grey"], bevel=0.04)
    box("Door", (0, 0, 1.0), (0.12, 1.3, 2.0), C["wall"], bevel=0.03)
    for i in range(5):
        box(f"Hazard{i}", (-0.065, -0.52 + i * 0.26, 0.25), (0.01, 0.13, 0.3), C["yellow"] if i % 2 == 0 else C["dark"], bevel=0)
    cyl("Porthole", (-0.07, 0, 1.4), 0.22, 0.04, C["glass"], rot=(0, 90, 0), bevel=0)
    tor("PortRim", (-0.08, 0, 1.4), 0.23, 0.03, C["metal"], rot=(0, 90, 0))
    sph("Light", (-0.12, 0.6, 2.05), (0.06, 0.06, 0.06), C["led_g"], seg=12)
    box("Panel", (-0.12, -0.6, 1.2), (0.03, 0.16, 0.24), C["dark"], bevel=0.01)
    box("Btn", (-0.14, -0.6, 1.22), (0.01, 0.06, 0.06), C["led_r"], bevel=0)
    return a

# ---------------- stations ----------------
def chair(x, y, m):
    cyl("ChairBase", (x, y, 0.03), 0.22, 0.06, C["dark"])
    cyl("ChairPole", (x, y, 0.22), 0.04, 0.34, C["metal"], bevel=0)
    box("ChairSeat", (x, y, 0.42), (0.46, 0.42, 0.1), m, bevel=0.04)
    box("ChairBack", (x, y + 0.2, 0.7), (0.46, 0.1, 0.5), m, bevel=0.04)

def pilot_station():
    a = asset("Station_Pilot")
    box("Desk", (0, 0, 0.4), (1.6, 0.7, 0.8), C["grey"], bevel=0.06)
    box("Top", (0, -0.02, 0.82), (1.64, 0.74, 0.05), C["dark"], bevel=0.02)
    box("Stripe", (0, -0.351, 0.55), (1.62, 0.02, 0.08), ROLE["pilot"], bevel=0)
    box("ScreenBody", (0, 0.22, 1.12), (1.3, 0.08, 0.55), C["dark"], bevel=0.03, rot=(-15, 0, 0))
    box("Screen", (0, 0.17, 1.12), (1.18, 0.02, 0.45), C["screen"], bevel=0, rot=(-15, 0, 0))
    cyl("StickBase", (-0.35, -0.12, 0.86), 0.07, 0.04, C["dark"])
    cyl("Stick", (-0.35, -0.12, 0.98), 0.02, 0.22, C["metal"], bevel=0)
    sph("StickKnob", (-0.35, -0.12, 1.1), 0.045, ROLE["pilot"], seg=16)
    box("Throttle", (0.35, -0.1, 0.9), (0.06, 0.14, 0.1), ROLE["pilot"], bevel=0.015)
    for i, m in enumerate((C["led_g"], C["led_y"], C["led_r"])):
        box(f"Btn{i}", (0.05 + i * 0.1, -0.2, 0.855), (0.06, 0.06, 0.02), m, bevel=0)
    chair(0, -0.75, ROLE["pilot"])
    return a

def nav_station():
    a = asset("Station_Nav")
    cyl("Pedestal", (0, 0, 0.35), 0.28, 0.7, C["grey"], bevel=0.05)
    cyl("Table", (0, 0, 0.75), 0.62, 0.1, C["dark"], bevel=0.03)
    tor("Rim", (0, 0, 0.8), 0.62, 0.03, ROLE["nav"])
    cyl("Projector", (0, 0, 0.81), 0.45, 0.01, C["holo"], bevel=0)
    sph("HoloPlanet", (0, 0, 1.2), 0.2, C["holo"], seg=24)
    tor("HoloOrbit", (0, 0, 1.2), 0.38, 0.008, C["holo"], rot=(20, 0, 0))
    sph("HoloShip", (0.36, 0, 1.33), 0.035, C["led_y"], seg=12)
    for ang in range(0, 360, 90):
        r = math.radians(ang)
        box("Foot", (0.3 * math.cos(r), 0.3 * math.sin(r), 0.03), (0.22, 0.08, 0.06), C["dark"], bevel=0.01, rot=(0, 0, ang))
    return a

def engine_station():
    a = asset("Station_Engine")
    cyl("Base", (0, 0, 0.15), 0.62, 0.3, C["grey"], bevel=0.05)
    cyl("Column", (0, 0, 0.95), 0.32, 1.3, C["glass"], bevel=0)
    cyl("Core", (0, 0, 0.95), 0.16, 1.2, C["holo"], bevel=0)
    for z in (0.5, 0.95, 1.4):
        tor("Ring", (0, 0, z), 0.34, 0.04, ROLE["sys"])
    cyl("Cap", (0, 0, 1.66), 0.42, 0.16, C["grey"], bevel=0.04)
    for ang in (30, 150, 270):
        r = math.radians(ang)
        tube("Pipe", [(0.5 * math.cos(r), 0.5 * math.sin(r), 0.25), (0.9 * math.cos(r), 0.9 * math.sin(r), 0.25),
                      (0.9 * math.cos(r), 0.9 * math.sin(r), 0.03)], 0.05, C["metal"])
    box("Gauge", (0, -0.63, 0.2), (0.3, 0.02, 0.14), C["dark"], bevel=0.01)
    box("GaugeBar", (-0.04, -0.645, 0.2), (0.18, 0.01, 0.05), C["led_g"], bevel=0)
    return a

def comms_station():
    a = asset("Station_Comms")
    box("Rack", (0, 0, 0.8), (0.9, 0.5, 1.6), C["grey"], bevel=0.05)
    for i, z in enumerate((1.25, 0.85)):
        box(f"Screen{i}", (0, -0.255, z), (0.7, 0.02, 0.3), C["screen"], bevel=0)
    for i in range(4):
        cyl(f"Knob{i}", (-0.27 + i * 0.18, -0.27, 0.5), 0.04, 0.04, C["dark"], rot=(90, 0, 0))
    box("Stripe", (0, -0.252, 0.3), (0.86, 0.02, 0.06), ROLE["nav"], bevel=0)
    cyl("Mast", (0.25, 0, 1.85), 0.02, 0.5, C["metal"], bevel=0)
    cyl("Dish", (0.25, 0, 2.1), 0.22, 0.06, C["white"], r2=0.03, rot=(-25, 0, 0))
    sph("Blink", (-0.3, -0.25, 1.5), 0.03, C["led_r"], seg=12)
    return a

def charger_station():
    a = asset("Station_Charger")
    box("Pedestal", (0, 0, 0.3), (0.9, 0.5, 0.6), C["grey"], bevel=0.05)
    for i, x in enumerate((-0.22, 0.22)):
        box(f"Socket{i}", (x, 0, 0.62), (0.34, 0.26, 0.06), C["dark"], bevel=0.02)
        box(f"SocketGlow{i}", (x, 0, 0.655), (0.30, 0.22, 0.01), C["led_g"], bevel=0)
    box("Sign", (0, -0.252, 0.35), (0.5, 0.02, 0.16), C["yellow"], bevel=0)
    text("PWR", (0, -0.27, 0.35), 0.1, C["dark"], extrude=0.003)
    return a

builders = [("shell", room_shell), ("airlock", airlock), ("pilot", pilot_station), ("nav", nav_station),
            ("engine", engine_station), ("comms", comms_station), ("charger", charger_station)]
A = {}
for k, fn in builders:
    A[k] = fn(); end_asset()

# ---------------- layout inside the room ----------------
A["airlock"].location = (W / 2 + 0.1, 0, 0)
A["pilot"].location = (0, D / 2 - 1.1, 0)
A["nav"].location = (-3.0, 0.6, 0)
A["engine"].location = (3.3, 2.1, 0)
A["comms"].location = (-4.2, D / 2 - 0.6, 0)
A["charger"].location = (2.8, -1.6, 0)

# save under our own name first: Blender refuses to append from the currently open file
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "blend", "room.blend"))

# props from props.blend (append roots + children), placed as a live incident
def append_assets(path, names):
    with bpy.data.libraries.load(path, link=False) as (src, dst):
        dst.objects = [n for n in src.objects if any(n == r or n.startswith(r + ".") for r in names)]
    roots = {}
    for o in dst.objects:
        if o is None:
            continue
        bpy.context.scene.collection.objects.link(o)
        if o.name in names:
            roots[o.name] = o
    return roots

props = append_assets(os.path.join(ROOT, "blend", "props.blend"),
                      ["Incident_Fire", "Incident_Alarm", "Incident_BrokenPipe", "Item_Battery", "Item_Toolbox",
                       "Item_Extinguisher", "Item_Oxygen"])
props["Incident_Fire"].location = (2.2, 2.4, 0)
props["Incident_Alarm"].location = (0, D / 2 + 0.0, BACK_H + 0.05)
props["Incident_BrokenPipe"].location = (-2.4, D / 2 - 0.15, 0.25)
props["Item_Toolbox"].location = (-1.2, -0.6, 0)
props["Item_Extinguisher"].location = (1.2, 1.2, 0)
props["Item_Oxygen"].location = (-4.4, -2.6, 0)

# crew (append the rigged + animated characters)
crew_src = os.path.join(ROOT, "blend", "crew_v7_anim.blend")
with bpy.data.libraries.load(crew_src, link=False) as (src, dst):
    dst.objects = [n for n in src.objects if n.startswith("Civilian_")]
    dst.actions = list(src.actions)
crew = {}
for o in dst.objects:
    bpy.context.scene.collection.objects.link(o)
    if o.name in ("Civilian_Nav", "Civilian_Pilot", "Civilian_Sys"):
        crew[o.name] = o
acts = {a.name: a for a in dst.actions}
def place(name, loc, rot_z, action, frame):
    root = crew[name]; root.location = loc; root.rotation_euler = (0, 0, math.radians(rot_z))
    rig = next(c for c in root.children if c.type == "ARMATURE")
    rig.animation_data.action = acts[action]
    return frame
place("Civilian_Nav", (-3.0, -0.3, 0), 180, "Idle", 10)                 # reading the nav table
place("Civilian_Sys", (2.65, 1.45, 0), 225, "Work", 4)                  # fixing the engine near the fire
place("Civilian_Pilot", (1.2, -1.6, 0), 90, "Carry", 6)               # carrying a battery to the charger
# character front is local -Y; rotated 90 deg it faces +X (toward the charger)
props["Item_Battery"].location = (1.2 + 0.30, -1.6, 0.46)
props["Item_Battery"].rotation_euler = (0, 0, math.radians(90))
scn = bpy.context.scene
scn.frame_set(6)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "blend", "room.blend"))

# ---------------- renders ----------------
cam = studio((0, -9.5, 9.0), (47, 0, 0), lens=30, bg="#0b1022")
render(os.path.join(ROOT, "renders", "room_game_view.png"))
cam.location = (7.5, -7.5, 4.2); cam.rotation_euler = (math.radians(72), 0, math.radians(45)); cam.data.lens = 28
render(os.path.join(ROOT, "renders", "room_34.png"))

# ---------------- export ----------------
out = []
for k, a in A.items():
    out.append((a.name, export_asset(a, os.path.join(ROOT, "export", "room"))[1]))
print(out)
