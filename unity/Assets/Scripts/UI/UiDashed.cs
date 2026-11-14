// 書いた人：iizuka（作成：Claude）／ 最終更新：2026-11-14
// 点線（UI ガイドライン 3章：パネルの見出しの下、空席のカードの枠）。角丸の枠か、横の1本を描く
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UiDashed : MaskableGraphic
{
    public bool straight;               // true：rect の高さの中央に横の線。false：rect の内側に角丸の枠
    public float radius = UiTokens.RadiusM;
    public float thickness = UiTokens.Border;
    public float dash = UiTokens.DashLen, gap = UiTokens.DashGap;

    const int ArcSteps = 8;
    readonly List<Vector2> pts = new List<Vector2>();

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = GetPixelAdjustedRect();
        pts.Clear();
        float h = thickness * 0.5f;
        if (straight)
        {
            pts.Add(new Vector2(r.xMin, r.center.y));
            pts.Add(new Vector2(r.xMax, r.center.y));
        }
        else
        {
            var a = new Rect(r.xMin + h, r.yMin + h, r.width - thickness, r.height - thickness);
            float rad = Mathf.Max(0f, Mathf.Min(radius - h, Mathf.Min(a.width, a.height) * 0.5f));
            Arc(new Vector2(a.xMax - rad, a.yMax - rad), rad, 90f, 0f);
            Arc(new Vector2(a.xMax - rad, a.yMin + rad), rad, 0f, -90f);
            Arc(new Vector2(a.xMin + rad, a.yMin + rad), rad, -90f, -180f);
            Arc(new Vector2(a.xMin + rad, a.yMax - rad), rad, 180f, 90f);
            pts.Add(pts[0]);
        }

        float period = dash + gap, d = 0f;
        Color32 c = color;
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            Vector2 p0 = pts[i], p1 = pts[i + 1];
            float len = Vector2.Distance(p0, p1), s = 0f;
            if (len <= 0f) continue;
            Vector2 dir = (p1 - p0) / len;
            while (s < len)
            {
                float phase = d % period;
                float run = phase < dash ? Mathf.Min(dash - phase, len - s) : Mathf.Min(period - phase, len - s);
                if (run <= 1e-4f) run = 1e-4f;
                if (phase < dash) Quad(vh, p0 + dir * s, p0 + dir * (s + run), c);
                s += run; d += run;
            }
        }
    }

    void Arc(Vector2 center, float rad, float from, float to)
    {
        for (int i = 0; i <= ArcSteps; i++)
        {
            float ang = Mathf.Lerp(from, to, i / (float)ArcSteps) * Mathf.Deg2Rad;
            pts.Add(center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad);
        }
    }

    void Quad(VertexHelper vh, Vector2 a, Vector2 b, Color32 c)
    {
        Vector2 n = (b - a).normalized;
        Vector2 o = new Vector2(-n.y, n.x) * (thickness * 0.5f);
        int i = vh.currentVertCount;
        vh.AddVert(a + o, c, Vector2.zero); vh.AddVert(b + o, c, Vector2.zero);
        vh.AddVert(b - o, c, Vector2.zero); vh.AddVert(a - o, c, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i + 2, i + 3, i);
    }
}
