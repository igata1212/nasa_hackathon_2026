// 書いた人：Claude ／ 最終更新：2026-11-14
// WebSocket でサーバー（Python、server/README.md）につなぐクライアント。
// .NET の System.Net.WebSockets.ClientWebSocket を使う（パッケージなし）。エディタ・iOS・デスクトップで動く。
// WebGL のビルドでは ClientWebSocket が使えないので、ブラウザの WebSocket を呼ぶ（Plugins/WebGL/MrWebSocket.jslib。#222 更新：Taiga 2026-11-15）。
// 状態の文（Connected / Disconnected / Connection error: …）はどちらも同じ。https のページからは wss:// だけつながる（ブラウザの決まり）。
// 受信は別スレッドで読み、ConcurrentQueue に積んで、Update（メインスレッド）でイベントを出す。
// つなぐ先：ws://localhost:8765（server/main.py の既定）。公開は wss://…（Cloudflare Tunnel）。
// 最初の1通：{"type":"create","name":…} か {"type":"join","room":"1234","name":…,"player_id":…}（再接続は player_id を付ける）
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Net.WebSockets;
#else
using System.Runtime.InteropServices;
#endif

public class WsClient : GameClientBase
{
    public string Url = "ws://localhost:8765";
    readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
    readonly ConcurrentQueue<string> status = new ConcurrentQueue<string>();
    volatile bool open;

#if !UNITY_WEBGL || UNITY_EDITOR
    ClientWebSocket ws;
    CancellationTokenSource cts;
    readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
#else
    [DllImport("__Internal")] static extern void MrWsOpen(string url);
    [DllImport("__Internal")] static extern int MrWsState();
    [DllImport("__Internal")] static extern int MrWsSend(string json);
    [DllImport("__Internal")] static extern string MrWsPoll();
    [DllImport("__Internal")] static extern string MrWsError();
    [DllImport("__Internal")] static extern void MrWsClose();
    bool active;        // Connect してから、閉じたのを知らせるまで
    string firstJson;   // つながったら最初に送る1通（create / join）
#endif

    public override string Label => "ONLINE " + Url;
    public override bool IsConnected => open;
    /// <summary>接続の状態の知らせ（画面に出す用）。Update で取り出す</summary>
    public Action<string> OnStatus;

    public WsClient(string url) { Url = url; }

    public override void Connect(string room, string playerName, bool create)
    {
        ResetPhase();
        var first = new Dictionary<string, object> { ["type"] = create ? "create" : "join", ["name"] = playerName };
        if (!create)
        {
            first["room"] = room;
            string pid = PlayerPrefs.GetString("player_id_" + room, "");
            if (pid != "") first["player_id"] = pid;
        }
        string firstJson = MiniJson.Serialize(first);
#if UNITY_WEBGL && !UNITY_EDITOR
        this.firstJson = firstJson;
        open = false;
        active = true;
        MrWsOpen(Url);
#else
        cts = new CancellationTokenSource();
        ws = new ClientWebSocket();
        _ = Run(firstJson, cts.Token);
#endif
    }

#if !UNITY_WEBGL || UNITY_EDITOR
    async Task Run(string firstJson, CancellationToken ct)
    {
        try
        {
            await ws.ConnectAsync(new Uri(Url), ct);
            open = true;
            status.Enqueue("Connected");
            await SendRaw(firstJson);
            var buf = new byte[16 * 1024];
            var sb = new StringBuilder();
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), ct);
                if (r.MessageType == WebSocketMessageType.Close) break;
                sb.Append(Encoding.UTF8.GetString(buf, 0, r.Count));
                if (r.EndOfMessage) { inbox.Enqueue(sb.ToString()); sb.Clear(); }
            }
        }
        catch (Exception e)
        {
            if (!ct.IsCancellationRequested) status.Enqueue("Connection error: " + e.Message);
        }
        open = false;
        if (!ct.IsCancellationRequested) status.Enqueue("Disconnected");
    }

    async Task SendRaw(string json)
    {
        await sendLock.WaitAsync();
        try
        {
            if (ws != null && ws.State == WebSocketState.Open)
                await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (Exception e) { status.Enqueue("Send error: " + e.Message); }
        finally { sendLock.Release(); }
    }
#endif

    public override void Send(Dictionary<string, object> message)
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        _ = SendRaw(MiniJson.Serialize(message));
#else
        if (open) MrWsSend(MiniJson.Serialize(message));
#endif
    }

    public override void Update(float dt)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (active)
        {
            int st = MrWsState();
            if (st == 1 && !open) { open = true; status.Enqueue("Connected"); MrWsSend(firstJson); }
            for (string m = MrWsPoll(); m != null; m = MrWsPoll()) inbox.Enqueue(m);
            if (st >= 2)
            {
                if (st == 3) status.Enqueue("Connection error: " + MrWsError());
                open = false; active = false;
                status.Enqueue("Disconnected");
            }
        }
#endif
        while (status.TryDequeue(out var s)) OnStatus?.Invoke(s);
        while (inbox.TryDequeue(out var json))
        {
            try { Dispatch(MiniJson.ParseObject(json)); }
            catch (Exception e) { Debug.LogWarning("[Ws] 受信を読めない: " + e.Message + " / " + json); }
        }
    }

    public override void Close()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        try { cts?.Cancel(); ws?.Dispose(); } catch { }
#else
        active = false;
        MrWsClose();
#endif
        open = false;
    }
}
