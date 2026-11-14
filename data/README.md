# data

書いた人：Taiga ／ 最終更新：2026-11-14

ゲームが読む外部データ。ファイルを足したら、下の表に1行ずつ書く（提出のときに、使った NASA のデータとツールを一覧で出すため）。

| ファイル | 出典 | 取った人 |
|---|---|---|
| `gmat/return_routes.script` | 自作。NASA GMAT R2026a 用のスクリプト（https://software.nasa.gov/software/GSC-19468-1） | Taiga |
| `gmat/route_a.csv` `gmat/route_b.csv` | 上のスクリプトを GMAT R2026a で実行した出力（天体の位置は GMAT 付属の DE405） | Taiga |
| `gmat/route_summary.csv` | 同上。噴射量と飛行時間のまとめ | Taiga |
| `gmat/preview.svg` | `gmat/preview.py` が CSV から作る確認用の図 | Taiga |

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
