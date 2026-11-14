#!/bin/sh
# Blender をコマンドラインで起動してスクリプトを流す。GUI の Blender（MCP）は使わない。
#   modeling/core/run.sh modeling/fx/build_alarm.py
#   modeling/core/run.sh modeling/core/check.py modeling/fx
# 書いた人：iizuka（作成：Fable 5.2）／ 最終更新：2026-11-14
BLENDER="${BLENDER:-/Applications/Blender.app/Contents/MacOS/Blender}"
script="$1"
[ -n "$script" ] || { echo "使い方: run.sh <script.py> [引数...]" >&2; exit 2; }
shift
PYTHONDONTWRITEBYTECODE=1 exec "$BLENDER" -b --factory-startup --python-exit-code 1 --python "$script" -- "$@"
