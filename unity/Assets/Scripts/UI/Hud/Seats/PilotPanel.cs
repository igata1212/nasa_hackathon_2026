// 書いた人：Claude ／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で CabinHud から分けた）／ 更新：iizuka（Opus 5.5、2026-11-14、#80）／ 更新：iizuka（Opus 5.5、2026-11-15、#189 左のカードを向きの見出しに）／ 更新：iizuka（Opus 5.5、2026-11-15、#190 あつ森に寄せた）
// 作る Issue：#80（Pilot のパネル）。下の PilotCommsKit は #82（Comms & Power）と共用の小さな部品
//
// Pilot（▲）の席：左から「燃料の数のチップ」「向きの見出し（Point ship at）」「船の向き（丸い枠 4〜5 つから1つ）」、盆の右端に「大ボタン1つ（軌道修正・点火・HELP SWAP のどれか）」。
//   ・#190（ui-guidelines 3章「グリッドの選択」【09】【10】）：向きは丸い枠（UiKit.Slot）。今の向きの右上にチェック、上に品名の札（何のための向きか）。
//     丸い枠の下に向きの名前（【09】の値段の位置）。手は今の向きに乗る。いま要る物があれば手はそこへ動いて突き、札はその「何をするか」
//   ・HELP SWAP：CO2 が注意の値を超え、フィルターが残っていて、自分が Life Support を持っていないときだけ（#81・#83 と同じ条件と文言）
//   ・押せない大ボタンは line の面＋鍵。2行目に「足りない物（担当の席の形つき）」か「あと何秒」。いま押せる大ボタンは主ボタン（go＋縞）で息をする
//   ・UI は判定しない：押せる・要るの見た目は state の値（t・t_burn_open・sw・attitude・correction_done・burn_done）を
//     SPEC 6章のとおりに並べただけ。効くかどうかはサーバーが決める
//   ・練習（M03）の間は state が無いので、全部押せる見た目にする。#84 は Highlight("set_attitude") で向きを「次に押す物」にする
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
    // 向き（server の ATTITUDES）：名前（丸い枠の下）・アイコン・何のための向きか（品名の札）。sun はソーラーパネルを積んだときだけ出す
    static readonly (string mode, string title, string icon, string note)[] Attitudes =
    {
        ("earth", "Earth", "earth", "Send data"),
        ("moon", "Moon", "moon", "Observe"),
        ("sun", "Sun", "sun", "Charge"),
        ("burn", "Burn", "rocket", "Fire engine"),
        ("heatshield", "Shield", "heat_shield", "Heat shield first"),
    };

    static class Words
    {
        // 向きの見出し（#189）。2行目は {0} = 向きを変える1回の燃料
        public const string Heading = "Point ship at";
        public const string FuelNote = "-{0} fuel per turn";
        public const string FuelUnit = "fuel";
        // いま要る向きの品名の札
        public const string TurnObserve = "Observe now!";
        public const string TurnSend = "Send data now!";
        public const string TurnBurn = "Engine burn now!";
        public const string TurnGetReady = "Next: engine burn";
        public const string TurnShield = "Re-entry now!";
        // 軌道修正（correction_burn）
        public const string Fix = "Fix course";
        public const string FixUnlocks = "Unlocks in {0}";
        public const string FixCost = "Uses {0} fuel";
        public const string FixLate = "Late: uses {0} fuel";
    }

    const string SolarPart = "solar_panel";                                   // parts.json の id
    static readonly string[] Instruments = { "camera", "spectrometer", "radar" };   // reference_sim の INSTRUMENTS

    // ------------------------------------------------------------------ 部品
    readonly Dictionary<string, UiSlot> att = new Dictionary<string, UiSlot>();
    readonly HashSet<string> highlighted = new HashSet<string>();
    SeatStrip strip;
    UiCounter fuel;
    TextMeshProUGUI heading, headNote;
    PcCard fix, ignite;
    PcHelp help;
    UiItemTag tag; RectTransform tagAt; string tagText;
    string practiceAtt;   // 練習の間（state が無い）に押した向き。見た目だけ
    bool built;

    protected override void BuildControls(RectTransform row)
    {
        string me = Seat;
        strip = SeatLook.Strip(row);

        fuel = UiKit.Counter(strip.Rect, "fuel", Words.FuelUnit);
        var head = UiKit.Col(strip.Rect, 0f, TextAnchor.MiddleLeft);
        heading = UiKit.Text(head, Words.Heading, TextStyle.RowName, T.Text, TextAlignmentOptions.MidlineLeft);
        SeatLook.Tight(heading);
        headNote = UiKit.Text(head, "", TextStyle.Label, T.TextSoft, TextAlignmentOptions.MidlineLeft);
        SeatLook.ReserveWidth(headNote, K.Words.PracticeTry, K.Words.PracticeDone, K.F(Words.FuelNote, 99));
        headNote.gameObject.SetActive(false);   // #210 見出しの2行目は出さない（1行にする）

        var grid = UiKit.Row(strip.Rect, T.S4 + T.S1, TextAnchor.MiddleCenter);
        grid.name = "Attitude";
        foreach (var a in Attitudes)
        {
            if (a.mode == "sun" && !Ctx.HasPart(SolarPart)) continue;
            string mode = a.mode;
            var col = UiKit.Col(grid, T.S1 / 2f, TextAnchor.UpperCenter);
            col.name = "Att_" + mode;
            var slot = UiKit.Slot(col, a.icon, () => TurnTo(mode));
            var name = UiKit.Text(col, a.title, TextStyle.Price, T.TextSoft, TextAlignmentOptions.Center);
            SeatLook.Tight(name);
            att[mode] = slot;
        }

        SeatLook.Spacer(strip.Rect);
        var slotR = K.Slot(strip.Rect);
        fix = K.BigButton(slotR, "FixCourse", "route", () => Ctx.Send(Cmd.Simple("correction_burn", me)));
        ignite = K.BigButton(slotR, "Ignite", "ignition", () => Ctx.SendDual("ignite", me));
        help = PcHelp.Build(slotR, Ctx, me);
        built = true;
    }

    protected override void BuildStir(RectTransform row) => K.StirNote(row);

    void TurnTo(string mode)
    {
        Ctx.Send(Cmd.Attitude(Seat, mode));
        if (Ctx.State == null) practiceAtt = mode;   // 練習の間は state が来ないので、押した向きを見た目だけ残す
        UiSelect.CursorOf(Root).Poke();
        Refresh(Ctx.State);
    }

    public override void OnEvent(GameEvent e) { if (built) help.OnEvent(e); }

    /// <summary>#84：練習の間「まだ試していない操作」を「次に押す物」にする（set_attitude → 向き、correction_burn・ignite → 大ボタン）</summary>
    public override void Highlight(string cmd, bool on)
    {
        if (on ? !highlighted.Add(cmd) : !highlighted.Remove(cmd)) return;   // #84 は毎フレーム呼ぶので、変わったときだけ描き直す
        Refresh(Ctx.State);
    }

    public override void Tick(float now)
    {
        if (!built) return;
        strip.Fit();
        PokeCursor(now);
        if (help.Tick(now)) Refresh(Ctx.State);
    }

    // ------------------------------------------------------------------ 描く
    public override void Refresh(GameState s)
    {
        base.Refresh(s);
        if (!built) return;
        string hint = null;
        string now = s != null ? NowTarget(s, out hint) : null;

        // 燃料（#210 見出しの2行目は出さない。headNote は BuildControls で隠してある）
        fuel.Set(s != null ? K.Whole(s.fuel) : K.Words.NoValue);

        // 向き：今の向きにチェック。札と手は「いま要る向き」があればそこ、無ければ今の向き
        string cur = s != null ? s.attitude : practiceAtt;
        foreach (var kv in att) UiSelect.Check(kv.Value.Root, kv.Key == cur);
        string tagMode = now != null && now.StartsWith("att:") ? now.Substring(4) : cur;
        bool attNow = tagMode != cur;
        if (tagMode == null && highlighted.Contains("set_attitude")) tagMode = FirstAttitude();
        ShowTag(tagMode, attNow ? hint : Note(tagMode));

        // 大ボタン：いま押せる物を優先して1つだけ（軌道修正・点火 ＞ HELP SWAP ＞ まだ押せない軌道修正・点火）
        string act = ActionToShow(s);
        fix.Show(act == "fix"); ignite.Show(act == "ignite"); help.Card.Show(act == "help");
        bool actNow;
        if (act == "fix") actNow = RefreshFix(s, now == "fix");
        else if (act == "help") actNow = help.Refresh(now == "help");
        else
        {
            K.RefreshIgnite(ignite, s, Ctx, Seat);
            actNow = now == "ignite" || highlighted.Contains("ignite");
            ignite.SetNow(actNow);
        }
        if (s == null && !actNow && highlighted.Contains("correction_burn")) actNow = true;

        // 手（1画面に1つ）：いま要る向き ＞ いま押す大ボタン ＞ 練習で試す向き ＞ 今の向き
        var actCard = act == "fix" ? fix : act == "help" ? help.Card : ignite;
        if (attNow && tagMode != null) Point(att[tagMode].Root, CursorDir.UpLeft, true);
        else if (actNow) Point(actCard.Root, CursorDir.UpLeft, true);
        else if (tagMode != null && att.ContainsKey(tagMode)) Point(att[tagMode].Root, CursorDir.UpLeft, highlighted.Contains("set_attitude"));
        else Point(null, CursorDir.UpLeft);
    }

    /// <summary>品名の札を mode の丸い枠の上に（null で隠す）。変わったときだけ出し直す（出すたびにぽんと弾むので）</summary>
    void ShowTag(string mode, string text)
    {
        RectTransform at = mode != null && att.TryGetValue(mode, out var sl) ? sl.Root : null;
        if (at == tagAt && text == tagText) return;
        if (tag != null && at != tagAt) tag.Hide();
        tagAt = at; tagText = text;
        tag = at != null && !string.IsNullOrEmpty(text) ? UiSelect.ItemTag(at, text) : null;
    }

    string FirstAttitude()
    {
        foreach (var a in Attitudes) if (att.ContainsKey(a.mode)) return a.mode;
        return null;
    }

    static string Note(string mode)
    {
        foreach (var a in Attitudes) if (a.mode == mode) return a.note;
        return null;
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

    /// <summary>軌道修正のボタンを描く。「いま押す物」なら true</summary>
    bool RefreshFix(GameState s, bool now)
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
        bool on = now || highlighted.Contains("correction_burn");
        fix.SetNow(on);
        return on;
    }

    /// <summary>いま Pilot が一番やるべき物（"att:moon"・"fix"・"ignite"）。無ければ null。hint は品名の札の文</summary>
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
// #190：大ボタン（PcCard）は盆の右端のピル（主ボタン＝go＋縞、押せない＝line＋鍵）。見た目は SeatPanel.cs の SeatLook／SeatFace
// ======================================================================

/// <summary>大ボタンの見た目の段階</summary>
public enum PcLook
{
    Normal,     // 副（panel の面＋bgDeep のふち。SEND DATA）
    Ready,      // 主ボタン（go＋縞。いま押せる）
    Waiting,    // line の面＋鍵（まだ押せない。2行目に理由）
    Done,       // 副の面＋チェック（済んだ）
    Missed,     // 副の面＋バツ（逃した）
}

/// <summary>大ボタン：ピル 96 の左にアイコン、右に「題（＋つまみ）」と「2行目（最大2行に折り返す）」。ボタン全体が押せる所</summary>
public class PcCard
{
    public RectTransform Root, Body;
    public Button Button;
    public SeatFace Face;
    public Image Icon;
    public TextMeshProUGUI Title, Note;
    public SeatKnob Knob;          // SEND DATA だけ
    internal string icon;
    PcLook look = (PcLook)(-1);
    bool now;

    public void Set(PcLook l, string title, string note) { SetLook(l); SetText(title, note); }

    public void SetText(string title, string note)
    {
        note = note ?? "";
        if (Title.text != title) Title.text = title;
        if (Note.text != note) Note.text = note;
        Note.gameObject.SetActive(note.Length > 0);
    }

    /// <summary>いま押す物：息をする（手は SeatPanel.Point が乗せる）</summary>
    public void SetNow(bool on)
    {
        if (on == now) return;
        now = on;
        SeatLook.Breathe(Root, Button, on);
    }

    public void Show(bool on) { if (Root.gameObject.activeSelf != on) Root.gameObject.SetActive(on); }

    public void SetLook(PcLook l)
    {
        if (l == look) return;
        look = l;
        var tone = l == PcLook.Ready ? SeatTone.Main : l == PcLook.Waiting ? SeatTone.Off : SeatTone.Plain;
        Face.Paint(tone);
        SeatFace.Ink(tone, out var fg, out var sub);
        Title.color = fg; Note.color = sub;
        string sprite = l == PcLook.Waiting ? "lock" : l == PcLook.Done ? "check" : l == PcLook.Missed ? "cross" : icon;
        Icon.sprite = sprite != null ? T.Icon(sprite) : null;
        Icon.color = l == PcLook.Done ? T.Go : l == PcLook.Missed ? T.Danger : fg;
        Icon.gameObject.SetActive(Icon.sprite != null);
        Button.interactable = l == PcLook.Normal || l == PcLook.Ready;
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
        h.Card = PilotCommsKit.BigButton(slot, "HelpSwap", "filter", () => ctx.SendDual("swap_filter", seat));
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

    /// <summary>描く。「いま押す物」なら true</summary>
    public bool Refresh(bool now)
    {
        Card.Set(PcLook.Ready, PilotCommsKit.Words.Help, feedback);   // #210 押す前は1行（結果の文は押した後だけ）
        bool on = now || partnerFirst;
        Card.SetNow(on);
        return on;
    }
}

public static class PilotCommsKit
{
    // ------------------------------------------------------------------ 大きさ（UiTokens・SeatLook から）
    public const float NoteSpacing = -20f;                 // 大ボタンの2行目の行間（TMP の em/100。2行をピル 96 に入れる）
    public const float FeedbackSec = 2.5f;                 // 同時押しの結果（waiting・swapped など）を出しておく秒（BurnCheck・LpKit と同じ）

    // ------------------------------------------------------------------ 文言（両方の席で使うもの）
    public static class Words
    {
        public const string Ignite = "Ignite";
        public const string OpensIn = "Opens in {0}";
        public const string PressWithComms = "Press with ■ Comms";
        public const string PressWithPilot = "Press with ▲ Pilot";
        public const string PressSolo = "Press once: fires both seats";
        public const string PracticeIgnite = "Practice: press once";
        public const string Needs = "Needs {0}";
        public const string Sep = " · ";
        public const string MissFix = "▲ Fix course";
        public const string MissFace = "▲ face Burn";
        public const string MissNav = "■ Nav on";
        public const string MissRouteB = "◆ route B";
        public const string MissFuel = "more fuel";
        public const string BurnDone = "Burn done";
        public const string NextShield = "Next: ▲ face Shield";
        public const string NextNav = "Next: keep ■ Nav on";
        public const string ReadyReentry = "Ready for re-entry";
        public const string BurnMissed = "Burn missed";
        public const string WindowClosed = "Window closed: long way home";
        public const string PracticeTry = "Practice: tap any button";
        public const string PracticeDone = "Practice done!";
        public const string NoValue = "--";              // 練習の間（state が無い）の数のチップ
        // CO2 フィルター交換の手伝い（Life Support と同時押し。文言は #83 の PlannerPanel と同じ）
        public const string Help = "Help swap", HelpNote = "CO2 filter, with ● Life Support";
        public const string PressWaiting = "waiting for partner", PressNow = "press now!";
        public const string PressTogether = "swapped!", PressFailed = "missed, try again";
        public const string StirTitle = "Stand by";
        public const string StirNote = "● Life Support is stirring the O2 tank";
    }

    // ------------------------------------------------------------------ 並べ方
    /// <summary>大ボタンの置き場（中の候補を1つだけ見せる）。盆の右端（SeatLook.Spacer の後ろ）に置く</summary>
    public static RectTransform Slot(RectTransform strip)
    {
        var r = Ui.Rect("Action", strip);
        UiKit.Fixed(r, SeatLook.ActionW, T.Hit);
        return r;
    }

    // ------------------------------------------------------------------ 大ボタン
    /// <param name="knob">ON/OFF のつまみを題の右に付ける（SEND DATA）</param>
    public static PcCard BigButton(RectTransform slot, string name, string icon, Action onClick, bool knob = false)
    {
        var c = new PcCard { icon = icon };
        c.Root = Ui.Rect(name, slot); UiKit.Fill(c.Root);
        var hit = c.Root.gameObject.AddComponent<Image>(); hit.color = T.Clear;   // 押せる所＝置き場いっぱい（96）
        c.Body = Ui.Rect("Body", c.Root); UiKit.MiddleBand(c.Body, SeatLook.MainH);
        c.Face = SeatFace.Pill(c.Body, SeatLook.MainH);

        var row = UiKit.Row(c.Body, T.S2, TextAnchor.MiddleLeft, T.S3, 0f); UiKit.Fill(row);
        c.Icon = UiKit.Icon(row, icon, T.IconM, T.Text);
        var col = UiKit.Col(row, 0f, TextAnchor.MiddleLeft, 0f, 0f, true);
        Ui.Size(col, flexW: 1f);
        var top = UiKit.Row(col, T.S1, TextAnchor.MiddleLeft);
        c.Title = UiKit.Text(top, "", TextStyle.Choice, T.Text, TextAlignmentOptions.MidlineLeft);
        SeatLook.Tight(c.Title);
        Ui.Size(c.Title, flexW: 1f);
        if (knob) c.Knob = SeatLook.Knob(top);
        c.Note = UiKit.Text(col, "", TextStyle.Label, T.TextSoft, TextAlignmentOptions.MidlineLeft, false);
        c.Note.textWrappingMode = TextWrappingModes.Normal;   // 2行まで折り返す（文言は短く）
        c.Note.lineSpacing = NoteSpacing;

        c.Button = SeatLook.Press(c.Root, c.Face.Face, onClick);
        c.SetLook(PcLook.Normal);
        return c;
    }

    /// <summary>stir（M04）の間の Pilot・Comms & Power：待つだけ、と1つで伝える（台に時計＋題＋文）</summary>
    public static void StirNote(RectTransform row)
    {
        var strip = SeatLook.Strip(row);
        var r = UiKit.Row(strip.Rect, SeatLook.Gap, TextAnchor.MiddleLeft);
        UiKit.Disc(r, "clock", T.Text, T.RowDiscD);
        var col = UiKit.Col(r, 0f, TextAnchor.MiddleLeft);
        SeatLook.Tight(UiKit.Text(col, Words.StirTitle, TextStyle.RowName, T.Text, TextAlignmentOptions.MidlineLeft));   // #210 2行目（StirNote）は出さない
        strip.Rect.gameObject.AddComponent<SeatStripFit>().strip = strip;   // stir の間は Tick で触る部品が無いので、自分で合わせる
    }

    /// <summary>練習（M03）の間の、見出しの2行目</summary>
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

    /// <summary>点火のボタンを描く。押せるのは噴射窓の中で足りない物が無いときだけ（練習の間は押せる）</summary>
    public static void RefreshIgnite(PcCard card, GameState s, HudContext ctx, string seat)
    {
        bool solo = ctx.SoloPair("ignite") != null;
        string press = solo ? Words.PressSolo : seat == Seats.Pilot ? Words.PressWithComms : Words.PressWithPilot;
        if (s == null) { card.Set(PcLook.Ready, Words.Ignite, null); return; }   // #210 練習の Ignite は2行目なし
        if (s.strictReason == "missed_burn") { card.Set(PcLook.Missed, Words.BurnMissed, Words.WindowClosed); return; }   // 窓を逃すと server がルート B で帰す（burn_done も true になる）
        if (s.burnDone)
        {
            card.Set(PcLook.Done, Words.BurnDone, null);   // #210 2行目（次の向き・Ready for re-entry）は出さない
            return;
        }
        if (s.tBurnClose > 0f && s.t >= s.tBurnClose) { card.Set(PcLook.Missed, Words.BurnMissed, Words.WindowClosed); return; }
        if (s.t < s.tBurnOpen) { card.Set(PcLook.Waiting, Words.Ignite, F(Words.OpensIn, Ui.Mmss(s.tBurnOpen - s.t))); return; }
        var miss = IgniteMissing(s);
        if (miss.Count > 0) card.Set(PcLook.Waiting, Words.Ignite, null);   // #210 足りない物の2行目は出さない
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
}

/// <summary>Tick を受けない帯（stir の間の待つだけの表示）を、毎フレーム画面に合わせる</summary>
public class SeatStripFit : MonoBehaviour
{
    internal SeatStrip strip;
    void LateUpdate() => strip?.Fit();
}
