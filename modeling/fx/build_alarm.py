"""Fx_Alarm：警報の回転灯。midori の props.py の alarm() を core で組み直したもの（分割数を下げ、煙・火花なし）。

書いた人：iizuka（作成：Fable 5.2）／ 最終更新：2026-11-14

  modeling/core/run.sh modeling/fx/build_alarm.py
出力：unity/Assets/Models/Fx/Fx_Alarm.fbx、modeling/fx/export/Fx_Alarm.glb、modeling/fx/renders/Fx_Alarm_*.png、
      modeling/fx/parts.json
部品：Base（台）、Cage（ガードの輪）、Lamp_Red（光るドーム。Unity で発光に差し替える）、
      Move_Shade（ドームのまわりを回る遮光板。原点は回転軸＝Z軸上）
"""
import sys, os
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "core"))
import corekit as ck

KIND, NAME = "fx", "Fx_Alarm"

ck.clear_scene()
root = ck.asset(NAME)
ck.cyl("Base", (0, 0, 0.03), 0.15, 0.06, "gray_dark", bevel=0.015)
ck.tor("Cage", (0, 0, 0.07), 0.112, 0.012, "gray")
ck.dome("Lamp_Red", (0, 0, 0.06), (0.10, 0.10, 0.13), "lamp_red")

# 遮光板：てっぺんの軸受け＋腕＋背中側（+Y）の板を1つのメッシュにまとめ、原点を回転軸に置く
cap = ck.cyl("ShadeCap", (0, 0, 0.20), 0.05, 0.04, "gray_light", bevel=0.01)
arm = ck.box("ShadeArm", (0, 0.075, 0.205), (0.05, 0.13, 0.03), "gray_light", bevel=0.0075)
plate = ck.box("ShadePlate", (0, 0.135, 0.15), (0.16, 0.025, 0.14), "orange", bevel=0.006)
shade = ck.join([cap, arm, plate], "Move_Shade")
ck.set_origin(shade, (0, 0, 0.06))
ck.end_asset()

parts = [{"name": "Move_Shade", "type": "rotate", "axis": "Z", "min": -180, "max": 180, "default": 0,
          "note": "警報中は回し続ける（1周 約1秒）。範囲は1周ぶん"}]
ck.write_parts(KIND, root, parts)
ck.export_all(root, KIND)
ck.render_check(root, KIND, parts)
print(f"[build] {NAME}: {ck.tri_count(root)} 三角")
