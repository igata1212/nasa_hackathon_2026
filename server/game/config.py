"""SPEC に書いてあるが parameters.json に無い数値と、サーバー側の都合の値。

本当は parameters.json に入れたい（SPEC 0章「数値はJSON」）。入ったらここを JSON 読みに替える。
時間はすべて「ゲーム内の秒」。--speed で早送りすると実時間は 1/speed になる。
"""
PRACTICE_MAX_S = 90        # M03：各席が1回操作 or 90秒
STIR_REPEAT_S = 20         # M04：20秒押されなければ台詞を繰り返す
M09_LEAD_S = 10            # M09：T_LOS の10秒前に流す（SPEC 4章・提案）
LEAKING_TANK = 2           # SPEC 6章：漏れているのは Tank 2 固定
TANKS = (1, 2)
H09_AFTER_S = 30           # H09：事故から30秒たっても何も切っていない
WARN_BEFORE_BURN_S = 30    # H24・H25：噴射窓まで30秒
ENDING_HOLD_S = 8          # Ending の画面を出してから Report に進むまで（"next" でも進む）

# 状況に反応する台詞の閾値（houston_lines.json の when の文に書いてある数値）
H_POWER_HALF = 0.50        # H10：容量の50%
H_POWER_LOW = 0.25         # H11：容量の25%
H_POWER_CRIT = 0.10        # H12：容量の10%
H_OXYGEN_40 = 40           # H13
H_OXYGEN_20 = 20           # H14
H_FUEL_LOW = 15            # H26

# 流す反応の台詞の優先度（H27 は SPEC 9章のルールなので別扱いでいつも流す）
HOUSTON_PRIORITIES = ("critical", "high")
