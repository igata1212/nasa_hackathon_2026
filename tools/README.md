# tools

- `python3 tools/xlsx_to_json.py` : `docs/design/ミッション・リターン イベント表.xlsx` から `parameters.json` / `parts.json` / `houston_lines.json` を `docs/design/` に作る（標準ライブラリのみ）。
- key とシートの行の対応表はスクリプト内の `PARAM_KEYS` / `PART_ROWS` / `LIMIT_ROWS`。シートの行を増減したらここも直す。
- 確認は `python3 docs/design/reference_sim.py`。結果が SPEC.md 10章の T1〜T5 と一致すればよい。
