// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#76）／ 更新：iizuka（Opus 5.5、2026-11-14、#146 声なしの警告もこの帯に）
// 作る Issue：#76（事故の警報と危機の帯）
//
// 中央上の危機の帯（同時に1本、ui-guidelines 3章「警告の帯」）。練習の案内（#84）と同じ場所で、同時には出ない（帯は flight の間だけ）
//   ・事故の瞬間（M05 の台詞／stir → flight）：MASTER ALARM の大きな帯を Look.AlarmSec 秒、点滅させる（赤い縁・揺れ・ランプは UrgencyFx）
//   ・そのあと：いま一番まずいこと1つ（UrgencyFx の Level・Cause）＝ 注意／危険のアイコン＋原因のアイコン＋短い言葉＋直す席のバッジ＋だれが何をするか1行
//     段階 1 は注意（accent に ink の文字）、2・3 は危険（danger に白文字）。自分の席が直す番なら、バッジに ink の輪（自分の席の印）
//   ・ほかの危機は帯の右に小さなアイコンで待機表示（state とサーバーの閾値 thresholds を見るだけ。どれを前に出すかは UrgencyFx が決める）
//   ・ヒューストンが話している間（houston イベントから少しの間）は帯を小さく（#75 と二重に大きく出さない）。通信途絶・通信 OFF のときは小さくしない（帯が主役）
//   ・照明 OFF でも暗くしない（Detail を持たない。#87）
//   ・声なしの警告（event kind=warning：通信途絶・通信 OFF の間のヒューストンの台詞）も、この帯の1行に Look.WarnSec 秒出す（2行まで）。
//     前は HoustonBox の上に別の赤い帯を出していて、危機の帯と2本並んだ（#146。ui-guidelines 7章：帯は1本）。危機が無いときは NO RADIO の帯で出す
//   ・画面の縁の赤（UrgencyFx の枠）と溶けないように、帯の外側に panel の輪と影を付ける。帯そのものは脈打たせない（脈は縁だけ）
// 文言・アイコン・席は下の Crises の表の1か所（文言は Taiga が決める。今は houston_lines.json の H07・H11・H14・H16・H18・H26 に合わせた仮）
// 見た目の値は Look の1か所。帯の形は自分の中の Band にまとめた（#71 の HudKit.Banner ができたら差し替える）
// UrgencyFx は Object.FindAnyObjectByType で探す（シーンに参照を足さない）。無ければ MASTER ALARM だけ出す
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;

public class CrisisBanner : HudPart
{
    // ================================================================== 文言・アイコン・席（直すのはここ）
    class Crisis
    {
        public string cause;   // UrgencyFx.Cause と同じ名前（待機の判定もこの名前）
        public string icon;    // ui-guidelines 4章のアイコン（Art/UI/Icons のファイル名）。null なら glyph の文字を円に入れる
        public string glyph;   // アイコンが無いときの文字（酸素 = O2、ui-guidelines 4章）
        public string title;   // 短い言葉（Baloo 32）
        public string seat;    // 直す席（席バッジ）。null なら出さない
        public string line;    // だれが何をするか（1行）
    }

    // 並び = 待機のアイコンの並び（#76 の表の順：必須 → 余裕があれば）
    static readonly Crisis[] Crises =
    {
        new Crisis { cause = "leak",   icon = "valve",       title = "OXYGEN LEAK", seat = Seats.Life,    line = "Life Support: close the leaking valve" },
        new Crisis { cause = "power",  icon = "power",       title = "LOW POWER",   seat = Seats.Comms,   line = "Comms & Power: shut down extra systems" },
        new Crisis { cause = "oxygen", glyph = "O2",         title = "LOW OXYGEN",  seat = Seats.Planner, line = "Mission Planner: pick the faster route" },
        new Crisis { cause = "co2",    icon = "co2",         title = "HIGH CO2",    seat = Seats.Life,    line = "Life Support + a teammate: swap the filter" },
        new Crisis { cause = "temp",   icon = "temperature", title = "TOO COLD",    seat = Seats.Comms,   line = "Comms & Power: turn the heater on" },
        new Crisis { cause = "fuel",   icon = "fuel",        title = "LOW FUEL",    seat = Seats.Pilot,   line = "Pilot: turn the ship less" },
    };
    /// <summary>表に無い原因（UrgencyFx の debugLevel の "debug" など）</summary>
    static readonly Crisis Unknown = new Crisis { cause = "", icon = "alarm", title = "WARNING", line = "Check the gauges" };
    /// <summary>危機が無いときに声なしの警告だけ出す帯（1行はヒューストンの台詞。席は台詞に出てきた席）</summary>
    static readonly Crisis Radio = new Crisis { cause = "", icon = "comms_lost", title = "NO RADIO", line = "" };
    const string AlarmText = "MASTER ALARM";

    // ================================================================== 見た目の値（直すのはここ）
    static class Look
    {
        public const float BandH = 72, BandPadX = 40;   // ui-guidelines 3章「警告の帯」（#71 の T.BannerH・T.BannerPadX と同じ値）
        public const float MaxW = 880;                  // 帯の最大の幅。CabinHud.Layout の CenterTop（0.22〜0.78 ＝ 896）に収める（#146）
        public const float WarnH = 96;                  // 声なしの警告を出している間の帯の高さ（1行を2行まで折り返す）
        public const float WarnSec = HoustonStyle.WarnS; // 声なしの警告を出しておく秒（前の HoustonBox の帯と同じ）
        public const float BadgeD = T.BadgeS;           // 直す席のバッジ
        public const float StandbyD = T.TagH;           // 待機のアイコンの円
        public const int StandbyMax = 3;                // 待機のアイコンは3つまで
        public const float AlarmH = 96;                 // MASTER ALARM の帯の高さ（主ボタンと同じ）
        public const float AlarmSec = 2.5f;             // MASTER ALARM を出す秒（#76「2〜3秒」）
        public const float AlarmBlinkHz = 2f;
        public const float AlarmBlinkLow = 0.55f;       // 点滅の暗いほうの不透明度
        public const float VoiceScale = 0.8f;           // ヒューストンが話している間の帯の大きさ
        public const float VoiceScaleSpeed = 3f;        // 大きさが変わる速さ（/秒）
        public const float VoiceBaseSec = 4f, VoicePerChar = 0.05f;   // 話している時間の見積もり（HoustonBox の読む時間と同じ式）
        public const float BounceScale = 1.08f, BounceSec = 0.25f;    // 危機が替わったときに弾む（ui-guidelines 3章のカウントダウンと同じ値）
        public const float FindEverySec = 1f;           // UrgencyFx を探し直す間隔
    }

    // ================================================================== 状態
    Band band;
    Alarm alarm;
    UrgencyFx fx;
    float nextFind;
    bool warnedNoFx;

    int shownLevel; string shownCause = "";
    bool dirty;                       // OnLevelChanged が来た（次の Tick で描き直す）
    float bounceAt = -99f;
    float voiceUntil, voiceK = 1f, lastNow;
    string lastPhase = "";
    string warnText = "", warnPriority = ""; float warnUntil; bool warnShown;   // 声なしの警告（#146）
    bool alarmDone; float alarmStart, alarmUntil;

    protected override void Build(RectTransform root)
    {
        band = Band.Make(root);
        alarm = Alarm.Make(root);
        band.Root.gameObject.SetActive(false);
        alarm.Root.gameObject.SetActive(false);
    }

    // ================================================================== サーバーから
    public override void Refresh(GameState s)
    {
        string ph = Ctx.Model.Phase;
        if (ph != lastPhase)
        {
            if (lastPhase == "stir" && ph == "flight") TriggerAlarm();   // 事故（M05）
            if (ph != "stir" && ph != "flight") alarmDone = false;       // 次の飛行でまた鳴らす
            lastPhase = ph;
        }
        if (band.Root.gameObject.activeSelf && UpdateStandby(s)) Fit();
    }

    public override void OnEvent(GameEvent e)
    {
        if (e.kind == "warning")
        {
            warnText = HoustonScript.Clean(e.text ?? ""); warnPriority = e.priority ?? "";
            warnUntil = warnText != "" ? Time.unscaledTime + Look.WarnSec : 0f;
            dirty = true; bounceAt = Time.unscaledTime;
            return;
        }
        if (e.kind != "houston") return;
        if (e.id == "M05") TriggerAlarm();
        if (!string.IsNullOrEmpty(e.text))
            voiceUntil = Time.unscaledTime + Look.VoiceBaseSec + e.text.Length * Look.VoicePerChar;
    }

    void TriggerAlarm()
    {
        if (alarmDone) return;   // M05 の台詞と stir → flight の両方で呼ばれる。1回だけ
        alarmDone = true;
        alarmStart = Time.unscaledTime;
        alarmUntil = alarmStart + Look.AlarmSec;
    }

    // ================================================================== 毎フレーム
    public override void Tick(float now)
    {
        float dt = lastNow > 0f ? Mathf.Clamp(now - lastNow, 0f, 0.1f) : 0f;
        lastNow = now;
        FindFx(now);

        // MASTER ALARM（帯は同時に1本なので、鳴っている間は危機の帯を出さない）
        bool alarmOn = now < alarmUntil;
        if (alarm.Root.gameObject.activeSelf != alarmOn) alarm.Root.gameObject.SetActive(alarmOn);
        if (alarmOn) alarm.Group.alpha = Mathf.Repeat((now - alarmStart) * Look.AlarmBlinkHz, 1f) < 0.5f ? 1f : Look.AlarmBlinkLow;

        // 危機の帯
        int level = fx != null && Ctx.Model.Phase == "flight" ? fx.Level : 0;
        string cause = fx != null ? fx.Cause : "";
        bool warnOn = now < warnUntil && Ctx.Model.Phase == "flight";
        bool show = !alarmOn && (level > 0 || warnOn);
        if (band.Root.gameObject.activeSelf != show) band.Root.gameObject.SetActive(show);
        if (!show) { shownLevel = 0; shownCause = ""; warnShown = false; dirty = false; return; }
        if (dirty || level != shownLevel || cause != shownCause || warnOn != warnShown) Apply(level, cause, warnOn);

        // 大きさ：ヒューストンが話している間は小さく、危機が替わったら弾む
        float target = VoiceActive(now) ? Look.VoiceScale : 1f;
        voiceK = Mathf.MoveTowards(voiceK, target, Look.VoiceScaleSpeed * dt);
        float bt = now - bounceAt, k = 1f;
        if (bt >= 0f && bt < Look.BounceSec) k = 1f + (Look.BounceScale - 1f) * Mathf.Sin(Mathf.PI * bt / Look.BounceSec);
        band.Root.localScale = Vector3.one * (voiceK * k);
    }

    /// <summary>ヒューストンの声が出ているか。通信途絶・通信 OFF の間は声が無いので、帯が主役（小さくしない）</summary>
    bool VoiceActive(float now)
    {
        var s = Ctx.State;
        return now < voiceUntil && s != null && s.hasSignal && s.Switch("comms");
    }

    // ------------------------------------------------------------------ UrgencyFx をつなぐ
    void FindFx(float now)
    {
        if (fx != null || now < nextFind) return;
        nextFind = now + Look.FindEverySec;
        fx = Object.FindAnyObjectByType<UrgencyFx>();
        if (fx != null) fx.OnLevelChanged += OnLevel;
        else if (!warnedNoFx) { warnedNoFx = true; Debug.LogWarning("[CrisisBanner] シーンに UrgencyFx が無い。危機の帯は出さない（MASTER ALARM だけ）"); }
    }

    void OnLevel(int prev, int next)
    {
        if (Root == null) { if (fx != null) fx.OnLevelChanged -= OnLevel; return; }   // HUD が消えた
        dirty = true;
        if (next > prev) bounceAt = Time.unscaledTime;   // 悪くなったときだけ弾む
    }

    // ================================================================== 描く
    void Apply(int level, string cause, bool warn)
    {
        if (level > shownLevel || cause != shownCause) bounceAt = Time.unscaledTime;
        shownLevel = level; shownCause = cause; warnShown = warn; dirty = false;
        var c = level > 0 ? Find(cause) ?? Unknown : Radio;
        bool danger = level >= 2 || (warn && warnPriority == "critical");
        Color face = danger ? T.Danger : T.Accent, fg = danger ? T.Panel : T.Ink;
        band.Face.color = face;
        band.LevelIcon.sprite = T.Icon(danger ? "danger" : "caution"); band.LevelIcon.color = fg;
        band.Cause.Set(c, fg, face);
        band.Title.text = c.title; band.Title.color = fg;
        band.Title.gameObject.SetActive(!warn);   // 警告の台詞を出す間は短い言葉を隠して、台詞に幅をあげる（2行で 110 字ほど入る）
        band.Line.text = warn ? warnText : c.line; band.Line.color = fg;
        band.Line.textWrappingMode = warn ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;   // 警告の台詞は長いので2行まで
        band.Root.sizeDelta = new Vector2(band.Root.sizeDelta.x, warn ? Look.WarnH : Look.BandH);
        string seat = c.seat;
        if (seat == null && warn) { var seats = HoustonStyle.SeatsIn(warnText); if (seats.Count > 0) seat = seats[0]; }
        band.Badge.Root.gameObject.SetActive(seat != null);
        if (seat != null) band.Badge.Set(seat, Ctx.Model.MySeats.Contains(seat), false);
        UpdateStandby(Ctx.State);
        Fit();
    }

    static Crisis Find(string cause)
    {
        foreach (var c in Crises) if (c.cause == cause) return c;
        return null;
    }

    /// <summary>待機のアイコン（前に出ている物以外で、今まずい物）。変わったら true</summary>
    bool UpdateStandby(GameState s)
    {
        bool changed = false; int n = 0;
        for (int i = 0; i < Crises.Length; i++)
        {
            bool on = s != null && n < Look.StandbyMax && Crises[i].cause != shownCause && Waiting(Crises[i].cause, s);
            if (on) n++;
            var go = band.Standby[i].Root.gameObject;
            if (go.activeSelf != on) { go.SetActive(on); changed = true; }
        }
        band.StandbyCount = n;
        bool row = n > 0;
        if (band.StandbyRow.gameObject.activeSelf != row) { band.StandbyRow.gameObject.SetActive(row); changed = true; }
        return changed;
    }

    /// <summary>待機に出すか：サーバーの値（state・thresholds）を比べるだけ。閾値が届いていなければ出さない</summary>
    static bool Waiting(string cause, GameState s)
    {
        float v;
        switch (cause)
        {
            case "leak": return s.leakOpen && (!GameParams.TryGet("oxygen_leak_max", out v) || s.leaked < v);   // 漏れは oxygen_leak_max で止まる
            case "power": return s.capacity > 0f && s.Threshold("power_frac", "half", out v) && s.power < s.capacity * v;
            case "oxygen": return s.Threshold("oxygen", "mid", out v) && s.oxygen < v;
            case "co2": return s.Threshold("co2", "warn", out v) && s.co2 > v;
            case "temp": return s.Threshold("temp", "slow", out v) && s.temp < v;
            case "fuel": return !s.burnDone && s.Threshold("fuel", "low", out v) && s.fuel < v;   // 点火の後の燃料は気にしなくてよい
            default: return false;
        }
    }

    /// <summary>帯の幅を Look.MaxW までに収める：1行の文字を本文（28）→ 補助ラベル（22）に下げ、それでも長ければ … で切る</summary>
    void Fit()
    {
        bool title = band.Title.gameObject.activeSelf;
        float titleW = title ? band.Title.GetPreferredValues(band.Title.text).x : 0f;
        float w = Look.BandPadX * 2f + T.IconM * 2f + titleW + T.S2 * (title ? 3f : 2f);   // 注意のアイコン・原因のアイコン・短い言葉と、その間（1行の文字の前まで）
        if (band.Badge.Root.gameObject.activeSelf) w += Look.BadgeD + T.S2;
        if (band.StandbyCount > 0) w += T.S2 + band.StandbyCount * Look.StandbyD + (band.StandbyCount - 1) * T.S1;
        float budget = Mathf.Max(0f, Look.MaxW - w);
        band.Line.fontSize = T.TextBody;
        float lineW = band.Line.GetPreferredValues(band.Line.text).x;
        if (lineW > budget) { band.Line.fontSize = T.TextLabel; lineW = band.Line.GetPreferredValues(band.Line.text).x; }
        band.LineSize.preferredWidth = Mathf.Min(lineW, budget);
    }

    // ================================================================== 部品（#71 の HudKit.Banner ができたら差し替える）
    /// <summary>白いアイコンか、円に文字（O2）。色は Set で付ける</summary>
    class Glyph
    {
        public RectTransform Root;
        public Image Icon, Chip;
        public TextMeshProUGUI Text;

        /// <param name="d">円の直径</param><param name="iconD">アイコンの大きさ</param>
        public static Glyph Make(Transform p, float d, float iconD, string name)
        {
            var g = new Glyph();
            g.Root = Ui.Rect(name, p);
            g.Root.sizeDelta = new Vector2(d, d);
            UiKit.Fixed(g.Root, d, d);
            g.Chip = UiKit.Pill(g.Root, T.Ink, d, "Chip"); UiKit.Fill(g.Chip.rectTransform);
            g.Text = UiKit.Text(g.Chip.transform, "", TextStyle.Tag, T.Panel, TextAlignmentOptions.Center, false); UiKit.Fill(g.Text.rectTransform);
            g.Icon = UiKit.Shape(g.Root, null, T.Ink, 0f, "Icon"); g.Icon.preserveAspect = true;
            UiKit.Center(g.Icon.rectTransform, iconD, iconD);
            return g;
        }

        /// <param name="fg">アイコン・円の色</param><param name="bg">円の中の文字の色</param>
        public void Set(Crisis c, Color fg, Color bg)
        {
            bool icon = c.icon != null;
            Icon.gameObject.SetActive(icon);
            Chip.gameObject.SetActive(!icon);
            if (icon) { Icon.sprite = T.Icon(c.icon); Icon.color = fg; }
            else { Chip.color = fg; Text.text = c.glyph; Text.color = bg; }
        }
    }

    /// <summary>危機の帯：ピル（高さ 72）。影・panel の輪・面の3枚を敷き、中身を横に並べる。幅は中身に合わせる（上の中央に吊る）</summary>
    class Band
    {
        public RectTransform Root, StandbyRow;
        public Image Face, LevelIcon;
        public Glyph Cause;
        public TextMeshProUGUI Title, Line;
        public LayoutElement LineSize;
        public UiSeatBadge Badge;
        public Glyph[] Standby;
        public int StandbyCount;

        public static Band Make(RectTransform parent)
        {
            var b = new Band();
            b.Root = UiKit.Row(parent, T.S2, TextAnchor.MiddleCenter, Look.BandPadX);
            b.Root.name = "CrisisBand";
            HangTopCenter(b.Root, Look.BandH);
            b.Face = PillBack(b.Root, Look.BandH, T.Danger);

            b.LevelIcon = UiKit.Icon(b.Root, "danger", T.IconM, T.Panel);
            b.Cause = Glyph.Make(b.Root, T.IconM, T.IconM, "Cause");
            b.Title = UiKit.Text(b.Root, "", TextStyle.Sub, T.Panel);
            b.Badge = UiKit.SeatBadge(b.Root, Seats.Life, Look.BadgeD);
            b.Line = UiKit.Text(b.Root, "", TextStyle.Body, T.Panel, TextAlignmentOptions.MidlineLeft);
            b.Line.overflowMode = TextOverflowModes.Ellipsis;
            b.LineSize = Ui.Size(b.Line);

            // 待機：panel の小さな円に ink のアイコン
            b.StandbyRow = UiKit.Row(b.Root, T.S1, TextAnchor.MiddleCenter);
            b.StandbyRow.name = "Standby";
            b.Standby = new Glyph[Crises.Length];
            for (int i = 0; i < Crises.Length; i++)
            {
                var g = Glyph.Make(b.StandbyRow, Look.StandbyD, T.IconS, "Standby_" + Crises[i].cause);
                var bg = UiKit.Pill(g.Root, T.Panel, Look.StandbyD, "Face"); UiKit.Fill(bg.rectTransform);
                bg.transform.SetAsFirstSibling();
                g.Set(Crises[i], T.Ink, T.Panel);
                if (Crises[i].icon == null) { g.Chip.color = T.Clear; g.Text.color = T.Ink; }   // O2 は円を重ねず、文字だけ
                g.Root.gameObject.SetActive(false);
                b.Standby[i] = g;
            }
            b.StandbyRow.gameObject.SetActive(false);
            return b;
        }
    }

    /// <summary>MASTER ALARM：大きいピル（高さ 96）。警報のアイコン＋文字＋警報のアイコン。CanvasGroup で点滅</summary>
    class Alarm
    {
        public RectTransform Root;
        public CanvasGroup Group;

        public static Alarm Make(RectTransform parent)
        {
            var a = new Alarm();
            a.Root = UiKit.Row(parent, T.S3, TextAnchor.MiddleCenter, Look.BandPadX);
            a.Root.name = "MasterAlarm";
            HangTopCenter(a.Root, Look.AlarmH);
            a.Group = a.Root.gameObject.AddComponent<CanvasGroup>();
            a.Group.blocksRaycasts = false; a.Group.interactable = false;
            PillBack(a.Root, Look.AlarmH, T.Danger);
            UiKit.Icon(a.Root, "alarm", T.IconL, T.Panel);
            UiKit.Text(a.Root, AlarmText, TextStyle.Big, T.Panel);
            UiKit.Icon(a.Root, "alarm", T.IconL, T.Panel);
            return a;
        }
    }

    /// <summary>親の上の中央に吊る。高さ h、幅は中身に合わせる</summary>
    static void HangTopCenter(RectTransform r, float h)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = new Vector2(0f, h);
        r.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    /// <summary>ピルの裏：影（下に ShadowY）・panel の輪（外に Border。画面の縁の赤と分ける）・面。どれもレイアウトの外。面を返す</summary>
    static Image PillBack(RectTransform r, float h, Color face)
    {
        float ringH = h + T.Border * 2f;
        var sh = UiKit.Pill(r, T.Shadow, ringH, "Shadow"); UiKit.Fill(sh.rectTransform, -T.Border); UiKit.Ignore(sh);
        sh.rectTransform.anchoredPosition = new Vector2(0f, -T.ShadowY);
        var ring = UiKit.Pill(r, T.Panel, ringH, "Ring"); UiKit.Fill(ring.rectTransform, -T.Border); UiKit.Ignore(ring);
        var f = UiKit.Pill(r, face, h, "Face"); UiKit.Fill(f.rectTransform); UiKit.Ignore(f);
        return f;
    }
}
