// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// ヒューストンの吹き出しの見た目（#75。ui-guidelines 3章「吹き出し」）。組み立てて、言われたものを描くだけ（いつ何を出すかは HoustonBox）。
//   panel の角丸 40 ＋影 6。左上に名前タグ（go のピル、−4°、上の端に半分かぶせる）と補助ラベル「Mission Control on Earth」
//   右上に台詞に出てきた席のバッジ（だれの仕事か）。下の真ん中に ▼（続きがあるとき、上下に揺れる）、右下にページ数
//   NO SIGNAL・COMMS OFF のときは、文の代わりにアイコン＋見出し＋理由（COMMS OFF は ■ の席のバッジ付き）
//   上に声なしの警告の帯（danger のピル）、ブリーフィングのときは右上に SKIP
// #71 の共通の吹き出し（UiKitHud.Bubble）ができたら、このファイルを差し替える（HoustonBox から見える形は下の public だけ）。
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
    readonly RectTransform bubble, textArea, caret, linkView, warn;
    readonly TextMeshProUGUI body, footer, nameText, roleText, linkTitle, linkHint, warnText;
    readonly Image nameFace, linkIcon;
    readonly RectTransform roleTag;
    readonly UiSeatBadge linkSeat;
    readonly Dictionary<string, UiSeatBadge> seatBadges = new Dictionary<string, UiSeatBadge>();
    readonly UiButton skip;
    float caretBaseY;

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

        // 吹き出しの面（影つき）
        bubble = Ui.Rect("Bubble", Root); UiKit.Fill(bubble);
        UiKit.Surface(bubble, T.RadiusL, T.Panel);

        textArea = Ui.Rect("TextArea", bubble); UiKit.Fill(textArea);
        textArea.offsetMin = new Vector2(S.PadX, S.PadBottom); textArea.offsetMax = new Vector2(-S.PadX, -S.PadTop);
        body = UiKit.Text(textArea, "", TextStyle.Body, T.Ink, TextAlignmentOptions.TopLeft, false);
        UiKit.Fill(body.rectTransform);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.enableAutoSizing = true; body.fontSizeMin = S.TextMin; body.fontSizeMax = T.TextBody;
        body.overflowMode = TextOverflowModes.Ellipsis;
        body.richText = true;

        // NO SIGNAL・COMMS OFF
        linkView = Ui.Rect("Link", textArea); UiKit.Fill(linkView);
        linkIcon = UiKit.Icon(linkView, S.NoSignalIcon, S.LinkIcon, T.Danger);
        Pin(linkIcon.rectTransform, new Vector2(0f, 0.5f), S.LinkIcon, S.LinkIcon, Vector2.zero);
        linkSeat = UiKit.SeatBadge(linkView, Seats.Comms, T.BadgeS);
        Pin(linkSeat.Root, new Vector2(1f, 0.5f), T.BadgeS, T.BadgeS, Vector2.zero);
        linkTitle = UiKit.Text(linkView, "", TextStyle.Sub, T.Danger, TextAlignmentOptions.BottomLeft, false);
        linkHint = UiKit.Text(linkView, "", TextStyle.Label, T.InkSub, TextAlignmentOptions.TopLeft, false);
        linkHint.textWrappingMode = TextWrappingModes.Normal;
        linkHint.overflowMode = TextOverflowModes.Ellipsis;

        // 下：▼ とページ数
        caret = UiKit.Shape(bubble, T.Theme.caretDown, T.Accent, 0f, "Caret").rectTransform;
        caretBaseY = S.PadBottom * 0.5f - S.CaretSize * 0.3f;
        Pin(caret, new Vector2(0.5f, 0f), S.CaretSize, S.CaretSize * 14f / 24f, new Vector2(0f, caretBaseY));
        footer = UiKit.Text(bubble, "", TextStyle.Label, T.InkSub, TextAlignmentOptions.BottomRight, false);
        var fr = footer.rectTransform;
        fr.anchorMin = new Vector2(0f, 0f); fr.anchorMax = new Vector2(1f, 0f); fr.pivot = new Vector2(0.5f, 0f);
        fr.sizeDelta = new Vector2(-S.PadX * 2f, S.PadBottom + T.S1); fr.anchoredPosition = new Vector2(0f, 2f);

        // 左上：名前タグと補助ラベル（上の端に半分かぶせる）
        var tags = UiKit.Row(bubble, T.S1, TextAnchor.MiddleLeft);
        tags.name = "Tags";
        tags.anchorMin = tags.anchorMax = new Vector2(0f, 1f); tags.pivot = new Vector2(0f, 0.5f);
        tags.anchoredPosition = new Vector2(S.PadX - T.S2, 0f);
        var fit = tags.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var nameTag = UiKit.Tag(tags, S.Name, T.Go, T.Panel);
        nameTag.localRotation = Quaternion.Euler(0f, 0f, S.TagTilt);
        nameFace = nameTag.Find("Face").GetComponent<Image>();
        nameText = nameTag.GetComponentInChildren<TextMeshProUGUI>();
        roleTag = UiKit.Row(tags, T.S1, TextAnchor.MiddleCenter, T.S2);
        roleTag.name = "Role";
        var rframe = UiKit.Pill(roleTag, T.Line, T.TagH, "Frame"); UiKit.Fill(rframe.rectTransform); UiKit.Ignore(rframe);
        var rface = UiKit.Pill(roleTag, T.Bg, T.TagH - T.Border * 2f, "Face"); UiKit.Fill(rface.rectTransform, T.Border); UiKit.Ignore(rface);
        UiKit.Icon(roleTag, S.RoleIcon, T.IconS, T.InkSub);
        roleText = UiKit.Text(roleTag, S.Role, TextStyle.Label, T.InkSub);
        var rle = Ui.Size(roleTag, h: T.TagH); rle.minHeight = T.TagH;

        // 右上：台詞に出てきた席
        var seats = UiKit.Row(bubble, T.S1, TextAnchor.MiddleRight);
        seats.name = "Seats";
        seats.anchorMin = seats.anchorMax = new Vector2(1f, 1f); seats.pivot = new Vector2(1f, 0.5f);
        seats.anchoredPosition = new Vector2(-(S.PadX - T.S2), 0f);
        var sfit = seats.gameObject.AddComponent<ContentSizeFitter>();
        sfit.horizontalFit = sfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        foreach (var s in Seats.All)
        {
            var b = UiKit.SeatBadge(seats, s, S.SeatBadge);
            b.Root.gameObject.SetActive(false);
            seatBadges[s] = b;
        }

        // 上：声なしの警告の帯
        warn = Ui.Rect("Warning", Root);
        warn.anchorMin = new Vector2(0f, 1f); warn.anchorMax = new Vector2(1f, 1f); warn.pivot = new Vector2(0.5f, 0f);
        warn.sizeDelta = new Vector2(0f, S.WarnH); warn.anchoredPosition = new Vector2(0f, T.TagH * 0.5f + S.WarnGap - T.S2);
        UiKit.Fill(UiKit.Pill(warn, T.Danger, S.WarnH, "Face").rectTransform);
        var wicon = UiKit.Icon(warn, S.WarnIcon, T.IconM, T.Panel);
        Pin(wicon.rectTransform, new Vector2(0f, 0.5f), T.IconM, T.IconM, new Vector2(T.S4, 0f));
        warnText = UiKit.Text(warn, "", TextStyle.Body, T.Panel, TextAlignmentOptions.MidlineLeft, false);
        UiKit.Fill(warnText.rectTransform);
        warnText.rectTransform.offsetMin = new Vector2(T.S4 + T.IconM + T.S2, T.S1); warnText.rectTransform.offsetMax = new Vector2(-T.S4, -T.S1);
        warnText.textWrappingMode = TextWrappingModes.Normal;
        warnText.enableAutoSizing = true; warnText.fontSizeMin = S.TextMin; warnText.fontSizeMax = T.TextBody;
        warnText.overflowMode = TextOverflowModes.Ellipsis;
        warn.gameObject.SetActive(false);

        // 右上の上：SKIP（ブリーフィングだけ）
        skip = UiKit.SecondaryButton(Root, S.Skip, null, () => OnSkip?.Invoke());
        var sle = skip.Root.GetComponent<LayoutElement>();
        // #146（Fable）：吹き出しの右下の外。右上だと後ろの設計の画面の文字に重なって見えた
        skip.Root.anchorMin = skip.Root.anchorMax = new Vector2(1f, 0f); skip.Root.pivot = new Vector2(1f, 1f);
        skip.Root.sizeDelta = new Vector2(sle.preferredWidth, sle.preferredHeight);
        skip.Root.anchoredPosition = new Vector2(0f, -S.SkipGap + (T.Hit - T.SecondaryH) * 0.5f);
        skip.Root.gameObject.SetActive(false);

        Root.gameObject.SetActive(false);
    }

    static void Pin(RectTransform r, Vector2 anchor, float w, float h, Vector2 pos)
    {
        r.anchorMin = r.anchorMax = r.pivot = anchor;
        r.sizeDelta = new Vector2(w, h); r.anchoredPosition = pos;
    }

    // ================================================================== 置き場所
    public void Place(HoustonPlace p)
    {
        Root.anchorMin = new Vector2(p.x0, p.anchorY); Root.anchorMax = new Vector2(p.x1, p.anchorY);
        Root.pivot = new Vector2(0.5f, p.pivotY);
        Root.sizeDelta = new Vector2(0f, p.height);
        Root.anchoredPosition = new Vector2(0f, p.offsetY);
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
        nameText.text = speakerName;
        nameFace.color = tag;
        roleTag.gameObject.SetActive(houston && (firstTime || S.RoleAlways));   // #146（Fable）：説明のラベルは最初の間だけ（#189：小窓の下の帯に ON EARTH がいつも出ている）
        roleText.text = firstTime ? S.RoleFirst : S.Role;
        body.gameObject.SetActive(true);
        body.text = marked;
        linkView.gameObject.SetActive(false);
        foreach (var kv in seatBadges) kv.Value.Root.gameObject.SetActive(seats != null && seats.Contains(kv.Key));
        caret.gameObject.SetActive(more);
        footer.text = footerText ?? "";
        probe.text = plain;
    }

    /// <summary>通信できないとき。noSignal=false なら COMMS OFF</summary>
    public void ShowLink(bool noSignal, string hint)
    {
        nameText.text = S.Name;
        nameFace.color = T.InkSub;
        roleTag.gameObject.SetActive(S.RoleAlways);
        roleText.text = S.Role;
        body.gameObject.SetActive(false);
        linkView.gameObject.SetActive(true);
        linkIcon.sprite = T.Icon(noSignal ? S.NoSignalIcon : S.CommsOffIcon);
        linkIcon.color = noSignal ? T.Danger : T.Ink;
        linkTitle.text = noSignal ? S.NoSignal : S.CommsOff;
        linkTitle.color = noSignal ? T.Danger : T.Ink;
        linkHint.text = hint;
        linkSeat.Root.gameObject.SetActive(!noSignal);
        float left = S.LinkIcon + T.S2, right = noSignal ? 0f : T.BadgeS + T.S2;
        var tr = linkTitle.rectTransform; tr.anchorMin = new Vector2(0f, 0.5f); tr.anchorMax = new Vector2(1f, 1f);
        tr.offsetMin = new Vector2(left, 0f); tr.offsetMax = new Vector2(-right, 0f);
        var hr = linkHint.rectTransform; hr.anchorMin = new Vector2(0f, 0f); hr.anchorMax = new Vector2(1f, 0.5f);
        hr.offsetMin = new Vector2(left, 0f); hr.offsetMax = new Vector2(-right, 0f);
        foreach (var kv in seatBadges) kv.Value.Root.gameObject.SetActive(false);
        caret.gameObject.SetActive(false);
        footer.text = "";
        probe.text = noSignal ? S.NoSignal : S.CommsOff;   // HoustonWindow はこの2つの文字で「通信できない」を見分ける
    }

    public void ShowNothing() { probe.text = ""; }

    public void ShowWarning(string text)
    {
        bool on = !string.IsNullOrEmpty(text);
        if (on) warnText.text = text;
        if (warn.gameObject.activeSelf != on) warn.gameObject.SetActive(on);
    }

    public bool WarningOn => warn.gameObject.activeSelf;

    public void ShowSkip(bool on) { if (skip.Root.gameObject.activeSelf != on) skip.Root.gameObject.SetActive(on); }

    /// <summary>▼ を上下に揺らす（毎フレーム）</summary>
    public void Animate(float now)
    {
        if (!caret.gameObject.activeInHierarchy) return;
        float k = Mathf.Sin(now * Mathf.PI * 2f / S.CaretBobS) * 0.5f + 0.5f;
        caret.anchoredPosition = new Vector2(0f, caretBaseY - k * S.CaretBobPx);
    }

    /// <summary>今の文（色の印なし）。小窓（HoustonWindow）と同じものを読む</summary>
    public string ProbeText => probe.text;
}
