// 書いた人：Claude ／ 最終更新：2026-11-14
// サーバーなしで「1回遊んだ記録」を再生するクライアント。形は Assets/Data/Sim/timeline_*.json：
//   welcome[]   入室直後に届くメッセージ（welcome・room(lobby)・design）
//   on_start[]  "start" を送ったとき（room(design)・M01・design）
//   on_launch[] "launch" を送ったとき（launch 判定・M02・M03・room(flight)）。練習(practice)とかき混ぜ(stir)は省いてある
//   flight[]    {at, msg}：at は事故からの秒（ゲーム内）。M05 から着水・結果まで
//   on_next[]   "next" を送ったとき（room(report)・R01・report）
// 記録の作り方：Assets/Data/Sim/record_timeline.py.txt（tmp/wt/server の Game を直接進めて書き出した）。
// サーバー担当が data/sim/timeline_*.json を出したら、同じ形ならそのまま差し替えられる。形が違えばここで読み替える。
// 再生中の操作（cmd）は無視する（記録なので結果は変わらない）。設計の ± は個数だけ変わる（合計は記録のまま）。
using System.Collections.Generic;
using UnityEngine;

public class OfflineClient : GameClientBase
{
    readonly Dictionary<string, object> root;
    readonly List<(float at, Dictionary<string, object> msg)> flight = new List<(float, Dictionary<string, object>)>();
    Dictionary<string, object> design;
    int idx;
    float clock;
    bool flying;

    public float Speed = 1f;      // 再生の速さ（ゲーム内の秒 / 実時間の秒）
    public bool Paused;
    public float Clock => clock;
    public float Length => flight.Count > 0 ? flight[flight.Count - 1].at : 0f;

    public override string Label => "OFFLINE (replay)";
    public override bool IsConnected => true;

    public OfflineClient(TextAsset timeline)
    {
        root = MiniJson.ParseObject(timeline.text);
        foreach (var o in J.Arr(root, "flight"))
        {
            var e = (Dictionary<string, object>)o;
            flight.Add((J.Num(e, "at"), J.Obj(e, "msg")));
        }
    }

    void Play(string key)
    {
        var a = J.Arr(root, key);
        if (a == null) return;
        foreach (var o in a)
        {
            var m = o as Dictionary<string, object>;
            if (J.Str(m, "type") == "design") design = Clone(m);
            Dispatch(m);
        }
    }

    public override void Connect(string room, string playerName, bool create)
    {
        ResetPhase();
        Play("welcome");
    }

    public override void Send(Dictionary<string, object> m)
    {
        string type = J.Str(m, "type");
        switch (type)
        {
            case "start": Play("on_start"); break;
            case "again": Play("on_start"); break;
            case "design": EditDesign(m); break;
            case "launch":
                Play("on_launch");
                idx = 0; clock = 0f; flying = true;
                break;
            case "next": Play("on_next"); break;
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
            int n = J.Int(parts, id) + (int)(double)m["delta"];
            if (n < 0) n = 0;
            parts[id] = (double)n;     // 上限・下限はサーバーが決める。オフラインでは見た目だけ
        }
        if (m.ContainsKey("trajectory")) d["trajectory"] = m["trajectory"];
        if (m.ContainsKey("rocket")) d["rocket"] = m["rocket"];
        Dispatch(design);
    }

    public override void Update(float dt)
    {
        if (!flying || Paused) return;
        clock += dt * Speed;
        while (idx < flight.Count && flight[idx].at <= clock)
        {
            Dispatch(flight[idx].msg);
            idx++;
        }
        if (idx >= flight.Count) flying = false;
    }

    /// <summary>再生を最後まで飛ばす（確認用）</summary>
    public void SkipToEnd()
    {
        while (flying && idx < flight.Count) { Dispatch(flight[idx].msg); idx++; }
        flying = false;
    }

    static Dictionary<string, object> Clone(Dictionary<string, object> m) => (Dictionary<string, object>)MiniJson.Parse(MiniJson.Serialize(m));
}
