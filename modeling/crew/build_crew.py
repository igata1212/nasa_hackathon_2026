"""クルー4人（同じ形で、席の色とアイコンだけ違う）。

書いた人：iizuka（作成：Fable 5.2、#39 可愛く作り直し：Opus 5.5）／ 最終更新：2026-11-14

  modeling/core/run.sh modeling/crew/build_crew.py            4人とも
  modeling/core/run.sh modeling/crew/build_crew.py Crew_Pilot 1人だけ
出力：unity/Assets/Models/Crew/Crew_*.fbx、modeling/crew/export/、modeling/crew/renders/、modeling/crew/parts.json

形の元：iizuka のスケッチ（#39）。大きな丸いヘルメットに横長の角丸バイザー、体は1つのやわらかいかたまり
（丸い肩、両脇にだらんと下がる短い腕、短い脚2本。手袋・靴の段差なし）。約2頭身。
身長 1.20m、ヘルメット直径 0.58m。正面は −Y。キャラの左手は +X 側。
部品（どれもルートの子。Eyes_* だけ Helmet の子）：
  Helmet      白い球＋横長の角丸バイザー（黒紺、少し盛り上げた別の形）＋席の色の首のリング。原点は首 (0, 0, NECK_Z)
  Eyes_Normal / Eyes_Panic / Eyes_Relief   ふつう・焦る・ほっとする。バイザーのやや下に、間隔を広めに。
              Unity で1つだけ表示する（FBX には3つとも入る）
  Torso       席の色のやわらかい胴（角の丸い球＝超楕円体）＋胸の席アイコン（白）
  Backpack    席の色の角丸の箱＋背中の席アイコン（白）
  Arm_L / Arm_R   席の色の短く太いカプセル。付け根は胴に埋める。原点は肩。腕は下ろして少し外に開いた姿で作ってある
  Leg_L / Leg_R   席の色の短く太いカプセル（底は少し平ら）。付け根は胴に埋める。原点は股
似すぎの線引き（docs/design/art-research.md 4章）：ヘルメットと胴が別のかたまり（白と席の色、首のリング）、
目がある、背中の装置がある、腕がつながっている、で Among Us・Fall Guys・Overcooked と区別する。
"""
import sys, os, math
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "core"))
import bpy
import bmesh
from mathutils import Matrix, Vector
import corekit as ck

KIND = "crew"
# 名前: (席の色, アイコン, レンダーで見せる目)。席とアイコンは全員共通の決定（Opus-3 のコンソールと同じ）
CREW = {
    "Crew_Pilot":          ("seat_red",    "triangle", "Eyes_Normal"),
    "Crew_LifeSupport":    ("seat_green",  "circle",   "Eyes_Relief"),
    "Crew_CommsPower":     ("seat_yellow", "square",   "Eyes_Panic"),
    "Crew_MissionPlanner": ("seat_blue",   "diamond",  "Eyes_Normal"),
}

H = 1.20
HEAD_R = 0.29                          # ヘルメット直径 0.58m（身長の約半分＝約2頭身）
HEAD_Z = H - HEAD_R                    # ヘルメットの中心
NECK_Z = 0.645                         # 首（ヘルメットの原点・リングの高さ）
# 胴（超楕円体）：中心・半径・丸さ（2＝球、大きいほど角ばる）・すそ広がり
TORSO_C = (0, 0, 0.41)
TORSO_R = (0.23, 0.19, 0.27)
TORSO_N = 2.6
TORSO_FLARE = 0.08                     # 下 +8%、上 −8%
SHOULDER_X, SHOULDER_Z = 0.20, 0.53    # 腕の原点（肩）。胴の中（首のリングに当たらない高さ）
ARM_R, ARM_L, ARM_OUT = 0.075, 0.17, 34   # 腕の太さ（半径）・長さ（球の中心どうし）・外に開く角度（度）
HIP_X, HIP_Z = 0.10, 0.20              # 脚の原点（股）。胴の中
LEG_R, LEG_FLAT = 0.085, 0.7           # 脚の太さ（半径）、底の丸みのつぶし具合
# バイザー：少し上を向いた平面に描いた角丸長方形を、その向きのままヘルメットの球面に写す
# （緯度・経度で描くと、見下ろしたとき上の縁が中央で垂れて「覆面」に見えたため）
VIS_TILT = 22                          # 写す向き（正面から上へ何度。ゲームのカメラは見下ろし 50°）
VIS_W, VIS_H, VIS_N = 0.24, 0.13, 5    # 半幅・半高（m、写す前の平面で）・角の丸さ（大きいほど四角）
VIS_OUT, VIS_IN = 0.018, 0.015         # ヘルメットの表面から外へ／内へ
VIS_RF = HEAD_R + VIS_OUT              # バイザーの表の半径
EYE_U, EYE_W = 0.095, -0.03            # 目の位置（写す前の平面で。バイザーの中心より少し下、左右に広め）
_T = math.radians(VIS_TILT)
_D = Vector((0, -math.cos(_T), math.sin(_T)))       # 写す向き
_UP = Vector((0, math.sin(_T), math.cos(_T)))       # 平面の上
_C = Vector((0, 0, HEAD_Z))


def proj(u, w, r=VIS_RF):
    """平面の点 (横 u, 縦 w) を、半径 r の球面（ヘルメットと同じ中心）に写した点（世界座標）。"""
    p = Vector((u, 0, 0)) + _UP * w
    return _C + p + _D * math.sqrt(max(r * r - p.length_squared, 0))


def lonlat(pt):
    """球面の点の経度・緯度（度、正面 −Y が 0）。"""
    n = (pt - _C).normalized()
    return math.degrees(math.atan2(n.x, -n.y)), math.degrees(math.asin(max(-1, min(1, n.z))))


def squircle(t, n):
    """角の丸い長方形（超楕円）の外周の点（-1〜1）。"""
    c, s = math.cos(t), math.sin(t)
    return (math.copysign(abs(c) ** (2 / n), c), math.copysign(abs(s) ** (2 / n), s))


def mesh_obj(name, bm):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    return o


def visor(name, n_edge=32, rings=(1.0, 0.66, 0.33)):
    """横長の角丸長方形のバイザー。表は球面（半径 VIS_RF）に沿い、縁はヘルメットの中（HEAD_R − VIS_IN）まで下ろす。
    裏はヘルメットの中に隠れるので面を張らない。"""
    bm = bmesh.new()
    loops = []
    for k in rings:
        loop = []
        for i in range(n_edge):
            sx, sz = squircle(2 * math.pi * i / n_edge, VIS_N)
            loop.append(bm.verts.new(proj(k * VIS_W * sx, k * VIS_H * sz)))
        loops.append(loop)
    center = bm.verts.new(proj(0, 0))
    back = []
    for i in range(n_edge):
        sx, sz = squircle(2 * math.pi * i / n_edge, VIS_N)
        back.append(bm.verts.new(proj(VIS_W * sx, VIS_H * sz, HEAD_R - VIS_IN)))
    for a, b in zip(loops, loops[1:]):           # 表の帯（外→内）。法線が外（前）を向く順
        for i in range(n_edge):
            j = (i + 1) % n_edge
            bm.faces.new((a[i], a[j], b[j], b[i]))
    inner = loops[-1]
    for i in range(n_edge):
        bm.faces.new((inner[i], inner[(i + 1) % n_edge], center))
    outer = loops[0]
    for i in range(n_edge):                       # 縁（表→ヘルメットの中）
        j = (i + 1) % n_edge
        bm.faces.new((back[i], back[j], outer[j], outer[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    o = mesh_obj(name, bm)
    # 表の面が前を向いているか（recalc は開いたメッシュで裏返ることがある）
    f0 = o.data.polygons[0]
    if f0.normal.dot(Vector(f0.center) - Vector((0, 0, HEAD_Z))) < 0:
        for p in o.data.polygons:
            p.flip()
    return ck._fin(o, "visor", name, bevel=ck._bevel(0.01, VIS_OUT + VIS_IN))


def superball(name, center, radii, n, color, seg=20, flare=0.0):
    """角の丸い球（|x/a|^n+|y/b|^n+|z/c|^n=1）。flare で下を広く・上を細くする。"""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=seg // 2, radius=1, location=(0, 0, 0))
    o = bpy.context.object
    for v in o.data.vertices:
        p = v.co
        k = (abs(p.x) ** n + abs(p.y) ** n + abs(p.z) ** n) ** (1 / n)
        q = p / k
        f = 1 - flare * q.z
        v.co = Vector((center[0] + q.x * radii[0] * f, center[1] + q.y * radii[1] * f, center[2] + q.z * radii[2]))
    return ck._fin(o, color, name)


def torso_front_y(x, z):
    """胴の前の面の y（胸のアイコンを貼るのに使う）。"""
    t = (z - TORSO_C[2]) / TORSO_R[2]
    f = 1 - TORSO_FLARE * t
    r = 1 - abs(x / (TORSO_R[0] * f)) ** TORSO_N - abs(t) ** TORSO_N
    return TORSO_C[1] - TORSO_R[1] * f * max(r, 0) ** (1 / TORSO_N)


def capsule(name, top, length, r, color, tilt=(0, 0), seg=12, flat=1.0):
    """カプセル（上の球の中心が top、下へ length 伸びる）。tilt=(X まわり, Y まわり) の度で傾ける。
    flat < 1 で下の丸みをつぶす（脚の底）。"""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=seg // 2, radius=r, location=(0, 0, 0))
    o = bpy.context.object
    for v in o.data.vertices:
        if v.co.z < -1e-6:
            v.co.z = v.co.z * flat - length
    m = Matrix.Translation(top) @ Matrix.Rotation(math.radians(tilt[1]), 4, "Y") @ Matrix.Rotation(math.radians(tilt[0]), 4, "X")
    o.data.transform(m)
    return ck._fin(o, color, name)


def icon(name, shape, center, color, size=0.075, depth=0.03):
    """席アイコンの板（前後どちらにも向く。▲ は上向き）。"""
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


def eye_blob(name, u, w, rx, rz, color, lift=0.0, thick=0.012, seg=8):
    """バイザーの表に貼る平たい楕円（面の向きに合わせて回す）。位置は写す前の平面で (u, w)。"""
    c = proj(u, w, VIS_RF - 0.004 + lift)
    lon, lat = lonlat(c)
    return ck.sph(name, c, (rx, thick, rz), color, seg=seg, rot=(-lat, 0, lon))


def build(name):
    seat, shape, show_eyes = CREW[name]
    ck.clear_scene()
    root = ck.asset(name)

    # --- ヘルメット ---
    shell = ck.sph("HelmetShell", (0, 0, HEAD_Z), HEAD_R, "white", seg=ck.SEG_HELMET)
    vis = visor("HelmetVisor")
    ring = ck.tor("HelmetRing", (0, 0, NECK_Z), 0.17, 0.04, seat, seg=(16, 6))
    helmet = ck.join([shell, vis, ring], "Helmet")
    ck.set_origin(helmet, (0, 0, NECK_Z))

    # --- 目（3種類。バイザーの表に貼る。頭からは飛び出さない） ---
    n = [eye_blob(f"EyeN{i}", s * EYE_U, EYE_W, 0.032, 0.042, "white") for i, s in enumerate((-1, 1))]
    eyes_n = ck.join(n, "Eyes_Normal")
    p = [eye_blob(f"EyeP{i}", s * EYE_U, EYE_W + 0.004, 0.046, 0.046, "white", seg=10) for i, s in enumerate((-1, 1))]
    p += [eye_blob(f"PupilP{i}", s * EYE_U, EYE_W + 0.004, 0.015, 0.015, "visor", lift=0.012, thick=0.010)
          for i, s in enumerate((-1, 1))]
    eyes_p = ck.join(p, "Eyes_Panic")
    r = []
    for i, s in enumerate((-1, 1)):            # にっこり閉じた目（∩ の弧）
        pts = []
        for k in range(5):
            a = math.radians(160 - 35 * k)     # 160° → 20°
            pts.append(proj(s * EYE_U + 0.034 * math.cos(a), EYE_W - 0.014 + 0.034 * math.sin(a), VIS_RF + 0.002))
        r.append(ck.tube(f"EyeR{i}", pts, 0.011, "white"))
    eyes_r = ck.join(r, "Eyes_Relief")
    for e in (eyes_n, eyes_p, eyes_r):
        ck.attach(e, helmet)

    # --- 胴＋胸のアイコン（ヘルメットに隠れないよう、おなか寄り） ---
    body = superball("TorsoBody", TORSO_C, TORSO_R, TORSO_N, seat, flare=TORSO_FLARE)
    iz = 0.37
    chest = icon("TorsoIcon", shape, (0, torso_front_y(0, iz) + 0.004, iz), "white", size=0.09)
    torso = ck.join([body, chest], "Torso")
    ck.set_origin(torso, (0, 0, HIP_Z))

    # --- 背中の装置＋背中のアイコン ---
    pack = ck.box("PackBody", (0, 0.235, 0.44), (0.32, 0.15, 0.34), seat, bevel=0.04)
    back = icon("PackIcon", shape, (0, 0.31, 0.44), "white")
    backpack = ck.join([pack, back], "Backpack")

    # --- 腕（下ろして少し外へ開く）。原点は肩。付け根は胴に埋める ---
    for side, s in (("R", -1), ("L", 1)):
        top = (s * SHOULDER_X, 0, SHOULDER_Z)
        capsule(f"Arm_{side}", top, ARM_L, ARM_R, seat, tilt=(0, -s * ARM_OUT))
        ck.set_origin(bpy.data.objects[f"{name}.Arm_{side}"], top)

    # --- 脚。原点は股。付け根は胴に埋める ---
    leg_len = HIP_Z - LEG_R * LEG_FLAT          # 底がちょうど z=0
    for side, s in (("R", -1), ("L", 1)):
        top = (s * HIP_X, 0, HIP_Z)
        capsule(f"Leg_{side}", top, leg_len, LEG_R, seat, flat=LEG_FLAT)
        ck.set_origin(bpy.data.objects[f"{name}.Leg_{side}"], top)
    ck.end_asset()

    # 可動部の表。腕と脚は X まわり（−で前・上へ、＋で後ろへ）、頭は Z まわり（左右を見る）。
    # 腕：0° が下ろした姿、−90° で前へ水平、−180° で真上（腕が外に開いているのでヘルメット・首のリングに当たらない）、
    #     ＋60° で後ろ（背中の装置に当たらない）。めり込みは頂点で 15° ごとに確かめた。
    #     手の高さは −90° で肩と同じ 約0.53m（上の端 0.61m）、真上でも上の端 約0.75m。コンソールの天板（0.80m）には届かない（腕が短く肩が低いため）。
    # 脚：±30°（ぶらぶら・歩く）。付け根は胴の中で、動かしても胴から出入りしない。
    parts = [
        {"name": "Helmet", "type": "rotate", "axis": "Z", "min": -40, "max": 40, "default": 0},
        {"name": "Arm_L", "type": "rotate", "axis": "X", "min": -180, "max": 60, "default": 0},
        {"name": "Arm_R", "type": "rotate", "axis": "X", "min": -180, "max": 60, "default": 0},
        {"name": "Leg_L", "type": "rotate", "axis": "X", "min": -30, "max": 30, "default": 0},
        {"name": "Leg_R", "type": "rotate", "axis": "X", "min": -30, "max": 30, "default": 0},
    ]
    ck.write_parts(KIND, root, parts)
    ck.export_all(root, KIND)

    # レンダーでは目を1種類だけ見せる（FBX には3つとも入っている）
    for e in (eyes_n, eyes_p, eyes_r):
        e.hide_render = not e.name.endswith(show_eyes)
    # 3枚目は、右腕を真上へ・左腕を後ろへ、脚を前後に、頭を回した姿
    pose = [dict(q) for q in parts]
    pose[2]["max"] = pose[2]["min"]; pose[4]["max"] = pose[4]["min"]
    ck.render_check(root, KIND, pose)
    print(f"[build] {name}: {ck.tri_count(root)} 三角")


args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
for nm in (args or CREW):
    build(nm)
