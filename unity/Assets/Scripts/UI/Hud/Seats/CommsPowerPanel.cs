// 書いた人：Claude ／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で CabinHud から分けた）／ 更新：iizuka（Opus 5.5、2026-11-14、#82）
// 作る Issue：#82（Comms & Power のパネル）。部品は PilotPanel.cs の下の PilotCommsKit（#80 と共用）
//
// Comms & Power（■）の席：左から「電力（表示だけ）」「船のスイッチ4つ（1枚の電源盤、#102）」「大ボタン1つ（データ送信・点火・HELP SWAP のどれか）」。
//   ・スイッチのカードは2行目に「切ると何が起きるか」を1行で（電力がいくら浮くか＋困ること）。切ってあるときは「いま起きていること」
//     NAV は切ってあるとき「入れ直すと電力が減る」を先に見せる（power_nav_restart）
//   ・大ボタン：噴射窓の中だけ IGNITE（Pilot と同時押し）。CO2 が注意の値を超えたら HELP SWAP（Life Support と同時押し。自分が Life Support を
//     持っていないときだけ、#81・#83 と同じ条件）。それ以外は SEND DATA（送信の ON/OFF。送れないときは足りない物を出す）
//   ・いま要る物を1つだけ橙の輪で光らせ、そのカードの2行目に「何をするか」を書く（NowTarget）
//   ・UI は判定しない：見た目は state の値（sw・transmit・has_signal・attitude・data_*・thresholds）を SPEC 5・6章のとおりに並べただけ
//   ・照明 OFF で暗くしてよいのは大ボタンの置き場だけ（電源盤は暗くしない。LIGHTS を入れ直せるように）
//   ・練習（M03）の間は state が無いので、スイッチは事故直後の値（全部 ON）で見せる。#84 は Highlight("switch") で電源盤を光らせる
// 文言は下の Switches と Words（Fable が直すのはここ）。数値は parameters.json（GameParams）と state から
using System.Collections.Generic;
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
    // LIGHTS のアイコンは電球（Ionicons の bulb）がまだ無いので sun で代用（#71 で足したら替える）
    static readonly (string name, string title, string icon, string onNote, string offNote)[] Switches =
    {
        ("heater", "HEATER", "temperature", "Off: saves {0}/min, gets cold", "Cabin is cooling down"),
        ("lights", "LIGHTS", "sun", "Off: saves {0}/min, dark", "Screen is dark"),
        ("nav", "NAV", "attitude", "Off: saves {0}/min, no burns", "No burns. ON again costs {1}"),
        ("comms", "RADIO", "comms", "Off: saves {0}/min, no Houston", "No Houston, no data sent"),
    };

    static class Words
    {
        public const string Power = "POWER {0}";
        public const string PowerPractice = "POWER";
        public const string PowerNote = "Using {0}/min";
        // いま要るスイッチのカードの2行目
        public const string NavForBurn = "Turn ON: the burn needs it";
        public const string NavForFix = "Turn ON to fix course";
        public const string NavForReentry = "Turn ON for re-entry";
        public const string LightsSave = "Turn OFF to save power";
        public const string HeaterOn = "Turn ON: too cold";
        public const string RadioForData = "Turn ON to send data";
        // データ送信（set_transmit）
        public const string Send = "SEND DATA";
        public const string SendNow = "Tap: send {0} data home";
        public const string Sending = "Sending... {0} left";
        public const string Paused = "Paused. Needs {0}";
        public const string OnBoard = "{0} data on board";
        public const string AllSent = "All data sent";
        public const string NoData = "No data yet";
        public const string SendPractice = "Sends science home";
        public const string MissSignal = "signal";
        public const string MissRadio = "■ RADIO ON";
        public const string MissEarth = "▲ face EARTH";
    }

    // ------------------------------------------------------------------ 部品
    readonly Dictionary<string, PcCard> sw = new Dictionary<string, PcCard>();
    readonly HashSet<string> highlighted = new HashSet<string>();
    PcCard power, send, ignite;
    PcHelp help;
    Image boardGlow;
    bool built;

    protected override void BuildControls(RectTransform row)
    {
        string me = Seat;
        var line = K.Line(row);

        power = K.Card(line, "Power", null, null);   // 表示だけ。幅が狭いのでアイコンなし（「POWER」の字で伝える）
        K.Width(power.Root, K.InfoW, 0f);

        var board = UiKit.Row(line, K.Gap, TextAnchor.MiddleLeft);
        board.name = "Switchboard";
        board.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
        K.Width(board, T.Hit, 1f);
        boardGlow = K.GroupGlow(board);
        foreach (var x in Switches)
        {
            string n = x.name;
            var c = K.Card(board, "Switch_" + n, x.icon, () => Ctx.Send(Cmd.Switch(me, n, !IsOn(Ctx.State, n))), knob: true);
            K.Width(c.Root, T.Hit, 1f);
            sw[n] = c;
        }

        var slot = K.Slot(line);
        Detail = slot.gameObject.AddComponent<CanvasGroup>();   // 照明 OFF で暗くしてよいのはここだけ
        send = K.Card(slot, "SendData", "send", () => { var st = Ctx.State; Ctx.Send(Cmd.Transmit(me, !(st != null && st.transmit))); }, knob: true, big: true);
        ignite = K.Card(slot, "Ignite", "ignition", () => Ctx.SendDual("ignite", me), big: true);
        UiKit.Fill(send.Root); UiKit.Fill(ignite.Root);
        help = PcHelp.Build(slot, Ctx, me);
        built = true;
    }

    protected override void BuildStir(RectTransform row) => K.StirNote(row);

    public override void OnEvent(GameEvent e) { if (built) help.OnEvent(e); }

    /// <summary>スイッチが ON か。練習の間（state が無い）は事故直後の値（船のスイッチは全部 ON、SPEC 3章）</summary>
    static bool IsOn(GameState s, string name) => s == null || s.Switch(name);

    /// <summary>#84：練習の間「まだ試していない操作」を橙の輪で光らせる（switch → 電源盤、set_transmit → SEND DATA、ignite → IGNITE）</summary>
    public override void Highlight(string cmd, bool on)
    {
        if (on ? !highlighted.Add(cmd) : !highlighted.Remove(cmd)) return;   // #84 は毎フレーム呼ぶので、変わったときだけ描き直す
        Refresh(Ctx.State);
    }

    public override void Tick(float now)
    {
        if (!built) return;
        K.Pulse(boardGlow, now);
        foreach (var c in sw.Values) K.Pulse(c.Glow, now);
        K.Pulse(send.Glow, now); K.Pulse(ignite.Glow, now); K.Pulse(help.Card.Glow, now);
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
        if (s == null) power.Set(PcLook.Normal, Words.PowerPractice, K.PracticeNote(Ctx, Seat));
        else power.Set(PcLook.Normal, K.F(Words.Power, K.Whole(s.power)), K.F(Words.PowerNote, K.Num(Using(s))));

        // 電源盤
        boardGlow.enabled = highlighted.Contains("switch");
        foreach (var x in Switches)
        {
            var c = sw[x.name];
            bool on = IsOn(s, x.name);
            bool want = now == "sw:" + x.name;
            c.Knob.Set(on);
            c.Set(PcLook.Normal, x.title, want ? hint : on ? K.F(x.onNote, K.P("power_" + x.name)) : K.F(x.offNote, "", K.P("power_nav_restart")));
            c.SetGlow(want);
        }

        // 大ボタン：噴射窓の中は IGNITE、CO2 が高いときは HELP SWAP、それ以外は SEND DATA
        bool window = K.WindowOpen(s), helping = !window && K.HelpShown(s, Ctx);
        ignite.Show(window); help.Card.Show(helping); send.Show(!window && !helping);
        if (window)
        {
            K.RefreshIgnite(ignite, s, Ctx, Seat);
            ignite.SetGlow(now == "ignite" || highlighted.Contains("ignite"));
        }
        else if (helping) help.Refresh(now == "help");
        else RefreshSend(s, now == "send");
    }

    void RefreshSend(GameState s, bool glow)
    {
        bool on = s != null && s.transmit;
        send.Knob.Set(on);
        string note;
        if (s == null) note = Words.SendPractice;
        else
        {
            int n = K.OnBoard(s);
            var miss = SendMissing(s);
            if (glow) note = K.F(Words.SendNow, n);
            else if (on && n > 0) note = miss.Count == 0 ? K.F(Words.Sending, n) : K.F(Words.Paused, string.Join(K.Words.Sep, miss));
            else if (n > 0) note = K.F(Words.OnBoard, n);
            else note = s.dataCollected > 0f ? Words.AllSent : Words.NoData;
        }
        send.Set(PcLook.Normal, Words.Send, note);
        send.SetGlow(glow || highlighted.Contains("set_transmit"));
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

    /// <summary>いま Comms & Power が一番やるべき物（"sw:nav"・"send"・"ignite" など）。無ければ null。hint はそのカードの2行目</summary>
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
