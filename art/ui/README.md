# art/ui（UI 素材を作るスクリプト）

書いた人：iizuka（作成：Haiku 5.5）／ 最終更新：2026-11-14

UI ガイドライン（下書き）の 2〜5 章に合わせて、書体・アイコン・白い形（角丸・ピル・▼・斜め縞）を作る。出力は `unity/Assets/Art/UI/`（`Fonts/`・`Icons/`・`Sprites/`）。元の書体と SVG は `tmp/ui_ref/` から読む（コミットしない）。

作り直し（リポジトリ直下で）：

    bash art/ui/make_fonts.sh
    uv run --with resvg-py --with pillow art/ui/make_icons.py
    uv run --with pillow art/ui/make_sprites.py

Unity 側の設定（Sprite 化・Sprite Atlas・TextMeshPro の Font Asset）は Unity で行う。このスクリプトは Unity を開かない。

## 出典とライセンス

| 素材 | 作者 | ライセンス | 元 |
|---|---|---|---|
| Ionicons（filled） | Ionic | MIT（`unity/Assets/Art/UI/Icons/LICENSE-ionicons.txt`） | https://github.com/ionic-team/ionicons |
| Baloo 2 | The Baloo 2 Project Authors | SIL OFL 1.1（`unity/Assets/Art/UI/Fonts/OFL-Baloo2.txt`） | https://github.com/google/fonts/tree/main/ofl/baloo2 |
| Nunito | The Nunito Project Authors | SIL OFL 1.1（`unity/Assets/Art/UI/Fonts/OFL-Nunito.txt`） | https://github.com/google/fonts/tree/main/ofl/nunito |
| Zen Maru Gothic | The Zen Maru Gothic Project Authors | SIL OFL 1.1（`unity/Assets/Art/UI/Fonts/OFL-ZenMaruGothic.txt`） | https://github.com/google/fonts/tree/main/ofl/zenmarugothic |

角丸・ピル・▼・斜め縞は `make_sprites.py` で描いた（外部の素材は使っていない）。
