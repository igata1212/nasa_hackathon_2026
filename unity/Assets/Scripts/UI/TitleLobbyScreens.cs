// 書いた人：Claude ／ 最終更新：2026-11-15 ／ 更新：iizuka（作成：Claude、#72 UI ガイドラインの見た目にした。2026-11-14）／ 更新：iizuka（Opus 5.5、2026-11-14、#153） ／ 更新：iizuka（Opus 5.5、2026-11-14、#141 DEV パネルに音のミュート）／ 更新：iizuka（Opus 5.5、2026-11-15、#189 歯車の DEV の文字を消した）
// Title（名前・合言葉・部屋を作る／入る）と Lobby（合言葉・4人のクルー・開始）。docs/design/ui-guidelines.md の 6章のワイヤーフレーム。
// 部屋の画面は、席カードの代わりに 4人のクルーの 3D を手前に並べ、頭の上に名前のタグを出す（iizuka の指示、2026-11-14。LobbyStage）。
// 色と大きさは UiTokens、部品は UiKit。開発用（Server URL・記録の再生）は右上の歯車で開くパネルに入れた
// #153：あつ森の方向へ。動く背景（TitleBackdrop）、左右の下から顔を出すクルー（TitleStage）、1文字ずつ弾むロゴ（UiLetterWave）、
// 順に跳ねる席バッジ（UiHop）、次に何をするかを話すヒューストンの吹き出し（UiHintBubble）、押すと縮んで弾むボタン（UiSquish）。
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

    readonly GameFlow f;
    readonly UiInput name, room, url;
    readonly UiButton join;
    readonly GameObject dev;
    readonly UiHintBubble hint;

    public TitleScreen(Transform parent, GameFlow f)
    {
        this.f = f;
        var bg = Ui.PanelImg(parent, T.Bg, "Title"); Root = bg.gameObject; Ui.Fill(bg.rectTransform);
        TitleBackdrop.Build(bg.rectTransform);                       // 模様・星・ロケット・地球と雲（一番奥）
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
        UiKit.Space(main, T.S3);
        var logo = UiKit.Text(main, "MISSION RETURN", TextStyle.Title, T.Ink);
        logo.gameObject.AddComponent<UiLetterWave>();
        UiPopIn.Add(UiKit.Text(main, "Bring the crew home. Together.", TextStyle.Body, T.InkSub), L.SubtitleDelay);
        UiKit.Space(main, L.BubbleGapTop);

        // ヒューストンの吹き出し：今の入力の状態で「次に何をするか」を1つだけ話す。しっぽは名前の欄を指す
        hint = UiHintBubble.Create(main, L.BubbleTitleW, L.BubbleTitleH, UiHintBubble.Tail.Down, L.BubbleTitleTailX, L.BubbleDelay);
        UiKit.Space(main, T.S3);

        // 名前と合言葉。左の列（名前 → 部屋を作る）と右の列（合言葉 → 入る）
        var form = UiKit.Panel(main, T.S2, "Form");
        Ui.Size(form, w: T.TitleFormW, flexW: 0f);
        UiPopIn.Add(form, L.FormDelay);
        var cols = UiKit.Row(form, T.S3, TextAnchor.UpperCenter);
        var left = UiKit.Col(cols, T.S2, TextAnchor.UpperCenter, 0f, 0f, true);
        var right = UiKit.Col(cols, T.S2, TextAnchor.UpperCenter, 0f, 0f, true);
        foreach (var c in new[] { left, right }) Ui.Size(c, w: 0f, flexW: 1f);

        name = UiKit.Input(left, "seat", "Your name", PlayerPrefs.GetString(NameKey, ""));
        name.Field.characterLimit = T.NameMaxChars;
        name.Field.onValueChanged.AddListener(_ => RefreshHint());
        UiSquish.Add(UiKit.PrimaryButton(left, "CREATE ROOM", "create", () => Go(true)), L.BreatheAmp);

        room = UiKit.Input(right, "room_code", "Room code", "");
        room.Field.contentType = TMP_InputField.ContentType.IntegerNumber;
        room.Field.characterLimit = T.RoomCodeChars;
        room.Field.onValueChanged.AddListener(_ => { RefreshJoin(); RefreshHint(); });
        room.Field.onSubmit.AddListener(_ => { if (CanJoin) Go(false); });
        join = UiKit.SecondaryButton(right, "JOIN", "enter", () => Go(false));
        UiSquish.Add(join, 0f, () => hint.Say(L.Rich(L.HintNeedCode)));   // 鍵のかかった JOIN を押したら、首を振って理由を話す
        RefreshJoin();
        RefreshHint();

        // 開発用（右上の歯車）。#189（Fable）：下の「DEV」の文字は出さない（デモの画面に映る）。開くと DEVELOPER の板
        var gear = UiKit.IconButton(safe, "settings", null, () => dev.SetActive(true));
        gear.Root.anchorMin = gear.Root.anchorMax = gear.Root.pivot = Vector2.one;
        gear.Root.anchoredPosition = new Vector2(-T.ScreenEdge, -T.ScreenEdge);
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
    void RefreshJoin() => join.SetEnabled(CanJoin);

    void Go(bool create)
    {
        PlayerPrefs.SetString(NameKey, name.Text);
        f.StartOnline(url.Text, name.Text, create ? "" : room.Text, create);
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
        Ui.Size(UiKit.Text(head, "DEVELOPER", TextStyle.Sub, T.Ink, TextAlignmentOptions.MidlineLeft), flexW: 1f);
        UiKit.IconButton(head, "cross", null, () => scrim.gameObject.SetActive(false), T.Bg);
        UiKit.DashedLine(panel, T.Line);

        UiKit.Text(panel, "Server URL", TextStyle.Label, T.InkSub, TextAlignmentOptions.MidlineLeft);
        urlField = UiKit.Input(panel, "comms", "ws://host:port", f.wsUrl);
        UiKit.Text(panel, "Replay a recorded game (no server)", TextStyle.Label, T.InkSub, TextAlignmentOptions.MidlineLeft);
        UiKit.SecondaryButton(panel, "REPLAY: SUCCESS (T1)", "check", () => f.StartOffline(false));
        UiKit.SecondaryButton(panel, "REPLAY: FAILURE (T4)", "cross", () => f.StartOffline(true));

        // 音のミュート（#141。SoundPrefs に残る。チェック＝鳴る、バツ＝ミュート）
        UiKit.Text(panel, "Sound (tap to mute)", TextStyle.Label, T.InkSub, TextAlignmentOptions.MidlineLeft);
        var snd = UiKit.Row(panel, T.S2, TextAnchor.MiddleLeft);
        MuteButton(snd, SoundBus.Music, "MUSIC");
        MuteButton(snd, SoundBus.Sfx, "SFX");
        MuteButton(snd, SoundBus.Voice, "VOICE");
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
    readonly TextMeshProUGUI roomCode, count;
    readonly UiCrewTag[] tags = new UiCrewTag[Seats.All.Length];
    readonly LobbyStage stage;
    readonly UiHintBubble hint;

    public LobbyScreen(Transform parent, GameFlow f)
    {
        this.f = f;
        // 後ろ：4人のクルーの 3D（LobbyStage が RenderTexture に描く）
        var view = Ui.Rect("Lobby", parent).gameObject.AddComponent<RawImage>();
        Root = view.gameObject; Ui.Fill(view.rectTransform);
        view.color = T.StageBg; view.raycastTarget = true;   // RenderTexture ができるまでは背景の色（LobbyStage が白に戻す）

        // 頭の上の名前のタグ（席の順）
        var tagLayer = Ui.Rect("Tags", view.transform); Ui.Fill(tagLayer);
        var tagRects = new RectTransform[tags.Length];
        for (int i = 0; i < tags.Length; i++) { tags[i] = UiKit.CrewTag(tagLayer, Seats.All[i]); tagRects[i] = tags[i].Root; }

        // 上：合言葉のチップ（accent）と人数。下：START。クルーを隠さないように上下に寄せる
        var main = UiKit.Col(UiKit.SafeArea(view.transform), T.S3, TextAnchor.UpperCenter, T.ScreenEdge, T.ScreenEdge, true);
        UiKit.Fill(main);
        var head = UiKit.Row(main, T.S3, TextAnchor.MiddleLeft);
        var chip = UiKit.Chip(head, T.Accent, T.ChipH, "RoomChip");
        UiKit.Icon(chip, "room_code", T.IconM, T.Ink);
        UiKit.Text(chip, "ROOM", TextStyle.Sub, T.Ink);
        roomCode = UiKit.Text(chip, "", TextStyle.Big, T.Ink);
        UiPopIn.Add(chip, L.LobbyChipDelay);
        // ヒューストンの吹き出し（しっぽは左の合言葉を指す）。人数で「次に何をするか」を1つだけ話す
        hint = UiHintBubble.Create(head, L.BubbleLobbyW, L.BubbleLobbyH, UiHintBubble.Tail.Left, 0f, L.LobbyBubbleDelay);
        Ui.Size(UiKit.Space(head, 0f), flexW: 1f);
        var people = UiKit.Chip(head, T.Panel, T.ChipH, "People");
        UiKit.Icon(people, "crew", T.IconM, T.Ink);
        count = UiKit.Text(people, "", TextStyle.Number, T.Ink);

        UiKit.Space(main, 0f, true);
        var bottom = UiKit.Row(main, 0f);
        var start = UiKit.PrimaryButton(bottom, "START MISSION", "rocket", () => f.Client.Send(Cmd.Type("start")));
        UiSquish.Add(start, L.BreatheAmp);
        UiPopIn.Add(bottom, L.LobbyStartDelay);

        stage = Root.AddComponent<LobbyStage>();
        stage.Init(view, tagRects);
    }

    public void Refresh()
    {
        roomCode.text = f.Model.Room;
        count.text = f.Model.Players.Count + " / " + Seats.All.Length;
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
