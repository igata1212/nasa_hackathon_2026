"""小物（Prop_）の4つ：ペン・手順書・宇宙食のパック・ダクトテープ。無重力の船内に浮かせる物（#55 の推奨1）。

書いた人：iizuka（作成：Haiku 5.5）／ 最終更新：2026-11-14

  modeling/core/run.sh modeling/props/build_props.py
出力：unity/Assets/Models/Props/Prop_*.fbx、modeling/props/export/（glb と stats.json）、
      modeling/props/renders/（各アセットの PNG と、4つを並べた props_lineup.png）
原点は中心（浮かせて回すため。床置きではないので底面 z=0 のルールは使わない。check.py は中心が原点かを見る）。
"""
import sys, os, math
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "core"))
import bpy
import corekit as ck
import export as ex
import render_check as rc

KIND = "props"


def pen():
    """ペン：長さ 0.30m（実物の2倍に誇張）、太さ 3.2cm。キャップはオレンジ。軸は X（−X が先の細い側）。"""
    root = ck.asset("Prop_Pen")
    ck.cyl("Tip", (-0.135, 0, 0), 0.016, 0.03, "gray_dark", rot=(0, -90, 0), r2=0.005, bevel=0.003)
    ck.cyl("Barrel", (-0.01, 0, 0), 0.016, 0.22, "white", rot=(0, 90, 0))
    ck.cyl("Cap", (0.125, 0, 0), 0.0165, 0.05, "orange", rot=(0, 90, 0))
    ck.end_asset()
    return root


def checklist():
    """手順書：リングで綴じた小さな本。0.30×0.22×0.04m（X×Y×Z）。表紙は青緑、紙はクリーム（右の小口）。綴じ側は x=−0.15。"""
    root = ck.asset("Prop_Checklist")
    ck.box("Cover", (-0.015, 0, 0), (0.27, 0.22, 0.04), "teal")
    ck.box("Pages", (0.135, 0, 0), (0.03, 0.21, 0.036), "cream")
    # 綴じリング3つ（軸は Y。外径 0.062m、管の太さ 2.5cm）。輪の外の端が綴じ側の x=−0.15 に合う
    rings = [ck.tor(f"Ring_{i}", (-0.119, y, 0), 0.0185, 0.0125, "gray_light", rot=(90, 0, 0), seg=(12, 6))
             for i, y in enumerate((-0.07, 0.0, 0.07), 1)]
    ck.join(rings, "Rings")
    ck.end_asset()
    return root


# 宇宙食のパック：枕型の袋（超楕円体）と、その上面に寝かせた封の帯。
PILLOW_A, PILLOW_B, PILLOW_C = 0.10, 0.07, 0.03     # 袋の半径（m）。長さ 0.20・幅 0.14・厚み 0.06
PILLOW_E1, PILLOW_E2 = 0.8, 0.5                    # 指数。E1 は厚みの丸み（小さいほど上面が平らで縁が丸い）、E2 は四隅の丸み（小さいほど角が丸い）
BAND_W, BAND_LIFT = 0.03, 0.008                    # 帯の幅（x）と、袋の表面からの浮き（帯の厚み）


def _pw(t, e):
    """符号つきの累乗（超楕円体の式）。"""
    return math.copysign(abs(t) ** e, t)


def _pillow_top(x, y, a, b, c):
    """枕型の上面の高さ。(x, y) が輪郭の内側のとき（外は 0）。"""
    f = ((abs(x) / a) ** (2 / PILLOW_E2) + (abs(y) / b) ** (2 / PILLOW_E2)) ** (PILLOW_E2 / PILLOW_E1)
    return c * max(0.0, 1 - f) ** (PILLOW_E1 / 2)


def _pillow_edge(x, a, b):
    """枕型の輪郭（z=0 の縁）の y。x の位置で。"""
    return b * max(0.0, 1 - (abs(x) / a) ** (2 / PILLOW_E2)) ** (PILLOW_E2 / 2)


def foodpack():
    """宇宙食のパック：枕型の袋（白）と、上面に寝かせて貼った薄いオレンジの封の帯。
    帯は袋の上面に沿う薄い板。袋より BAND_LIFT だけ外側で、両端は縁から少しはみ出す。"""
    import bmesh
    root = ck.asset("Prop_FoodPack")
    a, b, c = PILLOW_A, PILLOW_B, PILLOW_C
    dz = -BAND_LIFT / 2        # 帯の分だけ上に出るので、全体を下げて bbox の中心を原点に合わせる（check.py の原点）
    # 袋：UV球の (η, ω) をそのまま超楕円体の式に通す（極は1点のまま、他は四角形の格子）
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=10, radius=1, location=(0, 0, 0))
    bag = bpy.context.object
    for v in bag.data.vertices:
        eta = math.asin(max(-1.0, min(1.0, v.co.z)))
        om = math.atan2(v.co.y, v.co.x)
        ce, se, co, so = math.cos(eta), math.sin(eta), math.cos(om), math.sin(om)
        v.co = (a * _pw(ce, PILLOW_E1) * _pw(co, PILLOW_E2),
                b * _pw(ce, PILLOW_E1) * _pw(so, PILLOW_E2),
                c * _pw(se, PILLOW_E1) + dz)
    ck._fin(bag, "white", "Bag")
    # 帯：上面の格子（x は両脇と中央の3本、y は縁から縁まで12分割）。外側は袋より BAND_LIFT 大きく、内側は袋に 2mm 沈める
    xs = (-BAND_W / 2, 0.0, BAND_W / 2)
    ts = [-1 + 2 * j / 12 for j in range(13)]

    def grid(off):
        rows = []
        for x in xs:
            row = []
            for t in ts:
                y = t * _pillow_edge(x, a + off, b + off)
                row.append((x, y, _pillow_top(x, y, a + off, b + off, c + off) + dz))
            rows.append(row)
        return rows

    bm = bmesh.new()
    vo = [[bm.verts.new(p) for p in row] for row in grid(BAND_LIFT)]
    vi = [[bm.verts.new(p) for p in row] for row in grid(-0.002)]
    nx, nt = len(xs), len(ts)
    for i in range(nx - 1):
        for j in range(nt - 1):
            bm.faces.new((vo[i][j], vo[i + 1][j], vo[i + 1][j + 1], vo[i][j + 1]))   # 上（外側）
            bm.faces.new((vi[i][j], vi[i][j + 1], vi[i + 1][j + 1], vi[i + 1][j]))   # 袋側
    for i in (0, nx - 1):                                                            # 両脇の壁
        for j in range(nt - 1):
            bm.faces.new((vo[i][j], vo[i][j + 1], vi[i][j + 1], vi[i][j]))
    for j in (0, nt - 1):                                                            # 両端の壁（縁）
        for i in range(nx - 1):
            bm.faces.new((vo[i][j], vo[i + 1][j], vi[i + 1][j], vi[i][j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new("Band")
    bm.to_mesh(me)
    bm.free()
    band = bpy.data.objects.new("Band", me)
    bpy.context.scene.collection.objects.link(band)
    ck._fin(band, "orange", "Band")
    ck.end_asset()
    return root


def ducttape():
    """ダクトテープの巻き：直径 0.16m、高さ 0.05m、灰色、穴の直径 0.06m（アポロ13号の CO2 フィルターの象徴）。"""
    root = ck.asset("Prop_DuctTape")
    _washer("Roll", 0.08, 0.03, 0.025, "gray")
    ck.end_asset()
    return root


def _washer(name, R, r, h, color, n=24):
    """輪（外径 2R、穴 2r、高さ 2h）。ck の形にないので bmesh で組み、ck._fin で部品にする（README「自分で形を作るとき」）。"""
    import bmesh
    bm = bmesh.new()

    def ring(rad, z):
        return [bm.verts.new((rad * math.cos(2 * math.pi * i / n), rad * math.sin(2 * math.pi * i / n), z))
                for i in range(n)]
    ot, ob, it, ib = ring(R, h), ring(R, -h), ring(r, h), ring(r, -h)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((ot[i], ot[j], ob[j], ob[i]))      # 外側
        bm.faces.new((it[i], it[j], ib[j], ib[i]))      # 穴の内側
        bm.faces.new((ot[i], ot[j], it[j], it[i]))      # 上
        bm.faces.new((ob[i], ob[j], ib[j], ib[i]))      # 下
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    return ck._fin(o, color, name, bevel=ck._bevel(0.02, 2 * h))


def lineup(roots):
    """4つを横に並べて1枚（目で見る確認用）。FBX は書き出し済みなので、ここで位置を動かしてよい。"""
    for i, r in enumerate(roots):
        r.location = ((i - 1.5) * 0.45, 0, 0)
    bpy.context.view_layer.update()
    lo, hi = ex.world_bounds([m for r in roots for m in ex.meshes(r)])
    _made, cam, _ = rc._helpers(lo, hi, False, False)       # 床なし・ダミーなし（ほかの物を並べて比べるだけ）
    rc._world()
    scn = bpy.context.scene
    scn.render.engine = "BLENDER_EEVEE"
    scn.render.resolution_x, scn.render.resolution_y = 1200, 600
    scn.render.resolution_percentage = 100
    scn.render.film_transparent = False
    scn.view_settings.view_transform = "Standard"; scn.view_settings.look = "None"
    rc._aim(cam, rc._corners(lo, hi), 0, 40, margin=0.6)
    out_dir = os.path.join(ex.MODELING, KIND, "renders")
    os.makedirs(out_dir, exist_ok=True)
    p = rc._render(os.path.join(out_dir, "props_lineup.png"))
    rc._small(p)
    print("[lineup]", os.path.relpath(p, ex.REPO))


ck.clear_scene()
roots = []
for make in (pen, checklist, foodpack, ducttape):
    root = make()
    ck.export_all(root, KIND)                    # 仕上げ → unity/Assets/Models/Props/・modeling/props/export/
    ck.render_check(root, KIND, floor=False)     # 床なし（浮かせる）。1.2m のダミーは大きさの比較用に残す
    roots.append(root)
    print(f"[build] {root.name}: {ck.tri_count(root)} 三角")
lineup(roots)
