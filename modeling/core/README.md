書いた人：iizuka（作成：Fable 5.2、追記：Opus-5、Opus 5.5）／ 最終更新：2026-11-14

# modeling/core：3D モデル制作の共通の土台（#34）

全員がここのキットとルールで作る。計画は `docs/reference/modeling-plan.md`。ここはその C 章（ルール）と E 章（チェック基準）を、作る人がそのまま使える形にしたもの。

## 1. 流し方

Blender 5.1.2 のコマンドラインだけを使う（GUI の Blender／MCP は使わない）。

```sh
modeling/core/run.sh modeling/fx/build_alarm.py        # 作る → 書き出し → レンダー
modeling/core/run.sh modeling/core/check.py modeling/fx   # 機械チェック（フォルダ、FBX 1つ、引数なし＝全部）
modeling/core/run.sh modeling/core/render_all.py          # 全部を並べた一覧（modeling/renders/all_lineup.png・all_exterior.png）
python3 modeling/core/palette.py                        # パレットを作り直す（色を変えたときだけ）
```

新しいアセットは `build_template.py` を `modeling/<種類>/build_<名前>.py` にコピーして始める（冒頭の3か所を直す）。雛形はそのまま流しても `tmp/modeling_trial/` に出るだけで、`modeling/` と `unity/` は汚さない（`ck.trial()`）。見本は `modeling/fx/build_alarm.py`。

| ファイル | 中身 |
|---|---|
| `corekit.py` | 形を作る関数と仕上げ（元：midori の `kit.py`）。`import corekit as ck` |
| `palette.py` → `palette.png`・`palette.json` | 16色。256×256、4×4 マス。色名 → マス中心の UV |
| `export.py` | 原点に置いて FBX・glb・`stats.json` を出す。FBX の設定はここ1か所 |
| `render_check.py` | 同じカメラ・光で PNG 3枚＋320px 版 |
| `render_all.py` | 全アセットを並べた一覧（`export/*.glb` を読む）。目で見る10項目の判定用 |
| `check.py` | 下の「5. チェック基準」を機械判定 |
| `build_template.py` | 雛形 |
| `build_axis_check.py` | 軸の確認用モデル（FBX の設定や Unity の版を変えたときだけ使う） |
| `run.sh` | `Blender -b --factory-startup --python <script> -- <引数>` |

Unity 側（`unity/Assets/Editor/`。どれも取り込みのたびに自動で動く）：

| ファイル | 中身 |
|---|---|
| `ModelImportReport.cs` | `Assets/Models/` の FBX を取り込むたびに、全部品の位置・回転・スケール・メッシュの大きさを `[ModelImportReport]` でログに出す（`unity/Logs/Editor.log`） |
| `CorePaletteImport.cs` | `palette.png` の取り込み設定を決める。パレットの FBX の `M_Core_Palette` を共通マテリアルに差し替える（下の4章「色」） |
| `CorePalette.cs` | 共通マテリアル `Assets/Models/Core/M_Core_Palette.mat` がなければ作る。メニュー **Tools/Models/Core Palette を確かめる** で、全モデルのマテリアルを `[CorePalette]` でログに出す |

## 2. corekit の使い方

```python
root = ck.asset("Fx_Alarm")                        # ルート（Empty）。以後の部品は子になり <ルート>.<部品> の名前が付く
ck.box("Body", 中心, (幅X, 奥行Y, 高さZ), "cream")  # ほかに cyl / sph / dome / ico / tor / tube
ck.ctrl_base(0.2, 0.2)                             # 操作部品の共通台座（部品名 Base、0.2m の倍数、底が z=0）
m = ck.join([a, b], "Move_Lever")                  # 色の違う形を1つのメッシュに
ck.set_origin(m, (x, y, z))                        # 原点を回転軸の上へ（世界座標。省くと底面の中心）
ck.attach(child, m)                                # 可動部と一緒に動く部品を子にする
ck.slot("Slot_1", (x, y, z))                       # 取り付け位置（Empty）
ck.end_asset()
ck.write_parts("fx", root, parts)                  # 可動部の表 parts.json
ck.export_all(root, "fx")                          # 仕上げ → FBX・glb・stats.json
ck.render_check(root, "fx", parts)                 # renders/ に画像
```

- **色**は色名を渡すだけ（`paint` が UV をマス中心に寄せる）。1部品1色。2色以上のメッシュは色ごとに作って `join`。後から塗り替えるときは `ck.paint(obj, "色名")`。
- **分割数**は既定のまま使う：円柱・球 16、ヘルメットだけ `seg=ck.SEG_HELMET`（24）、トーラス 24×8、ベベル2段。
- **ベベル**は既定で 2cm・2段。薄い部品は厚みの 1/4 まで自動で縮む。大きい形は `bevel=0.03` や `0.04` を渡す。`bevel=0` でなし。
- `finalize`（`export_all` が呼ぶ）が、回転・スケールの適用、ベベルの確定、三角化、重み付き法線、名前とマテリアルの確認をする。ルール外なら `AssertionError` で止まる。
- 1つのスクリプトで数個作るとき（クルー4色など）は、`asset()`〜`export_all()`〜`render_check()` を繰り返せばよい（レンダーはほかのアセットを隠す）。
- 自分で形を作るとき（bmesh の押し出しなど）は `ck._fin(o, 色名, 部品名, bevel=…)`・`ck._apply(o)`・`ck._bevel(幅, 薄さ)` を使ってよい。名前は「_」始まりだが、いくつもの build_*.py が使っているので名前と引数は変えない。
- 可動部の表は1フォルダに数ファイルあってよい（`parts.json` と `parts_*.json`。例：操作部品の `parts_a.json`・`parts_b.json`）。`check.py` と `render_check.py` は全部を読む。同じルートが2つのファイルにあって中身が違うと `check.py` の「原点」が不合格になる。

## 3. ルール

**パレット16色**（`palette.json` の色名）

| | 1 | 2 | 3 | 4 |
|---|---|---|---|---|
| 船内 | `cream` #F2E3C6 | `orange` #F28C28 | `teal` #2A9D8F | `navy` #1D2D50 |
| 席 | `seat_red` #E4572E（Pilot ▲） | `seat_blue` #3A86FF（Mission Planner ◆） | `seat_yellow` #FFC857（Comms & Power ■） | `seat_green` #6BBF59（Life Support ●） |
| 無彩色 | `white` #FBF7EE | `gray_light` #C8CCD4 | `gray` #8B93A1 | `gray_dark` #4B5263 |
| 黒紺＋ランプ専用 | `visor` #0E1630 | `lamp_green` #2ECC71 | `lamp_yellow` #FFD60A | `lamp_red` #FF3B30 |

| 項目 | ルール |
|---|---|
| マテリアル | 全メッシュ `M_Core_Palette` 1つ。例外は `M_Body_Earth`・`M_Body_Moon`。透明なし |
| ランプ | `lamp_*` の3色は `.Lamp_〜` の部品だけ。Blender では塗るだけ。発光は Unity で `Lamp_` の部品のマテリアルを差し替える |
| 単位・向き | 1 unit = 1m。Z 上、正面 −Y。ルートは位置0・回転0・スケール1。子もスケール・回転適用済み |
| 原点 | 床置きは底面中心。操作部品は台座の底の中心。可動部は回転軸の上。地球・月は球の中心 |
| 宇宙船の例外 | `Ship_` は原点＝ノズル出口の中心、長軸＝+Z（司令船の先端が +Z）。発射台に立てた姿で「底面中心」と同じ扱い（底が z=0） |
| 命名 | ルート `Ctrl_` `Console_` `Crew_` `Tank_` `Ship_` `Body_` `Fx_` `Part_` `Prop_`＋大文字始まり（`Part_` は設計画面の部品（#48）。`Prop_` は船内に浮く小物：三角 800 以下、原点は中心、`modeling/props/` → `unity/Assets/Models/Props/`）。子 `<ルート>.<部品>`。可動部 `.Move_〜`、ランプ `.Lamp_〜`、取り付け位置 `.Slot_〜`。`.001` 不可。入れ子のアセット（`Ship_Stack` の下の `Ship_CM`）はルートと同じ形の名前で、その部品は `Ship_CM.<部品>` |
| 決まった部品名 | 操作部品の台座は `.Base`（`ck.ctrl_base`）。コンソールの天板は `.Top`（check.py が高さ 0.80m を見る）。破れたタンクは名前に `Broken` を入れる（三角数の上限が 2,000 になる） |
| 可動部の表 | 各フォルダに `parts.json`（`ck.write_parts`）。部品名・種類（rotate／translate）・軸（部品のローカル軸、Blender）・範囲・初期値。原点と Unity での軸・符号は自動で入る。角度は度、押し込み量などは m |
| ベベル | 2〜4cm・2段。最小の太さ 2cm |
| 書き出し | FBX → `unity/Assets/Models/<種類>/`、glb と `stats.json` → `modeling/<種類>/export/` |
| フォルダ | `modeling/{controls,console,crew,tank,ship,bodies,fx,part,props}/`（`build_*.py`・`renders/`・`export/`・`parts.json`）。Unity は `unity/Assets/Models/{Controls,Console,Crew,Tank,Ship,Bodies,Fx,Part,Props,Core}/`。自分のフォルダ以外に書かない |
| 触らない | `modeling/PhysDraft-SpaceCrew3D/` と `unity/Assets/Models/NASA/`。import するスクリプトは `sys.dont_write_bytecode = True` |
| .blend | コミットしない（スクリプトから作り直せる） |
| .meta | 手で書かない。Unity に作らせる（Unity を前に出すと取り込みが走る） |
| 旗・文字 | 付けない（NASA の記章も） |

## 4. Unity での向き（6000.6.9f1 に入れて確かめた）

見本 `Fx_Alarm` と軸の確認用モデルを取り込み、`ModelImportReport` の数値で確かめた結果。

| Blender | Unity |
|---|---|
| ルート・子・孫 | 全部 回転 (0,0,0)・スケール (1,1,1)。寸法は m のまま（0.3m → 0.3） |
| 位置 (x, y, z) | (−x, z, −y) |
| 正面 −Y | **+Z（transform.forward）** |
| 上 +Z | +Y |
| 右 +X | −X |
| X軸まわり +θ | X軸まわり +θ |
| Y軸まわり +θ | Z軸まわり +θ |
| Z軸まわり +θ | Y軸まわり −θ |

`parts.json` の `unity_axis`・`unity_sign` はこの表のとおりに自動で入る（例：Blender の Z まわり → Unity では `localEulerAngles.y = −角度`）。

**FBX の設定**（`export.py` の `FBX_SETTINGS`）：`apply_scale_options="FBX_SCALE_UNITS"`、`axis_forward="-Z"`、`axis_up="Y"`、**`bake_space_transform=False`**、`mesh_smooth_type="OFF"`、`path_mode="STRIP"`。Unity の取り込み設定は既定のまま（マテリアルの差し替えだけ自動で入る。下の「色」）。

midori と同じ `bake_space_transform=True` は**使わない**。Blender 5.1.2 の FBX 書き出しは、これを True にすると孫（子の子）の位置が狂い、90° 回って入る（子までは正しい）。代わりに `export.export_fbx()` が、書き出す間だけ自分で全部品を Y-up に組み替える（頂点も行列も）。そのため **FBX は必ず `ck.export_all()` か `export.export_fbx()` で出す**。`bpy.ops.export_scene.fbx` を直接呼ばない。

色：FBX に入る画像のパスはマシンに依らない（同じフォルダの画像はファイル名だけ、`palette.png` は `Assets/Models/Core/palette.png` と `../Core/palette.png`）。`/Users/...` は入らない（`export._no_abs_image_paths`）。

**色（共通マテリアル、#38）**：Unity は FBX の中の `M_Core_Palette` に `Core/palette.png` を貼らない（同じフォルダか、上の階層の `Textures/` しか探さない。2026-11-14 に確かめた）。そこで取り込みのときに、共通のマテリアル1つに差し替える。

| 物 | 中身 |
|---|---|
| `Assets/Models/Core/M_Core_Palette.mat` | URP Lit、Base Map＝`palette.png`、Base Color 白、Smoothness 0、Metallic 0。Unity が作ったもの（手で書かない） |
| `palette.png` の取り込み設定 | Point フィルタ・ミップマップなし・圧縮なし・sRGB・Clamp（マスの色がにじまない・変わらない）。`CorePaletteImport.OnPreprocessTexture` が毎回決める |
| FBX の差し替え | `CorePaletteImport.OnPreprocessModel` が `ModelImporter.AddRemap` で「FBX の `M_Core_Palette` → 共通マテリアル」を付ける。`.fbx.meta` の `externalObjects` に残るので、ほかの人の環境でも同じになる。FBX ごとのマテリアルは作られない |
| 対象 | `Assets/Models/` の `.fbx` のうち `NASA/`（midori の物）と `Bodies/`（地球・月は自分の画像の `M_Body_Earth`・`M_Body_Moon`）を除く全部 |

- **新しいモデルを足したとき**：何もしなくてよい。`build_*.py` で `Assets/Models/<種類>/` に FBX を出せば、取り込みのときに自動で差し替わる（`.meta` も Unity が作り、差し替えが入る）。`Assets/Models/` の下に新しいフォルダを作っても同じ（`NASA/`・`Bodies/` だけが対象外）。
- **共通マテリアルがないとき**（消した、など）：FBX は差し替えずに（灰色で）取り込み、取り込みのあと `OnPostprocessAllAssets` が共通マテリアルを作る。それを待っている FBX は Unity が自動で読み直し、こんどは差し替わる。手でマテリアルを作らない。
- **パレットの色を変えたとき**：`python3 modeling/core/palette.py` で `palette.png` を作り直すだけ。共通マテリアルはその画像を指しているので、そのまま新しい色になる。
- **確かめ方**：メニュー Tools/Models/Core Palette を確かめる（UnityMCP なら `execute_code` で `CorePalette.Report();`）。`[CorePalette] 合計 OK=31 NG=0 対象外=2` が出れば全部差し替わっている（2026-11-14）。見た目は `Assets/Scenes/ModelCheck.unity`（33個を並べた確認用シーン）。
- **ランプの発光**（#55）：FBX のランプは共通マテリアルのまま（光らない）。ゲームの中で `LampGlow`（`Assets/Scripts/Visual/LampGlow.cs`）を付けると、子の `.Lamp_Red`／`.Lamp_Yellow`／`.Lamp_Green` を発光マテリアル `Assets/Art/Materials/M_Lamp_*.mat` に差し替えて点滅させる。船室の天井灯 `Lamp_Cabin` は点滅せず、暖色の白で常に発光する。取り込み時に差し替えるかは未決定（`docs/design/art-direction.md`）。

## 5. チェック基準（`check.py` が FBX を読み直して判定）

| 項目 | 合格 |
|---|---|
| 三角数 | 操作部品 1,500／コンソール 3,000／クルー 3,000／タンク 1,500（破れ 2,000）／宇宙船 6,000／地球・月 各 2,500／警報・パイプ 1,500／設計画面の部品（`Part_`）1,500 以下 |
| スケール | 全部 (1,1,1)。クルー高さ 1.20±0.02m、天板（`.Top`）0.80±0.01m、操作部品の台座（`.Base`）0.2m の倍数 |
| パレット | `M_Core_Palette` だけ（地球・月を除く）。UV はマス中心から 1/4 マス以内。ランプ色は `Lamp_` だけ |
| 原点 | ルート (0,0,0)、底面 z=0±1mm（地球・月は中心が原点）。`parts.json` の部品が別オブジェクトで、原点が表の値と一致 |
| 命名 | `^(Ctrl|Console|Crew|Tank|Ship|Body|Fx|Part)_[A-Z][A-Za-z0-9_]*$`、`.001` なし |
| 太さ | 部品ごとの外形の、いちばん薄い向きが 2cm 以上 |
| 容量 | FBX 1つ 500KB 以下、追加合計 30MB 以下（#34 では 20MB。モデルが増えたので広げた）（FBX・glb・レンダー・テクスチャなど。`modeling/source/`（ダウンロードした元データ）、midori のフォルダ、NASA の FBX、.blend は数えない） |
| 再取り込み | FBX を読み直して、三角数・寸法・全部品の原点が書き出し時（`stats.json`）と同じ |

試し書き（`ck.trial()`）のフォルダを渡すと（例：`check.py tmp/modeling_trial/fx`）、試し書きの FBX と、その横の `parts*.json`・`stats.json` を使う（本番より先に見る）。

「太さ」について：計画の「一番短い辺 2cm 以上」は、ベベル2段や16角形の円柱だと辺そのものは必ず 2cm を切るので、「部品の外形のいちばん薄い向き」と読んで判定している。斜めに置いた薄い板は実際より厚く測られるので、そこは目で見る。

目で見る10項目（9/10 以上で合格。`renders/` の画像で人が見る）：320px でも操作部品が見分けられる／席の色とアイコンが読める／ベベルの光が見える／面のちらつきなし／可動姿勢でめり込まない／ヘルメットと胴が別パーツに見える／目が見える／宇宙船が円錐＋円筒＋ノズルに見える／地球・月の継ぎ目が目立たない／全部並べて同じゲームの物に見える。

## 6. レンダー確認（`render_check.py`）

暖色の平行光1灯（`#FFE2B8`）＋環境光、背景は濃紺、960×600。PNG は軽くする設定で保存する（`render_check.output_settings()`：ディザ 0、RGB、8bit、圧縮 100。1枚 約520KB → 約70KB）。`rc._render()` が毎回この設定をかけるので、各フォルダの一覧画像も同じになる。

| 画像 | 中身 |
|---|---|
| `<名前>_1_game.png` | ゲームと同じ角度（縦の画角 30°、見下ろし 50°、正面から）。アセットと 1.2m のダミーが入る距離 |
| `<名前>_1_game_320.png` | 上の 320px 版 |
| `<名前>_2_front.png` | 斜め前の寄り |
| `<名前>_3_move.png` | 可動部を最大まで動かした姿（可動部がなければ斜め後ろから） |

カメラの距離はいつもアセットの外接箱から決まるので、実寸の宇宙船でも画面に収まる。仮の床（直径 6m）は、アセットが床に収まらないときは自動で外れる。`ck.render_check(root, kind, parts, floor=True/False/"auto", dummy=True/False)` で選べる（単体で流すときは `--floor`／`--no-floor`／`--no-dummy`）。ゲームのカメラの距離は未定（#9）なので、1枚目は「角度と画角だけゲームと同じ」。

## 7. Unity に入れて確かめる手順

1. `build_*.py` を流す（FBX が `unity/Assets/Models/<種類>/` に出る）。
2. Unity を前に出す（エディタが開いているとき。バッチモードは同時に起動できない）。取り込みは「ほかのアプリから Unity に戻った瞬間」に走るので、もう前に出ているときは、いちど別のアプリを前にしてから戻す。下のコマンドで切り替わらないことがある（2026-11-14 に1回あった）。`Editor.log` が更新されないときは、手でほかのアプリをクリックしてから Unity をクリックする。
   ```sh
   PID=$(pgrep -f "Unity.app/Contents/MacOS/Unity -projectpath")
   osascript -e 'tell application "Finder" to activate'; sleep 2
   osascript -e "tell application \"System Events\" to set frontmost of (first process whose unix id is $PID) to true"
   ```
   2026-11-14（2回目）は、このコマンドでも `open -a` でも走らなかった（Unity が前のまま、ほかのアプリが前に出なかった）。
   代わりに Unity MCP（`.mcp.json` の `UnityMCP`）の `refresh_unity`（`mode: if_dirty`、`scope: assets`）で走った。
3. `grep "ModelImportReport\]" unity/Logs/Editor.log | tail -40` で、ルートが回転0・スケール1、`.Move_` の `pos` が `parts.json` の `origin` を (−x, z, −y) に直した値、`world size` が m の寸法になっているかを見る。
4. 色：メニュー Tools/Models/Core Palette を確かめる で、新しいモデルが `[CorePalette] OK` になっているかを見る（4章「色」）。

## 8. 決定事項（2026-11-14、#34 の第1段のまとめ）

| 項目 | 決めたこと |
|---|---|
| 席の色 | `palette.json` の `seat_red`＝Pilot（▲）、`seat_green`＝Life Support（●）、`seat_yellow`＝Comms & Power（■）、`seat_blue`＝Mission Planner（◆） |
| クルーの可動部 | `Helmet`・`Arm_L`・`Arm_R`・`Leg_L`・`Leg_R`（`.Move_` を付けない例外。`Torso`・`Backpack` は動かさない部品）。目は `Eyes_Normal`・`Eyes_Panic`・`Eyes_Relief` の3つが FBX に入っていて、Unity で表示を切り替える |
| 宇宙船 | 原点＝ノズル出口の中心、長軸 +Z（司令船の先端が +Z） |
| バルブ | `Ctrl_Valve`（直径 0.3m）はコンソールの 0.2m 間隔の取り付け位置ではなく、タンクの `.Slot_Valve` に付ける想定 |
| かき混ぜスイッチ | `Ctrl_Stir` は「ひねると戻るつまみ」で作った。SPEC の M04 は「押す」なので未決定（押す形にするなら作り直す） |
| 送信ボタン | `Ctrl_Transmit` は ON の間は押し込まれたまま（−0.02m） |
| ルート切替レバー | `Ctrl_RouteLever` の初期値は −30（A 側） |

## 9. 船室（#40、追記：Fable 5.2、2026-11-14）

船室 `Ship_Interior` は、元データが `modeling/interior/`、FBX が外観と同じ `unity/Assets/Models/Ship/`。設計と Slot の一覧は `modeling/interior/layout.md`。core を次のように直した。

| ファイル | 直したこと |
|---|---|
| `export.py` | `KINDS` に `"interior": "Ship"` を足した（`ck.export_all(root, "interior")` で FBX は `Ship/`、glb と `stats.json` は `modeling/interior/export/` に出る） |
| `check.py` | 三角数の上限：`Ship_` で名前に `Interior` を含む物は 10,000（art-direction.md の「船室 10,000 以下」）。ほかの `Ship_` は 6,000 のまま |
| `check.py` | `Ship/` に外観と船室の FBX が同居するので、フォルダを渡したとき（`check.py modeling/interior`・`check.py modeling/ship`）は、そのフォルダの `export/<名前>.stats.json` がある FBX だけを見る。`parts.json` と `stats.json` も、その FBX を書き出したフォルダの物を使う |

船室の原点は船底の中心（底が z=0）。床の上面は z=0.30 で、置き場所は `.Slot_〜` が持っている。

## 10. 設計画面の部品 `Part_`（#48、追記：Taiga・Fable 5.2、2026-11-14）

設計画面（M01）で選ぶ部品の名前の頭を `Part_` にした（#48 の案。#45 の必須の4つも同じ頭で作れる）。元データは `modeling/part/`、FBX は `unity/Assets/Models/Part/`。core を次のように直した。

| ファイル | 直したこと |
|---|---|
| `corekit.py`・`check.py` | ルートの名前に `Part_` を足した |
| `check.py` | 三角数の上限：`Part_` は 1,500 |
| `export.py` | `KINDS` に `"part": "Part"` を足した |

設計画面用のアイコン（PNG）は、大きさと背景が未決定。決まるまでは `renders/` の画像（`_2_front.png`）を使う。

## 11. 場面の背景 `Scene_`（#52・#53、追記：Taiga・Fable 5.2、2026-11-14）

場面の背景（発射台・地面・管制室の部屋）の名前の頭を `Scene_` にした。元データは `modeling/scene/`、FBX は `unity/Assets/Models/Scene/`。その場面にだけ出る人（管制室の先生など）も同じフォルダに置く。core を次のように直した。

| ファイル | 直したこと |
|---|---|
| `corekit.py`・`check.py` | ルートの名前に `Scene_` を足した |
| `check.py` | 三角数の上限：`Scene_` は 6,000 |
| `export.py` | `KINDS` に `"scene": "Scene"` を足した |

各場面の部品と置き方は `modeling/scene/README.md`。
