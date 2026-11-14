"""Fx_Co2Filter_New・Fx_Co2Filter_Used・Fx_Co2FilterHolder：CO2 フィルターの缶と差し込み口（#49）。

書いた人：Taiga（作成：Fable 5.2）／ 最終更新：2026-11-14

  modeling/core/run.sh modeling/fx/build_co2_filter.py
出力：unity/Assets/Models/Fx/Fx_Co2Filter*.fbx、modeling/fx/export/、modeling/fx/renders/、modeling/fx/parts.json
名前の頭は、今のルールで作れる Fx_ にした（Part_ は #48 で足す案。決まったら移してよい）。
下敷き：アポロ司令船の水酸化リチウムの缶は四角い箱（アポロ13号の「四角い缶を丸い穴に合わせる」話）。形だけ借りた。メッシュは新規。

缶（0.2×0.2×0.15m、原点＝底の中心、正面 −Y）：
  Body    箱。新品 white、使用済み gray
  Grille  正面の網の板。新品 teal、使用済み gray_dark（色が見分けにくくても、使用済みは Port がふさがっている）
  Port    丸い口。新品は visor の穴、使用済みは gray_dark の出っ張ったふた
  Handle  上の取っ手。新品 orange、使用済み gray_dark
ホルダー Fx_Co2FilterHolder（幅 0.82m、原点＝底の中心）：
  Base         台の板。gray_dark
  Socket       差し込み口の枠（左。缶が前から入る）。gray_light
  Duct         枠の上の丸い管（空気の出口）。teal
  Move_Canister 差し込んである缶。前（−Y）へ 0.2m 抜ける（translate、Blender の Y 軸、−0.2〜0）。原点＝缶の底の中心
  Tray         予備の置き場（右）。敷き板・奥の壁・仕切り。gray
  Slot_1・Slot_2 予備の缶（Fx_Co2Filter_New など）を置く位置。缶の原点（底の中心）を合わせる
交換の見せ方（Unity）：Move_Canister を抜く → 使用済みの見た目に替える／Slot の缶を1つ消す → 戻す。
"""
import sys, os
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "core"))
import corekit as ck

KIND = "fx"
W, D, H = 0.20, 0.20, 0.15       # 缶
PULL = 0.20                      # 抜く距離
BASE_H = 0.04
SOCKET_X = -0.27
PAD = 0.02                       # 予備の置き場の敷き板の厚み
SLOTS = [(0.03, 0, BASE_H + PAD), (0.27, 0, BASE_H + PAD)]


def can(prefix, x, z, used):
    """缶の部品を作って返す（prefix を名前の頭に付ける。x・z は底の中心）。"""
    body = ck.box(prefix + "Body", (x, 0, z + H / 2), (W, D, H), "gray" if used else "white", bevel=0.02)
    grille = ck.box(prefix + "Grille", (x, -D / 2 - 0.005, z + H / 2), (W - 0.05, 0.03, H - 0.05),
                    "gray_dark" if used else "teal", bevel=0.0075)
    if used:
        port = ck.cyl(prefix + "Port", (x, -D / 2 - 0.03, z + H / 2), 0.04, 0.03, "gray", rot=(90, 0, 0), bevel=0.0075)
    else:
        port = ck.cyl(prefix + "Port", (x, -D / 2 - 0.012, z + H / 2), 0.035, 0.022, "visor", rot=(90, 0, 0), bevel=0)
    handle = ck.box(prefix + "Handle", (x, 0, z + H + 0.012), (0.10, 0.04, 0.03), "gray_dark" if used else "orange",
                    bevel=0.0075)
    return [body, grille, port, handle]


def build_can(name, used):
    root = ck.asset(name)
    can("", 0, 0, used)
    ck.end_asset()
    return root


def build_holder():
    root = ck.asset("Fx_Co2FilterHolder")
    ck.box("Base", (0, 0, BASE_H / 2), (0.82, 0.30, BASE_H), "gray_dark", bevel=0.01)
    # 差し込み口：左右の壁・奥の壁・天井を1つに（前が開いている）
    t, sh = 0.03, H + 0.02
    sx = SOCKET_X
    walls = [ck.box("SockL", (sx - W / 2 - t / 2 - 0.004, 0.01, BASE_H + sh / 2), (t, D + 0.04, sh), "gray_light", bevel=0.0075),
             ck.box("SockR", (sx + W / 2 + t / 2 + 0.004, 0.01, BASE_H + sh / 2), (t, D + 0.04, sh), "gray_light", bevel=0.0075),
             ck.box("SockB", (sx, D / 2 + t / 2 + 0.004, BASE_H + sh / 2), (W + 2 * t + 0.008, t, sh), "gray_light", bevel=0.0075),
             ck.box("SockT", (sx, 0.01, BASE_H + sh + t / 2), (W + 2 * t + 0.008, D + 0.04, t), "gray_light", bevel=0.0075)]
    ck.join(walls, "Socket")
    ck.cyl("Duct", (sx, 0.03, BASE_H + sh + t + 0.04), 0.06, 0.08, "teal", bevel=0.012)
    mv = ck.join(can("Can", sx, BASE_H, False), "Move_Canister")
    ck.set_origin(mv, (sx, 0, BASE_H))
    # 予備の置き場：2個ぶんの敷き板と、奥の壁、まん中の仕切り（三角数を抑えるため縁は奥だけ）
    cx = (SLOTS[0][0] + SLOTS[1][0]) / 2
    tw, td, rh, rt = 0.50, 0.26, 0.07, 0.025
    ck.join([ck.box("TrPad", (cx, 0, BASE_H + PAD / 2), (tw, td, PAD), "gray", bevel=0.005),
             ck.box("TrB", (cx, td / 2 - rt / 2, BASE_H + rh / 2), (tw, rt, rh), "gray", bevel=0.006),
             ck.box("TrM", (cx, 0, BASE_H + rh / 2), (rt, td, rh), "gray", bevel=0.006)], "Tray")
    for i, p in enumerate(SLOTS):
        ck.slot(f"Slot_{i + 1}", p)
    ck.end_asset()
    return root


if __name__ == "__main__":
    ck.clear_scene()
    for name, used in (("Fx_Co2Filter_New", False), ("Fx_Co2Filter_Used", True)):
        root = build_can(name, used)
        ck.export_all(root, KIND)
        ck.render_check(root, KIND)
        print(f"[build] {name}: {ck.tri_count(root)} 三角")
    root = build_holder()
    parts = ck.write_parts(KIND, root, [{"name": "Move_Canister", "type": "translate", "axis": "Y",
                                         "min": -PULL, "max": 0, "default": 0}])
    ck.export_all(root, KIND)
    # 3枚目は最大値（0＝差し込んだ姿）になるので、抜いた姿が見えるよう min 側を最大として渡す
    shown = [dict(parts[0], min=0, max=-PULL)]
    ck.render_check(root, KIND, shown)
    print(f"[build] {root.name}: {ck.tri_count(root)} 三角")
