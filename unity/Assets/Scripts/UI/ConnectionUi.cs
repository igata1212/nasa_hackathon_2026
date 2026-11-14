// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// 作る Issue：#94（接続が切れたときの表示。決定：#94 のコメント・#143・architecture.md「再接続」。切れた人は自動で同じ部屋・同じ席に戻る。引き継ぎ（TAKE OVER）は作らない）
//
// するのは3つ：
//   ① 自分が切れた → 自動でつなぎ直す。同じ合言葉に join ＋ player_id（WsClient.Connect が PlayerPrefs から付ける）。サーバーは同じ席に戻す（server/README.md）
//      その間は画面全体に RECONNECTING...（暗幕＋カード＋回る点。ゲームの中の通信途絶 NO SIGNAL＝HUD の帯・cloud-offline とは別の見た目）。後ろの操作は通さない
//      戻れない（部屋が閉じた・席が無い・出発してしまった）ときは理由と BACK TO TITLE。長くかかったときも BACK TO TITLE を出す（試すのはやめない）
//   ② ほかの人が切れた → その人の席のバッジに切断の印（danger の丸に ×）を付けた小さな札。船内は席タブの右上（席のパネルの上の端）、ほかの画面は右上の角
//   ③ 切れた・戻ったとき → 短い知らせ（GameToast）。自分が戻ったら「Back online. Same seat.」、ほかの人は「Ken lost connection」「Ken is back」
// GameFlow が作って毎フレーム Tick を呼ぶ（Toast の隣の2行）。WsClient は変えず、同じ WsClient の Close → Connect を呼び直してつなぎ直す
// （同じ WsClient なので GameFlow の受信はそのまま。Connect が段階を忘れるので、戻ると room の段階で画面が出し直される）。
// Wi-Fi が黙って落ちたときにも気づけるように ping を送り、SilentAfter 秒なにも届かなければ切れたことにする。
// 切断の印は AddOfflineMark(バッジ) で、ほかの画面（席タブ・部屋の名前タグ）からも付けられる。
// 注意：ロビーで切れると、サーバーはその人を消して席を配り直す（room.py の disconnect）。だからロビーでは player_id でなく名前で入り直す（席は変わることがある）
// 文言・時間・置き場所は下の「決めること」の1か所
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using T = UiTokens;

public class ConnectionUi
{
    // ================================================================== 決めること（直すときはここだけ）
    /// <summary>StoryDirector の演出（50）より上、GameToast（61）より下</summary>
    public const int SortingOrder = 60;
    const float PingEvery = 2f;          // 実時間の秒。つながっている間に ping を送る間
    const float SilentAfter = 8f;        // これだけ何も届かなければ切れたことにする（黙って落ちた Wi-Fi）
    const float OverlayDelay = 0.4f;     // すぐ戻れたときに画面をちらつかせない
    static readonly float[] RetryAfter = { 0.5f, 1f, 2f, 3f, 5f };   // n 回目を試すまでの間（最後の値をくり返す）
    const float AttemptTimeout = 6f;     // つながったのに welcome が来ないとき、ここで試し直す
    const float CloseGap = 0.25f;        // Close してから Connect するまで（前の試しの後始末を先に終わらせる）
    const float LeaveAfter = 15f;        // これだけ戻れなければ BACK TO TITLE も出す
    const float SpinStep = 0.1f;         // 回る点が1つ進む秒
    const int SpinDots = 8;
    const float CardW = T.TitleFormW;    // カードの幅（タイトルの入力パネルと同じ）
    const float MarkD = T.IconS + T.S1;  // 切断の印の丸（中の × は IconS）
    const float ChipH = T.BadgeS + T.S1 * 2f;   // 札の高さ（席バッジ＋上下 8）
    const string PlayerIdKey = "player_id_";    // GameFlow・WsClient と同じ PlayerPrefs のキー（＋合言葉）

    /// <summary>戻れないときのサーバーのエラー code → 理由。ほかの code では試し続ける</summary>
    static readonly Dictionary<string, string> FailText = new Dictionary<string, string>
    {
        ["no_such_room"] = "This room has closed.",
        ["unknown_player"] = "Your seat is not in this room anymore.",
        ["game_started"] = "The mission started while you were away.",
        ["room_full"] = "The room filled up while you were away.",
        ["no_free_seat"] = "The room filled up while you were away.",
    };

    static class Words
    {
        public const string Reconnecting = "RECONNECTING...";
        public const string SeatSaved = "Your seat is saved. Getting you back in.";
        public const string LobbyBack = "Getting you back into the room.";
        public const string TryingNow = "Trying now...";
        public const string TryAgainIn = "Trying again in {0} s";
        public const string CantRejoin = "CAN'T REJOIN";
        public const string BackToTitle = "BACK TO TITLE";
        public const string Room = "ROOM ";
        public const string BackSameSeat = "Back online. Same seat.";
        public const string BackOnline = "Back online.";
        public const string OtherLost = "{0} lost connection";
        public const string OtherBack = "{0} is back";
        public const string Offline = "OFFLINE";
        public const string NoName = "Player";
    }

    /// <summary>ほかの人が切れた札の置き場所（段階 → 位置・札のどこを合わせるか）。船内は右の列、軌道の地図（CabinHud の TopRight：下 0.64）のすぐ下に吊る
    /// （#146：席のパネルの上の右は SLOW の札（CabinOverlay）が使う。地図の下はオフライン再生の操作と同じ所だが、オンラインとオフラインは同時に無い）</summary>
    static readonly (string phase, Vector2 at, Vector2 pivot)[] StripPlaces =
    {
        ("practice", new Vector2(0.99f, 0.63f), new Vector2(1f, 1f)),
        ("stir",     new Vector2(0.99f, 0.63f), new Vector2(1f, 1f)),
        ("flight",   new Vector2(0.99f, 0.63f), new Vector2(1f, 1f)),
    };
    static readonly Vector2 StripDefaultAt = Vector2.one, StripDefaultPivot = Vector2.one;   // ほかは右上の角（端から ScreenEdge）
    /// <summary>札を出さない段階（タイトルは人がいない。ロビーでは切れた人はサーバーから消える）</summary>
    static readonly HashSet<string> NoStrip = new HashSet<string> { "title", "lobby" };

    // ================================================================== 中身
    enum Link { Idle, Online, Reconnecting, Failed }

    readonly GameFlow flow;
    readonly GameToast toast;
    readonly RectTransform strip, who;
    readonly GameObject scrim, spinner, failIcon, leaveRow;
    readonly TextMeshProUGUI title, sub, status;

    IGameClient client;
    GameModel model;
    Link link;
    float heard, lastPing, since, nextTry, connectAt = -1f, tryAt;
    int attempt;
    string room = "", myName = "", failReason = "";
    bool fromLobby, overlayShown, reseed = true;
    List<string> seatsBefore = new List<string>();
    readonly Dictionary<string, bool> seen = new Dictionary<string, bool>();
    string stripKey;

    public ConnectionUi(Transform parent, GameFlow flow, GameToast toast)
    {
        this.flow = flow; this.toast = toast;
        var root = Ui.Rect("Connection", parent); UiKit.Fill(root);
        var cv = root.gameObject.AddComponent<Canvas>(); cv.overrideSorting = true; cv.sortingOrder = SortingOrder;
        root.gameObject.AddComponent<GraphicRaycaster>();

        // ② ほかの人が切れた札（押せない。後ろの操作を止めない）
        strip = UiKit.Row(UiKit.SafeArea(root), T.S2, TextAnchor.MiddleRight);
        strip.name = "OfflineCrew";
        var fit = strip.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        strip.gameObject.SetActive(false);

        // ① RECONNECTING（暗幕が後ろへの押下を止める）
        var dim = Ui.PanelImg(root, T.Scrim, "Reconnecting"); UiKit.Fill(dim.rectTransform); dim.raycastTarget = true;
        scrim = dim.gameObject;
        var card = UiKit.Panel(UiKit.SafeArea(dim.transform), T.S2, "Card");
        UiKit.Center(card, CardW, 0f);
        card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var head = UiKit.Row(card, T.S3, TextAnchor.MiddleCenter);
        spinner = BuildSpinner(head).gameObject;
        failIcon = BuildFailIcon(head);
        title = UiKit.Text(head, Words.Reconnecting, TextStyle.Heading, T.Ink);
        UiKit.DashedLine(card, T.Line);
        who = UiKit.Row(card, T.S2, TextAnchor.MiddleCenter);
        sub = UiKit.Text(card, "", TextStyle.Body, T.Ink, TextAlignmentOptions.Midline);
        status = UiKit.Text(card, "", TextStyle.Label, T.InkSub, TextAlignmentOptions.Midline);
        var btn = UiKit.Row(card, 0f);
        UiKit.SecondaryButton(btn, Words.BackToTitle, null, BackToTitle);
        leaveRow = btn.gameObject;
        scrim.SetActive(false);
    }

    // ================================================================== 毎フレーム（GameFlow.Update が Toast の後に呼ぶ）
    public void Tick(float now)
    {
        Track();
        var ws = client as WsClient;   // 記録の再生（OfflineClient）は切れないので何もしない
        switch (link)
        {
            case Link.Idle:
                if (ws != null && ws.IsConnected && flow.Model.PlayerId != "") { link = Link.Online; heard = lastPing = now; }
                break;
            case Link.Online:
                if (ws == null) { link = Link.Idle; break; }
                if (now - lastPing >= PingEvery) { lastPing = now; ws.Send(Cmd.Type("ping")); }
                if (!ws.IsConnected || now - heard > SilentAfter) Begin(now);
                break;
            case Link.Reconnecting:
                Retry(now, ws);
                break;
        }
        RefreshOverlay(now);
        WatchCrew();
        RefreshStrip();
    }

    /// <summary>GameFlow が接続を作り直したら（StartOnline・StartOffline）、受信の口を付け替えて最初から</summary>
    void Track()
    {
        if (flow.Client != client)
        {
            if (client != null) { client.OnMessage -= OnMessage; client.OnState -= OnState; client.OnEvent -= OnEvent; }
            client = flow.Client;
            if (client != null) { client.OnMessage += OnMessage; client.OnState += OnState; client.OnEvent += OnEvent; }
            link = Link.Idle; toast.Muted = false; overlayShown = false; connectAt = -1f;
        }
        if (flow.Model != model) { model = flow.Model; reseed = true; }
    }

    // GameFlow が先に受けて Model を直した後に来る（同じ WsClient に GameFlow が先に付けている）
    void OnState(GameState s) => heard = Time.unscaledTime;
    void OnEvent(GameEvent e) => heard = Time.unscaledTime;
    void OnMessage(Dictionary<string, object> m)
    {
        heard = Time.unscaledTime;
        if (link != Link.Reconnecting) return;
        string type = J.Str(m, "type");
        if (type == "welcome") Done(Time.unscaledTime);
        else if (type == "error" && FailText.TryGetValue(J.Str(m, "code"), out var why)) Fail(why);
    }

    // ================================================================== ① つなぎ直す
    void Begin(float now)
    {
        var m = flow.Model;
        link = Link.Reconnecting;
        since = tryAt = now; attempt = 0; nextTry = now + RetryAfter[0]; connectAt = -1f;
        room = m.Room; fromLobby = m.Phase == "lobby";
        seatsBefore = new List<string>(m.MySeats);
        var me = m.Players.Find(p => p.id == m.PlayerId);
        myName = me != null && !string.IsNullOrEmpty(me.name) ? me.name : Words.NoName;   // ロビーで名前で入り直すとき用（切り詰めない）
        overlayShown = false;
        toast.Muted = true;   // 状態の文（Disconnected など）は出さない。カードが代わりに出る
        client.Close();       // 黙って切れたとき（ping が返らない）は、まだ開いていることになっているので閉じる
        BuildWho();
        if (string.IsNullOrEmpty(room)) Fail(FailText["no_such_room"]);
    }

    void Retry(float now, WsClient ws)
    {
        if (ws == null) return;
        if (connectAt >= 0f)
        {
            if (now < connectAt) return;
            connectAt = -1f;
            attempt++; tryAt = now;
            nextTry = now + RetryAfter[Mathf.Min(attempt, RetryAfter.Length - 1)];
            if (fromLobby) PlayerPrefs.DeleteKey(PlayerIdKey + room);   // ロビーではサーバーが人を消している。名前で入り直す
            ws.Connect(room, myName, false);
            return;
        }
        if (now < nextTry) return;
        if (ws.IsConnected && now - tryAt < AttemptTimeout) return;   // つながって welcome を待っている
        ws.Close();
        connectAt = now + CloseGap;
    }

    void Done(float now)
    {
        link = Link.Online; heard = lastPing = now;
        toast.Muted = false;
        reseed = true;   // 切れている間のほかの人の出入りは知らせない（札には出る）
        if (overlayShown)
        {
            var seats = flow.Model.MySeats;   // welcome で GameFlow が直した後
            toast.Show(SameSeats(seatsBefore, seats) ? Words.BackSameSeat : Words.BackOnline, ToastKind.Good, seats);
        }
        overlayShown = false;
    }

    void Fail(string why)
    {
        link = Link.Failed; failReason = why;
        client?.Close();
    }

    /// <summary>タイトルへ戻る。GameFlow に戻す口が無いので、シーン（Game.unity）を読み直す。合言葉の player_id は残るので、同じ部屋に JOIN すれば同じ席に戻れる</summary>
    void BackToTitle()
    {
        client?.Close();
        var sc = SceneManager.GetActiveScene();
        if (sc.buildIndex >= 0) SceneManager.LoadScene(sc.buildIndex); else SceneManager.LoadScene(sc.name);
    }

    void RefreshOverlay(float now)
    {
        bool show = link == Link.Failed || link == Link.Reconnecting && now - since >= OverlayDelay;
        if (scrim.activeSelf != show) scrim.SetActive(show);
        if (!show) return;
        overlayShown = true;
        bool failed = link == Link.Failed;
        spinner.SetActive(!failed);
        failIcon.SetActive(failed);
        title.text = failed ? Words.CantRejoin : Words.Reconnecting;
        sub.text = failed ? failReason : fromLobby ? Words.LobbyBack : Words.SeatSaved;
        bool waiting = connectAt < 0f && !client.IsConnected && nextTry - now > 0.5f;
        status.text = waiting ? string.Format(Words.TryAgainIn, Mathf.CeilToInt(nextTry - now)) : Words.TryingNow;
        status.gameObject.SetActive(!failed);
        leaveRow.SetActive(failed || now - since >= LeaveAfter);
        if (!failed) spinner.transform.localEulerAngles = new Vector3(0f, 0f, -360f / SpinDots * Mathf.Floor(now / SpinStep));
    }

    /// <summary>カードの「ROOM 1234 と自分の席」（切れたときの席で作る）</summary>
    void BuildWho()
    {
        Clear(who);
        UiKit.Tag(who, Words.Room + room, T.Accent, T.Ink);
        foreach (var s in seatsBefore) UiKit.SeatBadge(who, s, T.BadgeS).Set(s, true, false);
    }

    // ================================================================== ②③ ほかの人
    /// <summary>ほかの人の connected が変わったら短く知らせる。段階の始め・自分が戻った直後は黙って覚えるだけ</summary>
    void WatchCrew()
    {
        if (link == Link.Reconnecting || link == Link.Failed) return;   // 自分が切れている間の players は古い
        var m = flow.Model;
        bool quiet = reseed;
        if (reseed) { seen.Clear(); reseed = false; }
        foreach (var p in m.Players)
        {
            if (p.id == m.PlayerId) continue;
            if (!quiet && seen.TryGetValue(p.id, out var was) && was != p.connected)
            {
                var made = toast.Show(string.Format(p.connected ? Words.OtherBack : Words.OtherLost, NameOf(p)),
                                      p.connected ? ToastKind.Good : ToastKind.Info, p.seats);
                if (!p.connected) foreach (var b in made) AddOfflineMark(b.Root);
            }
            seen[p.id] = p.connected;
        }
    }

    /// <summary>切れているほかの人の札。中身が変わったときだけ作り直す</summary>
    void RefreshStrip()
    {
        var m = flow.Model;
        var off = new List<PlayerInfo>();
        if (!NoStrip.Contains(m.Phase))
            foreach (var p in m.Players) if (!p.connected && p.id != m.PlayerId && p.seats.Count > 0) off.Add(p);
        var key = m.Phase;
        foreach (var p in off) key += "|" + p.id + ":" + string.Join(",", p.seats);
        if (key == stripKey) return;
        stripKey = key;

        Clear(strip);
        foreach (var p in off) BuildChip(strip, p);
        strip.gameObject.SetActive(off.Count > 0);
        Vector2 at = StripDefaultAt, pivot = StripDefaultPivot, offset = new Vector2(-T.ScreenEdge, -T.ScreenEdge);
        foreach (var pl in StripPlaces) if (pl.phase == m.Phase) { at = pl.at; pivot = pl.pivot; offset = Vector2.zero; }
        strip.anchorMin = strip.anchorMax = at; strip.pivot = pivot; strip.anchoredPosition = offset;
    }

    /// <summary>1人ぶんの札：その人の席のバッジ（切断の印つき）＋ OFFLINE ＋名前</summary>
    static void BuildChip(Transform p, PlayerInfo who)
    {
        var chip = UiKit.Row(p, T.S2, TextAnchor.MiddleLeft, T.S3, T.S1);
        chip.name = "Chip_" + who.id;
        var sh = UiKit.Pill(chip, T.Shadow, ChipH, "Shadow"); UiKit.Fill(sh.rectTransform); UiKit.Ignore(sh);
        sh.rectTransform.offsetMin = new Vector2(0f, -T.ShadowY); sh.rectTransform.offsetMax = new Vector2(0f, -T.ShadowY);
        var face = UiKit.Pill(chip, T.Panel, ChipH, "Face"); UiKit.Fill(face.rectTransform); UiKit.Ignore(face);
        foreach (var s in who.seats) AddOfflineMark(UiKit.SeatBadge(chip, s, T.BadgeS).Root);
        var col = UiKit.Col(chip, 0f, TextAnchor.MiddleLeft);
        UiKit.Text(col, Words.Offline, TextStyle.Tag, T.Danger, TextAlignmentOptions.MidlineLeft);
        UiKit.Text(col, NameOf(who), TextStyle.Label, T.InkSub, TextAlignmentOptions.MidlineLeft);
    }

    // ================================================================== 部品（あとで UiKitHud に移せるようにここにまとめる）
    /// <summary>席バッジの右下に切断の印（danger の丸に白い ×、panel の縁）を重ねる。席タブ・部屋の名前タグからも使える</summary>
    public static RectTransform AddOfflineMark(RectTransform badge)
    {
        var r = Ui.Rect("OfflineMark", badge);
        r.anchorMin = r.anchorMax = new Vector2(1f, 0f); r.pivot = new Vector2(0.7f, 0.3f);
        r.sizeDelta = new Vector2(MarkD, MarkD); r.anchoredPosition = Vector2.zero;
        var ring = UiKit.Pill(r, T.Panel, MarkD + T.Border * 2f, "Ring"); UiKit.Fill(ring.rectTransform, -T.Border);
        var c = UiKit.Pill(r, T.Danger, MarkD, "Circle"); UiKit.Fill(c.rectTransform);
        var ic = UiKit.Icon(r, "cross", T.IconS, T.Panel); UiKit.Center(ic.rectTransform, T.IconS, T.IconS);
        return r;
    }

    /// <summary>回る点（8つの点。先頭が濃く、後ろほど薄い）。Tick で SpinStep ごとに1つ進める</summary>
    static RectTransform BuildSpinner(Transform p)
    {
        var r = Ui.Rect("Spinner", p);
        UiKit.Fixed(r, T.IconL, T.IconL);
        float dot = T.S2, rad = (T.IconL - dot) * 0.5f;
        for (int i = 0; i < SpinDots; i++)
        {
            float a = (90f + 360f / SpinDots * i) * Mathf.Deg2Rad;   // 上から反時計回りに薄くなる → 時計回りに回すと尾を引く
            var d = UiKit.Pill(r, T.WithAlpha(T.Ink, (SpinDots - i) / (float)SpinDots), dot, "Dot");
            var dr = d.rectTransform;
            dr.anchorMin = dr.anchorMax = dr.pivot = new Vector2(0.5f, 0.5f);
            dr.sizeDelta = new Vector2(dot, dot); dr.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
        }
        return r;
    }

    /// <summary>戻れないときの印（danger の丸に白い ×）</summary>
    static GameObject BuildFailIcon(Transform p)
    {
        var r = Ui.Rect("Fail", p);
        UiKit.Fixed(r, T.IconL, T.IconL);
        var c = UiKit.Pill(r, T.Danger, T.IconL, "Circle"); UiKit.Fill(c.rectTransform);
        var ic = UiKit.Icon(r, "cross", T.IconM, T.Panel); UiKit.Center(ic.rectTransform, T.IconM, T.IconM);
        r.gameObject.SetActive(false);
        return r.gameObject;
    }

    static string NameOf(PlayerInfo p)
    {
        string n = p == null || string.IsNullOrEmpty(p.name) ? Words.NoName : p.name;
        return n.Length > T.NameMaxChars ? n.Substring(0, T.NameMaxChars) + "..." : n;
    }

    static bool SameSeats(List<string> a, List<string> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var s in a) if (!b.Contains(s)) return false;
        return true;
    }

    static void Clear(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--) { var c = t.GetChild(i); c.SetParent(null); Object.Destroy(c.gameObject); }
    }
}
