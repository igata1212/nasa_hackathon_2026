// 書いた人：Claude ／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で CabinHud から分けた）／ 更新：iizuka（Opus 5.5、2026-11-14、#81）／ 更新：iizuka（Opus 5.5、2026-11-15、#190 あつ森に寄せた）
// 作る Issue：#81（Life Support のパネル）。かき混ぜのボタン（BuildStir）の文字は #84 の案内と同じ Stir tanks
//
// Life Support（●）の席。かたまりは2つ（#102「1席 2〜3 個」）：
//   ① タンク：Tank 1・Tank 2 の圧力計＋バルブを閉じる（#100：どちらかを選ぶ・開け直しなし）
//      どちらが漏れているかは針の動きだけで分かる（#101：状態は足さず、leak_open と leaked から Tank 2 の針だけ下げる）。色や帯や手で Tank 2 を指さない
//   ② CO2 フィルター交換（ほかの1席と同時押し。1人で両席なら2通。Ctx.SendDual）。残りの回数は数のチップ（【09】）。盆の右端
//   stir の段階は「Stir tanks」の主ボタンだけ（M04）。押したら消す
// #190：今できるかたまりには指さしの手（漏れている間はタンクの案内の文、CO2 が注意の値を超えたら Swap のボタン。Swap は主ボタンになって息をする）。
//   終わったかたまりは薄くする。光る輪・影・紺の塗りはやめた
// 文言は下の Txt、大きさ・色は一番下の LpKit と SeatPanel.cs の SeatLook
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
        public const string Tank = "Tank ";                       // ＋番号
        public const string Close = "Close valve", Closed = "Closed";   // 練習の案内（PracticeGuide）の「tap Close valve」と同じ
        public const string HintPractice = "Practice:\ntry a valve.\nNo leak yet.";
        public const string HintFind = "Find the leak.\nWatch which\nneedle drops.";
        public const string HintWrong = "Still leaking!\nWatch which\nneedle drops.";
        public const string HintStopped = "Leak stopped.";
        public const string HintEmpty = "A tank is\nempty. No\nmore leak.";
        public const string FilterUnit = "left";                  // 残りの交換回数（CO2 の値は左の StatusPanel に出ている）
        public const string Swap = "Swap filter", SwapPair = "needs 2 people", SwapSolo = "you press for both", SwapNone = "none left";
        public const string Stir = "Stir tanks";                  // #84 の案内と同じ言葉
        public const string StirNote = "Houston asked for this.\nPress it once.", StirDone = "Stirring...";
    }

    // 形（数値ではなく作り）：メインの酸素タンクは 2 つ（close_valve の tank は 1 か 2）。漏れは Tank 2 固定（SPEC 6章・server config.LEAKING_TANK）。
    // 針を下げるのは Tank 2 だけ（#101）。どのタンクが漏れているかは画面では言わない
    const int Tanks = 2, LeakTank = 2;

    // 閉じたバルブ。席のパネルは席・段階が変わるたびに作り直すので static に置き、新しい飛行（t が戻る・飛行の前）で消す
    static readonly HashSet<int> closed = new HashSet<int>();
    static float closedT = -1f;

    SeatStrip strip;
    LpCard tankCard, filterCard, stirCard;
    readonly LpDial[] dials = new LpDial[Tanks];
    readonly TextMeshProUGUI[] values = new TextMeshProUGUI[Tanks];
    readonly UnityEngine.UI.Image[] falling = new UnityEngine.UI.Image[Tanks];
    readonly int[] last = { -1, -1 };
    readonly float[] fallingUntil = new float[Tanks];
    readonly LpButton[] valves = new LpButton[Tanks];
    TextMeshProUGUI hint, stirNote;
    UiCounter filterLeft;
    LpButton swap;
    UiButton stirBtn;
    string feedback; float feedbackUntil;
    readonly HashSet<string> lit = new HashSet<string>();   // Highlight（#84 が「次に押す物」にしているコマンド）
    float lastNow;

    // ------------------------------------------------------------------ 作る
    protected override void BuildControls(RectTransform row)
    {
        RootAsDetail();
        if (Ctx.Model.Phase != "flight") closed.Clear();
        strip = SeatLook.Strip(row, SeatLook.CursorRoom);   // 左の案内の文を手で指すので、左に手の場所を空ける

        // ① タンク：ヒント・Tank 1・Tank 2
        tankCard = LpKit.Card(strip.Rect, "Tanks");
        hint = LpKit.Note(tankCard.Root, "", TextStyle.Label, T.Text);
        SeatLook.ReserveWidth(hint, Txt.HintPractice, Txt.HintFind, Txt.HintWrong, Txt.HintStopped, Txt.HintEmpty);
        for (int i = 0; i < Tanks; i++)
        {
            int tank = i + 1;
            var unit = UiKit.Row(tankCard.Root, LpKit.Gap, TextAnchor.MiddleLeft); unit.name = "Tank" + tank;
            dials[i] = LpKit.Dial(unit, LpKit.DialD);
            var col = UiKit.Col(unit, 0f, TextAnchor.MiddleLeft);
            SeatLook.Tight(UiKit.Text(col, Txt.Tank + tank, TextStyle.RowName, T.Text, TextAlignmentOptions.MidlineLeft));
            var num = UiKit.Row(col, T.S1, TextAnchor.MiddleLeft);
            values[i] = UiKit.Text(num, "100%", TextStyle.Number, T.Text, TextAlignmentOptions.MidlineRight);
            SeatLook.ReserveWidth(values[i], "100%");   // 桁が変わっても幅を変えない
            falling[i] = LpKit.Caret(num);
            valves[i] = LpKit.Button(unit, "valve", Txt.Close, null, () => CloseValve(tank));
            valves[i].AddMark();   // 閉じたらチェック（場所は先に取っておく）
            valves[i].Reserve(Txt.Closed, null);
        }

        // ② CO2 フィルター（盆の右端）：残りの数のチップ＋Swap
        SeatLook.Spacer(strip.Rect);
        filterCard = LpKit.Card(strip.Rect, "Filter");
        filterLeft = UiKit.Counter(filterCard.Root, "filter", Txt.FilterUnit);
        swap = LpKit.Button(filterCard.Root, "filter", Txt.Swap, SwapSub, Swap);
        swap.AddMark();
        foreach (var sub in new[] { Txt.SwapNone, Txt.SwapPair, Txt.SwapSolo, LpKit.PressWaiting, LpKit.PressNow, LpKit.PressTogether, LpKit.PressFailed })
            swap.Reserve(Txt.Swap, sub);
    }

    /// <summary>M04：かき混ぜの主ボタンだけ。押したら消す（M04 がもう一度流れたら出し直す）</summary>
    protected override void BuildStir(RectTransform row)
    {
        RootAsDetail();
        strip = SeatLook.Strip(row);
        stirCard = LpKit.Card(strip.Rect, "Stir");
        stirBtn = UiKit.PrimaryButton(stirCard.Root, Txt.Stir, "switch", Stir);
        UiKit.Breathe(stirBtn, TitleLook.BreatheAmp);
        stirNote = LpKit.Note(stirCard.Root, Txt.StirNote, TextStyle.Label, T.Text);
        SeatLook.ReserveWidth(stirNote, Txt.StirNote, Txt.StirDone);
        Point(stirBtn.Root, CursorDir.UpLeft, true);
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
        Point(null, CursorDir.UpLeft);
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
            valves[i].SetText(shut ? Txt.Closed : Txt.Close, null);
            valves[i].Set(shut ? LpLook.Off : LpLook.Secondary, !shut);
            valves[i].SetMark(shut ? "check" : null);
        }
        hint.text = !flight ? Txt.HintPractice
            : leaking ? (closed.Count == 0 ? Txt.HintFind : Txt.HintWrong)
            : drained && s.leakOpen ? Txt.HintEmpty : Txt.HintStopped;

        // ② フィルター：CO2 が注意の値（thresholds.co2.warn）を超えたら主ボタンにする。残りが 0 なら押せない
        int left = flight ? s.filters : Mathf.RoundToInt(GameParams.Get("co2_filters"));
        filterLeft.Set(left);
        bool none = flight && s.filters <= 0;
        bool co2High = flight && s.co2 >= Co2Warn(s);

        bool tankFocus = leaking || lit.Contains("close_valve");
        bool filterFocus = (!leaking && co2High && !none) || lit.Contains("swap_filter");
        tankCard.Set(tankFocus, flight && !leaking);
        filterCard.Set(filterFocus, none);
        swap.Set(none ? LpLook.Off : filterFocus ? LpLook.Primary : LpLook.Secondary, !none);
        swap.SetMark(none ? "lock" : null);
        swap.SetNow(filterFocus && !none);
        if (feedback == null) swap.SetText(Txt.Swap, none ? Txt.SwapNone : SwapSub);

        // 手（1画面に1つ）：漏れている間はタンクの案内の文（どちらのタンクも指さない、#101）＞ フィルター
        if (tankFocus) Point(hint.rectTransform, CursorDir.Right, true);
        else if (filterFocus && !none) Point(swap.Root, CursorDir.UpLeft, true);
        else Point(null, CursorDir.UpLeft);
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
            Point(stirBtn.Root, CursorDir.UpLeft, true);
        }
    }

    /// <summary>#84：練習の間「まだ試していない操作」を「次に押す物」にする（close_valve → タンク、swap_filter → フィルター、stir → かき混ぜ）</summary>
    public override void Highlight(string cmd, bool on)
    {
        if (on ? !lit.Add(cmd) : !lit.Remove(cmd)) return;   // #84 は毎フレーム呼ぶので、変わったときだけ描き直す
        if (stirCard != null && cmd == "stir" && stirBtn.Root.gameObject.activeSelf) Point(stirBtn.Root, CursorDir.UpLeft, true);
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
        if (feedback != null && now > feedbackUntil) { feedback = null; Refresh(Ctx.State); }
        strip?.Fit();
        PokeCursor(now);
    }
}

// ====================================================================== 席のパネルの小さな部品（#81・#83 の席のパネル用）
// 名前がほかの担当とぶつからないよう Lp（Life・Planner）を付けた。#190：影・紺の塗り・光る輪をやめ、ピルのボタンは SeatFace（SeatPanel.cs）

/// <summary>ボタンの見た目。Secondary＝副（panel＋bgDeep のふち）／Primary＝主ボタン（go＋縞、今これを押す）／Off＝押せない・済んだ（line）</summary>
public enum LpLook { Secondary, Primary, Off }

/// <summary>ピルのボタン：左にアイコン、右に「主の言葉」と「補助の1行」。見た目の高さ LpKit.BtnH、押せる範囲は T.Hit</summary>
public class LpButton
{
    public RectTransform Root, Body, Content, LabelRow;
    public UnityEngine.UI.Button Button;
    public SeatFace Face;
    public UnityEngine.UI.Image Icon, Mark;
    public TextMeshProUGUI Label, Sub;
    public LpLook Look { get; private set; } = LpLook.Secondary;
    float reserved;
    string mark;
    bool now;

    public void Set(LpLook look, bool interactable = true)
    {
        Look = look;
        Button.interactable = interactable;
        var tone = look == LpLook.Primary ? SeatTone.Main : look == LpLook.Off ? SeatTone.Off : SeatTone.Plain;
        Face.Paint(tone);
        SeatFace.Ink(tone, out var fg, out var sub);
        Label.color = fg;
        if (Sub != null) Sub.color = sub;
        if (Icon != null) Icon.color = fg;
        if (Mark != null) Mark.color = mark != null ? (mark == "check" && tone != SeatTone.Main ? T.Go : fg) : T.Clear;
    }

    /// <summary>いま押す物：息をする（手は SeatPanel.Point が乗せる）</summary>
    public void SetNow(bool on)
    {
        if (on == now) return;
        now = on;
        SeatLook.Breathe(Root, Button, on);
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
        if (icon != null) AddMark();
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
        if (Sub != null && sub != null) { Sub.text = sub; Sub.gameObject.SetActive(sub.Length > 0); }
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

/// <summary>かたまり。面は持たない（盆の上に直に並べる）。終わった・使えないなら薄くする（Idle）。Focus は「今できる」（手を乗せるかはパネルが決める）</summary>
public class LpCard
{
    public RectTransform Root;
    public CanvasGroup Group;
    public bool Focus { get; private set; }

    public void Set(bool focus, bool idle)
    {
        Focus = focus;
        Group.alpha = idle && !focus ? LpKit.IdleAlpha : 1f;
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

public static class LpKit
{
    // ------------------------------------------------------------------ 大きさ（UiTokens・SeatLook から。px は Canvas の基準 1600×900）
    public const float BtnH = SeatLook.BtnH;          // ボタンの見た目の高さ 72（押せる範囲は T.Hit）
    public const float DialD = T.Hit - T.S2;          // 圧力計の直径 80
    public const float NeedleW = T.Border * 2f;       // 針の太さ 6
    public const float HubD = T.S2;                   // 針の軸 16
    public const float MarkLen = T.S2;                // 目盛りの長さ 16
    public const float Gap = SeatLook.Gap;
    // ------------------------------------------------------------------ 動き
    public const float DialSweep = 240f;              // 針の振れ幅（度）。左下＝空、右下＝満タン
    public const float NeedleEase = 4f;               // 針が追いつく速さ（1/秒）
    public const float NeedleSnap = 0.05f;            // これより近ければ止める（度）
    public const float IdleAlpha = 0.5f;              // 終わった・使えないかたまりの濃さ
    public const float FeedbackSec = 2.5f;            // 同時押しの結果（waiting・swapped など）を出しておく秒
    public const float FallingSec = 1.5f;             // 針が下がったあと ▼ を出しておく秒
    public const float MaxDt = 0.1f;
    // ------------------------------------------------------------------ 同時押しの結果の文言（Life Support と Planner の手伝いのボタンで同じ）
    public const string PressWaiting = "waiting for partner", PressNow = "press now!";
    public const string PressTogether = "swapped!", PressFailed = "missed, try again";

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

    /// <summary>かたまり：中身は返す Root に横に足す（面・影・輪なし）</summary>
    public static LpCard Card(Transform strip, string name)
    {
        var c = new LpCard();
        c.Root = UiKit.Row(strip, Gap, TextAnchor.MiddleLeft);
        c.Root.name = name;
        c.Group = c.Root.gameObject.AddComponent<CanvasGroup>();
        c.Set(false, false);
        return c;
    }

    /// <summary>ピルのボタン（左にアイコン）。sub は補助の1行（null なら無し）</summary>
    public static LpButton Button(Transform p, string icon, string label, string sub, System.Action on)
    {
        var b = new LpButton();
        b.Root = Ui.Rect("Button_" + label, p);
        var hit = b.Root.gameObject.AddComponent<UnityEngine.UI.Image>(); hit.color = T.Clear;   // 押せる範囲は T.Hit（見た目より大きい）
        b.Body = Ui.Rect("Body", b.Root); UiKit.MiddleBand(b.Body, BtnH);
        b.Face = SeatFace.Pill(b.Body, BtnH);

        b.Content = UiKit.Row(b.Body, T.S1, TextAnchor.MiddleCenter, T.S3); UiKit.Fill(b.Content);
        if (icon != null) b.Icon = UiKit.Icon(b.Content, icon, T.IconM, T.Text);
        var col = UiKit.Col(b.Content, 0f, TextAnchor.MiddleLeft);
        b.LabelRow = UiKit.Row(col, T.S1, TextAnchor.MiddleLeft);
        b.Label = UiKit.Text(b.LabelRow, label, TextStyle.RowName, T.Text, TextAlignmentOptions.MidlineLeft);
        SeatLook.Tight(b.Label);
        if (sub != null)
        {
            b.Sub = UiKit.Text(col, sub, TextStyle.Label, T.TextSoft, TextAlignmentOptions.MidlineLeft);
            SeatLook.Tight(b.Sub);
            b.Sub.gameObject.SetActive(sub.Length > 0);
        }

        b.Button = SeatLook.Press(b.Root, b.Face.Face, on);
        var le = Ui.Size(b.Root, h: T.Hit); le.minHeight = T.Hit;
        b.Set(LpLook.Secondary);
        b.Fit();
        return b;
    }

    /// <summary>圧力計（直径 d）。bgDeep のふち＋panel の文字盤、目盛り3つ（空・半分・満タン）textSoft、針と軸 text</summary>
    public static LpDial Dial(Transform p, float d)
    {
        var g = new LpDial();
        g.Root = Ui.Rect("Dial", p); UiKit.Fixed(g.Root, d, d);
        var rim = UiKit.Pill(g.Root, T.BgDeep, d, "Rim"); UiKit.Fill(rim.rectTransform);
        var face = UiKit.Pill(g.Root, T.Panel, d - T.RimButton * 2f, "Face"); UiKit.Fill(face.rectTransform, T.RimButton);
        foreach (float a in new[] { DialSweep * 0.5f, 0f, -DialSweep * 0.5f })
        {
            var arm = Ui.Rect("Mark", g.Root);
            arm.anchorMin = arm.anchorMax = arm.pivot = new Vector2(0.5f, 0.5f); arm.sizeDelta = Vector2.zero;
            arm.localEulerAngles = new Vector3(0f, 0f, a);
            var m = UiKit.Pill(arm, T.TextSoft, T.Border, "Tick").rectTransform;
            m.anchorMin = m.anchorMax = new Vector2(0.5f, 0.5f); m.pivot = new Vector2(0.5f, 1f);
            m.sizeDelta = new Vector2(T.Border, MarkLen); m.anchoredPosition = new Vector2(0f, d * 0.5f - T.RimButton - T.S1 / 2f);
        }
        g.Needle = Ui.Rect("Needle", g.Root);
        g.Needle.anchorMin = g.Needle.anchorMax = new Vector2(0.5f, 0.5f); g.Needle.pivot = new Vector2(0.5f, 0f);
        g.Needle.sizeDelta = new Vector2(NeedleW, d * 0.5f - T.RimButton - T.S1); g.Needle.anchoredPosition = Vector2.zero;
        var bar = UiKit.Pill(g.Needle, T.Text, NeedleW, "Bar"); UiKit.Fill(bar.rectTransform);
        var hub = UiKit.Pill(g.Root, T.Text, HubD, "Hub"); UiKit.Center(hub.rectTransform, HubD, HubD);
        g.Set(1f);
        return g;
    }

    /// <summary>針が下がっているときに数字の横に出す ▼（text。色で特定のタンクを指さない）。隠すときも場所は残す</summary>
    public static UnityEngine.UI.Image Caret(Transform p)
    {
        var img = UiKit.Shape(p, T.Art(T.CaretSoft) ?? T.Theme.caretDown, T.Text, 0f, "Falling");
        img.preserveAspect = true;
        UiKit.Fixed(img, T.IconS, T.IconS);
        ShowCaret(img, false);
        return img;
    }

    public static void ShowCaret(UnityEngine.UI.Image img, bool on) => img.color = on ? T.Text : T.Clear;

    public static LpLine Line(Transform p)
    {
        var l = new LpLine();
        l.Root = UiKit.Row(p, T.S1, TextAnchor.MiddleLeft); l.Root.name = "Line";
        l.Icon = UiKit.Icon(l.Root, "check", T.IconS, T.Text);
        l.Text = UiKit.Text(l.Root, "", TextStyle.Label, T.Text, TextAlignmentOptions.MidlineLeft);
        return l;
    }

    /// <summary>短い文（改行で2〜3行まで）。左寄せ</summary>
    public static TextMeshProUGUI Note(Transform p, string text, TextStyle st, Color c) =>
        UiKit.Text(p, text, st, c, TextAlignmentOptions.MidlineLeft, false);
}
