// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// 作る Issue：#147（土台。部品が使う共通の口）
//
// HUD の部品（HudPart）が使うもの：ゲームの状態・送信・席・ほかの部品。CabinHud が1つ作って全部の部品に渡す。
// 数値の表（parameters.json）は static の GameParams から読む（ここには持たない）。
using System.Collections.Generic;
using UnityEngine;

public class HudContext
{
    public readonly GameFlow Flow;
    /// <summary>軌道の小窓（GameFlow.trajectoryCamera が描く RenderTexture）。カメラが無ければ null</summary>
    public readonly RenderTexture TrajectoryRt;
    internal CabinHud Hud;

    public HudContext(GameFlow flow, RenderTexture trajectoryRt)
    {
        Flow = flow; TrajectoryRt = trajectoryRt;
    }

    /// <summary>接続をし直すと GameFlow が作り直すので、毎回ここから取る（覚えておかない）</summary>
    public GameModel Model => Flow.Model;
    public GameState State => Flow.Model.State;
    public IGameClient Client => Flow.Client;
    /// <summary>操作の遅れ（%）。長押しの時間は 0.5秒 ×（1 + SlowPct/100）（SPEC 6章、#71 の HoldButton）</summary>
    public float SlowPct => State != null ? State.slowPct : 0f;
    /// <summary>記録の再生中（操作は送っても無視される）</summary>
    public bool Offline => Flow.Client is OfflineClient;
    /// <summary>軌道の地図・タイムライン・ルートの比較を出すか。Mission Planner の席を持つ人だけ（SPEC 6章「席ごとに見えるもの」、#157）。
    /// 兼任なら持っている席で決まる。記録の再生（オフライン）は視点が1席だけなので全部出す（#200）</summary>
    public bool ShowPlannerView => Offline || Model.MySeats.Contains(Seats.Planner);
    /// <summary>席タブで選んでいる席（自分の席のどれか）。席がまだ無ければ null</summary>
    public string ActiveSeat { get; internal set; }
    /// <summary>照明 OFF か（state の sw.lights）</summary>
    public bool LightsOff { get; internal set; }
    /// <summary>今ある部品全部（今の席のパネルを含む）。CabinOverlay（#87）がほかの部品の Detail を暗くするときなどに使う</summary>
    public IEnumerable<HudPart> Parts => Hud.AllParts();

    public bool HasPart(string id) => Flow.HasPart(id);
    public void Toast(string s) => Flow.Toast(s);
    public void Send(Dictionary<string, object> message) => Flow.Client.Send(message);
    /// <summary>席タブで席を選ぶ（SeatTabBar が呼ぶ）</summary>
    public void SelectSeat(string seat) => Hud.SelectSeat(seat);

    // ------------------------------------------------------------------ 同時押し（#123：1人で両方の席を持つときは2通）
    // 組み合わせは server/game/seats.py の DUAL：ignite = pilot ＋ comms_power、swap_filter = life_support ＋ ほかの1席

    /// <summary>このコマンドの同時押しを1人でできるなら、その2席（送る順）。できなければ null</summary>
    public List<string> SoloPair(string cmd)
    {
        var mine = Model.MySeats;
        if (cmd == "ignite")
            return mine.Contains(Seats.Pilot) && mine.Contains(Seats.Comms) ? new List<string> { Seats.Pilot, Seats.Comms } : null;
        if (cmd == "swap_filter")
        {
            if (!mine.Contains(Seats.Life)) return null;
            string mate = mine.Contains(Seats.Pilot) ? Seats.Pilot : mine.Contains(Seats.Comms) ? Seats.Comms : mine.Contains(Seats.Planner) ? Seats.Planner : null;
            return mate != null ? new List<string> { Seats.Life, mate } : null;
        }
        return null;
    }

    /// <summary>同時押しのコマンドを送る。1人で両席を持つなら2席ぶん、そうでなければ seat の1通</summary>
    public void SendDual(string cmd, string seat)
    {
        var pair = SoloPair(cmd);
        if (pair == null) { Send(Cmd.Simple(cmd, seat)); return; }
        foreach (var s in pair) Send(Cmd.Simple(cmd, s));
    }
}
