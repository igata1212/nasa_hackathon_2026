## 2026-11-14
- 14:10 #35 #9 の決定に合わせて planning-meeting.md を SPEC.md と揃えた（設計・スコア・イベント・難易度・人数・移動・3Dモデル）
- 16:04 #46 予備の酸素タンク Tank_OxygenSpare1・2 を作った（orange の帯2本、Slot_Valve 付き。Fable 5.2）
- 16:05 #47 ロケット Ship_RocketSmall（70m）・Ship_RocketLarge（110m）を作った（Fable 5.2）
- 16:05 #50 着水のパラシュート Fx_Parachute を作った（主傘3つ、orange と white の縞。Fable 5.2）
- 16:05 #49 CO2 フィルターの缶（新品・使用済み）とホルダーを作った。名前の頭は Fx_（Fable 5.2）
- 16:05 #48 設計画面の部品 Part_SolarPanel・Part_FuelTank・Part_RadarSounder を作った。core に Part_ を足した（Fable 5.2）
- 17:10 #52 出発の場面の発射台 Scene_LaunchPad（腕3本が回る）と地面 Scene_Ground を作った。core に Scene_ を足した（Fable 5.2）
- 17:50 #53 管制室 Scene_MissionControl、ヒューストン先生 Crew_Houston、管制官 Crew_Controller_A・B・C を作った。顔にシェイプキー7個（口・目・眉）。export.py をシェイプキー対応にした（Fable 5.2）
- 16:50 #51 圧力計 Ctrl_Gauge を作った（針 Move_Needle が Z まわりに ±120°、帯は赤・orange・teal。Fable 5.2）
- 16:22 #65 GMAT R2026a で帰還ルート A / B の軌道を試作し、data/gmat/ に置いた。Unity での描画は iizuka に渡した（Fable 5.2）
- 18:40 #120 先生と管制官の FBX に、話すアニメーション Talk（3秒、口・まばたき・頭・腕）を入れた。export.py にアニメーションを焼く口を足した（Fable 5.2）
- 18:54 #134 食い違いの Issue 13件（#97〜#109）を決めて、SPEC・H08 の台詞（xlsx と JSON）・ui-guidelines に反映した（Fable 5.2）
- 19:05 #110 UI の担当を決めた（Taiga は絵とレイアウトまで、Unity への組み込みは iizuka）。SPEC・architecture.md・CLAUDE.md に書いた（Fable 5.2）
- 19:09 #136 緊迫感のために数値を詰めた。事故から着水まで 8分 → 6分、漏れ 2%/s → 1%/s、ヒーターを切ったままだと ED-B。xlsx・parameters.json・SPEC 4章と10章・PLAY_GOOD・テストの期待値を合わせた（Fable 5.2）
- 21:57 #142 未決定の Issue 5件を決めた（#91 #92 #93 は閉じた。#94 は自動で同じ席に戻るだけ、#54 は顔を付けず本物の地形を使う）。storyboard・art-direction・architecture に書いた（Fable 5.2）
- 21:58 #54 月の表面 Scene_MoonSurface を、NASA の Apollo 16 着陸地点の地形（STL）から作った。5,200 三角、灰色4色、check.py 合格。月に顔は付けない。どの場面で使うかは未決定（Fable 5.2）
- 23:29 #170 SWAP FILTER ボタンに残り回数を出し、0 のときは灰色の NO FILTERS LEFT にした（Unity での表示は未確認）（Fable 5.2）

## 2026-11-15
- 12:08 #154 #149 ヒューストンの台詞を21本短くした（4,612字 → 2,939字。全部120字以内）。部品の説明文を #136 の数値に合わせ、#149 の9項目に返事した。声の mp3 は21本作り直しが要る（Fable 5.2）
- 12:10 #157 Mission Planner の役を決めた（地図・カウントダウン・ルートの比較は Planner の画面にだけ出す）。SPEC 6章に書いた。Unity への組み込みは iizuka（Fable 5.2）
- 12:39 #167 席ごとの計器モニター Fx_SeatMonitor_*（4席分、画面 0.80×0.50m、状態ランプ3つ、Slot_Screen）を作った。check.py 合格。船室への配置と数値の表示は未（Fable 5.2）
- 14:05 #167 計器盤を針のメーター5つに・一人称のカメラ（V で見下ろし）・ヒューストンの顔を盤に・揺れを強く・一人称の間は船室を閉じる・GMAT のメーター3つ（左のポップアップ）・下の盆をしまうつまみ。窓の外は未着手（Opus 5.5）
- 14:48 #206 デモ動画の参考映像 2 本（DEADLINE DELIVERY トレーラー、Splatoon 紹介映像）を docs/reference/demo-video-ref/ に入れ、earshot で音を解析した。構成・カット・音の調査メモを書いた。Splatoon は 71MB → 44MB に圧縮。公開前に mp4 は消す（Fable 5.2）
