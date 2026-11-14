# 書いた人：iizuka（作成：Haiku 5.5）／ 最終更新：2026-11-14
#
# UI の書体を Unity に入れる形にする（UI ガイドライン 2 章・5 章）。
# 元：tmp/ui_ref/fonts/（google/fonts、SIL OFL 1.1。コミットしない）
# 出力：unity/Assets/Art/UI/Fonts/
#   Baloo2-ExtraBold.ttf（見出し・ボタン。wght 800 を static に）
#   Nunito-Regular.ttf・Nunito-Bold.ttf（本文・台詞。wght 400・700 を static に）
#   ZenMaruGothic-Black/Regular/Bold.ttf（日本語の開発用の表示。そのままコピー）
#   OFL-*.txt（各書体の OFL 本文。書体名を付けた）
#
#   bash art/ui/make_fonts.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SRC="$ROOT/tmp/ui_ref/fonts"
DST="$ROOT/unity/Assets/Art/UI/Fonts"
mkdir -p "$DST"

# 可変フォントを1つの太さに固定する（fontTools の instancer）
instance() {  # 元の .ttf、軸の指定（例 wght=800）、出力名
  uv run --with fonttools python -m fontTools.varLib.instancer "$1" "$2" -o "$DST/$3" --update-name-table
}
instance "$SRC/Baloo2/Baloo2[wght].ttf" wght=800 Baloo2-ExtraBold.ttf
instance "$SRC/Nunito/Nunito[wght].ttf" wght=400 Nunito-Regular.ttf
instance "$SRC/Nunito/Nunito[wght].ttf" wght=700 Nunito-Bold.ttf

# Zen Maru Gothic は static のままコピーする
for w in Black Regular Bold; do
  cp "$SRC/ZenMaruGothic/ZenMaruGothic-$w.ttf" "$DST/"
done

# OFL 本文（書体名を付けて置く）
cp "$SRC/Baloo2/OFL.txt" "$DST/OFL-Baloo2.txt"
cp "$SRC/Nunito/OFL.txt" "$DST/OFL-Nunito.txt"
cp "$SRC/ZenMaruGothic/OFL.txt" "$DST/OFL-ZenMaruGothic.txt"

echo "書体を $DST に置いた"
