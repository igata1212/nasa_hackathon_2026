# data

書いた人：Taiga ／ 最終更新：2026-11-14 ／ 更新：iizuka（2026-11-15、#220 data/ の外にある NASA のデータ）

ゲームが読む外部データ。ファイルを足したら、下の表に1行ずつ書く（提出のときに、使った NASA のデータとツールを一覧で出すため）。

| ファイル | 出典 | 取った人 |
|---|---|---|
| `gmat/return_routes.script` | 自作。NASA GMAT R2026a 用のスクリプト（https://software.nasa.gov/software/GSC-19468-1） | Taiga |
| `gmat/route_a.csv` `gmat/route_b.csv` | 上のスクリプトを GMAT R2026a で実行した出力（天体の位置は GMAT 付属の DE405） | Taiga |
| `gmat/route_summary.csv` | 同上。噴射量と飛行時間のまとめ | Taiga |
| `gmat/preview.svg` | `gmat/preview.py` が CSV から作る確認用の図 | Taiga |
| `sim/timeline_t1_good.json` `sim/timeline_t4_fail.json` | NASA のデータではない。`server/tools/export_timeline.py` で生成（reference_sim の T1・T4 を1回遊んだ記録。Unity のオフライン再生用、#69） | iizuka（Claude） |

## gmat/ の中身（試作、Issue #65）

アポロ13号型の軌道。月の裏側を高度 254 km で回るフリーリターン軌道に乗り、最接近の2時間後に加速して地球へ帰る。ルート A は大きく加速して早く帰り、ルート B は小さく加速してゆっくり帰る。往路は A / B で共通。

| | 噴射量 | 噴射から再突入まで |
|---|---|---|
| ルート A | 0.271 km/s | 63.0 時間 |
| ルート B | 0.052 km/s | 70.6 時間 |

作り直すには（Windows）：`powershell -ExecutionPolicy Bypass -File data\gmat\run.ps1 -Gmat "GMAT を展開したフォルダ"`。噴射量や月の高度は、スクリプトの `BeginMissionSequence` の直後の5行で変えられる。GMAT の画面で開いて実行すると 3D でも見られる。

CSV の列（単位は km、1行目が見出し）：

| 列 | 中身 |
|---|---|
| `UTCModJulian` | 時刻（日）。差を取って経過時間にする |
| `EarthMJ2000Eq.X/Y/Z` | 地球中心・慣性系での宇宙船の位置 |
| `MoonMJ2000Eq.X/Y/Z` | 月中心・慣性系での宇宙船の位置。月の位置は「地球中心の位置 − これ」 |
| `EarthMoonRot.X/Y/Z` | 地球中心で、地球と月を結ぶ線と一緒に回る座標。月がほぼ +X 上に止まるので、8の字に描ける |
| `Earth.Altitude` `Luna.Altitude` | 地球・月の表面からの高度 |

行の間隔は一定ではない（最大10分）。始まりは打ち上げ側の高度 185 km、終わりは再突入の高度 121.92 km。

## ゲーム時間との対応（`gmat/time_map.json`、Issue #65）

`gmat/make_time_map.py` が CSV と `docs/design/parameters.json`（ゲーム側の時刻）から作る（#161 で 90 / 210 / 260 / 300 / 360 に更新。作り直した人：Claude／iizuka の依頼）。`h` は CSV の最初の行からの経過時間（時間）で、区切りの間は線形につなぐ。Unity（`GmatRoute`）とサーバーのどちらも読める。

| ゲーム t（秒） | できごと | GMAT の h（時間） | 決め方 |
|---|---|---|---|
| 0 | 事故 | 52.77 | LOS の 21.23 時間前（アポロ13号の本物：事故から LOS まで 21 時間余り） |
| 90 | LOS（月の裏側に入る） | 73.99 | 月の円柱の影に入った最初の行（EarthMoonRot で x > 月の中心 かつ \|y\| < 月の半径） |
| 210 | AOS（出る） | 74.37 | 同じ影を出た最後の行。裏側にいるのは約22分（ゲームは120秒＝約 11 倍速） |
| 260 | 噴射（窓 240〜280 の中央） | 76.18 | 最接近（74.16）の2時間後。A と B の軌道が分かれる最初の行 |
| 300（A） | 再突入 | 139.16 | A の最後の行 |
| 360（B） | 再突入 | 146.72 | B の最後の行 |

事故までの往路は A / B で同じ。倍速は区間ごとに違う（事故〜LOS は約 850 倍、噴射〜再突入は A 約 5700 倍・B 約 2500 倍）。
出典（事故・LOS・AOS・噴射・再突入の本物の時刻）：NASA Apollo 13 Mission Report（MSC-02680）、Apollo Flight Journal。事故 GET 55:54:53、LOS 約 77:08、AOS 約 77:34、PC+2 噴射 79:27:39、再突入 142:40:46。

## data/ の外にある NASA のデータ（提出の一覧用、#220）

ゲームに入っている、または数値の裏付けに使った NASA のデータで、`data/` に置いていない物。作者・ライセンスの細かい所は `CREDITS.md`。

| 物 | 出典 | 置き場所・使い方 |
|---|---|---|
| アポロ13号の交信「Houston, we've had a problem」 | NASA（Wikimedia Commons 経由：https://commons.wikimedia.org/wiki/File:Apollo13-wehaveaproblem.ogg ） | `unity/Assets/Audio/Sfx/voice_problem.wav`。事故の瞬間に流す |
| 地球の画像 Blue Marble: Next Generation | NASA Earth Observatory（https://science.nasa.gov/earth/earth-observatory/blue-marble-next-generation/ ） | 地球のモデルの画像 |
| 月の画像 CGI Moon Kit | NASA's Scientific Visualization Studio（https://svs.gsfc.nasa.gov/4720 ） | 月のモデルの画像 |
| NASA 3D Resources（Apollo Lunar Module・Apollo Soyuz・Astronaut） | https://github.com/nasa/NASA-3D-Resources | 寸法を測るためだけ。ゲームには入れていない（`modeling/PhysDraft-SpaceCrew3D/nasa_source/`） |
| アポロ13号の文書（Mission Report・Review Board・TN D-6725・Press Kit など） | NTRS 19710003598・19700076776・19720013191 ほか | 時刻・数値の裏付け。一覧と数値は `docs/reference/apollo13-data.md`、URL は `CREDITS.md` |

