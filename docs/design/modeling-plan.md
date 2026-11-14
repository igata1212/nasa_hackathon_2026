書いた人：iizuka（計画：Fable 5.2）／ 最終更新：2026-11-14

# 3D モデル制作の計画（#34）

#9 がどう決まっても必要なモデルを、既存データを改変・参考にして作る。精度重視（90%）。

## 0. 食い違いと、この計画での扱い

| # | 食い違い | 扱い |
|---|---|---|
| 1 | art-direction は「glb＋glTFast」。glTFast は未導入で、midori は FBX を置いた | **FBX を正とし、glb も併せて出す** |
| 2 | 「影は頂点カラーに焼く」と「URP Lit の共通マテリアル1つ」は両立しない | 頂点カラー焼きはやめる |
| 3 | 「16色パレット」だが定義は8色。状態色はモデルに塗らないのに警報ランプは要る | 残り8色を提案（下）。状態色は `Lamp_` の部品だけ |
| 4 | 席の色と状態色がぶつかる。どの席が何色かも未定 | 仮：Pilot=赤、Life Support=緑、Comms & Power=黄、Mission Planner=青。席アイコンの形を必ず付ける |
| 5 | SPEC 6章の `correction_burn`、`swap_filter`、2席目の `ignite` の部品がない | 汎用押しボタン `Ctrl_Button` で3用途をまかなう |
| 6 | 船室は art-direction で必須だが、カメラ未定（#9） | 今回は作らない。確認用に直径6m の仮の床だけ core に置く |
| 7 | midori は3役・1.5m・3頭身、art-direction は4席・1.2m・2〜2.5頭身 | art-direction に合わせる。midori のファイルは触らない |
| 8 | midori のスクリプトは作者のマシンの絶対パス固定 | core に写して `__file__` 基準に直す |
| 9 | midori の .blend は Blender 5.3 製、ここは 5.1.2 | 開くだけ。上書き保存しない |
| 10 | midori の README「アポロ13号の飛行中は脚が畳まれていた」は要確認 | 脚は開いた状態で作る。midori に確認 |

実測：midori のモデルは三角数が上限を大きく超え（クルー約13,000、小物 2,600〜6,000、Apollo13_Stack 111,550）、マテリアルも1モデル3〜12個。**メッシュは流用できない。スクリプトは流用できる。** `kit.py` は Blender 5.1.2 のコマンドラインでそのまま動き、EEVEE レンダーと glb/FBX 書き出しも成功（Unity 取り込みは未確認）。

## A. モデルごとの方針

| # | モデル | 方針 | 下敷き |
|---|---|---|---|
| 1 | 操作部品 | 新規（kit の box/cyl/tor） | なし |
| 2 | コンソール台 | 新規 | `room.py` の Station_Pilot の寸法感 |
| 3 | クルー | 参考（作り直し） | `crew_v5.py`（部品ごとに別オブジェクト、原点は関節）。比率・分割数・顔を変える。ボーンなし |
| 4 | 酸素タンク | 参考 | アポロ機械船の酸素タンクは球形。破れ版は穴とめくれた板 |
| 5 | 宇宙船の外観 | 参考 | NASA メッシュで寸法だけ取り、円錐＋円筒＋ノズルで作り直す |
| 6 | 地球・月 | 新規の球＋NASA 画像を縮小・減色 | Blue Marble、CGI Moon Kit |
| 7 | 警報ランプ・破れたパイプ | 改変 | `props.py` の `alarm()` `broken_pipe()` を core で組み直し、分割数を下げ、煙・火花は外す |

`modeling/core/corekit.py` は midori の `kit.py` を写して作る（冒頭に出典）。分割数を下げる（円柱・球 16、ヘルメット 24、トーラス 24×8、ベベル2段）、`paint(obj, "色名")` で UV をパレットのマスに寄せる、`set_origin()`・FBX 書き出し・三角数集計を足す。

## B. ダウンロードするデータ（合計 約3.5MB）

| 用途 | URL | ライセンス |
|---|---|---|
| LM・CSM の下敷き | リポジトリ内 `modeling/PhysDraft-SpaceCrew3D/nasa_source/`（不要） | NASA Media Usage Guidelines |
| 宇宙服の参考 | https://raw.githubusercontent.com/nasa/NASA-3D-Resources/master/3D%20Models/Astronaut/Astronaut.glb | 同上（下敷き専用、書き出しに含めない） |
| 地球の画像 | https://assets.science.nasa.gov/content/dam/science/esd/eo/images/bmng/bmng-topography-bathymetry/december/world.topo.bathy.200412.3x5400x2700.jpg | 同上（Blue Marble Next Generation） |
| 月の画像 | https://svs.gsfc.nasa.gov/vis/a000000/a004700/a004720/lroc_color_poles_1k.jpg | 同上（NASA's Scientific Visualization Studio、CGI Moon Kit） |
| クルーの保険 | https://quaternius.com/packs/ultimatespacekit.html | CC0（クルーが不合格のときだけ） |

置き場所：`modeling/source/nasa/`、`modeling/source/textures/`。根拠：https://www.nasa.gov/nasa-brand-center/images-and-media/

## C. 共通の土台（`modeling/core/`）

| ファイル | 中身 |
|---|---|
| `README.md` | ルール全文と実行のしかた |
| `corekit.py` | 上記＋操作部品の共通台座 `ctrl_base(w, d)` |
| `palette.py` → `palette.png`・`palette.json` | 256×256、4×4 マス。色名→UV |
| `export.py` | 原点に置き FBX と glb を出す |
| `render_check.py` | 同じカメラ・光で PNG 3枚 |
| `check.py` | E の基準を機械判定 |
| `run.sh` | `Blender -b --factory-startup --python "$1" -- <引数>` |

**パレット16色**：船内 クリーム `#F2E3C6`・オレンジ `#F28C28`・青緑 `#2A9D8F`・濃紺 `#1D2D50`／席 赤 `#E4572E`・青 `#3A86FF`・黄 `#FFC857`・緑 `#6BBF59`／無彩色（提案）白 `#FBF7EE`・明るい灰 `#C8CCD4`・灰 `#8B93A1`・暗い灰 `#4B5263`・バイザー黒紺 `#0E1630`／ランプ専用（提案）緑 `#2ECC71`・黄 `#FFD60A`・赤 `#FF3B30`

| 項目 | ルール |
|---|---|
| マテリアル | 全メッシュ `M_Core_Palette` 1つ。例外は `M_Body_Earth`・`M_Body_Moon`。透明なし |
| ランプの発光 | Blender では塗るだけ。Unity で `Lamp_` の部品を差し替え |
| 単位・向き | 1 unit = 1m。Z 上、正面 −Y。ルートは位置0・回転0・スケール1。子もスケール適用済み |
| 原点 | 床置きは底面中心。操作部品は台座の底の中心。可動部は回転軸 |
| 命名 | ルート `Ctrl_` `Console_` `Crew_` `Tank_` `Ship_` `Body_` `Fx_`＋大文字始まり。子 `<ルート>.<部品>`。可動部 `.Move_〜`、ランプ `.Lamp_〜`、取り付け位置 `.Slot_〜`。`.001` 不可 |
| 可動部の表 | 各フォルダに `parts.json`（部品名・回転軸・角度範囲・初期値） |
| ベベル | 2〜4cm・2段。最小の太さ 2cm |
| 書き出し | FBX → `unity/Assets/Models/<種類>/`、glb → `modeling/<種類>/export/` |
| フォルダ | `modeling/{controls,console,crew,tank,ship,bodies,fx}/`（`build_*.py`・`renders/`・`export/`・`parts.json`）。Unity は `unity/Assets/Models/{Controls,Console,Crew,Tank,Ship,Bodies,Fx,Core}/` |
| .blend | コミットしない（スクリプトから作り直せる） |
| .meta | 手で書かない。Unity に作らせる |

FBX の設定は midori と同じ（`apply_scale_options="FBX_SCALE_UNITS"`、`bake_space_transform=True`、`axis_forward="-Z"`、`axis_up="Y"`）から始め、見本を Unity に入れて確かめてから本番に入る。

**レンダー確認**：暖色の平行光1灯（`#FFE2B8`）＋環境光、背景は濃紺、直径6m の仮の床と 1.2m のダミー。(1) ゲームと同じカメラ（FOV 30°、見下ろし 50°）、(2) 斜め前の寄り、(3) 可動部を最大角まで動かした姿。960×600、1枚目の 320px 版も。

## D. 割り当て

第0段は Fable-A が core と見本 `Fx_Alarm`、Fable-B がダウンロードと寸法取り。第1段は6体が並列。自分のフォルダだけに書く。コミットは親がまとめる。

| 担当 | 第1段 | 要点 |
|---|---|---|
| Fable-A | クルー4色 | 1.20m、ヘルメット直径 約0.5m（2.4頭身）。Helmet／Torso／Arm_L・R（原点は肩）／Leg_L・R（原点は股）／Backpack。目は `Eyes_Normal`・`Eyes_Panic`・`Eyes_Relief`。ヘルメットのリングと胴が席の色、胸と背中に席アイコン。一色の豆型と手だけ浮く体型は不可 |
| Fable-B | 宇宙船の外観 | `Ship_Stack` の子に `Ship_CM`・`Ship_SM`（ノズル＋小型噴射器4か所、吹き飛ぶ外板 `.Move_Panel4`）・`Ship_LM`（脚4本は開く）。旗・文字・記章なし。実寸。6,000三角以下 |
| Opus-1 | 操作部品 A：トグル4種、観測機器スイッチ3種、かき混ぜスイッチ | 台座 0.2m 角。種類の違いは色でなく形 |
| Opus-2 | 操作部品 B：バルブ、点火ボタン（カバー付き）、汎用ボタン、姿勢スティック（2軸）、ルート切替レバー、送信ボタン | 点火は丸、送信は四角。押し込み量も `parts.json` に |
| Opus-3 | コンソール台4色＋酸素タンク（通常・破れ） | 天板 0.80m、幅 約1.2m、傾いたパネルに `.Slot_1〜6`。タンクは直径 約0.5m の球、番号は点の数 |
| Opus-4 | 地球・月＋破れたパイプ＋全体の検査 | 球 48×24・半径1、画像 1024×512、北が +Z。最後に `check.py` と一覧画像、不合格を各担当へ |

## E. 品質チェックの基準

| 項目 | 合格 |
|---|---|
| 三角数 | 操作部品 1,500／コンソール 3,000／クルー 3,000／タンク 1,500（破れ 2,000）／宇宙船 6,000／地球・月 各 2,500／警報・パイプ 1,500 以下 |
| スケール | 全部 (1,1,1)。クルー高さ 1.20±0.02m、天板 0.80±0.01m、操作部品の台座 0.2m の倍数 |
| パレット | `M_Core_Palette` だけ（地球・月を除く）。UV はマス中心から 1/4 マス以内。ランプ色は `Lamp_` だけ |
| 原点 | ルート (0,0,0)、底面 z=0±1mm。`parts.json` の部品が別オブジェクトで原点が軸と一致 |
| 命名 | `^(Ctrl|Console|Crew|Tank|Ship|Body|Fx)_[A-Z][A-Za-z0-9_]*$`、`.001` なし |
| 太さ | 一番短い辺 2cm 以上 |
| 容量 | FBX 1つ 500KB 以下、追加合計 20MB 以下 |
| 再取り込み | FBX を読み直して三角数と寸法が同じ |

目で見る10項目（9/10 以上で合格）：320px でも操作部品が見分けられる／席の色とアイコンが読める／ベベルの光が見える／面のちらつきなし／可動姿勢でめり込まない／ヘルメットと胴が別パーツに見える／目が見える／宇宙船が円錐＋円筒＋ノズルに見える／地球・月の継ぎ目が目立たない／全部並べて同じゲームの物に見える。

## F. リスク

| リスク | 対処 |
|---|---|
| ライセンス | NASA の旗・文字・記章を付けない。`CREDITS.md` に名前・作者・URL・ライセンス。midori の kit を写したことも書く |
| 容量 | レンダーは 960×600・1アセット3枚まで。.blend はコミットしない |
| Blender の版 | 全員 5.1.2 のコマンドライン |
| midori との衝突 | `modeling/PhysDraft-SpaceCrew3D/` と `unity/Assets/Models/NASA/` に書かない。import するときは `sys.dont_write_bytecode = True` |
| 並列の衝突 | GUI の Blender（MCP）は使わない。自分のフォルダ以外に書かない |
| 未決定 | 席と色、追加8色、頭身、汎用ボタン、FBX 採用は仮の値で進める。スクリプトの定数なので後から数分で作り直せる |
