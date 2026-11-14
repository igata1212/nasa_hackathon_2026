"""管制室の場面（storyboard.md の場面4）：部屋 Scene_MissionControl、ヒューストン先生 Crew_Houston、
うしろの管制官 Crew_Controller_A / B / C（#53）。

書いた人：Taiga（作成：Fable 5.2）／ 最終更新：2026-11-14

  modeling/core/run.sh modeling/scene/build_mission_control.py
出力：unity/Assets/Models/Scene/*.fbx、modeling/scene/export/、modeling/scene/renders/
      （各3枚＋320px 版、部屋に人を並べた mission_control_scene.png、先生のバストアップ houston_popup_*.png）、
      modeling/scene/parts.json

決めたこと（#53、Taiga、2026-11-14）：
  管制室は 3D で作る。先生のバストアップをポップアップ画面に出し、うしろは人が大勢いるにぎやかな管制室。
  先生は顔を出す（クルーはバイザーの中の目だけ）。白いベストにネクタイ、片耳のマイク付きヘッドセット、短い平らな髪。
  話すときに口が動くように、顔（Face）にシェイプキーを入れる。

人（先生も管制官も同じ作り。クルーと同じ 約2頭身・身長 1.2m。正面は −Y、左手は +X 側）：
  Head     頭（肌 cream）＋髪＋耳またはヘッドセット。原点は首。Z まわりに回して左右を見る
  Face     目・眉・口（Head の子）。シェイプキー7個（下の KEYS）。Unity では SkinnedMeshRenderer のブレンドシェイプ
  Torso    胴（先生は白いベスト＋navy のネクタイ＋orange の名札。文字なし）
  Arm_L / Arm_R  そで＋手。原点は肩。X まわりに回る（−で前・上へ）
  Legs     脚2本（動かさない）
シェイプキー（0〜1。Unity では 0〜100）：
  MouthOpen   口を大きく開ける（あ）      MouthO      小さく丸く開ける（お・う）
  MouthWide   横に広げる（い・え）        Smile       口の両端を上げる
  Blink       目を閉じる                  BrowsUp     眉を上げる（驚き）
  BrowsWorried 眉の内側を上げる（心配）
  話す動き：MouthOpen を 0 → 0.4〜1 → 0 と細かく上下させる。ときどき MouthO・MouthWide を混ぜる。

部屋 Scene_MissionControl（原点＝床の底の中心の手前寄り。床の上面は z=0.10。手前の壁と天井はない）：
  Floor・Walls・Tiers  床、奥と左右の壁（navy）、奥へ行くほど高くなる段2つ
  Desks_1〜3           机の列（手前から。1列に机3台）。天板は段の床から 0.8m
  Risers               机のうしろの踏み台（人が机から顔を出す高さにする）
  Screen_Center / Left / Right  奥の壁の大きな画面（文字なし。中央は地球→月→地球の軌道の絵）
  Lamp_Green / Yellow / Red     机の前の面のランプ（発光は Unity）
  Slot_Houston        先生を置く位置（床の上、部屋の手前の中央。正面 −Y）
  Slot_Seat_11〜36    管制官を置く位置（列・左から。1列6人、3列で18人。全員 −Y 向き）
  Slot_PopupCamera    ポップアップ用のカメラの位置。ここから +Y（先生のほう）を見ると、胸から上と、うしろの管制室が入る
"""
import sys, os, math, random
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "core"))
import bpy, bmesh
from mathutils import Matrix, Vector
import corekit as ck
import render_check as rc

KIND = "scene"
OUT = os.path.join(ck._export.MODELING, KIND, "renders")

# ================= 人 =================
HEAD_R = 0.27
HEAD_Z = 0.89                          # 頭の中心
NECK_Z = 0.66                          # 頭の原点（首）
TORSO_C, TORSO_R, TORSO_N, TORSO_FLARE = (0, 0, 0.41), (0.23, 0.19, 0.27), 2.6, 0.08     # クルーと同じ胴
SHOULDER_X, SHOULDER_Z = 0.20, 0.53
ARM_R, ARM_L, ARM_OUT = 0.07, 0.17, 34
HIP_X, HIP_Z, LEG_R, LEG_FLAT = 0.10, 0.20, 0.085, 0.7
FACE_TILT = 6                          # 顔を少し上に向ける（度）
_T = math.radians(FACE_TILT)
_D = Vector((0, -math.cos(_T), math.sin(_T)))       # 顔の向き
_UP = Vector((0, math.sin(_T), math.cos(_T)))
_X = Vector((1, 0, 0))
_C = Vector((0, 0, HEAD_Z))
# 顔の部品の位置（顔の平面で、横 u・縦 w。m）
MOUTH_W, MOUTH_RX, MOUTH_RZ, MOUTH_CURVE = -0.105, 0.075, 0.012, 0.012
EYE_U, EYE_W, EYE_RX, EYE_RZ = 0.095, 0.015, 0.030, 0.042
BROW_U, BROW_W, BROW_RX, BROW_RZ = 0.095, 0.097, 0.046, 0.012

PEOPLE = {
    # 名前: 胴, そで, ズボン, 髪の色, 髪型, ヘッドセット, ベスト（ネクタイと名札）
    "Crew_Houston":      dict(torso="white", sleeve="gray_light", pants="navy", hair="gray_dark", style="flat",
                              headset=True, vest=True),
    "Crew_Controller_A": dict(torso="teal", sleeve="teal", pants="gray_dark", hair="navy", style="round",
                              headset=True, vest=False),
    "Crew_Controller_B": dict(torso="white", sleeve="white", pants="navy", hair="orange", style="bob",
                              headset=False, vest=False),
    "Crew_Controller_C": dict(torso="navy", sleeve="navy", pants="gray_dark", hair="gray_light", style="round",
                              headset=True, vest=False),
}
# 髪型: (生え際の前の高さ, うしろの高さ, 頭からの厚み, てっぺんを平らに切る高さ or None)。高さは頭の中心から
HAIR = {"flat": (0.17, -0.06, 0.03, 0.275), "round": (0.15, -0.12, 0.02, None), "bob": (0.14, -0.27, 0.025, None)}


def proj(u, w, r):
    """顔の平面の点 (u, w) を、頭と同じ中心の半径 r の球面に写す（世界座標）。"""
    p = _X * u + _UP * w
    return _C + p + _D * math.sqrt(max(r * r - p.length_squared, 0.0))


def unproj(pt):
    """proj の逆。(u, w, r)。"""
    n = pt - _C
    return n.dot(_X), n.dot(_UP), n.length


def blob(name, uc, wc, rx, rz, color, thick=0.014, seg=8, curve=0.0):
    """顔に貼る平たい楕円。curve を入れると両端が上がる（口）。"""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=seg // 2, radius=1, location=(0, 0, 0))
    o = bpy.context.object
    for v in o.data.vertices:
        sx, sy, sz = v.co
        v.co = proj(uc + rx * sx, wc + rz * sz + curve * sx * sx, HEAD_R + 0.002 + thick * (-sy))
    return ck._fin(o, color, name)


def superball(name, center, radii, n, color, seg=20, flare=0.0):
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
    t = (z - TORSO_C[2]) / TORSO_R[2]
    f = 1 - TORSO_FLARE * t
    r = 1 - abs(x / (TORSO_R[0] * f)) ** TORSO_N - abs(t) ** TORSO_N
    return TORSO_C[1] - TORSO_R[1] * f * max(r, 0) ** (1 / TORSO_N)


def capsule(name, top, length, r, color, tilt=(0, 0), seg=12, flat=1.0):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=seg // 2, radius=r, location=(0, 0, 0))
    o = bpy.context.object
    for v in o.data.vertices:
        if v.co.z < -1e-6:
            v.co.z = v.co.z * flat - length
    m = Matrix.Translation(top) @ Matrix.Rotation(math.radians(tilt[1]), 4, "Y") @ Matrix.Rotation(math.radians(tilt[0]), 4, "X")
    o.data.transform(m)
    return ck._fin(o, color, name)


def hair(name, color, style):
    front, back, add, flat = HAIR[style]
    r = HEAD_R + add
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=r, location=(0, 0, 0))
    o = bpy.context.object
    f = Vector((0, -HEAD_R * 0.75, front)); b = Vector((0, HEAD_R * 0.9, back))
    n = Vector((0, -(b.z - f.z), b.y - f.y)).normalized()
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=f, plane_no=n, clear_inner=True)
    for v in bm.verts:
        if flat is not None:
            v.co.z = min(v.co.z, flat)
        v.co += _C
    bm.to_mesh(o.data); bm.free()
    return ck._fin(o, color, name)


def add_face_keys(face):
    """Face にシェイプキーを足す（finalize の後で呼ぶ。頂点は動かすだけで増やさない）。"""
    mw = face.matrix_world.copy(); mwi = mw.inverted()
    uvr = [unproj(mw @ v.co) for v in face.data.vertices]

    def part(u, w):
        return "mouth" if w < -0.05 else ("eye" if w < 0.068 else "brow")

    def mouth_ab(u, w):
        a = u / MOUTH_RX
        return a, (w - MOUTH_W - MOUTH_CURVE * a * a) / MOUTH_RZ

    def k_open(u, w):
        a, b = mouth_ab(u, w)
        return u * 0.82, MOUTH_W - 0.022 + MOUTH_CURVE * 0.3 * a * a + b * 0.055

    def k_o(u, w):
        a, b = mouth_ab(u, w)
        return u * 0.45, MOUTH_W - 0.01 + b * 0.04

    def k_wide(u, w):
        a, b = mouth_ab(u, w)
        return u * 1.18, MOUTH_W + MOUTH_CURVE * a * a + b * 0.024

    def k_smile(u, w):
        a, b = mouth_ab(u, w)
        return u * 1.12, w + 0.026 * a * a - 0.006

    def k_blink(u, w):
        return u, EYE_W - 0.012 + (w - EYE_W) * 0.28

    def k_brow_up(u, w):
        return u, w + 0.026

    def k_brow_worried(u, w):
        inner = max(0.0, min(1.0, (BROW_U + BROW_RX - abs(u)) / (2 * BROW_RX)))
        return u, w + 0.034 * inner - 0.006

    keys = [("MouthOpen", "mouth", k_open), ("MouthO", "mouth", k_o), ("MouthWide", "mouth", k_wide),
            ("Smile", "mouth", k_smile), ("Blink", "eye", k_blink), ("BrowsUp", "brow", k_brow_up),
            ("BrowsWorried", "brow", k_brow_worried)]
    face.shape_key_add(name="Basis", from_mix=False)
    for name, target, fn in keys:
        kb = face.shape_key_add(name=name, from_mix=False)
        for i, (u, w, r) in enumerate(uvr):
            if part(u, w) == target:
                u2, w2 = fn(u, w)
                kb.data[i].co = mwi @ proj(u2, w2, r)
    return [k[0] for k in keys]


KEYS = ["MouthOpen", "MouthO", "MouthWide", "Smile", "Blink", "BrowsUp", "BrowsWorried"]


def build_person(name):
    cfg = PEOPLE[name]
    root = ck.asset(name)
    skin = "cream"

    # --- 頭 ---
    hp = [ck.sph("HeadSkin", tuple(_C), HEAD_R, skin, seg=20),
          hair("HeadHair", cfg["hair"], cfg["style"])]
    ear_z = HEAD_Z - 0.02
    if cfg["headset"]:
        for i, s in enumerate((-1, 1)):
            hp.append(ck.cyl(f"HeadCup{i}", (s * (HEAD_R + 0.015), 0.01, ear_z), 0.065, 0.06, "gray_dark", rot=(0, 90, 0),
                             verts=12, bevel=0.012))
        rb = HEAD_R + 0.04
        hp.append(ck.tube("HeadBand", [(rb * math.cos(math.radians(a)), 0.01, HEAD_Z + rb * math.sin(math.radians(a)))
                                       for a in range(5, 176, 17)], 0.016, "gray_dark"))
        mic = [(HEAD_R + 0.03, -0.03, ear_z - 0.03), (0.27, -0.16, HEAD_Z - 0.10), (0.18, -0.255, HEAD_Z - 0.13)]
        hp.append(ck.tube("HeadMicArm", mic, 0.012, "gray_dark"))
        hp.append(ck.sph("HeadMic", mic[-1], 0.032, "gray_dark", seg=8))
    else:
        for i, s in enumerate((-1, 1)):
            hp.append(ck.sph(f"HeadEar{i}", (s * (HEAD_R - 0.005), 0.01, ear_z), (0.035, 0.045, 0.055), skin, seg=8))
    head = ck.join(hp, "Head")
    ck.set_origin(head, (0, 0, NECK_Z))

    # --- 顔（目・眉・口。あとでシェイプキーを足す） ---
    fseg = 12 if cfg["vest"] else 8             # 先生はバストアップで寄るので、顔を細かく
    fp = [blob("FaceMouth", 0, MOUTH_W, MOUTH_RX, MOUTH_RZ, "visor", seg=fseg, curve=MOUTH_CURVE)]
    for i, s in enumerate((-1, 1)):
        fp.append(blob(f"FaceEye{i}", s * EYE_U, EYE_W, EYE_RX, EYE_RZ, "visor", seg=fseg))
        fp.append(blob(f"FaceBrow{i}", s * BROW_U, BROW_W, BROW_RX, BROW_RZ, cfg["hair"]))
    face = ck.join(fp, "Face")
    ck.set_origin(face, (0, 0, NECK_Z))
    ck.attach(face, head)

    # --- 胴 ---
    tp = [superball("TorsoBody", TORSO_C, TORSO_R, TORSO_N, cfg["torso"], flare=TORSO_FLARE)]
    if cfg["vest"]:
        tz = 0.48
        tp.append(ck.box("TorsoTie", (0, torso_front_y(0, tz) - 0.004, tz), (0.065, 0.03, 0.24), "navy", bevel=0.008))
        tp.append(ck.box("TorsoBadge", (0.12, torso_front_y(0.12, 0.5) - 0.004, 0.5), (0.075, 0.03, 0.055), "orange",
                         bevel=0.006))
        for i, bz in enumerate((0.34, 0.26)):          # ベストのボタン
            tp.append(ck.cyl(f"TorsoButton{i}", (0, torso_front_y(0, bz) - 0.002, bz), 0.02, 0.03, "gray_light",
                             rot=(90, 0, 0), verts=8, bevel=0))
    torso = ck.join(tp, "Torso")
    ck.set_origin(torso, (0, 0, HIP_Z))

    # --- 腕（そで＋手）。原点は肩 ---
    a = math.radians(ARM_OUT)
    for side, s in (("R", -1), ("L", 1)):
        top = (s * SHOULDER_X, 0, SHOULDER_Z)
        arm = capsule(f"Sleeve{side}", top, ARM_L, ARM_R, cfg["sleeve"], tilt=(0, -s * ARM_OUT))
        hand_c = (top[0] + s * (ARM_L + 0.03) * math.sin(a), 0, top[2] - (ARM_L + 0.03) * math.cos(a))
        hand = ck.sph(f"Hand{side}", hand_c, 0.078, skin, seg=12 if cfg["vest"] else 8)
        ck.set_origin(ck.join([arm, hand], f"Arm_{side}"), top)

    # --- 脚（動かさない） ---
    leg_len = HIP_Z - LEG_R * LEG_FLAT
    ck.join([capsule(f"Leg{side}", (s * HIP_X, 0, HIP_Z), leg_len, LEG_R, cfg["pants"], flat=LEG_FLAT)
             for side, s in (("R", -1), ("L", 1))], "Legs")
    ck.end_asset()

    ck.finalize(root)
    add_face_keys(face)
    parts = [
        {"name": "Head", "type": "rotate", "axis": "Z", "min": -40, "max": 40, "default": 0,
         "note": "左右を見る。うなずきは X まわりに ±12° まで（顔 Face は Head の子で一緒に動く）"},
        {"name": "Arm_L", "type": "rotate", "axis": "X", "min": -150, "max": 40, "default": 0, "note": "−で前・上へ"},
        {"name": "Arm_R", "type": "rotate", "axis": "X", "min": -150, "max": 40, "default": 0, "note": "−で前・上へ"},
    ]
    ck.write_parts(KIND, root, parts)
    ck.export_all(root, KIND)
    pose = [dict(q) for q in parts]
    pose[1]["max"] = pose[1]["min"] = -70
    pose[2]["max"] = 20
    ck.render_check(root, KIND, pose)
    print(f"[build] {name}: {ck.tri_count(root)} 三角、シェイプキー {[k.name for k in face.data.shape_keys.key_blocks]}")
    return root


# ================= 部屋 =================
FLOOR = 0.10
X_HALF = 4.6
Y0, Y1 = -2.2, 6.4                     # 手前と奥（内側）
WALL_H = 4.5
ROWS = [(1.5, 0.0), (3.1, 0.35), (4.7, 0.70)]     # 机の列：(y, 段の高さ)
DESK_X = (-2.7, 0.0, 2.7)
DESK_W, DESK_D, DESK_H = 2.4, 0.7, 0.8
RISER = 0.12
SEAT_DY = 0.72                         # 机の中心から人まで（奥へ）
POPUP_CAM = (0.0, -2.3, 0.88)


def build_room():
    root = ck.asset("Scene_MissionControl")
    yc, yd = (Y0 + Y1) / 2, Y1 - Y0
    ck.box("Floor", (0, yc, FLOOR / 2), (2 * X_HALF + 0.4, yd + 0.2, FLOOR), "gray_dark", bevel=0.02)
    walls = [ck.box("WallBack", (0, Y1 + 0.1, FLOOR + WALL_H / 2), (2 * X_HALF + 0.4, 0.2, WALL_H), "navy", bevel=0.03)]
    for i, s in enumerate((-1, 1)):
        walls.append(ck.box(f"WallSide{i}", (s * (X_HALF + 0.1), yc, FLOOR + WALL_H / 2), (0.2, yd, WALL_H), "navy",
                            bevel=0.03))
    ck.join(walls, "Walls")
    tiers = []
    for i, (y, h) in enumerate(ROWS[1:]):
        y_front = y - 0.5
        tiers.append(ck.box(f"Tier{i}", (0, (y_front + Y1) / 2, FLOOR + h / 2), (2 * X_HALF, Y1 - y_front, h), "gray",
                            bevel=0.03))
    ck.join(tiers, "Tiers")

    lamps = {"green": [], "yellow": [], "red": []}
    rnd = random.Random(53)
    risers = []
    for r, (y, h) in enumerate(ROWS, 1):
        z0 = FLOOR + h
        dp = []
        for d, x in enumerate(DESK_X):
            dp.append(ck.box(f"D{r}{d}Body", (x, y, z0 + DESK_H / 2), (DESK_W, DESK_D, DESK_H), "gray_light", bevel=0.03))
            dp.append(ck.box(f"D{r}{d}Lip", (x, y - 0.26, z0 + DESK_H + 0.035), (DESK_W, 0.18, 0.11), "cream", bevel=0.02))
            dp.append(ck.box(f"D{r}{d}Panel", (x, y + 0.08, z0 + DESK_H + 0.005), (DESK_W - 0.2, 0.42, 0.03), "teal", bevel=0))
            dp.append(ck.box(f"D{r}{d}Stripe", (x, y - DESK_D / 2 - 0.005, z0 + 0.38), (DESK_W - 0.24, 0.03, 0.16), "teal",
                             bevel=0))
            for k in range(4):
                col = rnd.choice(["green", "green", "green", "yellow", "red"])
                lamps[col].append(ck.box(f"Lamp_{r}{d}{k}", (x - 0.75 + k * 0.5, y - DESK_D / 2 - 0.01, z0 + 0.62),
                                         (0.16, 0.04, 0.07), f"lamp_{col}", bevel=0))
            for k, sx in enumerate((-0.6, 0.6)):
                ck.slot(f"Slot_Seat_{r}{d * 2 + k + 1}", (x + sx, y + SEAT_DY, z0 + RISER))
        ck.join(dp, f"Desks_{r}")
        risers.append(ck.box(f"Riser{r}", (0, y + SEAT_DY, z0 + RISER / 2), (2 * X_HALF - 1.0, 0.55, RISER), "gray_dark",
                             bevel=0.02))
    ck.join(risers, "Risers")
    for col, objs in lamps.items():
        if objs:
            ck.join(objs, f"Lamp_{col.capitalize()}")

    # --- 奥の壁の画面（文字なし） ---
    yw = Y1                                         # 壁の面
    def screen(tag, x, z, w, h):
        return [ck.box(f"S{tag}Frame", (x, yw - 0.05, z), (w, 0.12, h), "gray", bevel=0.03),
                ck.box(f"S{tag}Face", (x, yw - 0.12, z), (w - 0.2, 0.04, h - 0.2), "visor", bevel=0)]
    yg = yw - 0.15                                  # 絵の面
    # 中央：地球 → 月をまわって → 地球（自由帰還軌道の絵）
    sc = screen("C", 0, 3.15, 3.8, 2.1)
    ex, ez, mx, mz = -1.25, 2.75, 1.25, 3.55
    sc.append(ck.cyl("SCEarth", (ex, yg, ez), 0.34, 0.04, "teal", rot=(90, 0, 0), verts=16, bevel=0))
    sc.append(ck.cyl("SCMoon", (mx, yg, mz), 0.17, 0.04, "gray_light", rot=(90, 0, 0), verts=12, bevel=0))
    path = []
    for k in range(13):                             # 行き（下の弧）→ 月のうしろ → 帰り（上の弧）
        t = k / 12
        ang = math.radians(-150 + 300 * t)
        px = (ex + mx) / 2 + 0.15 + 1.45 * math.cos(ang) * (1 if True else 1)
        pz = (ez + mz) / 2 + 0.05 + 0.62 * math.sin(ang)
        path.append((px, yg, pz))
    sc.append(ck.tube("SCPath", path, 0.022, "cream"))
    sc.append(ck.sph("SCShip", path[9], 0.07, "orange", seg=8))
    ck.join(sc, "Screen_Center")
    # 左：棒グラフ
    sl = screen("L", -3.1, 3.05, 2.0, 1.5)
    for k, (bh, col) in enumerate(((0.7, "teal"), (0.95, "teal"), (0.5, "orange"), (0.85, "teal"), (0.3, "seat_red"))):
        sl.append(ck.box(f"SLBar{k}", (-3.1 - 0.64 + k * 0.32, yg, 2.5 + bh / 2), (0.2, 0.04, bh), col, bevel=0))
    ck.join(sl, "Screen_Left")
    # 右：状態のます目
    sr = screen("R", 3.1, 3.05, 2.0, 1.5)
    cols = ["teal", "teal", "cream", "teal", "orange", "teal", "teal", "cream", "teal", "teal", "teal", "orange"]
    for k, col in enumerate(cols):
        sr.append(ck.box(f"SRCell{k}", (3.1 - 0.6 + (k % 4) * 0.4, yg, 3.45 - (k // 4) * 0.4), (0.28, 0.04, 0.26), col,
                         bevel=0))
    ck.join(sr, "Screen_Right")

    ck.slot("Slot_Houston", (0, 0, FLOOR))
    ck.slot("Slot_PopupCamera", (POPUP_CAM[0], POPUP_CAM[1], FLOOR + POPUP_CAM[2]))
    ck.end_asset()
    ck.export_all(root, KIND)
    ck.render_check(root, KIND)
    print(f"[build] {root.name}: {ck.tri_count(root)} 三角")
    return root


# ================= 組み合わせたレンダー =================
def instance(root, loc, keys=None, head=0.0, arms=(0, 0)):
    """root 以下を複製して loc に置く（レンダー用。FBX には入らない）。keys はシェイプキーの値。"""
    m = {}
    for o in ck.tree(root):
        c = o.copy()
        if o.data:
            c.data = o.data.copy()
        bpy.context.scene.collection.objects.link(c)
        c.hide_render = False
        m[o] = c
    for o, c in m.items():
        if o.parent in m:
            c.parent = m[o.parent]
    m[root].location = loc
    for o, c in m.items():
        part = o.name.split(".")[-1]
        if part == "Head":
            c.rotation_euler = (0, 0, math.radians(head))
        if part == "Arm_L":
            c.rotation_euler = (math.radians(arms[0]), 0, 0)
        if part == "Arm_R":
            c.rotation_euler = (math.radians(arms[1]), 0, 0)
        if part == "Face" and keys:
            for k, v in keys.items():
                c.data.shape_keys.key_blocks[k].value = v
    return m


def camera(loc, target, fov=30):
    cd = bpy.data.cameras.new("scene_cam"); cd.sensor_fit = "VERTICAL"; cd.angle_y = math.radians(fov)
    cd.clip_start = 0.05; cd.clip_end = 200
    cam = bpy.data.objects.new("scene_cam", cd)
    bpy.context.scene.collection.objects.link(cam)
    cam.location = loc
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = cam
    return cam


def scene_renders(room, people):
    for o in list(bpy.data.objects):                       # render_check の床・ダミー・光・カメラを片づける
        if o.name.startswith(rc.HELPER):
            bpy.data.objects.remove(o, do_unlink=True)
    for r in people.values():
        for o in ck.tree(r):
            o.hide_render = True
    for o in ck.tree(room):
        o.hide_render = False
    rc._world()
    sd = bpy.data.lights.new("scene_sun", "SUN"); sd.energy = 3.0; sd.color = rc._srgb(rc.SUN); sd.angle = math.radians(10)
    sun = bpy.data.objects.new("scene_sun", sd)
    sun.rotation_euler = (math.radians(52), 0, math.radians(-25))
    bpy.context.scene.collection.objects.link(sun)
    scn = bpy.context.scene
    scn.render.engine = "BLENDER_EEVEE"
    scn.render.resolution_x, scn.render.resolution_y = rc.RES
    scn.render.resolution_percentage = 100
    scn.view_settings.view_transform = "Standard"; scn.view_settings.look = "None"

    rnd = random.Random(7)
    names = ["Crew_Controller_A", "Crew_Controller_B", "Crew_Controller_C"]
    seats = [o for o in ck.tree(room) if ".Slot_Seat_" in o.name]
    for i, s in enumerate(sorted(seats, key=lambda o: o.name)):
        if i in (4, 13):                                    # 空席を2つ
            continue
        keys = {"MouthOpen": rnd.choice([0, 0, 0.5, 0.9]), "Smile": rnd.choice([0, 0.6, 1]),
                "Blink": rnd.choice([0, 0, 0, 1]), "BrowsUp": rnd.choice([0, 0, 0.8])}
        arms = rnd.choice([(0, 0), (0, 0), (-150, 0), (0, -140), (-150, -150)])
        instance(people[names[(i * 2 + i // 6) % 3]], tuple(s.matrix_world.translation), keys,
                 head=rnd.uniform(-35, 35), arms=arms)
    hm = instance(people["Crew_Houston"], (0, 0, FLOOR), {})
    face = next(c for o, c in hm.items() if o.name.endswith(".Face"))
    kb = face.data.shape_keys.key_blocks

    cam = camera((-3.4, -7.2, 4.2), (0.2, 2.6, 1.3), fov=34)
    rc._render(os.path.join(OUT, "mission_control_scene.png")); rc._small(os.path.join(OUT, "mission_control_scene.png"))

    # 先生のバストアップ（ポップアップの見え方）。口を閉じた・開けた・心配
    cam.data.angle_y = math.radians(30)
    cam.location = (POPUP_CAM[0], POPUP_CAM[1], FLOOR + POPUP_CAM[2])
    cam.rotation_euler = (math.radians(90), 0, 0)           # +Y をまっすぐ見る
    shots = {"1_closed": {}, "2_open": {"MouthOpen": 1}, "3_worried": {"BrowsWorried": 1, "MouthWide": 0.6}}
    for tag, vals in shots.items():
        for k in kb[1:]:
            k.value = vals.get(k.name, 0)
        p = os.path.join(OUT, f"houston_popup_{tag}.png")
        rc._render(p)
    rc._small(os.path.join(OUT, "houston_popup_2_open.png"))
    print("[scene] mission_control_scene.png, houston_popup_*.png")


if __name__ == "__main__":
    ck.clear_scene()
    people = {n: build_person(n) for n in PEOPLE}
    room = build_room()
    scene_renders(room, people)
