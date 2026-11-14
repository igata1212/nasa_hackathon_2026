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
    - EN: Let's verify before we fly. Mass against the rocket's limit. Cost against the budget. ...You're go for launch.
    - JA: 飛ぶ前に確かめよう。重さはロケットの上限以内か。コストは予算以内か。……よし、打ち上げだ。
- 出うる反応：H01・H02（判定で落ちて設計画面に戻った場合のみ）

## 場面3　月へ向かう宇宙船
- 時刻：事故前（M03〜M04）→ t=0（M05 事故）。SPEC 4章
- 何が起きる（storyboard）：「斜めに飛ぶ宇宙船の断面。中にクルー2人。右上に顔のある月（moon）。」（SPEC 4章：「M03 自由に操作を試す → M04 かき混ぜスイッチ」「M05 事故。ループ開始」）
- 流れる台詞：
  - **M03**
    - EN: Launch was a success. Two days out from Earth, and the Moon is getting close. Take your seats and try your controls. Nothing can go wrong yet.
    - JA: 打ち上げ成功だ。地球を出て2日、月までもう少し。席について、自分の操作を試してみてくれ。今はまだ何も壊れない。
  - **M04**
    - EN: Looking good. One routine job. Life Support, switch on the oxygen tank stir, please.
    - JA: いい感じだ。定期作業をひとつ。生命維持担当、酸素タンクのかき混ぜスイッチを入れてくれるかな。
  - **M05**
    - EN: ...What was that noise?! Oxygen pressure is dropping fast. One of your main tanks is leaking, and I can't stop it from here. From now on, you decide what stays on and what goes off. I'll tell you what I see.
    - JA: ……今の音は何だ!? 酸素の圧力が急に下がってる。メインタンクのどちらかが漏れている。こちらからは止められない。ここからは、何を残して何を切るか、君たちが決めるんだ。こちらは見えていることを伝える。
- 出うる反応：練習中（事故前）なら H08・H21・H23・H27・H28。M05 の直後なら H07（8 秒後）・H09（30 秒後）

## 場面4　管制室（the earth）
- 時刻：t=20（M06）〜 t=120（M09）。SPEC 4章。場面としての時刻は材料になし
- 何が起きる（storyboard）：「部屋の中に机とコンピュータと人。「ここが先生」と注記がある。」（SPEC になし。art-direction.md は「作っていないもの」に管制室を挙げる）
- 流れる台詞（ヒューストンの声。M10 は場面6）：
  - **M06**
    - EN: ...Everyone, listen. The mission as planned is over. From here on there is only one goal: all of you get back to Earth alive. Any science you can still bring home is a bonus, and it's your call.
    - JA: ……みんな、聞いてくれ。当初の計画は中止だ。ここからの目標はひとつだけ。全員、生きて地球に帰ること。持ち帰れる科学データがあれば、それはボーナスだ。どうするかは君たちが決めていい。
  - **M07**（観測向き軌道のみ）
    - EN: There's one problem. To get better observations, you're flying a path that left the free-return trajectory. If nothing changes, you'll swing around the Moon and still not come back to Earth. You need a short engine burn before you go behind the Moon. Pilot, it's yours when you're ready.
    - JA: ひとつ問題がある。君たちは観測のために、自由帰還軌道から外れた道を飛んでいる。このままだと、月を回っても地球には戻れない。月の裏側に入る前に、エンジンを短く噴く必要がある。操縦担当、準備ができたら頼む。
  - **M08**
    - EN: You can't turn around. We'll use the Moon's gravity to swing you around and send you home. There are two ways back. A: burn the engine hard after the far side and get home fast. Lots of fuel, but less oxygen and power. B: a small burn and a slower trip. You save fuel, but you'll need oxygen and power for longer. Tell me before the burn window. You can change your mind until ignition.
    - JA: 引き返すことはできない。月の重力を使って、ぐるっと回って帰る。帰り道は2つだ。A：裏側を抜けたらエンジンを強く噴いて、早く帰る。燃料は多く使うが、酸素と電力は少なくて済む。B：噴射を小さくして、ゆっくり帰る。燃料は節約できるが、そのぶん酸素と電力が長く要る。噴射窓の前に教えてくれ。点火するまでは変えていい。
  - **M09**（食い違いあり）
    - EN: You're about to go behind the Moon. The Moon will block our signal, so you're on your own for a while. It's also your only chance to observe the far side. What you run back there is up to you. ...You'll be fine.
    - JA: もうすぐ月の裏側だ。月が電波をさえぎるから、しばらく君たちだけになる。裏側を観測できるのもこの時だけだ。何を動かすかは任せる。……君たちなら大丈夫だ。
- 出うる反応：H07〜H28 のすべて（通信途絶 t=120〜300 の間は声なし。SPEC 9章）

## 場面5　素材の画面（部品を「使う」「使わない」）
- 時刻：未確定。M01 なら事故前。修理の場面なら事故後（storyboard も「未確定」）
- 何が起きる（storyboard）：「部品が2段（上5・下6）で並ぶパネル。下に「使う」「使わない」の丸ボタン。」（SPEC 7章 M01：「設計画面。質量・コスト・電力・酸素・燃料の合計を常に表示」）
- 流れる台詞（候補）：
  - **M01**（ゲーム開始の台詞）
    - EN: Houston here. Scientists on Earth need data on the far side of the Moon, the side we can never see from here. Your mission: fly around the Moon, observe the far side, send that data home, and come back safely. You have a parts catalog, a budget and a mass limit. How you build the ship is up to you. Just remember the four requirements.
    - JA: こちらヒューストン。地球の科学者たちが、月の裏側のデータを待っている。地球からは決して見えない側だ。ミッションは、月を回って裏側を観測し、データを地球に送り、無事に帰ってくること。部品カタログと、予算と、重さの上限がある。船をどう作るかは君たち次第だ。4つの要件だけは忘れないでくれ。
- 出うる反応：設計画面なら H01〜H06。修理なら未定（下の食い違いを参照）

## 場面6　地球に向かう宇宙船
- 時刻：t=300（M10 通信回復）→ 330（M11 噴射窓）→ 420 / 510（M12 再突入。ルートA / ルートB）。SPEC 4章
- 何が起きる（storyboard）：「大きな地球（the earth）の弧に向かって宇宙船が飛ぶ。」（SPEC 4章：「通信回復、データを送れる」「噴射窓が開く（60秒）」「再突入開始（ルートA / ルートB）」）
- 流れる台詞：
  - **M10**
    - EN: ...Houston here! Do you read me? You made it through! I can take your data whenever you point the antenna at Earth.
    - JA: ……こちらヒューストン！ 聞こえるか？ よく持ちこたえた！ アンテナを地球に向けてくれれば、いつでもデータを受け取れる。
  - **M11**
    - EN: Burn window is open. Navigation on, ship in burn attitude, then the two ignition switches. Pilot and Comms & Power, press them together when you're both ready.
    - JA: 噴射窓が開いた。航法をON、船を噴射の姿勢に。それから点火スイッチ2つだ。操縦担当と通信・電力担当、準備ができたら同時に押してくれ。
  - **M12**
    - EN: There's Earth. One last hurdle: re-entry. The heat will cut our radio again. Heat shield forward, and make sure life support and navigation have power. See you on the other side.
    - JA: 地球が見えてきた。最後の山場、再突入だ。熱でまた通信が途切れる。熱シールドを前に向けて、生命維持と航法に電力が回っているか確かめてくれ。向こう側で会おう。
- 出うる反応：H10〜H19（資源の閾値）、H21〜H28（H24・H25 は t=300 のみ）

## 場面7　「成功」の画面
- 時刻：着水。t=480 / 570（SPEC 4章）。判定は SPEC 8章（着水時）
- 何が起きる（storyboard）：「成功と書かれた画面。写真の下半分は裏写りで、描かれていない。」（storyboard：ED-A または ED-B にあたりそう。ED-C の場面はない）
- 流れる台詞（判定で ED-A か ED-B のどちらか。その後 R01）：
  - **ED-A**
    - EN: ...We see parachutes! Welcome home, everyone. You did great.
    - JA: ……パラシュートを確認！ おかえり、みんな。本当によくやった。
  - **ED-B**
    - EN: Splashdown confirmed. The ship is a wreck, but you're all safe. That's what matters most.
    - JA: 着水を確認。船はボロボロだけど、君たちは無事だ。それが一番大事なことだよ。
  - **R01**（着水後の振り返り）
    - EN: Time for the mission debrief. Let's see where each of your decisions made a difference.
    - JA: ミッションの振り返りだ。君たちの判断が、どこでどう効いたのか見てみよう。
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
