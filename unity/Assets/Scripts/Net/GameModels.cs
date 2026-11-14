// 書いた人：Claude ／ 最終更新：2026-11-14
// サーバーから届くメッセージを、画面が使いやすい形にしたもの。数値はサーバーの値をそのまま入れる（ここに数値を書かない）。
// 形の元：server/game/room.py の snapshot()・milestone_event()・Game.room_msg()
using System.Collections.Generic;

public class GameState
{
    public float t, capacity, power, oxygen, fuel, temp, co2, slowPct;
    public int filters, swaps;
    public bool leakOpen, transmit, correctionDone, burnDone, strict, hasSignal, inFarside;
    public string attitude = "earth", route = "", ending = "";
    public float tLos, tAos, tBurnOpen, tBurnClose, tReentry, tSplashdown;
    public float dataCollected, dataSent;
    public Dictionary<string, bool> sw = new Dictionary<string, bool>();

    public bool Switch(string name) => sw.TryGetValue(name, out var v) && v;

    public static GameState From(Dictionary<string, object> m)
    {
        var s = J.Obj(m, "state") ?? m;
        var g = new GameState
        {
            t = J.Num(s, "t"), capacity = J.Num(s, "capacity"), power = J.Num(s, "power"), oxygen = J.Num(s, "oxygen"),
            fuel = J.Num(s, "fuel"), temp = J.Num(s, "temp"), co2 = J.Num(s, "co2"), slowPct = J.Num(s, "slow_pct"),
            filters = J.Int(s, "filters"), swaps = J.Int(s, "swaps"),
            leakOpen = J.Bool(s, "leak_open"), transmit = J.Bool(s, "transmit"), correctionDone = J.Bool(s, "correction_done"),
            burnDone = J.Bool(s, "burn_done"), strict = J.Bool(s, "strict"), hasSignal = J.Bool(s, "has_signal"),
            inFarside = J.Bool(s, "in_farside"),
            attitude = J.Str(s, "attitude", "earth"), route = J.Str(s, "route"), ending = J.Str(s, "ending"),
            tLos = J.Num(s, "t_los"), tAos = J.Num(s, "t_aos"), tBurnOpen = J.Num(s, "t_burn_open"),
            tBurnClose = J.Num(s, "t_burn_close"), tReentry = J.Num(s, "t_reentry"), tSplashdown = J.Num(s, "t_splashdown"),
            dataCollected = J.Num(s, "data_collected"), dataSent = J.Num(s, "data_sent"),
        };
        var sw = J.Obj(s, "sw");
        if (sw != null) foreach (var kv in sw) g.sw[kv.Key] = kv.Value is bool b && b;
        return g;
    }
}

/// <summary>{"type":"event","kind":"houston|launch|practice|press|cmd", ...}</summary>
public class GameEvent
{
    public string kind, id, priority, text, textJa, status, cmd;
    public float t;
    public Dictionary<string, object> raw;

    public static GameEvent From(Dictionary<string, object> m) => new GameEvent
    {
        kind = J.Str(m, "kind"), id = J.Str(m, "id"), priority = J.Str(m, "priority"),
        text = J.Str(m, "text"), textJa = J.Str(m, "text_ja"), status = J.Str(m, "status"), cmd = J.Str(m, "cmd"),
        t = J.Num(m, "t"), raw = m,
    };
}

public class PlayerInfo
{
    public string id, name;
    public List<string> seats = new List<string>();
    public bool connected;

    public static PlayerInfo From(Dictionary<string, object> m)
    {
        var p = new PlayerInfo { id = J.Str(m, "id"), name = J.Str(m, "name"), connected = J.Bool(m, "connected", true) };
        var a = J.Arr(m, "seats");
        if (a != null) foreach (var x in a) p.seats.Add(x.ToString());
        return p;
    }
}
