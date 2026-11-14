"""Ship_Interior：宇宙船の内部（船室）。コンソール・クルー・タンクなどは入っていない（置き場所の .Slot_〜 だけ）。

書いた人：iizuka（作成：Fable 5.2）／ 最終更新：2026-11-14

  modeling/core/run.sh modeling/interior/build_interior.py            作る → 書き出し → レンダー
  modeling/core/run.sh modeling/interior/build_interior.py -- --trial  試し書き（tmp/modeling_trial/ に出す）
  modeling/core/run.sh modeling/interior/render_assembled.py           組み立て図（ほかの glb を Slot に置いて撮る）
  modeling/core/run.sh modeling/core/check.py modeling/interior        機械チェック
出力：unity/Assets/Models/Ship/Ship_Interior.fbx、modeling/interior/export/Ship_Interior.glb・.stats.json、
      modeling/interior/parts.json（可動部）、modeling/interior/layout.json（Slot の座標・表示の切り替え・カメラ）、
      modeling/interior/renders/Ship_Interior_*.png
設計の理由は同じフォルダの layout.md。

形：円形の床（直径 6m）＋円筒の壁（高さ 2.15m）＋円錐の屋根＋てっぺんのドッキング用の筒とハッチ。
    y=0 の面で前後に割ってある（ドールハウス）。カメラは −Y 側（正面）から見る。前半分（Wall_Front・Roof_Front）を
    消せば、見下ろし 50° でも横からの断面でも中が見える。切り口はオレンジ。
向き：Z が上、正面（カメラの側）は −Y。原点は船底の中心（底が z=0）。床の上面は z=0.30（FLOOR_Z）。
角度 θ：真上から見て、奥（+Y）を 0°、右（+X）へ向かって ＋（時計回り）。点は (r·sinθ, r·cosθ, z)。

部品（どれもルートの子。Move_Hatch と Lamp_Cabin だけ Roof_Back の子）：
  Floor        船底＋床板＋中央の輪＋席の印（席の色の円とアイコン）＋タンクの台
  Wall_Back    奥半分の壁（窓の穴つき）＋すその帯＋オレンジの帯＋窓枠＋配管の箱2つ＋壁の画面4つ   いつも出す
  Wall_Front   手前半分の壁                                     カメラが中を見るときは消す
  Roof_Back    奥半分の屋根（円錐＋筒）＋骨4本                  断面のカメラでは出す。見下ろしでは消す（屋根なし）
  Roof_Front   手前半分の屋根                                   カメラが中を見るときは消す
  Move_Hatch   てっぺんのハッチ（ふた）。奥の縁が蝶番。Roof_Back の子なので、屋根を消すと一緒に消える
  Lamp_Cabin   室内灯（屋根の内側の白い板 3枚）。Roof_Back の子。Unity で発光に差し替える
  Pipe_Intact  事故の前のパイプ。事故のとき消して、同じ場所（.Slot_Pipe）に Fx_BrokenPipe を出す
  Slot_〜      取り付け位置（下の SEATS と build() の最後）。一覧と座標は layout.md・layout.json
               Slot_Console_<席>・Slot_Crew_<席>・Slot_Crew_Dual_CommsPlanner・Slot_Tank_1/2・Slot_Pipe・Slot_Alarm_1/2・
               Slot_Center・Slot_Window・Slot_Cam_Topdown・Slot_Cam_Topdown_Close・Slot_Cam_Cutaway
"""
import sys, os, math, json
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "core"))
from collections import Counter
import bpy, bmesh
from mathutils import Matrix, Vector
import corekit as ck
import render_check as rc

KIND, NAME = "interior", "Ship_Interior"
if "--trial" in sys.argv:
    ck.trial()

# ---------------------------------------------------------------- 寸法（m）
SEG = 48                    # 1周の分割（7.5° ごと）。直径 6m の壁を中から見るので、core の既定（16）より細かい
STEP = 360.0 / SEG
R_OUT = 3.00                # 床の直径 6m（art-direction.md）
WALL_T = 0.15               # 壁の厚み（切り口の太さ）
R_IN = R_OUT - WALL_T       # 壁の内側 2.85
BASE_H = 0.26               # 船底の厚み。上の面に壁が載る
FLOOR_Z = 0.30              # 床の上面。コンソール・タンクの Slot はこの高さ
WALL_H = 2.15               # 壁の高さ（床から）
WALL_TOP = FLOOR_Z + WALL_H             # 2.45
CONE_TOP, R_TOP = 4.00, 0.85            # 円錐の上端（外側の半径）
CONE_TOP_IN = 3.90                      # 円錐の内側の上端（外より低くして、屋根の切り口を厚く見せる）
TUNNEL_TOP = 4.20                       # ドッキング用の筒の上端
TUNNEL_R_IN = R_TOP - WALL_T            # 0.70
HATCH_R, HATCH_T = 0.80, 0.08           # ハッチ（ふた）

# 席：(名前, θ, 席の色, アイコン)。左から SPEC の順（Pilot／Life Support／Comms & Power／Mission Planner）。
# まん中（θ=0）は酸素タンク。Life Support が漏れる Tank 2 の隣、Comms & Power と Mission Planner が隣どうし（3人のとき兼ねる）
SEATS = [
    ("Pilot", -72, "seat_red", "triangle"),
    ("LifeSupport", -32, "seat_green", "circle"),
    ("CommsPower", 32, "seat_yellow", "square"),
    ("MissionPlanner", 72, "seat_blue", "diamond"),
]
R_CONSOLE = 2.30            # コンソールの原点（底面の中心）の半径。操作する側（−Y）が部屋の中心を向く
CREW_BACK = 0.55            # コンソールの原点からクルーの原点まで（手袋がパネルの上に来る距離）
R_CREW = R_CONSOLE - CREW_BACK          # 1.75
CREW_FLOAT = 0.12           # クルーが床から浮く高さ（肩 0.63 ＋ 0.12 ＝ パネルの高さ 0.75）
DUAL_TH, R_DUAL = 52, 1.60  # 3人のとき、Comms & Power のクルーが2台の間に浮く位置（任意）
CONSOLE_W, CONSOLE_Y0, CONSOLE_Y1 = 1.286, -0.329, 0.32   # コンソールの外形（modeling/console の stats.json）

# 奥のまん中：タンク → パイプ → 窓 の順に下から積む
TANK_X, TANK_Y = 0.37, 2.38
DECK_H = 0.07               # タンクの台の高さ（床から）
PIPE_Y, PIPE_Z = 2.585, FLOOR_Z + 1.26  # パイプの軸。バルブの上（床から 0.955）とコンソールの板（1.11）をよける
PIPE_HALF, PIPE_R, PIPE_FL_R, PIPE_FL_T = 0.90, 0.08, 0.115, 0.04   # Fx_BrokenPipe と同じ太さ・長さ
JBOX_TH, JBOX_R = 22.5, 2.68            # パイプの両端を受ける箱（壁に付く）。上に警報ランプを置く
JBOX_SIZE = (0.38, 0.40, 0.34)          # (幅＝壁に沿う向き, 奥行き＝壁から出る向き, 高さ)
JBOX_TOP = FLOOR_Z + 1.46              # 箱の下の面は床から 1.12（コンソールの板 1.11 の上）
WIN_TH = 15.0               # 窓の半分の幅（度）。幅 約1.48m
WIN_Z0, WIN_Z1 = FLOOR_Z + 1.44, FLOOR_Z + 2.02
BAND_Z0, BAND_Z1 = FLOOR_Z + 1.28, FLOOR_Z + 1.36       # オレンジの帯（コンソールの板の上、窓の下）
KICK_Z1 = FLOOR_Z + 0.20                                # すその帯（濃紺）
MONITORS = [(-72, 0.80), (-37, 0.70), (37, 0.70), (72, 0.80)]   # 壁の画面：(θ, 幅)
MON_Z0, MON_Z1 = FLOOR_Z + 1.50, FLOOR_Z + 1.94
RIBS = (-67.5, -30, 30, 67.5)           # 屋根の骨（θ）

# カメラ（提案）。縦の画角 30°、正面（−Y）から。見下ろしは art-direction.md の 50°、断面は 15°
CAM_FOV, CAM_ASPECT = 30.0, 16 / 10
CAM_TOPDOWN_ELEV, CAM_CUTAWAY_ELEV = 50.0, 15.0
CAM_CLOSE_Y = -1.35         # 「席に寄せる」見下ろしカメラで、画面の下の端に来る床の位置（中央の輪のすぐ手前）


# ---------------------------------------------------------------- 小道具
def polar(r, th, z=0.0):
    a = math.radians(th)
    return Vector((r * math.sin(a), r * math.cos(a), z))


def rot_th(th):
    """ローカル +Y を「θ の向きの外向き」に向ける回転（Z まわりに −θ）。"""
    return Matrix.Rotation(math.radians(-th), 4, "Z")


def crisp(o, deg=40):
    """ベベルなしの部品用。deg より急な角を「鋭い辺」にする（重み付き法線が面をまたいでにじまないように）。"""
    o.data.set_sharp_from_angle(angle=math.radians(deg))
    return o


def _obj(bm, name):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    return o


def snap_uv(o):
    """面ごとに UV を1つのマスの中心にそろえる。色の違う面の境目にベベルをかけると、ベベルの面の UV が
    2色の間に散るので、面の中でいちばん多いマスに寄せる（色がにじまない。check.py の「パレット」も通る）。"""
    cell = 0.25
    uv = o.data.uv_layers.active.data
    for p in o.data.polygons:
        cnt = Counter((min(int(uv[i].uv[0] / cell), 3), min(int((1 - uv[i].uv[1]) / cell), 3)) for i in p.loop_indices)
        (c, r), _ = cnt.most_common(1)[0]
        for i in p.loop_indices:
            uv[i].uv = ((c + 0.5) * cell, 1 - (r + 0.5) * cell)
    return o


def shell(name, ths, prof, colors, holes=(), bevel=0.03, skip=()):
    """回転体の壁の一部（角度の範囲つき、穴つき）。
    ths：θ の境目（度、小さい順）。prof：断面の点の列 [(内側の r, 内側の z, 外側の r, 外側の z), ...]（下から上）。
    holes：[(θa, θb, j0, j1)]＝θa〜θb で、断面の j0〜j1 番目の帯を抜く。
    colors：{"in": 内側, "out": 外側, "cut": θ の両端の切り口, "rim": 上下の縁と穴の縁}（色名）。skip：作らない面の種類。
    面の向きは作る順で決めてある（内側は軸向き、外側は外向き）。"""
    n, m = len(ths) - 1, len(prof) - 1

    def present(i, j):
        if not (0 <= i < n and 0 <= j < m):
            return False
        return not any(a - 1e-6 <= ths[i] and ths[i + 1] <= b + 1e-6 and j0 <= j < j1 for a, b, j0, j1 in holes)

    bm = bmesh.new()
    cache = {}

    def v(side, i, j):
        key = (side, i, j)
        if key not in cache:
            ri, zi, ro, zo = prof[j]
            cache[key] = bm.verts.new(polar(ri, ths[i], zi) if side == "I" else polar(ro, ths[i], zo))
        return cache[key]

    kinds = []

    def face(kind, vs):
        if kind in skip:
            return
        bm.faces.new(vs); kinds.append(kind)

    for i in range(n):
        for j in range(m):
            if not present(i, j):
                continue
            face("in", (v("I", i, j), v("I", i + 1, j), v("I", i + 1, j + 1), v("I", i, j + 1)))
            face("out", (v("O", i, j), v("O", i, j + 1), v("O", i + 1, j + 1), v("O", i + 1, j)))
            if not present(i, j - 1):
                face("rim", (v("I", i, j), v("O", i, j), v("O", i + 1, j), v("I", i + 1, j)))
            if not present(i, j + 1):
                face("rim", (v("I", i, j + 1), v("I", i + 1, j + 1), v("O", i + 1, j + 1), v("O", i, j + 1)))
            if not present(i - 1, j):
                face("cut" if i == 0 else "rim", (v("I", i, j), v("I", i, j + 1), v("O", i, j + 1), v("O", i, j)))
            if not present(i + 1, j):
                face("cut" if i == n - 1 else "rim",
                     (v("I", i + 1, j), v("O", i + 1, j), v("O", i + 1, j + 1), v("I", i + 1, j + 1)))
    o = _obj(bm, "__shell__")
    thin = min(abs(p[2] - p[0]) + abs(p[3] - p[1]) for p in prof)
    o = ck._fin(o, colors["in"], name, bevel=ck._bevel(bevel, thin) if bevel else 0)
    uv = o.data.uv_layers.active.data
    for p, k in zip(o.data.polygons, kinds):
        u, w = ck.PALETTE[colors.get(k, colors["in"])]["uv"]
        for i in p.loop_indices:
            uv[i].uv = (u, w)
    if bevel:
        ck._apply_modifiers(o)
        snap_uv(o)
    else:
        crisp(o)
    return o


def lathe(name, prof, color, seg=SEG, bevel=0.02):
    """1周の回転体。prof は断面の多角形 [(r, z), ...]（閉じた形。r=0 の点は軸の上）。"""
    area = sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(prof, prof[1:] + prof[:1]))
    if area < 0:                       # 反時計回りにそろえる（面が外を向く）
        prof = prof[::-1]
    bm = bmesh.new()
    rings = []
    for r, z in prof:
        if r <= 1e-6:
            rings.append([bm.verts.new((0, 0, z))])
        else:
            rings.append([bm.verts.new(polar(r, i * 360.0 / seg, z)) for i in range(seg)])
    for a, b in zip(rings, rings[1:] + rings[:1]):
        for i in range(seg):
            k = (i + 1) % seg
            if len(a) == 1 and len(b) == 1:
                continue
            if len(a) == 1:
                bm.faces.new((a[0], b[i], b[k]))
            elif len(b) == 1:
                bm.faces.new((a[i], b[0], a[k]))
            else:
                bm.faces.new((a[i], b[i], b[k], a[k]))
    o = _obj(bm, "__lathe__")
    thin = min(max(p[0] for p in prof) - min(p[0] for p in prof), max(p[1] for p in prof) - min(p[1] for p in prof))
    o = ck._fin(o, color, name, bevel=ck._bevel(bevel, thin) if bevel else 0)
    return o if bevel else crisp(o)


def prism(name, pts, depth, color, mat, bevel=0.0):
    """2D の多角形 pts（ローカル XY）をローカル +Z へ depth だけ押し出し、mat で置く（console の prism と同じ作り）。"""
    bm = bmesh.new()
    bot = [bm.verts.new((x, y, 0)) for x, y in pts]
    top = [bm.verts.new((x, y, depth)) for x, y in pts]
    bm.faces.new(bot); bm.faces.new(top)
    k = len(pts)
    for i in range(k):
        j = (i + 1) % k
        bm.faces.new((bot[i], bot[j], top[j], top[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bmesh.ops.transform(bm, matrix=mat, verts=bm.verts[:])
    o = ck._fin(_obj(bm, "__prism__"), color, name, bevel=bevel)
    return o if bevel else crisp(o)


def box_m(name, size, mat, color, bevel=None):
    """mat の座標系に置く直方体（中心が mat の原点）。部品の原点は (0,0,0) のまま。"""
    o = ck.box(name, (0, 0, 0), size, color, bevel=bevel)
    o.data.transform(mat)
    return o if bevel != 0 else crisp(o)


def cyl_x(name, center, r, length, color, bevel=None, verts=ck.SEG_CYL):
    """軸が X の円柱（部品の原点は (0,0,0)）。"""
    o = ck.cyl(name, (0, 0, 0), r, length, color, verts=verts, bevel=bevel)
    o.data.transform(Matrix.Translation(center) @ Matrix.Rotation(math.radians(90), 4, "Y"))
    return o if bevel != 0 else crisp(o)


def icon_pts(kind, s=1.0):
    """席アイコン（console と同じ形。中心が原点、上が +Y）。"""
    if kind == "triangle":
        r = 0.125 * s
        return [(r * math.cos(math.radians(a)), r * math.sin(math.radians(a)) - 0.018 * s) for a in (90, 210, 330)]
    if kind == "circle":
        r = 0.098 * s
        return [(r * math.cos(2 * math.pi * i / 16), r * math.sin(2 * math.pi * i / 16)) for i in range(16)]
    if kind == "square":
        h = 0.086 * s
        return [(-h, -h), (h, -h), (h, h), (-h, h)]
    if kind == "diamond":
        r = 0.12 * s
        return [(0, -r), (r, 0), (0, r), (-r, 0)]
    raise ValueError(kind)


def ths(a, b):
    k = round((b - a) / STEP)
    return [a + STEP * i for i in range(k + 1)]


BACK, FRONT = ths(-90, 90), ths(90, 270)
WALL_COLORS = {"in": "cream", "out": "white", "cut": "orange", "rim": "gray_light"}


def one(color):
    return {"in": color, "out": color, "cut": color, "rim": color}


# ---------------------------------------------------------------- 部品
def build_floor():
    parts = []
    # 船底：下の角を丸めた皿。上の面（z=BASE_H）の外周に壁が載る
    parts.append(lathe("FloorBase", [(0, 0), (2.50, 0), (2.86, 0.08), (R_OUT, 0.20), (R_OUT, BASE_H), (0, BASE_H)],
                       "gray_light", bevel=0.03))
    # 床板：壁の内側より 1cm 大きくして、壁の下にもぐらせる（すき間が見えない）
    parts.append(lathe("FloorPlate", [(0, BASE_H - 0.01), (R_IN + 0.01, BASE_H - 0.01), (R_IN + 0.01, FLOOR_Z),
                                      (0, FLOOR_Z)], "gray_dark", bevel=0))
    # 中央の輪（部屋の中心の目印）
    parts.append(lathe("FloorRing", [(0.98, FLOOR_Z - 0.015), (1.07, FLOOR_Z - 0.015), (1.07, FLOOR_Z + 0.012),
                                     (0.98, FLOOR_Z + 0.012)], "teal", seg=32, bevel=0))
    # 席の印：クルーが浮く位置の真下に、席の色の円とアイコン。席が空いていても「ここが誰の席か」が分かる
    for seat, th, color, icon in SEATS:
        c = polar(R_CREW, th)
        pad = ck.cyl(f"Pad{seat}", (0, 0, 0), 0.40, 0.027, color, verts=24, bevel=0)
        pad.data.transform(Matrix.Translation((c.x, c.y, FLOOR_Z - 0.0015)))
        parts.append(crisp(pad))
        m = Matrix.Translation((c.x, c.y, FLOOR_Z + 0.004)) @ rot_th(th)      # アイコンの上がコンソールの側
        parts.append(prism(f"PadIcon{seat}", icon_pts(icon, 2.1), 0.02, "navy", m))
    # タンクの台：オレンジの縁（注意の色）の上に灰色の板
    parts.append(ck.box("DeckEdge", (0, TANK_Y, FLOOR_Z + 0.02), (1.56, 0.80, 0.06), "orange", bevel=0.012))
    parts.append(ck.box("DeckTop", (0, TANK_Y, FLOOR_Z + DECK_H - 0.045), (1.44, 0.68, 0.09), "gray", bevel=0.012))
    floor = ck.join(parts, "Floor")
    ck.set_origin(floor, (0, 0, 0))
    return floor


def band(name, th_list, z0, z1, out, color):
    """壁の内側に付く帯（壁から out だけ出る）。壁の中に隠れる外側の面は作らない。"""
    prof = [(R_IN - out, z0, R_IN + 0.01, z0), (R_IN - out, z1, R_IN + 0.01, z1)]
    return shell(name, th_list, prof, one(color), bevel=0, skip=("out",))


def build_wall_back():
    parts = []
    prof = [(R_IN, BASE_H, R_OUT, BASE_H), (R_IN, WIN_Z0, R_OUT, WIN_Z0), (R_IN, WIN_Z1, R_OUT, WIN_Z1),
            (R_IN, WALL_TOP, R_OUT, WALL_TOP)]
    parts.append(shell("WallBackShell", BACK, prof, WALL_COLORS, holes=[(-WIN_TH, WIN_TH, 1, 2)], bevel=0.03))
    parts.append(band("WallBackKick", BACK, BASE_H, KICK_Z1, 0.03, "navy"))
    parts.append(band("WallBackBand", BACK, BAND_Z0, BAND_Z1, 0.025, "orange"))
    # 窓枠：壁から 5cm 出る枠（穴つきの板）。θ は 3.75° 刻みで窓の縁に合わせる
    fr = WIN_TH + 2.4
    fths = [-fr] + [-WIN_TH + 3.75 * i for i in range(round(2 * WIN_TH / 3.75) + 1)] + [fr]
    fprof = [(R_IN - 0.05, z, R_IN + 0.01, z) for z in (WIN_Z0 - 0.07, WIN_Z0, WIN_Z1, WIN_Z1 + 0.07)]
    parts.append(shell("WallBackFrame", fths, fprof, one("teal"), holes=[(-WIN_TH, WIN_TH, 1, 2)], bevel=0.012,
                       skip=("out",)))
    # パイプの両端を受ける箱（壁に 3cm もぐる）。上の面が警報ランプの台
    for side, s in (("L", -1), ("R", 1)):
        th = s * JBOX_TH
        m = Matrix.Translation(polar(JBOX_R, th, JBOX_TOP - JBOX_SIZE[2] / 2)) @ rot_th(th)
        parts.append(box_m(f"JBox{side}", JBOX_SIZE, m, "gray_light", bevel=0.03))
        parts.append(box_m(f"JBoxCap{side}", (JBOX_SIZE[0] - 0.08, JBOX_SIZE[1] - 0.10, 0.03),
                           m @ Matrix.Translation((0, -0.02, JBOX_SIZE[2] / 2 + 0.005)), "gray_dark", bevel=0))
    # 壁の画面（枠＋青緑の画面。文字は入れない。Unity で絵を貼れる）
    for k, (th, w) in enumerate(MONITORS):
        zc = (MON_Z0 + MON_Z1) / 2
        m = Matrix.Translation(polar(R_IN - 0.03, th, zc)) @ rot_th(th)
        parts.append(box_m(f"Mon{k}", (w, 0.10, MON_Z1 - MON_Z0), m, "gray_light", bevel=0.02))
        parts.append(box_m(f"MonScreen{k}", (w - 0.16, 0.04, MON_Z1 - MON_Z0 - 0.14),
                           m @ Matrix.Translation((0, -0.045, 0)), "teal", bevel=0))
    wall = ck.join(parts, "Wall_Back")
    ck.set_origin(wall, (0, 0, 0))
    return wall


def build_wall_front():
    prof = [(R_IN, BASE_H, R_OUT, BASE_H), (R_IN, WALL_TOP, R_OUT, WALL_TOP)]
    parts = [shell("WallFrontShell", FRONT, prof, WALL_COLORS, bevel=0.03),
             band("WallFrontKick", FRONT, BASE_H, KICK_Z1, 0.03, "navy"),
             band("WallFrontBand", FRONT, BAND_Z0, BAND_Z1, 0.025, "orange")]
    # 外から見たときの目印：正面の側面ハッチ（動かない飾り）と小さな丸窓の枠
    m = Matrix.Translation(polar(R_OUT + 0.01, 180, FLOOR_Z + 1.0)) @ rot_th(180)
    parts.append(box_m("SideHatch", (0.95, 0.10, 1.25), m, "gray_light", bevel=0.04))
    parts.append(box_m("SideHatchPort", (0.34, 0.04, 0.34), m @ Matrix.Translation((0, 0.05, 0.25)), "navy", bevel=0))
    parts.append(box_m("SideHatchBar", (0.50, 0.06, 0.08), m @ Matrix.Translation((0, 0.06, -0.25)), "orange", bevel=0))
    wall = ck.join(parts, "Wall_Front")
    ck.set_origin(wall, (0, 0, 0))
    return wall


ROOF_PROF = [(R_IN, WALL_TOP, R_OUT, WALL_TOP), (TUNNEL_R_IN, CONE_TOP_IN, R_TOP, CONE_TOP),
             (TUNNEL_R_IN, TUNNEL_TOP, R_TOP, TUNNEL_TOP)]


def rib(name, th):
    """屋根の内側の骨。円錐の内側の線に沿う角材（3cm もぐらせる）。"""
    a = Vector((R_IN, WALL_TOP + 0.02))                 # (r, z)
    b = Vector((TUNNEL_R_IN + 0.02, CONE_TOP_IN - 0.01))
    d2 = (b - a).normalized()
    n2 = Vector((-d2.y, d2.x))                          # 内側（軸のほう・下）を向く法線
    if n2.x > 0:
        n2 = -n2
    u, z = polar(1, th), Vector((0, 0, 1))
    D = u * d2.x + z * d2.y
    N = u * n2.x + z * n2.y
    T = D.cross(N).normalized()
    c2 = (a + b) / 2 + n2 * 0.025
    m = Matrix((N, T, D)).transposed().to_4x4()
    m.translation = u * c2.x + z * c2.y
    return box_m(name, (0.11, 0.16, (b - a).length), m, "gray_light", bevel=0.02)


def roof_frame(th, t, out):
    """屋根の内側の面の上の座標系。t：壁の上端(0)〜筒の下端(1)。out：面から室内へ出る量。
    返り値の行列は、ローカル X＝面の法線（室内向き）、Y＝壁に沿う向き、Z＝斜面を上る向き。"""
    a = Vector((R_IN, WALL_TOP))
    b = Vector((TUNNEL_R_IN, CONE_TOP_IN))
    d2 = (b - a).normalized()
    n2 = Vector((-d2.y, d2.x))
    if n2.x > 0:
        n2 = -n2
    u, z = polar(1, th), Vector((0, 0, 1))
    D = u * d2.x + z * d2.y
    N = u * n2.x + z * n2.y
    c2 = a + (b - a) * t + n2 * out
    m = Matrix((N, D.cross(N).normalized(), D)).transposed().to_4x4()
    m.translation = u * c2.x + z * c2.y
    return m


def build_cabin_lamp(roof_back):
    """室内灯（屋根の内側の白い板 3枚）。Lamp_ の部品なので Unity で発光に差し替えられる（照明スイッチ OFF で消す用）。
    色は white（ランプ専用の3色ではない）。屋根の奥半分の子。"""
    ps = [box_m(f"CabinLight{k}", (0.07, w, 0.40), roof_frame(th, 0.46, 0.012), "white", bevel=0.02)
          for k, (th, w) in enumerate(((-49, 0.62), (0, 0.80), (49, 0.62)))]
    lamp = ck.join(ps, "Lamp_Cabin")
    ck.set_origin(lamp, (0, 0, 0))
    ck.attach(lamp, roof_back)
    return lamp


def build_roof_back():
    parts = [shell("RoofBackShell", BACK, ROOF_PROF, WALL_COLORS, bevel=0.03)]
    parts += [rib(f"Rib{k}", th) for k, th in enumerate(RIBS)]
    roof = ck.join(parts, "Roof_Back")
    ck.set_origin(roof, (0, 0, 0))
    return roof


def build_roof_front():
    roof = ck.join([shell("RoofFrontShell", FRONT, ROOF_PROF, WALL_COLORS, bevel=0.03)], "Roof_Front")
    ck.set_origin(roof, (0, 0, 0))
    return roof


def build_hatch(roof_back):
    """てっぺんのふた。奥（+Y）の縁が蝶番で、上へ開く。屋根の奥半分の子。"""
    z0 = TUNNEL_TOP
    lid = ck.cyl("HatchLid", (0, 0, z0 + HATCH_T / 2), HATCH_R, HATCH_T, "gray_light", verts=24, bevel=0.02)
    hub = crisp(ck.cyl("HatchHub", (0, 0, z0 + HATCH_T + 0.02), 0.20, 0.06, "gray", verts=12, bevel=0))
    bar = ck.box("HatchBar", (0, 0, z0 + HATCH_T + 0.06), (0.74, 0.10, 0.06), "orange", bevel=0.015)
    hinge = ck.box("HatchHinge", (0, HATCH_R - 0.02, z0 + HATCH_T / 2), (0.36, 0.14, 0.10), "gray", bevel=0.02)
    hatch = ck.join([lid, hub, bar, hinge], "Move_Hatch")
    ck.set_origin(hatch, (0, HATCH_R, z0 + HATCH_T / 2))
    ck.attach(hatch, roof_back)
    return hatch


def build_pipe():
    """事故の前のパイプ。Fx_BrokenPipe と同じ太さ・長さ・色で、同じ場所に置く。"""
    c = Vector((0, PIPE_Y, PIPE_Z))
    parts = [cyl_x("PipeTube", c, PIPE_R, 2 * PIPE_HALF - 0.02, "gray_light", bevel=0)]     # 両端はフランジの中
    for side, s in (("L", -1), ("R", 1)):
        parts.append(cyl_x(f"PipeFlange{side}", c + Vector((s * PIPE_HALF, 0, 0)), PIPE_FL_R, PIPE_FL_T, "gray",
                           bevel=0.012))
        parts.append(cyl_x(f"PipeBand{side}", c + Vector((s * (PIPE_HALF - 0.12), 0, 0)), PIPE_R + 0.008, 0.06,
                           "orange", bevel=0))
    pipe = ck.join(parts, "Pipe_Intact")
    ck.set_origin(pipe, tuple(c))
    return pipe


# ---------------------------------------------------------------- カメラ（提案）の位置
def fit_camera(points, elev_deg, margin=1.08):
    """正面（−Y）から、見下ろし elev 度で、points が全部入るいちばん近い位置。返り値は (位置, 見る向き, 上)。"""
    e = math.radians(elev_deg)
    view = Vector((0, math.cos(e), -math.sin(e)))
    up = Vector((0, math.sin(e), math.cos(e)))
    right = Vector((1, 0, 0))
    tv = math.tan(math.radians(CAM_FOV) / 2) / margin
    th = tv * CAM_ASPECT
    c = sum(points, Vector()) / len(points)

    def spans(d, s):
        p = c + up * s - view * d
        av, ah = [], []
        for q in points:
            rel = q - p
            depth = rel.dot(view)
            if depth <= 0.1:
                return None
            av.append(rel.dot(up) / depth); ah.append(abs(rel.dot(right)) / depth)
        return max(av), min(av), max(ah)

    def best_s(d):
        lo, hi = -20.0, 20.0
        for _ in range(60):                    # 上下の余白が同じになる s（s を上げると全部が下へずれる）
            s = (lo + hi) / 2
            r = spans(d, s)
            if r is None or r[0] + r[1] > 0:
                lo = s
            else:
                hi = s
        return (lo + hi) / 2

    def fits(d):
        r = spans(d, best_s(d))
        return r is not None and r[0] <= tv and -r[1] <= tv and r[2] <= th

    lo, hi = 1.0, 200.0
    for _ in range(60):
        d = (lo + hi) / 2
        if fits(d):
            hi = d
        else:
            lo = d
    return c + up * best_s(hi) - view * hi, view, up


def cam_slot(name, pos, view, up):
    """カメラの位置と向きを Empty に入れる。ローカル −Y（＝Unity の forward）が見る向き、ローカル +Z が上。"""
    y = -view
    x = y.cross(up).normalized()
    m = Matrix((x, y, up)).transposed().to_4x4()
    m.translation = pos
    s = ck.slot(name, (0, 0, 0))
    s.matrix_world = m
    return s


def rim_points(r, z, a, b, n=24):
    return [polar(r, a + (b - a) * i / n, z) for i in range(n + 1)]


# ---------------------------------------------------------------- 組み立て
def check_layout():
    """置き場所がぶつからないかを数値で確かめる（コンソールの角が壁の中、隣どうしが離れている、クルーが重ならない）。"""
    corners = {}
    for seat, th, _, _ in SEATS:
        m = Matrix.Translation(polar(R_CONSOLE, th)) @ rot_th(th)
        cs = [m @ Vector((sx * CONSOLE_W / 2, y, 0)) for sx in (-1, 1) for y in (CONSOLE_Y0, CONSOLE_Y1)]
        corners[seat] = cs
        far = max(c.length for c in cs)
        assert far < R_IN - 0.035, f"{seat} のコンソールが壁に当たる: {far:.3f}"
    names = [s[0] for s in SEATS]
    for a, b in zip(names, names[1:]):
        gap = min((p - q).length for p in corners[a] for q in corners[b])
        assert gap > 0.10, f"{a} と {b} のコンソールが近すぎる: {gap:.3f}"
    crew = [polar(R_CREW, th) for _, th, _, _ in SEATS]
    for (a, p), (b, q) in zip(zip(names, crew), list(zip(names, crew))[1:]):
        assert (p - q).length > 0.75, f"{a} と {b} のクルーが近すぎる: {(p - q).length:.3f}"
        assert abs(p.x - q.x) > 0.66, f"横から見て {a} と {b} のクルーが重なる: {abs(p.x - q.x):.3f}"
    assert PIPE_Z - PIPE_FL_R > FLOOR_Z + DECK_H + 0.645 + 0.24 + 0.03, "パイプがバルブに当たる"
    assert JBOX_TOP - JBOX_SIZE[2] >= FLOOR_Z + 1.111 + 0.005, "配管の箱がコンソールの板に当たる"
    assert WIN_Z0 - 0.07 >= BAND_Z1 and WIN_Z1 + 0.07 <= WALL_TOP, "窓枠が帯か壁の上端に当たる"


def build():
    check_layout()
    ck.clear_scene()
    root = ck.asset(NAME)
    build_floor()
    build_wall_back()
    build_wall_front()
    roof_back = build_roof_back()
    build_roof_front()
    build_hatch(roof_back)
    build_cabin_lamp(roof_back)
    build_pipe()

    slots = {}

    def slot(name, loc, rot_z=0.0, note=""):
        rot_z = (rot_z + 180.0) % 360.0 - 180.0            # −180〜180 にそろえる
        s = ck.slot(name, tuple(loc))
        s.rotation_euler = (0, 0, math.radians(rot_z))
        slots[name] = {"pos": [round(v, 4) + 0.0 for v in loc], "rot_z": round(rot_z, 3) + 0.0, "note": note}
        return s

    for seat, th, _, _ in SEATS:
        slot(f"Slot_Console_{seat}", polar(R_CONSOLE, th, FLOOR_Z), -th,
             f"Console_{seat} の原点（底面の中心）。操作する側が部屋の中心を向く")
        slot(f"Slot_Crew_{seat}", polar(R_CREW, th, FLOOR_Z + CREW_FLOAT), 180 - th,
             f"Crew_{seat} の原点（足の底の中心）。床から {CREW_FLOAT}m 浮いてコンソールを向く。Unity でここを中心に上下に揺らす")
    slot("Slot_Crew_Dual_CommsPlanner", polar(R_DUAL, DUAL_TH, FLOOR_Z + CREW_FLOAT), 180 - DUAL_TH,
         "3人のとき：Comms & Power のクルーが Mission Planner と2台を兼ねる位置（2台の間。任意）")
    # 漏れる Tank 2 を左（Life Support の隣）に置く。破れ版の穴は右前を向くので、ガスは部屋のまん中へ噴き出す
    slot("Slot_Tank_1", (TANK_X, TANK_Y, FLOOR_Z + DECK_H), 0, "Tank_Oxygen1 の原点（右）。上の .Slot_Valve に Ctrl_Valve")
    slot("Slot_Tank_2", (-TANK_X, TANK_Y, FLOOR_Z + DECK_H), 0,
         "Tank_Oxygen2 の原点（左。漏れるタンク。Life Support の隣）。事故のとき Tank_Oxygen2_Broken に差し替える。上の .Slot_Valve に Ctrl_Valve")
    slot("Slot_Pipe", (0, PIPE_Y, PIPE_Z - PIPE_FL_R), 0,
         "Fx_BrokenPipe の原点。事故のとき .Pipe_Intact を消してここに出す（ガスは Fx_BrokenPipe.Slot_Leak から）")
    for k, s in ((1, -1), (2, 1)):
        slot(f"Slot_Alarm_{k}", polar(JBOX_R, s * JBOX_TH, JBOX_TOP + 0.02) + polar(-0.02, s * JBOX_TH), 0,
             "Fx_Alarm の原点（窓の左右の箱の上）。1 が左、2 が右。片方だけでもよい")
    slot("Slot_Center", (0, 0, FLOOR_Z), 0, "床のまん中（青緑の輪の中心）。空き。浮いている小物や演出用")
    slot("Slot_Window", (0, R_OUT, (WIN_Z0 + WIN_Z1) / 2), 0,
         "窓のまん中（壁の外側の面）。ローカル +Y（Unity では −Z）が外。地球・月はこの先に置く")

    # カメラ（提案）。入れる点：床の縁、奥の壁の上の縁、（断面は）屋根とハッチ
    base = rim_points(R_OUT, 0, 0, 360, 48) + rim_points(R_OUT, BASE_H, 0, 360, 48)
    wall = rim_points(R_OUT, WALL_TOP, -90, 90)
    roof = rim_points(R_TOP, TUNNEL_TOP + 0.15, -90, 90) + [Vector((0, -HATCH_R, TUNNEL_TOP + 0.15))]
    back = rim_points(R_OUT, BASE_H, -90, 90)
    front = [Vector((x, CAM_CLOSE_Y, FLOOR_Z)) for x in (-2.6, 2.6)]
    cams = {}
    for name, elev, pts, note in (
            ("Slot_Cam_Topdown", CAM_TOPDOWN_ELEV, base + wall, "見下ろし 50°、床の全体（屋根なし）"),
            ("Slot_Cam_Topdown_Close", CAM_TOPDOWN_ELEV, back + wall + front,
             "見下ろし 50°、席に寄せる（屋根なし）。床の手前は画面の下に切れる（そこに UI を置ける）"),
            ("Slot_Cam_Cutaway", CAM_CUTAWAY_ELEV, base + wall + roof, "横からの断面 15°（奥の屋根あり）")):
        pos, view, up = fit_camera(pts, elev)
        cam_slot(name, pos, view, up)
        cams[name] = {"pos": [round(v, 4) + 0.0 for v in pos], "elev": elev, "fov": CAM_FOV, "note": note}
    ck.end_asset()
    return root, slots, cams


def write_layout(slots, cams):
    """Slot の座標・表示の切り替え・カメラを layout.json に書く（Unity の位置は Blender (x, y, z) → (−x, z, −y)）。"""
    def unity_pos(p):
        return [round(-p[0], 4) + 0.0, round(p[2], 4) + 0.0, round(-p[1], 4) + 0.0]
    out = {
        "asset": NAME,
        "note": "build_interior.py が書く。pos・rot_z は Blender（m、度、Z まわり）。unity_pos・unity_euler は Unity（ルートが原点・回転0 のとき）",
        "floor_z": FLOOR_Z, "floor_radius": R_OUT, "wall_top_z": round(WALL_TOP, 4), "top_z": round(TUNNEL_TOP + HATCH_T + 0.09, 4),
        "slots": {k: {**v, "unity_pos": unity_pos(v["pos"]), "unity_euler": [0, round(-v["rot_z"], 3) + 0.0, 0]}
                  for k, v in slots.items()},
        "cameras": {k: {**v, "unity_pos": unity_pos(v["pos"]), "unity_euler": [v["elev"], 180, 0], "aspect": "16:10 で合わせた"}
                    for k, v in cams.items()},
        "show": {
            "topdown": {"hide": [f"{NAME}.Wall_Front", f"{NAME}.Roof_Front", f"{NAME}.Roof_Back"],
                        "camera": "Slot_Cam_Topdown", "camera_close": "Slot_Cam_Topdown_Close"},
            "cutaway": {"hide": [f"{NAME}.Wall_Front", f"{NAME}.Roof_Front"], "camera": "Slot_Cam_Cutaway"},
            "closed": {"hide": [], "camera": None},
        },
        "swap_on_accident": {
            f"{NAME}.Pipe_Intact": "消して Slot_Pipe に Fx_BrokenPipe",
            "Tank_Oxygen2": "Slot_Tank_2 で Tank_Oxygen2_Broken に差し替え",
        },
    }
    path = os.path.join(ck._export.MODELING, KIND, "layout.json")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1); f.write("\n")
    return path


def render_closed(root, path):
    """全部の部品を出した姿（外から見た宇宙船）を1枚。"""
    lo, hi = ck._export.world_bounds(ck.meshes(root))
    made, cam, _ = rc._helpers(lo, hi, False, False)
    world = rc._world()
    scn = bpy.context.scene
    scn.render.engine = "BLENDER_EEVEE"
    scn.render.resolution_x, scn.render.resolution_y = rc.RES
    scn.render.resolution_percentage = 100
    scn.view_settings.view_transform = "Standard"; scn.view_settings.look = "None"
    rc._aim(cam, rc._corners(lo, hi), 25, 20, margin=0.95)
    rc._render(path)
    for o in made:
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.worlds.remove(world)
    print("[closed]", os.path.relpath(path, ck._export.REPO))


if __name__ == "__main__":
    root, slots, cams = build()
    parts = [{"name": "Move_Hatch", "type": "rotate", "axis": "X", "min": -110, "max": 0, "default": 0,
              "note": "てっぺんのハッチ。奥（+Y）の縁が蝶番。− で上へ開く（−110° で全開）。Roof_Back の子"}]
    ck.write_parts(KIND, root, parts)
    ck.export_all(root, KIND)
    write_layout(slots, cams)
    print(f"[build] {NAME}: {ck.tri_count(root)} 三角")
    for o in ck.meshes(root):
        print(f"   {o.name}: {ck._export.tri_count(o)}")
    out_dir = os.path.join(ck._export.MODELING, KIND, "renders")
    render_closed(root, os.path.join(out_dir, NAME + "_4_closed.png"))
    # 単体の確認用レンダーは「断面」の状態（手前半分を消す）。3枚目はハッチを開いた姿
    for n in ("Wall_Front", "Roof_Front"):
        bpy.data.objects[f"{NAME}.{n}"].hide_render = True
    pose = [dict(parts[0], max=-110)]
    ck.render_check(root, KIND, pose)
