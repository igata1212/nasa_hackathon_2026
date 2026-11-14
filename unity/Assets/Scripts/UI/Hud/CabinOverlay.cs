// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#87）
// 作る Issue：#87（照明 OFF の見え方）・#88（操作が遅くなる表示）
//
// 画面全体に重ねる物。置き場所は HUD のいちばん後ろ（3D の上、ほかの部品の下）。押す所の邪魔をしないように raycastTarget は全部 false
// #87 照明 OFF（state の sw.lights。CabinHud が Ctx.LightsOff に入れる）：
//   ・暗幕：船室（3D）の上に ink の幕。自前の Canvas（overrideSorting）で UrgencyFx の赤い枠の1つ下に描く
//     → 照明 OFF でも警報の赤い縁は同じ強さで見える（#76・#87 の「警報を照明 OFF でも同じ強さで出すか」への答え：出す）
//   ・ほかの部品の Detail（CanvasGroup）の alpha を下げる（ゲージ・地図・タイムラインの細部が読みにくくなる）。
//     Detail の外（席の押せるボタン・危機の帯・同時押しの合図など）はそのまま
//   ・席のパネルは席を変えると作り直されるので、照明 OFF の間はときどき掛け直す
//   ・船室の天井灯（Lamp_Cabin、#106）を消すのは 3D 側（この部品ではしない）
// 見た目の値（暗さ・速さ）は Look の1か所
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;

public class CabinOverlay : HudPart
{
    // ================================================================== 見た目の値（直すのはここ）
    static class Look
    {
        public static readonly Color Scrim = T.Ink;     // 暗幕の色（不透明度は ScrimAlpha）
        public const float ScrimAlpha = 0.55f;          // 照明 OFF で船室（3D）を暗くする量
        public const float DetailAlpha = 0.3f;          // 照明 OFF のときの、ほかの部品の Detail の不透明度（1 = そのまま）
        public const float FadeSec = 0.35f;             // 暗くなる・明るくなるのにかかる秒
        public const float ReapplySec = 0.5f;           // 照明 OFF の間、作り直された部品（席のパネル）に掛け直す間隔
        public const int ScrimBelowFrame = 1;           // 暗幕の Canvas の順番 = UrgencyFx.frameSortingOrder − これ（赤い枠の下）
        public const int ScrimOrderFallback = 8;        // UrgencyFx が無いとき（GameCanvas = 10・赤い枠 = 9 の下）
    }

    Image scrim;
    float dim;          // 0 = 照明 ON、1 = 照明 OFF（なめらかに動かす）
    float lastNow, nextReapply;

    protected override void Build(RectTransform root)
    {
        BuildScrim(root);
    }

    // ================================================================== #87 照明 OFF
    void BuildScrim(RectTransform root)
    {
        var r = Ui.Rect("LightsOffScrim", root); Ui.Fill(r);
        var cv = r.gameObject.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder = ScrimOrder(root);
        scrim = r.gameObject.AddComponent<Image>();
        scrim.color = T.WithAlpha(Look.Scrim, 0f);
        scrim.raycastTarget = false;
        scrim.enabled = false;
    }

    /// <summary>暗幕の順番：UrgencyFx の赤い枠の下、HUD（GameCanvas）の下</summary>
    static int ScrimOrder(RectTransform root)
    {
        var fx = Object.FindAnyObjectByType<UrgencyFx>();
        int order = fx != null ? fx.frameSortingOrder - Look.ScrimBelowFrame : Look.ScrimOrderFallback;
        var parent = root.GetComponentInParent<Canvas>();
        if (parent != null) order = Mathf.Min(order, parent.rootCanvas.sortingOrder - 1);
        return order;
    }

    public override void SetLightsOff(bool off) => nextReapply = 0f;   // すぐ掛け直す（暗さは Tick が Ctx.LightsOff を見て動かす）

    public override void Tick(float now)
    {
        float dt = lastNow > 0f ? Mathf.Clamp(now - lastNow, 0f, 0.1f) : 0f;
        lastNow = now;
        float target = Ctx.LightsOff ? 1f : 0f;
        bool moving = dim != target;
        dim = Mathf.MoveTowards(dim, target, dt / Look.FadeSec);
        if (moving || (dim > 0f && now >= nextReapply))
        {
            ApplyDim();
            nextReapply = now + Look.ReapplySec;
        }
    }

    void ApplyDim()
    {
        scrim.color = T.WithAlpha(Look.Scrim, Look.ScrimAlpha * dim);
        scrim.enabled = dim > 0f;
        float a = Mathf.Lerp(1f, Look.DetailAlpha, dim);
        foreach (var p in Ctx.Parts)
            if (p != this && p.Detail != null) p.Detail.alpha = a;
    }
}
