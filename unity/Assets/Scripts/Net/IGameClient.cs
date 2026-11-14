// 書いた人：Claude ／ 最終更新：2026-11-14
// ゲームの流れとサーバー（または記録の再生）のつなぎ口。
// 画面は IGameClient だけを見る。OfflineClient（記録の再生）と WsClient（WebSocket）を差し替えられる。
// メッセージの形は server/game/room.py に合わせてある（architecture.md の案より細かい）。
//
//   サーバー → クライアント（"type"）
//     welcome  {room, player_id, seats[], speed}       入室できた
//     room     {room, phase, players[{id,name,seats[],connected}]}   phase: lobby|design|practice|stir|flight|ending|report
//     design   {design{parts{id:n},trajectory,rocket}, totals{mass,cost,status,mass_limit,budget,capacity,oxygen,fuel}}
//     state    {t, state{power,oxygen,fuel,temp,co2,sw{},attitude,...}}   毎秒
//     event    {kind:houston|launch|practice|press|cmd, ...}   houston は {id,priority,text,text_ja,t}
//     result   {result{ending,requirements{},score,...}}
//     report   {result, design, score_breakdown{}, history[], commands[]}
//     error    {code, detail}
//   クライアント → サーバー
//     {"type":"create"|"join", name, room?, player_id?}   最初の1通
//     {"type":"start"|"launch"|"next"|"again"}
//     {"type":"design", part?, delta?|n?, trajectory?, rocket?}
//     {"cmd":"switch|set_attitude|...", seat, ...args}
using System;
using System.Collections.Generic;

public interface IGameClient
{
    /// <summary>段階が変わった（"lobby" "design" "practice" "stir" "flight" "ending" "report"）</summary>
    event Action<string> OnPhase;
    /// <summary>毎秒の状態</summary>
    event Action<GameState> OnState;
    /// <summary>台詞・マイルストーン・打ち上げ判定・同時押しの状況など（type=event）</summary>
    event Action<GameEvent> OnEvent;
    /// <summary>それ以外のメッセージ（welcome / room / design / result / report / error / pong）の生の辞書</summary>
    event Action<Dictionary<string, object>> OnMessage;

    string Label { get; }
    bool IsConnected { get; }

    /// <summary>入室する。create=true なら部屋を作る。</summary>
    void Connect(string room, string playerName, bool create);
    void Send(Dictionary<string, object> message);
    /// <summary>毎フレーム呼ぶ（受信キューの処理・再生の時計）</summary>
    void Update(float dt);
    void Close();
}

/// <summary>両方の実装が共通で使う、受信メッセージ → イベントの振り分け</summary>
public abstract class GameClientBase : IGameClient
{
    public event Action<string> OnPhase;
    public event Action<GameState> OnState;
    public event Action<GameEvent> OnEvent;
    public event Action<Dictionary<string, object>> OnMessage;

    string lastPhase;

    public abstract string Label { get; }
    public abstract bool IsConnected { get; }
    public abstract void Connect(string room, string playerName, bool create);
    public abstract void Send(Dictionary<string, object> message);
    public virtual void Update(float dt) { }
    public virtual void Close() { }

    protected void Dispatch(Dictionary<string, object> m)
    {
        if (m == null) return;
        switch (J.Str(m, "type"))
        {
            case "state":
                OnState?.Invoke(GameState.From(m));
                break;
            case "event":
                OnEvent?.Invoke(GameEvent.From(m));
                break;
            case "room":
                OnMessage?.Invoke(m);   // 先に players を渡してから段階を切り替える
                string ph = J.Str(m, "phase");
                if (ph != lastPhase) { lastPhase = ph; OnPhase?.Invoke(ph); }
                break;
            default:
                OnMessage?.Invoke(m);
                break;
        }
    }

    protected void ResetPhase() { lastPhase = null; }
}

/// <summary>コマンドの作り方（room.py の _args と合わせる）</summary>
public static class Cmd
{
    static Dictionary<string, object> Make(string cmd, string seat)
    {
        var d = new Dictionary<string, object> { ["cmd"] = cmd };
        if (!string.IsNullOrEmpty(seat)) d["seat"] = seat;
        return d;
    }
    public static Dictionary<string, object> Switch(string seat, string name, bool on) { var d = Make("switch", seat); d["name"] = name; d["on"] = on; return d; }
    public static Dictionary<string, object> Attitude(string seat, string mode) { var d = Make("set_attitude", seat); d["mode"] = mode; return d; }
    public static Dictionary<string, object> Transmit(string seat, bool on) { var d = Make("set_transmit", seat); d["on"] = on; return d; }
    public static Dictionary<string, object> Route(string seat, string route) { var d = Make("set_route", seat); d["route"] = route; return d; }
    public static Dictionary<string, object> Valve(string seat, int tank) { var d = Make("close_valve", seat); d["tank"] = tank; return d; }
    public static Dictionary<string, object> Simple(string cmd, string seat) => Make(cmd, seat);   // stir / correction_burn / ignite / swap_filter

    public static Dictionary<string, object> Type(string type) => new Dictionary<string, object> { ["type"] = type };
    public static Dictionary<string, object> DesignPart(string part, int delta) => new Dictionary<string, object> { ["type"] = "design", ["part"] = part, ["delta"] = delta };
    public static Dictionary<string, object> DesignTrajectory(string t) => new Dictionary<string, object> { ["type"] = "design", ["trajectory"] = t };
    public static Dictionary<string, object> DesignRocket(string r) => new Dictionary<string, object> { ["type"] = "design", ["rocket"] = r };
}
