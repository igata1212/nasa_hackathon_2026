書いた人：iizuka（下書き：Fable 5.2）／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#141 音）

# CREDITS（下書き）

このゲームで使った外部の素材と、ほかの人の成果物。3D モデル制作（#34）の時点の下書き。素材を足したら行を足す。「何に使ったか」は #34 の計画どおりに進んだ場合の書き方で、制作中の物を含む。できあがりと違ったら直す。

NASA の素材は [NASA Images and Media Usage Guidelines](https://www.nasa.gov/nasa-brand-center/images-and-media/) に従って使っている。NASA がこのゲームを推奨・承認しているわけではない。NASA の記章・ロゴ・旗・文字はモデルに付けていない。

## ゲームに入る素材

| 名前 | 作者 | URL | ライセンス | 何に使ったか |
|---|---|---|---|---|
| Blue Marble: Next Generation（2004年12月、地形と海底地形つき、5400×2700） | NASA Earth Observatory | https://science.nasa.gov/earth/earth-observatory/blue-marble-next-generation/base-topography-bathymetry/ （ファイル：https://assets.science.nasa.gov/content/dam/science/esd/eo/images/bmng/bmng-topography-bathymetry/december/world.topo.bathy.200412.3x5400x2700.jpg ） | NASA Media Usage Guidelines | 地球（`Body_Earth`）の画像。縮小・減色して使う |
| CGI Moon Kit（`lroc_color_poles_1k.jpg`） | NASA's Scientific Visualization Studio | https://svs.gsfc.nasa.gov/4720 （ファイル：https://svs.gsfc.nasa.gov/vis/a000000/a004700/a004720/lroc_color_poles_1k.jpg ） | NASA Media Usage Guidelines | 月（`Body_Moon`）の画像。減色して使う |
| Skyboxes Space（`skybox-space-galaxy.png`、4096×2048） | Kenney（www.kenney.nl） | https://kenney.nl/assets/skyboxes-space （ファイル：https://opengameart.org/sites/default/files/kenney_skyboxes-space.zip ） | CC0 1.0 | 宇宙の空（`unity/Assets/Art/Sky/Sky_Space_Navy.png`）。濃紺に寄せて彩度を落とし、2048×1024 に縮めた（`art/make_sky.py`） |
| Smoke Particles（White puff の `whitePuff01`・`10`・`12`・`18`） | Kenney（Kenney Vleugels、www.kenney.nl） | https://opengameart.org/content/smoke-particle-assets （ファイル：https://opengameart.org/sites/default/files/smokeParticleAssets.zip ） | CC0 1.0 | 酸素ガスの煙（`unity/Assets/Art/VFX/Smoke_Puff_2x2.png`、`GasLeak.prefab`）。4枚を 2×2 にまとめ、色を白にしてアルファだけ使う（`art/make_smoke.py`） |
| PhysDraft-SpaceCrew3D の Blender スクリプト（`kit.py`、`crew_v5.py`、`props.py`、`room.py`） | midori（`midori11111111`） | リポジトリ内 `modeling/PhysDraft-SpaceCrew3D/scripts/` | チームのメンバーの成果物（ライセンスの明記なし。midori に確認する） | `kit.py` を写して `modeling/core/corekit.py` を作った。クルー・警報ランプ・破れたパイプ・コンソール台は midori のスクリプトの作り方と寸法感を参考にした。メッシュは流用していない |
| Ionicons（filled の SVG。使った 38 個は `art/ui/make_icons.py` の表） | Ionic（ionic.io） | https://github.com/ionic-team/ionicons （取得元：http://ionicons.com/ ） | MIT（全文：`unity/Assets/Art/UI/Icons/LICENSE-ionicons.txt`） | UI のアイコン（電力・温度・ロケットなど）。白の PNG 128×128 にして `unity/Assets/Art/UI/Icons/` に置いた。色は Image の Color で付ける |
| Baloo 2（wght 800 に固定。`unity/Assets/Art/UI/Fonts/Baloo2-ExtraBold.ttf`） | The Baloo 2 Project Authors（https://github.com/EkType/Baloo2 ） | https://github.com/google/fonts/tree/main/ofl/baloo2 | SIL Open Font License 1.1（`unity/Assets/Art/UI/Fonts/OFL-Baloo2.txt`） | 見出し・小見出し・ボタンの文字と、大きい数字（ゲージ・カウントダウン） |
| Nunito（wght 400・700 に固定。`Nunito-Regular.ttf`・`Nunito-Bold.ttf`） | The Nunito Project Authors（https://github.com/googlefonts/nunito ） | https://github.com/google/fonts/tree/main/ofl/nunito | SIL Open Font License 1.1（`unity/Assets/Art/UI/Fonts/OFL-Nunito.txt`） | 本文・吹き出しの台詞・補助ラベル |
| Zen Maru Gothic（Black・Regular・Bold。そのまま。`ZenMaruGothic-*.ttf`） | The Zen Maru Gothic Project Authors（https://github.com/googlefonts/zen-marugothic ） | https://github.com/google/fonts/tree/main/ofl/zenmarugothic | SIL Open Font License 1.1（`unity/Assets/Art/UI/Fonts/OFL-ZenMaruGothic.txt`） | 日本語の開発用の表示だけ（提出の画面は英語） |

## 音（#141）

置き場所は `unity/Assets/Audio/`（効果音 `Sfx/`・BGM とジングル `Music/`）。1つずつの用途・切り出し・加工は各フォルダの `SOURCES.md`。

| 名前 | 作者 | URL | ライセンス | 何に使ったか |
|---|---|---|---|---|
| Spacedman | Shane Ivers（Silverman Sound） | https://www.silvermansound.com/free-music/spacedman （ファイル：https://www.silvermansound.com/wp-content/uploads/spacedman.mp3 ） | CC BY 4.0（**表記が必要**。表記文：`Music: Spacedman by Shane Ivers - https://www.silvermansound.com`） | BGM（設計・練習・飛行）。`Music/space_calm_01.mp3`。そのまま |
| Music Jingles（`STEEL02`・`STEEL04`・`STEEL07`・`STEEL10`・`PIZZI08`・`PIZZI13`） | Kenney（www.kenney.nl） | https://kenney.nl/assets/music-jingles | CC0 1.0 | ジングル（生還・ぎりぎりの生還・失敗・打ち上げ・部屋に人が入った・危機）。`Music/jingle_*.wav`。音量をそろえて WAV にした |
| Interface Sounds（`click_001`・`switch_004`・`switch_007`・`confirmation_003`・`error_005`） | Kenney（www.kenney.nl） | https://kenney.nl/assets/interface-sounds （取得元：https://github.com/Calinou/kenney-interface-sounds ） | CC0 1.0 | ボタン・スイッチ・成功・エラーの音（`Sfx/ui_*.wav`）。スイッチはモノラルにした |
| Sci-Fi Sounds（`space_engine_low_000`） | Kenney（www.kenney.nl） | https://kenney.nl/assets/sci-fi-sounds （取得元：https://github.com/Boyquotes/kenney-sci-fi-sounds-for-godot ） | CC0 1.0 | 噴射の音（`Sfx/thruster_burn_loop.wav`）。ループにして音量を下げた |
| Apollo 13 の交信「Houston, we've had a problem」 | NASA | https://commons.wikimedia.org/wiki/File:Apollo13-wehaveaproblem.ogg | パブリックドメイン（Commons の表記 "NASA public domain"。NASA 本体での確認は未了）。NASA Media Usage Guidelines | 事故の瞬間の本物の声（`Sfx/voice_problem.wav`）。7.21〜8.29 秒を切り出し、音量をそろえて低音を切った |
| 合成の効果音（警報・ガス漏れ・Quindar トーン・無線の雑音・再突入・着水・バルブ） | このチーム（iizuka。Claude が合成） | リポジトリ内 `art/audio/synth_sfx.py`・`art/audio/synth_valve.py` | 自作 | `Sfx/` の `alarm_*`・`gas_hiss_loop`・`quindar_*`・`radio_static_loop`・`reentry_rumble_loop`・`splash`・`valve_turn` |

## 寸法取りだけに使った素材（ゲームには入らない）

| 名前 | 作者 | URL | ライセンス | 何に使ったか |
|---|---|---|---|---|
| Apollo Lunar Module（glb） | NASA（NASA 3D Resources） | https://github.com/nasa/NASA-3D-Resources （`3D Models/Apollo Lunar Module/`。リポジトリ内 `modeling/PhysDraft-SpaceCrew3D/nasa_source/`、追加：midori） | NASA Media Usage Guidelines | 月着陸船の形と比率を測った。`Ship_LM` は測った値と実物の公表値から作り直した |
| Apollo Soyuz（glb） | NASA（NASA 3D Resources） | https://github.com/nasa/NASA-3D-Resources （`3D Models/Apollo Soyuz/`。置き場所は上と同じ） | NASA Media Usage Guidelines | アポロ側（司令船・機械船）の形と比率を測った。`Ship_CM`・`Ship_SM` は測った値と実物の公表値から作り直した |
| Astronaut（glb） | NASA（NASA 3D Resources） | https://github.com/nasa/NASA-3D-Resources/tree/master/3D%20Models/Astronaut （ファイル：https://raw.githubusercontent.com/nasa/NASA-3D-Resources/master/3D%20Models/Astronaut/Astronaut.glb ） | NASA Media Usage Guidelines | 宇宙服の背中の装置の比率を測った。クルーのモデルは作り直しで、このメッシュは入っていない |

## 別に入っている素材（#34 より前）

| 名前 | 作者 | URL | ライセンス | 何に使ったか |
|---|---|---|---|---|
| Apollo13_Stack／Apollo_CSM／Apollo_LM（FBX・glb） | NASA（NASA 3D Resources）のモデルを midori が加工 | 元データは上の2つ。加工の内容は `modeling/PhysDraft-SpaceCrew3D/README.md` | NASA Media Usage Guidelines | `unity/Assets/Models/NASA/` に置いてある。ゲームで使うかは未定 |

## まだ決まっていないこと

- midori のスクリプトのライセンス（チーム内の成果物として使ってよいか、表記のしかた）
- ゲーム内のクレジット画面（または提出ページ）に **Spacedman の表記文**（CC BY 4.0 で必須）を出す場所。今はゲームのどこにも出ていない
- ゲーム内のクレジット画面に出す文面。案：「Earth and Moon imagery: NASA Earth Observatory / NASA's Scientific Visualization Studio」「Spacecraft reference models: NASA 3D Resources」
- 取ってきた元ファイルの置き場所と sha256 は `modeling/source/README.md`
