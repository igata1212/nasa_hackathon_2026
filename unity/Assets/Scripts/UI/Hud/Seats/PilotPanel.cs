// 書いた人：Claude ／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で CabinHud から分けた）／ 更新：iizuka（Opus 5.5、2026-11-14、#80）／ 更新：iizuka（Opus 5.5、2026-11-15、#189 左のカードを向きの見出しに）
// 作る Issue：#80（Pilot のパネル）。下の PilotCommsKit は #82（Comms & Power）と共用の小さな部品
//
// Pilot（▲）の席：左から「向きの見出し（POINT SHIP AT ＋燃料。表示だけ）」「船の向き（5つから1つ）」「大ボタン1つ（軌道修正・点火・HELP SWAP のどれか）」。
//   ・#189（Fable）：左のカードは EARTH・MOON などが「船を向ける先」だと分かるように見出しにした。燃料と1回の量は2行目
//   ・HELP SWAP：CO2 が注意の値を超え、フィルターが残っていて、自分が Life Support を持っていないときだけ（#81・#83 と同じ条件と文言）
//   ・いま要る物を1つだけ橙の輪で光らせ、そのカードの2行目に「何をするか」を書く（NowTarget）
//   ・押せない大ボタンは灰色＋鍵。2行目に「足りない物（担当の席の形つき）」か「あと何秒」
//   ・UI は判定しない：押せる・要るの見た目は state の値（t・t_burn_open・sw・attitude・correction_done・burn_done）を
//     SPEC 6章のとおりに並べただけ。効くかどうかはサーバーが決める
//   ・練習（M03）の間は state が無いので、全部押せる見た目にする。#84 は Highlight("set_attitude") で向きの列を光らせる
// 文言は下の Words と Attitudes（Fable が直すのはここ）。数値は parameters.json（GameParams）と state から
using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;
using K = PilotCommsKit;

public class PilotPanel : SeatPanel
{
    public override string Seat => Seats.Pilot;
    public override IEnumerable<string> Commands => new[] { "set_attitude", "correction_burn", "ignite" };

    // ------------------------------------------------------------------ 文言
    // 向き（server の ATTITUDES）：名前・アイコン・何のための向きか。sun はソーラーパネルを積んだときだけ出す
    static readonly (string mode, string title, string icon, string note)[] Attitudes =
    {
        ("earth", "EARTH", "earth", "Send data"),
        ("moon", "MOON", "moon", "Observe"),
        ("sun", "SUN", "sun", "Charge"),
        ("burn", "BURN", "rocket", "Fire engine"),
        ("heatshield", "SHIELD", "heat_shield", "Heat shield first"),
    };

    static class Words
    {
        // 左のカード：向きの列の見出し（#189）。2行目は {0} = 燃料の残り、{1} = 向きを変える1回の燃料。
        //   <nobr> は「per turn」を2行に割らないため（表示は "Fuel 83, -1 per turn"。幅が足りなければ "Fuel 83, -1" / "per turn"）
        public const string Fuel = "POINT SHIP AT";
        public const string FuelPractice = "POINT SHIP AT";
        public const string FuelNote = "Fuel {0}, -{1} <nobr>per turn</nobr>";
        // いま要る向きのカードの2行目
        // （向きのカードは幅が約 200 しかないので短く。2行まで）
        public const string TurnObserve = "Observe now!";
        public const string TurnSend = "Send data now!";
        public const string TurnBurn = "Engine burn now!";
        public const string TurnGetReady = "Next: engine burn";
        public const string TurnShield = "Re-entry now!";
        // 軌道修正（correction_burn）
        public const string Fix = "FIX COURSE";
        public const string FixUnlocks = "Unlocks in {0}";
        public const string FixCost = "Uses {0} fuel";
        public const string FixLate = "Late: uses {0} fuel";
    }

    const string SolarPart = "solar_panel";                                   // parts.json の id
    static readonly string[] Instruments = { "camera", "spectrometer", "radar" };   // reference_sim の INSTRUMENTS

    // ------------------------------------------------------------------ 部品
    readonly Dictionary<string, PcCard> att = new Dictionary<string, PcCard>();
    readonly HashSet<string> highlighted = new HashSet<string>();
    PcCard fuel, fix, ignite;
    PcHelp help;
    Image dialGlow;
    bool built;

    protected override void BuildControls(RectTransform row)
    {
        RootAsDetail();   // 照明 OFF で暗くしてよい（#87 が量を決める）
        string me = Seat;
        var line = K.Line(row);

        fuel = K.Card(line, "Heading", null, null);   // 向きの見出し（表示だけ。アイコンなし）。2行目に燃料
        // 幅：InfoW（176）では「POINT SHIP AT」（Baloo 24 で約 157）が内側 16×2 に入らないので、見出しの幅に合わせて広げる（約 189）
        float headW = Mathf.Max(fuel.Title.GetPreferredValues(Words.Fuel).x, fuel.Title.GetPreferredValues(Words.FuelPractice).x);
        K.Width(fuel.Root, Mathf.Max(K.InfoW, Mathf.Ceil(headW) + K.PadX * 2f), 0f);

        var dial = UiKit.Row(line, K.Gap, TextAnchor.MiddleLeft);
        dial.name = "Attitude";
        dial.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
        K.Width(dial, T.Hit, 1f);
        dialGlow = K.GroupGlow(dial);
        foreach (var a in Attitudes)
        {
            if (a.mode == "sun" && !Ctx.HasPart(SolarPart)) continue;
            string mode = a.mode;
            var c = K.Card(dial, "Att_" + mode, a.icon, () => Ctx.Send(Cmd.Attitude(me, mode)));
            K.Width(c.Root, T.Hit, 1f);
            att[mode] = c;
        }

        var slot = K.Slot(line);
        fix = K.Card(slot, "FixCourse", "route", () => Ctx.Send(Cmd.Simple("correction_burn", me)), big: true);
        ignite = K.Card(slot, "Ignite", "ignition", () => Ctx.SendDual("ignite", me), big: true);
        UiKit.Fill(fix.Root); UiKit.Fill(ignite.Root);
        help = PcHelp.Build(slot, Ctx, me);
        built = true;
    }

    protected override void BuildStir(RectTransform row) => K.StirNote(row);

    public override void OnEvent(GameEvent e) { if (built) help.OnEvent(e); }

    /// <summary>#84：練習の間「まだ試していない操作」を橙の輪で光らせる（set_attitude → 向きの列、correction_burn・ignite → 大ボタン）</summary>
    public override void Highlight(string cmd, bool on)
    {
        if (on ? !highlighted.Add(cmd) : !highlighted.Remove(cmd)) return;   // #84 は毎フレーム呼ぶので、変わったときだけ描き直す
        Refresh(Ctx.State);
    }

    public override void Tick(float now)
    {
        if (!built) return;
        K.Pulse(dialGlow, now);
        foreach (var c in att.Values) K.Pulse(c.Glow, now);
        K.Pulse(fix.Glow, now); K.Pulse(ignite.Glow, now); K.Pulse(help.Card.Glow, now);
        if (help.Tick(now)) Refresh(Ctx.State);
    }

    // ------------------------------------------------------------------ 描く
    public override void Refresh(GameState s)
    {
        base.Refresh(s);
        if (!built) return;
        string hint = null;
        string now = s != null ? NowTarget(s, out hint) : null;

        // 向きの見出し＋燃料（練習の間は state が無いので、2行目は練習の案内）
        if (s == null) fuel.Set(PcLook.Normal, Words.FuelPractice, K.PracticeNote(Ctx, Seat));
        else fuel.Set(PcLook.Normal, Words.Fuel, K.F(Words.FuelNote, K.Whole(s.fuel), K.P("fuel_attitude_change")));

        // 向き：いまの向きは濃紺、いま要る向きは橙の輪＋2行目が「何をするか」
        dialGlow.enabled = highlighted.Contains("set_attitude");
        foreach (var a in Attitudes)
        {
            if (!att.TryGetValue(a.mode, out var c)) continue;
            bool want = now == "att:" + a.mode;
            c.Set(s != null && s.attitude == a.mode ? PcLook.Selected : PcLook.Normal, a.title, want ? hint : a.note);
            c.SetGlow(want);
        }

        // 大ボタン：いま押せる物を優先して1つだけ（軌道修正・点火 ＞ HELP SWAP ＞ まだ押せない軌道修正・点火）
        string act = ActionToShow(s);
        fix.Show(act == "fix"); ignite.Show(act == "ignite"); help.Card.Show(act == "help");
        if (act == "fix") RefreshFix(s, now == "fix");
        else if (act == "help") help.Refresh(now == "help");
        else
        {
            K.RefreshIgnite(ignite, s, Ctx, Seat);
            ignite.SetGlow(now == "ignite" || highlighted.Contains("ignite"));
        }
    }

    bool NeedFix(GameState s) => s != null && !s.correctionDone && !s.burnDone;
    bool FixReady(GameState s) => NeedFix(s) && s.t >= GameParams.Get("m06_delay_s") && s.Switch("nav");

    /// <summary>大ボタンの置き場に出す物："fix"・"ignite"・"help"</summary>
    string ActionToShow(GameState s)
    {
        bool window = K.WindowOpen(s);
        if (NeedFix(s) && (FixReady(s) || window)) return "fix";   // 軌道修正が済まないと点火できない
        if (window) return "ignite";
        if (K.HelpShown(s, Ctx)) return "help";
        return NeedFix(s) ? "fix" : "ignite";
    }

    void RefreshFix(GameState s, bool glow)
    {
        float unlock = GameParams.Get("m06_delay_s");   // M07 で解禁（server は t < m06_delay_s を locked で返す）
        if (s.t < unlock) fix.Set(PcLook.Waiting, Words.Fix, K.F(Words.FixUnlocks, Ui.Mmss(unlock - s.t)));
        else if (!s.Switch("nav")) fix.Set(PcLook.Waiting, Words.Fix, K.F(K.Words.Needs, K.Words.MissNav));
        else
        {
            bool late = s.t >= s.tLos;   // 月の裏側に入ってからは fuel_burn_retry も引かれる（SPEC 6章）
            float cost = GameParams.Get("fuel_correction_burn") + (late ? GameParams.Get("fuel_burn_retry") : 0f);
            fix.Set(PcLook.Ready, Words.Fix, K.F(late ? Words.FixLate : Words.FixCost, K.Num(cost)));
        }
        fix.SetGlow(glow || highlighted.Contains("correction_burn"));
    }

    /// <summary>いま Pilot が一番やるべき物（"att:moon"・"fix"・"ignite"）。無ければ null。hint は向きのカードの2行目</summary>
    string NowTarget(GameState s, out string hint)
    {
        hint = null;
        bool needFix = NeedFix(s);
        if (FixReady(s)) return "fix";
        if (K.WindowOpen(s))
        {
            if (s.attitude != "burn") { hint = Words.TurnBurn; return "att:burn"; }
            return K.IgniteReady(s) ? "ignite" : null;
        }
        if (K.HelpShown(s, Ctx)) return "help";
        if (s.burnDone)
        {
            if (s.attitude == "heatshield") return null;   // 熱シールドを向いたら、もう離れさせない（再突入の準備チェックは t_reentry + reentry_ready_s）
            string want = K.OnBoard(s) > 0 && s.hasSignal ? "earth" : "heatshield";   // 送れるうちは送り、そのあと熱シールド
            if (s.attitude == want) return null;
            hint = want == "earth" ? Words.TurnSend : Words.TurnShield;
            return "att:" + want;
        }
        if (s.inFarside && CarriesInstrument() && s.attitude != "moon") { hint = Words.TurnObserve; return "att:moon"; }
        if (s.t >= s.tAos && s.t < s.tBurnOpen && !needFix)   // 通信が戻ってから噴射窓が開くまで
        {
            if (K.OnBoard(s) > 0 && s.hasSignal) { if (s.attitude == "earth") return null; hint = Words.TurnSend; return "att:earth"; }
            if (s.attitude != "burn") { hint = Words.TurnGetReady; return "att:burn"; }
        }
        return null;
    }

    bool CarriesInstrument()
    {
        foreach (var n in Instruments) if (Ctx.HasPart(n)) return true;
        return false;
    }
}

// ======================================================================
// PilotCommsKit：Pilot（#80）と Comms & Power（#82）の席のパネルが使う小さな部品と、点火のボタンの中身。
// #71 の UiKitHud（Toggle・CountdownChip・HoldButton など）ができたら、ここを差し替える。パネルの側は PcCard の口だけを使う。
// 色・大きさは UiTokens から組む。下の定数は UiTokens に移す候補（第2波は UiTokens を触らないので、ここに置いた）
// ======================================================================

/// <summary>カードの見た目の段階</summary>
public enum PcLook
{
    Normal,     // panel の面（ふつうに押せる・表示だけ）
    Selected,   // ink の面（選んでいる向き）
    Ready,      // go の面（いま押せる大ボタン）
    Waiting,    // line の面＋鍵（まだ押せない。2行目に理由）
    Done,       // panel の面＋チェック（済んだ）
    Missed,     // panel の面＋バツ（逃した）
}

/// <summary>ON/OFF のつまみ（ピルの溝の中を丸が動く。ON は右で go、OFF は左で line。文字も出すので色だけに頼らない）</summary>
public class PcKnob
{
    public RectTransform Root, Dot;
    public Image Track;
    public TextMeshProUGUI Label;

    public void Set(bool on)
    {
        Track.color = on ? T.Go : T.Line;
        Label.text = on ? PilotCommsKit.Words.On : PilotCommsKit.Words.Off;
        Label.color = on ? T.Panel : T.Ink;
        float x = on ? 1f : 0f, pad = PilotCommsKit.KnobPad, d = PilotCommsKit.KnobH;
        Dot.anchorMin = Dot.anchorMax = Dot.pivot = new Vector2(x, 0.5f);
        Dot.anchoredPosition = new Vector2(on ? -pad : pad, 0f);
        var lr = Label.rectTransform;   // 文字は丸の反対側
        lr.offsetMin = new Vector2(on ? pad : d, 0f);
        lr.offsetMax = new Vector2(on ? -d : -pad, 0f);
    }
}

/// <summary>角丸のカード：左にアイコン、右に「題（＋つまみ）」と「2行目（最大2行に折り返す）」。押せるカードはカード全体が押せる所</summary>
public class PcCard
{
    public RectTransform Root, Body;
    public Button Button;        // null なら表示だけ
    public Image Glow, Face, Icon;   // Icon は null のことがある
    public TextMeshProUGUI Title, Note;
    public PcKnob Knob;          // スイッチだけ
    internal string icon;
    PcLook look = (PcLook)(-1);

    public void Set(PcLook l, string title, string note) { SetLook(l); SetText(title, note); }

    public void SetText(string title, string note)
    {
        note = note ?? "";
        if (Title.text != title) Title.text = title;
        if (Note.text != note) Note.text = note;
        Note.gameObject.SetActive(note.Length > 0);
    }

    /// <summary>橙の輪（いま要る物・練習で光らせる物）。明るさは Tick の Pulse で揺らす</summary>
    public void SetGlow(bool on) { if (Glow != null) Glow.enabled = on; }

    public void Show(bool on) { if (Root.gameObject.activeSelf != on) Root.gameObject.SetActive(on); }

    public void SetLook(PcLook l)
    {
        if (l == look) return;
        look = l;
        Color face = T.Panel, fg = T.Ink, sub = T.InkSub, ic = T.Ink;
        string sprite = icon; bool can = true;
        switch (l)
        {
            case PcLook.Selected: face = T.Ink; fg = sub = ic = T.Panel; break;
            case PcLook.Ready: face = T.Go; fg = sub = ic = T.Panel; break;
            case PcLook.Waiting: face = T.Line; fg = T.InkSub; sub = T.Ink; ic = T.InkSub; sprite = "lock"; can = false; break;
            case PcLook.Done: ic = T.Go; sprite = "check"; can = false; break;
            case PcLook.Missed: ic = T.Danger; sprite = "cross"; can = false; break;
        }
        Face.color = face; Title.color = fg; Note.color = sub;
        if (Icon != null)
        {
            Icon.sprite = sprite != null ? T.Icon(sprite) : null; Icon.color = ic;
            Icon.gameObject.SetActive(Icon.sprite != null);
        }
        if (Button != null) Button.interactable = can;
    }
}

/// <summary>HELP SWAP のボタン（Life Support と同時押しの CO2 フィルター交換）。press イベントの結果を2行目に少しの間出す</summary>
public class PcHelp
{
    public PcCard Card;
    string seat, feedback;
    float until;
    bool partnerFirst;   // Life Support が先に押して待っている（いま押せば成立する）

    public static PcHelp Build(RectTransform slot, HudContext ctx, string seat)
    {
        var h = new PcHelp { seat = seat };
        h.Card = PilotCommsKit.Card(slot, "HelpSwap", "filter", () => ctx.SendDual("swap_filter", seat), big: true);
        UiKit.Fill(h.Card.Root);
        return h;
    }

    public void OnEvent(GameEvent e)
    {
        if (e.kind != "press" || e.cmd != "swap_filter" || !Card.Root.gameObject.activeSelf) return;
        var t = PilotCommsKit.PressNote(e, seat);
        if (t == null) return;
        feedback = t;
        partnerFirst = e.status == "waiting" && !e.seats.Contains(seat);
        until = Time.unscaledTime + PilotCommsKit.FeedbackSec;
        Refresh(false);
    }

    /// <summary>結果の文言を消す時刻になったら true（パネルが描き直す）</summary>
    public bool Tick(float now)
    {
        if (feedback == null || now <= until) return false;
        feedback = null; partnerFirst = false;
        return true;
    }

    public void Refresh(bool glow)
    {
        Card.Set(PcLook.Ready, PilotCommsKit.Words.Help, feedback ?? PilotCommsKit.Words.HelpNote);
        Card.SetGlow(glow || partnerFirst);
    }
}

public static class PilotCommsKit
{
    // ------------------------------------------------------------------ 大きさ（UiTokens に移す候補）
    public const float Gap = T.S1;                         // カードどうしの間
    public const float InfoW = T.PrimaryMinW / 2f + T.S2;  // 燃料・電力のカードの幅（176）
    public const float ActionW = T.PrimaryMinW + T.S3;     // 大ボタンの幅（344）
    public const float PadX = T.S2, PadY = T.S1 / 2f;      // カードの内側（下の段は高さが約 100 しかないので上下は詰める。大ボタンは上下 0）
    public const float KnobW = T.Hit - T.S3, KnobH = T.S4; // つまみ（72×32。文字は Nunito 22）
    public const float KnobPad = T.S1 / 2f;                // つまみの丸と溝のすき間
    public const float GlowW = T.SelfRing;                 // 光らせる輪の太さ
    public const float GlowMin = 0.35f, GlowHz = 1.2f;     // 輪の不透明度の下限と、1秒に何回明滅するか
    public const float FeedbackSec = 2.5f;                 // 同時押しの結果（waiting・swapped など）を出しておく秒（BurnCheck・LpKit と同じ）

    // ------------------------------------------------------------------ 文言（両方の席で使うもの）
    public static class Words
    {
        public const string On = "ON", Off = "OFF";
        public const string Ignite = "IGNITE";
        public const string OpensIn = "Opens in {0}";
        public const string PressWithComms = "Press with ■ Comms";
        public const string PressWithPilot = "Press with ▲ Pilot";
        public const string PressSolo = "Press once: fires both seats";
        public const string PracticeIgnite = "Practice: press once";
        public const string Needs = "Needs {0}";
        public const string Sep = " · ";
        public const string MissFix = "▲ fix course";
        public const string MissFace = "▲ face BURN";
        public const string MissNav = "■ NAV ON";
        public const string MissRouteB = "◆ route B";
        public const string MissFuel = "more fuel";
        public const string BurnDone = "BURN DONE";
        public const string NextShield = "Next: ▲ face SHIELD";
        public const string NextNav = "Next: keep ■ NAV ON";
        public const string ReadyReentry = "Ready for re-entry";
        public const string BurnMissed = "BURN MISSED";
        public const string WindowClosed = "Window closed: long way home";
        public const string PracticeTry = "Practice: tap any button";
        public const string PracticeDone = "Practice done!";
        // CO2 フィルター交換の手伝い（Life Support と同時押し。文言は #83 の PlannerPanel と同じ）
        public const string Help = "HELP SWAP", HelpNote = "CO2 filter, with ● Life Support";
        public const string PressWaiting = "waiting for partner", PressNow = "press now!";
        public const string PressTogether = "swapped!", PressFailed = "missed, try again";
        public const string StirTitle = "STAND BY";
        public const string StirNote = "● Life Support is stirring the O2 tank";
    }

    // ------------------------------------------------------------------ 並べ方
    /// <summary>席のパネルの1段（SeatPanel の row の中に1つ）。子は高さいっぱい、幅は各自の Width</summary>
    public static RectTransform Line(RectTransform row)
    {
        var r = UiKit.Row(row, Gap, TextAnchor.MiddleLeft);
        r.name = "Line";
        r.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
        Ui.Size(r, flexW: 1f, flexH: 1f);
        return r;
    }

    /// <summary>大ボタンの置き場（中の候補を1つだけ見せる）</summary>
    public static RectTransform Slot(RectTransform line)
    {
        var r = Ui.Rect("Action", line);
        Width(r, ActionW, 0f);
        return r;
    }

    public static void Width(Component c, float w, float flex)
    {
        var le = Ui.Size(c);
        le.minWidth = w; le.preferredWidth = w; le.flexibleWidth = flex;
    }

    /// <summary>列全体を囲む橙の輪（練習の案内 #84 が「この列を試して」と光らせる）。列の後ろに敷く</summary>
    public static Image GroupGlow(RectTransform group)
    {
        var g = UiKit.Round(group, T.RadiusM + GlowW, T.Accent, "Glow");
        UiKit.Fill(g.rectTransform, -GlowW); UiKit.Ignore(g);
        g.transform.SetAsFirstSibling();
        g.enabled = false;
        return g;
    }

    // ------------------------------------------------------------------ カード
    /// <param name="icon">null ならアイコンなし（表示だけの狭いカード）</param>
    /// <param name="onClick">null なら表示だけのカード</param>
    /// <param name="knob">ON/OFF のつまみを題の右に付ける</param>
    /// <param name="big">大ボタン（角丸 40、題は Baloo 32）</param>
    public static PcCard Card(Transform p, string name, string icon, Action onClick, bool knob = false, bool big = false)
    {
        var c = new PcCard { icon = icon };
        float radius = big ? T.RadiusL : T.RadiusM;
        c.Root = Ui.Rect(name, p);
        if (onClick != null) c.Root.gameObject.AddComponent<Image>().color = T.Clear;   // 押せる所＝カード全体

        c.Glow = UiKit.Round(c.Root, radius + GlowW, T.Accent, "Glow"); UiKit.Fill(c.Glow.rectTransform, -GlowW);
        c.Glow.enabled = false;
        var sh = UiKit.Round(c.Root, radius, T.Shadow, "Shadow"); UiKit.Fill(sh.rectTransform);
        sh.rectTransform.offsetMin = sh.rectTransform.offsetMax = new Vector2(0f, -T.ShadowY);

        c.Body = Ui.Rect("Body", c.Root); UiKit.Fill(c.Body);
        c.Face = UiKit.Round(c.Body, radius, T.Panel, "Face"); UiKit.Fill(c.Face.rectTransform); UiKit.Ignore(c.Face);
        var row = c.Body.gameObject.AddComponent<HorizontalLayoutGroup>();
        int px = Mathf.RoundToInt(PadX), py = big ? 0 : Mathf.RoundToInt(PadY);
        row.padding = new RectOffset(px, px, py, py);
        row.spacing = T.S1; row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;

        if (icon != null) c.Icon = UiKit.Icon(c.Body, icon, T.IconM, T.Ink);
        var col = UiKit.Col(c.Body, 0f, TextAnchor.MiddleLeft, 0f, 0f, true);
        Ui.Size(col, flexW: 1f, flexH: 1f);
        var top = UiKit.Row(col, T.S1, TextAnchor.MiddleLeft);
        c.Title = UiKit.Text(top, "", big ? TextStyle.Sub : TextStyle.Tag, T.Ink, TextAlignmentOptions.MidlineLeft);
        Ui.Size(c.Title, flexW: 1f);
        if (knob) c.Knob = Knob(top);
        c.Note = UiKit.Text(col, "", TextStyle.Label, T.InkSub, TextAlignmentOptions.MidlineLeft);
        c.Note.textWrappingMode = TextWrappingModes.Normal;   // 2行まで折り返す（文言は短く）

        if (onClick != null)
        {
            c.Button = c.Root.gameObject.AddComponent<Button>();
            c.Button.targetGraphic = c.Face;
            c.Button.navigation = new Navigation { mode = Navigation.Mode.None };
            var cb = c.Button.colors;
            cb.normalColor = cb.selectedColor = cb.disabledColor = Color.white;
            cb.highlightedColor = T.TintHover; cb.pressedColor = T.TintPressed; cb.fadeDuration = T.TintFade;
            c.Button.colors = cb;
            c.Button.onClick.AddListener(() => onClick());
            c.Root.gameObject.AddComponent<UiPress>().Init(c.Body, c.Button);
        }
        c.SetLook(PcLook.Normal);
        return c;
    }

    static PcKnob Knob(Transform p)
    {
        var k = new PcKnob();
        k.Root = Ui.Rect("Knob", p);
        UiKit.Fixed(k.Root, KnobW, KnobH);
        k.Track = UiKit.Pill(k.Root, T.Line, KnobH, "Track"); UiKit.Fill(k.Track.rectTransform);
        var dot = UiKit.Pill(k.Root, T.Panel, KnobH - KnobPad * 2f, "Dot");
        k.Dot = dot.rectTransform; k.Dot.sizeDelta = Vector2.one * (KnobH - KnobPad * 2f);
        k.Label = UiKit.Text(k.Root, Words.Off, TextStyle.Label, T.Ink, TextAlignmentOptions.Center);
        var lr = k.Label.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        k.Set(false);
        return k;
    }

    /// <summary>stir（M04）の間の Pilot・Comms & Power：待つだけ、と1枚で伝える</summary>
    public static void StirNote(RectTransform row)
    {
        var line = Line(row);
        var c = Card(line, "StandBy", "clock", null);
        Width(c.Root, ActionW * 2f, 0f);
        c.Set(PcLook.Normal, Words.StirTitle, Words.StirNote);
    }

    /// <summary>練習（M03）の間の、燃料・電力のカードの2行目</summary>
    public static string PracticeNote(HudContext ctx, string seat) => ctx.Model.Practiced.Contains(seat) ? Words.PracticeDone : Words.PracticeTry;

    // ------------------------------------------------------------------ 点火（Pilot と Comms & Power で同じ）
    public static bool WindowOpen(GameState s) =>
        s != null && !s.burnDone && s.tBurnClose > 0f && s.t >= s.tBurnOpen && s.t < s.tBurnClose;

    /// <summary>点火に足りない物（担当の席の形つき）。SPEC 6章の ignite の条件を state で並べただけ</summary>
    public static List<string> IgniteMissing(GameState s)
    {
        var miss = new List<string>();
        if (!s.correctionDone) miss.Add(Words.MissFix);
        if (s.attitude != "burn") miss.Add(Words.MissFace);
        if (!s.Switch("nav")) miss.Add(Words.MissNav);
        float needA = GameParams.Get("fuel_burn_a"), needB = GameParams.Get("fuel_burn_b");
        if (s.route == "a" && s.fuel < needA) miss.Add(s.fuel >= needB ? Words.MissRouteB : Words.MissFuel);
        else if (s.route != "a" && s.fuel < needB) miss.Add(Words.MissFuel);   // route が null なら server が A か B を選ぶ
        return miss;
    }

    public static bool IgniteReady(GameState s) => WindowOpen(s) && IgniteMissing(s).Count == 0;

    /// <summary>点火のカードを描く。押せるのは噴射窓の中で足りない物が無いときだけ（練習の間は押せる）</summary>
    public static void RefreshIgnite(PcCard card, GameState s, HudContext ctx, string seat)
    {
        bool solo = ctx.SoloPair("ignite") != null;
        string press = solo ? Words.PressSolo : seat == Seats.Pilot ? Words.PressWithComms : Words.PressWithPilot;
        if (s == null) { card.Set(PcLook.Ready, Words.Ignite, Words.PracticeIgnite); return; }
        if (s.strictReason == "missed_burn") { card.Set(PcLook.Missed, Words.BurnMissed, Words.WindowClosed); return; }   // 窓を逃すと server がルート B で帰す（burn_done も true になる）
        if (s.burnDone)
        {
            string next = seat == Seats.Pilot
                ? (s.attitude == "heatshield" ? Words.ReadyReentry : Words.NextShield)
                : (s.Switch("nav") ? Words.ReadyReentry : Words.NextNav);
            card.Set(PcLook.Done, Words.BurnDone, next);
            return;
        }
        if (s.tBurnClose > 0f && s.t >= s.tBurnClose) { card.Set(PcLook.Missed, Words.BurnMissed, Words.WindowClosed); return; }
        if (s.t < s.tBurnOpen) { card.Set(PcLook.Waiting, Words.Ignite, F(Words.OpensIn, Ui.Mmss(s.tBurnOpen - s.t))); return; }
        var miss = IgniteMissing(s);
        if (miss.Count > 0) card.Set(PcLook.Waiting, Words.Ignite, F(Words.Needs, string.Join(Words.Sep, miss)));
        else card.Set(PcLook.Ready, Words.Ignite, press);
    }

    // ------------------------------------------------------------------ 手伝い：CO2 フィルター交換（#81・#83 の HELP SWAP と同じ条件）
    /// <summary>HELP SWAP を出すか：CO2 が注意の値以上、フィルターが残っている、自分が Life Support を持っていない（持っていれば Life Support の席で1人で押せる）</summary>
    public static bool HelpShown(GameState s, HudContext ctx) =>
        s != null && s.filters > 0 && s.co2 >= Co2Warn(s) && !ctx.Model.MySeats.Contains(Seats.Life);

    static float Co2Warn(GameState s) => s.Threshold("co2", "warn", out var v) ? v : GameParams.Get("co2_warn");

    /// <summary>press イベント（swap_filter）の結果の文言。自分が押して待っているか、相手が先に押したか</summary>
    public static string PressNote(GameEvent e, string seat)
    {
        switch (e.status)
        {
            case "waiting": return e.seats.Contains(seat) ? Words.PressWaiting : Words.PressNow;
            case "together": return Words.PressTogether;
            case "failed": return Words.PressFailed;
        }
        return null;
    }

    // ------------------------------------------------------------------ 値
    /// <summary>船にあって、まだ送っていないデータ（data_collected − data_sent、四捨五入）</summary>
    public static int OnBoard(GameState s) => s == null ? 0 : Mathf.Max(0, Mathf.RoundToInt(s.dataCollected - s.dataSent));

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public static string Num(float v) => v.ToString("0.#", Inv);
    /// <summary>電力・燃料の残り（左のゲージ StatusPanel と同じく整数）</summary>
    public static string Whole(float v) => v.ToString("0", Inv);
    /// <summary>parameters.json の値を文字に。無ければ "?"</summary>
    public static string P(string key) => GameParams.TryGet(key, out var v) ? Num(v) : "?";
    public static string F(string fmt, params object[] a) => string.Format(Inv, fmt, a);

    /// <summary>輪の明るさを揺らす（Tick から毎フレーム）</summary>
    public static void Pulse(Image ring, float now)
    {
        if (ring == null || !ring.enabled) return;
        float k = 0.5f + 0.5f * Mathf.Sin(now * Mathf.PI * 2f * GlowHz);
        var c = T.Accent; c.a = Mathf.Lerp(GlowMin, 1f, k);
        ring.color = c;
    }
}
