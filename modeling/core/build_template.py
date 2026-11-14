"""雛形。これを modeling/<種類>/build_<名前>.py にコピーして使う（1ファイル＝1アセット、または同じ種類の数個）。

書いた人：iizuka（作成：Fable 5.2）／ 最終更新：2026-11-14

  modeling/core/run.sh modeling/controls/build_toggle.py
流れ：作る → paint（色名を渡すだけ）→ 原点 → parts.json → 書き出し → レンダー。最後に check.py を流す。
コピーしたら (1) KIND と NAME を直す (2) TRIAL = False にする (3) sys.path の "." を "../core" に直す。
このままで流すと tmp/modeling_trial/ に出るだけで、modeling/ と unity/ は汚さない。
"""
import sys, os
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "."))   # コピー先では "..", "core"
import corekit as ck

KIND, NAME = "controls", "Ctrl_Template"     # KIND は controls/console/crew/tank/ship/bodies/fx
TRIAL = True
if TRIAL:
    ck.trial()

ck.clear_scene()
root = ck.asset(NAME)                         # ルート（Empty）。原点は台座の底の中心 = (0,0,0)

# 形を作る。座標は m、Z が上、正面（操作する人の側）は −Y。色はパレットの色名
ck.ctrl_base(0.2, 0.2)                        # 操作部品は必ず共通の台座から（部品名 Base）
ck.box("Housing", (0, 0, 0.065), (0.12, 0.14, 0.05), "cream")
ck.cyl("Lamp_Green", (0, 0.045, 0.10), 0.02, 0.03, "lamp_green", bevel=0.005)   # ランプ色は Lamp_ の部品だけ

# 可動部：色の違う形を join で1メッシュにまとめ、原点を回転軸に置く
stem = ck.cyl("LeverStem", (0, -0.02, 0.14), 0.014, 0.12, "gray_light", bevel=0.003)
knob = ck.sph("LeverKnob", (0, -0.02, 0.21), 0.03, "seat_red")
lever = ck.join([stem, knob], "Move_Lever")
ck.set_origin(lever, (0, -0.02, 0.09))        # 回転軸（X軸に平行）の上の点。世界座標で渡す
ck.end_asset()

# 可動部の表。axis は部品のローカル軸（Blender）、角度は度、移動は m
parts = [{"name": "Move_Lever", "type": "rotate", "axis": "X", "min": -30, "max": 30, "default": 0}]
ck.write_parts(KIND, root, parts)

ck.export_all(root, KIND)                     # 仕上げ（ベベル確定・三角化・法線）→ FBX・glb・stats.json
ck.render_check(root, KIND, parts)            # renders/ に3枚＋320px 版
print(f"[build] {NAME}: {ck.tri_count(root)} 三角")
