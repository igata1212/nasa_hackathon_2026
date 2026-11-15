// 書いた人：Claude ／ 最終更新：2026-11-15 ／ 更新：iizuka（作成：Claude、#72 UI ガイドラインの見た目にした。2026-11-14）／ 更新：iizuka（Opus 5.5、2026-11-14、#153） ／ 更新：iizuka（Opus 5.5、2026-11-14、#141 DEV パネルに音のミュート）／ 更新：iizuka（Opus 5.5、2026-11-15、#189 歯車の DEV の文字を消した）／ 更新：iizuka（Opus 5.5、2026-11-15、#190 あつ森の選択肢・ヒントのピル・数のチップ・名前の吹き出し・手）
// Title（名前・合言葉・部屋を作る／入る）と Lobby（合言葉・4人のクルー・開始）。docs/design/ui-guidelines.md の 3章「画面ごとの選択の替え方」。
// 部屋の画面は、席カードの代わりに 4人のクルーの 3D を手前に並べ、頭の上に名前の吹き出しを出す（iizuka の指示、2026-11-14。LobbyStage）。
// 色と大きさは UiTokens、部品は UiKit。開発用（Server URL・記録の再生・音のミュート）は右下のヒントのピル（Settings）で開く板に入れた
// #153：あつ森の方向へ。動く背景（TitleBackdrop）、左右の下から顔を出すクルー（TitleStage）、1文字ずつ弾むロゴ（UiLetterWave）、
// 順に跳ねる席バッジ（UiHop）、次に何をするかを話すヒューストンの吹き出し（UiHintBubble）、押すと縮んで弾むボタン（UiSquish）。
// #190：タイトル＝入力のパネル（【05】）＋選択肢の吹き出し（【10】Create a room／Join with a code。手は次に押す方）＋右下のヒントのピル（【09】）。
//   部屋＝合言葉と人数を数のチップ（【09】）、名前を小さな吹き出し（【08】blob_pill＋丸いしっぽ。LobbyTag）、Start mission! に手（【05】）。
// 動きの値と案内の文は TitleLook の1か所
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;
using L = TitleLook;

public class TitleScreen
{
    public readonly GameObject Root;
    const string NameKey = "ui_player_name";
    const int Create = 0, Join = 1;   // 選択肢の行

    readonly GameFlow f;
    readonly UiInput name, room, url;
    readonly UiChoice choice;
    readonly GameObject joinLock;
    readonly GameObject dev;
    readonly UiHintBubble hint;

    public TitleScreen(Transform parent, GameFlow f)
    {
        this.f = f;
        var bg = Ui.PanelImg(parent, T.Bg, "Title"); Root = bg.gameObject; Ui.Fill(bg.rectTransform);
        TitleBackdrop.Build(bg.rectTransform);                       // 地の大きな柄・模様・星・ロケット・地球と雲（一番奥）
        var crewView = Ui.Rect("Crew", bg.transform).gameObject.AddComponent<RawImage>();
        Ui.Fill(crewView.rectTransform);
        Root.AddComponent<TitleStage>().Init(crewView);              // 左右の下から顔を出すクルー（背景の上、文字の下）
        var safe = UiKit.SafeArea(bg.transform);

        var main = UiKit.Col(safe, 0f, TextAnchor.MiddleCenter, T.ScreenEdge, T.ScreenEdge);
        UiKit.Fill(main);
        // 席バッジ：順にぽんと出て、ときどき順に跳ねる（跳ねる分はレイアウトの外の入れ物の中で動かす）
        var seats = UiKit.Row(main, T.S2);
        for (int i = 0; i < Seats.All.Length; i++)
        {
            var holder = Ui.Rect("Hop_" + Seats.All[i], seats);
            UiKit.Fixed(holder, T.BadgeS, T.BadgeS);
            var badge = UiKit.SeatBadge(holder, Seats.All[i], T.BadgeS);
            UiHop.Add(badge.Root, i * L.HopStagger);
            UiPopIn.Add(holder, L.BadgeDelay + i * L.BadgeStagger, 0f);
        }
        UiKit.Space(main, T.S1);
        var logo = UiKit.Text(main, L.Logo, TextStyle.Title, T.Text);
        logo.gameObject.AddComponent<UiLetterWave>();
        UiPopIn.Add(UiKit.Text(main, L.Subtitle, TextStyle.Body, T.TextSoft), L.SubtitleDelay);
        UiKit.Space(main, L.BubbleGapTop);

        // ヒューストンの吹き出し：今の入力の状態で「次に何をするか」を1つだけ話す。しっぽは名前の欄を指す
        hint = UiHintBubble.Create(main, L.BubbleTitleW, L.BubbleTitleH, UiHintBubble.Tail.Down, L.BubbleTitleTailX, L.BubbleDelay);
        UiKit.Space(main, T.S2);

        // 左：入力のパネル（名前 → 合言葉。【05】）、右：選択肢の吹き出し（Create a room → Join with a code。【10】）。上下の順で対になる
        var form = UiKit.Row(main, L.FormGap, TextAnchor.MiddleCenter);
        form.name = "Form";
        Ui.Size(form, w: L.FormW, flexW: 0f);
        UiPopIn.Add(form, L.FormDelay);
        var fields = UiKit.Panel(form, 0f, "Fields");
        Ui.Size(fields, w: 0f, flexW: 1f);

        name = UiKit.Input(fields, "seat", "Your name", PlayerPrefs.GetString(NameKey, ""));
        name.Field.characterLimit = T.NameMaxChars;
        name.Field.onValueChanged.AddListener(_ => RefreshHint());

        room = UiKit.Input(fields, "room_code", "Room code", "");
        room.Field.contentType = TMP_InputField.ContentType.IntegerNumber;
        room.Field.characterLimit = T.RoomCodeChars;
        room.Field.onValueChanged.AddListener(_ => { RefreshJoin(); RefreshHint(); });
        room.Field.onSubmit.AddListener(_ => { if (CanJoin) Go(false); });

        // 押すとすぐ決まる（今までの2つのボタンと同じ動き）。手は次に押す方（合言葉が4桁そろうまで Create、そろうと Join）
        choice = UiKit.Choice(form, new[] { L.ChoiceCreate, L.ChoiceJoin }, i => Go(i == Create), Create);
        Ui.Size(choice.Root, flexW: 0f);   // 中身の幅で止める（Row の中で伸びてクルーにかぶるのを防ぐ）
        for (int i = 0; i < choice.Rows.Length; i++)
        {
            // 押すと縮んで弾む。押せない Join を押したら首を振って、ヒューストンが理由を話す
            var row = new UiButton { Root = choice.Rows[i], Button = choice.Buttons[i] };
            UiKit.Breathe(row, 0f, i == Join ? () => hint.Say(L.Rich(L.HintNeedCode)) : (System.Action)null);
        }
        // 押せない物の印：Join の右上に鍵の台（ui-guidelines 3章「選ぶ」の押せない物）
        var lk = UiKit.Disc(choice.Rows[Join], "lock", T.TextSoft, T.CheckD);
        lk.Root.anchorMin = lk.Root.anchorMax = Vector2.one; lk.Root.pivot = new Vector2(0.5f, 0.5f);
        lk.Root.anchoredPosition = new Vector2(L.LockOffsetX, -T.S2);
        joinLock = lk.Root.gameObject;
        // 手（画面の Root の直下）は、選択肢がふわっと出るまで見せない
        UiAlphaLink.Add(UiSelect.CursorOf(choice.Root), form);
        RefreshJoin();
        RefreshHint();

        // 開発用：右下のヒントのピル（【09】）。#189（Fable）：「DEV」の文字は出さない（デモの画面に映る）。開くと Developer の板
        var corner = UiKit.Row(safe, T.S2, TextAnchor.LowerRight);
        corner.name = "Hints";
        corner.anchorMin = corner.anchorMax = corner.pivot = new Vector2(1f, 0f);
        corner.anchoredPosition = new Vector2(-T.ScreenEdge, T.S2);
        var cf = corner.gameObject.AddComponent<ContentSizeFitter>();
        cf.horizontalFit = cf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        UiKit.Hint(corner, "settings", L.Settings, OpenDev);
        UiPopIn.Add(corner, L.FormDelay);
        dev = BuildDev(bg.transform, out url);
        dev.SetActive(false);
    }

    /// <summary>吹き出しの文：合言葉を打っている途中 → 4桁そろった → 名前が空 → 名前あり の順に見て、1つだけ</summary>
    void RefreshHint()
    {
        int code = room.Text.Length;
        string s = code >= T.RoomCodeChars ? L.HintJoin
                 : code > 0 ? L.HintCode
                 : name.Text.Trim().Length == 0 ? L.HintName
                 : L.HintCreate;
        hint.Say(L.Rich(s));
    }

    bool CanJoin => room.Text.Length == T.RoomCodeChars;

    /// <summary>Join は4桁そろうまで押せない見た目（文字 line＋鍵の台）。手とマーカーは次に押す方へ</summary>
    void RefreshJoin()
    {
        bool can = CanJoin;
        choice.SetEnabled(Join, can);
        joinLock.SetActive(!can);
        choice.Select(can ? Join : Create);
    }

    void Go(bool create)
    {
        PlayerPrefs.SetString(NameKey, name.Text);
        f.StartOnline(url.Text, name.Text, create ? "" : room.Text, create);
    }

    void OpenDev()
    {
        dev.SetActive(true);
        dev.transform.SetAsLastSibling();   // 手（画面の Root の直下）より手前に
    }

    GameObject BuildDev(Transform parent, out UiInput urlField)
    {
        var scrim = Ui.PanelImg(parent, T.Scrim, "Dev"); Ui.Fill(scrim.rectTransform);
        var close = scrim.gameObject.AddComponent<Button>();
        close.transition = Selectable.Transition.None;
        close.onClick.AddListener(() => scrim.gameObject.SetActive(false));

        var panel = UiKit.Panel(scrim.transform, T.S2, "DevPanel");
        UiKit.Center(panel, T.DevPanelW, 0f);
        panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var head = UiKit.Row(panel, T.S2, TextAnchor.MiddleLeft);
        Ui.Size(UiKit.Text(head, "Developer", TextStyle.RowName, T.Text, TextAlignmentOptions.MidlineLeft), flexW: 1f);
        UiKit.IconButton(head, "cross", null, () => scrim.gameObject.SetActive(false), T.Bg);
        UiKit.DashedLine(panel, T.BgDeep);

        UiKit.Text(panel, "Server URL", TextStyle.Label, T.TextSoft, TextAlignmentOptions.MidlineLeft);
        urlField = UiKit.Input(panel, "comms", "ws://host:port", f.wsUrl);
        UiKit.Text(panel, "Replay a recorded game (no server)", TextStyle.Label, T.TextSoft, TextAlignmentOptions.MidlineLeft);
        UiKit.SecondaryButton(panel, "Replay: success (T1)", "check", () => f.StartOffline(false));
        UiKit.SecondaryButton(panel, "Replay: failure (T4)", "cross", () => f.StartOffline(true));

        // 音のミュート（#141。SoundPrefs に残る。チェック＝鳴る、バツ＝ミュート）
        UiKit.Text(panel, "Sound (tap to mute)", TextStyle.Label, T.TextSoft, TextAlignmentOptions.MidlineLeft);
        var snd = UiKit.Row(panel, T.S2, TextAnchor.MiddleLeft);
        MuteButton(snd, SoundBus.Music, "Music");
        MuteButton(snd, SoundBus.Sfx, "SFX");
        MuteButton(snd, SoundBus.Voice, "Voice");
        return scrim.gameObject;
    }

    static void MuteButton(Transform parent, SoundBus bus, string label)
    {
        UiButton b = null;
        void Draw()
        {
            var sp = T.Icon(SoundPrefs.Muted(bus) ? "cross" : "check");
            b.Icon.sprite = sp; b.iconOn = sp;
            b.Icon.gameObject.SetActive(sp != null);
            UiKit.FitWidth(b);
        }
        b = UiKit.SecondaryButton(parent, label, "check", () => { SoundPrefs.SetMuted(bus, !SoundPrefs.Muted(bus)); Draw(); });
        Draw();
    }
}

public class LobbyScreen
{
    public readonly GameObject Root;
    readonly GameFlow f;
    readonly UiCounter roomCode, count;
    readonly LobbyTag[] tags = new LobbyTag[Seats.All.Length];
    readonly LobbyStage stage;
    readonly UiHintBubble hint;

    public LobbyScreen(Transform parent, GameFlow f)
    {
        this.f = f;
        // 後ろ：4人のクルーの 3D（LobbyStage が RenderTexture に描く）
        var view = Ui.Rect("Lobby", parent).gameObject.AddComponent<RawImage>();
        Root = view.gameObject; Ui.Fill(view.rectTransform);
        view.color = T.StageBg; view.raycastTarget = true;   // RenderTexture ができるまでは背景の色（LobbyStage が白に戻す）

        // 頭の上の名前の吹き出し（席の順）
        var tagLayer = Ui.Rect("Tags", view.transform); Ui.Fill(tagLayer);
        var tagRects = new RectTransform[tags.Length];
        for (int i = 0; i < tags.Length; i++) { tags[i] = LobbyTag.Create(tagLayer, Seats.All[i]); tagRects[i] = tags[i].Root; }

        // 上：合言葉と人数の数のチップ（【09】。宇宙の上なので面は accent、上にアイコンの台）。下：Start mission!。クルーを隠さないように上下に寄せる
        var main = UiKit.Col(UiKit.SafeArea(view.transform), T.S3, TextAnchor.UpperCenter, T.ScreenEdge, T.ScreenEdge, true);
        UiKit.Fill(main);
        var head = UiKit.Row(main, T.S4 + T.S2, TextAnchor.MiddleLeft);
        roomCode = UiKit.Counter(head, "room_code", L.RoomUnit, T.Accent);
        roomCode.Disc.Set("room_code", T.Go);                  // 台は面と違う色に（同じ accent だと面に溶ける）
        UiPopIn.Add(roomCode.Root, L.LobbyChipDelay);
        // ヒューストンの吹き出し（しっぽは左の合言葉を指す）。人数で「次に何をするか」を1つだけ話す
        hint = UiHintBubble.Create(head, L.BubbleLobbyW, L.BubbleLobbyH, UiHintBubble.Tail.Left, 0f, L.LobbyBubbleDelay);
        Ui.Size(UiKit.Space(head, 0f), flexW: 1f);
        count = UiKit.Counter(head, "crew", L.CrewUnit, T.Accent);
        count.Disc.Set("crew", T.Plum);
        UiPopIn.Add(count.Root, L.LobbyChipDelay);

        UiKit.Space(main, 0f, true);
        var bottom = UiKit.Row(main, 0f);
        var start = UiKit.PrimaryButton(bottom, L.StartMission, "rocket", () => f.Client.Send(Cmd.Type("start")));
        UiKit.Breathe(start, L.BreatheAmp);
        UiPopIn.Add(bottom, L.LobbyStartDelay);
        // 手は主ボタンの右下（【05】）。ボタンがふわっと出るまで見せない
        UiSelect.Cursor(start.Root, CursorDir.UpLeft);
        UiAlphaLink.Add(UiSelect.CursorOf(start.Root), bottom);

        stage = Root.AddComponent<LobbyStage>();
        stage.Init(view, tagRects);
    }

    public void Refresh()
    {
        roomCode.Set(f.Model.Room);
        count.Set(f.Model.Players.Count + " / " + Seats.All.Length);
        for (int i = 0; i < tags.Length; i++)
        {
            string seat = Seats.All[i];
            PlayerInfo who = null;
            foreach (var p in f.Model.Players) if (p.seats.Contains(seat)) { who = p; break; }
            bool self = who != null && who.id == f.Model.PlayerId;
            string main = who != null && who.seats.Count > 0 && who.seats[0] != seat ? who.seats[0] : null;
            tags[i].Set(who == null ? null : string.IsNullOrEmpty(who.name) ? "Player" : who.name, self, main);
            stage.SetSeat(i, who != null, self);
        }
        int n = f.Model.Players.Count;
        hint.Say(L.Rich(n >= Seats.All.Length ? L.LobbyFull : n >= 2 ? L.LobbySome : L.LobbySolo));
    }
}

/// <summary>部屋の画面のクルーの頭の上の名前の吹き出し（#190。【08】小さな吹き出し：blob_pill＋下に小さな丸のしっぽ2つ。影なし）。
/// 席バッジ（色＋形）＋名前（＋You）＋席の名前＋席の仕事。自分（go の輪＋You）・ほかの人・空席（薄い面＋点線の枠＋Waiting）・兼任（主の席の小さなバッジ＋Covering）を、色に頼らず見分けられる。
/// Root の下の真ん中（しっぽの先）を頭の上に合わせる（LobbyStage）</summary>
public class LobbyTag
{
    public string Seat;
    public RectTransform Root, You;
    public Image Ring, Face;
    public Image[] Tail;
    public UiDashed Dashed;
    public UiSeatBadge Badge, Mini;
    public TextMeshProUGUI Name, Sub, Job, Note;

    public static LobbyTag Create(Transform p, string seat)
    {
        var c = new LobbyTag { Seat = seat };
        c.Root = UiKit.Col(p, 0f, TextAnchor.LowerCenter);
        c.Root.name = "Tag_" + seat;
        c.Root.pivot = new Vector2(0.5f, 0f);
        var fit = c.Root.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ピル：輪（自分）→ 面 → 点線（空席）→ 中身
        var pill = UiKit.Row(c.Root, T.S2, TextAnchor.MiddleLeft, L.TagPadX, L.TagPadY);
        pill.name = "Pill";
        float r = L.TagH * 0.5f;
        c.Ring = UiKit.Blob(pill, T.BlobPill, T.Go, r + L.TagRing, "Ring"); UiKit.Fill(c.Ring.rectTransform, -L.TagRing); UiKit.Ignore(c.Ring);
        c.Face = UiKit.Blob(pill, T.BlobPill, T.Panel, r, "Face"); UiKit.Fill(c.Face.rectTransform); UiKit.Ignore(c.Face);
        c.Dashed = UiKit.DashedFrame(pill, r, T.HudSub);

        c.Badge = UiKit.SeatBadge(pill, seat, T.BadgeS);
        var col = UiKit.Col(pill, 0f, TextAnchor.MiddleLeft);
        var who = UiKit.Row(col, T.S1, TextAnchor.MiddleLeft);
        c.Mini = UiKit.SeatBadge(who, seat, T.TagH);
        c.Name = UiKit.Text(who, "", TextStyle.RowName, T.Text, TextAlignmentOptions.MidlineLeft, false);
        c.Name.overflowMode = TextOverflowModes.Ellipsis;
        c.You = UiKit.Tag(who, L.You, T.Go, T.Panel);
        c.Sub = UiKit.Text(col, "", TextStyle.Label, T.TextSoft, TextAlignmentOptions.MidlineLeft);
        c.Job = UiKit.Text(col, "", TextStyle.Label, T.TextSoft, TextAlignmentOptions.MidlineLeft);   // 席の仕事（#189）
        c.Note = UiKit.Text(col, L.Covering, TextStyle.Label, T.TextSoft, TextAlignmentOptions.MidlineLeft);

        // しっぽ：ピルの下に小さな丸2つ（大 → 小。【08】）。Root の下の端（しっぽの先）までの高さを空ける
        var tail = Ui.Rect("Tail", c.Root);
        UiKit.Fixed(tail, 0f, L.TagTailH);
        c.Tail = new Image[L.TagTailD.Length];
        for (int i = 0; i < c.Tail.Length; i++)
        {
            float d = L.TagTailD[i];
            var dot = UiKit.Blob(tail, T.BlobDisc, T.Panel, d * 0.5f, "Dot" + i);
            var dr = dot.rectTransform;
            dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 1f); dr.pivot = new Vector2(0.5f, 1f);
            dr.sizeDelta = new Vector2(d, d); dr.anchoredPosition = L.TagTailAt[i];
            c.Tail[i] = dot;
        }
        c.Set(null, false, null);
        return c;
    }

    /// <param name="player">座っている人の名前。null なら空席</param>
    /// <param name="mainSeat">兼任のとき、その人の主の席。主の席なら null</param>
    public void Set(string player, bool self, string mainSeat)
    {
        bool open = player == null;
        Ring.enabled = self;
        Face.color = open ? T.WithAlpha(T.Panel, L.TagOpenAlpha) : T.Panel;
        foreach (var d in Tail) d.color = Face.color;
        Dashed.enabled = open;
        Badge.Set(Seat, self, open);
        Mini.Root.gameObject.SetActive(mainSeat != null);
        if (mainSeat != null) Mini.Set(mainSeat, false, false);
        if (player != null && player.Length > T.NameMaxChars) player = player.Substring(0, T.NameMaxChars) + "…";   // 隣の吹き出しとぶつからないように
        Name.text = open ? L.Waiting : player;
        // 空席は面が薄い（宇宙が透ける）ので、文字は暗い面の色（hudText／hudSub）
        Name.color = open ? T.HudText : T.Text;
        Sub.color = Job.color = Note.color = open ? T.HudSub : T.TextSoft;
        You.gameObject.SetActive(self);
        Sub.text = Seats.Title(Seat);
        Job.text = Seats.Job(Seat);
        // 3行目：ふだんは席の仕事（#189）、兼任のときは Covering this seat too（横に広げると隣とぶつかる。4行にすると 844×390 で上のチップにかぶる）
        Job.gameObject.SetActive(mainSeat == null);
        Note.gameObject.SetActive(mainSeat != null);
    }
}
