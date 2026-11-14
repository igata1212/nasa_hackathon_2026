# Exterior assets: crew ship, asteroids, moon terrain tile, moon base. Renders a hero shot and a
# landing diorama, exports GLBs. Writes blend/exterior.blend.
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
EARTH = mat("K_Earth", "#2f6fd6", 0.5, 0, 0.6)
LAND = mat("K_Land", "#3fae5a", 0.6, 0, 0.4)
CLOUD = mat("K_Cloud", "#ffffff", 0.8, 0, 0.8, 0.85)

def ship():
    """Chubby lander-ship. Nose = +Y. Landing pads touch z=0."""
    a = asset("Ship_Lander")
    sph("Hull", (0, 0, 1.25), (1.25, 2.1, 1.0), C["white"], seg=48)
    tor("Stripe", (0, 0.2, 1.25), 1.0, 0.08, C["orange"], rot=(90, 0, 0), scale=(1.27, 1.03, 1.0))
    sph("Cockpit", (0, 1.25, 1.75), (0.62, 0.75, 0.45), C["glass"], seg=32)
    tor("CockpitRim", (0, 1.25, 1.62), 0.62, 0.035, C["gold"], scale=(1, 1.2, 1))
    for s, side in ((-1, "L"), (1, "R")):
        cyl(f"Engine.{side}", (s * 1.3, -1.1, 0.95), 0.28, 1.0, C["grey"], rot=(90, 0, 0), bevel=0.05)
        cyl(f"Nozzle.{side}", (s * 1.3, -1.7, 0.95), 0.22, 0.24, C["dark"], rot=(90, 0, 0), r2=0.29)
        cyl(f"Flame.{side}", (s * 1.3, -1.83, 0.95), 0.18, 0.03, C["holo"], rot=(90, 0, 0), bevel=0)
        box(f"Pylon.{side}", (s * 1.05, -1.0, 1.0), (0.45, 0.45, 0.1), C["white"], bevel=0.04)
        box(f"Fin.{side}", (s * 1.3, -1.35, 1.3), (0.06, 0.5, 0.36), C["orange"], bevel=0.025, rot=(-25, 0, 0))
    cyl("MainNozzle", (0, -2.05, 1.2), 0.35, 0.35, C["dark"], rot=(90, 0, 0), r2=0.5)
    cyl("MainFlame", (0, -2.24, 1.2), 0.3, 0.04, C["holo"], rot=(90, 0, 0), bevel=0)
    # role lights on top: nav / pilot / sys
    for x, m in ((-0.45, ROLE["nav"]), (0, ROLE["pilot"]), (0.45, ROLE["sys"])):
        sph("RoleLight", (x, -0.5, 2.22), 0.1, m, seg=16)
    cyl("Antenna", (0.5, -1.0, 2.35), 0.02, 0.5, C["metal"], bevel=0)
    sph("AntennaTip", (0.5, -1.0, 2.62), 0.05, C["led_r"], seg=12)
    # landing legs (3) + pads
    for ang in (90, 210, 330):
        r = math.radians(ang)
        top = (0.8 * math.cos(r), 1.2 * math.sin(r), 0.75)
        foot = (1.45 * math.cos(r), 1.9 * math.sin(r), 0.08)
        tube("Leg", [top, foot], 0.06, C["metal"])
        cyl("Pad", (foot[0], foot[1], 0.04), 0.22, 0.08, C["dark"])
    box("Hatch", (1.2, 0.3, 1.1), (0.06, 0.6, 0.8), C["grey"], bevel=0.03)
    text("PD-1", (-1.24, 0.1, 1.35), 0.28, C["dark"], rot=(90, 0, -90), extrude=0.01)
    return a

def asteroid(name, r, seed):
    a = asset(name)
    ico("Rock", (0, 0, 0), r, C["rock"], sub=3, noise=0.22, seed=seed, scale=(1.2, 1.0, 0.85))
    rnd = random.Random(seed)
    for i in range(6):
        th, ph = rnd.uniform(0, math.tau), rnd.uniform(-1.0, 1.0)
        d = Vector((math.cos(th) * math.cos(ph) * 1.2, math.sin(th) * math.cos(ph), math.sin(ph) * 0.85)).normalized()
        cr = rnd.uniform(0.12, 0.25) * r
        o = sph(f"Crater{i}", d * r * 0.93, (cr, cr, cr * 0.35), C["moon2"], seg=16)
        o.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    return a

def moon_tile(size=24, n=140, seed=3):
    """Cratered terrain; pad area around origin kept flat."""
    a = asset("Moon_Terrain")
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=n, y_subdivisions=n, size=size)
    o = bpy.context.object
    rnd = random.Random(seed)
    craters = [(rnd.uniform(-10, 10), rnd.uniform(-10, 10), rnd.uniform(0.8, 3.0)) for _ in range(16)]
    craters = [c for c in craters if math.hypot(c[0], c[1]) > 5.5]
    for v in o.data.vertices:
        x, y = v.co.x, v.co.y
        z = 0.12 * math.sin(x * 0.45 + 1.3) * math.cos(y * 0.38) + 0.06 * math.sin(x * 1.3 + y * 0.9)
        for cx, cy, R in craters:
            d = math.hypot(x - cx, y - cy) / R
            # bowl + soft raised rim (smoothstep falloff, no hard edge)
            if d < 1.0:
                z -= 0.22 * R * (1 - d * d) ** 1.5
            if 0.6 < d < 1.7:
                u = (d - 0.6) / 1.1
                z += 0.07 * R * math.sin(math.pi * u) ** 2
        flat = min(1.0, max(0.0, (math.hypot(x, y) - 3.5) / 2.0))      # landing zone stays flat
        edge = min(1.0, max(0.0, (size / 2 - max(abs(x), abs(y))) / 1.5))
        v.co.z = z * flat * edge
    _ = kit._fin(o, C["moon"], "Ground")
    return a

def moon_base():
    a = asset("Moon_Base")
    # habitat domes
    for i, (x, y, r) in enumerate([(0, 0, 1.4), (3.2, 0.6, 1.0), (-2.6, 1.4, 0.9)]):
        sph(f"Dome{i}", (x, y, 0), (r, r, r * 0.85), C["white"], seg=48)
        tor(f"DomeBand{i}", (x, y, r * 0.18), r * 0.99, 0.05, C["orange"])
        sph(f"DomeWin{i}", (x, y - r * 0.82, r * 0.38), (r * 0.28, 0.08, r * 0.16), C["screen"], seg=16)
    for (x0, y0), (x1, y1) in [((1.2, 0.2), (2.4, 0.5)), ((-1.2, 0.5), (-1.9, 1.1))]:
        tube("Tunnel", [(x0, y0, 0.45), (x1, y1, 0.45)], 0.32, C["grey"])
    # solar array
    for i in range(3):
        cyl(f"SolarPole{i}", (-4.6 + i * 1.1, -1.4, 0.5), 0.04, 1.0, C["metal"], bevel=0)
        box(f"Solar{i}", (-4.6 + i * 1.1, -1.4, 1.05), (0.9, 0.6, 0.04), C["solar"], bevel=0.0, rot=(30, 0, 0))
    # comm dish
    cyl("DishPole", (2.0, 2.4, 0.6), 0.06, 1.2, C["metal"], bevel=0)
    cyl("Dish", (2.0, 2.4, 1.35), 0.6, 0.15, C["white"], r2=0.08, rot=(-35, 0, 20))
    sph("Beacon", (0, 0, 1.25), 0.08, C["led_r"], seg=12)
    # cargo dock where supplies are handed over
    box("Dock", (0.0, -1.9, 0.12), (1.4, 0.9, 0.24), C["grey"], bevel=0.04)
    box("DockGlow", (0.0, -1.9, 0.245), (1.2, 0.7, 0.01), C["led_g"], bevel=0)
    for i, x in enumerate((-0.55, -0.18, 0.18, 0.55)):
        box(f"DockHazard{i}", (x, -2.36, 0.12), (0.18, 0.01, 0.2), C["yellow"] if i % 2 == 0 else C["dark"], bevel=0)
    return a

def landing_pad():
    a = asset("Moon_LandingPad")
    cyl("Pad", (0, 0, 0.05), 2.4, 0.1, C["grey"], bevel=0.04, verts=64)
    tor("Ring", (0, 0, 0.1), 2.0, 0.05, C["yellow"], seg=64)
    text("H", (0, 0, 0.105), 1.6, C["yellow"], rot=(0, 0, 0), extrude=0.005)
    for ang in range(0, 360, 45):
        r = math.radians(ang)
        sph("PadLight", (2.25 * math.cos(r), 2.25 * math.sin(r), 0.12), 0.06, C["led_g"], seg=12)
    return a

A = {}
A["ship"] = ship(); end_asset()
A["ast1"] = asteroid("Asteroid_Large", 1.6, 11); end_asset()
A["ast2"] = asteroid("Asteroid_Small", 0.7, 23); end_asset()
A["terrain"] = moon_tile(); end_asset()
A["base"] = moon_base(); end_asset()
A["pad"] = landing_pad(); end_asset()

# --- diorama layout: pad at origin, base to the side, ship landed on pad ---
A["base"].location = (6.5, 3.5, 0)
A["pad"].location = (0, 0, 0)
A["ship"].location = (0, 0, 0.1); A["ship"].rotation_euler = (0, 0, math.radians(-30))
A["ast1"].location = (-9, 14, 7.5)
A["ast2"].location = (-5, 10, 9.0)
sph("Earth", (12, 40, 11), 5.0, EARTH, seg=48)
for i, (x, z, sx, sz) in enumerate([(10.5, 12.5, 1.8, 1.2), (13.5, 9.5, 1.4, 1.6), (11.5, 9.8, 1.0, 0.8)]):
    sph(f"EarthLand{i}", (x, 35.3, z), (sx, 0.4, sz), LAND, seg=24)
rnd = random.Random(5)
for i in range(120):
    sph(f"Star{i}", (rnd.uniform(-60, 60), rnd.uniform(50, 60), rnd.uniform(0, 45)), rnd.uniform(0.05, 0.16), C["star"], seg=8)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "blend", "exterior.blend"))

cam = studio((-11, -13, 6.5), (78, 0, -40), lens=30, bg="#04060d")
for l in bpy.data.lights:           # studio lights are tuned for indoor props; dim for the moon
    l.energy *= 0.3
# sunlight: hard and low, like the lunar south pole
sun = bpy.data.lights.new("Sun", "SUN"); sun.energy = 2.2; sun.angle = math.radians(1.5)
so = bpy.data.objects.new("Sun", sun); so.rotation_euler = (math.radians(70), 0, math.radians(-60))
bpy.context.scene.collection.objects.link(so)
render(os.path.join(ROOT, "renders", "exterior_landing.png"))
# ship hero shot
# sun travels toward +x+y, so the lit side faces -x-y: turn the nose that way and hide the base
ship_rot = A["ship"].rotation_euler.copy()
A["ship"].rotation_euler = (0, 0, math.radians(125))
hidden = [A["base"]] + list(A["base"].children_recursive)
for o in hidden:
    o.hide_render = True
cam.location = (-5.2, -5.6, 2.3); cam.rotation_euler = (math.radians(82), 0, math.radians(-43)); cam.data.lens = 35
render(os.path.join(ROOT, "renders", "exterior_ship.png"))
for o in hidden:
    o.hide_render = False
A["ship"].rotation_euler = ship_rot

out = []
for k, a in A.items():
    out.append((a.name, export_asset(a, os.path.join(ROOT, "export", "exterior"))[1]))
print(out)
