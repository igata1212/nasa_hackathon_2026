// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// コードで書くアニメの「なめらかさ」（#153。DOTween などは入れない）。p は 0〜1。弾みの強さは TitleLook.BackOvershoot
using UnityEngine;

public static class UiEase
{
    /// <summary>少し行き過ぎてから戻る（ぽんと出る）</summary>
    public static float OutBack(float p)
    {
        p = Mathf.Clamp01(p);
        float s = TitleLook.BackOvershoot, q = p - 1f;
        return 1f + (s + 1f) * q * q * q + s * q * q;
    }

    public static float OutCubic(float p) { p = 1f - Mathf.Clamp01(p); return 1f - p * p * p; }
    public static float InOutSine(float p) => 0.5f - 0.5f * Mathf.Cos(Mathf.Clamp01(p) * Mathf.PI);

    /// <summary>0 → 1 → 0 の山（跳ねる・波）</summary>
    public static float Bump(float p) => p <= 0f || p >= 1f ? 0f : Mathf.Sin(p * Mathf.PI);

    /// <summary>UI のアニメの時間（記録の再生で Time.timeScale を変えても速さが変わらないように）</summary>
    public static float Dt => Mathf.Min(Time.unscaledDeltaTime, TitleLook.MaxStep);
    public static float Now => Time.unscaledTime;
}
