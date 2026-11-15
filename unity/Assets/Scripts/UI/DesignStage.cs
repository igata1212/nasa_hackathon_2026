// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-15
// 設計の画面の 3D の舞台（#168）：昼の発射場で、回る台に載った宇宙船に部品が「ぽん」と付く。後ろ左にロケットが立つ。
// LobbyStage と同じ作り：船室から遠く（DesignLook.Origin）に実行時に組み、専用のカメラで RenderTexture に描いて、設計の画面の RawImage に出す。
// 設計の画面の Root（RawImage）に付けるので、画面が出ている間だけ描く（GameFlow は変えない）。
//   ・部品が動くのはサーバーの design が届いたとき（Set に渡る個数の差を見る）。4人の誰が押しても全員の画面で同じ動きになる。UI は先読みしない
//   ・何もしないと台が回る。舞台を横になぞると回せる。離して（部品が付いて）から少しすると自動に戻る
//   ・カメラは〈寄り〉（部品のタブ）と〈引き〉（FLIGHT。ロケットの全身）の2つの構え
// 値（大きさ・秒・取り付け位置の表）は DesignLook、モデルの参照は Resources/DesignStage.asset（DesignStageAssets）
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using L = DesignLook;

public class DesignStage : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    class Item
    {
        public string Id;
        public int Index;
        public Transform T;
        public Vector3 Pos, From, Out, Face, SpinAxis;   // Face：見せたい面の向き（レンズ・稲妻・パネルの面）
        public Quaternion Rot;
        public float Scale, Age;
        public bool Landed;
    }

    RawImage view;
    DesignStageAssets assets;
    Transform root, turn, holder;
    Camera cam;
    RenderTexture rt;
    Renderer rim;
    MaterialPropertyBlock mpb;
    readonly List<Item> items = new List<Item>(), leaving = new List<Item>();
    readonly HashSet<string> warned = new HashSet<string>();
    Transform rocketNow, rocketOld;
    string rocketKey;
    float rocketNowT = 99f, rocketOldT = 99f;
    bool snap = true, wide, dragging, overMass;
    float yaw, zoom, zoomFrom, zoomTo, zoomT = 99f;
    float holdUntil, faceFrom, faceTo, faceT = 99f;
    float squashT = 99f, shakeT = 99f;

    /// <summary>いま付いている部品の数（確かめる用）</summary>
    public int PartCount => items.Count;
    public float Yaw => yaw;
    /// <summary>画面が開いてからまだ Set していない（最初の1回は動かさずに置く）</summary>
    public bool Fresh => snap;

    // ================================================================== 組み立て
    public void Init(RawImage view)
    {
        this.view = view;
        assets = DesignStageAssets.Load();
        mpb = new MaterialPropertyBlock();
        root = new GameObject("DesignStage").transform;
        root.position = L.Origin;
        root.rotation = Quaternion.Euler(0f, L.StageYaw, 0f);

        Disc("Ground", root, L.GroundR, L.GroundH, L.GroundY - L.GroundH * 0.5f, L.Ground, false);
        turn = new GameObject("Turntable").transform;
        turn.SetParent(root, false);
        Disc("Table", turn, L.TableR, L.TableH, -L.TableH * 0.5f, L.Table, true);
        rim = Disc("Rim", turn, L.RimR, L.RimH, L.RimTop - L.RimH * 0.5f, L.Rim, true);

        holder = new GameObject("Ship").transform;
        holder.SetParent(turn, false);
        if (assets != null && assets.ship != null)
        {
            var s = Instantiate(assets.ship, holder);
            s.name = "DesignShip";
        }
        yaw = YawToward(L.Out(L.StartYaw), 0f);

        // 前左からの弱い明かり（Key Light の影の側を明るく）
        var fg = new GameObject("DesignFill");
        fg.transform.SetParent(root, false);
        fg.transform.localRotation = Quaternion.Euler(L.FillEuler);
        var fill = fg.AddComponent<Light>();
        fill.type = LightType.Directional; fill.intensity = L.FillIntensity; fill.color = L.FillColor; fill.shadows = LightShadows.None;

        var cg = new GameObject("DesignCamera");
        cg.transform.SetParent(root, false);
        cam = cg.AddComponent<Camera>();
        cam.fieldOfView = L.Fov; cam.nearClipPlane = 0.3f; cam.farClipPlane = L.Far;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = L.Sky;
        cam.depth = -20f; cam.allowHDR = true; cam.allowMSAA = true;
        var data = cg.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true; data.renderShadows = true;   // 打ち上げの演出（StoryCamera）と同じ。空の色がそろう

        Show(isActiveAndEnabled);
    }

    Renderer Disc(string name, Transform parent, float r, float h, float y, Color c, bool shadows)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(g.GetComponent<Collider>());
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = new Vector3(0f, y, 0f);
        g.transform.localScale = new Vector3(r * 2f, h * 0.5f, r * 2f);   // Cylinder は直径 1・高さ 2
        var rd = g.GetComponent<Renderer>();
        if (assets != null && assets.surface != null) rd.sharedMaterial = assets.surface;
        rd.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        Paint(rd, c);
        return rd;
    }

    void Paint(Renderer rd, Color c)
    {
        mpb.SetColor(BaseColor, c);
        rd.SetPropertyBlock(mpb);
    }

    // ================================================================== 外から
    /// <summary>サーバーの design が届いたとき。前の個数との差で、部品を付ける・外す・ロケットを入れ替える。
    /// 画面が開いた直後の1回は動かさずにそのまま置く</summary>
    public void Set(IDictionary<string, int> parts, string rocket, bool overMassNow)
    {
        if (root == null) return;
        bool anim = !snap && isActiveAndEnabled;
        snap = false;

        var ids = new List<string>();
        foreach (var it in items) if (!ids.Contains(it.Id)) ids.Add(it.Id);
        if (parts != null) foreach (var kv in parts) if (!ids.Contains(kv.Key)) ids.Add(kv.Key);
        Item last = null;
        foreach (var id in ids)
        {
            int want = parts != null && parts.TryGetValue(id, out var n) ? n : 0;
            int have = Count(id);
            while (have < want) { var a = Attach(id, have, anim); if (a != null) last = a; have++; if (a == null) break; }
            while (have > want) { Detach(id, anim); have--; }
        }
        if (last != null && anim) Face(last);

        SetRocket(rocket, anim);
        if (overMassNow != overMass || !anim) { overMass = overMassNow; Paint(rim, overMass ? L.RimOver : L.Rim); }
    }

    /// <summary>〈引き〉（FLIGHT のタブ）と〈寄り〉を ZoomSec で行き来する</summary>
    public void Wide(bool on)
    {
        if (on == wide) return;
        wide = on;
        zoomFrom = zoom; zoomTo = on ? 1f : 0f; zoomT = 0f;
    }

    /// <summary>打ち上げがだめだったとき、船がぶるっと揺れる</summary>
    public void Shake() => shakeT = 0f;

    int Count(string id)
    {
        int c = 0;
        foreach (var it in items) if (it.Id == id) c++;
        return c;
    }

    // ================================================================== 付ける・外す
    Item Attach(string id, int index, bool anim)
    {
        if (!L.FindMount(id, index, out var m)) { Warn("取り付け位置が無い: " + id + " #" + index); return null; }
        var prefab = assets != null ? assets.Prefab(id) : null;
        if (prefab == null) { Warn("モデルが無い: " + id); return null; }
        var go = Instantiate(prefab, holder);
        go.name = "Part_" + id + "_" + index;

        var rot = L.Rotation(m);
        var outDir = L.Out(m.Angle);
        float minOut = float.MaxValue;
        foreach (var c in Corners(LocalBounds(go))) minOut = Mathf.Min(minOut, Vector3.Dot(rot * (c * m.Scale), outDir));
        if (minOut == float.MaxValue) minOut = 0f;
        var pos = outDir * (L.HullR - L.HullSink - minOut) + Vector3.up * m.Z;

        var up = L.Axis(m.Up, m.Angle);
        var face = Mathf.Abs(up.y) < 0.5f ? up : L.Axis(m.Front, m.Angle);   // 上が横を向く部品（外向き・翼）は上の面、立てる部品は正面
        var it = new Item { Id = id, Index = index, T = go.transform, Pos = pos, Rot = rot, Scale = m.Scale, Out = outDir, Face = face, Age = anim ? 0f : L.AttachSec, Landed = !anim };
        it.From = anim ? FlyFrom() : pos;
        items.Add(it);
        Pose(it);
        return it;
    }

    void Detach(string id, bool anim)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (items[i].Id != id) continue;
            var it = items[i];
            items.RemoveAt(i);
            if (!anim) { Destroy(it.T.gameObject); return; }
            it.Age = 0f;
            it.Pos = it.T.localPosition;   // 付く途中で外れても、今の所から落ちる
            it.SpinAxis = Random.onUnitSphere;
            leaving.Add(it);
            GameAudio.Part(false);
            return;
        }
    }

    /// <summary>飛び始めの所：画面の右（棚のあたり）を、船と同じ奥行きで船のローカルに直したもの</summary>
    Vector3 FlyFrom()
    {
        if (cam == null) return Vector3.up * 8f;
        float depth = Vector3.Distance(cam.transform.position, holder.position + holder.up * 5f);
        var w = cam.ViewportToWorldPoint(new Vector3(L.AttachFrom.x, L.AttachFrom.y, depth));
        return holder.InverseTransformPoint(w);
    }

    /// <summary>付いた部品の見せたい面がカメラ（から FaceAngleBias ずらした向き）を向くよう、台を FaceSec で回す</summary>
    void Face(Item it)
    {
        faceFrom = yaw;
        faceTo = yaw + Mathf.DeltaAngle(yaw, YawToward(it.Face, L.FaceAngleBias));
        faceT = 0f;
        holdUntil = UiEase.Now + L.FaceSec + L.ResumeSec;
    }

    /// <summary>船のローカルの向き dir が、台の中心から見たカメラの方向（横にずらしたぶんも入れる）から bias 度ずれた向きを向く台の回転</summary>
    float YawToward(Vector3 dir, float bias)
    {
        var c = cam != null ? cam.transform.localPosition : Vector3.forward;
        var toward = Quaternion.Euler(0f, bias, 0f) * new Vector3(c.x, 0f, c.z).normalized;
        dir.y = 0f;
        return Vector3.SignedAngle(dir, toward, Vector3.up);
    }

    static Bounds LocalBounds(GameObject g)
    {
        var inv = g.transform.worldToLocalMatrix;
        bool any = false;
        var b = new Bounds();
        foreach (var mf in g.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var m = inv * mf.transform.localToWorldMatrix;
            foreach (var c in Corners(mf.sharedMesh.bounds))
            {
                var p = m.MultiplyPoint3x4(c);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
        }
        return b;
    }

    static IEnumerable<Vector3> Corners(Bounds b)
    {
        for (int i = 0; i < 8; i++)
            yield return new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
    }

    void Warn(string s) { if (warned.Add(s)) Debug.LogWarning("[DesignStage] " + s); }

    // ================================================================== ロケット（後ろ左）
    void SetRocket(string key, bool anim)
    {
        key = key ?? "";
        if (key == rocketKey) return;
        rocketKey = key;
        if (rocketOld != null) Destroy(rocketOld.gameObject);
        rocketOld = rocketNow; rocketOldT = 0f;
        if (!anim && rocketOld != null) { Destroy(rocketOld.gameObject); rocketOld = null; }
        rocketNow = null;
        var prefab = assets == null ? null : key == "large" ? assets.rocketLarge : key == "small" ? assets.rocketSmall : null;
        if (prefab != null)
        {
            var g = Instantiate(prefab, root);
            g.name = "DesignRocket_" + key;
            g.transform.localPosition = L.RocketPos + Vector3.up * L.GroundY;
            rocketNow = g.transform;
            rocketNowT = anim ? 0f : L.RocketInSec;
            PoseRocket();
        }
    }

    void PoseRocket()
    {
        float s = L.RocketScale;
        if (rocketNow != null)
        {
            float p = rocketNowT / L.RocketInSec;
            float y = p >= 1f ? 1f : p < 0.7f ? Mathf.Lerp(0f, L.RocketInOvershoot, UiEase.OutCubic(p / 0.7f)) : Mathf.Lerp(L.RocketInOvershoot, 1f, UiEase.InOutSine((p - 0.7f) / 0.3f));
            rocketNow.localScale = new Vector3(s, s * Mathf.Max(0.001f, y), s);   // 原点（ノズルの出口）が下なので、下から伸びる
        }
        if (rocketOld != null)
        {
            float p = rocketOldT / L.RocketOutSec;
            if (p >= 1f) { Destroy(rocketOld.gameObject); rocketOld = null; return; }
            float k = 1f - UiEase.OutCubic(p);
            rocketOld.localScale = new Vector3(s * k, s * k, s * k);
        }
    }

    // ================================================================== なぞって回す
    public void OnBeginDrag(PointerEventData e) { dragging = true; faceT = 99f; }

    public void OnDrag(PointerEventData e)
    {
        float scale = view != null && view.canvas != null ? Mathf.Max(0.01f, view.canvas.scaleFactor) : 1f;
        yaw -= e.delta.x / scale * L.DragDegPerPx;
        holdUntil = UiEase.Now + L.ResumeSec;
    }

    public void OnEndDrag(PointerEventData e) { dragging = false; holdUntil = UiEase.Now + L.ResumeSec; }

    // ================================================================== 出す・隠す
    void OnEnable() { snap = true; Show(true); }
    void OnDisable() { dragging = false; Show(false); }

    void Show(bool on)
    {
        if (root != null) root.gameObject.SetActive(on);
        if (cam != null) cam.enabled = on;
    }

    void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
        if (rt != null) { rt.Release(); Destroy(rt); }
    }

    // ================================================================== 毎フレーム
    void LateUpdate()
    {
        if (cam == null) return;
        int w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
        if (rt == null || rt.width != w || rt.height != h)
        {
            if (rt != null) { cam.targetTexture = null; rt.Release(); Destroy(rt); }
            rt = new RenderTexture(w, h, 24) { name = "DesignStage", antiAliasing = 4 };
            cam.targetTexture = rt;
            view.texture = rt;
            view.color = Color.white;
        }
        float dt = UiEase.Dt, now = UiEase.Now;

        // 台の回り方：正面へ回す途中 ＞ なぞっている ＞ 待つ ＞ 自動
        if (faceT < L.FaceSec)
        {
            faceT += dt;
            yaw = Mathf.Lerp(faceFrom, faceTo, UiEase.OutCubic(faceT / L.FaceSec));
        }
        else if (!dragging && now >= holdUntil) yaw += L.IdleSpin * dt;
        yaw = Mathf.Repeat(yaw + 180f, 360f) - 180f;
        turn.localRotation = Quaternion.Euler(0f, yaw, 0f);

        // 付く・外れる
        foreach (var it in items)
        {
            if (it.Age >= L.AttachSec && it.Landed) continue;
            it.Age += dt;
            Pose(it);
            if (!it.Landed && it.Age >= L.AttachSec) { it.Landed = true; squashT = 0f; GameAudio.Part(true); }
        }
        for (int i = leaving.Count - 1; i >= 0; i--)
        {
            var it = leaving[i];
            it.Age += dt;
            float p = it.Age / L.DetachSec;
            if (p >= 1f || it.T == null) { if (it.T != null) Destroy(it.T.gameObject); leaving.RemoveAt(i); continue; }
            it.T.localPosition = it.Pos + it.Out * (L.DetachOut * p) + Vector3.up * (L.DetachHop * (2.2f * p - 3.2f * p * p));
            it.T.localRotation = Quaternion.AngleAxis(L.DetachSpin * p, it.SpinAxis) * it.Rot;
            float k = it.Scale * (1f - p * p);
            it.T.localScale = new Vector3(k, k, k);
        }

        // 船：付いた瞬間につぶれる・だめなときに揺れる
        float sq = 1f;
        if (squashT < L.SquashSec) { squashT += dt; sq = Mathf.Lerp(L.SquashScale, 1f, UiEase.OutBack(squashT / L.SquashSec)); }
        holder.localScale = new Vector3(2f - sq, sq, 2f - sq);
        float sx = 0f;
        if (shakeT < L.ShakeSec) { shakeT += dt; float p = shakeT / L.ShakeSec; sx = L.ShakeAmp * Mathf.Sin(p * L.ShakeSec * L.ShakeHz * Mathf.PI * 2f) * (1f - p); }
        holder.localPosition = new Vector3(sx, 0f, 0f);

        // ロケットの入れ替え。〈寄り〉のときは隠す（横長の画面だと端に切れて見えるため。引きはじめると出る）
        rocketNowT += dt; rocketOldT += dt;
        PoseRocket();
        bool showRocket = zoom > 0.01f || zoomTo > 0.5f;
        if (rocketNow != null && rocketNow.gameObject.activeSelf != showRocket) rocketNow.gameObject.SetActive(showRocket);
        if (!showRocket && rocketOld != null) { Destroy(rocketOld.gameObject); rocketOld = null; }

        // カメラ
        if (zoomT < L.ZoomSec) { zoomT += dt; zoom = Mathf.Lerp(zoomFrom, zoomTo, UiEase.InOutSine(zoomT / L.ZoomSec)); }
        PlaceCamera();
    }

    void Pose(Item it)
    {
        float p = Mathf.Clamp01(it.Age / L.AttachSec);
        if (p >= 1f)
        {
            it.T.localPosition = it.Pos; it.T.localRotation = it.Rot;
            it.T.localScale = new Vector3(it.Scale, it.Scale, it.Scale);
            return;
        }
        // 棚から弧を描いて飛ぶ。大きさ 0 → AttachOvershoot → 1
        var pos = Vector3.Lerp(it.From, it.Pos, UiEase.OutCubic(p)) + Vector3.up * (L.AttachArc * UiEase.Bump(p));
        float k = p < 0.7f ? Mathf.Lerp(0.05f, L.AttachOvershoot, UiEase.OutCubic(p / 0.7f)) : Mathf.Lerp(L.AttachOvershoot, 1f, UiEase.InOutSine((p - 0.7f) / 0.3f));
        it.T.localPosition = pos;
        it.T.localRotation = it.Rot;
        it.T.localScale = Vector3.one * (it.Scale * k);
    }

    /// <summary>2つの構えの間（zoom 0＝寄り、1＝引き）。船の中心を画面の左寄り（ShipScreenX px）に置く（カメラを横にずらす）</summary>
    void PlaceCamera()
    {
        float k = zoom;
        float dist = Mathf.Lerp(L.CloseDist, L.WideDist, k);
        float pitch = Mathf.Lerp(L.ClosePitch, L.WidePitch, k);
        float lookY = Mathf.Lerp(L.CloseLookY, L.WideLookY, k);
        float lookX = Mathf.Lerp(0f, L.WideLookX, k);
        float fy = Mathf.Lerp(L.CloseScreenY, L.WideScreenY, k);
        float canvasW = view != null ? Mathf.Max(1f, view.rectTransform.rect.width) : 1600f;
        float fx = Mathf.Clamp01(L.ShipScreenX / canvasW);

        float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad), tanH = tanV * cam.aspect;
        var rot = Quaternion.Euler(pitch, 180f, 0f);   // 舞台のローカル +Z から −Z（船）を見る
        var target = new Vector3(lookX, lookY, 0f);
        var pos = target - rot * Vector3.forward * dist
                + rot * Vector3.right * ((0.5f - fx) * 2f * dist * tanH)
                + rot * Vector3.up * ((0.5f - fy) * 2f * dist * tanV);
        cam.transform.localPosition = pos;
        cam.transform.localRotation = rot;
    }
}
