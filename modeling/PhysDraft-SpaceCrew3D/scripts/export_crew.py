# Export each crew character to export/<Name>.glb with all shared clips (Idle/Walk/Carry/Work),
# decimated for web. Usage: bl.py reload_anim.py && bl.py export_crew.py
# (reopening a file in the same call leaves bpy.context without a window, which the exporter needs)
import bpy, os

ROOT = "/Users/tomokiashizawa/Claude/アプリ作成/PhysDraft-SpaceCrew3D"
assert os.path.basename(bpy.data.filepath) == "crew_v7_anim.blend" and not bpy.data.is_dirty, \
    "run scripts/reload_anim.py first"
vl = bpy.context.view_layer
RATIO = 0.2
exp = os.path.join(ROOT, "export"); os.makedirs(exp, exist_ok=True)
# Walk is the active action on every rig; the exporter then hands it to only the first armature.
# Unassign it everywhere so all four fake-user actions are exported for each character.
rigs = [o for o in bpy.data.objects if o.type == "ARMATURE"]
for r in rigs:
    if r.animation_data:
        r.animation_data.action = None
report = []
for r in rigs:
    root = r.parent
    added = []
    for o in root.children_recursive:
        if o.type == "MESH":
            m = o.modifiers.new("web_decimate", "DECIMATE"); m.ratio = RATIO; added.append((o, m))
    keep = root.location.copy(); root.location = (0, 0, 0); vl.update()
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for o in root.children_recursive:
        o.select_set(True)
    vl.objects.active = r
    path = os.path.join(exp, f"{root.name}.glb")
    # ACTIONS mode exports every action whose bones match this armature: the 4 shared clips
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_apply=True,
                              export_animations=True, export_animation_mode="ACTIONS")
    root.location = keep
    for o, m in added:
        o.modifiers.remove(m)
    report.append((root.name, os.path.getsize(path)))
print(report)
