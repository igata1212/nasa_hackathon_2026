// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// 作る Issue：#132（GameFlow の Toast が黄色の文字で、クリームのタイトルの上で読めない）
//
// 画面の短いお知らせ（Toast）。影つきのピルを1枚、ShowSeconds で消える（新しいものが来たら差し替え）。
// 色は ui-guidelines 2章：ふだん（Info）は panel の面に ink の文字／うまくいった（Good）は panel の面に go のチェック／
// エラー（Error）は danger の面に panel の文字と warning のアイコン。席のバッジを左に並べられる（#94 の「Ken lost connection」など）
// GameFlow.Toast(s) が呼ぶ。種類を省くと（Auto）、文の頭で決める（下の AutoKinds。WsClient の状態の文と GameFlow の "ERROR: code detail"）。
// サーバーのエラーは code を英語の短い文に替える（detail は日本語のことがあるので画面に出さない。Console には出す）
// 文言・時間・置き場所は下の「決めること」の1か所
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;

public enum ToastKind { Auto, Info, Good, Error }

public class GameToast
{
    // ================================================================== 決めること（直すときはここだけ）
    public const float ShowSeconds = 3f;
    /// <summary>StoryDirector の演出（50）と接続の表示（ConnectionUi、60）より上</summary>
    public const int SortingOrder = 61;
    const float Height = T.SecondaryH;   // ピルの高さ（副ボタンと同じ 72）

    /// <summary>種類を省いたとき、文の頭で決める（WsClient の OnStatus の文・GameFlow の "ERROR: …"）</summary>
    static readonly (string head, ToastKind kind)[] AutoKinds =
    {
        ("ERROR", ToastKind.Error), ("Connection error", ToastKind.Error), ("Send error", ToastKind.Error),
        ("Disconnected", ToastKind.Error), ("WebSocket is not supported", ToastKind.Error),
        ("Connected", ToastKind.Good),
    };

    /// <summary>サーバーのエラーの code → 画面の文（server/README.md のエラーの code）。無い code は Unknown の文</summary>
    static readonly Dictionary<string, string> ErrorText = new Dictionary<string, string>
    {
        ["no_such_room"] = "No room with that code.",
        ["room_full"] = "That room is full (4 crew max).",
        ["game_started"] = "That mission has already started.",
        ["unknown_player"] = "Your seat is not in this room anymore.",
        ["seat_taken"] = "That seat is already taken.",
        ["no_free_seat"] = "No free seat left.",
        ["not_your_seat"] = "That is another crew member's control.",
        ["seat_not_allowed"] = "Your seat can't do that.",
        ["locked"] = "Not yet. Wait for Houston.",
        ["wrong_phase"] = "Not now.",
        ["not_in_room"] = "Join a room first.",
    };
    const string UnknownError = "Something went wrong ({0}).";

    /// <summary>置き場所（段階 → 画面に対する位置と、ピルのどこを合わせるか）。船内はタイムラインの帯（上）・同時押しの合図・席のパネル（下）を隠さない高さ</summary>
    static readonly (string phase, Vector2 at, Vector2 pivot)[] Places =
    {
        ("practice", new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.5f)),
        ("stir",     new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.5f)),
        ("flight",   new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.5f)),
    };
    static readonly Vector2 DefaultAt = new Vector2(0.5f, 1f), DefaultPivot = new Vector2(0.5f, 1f);   // ほかは上の真ん中（端から ScreenEdge）

    // ================================================================== 中身
    readonly GameFlow flow;
    readonly RectTransform root, pill, badges;
    readonly Image face, icon;
    readonly TextMeshProUGUI text;
    float until;
    bool muted;

    /// <summary>true の間は出さない（ConnectionUi がつなぎ直している間。RECONNECTING の画面が代わりに出る）</summary>
    public bool Muted { get => muted; set { muted = value; if (value) Hide(); } }

    public GameToast(Transform parent, GameFlow flow)
    {
        this.flow = flow;
        root = Ui.Rect("Toast", parent); UiKit.Fill(root);
        var cv = root.gameObject.AddComponent<Canvas>(); cv.overrideSorting = true; cv.sortingOrder = SortingOrder;
        var safe = UiKit.SafeArea(root);

        pill = UiKit.Row(safe, T.S2, TextAnchor.MiddleCenter, T.S4);
        pill.name = "Pill";
        var fit = pill.gameObject.AddComponent<ContentSizeFitter>(); fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        var sh = UiKit.Pill(pill, T.Shadow, Height, "Shadow"); UiKit.Fill(sh.rectTransform); UiKit.Ignore(sh);
        sh.rectTransform.offsetMin = new Vector2(0f, -T.ShadowY); sh.rectTransform.offsetMax = new Vector2(0f, -T.ShadowY);
        face = UiKit.Pill(pill, T.Panel, Height, "Face"); UiKit.Fill(face.rectTransform); UiKit.Ignore(face);
        icon = UiKit.Icon(pill, null, T.IconM, T.Ink);
        badges = UiKit.Row(pill, T.S1);
        text = UiKit.Text(pill, "", TextStyle.Body, T.Ink, TextAlignmentOptions.MidlineLeft);
        root.gameObject.SetActive(false);
    }

    /// <summary>お知らせを出す。seats を渡すと左に席のバッジを並べる。作ったバッジを返す（#94 が切断の印を重ねる）</summary>
    public List<UiSeatBadge> Show(string s, ToastKind kind = ToastKind.Auto, IEnumerable<string> seats = null)
    {
        var made = new List<UiSeatBadge>();
        if (muted || string.IsNullOrEmpty(s)) return made;
        if (kind == ToastKind.Auto) kind = KindOf(s);
        if (s.StartsWith("ERROR")) s = Friendly(s);

        bool error = kind == ToastKind.Error;
        face.color = error ? T.Danger : T.Panel;
        text.color = error ? T.Panel : T.Ink;
        string iconName = error ? "danger" : kind == ToastKind.Good ? "check" : null;
        icon.sprite = iconName != null ? T.Icon(iconName) : null;
        icon.color = error ? T.Panel : T.Go;
        icon.gameObject.SetActive(icon.sprite != null);

        for (int i = badges.childCount - 1; i >= 0; i--) { var c = badges.GetChild(i); c.SetParent(null); Object.Destroy(c.gameObject); }
        if (seats != null) foreach (var seat in seats) made.Add(UiKit.SeatBadge(badges, seat, T.TagH));
        badges.gameObject.SetActive(made.Count > 0);

        text.text = s;
        Place(flow.Model.Phase);
        root.gameObject.SetActive(true);
        until = Time.unscaledTime + ShowSeconds;
        return made;
    }

    public void Hide() => root.gameObject.SetActive(false);

    /// <summary>毎フレーム（GameFlow.Update）。時間が来たら消す</summary>
    public void Tick(float now)
    {
        if (root.gameObject.activeSelf && now > until) Hide();
    }

    void Place(string phase)
    {
        Vector2 at = DefaultAt, pivot = DefaultPivot, offset = new Vector2(0f, -T.ScreenEdge);
        foreach (var p in Places) if (p.phase == phase) { at = p.at; pivot = p.pivot; offset = Vector2.zero; }
        pill.anchorMin = pill.anchorMax = at; pill.pivot = pivot;
        pill.anchoredPosition = offset;
        pill.sizeDelta = new Vector2(pill.sizeDelta.x, Height);
    }

    static ToastKind KindOf(string s)
    {
        foreach (var k in AutoKinds) if (s.StartsWith(k.head)) return k.kind;
        return ToastKind.Info;
    }

    /// <summary>"ERROR: code detail"（GameFlow.OnMessage）→ 英語の短い文。元の文は Console に</summary>
    static string Friendly(string s)
    {
        Debug.Log("[Toast] " + s);
        var rest = s.Substring("ERROR".Length).TrimStart(':', ' ');
        int sp = rest.IndexOf(' ');
        string code = sp >= 0 ? rest.Substring(0, sp) : rest;
        return ErrorText.TryGetValue(code, out var t) ? t : string.Format(UnknownError, code);
    }
}
