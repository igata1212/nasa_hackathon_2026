// 書いた人：Claude ／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で CabinHud から分けた）／ 更新：iizuka（Opus 5.5、2026-11-14、#74・#131）／ 更新：iizuka（Opus 5.5、2026-11-14、#146 時計を消した）
// 作る Issue：#74（常に見える状態）＋ #131（ゲージの色の閾値を parameters.json に合わせる）
//
// 左のパネル。上にゲージ5本（電力・酸素・温度・CO2・燃料）、下に向き・データ、いちばん下に生還ラインの説明（時計は TimelineBar だけ。#146）。
//   状態は「色＋アイコン＋言葉」：安全＝青緑（印なし）／注意＝オレンジ＋alert-circle＋LOW など／危険＝クリムゾン＋warning＋斜めの縞＋行の後ろも赤（ui-guidelines 2章 #109）。
//   緑・黄・赤は使わない。色は UiTokens、文言・アイコン・大きさはこのファイルの上の「直しやすい所」の1か所。
// 閾値（#131）：state の thresholds（#148。server/game/room.py の thresholds()）が正。比べ方は server/game/houston.py と同じ
//   （電力 < 容量×power_frac、酸素 < oxygen、燃料 < fuel.low、CO2 > co2.warn・>= co2.slow、温度 < temp）。
//   thresholds が届かないときは parameters.json（GameParams）の co2_warn などで代わりにする。電力・酸素・燃料の閾値は config.py にしか無いので、そのときは色を変えない。
//   UI は判定をしない：サーバーが送った閾値と比べて色を選ぶだけ。ほかに使うサーバーの値は leak_open・leaked・route・burn_done・strict
// 数字はサーバーの値そのまま（電力・酸素・燃料は parameters.json の単位 %、温度は ℃）。ゲージの満タンは 電力＝capacity、酸素・燃料＝設計の合計（Model.Max）、温度＝temp_start、CO2＝100（SPEC 3章の範囲）
// 生還ライン（ink の縦線）：電力・酸素＝end_min（strict のときは ＋strict_margin。reference_sim の result と同じ）、温度＝end_min.temp、燃料＝点火に要る量（選んだルートの fuel_burn_a/b。選ぶ前は B）
// 練習・かき混ぜの間（state が来ない）は、設計の合計で満タンの静かな表示（#74「M03・M04 は静かな表示」）
// 左の領域（CabinHud.Layout の Left）に入りきらないときは、パネルごと縮める（Fit）。Detail：パネル全体（照明 OFF で暗くしてよい）
// 共通のゲージ（#71 の UiKitHud.Gauge）ができたら、下の MiniGauge だけを差し替える
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;

public class StatusPanel : HudPart
{
    // ================================================================== 直しやすい所（文言・アイコン・大きさ）
    enum Level { Safe = 0, Caution = 1, Danger = 2 }

    /// <summary>大きさ（px、1600×900 基準）と動き。UiTokens に無いものだけ（#71 の UiKitHud ができたらそちらへ寄せる）</summary>
    static class Look
    {
        public const float Pad = T.S2;                 // パネルの内側（ガイドラインは 24。左の領域が狭いので 16）
        public const float Gap = T.S1;                 // 行の間
        public const float NameH = 22, BarH = 30;      // ゲージの行：上に名前（＋状態）、下にゲージと数字
        public const float RowH = NameH + BarH;
        public const float ChipD = 48, ChipIcon = 32;  // 左の円とアイコン
        public const float GaugeH = 28;                // ゲージのピル（ガイドライン 3章）
        public const float NumW = 88;                  // 数字の箱（右寄せで桁ぶれを防ぐ）
        public const TextStyle Num = TextStyle.Sub;    // ガイドラインは 40（TextStyle.Number）。左の領域が狭いので 32
        public const float StatusIcon = 22;            // 名前の横の注意・危険のアイコン
        public const float LineW = 4, LineOver = 6;    // 生還ライン（幅 4、上下に 6 はみ出す）
        public const float InfoH = 32, InfoIcon = T.IconS, ValueIcon = 28;   // 下の行（向き・データ）
        public const float LegendH = 28, LegendBarH = 22;
        // 高さの合計：内側 32 ＋ ゲージ 52×5 ＋ 間 8×5 ＋ 下の行 32×2 ＋ 説明 28 ＝ 424。Left（#146：1600×900 で 459）に等倍で入る（入らなければ Fit で縮む）
        public const float TintCaution = 0.22f, TintDanger = 0.14f;   // 注意・危険の行の後ろに敷く色の濃さ
        public const float TintPadX = T.S1, TintPadY = 2f;
        public const float StripeScale = 2f;           // 縞のタイル 64 を 32 に（細いゲージで縞が見えるように）
        public const float PulseHz = 1f, PulseAmp = 0.08f;            // 危険の円が弾む（カウントダウンのチップと同じ 1.08 倍）
        public const float CaretW = 24, CaretH = 14, CaretGap = 4, CaretBob = 4, CaretPeriod = 0.6f;   // 酸素が漏れている ▼
    }

    class RowDef
    {
        public string key, label, icon, chipText, unitParam;
        public string[] words;     // Safe・Caution・Danger のときに名前の横に出す言葉
        public bool endIsDanger;   // 生還ラインの下なら危険（UrgencyFx の段階3 と同じ：電力・酸素）
    }

    static readonly RowDef[] Defs =
    {
        new RowDef { key = "power",  label = "POWER",  icon = "power",       unitParam = "power_per_battery", words = new[] { "", "LOW", "VERY LOW" }, endIsDanger = true },
        new RowDef { key = "oxygen", label = "OXYGEN", chipText = "O₂", unitParam = "oxygen_start",      words = new[] { "", "LOW", "VERY LOW" }, endIsDanger = true },
        new RowDef { key = "temp",   label = "TEMP",   icon = "temperature", unitParam = "temp_start",        words = new[] { "", "COLD", "TOO COLD" } },
        new RowDef { key = "co2",    label = "CO₂", icon = "co2",       unitParam = "co2_start",         words = new[] { "", "HIGH", "TOO HIGH" } },
        new RowDef { key = "fuel",   label = "FUEL",   icon = "fuel",        unitParam = "fuel_start",        words = new[] { "", "LOW", "NOT ENOUGH" } },
    };
    const string WordLeak = "LEAKING";   // 酸素が漏れている間（leak_open）。危険にする

    // 船の向き（state の attitude → 言葉・アイコン）。Pilot のパネル（#80）と言葉をそろえるときはここ
    static readonly Dictionary<string, (string word, string icon)> Facing = new Dictionary<string, (string, string)>
    {
        { "earth", ("EARTH", "earth") }, { "moon", ("MOON", "moon") }, { "sun", ("SUN", "sun") },
        { "burn", ("BURN", "ignition") }, { "heatshield", ("HEAT SHIELD", "heat_shield") },
    };
    const string LabelFacing = "FACING", LabelData = "DATA SENT";
    const string DataFormat = "{0} / {1}";           // 送った量 / 集めた量
    const string Legend = "Keep above the line";     // 生還ライン（ink の縦線）の説明
    const float Co2Scale = 100f;                     // SPEC 3章：co2 は 0〜100（reference_sim も 100 で止める）

    // ================================================================== 中身
    class Row
    {
        public RowDef def;
        public RectTransform chip;
        public Image tint, chipFace, chipIcon, statusIcon;
        public TextMeshProUGUI chipText, name, word, value;
        public MiniGauge gauge;
        public Level level = (Level)(-1);
    }

    readonly Dictionary<string, Row> rows = new Dictionary<string, Row>();
    RectTransform panel;
    Image facingIcon;
    TextMeshProUGUI facingText, dataText;
    float fitScale = -1f, fitW = -1f;

    protected override void Build(RectTransform root)
    {
        RootAsDetail();
        panel = Ui.Rect("Panel", root);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 1f);
        panel.anchoredPosition = Vector2.zero;
        UiKit.Surface(panel, T.RadiusL, T.Panel);
        var vl = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        int pad = Mathf.RoundToInt(Look.Pad);
        vl.padding = new RectOffset(pad, pad, pad, pad);
        vl.spacing = Look.Gap; vl.childAlignment = TextAnchor.UpperLeft;
        vl.childControlWidth = vl.childControlHeight = true;
        vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;

        foreach (var d in Defs) rows[d.key] = BuildRow(panel, d);

        // 下の行は詰めて並べる（行の高さに余白があるので間をあけない）
        var info = UiKit.Col(panel, 0f, TextAnchor.UpperLeft, 0f, 0f, true);
        info.name = "Info";
        facingText = InfoRow(info, "attitude", LabelFacing, out facingIcon);
        dataText = InfoRow(info, "science", LabelData, out _);
        // TIME（T+）の行は出さない：時計はタイムラインの帯（TimelineBar）の左に1つだけ（#146 で2つあったのを1つに）
        BuildLegend(info);

        Quiet();
    }

    Row BuildRow(Transform parent, RowDef d)
    {
        var r = new Row { def = d };
        var row = UiKit.Row(parent, T.S1, TextAnchor.MiddleLeft);
        row.name = "Row_" + d.key;
        var le = Ui.Size(row, h: Look.RowH); le.minHeight = Look.RowH;

        // 注意・危険のときに行の後ろに敷く色（レイアウトの外）
        r.tint = UiKit.Round(row, T.RadiusS, T.Clear, "Tint"); UiKit.Ignore(r.tint);
        var tr = r.tint.rectTransform; UiKit.Fill(tr);
        tr.offsetMin = new Vector2(-Look.TintPadX, -Look.TintPadY); tr.offsetMax = new Vector2(Look.TintPadX, Look.TintPadY);

        // 左の円（アイコン。酸素はアイコンが無いので O₂ の文字。ガイドライン 4章）
        r.chip = Ui.Rect("Chip", row); UiKit.Fixed(r.chip, Look.ChipD, Look.ChipD);
        r.chipFace = UiKit.Pill(r.chip, T.Bg, Look.ChipD, "Face"); UiKit.Fill(r.chipFace.rectTransform);
        if (d.icon != null)
        {
            r.chipIcon = UiKit.Icon(r.chip, d.icon, Look.ChipIcon, T.Ink);
            UiKit.Center(r.chipIcon.rectTransform, Look.ChipIcon, Look.ChipIcon);
        }
        else
        {
            r.chipText = UiKit.Text(r.chip, d.chipText, TextStyle.Tag, T.Ink, TextAlignmentOptions.Center, false);
            UiKit.Fill(r.chipText.rectTransform);
        }

        var col = UiKit.Col(row, 0f, TextAnchor.UpperLeft, 0f, 0f, true);
        Ui.Size(col, flexW: 1f);
        // 上の行：名前＋（注意・危険のとき）アイコンと言葉
        var top = UiKit.Row(col, T.S1 * 0.5f, TextAnchor.MiddleLeft);
        Ui.Size(top, h: Look.NameH).minHeight = Look.NameH;
        r.name = UiKit.Text(top, d.label, TextStyle.Label, T.Ink, TextAlignmentOptions.MidlineLeft);
        r.statusIcon = UiKit.Icon(top, "caution", Look.StatusIcon, T.Ink);
        r.word = UiKit.Text(top, "", TextStyle.Label, T.Ink, TextAlignmentOptions.MidlineLeft);
        // 下の行：ゲージ＋数字
        var bottom = UiKit.Row(col, T.S1, TextAnchor.MiddleLeft);
        Ui.Size(bottom, h: Look.BarH).minHeight = Look.BarH;
        r.gauge = new MiniGauge(bottom);
        r.value = UiKit.Text(bottom, "", Look.Num, T.Ink, TextAlignmentOptions.MidlineRight, false);
        UiKit.Fixed(r.value, Look.NumW, Look.BarH);

        SetLevel(r, Level.Safe);
        return r;
    }

    /// <summary>下の行：[アイコン] 名前 ……… [値のアイコン] 値。値の文字を返す</summary>
    static TextMeshProUGUI InfoRow(Transform parent, string icon, string label, out Image valueIcon)
    {
        var row = UiKit.Row(parent, T.S1, TextAnchor.MiddleLeft);
        row.name = "Info_" + label;
        Ui.Size(row, h: Look.InfoH).minHeight = Look.InfoH;
        UiKit.Icon(row, icon, Look.InfoIcon, T.InkSub);
        UiKit.Text(row, label, TextStyle.Label, T.InkSub, TextAlignmentOptions.MidlineLeft);
        Ui.Size(UiKit.Space(row, 0f), flexW: 1f);
        valueIcon = UiKit.Icon(row, null, Look.ValueIcon, T.Ink);
        valueIcon.gameObject.SetActive(false);
        var v = UiKit.Text(row, "", TextStyle.Tag, T.Ink, TextAlignmentOptions.MidlineRight);
        return v;
    }

    static void BuildLegend(Transform parent)
    {
        var row = UiKit.Row(parent, T.S1, TextAnchor.MiddleLeft);
        row.name = "Legend";
        Ui.Size(row, h: Look.LegendH).minHeight = Look.LegendH;
        var bar = UiKit.Round(row, Look.LineW * 0.5f, T.Ink, "Line");
        UiKit.Fixed(bar, Look.LineW, Look.LegendBarH);
        UiKit.Text(row, Legend, TextStyle.Label, T.InkSub, TextAlignmentOptions.MidlineLeft);
    }

    // ================================================================== 状態を描く
    public override void Refresh(GameState s)
    {
        if (s == null) { Quiet(); return; }
        var m = Ctx.Model;

        // 電力：数字は power（%）、ゲージは capacity に対する割合。閾値は容量に対する割合（power_frac）
        float cap = Mathf.Max(s.capacity, 1f);
        var lv = Level.Safe;
        if (Frac(s, "power_frac", "low", out var pLow) && s.power < cap * pLow) lv = Level.Danger;
        else if (Frac(s, "power_frac", "half", out var pHalf) && s.power < cap * pHalf) lv = Level.Caution;
        float pLine = EndLine(s, "power", true, out var pEnd) ? pEnd : -1f;
        if (pLine >= 0f && s.power < pLine && rows["power"].def.endIsDanger) lv = Level.Danger;
        Draw("power", s.power, cap, Floor(s.power, "power"), lv, pLine);

        // 酸素：閾値は量（oxygen.mid・low）。漏れている間は危険
        float o2Max = m.Max("oxygen", s.oxygen);
        lv = Level.Safe;
        if (Frac(s, "oxygen", "low", out var oLow) && s.oxygen < oLow) lv = Level.Danger;
        else if (Frac(s, "oxygen", "mid", out var oMid) && s.oxygen < oMid) lv = Level.Caution;
        float oLine = EndLine(s, "oxygen", true, out var oEnd) ? oEnd : -1f;
        if (oLine >= 0f && s.oxygen < oLine && rows["oxygen"].def.endIsDanger) lv = Level.Danger;
        bool leaking = Leaking(s);
        if (leaking) lv = Level.Danger;
        Draw("oxygen", s.oxygen, o2Max, Floor(s.oxygen, "oxygen"), lv, oLine, leaking);

        // 温度：閾値は ℃（temp.slow・slower）。ゲージは 0 〜 temp_start
        float tMax = GameParams.TryGet("temp_start", out var t0) && t0 > 0f ? t0 : m.Max("temp", s.temp);
        lv = Level.Safe;
        if (Th(s, "temp", "slower", "temp_slower_c", out var tSlower) && s.temp < tSlower) lv = Level.Danger;
        else if (Th(s, "temp", "slow", "temp_slow_c", out var tSlow) && s.temp < tSlow) lv = Level.Caution;
        float tLine = EndLine(s, "temp", false, out var tEnd) ? tEnd : -1f;
        Draw("temp", s.temp, tMax, Floor(s.temp, "temp"), lv, tLine);

        // CO2：多いほど悪い（co2.warn を超えたら注意、co2.slow 以上で危険。houston の H17・H18 と同じ）
        lv = Level.Safe;
        if (Th(s, "co2", "slow", "co2_slow", out var cSlow) && s.co2 >= cSlow) lv = Level.Danger;
        else if (Th(s, "co2", "warn", "co2_warn", out var cWarn) && s.co2 > cWarn) lv = Level.Caution;
        Draw("co2", s.co2, Co2Scale, Mathf.CeilToInt(s.co2) + Unit("co2"), lv, -1f);

        // 燃料：fuel.low を下回ったら注意。点火の前で、点火に要る量より少なければ危険
        float fMax = m.Max("fuel", s.fuel);
        lv = Level.Safe;
        if (Frac(s, "fuel", "low", out var fLow) && s.fuel < fLow) lv = Level.Caution;
        float need = s.burnDone ? -1f : BurnNeed(s.route);
        if (need > 0f && s.fuel < need) lv = Level.Danger;
        Draw("fuel", s.fuel, fMax, Floor(s.fuel, "fuel"), lv, need);

        SetFacing(s.attitude);
        dataText.text = string.Format(DataFormat, Mathf.FloorToInt(s.dataSent), Mathf.FloorToInt(s.dataCollected));
    }

    /// <summary>state が来ない間（練習・かき混ぜ）：設計の合計で満タン、色は安全、時間は 0</summary>
    void Quiet()
    {
        var m = Ctx.Model;
        var tot = m.Totals;
        float cap = J.Num(tot, "capacity"), o2 = J.Num(tot, "oxygen"), fuel = J.Num(tot, "fuel");
        GameParams.TryGet("temp_start", out var t0);
        GameParams.TryGet("co2_start", out var c0);
        Draw("power", cap, Mathf.Max(cap, 1f), tot != null ? Floor(cap, "power") : "", Level.Safe, -1f);
        Draw("oxygen", o2, Mathf.Max(o2, 1f), tot != null ? Floor(o2, "oxygen") : "", Level.Safe, -1f);
        Draw("temp", t0, Mathf.Max(t0, 1f), GameParams.Has("temp_start") ? Floor(t0, "temp") : "", Level.Safe, -1f);
        Draw("co2", c0, Co2Scale, GameParams.Has("co2_start") ? Mathf.CeilToInt(c0) + Unit("co2") : "", Level.Safe, -1f);
        Draw("fuel", fuel, Mathf.Max(fuel, 1f), tot != null ? Floor(fuel, "fuel") : "", Level.Safe, -1f);
        SetFacing("earth");
        dataText.text = string.Format(DataFormat, 0, 0);
    }

    void Draw(string key, float v, float max, string text, Level lv, float line, bool falling = false)
    {
        var r = rows[key];
        SetLevel(r, lv);
        float m = Mathf.Max(max, 0.0001f);
        r.gauge.Set(Mathf.Clamp01(v / m), line >= 0f ? Mathf.Clamp01(line / m) : -1f, falling);
        r.value.text = text;
        r.word.text = falling ? WordLeak : r.def.words[(int)lv];
        r.word.gameObject.SetActive(r.word.text != "");
    }

    void SetLevel(Row r, Level lv)
    {
        if (r.level == lv) return;
        r.level = lv;
        bool caution = lv == Level.Caution, danger = lv == Level.Danger;
        Color face = danger ? T.Danger : caution ? T.Accent : T.Bg;
        Color onFace = danger ? T.Panel : T.Ink;           // 危険の面には白、注意（オレンジ）と安全には ink
        r.chipFace.color = face;
        if (r.chipIcon != null) r.chipIcon.color = onFace;
        if (r.chipText != null) r.chipText.color = onFace;
        r.tint.color = danger ? T.WithAlpha(T.Danger, Look.TintDanger) : caution ? T.WithAlpha(T.Accent, Look.TintCaution) : T.Clear;
        r.statusIcon.gameObject.SetActive(lv != Level.Safe);
        if (lv != Level.Safe) r.statusIcon.sprite = T.Icon(danger ? "danger" : "caution");
        r.statusIcon.color = danger ? T.Danger : T.Ink;
        r.word.color = danger ? T.Danger : T.Ink;
        r.value.color = danger ? T.Danger : T.Ink;
        r.gauge.SetLevel(danger ? T.Danger : caution ? T.Accent : T.Go, danger);
        if (!danger) r.chip.localScale = Vector3.one;
    }

    void SetFacing(string attitude)
    {
        if (!Facing.TryGetValue(attitude ?? "", out var f)) f = (attitude?.ToUpperInvariant() ?? "", null);
        facingText.text = f.word;
        var sp = f.icon != null ? T.Icon(f.icon) : null;
        facingIcon.sprite = sp;
        facingIcon.gameObject.SetActive(sp != null);
    }

    // ================================================================== 閾値（サーバーの thresholds が正。無ければ parameters.json）
    /// <summary>thresholds の値だけ（電力・酸素・燃料の閾値は config.py にしか無い）</summary>
    static bool Frac(GameState s, string group, string key, out float v) => s.Threshold(group, key, out v);

    /// <summary>thresholds の値。無ければ parameters.json の paramKey</summary>
    static bool Th(GameState s, string group, string key, string paramKey, out float v) =>
        s.Threshold(group, key, out v) || GameParams.TryGet(paramKey, out v);

    /// <summary>生還ライン（end_min）。margin=true なら strict のとき strict_margin を足す（reference_sim の result と同じ）</summary>
    static bool EndLine(GameState s, string key, bool margin, out float v)
    {
        if (!Th(s, "end_min", key, "end_" + key + "_min", out v)) return false;
        if (margin && s.strict)
        {
            float add = J.Has(s.thresholds, "strict_margin") ? J.Num(s.thresholds, "strict_margin") : GameParams.Get("end_strict_margin");
            v += add;
        }
        return true;
    }

    /// <summary>点火に要る燃料（選んだルート。選ぶ前は B＝少ない方）。parameters.json に無ければ -1</summary>
    static float BurnNeed(string route)
    {
        string r = route == "a" ? "a" : "b";
        return GameParams.TryGet("fuel_burn_" + r, out var v) ? v : -1f;
    }

    /// <summary>酸素が今も漏れているか（leak_open。漏れた量が oxygen_leak_max に届くと止まる）</summary>
    static bool Leaking(GameState s)
    {
        if (!s.leakOpen) return false;
        return !GameParams.TryGet("oxygen_leak_max", out var max) || s.leaked < max;
    }

    /// <summary>数字は切り捨て（「少ないほど悪い」値を、実際より良く見せない）＋ parameters.json の単位</summary>
    static string Floor(float v, string key) => Mathf.FloorToInt(v) + Unit(key);

    static string Unit(string key)
    {
        foreach (var d in Defs) if (d.key == key) return GameParams.Unit(d.unitParam);
        return "";
    }

    // ================================================================== 毎フレーム
    public override void Tick(float now)
    {
        Fit();
        float pulse = 1f + Look.PulseAmp * (0.5f - 0.5f * Mathf.Cos(now * Look.PulseHz * Mathf.PI * 2f));
        foreach (var r in rows.Values)
        {
            if (r.level == Level.Danger) r.chip.localScale = new Vector3(pulse, pulse, 1f);
            r.gauge.Tick(now);
        }
    }

    /// <summary>左の領域（Root）に入るように、パネルの幅を合わせ、高さが足りなければ全体を縮める</summary>
    void Fit()
    {
        var size = Root.rect.size;
        if (size.x <= 0f || size.y <= 0f) return;
        float ph = LayoutUtility.GetPreferredHeight(panel);
        if (ph <= 0f) return;   // まだ1回もレイアウトされていない
        float k = Mathf.Min(1f, size.y / ph);
        if (!Mathf.Approximately(k, fitScale) || !Mathf.Approximately(size.x, fitW))
        {
            fitScale = k; fitW = size.x;
            panel.localScale = new Vector3(k, k, 1f);
        }
        var want = new Vector2(size.x / k, ph);
        if ((panel.sizeDelta - want).sqrMagnitude > 0.25f) panel.sizeDelta = want;
    }

    // ================================================================== ゲージ（#71 の UiKitHud.Gauge ができたら、ここだけ差し替える）
    /// <summary>高さ 28 のピル。溝 line、中身は状態の色、危険のとき斜め縞。生還ラインは ink の縦線。漏れているときは中身の先に ▼</summary>
    class MiniGauge
    {
        readonly RectTransform fill, line, caret;
        readonly Image fillImg, stripes;
        bool falling;

        public MiniGauge(Transform parent)
        {
            var track = Ui.Rect("Gauge", parent);
            var le = Ui.Size(track, h: Look.GaugeH, flexW: 1f); le.minHeight = Look.GaugeH;
            var groove = UiKit.Pill(track, T.Line, Look.GaugeH, "Groove"); UiKit.Fill(groove.rectTransform);

            fillImg = UiKit.Pill(track, T.Go, Look.GaugeH, "Fill");
            fill = fillImg.rectTransform;
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            fill.gameObject.AddComponent<Mask>().showMaskGraphic = true;   // 縞をピルの形で切る

            var sp = T.Theme != null ? T.Theme.stripes : null;
            if (sp != null)
            {
                var sr = Ui.Rect("Stripes", fill); UiKit.Fill(sr);
                stripes = sr.gameObject.AddComponent<Image>();
                stripes.sprite = sp; stripes.type = Image.Type.Tiled; stripes.pixelsPerUnitMultiplier = Look.StripeScale;
                stripes.color = T.Panel; stripes.raycastTarget = false;   // 画像が白の 40%
                stripes.enabled = false;
            }

            var li = UiKit.Round(track, Look.LineW * 0.5f, T.Ink, "Line");
            line = li.rectTransform;
            line.anchorMin = line.anchorMax = new Vector2(0f, 0.5f); line.pivot = new Vector2(0.5f, 0.5f);
            line.sizeDelta = new Vector2(Look.LineW, Look.GaugeH + Look.LineOver * 2f);
            line.gameObject.SetActive(false);

            var cs = T.Theme != null ? T.Theme.caretDown : null;
            var ci = UiKit.Shape(track, cs, T.Ink, 0f, "Caret");
            caret = ci.rectTransform;
            caret.anchorMin = caret.anchorMax = new Vector2(0f, 0.5f); caret.pivot = new Vector2(0f, 0.5f);
            caret.sizeDelta = new Vector2(Look.CaretW, Look.CaretH);
            caret.gameObject.SetActive(false);
        }

        /// <param name="frac">中身（0〜1）</param><param name="lineFrac">生還ライン（0〜1）。無ければ負</param><param name="down">減っている印 ▼ を出すか</param>
        public void Set(float frac, float lineFrac, bool down)
        {
            fill.anchorMax = new Vector2(frac, 1f);
            fill.gameObject.SetActive(frac > 0.001f);
            line.gameObject.SetActive(lineFrac >= 0f);
            if (lineFrac >= 0f) { line.anchorMin = line.anchorMax = new Vector2(lineFrac, 0.5f); line.anchoredPosition = Vector2.zero; }
            falling = down;
            caret.gameObject.SetActive(down);
            if (down) caret.anchorMin = caret.anchorMax = new Vector2(frac, 0.5f);
        }

        public void SetLevel(Color c, bool striped)
        {
            fillImg.color = c;
            if (stripes != null) stripes.enabled = striped;
        }

        public void Tick(float now)
        {
            if (!falling) return;
            float y = -Look.CaretBob * (0.5f - 0.5f * Mathf.Cos(now / Look.CaretPeriod * Mathf.PI * 2f));
            caret.anchoredPosition = new Vector2(Look.CaretGap, y);
        }
    }
}
