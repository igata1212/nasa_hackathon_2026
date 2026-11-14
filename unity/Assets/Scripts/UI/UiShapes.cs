// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// 実行時に作る白い画像（#153）：タイトルの地の模様（点と小さな星をずらして並べた1マス）と、部屋の画面の 3D の星（スプライト）。
// 色は使う側で付ける（白＋透明度だけ）。1回だけ作って使い回す。画像のファイルを増やさないためにコードで描く
using UnityEngine;

public static class UiShapes
{
    static Texture2D pattern, sparkleTex;
    static Sprite sparkle;

    /// <summary>地の模様の1マス（左下に点、右上に小さな星）。Wrap は Repeat</summary>
    public static Texture2D Pattern
    {
        get
        {
            if (pattern != null) return pattern;
            int n = TitleLook.PatternTex;
            pattern = Make(n, "TitlePattern", (u, v) => Mathf.Max(Dot(u, v, 0.25f, 0.25f, 0.07f, n), Star(u, v, 0.75f, 0.75f, 0.17f, n)));
            pattern.wrapMode = TextureWrapMode.Repeat;
            return pattern;
        }
    }

    /// <summary>4つの角の星（3D の SpriteRenderer 用。1 unit = 1 m の大きさ）</summary>
    public static Sprite Sparkle
    {
        get
        {
            if (sparkle != null) return sparkle;
            int n = TitleLook.PatternTex;
            sparkleTex = Make(n, "Sparkle", (u, v) => Mathf.Max(Star(u, v, 0.5f, 0.5f, 0.48f, n), Dot(u, v, 0.5f, 0.5f, 0.12f, n)));
            sparkleTex.wrapMode = TextureWrapMode.Clamp;
            sparkle = Sprite.Create(sparkleTex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n, 0, SpriteMeshType.FullRect);
            sparkle.name = "Sparkle";
            return sparkle;
        }
    }

    static Texture2D Make(int n, string name, System.Func<float, float, float> alpha)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Bilinear };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float a = Mathf.Clamp01(alpha((x + 0.5f) / n, (y + 0.5f) / n));
                px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        return tex;
    }

    // 円（縁は 1px ぼかす）
    static float Dot(float u, float v, float cx, float cy, float r, int n)
    {
        float d = Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy));
        return Mathf.Clamp01((r - d) * n + 0.5f);
    }

    // 4つの角の星：|x|^(2/e) + |y|^(2/e) <= 1（e は TitleLook.SparkleExp。UiDisc の星と同じ形）
    static float Star(float u, float v, float cx, float cy, float r, int n)
    {
        float k = 2f / TitleLook.SparkleExp;
        float x = Mathf.Abs(u - cx) / r, y = Mathf.Abs(v - cy) / r;
        float f = Mathf.Pow(x, k) + Mathf.Pow(y, k);
        return Mathf.Clamp01((1f - f) * r * n * 0.5f + 0.5f);
    }
}
