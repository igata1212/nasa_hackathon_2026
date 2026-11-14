// 書いた人：iizuka（作成：Claude）／ 最終更新：2026-11-14
// UI ガイドライン 3章の部品（#72 のタイトルと部屋で使うもの）。コードで組む UGUI ＋ TextMeshPro。
// 色・大きさはすべて UiTokens から読む（ここに色コードや px を書かない）。Ui.cs（legacy Text の骨組み）は変えない。
// 影つきの部品は「Shadow（下に ShadowY ずらした同じ形）＋ Body（面と中身）」の2枚。押すと UiPress が Body を下げ、影が縮む
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;

public enum TextStyle { Title, Heading, Sub, Big, Number, Tag, Body, Label }

/// <summary>UiKit のボタン（主・副・アイコン）。押せない見た目への切り替えと、文字の差し替えができる</summary>
public class UiButton
{
    public RectTransform Root, Body;
    public Button Button;
    public Image Face, Frame, Icon;
    public TextMeshProUGUI Label;
    internal Color faceOn, fgOn, frameOn;
    internal Sprite iconOn;
    internal float minW = -1;

    /// <summary>押せないとき：面は line（枠つきは panel ＋ line の枠）、文字は inkSub、アイコンは lock</summary>
    public void SetEnabled(bool on)
    {
        Button.interactable = on;
        Face.color = on ? faceOn : Frame != null ? T.Panel : T.Line;
        if (Frame != null) Frame.color = on ? frameOn : T.Line;
        if (Label != null) Label.color = on ? fgOn : T.InkSub;
        if (Icon != null)
        {
            Icon.sprite = on ? iconOn : T.Icon("lock");
            Icon.color = on ? fgOn : T.InkSub;
            Icon.gameObject.SetActive(Icon.sprite != null);
        }
        UiKit.FitWidth(this);
    }

    public void SetText(string s) { Label.text = s; UiKit.FitWidth(this); }
}

/// <summary>入力欄（角丸 16・高さ 72、panel に line の枠。入力中は枠が go）</summary>
public class UiInput
{
    public RectTransform Root;
    public TMP_InputField Field;
    public Image Frame;
    public string Text => Field.text;
}

/// <summary>席バッジ：席の色の円に形（▲●■◆）。自分の席は外側に ink の輪</summary>
public class UiSeatBadge
{
    public RectTransform Root;
    public Image Ring, Circle;
    public TextMeshProUGUI Shape;

    public void Set(string seat, bool self, bool open)
    {
        Ring.enabled = self;
        Circle.color = open ? T.Line : T.SeatColor(seat);
        Shape.text = Seats.Icon(seat);
        Shape.color = open ? T.Panel : T.OnSeat(seat);
    }
}

/// <summary>クルーの頭の上の名前のタグ（部屋の画面）。自分（ink の輪＋YOU）・ほかの人・空席（WAITING・点線の枠）・兼任（主の席の小さなバッジ＋covering）を、色に頼らず見分けられる</summary>
public class UiCrewTag
{
    public string Seat;
    public RectTransform Root, You;
    public Image Ring, Shadow, Face;
    public UiDashed Dashed;
    public UiSeatBadge Badge, Mini;
    public TextMeshProUGUI Name, Sub, Note;

    /// <param name="player">座っている人の名前。null なら空席</param>
    /// <param name="mainSeat">兼任のとき、その人の主の席。主の席なら null</param>
    public void Set(string player, bool self, string mainSeat)
    {
        bool open = player == null;
        Ring.enabled = self;
        Shadow.enabled = !open;
        Face.color = open ? T.Bg : T.Panel;
        Dashed.enabled = open;
        Badge.Set(Seat, self, open);
        Mini.Root.gameObject.SetActive(mainSeat != null);
        if (mainSeat != null) Mini.Set(mainSeat, false, false);
        if (player != null && player.Length > T.NameMaxChars) player = player.Substring(0, T.NameMaxChars) + "…";   // タグが隣とぶつからないように
        Name.text = open ? "WAITING" : player;
        Name.color = open ? T.InkSub : T.Ink;
        You.gameObject.SetActive(self);
        Sub.text = Seats.Title(Seat);
        Note.gameObject.SetActive(mainSeat != null);   // 兼任のときだけ3行目（横に広げると隣のタグとぶつかる）
    }
}

public static class UiKit
{
    // ================================================================== 置き方
    public static void Fill(RectTransform r, float inset = 0f)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
    }

    /// <summary>親の縦の中央に、横いっぱい・高さ h で置く</summary>
    public static void MiddleBand(RectTransform r, float h)
    {
        r.anchorMin = new Vector2(0f, 0.5f); r.anchorMax = new Vector2(1f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(0f, h); r.anchoredPosition = Vector2.zero;
    }

    /// <summary>親の中央に w×h で置く</summary>
    public static void Center(RectTransform r, float w, float h)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(w, h); r.anchoredPosition = Vector2.zero;
    }

    /// <summary>Screen.safeArea に合わせた入れ物（画面の中身はこの中に置く。背景は外に敷く）</summary>
    public static RectTransform SafeArea(Transform p)
    {
        var r = Ui.Rect("SafeArea", p); Fill(r);
        r.gameObject.AddComponent<UiSafeArea>();
        return r;
    }

    /// <summary>レイアウトの外に出す（背景・影など、親いっぱいに敷くもの）</summary>
    public static void Ignore(Component c) => Ui.Size(c).ignoreLayout = true;

    /// <summary>固定の大きさ（最小も同じ。縮ませない）</summary>
    public static LayoutElement Fixed(Component c, float w, float h)
    {
        var le = Ui.Size(c, w, h);
        le.minWidth = w; le.minHeight = h; le.flexibleWidth = 0f; le.flexibleHeight = 0f;
        return le;
    }

    public static RectTransform Row(Transform p, float spacing, TextAnchor a = TextAnchor.MiddleCenter, float padX = 0f, float padY = 0f)
    {
        var r = Ui.Rect("Row", p);
        var l = r.gameObject.AddComponent<HorizontalLayoutGroup>();
        Setup(l, spacing, a, padX, padY);
        return r;
    }

    public static RectTransform Col(Transform p, float spacing, TextAnchor a = TextAnchor.UpperCenter, float padX = 0f, float padY = 0f, bool stretchWidth = false)
    {
        var r = Ui.Rect("Col", p);
        var l = r.gameObject.AddComponent<VerticalLayoutGroup>();
        Setup(l, spacing, a, padX, padY);
        l.childForceExpandWidth = stretchWidth;
        return r;
    }

    static void Setup(HorizontalOrVerticalLayoutGroup l, float spacing, TextAnchor a, float padX, float padY)
    {
        l.spacing = spacing; l.childAlignment = a;
        l.padding = new RectOffset(Mathf.RoundToInt(padX), Mathf.RoundToInt(padX), Mathf.RoundToInt(padY), Mathf.RoundToInt(padY));
        l.childControlWidth = l.childControlHeight = true;
        l.childForceExpandWidth = l.childForceExpandHeight = false;
    }

    /// <summary>縦の隙間（flex=true なら残りを埋める）</summary>
    public static RectTransform Space(Transform p, float h, bool flex = false)
    {
        var r = Ui.Rect("Space", p);
        Ui.Size(r, h: h, flexH: flex ? 1f : 0f);
        return r;
    }

    // ================================================================== 形・文字・アイコン
    /// <summary>白いスプライトに色を付けた Image。9-slice のものは半径 radius になるよう pixelsPerUnitMultiplier を合わせる</summary>
    public static Image Shape(Transform p, Sprite s, Color c, float radius, string name)
    {
        var r = Ui.Rect(name, p);
        var img = r.gameObject.AddComponent<Image>();
        img.sprite = s; img.color = c; img.raycastTarget = false;
        if (s != null && s.border.x > 0f)
        {
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = s.border.x / Mathf.Max(1f, radius);
        }
        return img;
    }

    public static Image Pill(Transform p, Color c, float h, string name = "Pill") => Shape(p, T.Theme.pill, c, h * 0.5f, name);
    public static Image Round(Transform p, float radius, Color c, string name = "Round") => Shape(p, T.Round(radius), c, radius, name);

    public static float Size(TextStyle st)
    {
        switch (st)
        {
            case TextStyle.Title: return T.TextTitle;
            case TextStyle.Heading: return T.TextHeading;
            case TextStyle.Sub: return T.TextSub;
            case TextStyle.Big: return T.TextBig;
            case TextStyle.Number: return T.TextNumber;
            case TextStyle.Tag: return T.TextTag;
            case TextStyle.Body: return T.TextBody;
            default: return T.TextLabel;
        }
    }

    /// <summary>文字。見出し系（Title〜Tag）は Baloo 2、本文と補助ラベルは Nunito Bold。
    /// tight：Baloo 2 は行の高さ（字の 1.6 倍）が大きいので、並べるときの高さを字の HeadingLineHeight 倍に詰める（見出し系だけ。
    /// 詰めた高さでは自動の縮小や Ellipsis が働かないので、それを使う文字は tight=false）</summary>
    public static TextMeshProUGUI Text(Transform p, string s, TextStyle st, Color c, TextAlignmentOptions a = TextAlignmentOptions.Midline, bool tight = true)
    {
        var r = Ui.Rect("Text", p);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        bool head = st != TextStyle.Body && st != TextStyle.Label;
        t.font = head ? T.FontHeading : T.FontBody;
        t.fontSize = Size(st);
        t.color = c; t.alignment = a; t.text = s;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        if (head && tight) Ui.Size(t, h: t.fontSize * T.HeadingLineHeight);
        return t;
    }

    public static Image Icon(Transform p, string name, float size, Color c)
    {
        var img = Shape(p, name == null ? null : T.Icon(name), c, 0f, "Icon_" + (name ?? "none"));
        img.preserveAspect = true;
        Fixed(img, size, size);
        return img;
    }

    // ================================================================== パネル・チップ・タグ
    /// <summary>影つきの面を root いっぱいに敷く（どちらもレイアウトの外）。面を返す</summary>
    public static Image Surface(RectTransform root, float radius, Color face, bool shadow = true)
    {
        if (shadow)
        {
            var sh = Round(root, radius, T.Shadow, "Shadow"); Fill(sh.rectTransform); Ignore(sh);
            sh.rectTransform.offsetMin = new Vector2(0f, -T.ShadowY); sh.rectTransform.offsetMax = new Vector2(0f, -T.ShadowY);
        }
        var f = Round(root, radius, face, "Face"); Fill(f.rectTransform); Ignore(f);
        return f;
    }

    /// <summary>パネル（panel・角丸 40・影 6・内側 24）。返す root に上から順に足す。後ろへの押下は止める</summary>
    public static RectTransform Panel(Transform p, float spacing = T.S2, string name = "Panel")
    {
        var r = Ui.Rect(name, p);
        Surface(r, T.RadiusL, T.Panel).raycastTarget = true;
        var l = r.gameObject.AddComponent<VerticalLayoutGroup>();
        Setup(l, spacing, TextAnchor.UpperCenter, T.PanelPad, T.PanelPad);
        l.childForceExpandWidth = true;
        return r;
    }

    /// <summary>色の面のピル（合言葉など）。中身は返す Row に足す</summary>
    public static RectTransform Chip(Transform p, Color face, float h, string name = "Chip")
    {
        var r = Row(p, T.S2, TextAnchor.MiddleCenter, T.S4);
        r.name = name;
        var bg = Pill(r, face, h, "Face"); Fill(bg.rectTransform); Ignore(bg);
        Ui.Size(r, h: h);
        return r;
    }

    /// <summary>小さなピルの札（YOU など。高さ 44、Baloo 24）</summary>
    public static RectTransform Tag(Transform p, string text, Color face, Color fg)
    {
        var r = Row(p, 0f, TextAnchor.MiddleCenter, T.S2);
        r.name = "Tag_" + text;
        var bg = Pill(r, face, T.TagH, "Face"); Fill(bg.rectTransform); Ignore(bg);
        Text(r, text, TextStyle.Tag, fg);
        var le = Ui.Size(r, h: T.TagH); le.minHeight = T.TagH; le.flexibleWidth = 0f;
        return r;
    }

    /// <summary>横の点線（パネルの見出しの下など）</summary>
    public static UiDashed DashedLine(Transform p, Color c)
    {
        var r = Ui.Rect("DashedLine", p);
        r.gameObject.AddComponent<CanvasRenderer>();
        var d = r.gameObject.AddComponent<UiDashed>();
        d.straight = true; d.color = c; d.raycastTarget = false;
        Ui.Size(d, h: T.S2);
        return d;
    }

    /// <summary>角丸の点線の枠（空席のカードなど）。親いっぱいに敷く</summary>
    public static UiDashed DashedFrame(RectTransform root, float radius, Color c)
    {
        var r = Ui.Rect("DashedFrame", root);
        r.gameObject.AddComponent<CanvasRenderer>();
        var d = r.gameObject.AddComponent<UiDashed>();
        d.radius = radius; d.color = c; d.raycastTarget = false;
        Fill(r); Ignore(d);
        return d;
    }

    // ================================================================== ボタン
    /// <summary>主ボタン：ピル 96、go に白文字 Baloo 32、左に白アイコン 40、影 6。1画面に1個</summary>
    public static UiButton PrimaryButton(Transform p, string text, string icon, Action on) =>
        MakeButton(p, text, icon, on, T.PrimaryH, T.Go, T.Panel, null, T.PrimaryMinW);

    /// <summary>副ボタン：ピル 72、panel に ink の文字、枠 3px ink。押せる範囲は 96</summary>
    public static UiButton SecondaryButton(Transform p, string text, string icon, Action on) =>
        MakeButton(p, text, icon, on, T.SecondaryH, T.Panel, T.Ink, T.Ink, -1f);

    static UiButton MakeButton(Transform p, string text, string icon, Action on, float h, Color face, Color fg, Color? frame, float minW)
    {
        var b = new UiButton { faceOn = face, fgOn = fg, frameOn = frame ?? face, minW = minW };
        b.Root = Ui.Rect("Button_" + text, p);
        var hit = b.Root.gameObject.AddComponent<Image>(); hit.color = T.Clear;   // 押せる範囲（見た目より大きいことがある）

        var sh = Pill(b.Root, T.Shadow, h, "Shadow"); MiddleBand(sh.rectTransform, h);
        sh.rectTransform.anchoredPosition = new Vector2(0f, -T.ShadowY);
        b.Body = Ui.Rect("Body", b.Root); MiddleBand(b.Body, h);
        float inset = 0f;
        if (frame.HasValue) { b.Frame = Pill(b.Body, frame.Value, h, "Frame"); Fill(b.Frame.rectTransform); inset = T.Border; }
        b.Face = Pill(b.Body, face, h - inset * 2f, "Face"); Fill(b.Face.rectTransform, inset);

        var row = Row(b.Body, T.S2, TextAnchor.MiddleCenter, T.S4); Fill(row);
        b.Icon = Icon(row, icon, T.IconM, fg);
        b.Icon.gameObject.SetActive(b.Icon.sprite != null);
        b.iconOn = b.Icon.sprite;
        b.Label = Text(row, text, TextStyle.Sub, fg);

        b.Button = b.Root.gameObject.AddComponent<Button>();
        b.Button.targetGraphic = b.Face;
        b.Button.navigation = new Navigation { mode = Navigation.Mode.None };
        var cb = b.Button.colors;
        cb.normalColor = cb.selectedColor = cb.disabledColor = Color.white;
        cb.highlightedColor = T.TintHover; cb.pressedColor = T.TintPressed; cb.fadeDuration = T.TintFade;
        b.Button.colors = cb;
        if (on != null) b.Button.onClick.AddListener(() => on());
        b.Root.gameObject.AddComponent<UiPress>().Init(b.Body, b.Button);

        var le = Ui.Size(b.Root, h: Mathf.Max(h, T.Hit)); le.minHeight = Mathf.Max(h, T.Hit);
        FitWidth(b);
        return b;
    }

    /// <summary>中身（アイコン＋文字）が入る幅を、最小の幅として入れる</summary>
    public static void FitWidth(UiButton b)
    {
        if (b.Label == null || b.minW < 0f && b.Frame == null) return;
        float w = T.S4 * 2f + b.Label.GetPreferredValues(b.Label.text).x;
        if (b.Icon != null && b.Icon.gameObject.activeSelf) w += T.IconM + T.S2;
        var le = Ui.Size(b.Root);
        le.minWidth = Mathf.Max(w, b.minW);
        le.preferredWidth = Mathf.Max(w, b.minW);
    }

    /// <summary>アイコンボタン：円 96、panel、アイコン 48、影 6。label があれば下に補助ラベル 22。
    /// パネルの上に置くときは face に bg を渡す（panel の上に panel の円では見えない）</summary>
    public static UiButton IconButton(Transform p, string icon, string label, Action on, Color? face = null)
    {
        float d = T.IconButtonD;
        float labelH = label == null ? 0f : T.S1 + T.TextLabel * T.BodyLineHeight;
        var b = new UiButton { faceOn = face ?? T.Panel, fgOn = T.Ink, frameOn = face ?? T.Panel };
        b.Root = Ui.Rect("IconButton_" + icon, p);
        b.Root.sizeDelta = new Vector2(d, d + labelH);
        var hit = b.Root.gameObject.AddComponent<Image>(); hit.color = T.Clear;

        var sh = Pill(b.Root, T.Shadow, d, "Shadow"); TopCenter(sh.rectTransform, d); sh.rectTransform.anchoredPosition = new Vector2(0f, -T.ShadowY);
        b.Body = Ui.Rect("Body", b.Root); TopCenter(b.Body, d);
        b.Face = Pill(b.Body, b.faceOn, d, "Face"); Fill(b.Face.rectTransform);
        b.Icon = Icon(b.Body, icon, T.IconInButton, T.Ink); Center(b.Icon.rectTransform, T.IconInButton, T.IconInButton);
        b.iconOn = b.Icon.sprite;
        if (label != null)
        {
            b.Label = Text(b.Root, label, TextStyle.Label, T.InkSub);
            var lr = b.Label.rectTransform;
            lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(1f, 0f); lr.pivot = new Vector2(0.5f, 0f);
            lr.sizeDelta = new Vector2(0f, labelH - T.S1); lr.anchoredPosition = Vector2.zero;
        }

        b.Button = b.Root.gameObject.AddComponent<Button>();
        b.Button.targetGraphic = b.Face;
        b.Button.navigation = new Navigation { mode = Navigation.Mode.None };
        var cb = b.Button.colors;
        cb.normalColor = cb.selectedColor = cb.disabledColor = Color.white;
        cb.highlightedColor = T.TintHover; cb.pressedColor = T.TintPressed; cb.fadeDuration = T.TintFade;
        b.Button.colors = cb;
        if (on != null) b.Button.onClick.AddListener(() => on());
        b.Root.gameObject.AddComponent<UiPress>().Init(b.Body, b.Button);
        Fixed(b.Root, d, d + labelH);
        return b;
    }

    static void TopCenter(RectTransform r, float d)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 1f);
        r.sizeDelta = new Vector2(d, d); r.anchoredPosition = Vector2.zero;
    }

    // ================================================================== 入力欄
    public static UiInput Input(Transform p, string icon, string placeholder, string initial)
    {
        var u = new UiInput();
        u.Root = Ui.Rect("Input", p);
        var hit = u.Root.gameObject.AddComponent<Image>(); hit.color = T.Clear;   // 押せる範囲は 96
        var le = Ui.Size(u.Root, h: Mathf.Max(T.InputH, T.Hit)); le.minHeight = le.preferredHeight;

        var body = Ui.Rect("Body", u.Root); MiddleBand(body, T.InputH);
        u.Frame = Round(body, T.RadiusS, T.Line, "Frame"); Fill(u.Frame.rectTransform);
        var face = Round(body, T.RadiusS - T.Border, T.Panel, "Face"); Fill(face.rectTransform, T.Border);
        float left = T.S2;
        if (icon != null)
        {
            var ic = Icon(body, icon, T.IconM, T.InkSub).rectTransform;
            ic.anchorMin = ic.anchorMax = ic.pivot = new Vector2(0f, 0.5f);
            ic.sizeDelta = new Vector2(T.IconM, T.IconM); ic.anchoredPosition = new Vector2(T.S2, 0f);
            left += T.IconM + T.S2;
        }
        var area = Ui.Rect("TextArea", body); Fill(area);
        area.offsetMin = new Vector2(left, 0f); area.offsetMax = new Vector2(-T.S2, 0f);
        area.gameObject.AddComponent<RectMask2D>();
        var ph = Text(area, placeholder, TextStyle.Body, T.InkSub, TextAlignmentOptions.Left); Fill(ph.rectTransform);
        var tx = Text(area, "", TextStyle.Body, T.Ink, TextAlignmentOptions.Left); Fill(tx.rectTransform);

        var f = u.Root.gameObject.AddComponent<TMP_InputField>();
        f.textViewport = area; f.textComponent = tx; f.placeholder = ph;
        f.targetGraphic = hit; f.transition = Selectable.Transition.None;
        f.navigation = new Navigation { mode = Navigation.Mode.None };
        f.fontAsset = T.FontBody; f.pointSize = T.TextBody;
        f.customCaretColor = true; f.caretColor = T.Ink; f.caretWidth = Mathf.RoundToInt(T.Border);
        f.selectionColor = T.Selection; f.richText = false;
        var frame = u.Frame;
        f.onSelect.AddListener(_ => frame.color = T.Go);
        f.onDeselect.AddListener(_ => frame.color = T.Line);
        f.text = initial ?? "";
        u.Field = f;
        return u;
    }

    // ================================================================== 名前のタグ（部屋の画面）
    /// <summary>名前のタグ（角丸 28・影 6）：席バッジ＋名前（＋YOU）＋席の名前。下の真ん中を頭の上に合わせる（pivot は下の中央）</summary>
    public static UiCrewTag CrewTag(Transform p, string seat)
    {
        var c = new UiCrewTag { Seat = seat };
        c.Root = Row(p, T.S2, TextAnchor.MiddleLeft, T.S2, T.S1);
        c.Root.name = "Tag_" + seat;
        c.Root.pivot = new Vector2(0.5f, 0f);
        var fit = c.Root.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        c.Ring = Round(c.Root, T.RadiusM + T.SelfRing, T.Ink, "Ring"); Fill(c.Ring.rectTransform, -T.SelfRing); Ignore(c.Ring);
        c.Shadow = Round(c.Root, T.RadiusM, T.Shadow, "Shadow"); Fill(c.Shadow.rectTransform); Ignore(c.Shadow);
        c.Shadow.rectTransform.offsetMin = new Vector2(0f, -T.ShadowY); c.Shadow.rectTransform.offsetMax = new Vector2(0f, -T.ShadowY);
        c.Face = Round(c.Root, T.RadiusM, T.Panel, "Face"); Fill(c.Face.rectTransform); Ignore(c.Face);
        c.Dashed = DashedFrame(c.Root, T.RadiusM, T.InkSub);

        c.Badge = SeatBadge(c.Root, seat, T.BadgeS);
        var col = Col(c.Root, 0f, TextAnchor.MiddleLeft);
        var who = Row(col, T.S1, TextAnchor.MiddleLeft);
        c.Mini = SeatBadge(who, seat, T.TagH);
        c.Name = Text(who, "", TextStyle.Body, T.Ink, TextAlignmentOptions.MidlineLeft);
        c.Name.overflowMode = TextOverflowModes.Ellipsis;
        c.You = Tag(who, "YOU", T.Ink, T.Panel);
        c.Sub = Text(col, "", TextStyle.Label, T.InkSub, TextAlignmentOptions.MidlineLeft);
        c.Note = Text(col, "Covering this seat too", TextStyle.Label, T.InkSub, TextAlignmentOptions.MidlineLeft);
        c.Set(null, false, null);
        return c;
    }

    // ================================================================== 席バッジ
    public static UiSeatBadge SeatBadge(Transform p, string seat, float d)
    {
        var b = new UiSeatBadge();
        b.Root = Ui.Rect("Seat_" + seat, p);
        b.Root.sizeDelta = new Vector2(d, d);
        Fixed(b.Root, d, d);
        b.Ring = Pill(b.Root, T.Ink, d + T.SelfRing * 2f, "Ring"); Fill(b.Ring.rectTransform, -T.SelfRing);
        b.Circle = Pill(b.Root, T.SeatColor(seat), d, "Circle"); Fill(b.Circle.rectTransform);
        b.Shape = Text(b.Root, Seats.Icon(seat), TextStyle.Tag, T.OnSeat(seat));
        b.Shape.font = T.FontShapes; b.Shape.fontSize = d * T.BadgeShapeScale;
        Fill(b.Shape.rectTransform); Ignore(b.Shape);
        b.Set(seat, false, false);
        return b;
    }
}
