// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// 画面が開いたとき、delay 秒あとに「ふわっと出て弾む」（#153）。大きさ（PopFrom → 1、EaseOutBack）と透明度（CanvasGroup）だけを変える。
// 位置は変えない（レイアウトの中の部品でも使える）。画面を開き直すたびに最初からやり直す。値は TitleLook
using UnityEngine;

public class UiPopIn : MonoBehaviour
{
    public float delay, dur = TitleLook.PopDur, from = TitleLook.PopFrom;
    CanvasGroup cg;
    float t;

    public static UiPopIn Add(Component c, float delay, float from = TitleLook.PopFrom)
    {
        var p = c.gameObject.AddComponent<UiPopIn>();
        p.delay = delay; p.from = from;
        p.Apply(0f);
        return p;
    }

    void OnEnable() { t = 0f; Apply(0f); }

    void Update()
    {
        if (t > delay + dur) return;
        t += UiEase.Dt;
        Apply((t - delay) / Mathf.Max(0.01f, dur));
    }

    void Apply(float p)
    {
        if (cg == null) { cg = GetComponent<CanvasGroup>(); if (cg == null) cg = gameObject.AddComponent<CanvasGroup>(); }
        p = Mathf.Clamp01(p);
        float s = Mathf.LerpUnclamped(from, 1f, UiEase.OutBack(p));
        transform.localScale = new Vector3(s, s, 1f);
        cg.alpha = Mathf.Clamp01(p * 3f);
    }
}
