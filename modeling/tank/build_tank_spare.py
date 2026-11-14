"""Tank_OxygenSpare1・Tank_OxygenSpare2：設計画面で積める予備の酸素タンク（#46）。

書いた人：Taiga（作成：Fable 5.2）／ 最終更新：2026-11-14

  modeling/core/run.sh modeling/tank/build_tank_spare.py
出力：unity/Assets/Models/Tank/Tank_OxygenSpare*.fbx、modeling/tank/export/、modeling/tank/renders/（各3枚＋320px 版、
      メインのタンクと並べた一覧 tank_spare_lineup.png と tank_spare_lineup_320.png）

形はメインのタンク（build_tank.py の build()）と同じ大きさ・同じ部品。違いは2つ：
  Band    orange の帯が上下に2本（メインは teal が1本）
  Label   札も orange。点の数が予備の番号（1・2）
破れた版はない（事故の影響を受けない）。Slot_Valve（バルブ Ctrl_Valve の取り付け位置）はメインと同じ高さ。
設計画面用のアイコンは、大きさと背景が決まるまで renders/ の画像（_2_front.png）を使う。
"""
import sys, os
sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "core"))
sys.path.insert(0, HERE)
import corekit as ck
import build_tank as bt

KIND = "tank"
SPARES = [("Tank_OxygenSpare1", 1), ("Tank_OxygenSpare2", 2)]

if __name__ == "__main__":
    ck.clear_scene()
    roots = []
    for name, number in SPARES:
        root = bt.build(name, number, False, spare=True)
        ck.export_all(root, KIND)
        ck.render_check(root, KIND)
        print(f"[build] {name}: {ck.tri_count(root)} 三角")
        roots.append(root)
    # 見比べ用にメインの1本目を左に置く（書き出さない。レンダーだけ）
    main = ck.finalize(bt.build("Tank_Oxygen1", 1, False))
    bt.lineup([main] + roots, os.path.join(ck._export.MODELING, KIND, "renders", "tank_spare_lineup.png"),
              gap=0.85, margin=0.85)
