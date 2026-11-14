// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// 船室の窓の外の地球・月を、GMAT の軌道に合わせて動かす（#126）。
//   宇宙船の位置：ゲームの t（事故からの秒）と route から GmatRoute で求める（EarthMoonRot：地球が原点、月は +X）
//     ゲームの t は、state の t_los・t_aos・t_reentry で GMAT の time_map（los・aos・reentry）の t に写してから使う。
//     time_map.json（LOS 120・AOS 300・再突入 A 420 / B 510）は parameters.json（LOS 90・AOS 210・再突入 A 300 / B 360）と
//     ずれているため。そのままだと、月の裏側の途中で通信が戻り、月が目の前にいるうちに再突入の光が出る
//   見かけ：本物の向き（窓の正面からの角度）と見かけの半径（asin(半径/距離)）を、窓の幅に入るよう縮め・太らせて、窓の穴の向こうに球を置く
//     窓の正面：地球と月への向きを、見かけの大きさで重み付けして足した向き（大きく見える方へ寄る）
//     往路：月が大きくなり、地球は小さく窓の端へ → 月の裏側：月が窓いっぱい、地球は月の後ろ（GMAT の LOS・AOS と同じ幾何）→ 帰還：地球が大きく
//   再突入：state の t_reentry から、窓の外の板（Sprites/Default。実行時に作る）をオレンジに光らせ、船内にオレンジの点光源を足す
// GameFlow は変えない：flow.Model.State を毎フレーム読むだけ。共有マテリアル（M_Core_Palette）は書き換えない。
// 置き方：船室のカメラ（Cam_Top）から窓のまん中を通る線のまわり、カメラから nearDepth / farDepth m（カメラの far 100 m の内）。
//   大きい球は、上の縁（地平線）を窓の中（下から horizonAt の高さ）に置き、上に黒い宇宙を残す。窓より大きい分は壁の後ろに隠す
//   （maxAngleDeg：大きすぎると船室のまわりの背景にはみ出す）。数値は見た目だけ（軌道は GMAT のデータ）。
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

    [Header("窓の穴（modeling/interior/build_interior.py の WIN_*。m）")]
    public float windowHalfWidth = 0.75f;
    public float windowHalfHeight = 0.29f;
    public float wallDepth = 0.2f;          // 壁の厚み＋窓枠（WALL_T 0.15＋枠 0.05）。上から見ると、窓の上の縁は内側の面で決まる

    [Header("見せ方（見た目だけ。窓の半幅を 1 とした単位）")]
    public float fieldHalfDeg = 110f;    // 本物の角度で、窓の端（±1）に来る角度
    public float sizeGain = 0.2f;        // 窓の上の半径 = sizeGain × (見かけの半径・度)^sizePower
    public float sizePower = 0.75f;
    public float minSize = 0.1f;         // 遠くても点にしない
    public float shrinkRefDeg = 1.1f;    // これより小さく見える天体は、小さくなり方を誇張する（事故の時点の地球 1.09°）
    public float shrinkPower = 3f;       // 誇張：本物の見かけの比の shrinkPower 乗（往路の地球 0.83 倍 → 0.57 倍）
    public float maxAngleDeg = 11f;      // カメラから見た球の半径の上限（窓の半幅 約4°。大きいと船室のまわりの背景にはみ出す）
    public float horizonAt = 0.72f;      // 窓より大きい球の上の縁を置く高さ（窓の下端 0〜上端 1）
    public float focusPower = 1f;        // 窓の正面を、大きく見える方へ寄せる強さ
    public bool mirror;                  // 左右を入れ替える
    public float nearDepth = 50f, farDepth = 70f;   // カメラから球の中心まで（m）。本当に近い方の天体を手前に置く
    public float earthSpinDegPerSec = 2f;
    public float smoothRate = 4f;        // t（毎秒届く）をなめらかにする速さ（1/秒）

    [Header("再突入の光（見た目だけ。t_reentry からの秒）")]
    public Color glowColor = new Color(1f, 0.36f, 0.08f);
    public float glowIntensity = 1.5f;   // HDR。Bloom（しきい値 0.9）で窓のまわりもにじむ。強いと黄色に飛ぶ
    public float glowMaxAlpha = 0.7f;    // 1 だと地球が見えなくなる
    public float glowRampIn = 4f, glowHold = 30f, glowFadeOut = 12f;
    public float glowFlicker = 0.15f;
    public float glowOffset = 0.02f;     // 壁の外側の面からの距離（m）。離すと上から見て壁の上の縁を越える
    public float cabinLightIntensity = 3f, cabinLightRange = 5f;

    [Header("確認用（Play 中にインスペクターで動かす）")]
    public bool debugOverride;
    public float debugT;                 // GMAT の time_map の t（0／120 LOS／300 AOS／360 噴射／A 420・B 510 再突入）
    public string debugRoute = "";

    [Header("今の値（読むだけ）")]
    public float currentT;               // GMAT の time_map の t
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
    float halfW, bottomY, topY;

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
        var r = RouteOf(route);
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

    /// <summary>t・tRe は GMAT の time_map の t（秒）</summary>
    void ReadState(out float t, out string route, out float tRe)
    {
        var s = flow != null ? flow.Model.State : null;
        route = debugOverride ? debugRoute : s != null ? s.route : "";
        route = (route ?? "").ToLowerInvariant();
        var r = RouteOf(route);
        if (debugOverride) t = debugT;
        else if (s != null) t = ToMapT(r, s.t, s.tLos, s.tAos, s.tReentry);
        else t = 0f;   // 事故の前・設計の画面：事故の時点の景色
        tRe = KnotT(r, "reentry", r.EndT);
    }

    // route が決まる前はサーバーが B として t_reentry を出す（server/README.md）。噴射の前は A と B が同じ道
    GmatRoute RouteOf(string route) => route == "a" ? routeA : routeB;

    /// <summary>ゲームの t（秒）→ time_map の t。los・aos・reentry の区切りごとに線形、再突入の後はそのまま進める</summary>
    static float ToMapT(GmatRoute r, float t, float tLos, float tAos, float tRe)
    {
        if (!(tLos > 0f && tAos > tLos && tRe > tAos)) return t;   // サーバーの値が無い：そのまま
        float kL = KnotT(r, "los", tLos), kA = KnotT(r, "aos", tAos), kR = KnotT(r, "reentry", tRe);
        if (t <= tLos) return t * kL / tLos;
        if (t <= tAos) return Mathf.Lerp(kL, kA, (t - tLos) / (tAos - tLos));
        if (t <= tRe) return Mathf.Lerp(kA, kR, (t - tAos) / (tRe - tAos));
        return kR + (t - tRe);
    }

    static float KnotT(GmatRoute r, string label, float fallback)
    {
        if (r != null && r.knots != null)
            foreach (var k in r.knots) if (k.label == label) return k.t;
        return fallback;
    }

    /// <summary>天体の向き h と見かけの半径 a（ラジアン）→ 窓の上の (x, y, 半径)。窓の半幅が 1</summary>
    Vector3 ToWindow(Vector3 h, float a, Vector3 look, Vector3 right, Vector3 up)
    {
        float f = Vector3.Dot(h, look), s = Vector3.Dot(h, right), u = Vector3.Dot(h, up);
        float x = Mathf.Atan2(s, f) * Mathf.Rad2Deg / fieldHalfDeg;
        float y = Mathf.Atan2(u, Mathf.Sqrt(s * s + f * f)) * Mathf.Rad2Deg / fieldHalfDeg;
        if (mirror) x = -x;
        float deg = a * Mathf.Rad2Deg;
        float r = sizeGain * Mathf.Pow(deg, sizePower);
        if (deg < shrinkRefDeg) r *= Mathf.Pow(deg / shrinkRefDeg, Mathf.Max(0f, shrinkPower - sizePower));   // 小さい側は本物の比の shrinkPower 乗
        r = Mathf.Max(minSize, r);
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
        topY = AngY(w - Outward() * wallDepth + Vector3.up * windowHalfHeight) / halfW;   // 見える上端（内側の面の上の縁）
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
        float y = Mathf.Min(w.y, Mathf.Lerp(bottomY, topY, horizonAt) - rU);   // 大きい球は下げて、上の縁だけを窓に入れる（地平線のように見える）
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
        // 板は穴より少し大きく（左右 +0.08 m・下 +0.08 m）。上は窓の上端まで：壁の上の縁（窓の上 0.13 m）を越えて空に出さない
        const float m = 0.08f;
        glowT.position = window.position + outward * glowOffset - Vector3.up * (m * 0.5f);
        glowT.rotation = Quaternion.LookRotation(outward, Vector3.up);
        glowT.localScale = new Vector3((windowHalfWidth + m) * 2f, windowHalfHeight * 2f + m, 1f);
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
