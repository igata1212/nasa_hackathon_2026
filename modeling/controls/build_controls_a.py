"""操作部品 A（8種）：トグルスイッチ4種、観測機器のスイッチ3種、かき混ぜスイッチ。

書いた人：iizuka（作成：Opus-1）／ 最終更新：2026-11-14

  modeling/core/run.sh modeling/controls/build_controls_a.py            作る → 書き出し → レンダー → 一覧
  modeling/core/run.sh modeling/controls/build_controls_a.py -- --trial    tmp/modeling_trial/ にだけ出す
  オプション：--no-render（レンダーを飛ばす）、--only Ctrl_Stir（1つだけ）
出力：unity/Assets/Models/Controls/<ルート名>.fbx、modeling/controls/export/<ルート名>.glb・.stats.json、
      modeling/controls/renders/<ルート名>_*.png、modeling/controls/renders/controls_a_lineup(.png/_320.png)、
      modeling/controls/parts_a.json（この8種の可動部の表）。check.py が読む parts.json にも同じ行を足す
      （ck.write_parts はルート名ごとに上書きするので、操作部品 B の行は消えない）。

既存データ：計画（modeling-plan.md A 章 1）のとおり下敷きなし。corekit の箱・円柱・球・パイプと、
扇・三角・矢印の板のための prism()（このファイル内）だけで作った。

見分け方（色でなく形。320px で効くのはシルエット）
  トグル（Comms & Power、レバーは X 軸まわりに前後に倒れる。奥が ON）
    Heater  レバーの先が球、台座の前に「波線3本」（熱）
    Lights  レバーの先が平たい円盤（きのこ形）、台座の前に「太陽」（丸と6本の光）
    Nav     レバーの先が円錐（とがる）、台座の前に「矢印」
    Comms   レバーの先が立方体、台座の前に「電波」（点と弧2本）
  観測機器（Mission Planner、つまみが Z 軸まわりに 90° 回る。左前＝OFF、右前＝ON）
    Camera       つまみがカメラの形（箱＋前を向くレンズ）
    Spectrometer つまみが三角柱（プリズム）
    Radar        つまみがパラボラ（傾いた皿＋受信棒）
  かき混ぜ（Life Support、M04）
    Stir         扇の形のつまみ（中心に3枚羽のファン）。Z 軸まわりに 50° ひねる。手を離すと戻る
"""
import sys, os, json, math
sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "core"))
import bpy, bmesh                       # noqa: E402
import corekit as ck                    # noqa: E402
import render_check as rc               # noqa: E402
import export as ex                     # noqa: E402

KIND = "controls"
ARGV = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
TRIAL = "--trial" in ARGV
RENDER = "--no-render" not in ARGV
ONLY = ARGV[ARGV.index("--only") + 1] if "--only" in ARGV else None
if TRIAL:
    ck.trial()

BASE_TOP = 0.04          # ck.ctrl_base の高さ
RELIEF = 0.025           # 浮き彫りの高さ（太さ 2cm 以上を守る）


# ---------- 形の道具 ----------
def prism(name, pts, z0, h, color, bevel=None):
    """XY 平面の凸多角形 pts を z0 から高さ h だけ押し出した板（扇・三角・矢じり用）。"""
    me = bpy.data.meshes.new("tmp_prism")
    bm = bmesh.new()
    bot = [bm.verts.new((x, y, z0)) for x, y in pts]
    top = [bm.verts.new((x, y, z0 + h)) for x, y in pts]
    n = len(pts)
    bm.faces.new(bot)
    bm.faces.new(top)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((bot[i], bot[j], top[j], top[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new("tmp_prism", me)
    bpy.context.scene.collection.objects.link(o)
    xs, ys = [p[0] for p in pts], [p[1] for p in pts]
    thin = min(h, max(xs) - min(xs), max(ys) - min(ys))
    return ck._fin(o, color, name, bevel=ck._bevel(bevel, thin))


def dirv(a_deg, r=1.0, c=(0.0, 0.0)):
    """正面（−Y）を 0°、右（+X）へ回る向きを正とした方向ベクトル（Blender の Z まわり +θ と同じ向き）。"""
    a = math.radians(a_deg)
    return (c[0] + r * math.sin(a), c[1] - r * math.cos(a))


def sector(c, R, a0, a1, n=8):
    """扇（中心 c、半径 R、角度 a0〜a1）の頂点列。"""
    return [c] + [dirv(a0 + (a1 - a0) * i / n, R, c) for i in range(n + 1)]


def spin_mesh(o, deg):
    """o を自分の原点のまわりに Z 回転させて、回転をメッシュに焼く（原点と回転0はそのまま）。"""
    o.rotation_euler = (0, 0, math.radians(deg))
    ck._apply(o)
    bpy.context.view_layer.update()


# ---------- トグルスイッチ ----------
PIVOT_T = (0.0, 0.035, 0.09)     # レバーの付け根（回転軸は X に平行）
ICON_C = (0.0, -0.052)           # 台座の前の浮き彫りの中心
TILT = 25                        # ON（奥）＝ −25°、OFF（手前）＝ +25°


def icon_heater():
    ps = []
    for i, x0 in enumerate((-0.036, 0.0, 0.036)):
        pts = [(x0 + 0.009 * math.sin(k * math.pi / 2), -0.088 + k * 0.014, BASE_TOP + 0.012) for k in range(6)]
        ps.append(ck.tube(f"IconWave{i}", pts, 0.0125, "cream"))
    return ps


def icon_lights():
    cx, cy = ICON_C
    # 丸（少し高い）の下に3本の棒を 60° ずつ重ねて、光の線6本に見せる
    ps = [ck.cyl("IconSun", (cx, cy, BASE_TOP + (RELIEF + 0.006) / 2), 0.02, RELIEF + 0.006, "cream", bevel=0.005)]
    for i in range(3):
        ps.append(ck.box(f"IconRay{i}", (cx, cy, BASE_TOP + RELIEF / 2), (0.02, 0.086, RELIEF), "cream",
                         rot=(0, 0, i * 60), bevel=0.005))
    return ps


def icon_nav():
    head = prism("IconArrowHead", [(0, -0.012), (-0.04, -0.052), (0.04, -0.052)], BASE_TOP, RELIEF, "cream")
    shaft = ck.box("IconArrowShaft", (0, -0.0715, BASE_TOP + RELIEF / 2), (0.026, 0.041, RELIEF), "cream")
    return [head, shaft]


def icon_comms():
    c = (0.0, -0.086)
    ps = [ck.cyl("IconDot", (c[0], c[1], BASE_TOP + RELIEF / 2), 0.012, RELIEF, "cream")]
    for i, r in enumerate((0.032, 0.057)):
        pts = [(*dirv(180 + a, r, c), BASE_TOP + 0.0125) for a in range(-45, 46, 15)]
        ps.append(ck.tube(f"IconArc{i}", pts, 0.0125, "cream"))
    return ps


def tip_heater(z):
    return ck.sph("TipBall", (PIVOT_T[0], PIVOT_T[1], z + 0.028), 0.032, "orange")


def tip_lights(z):
    return ck.cyl("TipDisc", (PIVOT_T[0], PIVOT_T[1], z + 0.012), 0.04, 0.026, "orange", bevel=0.008)


def tip_nav(z):
    return ck.cyl("TipCone", (PIVOT_T[0], PIVOT_T[1], z + 0.028), 0.034, 0.06, "orange", r2=0.006, bevel=0.004)


def tip_comms(z):
    return ck.box("TipCube", (PIVOT_T[0], PIVOT_T[1], z + 0.024), (0.05, 0.05, 0.05), "orange", bevel=0.008)


def build_toggle(name, icon_fn, tip_fn):
    root = ck.asset(name)
    ck.ctrl_base(0.2, 0.2)
    px, py, pz = PIVOT_T
    ck.box("Housing", (px, py, BASE_TOP + 0.02), (0.11, 0.08, 0.04), "gray", bevel=0.01)
    ck.cyl("Nut", (px, py, 0.09), 0.033, 0.02, "gray_light", verts=6, bevel=0.004)
    icon = ck.join(icon_fn(), "Icon")
    ck.set_origin(icon)
    # レバー：軸＋先端（先端の形で種類が分かる）。軸の下端はナットの中に隠れる
    stem = ck.cyl("LeverStem", (px, py, pz + 0.045), 0.012, 0.09, "gray_light", bevel=0.003)
    tip = tip_fn(pz + 0.085)
    lever = ck.join([stem, tip], "Move_Lever")
    ck.set_origin(lever, PIVOT_T)
    ck.end_asset()
    parts = [{"name": "Move_Lever", "type": "rotate", "axis": "X", "min": -TILT, "max": TILT, "default": -TILT,
              "on": -TILT, "off": TILT,
              "note": "2位置。ON＝奥（−Y の反対）へ倒す −25°、OFF＝手前へ +25°。初期は ON（SPEC 3章 sw[...] = true）"}]
    return root, parts


# ---------- 観測機器のスイッチ（回転つまみ） ----------
PIVOT_K = (0.0, 0.0, 0.06)       # つまみの回転軸（Z）
OFF_DEG, ON_DEG = -45, 45        # 指す向き（正面＝0°、右へ正）。OFF＝左前、ON＝右前


def marks(c, off_deg, on_deg, R=0.1):
    """止まる位置の目印（動かない）。OFF・戻り位置＝小さい灰の角、ON＝中心を指すクリームの三角（高い）。"""
    off = ck.box("MarkOff", (*dirv(off_deg, R + 0.004, c), BASE_TOP + 0.011), (0.022, 0.022, 0.022), "gray_light",
                 rot=(0, 0, off_deg), bevel=0.004)
    tri = [dirv(on_deg, R - 0.012, c), dirv(on_deg + 8, R + 0.012, c), dirv(on_deg - 8, R + 0.012, c)]
    on = prism("MarkOn", tri, BASE_TOP, 0.03, "cream", bevel=0.004)
    m = ck.join([on, off], "Marks")
    ck.set_origin(m)


def knob_common():
    """台座の上の丸い座と、OFF・ON の目印。動かない。"""
    ck.cyl("Dial", (0, 0, BASE_TOP + 0.01), 0.07, 0.02, "gray", bevel=0.006)
    marks((0.0, 0.0), OFF_DEG, ON_DEG)


def hub():
    """つまみの軸（灰の円柱）と、指す向きの爪（正面 −Y 側）。"""
    h = ck.cyl("KnobHub", (0, 0, 0.0725), 0.05, 0.025, "gray_light", bevel=0.006)
    nub = ck.box("KnobNub", (0, -0.052, 0.0725), (0.022, 0.03, 0.022), "cream", bevel=0.005)
    return [h, nub]


def icon_camera():
    body = ck.box("CamBody", (0, 0.006, 0.1075), (0.078, 0.05, 0.046), "teal", bevel=0.008)
    lens = ck.cyl("CamLens", (0, -0.03, 0.106), 0.025, 0.032, "visor", rot=(90, 0, 0), bevel=0.005)
    view = ck.box("CamFinder", (0.02, 0.012, 0.137), (0.026, 0.024, 0.02), "teal", bevel=0.004)
    return [body, lens, view]


def icon_spectrometer():
    # 頂点が正面（指す向き）の三角柱。上に白い「入射口」を1つ
    tri = prism("Prism", [dirv(0, 0.06), dirv(120, 0.06), dirv(240, 0.06)], 0.085, 0.06, "teal", bevel=0.007)
    slit = ck.box("PrismSlit", (0, 0.006, 0.153), (0.036, 0.022, 0.02), "white", bevel=0.004)
    return [tri, slit]


def icon_radar():
    d = (0.0, -0.6, 0.8)             # 皿の向き（前上）
    loc = (0.0, 0.0, 0.1175)         # 皿の背中の一番低いところ（約 0.081）がつまみの軸（上面 0.085）に少し埋まる高さ
    dish = ck.dome("RadarDish", loc, (0.05, 0.05, 0.026), "teal")
    dish.rotation_euler = (math.radians(-143.13), 0, 0)
    ck._apply(dish)
    rod = ck.cyl("RadarFeed", tuple(l + 0.022 * v for l, v in zip(loc, d)), 0.01, 0.044, "gray_light",
                 rot=(36.87, 0, 0), bevel=0.002)
    tip = ck.sph("RadarTip", tuple(l + 0.047 * v for l, v in zip(loc, d)), 0.013, "cream")
    return [dish, rod, tip]


def build_instr(name, icon_fn):
    root = ck.asset(name)
    ck.ctrl_base(0.2, 0.2)
    knob_common()
    knob = ck.join(icon_fn() + hub(), "Move_Knob")
    ck.set_origin(knob, PIVOT_K)
    spin_mesh(knob, OFF_DEG)          # 作ったときは正面向き → OFF（左前）に向ける
    ck.end_asset()
    parts = [{"name": "Move_Knob", "type": "rotate", "axis": "Z", "min": 0, "max": ON_DEG - OFF_DEG, "default": 0,
              "on": ON_DEG - OFF_DEG, "off": 0,
              "note": "2位置。0＝OFF（左前の小さい灰の角を指す）、90＝ON（右前のクリームの三角を指す）。初期は OFF（SPEC 3章）"}]
    return root, parts


# ---------- かき混ぜスイッチ（扇のつまみ） ----------
PIVOT_S = (0.0, 0.025, 0.06)
STIR_DEG = 50


def build_stir(name):
    root = ck.asset(name)
    ck.ctrl_base(0.2, 0.2)
    c = PIVOT_S[:2]
    ck.cyl("Dial", (c[0], c[1], BASE_TOP + 0.01), 0.068, 0.02, "gray", bevel=0.006)
    marks(c, 0, STIR_DEG, R=0.104)
    # つまみ：軸＋扇（正面を指す）＋上に3枚羽のファン
    z0 = 0.065
    hub_ = ck.cyl("StirHub", (c[0], c[1], (0.06 + 0.105) / 2), 0.036, 0.045, "orange", bevel=0.008)
    fan = prism("StirFan", sector(c, 0.088, -36, 36), z0, 0.04, "orange", bevel=0.008)
    blades = []
    for i in range(3):
        a = i * 120 + 20
        x, y = dirv(a, 0.017, c)
        blades.append(ck.box(f"Blade{i}", (x, y, 0.115), (0.022, 0.034, 0.02), "cream", rot=(0, 0, a),
                             bevel=0.004))
    knob = ck.join([hub_, fan] + blades, "Move_Knob")
    ck.set_origin(knob, PIVOT_S)
    ck.end_asset()
    parts = [{"name": "Move_Knob", "type": "rotate", "axis": "Z", "min": 0, "max": STIR_DEG, "default": 0,
              "note": "ひねると右前のクリームの三角まで 50° 回る。手を離すと 0 に戻る（モーメンタリ）。M04 で Life Support が操作"}]
    return root, parts


# ---------- 組み立て ----------
ASSETS = [
    ("Ctrl_Toggle_Heater", lambda n: build_toggle(n, icon_heater, tip_heater)),
    ("Ctrl_Toggle_Lights", lambda n: build_toggle(n, icon_lights, tip_lights)),
    ("Ctrl_Toggle_Nav", lambda n: build_toggle(n, icon_nav, tip_nav)),
    ("Ctrl_Toggle_Comms", lambda n: build_toggle(n, icon_comms, tip_comms)),
    ("Ctrl_Instr_Camera", lambda n: build_instr(n, icon_camera)),
    ("Ctrl_Instr_Spectrometer", lambda n: build_instr(n, icon_spectrometer)),
    ("Ctrl_Instr_Radar", lambda n: build_instr(n, icon_radar)),
    ("Ctrl_Stir", build_stir),
]


def lineup(built, out_dir):
    """全部をゲームと同じ角度（画角 30°、見下ろし 50°、正面）で並べた1枚と 320px 版。奥の列がトグル。"""
    scn = bpy.context.scene
    gap = 0.3
    rows = [[n for n, _, _ in built if n.startswith("Ctrl_Toggle")],
            [n for n, _, _ in built if not n.startswith("Ctrl_Toggle")]]
    for r, names in enumerate(rows):
        for i, n in enumerate(names):
            bpy.data.objects[n].location = ((i - (len(names) - 1) / 2) * gap, 0.17 - r * 0.34, 0)
    bpy.context.view_layer.update()
    ms = [o for n, _, _ in built for o in ex.meshes(bpy.data.objects[n])]
    lo, hi = ex.world_bounds(ms)
    made, cam, _ = rc._helpers(lo, hi, True, False)
    world = rc._world()
    scn.render.engine = "BLENDER_EEVEE"
    scn.render.resolution_x, scn.render.resolution_y = rc.RES
    scn.render.resolution_percentage = 100
    scn.render.image_settings.file_format = "PNG"
    scn.view_settings.view_transform = "Standard"; scn.view_settings.look = "None"
    rc._aim(cam, rc._corners(lo, hi), 0, 50, margin=0.66)
    os.makedirs(out_dir, exist_ok=True)
    p = rc._render(os.path.join(out_dir, "controls_a_lineup.png"))
    rc._small(p)
    for o in made:
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.worlds.remove(world)
    for n, _, _ in built:
        bpy.data.objects[n].location = (0, 0, 0)
    print("[lineup]", os.path.relpath(p, ex.REPO))


def main():
    ck.clear_scene()
    # レンダーの PNG を小さくする（1枚 約560KB → 約70KB）。ディザのノイズを切り、RGB・最大圧縮で保存（操作部品 B と同じ設定）
    scn = bpy.context.scene
    scn.render.dither_intensity = 0
    scn.render.image_settings.color_mode = "RGB"
    scn.render.image_settings.compression = 100
    built = []
    for name, fn in ASSETS:
        if ONLY and name != ONLY:
            continue
        root, parts = fn(name)
        ck.export_all(root, KIND)
        built.append((name, root, parts))
        print(f"[build] {name}: {ck.tri_count(root)} 三角")
    if RENDER:
        for name, root, parts in built:
            ck.render_check(root, KIND, parts)
    # 可動部の表：parts.json（check.py が読む。ほかのルートの行は残る）と、この8種だけの parts_a.json
    mine = {}
    for name, root, parts in built:
        mine[name] = ck.write_parts(KIND, root, parts)
    pa = os.path.join(ex.MODELING, KIND, "parts_a.json")
    old = {}
    if os.path.exists(pa):
        with open(pa, encoding="utf-8") as f:
            old = json.load(f)
    old.update(mine)
    with open(pa, "w", encoding="utf-8") as f:
        json.dump(dict(sorted(old.items())), f, ensure_ascii=False, indent=1); f.write("\n")
    if RENDER and not ONLY:
        lineup(built, os.path.join(ex.MODELING, KIND, "renders"))


main()
