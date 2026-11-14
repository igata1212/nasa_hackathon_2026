"""操作部品 B（6種）：バルブ、点火ボタン（カバー付き）、汎用押しボタン、姿勢スティック、ルート切替レバー、送信ボタン。

書いた人：iizuka（作成：Opus-2）／ 最終更新：2026-11-14

  modeling/core/run.sh modeling/controls/build_controls_b.py
出力：unity/Assets/Models/Controls/Ctrl_<名前>.fbx、modeling/controls/export/Ctrl_<名前>.{glb,stats.json}、
      modeling/controls/renders/Ctrl_<名前>_*.png、modeling/controls/renders/controls_b_lineup{,_320}.png、
      modeling/controls/parts_b.json（可動部の表。押し込み量もここ）

下敷きなし（計画 A 章の1：新規、kit の box/cyl/tor）。実物の約3倍に誇張。台座はどれも 0.2m 角（ck.ctrl_base）。
種類の違いは形で付ける：
  Ctrl_Valve          横向きの輪（直径 0.30m、スポーク3本、つまみ1つ）。高い軸の上に浮く
  Ctrl_Ignite         丸。丸い跳ね上げカバー（後ろにヒンジ、前につまみ）の下に丸いボタン
  Ctrl_Button         丸い低いボタン（カバーなし）。correction_burn・swap_filter などに使い回す
  Ctrl_AttitudeStick  背の高いスティック。根元に球、上に太い握り
  Ctrl_RouteLever     左右に倒れる平たいレバーと溝。左 A に点1つ、右 B に点2つ（文字は付けない）
  Ctrl_Transmit       四角。四角い台に四角いボタン、隅に短いアンテナ
席の色・ランプ色は使わない（汎用部品）。

parts_b.json について：ck.write_parts は modeling/controls/parts.json に書く（操作部品 A と同じファイル）ので、
同じ形式で parts_b.json に書く関数をここに置いた（write_parts_b）。check.py は parts.json しか読まない（報告済み）。
"""
import sys, os, json, math
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "core"))
import bpy
import corekit as ck
import render_check as rc

KIND = "controls"
HERE = os.path.dirname(os.path.abspath(__file__))
BASE_TOP = 0.04          # ck.ctrl_base の高さ


def write_parts_b(root, parts):
    """ck.write_parts と同じ中身を modeling/controls/parts_b.json に書く（ほかのルートの分は残す）。"""
    bpy.context.view_layer.update()
    path = os.path.join(ck._export.MODELING, KIND, "parts_b.json")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    data = {}
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            data = json.load(f)
    out = []
    for p in parts:
        full = p["name"] if p["name"].startswith(root.name + ".") else f"{root.name}.{p['name']}"
        o = bpy.data.objects.get(full)
        assert o is not None and o in root.children_recursive, f"parts_b.json の部品がない: {full}"
        assert p["type"] in ("rotate", "translate") and p["axis"] in "XYZ", p
        ua, us = (ck.UNITY_ROT if p["type"] == "rotate" else ck.UNITY_AXIS)[p["axis"]]
        q = {k: v for k, v in p.items() if k != "render"}
        q["name"] = full
        q["origin"] = [round(v, 5) for v in o.matrix_world.translation]
        q["unity_axis"] = ua; q["unity_sign"] = us
        q["unit"] = "deg" if p["type"] == "rotate" else "m"
        out.append(q)
    data[root.name] = out
    with open(path, "w", encoding="utf-8") as f:
        json.dump(dict(sorted(data.items())), f, ensure_ascii=False, indent=1); f.write("\n")
    return out


def render_parts(parts):
    """レンダーの「動かした姿」用。render があればそれ（例：カバーを開く・ボタンを押し込む向き）を max にする。"""
    out = []
    for p in parts:
        q = dict(p)
        if "render" in p:
            q["default"], q["max"] = 0, p["render"]
        out.append(q)
    return out


def finish(root, parts):
    ck.end_asset()
    write_parts_b(root, parts)
    ck.export_all(root, KIND)
    ck.render_check(root, KIND, render_parts(parts))
    print(f"[build] {root.name}: {ck.tri_count(root)} 三角")
    return root


# ---------- 1. バルブ（close_valve）----------
def valve():
    root = ck.asset("Ctrl_Valve")
    ck.ctrl_base(0.2, 0.2)
    ck.cyl("Bonnet", (0, 0, 0.095), 0.045, 0.11, "gray", bevel=0.015)            # 軸受け z 0.04〜0.15
    hz = 0.17                                                                     # 輪の高さ（回転軸の上の点）
    hub = ck.cyl("WheelHub", (0, 0, hz), 0.035, 0.045, "gray_light", bevel=0.01)
    rim = ck.tor("WheelRim", (0, 0, hz), 0.13, 0.02, "teal")                      # 外径 0.30m
    spokes = []
    for i, a in enumerate((90, 210, 330)):
        r = 0.0725
        c = (r * math.cos(math.radians(a)), r * math.sin(math.radians(a)), hz)
        spokes.append(ck.box(f"WheelSpoke{i}", c, (0.09, 0.025, 0.025), "teal", rot=(0, 0, a), bevel=0.006))
    knob = ck.cyl("WheelKnob", (0.13 * 0.866, -0.13 * 0.5, hz + 0.035), 0.019, 0.07, "gray_light", bevel=0.008)
    wheel = ck.join([hub, rim] + spokes + [knob], "Move_Wheel")
    ck.set_origin(wheel, (0, 0, hz))
    parts = [{"name": "Move_Wheel", "type": "rotate", "axis": "Z", "min": -720, "max": 0, "default": 0,
              "render": -60,
              "note": "close_valve：上から見て時計回り（Blender の Z まわり −）に回すと閉まる。2周で全閉。"
                      "輪は直径 0.30m で台座（0.2m）より左右前後に 5cm はみ出す（高さ 0.15〜0.19m）"}]
    return finish(root, parts)


# ---------- 2. 点火ボタン（ignite）----------
def ignite():
    root = ck.asset("Ctrl_Ignite")
    ck.ctrl_base(0.2, 0.2)
    ck.cyl("Collar", (0, 0, BASE_TOP + 0.015), 0.08, 0.03, "navy", bevel=0.01)     # z 0.04〜0.07
    btn = ck.cyl("Move_Button", (0, 0, 0.0875), 0.055, 0.035, "orange", bevel=0.01)  # z 0.07〜0.105
    ck.set_origin(btn, (0, 0, 0.07))
    # ヒンジの受け（左右2つ）。カバーの軸受けの棒をはさむ
    hy, hzz = 0.085, 0.1225                                                        # ヒンジの軸（X に平行）
    for side, x in (("L", -0.045), ("R", 0.045)):
        ck.box(f"Hinge{side}", (x, hy, (BASE_TOP + hzz + 0.015) / 2), (0.025, 0.03, hzz + 0.015 - BASE_TOP), "gray",
               bevel=0.006)
    lid = ck.cyl("CoverLid", (0, 0, hzz), 0.075, 0.025, "orange", bevel=0.008)     # z 0.11〜0.135
    tab = ck.box("CoverTab", (0, -0.082, hzz), (0.045, 0.03, 0.022), "orange", bevel=0.006)
    barrel = ck.cyl("CoverBarrel", (0, hy, hzz), 0.015, 0.06, "gray_light", rot=(0, 90, 0), bevel=0.005)
    cover = ck.join([lid, tab, barrel], "Move_Cover")
    ck.set_origin(cover, (0, hy, hzz))
    parts = [{"name": "Move_Cover", "type": "rotate", "axis": "X", "min": -105, "max": 0, "default": 0,
              "render": -105,
              "note": "跳ね上げカバー。原点はヒンジの軸。0 で閉、−105 で全開（前のつまみが上・後ろへ回る）"},
             {"name": "Move_Button", "type": "translate", "axis": "Z", "min": -0.02, "max": 0, "default": 0,
              "press": 0.02, "render": -0.02,
              "note": "押し込み量 0.02m（−Z）。カバーを開いてから押す。Pilot と Comms & Power の2台を同時押し"}]
    return finish(root, parts)


# ---------- 3. 汎用押しボタン（correction_burn、swap_filter など）----------
def button():
    root = ck.asset("Ctrl_Button")
    ck.ctrl_base(0.2, 0.2)
    ck.cyl("Collar", (0, 0, BASE_TOP + 0.0125), 0.075, 0.025, "gray", bevel=0.008)   # z 0.04〜0.065
    cap = ck.cyl("Move_Button", (0, 0, 0.085), 0.06, 0.04, "cream", r2=0.054, bevel=0.012)  # z 0.065〜0.105
    ck.set_origin(cap, (0, 0, 0.065))
    parts = [{"name": "Move_Button", "type": "translate", "axis": "Z", "min": -0.02, "max": 0, "default": 0,
              "press": 0.02, "render": -0.02, "note": "押し込み量 0.02m（−Z）"}]
    return finish(root, parts)


# ---------- 4. 姿勢スティック（set_attitude、2軸）----------
def stick():
    root = ck.asset("Ctrl_AttitudeStick")
    ck.ctrl_base(0.2, 0.2)
    ck.cyl("Boot", (0, 0, BASE_TOP + 0.0225), 0.07, 0.045, "gray", r2=0.045, bevel=0.01)   # z 0.04〜0.085（円錐台）
    bz = 0.085                                                                        # 根元の球の中心
    ball = ck.sph("StickBall", (0, 0, bz), 0.04, "gray_light")
    shaft = ck.cyl("StickShaft", (0, 0, (bz + 0.215) / 2), 0.014, 0.215 - bz, "gray", bevel=0.004)
    grip = ck.cyl("StickGrip", (0, 0, 0.26), 0.036, 0.10, "white", bevel=0.016)      # z 0.21〜0.31
    thumb = ck.cyl("StickThumb", (0, 0, 0.3155), 0.015, 0.024, "gray", bevel=0.004)
    st = ck.join([ball, shaft, grip, thumb], "Move_Stick")
    ck.set_origin(st, (0, 0, bz))
    parts = [{"name": "Move_Stick", "type": "rotate", "axis": "X", "min": -25, "max": 25, "default": 0,
              "note": "前後に倒す（+ で握りが −Y＝手前へ）。原点は根元の球の中心"},
             {"name": "Move_Stick", "type": "rotate", "axis": "Y", "min": -25, "max": 25, "default": 0,
              "note": "左右に倒す（+ で握りが +X＝右へ）。同じ部品の2つ目の軸"}]
    return finish(root, parts)


# ---------- 5. ルート切替レバー（set_route a / b）----------
def route_lever():
    root = ck.asset("Ctrl_RouteLever")
    ck.ctrl_base(0.2, 0.2)
    rails = [ck.box(f"Rail{s}", (0, y, BASE_TOP + 0.025), (0.17, 0.025, 0.05), "gray", bevel=0.006)
             for s, y in (("F", -0.03), ("B", 0.03))]                                 # 溝の幅 0.035m
    ends = [ck.box(f"RailEnd{s}", (x, 0, BASE_TOP + 0.025), (0.025, 0.035, 0.05), "gray", bevel=0.006)   # 溝の両端をふさぐ（レールと面が重ならない幅）
            for s, x in (("L", -0.0725), ("R", 0.0725))]
    ck.join(rails + ends, "Guide")
    # 位置の印：左 A に点1つ、右 B に点2つ（文字は付けない）
    dots = [ck.cyl(f"Dot{i}", (x, -0.072, BASE_TOP + 0.0125), 0.013, 0.025, "white", bevel=0.004)
            for i, x in enumerate((-0.06, 0.04, 0.072))]
    ck.join(dots, "Marks")
    pz = 0.055                                                                         # 回転軸（Y に平行）
    shaft = ck.box("LeverShaft", (0, 0, (0.045 + 0.20) / 2), (0.025, 0.025, 0.20 - 0.045), "gray_light",
                   bevel=0.005)
    paddle = ck.box("LeverPaddle", (0, 0, 0.225), (0.10, 0.035, 0.055), "teal", bevel=0.01)   # z 0.1975〜0.2525
    lv = ck.join([shaft, paddle], "Move_Lever")
    ck.set_origin(lv, (0, 0, pz))
    parts = [{"name": "Move_Lever", "type": "rotate", "axis": "Y", "min": -30, "max": 30, "default": -30,
              "render": 30,
              "note": "2位置。−30＝A（左、点1つ）、+30＝B（右、点2つ）。初期は A。形は真上（0）で作ってある"}]
    return finish(root, parts)


# ---------- 6. 送信ボタン（set_transmit）----------
def transmit():
    root = ck.asset("Ctrl_Transmit")
    ck.ctrl_base(0.2, 0.2)
    ck.box("Plinth", (-0.01, -0.01, BASE_TOP + 0.0125), (0.15, 0.15, 0.025), "teal", bevel=0.008)   # z 0.04〜0.065
    cap = ck.box("Move_Button", (-0.01, -0.01, 0.085), (0.11, 0.11, 0.04), "white", bevel=0.012)  # z 0.065〜0.105
    ck.set_origin(cap, (-0.01, -0.01, 0.065))
    foot = ck.cyl("AntennaFoot", (0.077, 0.077, BASE_TOP + 0.0125), 0.02, 0.025, "gray", bevel=0.006)
    rod = ck.cyl("AntennaRod", (0.077, 0.077, 0.115), 0.011, 0.10, "gray_light", bevel=0.003)
    tip = ck.sph("AntennaTip", (0.077, 0.077, 0.17), 0.022, "gray_light")
    ck.join([foot, rod, tip], "Antenna")
    parts = [{"name": "Move_Button", "type": "translate", "axis": "Z", "min": -0.02, "max": 0, "default": 0,
              "press": 0.02, "render": -0.02,
              "note": "押し込み量 0.02m（−Z）。set_transmit(on) は ON/OFF なので、ON の間は −0.02 で止める（押しっぱなし式）想定"}]
    return finish(root, parts)


# ---------- 一覧 ----------
def lineup(roots, path, spacing=0.36):
    """全部品を X に並べ、ゲームと同じ角度（縦の画角 30°、見下ろし 50°、正面から）で写す。320px 版も出す。"""
    scn = bpy.context.scene
    keep = [(r, r.location.copy()) for r in roots]
    n = len(roots)
    for i, r in enumerate(roots):
        r.location = ((i - (n - 1) / 2) * spacing, 0, 0)
    bpy.context.view_layer.update()
    mine = {o for r in roots for o in ck.tree(r)}
    hidden = [(o, o.hide_render) for o in bpy.data.objects if o not in mine]
    for o, _ in hidden:
        o.hide_render = True
    lo, hi = ck._export.world_bounds([m for r in roots for m in ck.meshes(r)])
    old_world, old_cam = scn.world, scn.camera
    made, cam, _ = rc._helpers(lo, hi, True, False)
    world = rc._world()
    scn.render.engine = "BLENDER_EEVEE"
    scn.render.resolution_x, scn.render.resolution_y = rc.RES
    scn.render.resolution_percentage = 100
    scn.render.film_transparent = False
    scn.render.image_settings.file_format = "PNG"
    scn.view_settings.view_transform = "Standard"; scn.view_settings.look = "None"
    rc._aim(cam, rc._corners(lo, hi), 0, 50, margin=0.68)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    rc._render(path)
    small = rc._small(path)
    for o in made:
        data = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if data and data.users == 0:
            getattr(bpy.data, {"MESH": "meshes", "LIGHT": "lights", "CAMERA": "cameras"}[data.id_type]).remove(data)
    bpy.data.worlds.remove(world)
    scn.world, scn.camera = old_world, old_cam
    for o, h in hidden:
        o.hide_render = h
    for r, loc in keep:
        r.location = loc
    bpy.context.view_layer.update()
    print("[lineup]", os.path.relpath(path, ck.REPO), os.path.relpath(small, ck.REPO))


ck.clear_scene()
# レンダーの PNG を小さくする（1枚 約560KB → 下）。ディザのノイズを切り、RGB・最大圧縮で保存。見た目はほぼ同じ
_scn = bpy.context.scene
_scn.render.dither_intensity = 0
_scn.render.image_settings.color_mode = "RGB"
_scn.render.image_settings.compression = 100
roots = [valve(), ignite(), button(), stick(), route_lever(), transmit()]
lineup(roots, os.path.join(ck._export.MODELING, KIND, "renders", "controls_b_lineup.png"))
for r in roots:
    print(f"[build] {r.name}: {ck.tri_count(r)} 三角")
