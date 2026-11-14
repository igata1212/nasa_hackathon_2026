書いた人：iizuka（下書き：Fable 5.2）／ 最終更新：2026-11-14

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
- ゲーム内のクレジット画面に出す文面。案：「Earth and Moon imagery: NASA Earth Observatory / NASA's Scientific Visualization Studio」「Spacecraft reference models: NASA 3D Resources」
- 取ってきた元ファイルの置き場所と sha256 は `modeling/source/README.md`
