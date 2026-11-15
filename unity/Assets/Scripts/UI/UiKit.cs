// 書いた人：iizuka（作成：Claude）／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-15、#189 名前タグに席の仕事）／ 更新：iizuka（Opus 5.5、2026-11-15、#190 あつ森の部品 Choice・Row・Counter・Hint・Ribbon・Card・Slot・Disc）
// UI ガイドライン 3章の部品（#72 のタイトルと部屋で使うもの）。コードで組む UGUI ＋ TextMeshPro。
// 色・大きさはすべて UiTokens から読む（ここに色コードや px を書かない）。Ui.cs（legacy Text の骨組み）は変えない。
// #190：影は使わない（原則 2）。形は波打つ sprite（blob_*）、ふち（内ふち）で縁取る。押すと UiSquish で縮んで弾み、UiPress は音だけ
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;

public enum TextStyle
{
    Title, Heading, Sub, Big, Number, Tag, Body, Label,
    // #190（Nunito。Talk・Hint・Price は Bold、ほかは ExtraBold）
    Talk, Choice, RowName, Ribbon, Hint, Price
}

/// <summary>UiKit のボタン（主・副・アイコン）。押せない見た目への切り替えと、文字の差し替えができる</summary>
public class UiButton
{
    public RectTransform Root, Body;
    public Button Button;
    public Image Face, Frame, Icon;
    public TextMeshProUGUI Label;
    internal Color faceOn, fgOn, frameOn;
    internal Color? faceOff, frameOff, iconFg;   // 押せないときの面・ふち（null なら前の決まり）、アイコンの色（null なら fgOn）
    internal Graphic stripes;                    // 主ボタンの縞（押せないときは消す）
    internal Sprite iconOn;
    internal float minW = -1;
    internal float extraW;   // 中身の幅に足す（ヒントのピルの左の丸など）

    /// <summary>押せないとき：面は line（副ボタンは panel ＋ line のふち）、文字は textSoft、アイコンは lock</summary>
    public void SetEnabled(bool on)
    {
        Button.interactable = on;
        Face.color = on ? faceOn : faceOff ?? (Frame != null ? T.Panel : T.Line);
        if (Frame != null) Frame.color = on ? frameOn : frameOff ?? T.Line;
        if (stripes != null) stripes.enabled = on;
        if (Label != null) Label.color = on ? fgOn : T.TextSoft;
        if (Icon != null)
        {
            Icon.sprite = on ? iconOn : T.Icon("lock");
            Icon.color = on ? iconFg ?? fgOn : T.TextSoft;
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

/// <summary>席バッジ：席の色の円（blob_disc）に形（▲●■◆）。自分の席は外側に text の輪</summary>
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
    public TextMeshProUGUI Name, Sub, Job, Note;

    /// <param name="player">座っている人の名前。null なら空席</param>
    /// <param name="mainSeat">兼任のとき、その人の主の席。主の席なら null</param>
    public void Set(string player, bool self, string mainSeat)
    {
        bool open = player == null;
        Ring.enabled = self;
        Shadow.enabled = false;   // #190 影なし（欄は残す）
        Face.color = open ? T.Bg : T.Panel;
        Dashed.enabled = open;
        Badge.Set(Seat, self, open);
        Mini.Root.gameObject.SetActive(mainSeat != null);
        if (mainSeat != null) Mini.Set(mainSeat, false, false);
        if (player != null && player.Length > T.NameMaxChars) player = player.Substring(0, T.NameMaxChars) + "…";   // タグが隣とぶつからないように
        Name.text = open ? "Waiting…" : player;
        Name.color = open ? T.TextSoft : T.Text;
        You.gameObject.SetActive(self);
        Sub.text = Seats.Title(Seat);
        Job.text = Seats.Job(Seat);
        // 3行目：ふだんは席の仕事（#189）、兼任のときは「Covering this seat too」（横に広げると隣のタグとぶつかる）。
        //   4行にすると、iPhone の横（844×390）で上の合言葉のチップにかぶるので、どちらか1つ
        Job.gameObject.SetActive(mainSeat == null);
        Note.gameObject.SetActive(mainSeat != null);
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
            case TextStyle.Talk: return T.TextTalk;
            case TextStyle.Choice: return T.TextChoice;
            case TextStyle.RowName: return T.TextRow;
            case TextStyle.Ribbon: return T.TextRibbon;
            case TextStyle.Hint: return T.TextHint;
            case TextStyle.Price: return T.TextPrice;
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
        bool head = st != TextStyle.Body && st != TextStyle.Label && st < TextStyle.Talk;
        // #190：Baloo 2 はロゴと数字だけ。見出し・ボタン・札・選択肢・行の名前は Nunito ExtraBold、台詞・本文・補助は Nunito Bold
        bool baloo = st == TextStyle.Title || st == TextStyle.Big || st == TextStyle.Number;
        bool strong = st == TextStyle.Heading || st == TextStyle.Sub || st == TextStyle.Tag
            || st == TextStyle.Choice || st == TextStyle.RowName || st == TextStyle.Ribbon;
        t.font = baloo ? T.FontHeading : strong ? T.FontStrong : T.FontBody;
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
    /// <summary>面を root いっぱいに敷く（レイアウトの外）。面を返す。#190 で影はやめた（shadow は呼び出しの形のために残す）</summary>
    public static Image Surface(RectTransform root, float radius, Color face, bool shadow = true)
    {
        var f = Round(root, radius, face, "Face"); Fill(f.rectTransform); Ignore(f);
        return f;
    }

    /// <summary>パネル（blob_panel、外に内ふち 12（rim）、中は panel と panelBand の横縞、内側 32、影なし）。返す root に上から順に足す。後ろへの押下は止める</summary>
    public static RectTransform Panel(Transform p, float spacing = T.S2, string name = "Panel")
    {
        var r = Ui.Rect(name, p);
        PanelFace(r, T.Panel).raycastTarget = true;
        var l = r.gameObject.AddComponent<VerticalLayoutGroup>();
        Setup(l, spacing, TextAnchor.UpperCenter, T.S4, T.S4);
        l.childForceExpandWidth = true;
        return r;
    }

    /// <summary>パネルの面だけを root いっぱいに敷く（外＝rim の blob_panel、12 内側に face の blob_panel、その中に横縞）。外の面を返す</summary>
    public static Image PanelFace(RectTransform root, Color face, bool bands = true)
    {
        float b = T.PanelCorner;
        var rim = Blob(root, T.BlobPanel, T.Rim, b, "Rim"); Fill(rim.rectTransform); Ignore(rim);
        var f = Blob(root, T.BlobPanel, face, b - T.RimPanel, "Face"); Fill(f.rectTransform, T.RimPanel); Ignore(f);
        var band = bands ? T.Art(T.Bands) : null;
        if (band != null)
        {
            f.gameObject.AddComponent<Mask>().showMaskGraphic = true;   // 横縞を面の形で切る
            var bi = Shape(f.transform, band, T.PanelBand, 0f, "Bands");
            bi.type = Image.Type.Tiled; Fill(bi.rectTransform);
        }
        return rim;
    }

    /// <summary>色の面のピル（合言葉など）。中身は返す Row に足す</summary>
    public static RectTransform Chip(Transform p, Color face, float h, string name = "Chip")
    {
        var r = Row(p, T.S2, TextAnchor.MiddleCenter, T.S4);
        r.name = name;
        var bg = Blob(r, T.BlobPill, face, h * 0.5f, "Face"); Fill(bg.rectTransform); Ignore(bg);
        Ui.Size(r, h: h);
        return r;
    }

    /// <summary>小さなピルの札（YOU など。高さ 44、Baloo 24）</summary>
    public static RectTransform Tag(Transform p, string text, Color face, Color fg)
    {
        var r = Row(p, 0f, TextAnchor.MiddleCenter, T.S2);
        r.name = "Tag_" + text;
        var bg = Blob(r, T.BlobPill, face, T.TagH * 0.5f, "Face"); Fill(bg.rectTransform); Ignore(bg);
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
    /// <summary>主ボタン：blob_pill 96、go の面に goLight の斜めの縞（流れる）とふち 5、白文字 32。影なし。1画面に1個（手を乗せる）</summary>
    public static UiButton PrimaryButton(Transform p, string text, string icon, Action on) =>
        MakeButton(p, text, icon, on, T.PrimaryH, T.Go, T.Panel, T.GoLight, T.PrimaryMinW, true);

    /// <summary>副ボタン：blob_pill 72、panel の面に bgDeep のふち 5、text の文字 30。押せる範囲は 96</summary>
    public static UiButton SecondaryButton(Transform p, string text, string icon, Action on) =>
        MakeButton(p, text, icon, on, T.SecondaryH, T.Panel, T.Text, T.BgDeep, -1f, false);

    static UiButton MakeButton(Transform p, string text, string icon, Action on, float h, Color face, Color fg, Color frame, float minW, bool primary)
    {
        var b = new UiButton { faceOn = face, fgOn = fg, frameOn = frame, minW = minW };
        if (primary) { b.faceOff = T.Line; b.frameOff = T.BgDeep; }
        b.Root = Ui.Rect("Button_" + text, p);
        var hit = b.Root.gameObject.AddComponent<Image>(); hit.color = T.Clear;   // 押せる範囲（見た目より大きいことがある）

        b.Body = Ui.Rect("Body", b.Root); MiddleBand(b.Body, h);
        float inset = T.RimButton;
        b.Frame = Blob(b.Body, T.BlobPill, frame, h * 0.5f, "Frame"); Fill(b.Frame.rectTransform);
        b.Face = Blob(b.Body, T.BlobPill, face, h * 0.5f - inset, "Face"); Fill(b.Face.rectTransform, inset);
        if (primary) b.stripes = Stripes(b.Face);

        var row = Row(b.Body, T.S2, TextAnchor.MiddleCenter, T.S4); Fill(row);
        b.Icon = Icon(row, icon, T.IconM, fg);
        b.Icon.gameObject.SetActive(b.Icon.sprite != null);
        b.iconOn = b.Icon.sprite;
        b.Label = Text(row, text, primary ? TextStyle.Choice : TextStyle.RowName, fg);

        b.Button = b.Root.gameObject.AddComponent<Button>();
        b.Button.targetGraphic = b.Face;
        b.Button.navigation = new Navigation { mode = Navigation.Mode.None };
        var cb = b.Button.colors;
        cb.normalColor = cb.selectedColor = cb.disabledColor = Color.white;
        cb.highlightedColor = T.TintHover; cb.pressedColor = T.TintPressed; cb.fadeDuration = T.TintFade;
        b.Button.colors = cb;
        if (on != null) b.Button.onClick.AddListener(() => on());
        b.Root.gameObject.AddComponent<UiPress>().Init(b.Body, b.Button);
        Squish(b);

        var le = Ui.Size(b.Root, h: Mathf.Max(h, T.Hit)); le.minHeight = Mathf.Max(h, T.Hit);
        FitWidth(b);
        return b;
    }

    /// <summary>主ボタンの斜めの縞（stripes64 を goLight 35% で、面の形で切って横に流す）</summary>
    static Graphic Stripes(Image face)
    {
        var tex = T.Theme.stripes != null ? T.Theme.stripes.texture : null;
        if (tex == null) return null;
        face.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        var r = Ui.Rect("Stripes", face.transform); Fill(r);
        var raw = r.gameObject.AddComponent<RawImage>();
        raw.texture = tex; raw.raycastTarget = false;
        raw.color = T.WithAlpha(T.GoLight, T.StripeAlpha / 0.4f);   // stripes64 は白の 40%
        r.gameObject.AddComponent<UiStripes>();
        return raw;
    }

    /// <summary>押すと縮んで弾む（UiSquish）を、画面のコードが自分で付けていなければ付ける（2つ付くと揺れがぶつかる）</summary>
    static void Squish(UiButton b) => b.Root.gameObject.AddComponent<UiSquishAuto>().b = b;

    /// <summary>主ボタンの息（UiSquish.breathe）と、押せないときに押した（onBlocked）を設定する。UiSquish.Add を呼ぶ代わりに使う</summary>
    public static UiSquish Breathe(UiButton b, float breathe, Action onBlocked = null)
    {
        var s = b.Root.GetComponent<UiSquish>();
        if (s == null) s = UiSquish.Add(b);
        s.breathe = breathe;
        if (onBlocked != null) s.onBlocked = onBlocked;
        return s;
    }

    /// <summary>中身（アイコン＋文字）が入る幅を、最小の幅として入れる</summary>
    public static void FitWidth(UiButton b)
    {
        if (b.Label == null || b.minW < 0f && b.Frame == null) return;
        float w = T.S4 * 2f + b.Label.GetPreferredValues(b.Label.text).x;
        if (b.Icon != null && b.Icon.gameObject.activeSelf) w += T.IconM + T.S2;
        w += b.extraW;
        var le = Ui.Size(b.Root);
        le.minWidth = Mathf.Max(w, b.minW);
        le.preferredWidth = Mathf.Max(w, b.minW);
    }

    /// <summary>丸いアイコンのボタン：blob_disc 96、go の面に白い絵、ふち 5（panel）。label があれば下に補助ラベル 22。
    /// face を渡すとその面（明るい面なら絵は text）</summary>
    public static UiButton IconButton(Transform p, string icon, string label, Action on, Color? face = null)
    {
        float d = T.IconButtonD;
        float labelH = label == null ? 0f : T.S1 + T.TextLabel * T.BodyLineHeight;
        var fc = face ?? T.Go;
        var ic = Light(fc) ? T.Text : T.Panel;
        var b = new UiButton { faceOn = fc, fgOn = T.TextSoft, frameOn = T.Panel, iconFg = ic };
        b.Root = Ui.Rect("IconButton_" + icon, p);
        b.Root.sizeDelta = new Vector2(d, d + labelH);
        var hit = b.Root.gameObject.AddComponent<Image>(); hit.color = T.Clear;

        b.Body = Ui.Rect("Body", b.Root); TopCenter(b.Body, d);
        b.Frame = Blob(b.Body, T.BlobDisc, T.Panel, d * 0.5f, "Frame"); Fill(b.Frame.rectTransform);
        b.Face = Blob(b.Body, T.BlobDisc, b.faceOn, d * 0.5f - T.RimButton, "Face"); Fill(b.Face.rectTransform, T.RimButton);
        b.Icon = Icon(b.Body, icon, T.IconInButton, ic); Center(b.Icon.rectTransform, T.IconInButton, T.IconInButton);
        b.iconOn = b.Icon.sprite;
        if (label != null)
        {
            b.Label = Text(b.Root, label, TextStyle.Label, T.TextSoft);
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
        Squish(b);
        Fixed(b.Root, d, d + labelH);
        return b;
    }

    /// <summary>明るい色か（その上の文字・絵を text にする）</summary>
    public static bool Light(Color c) => c.a > 0.5f && 0.299f * c.r + 0.587f * c.g + 0.114f * c.b > 0.7f;

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
        u.Frame = Round(body, T.RadiusS, T.PanelBand, "Frame"); Fill(u.Frame.rectTransform);   // ふちは入力中だけ go
        var face = Round(body, T.RadiusS - T.RimButton, T.PanelBand, "Face"); Fill(face.rectTransform, T.RimButton);
        float left = T.S2;
        if (icon != null)
        {
            // 単色の線のアイコンを置かず、色つきの台に載せる（原則 10、【01】【11】。名前＝go、合言葉＝accent。Fable 採点 a-7）
            var ic = Disc(body, icon, icon == "room_code" ? T.Accent : T.Go, T.RowDiscD).Root;
            ic.anchorMin = ic.anchorMax = ic.pivot = new Vector2(0f, 0.5f);
            ic.anchoredPosition = new Vector2(T.S2, 0f);
            left += T.RowDiscD + T.S2;
        }
        var area = Ui.Rect("TextArea", body); Fill(area);
        area.offsetMin = new Vector2(left, 0f); area.offsetMax = new Vector2(-T.S2, 0f);
        area.gameObject.AddComponent<RectMask2D>();
        var ph = Text(area, placeholder, TextStyle.Body, T.TextSoft, TextAlignmentOptions.Left); Fill(ph.rectTransform);
        var tx = Text(area, "", TextStyle.Body, T.Text, TextAlignmentOptions.Left); Fill(tx.rectTransform);

        var f = u.Root.gameObject.AddComponent<TMP_InputField>();
        f.textViewport = area; f.textComponent = tx; f.placeholder = ph;
        f.targetGraphic = hit; f.transition = Selectable.Transition.None;
        f.navigation = new Navigation { mode = Navigation.Mode.None };
        f.fontAsset = T.FontBody; f.pointSize = T.TextBody;
        f.customCaretColor = true; f.caretColor = T.Text; f.caretWidth = Mathf.RoundToInt(T.Border);
        f.selectionColor = T.Selection; f.richText = false;
        var frame = u.Frame;
        f.onSelect.AddListener(_ => frame.color = T.Go);
        f.onDeselect.AddListener(_ => frame.color = T.PanelBand);
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
        // #190 小さな吹き出し（【08】）：blob_panel の面＋下に小さな丸のしっぽ2つ。影なし
        c.Ring = Blob(c.Root, T.BlobPanel, T.Text, T.RadiusM + T.SelfRing, "Ring"); Fill(c.Ring.rectTransform, -T.SelfRing); Ignore(c.Ring);
        c.Shadow = Round(c.Root, T.RadiusM, T.Clear, "Shadow"); Fill(c.Shadow.rectTransform); Ignore(c.Shadow);
        c.Shadow.enabled = false;
        c.Face = Blob(c.Root, T.BlobPanel, T.Panel, T.RadiusM, "Face"); Fill(c.Face.rectTransform); Ignore(c.Face);
        for (int i = 0; i < 2; i++)
        {
            float d = i == 0 ? T.S3 : T.S2;
            var dot = Blob(c.Face.transform, T.BlobDisc, T.Panel, d * 0.5f, "Tail" + i);
            var dr = dot.rectTransform;
            dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 0f); dr.pivot = new Vector2(0.5f, 1f);
            dr.sizeDelta = new Vector2(d, d); dr.anchoredPosition = new Vector2(i == 0 ? 0f : -T.S2, i == 0 ? -T.S1 * 0.5f : -T.S4 - T.S1 * 0.5f);
        }
        c.Dashed = DashedFrame(c.Root, T.RadiusM, T.TextSoft);

        c.Badge = SeatBadge(c.Root, seat, T.BadgeS);
        var col = Col(c.Root, 0f, TextAnchor.MiddleLeft);
        var who = Row(col, T.S1, TextAnchor.MiddleLeft);
        c.Mini = SeatBadge(who, seat, T.TagH);
        c.Name = Text(who, "", TextStyle.Body, T.Text, TextAlignmentOptions.MidlineLeft);
        c.Name.overflowMode = TextOverflowModes.Ellipsis;
        c.You = Tag(who, "You", T.Text, T.Panel);
        c.Sub = Text(col, "", TextStyle.Label, T.TextSoft, TextAlignmentOptions.MidlineLeft);
        c.Job = Text(col, "", TextStyle.Label, T.TextSoft, TextAlignmentOptions.MidlineLeft);   // 席の仕事（#189）
        c.Note = Text(col, "Covering this seat too", TextStyle.Label, T.TextSoft, TextAlignmentOptions.MidlineLeft);
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
        b.Ring = Blob(b.Root, T.BlobDisc, T.Text, d * 0.5f + T.SelfRing, "Ring"); Fill(b.Ring.rectTransform, -T.SelfRing);
        b.Circle = Blob(b.Root, T.BlobDisc, T.SeatColor(seat), d * 0.5f, "Circle"); Fill(b.Circle.rectTransform);
        b.Shape = Text(b.Root, Seats.Icon(seat), TextStyle.Tag, T.OnSeat(seat));
        b.Shape.font = T.FontShapes; b.Shape.fontSize = d * T.BadgeShapeScale;
        Fill(b.Shape.rectTransform); Ignore(b.Shape);
        b.Set(seat, false, false);
        return b;
    }

    // ================================================================== #190 あつ森の部品（ui-guidelines 3章「部品の表」）
    /// <summary>波打つ形（T.BlobPanel など）。border＝9-slice の縁を何 px に描くか（ピル・丸は高さの半分）。
    /// sprite がまだ無ければ、ピル・丸は pill、吹き出し・パネルは角丸で代わりにする</summary>
    public static Image Blob(Transform p, string sprite, Color c, float border, string name)
    {
        var s = T.Art(sprite);
        if (s == null) s = sprite == T.BlobPill || sprite == T.BlobDisc ? T.Theme.pill : T.Round(border);
        return Shape(p, s, c, border, name);
    }

    /// <summary>アイコンの台：blob_disc（直径 d）に panel 色の絵（台の 60%）。icon が null なら台だけ</summary>
    public static UiIconDisc Disc(Transform p, string icon, Color face, float d)
    {
        var u = new UiIconDisc();
        u.Root = Ui.Rect("Disc_" + (icon ?? "none"), p);
        u.Root.sizeDelta = new Vector2(d, d);
        Fixed(u.Root, d, d);
        u.Face = Blob(u.Root, T.BlobDisc, face, d * 0.5f, "Face"); Fill(u.Face.rectTransform);
        u.Icon = Icon(u.Root, icon, d * 0.6f, T.Panel); Center(u.Icon.rectTransform, d * 0.6f, d * 0.6f);
        u.Icon.gameObject.SetActive(u.Icon.sprite != null);
        return u;
    }

    /// <summary>選択肢の吹き出し：choice の blob_panel に、言葉を縦に（行 80）。押すとすぐ決まり onPick(i)。
    /// 選んだ行にマーカーと手（Right）。selected が -1 なら最初は何も選ばない。cursor=false なら手を動かさない（手は画面に1つ）</summary>
    public static UiChoice Choice(Transform p, string[] options, Action<int> onPick, int selected = 0, bool cursor = true)
    {
        var u = new UiChoice { onPick = onPick, UseCursor = cursor };
        u.Root = Col(p, 0f, TextAnchor.MiddleCenter, T.ChoicePad, T.ChoicePad * 0.5f, true);
        u.Root.name = "Choice";
        u.Face = Blob(u.Root, T.BlobPanel, T.Choice, T.ChoiceCorner, "Face"); Fill(u.Face.rectTransform); Ignore(u.Face);
        u.Face.raycastTarget = true;
        int n = options.Length;
        u.Rows = new RectTransform[n]; u.Labels = new TextMeshProUGUI[n]; u.Markers = new UiMarker[n]; u.Buttons = new Button[n];
        float w = 0f;
        for (int i = 0; i < n; i++)
        {
            int k = i;
            var row = Ui.Rect("Option_" + i, u.Root);
            var hit = row.gameObject.AddComponent<Image>(); hit.color = T.Clear;
            var le = Ui.Size(row, h: T.ChoiceRowH); le.minHeight = T.ChoiceRowH;
            var lab = Text(row, options[i], TextStyle.Choice, T.Text, TextAlignmentOptions.Center, false); Fill(lab.rectTransform);
            u.Markers[i] = UiSelect.Marker(lab); u.Markers[i].SetOn(false);
            var b = row.gameObject.AddComponent<Button>();
            b.targetGraphic = hit; b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => u.Pick(k));
            row.gameObject.AddComponent<UiPress>().Init(row, b);
            u.Rows[i] = row; u.Labels[i] = lab; u.Buttons[i] = b;
            w = Mathf.Max(w, lab.GetPreferredValues(options[i]).x);
        }
        var rle = Ui.Size(u.Root, w + T.ChoicePad * 2f + T.MarkPadX * 2f);
        rle.minWidth = rle.preferredWidth;
        if (selected >= 0 && selected < n) u.Select(selected, false);
        return u;
    }

    /// <summary>材料の行：panelBand の帯（角丸 12）に 台 48＋名前 30＋点線＋色の箱の数＋/ 上限。onStep があれば右に − ＋（行 96）、押すと onStep(-1/+1)。
    /// 数は SetCount、右寄せの値（レポート）は SetValue、名前の下の値段は SetSub。トグルなどは Right に足す。
    /// UiKit.Row(p, spacing, …)（横に並べる入れ物）とは2つ目の引数の型で分かれる</summary>
    public static UiRow Row(Transform p, string icon, string name, Action<int> onStep = null, Color? disc = null)
    {
        var u = new UiRow();
        float h = onStep != null ? T.RowStepH : T.RowH;
        u.Root = Row(p, T.S2, TextAnchor.MiddleLeft, T.S2, 0f);
        u.Root.name = "Row_" + name;
        var le = Ui.Size(u.Root, h: h); le.minHeight = h;
        u.Band = Round(u.Root, T.RowRadius, T.PanelBand, "Band"); Fill(u.Band.rectTransform); Ignore(u.Band);
        u.Disc = Disc(u.Root, icon, disc ?? T.Go, T.RowDiscD);
        var col = Col(u.Root, 0f, TextAnchor.MiddleLeft);
        u.Name = Text(col, name, TextStyle.RowName, T.Text, TextAlignmentOptions.MidlineLeft);
        u.Sub = Text(col, "", TextStyle.Price, T.TextSoft, TextAlignmentOptions.MidlineLeft);
        u.Sub.gameObject.SetActive(false);
        u.Dash = DashedLine(u.Root, T.BgDeep);
        Ui.Size(u.Dash, w: T.S3, flexW: 1f);
        u.Box = Round(u.Root, T.NumBoxRadius, T.Mark, "Box");
        var box = Row(u.Box.transform, 0f, TextAnchor.MiddleCenter, T.S1);
        Fill(box);
        Fixed(u.Box, T.RowDiscD, T.RowDiscD);
        u.Count = Text(box, "0", TextStyle.RowName, T.Text);
        u.Max = Text(u.Root, "", TextStyle.RowName, T.TextSoft, TextAlignmentOptions.MidlineLeft);
        u.Value = Text(u.Root, "", TextStyle.RowName, T.Text, TextAlignmentOptions.MidlineRight);
        u.Box.gameObject.SetActive(false); u.Max.gameObject.SetActive(false); u.Value.gameObject.SetActive(false);
        u.Right = Row(u.Root, T.S1, TextAnchor.MiddleRight);
        if (onStep != null)
        {
            u.Minus = StepButton(u.Right, "−", () => onStep(-1));
            u.Plus = StepButton(u.Right, "+", () => onStep(+1));
        }
        return u;
    }

    static Button StepButton(Transform p, string s, Action on)
    {
        float d = T.RowStepH - T.S2;
        var r = Ui.Rect("Step_" + s, p);
        Fixed(r, d, d);
        var face = Blob(r, T.BlobDisc, T.Panel, d * 0.5f, "Face"); Fill(face.rectTransform, T.S1);
        face.raycastTarget = true;
        var t = Text(r, s, TextStyle.Number, T.Text); Fill(t.rectTransform); Ignore(t);
        var b = r.gameObject.AddComponent<Button>();
        b.targetGraphic = face; b.navigation = new Navigation { mode = Navigation.Mode.None };
        var cb = b.colors; cb.normalColor = cb.selectedColor = Color.white; cb.highlightedColor = T.TintHover; cb.pressedColor = T.TintPressed; cb.disabledColor = T.Line; cb.fadeDuration = T.TintFade; b.colors = cb;
        b.onClick.AddListener(() => on());
        r.gameObject.AddComponent<UiPress>().Init(r, b);
        return b;
    }

    /// <summary>数のチップ：text の blob_pill（高さ 88）に Baloo 40 の数字（panel）。上にアイコンの台 48 を半分かぶせ、右下に単位の札（−4°）。
    /// 暗い画面では face に T.Accent。数字は Set で</summary>
    public static UiCounter Counter(Transform p, string icon, string unit, Color? face = null)
    {
        var u = new UiCounter();
        u.Root = Row(p, 0f, TextAnchor.MiddleCenter, T.S4 + T.S2, 0f);
        u.Root.name = "Counter_" + (icon ?? unit);
        var le = Ui.Size(u.Root, h: T.CounterH); le.minHeight = T.CounterH;
        le.minWidth = T.CounterH * 2f;
        u.Face = Blob(u.Root, T.BlobPill, face ?? T.Text, T.CounterH * 0.5f, "Face"); Fill(u.Face.rectTransform); Ignore(u.Face);
        u.Number = Text(u.Root, "0", TextStyle.Number, T.Panel);
        u.Disc = Disc(u.Root, icon, T.Accent, T.CounterIconD);
        Ignore(u.Disc.Root);
        var dr = u.Disc.Root;
        dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 1f); dr.pivot = new Vector2(0.5f, 0.5f);
        dr.anchoredPosition = new Vector2(0f, T.S1);
        u.Disc.Root.gameObject.SetActive(icon != null);
        u.Unit = Tag(u.Root, unit ?? "", T.Panel, T.Text);
        Ignore(u.Unit);
        var ur = u.Unit;
        ur.anchorMin = ur.anchorMax = new Vector2(1f, 0f); ur.pivot = new Vector2(0.5f, 0.5f);
        ur.sizeDelta = new Vector2(Mathf.Max(T.TagH, ur.GetComponentInChildren<TextMeshProUGUI>().GetPreferredValues(unit ?? "").x + T.S4), T.TagH);
        ur.anchoredPosition = new Vector2(-T.S3, T.S1);
        ur.localRotation = Quaternion.Euler(0f, 0f, T.CounterUnitTilt);
        u.Unit.gameObject.SetActive(!string.IsNullOrEmpty(unit));
        return u;
    }

    /// <summary>ヒントのピル（右下。Skip・設定・戻る）：panel 90% の blob_pill（高さ 56）、左に text の小さな丸に白いアイコン、言葉 26。押せる範囲は 96</summary>
    public static UiButton Hint(Transform p, string icon, string text, Action on)
    {
        var b = new UiButton { faceOn = T.WithAlpha(T.Panel, 0.9f), fgOn = T.Text, frameOn = T.WithAlpha(T.Panel, 0.9f), minW = 0f };
        b.Root = Ui.Rect("Hint_" + text, p);
        var hit = b.Root.gameObject.AddComponent<Image>(); hit.color = T.Clear;
        b.Body = Ui.Rect("Body", b.Root); MiddleBand(b.Body, T.HintH);
        b.Face = Blob(b.Body, T.BlobPill, b.faceOn, T.HintH * 0.5f, "Face"); Fill(b.Face.rectTransform);
        var row = Row(b.Body, T.S1, TextAnchor.MiddleCenter, T.S2); Fill(row);
        var disc = Disc(row, icon, T.Text, T.HintDiscD);
        disc.Root.gameObject.SetActive(icon != null);
        b.extraW = icon != null ? T.HintDiscD + T.S1 - T.S4 : -T.S4;   // FitWidth の左右 32 を 16 にする
        b.Label = Text(row, text, TextStyle.Hint, T.Text);
        b.Button = b.Root.gameObject.AddComponent<Button>();
        b.Button.targetGraphic = b.Face;
        b.Button.navigation = new Navigation { mode = Navigation.Mode.None };
        var cb = b.Button.colors;
        cb.normalColor = cb.selectedColor = cb.disabledColor = Color.white;
        cb.highlightedColor = T.TintHover; cb.pressedColor = T.TintPressed; cb.fadeDuration = T.TintFade;
        b.Button.colors = cb;
        if (on != null) b.Button.onClick.AddListener(() => on());
        b.Root.gameObject.AddComponent<UiPress>().Init(b.Body, b.Button);
        Squish(b);
        var le = Ui.Size(b.Root, h: T.Hit); le.minHeight = T.Hit;
        FitWidth(b);
        return b;
    }

    /// <summary>見出しの帯（画面の左上）：plum（か go）の面、高さ 80、白文字 36</summary>
    public static UiRibbon Ribbon(Transform p, string text, Color? face = null)
    {
        var u = new UiRibbon();
        u.Root = Row(p, 0f, TextAnchor.MiddleLeft, T.S4 + T.S1, 0f);
        u.Root.name = "Ribbon_" + text;
        var le = Ui.Size(u.Root, h: T.RibbonH); le.minHeight = T.RibbonH; le.flexibleWidth = 0f;
        u.Face = Blob(u.Root, T.BlobPill, face ?? T.Plum, T.RibbonH * 0.5f, "Face"); Fill(u.Face.rectTransform); Ignore(u.Face);
        u.Label = Text(u.Root, text, TextStyle.Ribbon, T.Panel, TextAlignmentOptions.MidlineLeft);
        return u;
    }

    /// <summary>カード：角丸 16、face の面、内側 6 に 2px の明るい線。中身は Content（縦に並ぶ、文字は T.Panel で）。
    /// 選ぶ：SetPicked（1.12 倍＋チェック。手は呼ぶ側が UiSelect.Cursor(card.Root, CursorDir.UpLeft)）、決まった後の他方：SetMuted</summary>
    public static UiCard Card(Transform p, Color face)
    {
        var u = new UiCard { faceOn = face };
        u.Root = Ui.Rect("Card", p);
        u.Face = Round(u.Root, T.CardRadius, face, "Face"); Fill(u.Face.rectTransform); Ignore(u.Face);
        u.Face.raycastTarget = true;
        u.Line = Round(u.Root, T.CardRadius - T.CardLineInset, UiCard.Light(face), "Line"); Fill(u.Line.rectTransform, T.CardLineInset); Ignore(u.Line);
        u.Inner = Round(u.Root, T.CardRadius - T.CardLineInset - T.CardLine, face, "Inner"); Fill(u.Inner.rectTransform, T.CardLineInset + T.CardLine); Ignore(u.Inner);
        var l = u.Root.gameObject.AddComponent<VerticalLayoutGroup>();
        Setup(l, T.S1, TextAnchor.MiddleCenter, T.S3, T.S3);
        l.childForceExpandWidth = true;
        u.Content = u.Root;
        return u;
    }

    /// <summary>丸い枠（グリッドの選択）：bgDeep の blob_disc 96 に text 色の絵。icon が null なら空き（枠だけ）。on があれば押せる</summary>
    public static UiSlot Slot(Transform p, string icon, Action on = null)
    {
        var u = new UiSlot();
        float d = T.SlotD;
        u.Root = Ui.Rect("Slot_" + (icon ?? "empty"), p);
        u.Root.sizeDelta = new Vector2(d, d);
        Fixed(u.Root, d, d);
        u.Face = Blob(u.Root, T.BlobDisc, T.BgDeep, d * 0.5f, "Face"); Fill(u.Face.rectTransform);
        u.Face.raycastTarget = true;
        u.Icon = Icon(u.Root, icon, d * 0.6f, T.Text); Center(u.Icon.rectTransform, d * 0.6f, d * 0.6f);
        u.Icon.gameObject.SetActive(u.Icon.sprite != null);
        if (on != null)
        {
            u.Button = u.Root.gameObject.AddComponent<Button>();
            u.Button.targetGraphic = u.Face; u.Button.transition = Selectable.Transition.None;
            u.Button.navigation = new Navigation { mode = Navigation.Mode.None };
            u.Button.onClick.AddListener(() => { UiSelect.Pick(u.Root); on(); });
            u.Root.gameObject.AddComponent<UiPress>().Init(u.Root, u.Button);
        }
        return u;
    }
}

/// <summary>アイコンの台（UiKit.Disc）</summary>
public class UiIconDisc
{
    public RectTransform Root;
    public Image Face, Icon;
    public void Set(string icon, Color face)
    {
        Face.color = face;
        Icon.sprite = icon == null ? null : UiTokens.Icon(icon);
        Icon.gameObject.SetActive(Icon.sprite != null);
    }
}

/// <summary>選択肢の吹き出し（UiKit.Choice）。Selected が今の選択（-1 なら無し）</summary>
public class UiChoice
{
    public RectTransform Root;
    public Image Face;
    public RectTransform[] Rows;
    public TextMeshProUGUI[] Labels;
    public UiMarker[] Markers;
    public Button[] Buttons;
    public int Selected { get; private set; } = -1;
    public bool UseCursor = true;   // false なら手を動かさない（手は画面に1つ。主ボタンに乗せたいとき）
    internal Action<int> onPick;

    /// <summary>押したとき：選んで onPick(i)</summary>
    internal void Pick(int i)
    {
        if (!Buttons[i].interactable) return;
        Select(i, true);
        if (UseCursor) UiSelect.CursorOf(Root).Poke();
        UiSelect.Pick(Labels[i]);
        onPick?.Invoke(i);
    }

    /// <summary>選んだ見た目にする（onPick は呼ばない。サーバーから来た状態を映すとき。notify は呼び出しの形のために残す）</summary>
    public void Select(int i, bool notify = false)
    {
        Selected = i;
        for (int k = 0; k < Markers.Length; k++) Markers[k].SetOn(k == i);
        if (UseCursor && i >= 0) UiSelect.Cursor(Rows[i], CursorDir.Right);
    }

    /// <summary>押せない行（文字を line、押しても決まらない）</summary>
    public void SetEnabled(int i, bool on)
    {
        Buttons[i].interactable = on;
        Labels[i].color = on ? UiTokens.Text : UiTokens.WithAlpha(UiTokens.TextSoft, 0.6f);   // 押せない行も読める濃さ（3章、Fable 採点 a-2）
    }
}

/// <summary>材料の行（UiKit.Row の icon・name の形）</summary>
public class UiRow
{
    public RectTransform Root, Right;
    public Image Band, Box;
    public UiIconDisc Disc;
    public TextMeshProUGUI Name, Sub, Count, Max, Value;
    public UiDashed Dash;
    public Button Minus, Plus;

    /// <summary>色の箱の数＋「/ 上限」（max が 0 以下なら上限を出さない）</summary>
    public void SetCount(int n, int max = 0)
    {
        Box.gameObject.SetActive(true);
        Count.text = n.ToString();
        Max.gameObject.SetActive(max > 0);
        Max.text = "/ " + max;
        if (Minus != null) Minus.interactable = n > 0;
        if (Plus != null) Plus.interactable = max <= 0 || n < max;
    }

    /// <summary>点線の右に右寄せの値（レポートの行など）。null で消す</summary>
    public void SetValue(string s)
    {
        Value.gameObject.SetActive(s != null);
        Value.text = s ?? "";
    }

    /// <summary>名前の下の値段（小さなアイコンは文字に含めない。null で消す）</summary>
    public void SetSub(string s)
    {
        Sub.gameObject.SetActive(!string.IsNullOrEmpty(s));
        Sub.text = s ?? "";
    }

    /// <summary>台の右上のチェック（1個以上積んだ行）</summary>
    public void SetChecked(bool on) => UiSelect.Check(Disc.Root, on);

    /// <summary>押せない行：台と文字を line／textSoft に</summary>
    public void SetEnabled(bool on)
    {
        Disc.Face.color = on ? UiTokens.Go : UiTokens.Line;
        Name.color = on ? UiTokens.Text : UiTokens.TextSoft;
        if (Minus != null) Minus.interactable = on && Minus.interactable;
        if (Plus != null) Plus.interactable = on && Plus.interactable;
    }
}

/// <summary>数のチップ（UiKit.Counter）</summary>
public class UiCounter
{
    public RectTransform Root, Unit;
    public Image Face;
    public UiIconDisc Disc;
    public TextMeshProUGUI Number;
    public void Set(string s) => Number.text = s;
    public void Set(int n) => Number.text = n.ToString();
}

/// <summary>見出しの帯（UiKit.Ribbon）</summary>
public class UiRibbon
{
    public RectTransform Root;
    public Image Face;
    public TextMeshProUGUI Label;
    public void SetText(string s) => Label.text = s;
}

/// <summary>カード（UiKit.Card）。中身は Content に足す</summary>
public class UiCard
{
    public RectTransform Root, Content;
    public Image Face, Line, Inner;
    internal Color faceOn;
    public bool Picked { get; private set; }

    internal static Color Light(Color c) => Color.Lerp(c, Color.white, 0.35f);

    /// <summary>選んだカード：1.12 倍で手前＋右上にチェック。外すと 1.0</summary>
    public void SetPicked(bool on)
    {
        Picked = on;
        if (on) UiSelect.Pick(Root, true);
        else UiSelect.Unpick(Root);
        UiSelect.Check(Root, on);
    }

    /// <summary>決まった後の選ばなかった方：面を line に</summary>
    public void SetMuted(bool on)
    {
        var c = on ? UiTokens.Line : faceOn;
        Face.color = c; Inner.color = c; Line.color = Light(c);
    }
}

/// <summary>丸い枠（UiKit.Slot）</summary>
public class UiSlot
{
    public RectTransform Root;
    public Image Face, Icon;
    public Button Button;
}

/// <summary>主ボタンの縞を横に流す（RawImage の uvRect。StripeSpeed px/秒。縞 64px の周期）</summary>
public class UiStripes : MonoBehaviour
{
    RawImage raw;
    RectTransform rt;
    void Awake() { raw = GetComponent<RawImage>(); rt = (RectTransform)transform; }
    void Update()
    {
        if (raw == null || raw.texture == null) return;
        float tw = raw.texture.width, th = raw.texture.height;
        var r = rt.rect;
        float x = Mathf.Repeat(UiEase.Now * UiTokens.StripeSpeed / tw, 1f);
        raw.uvRect = new Rect(-x, 0f, r.width / tw, r.height / th);
    }
}

/// <summary>次のフレームに、まだ UiSquish が無ければ付ける（画面のコードが UiSquish.Add を呼んでいたら2つにしない）</summary>
public class UiSquishAuto : MonoBehaviour
{
    internal UiButton b;
    void Start()
    {
        if (b != null && GetComponent<UiSquish>() == null) UiSquish.Add(b);
        Destroy(this);
    }
}
