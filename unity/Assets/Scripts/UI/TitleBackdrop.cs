// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// タイトルのやわらかく動く背景（#153）。クリームの地の上に、奥から順に：
//   1. 細かい模様（点と小さな星を並べた1マス）が斜めにゆっくり流れる
//   2. またたく星（オレンジと薄い濃紺。大きさが呼吸する）
//   3. ときどき空を横切る小さなロケット（点の尾つき）
//   4. 下の丸い地球（大気の輪・陸がゆっくり回る）と、その前を流れる雲
// 形はすべて UiDisc（頂点で描く円と星）と、実行時に描く模様の画像（UiShapes）。値は TitleLook。押す所は無い（raycastTarget なし）
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;
using L = TitleLook;

public class TitleBackdrop : MonoBehaviour
{
    class Star { public RectTransform r; public float hz, phase; }
    class Drift { public RectTransform r; public float speed, span, frac = -1f; }   // span > 0：陸（その幅で回す）。frac：雲の最初の x（画面の幅の割合）

    RectTransform area;
    RawImage pattern;
    readonly List<Star> stars = new List<Star>();
    readonly List<Drift> drifts = new List<Drift>();
    RectTransform rocket;
    readonly List<RectTransform> trail = new List<RectTransform>();
    float start;

    /// <summary>親（タイトルの背景）いっぱいに背景を組む。親の一番奥に置く</summary>
    public static TitleBackdrop Build(RectTransform parent)
    {
        var area = Ui.Rect("Backdrop", parent); UiKit.Fill(area);
        area.SetAsFirstSibling();
        var b = area.gameObject.AddComponent<TitleBackdrop>();
        b.area = area;
        var rnd = new System.Random(L.StarSeed);

        // 1. 模様
        b.pattern = Ui.Rect("Pattern", area).gameObject.AddComponent<RawImage>();
        UiKit.Fill(b.pattern.rectTransform);
        b.pattern.texture = UiShapes.Pattern;
        b.pattern.color = L.PatternColor;
        b.pattern.raycastTarget = false;

        // 2. 星（画面の割合で置くので、どの大きさの画面でも散らばる）
        var starLayer = Ui.Rect("Stars", area); UiKit.Fill(starLayer);
        for (int i = 0; i < L.StarCount; i++)
        {
            float size = Mathf.Lerp(L.StarMin, L.StarMax, (float)rnd.NextDouble());
            var d = UiDisc.Add(starLayer, UiDisc.Shape.Sparkle, i % 3 == 2 ? L.StarColorB : L.StarColorA, size, "Star", 32);
            var r = d.rectTransform;
            var a = new Vector2(L.StarArea.x + L.StarArea.width * (float)rnd.NextDouble(), L.StarArea.y + L.StarArea.height * (float)rnd.NextDouble());
            r.anchorMin = r.anchorMax = a;
            r.anchoredPosition = Vector2.zero;
            b.stars.Add(new Star { r = r, hz = Mathf.Lerp(L.StarHzMin, L.StarHzMax, (float)rnd.NextDouble()), phase = (float)rnd.NextDouble() * Mathf.PI * 2f });
        }

        // 3. ロケット（尾の点 → 本体 の順に重ねる）
        var sky = Ui.Rect("Rocket", area); UiKit.Fill(sky);
        for (int i = 0; i < L.RocketTrail; i++)
        {
            float s = L.RocketTrailSize * (1f - i / (float)(L.RocketTrail + 1));
            b.trail.Add(UiDisc.Add(sky, UiDisc.Shape.Disc, T.WithAlpha(L.RocketColor, L.RocketColor.a * (1f - i / (float)L.RocketTrail)), s, "Trail", 16).rectTransform);
        }
        var ic = UiKit.Icon(sky, "rocket", L.RocketSize, L.RocketColor);
        b.rocket = ic.rectTransform;
        b.rocket.anchorMin = b.rocket.anchorMax = Vector2.zero;
        b.rocket.sizeDelta = new Vector2(L.RocketSize, L.RocketSize);

        // 4. 地球（下の中央。上の EarthRise px だけ見える）と雲
        var earthBase = Ui.Rect("Earth", area);
        earthBase.anchorMin = earthBase.anchorMax = new Vector2(0.5f, 0f);
        earthBase.sizeDelta = Vector2.zero;
        earthBase.anchoredPosition = new Vector2(0f, L.EarthRise - L.EarthD * 0.5f);
        var halo = UiDisc.Add(earthBase, UiDisc.Shape.Disc, L.AtmosphereColor, L.EarthD + L.AtmosphereW * 2f, "Atmosphere", L.DiscSegments);
        halo.rectTransform.anchoredPosition = Vector2.zero;
        var earth = UiDisc.Add(earthBase, UiDisc.Shape.Disc, L.EarthColor, L.EarthD, "Globe", L.DiscSegments);
        earth.rectTransform.anchoredPosition = Vector2.zero;
        earth.gameObject.AddComponent<Mask>().showMaskGraphic = true;   // 陸を地球の丸の中だけに
        // 陸：地球の上の縁の近くに、丸を2〜3個重ねた塊。横に流して、端まで行ったら反対の端へ
        float landSpan = L.EarthD;
        for (int i = 0; i < L.LandCount; i++)
        {
            var land = Ui.Rect("Land", earth.rectTransform);
            land.anchorMin = land.anchorMax = new Vector2(0.5f, 1f);
            land.sizeDelta = Vector2.zero;
            float x = -landSpan * 0.5f + landSpan * (i + (float)rnd.NextDouble() * 0.5f) / L.LandCount;
            float y = -L.EarthRise * Mathf.Lerp(0.35f, 0.9f, (float)rnd.NextDouble());
            land.anchoredPosition = new Vector2(x, y);
            int blobs = 2 + rnd.Next(2);
            for (int k = 0; k < blobs; k++)
            {
                float s = Mathf.Lerp(70f, 150f, (float)rnd.NextDouble());
                var blob = UiDisc.Add(land, UiDisc.Shape.Disc, L.LandColor, s, "Blob", 40);
                blob.rectTransform.sizeDelta = new Vector2(s * 1.5f, s);
                blob.rectTransform.anchoredPosition = new Vector2(k * s * 0.7f, (k % 2) * s * 0.25f);
            }
            b.drifts.Add(new Drift { r = land, speed = L.LandSpeed, span = landSpan });
        }
        // 雲：地平線の上下を、地球より速く流れる（画面の幅ぶんで回す）
        var clouds = Ui.Rect("Clouds", area);
        clouds.anchorMin = new Vector2(0f, 0f); clouds.anchorMax = new Vector2(1f, 0f);
        clouds.sizeDelta = Vector2.zero; clouds.anchoredPosition = Vector2.zero;
        for (int i = 0; i < L.CloudCount; i++)
        {
            var c = Cloud(clouds, Mathf.Lerp(0.7f, 1.2f, (float)rnd.NextDouble()));
            c.anchorMin = c.anchorMax = new Vector2(0f, 0f);
            c.anchoredPosition = new Vector2(0f, L.EarthRise + Mathf.Lerp(-60f, 40f, (float)rnd.NextDouble()));
            b.drifts.Add(new Drift
            {
                r = c, span = -1f, frac = (i + (float)rnd.NextDouble() * 0.6f) / L.CloudCount,
                speed = L.CloudSpeed * Mathf.Lerp(0.7f, 1.3f, (float)rnd.NextDouble()),
            });
        }
        b.start = UiEase.Now;
        return b;
    }

    /// <summary>雲：横長の丸の上に、大小の丸を2つ（同じ色で重ねて1つの形に見せる）</summary>
    static RectTransform Cloud(Transform p, float k)
    {
        var r = Ui.Rect("Cloud", p);
        r.sizeDelta = Vector2.zero;
        var bottom = UiDisc.Add(r, UiDisc.Shape.Disc, L.CloudColor, 0f, "Base", 40);
        bottom.rectTransform.sizeDelta = new Vector2(200f * k, 56f * k);
        bottom.rectTransform.anchoredPosition = Vector2.zero;
        var big = UiDisc.Add(r, UiDisc.Shape.Disc, L.CloudColor, 84f * k, "Puff", 40);
        big.rectTransform.anchoredPosition = new Vector2(-24f * k, 26f * k);
        var small = UiDisc.Add(r, UiDisc.Shape.Disc, L.CloudColor, 60f * k, "Puff", 40);
        small.rectTransform.anchoredPosition = new Vector2(34f * k, 18f * k);
        return r;
    }

    void Update()
    {
        float dt = UiEase.Dt, now = UiEase.Now - start;
        var size = area.rect.size;
        if (size.x <= 0f || size.y <= 0f) return;

        // 模様：1マス L.PatternTile px で敷きつめて、斜めに流す
        var uv = pattern.uvRect;
        uv.width = size.x / L.PatternTile; uv.height = size.y / L.PatternTile;
        uv.x = Mathf.Repeat(uv.x - L.PatternSpeed.x / L.PatternTile * dt, 1f);
        uv.y = Mathf.Repeat(uv.y - L.PatternSpeed.y / L.PatternTile * dt, 1f);
        pattern.uvRect = uv;

        // 星：大きさが呼吸し、少し回る
        foreach (var s in stars)
        {
            float k = 0.5f + 0.5f * Mathf.Sin(now * s.hz * Mathf.PI * 2f + s.phase);
            float sc = Mathf.Lerp(L.StarDim, 1f, k);
            s.r.localScale = new Vector3(sc, sc, 1f);
            s.r.localRotation = Quaternion.Euler(0f, 0f, (k - 0.5f) * 20f);
        }

        // ロケット：RocketEvery 秒ごとに、左の外から右の外へ、ゆるい山なりに飛ぶ
        float c = now - L.RocketFirst;
        float p = c < 0f ? -1f : Mathf.Repeat(c, L.RocketEvery) / L.RocketDur;
        bool fly = p >= 0f && p <= 1f;
        rocket.gameObject.SetActive(fly);
        for (int i = 0; i < trail.Count; i++)
        {
            float q = p - (i + 1) * L.RocketTrailGap;
            trail[i].gameObject.SetActive(fly && q >= 0f);
            if (fly) Place(trail[i], q, size);
        }
        if (fly)
        {
            Place(rocket, p, size);
            var ahead = RocketAt(p + 0.01f, size) - RocketAt(p, size);
            float heading = Mathf.Atan2(ahead.y, ahead.x) * Mathf.Rad2Deg;
            rocket.localRotation = Quaternion.Euler(0f, 0f, heading - L.RocketIconDeg);
        }

        // 陸と雲：横に流し、端を越えたら反対側へ
        foreach (var d in drifts)
        {
            var pos = d.r.anchoredPosition;
            if (d.span > 0f)
            {
                pos.x += d.speed * dt;
                if (pos.x > d.span * 0.5f) pos.x -= d.span;
            }
            else
            {
                // 雲：最初の1回だけ、画面の幅の割合から px にする（組んだときは画面の大きさがまだ決まっていない）
                if (d.frac >= 0f) { pos.x = d.frac * size.x; d.frac = -1f; }
                pos.x += d.speed * dt;
                if (pos.x > size.x + L.CloudMargin) pos.x -= size.x + L.CloudMargin * 2f;
            }
            d.r.anchoredPosition = pos;
        }
    }

    Vector2 RocketAt(float p, Vector2 size)
    {
        float m = L.RocketSize * 2f;
        float x = Mathf.Lerp(-m, size.x + m, p);
        float y = size.y * L.RocketY + L.RocketArc * Mathf.Sin(p * Mathf.PI);
        return new Vector2(x, y);
    }

    void Place(RectTransform r, float p, Vector2 size)
    {
        r.anchorMin = r.anchorMax = Vector2.zero;
        r.anchoredPosition = RocketAt(p, size);
    }
}
