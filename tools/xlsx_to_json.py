#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ミッション・リターン イベント表.xlsx から JSON を作る（標準ライブラリのみ）

出力（docs/design/）: parameters.json / parts.json / houston_lines.json
key とシートの行の対応表は下の PARAM_KEYS / PART_ROWS に明示してある（自動生成しない）。
値はシートのまま（% も小数にせず、分も秒にしない。読み替えは reference_sim 側）。

使い方:  python3 tools/xlsx_to_json.py
"""
import json
import os
import re
import sys
import zipfile
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DESIGN = os.path.join(ROOT, "docs", "design")
XLSX = os.path.join(DESIGN, "ミッション・リターン イベント表.xlsx")
GENERATED_BY = "tools/xlsx_to_json.py（元：ミッション・リターン イベント表.xlsx）"

NS = {"m": "http://schemas.openxmlformats.org/spreadsheetml/2006/main",
      "r": "http://schemas.openxmlformats.org/officeDocument/2006/relationships"}
M = "{%s}" % NS["m"]


# ---------------------------------------------------------------- xlsx 読み込み
def read_workbook(path):
    """戻り値 {シート名: {行番号: {列文字: 値}}}（値は文字列。数値セルは数値）"""
    z = zipfile.ZipFile(path)
    ss = []
    if "xl/sharedStrings.xml" in z.namelist():
        for si in ET.fromstring(z.read("xl/sharedStrings.xml")).findall("m:si", NS):
            ss.append("".join(t.text or "" for t in si.iter(M + "t")))
    rels = {r.get("Id"): r.get("Target")
            for r in ET.fromstring(z.read("xl/_rels/workbook.xml.rels"))}
    book = {}
    for s in ET.fromstring(z.read("xl/workbook.xml")).find("m:sheets", NS):
        target = rels[s.get("{%s}id" % NS["r"])].lstrip("/")
        if not target.startswith("xl/"):
            target = "xl/" + target
        rows = {}
        for row in ET.fromstring(z.read(target)).iter(M + "row"):
            cells = {}
            for c in row:
                col = re.sub(r"\d+", "", c.get("r"))
                v = c.find("m:v", NS)
                if v is None:
                    is_ = c.find("m:is", NS)
                    if is_ is None:
                        continue
                    val = "".join(t.text or "" for t in is_.iter(M + "t"))
                elif c.get("t") == "s":
                    val = ss[int(v.text)]
                elif c.get("t") in ("str", "inlineStr", "b", "e"):
                    val = v.text
                else:
                    f = float(v.text)
                    val = int(f) if f == int(f) else f
                cells[col] = val
            rows[int(row.get("r"))] = cells
        book[s.get("name")] = rows
    return book


# ---------------------------------------------------------------- 対応表: Parameters（行番号 → key）
PARAM_KEYS = {
    2: "time_p1_cruise_min",            # P1 出発・巡航 2 min
    3: "time_p2_accident_min",          # P2 事故（月の裏側に入るまで）2 min
    4: "time_p3_farside_min",           # P3 月の裏側 3 min
    5: "time_p4_return_a_min",          # P4 帰還ルートA 2 min
    6: "time_p4_return_b_min",          # P4 帰還ルートB 3.5 min
    7: "time_p5_reentry_min",           # P5 再突入 1 min
    8: "power_per_battery",             # バッテリー1個あたりの容量 50 %
    9: "power_accident_loss",           # 事故での減少 20 %
    10: "power_life_support",           # 生命維持 3 %/min
    11: "power_heater",                 # 暖房
    12: "power_lights",                 # 照明
    13: "power_nav",                    # 航法コンピュータ
    14: "power_comms",                  # 通信
    15: "power_nav_restart",            # 航法の再起動 8 %
    16: "power_camera",                 # カメラ（観測中）
    17: "power_spectrometer",           # 分光計（観測中）
    18: "power_radar",                  # レーダーサウンダー（観測中）
    19: "solar_sun",                    # ソーラーパネル 太陽向き 3 %/min
    20: "solar_other",                  # ソーラーパネル 他の姿勢 1 %/min
    21: "oxygen_start",                 # 100 %
    22: "oxygen_per_spare_tank",        # 20 %
    23: "oxygen_leak_per_s",            # 漏れの速さ 2 %/s
    24: "oxygen_leak_max",              # 漏れで失う量の上限 40 %
    25: "oxygen_breathing",             # 呼吸 6 %/min
    26: "oxygen_co2_extra",             # CO2が高い間の追加消費 2 %/min
    27: "fuel_start",                   # 100 %
    28: "fuel_per_extra_tank",          # 25 %
    29: "fuel_attitude_change",         # 姿勢変更1回 1 %
    30: "fuel_correction_burn",         # 軌道修正の噴射 15 %
    31: "fuel_burn_a",                  # 帰還噴射ルートA 50 %
    32: "fuel_burn_b",                  # 帰還噴射ルートB 25 %
    33: "fuel_burn_retry",              # 噴射のやり直し 10 %
    34: "temp_start",                   # 22 °C
    35: "temp_drop",                    # 暖房OFFでの低下 1.5 °C/min
    36: "temp_rise",                    # 暖房ONでの上昇 3 °C/min
    37: "temp_slow_c",                  # 動作が遅くなる温度 10 °C（Houston 台詞・操作速度用）
    38: "temp_slower_c",                # さらに遅くなる温度 5 °C
    39: "co2_start",                    # 事故直後の値 10
    40: "co2_rise",                     # 上昇の速さ 12 /min
    41: "co2_warn",                     # 警告を出す値 60
    42: "co2_slow",                     # 動作が遅くなる値 80
    43: "co2_filters",                  # フィルター交換できる回数 2
    44: "sci_camera",                   # カメラの科学価値 1
    45: "sci_spectrometer",             # 分光計 2
    46: "sci_radar",                    # レーダー 1.5
    47: "data_camera",                  # 10 data/min
    48: "data_spectrometer",            # 20
    49: "data_radar",                   # 15
    50: "tx_standard",                  # 送信速度 標準 30
    51: "tx_high_gain",                 # 高利得 60
    52: "sci_trajectory_mult",          # 観測向き軌道の倍率 1.5
    53: "sci_carried_home",             # 送らずに持ち帰ったデータの価値 0.5
    54: "burn_window_s",                # 噴射窓の長さ 60 s
    55: "press_window_s",               # 同時押しの受付幅 0.5 s
    56: "end_oxygen_min",               # 全員生還の基準 酸素 20 %
    57: "end_power_min",                # 電力 15 %
    58: "end_temp_min",                 # 温度 10 °C
    59: "req_science_min",              # Req 2 科学価値の最小 1.5
    60: "score_ed_a",                   # 500
    61: "score_ed_b",                   # 300
    62: "score_per_science",            # 100
    63: "score_per_resource",           # 1 pt/%
    64: "score_per_budget",             # 2 pt/unit
    65: "score_teamwork",               # 50
    66: "score_all_requirements",       # 200
    67: "difficulty_1",                 # 1
    68: "difficulty_2",                 # 1.15
    69: "difficulty_3",                 # 1.3
    70: "burn_window_open_after_aos_s", # 通信回復から噴射窓が開くまで 30 s
    71: "reentry_ready_s",              # 再突入の準備に使える時間 20 s
    72: "slow_cold_pct",                # 10℃未満での操作の遅れ 30 %
    73: "slow_colder_pct",              # 5℃未満 50 %
    74: "slow_co2_pct",                 # CO2が高い間 30 %
    75: "end_strict_margin",            # 厳しくする幅 5 %points
    76: "houston_cooldown_s",           # 30 s
    77: "houston_leak_repeat_s",        # 8 s
    78: "m06_delay_s",                  # 事故からM06の台詞まで 20 s
}

# ---------------------------------------------------------------- 対応表: Parts（id → シートの行）
# 上限は行1〜3（B列）。部品は行7〜19（C=質量, D=コスト, E=最小, F=最大）。
LIMIT_ROWS = {"budget": 1, "rocket_small_mass": 2, "rocket_large_mass": 3}
PART_ROWS = {
    "bus": 7,
    "battery": 8,
    "solar_panel": 9,
    "spare_o2_tank": 10,
    "camera": 11,
    "spectrometer": 12,
    "radar": 13,
    "high_gain_antenna": 14,
    "extra_fuel_tank": 15,
    "trajectory_free_return": 16,
    "trajectory_science": 17,
    "rocket_small": 18,   # コストは D 列（0）。質量上限は limits 側
    "rocket_large": 19,   # コスト 20
}

# 優先度（日本語 → SPEC の英語）
PRIORITY = {"最優先": "critical", "高": "high", "中": "medium", "低": "low"}


def build_parameters(book):
    sh = book["Parameters"]
    params = []
    for row, key in PARAM_KEYS.items():
        c = sh[row]
        params.append({"key": key, "value": c["C"], "unit": c.get("D", ""),
                       "group": c.get("F", ""), "item": c.get("G", ""),
                       "item_ja": c["B"], "row": row})
    assert len(params) == 77 and len({p["key"] for p in params}) == 77
    assert sorted(PARAM_KEYS) == sorted(r for r in sh if r >= 2), "Parameters の行と対応表がずれている"
    return {"_generated_by": GENERATED_BY, "params": params}


def build_parts(book):
    sh = book["Parts"]
    limits = {k: sh[r]["B"] for k, r in LIMIT_ROWS.items()}
    parts = []
    for pid, r in PART_ROWS.items():
        c = sh[r]
        p = {"id": pid, "name": c.get("K", ""), "category": c.get("J", ""), "row": r,
             "mass": c["C"] if isinstance(c.get("C"), (int, float)) else None,
             "cost": c["D"]}
        if "E" in c:
            p["min"], p["max"] = c["E"], c["F"]
        p["benefit"], p["weakness"] = c.get("L", ""), c.get("M", "")
        parts.append(p)
    return {"_generated_by": GENERATED_BY, "parts": parts, "limits": limits}


def build_houston(book):
    ms = []
    for r, c in sorted(book["Milestones"].items()):
        if r == 1:
            continue
        ms.append({"id": c["A"], "phase": c.get("J", ""), "trigger": c.get("K", ""),
                   "line": c.get("L", ""), "line_ja": c.get("D", ""),
                   "opens": c.get("M", ""), "required": c.get("N", ""),
                   "if_missed": c.get("O", ""), "row": r})
    hl = []
    for i, (r, c) in enumerate(sorted((r, c) for r, c in book["Houston lines"].items() if r >= 4), 1):
        hl.append({"id": "H%02d" % i, "situation": c.get("F", ""), "when": c.get("G", ""),
                   "line": c.get("H", ""), "line_ja": c.get("C", ""),
                   "priority": PRIORITY[c["D"]], "notes": c.get("J", ""), "row": r})
    # シートの Milestones は M01-M12 + ED-A/B/C + R01 の16本（SPEC.md 0章と同じ。#97）
    assert len(ms) == 16 and len(hl) == 28, (len(ms), len(hl))
    return {"_generated_by": GENERATED_BY, "milestone_lines": ms, "reactive_lines": hl}


def main():
    book = read_workbook(XLSX)
    out = {"parameters.json": build_parameters(book), "parts.json": build_parts(book),
           "houston_lines.json": build_houston(book)}
    for name, data in out.items():
        with open(os.path.join(DESIGN, name), "w", encoding="utf-8") as f:
            json.dump(data, f, ensure_ascii=False, indent=2)
            f.write("\n")
        print("wrote", os.path.join("docs/design", name))


if __name__ == "__main__":
    sys.exit(main())
