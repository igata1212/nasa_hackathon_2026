# 書いた人：iizuka（下書き：Fable 5.2）／ 最終更新：2026-11-14
#
# NASA の 3D モデル（下敷き）から主要寸法を測る。読むだけで、何も上書きしない。
#
# 実行（リポジトリ直下で）:
#   /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup \
#       --python modeling/ship/measure.py
# 出力: 画面に表、同じ内容を modeling/ship/measure_out.json に書く。
#
# 測る対象:
#   modeling/PhysDraft-SpaceCrew3D/nasa_source/Apollo Soyuz.glb        （x < 0 側がアポロ司令船・機械船）
#   modeling/PhysDraft-SpaceCrew3D/nasa_source/Apollo Lunar Module.glb
#   modeling/source/nasa/Astronaut.glb
#
# 数値はモデルの単位のまま（glb を Blender に読んだ値。Z が上）。
import sys
sys.dont_write_bytecode = True

import json
import math
import os
from collections import defaultdict

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
MODELING = os.path.dirname(HERE)
SRC_MIDORI = os.path.join(MODELING, "PhysDraft-SpaceCrew3D", "nasa_source")
SRC_NASA = os.path.join(MODELING, "source", "nasa")
OUT = os.path.join(HERE, "measure_out.json")


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)
    us = bpy.context.scene.unit_settings
    print("[load] %s  unit=%s scale_length=%s" % (os.path.basename(path), us.system, us.scale_length))
    return [o for o in bpy.context.scene.objects if o.type == "MESH"]


def world_verts(o):
    m = o.matrix_world
    return [m @ v.co for v in o.data.vertices]


def edge_samples(o, step):
    """面の辺に沿って step ごとに点を打つ（頂点がまばらな円筒でも輪郭が取れる）。材質名つき。"""
    me = o.data
    co = world_verts(o)
    for f in me.polygons:
        mat = me.materials[f.material_index].name if me.materials else ""
        vs = [co[i] for i in f.vertices]
        for a, b in zip(vs, vs[1:] + vs[:1]):
            n = max(1, int((a - b).length / step))
            for k in range(n + 1):
                yield mat, a.lerp(b, k / n)


def rng(vals):
    return [round(min(vals), 3), round(max(vals), 3)]


def r3(x):
    return round(x, 3)


# ---------------------------------------------------------------- 司令船・機械船
def measure_csm():
    o = load(os.path.join(SRC_MIDORI, "Apollo Soyuz.glb"))[0]
    by_mat = defaultdict(list)
    for mat, c in edge_samples(o, 0.03):
        if c.x < 0.0:
            by_mat[mat].append(c)
    # 材質と部位の対応（x の範囲と半径から確かめた）
    SM, SM_AFT, NOZZLE, CM = "blinn11SG", "blinn4SG", "blinn13SG", "blinn14SG"
    RCS = [k for k in by_mat if k.startswith("apollohorns")]

    sm = by_mat[SM]
    cy = (min(c.y for c in sm) + max(c.y for c in sm)) / 2
    cz = (min(c.z for c in sm) + max(c.z for c in sm)) / 2

    def rad(c):
        return math.hypot(c.y - cy, c.z - cz)

    res = {"axis": "X（+X が司令船の先端）", "axis_center_yz": [r3(cy), r3(cz)]}
    sm_r = max(rad(c) for c in sm)
    sm_x = rng([c.x for c in sm])
    aft_x = rng([c.x for c in by_mat[SM_AFT]])
    res["sm_diameter"] = r3(2 * sm_r)
    res["sm_cylinder_x"] = sm_x
    res["sm_cylinder_length"] = r3(sm_x[1] - sm_x[0])
    res["sm_aft_shield_x"] = aft_x
    res["sm_aft_shield_diameter"] = r3(2 * max(rad(c) for c in by_mat[SM_AFT]))
    res["sm_length_with_aft_shield"] = r3(sm_x[1] - aft_x[0])

    nz = by_mat[NOZZLE]
    nz_x = rng([c.x for c in nz])
    exit_r = max(rad(c) for c in nz if c.x < nz_x[0] + 0.05)
    res["nozzle_x"] = nz_x
    res["nozzle_length_total"] = r3(nz_x[1] - nz_x[0])
    res["nozzle_length_outside_sm"] = r3(aft_x[0] - nz_x[0])
    res["nozzle_exit_diameter"] = r3(2 * exit_r)
    res["nozzle_throat_diameter"] = r3(2 * min(
        max(rad(c) for c in nz if abs(c.x - x0) < 0.05)
        for x0 in [nz_x[0] + 0.1 * i for i in range(int((nz_x[1] - nz_x[0]) / 0.1) + 1)]
        if any(abs(c.x - x0) < 0.05 for c in nz)))

    cm = by_mat[CM]
    cm_x = rng([c.x for c in cm])
    cm_r = max(rad(c) for c in cm)
    base_x = max(c.x for c in cm if rad(c) > cm_r - 0.01)   # 一番太い所（底面の縁）
    res["cm_x"] = cm_x
    res["cm_base_diameter"] = r3(2 * cm_r)
    res["cm_base_edge_x"] = r3(base_x)
    res["cm_height_base_edge_to_apex"] = r3(cm_x[1] - base_x)
    res["cm_height_total_with_heatshield_bulge"] = r3(cm_x[1] - cm_x[0])
    # 側面の傾き（軸からの角度）: 底面の縁と、先端の手前の2点から
    x_hi = cm_x[1] - 0.5
    r_hi = max(rad(c) for c in cm if abs(c.x - x_hi) < 0.03)
    res["cm_side_half_angle_deg"] = round(math.degrees(math.atan2(cm_r - r_hi, x_hi - base_x)), 1)

    rcs = [c for k in RCS for c in by_mat[k]]
    out = [c for c in rcs if rad(c) > sm_r + 0.05]
    res["rcs_x"] = rng([c.x for c in out])          # 外板から出ている部分だけ
    res["rcs_max_radius"] = r3(max(rad(c) for c in rcs))
    res["rcs_stickout_from_sm_skin"] = r3(max(rad(c) for c in rcs) - sm_r)
    res["rcs_center_from_sm_top"] = r3(sm_x[1] - (res["rcs_x"][0] + res["rcs_x"][1]) / 2)
    hist = defaultdict(int)
    for c in out:
        hist[int(round(math.degrees(math.atan2(c.z - cz, c.y - cy)) / 15.0)) * 15 % 360] += 1
    res["rcs_angle_histogram_deg(yz平面, 15度きざみ)"] = dict(sorted(hist.items()))

    res["csm_length_nozzle_exit_to_cm_apex"] = r3(cm_x[1] - nz_x[0])
    # 輪郭（0.25 ごとの最大半径）
    prof = defaultdict(float)
    for k in (SM, SM_AFT, NOZZLE, CM):
        for c in by_mat[k]:
            b = math.floor(c.x / 0.25) * 0.25
            prof[b] = max(prof[b], rad(c))
    res["profile_x_rmax"] = [[r3(b), r3(prof[b])] for b in sorted(prof)]
    return res


# ---------------------------------------------------------------- 月着陸船
def measure_lm():
    objs = load(os.path.join(SRC_MIDORI, "Apollo Lunar Module.glb"))
    V = {o.name: world_verts(o) for o in objs}
    allv = [c for cs in V.values() for c in cs]

    def bbox(names):
        cs = [c for n in names for c in V[n]]
        return {"x": rng([c.x for c in cs]), "y": rng([c.y for c in cs]), "z": rng([c.z for c in cs]),
                "r_max": r3(max(math.hypot(c.x, c.y) for c in cs))}

    res = {"axis": "Z が上。はしごは -Y 側", "objects": len(objs)}
    res["total_z"] = rng([c.z for c in allv])
    res["total_height_with_antennas"] = r3(res["total_z"][1] - res["total_z"][0])

    # 足の皿（一番外・一番下の輪）
    pads_name = min(V, key=lambda n: (min(c.z for c in V[n]), -len(V[n])))
    pads = V[pads_name]
    pr = [math.hypot(c.x, c.y) for c in pads]
    far = pads[pr.index(max(pr))]
    res["footpad_object"] = pads_name
    res["footpad_z"] = rng([c.z for c in pads])
    res["footpad_r_inner_outer"] = rng(pr)
    res["footpad_diameter"] = r3(max(pr) - min(pr))
    res["footpad_span_outer_edge_to_outer_edge"] = r3(2 * max(pr))
    res["footpad_span_center_to_center"] = r3(max(pr) + min(pr))
    res["leg_direction_deg"] = round(math.degrees(math.atan2(far.y, far.x)) % 90, 0)

    # 下段の箱: 半径 2.3 以内・z 0.8〜2.4 に収まる物体のうち、幅 4 以上のもの
    desc = [n for n, cs in V.items()
            if max(c.x for c in cs) - min(c.x for c in cs) > 3.9
            and max(math.hypot(c.x, c.y) for c in cs) < 2.3
            and min(c.z for c in cs) > 0.8 and max(c.z for c in cs) < 2.45]
    d = bbox(desc)
    res["descent_objects"] = sorted(desc)
    res["descent_body"] = d
    res["descent_width_across_flats"] = r3(d["x"][1] - d["x"][0])
    res["descent_corner_radius"] = d["r_max"]
    res["descent_height"] = r3(d["z"][1] - d["z"][0])

    # 下段エンジンのノズル: 軸上・下段の下にある小さな物体
    noz = [n for n, cs in V.items()
           if max(math.hypot(c.x, c.y) for c in cs) < 0.8 and max(c.z for c in cs) < 1.3
           and max(c.x for c in cs) - min(c.x for c in cs) > 0.5]
    if noz:
        nb = bbox(noz)
        res["descent_nozzle_objects"] = sorted(noz)
        res["descent_nozzle_z"] = nb["z"]
        res["descent_nozzle_exit_diameter"] = r3(nb["x"][1] - nb["x"][0])
        res["descent_nozzle_clearance_above_pads"] = r3(nb["z"][0] - res["total_z"][0])

    # 上段: 下段の天面より上で、幅 0.9 以上の物体（アンテナ・小物を除く）
    top_of_desc = d["z"][1]
    asc = [n for n, cs in V.items()
           if min(c.z for c in cs) > top_of_desc - 0.25
           and max(max(c.x for c in cs) - min(c.x for c in cs), max(c.y for c in cs) - min(c.y for c in cs)) > 0.9
           and max(c.z for c in cs) - min(c.z for c in cs) > 0.9]
    a = bbox(asc)
    res["ascent_objects"] = sorted(asc)
    res["ascent_body"] = a
    res["ascent_width_x"] = r3(a["x"][1] - a["x"][0])
    res["ascent_depth_y"] = r3(a["y"][1] - a["y"][0])
    res["ascent_height"] = r3(a["z"][1] - a["z"][0])
    res["height_pads_to_ascent_top"] = r3(a["z"][1] - res["total_z"][0])

    # 高さごとの外形（0.25 ごと）
    prof = defaultdict(list)
    for o in objs:
        for _, c in edge_samples(o, 0.05):
            prof[math.floor(c.z / 0.25) * 0.25].append(c)
    res["profile_z_xrange_yrange"] = [
        [r3(b), rng([c.x for c in cs]), rng([c.y for c in cs])] for b, cs in sorted(prof.items())]
    return res


# ---------------------------------------------------------------- 宇宙服
def measure_astronaut():
    o = load(os.path.join(SRC_NASA, "Astronaut.glb"))[0]
    pts = [c for _, c in edge_samples(o, 0.5)]
    res = {"axis": "Z が上。+Y が背中。腕は横に広げた姿勢"}
    zs = [c.z for c in pts]
    H = max(zs) - min(zs)
    z0 = min(zs)
    res["bbox"] = {"x": rng([c.x for c in pts]), "y": rng([c.y for c in pts]), "z": rng(zs)}
    res["height"] = r3(H)

    # 胴の背中の面: 背中の装置より下（腰〜もも）で |x| < 6 の一番うしろ
    hip = [c for c in pts if 0.20 * H < c.z - z0 < 0.32 * H and abs(c.x) < 6]
    back_y = max(c.y for c in hip)
    front_y_hip = min(c.y for c in hip)
    res["torso_back_surface_y"] = r3(back_y)
    res["torso_depth_at_hip"] = r3(back_y - front_y_hip)

    # 背中の装置: 背中の面より 6 以上うしろの点（ヘルメットの後ろ頭は 5 ほど出るので除く）
    pack = [c for c in pts if c.y > back_y + 6.0]
    pz = rng([c.z for c in pack])
    px = rng([c.x for c in pack])
    res["pack_z"] = pz
    res["pack_height"] = r3(pz[1] - pz[0])
    res["pack_width"] = r3(px[1] - px[0])
    res["pack_depth_behind_torso"] = r3(max(c.y for c in pack) - back_y)
    res["pack_bottom_from_feet"] = r3(pz[0] - z0)
    res["pack_top_from_feet"] = r3(pz[1] - z0)

    # ヘルメット: 上 12% で、背中の装置を除く（y < back_y+6）
    top = [c for c in pts if c.z - z0 > 0.88 * H and c.y < back_y + 6.0]
    res["helmet_width"] = r3(max(c.x for c in top) - min(c.x for c in top))
    res["helmet_depth"] = r3(max(c.y for c in top) - min(c.y for c in top))
    res["ratio_helmet_width_to_body_height"] = round(res["helmet_width"] / H, 3)
    # 肩幅: 腕を除くため、胸の高さで腕の付け根より内側は取りにくい。腰の幅で代用
    waist = [c for c in pts if 0.40 * H < c.z - z0 < 0.48 * H and c.y < back_y + 1.0]
    res["waist_width"] = r3(max(c.x for c in waist) - min(c.x for c in waist))
    chest = [c for c in pts if 0.60 * H < c.z - z0 < 0.75 * H and abs(c.x) < 6]
    res["chest_front_y"] = r3(min(c.y for c in chest))
    res["body_depth_at_chest_with_pack"] = r3(max(c.y for c in chest) - min(c.y for c in chest))

    res["ratio_pack_height_to_body_height"] = round(res["pack_height"] / H, 3)
    res["ratio_pack_width_to_body_height"] = round(res["pack_width"] / H, 3)
    res["ratio_pack_depth_to_body_height"] = round(res["pack_depth_behind_torso"] / H, 3)
    res["ratio_pack_bottom_to_body_height"] = round(res["pack_bottom_from_feet"] / H, 3)
    res["ratio_pack_top_to_body_height"] = round(res["pack_top_from_feet"] / H, 3)
    res["ratio_pack_width_to_waist_width"] = round(res["pack_width"] / res["waist_width"], 3)
    res["ratio_pack_depth_to_torso_depth"] = round(res["pack_depth_behind_torso"] / res["torso_depth_at_hip"], 3)
    res["if_unit_is_inch_height_m"] = round(H * 0.0254, 3)
    return res


def show(title, d):
    print("\n== %s ==" % title)
    for k, v in d.items():
        if k.startswith("profile"):
            print("  %s: (%d 行。measure_out.json を見る)" % (k, len(v)))
        else:
            print("  %-48s %s" % (k, v))


def main():
    out = {
        "blender": bpy.app.version_string,
        "csm": measure_csm(),
        "lm": measure_lm(),
        "astronaut": measure_astronaut(),
    }
    show("司令船・機械船（Apollo Soyuz.glb の x<0 側）", out["csm"])
    show("月着陸船（Apollo Lunar Module.glb）", out["lm"])
    show("宇宙服（Astronaut.glb）", out["astronaut"])
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
    print("\nwrote", OUT)


main()
