// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-15、#190 あつ森の会話の吹き出し【12】【02】【03】）
// ヒューストンの吹き出しの見た目（#75。ui-guidelines 3章「部品の表」の会話の吹き出し）。組み立てて、言われたものを描くだけ（いつ何を出すかは HoustonBox）。
//   #190：blob_bubble（下の辺が波打つ形）、影・枠・しっぽなし（【12】【02】）。台詞 36・行間 1.5・茶（船内だけ 32。HoustonStyle.Cabin の注記）。
//   短い1行は真ん中、2行以上は左上から（【02】【12】）。固有名詞は go、数字は accent（【03】。HoustonScript.Mark）
//   左上に名前タグ（blob_pill、右上がりに 6°、高さの 4 割を吹き出しにかぶせる。【12】）と補助ラベル「Mission Control on Earth」（最初の間だけ）
//   右上に台詞に出てきた席のバッジ（だれの仕事か）。下の真ん中に ▼（caret_soft、縁に半分かぶせて上下に揺れる。【12】）、右下の縁にページ数の小さな札
//   NO SIGNAL・COMMS OFF のときは、文の代わりにアイコン＋見出し＋理由（COMMS OFF は ■ の席のバッジ付き）
//   上に声なしの警告の帯（danger の blob_pill、ふち 5）、ブリーフィングのときは画面の右下に Skip のヒントのピル（【10】）
// 注意：管制室の先生の小窓（Story/HoustonWindow.cs）が、この Root（名前 "Houston"）の子の legacy Text を読んで
//       台詞と NO SIGNAL を見分けている。最初の子の見えない Text（Probe）がそのためのもの。消さないこと。
//       Root に LayoutGroup を付けない（小窓が Root の子に入るので、並べ替えられてしまう）
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;
using S = HoustonStyle;

public class HoustonBubble
{
    public readonly RectTransform Root;
    public event Action OnTap, OnSkip;

    readonly Text probe;
    readonly RectTransform bubble, textArea, caret, linkView, warn, footerTag;
    readonly Image face;
    readonly TextMeshProUGUI body, footer, nameText, roleText, linkTitle, linkHint, warnText;
    readonly Image nameFace, nameIcon, linkIcon;
    readonly RectTransform roleTag;
    readonly UiSeatBadge linkSeat;
    readonly Dictionary<string, UiSeatBadge> seatBadges = new Dictionary<string, UiSeatBadge>();
    readonly UiButton skip;
    HoustonPlace place;

    public HoustonBubble(Transform parent)
    {
        Root = Ui.Rect("Houston", parent);
        var hit = Root.gameObject.AddComponent<Image>(); hit.color = T.Clear;   // どこを押しても次へ
        var tap = Root.gameObject.AddComponent<Button>();
        tap.transition = Selectable.Transition.None;
        tap.navigation = new Navigation { mode = Navigation.Mode.None };
        tap.onClick.AddListener(() => OnTap?.Invoke());

        // 小窓（HoustonWindow）が読む見えない文字。最初の子にする
        probe = Ui.Label(Root, "", 10);
        probe.name = "Probe"; probe.gameObject.SetActive(false);

        // 吹き出しの面（blob_bubble。影・枠・しっぽなし【12】【02】）
        bubble = Ui.Rect("Bubble", Root); UiKit.Fill(bubble);
        face = UiKit.Blob(bubble, T.BlobBubble, T.Panel, T.BubbleCornerX, "Face"); UiKit.Fill(face.rectTransform);

        textArea = Ui.Rect("TextArea", bubble); UiKit.Fill(textArea);
        body = UiKit.Text(textArea, "", TextStyle.Talk, T.Text, TextAlignmentOptions.TopLeft, false);
        UiKit.Fill(body.rectTransform);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.lineSpacing = TalkSpacing(body);
        body.enableAutoSizing = true; body.fontSizeMin = S.TextMin; body.fontSizeMax = T.TextTalk;
        body.overflowMode = TextOverflowModes.Ellipsis;
        body.richText = true;

        // NO SIGNAL・COMMS OFF
        linkView = Ui.Rect("Link", textArea); UiKit.Fill(linkView);
        linkIcon = UiKit.Icon(linkView, S.NoSignalIcon, S.LinkIcon, T.Danger);
        Pin(linkIcon.rectTransform, new Vector2(0f, 0.5f), S.LinkIcon, S.LinkIcon, Vector2.zero);
        linkSeat = UiKit.SeatBadge(linkView, Seats.Comms, T.BadgeS);
        Pin(linkSeat.Root, new Vector2(1f, 0.5f), T.BadgeS, T.BadgeS, Vector2.zero);
        linkTitle = UiKit.Text(linkView, "", TextStyle.Choice, T.Danger, TextAlignmentOptions.BottomLeft, false);
        linkHint = UiKit.Text(linkView, "", TextStyle.Hint, T.TextSoft, TextAlignmentOptions.TopLeft, false);
        linkHint.textWrappingMode = TextWrappingModes.Normal;
        linkHint.overflowMode = TextOverflowModes.Ellipsis;
        linkHint.enableAutoSizing = true; linkHint.fontSizeMin = T.TextLabel; linkHint.fontSizeMax = T.TextHint;

        // 下の真ん中：▼（caret_soft、accent。縁に半分かぶせる【12】）
        var cs = T.Art(T.CaretSoft);
        caret = UiKit.Shape(bubble, cs != null ? cs : T.Theme.caretDown, T.Accent, 0f, "Caret").rectTransform;
        caret.anchorMin = caret.anchorMax = new Vector2(0.5f, 0f); caret.pivot = new Vector2(0.5f, 0.5f);
        caret.sizeDelta = new Vector2(T.CaretW, T.CaretH); caret.anchoredPosition = Vector2.zero;

        // 右下の縁：ページ数の小さな札（bgDeep の blob_pill に textSoft 22。縁に半分かぶせて、文にかぶらないように）
        footerTag = UiKit.Row(bubble, 0f, TextAnchor.MiddleCenter, T.S2);
        footerTag.name = "Pages";
        footerTag.anchorMin = footerTag.anchorMax = new Vector2(1f, 0f); footerTag.pivot = new Vector2(1f, 0.5f);
        footerTag.anchoredPosition = new Vector2(-S.PadX, 0f);
        var ffit = footerTag.gameObject.AddComponent<ContentSizeFitter>();
        ffit.horizontalFit = ffit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var fface = UiKit.Blob(footerTag, T.BlobPill, T.BgDeep, S.PageTagH * 0.5f, "Face"); UiKit.Fill(fface.rectTransform); UiKit.Ignore(fface);
        footer = UiKit.Text(footerTag, "", TextStyle.Label, T.TextSoft);
        var fle = Ui.Size(footerTag, h: S.PageTagH); fle.minHeight = S.PageTagH;
        footerTag.gameObject.SetActive(false);

        // 左上：名前タグと補助ラベル（高さの 4 割を吹き出しにかぶせ、残りは上にはみ出す【12】）
        var tags = UiKit.Row(bubble, S.RoleGap, TextAnchor.MiddleLeft);
        tags.name = "Tags";
        tags.anchorMin = tags.anchorMax = new Vector2(0f, 1f); tags.pivot = new Vector2(0f, 0.5f);
        tags.anchoredPosition = new Vector2(S.TagX, TagCenterY);
        var fit = tags.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var nameTag = UiKit.Row(tags, T.S1, TextAnchor.MiddleCenter, S.TagPadX);
        nameTag.name = "Name";
        nameFace = UiKit.Blob(nameTag, T.BlobPill, S.TagColor(""), T.NameTagH * 0.5f, "Face"); UiKit.Fill(nameFace.rectTransform); UiKit.Ignore(nameFace);
        nameIcon = UiKit.Icon(nameTag, S.WarnIcon, T.IconS, T.Panel);   // critical のとき（状態は色＋アイコン）
        nameIcon.gameObject.SetActive(false);
        nameText = UiKit.Text(nameTag, S.Name, TextStyle.Hint, T.Panel);
        nameText.font = T.FontStrong;
        var nle = Ui.Size(nameTag, h: T.NameTagH); nle.minHeight = T.NameTagH;
        nameTag.localRotation = Quaternion.Euler(0f, 0f, S.TagTilt);
        roleTag = UiKit.Row(tags, T.S1, TextAnchor.MiddleCenter, T.S2);
        roleTag.name = "Role";
        var rface = UiKit.Blob(roleTag, T.BlobPill, T.BgDeep, S.RoleH * 0.5f, "Face"); UiKit.Fill(rface.rectTransform); UiKit.Ignore(rface);
        UiKit.Icon(roleTag, S.RoleIcon, T.IconS, T.TextSoft);
        roleText = UiKit.Text(roleTag, S.Role, TextStyle.Label, T.TextSoft);
        var rle = Ui.Size(roleTag, h: S.RoleH); rle.minHeight = S.RoleH;

        // 右上：台詞に出てきた席
        var seats = UiKit.Row(bubble, T.S1, TextAnchor.MiddleRight);
        seats.name = "Seats";
        seats.anchorMin = seats.anchorMax = new Vector2(1f, 1f); seats.pivot = new Vector2(1f, 0.5f);
        seats.anchoredPosition = new Vector2(-S.TagX, TagCenterY);
        var sfit = seats.gameObject.AddComponent<ContentSizeFitter>();
        sfit.horizontalFit = sfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        foreach (var s in Seats.All)
        {
            var b = UiKit.SeatBadge(seats, s, S.SeatBadge);
            b.Root.gameObject.SetActive(false);
            seatBadges[s] = b;
        }

        // 上：声なしの警告の帯（danger の blob_pill、ふち 5 panel）
        warn = Ui.Rect("Warning", Root);
        warn.anchorMin = new Vector2(0f, 1f); warn.anchorMax = new Vector2(1f, 1f); warn.pivot = new Vector2(0.5f, 0f);
        warn.sizeDelta = new Vector2(0f, S.WarnH); warn.anchoredPosition = new Vector2(0f, TagCenterY + T.NameTagH * 0.5f + S.WarnGap);
        UiKit.Fill(UiKit.Blob(warn, T.BlobPill, T.Panel, S.WarnH * 0.5f, "Rim").rectTransform);
        UiKit.Fill(UiKit.Blob(warn, T.BlobPill, T.Danger, S.WarnH * 0.5f - T.RimButton, "Face").rectTransform, T.RimButton);
        var wicon = UiKit.Icon(warn, S.WarnIcon, T.IconM, T.Panel);
        Pin(wicon.rectTransform, new Vector2(0f, 0.5f), T.IconM, T.IconM, new Vector2(T.S4, 0f));
        warnText = UiKit.Text(warn, "", TextStyle.Body, T.Panel, TextAlignmentOptions.MidlineLeft, false);
        UiKit.Fill(warnText.rectTransform);
        warnText.rectTransform.offsetMin = new Vector2(T.S4 + T.IconM + T.S2, T.S1); warnText.rectTransform.offsetMax = new Vector2(-T.S4, -T.S1);
        warnText.textWrappingMode = TextWrappingModes.Normal;
        warnText.enableAutoSizing = true; warnText.fontSizeMin = T.TextLabel; warnText.fontSizeMax = T.TextBody;
        warnText.overflowMode = TextOverflowModes.Ellipsis;
        warn.gameObject.SetActive(false);

        // 画面の右下：Skip（ブリーフィングだけ。ヒントのピル【10】【09】。#190：紺の枠の SKIP をやめた）
        skip = UiKit.Hint(Root, S.SkipIcon, S.Skip, () => OnSkip?.Invoke());
        skip.Root.anchorMin = skip.Root.anchorMax = skip.Root.pivot = new Vector2(1f, 0f);
        skip.Root.gameObject.SetActive(false);

        place = S.Cabin;
        Root.gameObject.SetActive(false);
    }

    /// <summary>名前タグの中心の高さ（吹き出しの上の縁から）。タグの高さの TagOverlap だけ吹き出しにかぶる</summary>
    static float TagCenterY => T.NameTagH * (0.5f - S.TagOverlap);

    /// <summary>行間 1.5 にする TMP の lineSpacing（字の 1/100 単位で、書体の行の高さに足す）</summary>
    static float TalkSpacing(TMP_Text t)
    {
        var fi = t.font.faceInfo;
        return Mathf.Max(0f, (T.TalkLineHeight - fi.lineHeight / fi.pointSize) * 100f);
    }

    static void Pin(RectTransform r, Vector2 anchor, float w, float h, Vector2 pos)
    {
        r.anchorMin = r.anchorMax = r.pivot = anchor;
        r.sizeDelta = new Vector2(w, h); r.anchoredPosition = pos;
    }

    // ================================================================== 置き場所
    public void Place(HoustonPlace p)
    {
        place = p;
        Root.anchorMin = new Vector2(p.x0, p.anchorY); Root.anchorMax = new Vector2(p.x1, p.anchorY);
        Root.pivot = new Vector2(0.5f, p.pivotY);
        Root.sizeDelta = new Vector2(0f, p.height);
        Root.anchoredPosition = new Vector2(0f, p.offsetY);
        // 台詞の大きさと上下の内側（船内だけ 32・24／16）
        textArea.offsetMin = new Vector2(S.PadX, p.padBottom); textArea.offsetMax = new Vector2(-S.PadX, -p.padTop);
        body.fontSizeMax = p.textSize; body.fontSize = p.textSize;
        // blob_bubble の上下の縁（96＋96）が高さに入るように縮める（9-slice の縁が高さを超えると形がつぶれる）
        var s = face.sprite;
        if (s != null && face.type == Image.Type.Sliced && s.border.y + s.border.w > 0f)
            face.pixelsPerUnitMultiplier = 1f / Mathf.Min(1f, p.height * S.BubbleFit / (s.border.y + s.border.w));
        PlaceSkip();
    }

    /// <summary>Skip を画面（載せ先）の右下の角に置く。Root の右下からの距離を、載せ先の大きさと置き場所の割合で出す</summary>
    void PlaceSkip()
    {
        if (!skip.Root.gameObject.activeSelf || !(Root.parent is RectTransform pr)) return;
        var le = skip.Root.GetComponent<LayoutElement>();
        skip.Root.sizeDelta = new Vector2(le.preferredWidth, T.Hit);
        float pw = pr.rect.width, ph = pr.rect.height;
        float bottom = ph * place.anchorY + place.offsetY - place.pivotY * place.height;   // 載せ先の下の端から Root の下の端まで
        skip.Root.anchoredPosition = new Vector2(pw * (1f - place.x1) - S.SkipEdge, -bottom + S.SkipEdge - (T.Hit - T.HintH) * 0.5f);
    }

    // ================================================================== 描く
    public void SetVisible(bool root, bool bubbleOn)
    {
        if (Root.gameObject.activeSelf != root) Root.gameObject.SetActive(root);
        if (bubble.gameObject.activeSelf != bubbleOn) bubble.gameObject.SetActive(bubbleOn);
    }

    /// <summary>台詞を出す</summary>
    /// <param name="marked">色の印を付けたページの文</param>
    /// <param name="plain">印の無い文（小窓が読む）</param>
    /// <param name="more">続きがある（▼ を出す）</param>
    public void ShowLine(string speakerName, bool houston, Color tag, bool firstTime, string marked, string plain, List<string> seats, bool more, string footerText)
    {
        SetName(speakerName, tag);
        roleTag.gameObject.SetActive(houston && (firstTime || S.RoleAlways));   // #146（Fable）：説明のラベルは最初の間だけ（#189：小窓の下の帯に On Earth がいつも出ている）
        roleText.text = firstTime ? S.RoleFirst : S.Role;
        body.gameObject.SetActive(true);
        body.text = marked;
        // 短い1行は真ん中、2行以上は左上から（【02】【12】）。幅は大きい方の字（fontSizeMax）で測る
        bool oneLine = body.GetPreferredValues(marked).x <= textArea.rect.width;
        body.alignment = oneLine ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
        linkView.gameObject.SetActive(false);
        foreach (var kv in seatBadges) kv.Value.Root.gameObject.SetActive(seats != null && seats.Contains(kv.Key));
        caret.gameObject.SetActive(more);
        SetFooter(place.name == "cabin" ? "" : footerText);   // 船内はページ数の札を出さない（続きは ▼ が示す。Fable 採点 b-10）
        probe.text = plain;
    }

    void SetName(string speakerName, Color tag)
    {
        nameText.text = speakerName;
        nameFace.color = tag;
        nameIcon.gameObject.SetActive(tag == T.Danger);
    }

    void SetFooter(string s)
    {
        bool on = !string.IsNullOrEmpty(s);
        footer.text = on ? s : "";
        if (footerTag.gameObject.activeSelf != on) footerTag.gameObject.SetActive(on);
    }

    /// <summary>通信できないとき。noSignal=false なら COMMS OFF</summary>
    public void ShowLink(bool noSignal, string hint)
    {
        SetName(S.Name, T.TextSoft);
        roleTag.gameObject.SetActive(S.RoleAlways);
        roleText.text = S.Role;
        body.gameObject.SetActive(false);
        linkView.gameObject.SetActive(true);
        linkIcon.sprite = T.Icon(noSignal ? S.NoSignalIcon : S.CommsOffIcon);
        linkIcon.color = noSignal ? T.Danger : T.Text;
        linkTitle.text = noSignal ? S.NoSignalTitle : S.CommsOffTitle;
        linkTitle.color = noSignal ? T.Danger : T.Text;
        linkHint.text = hint;
        linkSeat.Root.gameObject.SetActive(!noSignal);
        float left = S.LinkIcon + T.S2, right = noSignal ? 0f : T.BadgeS + T.S2;
        var tr = linkTitle.rectTransform; tr.anchorMin = new Vector2(0f, 0.5f); tr.anchorMax = new Vector2(1f, 1f);
        tr.offsetMin = new Vector2(left, 0f); tr.offsetMax = new Vector2(-right, 0f);
        var hr = linkHint.rectTransform; hr.anchorMin = new Vector2(0f, 0f); hr.anchorMax = new Vector2(1f, 0.5f);
        hr.offsetMin = new Vector2(left, 0f); hr.offsetMax = new Vector2(-right, 0f);
        foreach (var kv in seatBadges) kv.Value.Root.gameObject.SetActive(false);
        caret.gameObject.SetActive(false);
        SetFooter("");
        probe.text = noSignal ? S.NoSignal : S.CommsOff;   // HoustonWindow はこの2つの文字で「通信できない」を見分ける（画面の言葉とは別）
    }

    public void ShowNothing() { probe.text = ""; }

    public void ShowWarning(string text)
    {
        bool on = !string.IsNullOrEmpty(text);
        if (on) warnText.text = text;
        if (warn.gameObject.activeSelf != on) warn.gameObject.SetActive(on);
    }

    public bool WarningOn => warn.gameObject.activeSelf;

    public void ShowSkip(bool on)
    {
        if (skip.Root.gameObject.activeSelf != on) skip.Root.gameObject.SetActive(on);
        if (on) PlaceSkip();
    }

    /// <summary>▼ を上下に揺らす（毎フレーム）。Skip は載せ先の大きさが変わっても右下の角に付いていく</summary>
    public void Animate(float now)
    {
        if (skip.Root.gameObject.activeInHierarchy) PlaceSkip();
        if (!caret.gameObject.activeInHierarchy) return;
        float k = Mathf.Sin(now * Mathf.PI * 2f / S.CaretBobS) * 0.5f + 0.5f;
        caret.anchoredPosition = new Vector2(0f, -k * S.CaretBobPx);
    }

    /// <summary>今の文（色の印なし）。小窓（HoustonWindow）と同じものを読む</summary>
    public string ProbeText => probe.text;
}
