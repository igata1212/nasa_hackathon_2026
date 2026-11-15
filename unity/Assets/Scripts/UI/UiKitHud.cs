// 書いた人：iizuka（作成：Claude Opus 5.5）／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-15、#190 影をやめ、明るい面の文字を text に、吹き出しを blob_bubble に）
// HUD の部品（#71 の残り。UI ガイドライン 3章）：ゲージ・トグル・長押しボタン・カウントダウン・警告の帯・吹き出し・席タブ・同時押しの合図。
// UiKit.cs と同じ作り（コードで組む UGUI ＋ TextMeshPro。色と大きさは UiTokens だけ）。UiKit.cs は変えない。
// 状態は色だけで伝えない：安全＝go／注意＝accent＋caution のアイコン／危険＝danger＋danger のアイコン＋斜めの縞（#109）。
// 動き（輪・弾む・▼の上下・つまみ）は UiTick が毎フレーム呼ぶ。見本は UiKitGallery（メニュー Tools/UI/部品の見本を開く）。
//
// 使い方（どれも親の Transform に足して、返ってきた物の Set などで中身を変える）
//   var g = HudKit.Gauge(parent, "power");                  g.Set(0.42f, HudKit.Low(42, 50, 25), "42%"); g.SetLine(0.1f);
//   var o = HudKit.Gauge(parent, null, "O₂");               // アイコンが無い物は文字の丸（酸素）
//   var t = HudKit.Toggle(parent, "comms", "COMMS", on => Send(on)); t.Set(state.Switch("comms")); t.SetEnabled(false);
//   var h = HudKit.HoldButton(parent, "ignition", "IGNITE", () => Send("ignite")); h.Seconds = HudKit.HoldSeconds(s.slowPct); h.SetEnabled(false, "switch");
//   var c = HudKit.Countdown(parent, "BURN");               c.Set(s.tBurnClose - s.t);
//   var b = HudKit.Banner(parent);                          b.Show("leak", "OXYGEN LEAK", HudLevel.Danger, 10); b.Clear("leak");
//   var u = HudKit.Bubble(parent);                          u.Set("HOUSTON", "Close " + HudKit.Em("Tank 2") + " now.", critical: false);
//   var s = HudKit.SeatTabs(parent, model.MySeats, seat => Rebuild(seat));   s.SetMark("comms_power", "comms_lost");
//   var d = HudKit.DualPress(parent, "ignition", new[] { Seats.Pilot, Seats.Comms }); d.SetMine(model.MySeats); d.Apply(gameEvent);
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;

/// <summary>状態の段階（ゲージ・警告の帯）。色＋アイコンで出す</summary>
public enum HudLevel { Safe, Caution, Danger }

// ====================================================================== ゲージ
/// <summary>ゲージ：左にアイコン 40、高さ 28 のピルの溝と中身、右に数字の箱、その右に状態のアイコン。危険のとき中身に斜めの縞。生還ラインは ink の縦線</summary>
public class UiGauge
{
    public RectTransform Root;
    public Image Icon, Groove, Fill, Stripes, Lifeline, Status;
    public TextMeshProUGUI Symbol, Number;
    public HudLevel Level { get; private set; }
    public float Value { get; private set; }

    /// <param name="frac">0〜1（満タンに対する割合）</param>
    /// <param name="number">右の数字（"42%" など。書式は呼ぶ側で決める）</param>
    public void Set(float frac, HudLevel level, string number)
    {
        Value = Mathf.Clamp01(frac);
        Level = level;
        Fill.gameObject.SetActive(Value > 0f);   // Mask の面を消すと縞がはみ出すので、物ごと消す
        HudKit.PlaceOnGroove(Fill.rectTransform, Value, true);
        Fill.color = HudKit.LevelColor(level);
        Stripes.gameObject.SetActive(level == HudLevel.Danger);
        Number.text = number;
        HudKit.SetStatusIcon(Status, level);
    }

    /// <summary>生還ライン（0〜1）。負なら出さない</summary>
    public void SetLine(float frac)
    {
        Lifeline.gameObject.SetActive(frac >= 0f);
        if (frac >= 0f) HudKit.PlaceOnGroove(Lifeline.rectTransform, Mathf.Clamp01(frac), false);
    }
}

// ====================================================================== トグル
/// <summary>トグル（ON/OFF）：アイコン＋名前＋スイッチ。ON＝go の面・つまみが右・「ON」、OFF＝inkSub の面・つまみが左・「OFF」。押せないとき line の面とつまみに鍵。行のどこを押してもよい（高さ 96）</summary>
public class UiToggle
{
    public RectTransform Root, Body, Knob;
    public Button Button;
    public Image Icon, Face, KnobFace, KnobIcon;
    public TextMeshProUGUI Label, State;
    /// <summary>押されたとき、なりたい値で呼ぶ（見た目は先に変える。サーバーの state が来たら Set で合わせ直す）</summary>
    public Action<bool> OnChange;
    public bool On { get; private set; }
    public bool Enabled { get; private set; } = true;
    float knobX, knobTarget;
    bool placed;

    public void Set(bool on) { On = on; Apply(); }

    public void SetEnabled(bool on) { Enabled = on; Button.interactable = on; Apply(); }

    internal void Click()
    {
        if (!Enabled) return;
        On = !On;
        Apply();
        OnChange?.Invoke(On);
    }

    void Apply()
    {
        float pad = (T.ToggleH - T.ToggleKnob) * 0.5f;
        float kx = T.ToggleW * 0.5f - pad - T.ToggleKnob * 0.5f;          // つまみの中心（右端）
        float left = -T.ToggleW * 0.5f + pad, right = kx - T.ToggleKnob * 0.5f;   // つまみの反対側の空き
        knobTarget = On ? kx : -kx;
        if (!placed) { knobX = knobTarget; placed = true; Knob.anchoredPosition = new Vector2(knobX, 0f); }
        var sr = State.rectTransform;
        sr.sizeDelta = new Vector2(right - left, T.ToggleH);
        sr.anchoredPosition = new Vector2((On ? 1f : -1f) * (left + right) * 0.5f, 0f);
        State.text = On ? "On" : "Off";
        Face.color = !Enabled ? T.Line : On ? T.Go : T.TextSoft;
        State.color = Enabled ? T.Panel : T.TextSoft;
        KnobIcon.enabled = !Enabled;
        Label.color = Enabled ? T.Text : T.TextSoft;
        if (Icon != null) Icon.color = Label.color;
    }

    internal void Tick(float dt)
    {
        if (Mathf.Approximately(knobX, knobTarget)) return;
        float speed = Mathf.Abs(knobTarget) * 2f / T.ToggleSlideSec;
        knobX = Mathf.MoveTowards(knobX, knobTarget, speed * dt);
        Knob.anchoredPosition = new Vector2(knobX, 0f);
    }
}

// ====================================================================== 長押しボタン
/// <summary>長押しボタン：円 96 の外側に進み具合の輪（go）。Seconds だけ押し続けると OnDone を呼び、少しのあいだ ✓ を出す。途中で離すと輪が戻る。
/// 押せないとき：line の面＋鍵、右上に足りない条件のアイコン（needIcon）</summary>
public class UiHoldButton
{
    public RectTransform Root, Body;
    public Button Button;
    public Image Track, Ring, Face, Icon, Need, NeedIcon;
    public TextMeshProUGUI Label;
    public Action OnDone;
    /// <summary>押し続ける時間（秒）。操作が遅くなるときは HudKit.HoldSeconds(slowPct) を入れ直す</summary>
    public float Seconds = T.HoldBaseSec;
    public float Progress { get; private set; }
    public bool Enabled { get; private set; } = true;
    internal Color faceOn;
    internal Sprite iconOn;
    bool holding;
    float flash = -1f;

    public void SetEnabled(bool on, string needIcon = null)
    {
        Enabled = on;
        Button.interactable = on;
        if (!on) holding = false;
        EndFlash();
        Face.color = on ? faceOn : T.Line;
        Icon.sprite = on ? iconOn : T.Icon("lock");
        Icon.color = on ? T.Text : T.TextSoft;
        if (Label != null) Label.color = on ? T.Text : T.TextSoft;
        bool need = !on && needIcon != null;
        Need.gameObject.SetActive(need);
        if (need) NeedIcon.sprite = T.Icon(needIcon);
    }

    internal void Down()
    {
        if (!Enabled) return;
        EndFlash();
        holding = true;
    }

    internal void Up() => holding = false;

    void EndFlash()
    {
        if (flash < 0f) return;
        flash = -1f;
        Icon.sprite = Enabled ? iconOn : T.Icon("lock");
    }

    internal void Tick(float dt)
    {
        if (flash >= 0f)
        {
            flash += dt;
            if (flash < T.DoneFlashSec) return;
            EndFlash();
        }
        if (holding)
        {
            Progress += Seconds > 0f ? dt / Seconds : 1f;
            if (Progress >= 1f)
            {
                Progress = 1f; holding = false; flash = 0f;
                Icon.sprite = T.Icon("check");
                OnDone?.Invoke();
            }
        }
        else if (Progress > 0f) Progress = Mathf.Max(0f, Progress - dt / T.HoldDrainSec);
        Ring.fillAmount = Progress;
    }
}

// ====================================================================== カウントダウン
/// <summary>カウントダウンのチップ：ピル 64、ink の面に白の数字、左に timer。残り 10 秒で面を danger にし、1秒ごとに弾む</summary>
public class UiCountdown
{
    public RectTransform Root;
    public Image Face, Icon;
    public TextMeshProUGUI Label, Number;
    int lastSec = int.MinValue;
    float bounce = -1f;

    public void Set(float seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
        bool warn = seconds <= T.CountdownWarnSec;
        Face.color = warn ? T.Danger : T.Ink;
        if (s == lastSec) return;
        if (warn && lastSec != int.MinValue) bounce = 0f;
        lastSec = s;
        Number.text = Ui.Mmss(seconds);
    }

    internal void Tick(float dt)
    {
        if (bounce < 0f) return;
        bounce += dt;
        Root.localScale = Vector3.one * HudKit.Bounce(bounce);
        if (bounce >= T.BounceSec) bounce = -1f;
    }
}

// ====================================================================== 警告の帯
/// <summary>警告の帯：画面上の中央、ピル 72。危険＝danger に白文字＋danger のアイコン、注意＝accent に ink の文字＋caution のアイコン。
/// 同時に出すのは1本だけ。Show した物を覚えておき、一番重い物（危険＞注意、その中は priority が大きい物、同じなら新しい物）だけ出し、残りの数を「+N」で添える</summary>
public class UiBanner
{
    public RectTransform Root, More;
    public Image Face, Icon, MoreFace;
    public TextMeshProUGUI Text, MoreText;
    public string Current { get; private set; }

    class Entry { public string key, text; public HudLevel level; public int priority, order; }
    readonly List<Entry> entries = new List<Entry>();
    int order;
    float bounce = -1f;

    /// <param name="key">同じ key で呼ぶと中身を入れ替える（"leak" など）</param>
    public void Show(string key, string text, HudLevel level, int priority = 0)
    {
        var e = entries.Find(x => x.key == key);
        if (e == null) { e = new Entry { key = key }; entries.Add(e); }
        e.text = text; e.level = level == HudLevel.Safe ? HudLevel.Caution : level; e.priority = priority; e.order = ++order;
        Apply();
    }

    public void Clear(string key) { entries.RemoveAll(x => x.key == key); Apply(); }
    public void ClearAll() { entries.Clear(); Apply(); }

    void Apply()
    {
        Entry top = null;
        foreach (var e in entries)
            if (top == null || e.level > top.level || e.level == top.level && (e.priority > top.priority || e.priority == top.priority && e.order > top.order)) top = e;
        Root.gameObject.SetActive(top != null);
        if (top == null) { Current = null; return; }
        bool danger = top.level == HudLevel.Danger;
        Color fg = danger ? T.Panel : T.Text;
        Face.color = danger ? T.Danger : T.Accent;
        Icon.sprite = T.Icon(danger ? "danger" : "caution"); Icon.color = fg;
        Text.text = top.text; Text.color = fg;
        More.gameObject.SetActive(entries.Count > 1);
        MoreText.text = "+" + (entries.Count - 1);
        if (Current != top.key) bounce = 0f;
        Current = top.key;
    }

    internal void Tick(float dt)
    {
        if (bounce < 0f) return;
        bounce += dt;
        Root.localScale = Vector3.one * HudKit.Bounce(bounce);
        if (bounce >= T.BounceSec) bounce = -1f;
    }
}

// ====================================================================== 吹き出し
/// <summary>吹き出し（ヒューストン先生の台詞）：panel・角丸 40・幅 880。左上に名前タグ（go に白文字、傾ける、上端に半分かぶせる）、下の中央に ▼（accent、上下に揺れる）。
/// 文は最大3行（はみ出す分は … で切る）。critical のときだけタグを danger にして danger のアイコンを足す。固有名詞と数字は HudKit.Em で色を付ける</summary>
public class UiBubble
{
    public RectTransform Root, Tag;
    public Image TagFace, TagIcon, Caret;
    public TextMeshProUGUI Name, Text;
    internal LayoutElement textSize;
    internal float width;
    float bobT;

    public void Set(string name, string text, bool critical = false)
    {
        Name.text = name;
        TagFace.color = critical ? T.Danger : T.Go;
        TagIcon.gameObject.SetActive(critical);
        Text.text = text;
        var face = Text.font.faceInfo;
        float lineH = Text.fontSize * Mathf.Max(T.TalkLineHeight, face.lineHeight / face.pointSize);
        float h = Mathf.Min(Text.GetPreferredValues(text, width - T.BubblePadX * 2f, 0f).y, lineH * T.BubbleMaxLines);
        // 短い1行は中央、2行以上は左寄せ（【02】【12】）
        Text.alignment = Text.GetPreferredValues(text).x <= width - T.BubblePadX * 2f ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
        textSize.preferredHeight = h; textSize.minHeight = h;
    }

    public void ShowCaret(bool on) => Caret.gameObject.SetActive(on);

    internal void Tick(float dt)
    {
        if (!Caret.gameObject.activeSelf) return;
        bobT = (bobT + dt) % T.CaretPeriod;
        float k = 0.5f - 0.5f * Mathf.Cos(bobT / T.CaretPeriod * Mathf.PI * 2f);
        Caret.rectTransform.anchoredPosition = new Vector2(0f, -T.CaretH * 0.5f + T.CaretBob * (1f - k));   // #190 縁に半分かぶせる
    }
}

// ====================================================================== 席タブ
/// <summary>席タブ（1人で複数の席を持つとき、1席ぶんずつ切り替える。#102）：ピル 72 に席バッジ＋席の名前。
/// 選んでいるタブ＝panel の面・ink の枠・影、ほかのタブ＝bg の面・line の枠・影なし。右上に印（切断・まだ試していない操作など）を出せる。席が1つなら隠す</summary>
public class UiSeatTabs
{
    public RectTransform Root;
    public Action<string> OnSelect;
    public bool HideWhenSingle = true;
    public string Selected { get; private set; }
    public IReadOnlyList<string> SeatList => seats;

    class Tab
    {
        public string Seat;
        public RectTransform Root, Body, Mark;
        public Image Shadow, Frame, Face, MarkIcon;
        public UiSeatBadge Badge;
        public TextMeshProUGUI Label;
    }
    readonly List<Tab> tabs = new List<Tab>();
    readonly List<string> seats = new List<string>();

    /// <summary>持っている席を入れ直す（同じなら何もしない）。選んでいた席が無くなったら先頭を選ぶ</summary>
    public void SetSeats(IList<string> list)
    {
        list = list ?? new List<string>();
        bool same = list.Count == seats.Count;
        for (int i = 0; same && i < list.Count; i++) same = list[i] == seats[i];
        if (!same)
        {
            foreach (var t in tabs) { t.Root.gameObject.SetActive(false); UnityEngine.Object.Destroy(t.Root.gameObject); }
            tabs.Clear(); seats.Clear(); seats.AddRange(list);
            foreach (var s in seats)
            {
                var t = new Tab { Seat = s };
                HudKit.BuildTab(this, s, out t.Root, out t.Body, out t.Shadow, out t.Frame, out t.Face, out t.Badge, out t.Label, out t.Mark, out t.MarkIcon);
                tabs.Add(t);
            }
        }
        Root.gameObject.SetActive(seats.Count > 1 || !HideWhenSingle);
        Select(seats.Contains(Selected) ? Selected : seats.Count > 0 ? seats[0] : null);
    }

    /// <summary>席を選ぶ。notify なら（変わったときだけ）OnSelect を呼ぶ</summary>
    public void Select(string seat, bool notify = false)
    {
        bool changed = seat != Selected;
        Selected = seat;
        foreach (var t in tabs)
        {
            bool on = t.Seat == seat;
            t.Shadow.enabled = on;
            t.Frame.color = on ? T.Text : T.Line;
            t.Face.color = on ? T.Panel : T.Bg;
            t.Label.color = on ? T.Text : T.TextSoft;
        }
        if (notify && changed && seat != null) OnSelect?.Invoke(seat);
    }

    /// <summary>タブの右上の印。icon が null なら消す（例："comms_lost"＝その席の人が切れた、"caution"＝まだ試していない操作がある）</summary>
    public void SetMark(string seat, string icon)
    {
        var t = tabs.Find(x => x.Seat == seat);
        if (t == null) return;
        t.Mark.gameObject.SetActive(icon != null);
        if (icon != null) t.MarkIcon.sprite = T.Icon(icon);
    }
}

// ====================================================================== 同時押しの合図
/// <summary>同時押しの合図：大きい席バッジ（96）を横に並べ、押した席は外側の輪（幅 8、go）が受付の時間（window_s）で一周する。
/// そろったら全員の輪が go で埋まり、バッジが ✓ に変わる。外れたら押した席の輪を danger にして ✗。上に「HOLD TOGETHER」。サーバーの press イベントは Apply に渡す</summary>
public class UiDualPress
{
    public enum Phase { Idle, Waiting, Together, Failed }
    public const string TitleIdle = "Hold together", TitleTogether = "Together!", TitleFailed = "Missed. Try again";

    public RectTransform Root, Row;
    public Image CmdIcon, StateIcon;
    public TextMeshProUGUI Title;
    public Phase State { get; private set; }

    class Slot
    {
        public string Seat;
        public RectTransform Root;
        public Image Track, Ring, Check;
        public UiSeatBadge Badge;
        public RectTransform You;
        public bool pressing;
        public float start, window;
    }
    readonly List<Slot> slots = new List<Slot>();
    readonly HashSet<string> mine = new HashSet<string>();
    float bounce = -1f;

    /// <summary>そろえる席を入れ直す（点火なら Pilot と Comms &amp; Power）</summary>
    public void SetSeats(IList<string> seats)
    {
        foreach (var s in slots) { s.Root.gameObject.SetActive(false); UnityEngine.Object.Destroy(s.Root.gameObject); }
        slots.Clear();
        foreach (var seat in seats)
        {
            var s = new Slot { Seat = seat };
            HudKit.BuildPressSlot(Row, seat, out s.Root, out s.Track, out s.Ring, out s.Badge, out s.Check, out s.You);
            slots.Add(s);
        }
        SetMine(mine);
        Reset();
    }

    /// <summary>自分の席に YOU を出す（1人で両方の席なら両方に出る）</summary>
    public void SetMine(IEnumerable<string> seats)
    {
        var list = seats == null ? new List<string>() : new List<string>(seats);
        mine.Clear(); foreach (var s in list) mine.Add(s);
        foreach (var s in slots) s.You.gameObject.SetActive(mine.Contains(s.Seat));
    }

    public void Reset()
    {
        State = Phase.Idle;
        foreach (var s in slots) { s.pressing = false; Paint(s, 0f, T.Go, false); }
        SetTitle(TitleIdle, null, T.Text);
    }

    /// <summary>pressed の席が押した。輪が windowSec で一周する（0 なら最初から一周）</summary>
    public void Waiting(IEnumerable<string> pressed, float windowSec)
    {
        if (State != Phase.Waiting) Reset();
        State = Phase.Waiting;
        var set = new HashSet<string>(pressed ?? new string[0]);
        foreach (var s in slots)
            if (set.Contains(s.Seat) && !s.pressing) { s.pressing = true; s.start = Time.unscaledTime; s.window = windowSec; }
    }

    public void Together()
    {
        State = Phase.Together;
        foreach (var s in slots) { s.pressing = false; Paint(s, 1f, T.Go, true); }
        SetTitle(TitleTogether, "check", T.Go);
        bounce = 0f;
    }

    /// <summary>そろわなかった。pressed（無ければ押していた席）の輪を danger にする</summary>
    public void Failed(IEnumerable<string> pressed = null)
    {
        var set = pressed == null ? null : new HashSet<string>(pressed);
        State = Phase.Failed;
        foreach (var s in slots)
        {
            bool hit = set != null ? set.Contains(s.Seat) : s.pressing;
            s.pressing = false;
            Paint(s, hit ? 1f : 0f, T.Danger, false);
        }
        SetTitle(TitleFailed, "cross", T.Danger);
    }

    /// <summary>サーバーの press イベント（waiting／together／failed）を出す。press でなければ false</summary>
    public bool Apply(GameEvent e)
    {
        if (e == null || e.kind != "press") return false;
        var seats = new List<string>();
        var a = J.Arr(e.raw, "seats");
        if (a != null) foreach (var x in a) seats.Add(x.ToString());
        if (e.status == "waiting") Waiting(seats, J.Num(e.raw, "window_s"));
        else if (e.status == "together") Together();
        else if (e.status == "failed") Failed(seats);
        else return false;
        return true;
    }

    void Paint(Slot s, float fill, Color ring, bool done)
    {
        s.Ring.fillAmount = fill; s.Ring.color = ring;
        s.Badge.Shape.enabled = !done;
        s.Check.enabled = done;
    }

    void SetTitle(string text, string icon, Color iconColor)
    {
        Title.text = text;
        StateIcon.enabled = icon != null;
        if (icon != null) { StateIcon.sprite = T.Icon(icon); StateIcon.color = iconColor; }
    }

    internal void Tick(float dt)
    {
        if (State == Phase.Waiting)
            foreach (var s in slots)
                if (s.pressing) s.Ring.fillAmount = s.window > 0f ? Mathf.Clamp01((Time.unscaledTime - s.start) / s.window) : 1f;
        if (bounce < 0f) return;
        bounce += dt;
        Row.localScale = Vector3.one * HudKit.Bounce(bounce);
        if (bounce >= T.BounceSec) bounce = -1f;
    }
}

// ====================================================================== 作る所
public static class HudKit
{
    // ------------------------------------------------------------------ 共通
    public static Color LevelColor(HudLevel l) => l == HudLevel.Danger ? T.Danger : l == HudLevel.Caution ? T.Accent : T.Go;

    /// <summary>少ないほど悪い値（電力・酸素・温度・燃料）。しきい値は parameters.json などから呼ぶ側が渡す</summary>
    public static HudLevel Low(float v, float caution, float danger) => v <= danger ? HudLevel.Danger : v <= caution ? HudLevel.Caution : HudLevel.Safe;

    /// <summary>多いほど悪い値（CO2）</summary>
    public static HudLevel High(float v, float caution, float danger) => v >= danger ? HudLevel.Danger : v >= caution ? HudLevel.Caution : HudLevel.Safe;

    /// <summary>長押しの時間：基本 ×（1 + slow/100）（SPEC 6章）</summary>
    public static float HoldSeconds(float slowPct) => T.HoldBaseSec * (1f + Mathf.Max(0f, slowPct) / 100f);

    /// <summary>台詞の固有名詞と数字に色を付ける（TMP のリッチテキスト）</summary>
    public static string Em(string s) => "<color=#" + ColorUtility.ToHtmlStringRGB(T.BubbleEm) + ">" + s + "</color>";

    /// <summary>弾む大きさ（弾んでから t 秒）。BounceSec で 1 に戻る</summary>
    public static float Bounce(float t) => t < 0f || t >= T.BounceSec ? 1f : 1f + (T.BounceScale - 1f) * Mathf.Sin(Mathf.PI * t / T.BounceSec);

    /// <summary>ゲージの溝の上に置く。中身（fill）は左端から、frac=0 で丸（幅 GaugeH）・1 で溝いっぱい。縦線は中身の右端と同じ位置</summary>
    internal static void PlaceOnGroove(RectTransform r, float frac, bool fill)
    {
        float edge = T.GaugeH * (1f - frac);   // 右端 = frac×幅 + GaugeH×(1−frac)
        if (fill)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(frac, 1f);
            r.offsetMin = Vector2.zero; r.offsetMax = new Vector2(edge, 0f);
        }
        else
        {
            r.anchorMin = new Vector2(frac, 0f); r.anchorMax = new Vector2(frac, 1f); r.pivot = new Vector2(1f, 0.5f);
            r.offsetMin = new Vector2(edge - T.LifelineW, -T.LifelineOver); r.offsetMax = new Vector2(edge, T.LifelineOver);
        }
    }

    internal static void SetStatusIcon(Image img, HudLevel l)
    {
        img.enabled = l != HudLevel.Safe;   // 消しても場所は空けておく（数字が動かない）
        if (l == HudLevel.Safe) return;
        img.sprite = T.Icon(l == HudLevel.Danger ? "danger" : "caution");
        img.color = LevelColor(l);
    }

    /// <summary>円（pill.png をそのまま）。radial なら上から時計回りに埋まる輪の面</summary>
    static Image Disc(Transform p, Color c, float d, string name, bool radial = false)
    {
        var r = Ui.Rect(name, p);
        var img = r.gameObject.AddComponent<Image>();
        img.sprite = T.Theme.pill; img.color = c; img.raycastTarget = false;
        if (radial)
        {
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Top;
            img.fillClockwise = true;
            img.fillAmount = 0f;
        }
        UiKit.Center(r, d, d);
        return img;
    }

    /// <summary>影つきのピルの面を root いっぱいに敷く（レイアウトの外）</summary>
    static Image PillFace(RectTransform root, float h, Color face, bool shadow)
    {
        // #190 影はやめた（shadow は呼び出しの形のために残す）。面は blob_pill
        var f = UiKit.Blob(root, T.BlobPill, face, h * 0.5f, "Face"); UiKit.Fill(f.rectTransform); UiKit.Ignore(f);
        return f;
    }

    /// <summary>右上の小さな印（ink の丸に panel のアイコン 24）</summary>
    static RectTransform Mark(RectTransform body, out Image icon)
    {
        var disc = Disc(body, T.Text, T.MarkD, "Mark");
        var r = disc.rectTransform;
        r.anchorMin = r.anchorMax = Vector2.one; r.anchoredPosition = new Vector2(-T.S1, -T.S1);
        icon = UiKit.Icon(r, null, T.IconS, T.Panel);
        UiKit.Center(icon.rectTransform, T.IconS, T.IconS);
        r.gameObject.SetActive(false);
        return r;
    }

    /// <summary>上の中央に w×h で置く</summary>
    static void Top(RectTransform r, float w, float h)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 1f);
        r.sizeDelta = new Vector2(w, h); r.anchoredPosition = Vector2.zero;
    }

    /// <summary>UiKit のボタンと同じ押し心地の Button（色は面に掛ける）</summary>
    static Button AddButton(GameObject go, Graphic target)
    {
        var b = go.AddComponent<Button>();
        b.targetGraphic = target;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        var cb = b.colors;
        cb.normalColor = cb.selectedColor = cb.disabledColor = Color.white;
        cb.highlightedColor = T.TintHover; cb.pressedColor = T.TintPressed; cb.fadeDuration = T.TintFade;
        b.colors = cb;
        return b;
    }

    static Image HitArea(RectTransform r)
    {
        var hit = r.gameObject.AddComponent<Image>();
        hit.color = T.Clear;
        return hit;
    }

    // ------------------------------------------------------------------ ゲージ
    /// <param name="icon">左のアイコン（power・temperature・co2・fuel など）。null なら symbol を丸に入れる</param>
    /// <param name="symbol">アイコンが無い物の文字（酸素の "O₂"）</param>
    public static UiGauge Gauge(Transform p, string icon, string symbol = null)
    {
        var g = new UiGauge();
        g.Root = UiKit.Row(p, T.S2, TextAnchor.MiddleLeft);
        g.Root.name = "Gauge_" + (icon ?? symbol);
        Ui.Size(g.Root, w: T.GaugeW, flexW: 1f);
        if (icon == null)
        {
            var chip = UiKit.Pill(g.Root, T.Text, T.IconM, "Symbol"); UiKit.Fixed(chip, T.IconM, T.IconM);
            g.Symbol = UiKit.Text(chip.transform, symbol, TextStyle.Tag, T.Panel, TextAlignmentOptions.Center, false);
            UiKit.Fill(g.Symbol.rectTransform);
        }
        else g.Icon = UiKit.Icon(g.Root, icon, T.IconM, T.Text);

        var box = Ui.Rect("Track", g.Root);
        var le = Ui.Size(box, h: T.GaugeH + T.LifelineOver * 2f, flexW: 1f);
        le.minWidth = T.GaugeMinTrackW; le.minHeight = le.preferredHeight;
        g.Groove = UiKit.Pill(box, T.Line, T.GaugeH, "Groove"); UiKit.MiddleBand(g.Groove.rectTransform, T.GaugeH);
        g.Fill = UiKit.Pill(g.Groove.transform, T.Go, T.GaugeH, "Fill");
        g.Fill.gameObject.AddComponent<Mask>().showMaskGraphic = true;   // 縞を中身の丸い形で切る
        var st = Ui.Rect("Stripes", g.Fill.transform);
        g.Stripes = st.gameObject.AddComponent<Image>();
        g.Stripes.sprite = T.Theme.stripes; g.Stripes.type = Image.Type.Tiled; g.Stripes.color = T.Panel; g.Stripes.raycastTarget = false;
        UiKit.Fill(st);
        g.Lifeline = UiKit.Pill(g.Groove.transform, T.Text, T.LifelineW, "Lifeline");

        g.Number = UiKit.Text(g.Root, "", TextStyle.Number, T.Text, TextAlignmentOptions.MidlineRight);
        UiKit.Fixed(g.Number, T.GaugeNumberW, T.TextNumber * T.HeadingLineHeight);
        g.Status = UiKit.Icon(g.Root, null, T.IconM, T.Text);
        g.Set(0f, HudLevel.Safe, "");
        g.SetLine(-1f);
        return g;
    }

    // ------------------------------------------------------------------ トグル
    public static UiToggle Toggle(Transform p, string icon, string label, Action<bool> onChange)
    {
        var t = new UiToggle { OnChange = onChange };
        t.Root = UiKit.Row(p, T.S2, TextAnchor.MiddleLeft);
        t.Root.name = "Toggle_" + label;
        HitArea(t.Root);
        var le = Ui.Size(t.Root, h: T.Hit); le.minHeight = T.Hit;
        if (icon != null) t.Icon = UiKit.Icon(t.Root, icon, T.IconM, T.Text);
        t.Label = UiKit.Text(t.Root, label, TextStyle.Sub, T.Text, TextAlignmentOptions.MidlineLeft);
        Ui.Size(t.Label, flexW: 1f);

        var sw = Ui.Rect("Switch", t.Root); UiKit.Fixed(sw, T.ToggleW, T.ToggleH);
        t.Body = Ui.Rect("Body", sw); UiKit.Fill(t.Body);
        t.Face = UiKit.Pill(t.Body, T.Go, T.ToggleH, "Face"); UiKit.Fill(t.Face.rectTransform);
        t.State = UiKit.Text(t.Body, "", TextStyle.Tag, T.Panel, TextAlignmentOptions.Center, false);
        UiKit.Center(t.State.rectTransform, T.ToggleW, T.ToggleH);
        t.Knob = Ui.Rect("Knob", t.Body); UiKit.Center(t.Knob, T.ToggleKnob, T.ToggleKnob);
        t.KnobFace = UiKit.Pill(t.Knob, T.Panel, T.ToggleKnob, "Face"); UiKit.Fill(t.KnobFace.rectTransform);
        t.KnobIcon = UiKit.Icon(t.Knob, "lock", T.IconS, T.TextSoft); UiKit.Center(t.KnobIcon.rectTransform, T.IconS, T.IconS);

        t.Button = AddButton(t.Root.gameObject, t.Face);
        t.Button.onClick.AddListener(t.Click);
        t.Root.gameObject.AddComponent<UiPress>().Init(t.Body, t.Button);
        UiTick.On(t.Root.gameObject, t.Tick);
        t.Set(false);
        return t;
    }

    // ------------------------------------------------------------------ 長押しボタン
    /// <param name="face">面の色。panel の上に置くときは bg を渡す（アイコンボタンと同じ）</param>
    public static UiHoldButton HoldButton(Transform p, string icon, string label, Action onDone, Color? face = null)
    {
        float ring = T.HoldD + T.RingW * 2f;
        float labelH = label == null ? 0f : T.S1 + T.TextLabel * T.BodyLineHeight;
        var b = new UiHoldButton { OnDone = onDone, faceOn = face ?? T.Panel };
        b.Root = Ui.Rect("Hold_" + (label ?? icon), p);
        b.Root.sizeDelta = new Vector2(ring, ring + labelH);
        HitArea(b.Root);

        b.Body = Ui.Rect("Body", b.Root); Top(b.Body, ring, ring);
        b.Track = Disc(b.Body, T.Line, ring, "Track");
        b.Ring = Disc(b.Body, T.Go, ring, "Ring", true);
        b.Face = Disc(b.Body, b.faceOn, T.HoldD, "Face");
        b.Icon = UiKit.Icon(b.Body, icon, T.IconInButton, T.Text); UiKit.Center(b.Icon.rectTransform, T.IconInButton, T.IconInButton);
        b.iconOn = b.Icon.sprite;
        b.Need = Mark(b.Body, out b.NeedIcon).GetComponent<Image>();
        if (label != null)
        {
            b.Label = UiKit.Text(b.Root, label, TextStyle.Label, T.Text);
            var lr = b.Label.rectTransform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = new Vector2(1f, 0f); lr.pivot = new Vector2(0.5f, 0f);
            lr.sizeDelta = new Vector2(0f, labelH - T.S1); lr.anchoredPosition = Vector2.zero;
        }

        b.Button = AddButton(b.Root.gameObject, b.Face);
        b.Root.gameObject.AddComponent<UiPress>().Init(b.Body, b.Button);
        var ptr = b.Root.gameObject.AddComponent<UiPointer>();
        ptr.Down = b.Down; ptr.Up = b.Up;
        UiTick.On(b.Root.gameObject, b.Tick);
        UiKit.Fixed(b.Root, ring, ring + labelH);
        return b;
    }

    // ------------------------------------------------------------------ カウントダウン
    /// <param name="label">数字の前の短い言葉（"BURN" など）。null なら timer と数字だけ</param>
    public static UiCountdown Countdown(Transform p, string label = null)
    {
        var c = new UiCountdown();
        c.Root = UiKit.Row(p, T.S2, TextAnchor.MiddleCenter, T.S3);
        c.Root.name = "Countdown" + (label != null ? "_" + label : "");
        var le = Ui.Size(c.Root, h: T.CountdownH); le.minHeight = T.CountdownH; le.flexibleWidth = 0f;
        c.Face = PillFace(c.Root, T.CountdownH, T.Ink, false);
        c.Icon = UiKit.Icon(c.Root, "timer", T.CountdownIcon, T.Panel);
        if (label != null) c.Label = UiKit.Text(c.Root, label, TextStyle.Tag, T.Panel);
        c.Number = UiKit.Text(c.Root, "", TextStyle.Number, T.Panel, TextAlignmentOptions.MidlineRight);
        UiKit.Fixed(c.Number, T.CountdownNumberW, T.TextNumber * T.HeadingLineHeight);
        UiTick.On(c.Root.gameObject, c.Tick);
        c.Set(0f);
        return c;
    }

    // ------------------------------------------------------------------ 警告の帯
    /// <summary>親の上の中央（画面の端から ScreenEdge）に置く。幅は中身に合わせる。最初は隠れている</summary>
    public static UiBanner Banner(Transform p)
    {
        var b = new UiBanner();
        b.Root = UiKit.Row(p, T.S2, TextAnchor.MiddleCenter, T.BannerPadX);
        b.Root.name = "WarningBanner";
        b.Root.anchorMin = b.Root.anchorMax = b.Root.pivot = new Vector2(0.5f, 1f);
        b.Root.anchoredPosition = new Vector2(0f, -T.ScreenEdge);
        var fit = b.Root.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var le = Ui.Size(b.Root, h: T.BannerH); le.minHeight = T.BannerH;
        b.Face = PillFace(b.Root, T.BannerH, T.Danger, true);
        b.Icon = UiKit.Icon(b.Root, "danger", T.IconM, T.Panel);
        b.Text = UiKit.Text(b.Root, "", TextStyle.Sub, T.Panel);

        b.More = UiKit.Row(b.Root, 0f, TextAnchor.MiddleCenter, T.S2);
        b.More.name = "More";
        b.MoreFace = UiKit.Pill(b.More, T.Panel, T.TagH, "Face"); UiKit.Fill(b.MoreFace.rectTransform); UiKit.Ignore(b.MoreFace);
        b.MoreText = UiKit.Text(b.More, "", TextStyle.Tag, T.Text);
        var mle = Ui.Size(b.More, h: T.TagH); mle.minHeight = T.TagH;

        UiTick.On(b.Root.gameObject, b.Tick);
        b.Root.gameObject.SetActive(false);
        return b;
    }

    // ------------------------------------------------------------------ 吹き出し
    /// <summary>会話の吹き出し（blob_bubble・影としっぽなし・文字 36 行間 1.5）。幅 width（既定 1040。船内は T.BubbleWCabin）。高さは文に合わせる。置き場所は呼ぶ側で決める（pivot は下の中央）</summary>
    public static UiBubble Bubble(Transform p, float width = T.BubbleW)
    {
        var u = new UiBubble { width = width };
        u.Root = Ui.Rect("Bubble", p);
        u.Root.pivot = new Vector2(0.5f, 0f);
        u.Root.sizeDelta = new Vector2(width, T.BubbleMinH);
        var l = u.Root.gameObject.AddComponent<VerticalLayoutGroup>();
        int px = Mathf.RoundToInt(T.BubblePadX), py = Mathf.RoundToInt(T.BubblePadY);
        l.padding = new RectOffset(px, px, py, py);
        l.childAlignment = TextAnchor.MiddleLeft;
        l.childControlWidth = l.childControlHeight = true;
        l.childForceExpandWidth = true; l.childForceExpandHeight = false;
        var le = Ui.Size(u.Root, w: width); le.minWidth = width; le.minHeight = T.BubbleMinH; le.flexibleWidth = 0f;
        u.Root.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var bf = UiKit.Blob(u.Root, T.BlobBubble, T.Panel, T.BubbleCornerX, "Face"); UiKit.Fill(bf.rectTransform); UiKit.Ignore(bf);

        u.Text = UiKit.Text(u.Root, "", TextStyle.Talk, T.Text, TextAlignmentOptions.MidlineLeft, false);
        if (u.Text.font != null)
        {
            var fi = u.Text.font.faceInfo;   // 行間 1.5（TMP の lineSpacing は字の 1/100 単位で足す）
            u.Text.lineSpacing = Mathf.Max(0f, (T.TalkLineHeight - fi.lineHeight / fi.pointSize) * 100f);
        }
        u.Text.textWrappingMode = TextWrappingModes.Normal;
        u.Text.overflowMode = TextOverflowModes.Ellipsis;
        u.Text.maxVisibleLines = T.BubbleMaxLines;
        u.textSize = Ui.Size(u.Text);

        // 名前タグ：左上、上端に半分かぶせて傾ける
        u.Tag = UiKit.Row(u.Root, T.S1, TextAnchor.MiddleCenter, T.S3);
        u.Tag.name = "NameTag";
        UiKit.Ignore(u.Tag);
        u.Tag.anchorMin = u.Tag.anchorMax = new Vector2(0f, 1f); u.Tag.pivot = new Vector2(0f, 0.5f);
        u.Tag.anchoredPosition = new Vector2(T.BubblePadX, 0f);
        u.Tag.localEulerAngles = new Vector3(0f, 0f, T.NameTagTilt);
        var tfit = u.Tag.gameObject.AddComponent<ContentSizeFitter>();
        tfit.horizontalFit = tfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var tle = Ui.Size(u.Tag, h: T.NameTagH); tle.minHeight = T.NameTagH;
        u.TagFace = UiKit.Blob(u.Tag, T.BlobPill, T.Go, T.NameTagH * 0.5f, "Face"); UiKit.Fill(u.TagFace.rectTransform); UiKit.Ignore(u.TagFace);
        u.TagIcon = UiKit.Icon(u.Tag, "danger", T.IconS, T.Panel);
        u.Name = UiKit.Text(u.Tag, "", TextStyle.Tag, T.Panel);
        u.Name.fontSize = T.TextHint;

        // ▼：下の中央で上下に揺れる
        u.Caret = UiKit.Shape(u.Root, T.Art(T.CaretSoft) ?? T.Theme.caretDown, T.Accent, 0f, "Caret");
        UiKit.Ignore(u.Caret);
        var cr = u.Caret.rectTransform;
        cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0f); cr.pivot = new Vector2(0.5f, 0f);
        cr.sizeDelta = new Vector2(T.CaretW, T.CaretH); cr.anchoredPosition = new Vector2(0f, -T.CaretH * 0.5f);

        UiTick.On(u.Root.gameObject, u.Tick);
        u.Set("Houston", "");
        return u;
    }

    // ------------------------------------------------------------------ 席タブ
    /// <summary>持っている席のタブ。押すと Select して onSelect(席) を呼ぶ。席が1つなら隠れる（HideWhenSingle）</summary>
    public static UiSeatTabs SeatTabs(Transform p, IList<string> seats, Action<string> onSelect)
    {
        var t = new UiSeatTabs { OnSelect = onSelect };
        t.Root = UiKit.Row(p, T.S2, TextAnchor.MiddleLeft);
        t.Root.name = "SeatTabs";
        t.SetSeats(seats);
        return t;
    }

    internal static void BuildTab(UiSeatTabs owner, string seat, out RectTransform root, out RectTransform body, out Image shadow, out Image frame, out Image face,
                                  out UiSeatBadge badge, out TextMeshProUGUI label, out RectTransform mark, out Image markIcon)
    {
        root = Ui.Rect("Tab_" + seat, owner.Root);
        HitArea(root);
        shadow = UiKit.Pill(root, T.Clear, T.TabH, "Shadow"); UiKit.MiddleBand(shadow.rectTransform, T.TabH);   // #190 影なし（欄は残す）
        body = Ui.Rect("Body", root); UiKit.MiddleBand(body, T.TabH);
        frame = UiKit.Pill(body, T.Text, T.TabH, "Frame"); UiKit.Fill(frame.rectTransform);
        face = UiKit.Pill(body, T.Panel, T.TabH - T.Border * 2f, "Face"); UiKit.Fill(face.rectTransform, T.Border);

        var row = UiKit.Row(body, T.S2, TextAnchor.MiddleLeft); UiKit.Fill(row);
        row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(Mathf.RoundToInt(T.S1), Mathf.RoundToInt(T.S3), 0, 0);
        badge = UiKit.SeatBadge(row, seat, T.BadgeS);
        label = UiKit.Text(row, Seats.Title(seat), TextStyle.Tag, T.Text);
        mark = Mark(body, out markIcon);

        var btn = AddButton(root.gameObject, face);
        string s = seat;
        btn.onClick.AddListener(() => owner.Select(s, true));
        root.gameObject.AddComponent<UiPress>().Init(body, btn);
        float w = T.S1 + T.BadgeS + T.S2 + label.GetPreferredValues(label.text).x + T.S3;
        UiKit.Fixed(root, w, T.Hit);
    }

    // ------------------------------------------------------------------ 同時押しの合図
    /// <param name="cmdIcon">そろえる操作のアイコン（点火なら "ignition"、フィルターなら "filter"）</param>
    /// <param name="seats">そろえる席（並べる順）</param>
    public static UiDualPress DualPress(Transform p, string cmdIcon, IList<string> seats)
    {
        var u = new UiDualPress();
        u.Root = UiKit.Panel(p, T.S2, "DualPress");
        var fit = u.Root.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var head = UiKit.Row(u.Root, T.S2, TextAnchor.MiddleCenter);
        u.CmdIcon = UiKit.Icon(head, cmdIcon, T.IconM, T.Text);
        u.Title = UiKit.Text(head, "", TextStyle.Sub, T.Text);
        float tw = 0f;   // 見出しが変わってもパネルの幅が動かないように、一番長い見出しの幅にそろえる
        foreach (var s in new[] { UiDualPress.TitleIdle, UiDualPress.TitleTogether, UiDualPress.TitleFailed }) tw = Mathf.Max(tw, u.Title.GetPreferredValues(s).x);
        UiKit.Fixed(u.Title, tw, T.TextSub * T.HeadingLineHeight);
        u.StateIcon = UiKit.Icon(head, null, T.IconM, T.Go);   // 消しても場所は空ける（見出しが左右に動かない）
        u.Row = UiKit.Row(u.Root, T.S4, TextAnchor.UpperCenter);
        u.SetSeats(seats);
        UiTick.On(u.Root.gameObject, u.Tick);
        return u;
    }

    internal static void BuildPressSlot(RectTransform row, string seat, out RectTransform root, out Image track, out Image ring, out UiSeatBadge badge, out Image check, out RectTransform you)
    {
        float d = T.BadgeL + T.RingW * 2f;
        root = UiKit.Col(row, T.S1, TextAnchor.UpperCenter);
        root.name = "Seat_" + seat;
        var box = Ui.Rect("Ring", root); UiKit.Fixed(box, d, d);
        track = Disc(box, T.Line, d, "Track");
        ring = Disc(box, T.Go, d, "Fill", true);
        badge = UiKit.SeatBadge(box, seat, T.BadgeL); UiKit.Center(badge.Root, T.BadgeL, T.BadgeL);
        check = UiKit.Icon(badge.Root, "check", T.IconL, T.OnSeat(seat)); UiKit.Center(check.rectTransform, T.IconL, T.IconL);
        check.enabled = false;
        UiKit.Text(root, Seats.Title(seat), TextStyle.Label, T.TextSoft);
        you = UiKit.Tag(root, "You", T.Text, T.Panel);
        you.gameObject.SetActive(false);
    }
}
