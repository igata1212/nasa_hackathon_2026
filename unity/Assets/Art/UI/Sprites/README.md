# UI のスプライト（白い形）

書いた人：iizuka（作成：Haiku 5.5）／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-15、#190 の形）

色は付けていない（白＋アルファだけ）。色は Image の Color で付ける。作り直しは `uv run --with pillow art/ui/make_sprites.py`。角丸の大きさはガイドライン 2 章（小 16・中 28・大 40、ピル＝高さの半分）。

Import（共通）：Texture Type = Sprite (2D and UI)、Pixels Per Unit = 100（既定）、Compression = None。

| ファイル | 大きさ（px） | 9-slice の border（左・上・右・下） | 使う所 |
|---|---|---|---|
| `round16.png` | 48×48 | 16・16・16・16 | 小さい角丸（チップ・入力欄）。Image は Sliced |
| `round28.png` | 84×84 | 28・28・28・28 | 中（カード）。Image は Sliced |
| `round40.png` | 120×120 | 40・40・40・40 | 大（パネル・吹き出し）。Image は Sliced |
| `pill.png` | 128×128（円） | 63・63・63・63 | ピル。高さは下の表の Multiplier で決める。Image は Sliced。名前タグ（44）とゲージ（28）も同じ画像 |
| `caret_down.png` | 24×14 | なし（Simple） | 吹き出しの ▼（表示は 24×14） |
| `stripes64.png` | 64×64（タイル） | なし（Simple） | 危険の斜め縞。Wrap Mode = Repeat。危険の面の子に白のまま重ねる（白の 40%） |
| `blob_bubble.png` | 384×256 | 120・96・120・96 | #190 会話の吹き出し（【12】）。Image は Sliced。船内の高さ 180 では Multiplier 1.33 |
| `blob_panel.png` | 192×192 | 72・72・72・72 | #190 パネル・選択肢・盆・計器（【10】【05】）。Image は Sliced。選択肢の吹き出しは Multiplier 0.62（豆形） |
| `blob_pill.png` | 192×96 | 48・0・48・0 | #190 名前タグ・品名の札・チップ・ボタン（【12】【09】）。Image は Sliced。Multiplier ＝ 96 ÷ 高さ |
| `blob_disc.png` | 128×128 | なし（Simple） | #190 アイコンの台・丸い枠・チェックの丸（【01】【10】） |
| `wave_edge.png` | 96×24（横のタイル） | なし | #190 盆の上端の波の縁（【04】【06】）。Wrap Mode = Repeat、Image は Tiled。下 12px を面に重ねる |
| `caret_soft.png` | 32×22 | なし（Simple） | #190 吹き出しの ▼（角の丸い三角。【12】） |
| `bands64.png` | 4×128（縦のタイル） | なし | #190 パネルの中の横縞（【05】）。Wrap Mode = Repeat、Image は Tiled、Color は `panelBand` |
| `glove_point.png` | 144×144（色つき） | なし（Simple） | #190 指さしの手。Color は白のまま、表示は 72×72。右向き、指の先 (136, 56)。左上向きは Rotation Z = 135 |

#190 の形の作り方と使い方は `art/ui/README.md`。#190 の形は Mesh Type = Full Rect にする。

## ピル（pill.png）の高さ

角の半径 ＝ border ÷ Pixels Per Unit Multiplier。角の半径を高さの半分にするので、Multiplier ＝ 126 ÷ 高さ。

| 高さ | 使う所 | Multiplier |
|---|---|---|
| 96 | 主ボタン | 1.31 |
| 72 | 副ボタン・警告の帯 | 1.75 |
| 64 | カウントダウン | 1.97 |
| 56 | 席バッジ（名前付き） | 2.25 |
| 44 | 名前タグ | 2.86 |
| 28 | ゲージ（溝と中身） | 4.5 |

この式は Unity の仕組みからの目安。Unity で角の丸さを見て直す。
