書いた人：iizuka（下書き：材料を並べただけ。物語は Taiga が直す）／ 最終更新：2026-11-14

# ゲームの台本（下書き）

材料：`tmp/wt/server/docs/design/houston_lines.json`（台詞の原文）、`docs/design/storyboard.md`（7場面）、`docs/design/SPEC.md`（2・4・7・8・9章）。場面も台詞も新しく作っていない。
「出うる反応」の H 番号は推測（SPEC 9章の条件と 4章の時刻から割り当てた。確認が要る）。

## 場面1　出発（地上のロケットとクルー）
- 時刻：事故前。SPEC 4章に時刻はない（storyboard は「M02 の打ち上げ判定の前後にあたりそう」）
- 何が起きる（storyboard）：「弧を描く地面の上に、クルー3人とロケットが立っている。見送りの場面。」
- 流れる台詞：なし（SPEC になし）
- 出うる反応：なし（操作のない見送り）

## 場面2　ロケット内部の断面
- 時刻：事故前（SPEC 4章。M02 打ち上げ判定の後）
- 何が起きる（storyboard）：「ロケットを横から切った図。中にクルー3人、下に発射台の脚。」（SPEC 7章 M02 の進む条件：「「Ready for launch」を押す」）
- 流れる台詞：
  - **M02**
    - EN: Checking weight and budget... You're go for launch!
    - JA: 重さと予算を確かめる……。打ち上げてよし！
- 出うる反応：H01・H02（判定で落ちて設計画面に戻った場合のみ）

## 場面3　月へ向かう宇宙船
- 時刻：事故前（M03〜M04）→ t=0（M05 事故）。SPEC 4章
- 何が起きる（storyboard）：「斜めに飛ぶ宇宙船の断面。中にクルー2人。右上に顔のある月（moon）。」（SPEC 4章：「M03 自由に操作を試す → M04 かき混ぜスイッチ」「M05 事故。ループ開始」）
- 流れる台詞：
  - **M03**
    - EN: Launch was a success! Try your controls now, while it's still safe.
    - JA: 打ち上げ成功！ 今のうちに操作を試して。まだ安全だから。
  - **M04**
    - EN: Looking good. Life Support, please press the oxygen tank stir button.
    - JA: 順調だ。Life Support、酸素タンクのかき混ぜボタンを押してくれ。
  - **M05**
    - EN: We see it too! Oxygen is dropping fast. One of your main tanks is leaking!
    - JA: こちらでも見えている！ 酸素が急に減っている。メインタンクの1つが漏れている！
- 出うる反応：練習中（事故前）なら H08・H21・H23・H27・H28。M05 の直後なら H07（8 秒後）・H09（30 秒後）

## 場面4　管制室（the earth）
- 時刻：t=20（M06）〜 t=90（M09）。SPEC 4章。場面としての時刻は材料になし
- 何が起きる（storyboard）：「部屋の中に机とコンピュータと人。「ここが先生」と注記がある。」（SPEC になし。art-direction.md は「作っていないもの」に管制室を挙げる）
- 流れる台詞（ヒューストンの声。M10 は場面6）：
  - **M06**
    - EN: New goal: everyone gets home alive. Science is a bonus now. It's your call.
    - JA: 目標を変える。全員が生きて帰ること。科学はおまけだ。決めるのは君たちだ。
  - **M07**（観測向き軌道のみ）
    - EN: Your path won't bring you home. Pilot, press Correction Burn before the far side.
    - JA: 今の道では帰れない。Pilot、月の裏側に入る前に Correction Burn を押してくれ。
  - **M08**
    - EN: Two ways home. A: fast, but uses lots of fuel. B: slow, and uses more oxygen and power. Mission Planner, choose.
    - JA: 帰り道は2つ。A：早いが、燃料を多く使う。B：遅く、酸素と電力を多く使う。Mission Planner、選んでくれ。
  - **M09**（食い違いあり）
    - EN: The Moon will block our radio soon. It's your only chance to study the far side. Good luck.
    - JA: もうすぐ月が無線をさえぎる。月の裏側を調べられるのは今だけだ。幸運を。
- 出うる反応：H07〜H28 のすべて（通信途絶 t=90〜210 の間は声なし。SPEC 9章）

## 場面5　素材の画面（部品を「使う」「使わない」）
- 時刻：未確定。M01 なら事故前。修理の場面なら事故後（storyboard も「未確定」）
- 何が起きる（storyboard）：「部品が2段（上5・下6）で並ぶパネル。下に「使う」「使わない」の丸ボタン。」（SPEC 7章 M01：「設計画面。質量・コスト・電力・酸素・燃料の合計を常に表示」）
- 流れる台詞（候補）：
  - **M01**（ゲーム開始の台詞）
    - EN: Let's build your ship. Pick parts, a path and a rocket. Watch weight and budget.
    - JA: 船を作ろう。部品と、月への道すじと、ロケットを選ぶ。重さと予算に気をつけて。
- 出うる反応：設計画面なら H01〜H06。修理なら未定（下の食い違いを参照）

## 場面6　地球に向かう宇宙船
- 時刻：t=210（M10 通信回復）→ 240（M11 噴射窓）→ 300 / 360（M12 再突入。ルートA / ルートB）。SPEC 4章
- 何が起きる（storyboard）：「大きな地球（the earth）の弧に向かって宇宙船が飛ぶ。」（SPEC 4章：「通信回復、データを送れる」「噴射窓が開く（40秒）」「再突入開始（ルートA / ルートB）」）
- 流れる台詞：
  - **M10**
    - EN: Houston here! You made it! Point at Earth and send your data.
    - JA: こちらヒューストン！ よくやった！ 地球に向けて、データを送ってくれ。
  - **M11**
    - EN: Time to fire the engine! Navigation on, turn to Burn. Pilot and Comms & Power, press Ignite together.
    - JA: エンジンを噴かす時だ！ Navigation を入れ、向きを Burn に。Pilot と Comms & Power、Ignite を同時に押して。
  - **M12**
    - EN: Earth ahead! Turn the heat shield forward and keep navigation on. See you soon.
    - JA: 地球だ！ 耐熱シールドを前に向け、Navigation は入れたままに。またすぐ会おう。
- 出うる反応：H10〜H19（資源の閾値）、H21〜H28（H24・H25 は t=210 のみ）

## 場面7　「成功」の画面
- 時刻：着水。t=360 / 420（SPEC 4章）。判定は SPEC 8章（着水時）
- 何が起きる（storyboard）：「成功と書かれた画面。写真の下半分は裏写りで、描かれていない。」（storyboard：ED-A または ED-B にあたりそう。ED-C の場面はない）
- 流れる台詞（判定で ED-A か ED-B のどちらか。その後 R01）：
  - **ED-A**
    - EN: ...We see parachutes! Welcome home, everyone. You did great.
    - JA: ……パラシュートを確認！ おかえり、みんな。本当によくやった。
  - **ED-B**
    - EN: Splashdown! The ship is a wreck, but you're all safe. That's what matters.
    - JA: 着水した！ 船はぼろぼろだが、全員無事だ。それがいちばん大事だ。
  - **R01**（着水後の振り返り）
    - EN: Mission debrief. Let's see which decisions made a difference.
    - JA: ミッションの振り返りだ。どの判断が効いたのか見てみよう。
- 出うる反応：なし（判定の後）

## 食い違い（確認が要る）
- M09：SPEC 7章は「台詞なし」、SPEC 4章は「T_LOS の10秒前に流してください（提案）」。JSON には台詞がある。
- 台詞の数：SPEC 0章は「マイルストーンの台詞 17本」、JSON は 16本。
- 時刻の基準：JSON の M03 は「After launch (0:00)」（打ち上げが 0:00）、SPEC 4章の t=0 は事故（M05）。
- 場面5 を M01 にすると、storyboard の順（出発→月へ→管制室→素材→地球へ）と SPEC 2章（M01 から始まる）が合わない。
- 場面3：storyboard は「M03〜M05（巡航・事故前）」と書くが、M05 は事故そのもの（SPEC 4章）。
- 月の顔：art-direction.md は「作っていないもの」に月の顔を挙げるが、storyboard は顔を付けるかを「決まっていない」と書く。
- 場面4：storyboard は M06・M09・M10 を挙げるが、SPEC 4章は M06 の直後に M07・M08 が続くと書く。
- 管制室：storyboard の表は「作らない（2D の通信ウィンドウで代用）」を art-direction.md から引くが、art-direction.md にはその文言がない。
- 場面7：「成功」の画面に ED-B を当てるとされるが、ED-B の台詞は「船はボロボロ」。
- クルーの人数：場面1・2 は3人、場面3 は2人（storyboard の表に既出。SPEC は 1〜4人）。
- houston_lines.json・parameters.json・parts.json は docs/design/ になく、tmp/ にしかない（tmp はコミットされない）。

## 足りないもの（材料になし。埋めていない）
- 出発（場面1）の台詞
- 管制室（場面4）だけの台詞。M06〜M09 の流用でよいか
- キャラクター名：クルー3人、ヒューストン先生（席は Pilot / Mission Planner / Life Support / Comms & Power のみ）
- 高校生3人の設定の中身（planning-meeting は「高校生3名設定」とだけ）
- 事故（M05）の絵。SPEC 7章には「警報、画面の揺れ、赤ランプ」とだけある
- 軌道修正（M07）と帰還ルートの選択（M08）の場面。台詞は場面4 にある
- 通信が切れる場面（M09）、噴射（M11）、再突入（M12）の絵。storyboard は接近まで
- 失敗の場面（ED-C）。台詞 ED-C は JSON にある
- エンディング後の締めの一言（R01 は振り返りの導入）
