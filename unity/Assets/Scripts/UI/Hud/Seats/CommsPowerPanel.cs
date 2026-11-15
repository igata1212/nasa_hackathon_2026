// 書いた人：Claude ／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で CabinHud から分けた）／ 更新：iizuka（Opus 5.5、2026-11-14、#82）／ 更新：iizuka（Opus 5.5、2026-11-15、#190 あつ森に寄せた）
// 作る Issue：#82（Comms & Power のパネル）。部品は PilotPanel.cs の下の PilotCommsKit（#80 と共用）と SeatPanel.cs の SeatLook
//
// Comms & Power（■）の席：左から「電力の数のチップ」「船のスイッチ4つ（材料の行につまみ、2×2。1枚の電源盤、#102）」、盆の右端に「大ボタン1つ（データ送信・点火・HELP SWAP のどれか）」。
//   ・#190（ui-guidelines 3章「スイッチ＝材料の行にトグル」【11】）：行＝帯＋色つきの台＋名前＋2行目＋つまみ。行のどこを押してもよい
//   ・スイッチの行は2行目に「切ると何が起きるか」を1行で（電力がいくら浮くか＋困ること）。切ってあるときは「いま起きていること」
//     NAV は切ってあるとき「入れ直すと電力が減る」を先に見せる（power_nav_restart）
//   ・数のチップ（【09】）：電力の残り。右下の札に毎分いくら使っているか
//   ・大ボタン：噴射窓の中だけ Ignite（Pilot と同時押し）。CO2 が注意の値を超えたら Help swap（Life Support と同時押し。自分が Life Support を
//     持っていないときだけ、#81・#83 と同じ条件）。それ以外は Send data（送信の ON/OFF。送れないときは足りない物を出す）
//   ・いま要る物には指さしの手が乗って突き、その行の2行目に「何をするか」を書く（NowTarget）。無いときは手は大ボタンで休む
//   ・UI は判定しない：見た目は state の値（sw・transmit・has_signal・attitude・data_*・thresholds）を SPEC 5・6章のとおりに並べただけ
//   ・照明 OFF で暗くしてよいのは大ボタンの置き場だけ（電源盤は暗くしない。Lights を入れ直せるように）
//   ・練習（M03）の間は state が無いので、スイッチは事故直後の値（全部 ON）で見せる。#84 は Highlight("switch") で電源盤を「次に押す物」にする
// 文言は下の Switches と Words（Fable が直すのはここ）。数値は parameters.json（GameParams）と state から
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;
using K = PilotCommsKit;

public class CommsPowerPanel : SeatPanel
{
    public override string Seat => Seats.Comms;
    public override IEnumerable<string> Commands => new[] { "switch", "set_transmit", "ignite" };

    // ------------------------------------------------------------------ 文言
    // 船のスイッチ（server/game/seats.py の SHIP_SWITCHES）：名前・アイコン・ON のときの2行目（{0}＝毎分の電力）・OFF のときの2行目（{1}＝入れ直しの電力）
    // LIGHTS のアイコンは電球（Ionicons の bulb）がまだ無いので sun で代用（#71 で足したら替える）。名前は練習の案内（PracticeGuide）の「Turn Nav on」と同じ書き方
    static readonly (string name, string title, string icon, string onNote, string offNote)[] Switches =
    {
        ("heater", "Heater", "temperature", "Off: saves {0}/min, gets cold", "Cabin is cooling down"),
        ("lights", "Lights", "sun", "Off: saves {0}/min, dark", "Screen is dark"),
        ("nav", "Nav", "attitude", "Off: saves {0}/min, no burns", "No burns. ON again costs {1}"),
        ("comms", "Radio", "comms", "Off: saves {0}/min, no Houston", "No Houston, no data sent"),
    };
    // スイッチの台の色（隣どうしを同じ色にしない。ガイドライン 2章「台とカードの色」。状態の色 accent・danger は使わない）
    static readonly Color[] DiscColors = { T.Plum, T.Text, T.Go, T.Ink };

    static class Words
    {
        public const string PowerUnit = "power";
        public const string PowerUse = "-{0}/min";
        // いま要るスイッチの行の2行目
        public const string NavForBurn = "Turn ON: the burn needs it";
        public const string NavForFix = "Turn ON to fix course";
        public const string NavForReentry = "Turn ON for re-entry";
        public const string LightsSave = "Turn OFF to save power";
        public const string HeaterOn = "Turn ON: too cold";
        public const string RadioForData = "Turn ON to send data";
        // データ送信（set_transmit）
        public const string Send = "Send data";
        public const string SendNow = "Tap: send {0} data home";
        public const string Sending = "Sending... {0} left";
        public const string Paused = "Paused. Needs {0}";
        public const string OnBoard = "{0} data on board";
        public const string AllSent = "All data sent";
        public const string NoData = "No data yet";
        public const string SendPractice = "Sends science home";
        public const string MissSignal = "signal";
        public const string MissRadio = "■ Radio on";
        public const string MissEarth = "▲ face Earth";
    }

    // ------------------------------------------------------------------ 部品
    class SwitchRow { public UiRow Row; public SeatKnob Knob; }
    readonly Dictionary<string, SwitchRow> sw = new Dictionary<string, SwitchRow>();
    readonly HashSet<string> highlighted = new HashSet<string>();
    SeatStrip strip;
    UiCounter power;
    PcCard send, ignite;
    PcHelp help;
    bool built;

    protected override void BuildControls(RectTransform row)
    {
        string me = Seat;
        strip = SeatLook.Strip(row);

        power = UiKit.Counter(strip.Rect, "power", Words.PowerUnit);   // 表示だけ

        // 電源盤：2列 × 2行（4つを1列にすると盆に入らず全体が縮む）
        var board = UiKit.Row(strip.Rect, T.S1, TextAnchor.MiddleLeft);
        board.name = "Switchboard";
        RectTransform col = null;
        for (int i = 0; i < Switches.Length; i++)
        {
            var x = Switches[i];
            if (i % 2 == 0) col = UiKit.Col(board, T.S1, TextAnchor.MiddleLeft, 0f, 0f, true);
            string n = x.name;
            var r = UiKit.Row(col, x.icon, x.title, null, DiscColors[i % DiscColors.Length]);
            SeatLook.Tight(r.Name);
            r.Sub.fontSize = T.TextLabel;   // 2行目は補助ラベル 22（行 64 に名前と2行に入れる）
            SeatLook.Tight(r.Sub);
            r.Sub.gameObject.SetActive(true);
            SeatLook.ReserveWidth(r.Sub, K.F(x.onNote, "9.9"), K.F(x.offNote, "", "9.9"), Words.NavForBurn, Words.NavForFix, Words.NavForReentry,
                Words.LightsSave, Words.HeaterOn, Words.RadioForData);
            r.Dash.gameObject.SetActive(false);
            var knob = SeatLook.Knob(r.Right);
            // 行のどこを押してもよい（押せる範囲は行の間のすき間まで広げる）
            var hit = Ui.Rect("Hit", r.Root); UiKit.Fill(hit, -T.S1 / 2f);
            var hi = hit.gameObject.AddComponent<Image>(); hi.color = T.Clear; UiKit.Ignore(hi);
            hit.SetAsFirstSibling();
            SeatLook.Press(r.Root, r.Band, () => Flip(n));
            sw[n] = new SwitchRow { Row = r, Knob = knob };
        }

        SeatLook.Spacer(strip.Rect);
        var slot = K.Slot(strip.Rect);
        Detail = slot.gameObject.AddComponent<CanvasGroup>();   // 照明 OFF で暗くしてよいのはここだけ
        send = K.BigButton(slot, "SendData", "send", () => { var st = Ctx.State; Ctx.Send(Cmd.Transmit(me, !(st != null && st.transmit))); }, knob: true);
        ignite = K.BigButton(slot, "Ignite", "ignition", () => Ctx.SendDual("ignite", me));
        help = PcHelp.Build(slot, Ctx, me);
        built = true;
    }

    void Flip(string n)
    {
        Ctx.Send(Cmd.Switch(Seat, n, !IsOn(Ctx.State, n)));
        UiSelect.CursorOf(Root).Poke();
    }

    protected override void BuildStir(RectTransform row) => K.StirNote(row);

    public override void OnEvent(GameEvent e) { if (built) help.OnEvent(e); }

    /// <summary>スイッチが ON か。練習の間（state が無い）は事故直後の値（船のスイッチは全部 ON、SPEC 3章）</summary>
    static bool IsOn(GameState s, string name) => s == null || s.Switch(name);

    /// <summary>#84：練習の間「まだ試していない操作」を「次に押す物」にする（switch → 電源盤、set_transmit → Send data、ignite → Ignite）</summary>
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

        // 電力：いまの量と、毎分いくら使っているか（生命維持＋ON のスイッチの power_*。難易度の倍率とソーラーの発電は入れていない）
        power.Set(s != null ? K.Whole(s.power) : K.Words.NoValue);
        SeatLook.SetUnit(power, s != null ? K.F(Words.PowerUse, K.Num(Using(s))) : Words.PowerUnit);

        // 電源盤
        RectTransform wantRow = null;
        foreach (var x in Switches)
        {
            var c = sw[x.name];
            bool on = IsOn(s, x.name);
            bool want = now == "sw:" + x.name;
            c.Knob.Set(on);
            c.Row.Sub.text = want ? hint : on ? K.F(x.onNote, K.P("power_" + x.name)) : K.F(x.offNote, "", K.P("power_nav_restart"));
            c.Row.Sub.color = want ? T.Text : T.TextSoft;
            if (want) wantRow = c.Row.Root;
        }

        // 大ボタン：噴射窓の中は Ignite、CO2 が高いときは Help swap、それ以外は Send data
        bool window = K.WindowOpen(s), helping = !window && K.HelpShown(s, Ctx);
        ignite.Show(window); help.Card.Show(helping); send.Show(!window && !helping);
        bool actNow;
        PcCard act;
        if (window)
        {
            K.RefreshIgnite(ignite, s, Ctx, Seat);
            actNow = now == "ignite" || highlighted.Contains("ignite");
            ignite.SetNow(actNow);
            act = ignite;
        }
        else if (helping) { actNow = help.Refresh(now == "help"); act = help.Card; }
        else { actNow = RefreshSend(s, now == "send"); act = send; }

        // 手（1画面に1つ）：いま要るスイッチ ＞ いま押す大ボタン ＞ 練習で試す電源盤 ＞ 大ボタン（休む）
        if (wantRow != null) Point(wantRow, CursorDir.Right, true);
        else if (actNow) Point(act.Root, CursorDir.UpLeft, true);
        else if (highlighted.Contains("switch")) Point(sw[Switches[0].name].Row.Root, CursorDir.Right, true);
        else Point(act.Root, CursorDir.UpLeft);
    }

    /// <summary>Send data を描く。「いま押す物」なら true</summary>
    bool RefreshSend(GameState s, bool now)
    {
        bool on = s != null && s.transmit;
        send.Knob.Set(on);
        string note;
        if (s == null) note = null;   // #210 練習の Send data は2行目なし
        else
        {
            int n = K.OnBoard(s);
            var miss = SendMissing(s);
            if (now) note = K.F(Words.SendNow, n);
            else if (on && n > 0) note = miss.Count == 0 ? K.F(Words.Sending, n) : K.F(Words.Paused, string.Join(K.Words.Sep, miss));
            else if (n > 0) note = K.F(Words.OnBoard, n);
            else note = s.dataCollected > 0f ? Words.AllSent : Words.NoData;
        }
        send.Set(PcLook.Normal, Words.Send, note);
        bool lit = now || highlighted.Contains("set_transmit");
        send.SetNow(lit);
        return lit;
    }

    /// <summary>送れない理由（SPEC 5章の 7)：通信できる時間帯／通信 ON／地球向き）。席の形つき</summary>
    static List<string> SendMissing(GameState s)
    {
        var miss = new List<string>();
        if (!s.hasSignal) miss.Add(Words.MissSignal);
        if (!s.Switch("comms")) miss.Add(Words.MissRadio);
        if (s.attitude != "earth") miss.Add(Words.MissEarth);
        return miss;
    }

    /// <summary>毎分の電力（SPEC 5章の 2) の load）。表示用の足し算だけ</summary>
    static float Using(GameState s)
    {
        float use = GameParams.Get("power_life_support");
        foreach (var kv in s.sw)
            if (kv.Value && GameParams.TryGet("power_" + kv.Key, out var v)) use += v;
        return use;
    }

    /// <summary>いま Comms & Power が一番やるべき物（"sw:nav"・"send"・"ignite" など）。無ければ null。hint はその行の2行目</summary>
    string NowTarget(GameState s, out string hint)
    {
        hint = null;
        bool nav = s.Switch("nav");
        bool needFix = !s.correctionDone && !s.burnDone;
        if (K.WindowOpen(s))
        {
            if (!nav) { hint = Words.NavForBurn; return "sw:nav"; }
            return K.IgniteReady(s) ? "ignite" : null;
        }
        if (!nav)
        {
            if (needFix && s.t >= GameParams.Get("m06_delay_s")) { hint = Words.NavForFix; return "sw:nav"; }
            if (s.burnDone) { hint = Words.NavForReentry; return "sw:nav"; }
            if (s.t >= s.tAos) { hint = Words.NavForBurn; return "sw:nav"; }   // 噴射窓の前に入れておく（H24）
        }
        if (K.HelpShown(s, Ctx)) return "help";
        if (s.Switch("lights") && s.Threshold("power_frac", "half", out var half) && s.power < half * s.capacity)
        { hint = Words.LightsSave; return "sw:lights"; }
        if (!s.Switch("heater") && s.Threshold("temp", "slow", out var cold) && s.temp < cold)
        { hint = Words.HeaterOn; return "sw:heater"; }
        if (K.OnBoard(s) > 0 && s.hasSignal)
        {
            if (!s.Switch("comms")) { hint = Words.RadioForData; return "sw:comms"; }
            if (!s.transmit) return "send";
        }
        return null;
    }
}
