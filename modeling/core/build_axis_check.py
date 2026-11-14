"""軸の確認用モデル。FBX の設定や Unity の版を変えたときに、Blender の向き→Unity の向きを確かめ直すためのもの。

書いた人：iizuka（作成：Fable 5.2）／ 最終更新：2026-11-14

  modeling/core/run.sh modeling/core/build_axis_check.py
unity/Assets/Models/Core/Fx_AxisCheck.fbx を書く。Unity を前に出して取り込ませ、Editor.log の [ModelImportReport] を見る。
確かめ終わったら FBX と .meta を消す（ゲームには使わない）。
  PX / NY / PZ ：Blender の +X 0.3m、−Y（正面）0.5m、+Z 0.7m に置いた箱と Slot
  RotX / RotY / RotZ ：それぞれの軸まわりに +20° 回した Slot（回転の符号を見る）
  Move_Arm ：原点を (0.1, -0.2, 0.3) に置いた可動部（原点がずれないかを見る）
"""
import sys, os
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import math
import corekit as ck
import export as ex

ck.clear_scene()
root = ck.asset("Fx_AxisCheck")
ck.box("Floor", (0, 0, 0.02), (0.2, 0.2, 0.04), "gray_dark", bevel=0)
ck.box("PX", (0.3, 0, 0.05), (0.1, 0.1, 0.1), "seat_red", bevel=0)
ck.box("NY", (0, -0.5, 0.05), (0.1, 0.1, 0.1), "seat_green", bevel=0)
ck.box("PZ", (0, 0, 0.7), (0.1, 0.1, 0.1), "seat_blue", bevel=0)
ck.slot("Slot_PX", (0.3, 0, 0)); ck.slot("Slot_NY", (0, -0.5, 0)); ck.slot("Slot_PZ", (0, 0, 0.7))
for i, a in enumerate("XYZ"):
    s = ck.slot("Slot_Rot" + a, (0, 0, 0)); s.rotation_euler[i] = math.radians(20)
arm = ck.box("Move_Arm", (0.1, -0.2, 0.4), (0.04, 0.04, 0.2), "orange", bevel=0)
ck.set_origin(arm, (0.1, -0.2, 0.3))
tip = ck.box("ArmTip", (0.1, -0.25, 0.5), (0.04, 0.1, 0.04), "cream", bevel=0)
ck.attach(tip, arm)
ck.end_asset()
ck.finalize(root)
print(ex.export_fbx(root, os.path.join(ex.UNITY_MODELS, "Core", "Fx_AxisCheck.fbx")))
