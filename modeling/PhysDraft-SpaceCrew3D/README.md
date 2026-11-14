# PhysDraft SpaceCrew 3D

作った人：midori（`midori11111111`）／ リポジトリに追加：iizuka（2026-11-14）

3人協力の宇宙航海ゲーム用の3Dアセット。すべてBlender 5.3 + Pythonスクリプトで生成し、`export/` にGLB（Web/three.js向け）を出力する。

## 中身

| 種類 | GLB | 備考 |
|---|---|---|
| クルー6体 | `export/{Astronaut,Civilian}_{Nav,Pilot,Sys}.glb` | 3頭身・Aポーズ。航法=青、操縦=赤、機体=黄。ボーン7本、クリップ `Idle` `Walk` `Carry` `Work` |
| 運ぶ物 | `export/items/Item_*.glb` | Battery / Comms / Toolbox / Extinguisher / Oxygen |
| 事件 | `export/incidents/Incident_*.glb` | BrokenPipe / HullBreach / Alarm / Debris / Fire |
| 船の部屋 | `export/room/*.glb` | Room_Shell（床・壁・窓・宇宙）＋ Station_Pilot / Nav / Engine / Comms / Charger / Airlock |
| 船外 | `export/exterior/*.glb` | Ship_Lander / Moon_Terrain / Moon_Base / Moon_LandingPad / Asteroid_Large / Asteroid_Small |

単位はメートル相当（クルーの身長1.5）。Blenderの +Y が three.js の -Z。

## 確認

`viewer.html` をローカルサーバー経由で開く（例: `python3 -m http.server 8765` → http://localhost:8765/viewer.html）。

## 作り直し

Blenderを起動し、MCP for Blender アドオンのサーバー（port 9876）が動いている状態で:

```
python3 scripts/bl.py scripts/crew_v5.py        # クルー6体の形と配色 → blend/crew_v5.blend
python3 scripts/bl.py scripts/rig_walk.py       # ボーン入れ → blend/crew_v6_rigged.blend
python3 scripts/bl.py scripts/reload_rig.py && python3 scripts/bl.py scripts/anims.py      # 4クリップ → crew_v7_anim.blend
python3 scripts/bl.py scripts/reload_anim.py && python3 scripts/bl.py scripts/export_crew.py
python3 scripts/bl.py scripts/props.py          # 運ぶ物・事件
python3 scripts/bl.py scripts/room.py           # 船の部屋（props.blend と crew_v7_anim.blend を取り込む）
python3 scripts/bl.py scripts/exterior.py       # 船外
python3 scripts/bl.py scripts/reload_exterior.py && python3 scripts/bl.py scripts/export_terrain.py
```

ファイルを開き直す処理は別呼び出しに分けている（同じ呼び出し内で開き直すとglTF書き出しが失敗するため）。共通の部品・色・書き出し処理は `scripts/kit.py`。
