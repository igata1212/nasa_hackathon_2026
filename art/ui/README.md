# art/ui（UI 素材を作るスクリプト）

書いた人：iizuka（作成：Haiku 5.5）／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-15、#190 あつ森に寄せた形と書体）

UI ガイドライン（下書き）の 2〜5 章に合わせて、書体・アイコン・白い形（角丸・ピル・▼・斜め縞）を作る。出力は `unity/Assets/Art/UI/`（`Fonts/`・`Icons/`・`Sprites/`）。元の書体と SVG は `tmp/ui_ref/` から読む（コミットしない）。

作り直し（リポジトリ直下で）：

    bash art/ui/make_fonts.sh
    uv run --with resvg-py --with pillow art/ui/make_icons.py
    uv run --with pillow art/ui/make_sprites.py              # ＋ --preview で見本の絵 art/ui/preview_acnh.png も作る

Unity 側の設定（Sprite 化・Sprite Atlas・TextMeshPro の Font Asset）は Unity で行う。このスクリプトは Unity を開かない。

## #190 で足した物（UI ガイドライン 1・2・5 章、`tmp/handoff/190-plan.md` 1 章）

形は全部 `make_sprites.py` の式で描いた（基準の画像 `docs/reference/art-ref/acnh/` は見比べただけ。なぞっていない）。白＋アルファで、色は Image の Color で付ける（`glove_point` だけ色つき）。

| ファイル | 大きさ | 9-slice の border（左・上・右・下） | Image | 形（基準） |
|---|---|---|---|---|
| `blob_bubble.png` | 384×256 | 120・96・120・96 | Sliced | 会話の吹き出し。超楕円（指数 3.2）に半径 ±2.5% の波（3 つ＋5 つ）、下の辺に ±4px の波 4 つ（【12】）。船内の高さ 180 では Multiplier 1.33 |
| `blob_panel.png` | 192×192 | 72・72・72・72 | Sliced | パネル・選択肢・盆・計器。角の半径 左上 52・右上 64・右下 56・左下 68（超楕円 2.6）、辺の真ん中を 3px ふくらませる（【10】【05】）。**選択肢の吹き出しは Multiplier 0.62**（角が高さの半分近くになり、【10】の豆形になる） |
| `blob_pill.png` | 192×96 | 48・0・48・0 | Sliced | 名前タグ・品名の札・チップ・ボタン・マーカー。両端は超楕円 2.5、上下の辺を 2px ふくらませ、左端（高さ 92）を右端（88）より太く（【12】【09】）。Multiplier ＝ 96 ÷ 高さ（96→1、88→1.09、72→1.33、64→1.5、56→1.71、48→2、40→2.4） |
| `blob_disc.png` | 128×128 | なし | Simple | アイコンの台・丸い枠・チェックの丸・タブの帯のこぶ。超楕円 2.4 に ±2% の波 3 つ（【01】【10】） |
| `wave_edge.png` | 96×24 | なし | Tiled（Wrap Mode = Repeat） | 面の端の波の縁。下 12px が白、上に半径 24 の円の頭のこぶ 2 つ（周期 48、高さ 12、谷は丸めた）。盆の上端に、下半分を盆に重ねて置く。帯の下の縁（【04】の向き）にするときは上下を返す（Scale Y = −1） |
| `caret_soft.png` | 32×22 | なし | Simple | 吹き出しの ▼（角の半径 5）。品名の札の下の小さな三角にも縮めて使う（【12】） |
| `bands64.png` | 4×128 | なし | Tiled（Wrap Mode = Repeat） | パネルの中の横縞。上 64 が透明・下 64 が白。Color を `panelBand` に（【05】） |
| `glove_point.png` | 144×144 | なし | Simple、**Color は白のまま** | 指さしの手（宇宙服のグローブ）。面 `panel`・輪郭 6px `text`・手首の輪 `accent`。表示は 72×72。右向き。指の先は (136, 56)（Pivot にするなら 0.94, 0.61）。左上向きは Rotation Z = 135（【09】【10】【05】は役目と大きさだけの基準） |

- Import（共通）：Texture Type = Sprite (2D and UI)、Mesh Type = Full Rect、Compression = None。border は Sprite Editor で上の数字を入れる
- 斜めの縞は今の `stripes64.png`（白の 40%）。主ボタンの縞を `goLight` 35% にするなら Color のアルファを 0.875
- 見本の絵 `art/ui/preview_acnh.png`：全部の sprite を cream と濃紺の上に並べ（点線＝9-slice の border）、Unity での組み方（9-slice・色・重ね方）をまねて吹き出し・選択肢・ボタン・数のチップ・タブの帯・パネル・盆を組んだもの。ゲームには入れない
- 書体：`Nunito-ExtraBold.ttf`（Nunito の wght 800。見出し・ボタン・名前・札・選択肢）。Font Asset は Unity で作る。Nunito には `→` と `✓` の字が無い（Font Asset の文字に入れても出ない。✓ は `check` アイコンで描く）

## 出典とライセンス

| 素材 | 作者 | ライセンス | 元 |
|---|---|---|---|
| Ionicons（filled） | Ionic | MIT（`unity/Assets/Art/UI/Icons/LICENSE-ionicons.txt`） | https://github.com/ionic-team/ionicons |
| Baloo 2 | The Baloo 2 Project Authors | SIL OFL 1.1（`unity/Assets/Art/UI/Fonts/OFL-Baloo2.txt`） | https://github.com/google/fonts/tree/main/ofl/baloo2 |
| Nunito | The Nunito Project Authors | SIL OFL 1.1（`unity/Assets/Art/UI/Fonts/OFL-Nunito.txt`） | https://github.com/google/fonts/tree/main/ofl/nunito |
| Zen Maru Gothic | The Zen Maru Gothic Project Authors | SIL OFL 1.1（`unity/Assets/Art/UI/Fonts/OFL-ZenMaruGothic.txt`） | https://github.com/google/fonts/tree/main/ofl/zenmarugothic |

角丸・ピル・▼・斜め縞・#190 の形（`blob_*`・波の縁・横縞・グローブの手）は `make_sprites.py` で描いた（外部の素材は使っていない）。
