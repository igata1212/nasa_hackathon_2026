// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// 船室の窓の外の地球・月を、GMAT の軌道に合わせて動かす（#126）。
//   宇宙船の位置：ゲームの t（事故からの秒）と route から GmatRoute で求める（TrajectoryView と同じ。EarthMoonRot：地球が原点、月は +X）
//   見かけ：本物の向き（窓の正面からの角度）と見かけの半径（asin(半径/距離)）を、窓の幅に入るよう縮め・太らせて、窓の穴の向こうに球を置く
//     窓の正面：地球と月への向きを、見かけの大きさで重み付けして足した向き（大きく見える方へ寄る）
//     往路：月が大きくなり、地球は小さく窓の端へ → 月の裏側：月が窓いっぱい、地球は月の後ろ（GMAT の LOS・AOS と同じ幾何）→ 帰還：地球が大きく
//   再突入：state の t_reentry から、窓の外の板（Sprites/Default。実行時に作る）をオレンジに光らせ、船内にオレンジの点光源を足す
// GameFlow は変えない：flow.Model.State を毎フレーム読むだけ。共有マテリアル（M_Core_Palette）は書き換えない。
// 置き方：船室のカメラ（Cam_Top）から窓のまん中を通る線のまわり、カメラから nearDepth / farDepth m（カメラの far 100 m の内）。
//   球の上の縁は、壁の上端より上（屋根がないので空が見える所）に出さない。数値は見た目だけ（軌道は GMAT のデータ）。
// 組み立ては Editor/WindowView/WindowViewScene.cs（Tools/GameFlow/シーンを作り直す から呼ばれる）。
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class WindowView : MonoBehaviour
{
    [Header("つなぐもの（Editor/WindowView/WindowViewScene.cs が入れる）")]
    public GameFlow flow;
    public GmatRoute routeA, routeB;
    public Camera viewCamera;            // 船室を撮るカメラ（Cam_Top）。空なら Camera.main
    public Transform window;             // Slot_Window（窓のまん中、壁の外側の面）
    public Transform shipCenter;         // Slot_Center（窓の外向きを決める）
    public Transform earth, moon;        // Body_Earth・Body_Moon（大きさと位置はここが毎フレーム決める）

    [Header("窓の穴（modeling/interior/build_interior.py の WIN_*・WALL_TOP。m）")]
    public float windowHalfWidth = 0.75f;
    public float windowHalfHeight = 0.29f;
    public float wallAboveWindow = 0.13f;   // 窓の上端から壁の上端まで。これより上は空なので、球をはみ出させない

    [Header("見せ方（見た目だけ。窓の半幅を 1 とした単位）")]
    public float fieldHalfDeg = 110f;    // 本物の角度で、窓の端（±1）に来る角度
    public float sizeGain = 0.2f;        // 窓の上の半径 = sizeGain × (見かけの半径・度)^sizePower
    public float sizePower = 0.75f;
    public float minSize = 0.12f;        // 遠くても点にしない
    public float maxAngleDeg = 35f;      // カメラから見た球の半径の上限（球が船室に食い込まない）
    public float focusPower = 1f;        // 窓の正面を、大きく見える方へ寄せる強さ
    public bool mirror;                  // 左右を入れ替える
    public float nearDepth = 50f, farDepth = 70f;   // カメラから球の中心まで（m）。本当に近い方の天体を手前に置く
    public float earthSpinDegPerSec = 2f;
    public float smoothRate = 4f;        // t（毎秒届く）をなめらかにする速さ（1/秒）

    [Header("再突入の光（見た目だけ。t_reentry からの秒）")]
    public Color glowColor = new Color(1f, 0.45f, 0.1f);
    public float glowIntensity = 2.5f;   // HDR。Bloom（しきい値 0.9）で窓のまわりもにじむ
    public float glowMaxAlpha = 0.85f;   // 1 だと地球が見えなくなる
    public float glowRampIn = 4f, glowHold = 30f, glowFadeOut = 12f;
    public float glowFlicker = 0.15f;
    public float glowOffset = 0.1f;      // 壁の外側の面からの距離（m）
    public float cabinLightIntensity = 3f, cabinLightRange = 5f;

    [Header("確認用（Play 中にインスペクターで動かす）")]
    public bool debugOverride;
    public float debugT;
    public string debugRoute = "";

    [Header("今の値（読むだけ）")]
    public float currentT;
    public float earthDistanceKm, moonDistanceKm;
    public bool earthBehindMoon, moonBehindEarth;
    public float glow;

    const float EarthRealKm = 6378.14f, MoonRealKm = 1737.4f;   // 本物の半径（TrajectoryView と同じ）

    class Body
    {
        public Transform t;
        public Renderer[] rs;
        public float unitR;      // 拡大 1 のときの半径（m）
        public Vector3 off;      // 拡大 1 のときの、原点から球の中心まで（局所）
        public int shown = -1;   // -1：まだ決めていない
    }

    Body bE, bM;
    Quaternion earthBase;
    float spin, tView = float.NaN;
    bool ready;

    Transform glowT;
    Renderer glowR;
    Material glowMat;
    Mesh glowMesh;
    Light glowLight;

    // 毎フレームの窓の幾何（カメラから見た角度。単位はラジアン、窓の半幅の角度 = halfW）
    Vector3 camPos, d0, camRight, camUp;
    float halfW, bottomY, skyY;

    void Awake()
    {
        if (viewCamera == null) viewCamera = Camera.main;
        if (routeA == null || earth == null || moon == null || window == null || viewCamera == null)
        {
            Debug.LogWarning("[WindowView] つなぐものが足りない（Tools/GameFlow/シーンを作り直す で入る）。窓の外は止まったまま");
            enabled = false;
            return;
        }
        if (routeB == null) routeB = routeA;
        bE = MakeBody(earth);
        bM = MakeBody(moon);
        earthBase = earth.rotation;
        BuildGlow();
        ready = true;
    }

    void OnDestroy()
    {
        if (glowMat != null) Destroy(glowMat);
        if (glowMesh != null) Destroy(glowMesh);
    }

    void LateUpdate()
    {
        if (!ready) return;
        ReadState(out float tTarget, out string route, out float tRe);
        if (float.IsNaN(tView) || tTarget < tView - 2f) tView = tTarget;   // 最初と、やり直し（t が戻った）ときは飛ぶ
        else tView = Mathf.Lerp(tView, tTarget, 1f - Mathf.Exp(-smoothRate * Time.deltaTime));
        currentT = tView;

        // 宇宙船の位置（km、EarthMoonRot）と、地球・月への向き・距離・見かけの半径
        var r = route == "b" ? routeB : routeA;          // 噴射の前は A と B が同じ道
        var p = r.PosAtHours(r.HoursAt(tView));
        var toE = -p;
        var toM = new Vector3(r.moonCenterXKm, 0f, 0f) - p;
        float dE = Mathf.Max(toE.magnitude, 1f), dM = Mathf.Max(toM.magnitude, 1f);
        var hE = toE / dE;
        var hM = toM / dM;
        float aE = Mathf.Asin(Mathf.Clamp01(EarthRealKm / dE));
        float aM = Mathf.Asin(Mathf.Clamp01(MoonRealKm / dM));
        earthDistanceKm = dE; moonDistanceKm = dM;

        // 本物の幾何で、どちらかがもう一方の円盤の後ろにいるか（月の裏側の通信途絶と同じ）
        float sep = Mathf.Acos(Mathf.Clamp(Vector3.Dot(hE, hM), -1f, 1f));
        earthBehindMoon = dE > dM && sep < aM;
        moonBehindEarth = dM > dE && sep < aE;

        // 窓の正面と、その左右・上下（軌道面の法線 +Z を上にする）
        float wE = Mathf.Pow(aE, focusPower), wM = Mathf.Pow(aM, focusPower);
        var look = wE * hE + wM * hM;
        if (look.sqrMagnitude < 1e-6f) look = aE >= aM ? hE : hM;
        look.Normalize();
        var right = Vector3.Cross(look, new Vector3(0f, 0f, 1f));
        if (right.sqrMagnitude < 1e-6f) right = new Vector3(0f, -1f, 0f);
        right.Normalize();
        var up = Vector3.Cross(right, look);

        WindowGeometry();
        spin += earthSpinDegPerSec * Time.deltaTime;
        earth.rotation = Quaternion.AngleAxis(spin, Vector3.up) * earthBase;

        bool earthNear = dE <= dM;
        PlaceBody(bE, ToWindow(hE, aE, look, right, up), earthNear ? nearDepth : farDepth, !earthBehindMoon);
        PlaceBody(bM, ToWindow(hM, aM, look, right, up), earthNear ? farDepth : nearDepth, !moonBehindEarth);
        UpdateGlow(tRe);
    }

    void ReadState(out float t, out string route, out float tRe)
    {
        var s = flow != null ? flow.Model.State : null;
        if (debugOverride) { t = debugT; route = debugRoute; tRe = 0f; }
        else if (s != null) { t = s.t; route = s.route; tRe = s.tReentry; }
        else { t = 0f; route = ""; tRe = 0f; }   // 事故の前・設計の画面：事故の時点の景色
        route = (route ?? "").ToLowerInvariant();
        if (tRe <= 0f) tRe = (route == "b" ? routeB : routeA).EndT;   // サーバーの値が無いときは GMAT の再突入（time_map の reentry）
    }

    /// <summary>天体の向き h と見かけの半径 a（ラジアン）→ 窓の上の (x, y, 半径)。窓の半幅が 1</summary>
    Vector3 ToWindow(Vector3 h, float a, Vector3 look, Vector3 right, Vector3 up)
    {
        float f = Vector3.Dot(h, look), s = Vector3.Dot(h, right), u = Vector3.Dot(h, up);
        float x = Mathf.Atan2(s, f) * Mathf.Rad2Deg / fieldHalfDeg;
        float y = Mathf.Atan2(u, Mathf.Sqrt(s * s + f * f)) * Mathf.Rad2Deg / fieldHalfDeg;
        if (mirror) x = -x;
        float r = Mathf.Max(minSize, sizeGain * Mathf.Pow(a * Mathf.Rad2Deg, sizePower));
        return new Vector3(x, y, r);
    }

    /// <summary>カメラから見た窓の大きさ（角度）。窓の外の球はこの角度の中に置く</summary>
    void WindowGeometry()
    {
        camPos = viewCamera.transform.position;
        var w = window.position;
        d0 = (w - camPos).normalized;
        camRight = Vector3.Cross(Vector3.up, d0).normalized;
        camUp = Vector3.Cross(d0, camRight);
        var wRight = Vector3.Cross(Vector3.up, Outward()).normalized;
        halfW = Mathf.Max(1e-4f, Mathf.Abs(AngX(w + wRight * windowHalfWidth)));
        bottomY = AngY(w - Vector3.up * windowHalfHeight) / halfW;
        skyY = AngY(w + Vector3.up * (windowHalfHeight + wallAboveWindow)) / halfW;
    }

    float AngX(Vector3 p) { var v = p - camPos; return Mathf.Atan2(Vector3.Dot(v, camRight), Vector3.Dot(v, d0)); }
    float AngY(Vector3 p) { var v = p - camPos; return Mathf.Atan2(Vector3.Dot(v, camUp), Vector3.Dot(v, d0)); }

    Vector3 Outward()
    {
        var o = shipCenter != null ? window.position - shipCenter.position : d0;
        o.y = 0f;
        return o.sqrMagnitude > 1e-6f ? o.normalized : Vector3.forward;
    }

    /// <summary>窓の上の (x, y, 半径) に見えるよう、球をカメラから depth m の所に置く</summary>
    void PlaceBody(Body b, Vector3 w, float depth, bool visible)
    {
        float rAng = Mathf.Min(w.z * halfW, maxAngleDeg * Mathf.Deg2Rad);
        float rU = rAng / halfW;
        float y = Mathf.Min(w.y, skyY - rU);   // 大きい球は下げて、上の縁だけを窓に入れる（地平線のように見える）
        bool show = visible && Mathf.Abs(w.x) - rU < 1f && y + rU > bottomY;
        if ((show ? 1 : 0) != b.shown)
        {
            foreach (var r in b.rs) if (r != null) r.enabled = show;
            b.shown = show ? 1 : 0;
        }
        if (!show) return;
        var dir = (d0 + camRight * Mathf.Tan(w.x * halfW) + camUp * Mathf.Tan(y * halfW)).normalized;
        float s = depth * Mathf.Sin(rAng) / b.unitR;
        b.t.localScale = Vector3.one * s;
        b.t.position = camPos + dir * depth - b.t.rotation * (b.off * s);
    }

    static Body MakeBody(Transform t)
    {
        var b = new Body { t = t, rs = t.GetComponentsInChildren<Renderer>(true) };
        foreach (var r in b.rs) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
        // 拡大 1 のときの大きさを、メッシュの箱から測る（描画が止まっていても測れるように Renderer.bounds は使わない）
        var s0 = t.localScale;
        t.localScale = Vector3.one;
        var bounds = new Bounds();
        bool first = true;
        foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            var mb = mf.sharedMesh.bounds;
            var m = mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                var wp = m.MultiplyPoint3x4(c);
                if (first) { bounds = new Bounds(wp, Vector3.zero); first = false; } else bounds.Encapsulate(wp);
            }
        }
        b.unitR = first ? 1f : Mathf.Max(1e-4f, Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z));
        b.off = first ? Vector3.zero : Quaternion.Inverse(t.rotation) * (bounds.center - t.position);
        t.localScale = s0;
        return b;
    }

    // ------------------------------------------------------------------ 再突入の光
    void BuildGlow()
    {
        var go = new GameObject("WindowGlow");
        go.transform.SetParent(transform, false);
        glowT = go.transform;
        glowMesh = new Mesh { name = "WindowGlowQuad" };
        glowMesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        glowMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        glowMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        glowMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        glowMesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = glowMesh;
        glowR = go.AddComponent<MeshRenderer>();
        glowR.shadowCastingMode = ShadowCastingMode.Off;
        glowR.receiveShadows = false;
        glowR.enabled = false;
        // Sprites/Default：常にビルドに入る（Always Included）。透明で、色を HDR にすると Bloom で光る（軌道の線と同じシェーダー）
        var sh = Shader.Find("Sprites/Default");
        if (sh != null)
        {
            glowMat = new Material(sh) { name = "M_Window_Glow" };
            glowR.sharedMaterial = glowMat;
        }
        else Debug.LogWarning("[WindowView] Sprites/Default が見つからない（再突入で窓は光らない）");

        var lgo = new GameObject("WindowGlowLight");
        lgo.transform.SetParent(transform, false);
        glowLight = lgo.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.shadows = LightShadows.None;
        glowLight.intensity = 0f;
        glowLight.enabled = false;
    }

    void UpdateGlow(float tRe)
    {
        glow = GlowAt(tView - tRe);
        if (glow > 0.001f) glow *= 1f + glowFlicker * (Mathf.PerlinNoise(Time.time * 7f, 0.37f) * 2f - 1f);
        bool on = glow > 0.001f && glowMat != null;
        glowR.enabled = on;
        glowLight.enabled = on;
        if (!on) return;
        var outward = Outward();
        glowT.position = window.position + outward * glowOffset;
        glowT.rotation = Quaternion.LookRotation(outward, Vector3.up);
        glowT.localScale = new Vector3(windowHalfWidth * 2.6f, windowHalfHeight * 5f, 1f);   // 斜めから見ても穴を覆う
        var c = glowColor * glowIntensity;
        c.a = Mathf.Clamp01(glow) * glowMaxAlpha;
        glowMat.color = c;
        glowLight.transform.position = window.position - outward * 0.8f;
        glowLight.color = glowColor;
        glowLight.range = cabinLightRange;
        glowLight.intensity = glow * cabinLightIntensity;
    }

    float GlowAt(float k)
    {
        if (k <= 0f) return 0f;
        float a = Mathf.Clamp01(k / Mathf.Max(0.01f, glowRampIn));
        float b = 1f - Mathf.Clamp01((k - glowRampIn - glowHold) / Mathf.Max(0.01f, glowFadeOut));
        return a * b;
    }
}
