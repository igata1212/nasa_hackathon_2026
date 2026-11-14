"""品質チェック（modeling-plan.md の E 章を機械で判定）。FBX を読み直して調べ、合否の表を出す。

書いた人：iizuka（作成：Fable 5.2、parts_*.json・試し書きの順番・容量の数え方：Opus-5、船室の上限と Ship/ の同居：Fable 5.2）／ 最終更新：2026-11-14

使い方：
  modeling/core/run.sh modeling/core/check.py modeling/fx                              フォルダ（その種類の FBX 全部）
  modeling/core/run.sh modeling/core/check.py unity/Assets/Models/Fx/Fx_Alarm.fbx      FBX 1つ
  modeling/core/run.sh modeling/core/check.py                                          全種類
1つでも不合格があると終了コード 1。目で見る10項目はここでは判定しない（renders/ の画像を人が見る）。
"""
import sys
sys.dont_write_bytecode = True
import bpy, json, math, os, re
from mathutils import Vector

CORE = os.path.dirname(os.path.abspath(__file__))
if CORE not in sys.path:
    sys.path.insert(0, CORE)
import export as _export          # noqa: E402
import palette as _palette        # noqa: E402

REPO, MODELING, UNITY_MODELS, KINDS = _export.REPO, _export.MODELING, _export.UNITY_MODELS, _export.KINDS
MAT_NAME = "M_Core_Palette"
EXTRA_MATS = ("M_Body_Earth", "M_Body_Moon")
ROOT_RE = re.compile(r"^(Ctrl|Console|Crew|Tank|Ship|Body|Fx)_[A-Z][A-Za-z0-9_]*$")
PART_RE = re.compile(r"^[A-Z][A-Za-z0-9_]*$")
TRI_LIMIT = {"Ctrl": 1500, "Console": 3000, "Crew": 3000, "Tank": 1500, "Ship": 6000, "Body": 2500, "Fx": 1500}
TRI_LIMIT_TANK_BROKEN = 2000       # 破れたタンク（名前に Broken を含む）
TRI_LIMIT_INTERIOR = 10000         # 船室（Ship_ で名前に Interior を含む）。art-direction.md の「船室 10,000 以下」（#40）
FBX_MAX = 500 * 1024
TOTAL_MAX = 20 * 1024 * 1024
MIN_THICK = 0.02
EPS = 1e-4


def tri_limit(name):
    pre = name.split("_")[0]
    if pre == "Tank" and "Broken" in name:
        return TRI_LIMIT_TANK_BROKEN
    if pre == "Ship" and "Interior" in name:
        return TRI_LIMIT_INTERIOR
    return TRI_LIMIT.get(pre, 1500)


def _clear():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
        for d in list(coll):
            coll.remove(d)


def _side_dirs(fbx):
    """FBX に対応する modeling/<種類>/ の候補。本番（unity/Assets/Models/<種類>/）の FBX なら modeling/<種類>/ だけ、
    試し書き（<試し書き>/unity/<種類>/）の FBX なら <試し書き>/<種類>/ を先に見る（本番の表と混ざらないように）。"""
    fbx_dir = os.path.dirname(os.path.abspath(fbx))
    kind_dir = os.path.basename(fbx_dir)
    name = os.path.splitext(os.path.basename(fbx))[0]
    # 1つの Unity のフォルダを2つの種類が使うことがある（Ship/ ← modeling/ship と modeling/interior）。
    # その FBX を書き出した種類（export/<名前>.stats.json がある方）を先にする
    kinds = [k for k, v in KINDS.items() if v == kind_dir] or [kind_dir.lower()]
    trial = None if os.path.dirname(fbx_dir) == os.path.abspath(UNITY_MODELS) else os.path.dirname(os.path.dirname(fbx_dir))
    bases = ([trial] if trial else []) + [MODELING]
    dirs = [os.path.join(b, k) for b in bases for k in kinds]
    own = [d for d in dirs if os.path.exists(os.path.join(d, "export", name + ".stats.json"))]
    return own or dirs


def _load_json(fbx, rel):
    for d in _side_dirs(fbx):
        p = os.path.join(d, rel)
        if os.path.exists(p):
            with open(p, encoding="utf-8") as f:
                return json.load(f)
    return None


def _load_parts(fbx):
    """可動部の表。parts.json と parts_*.json（操作部品の parts_a.json・parts_b.json など）を全部読む。
    最初に見つかったフォルダのものを使う。同じルートが2つのファイルにあって中身が違うときは、その旨を返す。"""
    for d in _side_dirs(fbx):
        fs = sorted(f for f in (os.listdir(d) if os.path.isdir(d) else [])
                    if f == "parts.json" or (f.startswith("parts_") and f.endswith(".json")))
        if not fs:
            continue
        data, src, clash = {}, {}, []
        for f in fs:
            with open(os.path.join(d, f), encoding="utf-8") as fh:
                for k, v in json.load(fh).items():
                    if k in data and data[k] != v:
                        clash.append(f"{k}（{src[k]} と {f} で違う）")
                    data.setdefault(k, v); src.setdefault(k, f)
        return data, src, clash
    return {}, {}, []


def _fmt(v):
    return "(" + ", ".join(f"{x:.3f}" for x in v) + ")"


def check_fbx(fbx):
    """返り値は [(項目, 合否, 値の説明)]。"""
    name = os.path.splitext(os.path.basename(fbx))[0]
    _clear()
    bpy.ops.import_scene.fbx(filepath=fbx, use_custom_normals=True)
    bpy.context.view_layer.update()
    objs = list(bpy.data.objects)
    tops = [o for o in objs if o.parent is None]
    root = bpy.data.objects.get(name)
    rows = []
    if root is None or len(tops) != 1 or tops[0] is not root:
        return [("ルート", False, f"ルートは FBX と同じ名前の1つだけ: {[o.name for o in tops]}")]
    ms = _export.meshes(root)
    lo, hi = _export.world_bounds(ms)
    size = hi - lo
    pre = name.split("_")[0]

    # 1 三角数
    tris = sum(_export.tri_count(o) for o in ms)
    lim = tri_limit(name)
    rows.append(("三角数", tris <= lim, f"{tris:,} / {lim:,}"))

    # 2 スケール・寸法
    bad = [o.name for o in objs if any(abs(s - 1) > EPS for s in o.matrix_world.to_scale())]
    msg = [f"寸法 {_fmt(size)} m"]
    ok = not bad
    if bad:
        msg.append(f"スケールが1でない: {bad}")
    if pre == "Crew":
        ok &= abs(hi.z - 1.20) <= 0.02; msg.append(f"高さ {hi.z:.3f}（1.20±0.02）")
    if pre == "Console":
        top = bpy.data.objects.get(name + ".Top")
        if top is None:
            ok = False; msg.append("天板の部品 .Top がない")
        else:
            tz = _export.world_bounds([top])[1].z
            ok &= abs(tz - 0.80) <= 0.01; msg.append(f"天板 {tz:.3f}（0.80±0.01）")
    if pre == "Ctrl":
        base = bpy.data.objects.get(name + ".Base")
        if base is None:
            ok = False; msg.append("台座の部品 .Base がない（ck.ctrl_base を使う）")
        else:
            blo, bhi = _export.world_bounds([base])
            w, d = bhi.x - blo.x, bhi.y - blo.y
            good = all(v > 0.1 and abs(v / 0.2 - round(v / 0.2)) * 0.2 <= 0.001 for v in (w, d))
            ok &= good; msg.append(f"台座 {w:.3f}×{d:.3f}（0.2 の倍数）")
    rows.append(("スケール", ok, "、".join(msg)))

    # 3 パレット
    pal = _palette.table()
    cell = 1 / _palette.GRID
    lamp_cells = {tuple(pal[c]["cell"]) for c in _palette.LAMP_COLORS}
    cell_name = {tuple(v["cell"]): k for k, v in pal.items()}
    errs, used = [], set()
    for o in ms:
        names = [m.name if m else "(なし)" for m in o.data.materials]
        if len(names) != 1 or not (names[0] == MAT_NAME or (pre == "Body" and names[0] in EXTRA_MATS)):
            errs.append(f"{o.name}: マテリアル {names}"); continue
        if names[0] != MAT_NAME:
            continue
        uv = o.data.uv_layers.active
        if uv is None:
            errs.append(f"{o.name}: UV がない"); continue
        cells, far = set(), 0
        for d in uv.data:
            u, v = d.uv
            col, row = min(int(u / cell), 3), min(int((1 - v) / cell), 3)
            cu, cv = (col + 0.5) * cell, 1 - (row + 0.5) * cell
            if abs(u - cu) > cell / 4 + 1e-6 or abs(v - cv) > cell / 4 + 1e-6 or not (0 <= u <= 1 and 0 <= v <= 1):
                far += 1
            cells.add((col, row))
        if far:
            errs.append(f"{o.name}: マス中心から 1/4 マスより遠い UV が {far} 個")
        if cells & lamp_cells and ".Lamp_" not in o.name:
            errs.append(f"{o.name}: Lamp_ でないのにランプ色")
        used |= cells
    rows.append(("パレット", not errs, "; ".join(errs) if errs else
                 f"{MAT_NAME} だけ、使った色 {', '.join(sorted(cell_name[c] for c in used))}"))

    # 4 原点
    errs, msg = [], []
    rp = root.matrix_world.translation
    if rp.length > 0.001:
        errs.append(f"ルートが原点にない {_fmt(rp)}")
    if pre != "Body":                       # 地球・月は中心が原点
        if abs(lo.z) > 0.001:
            errs.append(f"底面 z={lo.z:.4f}（0±0.001）")
        msg.append(f"底面 z={lo.z:.4f}")
    else:
        c = (lo + hi) / 2
        if c.length > 0.001:
            errs.append(f"球の中心が原点にない {_fmt(c)}")
        msg.append("中心が原点")
    table, src, clash = _load_parts(fbx)
    parts = table.get(name)
    errs += [f"可動部の表が食い違う: {c}" for c in clash if c.startswith(name + "（")]
    if parts is None:
        msg.append("可動部なし（parts*.json に記載なし）")
    else:
        for p in parts:
            o = bpy.data.objects.get(p["name"])
            if o is None or o is root:
                errs.append(f"parts.json の {p['name']} が別オブジェクトになっていない"); continue
            d = (o.matrix_world.translation - Vector(p["origin"])).length
            if d > 0.001:
                errs.append(f"{p['name']}: 原点が軸から {d * 1000:.1f}mm ずれている")
            if o.type != "MESH" and not o.children:
                errs.append(f"{p['name']}: 動かす形がない")
        msg.append(f"可動部 {len(parts)} 個の原点が {src[name]} と一致")
    rows.append(("原点", not errs, "; ".join(errs) if errs else "、".join(msg)))

    # 5 命名
    errs = []
    if not ROOT_RE.match(name):
        errs.append(f"ルート {name}")
    for o in objs:
        if o is root:
            continue
        anc, a = set(), o.parent
        while a:
            anc.add(a.name); a = a.parent
        if re.search(r"\.\d{3}$", o.name):
            errs.append(f"{o.name}（.001 形式）")
        elif ROOT_RE.match(o.name):
            pass                                             # 入れ子のアセット（例：Ship_Stack の下の Ship_CM）
        elif not (o.name.count(".") == 1 and o.name.split(".")[0] in anc and PART_RE.match(o.name.split(".")[1])
                  and ROOT_RE.match(o.name.split(".")[0])):
            errs.append(o.name)
    rows.append(("命名", not errs, "ルール外: " + ", ".join(errs) if errs else f"{len(objs)} 個とも規則どおり"))

    # 6 太さ（部品ごとの外形のいちばん薄い向き）
    errs, thin = [], 1e9
    for o in ms:
        xs = [v.co for v in o.data.vertices]
        dims = [max(c[i] for c in xs) - min(c[i] for c in xs) for i in range(3)]
        t = min(dims); thin = min(thin, t)
        if t < MIN_THICK - EPS:
            errs.append(f"{o.name} {t * 100:.1f}cm")
    rows.append(("太さ", not errs, "2cm 未満: " + ", ".join(errs) if errs else f"最小 {thin * 100:.1f}cm"))

    # 7 容量
    sz = os.path.getsize(fbx)
    rows.append(("容量", sz <= FBX_MAX, f"FBX {sz / 1024:.0f}KB / 500KB"))

    # 8 再取り込み
    st = _load_json(fbx, os.path.join("export", name + ".stats.json"))
    if st is None:
        rows.append(("再取り込み", False, "export/<名前>.stats.json がない（ck.export_all で書き出す）"))
    else:
        errs = []
        if st["tris"] != tris:
            errs.append(f"三角数 {st['tris']} → {tris}")
        if (Vector(st["size"]) - size).length > 0.001 or (Vector(st["min"]) - lo).length > 0.001:
            errs.append(f"寸法 {st['size']} → {_fmt(size)}")
        if set(st["objects"]) != {o.name for o in objs}:
            errs.append(f"部品の過不足 {sorted(set(st['objects']) ^ {o.name for o in objs})}")
        else:
            for n, v in st["objects"].items():
                d = (bpy.data.objects[n].matrix_world.translation - Vector(v["origin"])).length
                if d > 0.001:
                    errs.append(f"{n} の原点が {d * 1000:.1f}mm ずれた")
        rows.append(("再取り込み", not errs, "; ".join(errs) if errs else
                     f"三角数 {tris:,}・寸法・部品 {len(objs)} 個の原点が書き出し時と同じ"))
    return rows


SKIP_MODELING = ("PhysDraft-SpaceCrew3D", "source")    # midori の物、ダウンロードした元データ（下敷き。上限の対象外）


def total_size():
    """#34 で足したファイルの合計（FBX・glb・レンダー・テクスチャなど）。midori のフォルダと NASA の FBX、
    modeling/source/（ダウンロードした元データ）、.blend は数えない。"""
    total = 0
    roots = [os.path.join(MODELING, d) for d in os.listdir(MODELING) if d not in SKIP_MODELING]
    roots += [os.path.join(UNITY_MODELS, d) for d in os.listdir(UNITY_MODELS) if d not in ("NASA", "NASA.meta")]
    for r in roots:
        if os.path.isfile(r):
            total += os.path.getsize(r); continue
        for dp, _, fs in os.walk(r):
            total += sum(os.path.getsize(os.path.join(dp, f)) for f in fs if not f.endswith(".blend"))
    return total


def targets(argv):
    out = []
    for a in argv or [os.path.join(MODELING, k) for k in KINDS]:
        p = os.path.abspath(a)
        if p.lower().endswith(".fbx"):
            out.append(p); continue
        kind = os.path.basename(p.rstrip("/"))
        # 渡されたフォルダに近いものから探す：試し書き（<試し書き>/<種類> → <試し書き>/unity/<種類>）、
        # FBX のフォルダそのもの、最後に本番（modeling/<種類> → unity/Assets/Models/<種類>）
        cands = [os.path.join(os.path.dirname(p), "unity", KINDS.get(kind, kind)), p,
                 os.path.join(UNITY_MODELS, KINDS.get(kind, kind))]
        shared = sum(1 for v in KINDS.values() if v == KINDS.get(kind)) > 1
        for d in cands:
            fs = sorted(f for f in (os.listdir(d) if os.path.isdir(d) else []) if f.lower().endswith(".fbx"))
            if shared and d != p:          # Ship/ は外観（modeling/ship）と船室（modeling/interior）が同居。渡されたフォルダが書き出した物だけ見る
                fs = [f for f in fs if os.path.exists(os.path.join(p, "export", f[:-4] + ".stats.json"))]
            if fs:
                out += [os.path.join(d, f) for f in fs]; break
    return out


def main(argv):
    fbxs = targets(argv)
    if not fbxs:
        print("[check] FBX が見つからない:", argv); sys.exit(1)
    fails, lines = 0, []
    for fbx in fbxs:
        rows = check_fbx(fbx)
        bad = sum(1 for _, ok, _ in rows if not ok)
        fails += bad
        lines.append(f"\n## {os.path.splitext(os.path.basename(fbx))[0]}  （{os.path.relpath(fbx, REPO)}）"
                     f"  {'全項目合格' if not bad else f'不合格 {bad} 件'}")
        lines.append("| 項目 | 結果 | 値 |\n|---|---|---|")
        lines += [f"| {k} | {'合格' if ok else '不合格'} | {v} |" for k, ok, v in rows]
    tot = total_size()
    ok = tot <= TOTAL_MAX
    fails += 0 if ok else 1
    lines.append(f"\n合計容量：{'合格' if ok else '不合格'}（{tot / 1024 / 1024:.2f}MB / 20MB。modeling/ と unity/Assets/Models/ の #34 追加分。modeling/source/ は数えない）")
    lines.append(f"判定：{len(fbxs)} 個を調べて " + ("全部合格" if not fails else f"不合格 {fails} 件"))
    print("\n".join(lines))
    sys.exit(1 if fails else 0)


if __name__ == "__main__":
    main(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
