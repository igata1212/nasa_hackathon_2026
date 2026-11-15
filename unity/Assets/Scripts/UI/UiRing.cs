// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-15
// 楕円の帯（輪）の一部を頂点で描く（#190。タイトルの地の大きな柄の「輪のある惑星」）。画像を使わない。
// 外側 outer と内側 inner（どちらも半径。横・縦）の間を、角度 from〜to（度。右が 0、反時計回り）だけ塗る。縁は feather px だけぼかす（UiDisc と同じ作り）
using UnityEngine;
using UnityEngine.UI;

public class UiRing : MaskableGraphic
{
    public Vector2 outer = new Vector2(100f, 30f), inner = new Vector2(80f, 20f);
    public float from = 0f, to = 360f;
    public int segments = 96;
    public float feather = TitleLook.Feather;

    /// <summary>親の中央に輪の帯を足す。押せない（raycastTarget なし）</summary>
    public static UiRing Add(Transform p, Color c, Vector2 outer, Vector2 inner, float from, float to, string name = "Ring")
    {
        var r = Ui.Rect(name, p);
        r.gameObject.AddComponent<CanvasRenderer>();
        var g = r.gameObject.AddComponent<UiRing>();
        g.color = c; g.raycastTarget = false;
        g.outer = outer; g.inner = inner; g.from = from; g.to = to;
        r.sizeDelta = outer * 2f;
        return g;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Vector2 c = rectTransform.rect.center;
        Color32 solid = color, clear = color;
        clear.a = 0;
        int n = Mathf.Max(2, segments);
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.Lerp(from, to, i / (float)n) * Mathf.Deg2Rad;
            var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var po = c + new Vector2(d.x * outer.x, d.y * outer.y);
            var pi = c + new Vector2(d.x * inner.x, d.y * inner.y);
            vh.AddVert(po + d * feather, clear, Vector2.zero);
            vh.AddVert(po, solid, Vector2.zero);
            vh.AddVert(pi, solid, Vector2.zero);
            vh.AddVert(pi - d * feather, clear, Vector2.zero);
        }
        for (int i = 0; i < n; i++)
        {
            int a = i * 4, b = (i + 1) * 4;
            for (int k = 0; k < 3; k++)
            {
                vh.AddTriangle(a + k, b + k, b + k + 1);
                vh.AddTriangle(a + k, b + k + 1, a + k + 1);
            }
        }
    }
}
