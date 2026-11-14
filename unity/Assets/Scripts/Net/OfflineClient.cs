// 書いた人：Claude ／ 最終更新：2026-11-14
// サーバーなしで「1回遊んだ記録」を再生するクライアント。記録はサーバーの tools/export_timeline.py が書き出す
// data/sim/timeline_*.json（Unity には Tools/GameFlow/オフライン再生の記録を取り込む で Assets/Data/Sim/ に写す）：
//   {"meta":{..., "viewer":"<視点の player_id>"}, "messages":[{"at":秒, "to":null|player_id, "msg":{サーバーが送るのと同じ形}}, ...]}
// at は部屋を作ってからの実時間の秒（speed=1）。to が null（全員宛て）か viewer 宛てのものだけ流す。
// 人の操作が要る所で止める：start を送るまでロビー、launch を送るまで設計。next を送ると Ending を飛ばして Report へ、again で設計からやり直す。
// それ以外は at のとおりに流す（Speed 倍）。再生中の操作（cmd）は無視する（記録なので結果は変わらない）。設計の ± は個数の見た目だけ変わる。
using System.Collections.Generic;
using UnityEngine;

public class OfflineClient : GameClientBase
{
    readonly List<(float at, Dictionary<string, object> msg)> msgs = new List<(float, Dictionary<string, object>)>();
    readonly int gateStart = -1, gateLaunch = -1, gateReport = -1, firstState = -1;
    Dictionary<string, object> design;
    int idx;
    float clock;
    bool running;
    int waitAt = -1;                 // ここまで来たら人の操作を待つ

    public float Speed = 1f;         // 再生の速さ（記録の秒 / 実時間の秒）
    public bool Paused;
    /// <summary>事故からの秒（ゲーム内。最後に流した state の t）</summary>
    public float Clock { get; private set; }
    public float Length => msgs.Count > 0 ? msgs[msgs.Count - 1].at : 0f;

    public override string Label => "OFFLINE (replay)";
    public override bool IsConnected => true;

    public OfflineClient(TextAsset timeline)
    {
        var root = MiniJson.ParseObject(timeline.text);
        string viewer = J.Str(J.Obj(root, "meta"), "viewer");
        foreach (var o in J.Arr(root, "messages") ?? new List<object>())
        {
            var e = (Dictionary<string, object>)o;
            string to = e.TryGetValue("to", out var t) && t != null ? t.ToString() : null;
            if (to != null && to != viewer) continue;
            var m = J.Obj(e, "msg");
            int i = msgs.Count;
            msgs.Add((J.Num(e, "at"), m));
            string type = J.Str(m, "type"), phase = J.Str(m, "phase"), kind = J.Str(m, "kind");
            if (gateStart < 0 && type == "room" && phase == "design") gateStart = i;
            if (gateLaunch < 0 && type == "event" && kind == "launch" && J.Str(m, "status") == "ok") gateLaunch = i;
            if (gateReport < 0 && type == "room" && phase == "report") gateReport = i;
            if (firstState < 0 && type == "state") firstState = i;
        }
        if (msgs.Count == 0) Debug.LogWarning("[Offline] 記録に messages が無い（形が違う？）");
    }

    void Emit(Dictionary<string, object> m)
    {
        if (J.Str(m, "type") == "design") design = Clone(m);
        if (J.Str(m, "type") == "state") Clock = J.Num(m, "t");
        Dispatch(m);
    }

    void Seek(int i)
    {
        idx = Mathf.Clamp(i, 0, msgs.Count);
        clock = idx < msgs.Count ? msgs[idx].at : Length;
    }

    public override void Connect(string room, string playerName, bool create)
    {
        ResetPhase();
        Seek(0); running = true; waitAt = gateStart;
    }

    public override void Send(Dictionary<string, object> m)
    {
        switch (J.Str(m, "type"))
        {
            case "start":
                if (waitAt == gateStart) { Seek(gateStart); waitAt = gateLaunch; }
                break;
            case "again":
                ResetPhase(); Seek(gateStart); waitAt = gateLaunch; running = true;
                break;
            case "design": EditDesign(m); break;
            case "launch":
                if (waitAt == gateLaunch) { Seek(gateLaunch); waitAt = -1; }
                break;
            case "next":
                if (gateReport >= 0 && idx < gateReport) { Seek(gateReport); running = true; }
                break;
        }
        // cmd は無視（記録の再生）
    }

    void EditDesign(Dictionary<string, object> m)
    {
        if (design == null) return;
        var d = J.Obj(design, "design");
        if (m.ContainsKey("part"))
        {
            var parts = J.Obj(d, "parts");
            string id = (string)m["part"];
            int n = J.Int(parts, id) + (m.ContainsKey("delta") ? (int)(double)m["delta"] : 0);
            if (n < 0) n = 0;
            parts[id] = (double)n;     // 上限・下限はサーバーが決める。オフラインでは見た目だけ
        }
        if (m.ContainsKey("trajectory")) d["trajectory"] = m["trajectory"];
        if (m.ContainsKey("rocket")) d["rocket"] = m["rocket"];
        Dispatch(design);
    }

    public override void Update(float dt)
    {
        if (!running || Paused) return;
        // 設計の記録（ボットの操作）は人が待っている間も流すが、打ち上げの手前で止める
        if (waitAt >= 0 && idx >= waitAt) return;
        clock += dt * Speed;
        while (idx < msgs.Count && msgs[idx].at <= clock && !(waitAt >= 0 && idx >= waitAt))
        {
            Emit(msgs[idx].msg);
            idx++;
        }
        if (idx >= msgs.Count) running = false;
    }

    /// <summary>飛行を着水まで飛ばす（確認用。Ending の手前で止める）</summary>
    public void SkipToEnd()
    {
        int stop = gateReport >= 0 ? gateReport : msgs.Count;
        while (idx < stop && (waitAt < 0 || idx < waitAt)) { Emit(msgs[idx].msg); idx++; }
        if (idx < msgs.Count) clock = msgs[idx].at;
    }

    static Dictionary<string, object> Clone(Dictionary<string, object> m) => (Dictionary<string, object>)MiniJson.Parse(MiniJson.Serialize(m));
}
