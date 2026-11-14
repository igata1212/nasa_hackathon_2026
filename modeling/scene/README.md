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

## 管制室の場面（#53、`build_mission_control.py`）

`docs/design/storyboard.md` の場面4。ヒューストン先生のバストアップをポップアップ画面に出し、うしろは人が大勢いる管制室にする。

| FBX | 中身 | 三角数 |
|---|---|---|
| `Scene_MissionControl` | 部屋（床、奥と左右の壁、段2つ）、机の列3つ、奥の壁の画面3つ、ランプ | 4,432 |
| `Crew_Houston` | ヒューストン先生。白いベスト、navy のネクタイ、ヘッドセット、短い平らな髪 | 2,768 |
| `Crew_Controller_A`・`B`・`C` | うしろの管制官3種（服と髪が違う）。作りは先生と同じ | 1,780〜2,152 |

見本：`renders/houston_popup_1_closed.png`・`2_open.png`・`3_worried.png`（ポップアップの見え方）、`renders/mission_control_scene.png`（部屋の全体）。

### 組み立て方（Unity）

1. `Scene_MissionControl` を置く（床の上面は高さ 0.10。手前の壁と天井はない）。
2. 子の `Slot_Houston` に `Crew_Houston` を置く。
3. 子の `Slot_Seat_11`〜`36`（列・左から。3列×6人）に `Crew_Controller_A/B/C` を好きに置く。空席があってよい。全員、手前（Unity の +Z）を向く。
4. 子の `Slot_PopupCamera` にカメラを置き、先生のほう（Blender の +Y＝Unity の −Z）へまっすぐ向ける。縦の画角 30° で、先生の胸から上と、うしろの管制室が入る。このカメラの絵を RenderTexture に出して、ポップアップの UI に貼る。

### 話す動き（シェイプキー＝Unity のブレンドシェイプ）

顔は `Crew_Houston.Face`（`Head` の子）。Unity では `SkinnedMeshRenderer` になり、ブレンドシェイプが7個入る（値は 0〜100）。管制官の `Face` にも同じ7個が入っている。

| 名前 | 動き |
|---|---|
| `MouthOpen` | 口を大きく開ける（あ） |
| `MouthO` | 小さく丸く開ける（お・う） |
| `MouthWide` | 横に広げる（い・え） |
| `Smile` | 口の両端を上げる |
| `Blink` | 目を閉じる |
| `BrowsUp` | 眉を上げる（驚き） |
| `BrowsWorried` | 眉の内側を上げる（心配） |

```csharp
var face = houston.transform.Find("Crew_Houston.Head/Crew_Houston.Face").GetComponent<SkinnedMeshRenderer>();
int open = face.sharedMesh.GetBlendShapeIndex("MouthOpen");
// 話している間：毎フレーム、口を細かく開け閉めする
face.SetBlendShapeWeight(open, talking ? Mathf.PingPong(Time.time * 600f, 100f) : 0f);
```

- 話す：`MouthOpen` を 0 → 40〜100 → 0 と 1秒に 5〜8 回上下させる。ときどき `MouthO`・`MouthWide` を混ぜると単調にならない。台詞の音声があるなら、音量をそのまま `MouthOpen` に入れてもよい。
- まばたき：3〜5 秒に1回、`Blink` を 0.1 秒だけ 100 にする。
- 表情：事故（M05）は `BrowsWorried`、成功は `Smile`。口のキーは足し合わせない（`MouthOpen` と `Smile` を同時に 100 にすると形が崩れる）。眉・目のキーは口と一緒に使える。

### 話すアニメーション `Talk`（#120）

`Crew_Houston` と `Crew_Controller_A/B/C` の FBX に、アニメーションクリップ `Talk` が入っている（12fps・36コマ＝3秒。最初と最後が同じ姿なので、くり返せる）。Unity が FBX を取り込むと、FBX の中にクリップ `Talk` ができる。

- 中身：口（`MouthOpen`・`MouthO`・`MouthWide` の切り替え）、まばたき1回、眉を上げる、頭のうなずきと左右、左腕を1回上げる
- 管制官3種は、同じ動きを時間をずらして入れてある（並べても口がそろわない）
- 使い方：`Animator` を付け、`Talk` を入れた Animator Controller を作る。くり返すには FBX の Inspector → Animation タブ → `Talk` → **Loop Time** にチェックを入れる。話していない間は Animator を止めるか、口を閉じた Idle の状態に移す
- 自分で口を動かしたいとき（音量に合わせるなど）は、クリップを使わずに上の C# の例のようにブレンドシェイプを直接動かす。両方を同時に使うと、Animator が値を上書きする
- 作り方：`build_mission_control.py` の `talk_pose()` が1コマごとの姿を決め、`ck.export_all(root, KIND, anim=…)` が FBX に焼く。見本の動画は作っていない（`tmp/` に確認用の HTML を出しただけ）

体の動き（`parts.json`。クルーと同じく `.Move_` を付けない）：

| 部品 | 動き |
|---|---|
| `Head` | Blender の Z まわり ±40°（左右を見る）。Unity では `localEulerAngles.y = −角度`。うなずきは X まわり ±12° まで |
| `Arm_L`・`Arm_R` | X まわり −150°（前・上へ）〜 +40° |

### まだ確かめていないこと

- Unity への取り込み（Unity を開いていなかった）。Blender で FBX を読み直して、7個のシェイプキーと、クリップ `Talk`（口・頭・腕が動く）が入っていることは確かめた。上の C#、ブレンドシェイプの名前、クリップの見え方は Unity で未確認。
- core の `export.py` を直した（シェイプキーも一緒に Y-up に直す）。シェイプキーのないモデルには影響しない。
- 肌の色は全員 `cream`（パレット16色にほかの肌色がない）。

## 月の表面（#54、`build_moon_surface.py`）

月に目・口の顔は付けない（#54 の決定）。代わりに、NASA の Apollo 16 着陸地点（デカルト高地）の地形を月の表面として使う。

| FBX | 中身 | 三角数 |
|---|---|---|
| `Scene_MoonSurface` | 一辺 20m の四角い地面の板（本物は 30km 四方。1/1500）。クレーター・平地・高地。部品は `Ground` 1つ、可動部なし | 5,200 |

- 元データ：`modeling/source/nasa/Apollo16_Landing_Site.stl`（約14万三角。出典は `modeling/source/README.md`）。スクリプトが高さを読み、三角を減らし、パレットの灰色4色で塗る。
- 高さ：元データは NASA が 35 倍に誇張してある。見た目のためにさらに 1.8 倍した（合わせて本物の 63 倍）。いちばん高い所で 2.07m。
- 色：平地 `gray_light`、まわりより高い所（クレーターのふち・丘）`white`、まわりより低い所と急な斜面 `gray`、板の横と底 `gray_dark`。面は平らに塗る（ローポリの見た目）。
- 向き：元データの北が奥（Blender の +Y＝Unity の −Z）。原点は板の底の中心。
- 使い方（案）：月が大きく映る場面の地面。Unity 側で好きな大きさに拡大して、カメラの下に流す。四角い板なので、端が画面に入らないように置く。どの場面で使うかは未決定（窓の外の景色 #126 と合わせて iizuka と決める）。
- `.meta` はまだ無い（Unity を開くと作られる。作られたらコミットする）。

見本：`renders/Scene_MoonSurface_1_game.png`。
