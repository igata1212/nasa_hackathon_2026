// 書いた人：Claude ／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 でサーバーが送る値を全部読む）
// サーバーから届くメッセージを、画面が使いやすい形にしたもの。数値はサーバーの値をそのまま入れる（ここに数値を書かない）。
// 形の元：server/game/room.py の snapshot()・milestone_event()・Game.room_msg()、server/README.md のメッセージ表
// 「#148」の値はサーバーで足している途中（名前は #148 の作業中の版に合わせた）。届かない間は 0・空・null のまま
using System.Collections.Generic;

public class GameState
{
    public float t, capacity, power, oxygen, fuel, temp, co2, slowPct;
    public int filters, swaps;
    public float leaked;                                   // 漏れた酸素の量（#101 の Tank 2 の針）
    public bool leakOpen, transmit, correctionDone, burnDone, strict, hasSignal, inFarside;
    public bool? firstTry;                                 // 点火が1回目で成立したか。まだ点火していなければ null
    public string attitude = "earth", route = "", ending = "";
    public float tLos, tAos, tBurnOpen, tBurnClose, tReentry, tSplashdown;
    public float dataCollected, dataSent;
    public Dictionary<string, bool> sw = new Dictionary<string, bool>();
    public Dictionary<string, float> observedS = new Dictionary<string, float>();   // 観測機器ごとの観測した秒（camera・spectrometer・radar）

    // #148：ルート A・B 両方の時刻（route を選ぶ前でも両方わかる）
    public float tReentryA, tReentryB, tSplashdownA, tSplashdownB;
    // #148：strict の理由（"missed_burn"＝噴射窓を逃した／"reentry_not_ready"＝再突入の準備不足）。strict でなければ ""
    public string strictReason = "";
    // #148：Req 2 の今の見込み（ここで観測をやめて無事に帰ったとしたときの science）。送られていなければ hasScienceNow = false
    public float scienceNow;
    public bool hasScienceNow;
    // #148：注意・危険の閾値（{"power_frac":{"half","low","crit"}, "oxygen":{"mid","low"}, "fuel":{"low"}, "co2":{"warn","slow"}, ...}）。Threshold で読む
    public Dictionary<string, object> thresholds;
    /// <summary>state の中身そのまま（上に無いキーを読むとき）</summary>
    public Dictionary<string, object> raw;

    public bool Switch(string name) => sw.TryGetValue(name, out var v) && v;

    /// <summary>サーバーが送る閾値（#148）。例：Threshold("co2", "warn", out var v)。無ければ false</summary>
    public bool Threshold(string group, string key, out float v)
    {
        var g = J.Obj(thresholds, group);
        v = J.Num(g, key);
        return J.Has(g, key);
    }

    public float RouteReentry(string r) => r == "a" ? tReentryA : r == "b" ? tReentryB : tReentry;
    public float RouteSplashdown(string r) => r == "a" ? tSplashdownA : r == "b" ? tSplashdownB : tSplashdown;

    public static GameState From(Dictionary<string, object> m)
    {
        var s = J.Obj(m, "state") ?? m;
        var g = new GameState
        {
            t = J.Num(s, "t"), capacity = J.Num(s, "capacity"), power = J.Num(s, "power"), oxygen = J.Num(s, "oxygen"),
            fuel = J.Num(s, "fuel"), temp = J.Num(s, "temp"), co2 = J.Num(s, "co2"), slowPct = J.Num(s, "slow_pct"),
            filters = J.Int(s, "filters"), swaps = J.Int(s, "swaps"), leaked = J.Num(s, "leaked"),
            leakOpen = J.Bool(s, "leak_open"), transmit = J.Bool(s, "transmit"), correctionDone = J.Bool(s, "correction_done"),
            burnDone = J.Bool(s, "burn_done"), strict = J.Bool(s, "strict"), hasSignal = J.Bool(s, "has_signal"),
            inFarside = J.Bool(s, "in_farside"),
            firstTry = J.Has(s, "first_try") ? J.Bool(s, "first_try") : (bool?)null,
            attitude = J.Str(s, "attitude", "earth"), route = J.Str(s, "route"), ending = J.Str(s, "ending"),
            tLos = J.Num(s, "t_los"), tAos = J.Num(s, "t_aos"), tBurnOpen = J.Num(s, "t_burn_open"),
            tBurnClose = J.Num(s, "t_burn_close"), tReentry = J.Num(s, "t_reentry"), tSplashdown = J.Num(s, "t_splashdown"),
            dataCollected = J.Num(s, "data_collected"), dataSent = J.Num(s, "data_sent"),
            tReentryA = J.Num(s, "t_reentry_a"), tReentryB = J.Num(s, "t_reentry_b"),
            tSplashdownA = J.Num(s, "t_splashdown_a"), tSplashdownB = J.Num(s, "t_splashdown_b"),
            strictReason = J.Str(s, "strict_reason"),
            scienceNow = J.Num(s, "science_now"), hasScienceNow = J.Has(s, "science_now"),
            thresholds = J.Obj(s, "thresholds"),
            raw = s,
        };
        var sw = J.Obj(s, "sw");
        if (sw != null) foreach (var kv in sw) g.sw[kv.Key] = kv.Value is bool b && b;
        var obs = J.Obj(s, "observed_s");
        if (obs != null) foreach (var kv in obs) g.observedS[kv.Key] = kv.Value is double d ? (float)d : 0f;
        return g;
    }
}

/// <summary>{"type":"event","kind":"houston|warning|launch|practice|press|cmd", ...}</summary>
public class GameEvent
{
    public string kind, id, priority, text, textJa, status, cmd;
    public float t;
    public string seat, player;      // practice・cmd：誰がどの席で
    public string reason;            // warning：no_signal / comms_off
    public bool repeat;              // houston：M04 の繰り返し
    public bool? ok;                 // cmd：Sim のメソッドの戻り値（戻り値の無いコマンドは null）
    public float windowS;            // press（waiting）：同時押しの幅（秒）
    public float mass, cost;         // launch：設計の質量と費用
    public List<string> seats = new List<string>();   // press：押した席
    public List<string> done = new List<string>();    // practice：練習を済ませた席
    public Dictionary<string, object> args;           // cmd：コマンドの引数
    public Dictionary<string, object> raw;

    public static GameEvent From(Dictionary<string, object> m)
    {
        var e = new GameEvent
        {
            kind = J.Str(m, "kind"), id = J.Str(m, "id"), priority = J.Str(m, "priority"),
            text = J.Str(m, "text"), textJa = J.Str(m, "text_ja"), status = J.Str(m, "status"), cmd = J.Str(m, "cmd"),
            t = J.Num(m, "t"), seat = J.Str(m, "seat"), player = J.Str(m, "player"), reason = J.Str(m, "reason"),
            repeat = J.Bool(m, "repeat"), ok = J.Has(m, "ok") ? J.Bool(m, "ok") : (bool?)null,
            windowS = J.Num(m, "window_s"), mass = J.Num(m, "mass"), cost = J.Num(m, "cost"),
            args = J.Obj(m, "args"), raw = m,
        };
        Strings(J.Arr(m, "seats"), e.seats);
        Strings(J.Arr(m, "done"), e.done);
        return e;
    }

    static void Strings(List<object> a, List<string> to)
    {
        if (a != null) foreach (var x in a) to.Add(x.ToString());
    }
}

public class PlayerInfo
{
    public string id, name;
    public List<string> seats = new List<string>();
    public bool connected, bot;

    public static PlayerInfo From(Dictionary<string, object> m)
    {
        var p = new PlayerInfo { id = J.Str(m, "id"), name = J.Str(m, "name"), connected = J.Bool(m, "connected", true), bot = J.Bool(m, "bot") };
        var a = J.Arr(m, "seats");
        if (a != null) foreach (var x in a) p.seats.Add(x.ToString());
        return p;
    }
}
