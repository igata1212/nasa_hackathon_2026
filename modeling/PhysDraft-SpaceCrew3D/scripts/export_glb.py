# Export each rigged crew character to export/<Name>.glb with only its own "Walk" clip,
# decimated for web use. Usage: bl.py reload_rig.py && bl.py export_glb.py
import bpy, os

ROOT = "/Users/tomokiashizawa/Claude/アプリ作成/PhysDraft-SpaceCrew3D"
# Run scripts/reload_rig.py in a separate call first: reopening the file inside the
# same call leaves bpy.context without a window, and the glTF exporter needs one.
assert os.path.basename(bpy.data.filepath) == "crew_v6_rigged.blend" and not bpy.data.is_dirty, \
    "run scripts/reload_rig.py first"
vl = bpy.context.view_layer
RATIO = 0.2          # keep ~20% of triangles (after bevel)
exp = os.path.join(ROOT, "export"); os.makedirs(exp, exist_ok=True)

rigs = [o for o in bpy.data.objects if o.type == "ARMATURE"]
report = []
for r in rigs:
    root = r.parent
    act = r.animation_data.action
    real_name = act.name
    act.name = "Walk"
    meshes = [o for o in root.children_recursive if o.type == "MESH"]
    added = []
    for o in meshes:
        m = o.modifiers.new("web_decimate", "DECIMATE"); m.ratio = RATIO; added.append((o, m))
    keep = root.location.copy(); root.location = (0, 0, 0); vl.update()
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for o in root.children_recursive:
        o.select_set(True)
    vl.objects.active = r
    path = os.path.join(exp, f"{root.name}.glb")
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_apply=True,
                              export_animations=True, export_animation_mode="ACTIVE_ACTIONS",
                              export_nla_strips_merged_animation_name="Walk")
    root.location = keep
    for o, m in added:
        o.modifiers.remove(m)
    act.name = real_name
    report.append((root.name, os.path.getsize(path)))
vl.update()
print(report)
