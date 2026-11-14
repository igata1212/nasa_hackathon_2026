// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// ときどき跳ねる（#153。タイトルの席バッジ）：TitleLook.HopEvery 秒ごとに、offset 秒ずらして1回跳ぶ。
// 跳んでいる間は縦に伸び、着地で横につぶれる。付けた RectTransform の位置（下の中央が基準）と大きさを書くので、
// レイアウトの外（親にレイアウトの無い入れ物）に置く
using UnityEngine;

public class UiHop : MonoBehaviour
{
    public float offset;
    RectTransform r;
    float baseY;

    public static UiHop Add(RectTransform r, float offset)
    {
        var h = r.gameObject.AddComponent<UiHop>();
        h.offset = offset; h.r = r;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0f);              // 下の中央でつぶれる
        h.baseY = -r.sizeDelta.y * 0.5f;
        r.anchoredPosition = new Vector2(0f, h.baseY);
        return h;
    }

    void Update()
    {
        float c = Mathf.Repeat(UiEase.Now - offset, TitleLook.HopEvery);
        float y = 0f, sx = 1f, sy = 1f;
        if (c < TitleLook.HopDur)
        {
            float k = UiEase.Bump(c / TitleLook.HopDur);
            y = TitleLook.HopH * k; sx = 1f - TitleLook.HopStretch * k; sy = 1f + TitleLook.HopStretch * k;
        }
        else if (c < TitleLook.HopDur + TitleLook.HopSquashDur)
        {
            float k = UiEase.Bump((c - TitleLook.HopDur) / TitleLook.HopSquashDur);
            sx = 1f + TitleLook.HopSquash * k; sy = 1f - TitleLook.HopSquash * k;
        }
        r.anchoredPosition = new Vector2(0f, baseY + y);
        r.localScale = new Vector3(sx, sy, 1f);
    }
}
