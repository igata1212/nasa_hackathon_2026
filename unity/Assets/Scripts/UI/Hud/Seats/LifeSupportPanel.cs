// 書いた人：Claude ／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で CabinHud から分けた）／ 更新：iizuka（Opus 5.5、2026-11-14、#81）
// 作る Issue：#81（Life Support のパネル）。かき混ぜのボタン（BuildStir）の文字は #84 と合わせて STIR TANKS
//
// Life Support（●）の席。かたまりは2つ（#102「1席 2〜3 個」）：
//   ① タンク：Tank 1・Tank 2 の圧力計＋バルブを閉じる（#100：どちらかを選ぶ・開け直しなし）
//      どちらが漏れているかは針の動きだけで分かる（#101：状態は足さず、leak_open と leaked から Tank 2 の針だけ下げる）。色や帯で Tank 2 を指さない
//   ② CO2 フィルター交換（ほかの1席と同時押し。1人で両席なら2通。Ctx.SendDual）
//   stir の段階は「STIR TANKS」の大きなボタンだけ（M04）。押したら消す
// 今できるかたまりだけオレンジの輪で光らせる（漏れている間はタンク、CO2 が注意の値を超えたらフィルター）。終わったかたまりは薄くする
// 文言は下の Txt、大きさ・色は一番下の LpKit（仮の部品。#71 の HudKit ができたら替える）の1か所
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using T = UiTokens;

public class LifeSupportPanel : SeatPanel
{
    public override string Seat => Seats.Life;
    public override IEnumerable<string> Commands => new[] { "close_valve", "swap_filter", "stir" };

    // ------------------------------------------------------------------ 文言（Fable・Taiga が直す所。画面の文字は英語。書体に無い字は出ないので ASCII だけ）
    static class Txt
    {
        public const string Tank = "TANK ";                       // ＋番号
        public const string Close = "CLOSE", CloseSub = "valve", Closed = "CLOSED";
        public const string HintPractice = "Practice:\ntry a valve.\nNo leak yet.";
        public const string HintFind = "Find the leak.\nWatch which\nneedle drops.";
        public const string HintWrong = "Still leaking!\nWatch which\nneedle drops.";
        public const string HintStopped = "Leak stopped.";
        public const string HintEmpty = "A tank is\nempty. No\nmore leak.";
        public const string Filter = "CO2 FILTER";
        public const string FilterLeft = "{0} left";              // 残りの交換回数（CO2 の値は左の StatusPanel に出ている）
        public const string Swap = "SWAP", SwapPair = "needs 2 people", SwapSolo = "you press for both", SwapNone = "none left";
        public const string Stir = "STIR TANKS";                  // #84 の案内と同じ言葉
        public const string StirNote = "Houston asked for this.\nPress it once.", StirDone = "Stirring...";
    }

    // 形（数値ではなく作り）：メインの酸素タンクは 2 つ（close_valve の tank は 1 か 2）。漏れは Tank 2 固定（SPEC 6章・server config.LEAKING_TANK）。
    // 針を下げるのは Tank 2 だけ（#101）。どのタンクが漏れているかは画面では言わない
    const int Tanks = 2, LeakTank = 2;

    // 閉じたバルブ。席のパネルは席・段階が変わるたびに作り直すので static に置き、新しい飛行（t が戻る・飛行の前）で消す
    static readonly HashSet<int> closed = new HashSet<int>();
    static float closedT = -1f;

    LpStrip strip;
    LpCard tankCard, filterCard, stirCard;
    readonly LpDial[] dials = new LpDial[Tanks];
    readonly TextMeshProUGUI[] values = new TextMeshProUGUI[Tanks];
    readonly UnityEngine.UI.Image[] falling = new UnityEngine.UI.Image[Tanks];
    readonly int[] last = { -1, -1 };
    readonly float[] fallingUntil = new float[Tanks];
    readonly LpButton[] valves = new LpButton[Tanks];
    TextMeshProUGUI hint, filterInfo, stirNote;
    LpButton swap;
    UiButton stirBtn;
    string feedback; float feedbackUntil;
    readonly HashSet<string> lit = new HashSet<string>();   // Highlight（#84 が光らせているコマンド）
    float lastNow;

    // ------------------------------------------------------------------ 作る
    protected override void BuildControls(RectTransform row)
    {
        RootAsDetail();
        if (Ctx.Model.Phase != "flight") closed.Clear();
        strip = LpKit.Strip(row);

        // ① タンク：ヒント・Tank 1・Tank 2
        tankCard = LpKit.Card(strip.Rect, "Tanks");
        hint = LpKit.Note(tankCard.Root, "", TextStyle.Label, T.Ink);
        LpKit.ReserveWidth(hint, Txt.HintPractice, Txt.HintFind, Txt.HintWrong, Txt.HintStopped, Txt.HintEmpty);
        for (int i = 0; i < Tanks; i++)
        {
            int tank = i + 1;
            var unit = UiKit.Row(tankCard.Root, LpKit.Gap, TextAnchor.MiddleLeft); unit.name = "Tank" + tank;
            dials[i] = LpKit.Dial(unit, LpKit.DialD);
            var col = UiKit.Col(unit, 0f, TextAnchor.MiddleLeft);
            UiKit.Text(col, Txt.Tank + tank, TextStyle.Tag, T.Ink, TextAlignmentOptions.MidlineLeft);
            var num = UiKit.Row(col, T.S1, TextAnchor.MiddleLeft);
            values[i] = UiKit.Text(num, "100%", TextStyle.Number, T.Ink, TextAlignmentOptions.MidlineRight);
            LpKit.ReserveWidth(values[i], "100%");   // 桁が変わっても幅を変えない
            falling[i] = LpKit.Caret(num);
            valves[i] = LpKit.Button(unit, "valve", null, Txt.Close, Txt.CloseSub, () => CloseValve(tank));
            valves[i].Reserve(Txt.Closed, "");
        }

        // ② CO2 フィルター
        filterCard = LpKit.Card(strip.Rect, "Filter");
        var fcol = UiKit.Col(filterCard.Root, 0f, TextAnchor.MiddleLeft);
        UiKit.Text(fcol, Txt.Filter, TextStyle.Tag, T.Ink, TextAlignmentOptions.MidlineLeft);
        filterInfo = UiKit.Text(fcol, "", TextStyle.Label, T.InkSub, TextAlignmentOptions.MidlineLeft);
        LpKit.ReserveWidth(filterInfo, string.Format(Txt.FilterLeft, 9));
        swap = LpKit.Button(filterCard.Root, "filter", null, Txt.Swap, SwapSub, Swap);
        swap.AddMark();
        foreach (var sub in new[] { Txt.SwapNone, Txt.SwapPair, Txt.SwapSolo, LpKit.PressWaiting, LpKit.PressNow, LpKit.PressTogether, LpKit.PressFailed })
            swap.Reserve(Txt.Swap, sub);
    }

    /// <summary>M04：かき混ぜの大きなボタンだけ。押したら消す（M04 がもう一度流れたら出し直す）</summary>
    protected override void BuildStir(RectTransform row)
    {
        RootAsDetail();
        strip = LpKit.Strip(row);
        stirCard = LpKit.Card(strip.Rect, "Stir");
        stirBtn = UiKit.PrimaryButton(stirCard.Root, Txt.Stir, "switch", Stir);
        stirNote = LpKit.Note(stirCard.Root, Txt.StirNote, TextStyle.Label, T.Ink);
        LpKit.ReserveWidth(stirNote, Txt.StirNote, Txt.StirDone);
        stirCard.Set(true, false);
    }

    string SwapSub => Ctx.SoloPair("swap_filter") != null ? Txt.SwapSolo : Txt.SwapPair;

    // ------------------------------------------------------------------ 押したとき
    void CloseValve(int tank)
    {
        Ctx.Send(Cmd.Valve(Seat, tank));
        // 飛行中は押した時点で「閉じた」にする（開け直しは無い、#100）。記録の再生では送っても無視されるので変えない
        if (Ctx.Model.Phase == "flight" && !Ctx.Offline) { closed.Add(tank); Refresh(Ctx.State); }
    }

    void Swap() => Ctx.SendDual("swap_filter", Seat);

    void Stir()
    {
        Ctx.Send(Cmd.Simple("stir", Seat));
        stirBtn.Root.gameObject.SetActive(false);
        stirNote.text = Txt.StirDone;
        stirCard.Set(false, false);
    }

    // ------------------------------------------------------------------ 状態・イベント
    public override void Refresh(GameState s)
    {
        base.Refresh(s);
        if (tankCard == null) return;   // stir の段階（かき混ぜのボタンだけ）
        bool flight = s != null;
        if (flight)
        {
            if (s.t + 0.5f < closedT) closed.Clear();   // 新しい飛行
            closedT = s.t;
            if (!s.leakOpen) closed.Add(LeakTank);       // 入り直したときなど、押したのを見ていなくても漏れが止まっていれば閉じている
        }

        // ① タンク：Tank 2 の針 = 1 − 漏れた量 / 漏れの上限（上限まで漏れたら空）。Tank 1 は動かさない（#101）
        float leakMax = GameParams.Get("oxygen_leak_max");
        bool drained = flight && leakMax > 0f && s.leaked >= leakMax;
        bool leaking = flight && s.leakOpen && !drained;
        for (int i = 0; i < Tanks; i++)
        {
            float frac = 1f;
            if (flight && i + 1 == LeakTank && leakMax > 0f) frac = Mathf.Clamp01(1f - s.leaked / leakMax);
            dials[i].Set(frac);
            int pct = Mathf.RoundToInt(frac * 100f);
            if (last[i] >= 0 && pct < last[i]) fallingUntil[i] = Time.unscaledTime + LpKit.FallingSec;
            last[i] = pct;
            values[i].text = pct + "%";
            bool shut = closed.Contains(i + 1);
            valves[i].SetText(shut ? Txt.Closed : Txt.Close, shut ? "" : Txt.CloseSub);
            valves[i].Set(shut ? LpLook.Off : LpLook.Secondary, !shut);
        }
        hint.text = !flight ? Txt.HintPractice
            : leaking ? (closed.Count == 0 ? Txt.HintFind : Txt.HintWrong)
            : drained && s.leakOpen ? Txt.HintEmpty : Txt.HintStopped;

        // ② フィルター：CO2 が注意の値（thresholds.co2.warn）を超えたら光らせる。残りが 0 なら押せない
        int left = flight ? s.filters : Mathf.RoundToInt(GameParams.Get("co2_filters"));
        filterInfo.text = string.Format(Txt.FilterLeft, left);
        bool none = flight && s.filters <= 0;
        bool co2High = flight && s.co2 >= Co2Warn(s);

        bool tankFocus = leaking || lit.Contains("close_valve");
        bool filterFocus = (!leaking && co2High && !none) || lit.Contains("swap_filter");
        tankCard.Set(tankFocus, flight && !leaking);
        filterCard.Set(filterFocus, none);
        swap.Set(none ? LpLook.Off : filterFocus ? LpLook.Primary : LpLook.Secondary, !none);
        swap.SetMark(none ? "lock" : null);
        if (feedback == null) swap.SetText(Txt.Swap, none ? Txt.SwapNone : SwapSub);
    }

    static float Co2Warn(GameState s) => s.Threshold("co2", "warn", out var v) ? v : GameParams.Get("co2_warn");

    public override void OnEvent(GameEvent e)
    {
        if (e.kind == "cmd" && e.cmd == "close_valve")
        {
            int tank = J.Int(e.args, "tank");
            if (tank > 0) { closed.Add(tank); Refresh(Ctx.State); }
        }
        else if (e.kind == "press" && e.cmd == "swap_filter" && swap != null)
        {
            feedback = LpKit.PressText(e, Seat);
            if (feedback == null) return;
            feedbackUntil = Time.unscaledTime + LpKit.FeedbackSec;
            swap.SetText(null, feedback);
        }
        else if (e.kind == "houston" && e.id == "M04" && e.repeat && stirBtn != null)
        {
            // 押したのに届かなかった（M04 がもう一度流れた）：ボタンを出し直す
            stirBtn.Root.gameObject.SetActive(true);
            stirNote.text = Txt.StirNote;
            stirCard.Set(true, false);
        }
    }

    /// <summary>#84：練習の間「まだ試していない操作」を光らせる（close_valve → タンク、swap_filter → フィルター、stir → かき混ぜ）</summary>
    public override void Highlight(string cmd, bool on)
    {
        if (on) lit.Add(cmd); else lit.Remove(cmd);
        if (stirCard != null && cmd == "stir") stirCard.Set(on || stirBtn.Root.gameObject.activeSelf, false);
        Refresh(Ctx.State);
    }

    public override void Tick(float now)
    {
        float dt = lastNow > 0f ? Mathf.Clamp(now - lastNow, 0f, LpKit.MaxDt) : 0f;
        lastNow = now;
        for (int i = 0; i < Tanks; i++)
        {
            dials[i]?.Tick(dt);
            if (falling[i] != null) LpKit.ShowCaret(falling[i], now < fallingUntil[i]);
        }
        tankCard?.Tick(now); filterCard?.Tick(now); stirCard?.Tick(now);
        if (feedback != null && now > feedbackUntil) { feedback = null; Refresh(Ctx.State); }
        strip?.Fit();
    }
}

// ====================================================================== 仮の部品（#81・#83 の席のパネル用）
// #71 の HudKit（UiKitHud.cs）がまだ無いので、ここに小さく作った。HudKit ができたら替える。Pilot・Comms & Power の席と形をそろえるなら、統合のときに UiKitHud.cs へ移す。
// 名前がほかの担当とぶつからないよう Lp（Life・Planner）を付けた。大きさ・色・時間はこの LpKit の定数の1か所（UiTokens に移すなら統合のとき）

/// <summary>ボタンの見た目。Secondary＝枠つき（普通）／Primary＝青緑（今これを押す）／On＝スイッチ ON（青緑）／Selected＝選んでいる（濃紺）／Off＝押せない・済んだ（灰）</summary>
public enum LpLook { Secondary, Primary, On, Selected, Off }

/// <summary>ピルのボタン：左にアイコンか丸に1文字（A／B）、右に「主の言葉」と「補助の1行」。見た目の高さ LpKit.BtnH、押せる範囲は T.Hit</summary>
public class LpButton
{
    public RectTransform Root, Body, Content, LabelRow;
    public UnityEngine.UI.Button Button;
    public UnityEngine.UI.Image Frame, Face, Icon, LeadCircle, Mark;
    public TextMeshProUGUI Label, Sub, LeadText;
    /// <summary>足した飾り（燃料のアイコンと数字など）。文字と同じ色にそろえる</summary>
    public readonly List<UnityEngine.UI.Graphic> Extra = new List<UnityEngine.UI.Graphic>();
    public LpLook Look { get; private set; } = LpLook.Secondary;
    float reserved;
    string mark;

    public void Set(LpLook look, bool interactable = true)
    {
        Look = look;
        Button.interactable = interactable;
        LpKit.Colors(look, out var frame, out var face, out var fg, out var sub);
        Frame.color = frame; Face.color = face; Label.color = fg;
        if (Sub != null) Sub.color = sub;
        if (Icon != null) Icon.color = fg;
        if (LeadCircle != null) { LeadCircle.color = fg; LeadText.color = face; }
        if (Mark != null) Mark.color = mark != null ? fg : T.Clear;
        foreach (var g in Extra) g.color = fg;
    }

    /// <summary>文字を変える（null はそのまま、"" は補助の行を消す）。幅は Reserve した中で一番広いものに合わせる</summary>
    public void SetText(string label, string sub)
    {
        bool changed = false;
        if (label != null && Label.text != label) { Label.text = label; changed = true; }
        if (sub != null && Sub != null && Sub.text != sub) { Sub.text = sub; Sub.gameObject.SetActive(sub.Length > 0); changed = true; }
        if (changed) Fit();
    }

    /// <summary>「主の言葉」の後ろに小さなアイコンを置く場所を作る（鍵・チェック。色だけで状態を伝えないため）</summary>
    public void AddMark()
    {
        if (Mark != null) return;
        Mark = UiKit.Icon(LabelRow, "lock", T.IconS, T.Clear);
        Fit();
    }

    /// <summary>印のアイコン（"lock"・"check" など）。null で消す（場所は残すので幅は変わらない）</summary>
    public void SetMark(string icon)
    {
        if (Mark == null || icon == mark) return;
        mark = icon;
        if (icon != null) Mark.sprite = T.Icon(icon);
        Set(Look, Button.interactable);
    }

    /// <summary>この文字になっても幅が変わらないように、幅を取っておく（並びがずれて押し間違えないように）</summary>
    public void Reserve(string label, string sub)
    {
        string l0 = Label.text, s0 = Sub != null ? Sub.text : null;
        Label.text = label;
        if (Sub != null) { Sub.text = sub; Sub.gameObject.SetActive(!string.IsNullOrEmpty(sub)); }
        reserved = Mathf.Max(reserved, Measure());
        Label.text = l0;
        if (Sub != null) { Sub.text = s0; Sub.gameObject.SetActive(!string.IsNullOrEmpty(s0)); }
        Fit();
    }

    public void Fit()
    {
        float w = Mathf.Max(reserved, Measure());
        var le = Ui.Size(Root);
        le.minWidth = le.preferredWidth = w; le.flexibleWidth = 0f;
    }

    float Measure()
    {
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(Content);
        return UnityEngine.UI.LayoutUtility.GetPreferredWidth(Content);
    }
}

/// <summary>かたまり（カード）。今できるならオレンジの輪が脈打つ（Focus）。終わった・使えないなら薄くする（Idle）</summary>
public class LpCard
{
    public RectTransform Root;
    public UnityEngine.UI.Image Ring;
    public CanvasGroup Group;
    bool focus;

    public void Set(bool focus, bool idle)
    {
        this.focus = focus;
        Ring.enabled = focus;
        Group.alpha = idle && !focus ? LpKit.IdleAlpha : 1f;
    }

    public void Tick(float now)
    {
        if (!focus) return;
        Ring.color = T.WithAlpha(T.Accent, Mathf.Lerp(LpKit.PulseMin, 1f, 0.5f + 0.5f * Mathf.Sin(now * Mathf.PI * 2f * LpKit.PulseHz)));
    }
}

/// <summary>圧力計：丸い文字盤に針。Set(0〜1) で針の行き先を決め、Tick でなめらかに動かす（左下＝空、右下＝満タン）</summary>
public class LpDial
{
    public RectTransform Root, Needle;
    float cur, target;
    bool placed;

    public void Set(float frac)
    {
        target = Mathf.Lerp(LpKit.DialSweep * 0.5f, -LpKit.DialSweep * 0.5f, Mathf.Clamp01(frac));
        if (!placed) { cur = target; placed = true; Apply(); }
    }

    public void Tick(float dt)
    {
        if (cur == target) return;
        cur = Mathf.Lerp(cur, target, 1f - Mathf.Exp(-dt * LpKit.NeedleEase));
        if (Mathf.Abs(cur - target) < LpKit.NeedleSnap) cur = target;
        Apply();
    }

    void Apply() => Needle.localEulerAngles = new Vector3(0f, 0f, cur);
}

/// <summary>1行の状態：アイコン（✓・✗ など）＋短い文</summary>
public class LpLine
{
    public RectTransform Root;
    public UnityEngine.UI.Image Icon;
    public TextMeshProUGUI Text;

    public void Set(string icon, Color c, string text)
    {
        Icon.sprite = T.Icon(icon); Icon.color = c;
        Text.text = text;
    }
}

/// <summary>席のパネルの中身を横1列に並べる帯。入りきらない狭い画面では全体を縮める（Fit を毎フレーム）</summary>
public class LpStrip
{
    public RectTransform Rect, Area;

    public void Fit()
    {
        float w = Rect.rect.width, aw = Area.rect.width;
        float k = w > 0f && aw > 0f ? Mathf.Min(1f, aw / w) : 1f;
        if (!Mathf.Approximately(Rect.localScale.x, k)) Rect.localScale = new Vector3(k, k, 1f);
    }
}

public static class LpKit
{
    // ------------------------------------------------------------------ 大きさ（UiTokens から。px は Canvas の基準 1600×900）
    public const float BtnH = T.Hit - T.S2;          // ボタンの見た目の高さ 80（押せる範囲は T.Hit）。席のパネルの高さが約 108 しかないため
    public const float LeadD = T.BadgeS - T.S1;       // ボタンの左の丸（A／B）48
    public const float DialD = T.Hit - T.S2;          // 圧力計の直径 80
    public const float NeedleW = T.Border * 2f;       // 針の太さ 6
    public const float HubD = T.S2;                   // 針の軸 16
    public const float MarkLen = T.S2;                // 目盛りの長さ 16
    public const float FocusRing = T.SelfRing;        // 今できるかたまりの輪の太さ 4
    public const float CardPad = T.S2, Gap = T.S2, CardGap = T.S3;
    // ------------------------------------------------------------------ 動き
    public const float DialSweep = 240f;              // 針の振れ幅（度）。左下＝空、右下＝満タン
    public const float NeedleEase = 4f;               // 針が追いつく速さ（1/秒）
    public const float NeedleSnap = 0.05f;            // これより近ければ止める（度）
    public const float IdleAlpha = 0.5f;              // 終わった・使えないかたまりの濃さ
    public const float PulseHz = 1.2f, PulseMin = 0.45f;   // 輪の脈打ち
    public const float FeedbackSec = 2.5f;            // 同時押しの結果（waiting・swapped など）を出しておく秒
    public const float FallingSec = 1.5f;             // 針が下がったあと ▼ を出しておく秒
    public const float MaxDt = 0.1f;
    // ------------------------------------------------------------------ 同時押しの結果の文言（Life Support と Planner の手伝いのボタンで同じ）
    public const string PressWaiting = "waiting for partner", PressNow = "press now!";
    public const string PressTogether = "swapped!", PressFailed = "missed, try again";

    public static void Colors(LpLook look, out Color frame, out Color face, out Color fg, out Color sub)
    {
        switch (look)
        {
            case LpLook.Primary:
            case LpLook.On: frame = T.Go; face = T.Go; fg = T.Panel; sub = T.Panel; break;
            case LpLook.Selected: frame = T.Ink; face = T.Ink; fg = T.Panel; sub = T.Panel; break;
            case LpLook.Off: frame = T.Line; face = T.Line; fg = T.InkSub; sub = T.InkSub; break;
            default: frame = T.Ink; face = T.Panel; fg = T.Ink; sub = T.InkSub; break;
        }
    }

    /// <summary>同時押しのイベント（swap_filter）を、ボタンの補助の行の文にする。関係ないものは null</summary>
    public static string PressText(GameEvent e, string seat)
    {
        switch (e.status)
        {
            case "waiting": return e.seats.Contains(seat) ? PressWaiting : PressNow;
            case "together": return PressTogether;
            case "failed": return PressFailed;
        }
        return null;
    }

    /// <summary>帯：SeatPanel の行（Ui.HBox）の中に、レイアウトの外として左寄せで置く。幅は中身に合わせ、入りきらなければ縮める</summary>
    public static LpStrip Strip(RectTransform parent)
    {
        var r = UiKit.Row(parent, CardGap, TextAnchor.MiddleLeft);
        r.name = "Strip";
        Ui.Size(r).ignoreLayout = true;
        r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 0.5f);
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        r.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandHeight = true;
        r.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        return new LpStrip { Rect = r, Area = parent };
    }

    /// <summary>かたまり：panel の角丸 28（影つき）。後ろにオレンジの輪（Focus のときだけ）。中身は返す Root に横に足す</summary>
    public static LpCard Card(Transform strip, string name)
    {
        var c = new LpCard();
        c.Root = UiKit.Row(strip, Gap, TextAnchor.MiddleLeft, CardPad, 0f);
        c.Root.name = name;
        c.Ring = UiKit.Round(c.Root, T.RadiusM + FocusRing, T.Accent, "FocusRing");
        UiKit.Fill(c.Ring.rectTransform, -FocusRing); UiKit.Ignore(c.Ring);
        UiKit.Surface(c.Root, T.RadiusM, T.Panel);
        c.Group = c.Root.gameObject.AddComponent<CanvasGroup>();
        c.Set(false, false);
        return c;
    }

    /// <summary>ボタン。lead に1文字を渡すと左に丸い文字（A／B）、無ければ icon。sub は補助の1行（null なら無し）</summary>
    public static LpButton Button(Transform p, string icon, string lead, string label, string sub, System.Action on, TextStyle labelStyle = TextStyle.Sub)
    {
        var b = new LpButton();
        b.Root = Ui.Rect("Button_" + label, p);
        var hit = b.Root.gameObject.AddComponent<UnityEngine.UI.Image>(); hit.color = T.Clear;   // 押せる範囲は T.Hit（見た目より大きい）

        var sh = UiKit.Pill(b.Root, T.Shadow, BtnH, "Shadow"); UiKit.MiddleBand(sh.rectTransform, BtnH);
        sh.rectTransform.anchoredPosition = new Vector2(0f, -T.ShadowY);
        b.Body = Ui.Rect("Body", b.Root); UiKit.MiddleBand(b.Body, BtnH);
        b.Frame = UiKit.Pill(b.Body, T.Ink, BtnH, "Frame"); UiKit.Fill(b.Frame.rectTransform);
        b.Face = UiKit.Pill(b.Body, T.Panel, BtnH - T.Border * 2f, "Face"); UiKit.Fill(b.Face.rectTransform, T.Border);

        b.Content = UiKit.Row(b.Body, T.S2, TextAnchor.MiddleCenter, T.S3); UiKit.Fill(b.Content);
        if (lead != null)
        {
            var lr = Ui.Rect("Lead", b.Content); UiKit.Fixed(lr, LeadD, LeadD);
            b.LeadCircle = UiKit.Pill(lr, T.Ink, LeadD, "Circle"); UiKit.Fill(b.LeadCircle.rectTransform);
            b.LeadText = UiKit.Text(lr, lead, TextStyle.Sub, T.Panel, TextAlignmentOptions.Center, false); UiKit.Fill(b.LeadText.rectTransform);
        }
        else if (icon != null) b.Icon = UiKit.Icon(b.Content, icon, T.IconM, T.Ink);
        var col = UiKit.Col(b.Content, 0f, TextAnchor.MiddleLeft);
        b.LabelRow = UiKit.Row(col, T.S1, TextAnchor.MiddleLeft);
        b.Label = UiKit.Text(b.LabelRow, label, labelStyle, T.Ink, TextAlignmentOptions.MidlineLeft);
        if (sub != null) { b.Sub = UiKit.Text(col, sub, TextStyle.Label, T.InkSub, TextAlignmentOptions.MidlineLeft); b.Sub.gameObject.SetActive(sub.Length > 0); }

        b.Button = b.Root.gameObject.AddComponent<UnityEngine.UI.Button>();
        b.Button.targetGraphic = b.Face;
        b.Button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        var cb = b.Button.colors;
        cb.normalColor = cb.selectedColor = cb.disabledColor = Color.white;
        cb.highlightedColor = T.TintHover; cb.pressedColor = T.TintPressed; cb.fadeDuration = T.TintFade;
        b.Button.colors = cb;
        if (on != null) b.Button.onClick.AddListener(() => on());
        b.Root.gameObject.AddComponent<UiPress>().Init(b.Body, b.Button);

        var le = Ui.Size(b.Root, h: T.Hit); le.minHeight = T.Hit;
        b.Set(LpLook.Secondary);
        b.Fit();
        return b;
    }

    /// <summary>ボタンの「主の言葉」の後ろに小さなアイコンと数字（ルートの燃料など）。色はボタンの文字にそろう</summary>
    public static void Chip(LpButton b, string icon, string text)
    {
        var ic = UiKit.Icon(b.LabelRow, icon, T.IconS, T.Ink);
        var tx = UiKit.Text(b.LabelRow, text, TextStyle.Tag, T.Ink, TextAlignmentOptions.MidlineLeft);
        b.Extra.Add(ic); b.Extra.Add(tx);
        b.Set(b.Look, b.Button.interactable);
        b.Fit();
    }

    /// <summary>圧力計（直径 d）。文字盤 bg＋縁 ink、目盛り3つ（空・半分・満タン）、針と軸 ink</summary>
    public static LpDial Dial(Transform p, float d)
    {
        var g = new LpDial();
        g.Root = Ui.Rect("Dial", p); UiKit.Fixed(g.Root, d, d);
        var rim = UiKit.Pill(g.Root, T.Ink, d, "Rim"); UiKit.Fill(rim.rectTransform);
        var face = UiKit.Pill(g.Root, T.Bg, d - T.Border * 2f, "Face"); UiKit.Fill(face.rectTransform, T.Border);
        foreach (float a in new[] { DialSweep * 0.5f, 0f, -DialSweep * 0.5f })
        {
            var arm = Ui.Rect("Mark", g.Root);
            arm.anchorMin = arm.anchorMax = arm.pivot = new Vector2(0.5f, 0.5f); arm.sizeDelta = Vector2.zero;
            arm.localEulerAngles = new Vector3(0f, 0f, a);
            var m = UiKit.Pill(arm, T.InkSub, T.Border, "Tick").rectTransform;
            m.anchorMin = m.anchorMax = new Vector2(0.5f, 0.5f); m.pivot = new Vector2(0.5f, 1f);
            m.sizeDelta = new Vector2(T.Border, MarkLen); m.anchoredPosition = new Vector2(0f, d * 0.5f - T.Border);
        }
        g.Needle = Ui.Rect("Needle", g.Root);
        g.Needle.anchorMin = g.Needle.anchorMax = new Vector2(0.5f, 0.5f); g.Needle.pivot = new Vector2(0.5f, 0f);
        g.Needle.sizeDelta = new Vector2(NeedleW, d * 0.5f - T.Border - T.S1); g.Needle.anchoredPosition = Vector2.zero;
        var bar = UiKit.Pill(g.Needle, T.Ink, NeedleW, "Bar"); UiKit.Fill(bar.rectTransform);
        var hub = UiKit.Pill(g.Root, T.Ink, HubD, "Hub"); UiKit.Center(hub.rectTransform, HubD, HubD);
        g.Set(1f);
        return g;
    }

    /// <summary>針が下がっているときに数字の横に出す ▼（ink。色で特定のタンクを指さない）。隠すときも場所は残す</summary>
    public static UnityEngine.UI.Image Caret(Transform p)
    {
        var img = UiKit.Shape(p, T.Theme.caretDown, T.Ink, 0f, "Falling");
        img.preserveAspect = true;
        UiKit.Fixed(img, T.IconS, T.IconS);
        ShowCaret(img, false);
        return img;
    }

    public static void ShowCaret(UnityEngine.UI.Image img, bool on) => img.color = on ? T.Ink : T.Clear;

    public static LpLine Line(Transform p)
    {
        var l = new LpLine();
        l.Root = UiKit.Row(p, T.S1, TextAnchor.MiddleLeft); l.Root.name = "Line";
        l.Icon = UiKit.Icon(l.Root, "check", T.IconS, T.Ink);
        l.Text = UiKit.Text(l.Root, "", TextStyle.Label, T.Ink, TextAlignmentOptions.MidlineLeft);
        return l;
    }

    /// <summary>短い文（改行で2〜3行まで）。左寄せ</summary>
    public static TextMeshProUGUI Note(Transform p, string text, TextStyle st, Color c) =>
        UiKit.Text(p, text, st, c, TextAlignmentOptions.MidlineLeft, false);

    /// <summary>どの文になっても幅が変わらないように、一番広い文の幅を取っておく</summary>
    public static void ReserveWidth(TextMeshProUGUI t, params string[] samples)
    {
        float w = 0f;
        foreach (var s in samples) w = Mathf.Max(w, t.GetPreferredValues(s).x);
        var le = Ui.Size(t, w: w); le.minWidth = w;
    }
}
