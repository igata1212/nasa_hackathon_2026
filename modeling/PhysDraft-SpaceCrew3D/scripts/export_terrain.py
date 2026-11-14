# Re-export the moon terrain lighter for web. Usage: bl.py reload_exterior.py && bl.py export_terrain.py
import sys, importlib, os, bpy
sys.path.insert(0, "/Users/tomokiashizawa/Claude/アプリ作成/PhysDraft-SpaceCrew3D/scripts")
import kit; importlib.reload(kit)
root = bpy.data.objects["Moon_Terrain"]
print(kit.export_asset(root, os.path.join(kit.ROOT, "export", "exterior"), decimate=0.3))
