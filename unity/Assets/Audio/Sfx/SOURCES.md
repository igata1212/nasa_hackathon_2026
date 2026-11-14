# sfx 一覧

作成: 2026-11-14（Claude）。形式: 44.1kHz・16bit・モノラル WAV（全ファイル）。
合成は `art/audio/synth_sfx.py` で再生成できる（乱数は固定シード）。

| ファイル | 用途 | 元 | ライセンス | 長さ | 備考 |
|---|---|---|---|---|---|
| alarm_master.wav | マスターアラーム（ループ） | 合成 | 自作 | 1.80 s | 660Hz / 880Hz を交互。0.6秒周期×3。端は無音でつながる |
| alarm_soft.wav | 注意の柔らかい警告（ピコン） | 合成 | 自作 | 0.50 s | |
| gas_hiss_loop.wav | ガス漏れのシューッ（ループ） | 合成 | 自作 | 3.00 s | 2.5〜7kHz のノイズ。1Hz でゆらぐ |
| quindar_in.wav | Quindar トーン（2525Hz） | 合成 | 自作 | 0.25 s | in=2525Hz・out=2475Hz と想定（送信開始・終了の対応は推定） |
| quindar_out.wav | Quindar トーン（2475Hz） | 合成 | 自作 | 0.25 s | 前後 5ms フェード |
| radio_static_loop.wav | 無線のザー（通信途絶・ループ・小さめ） | 合成 | 自作 | 2.00 s | 350〜3800Hz のノイズ＋小さなプチプチ |
| reentry_rumble_loop.wav | 再突入のゴーッ（ループ） | 合成 | 自作 | 3.00 s | 45〜420Hz の低いノイズ。1Hz の唸り |
| splash.wav | 着水のザブン | 合成 | 自作 | 1.00 s | 低いうなり＋しぶき＋余韻＋小さな泡 |
| ui_click.wav | ボタン | Kenney Interface Sounds `click_001.wav` | CC0 | 0.12 s | |
| ui_switch_on.wav | スイッチ ON | Kenney Interface Sounds `switch_004.wav`（ステレオ→モノラル） | CC0 | 0.50 s | ON/OFF は音の明るさで振り分けた（推定） |
| ui_switch_off.wav | スイッチ OFF | Kenney Interface Sounds `switch_007.wav`（ステレオ→モノラル） | CC0 | 0.62 s | 同上 |
| ui_confirm.wav | 成功の短い音 | Kenney Interface Sounds `confirmation_003.wav` | CC0 | 0.32 s | |
| ui_error.wav | エラー | Kenney Interface Sounds `error_005.wav` | CC0 | 0.52 s | |
| valve_turn.wav | バルブを回す（カチカチ） | 合成（`art/audio/synth_valve.py` の `v3_P_ticks_only`） | 自作 | 0.60 s | 金属のラチェットのカチカチ6回。短い雑音を細かい共鳴40個（1.8〜9kHz）に通す。2026-11-14 に差し替え（旧：Kenney `scratch_005.wav`。レコードをこする音に聞こえた） |
| thruster_burn_loop.wav | 噴射（ループ） | Kenney Sci-Fi Sounds `space_engine_low_000.ogg` を加工 | CC0 | 4.50 s | 元は 5.00 秒。末尾と先頭を 0.5 秒クロスフェードしてループ化。RMS を -15dBFS に揃えた（元は -7.6dBFS と大きい） |
| voice_problem.wav | 「Houston, we've had a problem」 | `apollo13_full.wav`（NASA・Commons 由来）の 7.21〜8.29 秒 | パブリックドメイン（Commons 表記） | 1.08 s | 7.36〜8.14 秒が「Houston, we've had a problem」（ロベルの言い直し）。前後 0.15 秒の余白。ピーク -3dBFS にそろえ、100Hz 以下をカット |

## 出典・ライセンス
- Kenney Interface Sounds（CC0）: https://kenney.nl/assets/interface-sounds ／ 取得元 https://github.com/Calinou/kenney-interface-sounds ／ LICENSE: `tmp/space_assets/08-sfx/kenney_interface_sounds_godot/addons/kenney_interface_sounds/LICENSE.txt`
- Kenney Sci-Fi Sounds（CC0 1.0）: https://kenney.nl/assets/sci-fi-sounds ／ 取得元 https://github.com/Boyquotes/kenney-sci-fi-sounds-for-godot ／ LICENSE: `tmp/space_assets/08-sfx/kenney_sci_fi_sounds_godot/LICENSE`
- NASA（Commons）: https://commons.wikimedia.org/wiki/File:Apollo13-wehaveaproblem.ogg （Commons 表記は "NASA public domain"）。NASA 本体での確認は未了。ダウンロード元は二次的な再配布元（`tmp/space_assets/03-nasa-audio/SOURCES.md` 参照）。
- 合成音（alarm・gas・quindar・radio・reentry・splash）: `art/audio/synth_sfx.py` で生成。自作。

## 注意
- alarm_master: 0.6 秒周期と 2 秒は割り切れないため、ループの継ぎ目を揃えるために 1.8 秒（3 周期）にした。2.4 秒（4 周期）にもできる。
- voice_problem: 2026-11-14 に手元の Whisper（faster-whisper small・CPU・外部送信なし）の単語時刻付き文字起こしで確定。`[7.08-8.14] Oh, Houston, we've had a problem.`（Houston 7.36 / problem 終わり 8.14）。2.22〜3.72 秒は「Okay, we've had a problem here.」（スワイガート）で別の発話。音量の波形でも 7.0〜8.15 秒に発話、8.2 秒以降は無音。短く切った後だけを聞かせると Whisper は聞き間違える（雑音が多いため）。切り出し前の旧版は 1.9〜4.2 秒（"Okay" と聞こえる部分）で誤りだった。
  - 違う場合は、正しい秒数（開始・終了）を指定すれば切り直せる。
- Kenney の選定はファイル名・長さ・音の明るさ（スペクトル重心）で選んだ。試聴はしていない。
- valve_turn: 2026-11-14 に earshot で候補 23 個を聞き比べて選んだ。ラチェットのカチカチだけの版が「a ratchet clicking」89%（「レゴのカチカチ」「氷がグラスに当たる音」とも）。きしみ音（error buzz・glitch に聞こえる）、合成のガチャン（カリンバ・チャイムに聞こえる）、Kenney の `impact_metal_*`（木の板・泡に聞こえる）を足すと、どれも悪くなった。「閉まった」ことは、ガスのシューが止まる音と ui_confirm で伝える想定。
  - 本物の録音（Kenney RPG Audio の `metalLatch`・`creak` など、CC0）にすればもっと良くなる見込み。未取得。
- sfx/ 以外（`tmp/audio_build/music/`）には触れていない。
