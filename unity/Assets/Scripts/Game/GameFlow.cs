// 書いた人：Claude ／ 最終更新：2026-11-14
// ゲームの流れ。段階（title → lobby → design → practice/stir/flight(=Cabin) → ending → report）を IGameClient の OnPhase で切り替える。
// シーンは分けず、1シーン（Game.unity。船内の 3D は常に置く）の中で UGUI の画面を切り替える。
// 理由：船内のロードが1回で済む／サーバーの段階が途中で変わっても（再接続・again）画面の出し入れだけで追従できる／軽い。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameModel
{
    public string Phase = "title";
    public string Room = "", PlayerId = "";
    public List<string> MySeats = new List<string>();
    public List<PlayerInfo> Players = new List<PlayerInfo>();
    public Dictionary<string, object> Design, Totals, Result, Report;
    public string LaunchStatus = "";
    public GameState State;
    public HashSet<string> Practiced = new HashSet<string>();
    readonly Dictionary<string, float> max = new Dictionary<string, float>();

    /// <summary>ゲージの上限：設計の合計（酸素・燃料）か、見えた最大値（温度）</summary>
    public float Max(string key, float v)
    {
        float m = max.TryGetValue(key, out var x) ? x : 0f;
        if (Totals != null && (key == "oxygen" || key == "fuel")) m = Mathf.Max(m, J.Num(Totals, key));
        m = Mathf.Max(m, v);
        max[key] = m;
        return m;
    }
    public void ResetFlight() { State = null; Result = null; Report = null; max.Clear(); Practiced.Clear(); LaunchStatus = ""; }
}

public class GameFlow : MonoBehaviour
{
    public TextAsset partsJson;          // Data/Sim/parts.json
    public TextAsset timelineJson;       // Data/Sim/timeline_t1.json（オフライン再生）
    public string wsUrl = "ws://localhost:8765";
    public bool autoOffline;             // true なら Play 直後にオフライン再生へ（確認用）

    [System.NonSerialized] public GameModel Model = new GameModel();
    [System.NonSerialized] public IGameClient Client;

    TitleScreen title; LobbyScreen lobby; DesignScreen design; CabinHud cabin; EndingScreen ending; ReportScreen report;
    HoustonBox houston;
    Text toast; float toastUntil, pressClearAt;
    GameObject debugBar; Text speedLabel;

    void Awake()
    {
        var cgo = new GameObject("GameCanvas");
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();
        var root = (RectTransform)cgo.transform;

        cabin = new CabinHud(root, this);
        title = new TitleScreen(root, this);
        lobby = new LobbyScreen(root, this);
        design = new DesignScreen(root, this);
        ending = new EndingScreen(root, this);
        report = new ReportScreen(root, this);
        houston = new HoustonBox(root);
        BuildDebugBar(root);
        toast = Ui.Label(root, "", 28, Ui.Yellow, TextAnchor.MiddleCenter, FontStyle.Bold);
        Ui.Anchor(toast.rectTransform, 0.2f, 0.35f, 0.8f, 0.41f);
        ShowPhase("title");
    }

    void OnDestroy() { Client?.Close(); }

    void Start()
    {
        if (autoOffline) StartOffline();
    }

    // ------------------------------------------------------------------ 接続
    public void StartOffline()
    {
        if (timelineJson == null) { Toast("timeline が割り当てられていない"); return; }
        Use(new OfflineClient(timelineJson));
        Client.Connect("0000", "Player", true);
    }

    public void StartOnline(string url, string playerName, string room, bool create)
    {
        wsUrl = url;
        Use(new WsClient(url));
        Model.Room = room;
        ((WsClient)Client).OnStatus = msg => Toast(msg);
        Toast("Connecting to " + url + " ...");
        Client.Connect(room, string.IsNullOrEmpty(playerName) ? "Player" : playerName, create);
    }

    void Use(IGameClient c)
    {
        if (Client != null) { Client.OnPhase -= OnPhase; Client.OnState -= OnState; Client.OnEvent -= OnEvent; Client.OnMessage -= OnMessage; Client.Close(); }
        Client = c;
        Model = new GameModel();
        c.OnPhase += OnPhase; c.OnState += OnState; c.OnEvent += OnEvent; c.OnMessage += OnMessage;
    }

    // ------------------------------------------------------------------ 受信
    void OnPhase(string p)
    {
        if (p == "design") { Model.ResetFlight(); houston.Clear(); }
        ShowPhase(p);
    }

    void OnState(GameState s)
    {
        Model.State = s;
        cabin.Refresh();
        houston.SetLink(s.inFarside, !s.Switch("comms"));
    }

    void OnEvent(GameEvent e)
    {
        switch (e.kind)
        {
            case "houston": houston.Push(e.text); break;
            case "launch":
                Model.LaunchStatus = e.status == "ok" ? "" : e.status;
                design.Refresh();
                break;
            case "press": cabin.ShowPress(e); break;
            case "warning": break;   // 声なしの警告。NO SIGNAL / COMMS OFF は state から出すので今は何もしない
            case "practice":
                var done = J.Arr(e.raw, "done");
                Model.Practiced.Clear();
                if (done != null) foreach (var x in done) Model.Practiced.Add(x.ToString());
                cabin.Refresh();
                break;
        }
    }

    void OnMessage(Dictionary<string, object> m)
    {
        switch (J.Str(m, "type"))
        {
            case "welcome":
                Model.Room = J.Str(m, "room"); Model.PlayerId = J.Str(m, "player_id");
                PlayerPrefs.SetString("player_id_" + Model.Room, Model.PlayerId);
                SetSeats(J.Arr(m, "seats"));
                break;
            case "room":
                Model.Players.Clear();
                foreach (var o in J.Arr(m, "players") ?? new List<object>())
                {
                    var p = PlayerInfo.From((Dictionary<string, object>)o);
                    Model.Players.Add(p);
                    if (p.id == Model.PlayerId) Model.MySeats = p.seats;
                }
                lobby.Refresh();
                break;
            case "design":
                Model.Design = J.Obj(m, "design"); Model.Totals = J.Obj(m, "totals"); Model.LaunchStatus = "";
                design.Refresh();
                break;
            case "result": Model.Result = J.Obj(m, "result"); ending.Refresh(); break;
            case "report": Model.Report = m; report.Refresh(); break;
            case "error": Toast("ERROR: " + J.Str(m, "code") + " " + J.Str(m, "detail")); break;
        }
    }

    void SetSeats(List<object> a)
    {
        Model.MySeats = new List<string>();
        if (a != null) foreach (var x in a) Model.MySeats.Add(x.ToString());
    }

    public bool HasPart(string id) => Model.Design != null && J.Int(J.Obj(Model.Design, "parts"), id) > 0;

    // ------------------------------------------------------------------ 画面の切り替え
    void ShowPhase(string p)
    {
        Model.Phase = p;
        bool inCabin = p == "practice" || p == "stir" || p == "flight";
        title.Root.SetActive(p == "title");
        lobby.Root.SetActive(p == "lobby");
        design.Root.SetActive(p == "design");
        cabin.Root.SetActive(inCabin);
        debugBar.SetActive(Client is OfflineClient && (inCabin || p == "ending"));
        ending.Root.SetActive(p == "ending");
        report.Root.SetActive(p == "report");
        houston.Root.transform.SetAsLastSibling();
        if (p == "lobby") lobby.Refresh();
        if (p == "design") design.Refresh();
        if (inCabin) cabin.Refresh();
        if (p == "ending") ending.Refresh();
        if (p == "report") report.Refresh();
        if (!inCabin && p != "design") houston.Clear();
    }

    public void Toast(string s) { toast.text = s; toastUntil = Time.unscaledTime + 3f; }
    public void ClearPressLater() { pressClearAt = Time.unscaledTime + 2.5f; }

    void Update()
    {
        Client?.Update(Time.unscaledDeltaTime);
        houston.Tick(Time.unscaledTime);
        if (toast.text != "" && Time.unscaledTime > toastUntil) toast.text = "";
        if (pressClearAt > 0 && Time.unscaledTime > pressClearAt) { pressClearAt = 0; cabin.ClearPress(); }
        if (Client is OfflineClient oc && speedLabel != null) speedLabel.text = (oc.Paused ? "PAUSED" : "x" + oc.Speed) + "  T=" + Mathf.RoundToInt(oc.Clock);
    }

    // ------------------------------------------------------------------ オフライン再生の操作（右上）
    void BuildDebugBar(RectTransform root)
    {
        var bar = Ui.HBox(root, 4, 4, TextAnchor.MiddleLeft, false);
        Ui.Anchor(bar, 0.005f, 0.36f, 0.2f, 0.43f);   // ゲージの下・席パネルの上（左側）
        debugBar = bar.gameObject;
        speedLabel = Ui.Label(bar, "", 16, Ui.Cream, TextAnchor.MiddleRight); Ui.Size(speedLabel, w: 110);
        foreach (var sp in new[] { 1f, 5f, 20f, 60f })
        {
            float s = sp;
            Ui.Size(Ui.Btn(bar, "x" + s, Ui.Gray, () => { if (Client is OfflineClient o) { o.Speed = s; o.Paused = false; } }, 16, Ui.Cream), w: 52);
        }
        Ui.Size(Ui.Btn(bar, "PAUSE", Ui.Gray, () => { if (Client is OfflineClient o) o.Paused = !o.Paused; }, 16, Ui.Cream), w: 70);
        Ui.Size(Ui.Btn(bar, "SKIP", Ui.Gray, () => { if (Client is OfflineClient o) o.SkipToEnd(); }, 16, Ui.Cream), w: 60);
        debugBar.SetActive(false);
    }
}
