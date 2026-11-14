"""クルー4人（同じ形で、席の色とアイコンだけ違う）。

書いた人：iizuka（作成：Fable 5.2）／ 最終更新：2026-11-14

  modeling/core/run.sh modeling/crew/build_crew.py            4人とも
  modeling/core/run.sh modeling/crew/build_crew.py Crew_Pilot 1人だけ
出力：unity/Assets/Models/Crew/Crew_*.fbx、modeling/crew/export/、modeling/crew/renders/、modeling/crew/parts.json

参考：midori の crew_v5.py（部品ごとに別オブジェクト、原点は関節、という組み立て方）。形・比率・顔は作り直した。
身長 1.20m、ヘルメット直径 0.50m（2.4頭身）。正面は −Y。キャラの左手は +X 側。
部品（どれもルートの子。Eyes_* だけ Helmet の子）：
  Helmet      白い球（前の面を黒紺に塗り分けたバイザー）＋席の色のリング。原点は首 (0, 0, 0.72)
  Eyes_Normal / Eyes_Panic / Eyes_Relief   ふつう・焦る・ほっとする。Unity で1つだけ表示する（FBX には3つとも入る）
  Torso       席の色の胴＋胸の席アイコン（白）
  Backpack    明るい灰の箱＋背中の席アイコン（席の色）
  Arm_L / Arm_R   白い袖＋暗い灰の手袋。原点は肩。腕は前に出した姿で作ってある
  Leg_L / Leg_R   白い脚＋暗い灰の靴。原点は股
"""
import sys, os, math
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "core"))
import bpy
import corekit as ck

KIND = "crew"
# 名前: (席の色, アイコン, レンダーで見せる目)。席とアイコンは全員共通の決定（Opus-3 のコンソールと同じ）
CREW = {
    "Crew_Pilot":          ("seat_red",    "triangle", "Eyes_Normal"),
    "Crew_LifeSupport":    ("seat_green",  "circle",   "Eyes_Relief"),
    "Crew_CommsPower":     ("seat_yellow", "square",   "Eyes_Panic"),
    "Crew_MissionPlanner": ("seat_blue",   "diamond",  "Eyes_Normal"),
}

HEAD_R = 0.25
HEAD_Z = 1.20 - HEAD_R                 # ヘルメットの中心
NECK = (0, 0, 0.72)
SHOULDER_X, SHOULDER_Z = 0.245, 0.63
HIP_X, HIP_Z = 0.10, 0.34
ARM_DOWN = 5                           # 腕を水平から何度下げて前に出すか


def visor_y(x, z):
    """ヘルメットの表面（前側）の y。目を表面に貼るのに使う。"""
    return -math.sqrt(max(HEAD_R ** 2 - x ** 2 - (z - HEAD_Z) ** 2, 0))


def in_visor(c):
    """ヘルメットの面（中心 c、球の中心から見た座標）がバイザーの範囲か。前 ±60°・上下 ±30° の帯（四隅を落とすと階段状に見えたのでやめた）。"""
    lon = math.degrees(math.atan2(c.x, -c.y))
    lat = math.degrees(math.asin(max(-1, min(1, c.z / c.length))))
    return abs(lon) < 60 and abs(lat) < 30


def keep_faces(obj, pred):
    """pred(面の中心) が真の面だけ残す。"""
    import bmesh
    bm = bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if not pred(f.calc_center_median())], context="FACES")
    bm.to_mesh(obj.data); bm.free()
    return obj


def icon(name, shape, center, color, size=0.075, depth=0.03, facing=-1):
    """席アイコンの板。facing=-1 で正面（−Y）向き、+1 で背中（+Y）向き。▲ は上向き。"""
    rot = (90, 0, 0)
    if shape == "triangle":
        return ck.cyl(name, center, size * 1.15, depth, color, rot=rot, verts=3, bevel=0)
    if shape == "circle":
        return ck.cyl(name, center, size, depth, color, rot=rot, verts=16, bevel=0)
    if shape == "square":
        return ck.box(name, center, (size * 1.7, depth, size * 1.7), color, bevel=0)
    if shape == "diamond":
        return ck.cyl(name, center, size * 1.15, depth, color, rot=rot, verts=4, bevel=0)
    raise ValueError(shape)


def build(name):
    seat, shape, show_eyes = CREW[name]
    ck.clear_scene()
    root = ck.asset(name)

    # --- ヘルメット ---
    shell = ck.sph("HelmetShell", (0, 0, HEAD_Z), HEAD_R, "white", seg=ck.SEG_HELMET)
    visor = ck.sph("HelmetVisor", (0, 0, HEAD_Z), HEAD_R, "visor", seg=ck.SEG_HELMET)
    keep_faces(shell, lambda c: not in_visor(c))       # バイザーは球の面を塗り分ける（出っぱらせない）
    keep_faces(visor, in_visor)
    ring = ck.tor("HelmetRing", (0, 0, 0.735), 0.15, 0.04, seat, seg=(16, 6))
    helmet = ck.join([shell, visor, ring], "Helmet")
    ck.set_origin(helmet, NECK)

    # --- 目（3種類。バイザーの表面に貼る。頭からは飛び出さない） ---
    ex, ez = 0.085, HEAD_Z - 0.005
    ey = visor_y(ex, ez) + 0.004       # 少し埋めて、出っぱりを 1cm ほどにする
    n = [ck.sph(f"EyeN{i}", (s * ex, ey, ez), (0.03, 0.014, 0.046), "white", seg=8) for i, s in enumerate((-1, 1))]
    eyes_n = ck.join(n, "Eyes_Normal")
    p = [ck.sph(f"EyeP{i}", (s * ex, ey, ez), (0.05, 0.014, 0.05), "white", seg=10) for i, s in enumerate((-1, 1))]
    p += [ck.sph(f"PupilP{i}", (s * ex, ey - 0.010, ez), (0.018, 0.008, 0.018), "visor", seg=6) for i, s in enumerate((-1, 1))]
    eyes_p = ck.join(p, "Eyes_Panic")
    r = []
    for i, s in enumerate((-1, 1)):            # にっこり閉じた目（∩ の弧）
        pts = []
        for k in range(5):
            a = math.radians(160 - 35 * k)     # 160° → 20°
            x, z = s * ex + 0.042 * math.cos(a), ez - 0.02 + 0.042 * math.sin(a)
            pts.append((x, visor_y(x, z) + 0.002, z))
        r.append(ck.tube(f"EyeR{i}", pts, 0.012, "white"))
    eyes_r = ck.join(r, "Eyes_Relief")
    for e in (eyes_n, eyes_p, eyes_r):
        ck.attach(e, helmet)

    # --- 胴＋胸のアイコン ---
    body = ck.box("TorsoBody", (0, 0, 0.52), (0.38, 0.28, 0.40), seat, bevel=0.07)
    chest = icon("TorsoIcon", shape, (0, -0.14, 0.50), "white")
    torso = ck.join([body, chest], "Torso")
    ck.set_origin(torso, (0, 0, HIP_Z))

    # --- 背中の装置＋背中のアイコン ---
    pack = ck.box("PackBody", (0, 0.20, 0.54), (0.30, 0.16, 0.34), "gray_light", bevel=0.03)
    back = icon("PackIcon", shape, (0, 0.28, 0.54), seat, facing=1)
    backpack = ck.join([pack, back], "Backpack")

    # --- 腕（前に出す）。原点は肩 ---
    a = math.radians(ARM_DOWN)
    d = (0, -math.cos(a), -math.sin(a))        # 腕の向き
    for side, s in (("R", -1), ("L", 1)):
        sx = s * SHOULDER_X
        at = lambda t: (sx, d[1] * t, SHOULDER_Z + d[2] * t)
        sleeve = ck.cyl(f"Sleeve{side}", at(0.10), 0.068, 0.30, "white", rot=(90 + ARM_DOWN, 0, 0), verts=12, bevel=0.03)
        glove = ck.sph(f"Glove{side}", at(0.28), 0.078, "gray_dark", seg=12)
        arm = ck.join([sleeve, glove], f"Arm_{side}")
        ck.set_origin(arm, (sx, 0, SHOULDER_Z))

    # --- 脚。原点は股 ---
    for side, s in (("R", -1), ("L", 1)):
        hx = s * HIP_X
        leg = ck.cyl(f"Thigh{side}", (hx, 0, 0.22), 0.078, 0.30, "white", verts=12, bevel=0.02)
        boot = ck.box(f"Boot{side}", (hx, -0.025, 0.055), (0.17, 0.25, 0.11), "gray_dark", bevel=0.03)
        lg = ck.join([leg, boot], f"Leg_{side}")
        ck.set_origin(lg, (hx, 0, HIP_Z))
    ck.end_asset()

    # 可動部の表。腕と脚は X まわり（＋で下・後ろへ。腕を 45° より上げると手袋がヘルメットにめり込む）、頭は Z まわり（左右を見る）
    parts = [
        {"name": "Helmet", "type": "rotate", "axis": "Z", "min": -40, "max": 40, "default": 0},
        {"name": "Arm_L", "type": "rotate", "axis": "X", "min": -45, "max": 30, "default": 0},
        {"name": "Arm_R", "type": "rotate", "axis": "X", "min": -45, "max": 30, "default": 0},
        {"name": "Leg_L", "type": "rotate", "axis": "X", "min": -30, "max": 30, "default": 0},
        {"name": "Leg_R", "type": "rotate", "axis": "X", "min": -30, "max": 30, "default": 0},
    ]
    ck.write_parts(KIND, root, parts)
    ck.export_all(root, KIND)

    # レンダーでは目を1種類だけ見せる（FBX には3つとも入っている）
    for e in (eyes_n, eyes_p, eyes_r):
        e.hide_render = not e.name.endswith(show_eyes)
    # 3枚目は、右腕を上げ・左腕を下げ、脚を前後に、頭を回した姿
    pose = [dict(q) for q in parts]
    pose[2]["max"] = -45; pose[4]["max"] = -30
    ck.render_check(root, KIND, pose)
    print(f"[build] {name}: {ck.tri_count(root)} 三角")


args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
for nm in (args or CREW):
    build(nm)
