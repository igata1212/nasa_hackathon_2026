// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// 縁のなめらかな円と、4つの角の星（#153。タイトルの地球・雲・またたく星）。画像を使わず、頂点で描く。
// 縁の外に feather px だけ透明へ向かう帯を足して、大きくしてもギザギザにならないようにする。rect に合わせる（横長なら楕円）
using UnityEngine;
using UnityEngine.UI;

public class UiDisc : MaskableGraphic
{
    public enum Shape { Disc, Sparkle }
    public Shape shape = Shape.Disc;
    public int segments = 48;
    public float feather = TitleLook.Feather;
    public float sparkleExp = TitleLook.SparkleExp;   // 星の形（2 でひし形、大きいほど細い）

    /// <summary>親に色付きの円（星）を足す。押せない（raycastTarget なし）</summary>
    public static UiDisc Add(Transform p, Shape shape, Color c, float size, string name = "Disc", int segments = 48)
    {
        var r = Ui.Rect(name, p);
        r.gameObject.AddComponent<CanvasRenderer>();
        var d = r.gameObject.AddComponent<UiDisc>();
        d.shape = shape; d.segments = segments; d.color = c; d.raycastTarget = false;
        r.sizeDelta = new Vector2(size, size);
        return d;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = GetPixelAdjustedRect();
        Vector2 c = r.center;
        float rx = r.width * 0.5f, ry = r.height * 0.5f;
        if (rx <= 0f || ry <= 0f) return;
        Color32 solid = color, clear = color;
        clear.a = 0;
        int n = Mathf.Max(8, shape == Shape.Sparkle ? segments - segments % 4 : segments);

        vh.AddVert(c, solid, Vector2.zero);
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            float cx = Mathf.Cos(a), sy = Mathf.Sin(a);
            Vector2 dir;
            if (shape == Shape.Sparkle)
            {
                // x = cos^e, y = sin^e（符号つき）。e が 1 で円、2 でひし形、3 で細い星
                dir = new Vector2(Mathf.Sign(cx) * Mathf.Pow(Mathf.Abs(cx), sparkleExp), Mathf.Sign(sy) * Mathf.Pow(Mathf.Abs(sy), sparkleExp));
            }
            else dir = new Vector2(cx, sy);
            var p = c + new Vector2(dir.x * rx, dir.y * ry);
            var outward = new Vector2(cx, sy);   // 縁の外へ（おおよその法線）
            vh.AddVert(p, solid, Vector2.zero);
            vh.AddVert(p + outward * feather, clear, Vector2.zero);
        }
        for (int i = 0; i < n; i++)
        {
            int a0 = 1 + i * 2, b0 = 1 + (i + 1) % n * 2;
            vh.AddTriangle(0, a0, b0);
            vh.AddTriangle(a0, a0 + 1, b0 + 1);
            vh.AddTriangle(a0, b0 + 1, b0);
        }
    }
}
