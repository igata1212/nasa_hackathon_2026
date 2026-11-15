書いた人：iizuka（下書き：Opus 5.5）／ 最終更新：2026-11-15
更新：iizuka（Fable 5.2 の採点で ① を9本に書き直した、2026-11-15、#189）

# ヒューストンが話す前提知識（下書き）

Issue #152。宇宙を知らない高校生には、「ヒューストン」が誰か、アポロ13号が何か、何をするゲームかが分からない。そこで、ヒューストン（先生）に背景を話してもらう。

- 台詞は Taiga の担当なので、これは**下書き**（Taiga が直す）
- ①は `unity/Assets/Data/Story/briefing.json` に入れた（#75 がこの形で読む）
- ②〜⑤は**案**。json には入れていない（`houston_lines.json` は Taiga の xlsx から作るため）。使うなら xlsx に入れる

## この下書きで守ったこと

- 対象は宇宙を知らない高校生。英語は CEFR A2〜B1
- 1本は1〜2文、**80字以内**（#154 の「台詞を短く」と同じ方針）。ui-guidelines 3章の吹き出し（最大3行・1行40字）にも収まる
- 専門用語は出したらすぐ言い換える（下の「言葉の言い換え」）
- 数字（%・秒）は台詞に書かない（`parameters.json` で変わるため）
- 本物の話とゲームの決まりを混ぜない。本物の話には「Apollo 13」「real」を付ける
- 今の `houston_lines.json` と同じ中身は重ねない。重なるものは「置き換え」として書いた

## ① 部屋 → 設計の前のブリーフィング（9本・約55秒・SKIP 可）

- **いつ**：ロビーで START を押して、段階が lobby → design に変わった最初の1回だけ（#189 で書き直し）
- 話し手はすべて `houston`。1本80字以内。声は Achird（`unity/Assets/Resources/Voice/Houston/`）
- 狙い：ヒューストンが誰か → アポロ13号で何が起きたか（中身は言わず驚きを残す）→ ミッション → 席 → 月の裏で無線が切れる → まず船を作る
- M01（設計画面）はミッションの説明を繰り返さず、`You have a parts catalog...` から始めてよい（Taiga に確認）

| id | 英語 |
|---|---|
| B01 | Hi, crew! I'm Houston, NASA's Mission Control. We guide every flight from Earth. |
| B02 | We can't fly your ship. We watch it from Earth and talk by radio. |
| B03 | The game is based on Apollo 13. In 1970, a part broke on the way to the Moon. |
| B04 | Houston and the crew solved it one problem at a time. All three came home alive. |
| B05 | Your mission: fly around the Moon, study its far side, and come home. |
| B06 | Four seats, four jobs. Pilot turns the ship. Life Support keeps the air safe. |
| B07 | Comms & Power runs radio and batteries. Mission Planner does route and science. |
| B08 | The Moon blocks radio. Behind it, you won't hear me. You'll be on your own. |
| B09 | First, build your ship. Then fly it. Talk to each other, and you can get home. |

## ② 設計画面の最初（何を選ぶか）— M01 の置き換え案

- **案**：今の M01（336字・5文）を M01a〜M01d の4本に分ける。ミッションの話は①で済んでいるので、M01 は「何を選ぶか」と「4つの目標（Req 1〜4）」だけにする。4本で 289字（今の M01 より短い）
- **いつ**：今の M01 と同じ（設計画面が開いたとき。①のあと）。M01e は任意（例：全員が1回ずつ部品を触ったあと）
- H05 は書き換え、H05b は新しく足す（軌道を選んだとき）

| id | いつ | 英語 | 日本語訳 | ねらい |
|---|---|---|---|---|
| M01a | 設計画面が開いたとき（M01 を置き換え） | Let's build your ship. Pick the parts, the path to the Moon, and the rocket. | 船を作ろう。部品と、月への道すじと、ロケットを選ぶ。 | 画面の3つの選ぶもの（部品・軌道・ロケット）をそのまま言う |
| M01b | M01a の次 | Goals 1 and 2: everyone gets home, and you bring back enough science. | 目標1と2：全員が帰ってくること、そして十分な科学データを持ち帰ること。 | Req 1・2 を番号つきで。UI の要件の並び（ReqRow）と同じ順 |
| M01c | M01b の次 | Goals 3 and 4: the rocket can lift your ship, and you stay on budget. | 目標3と4：ロケットが船を持ち上げられること、そして予算内に収めること。 | Req 3・4。「質量の上限」を「ロケットが持ち上げられる」と言い換える |
| M01d | M01c の次 | Instruments, like the camera, get science. But every part adds weight and cost. | カメラなどの観測機器が科学データを集める。でも部品はどれも重さとお金が増える。 | 目標2と3・4が引っぱり合うこと（設計の悩みどころ） |
| M01e | 任意 | When the whole team agrees, press Ready for Launch. | チームのみんなが納得したら、Ready for Launch を押して。 | 次に押すボタン。全員で決める |
| H05（書き換え） | 観測向き軌道を選んだとき（今の H05 と同じ） | More data on the science path. But you'll need an extra engine burn to get home. | 観測向きの道はデータが多く取れる。でも帰るには、エンジンをもう1回噴かす必要がある。 | 今の H05「won't bring you home by itself」を、何が要るか（噴射が1回増える）が分かる形に |
| H05b（新規） | 自由帰還軌道を選んだとき | Free-return is the safe path. The Moon's gravity swings you back toward Earth. | 自由帰還は安全な道。月の重力が、船を地球の方へ振り戻してくれる。 | free-return の意味。「噴射が要らない」とは言わない（下の食い違いを参照） |

## ③ 練習（M03）— M03 の置き換えと、席ごとの1本

- **M03 の置き換え案**：今の M03（142字・4文）を短くする。「Two days out from Earth」は落とした（下の「迷った所」4）
- **P01〜P04（新規）**：練習の間に、**まだ操作していない席にだけ**1本ずつ流す。例：M03 から15秒たっても、`practice` イベントの `done` にその席がないとき。全員がすぐ操作すれば1本も流れない
- **P01b〜P03b は任意**（余裕があれば）。用語（burn・CO2・navigation）を先に教えておく
- 練習中の操作は状態を変えない（`room.py` の practice）ので、「試して」と言っても安全

| id | いつ | 英語 | 日本語訳 | ねらい |
|---|---|---|---|---|
| M03（置き換え） | 打ち上げのあと、練習に入ったとき | Launch was a success! Try your controls now, while it's still safe. | 打ち上げ成功！ 今のうちに操作を試して。まだ安全だから。 | 「今は壊れない、試していい」だけを短く |
| P01 | Pilot が未操作のとき | Pilot, turn the ship. Face Earth to send data, or the Moon to study it. | Pilot、船の向きを変えてみて。地球に向けるとデータを送れる。月に向けると調べられる。 | 向きで何が変わるか（送信は地球向き、観測は月向き。SPEC 5章） |
| P01b | 任意。P01 の次 | Later, you'll fire the engine. We call that a burn. | あとでエンジンを噴かす。それを「バーン（burn）」と呼ぶ。 | M07・M11・BURN WINDOW の「burn」を先に言い換えておく |
| P02 | Life Support が未操作のとき | Life Support, you look after the air: the two oxygen tanks and the CO2 filter. | Life Support、君は空気の係だ。酸素タンク2つと、CO2 フィルター。 | タンクが2つあること（M05 でどちらが漏れているか考える下準備） |
| P02b | 任意。P02 の次 | CO2 is the gas you breathe out. Too much is bad, so the filter cleans it out. | CO2 は、はく息に入っている気体。多すぎると体に悪いので、フィルターで取りのぞく。 | CO2 の言い換え。H17・H18 につながる |
| P03 | Comms & Power が未操作のとき | Comms & Power, every switch uses power. You choose what stays on. | Comms & Power、スイッチはどれも電気を使う。何をつけておくかは君が決める。 | 電気のやりくりがこの席の仕事（M05・H09 の下準備） |
| P03b | 任意。P03 の次 | Navigation is the ship's computer. It must be on for every engine burn. | 航法（ナビ）は船のコンピュータ。エンジンを噴かすときは必ずつけておく。 | NAV の言い換え。M11・H24・再突入の確認につながる |
| P04 | Mission Planner が未操作のとき | Mission Planner, you run the instruments. They get data only behind the Moon. | Mission Planner、観測機器は君の担当。データが取れるのは月の裏側だけだ。 | 観測の窓（SPEC 5章の 6)）。いつ機器をつけるか考える下準備 |

## ④ 事故（M05）の直後に「何が起きたか」— M05 の置き換え案

- **案**：今の M05（207字・5文）を M05a・M05b の2本に分ける。M05a が「何が起きたか」の一言
- **いつ**：今の M05 と同じ（かき混ぜスイッチの直後、t=0）。2本で約10秒なので、H07（8秒後に漏れの警告）とは少し重なる。H07 は critical なので、M05b の途中で割り込んでよい
- 今の M05 の「I'll tell you what I see.」は落とした（2文に収めるため）。残すなら M05b を3文にする
- **A01・A02（任意）**：本物との比べ。漏れが止まったとき（`leak_open` が false になった直後）か、事後レポート（#89）で出す。事故の最中は H07・M06 とぶつかるので流さない

| id | いつ | 英語 | 日本語訳 | ねらい |
|---|---|---|---|---|
| M05a | 事故の直後（M05 を置き換え） | An oxygen tank broke! You're losing oxygen, and some power is gone. | 酸素タンクが壊れた！ 酸素が減っていて、電気も一部なくなった。 | 何が起きたかを一言で。酸素と電気の2つが減ったことを結ぶ |
| M05b | M05a の次 | I can't fix it from here. You decide what stays on and what goes off. | こちらからは直せない。何を残して何を切るか、君たちが決めるんだ。 | 今の M05 の「君たちが決める」を残す |
| A01 | 任意。漏れが止まったとき、または #89 | The real Apollo 13 crew said it too: "Houston, we've had a problem." | 本物のアポロ13号のクルーも言った。「ヒューストン、問題が起きた」。 | 有名な一言で本物とつなぐ。映画の「we have a problem」ではなく本物の言葉 |
| A02 | 任意。A01 の次、または #89 | On Apollo 13, oxygen also made electricity. So power dropped too. | アポロ13号では、酸素で電気も作っていた。だから電気も減った。 | なぜ酸素の事故で電気まで減るのか（本物の燃料電池の話） |

## ⑤ 月の裏に入る前（通信が切れる理由）— M09 の置き換え案

- **案**：今の M09（211字・5文）は、T_LOS の10秒前に流すと読み切れない。F01・F02 の2本に分けて、少し早く始める
- **いつ**：F01 = T_LOS の20秒前、F02 = T_LOS の10秒前（今の M09 の時刻、`config.py` の `M09_LEAD_S`）。H20（30秒前、機器が全部 OFF のとき）とは10秒ずつずれる
- 今の M09 の「What you run back there is up to you.」は落とした（H20 が同じことを言う）
- 「本物は約25分話せなかった」は、振り返り（#89）に回す案

| id | いつ | 英語 | 日本語訳 | ねらい |
|---|---|---|---|---|
| F01 | T_LOS の20秒前（M09 を置き換え） | Soon you'll go behind the Moon, and the Moon will block our radio. | もうすぐ月の裏側に入る。月が私たちの電波をさえぎる。 | 通信が切れる理由（①の B07 をその場でもう一度） |
| F02 | T_LOS の10秒前 | It's your only chance to study the far side. You'll be fine. | 裏側を調べられるのは今だけだ。君たちなら大丈夫。 | 観測の窓が開くこと＋励まし |

## 言葉の言い換え（UI の言葉 → 台詞での言い方）

| UI・SPEC の言葉 | 台詞での言い方 | 出てくる台詞 |
|---|---|---|
| Houston / Mission Control | NASA's Mission Control in Houston, Texas ／ teacher | B01・B02 |
| FAR SIDE | the side we never see | B04・F02 |
| COMMS | radio | B06・B07・F01 |
| BURN | fire the engine | P01b・H05 |
| NAV | the ship's computer | P03b |
| CO2 | the gas you breathe out | P02b |
| Req 1〜4 | Goals 1〜4 | M01b・M01c |
| mass limit | the rocket can lift your ship | M01c |
| Free-return | the safe path, the Moon's gravity swings you back | H05b |

## 事実の確認（本物の話として言っていること）

出典の略号は `docs/reference/apollo13-data.md` と同じ。確かさ：◎＝NASA の一次資料、○＝NASA の別資料・概数・一般知識。

| 台詞 | 言っていること | 根拠 | 確かさ |
|---|---|---|---|
| B01 | NASA の管制センターはテキサス州ヒューストンにある | apollo13-data.md にはない。一般知識（1970年は Manned Spacecraft Center、今の Johnson Space Center） | ○ |
| B03 | アポロ13号は1970年 | apollo13-data.md 1章（打ち上げ 1970-04-11） | ◎ |
| B04・F02 | 月の裏側は地球から見えない | 一般知識（月はいつも同じ面を地球に向けている）。「暗い側」ではないので dark side とは言わない | ○ |
| B07・F01 | 月の裏では交信できない | apollo13-data.md 1章（LOS〜AOS 約25〜27分） | ○ |
| B05・B06 | 席は4つ | ゲームの決まり。本物は乗員3人＋地上の管制官（apollo13-data.md 7章の対応表）。B03 は「based on」なので本物も4席とは言っていない | — |
| M05a | 酸素タンクが壊れた | apollo13-data.md 3章・7章（タンク2） | ◎ |
| A01 | 「Houston, we've had a problem.」 | apollo13-data.md 9章（Lovell 055:55:35。Swigert は 055:55:19 に「...we've had a problem here」） | ○ |
| A02 | 酸素で電気も作っていた | apollo13-data.md 2章（燃料電池 FC1・FC3 が酸素を失って止まった） | ◎ |
| H05 | 観測向きの道から帰るには噴射が要る | apollo13-data.md 5章（MCC-2 で自由帰還から外れ、事故後の DPS-1 で戻した）。ゲームの M07 と同じ | ◎ |
| H05b | 自由帰還は月の重力で地球の方へ戻る道 | 一般知識＋apollo13-data.md 5章 | ○ |
| P02b | CO2 がたまるとよくない | apollo13-data.md 4章（CO2 がたまり、メールボックスで下げた） | ◎ |

ゲームの決まりで、本物の話ではないもの：P01（送信は地球向き・観測は月向き）、P04（データが取れるのは月の裏だけ）。どちらも「Apollo 13」と付けていない。

## 迷った所（Taiga に見てほしい）

1. **本物の目的とゲームの目的**：本物のアポロ13号は月に着陸する予定で、事故のあと着陸をやめ、月を回って帰った（着陸の予定は apollo13-data.md にない。一般知識）。ゲームの「裏側を観測する」はゲームの設定。B03 は「based on」、B04 は「Your mission」と書き、本物の目的だとは言っていない
2. **事故を先に言うか**：B08 は「Whatever happens」だけにして、事故の中身は言わない。アポロ13号を知っている人は予想がつくが、M05 の驚きは残る
3. **今の M05「What was that noise?!」**：本物では、バンという音を聞いたのは船の中の乗員で、地上は画面のデータで気づいた（はず）。地上のヒューストンが音を聞いたように読める。apollo13-data.md には「誰が音を聞いたか」は書いていないので確認待ち。M05a は音に触れない形にした
4. **今の M03「Two days out from Earth」**：本物の事故は打ち上げの約56時間後（apollo13-data.md 1章）なので「2日」は合う。ただゲームの M03 は打ち上げ直後（`houston_lines.json` の trigger「After launch (0:00)」）で、「打ち上げ成功」と「もう2日」が同じ台詞にある。置き換え案では落とした
5. **ブリーフィングの長さ**：8本で約34秒。もっと短くするなら B03 と B04 を1本にする（例「Based on Apollo 13 (1970): study the Moon's far side and come home.」）か、B02 を落とす

## 食い違い（気づいたもの）

- `docs/design/ui-guidelines.md` 3章の吹き出しは「最大3行・1行40字」（約120字）。`docs/design/houston_lines.json` では M01（336字）・M03・M05・M06・M07・M08（386字）・M09・M11・M12・ED-C の10本がそれより長い（#154 で直す）
- `docs/design/parts.json` の `trajectory_free_return` の benefit「returns to Earth with no burn」は、SPEC 6章・7章（自由帰還でも帰りの点火 `ignite` が要る）と合わない（UI の仕分けの表の食い違い5と同じ）。H05b は「swings you back toward Earth」にして、噴射が要らないとは言っていない
- サーバーは design に入ると同時に M01 を送る（`server/game/room.py`）。①を先に流すには、表示の側（#75）で M01 を待たせる必要がある
