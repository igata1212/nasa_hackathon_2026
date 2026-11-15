// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-14、#87・#88）／ 更新：iizuka（Opus 5.5、2026-11-15、#190 あつ森に寄せる）
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
// #88 操作が遅い（state の slow_pct > 0、flight の間）：左下（席のパネルのすぐ上）に SLOW の札
//   1行目：理由のアイコン（寒さ temperature／CO2 co2）＋ SLOW +遅れの %（slow_pct をそのまま）
//   理由ごとの行：理由の言葉＋直す席のバッジ＋直し方（「壊れた」と思われないよう、遅い理由を必ず出す）
//   理由は state とサーバーの閾値（thresholds の temp.slow・co2.slow。届いていなければ parameters.json の temp_slow_c・co2_slow）を比べるだけ
//   長押しの輪がゆっくり回るのは #71 の HoldButton（Ctx.SlowPct）。霜・もやの縁は入れていない（UrgencyFx の赤い縁と重なるため。余裕があれば）
// 見た目の値（暗さ・速さ・札の位置）と札の文言は Look・SlowReasons の1か所
// #190（ui-guidelines 3章「パネル」【05】【01】）：SLOW の札は明るいパネル（UiKit.PanelFace：blob_panel＋内ふち＋横縞、影なし）。理由のアイコンは accent の台（遅い＝注意）。
//   文字は茶（text・textSoft）、言葉は小文字まじり
using TMPro;
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
        public const float DetailAlpha = 0.9f;          // 照明 OFF のときの、ほかの部品の Detail の不透明度（1 = そのまま）。#146：0.3 だと T1（照明 OFF のまま飛ぶ）で HUD がずっと読めないので、暗くするのは船室（3D）だけにした
        public const float FadeSec = 0.35f;             // 暗くなる・明るくなるのにかかる秒
        public const float ReapplySec = 0.5f;           // 照明 OFF の間、作り直された部品（席のパネル）に掛け直す間隔
        public const int ScrimBelowFrame = 1;           // 暗幕の Canvas の順番 = UrgencyFx.frameSortingOrder − これ（赤い枠の下）
        public const int ScrimOrderFallback = 8;        // UrgencyFx が無いとき（GameCanvas = 10・赤い枠 = 9 の下）
        // #88 SLOW の札
        // 札の置き場所（Cabin に対する割合）。#146：左の列、ゲージのパネル（短くした）の下・先生の小窓（上の端 0.385）の上。
        //   右下はヒューストンの吹き出しの下に潜った。札の左下をここに合わせる
        public const float SlowX = 0.01f, SlowY = 0.39f;
        public static readonly Vector2 SlowPivot = new Vector2(0f, 0f);
        public const float SlowBadgeD = T.IconM;        // 直す席のバッジ
        public const string SlowFormat = "Slow +{0}%";  // 1行目（{0} = slow_pct）
    }

    /// <summary>遅くなる理由（#88）：key・アイコン・理由の言葉・直す席・直し方。文言は Taiga が決める（今は H16・H18 に合わせた仮）</summary>
    static readonly (string key, string icon, string why, string seat, string fix)[] SlowReasons =
    {
        ("cold", "temperature", "Cold", Seats.Comms, "heater on"),
        ("co2",  "co2",         "CO2", Seats.Life, "swap filter"),
    };

    Image scrim;
    float dim;          // 0 = 照明 ON、1 = 照明 OFF（なめらかに動かす）
    float lastNow, nextReapply;

    RectTransform slowCard;
    TextMeshProUGUI slowText;
    UiIconDisc[] slowIcons;
    RectTransform[] slowRows;
    UiSeatBadge[] slowBadges;

    protected override void Build(RectTransform root)
    {
        BuildScrim(root);
        BuildSlow(root);
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

    // ================================================================== #88 操作が遅い
    void BuildSlow(RectTransform root)
    {
        var card = UiKit.Col(root, T.S1, TextAnchor.UpperLeft, T.S4, T.S4 - T.S1);   // 内側は内ふち 12 を含めて 32（ui-guidelines 2章）
        card.name = "SlowCard";
        card.anchorMin = card.anchorMax = new Vector2(Look.SlowX, Look.SlowY);
        card.pivot = Look.SlowPivot;
        card.anchoredPosition = new Vector2(0f, T.S1);
        var fit = card.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        UiKit.PanelFace(card, T.Panel);

        var head = UiKit.Row(card, T.S1, TextAnchor.MiddleLeft);
        slowIcons = new UiIconDisc[SlowReasons.Length];
        for (int i = 0; i < SlowReasons.Length; i++) slowIcons[i] = UiKit.Disc(head, SlowReasons[i].icon, T.Accent, T.IconM);
        slowText = UiKit.Text(head, "", TextStyle.Choice, T.Text, TextAlignmentOptions.MidlineLeft);

        slowRows = new RectTransform[SlowReasons.Length];
        slowBadges = new UiSeatBadge[SlowReasons.Length];
        for (int i = 0; i < SlowReasons.Length; i++)
        {
            var r = SlowReasons[i];
            var row = UiKit.Row(card, T.S1, TextAnchor.MiddleLeft);
            row.name = "Slow_" + r.key;
            UiKit.Text(row, r.why, TextStyle.Label, T.TextSoft, TextAlignmentOptions.MidlineLeft);
            slowBadges[i] = UiKit.SeatBadge(row, r.seat, Look.SlowBadgeD);
            UiKit.Text(row, r.fix, TextStyle.Label, T.Text, TextAlignmentOptions.MidlineLeft);
            slowRows[i] = row;
        }
        slowCard = card;
        slowCard.gameObject.SetActive(false);
    }

    public override void Refresh(GameState s)
    {
        bool show = s != null && Ctx.Model.Phase == "flight" && s.slowPct > 0f;
        if (slowCard.gameObject.activeSelf != show) slowCard.gameObject.SetActive(show);
        if (!show) return;
        slowText.text = string.Format(Look.SlowFormat, Mathf.RoundToInt(s.slowPct));
        bool any = false;
        var on = new bool[SlowReasons.Length];
        for (int i = 0; i < SlowReasons.Length; i++) { on[i] = Slow(SlowReasons[i].key, s); any |= on[i]; }
        for (int i = 0; i < SlowReasons.Length; i++)
        {
            bool v = on[i] || !any;   // 閾値が届かず理由が分からないときは両方出す（理由の無い SLOW にしない）
            slowIcons[i].Root.gameObject.SetActive(v);
            slowRows[i].gameObject.SetActive(v);
            slowBadges[i].Set(SlowReasons[i].seat, Ctx.Model.MySeats.Contains(SlowReasons[i].seat), false);
        }
    }

    /// <summary>この理由で遅いか：サーバーの閾値（無ければ parameters.json）と比べるだけ</summary>
    static bool Slow(string key, GameState s)
    {
        float v;
        if (key == "cold") return (s.Threshold("temp", "slow", out v) || GameParams.TryGet("temp_slow_c", out v)) && s.temp < v;
        if (key == "co2") return (s.Threshold("co2", "slow", out v) || GameParams.TryGet("co2_slow", out v)) && s.co2 >= v;
        return false;
    }
}
