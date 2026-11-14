書いた人：iizuka（Claude） ／ 最終更新：2026-11-14

# Mission Return ゲームサーバー（Python・WebSocket）

`docs/design/architecture.md` の決定どおり、サーバーが状態を1つ持ち、クライアント（Unity）はコマンドを送って状態を受け取るだけ。
計算は `docs/design/reference_sim.py` を **import して** そのまま使う（複製しない。`game/sim_bridge.py`）。

## 動かす

```sh
cd server
uv sync
uv run python main.py --port 8765              # ローカル。ws://localhost:8765
uv run python main.py --port 8765 --speed 20   # 早送り（テスト用。ゲーム内1秒 = 実時間 1/20 秒）
uv run pytest                                  # 全テスト（通しのテストで約1分）
uv run python tools/bot_client.py --bots 4 --case T1                 # ボット4人で1回遊ぶ（結果が reference_sim と一致するか出る）
uv run python tools/bot_client.py --seats pilot,comms_power          # 人数の足りない席をボットで埋める（下の「人と一緒に遊ぶ」）
uv run python tools/export_timeline.py         # Unity のオフライン再生用の記録を data/sim/ に書き出す
```

`--host` を省略すると IPv4・IPv6 の両方で待つ。Unity（.NET の `ClientWebSocket`）は `ws://localhost:8765` でつなぐ（localhost が `::1` に解決されても届く。テストあり）。
1メッセージ = 1つのテキストフレームの JSON。

**人と一緒に遊ぶ（足りない席をボットで埋める）**：ボットは指定した席だけを持ち、PLAY_GOOD（`--case` で変更可）の時刻どおりにその席の操作を送る。練習（M03）とかき混ぜ（自分が Life Support のとき）も自動。レポートのあとも切らずに次のゲームを待つ（Ctrl+C で終わる）。

```sh
# A) ボットが部屋を作る → 出てきた合言葉で Unity から join → Unity から start・設計・launch
uv run python tools/bot_client.py --seats pilot,comms_power
# B) Unity が作った部屋に入る
uv run python tools/bot_client.py --seats life_support,mission_planner --room 1234
# C) 設計・打ち上げもボットに任せる（2人そろったら start）
uv run python tools/bot_client.py --seats pilot,comms_power --drive --players 2
```

同時押しの相手が人の席なら、ボットが押してから 0.5 秒（`press_window_s`）以内に人が押せば成立する。

**Cloudflare Tunnel で公開する**（ポート開放なし。wss が付く。WebGL は wss が必須）：

```sh
cloudflared tunnel --url http://localhost:8765
# 出てきた https://xxxx.trycloudflare.com を、Unity では wss://xxxx.trycloudflare.com として使う
```

## 構成

| ファイル | 中身 |
|---|---|
| `main.py` | 起動（`--host` `--port` `--speed` `--difficulty`） |
| `game/sim_bridge.py` | `reference_sim` と JSON（parameters / parts / houston_lines）を読む |
| `game/room.py` | 1部屋の進行（段階、コマンド、同時押し、tick、マイルストーン、記録）。WebSocket に依存しない |
| `game/seats.py` | 人数と席の割り当て、コマンドごとの席の権限 |
| `game/houston.py` | 状況に反応する台詞（SPEC 9章） |
| `game/report.py` | R01 のスコアの内訳 |
| `game/config.py` | SPEC にあるが parameters.json に無い数値（90秒、20秒など） |
| `game/server.py` | WebSocket、合言葉の部屋、部屋ごとの時計 |
| `tools/bot_client.py` | テスト用クライアント |
| `tools/export_timeline.py` | 1回遊んだ記録を JSON に書き出す（Unity のオフライン再生用） |
| `tests/` | 一致テスト・ルール・通しのテスト |

## 流れ

`lobby` →（`start`）→ `design`（M01。部品の ±、`launch` で M02 の判定。だめなら design のまま）→ `practice`（M03。各席が1回操作 or 90秒。リソースは減らない）→ `stir`（M04。Life Support が `stir`。20秒ごとに M04 を繰り返す）→ `flight`（M05〜。ゲーム内1秒ごとに tick）→ `ending`（ED-A/B/C。16秒か `next`）→ `report`（R01）→（`again`）→ `design`

flight の1 tick：①幅を過ぎた同時押しを不成立にする → ②届いた順にコマンドを `Sim` のメソッドに渡す → ③`sim.step()` → ④マイルストーンの台詞 → ⑤ヒューストン → ⑥記録して `state` を全員に送る。
tick k−1 と tick k の間に届いたコマンドは、`reference_sim.run()` で「時刻 k のイベント」と同じ扱いになる。

## メッセージ（すべて JSON）

### クライアント → サーバー

部屋に入る（最初の1通）：

```json
{"type":"create","name":"aki"}                       // 部屋を作る。4桁の合言葉が welcome で返る
{"type":"join","room":"1234","name":"ben"}            // 合言葉で入る（lobby の間だけ。1〜4人）
{"type":"join","room":"1234","player_id":"9f3a01bc"}  // 切れたあと同じ席に戻る（どの段階でも）
{"type":"join","room":"1234","name":"bot1","seats":["pilot","comms_power"],"bot":true}  // 席を指定して入る（bot_client --seats が使う）
```

`create` にも `seats` `bot` `difficulty` を付けられる。席を指定した人はその席だけを持ち、指定のない人には残りの席を下の割り当てで配る。

段階を進める・設計：

```json
{"type":"start"}                                      // lobby → design（誰が送ってもよい）
{"type":"design","part":"battery","delta":1}          // ± ボタン（min〜max に丸める）
{"type":"design","part":"battery","n":3}              // 個数を直接
{"type":"design","trajectory":"science"}              // free_return / science
{"type":"design","rocket":"large"}                    // small / large
{"type":"launch"}                                     // M02 打ち上げ判定
{"type":"next"}                                       // ending → report
{"type":"again"}                                      // report → design（設計は前回のまま）
{"type":"ping","t":123}                               // → pong（遅延の実測用）
```

操作（SPEC 6章。`architecture.md` の案どおり `cmd` キーで送る）。`seat` は省略可（自分の席のうち、そのコマンドを使える最初の席になる）。
practice の間は同じメッセージが「練習」として数えられるだけで、何も変わらない。

```json
{"cmd":"set_attitude","mode":"moon","seat":"pilot"}           // earth / moon / sun / burn / heatshield
{"cmd":"correction_burn","seat":"pilot"}                      // t >= m06_delay_s から
{"cmd":"ignite","seat":"pilot"}                               // 同時押し：pilot と comms_power
{"cmd":"close_valve","tank":2,"seat":"life_support"}          // tank は 1 か 2。漏れは Tank 2
{"cmd":"swap_filter","seat":"life_support"}                   // 同時押し：life_support と誰か1席
{"cmd":"switch","name":"heater","on":false,"seat":"comms_power"}     // heater / lights / nav / comms
{"cmd":"switch","name":"camera","on":true,"seat":"mission_planner"}  // camera / spectrometer / radar
{"cmd":"set_transmit","on":true,"seat":"comms_power"}
{"cmd":"set_route","route":"a","seat":"mission_planner"}      // t >= m06_delay_s から、点火まで
{"cmd":"stir","seat":"life_support"}                          // M04 のかき混ぜ（stir の段階だけ）
```

席：`pilot` `comms_power` `life_support` `mission_planner`。
人数と席（入室順。席を指定した人がいないとき）：4人＝1人1席、3人＝[pilot] [comms_power, mission_planner] [life_support]、2人＝[pilot, mission_planner] [life_support, comms_power]、1人＝全席。
1人で同時押しをするときは、同じ人が `seat` を変えて2通送る。

### サーバー → クライアント

```json
{"type":"welcome","room":"1234","player_id":"9f3a01bc","seats":["pilot"],"speed":1.0}   // 本人だけ
{"type":"room","room":"1234","phase":"design","players":[{"id":"9f3a01bc","name":"aki","seats":["pilot"],"connected":true,"bot":false}]}
// phase: lobby / design / practice / stir / flight / ending / report。段階が変わるたび・人の出入りのたびに全員へ
{"type":"design","design":{"parts":{"battery":3,"camera":1},"trajectory":"science","rocket":"large"},
 "totals":{"mass":96,"cost":90,"status":"ok","mass_limit":140,"budget":110,"capacity":150,"oxygen":100,"fuel":100}}
{"type":"state","t":120,"state":{ ... 下の表 ... }}                                          // flight の間、毎秒
{"type":"result","result":{ ... reference_sim の result() と同じ ... }}
{"type":"report","result":{...},"design":{...},"score_breakdown":{"ending":500,"science":337.5,"resources":113.1,"teamwork":100,"budget":10,"all_requirements":200},
 "history":[{"t":0,"power":130,"oxygen":100,"fuel":100,"temp":22,"co2":10,"data_collected":0,"data_sent":0,"attitude":"earth"}, ...],
 "commands":[{"t":0,"seat":"comms_power","player":"9f3a01bc","cmd":"switch","args":{"name":"lights","on":false},"ok":true}, ...]}
{"type":"error","code":"seat_not_allowed","detail":"...","echo":{...}}                      // 本人だけ
{"type":"pong","t":123}
```

イベント（`type":"event"`）：

```json
{"type":"event","kind":"houston","id":"M06","priority":"milestone","text":"...","text_ja":"...","t":20}   // マイルストーン・エンディング・R01
{"type":"event","kind":"houston","id":"H12","priority":"critical","text":"...","text_ja":"...","t":350}   // 状況に反応する台詞（声で流す）
{"type":"event","kind":"warning","id":"H18","priority":"high","text":"...","t":200,"reason":"no_signal"}  // 声なし、パネルの警告だけ（no_signal / comms_off）
{"type":"event","kind":"launch","status":"over_mass","mass":111,"cost":85}                               // ok / over_mass / over_budget
{"type":"event","kind":"practice","seat":"pilot","cmd":"set_attitude","done":["pilot"]}
{"type":"event","kind":"press","cmd":"ignite","status":"waiting","seats":["pilot"],"window_s":0.5}       // 同時押しの片方が届いた
{"type":"event","kind":"press","cmd":"ignite","status":"together","seats":["comms_power","pilot"]}       // 成立
{"type":"event","kind":"press","cmd":"ignite","status":"failed","seats":["pilot"]}                       // 不成立（ignite は fuel_burn_retry を引く）
{"type":"event","kind":"cmd","t":25,"seat":"pilot","player":"...","cmd":"correction_burn","args":{},"ok":true}  // Sim に渡したコマンドと結果
```

`ok` は Sim のメソッドの戻り値（`switch`・`correction_burn`・`ignite`・`swap_filter`。戻り値の無いものは `null`、違うタンクの `close_valve` は `false`）。
M04 の繰り返しは `"repeat":true` が付く。

`state.state` の中身（SPEC 3章の状態変数 + 軌道の時刻 + 操作の遅れ）：

| キー | 中身 |
|---|---|
| `t` | 事故からの秒 |
| `capacity` `power` `oxygen` `fuel` `temp` `co2` `filters` | リソース（float はまるめていない） |
| `leak_open` `leaked` | 酸素漏れ |
| `sw` | `{"heater","lights","nav","comms","camera","spectrometer","radar"}` の ON/OFF |
| `attitude` `transmit` | 姿勢、送信 |
| `observed_s` `data_collected` `data_sent` | 科学データ |
| `correction_done` `route` `burn_done` `first_try` `strict` `swaps` `ending` | 軌道・判定 |
| `has_signal` `in_farside` | 通信できるか、観測の窓の中か |
| `t_los` `t_aos` `t_burn_open` `t_burn_close` `t_reentry` `t_splashdown` | 軌道の時刻（Unity が GMAT の CSV と対応づける。`t_reentry` は route で変わる。route が null の間は B として計算） |
| `slow_pct` | 操作の遅れ（%）。`max(寒さ, CO2)`。長押しの時間「0.5秒 ×（1 + slow_pct/100）」は Unity 側 |

エラーの `code`：`bad_json` `not_in_room` `no_such_room` `room_full` `game_started` `unknown_player` `wrong_phase` `unknown_type` `unknown_cmd` `bad_args` `not_your_seat` `seat_not_allowed` `locked` `seat_taken` `no_free_seat`。
エラーを返しても接続は切らない。

## Unity のオフライン再生用（`data/sim/`）

`tools/export_timeline.py` が、T1（`PLAY_GOOD`）と T4（失敗）を1回遊んだ記録を、上と**同じ形のメッセージ**のまま時刻順に並べて書き出す。サーバーなしで画面を作るときに使う。

```json
{"meta":{"title":"...","design":{...},"events":[...],"players":[...],"viewer":"<bot0の player_id>","result":{...}},
 "messages":[{"at":0.0,"to":"<player_id>","msg":{"type":"welcome",...}},
             {"at":5.0,"to":null,"msg":{"type":"room","phase":"design",...}}, ...]}
```

`at` は部屋を作ってからの実時間の秒（speed=1）、`to` は null なら全員宛て。事故より前（ロビー・設計・打ち上げ判定・練習・かき混ぜ）は仮の操作を入れてある。4人プレイ、視点は bot0（Pilot）。

## テスト

- `tests/test_match.py`：WebSocket を通さずに Game を進め、T1・T2・T4・T5 × 1〜4人で `reference_sim.run()` の結果と dict ごと完全一致。SPEC 10章の表の値とも一致。T3 は質量オーバーで design に残る
- `tests/test_rules.py`：同時押しの成立・不成立（幅ちょうど／幅の外／窓の外）、swap_filter、1人で両席、人数と席、席の権限、解禁前の `locked`、H07 の繰り返し、通信 OFF→ON の H27、1 tick 1本、R01 の内訳の合計 = スコア、練習の90秒、M04 の繰り返し、席を指定して入る（seat_taken・no_free_seat）
- `tests/test_e2e.py`：サーバーを別プロセスで `--speed 40` で起動し、ボット（4人 T1・3人 T5・2人 T2・1人 T4）で部屋→設計→打ち上げ→練習→事故→着水→レポートまで。結果は reference_sim と完全一致。席を指定したボット（pilot・comms_power）＋もう1人でも完全一致。`localhost`・`127.0.0.1`・`[::1]` のどれでもつながる
- `tests/test_export.py`：オフライン再生用の記録が reference_sim と一致

## 今日の範囲（SPEC 11章の「必須」を優先）

- 計算（CO2・フィルター・ソーラーパネル・難易度を含む）は reference_sim をそのまま呼ぶので、サーバー側で削ったものはない。Unity 側で後回しにしてよいのは、それらの**画面**（フィルター交換のボタン、太陽向きの表示、難易度の選択など）
- 状況に反応する台詞は critical と high だけ流す（`game/config.py` の `HOUSTON_PRIORITIES`）：H01 H02 H07 H08 H09 H11 H12 H14 H16 H18 H24 H25。加えて SPEC 9章のルールの H27。medium の閾値の台詞（H10 H13 H15 H17 H26）はコードにあるが止めてある
- 食い違いは SPEC と reference_sim のとおり（`close_valve` は Tank 2 固定、かき混ぜは Life Support の押しボタン `stir`）

## TODO（未実装）

- 台詞：medium・low（H03 H04 H05 H06 H10 H13 H15 H17 H19 H20 H21 H22 H23 H26 H28）。閾値のもの（H10 H13 H15 H17 H26）は `HOUSTON_PRIORITIES` に "medium" を足せば出る
- 台詞の長さ：今は「1 tick に1本」。読み上げの長さを見て間を空ける処理はない
- M09 の「パネルの警告表示」用の専用イベントはない（`state.has_signal` で判断する）
- 再接続：`player_id` で戻れるが、全員が切れた部屋（lobby 以外）は消えない
- 部屋の一覧・ホストの権限・人数が途中で減ったときの席の付け替え
- 難易度：`--difficulty` と `create` の `difficulty` は Sim に渡すだけ（UI は無い）
- クラウド（Fly.io など）への置き方
