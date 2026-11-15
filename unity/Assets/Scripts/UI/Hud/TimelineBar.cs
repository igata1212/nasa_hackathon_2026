// 書いた人：Claude ／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で CabinHud から分けた）／ 更新：iizuka（Opus 5.5、2026-11-14、#77）／ 更新：iizuka（Opus 5.5、2026-11-15、#189 BURN NOW・HOME if A）／ 更新：iizuka（Opus 5.5、2026-11-15、#190 あつ森に寄せる：面を blob_panel＋星の模様、チップを blob_pill＋アイコンの台、大文字をやめる）
// 作る Issue：#77（タイムラインの帯とカウントダウン）
//
// 上の帯：左に時計（T+）、真ん中に1本の帯（月の裏側＝通信途絶・観測の窓／噴射窓／再突入 A・B／着水）と「いま」の縦線、
// 右に「次まで何秒か」のチップ（例：FAR SIDE in 0:45。噴射窓の残り 10 秒で danger にして弾む）。
// 時刻はすべて state の値（t_los・t_aos・t_burn_open・t_burn_close・t_reentry・t_splashdown と #148 の t_reentry_a/b・t_splashdown_a/b）。
// 帯の印には、その出来事で動く席のバッジ（観測 ◆＋▲、噴射 ▲＋■、再突入 ▲＋■）。自分の席のバッジには ink の輪。
// 事故の前（練習・かき混ぜ）は state が無いので、帯とチップを隠して段階の一言だけ出す。
// Detail：時計と帯（照明 OFF で暗くしてよい）。右のチップは暗くしない（残り秒は照明 OFF でも読めるように）
// 下の TimelineKit は #77・#78・#79 で使う小さな部品（#71 の UiKitHud ができたら中身を差し替える。呼ぶ側は変えない）
// #190（ui-guidelines 1章「船内の計器は濃紺のまま」【06】【01】【07】）：面の色（HudFace）は変えず、形を blob_panel に、小さな星の模様を hudText の 6% で敷く（HudStars）。影なし。
//   アイコンは色つきの台（UiKit.Disc）に。帯の区間とゲージは前の版の形のまま（あつ森に同じ物の基準が無い）
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;

public class TimelineBar : HudPart
{
    // ------------------------------------------------------------------ 文言（Fable の採点で直すときはここだけ。{0} は 分:秒）
    static class Words
    {
        public const string Clock = "T+ {0}";
        // 帯の中（長い順。入らなければ次の短いもの、最後はアイコンだけ）
        public static readonly string[] FarSide = { "Far side (no radio)", "No radio", "" };
        public static readonly string[] BurnWindow = { "Burn window", "Burn", "" };
        public static readonly string[] BurnDone = { "Burn done", "Done", "" };
        public static readonly string[] BurnMissed = { "Missed", "" };
        public static readonly string[] Reentry = { "Re-entry", "" };
        // #189（Fable）：ルートを選ぶ前の A・B は「どちらなら、いつ家に着くか」。アイコンも盾ではなく地球（右のチップの HOME! と同じ）。
        //   1600×900 では区間が約 106 px なので、ふつうは2つめの「if A」が出る（地球＋if A で「A なら家」と読める）
        public static readonly string[] IfA = { "Home if A", "if A", "A", "" };
        public static readonly string[] IfB = { "Home if B", "if B", "B", "" };
        // 右のチップ（次まで／窓の中の残り）
        public const string NextFarSide = "Far side in {0}";
        public const string InFarSide = "No radio · {0} left";
        public const string NextBurn = "Burn window in {0}";
        public const string InBurn = "Burn {0}";   // #189：残り秒は点火の準備のカード（BurnCheck）だけ（2か所に出さない）。残り 10 秒で赤く弾むのはそのまま
        public const string InReentry = "Re-entry {0}";
        public const string NextReentry = "Re-entry in {0}";
        public const string NextSplash = "Splashdown in {0}";
        public const string Home = "Home!";
    }

    // 出来事ごとに動く席（帯の印とチップのバッジ）
    static readonly string[] SeatsObserve = { Seats.Planner, Seats.Pilot };
    static readonly string[] SeatsBurn = { Seats.Pilot, Seats.Comms };
    static readonly string[] SeatsReentry = { Seats.Pilot, Seats.Comms };
    static readonly string[] NoSeats = { };
    const string IfIcon = "earth";   // ルートを選ぶ前の A・B の区間（#189。盾では意味が取れなかった）

    // ------------------------------------------------------------------ 見た目（UiTokens に無い大きさ。#71 の UiKitHud／UiTokens ができたらそちらへ）
    const float BarInsetY = 4f;          // slot の上下のすき間
    const float ClockW = 200f;           // 時計の幅
    const float ChipReserveW = 520f;     // 右の「次まで」のチップに空けておく幅
    const float TrackInsetY = 8f;        // 帯の上下のすき間（帯の高さ = slot − 2×(BarInsetY＋これ)）
    const float TrackH = 40f;            // 帯の角丸を決める高さ（実際の高さは slot の高さから決まる）
    const float NowW = 4f, NowOver = 6f; // 「いま」の縦線（ゲージの生還ラインと同じ形：幅 4、上下に 6 はみ出す）
    const float NowKnob = 18f;           // 縦線の上の丸

    class Seg
    {
        public RectTransform Rect, Row;
        public Image Face, Icon;
        public TextMeshProUGUI Text;
        public TimelineKit.BadgeRow Badges;
    }

    RectTransform bar, track, past, nowLine;
    TextMeshProUGUI clock, idle;
    Seg far, burn, reA, reB;
    Image splash;
    TimelineKit.Countdown next;

    const float CompactScreenW = 1000f;   // #216：画面の幅（px）がこれより狭い（844×390 など）と、帯の中の小さい文字を消してアイコンだけにする
    bool wide = true;                     // #216：いまの bar が全幅か（事故の前は時計のチップだけ）

    // #216：全幅の帯と、時計だけの小さなチップ（左上）を切り替える
    void SetWide(bool w)
    {
        if (w == wide) return;
        wide = w;
        if (w) { Ui.Anchor(bar, 0, 0, 1, 1, T.S2, BarInsetY, T.S2, BarInsetY); return; }
        bar.anchorMin = new Vector2(0f, 0f); bar.anchorMax = new Vector2(0f, 1f);
        bar.offsetMin = new Vector2(T.S2, BarInsetY); bar.offsetMax = new Vector2(T.S2 + ClockW + T.S3 * 2f, -BarInsetY);
    }

    protected override void Build(RectTransform root)
    {
        bar = Ui.Rect("Bar", root); UiKit.Fill(bar);
        bar.offsetMin = new Vector2(T.S2, BarInsetY); bar.offsetMax = new Vector2(-T.S2, -BarInsetY);

        // 照明 OFF で暗くしてよいもの：面・時計・帯
        var detail = Ui.Rect("Detail", bar); UiKit.Fill(detail);
        Detail = detail.gameObject.AddComponent<CanvasGroup>();
        TimelineKit.GaugeFace(detail, TrackH);   // 計器は暗い面（#190：blob_panel＋星の模様。色は HudFace のまま）

        clock = UiKit.Text(detail, "", TextStyle.Number, T.HudText, TextAlignmentOptions.MidlineLeft);
        var cr = clock.rectTransform;
        cr.anchorMin = new Vector2(0f, 0f); cr.anchorMax = new Vector2(0f, 1f); cr.pivot = new Vector2(0f, 0.5f);
        cr.sizeDelta = new Vector2(ClockW, 0f); cr.anchoredPosition = new Vector2(T.S3, 0f);

        idle = UiKit.Text(detail, "", TextStyle.Body, T.HudSub, TextAlignmentOptions.Midline);
        Ui.Anchor(idle.rectTransform, 0, 0, 1, 1, ClockW + T.S3, 0, T.S3, 0);

        track = Ui.Rect("Track", detail);
        Ui.Anchor(track, 0, 0, 1, 1, ClockW + T.S3 * 2f, TrackInsetY, ChipReserveW + T.S3 * 2f, TrackInsetY);
        var groove = UiKit.Pill(track, T.HudGroove, TrackH, "Groove"); UiKit.Fill(groove.rectTransform);

        far = MakeSeg("FarSide");
        burn = MakeSeg("BurnWindow");
        reA = MakeSeg("ReentryA");
        reB = MakeSeg("ReentryB");

        splash = UiKit.Icon(track, "earth", T.IconS, T.Go);
        splash.rectTransform.anchorMin = splash.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        splash.rectTransform.sizeDelta = new Vector2(T.IconS, T.IconS);

        past = UiKit.Pill(track, T.WithAlpha(T.Ink, 0.55f), TrackH, "Past").rectTransform;
        Ui.Anchor(past, 0, 0, 0, 1);

        nowLine = Ui.Rect("Now", track);
        nowLine.anchorMin = new Vector2(0f, 1f); nowLine.anchorMax = new Vector2(0f, 1f); nowLine.pivot = new Vector2(0.5f, 0.5f);
        nowLine.offsetMin = new Vector2(-NowW * 0.5f, -10f); nowLine.offsetMax = new Vector2(NowW * 0.5f, NowOver);
        nowLine.gameObject.AddComponent<Image>().color = T.HudText;
        var knob = UiKit.Pill(nowLine, T.HudText, NowKnob, "Knob").rectTransform;
        knob.anchorMin = knob.anchorMax = new Vector2(0.5f, 1f); knob.sizeDelta = new Vector2(NowKnob, NowKnob); knob.anchoredPosition = Vector2.zero;

        // 右：次まで（暗くしない）
        next = TimelineKit.Countdown.Make(bar, true);
        var nr = next.Root;
        nr.anchorMin = nr.anchorMax = new Vector2(1f, 0.5f); nr.pivot = new Vector2(1f, 0.5f);
        nr.sizeDelta = new Vector2(ChipReserveW, TimelineKit.CountdownH); nr.anchoredPosition = new Vector2(-T.S1, 0f);
    }

    Seg MakeSeg(string name)
    {
        var g = new Seg();
        g.Rect = Ui.Rect(name, track);
        g.Face = UiKit.Pill(g.Rect, T.Ink, TrackH, "Face"); UiKit.Fill(g.Face.rectTransform);
        g.Row = UiKit.Row(g.Rect, T.S1, TextAnchor.MiddleCenter, T.S1); UiKit.Fill(g.Row);
        g.Icon = UiKit.Icon(g.Row, null, T.IconS, T.Panel);
        g.Text = UiKit.Text(g.Row, "", TextStyle.Label, T.Panel);
        g.Badges = new TimelineKit.BadgeRow(g.Row, TimelineKit.MiniBadge);
        return g;
    }

    public override void Refresh(GameState s)
    {
        bool has = s != null;
        track.gameObject.SetActive(has);
        next.Root.gameObject.SetActive(has);
        idle.gameObject.SetActive(!has);
        SetWide(has);   // #216：事故の前は時計のチップだけ（練習の案内の「Practice 3/4」と二重にしない）
        if (!has)
        {
            clock.text = string.Format(Words.Clock, Ui.Mmss(0f));
            idle.gameObject.SetActive(false);
            return;
        }
        clock.text = string.Format(Words.Clock, Ui.Mmss(s.t));
        var mine = Ctx.Model.MySeats;

        // 帯の長さ：遅いほうのルートの着水まで（ルートを選んでも縮めない。選ばなかった側は灰色のまま残る）
        float end = Mathf.Max(s.tSplashdown, Mathf.Max(s.tSplashdownA, s.tSplashdownB));
        if (end <= 0f) return;

        // 月の裏側（通信途絶＝観測の窓）
        SetSeg(far, s.tLos, s.tAos, end, T.HudSeg, T.Panel, "comms_lost", Words.FarSide, SeatsObserve, mine);

        // 噴射窓
        bool missed = s.strictReason == "missed_burn";
        if (missed) SetSeg(burn, s.tBurnOpen, s.tBurnClose, end, T.Danger, T.Panel, "cross", Words.BurnMissed, NoSeats, mine);
        else if (s.burnDone) SetSeg(burn, s.tBurnOpen, s.tBurnClose, end, T.Go, T.Panel, "check", Words.BurnDone, NoSeats, mine);
        else SetSeg(burn, s.tBurnOpen, s.tBurnClose, end, T.Accent, T.Ink, "ignition", Words.BurnWindow, SeatsBurn, mine);

        // 再突入：ルートを選ぶ前は A・B の両方を薄く、選んだら選んだほうだけ（#148 の値が無いサーバーなら t_reentry の1本）
        string route = s.route ?? "";
        bool both = s.tReentryA > 0f && s.tReentryB > 0f;
        float splashAt = s.tSplashdown;
        if (!both)
        {
            SetSeg(reA, 0f, 0f, end, T.HudSeg, T.Panel, "heat_shield", Words.Reentry, SeatsReentry, mine);
            SetSeg(reB, s.tReentry, s.tSplashdown, end, T.HudSeg, T.Panel, "heat_shield", Words.Reentry, SeatsReentry, mine);
        }
        else if (route == "a" || route == "b")
        {
            var on = route == "a" ? reA : reB; var off = route == "a" ? reB : reA;
            SetSeg(on, s.RouteReentry(route), s.RouteSplashdown(route), end, T.HudSeg, T.Panel, "heat_shield", Words.Reentry, SeatsReentry, mine);
            SetSeg(off, 0f, 0f, end, T.HudSeg, T.Panel, "heat_shield", Words.Reentry, NoSeats, mine);
            splashAt = s.RouteSplashdown(route);
        }
        else
        {
            SetSeg(reA, s.tReentryA, s.tSplashdownA, end, T.HudSeg, T.Panel, IfIcon, Words.IfA, NoSeats, mine);
            SetSeg(reB, s.tReentryB, s.tSplashdownB, end, T.HudSeg, T.Panel, IfIcon, Words.IfB, NoSeats, mine);
        }

        // 着水（地球）・通った所・いま
        float xs = Mathf.Clamp01(splashAt / end);
        splash.rectTransform.anchorMin = splash.rectTransform.anchorMax = new Vector2(xs, 0.5f);
        splash.rectTransform.anchoredPosition = new Vector2(T.IconS * 0.5f + T.S1 * 0.5f, 0f);   // 再突入の区間のすぐ右（帯の右端なら帯の外のすき間）
        float xn = Mathf.Clamp01(s.t / end);
        past.anchorMax = new Vector2(xn, 1f);
        past.gameObject.SetActive(xn > 0f);
        nowLine.anchorMin = new Vector2(xn, 1f); nowLine.anchorMax = new Vector2(xn, 1f);

        RefreshNext(s, mine);
    }

    static float ReadyS(GameState s) =>
        J.Has(s.thresholds, "reentry_ready_s") ? J.Num(s.thresholds, "reentry_ready_s") : GameParams.Get("reentry_ready_s");

    /// <summary>右のチップ：次の出来事までの秒、窓の中なら残りの秒（BurnCheck の残り秒と同じ式：窓の終わり − t）。噴射窓の中は文字に秒を出さない（#189。残り 10 秒の赤と弾みには使う）</summary>
    void RefreshNext(GameState s, List<string> mine)
    {
        float t = s.t;
        if (t < s.tLos) next.Set("comms_lost", Words.NextFarSide, s.tLos - t, false, SeatsObserve, mine);
        else if (t < s.tAos) next.Set("comms_lost", Words.InFarSide, s.tAos - t, false, SeatsObserve, mine);
        else if (!s.burnDone && t < s.tBurnOpen) next.Set("ignition", Words.NextBurn, s.tBurnOpen - t, false, SeatsBurn, mine);
        else if (!s.burnDone && t < s.tBurnClose) next.Set("ignition", Words.InBurn, s.tBurnClose - t, true, SeatsBurn, mine);
        else if (s.burnDone && t >= s.tReentry && t < s.tReentry + ReadyS(s)) next.Set("heat_shield", Words.InReentry, s.tReentry + ReadyS(s) - t, true, SeatsReentry, mine);   // #210：準備の残り秒もここだけ
        else if (t < s.tReentry) next.Set("heat_shield", Words.NextReentry, s.tReentry - t, false, SeatsReentry, mine);
        else if (t < s.tSplashdown) next.Set("earth", Words.NextSplash, s.tSplashdown - t, false, NoSeats, mine);
        else next.Set("earth", Words.Home, -1f, false, NoSeats, mine);
    }

    /// <summary>帯の上の1区間。幅に入らなければ、文言を短いものへ → 席バッジを消す → アイコンだけ</summary>
    void SetSeg(Seg g, float t0, float t1, float end, Color face, Color fg, string icon, string[] texts, string[] seats, List<string> mine)
    {
        bool on = t1 > t0 && end > 0f;
        g.Rect.gameObject.SetActive(on);
        if (!on) return;
        Ui.Anchor(g.Rect, Mathf.Clamp01(t0 / end), 0f, Mathf.Clamp01(t1 / end), 1f);
        g.Face.color = face; g.Icon.color = fg; g.Text.color = fg;
        TimelineKit.SetIcon(g.Icon, icon);
        g.Badges.Set(seats, mine);

        float room = track.rect.width * (t1 - t0) / end - T.S1 * 2f;
        float iconW = T.IconS;
        float badgeW = g.Badges.Count > 0 ? T.S1 + g.Badges.Width : 0f;
        string pick = null; bool badges = g.Badges.Count > 0;
        if (Screen.width < CompactScreenW) texts = NoText;   // #216：狭い画面は区間の文字を消してアイコンだけ
        foreach (var tx in texts)
            if (Need(g, tx, iconW, badgeW) <= room) { pick = tx; break; }
        if (pick == null)
        {
            badges = false;
            foreach (var tx in texts)
                if (Need(g, tx, iconW, 0f) <= room) { pick = tx; break; }
        }
        g.Text.text = pick ?? "";
        g.Text.gameObject.SetActive(!string.IsNullOrEmpty(pick));
        g.Badges.Root.gameObject.SetActive(badges);
        g.Icon.gameObject.SetActive(g.Icon.sprite != null && iconW <= room);
    }

    static readonly string[] NoText = { "" };

    static float Need(Seg g, string text, float iconW, float badgeW) =>
        iconW + badgeW + (string.IsNullOrEmpty(text) ? 0f : T.S1 + g.Text.GetPreferredValues(text).x);

    public override void Tick(float now) => next.Tick(now);
}

/// <summary>
/// タイムライン（#77）・地図（#78）・同時押し（#79）で使う小さな部品。ui-guidelines 3章の「カウントダウンのチップ」「同時押しの合図」の仮の形。
/// #71 の UiKitHud.cs（CountdownChip・DualPressSignal）ができたら、ここの中身をそちらに差し替える（呼ぶ側は変えない）
/// </summary>
public static class TimelineKit
{
    // 大きさ（UiTokens に無いもの。#71 で UiTokens へ移す）
    public const float CountdownH = 64f;      // カウントダウンのチップの高さ（3章）
    public const float CountdownIcon = 32f;   // チップの timer の大きさ（3章）
    public const float UrgentS = 10f;         // 残りこの秒以下で面を danger にして、1秒ごとに弾む（3章）
    public const float Bounce = 1.08f;        // 弾む大きさ（3章）
    public const float BounceTime = 0.3f;     // 弾んで戻るまでの秒
    public const float MiniBadge = 28f;       // 帯・チップの中の小さい席バッジ（帯の高さに合わせた。3章の席バッジは 56）
    public const float BadgeGap = 4f;         // 小さい席バッジの間
    public const float DualRing = 8f;         // 同時押しの合図の輪の幅（3章）
    public const float ChipDiscD = 44f;       // チップの中のアイコンの台（#190。高さ 64 のチップに入る大きさ）
    public const float StarAlpha = 0.06f;     // 計器の星の模様（hudText の 6%。ui-guidelines 1章）

    /// <summary>計器の面（#190）：blob_panel の HudFace（色は変えない）＋小さな星の模様。影なし。root いっぱいに敷く（レイアウトの外）。面を返す</summary>
    public static Image GaugeFace(RectTransform root, float radius)
    {
        var f = UiKit.Blob(root, T.BlobPanel, T.HudFace, radius, "Face"); UiKit.Fill(f.rectTransform); UiKit.Ignore(f);
        HudStars.Add(root);
        return f;
    }

    /// <summary>見出しの帯（UiKit.Ribbon）の文字の前に、アイコンの台を入れる（#190。【11】の帯＋【01】の台）</summary>
    public static UiRibbon Ribbon(Transform p, string text, string icon, Color disc, out UiIconDisc d, Color? face = null)
    {
        var rb = UiKit.Ribbon(p, text, face);
        rb.Root.GetComponent<HorizontalLayoutGroup>().spacing = T.S2;
        d = UiKit.Disc(rb.Root, icon, disc, T.RowDiscD);
        d.Root.SetSiblingIndex(rb.Label.transform.GetSiblingIndex());   // 文字の前
        return rb;
    }

    /// <summary>アイコンを名前で替える（同じなら何もしない）。見つからなければ隠す</summary>
    public static void SetIcon(Image img, string name)
    {
        if (img == null) return;
        if (name == null) { img.sprite = null; img.enabled = false; return; }
        if (img.sprite == null || img.sprite.name != name) img.sprite = T.Icon(name);
        img.enabled = img.sprite != null;
    }

    /// <summary>"{0}" に 分:秒 を入れる。secs が負なら文言そのまま</summary>
    public static string Format(string fmt, float secs) => secs < 0f ? fmt : string.Format(fmt, Ui.Mmss(secs));

    static void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--) { var c = t.GetChild(i); c.SetParent(null); Object.Destroy(c.gameObject); }
    }

    // ================================================================== 席バッジの列
    /// <summary>小さい席バッジを横に並べる。並びが変わったときだけ作り直す。自分の席は ink の輪（UiSeatBadge の self）</summary>
    public class BadgeRow
    {
        public readonly RectTransform Root;
        readonly float d;
        string key;
        public int Count { get; private set; }
        public float Width => Count == 0 ? 0f : Count * d + (Count - 1) * BadgeGap;

        public BadgeRow(Transform p, float d)
        {
            this.d = d;
            Root = UiKit.Row(p, BadgeGap, TextAnchor.MiddleCenter);
            Root.name = "Badges";
        }

        public void Set(IList<string> seats, ICollection<string> mine)
        {
            string k = (seats == null ? "" : string.Join(",", seats)) + "|" + (mine == null ? "" : string.Join(",", mine));
            if (k == key) return;
            key = k;
            ClearChildren(Root);
            Count = 0;
            if (seats != null)
                foreach (var s in seats)
                {
                    var b = UiKit.SeatBadge(Root, s, d);
                    b.Set(s, mine != null && mine.Contains(s), false);
                    Count++;
                }
            Root.gameObject.SetActive(Count > 0);
        }
    }

    // ================================================================== カウントダウンのチップ
    /// <summary>blob_pill（暗い面の上は ink、明るい面の上は text）に、アイコンの台（go）＋文言（分:秒入り）＋席バッジ。残り UrgentS 秒で danger にして弾む（台は panel に赤い絵）</summary>
    public class Countdown
    {
        public RectTransform Root;
        Image face;
        UiIconDisc disc;
        string iconName;
        Color faceOn;
        TextMeshProUGUI text;
        BadgeRow badges;
        int lastSec = int.MinValue;
        float bounceAt = -1f;

        /// <param name="free">レイアウトの外に置くなら true（幅を中身に合わせる）。HorizontalLayoutGroup の中なら false</param>
        /// <param name="onFace">チップの面（#190：明るいパネルの上は T.Text＝数のチップの面。既定は計器の上の ink）</param>
        public static Countdown Make(Transform p, bool free, Color? onFace = null)
        {
            var c = new Countdown { faceOn = onFace ?? T.Ink };
            c.Root = UiKit.Row(p, T.S2, TextAnchor.MiddleCenter, T.S2);
            c.Root.name = "Countdown";
            var le = Ui.Size(c.Root, h: CountdownH); le.minHeight = CountdownH;
            c.face = UiKit.Blob(c.Root, T.BlobPill, c.faceOn, CountdownH * 0.5f, "Face"); UiKit.Fill(c.face.rectTransform); UiKit.Ignore(c.face);
            c.disc = UiKit.Disc(c.Root, "timer", T.Go, ChipDiscD);
            c.iconName = "timer";
            c.text = UiKit.Text(c.Root, "", TextStyle.Choice, T.Panel);
            c.badges = new BadgeRow(c.Root, MiniBadge);
            if (free)
            {
                var fit = c.Root.gameObject.AddComponent<ContentSizeFitter>();
                fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            return c;
        }

        /// <param name="secs">残りの秒（負なら数字なし）</param>
        /// <param name="canUrgent">残りが少ないとき danger にしてよいか（窓が閉じるまでの残りなど）</param>
        public void Set(string iconName, string fmt, float secs, bool canUrgent, IList<string> seats, ICollection<string> mine)
        {
            string name = iconName ?? "timer";
            bool urgent = canUrgent && secs >= 0f && secs <= UrgentS;
            if (name != this.iconName) { this.iconName = name; disc.Set(name, T.Go); }
            disc.Face.color = urgent ? T.Panel : T.Go;
            disc.Icon.color = urgent ? T.Danger : T.Panel;
            text.text = Format(fmt, secs);
            face.color = urgent ? T.Danger : faceOn;
            badges.Set(seats, mine);
            int sec = Mathf.CeilToInt(secs);
            if (urgent && sec != lastSec) bounceAt = Time.unscaledTime;
            lastSec = sec;
        }

        public void Tick(float now)
        {
            if (bounceAt < 0f) return;
            float k = (now - bounceAt) / BounceTime;
            if (k >= 1f) { bounceAt = -1f; Root.localScale = Vector3.one; return; }
            Root.localScale = Vector3.one * Mathf.Lerp(Bounce, 1f, k);
        }
    }

    // ================================================================== 同時押しの合図の1席ぶん
    public enum PressLook { Idle, Pressing, YourTurn, Waiting, Done, Missed }

    /// <summary>大きい席バッジ（96）＋外側の輪（幅 8、go）が押してから幅の秒で一周。そろったら輪が埋まり checkmark。下に一言</summary>
    public class DualBadge
    {
        public RectTransform Root;
        RectTransform pulse;
        Image ringBg, ring, mark, anyIcon;
        RectTransform anyRoot;
        UiSeatBadge badge;
        TextMeshProUGUI label;
        PressLook look;
        float fillFrom, fillDur;
        public string Seat { get; private set; }

        public static DualBadge Make(Transform p)
        {
            var b = new DualBadge();
            float d = T.BadgeL, outer = d + DualRing * 2f;
            b.Root = UiKit.Col(p, T.S1, TextAnchor.UpperCenter);
            b.Root.name = "DualBadge";
            b.pulse = Ui.Rect("Pulse", b.Root);
            UiKit.Fixed(b.pulse, outer, outer);
            b.ringBg = UiKit.Pill(b.pulse, T.Line, outer, "RingBg"); UiKit.Fill(b.ringBg.rectTransform);
            b.ring = UiKit.Pill(b.pulse, T.Go, outer, "Ring"); UiKit.Fill(b.ring.rectTransform);
            b.ring.type = Image.Type.Filled; b.ring.fillMethod = Image.FillMethod.Radial360;
            b.ring.fillOrigin = (int)Image.Origin360.Top; b.ring.fillClockwise = true; b.ring.fillAmount = 0f;
            var holder = Ui.Rect("Badge", b.pulse); UiKit.Center(holder, d, d);
            b.badge = UiKit.SeatBadge(holder, Seats.Pilot, d);
            UiKit.Center(b.badge.Root, d, d);
            var any = UiKit.Disc(holder, "crew", T.Text, d);   // だれでも1人：人のアイコンを text の台に（#190【01】）
            b.anyRoot = any.Root; UiKit.Center(b.anyRoot, d, d);
            b.anyIcon = any.Icon;
            b.mark = UiKit.Icon(holder, "check", T.IconL, T.Panel); UiKit.Center(b.mark.rectTransform, T.IconL, T.IconL);
            b.label = UiKit.Text(b.Root, "", TextStyle.Label, T.TextSoft);
            b.SetSeat(Seats.Pilot, false);
            b.SetLook(PressLook.Idle, "", 0f);
            return b;
        }

        /// <summary>席（null なら「だれでも1人」＝人のアイコン）</summary>
        public void SetSeat(string seat, bool mine)
        {
            Seat = seat;
            bool any = seat == null;
            badge.Root.gameObject.SetActive(!any);
            anyRoot.gameObject.SetActive(any);
            if (!any) badge.Set(seat, mine, false);
        }

        public void SetLook(PressLook l, string caption, float dur)
        {
            look = l; label.text = caption;
            fillFrom = Time.unscaledTime; fillDur = Mathf.Max(0.05f, dur);
            ring.color = l == PressLook.Missed ? T.Danger : T.Go;
            ring.fillAmount = l == PressLook.Done || l == PressLook.Missed ? 1f : 0f;
            mark.gameObject.SetActive(l == PressLook.Done || l == PressLook.Missed);
            SetIcon(mark, l == PressLook.Missed ? "cross" : "check");
            if (!badge.Root.gameObject.activeSelf) anyIcon.gameObject.SetActive(!mark.gameObject.activeSelf);
            else badge.Shape.gameObject.SetActive(!mark.gameObject.activeSelf);
            mark.color = badge.Root.gameObject.activeSelf ? T.OnSeat(Seat) : T.Panel;   // だれでも1人の台（text）の上は白
            label.color = l == PressLook.YourTurn ? T.Text : T.TextSoft;
            pulse.localScale = Vector3.one;
        }

        public void Tick(float now)
        {
            if (look == PressLook.Pressing) ring.fillAmount = Mathf.Clamp01((now - fillFrom) / fillDur);
            float s = look == PressLook.YourTurn ? 1f + 0.06f * Mathf.Abs(Mathf.Sin((now - fillFrom) * Mathf.PI * 3f)) : 1f;
            pulse.localScale = Vector3.one * s;
        }
    }
}

/// <summary>
/// 計器の面の小さな星の模様（#190。ui-guidelines 1章「船内の計器」：hudText の 6% で敷く。【07】の淡い地模様の作り）。
/// 画像を使わず、4つの角の星を頂点で描く。rect に合わせて敷き直す（大きさが変わると Unity が描き直す）。並びは決まった乱数で、毎回同じ
/// </summary>
public class HudStars : MaskableGraphic
{
    public float cell = 56f;     // 1つの星の区画（px）
    public float inset = 20f;    // 面の縁から離す（blob_panel の丸い角からはみ出さない）
    public float minR = 4f, maxR = 7f;   // 星の大きさ（中心から先まで）
    public float pinch = 0.32f;  // 星のくびれ（先の長さに対する、間の長さ）

    /// <summary>root いっぱいに星を敷く（レイアウトの外。押せない）</summary>
    public static HudStars Add(RectTransform root)
    {
        var r = Ui.Rect("Stars", root);
        var s = r.gameObject.AddComponent<HudStars>();
        s.color = UiTokens.WithAlpha(UiTokens.HudText, TimelineKit.StarAlpha);
        s.raycastTarget = false;
        UiKit.Fill(r); UiKit.Ignore(s);
        return s;
    }

    static float Hash(int x, int y, int k)
    {
        uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(k * 83492791);
        h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
        return (h & 0xFFFF) / 65535f;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = GetPixelAdjustedRect();
        float w = r.width - inset * 2f, h = r.height - inset * 2f;
        if (w <= 0f || h <= 0f) return;
        int nx = Mathf.Max(1, Mathf.FloorToInt(w / cell)), ny = Mathf.Max(1, Mathf.FloorToInt(h / cell));
        float cw = w / nx, ch = h / ny;
        Color32 c = color;
        for (int j = 0; j < ny; j++)
            for (int i = 0; i < nx; i++)
            {
                if (Hash(i, j, 0) < 0.35f) continue;   // 3つに1つは空ける（並びが格子に見えないように）
                float rad = Mathf.Lerp(minR, maxR, Hash(i, j, 1));
                float px = r.xMin + inset + cw * (i + Mathf.Lerp(0.2f, 0.8f, Hash(i, j, 2)));
                float py = r.yMin + inset + ch * (j + Mathf.Lerp(0.2f, 0.8f, Hash(i, j, 3)));
                int c0 = vh.currentVertCount;
                vh.AddVert(new Vector3(px, py), c, Vector2.zero);
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI * 0.25f + Mathf.PI * 0.5f;
                    float len = k % 2 == 0 ? rad : rad * pinch;
                    vh.AddVert(new Vector3(px + Mathf.Cos(a) * len, py + Mathf.Sin(a) * len), c, Vector2.zero);
                }
                for (int k = 0; k < 8; k++) vh.AddTriangle(c0, c0 + 1 + k, c0 + 1 + (k + 1) % 8);
            }
    }
}
