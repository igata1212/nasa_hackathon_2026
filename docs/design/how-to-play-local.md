書いた人：iizuka ／ 最終更新：2026-11-14

# 手元で通して遊ぶ（サーバー + Unity + ボット）

1. **サーバー**：`cd server && uv run python main.py --port 8765`（確認だけなら `--speed 4` で4倍速。ゲーム内6分 → 1分半）
2. **Unity**：`unity/` を開き `Assets/Scenes/Game.unity` を Play（無ければ メニュー Tools/GameFlow/シーンを作り直す）。Title で名前、Server URL `ws://localhost:8765`
3. **部屋**：Title の **CREATE ROOM** → Lobby に4桁の合言葉が出る。ほかの人は合言葉を入れて **JOIN**
4. **足りない席をボットで埋める**（人が持たない席を `--seats` に書く。START を押す前に入れる）
   - Unity が作った部屋に：`cd server && uv run python tools/bot_client.py --seats comms_power,life_support,mission_planner --room 1234`
   - ボットに部屋を作らせる：`uv run python tools/bot_client.py --seats pilot,comms_power` → 出た合言葉で Unity から JOIN
   - 席の名前：`pilot` `comms_power` `life_support` `mission_planner`。ボットは PLAY_GOOD の時刻どおりに自分の席だけ操作する
5. **流れ**：Lobby の START MISSION → 設計（± と軌道・ロケット → READY FOR LAUNCH。重すぎ・予算オーバーなら設計に戻る）→ 練習（各席1回操作、または90秒）→ かき混ぜ（Life Support の STIR）→ 事故 → 月の裏側 → 噴射（Pilot と Comms & Power が 0.5 秒以内に IGNITE）→ 再突入 → 着水 → Ending → Report → PLAY AGAIN（設計は前回のまま）

## 人数ごと

- **話し方**：同じ場所で声を出して話す。離れて遊ぶときは Discord・LINE などの通話を横でつなぐ（ゲームの中に声のやりとりは無い。定型の合図のクイックピンだけ、#156）
- **4人**：1人1席。**3人**：[Pilot] [Comms & Power + Mission Planner] [Life Support]。**2人**：[Pilot + Mission Planner] [Life Support + Comms & Power]
- **1人**：ボットなしで全席を持って遊べる（下のタブで席を切り替える）。同時押しは、2席を自分が持っていれば **IGNITE (both seats)**（Pilot と Comms & Power）・**SWAP FILTER (both seats)**（Life Support と自分の別の席）のボタンが出て、1回押すと両方の席の分を続けて送る。相手の席をボットに持たせてもよい
- 2台以上の PC：サーバーの PC の IP で `ws://192.168.x.x:8765`。外から：`cloudflared tunnel --url http://localhost:8765` → `wss://xxxx.trycloudflare.com`

## サーバーなしで見る

Title の **REPLAY: SUCCESS (T1)** / **REPLAY: FAILURE (T4)**：記録（`data/sim/timeline_*.json`。Tools/GameFlow/オフライン再生の記録を取り込む で Unity に写す）の再生。左の x5〜x60・SKIP で早送り。操作は効かない

## よくあるつまずき

- `ERROR: game_started`：START のあとは新しく入れない（ボットも）。START の前に全員そろえる
- `Connection error`：サーバーが動いていない／URL の打ち間違い（`ws://` を付ける。WebGL のビルドは未対応）
- 席のボタンが無い：自分の席でない（Lobby の表示を見る）。CORRECTION BURN と ROUTE は事故から20秒（M06）までは `locked`
- エディタを裏に回すと Play が止まる（Unity の仕様）。通しの確認は Unity を前に出して行う
