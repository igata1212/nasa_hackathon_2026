// 書いた人：Claude ／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 でイベントを CabinHud に配るだけにした・GameParams を読む）／ 更新：iizuka（Opus 5.5、2026-11-14、#132 Toast を GameToast に・#94 接続の表示 ConnectionUi を Toast の隣に）／ 更新：iizuka（Opus 5.5、2026-11-14、#146 UI の統合：台詞の id・ブリーフィング・警告の帯は CrisisBanner・ending/report では吹き出しを出さない）
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
    /// <summary>result のメッセージそのまま（result の外に並ぶ値：#148 の strict・strict_reason など）</summary>
    public Dictionary<string, object> ResultMsg;
    /// <summary>サーバーの時計の速さ（welcome の speed。1 = 実時間）</summary>
    public float Speed = 1f;
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
    public void ResetFlight() { State = null; Result = null; ResultMsg = null; Report = null; max.Clear(); Practiced.Clear(); LaunchStatus = ""; }
}

public class GameFlow : MonoBehaviour
{
    public TextAsset partsJson;          // Data/Sim/parts.json
    public TextAsset timelineJson;       // Data/Sim/timeline_t1_good.json（オフライン再生。正は data/sim/）
    public TextAsset timelineFailJson;   // Data/Sim/timeline_t4_fail.json
    public TextAsset parametersJson;     // Data/Sim/parameters.json（正は docs/design/。GameParams が読む。空なら UrgencyFx のものを使う）
    public TextAsset briefingJson;       // Data/Story/briefing.json（#152。部屋 → 設計の最初にヒューストンが話す前提知識。空でも Editor では読む）
    public TrajectoryView trajectory;    // 軌道の小窓（船内から遠くに置いた軌道の地図）
    public Camera trajectoryCamera;      // ↑を描くカメラ。RenderTexture に描いて HUD の隅に出す
    public string wsUrl = "ws://localhost:8765";
    public bool autoOffline;             // true なら Play 直後にオフライン再生へ（確認用）

    [System.NonSerialized] public GameModel Model = new GameModel();
    [System.NonSerialized] public IGameClient Client;

    TitleScreen title; LobbyScreen lobby; DesignScreen design; CabinHud cabin; EndingScreen ending; ReportScreen report;
    /// <summary>船内の HUD（#167：SeatView がゲージを席のモニターへ移すのに使う）。Start の前は null</summary>
    public CabinHud Cabin => cabin;
    HoustonBox houston;
    GameToast toast;   // #132 短いお知らせ（UI/GameToast.cs）
    ConnectionUi conn; // #94 切れたら自動でつなぎ直す・RECONNECTING・ほかの人の切断の印（UI/ConnectionUi.cs）
    /// <summary>オフライン再生の操作（PAUSE・SKIP・x1…）を出すか。#146（Fable）：ピッチの映像に入るので、ふだんは隠す（開発のときだけインスペクターで ON）</summary>
    public bool showReplayControls = false;
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
        LoadParams();

        RenderTexture miniRt = null;
        if (trajectoryCamera != null)
        {
            miniRt = new RenderTexture(640, 360, 24) { name = "TrajectoryMini", antiAliasing = 4 };
            trajectoryCamera.targetTexture = miniRt;
        }
        cabin = new CabinHud(root, this, miniRt);
        title = new TitleScreen(root, this);
        lobby = new LobbyScreen(root, this);
        design = new DesignScreen(root, this);
        ending = new EndingScreen(root, this);
        report = new ReportScreen(root, this);
        houston = new HoustonBox(root, this);
        BuildDebugBar(root);
        toast = new GameToast(root, this);
        conn = new ConnectionUi(root, this, toast);
        ShowPhase("title");
    }

    /// <summary>parameters.json を GameParams に読む。シーンを作り直す前（parametersJson が空）は UrgencyFx に割り当てたものを使う</summary>
    void LoadParams()
    {
        var ta = parametersJson;
        if (ta == null) { var fx = Object.FindAnyObjectByType<UrgencyFx>(); if (fx != null) ta = fx.parametersJson; }
        GameParams.Load(ta);
    }

    void OnDestroy() { Client?.Close(); }

    void OnApplicationQuit() { Client?.Close(); }

    void Start()
    {
        if (autoOffline) StartOffline();
    }

    // ------------------------------------------------------------------ 接続
    public void StartOffline() => StartOffline(false);

    /// <summary>記録の再生。fail=true なら T4（最安の設計 + バルブを閉じるだけ → ED-C）</summary>
    public void StartOffline(bool fail)
    {
        var ta = fail ? timelineFailJson : timelineJson;
        if (ta == null) { Toast("timeline が割り当てられていない"); return; }
        Use(new OfflineClient(ta));
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
        houston.Clear();
        c.OnPhase += OnPhase; c.OnState += OnState; c.OnEvent += OnEvent; c.OnMessage += OnMessage;
    }

    // ------------------------------------------------------------------ 受信
    void OnPhase(string p)
    {
        string prev = Model.Phase;
        if (p == "design")
        {
            // 部屋 → 設計の最初だけ、設計に入る前にブリーフィング（#152）。流している間に来た M01 は HoustonBox が待たせて、終わってから出す
            // （サーバーは start と同時に M01 を送るので、Clear より先に流し始める：Clear はブリーフィングと待たせている台詞を消さない）
            if (prev == "lobby") houston.PlayBriefing(briefingJson, null);
            Model.ResetFlight(); houston.Clear(); if (trajectory != null) trajectory.SetState(0f, "");
        }
        ShowPhase(p);
    }

    void OnState(GameState s)
    {
        Model.State = s;
        cabin.Refresh();
        houston.SetLink(!s.hasSignal, !s.Switch("comms"));
        if (trajectory != null) trajectory.SetState(s.t, s.route);
    }

    /// <summary>Model を直して、HUD（CabinHud の土台）には全部の kind を渡す。HUD の中で何を出すかは部品が決める</summary>
    void OnEvent(GameEvent e)
    {
        switch (e.kind)
        {
            case "houston":
                // エンディング・レポートの間は画面（EndingScreen・ReportScreen）が台詞を出すので、吹き出しは出さない
                if (Model.Phase != "ending" && Model.Phase != "report") houston.Push(e.id, e.text, e.priority);   // id で声を引く（#75）
                break;
            case "launch":
                Model.LaunchStatus = e.status == "ok" ? "" : e.status;
                design.Refresh();
                break;
            // warning（声なしの警告。通信できないとき）は危機の帯（CrisisBanner）が下の cabin.OnEvent で出す。帯は1本（ui-guidelines 7章）
            case "practice":
                Model.Practiced.Clear();
                foreach (var x in e.done) Model.Practiced.Add(x);
                break;
        }
        cabin.OnEvent(e);   // press（同時押しの合図）・practice（練習の案内）などは HUD の部品が受ける
    }

    void OnMessage(Dictionary<string, object> m)
    {
        switch (J.Str(m, "type"))
        {
            case "welcome":
                Model.Room = J.Str(m, "room"); Model.PlayerId = J.Str(m, "player_id"); Model.Speed = J.Num(m, "speed", 1f);
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
            case "result": Model.Result = J.Obj(m, "result"); Model.ResultMsg = m; ending.Refresh(); break;
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
        if (trajectoryCamera != null) trajectoryCamera.enabled = inCabin;
        debugBar.SetActive(showReplayControls && Client is OfflineClient && (inCabin || p == "ending"));
        ending.Root.SetActive(p == "ending");
        report.Root.SetActive(p == "report");
        houston.Root.transform.SetAsLastSibling();
        houston.Dock(p == "design");
        if (p == "lobby") lobby.Refresh();
        if (p == "design") design.Refresh();
        if (inCabin) cabin.Refresh();
        if (p == "ending") ending.Refresh();
        if (p == "report") report.Refresh();
        if (!inCabin && p != "design") houston.Clear();
    }

    /// <summary>短いお知らせ。kind を省くと文の頭で決める（"ERROR" ならエラーの色。GameToast の AutoKinds）</summary>
    public void Toast(string s, ToastKind kind = ToastKind.Auto) => toast.Show(s, kind);

    void Update()
    {
        Client?.Update(Time.unscaledDeltaTime);
        houston.Tick(Time.unscaledTime);
        toast.Tick(Time.unscaledTime);
        conn.Tick(Time.unscaledTime);
        cabin.Tick(Time.unscaledTime);   // 同時押しの文字を消すなど（HUD の部品の毎フレーム）
        if (Client is OfflineClient oc && speedLabel != null) speedLabel.text = (oc.Paused ? "PAUSED" : "x" + oc.Speed) + "  T=" + Mathf.RoundToInt(oc.Clock);
    }

    // ------------------------------------------------------------------ オフライン再生の操作（右上）
    void BuildDebugBar(RectTransform root)
    {
        // 右の列、軌道の地図（CabinHud.Layout の TopRight：下 0.64）の下に2段（#146：左はゲージが伸びたので右へ）
        var bar = Ui.VBox(root, 4, 0, TextAnchor.UpperRight, false);
        Ui.Anchor(bar, 0.79f, 0.545f, 0.99f, 0.63f);
        debugBar = bar.gameObject;
        var top = Ui.HBox(bar, 4, 0, TextAnchor.MiddleRight, false); Ui.Size(top, h: 36);
        var bottom = Ui.HBox(bar, 4, 0, TextAnchor.MiddleRight, false); Ui.Size(bottom, h: 36);
        speedLabel = Ui.Label(top, "", 16, Ui.Cream, TextAnchor.MiddleRight); Ui.Size(speedLabel, w: 110);
        Ui.Size(Ui.Btn(top, "PAUSE", Ui.Gray, () => { if (Client is OfflineClient o) o.Paused = !o.Paused; }, 16, Ui.Cream), w: 70);
        Ui.Size(Ui.Btn(top, "SKIP", Ui.Gray, () => { if (Client is OfflineClient o) o.SkipToEnd(); }, 16, Ui.Cream), w: 60);
        foreach (var sp in new[] { 1f, 5f, 20f, 60f })
        {
            float s = sp;
            Ui.Size(Ui.Btn(bottom, "x" + s, Ui.Gray, () => { if (Client is OfflineClient o) { o.Speed = s; o.Paused = false; } }, 16, Ui.Cream), w: 52);
        }
        debugBar.SetActive(false);
    }
}
