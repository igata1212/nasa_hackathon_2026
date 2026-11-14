# Preview: Nav pair Idle, Pilot pair Carry (with a crate), Sys pair Work. Renders frames to renders/anims/.
import bpy, os
ROOT = "/Users/tomokiashizawa/Claude/アプリ作成/PhysDraft-SpaceCrew3D"
assert os.path.basename(bpy.data.filepath) == "crew_v7_anim.blend"
scn = bpy.context.scene
A = bpy.data.actions
plan = {"Nav": "Idle", "Pilot": "Carry", "Sys": "Work"}
for r in [o for o in bpy.data.objects if o.type == "ARMATURE"]:
    key = r.parent.name.split("_")[1]
    ad = r.animation_data
    ad.action = A[plan[key]]
    if hasattr(ad, "action_slot") and ad.action_slot is None and len(ad.action.slots):
        ad.action_slot = ad.action.slots[0]
scn.frame_start, scn.frame_end = 0, 47
scn.camera = bpy.data.objects["Cam34"]
scn.render.resolution_x, scn.render.resolution_y = 960, 600
out = os.path.join(ROOT, "renders", "anims"); os.makedirs(out, exist_ok=True)
scn.render.filepath = os.path.join(out, "f_")
bpy.ops.render.render(animation=True)
print("ok", {r.name: r.animation_data.action.name for r in bpy.data.objects if r.type == "ARMATURE"})
