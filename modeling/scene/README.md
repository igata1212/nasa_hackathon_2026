書いた人：Taiga（作成：Fable 5.2）／ 最終更新：2026-11-14

# modeling/scene：場面の背景

場面ごとの背景と、その場面にだけ出る人。ルールは `modeling/core/README.md`（11章）。FBX は `unity/Assets/Models/Scene/`。

## 出発の場面（#52、`build_launch.py`）

`docs/design/storyboard.md` の場面1。発射台と地面だけ 3D で作った。ロケットの断面（場面2）は作っていない。

| FBX | 中身 | 三角数 |
|---|---|---|
| `Scene_Ground` | 弧を描く地面（直径 800m、中央が 40m 高い）、発射台の下の舗装、奥へ伸びる道 | 862 |
| `Scene_LaunchPad` | 台、脚6本、右に立つ塔、クレーン、腕3本 | 4,076 |

組み立て方（Unity。数字は Blender の座標、m）：

1. `Scene_Ground` を置く。
2. その子の `Slot_Pad`（舗装の上面の中心、高さ 40.3）に `Scene_LaunchPad` の原点を合わせる。
3. `Scene_LaunchPad` の子の `Slot_Rocket`（台の上面、高さ 14）に、ロケット（`Ship_RocketLarge` か `Ship_RocketSmall`。原点＝ノズル出口）を合わせる。

実寸（ロケット 約110m）なので、クルー（1.2m）と一緒に見せるときは Unity 側で縮める。組み合わせた見本は `renders/launch_scene.png`（大型ロケット）。

動かせる部品（`parts.json`）：

| 部品 | 動き |
|---|---|
| `Scene_LaunchPad.Move_Arm_1`〜`3` | 塔からロケットへ伸びる腕（下から）。Blender の Z まわりに 0（ロケットに付いた姿）〜 −90°（奥へ逃がした姿）。Unity では `localEulerAngles.y = −角度`（0 〜 +90）。打ち上げの直前に逃がす。3 がクルーの乗る腕（先に白い部屋） |
| `Scene_LaunchPad.Lamp_Beacon` | 塔のてっぺんの赤い灯。発光は Unity で付ける |

腕は大型ロケットの太さに合わせてある。小型ロケット（直径 6.6m、高さ 約70m）だと、腕 1・2 は胴に約 1.7m 届かず、腕 3 とクレーンはロケットより上になる。

ベベルは 0.25m（実寸で 100m を超えるので、ルールの 2〜4cm では角の光が見えないため。例外）。
