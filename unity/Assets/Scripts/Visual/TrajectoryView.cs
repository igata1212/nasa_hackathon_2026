// 書いた人：Claude（#65）／ 最終更新：2026-11-14
// 軌道の地図：GMAT の帰還ルートを線で描き、宇宙船を t に合わせて動かす。
// 入力は t（事故からの秒）と route（"a" / "b" / 空）。サーバーの state とのつなぎは後で（今はインスペクターで動かす）。
// 距離は km を縮尺（既定 1 unit = 10,000 km）にかけ、地球・月は見栄えの大きさに太らせる。
// 太らせると線が球に埋まるので、地球・月の近くだけ、線を球の外へ押し出している（Warp）。
using UnityEngine;

[ExecuteAlways]
public class TrajectoryView : MonoBehaviour
{
    [Header("データ（Tools/GMAT/軌道を取り込む で作る）")]
    public GmatRoute routeA;
    public GmatRoute routeB;
    public Material lineMaterial;

    [Header("入力（今はテスト用。サーバーの state の t と route をここに入れる）")]
    public float t;
    public string route = "";          // "a" / "b" / 空（まだ選んでいない）

    [Header("縮尺と大きさ")]
    public float unitsPerKm = 1f / 10000f;
    public float earthVisualKm = 13000f;   // 見える地球の半径（実際は 6,378）
    public float moonVisualKm = 7000f;     // 見える月の半径（実際は 1,737）
    public float warpRangeKm = 70000f;     // この距離までは線を外へ押し出す
    public float lineWidth = 0.1f;
    public float faintWidth = 0.05f;

    [Header("つなぐもの（シーンを作るスクリプトが入れる）")]
    public Transform ship;                 // Ship_Stack。+Z が進行方向
    public Transform earth, moon;

    public static readonly Color ColorA = new Color(0.302f, 0.722f, 0.667f);  // #4DB8AA（GoLight。A＝go、2D の地図・ルートのカードと同じ。#190）
    public static readonly Color ColorB = new Color(0.651f, 0.545f, 0.839f);  // #A68BD6（Plum を暗い宇宙で見えるよう明るく。B＝plum。#190）
    public static readonly Color ColorNone = new Color(0.984f, 0.969f, 0.933f);   // #FBF7EE
    const float EarthRealKm = 6378.14f, MoonRealKm = 1737.4f;

    LineRenderer faintA, faintB, past, future;
    Vector3[] warpedA, warpedB;
    float lastT = float.NaN;
    string lastRoute;

    public Vector3 MoonCenterKm => new Vector3(routeA != null ? routeA.moonCenterXKm : 404419f, 0f, 0f);

    void OnEnable() { Rebuild(); }

    void OnDisable() { DestroyLines(); }

    void Update()
    {
        if (t != lastT || route != lastRoute) Apply();
    }

    public void SetState(float newT, string newRoute) { t = newT; route = newRoute; Apply(); }

    /// <summary>km の位置 → シーンの位置。地球と月の近くは球の外へ押し出す</summary>
    public Vector3 ToScene(Vector3 km)
    {
        km = Warp(km, Vector3.zero, EarthRealKm, earthVisualKm);
        km = Warp(km, MoonCenterKm, MoonRealKm, moonVisualKm);
        // EarthMoonRot の (X, Y, Z) → シーンの (x, z, y)：上から見ると、X が右・Y が奥
        return transform.TransformPoint(new Vector3(km.x, km.z, km.y) * unitsPerKm);
    }

    Vector3 Warp(Vector3 p, Vector3 c, float real, float vis)
    {
        var d = p - c;
        float r = d.magnitude;
        if (r >= warpRangeKm || r < 1f) return p;
        float g = Mathf.Clamp01((warpRangeKm - r) / (warpRangeKm - real));
        return c + d / r * (r + (vis - real) * g);
    }

    void Rebuild()
    {
        DestroyLines();
        if (routeA == null || routeB == null) return;
        faintA = NewLine("Line_A_faint");
        faintB = NewLine("Line_B_faint");
        past = NewLine("Line_Past");
        future = NewLine("Line_Future");
        warpedA = WarpAll(routeA);
        warpedB = WarpAll(routeB);
        SetLine(faintA, warpedA, 0, warpedA.Length, Faint(ColorA), faintWidth);
        SetLine(faintB, warpedB, 0, warpedB.Length, Faint(ColorB), faintWidth);
        lastT = float.NaN;
        Apply();
    }

    Vector3[] WarpAll(GmatRoute r)
    {
        var a = new Vector3[r.posKm.Length];
        for (int i = 0; i < a.Length; i++) a[i] = ToScene(r.posKm[i]);
        return a;
    }

    LineRenderer NewLine(string name)
    {
        var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.sharedMaterial = lineMaterial;
        lr.numCornerVertices = 3;
        lr.numCapVertices = 3;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.alignment = LineAlignment.View;
        return lr;
    }

    void DestroyLines()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i);
            if (c.name.StartsWith("Line_")) { if (Application.isPlaying) Destroy(c.gameObject); else DestroyImmediate(c.gameObject); }
        }
    }

    static Color Faint(Color c) { c.a = 0.35f; return c; }
    static Color Dim(Color c) { var d = Color.Lerp(c, new Color(0.12f, 0.16f, 0.3f), 0.45f); d.a = 0.85f; return d; }

    static void SetLine(LineRenderer lr, Vector3[] pts, int from, int count, Color c, float width)
    {
        lr.startColor = lr.endColor = c;
        lr.startWidth = lr.endWidth = width;
        lr.positionCount = count;
        for (int i = 0; i < count; i++) lr.SetPosition(i, pts[from + i]);
    }

    /// <summary>今の t と route で、線の色・宇宙船の位置を決める</summary>
    public void Apply()
    {
        lastT = t; lastRoute = route;
        if (past == null) { Rebuild(); return; }   // 作った直後など、OnEnable の時点でデータが入っていなかったとき
        string sel = (route ?? "").ToLowerInvariant();
        bool hasSel = sel == "a" || sel == "b";
        var r = sel == "b" ? routeB : routeA;          // 噴射の前は A と B が同じ道なので、選ぶ前は A で動かす
        var pts = sel == "b" ? warpedB : warpedA;
        var col = !hasSel ? ColorNone : (sel == "a" ? ColorA : ColorB);

        // 選んでいない方は薄い線（Rebuild で作ったまま）。選んだ方は、下の past・future が上に重なって濃く見える

        float h = r.HoursAt(t);
        int i = r.IndexAt(h);
        var shipKm = r.PosAtHours(h);
        var shipPos = ToScene(shipKm);

        // 通った部分：最初の行から宇宙船まで／これから：宇宙船から最後の行まで（選ぶ前はこれからを出さない）
        var pastPts = new Vector3[i + 2];
        System.Array.Copy(pts, pastPts, i + 1);
        pastPts[i + 1] = shipPos;
        past.startColor = past.endColor = col;
        past.startWidth = past.endWidth = lineWidth;
        past.positionCount = pastPts.Length;
        past.SetPositions(pastPts);

        if (hasSel)
        {
            var fut = new Vector3[pts.Length - i];
            fut[0] = shipPos;
            System.Array.Copy(pts, i + 1, fut, 1, pts.Length - i - 1);
            future.startColor = future.endColor = Dim(col);
            future.startWidth = future.endWidth = lineWidth;
            future.positionCount = fut.Length;
            future.SetPositions(fut);
        }
        else future.positionCount = 0;

        if (ship != null)
        {
            ship.position = shipPos;
            float dh = 0.02f;
            var ahead = ToScene(r.PosAtHours(h + dh)) - ToScene(r.PosAtHours(h - dh));
            if (ahead.sqrMagnitude > 1e-12f) ship.rotation = Quaternion.LookRotation(ahead.normalized, transform.up);
        }
    }
}
