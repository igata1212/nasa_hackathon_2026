書いた人：iizuka（下書き：Fable 5.2）／ 最終更新：2026-11-14

# modeling/source — 外から取ってきた元データ

3D モデル制作（#34）の下敷きと画像。計画は `docs/design/modeling-plan.md` の B 章。
ここのファイルは**取ってきたまま**置く（加工しない）。加工した物は各モデルのフォルダに出す。

- 取得日：2026-11-14
- 取得：Fable-B（依頼：iizuka）。curl で元 URL から直接取った
- ライセンス：3つとも NASA の素材。[NASA Images and Media Usage Guidelines](https://www.nasa.gov/nasa-brand-center/images-and-media/) に従う（NASA が推奨・承認しているような見せ方をしない。NASA の記章・ロゴは使わない）

| ファイル | 元 URL | 掲載ページ | サイズ | 用途 |
|---|---|---|---|---|
| `nasa/Astronaut.glb` | https://raw.githubusercontent.com/nasa/NASA-3D-Resources/master/3D%20Models/Astronaut/Astronaut.glb | https://github.com/nasa/NASA-3D-Resources/tree/master/3D%20Models/Astronaut （[NASA 3D Resources](https://science.nasa.gov/3d-resources/)） | 762,988 バイト | **下敷き専用**。宇宙服の比率を測るだけ。ゲームにも書き出しにも入れない |
| `textures/earth_bmng_5400x2700.jpg` | https://assets.science.nasa.gov/content/dam/science/esd/eo/images/bmng/bmng-topography-bathymetry/december/world.topo.bathy.200412.3x5400x2700.jpg | https://science.nasa.gov/earth/earth-observatory/blue-marble-next-generation/base-topography-bathymetry/ | 2,566,770 バイト（5400×2700） | **ゲームに入る**（縮小・減色した物が `Body_Earth` の画像になる。この元ファイル自体は入れない） |
| `textures/moon_lroc_color_poles_1k.jpg` | https://svs.gsfc.nasa.gov/vis/a000000/a004700/a004720/lroc_color_poles_1k.jpg | https://svs.gsfc.nasa.gov/4720 （CGI Moon Kit） | 139,068 バイト（1024×512） | **ゲームに入る**（減色した物が `Body_Moon` の画像になる。この元ファイル自体は入れない） |

## sha256

```
d18c141410542b935113d1c25a9271d90c40d44015a64295b7b68a30f0ce2820  nasa/Astronaut.glb
a9f0088972dee0254610af851c4d6838ca3f2cf79176987e0a5713e2c15ec042  textures/earth_bmng_5400x2700.jpg
b246064f217f8d479df78c49c7c8595a8f5fbda008a72fd539978d2e121e0109  textures/moon_lroc_color_poles_1k.jpg
```

## 作者の表記

| ファイル | 表記 | 確かめ方 |
|---|---|---|
| Astronaut.glb | NASA（NASA 3D Resources） | リポジトリ名から。モデル個別の作者名は確かめていない |
| earth_bmng_5400x2700.jpg | NASA Earth Observatory（Blue Marble: Next Generation、2004年12月、地形と海底地形つき） | 掲載ページの題名から。クレジットの決まった書き方は掲載ページで見つけられなかった |
| moon_lroc_color_poles_1k.jpg | NASA's Scientific Visualization Studio | 掲載ページに「Please give credit for this item to: NASA's Scientific Visualization Studio」とある |

## 気づいたこと

- `Astronaut.glb` の単位は m ではない（高さ 74.26）。インチだとすると 1.886m になるが、ファイルに単位の記述はなく推測。比率だけ使う。測った値は `modeling/ship/dimensions.md`
- `Astronaut.glb` はアポロの宇宙服ではなく、背中の装置が大きい型（スペースシャトル期の船外活動服に見える。見た目からの推測）
- 月着陸船と司令船・機械船の下敷きは、ここではなく `modeling/PhysDraft-SpaceCrew3D/nasa_source/`（midori が追加）にある。そちらは読むだけ
