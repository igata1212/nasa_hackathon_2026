// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// 設計の画面（#73）の小さな部品：部品のカード・± のボタン・ゲージ・選択のピル・要件の札・アイコン＋数字。
// #71 が UiKitHud.cs に共通の Gauge・ReqRow などを作っている。できたら、この中の同じものを差し替える（DesignGauge → Gauge、DesignGoalChip → ReqRow）。
// 色と大きさは UiTokens と DesignTokens から読む（ここに色コードや px を書かない）
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;
using D = DesignTokens;

public static class DesignUi
{
    /// <summary>文字。h を渡すと高さを固定。wrap＝折り返す、ellipsis＝はみ出たら…、shrink＝はみ出たら補助ラベル（22）まで縮める。
    /// ellipsis・shrink・wrap のときは幅を親に合わせる（preferredWidth 0・flexibleWidth 1）</summary>
    public static TextMeshProUGUI Text(Transform p, string s, TextStyle st, Color c, TextAlignmentOptions a = TextAlignmentOptions.MidlineLeft,
        float h = -1f, bool wrap = false, bool ellipsis = false, bool shrink = false)
    {
        bool fit = wrap || ellipsis || shrink;
        var t = UiKit.Text(p, s, st, c, a, !fit && h < 0f);
        if (wrap) t.textWrappingMode = TextWrappingModes.Normal;
        if (ellipsis) t.overflowMode = TextOverflowModes.Ellipsis;
        if (shrink) { t.enableAutoSizing = true; t.fontSizeMin = T.TextLabel; t.fontSizeMax = t.fontSize; }
        var le = Ui.Size(t);
        if (h >= 0f) { le.preferredHeight = h; le.minHeight = h; }
        if (fit) { le.preferredWidth = 0f; le.flexibleWidth = 1f; le.minWidth = 0f; }
        return t;
    }

    /// <summary>色を付けただけの四角（ゲージの中身など）</summary>
    public static Image Rect(Transform p, Color c, string name)
    {
        var img = Ui.Rect(name, p).gameObject.AddComponent<Image>();
        img.color = c; img.raycastTarget = false;
        return img;
    }

    /// <summary>縦に流す入れ物。中身が入りきらないときだけ縦にずらせる（iPhone の横画面など）。中身を足す Col を返す</summary>
    public static RectTransform Scroll(Transform p, float spacing, string name)
    {
        var vp = Ui.Rect(name, p);
        vp.gameObject.AddComponent<RectMask2D>();
        var hit = vp.gameObject.AddComponent<Image>(); hit.color = T.Clear;   // 隙間をなぞってもずらせるように
        var le = Ui.Size(vp, h: 0f, flexH: 1f); le.minHeight = 0f;

        var content = Ui.Rect("Content", vp);
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = Vector2.one; content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        var l = content.gameObject.AddComponent<VerticalLayoutGroup>();
        l.spacing = spacing; l.childAlignment = TextAnchor.UpperLeft;
        l.padding = new RectOffset(0, 0, 0, Mathf.RoundToInt(T.ShadowY + T.S1));   // 一番下のカードの影が切れないように
        l.childControlWidth = l.childControlHeight = true;
        l.childForceExpandWidth = true; l.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var sr = vp.gameObject.AddComponent<ScrollRect>();
        sr.viewport = vp; sr.content = content;
        sr.horizontal = false; sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = T.S4;
        return content;
    }

    /// <summary>押せる面（影＋本体）。UiKit のボタンと同じ押し心地（ColorTint と UiPress）</summary>
    public static Button Press(RectTransform root, RectTransform body, Graphic target, Action on)
    {
        var b = root.gameObject.AddComponent<Button>();
        b.targetGraphic = target;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        var cb = b.colors;
        cb.normalColor = cb.selectedColor = cb.disabledColor = Color.white;
        cb.highlightedColor = T.TintHover; cb.pressedColor = T.TintPressed; cb.fadeDuration = T.TintFade;
        b.colors = cb;
        if (on != null) b.onClick.AddListener(() => on());
        root.gameObject.AddComponent<UiPress>().Init(body, b);
        return b;
    }

    /// <summary>影（同じ形を下に ShadowY ずらす）。root いっぱい</summary>
    public static Image Shadow(RectTransform root, Sprite sprite, float radius)
    {
        var sh = UiKit.Shape(root, sprite, T.Shadow, radius, "Shadow"); UiKit.Fill(sh.rectTransform); UiKit.Ignore(sh);
        sh.rectTransform.offsetMin = new Vector2(0f, -T.ShadowY); sh.rectTransform.offsetMax = new Vector2(0f, -T.ShadowY);
        return sh;
    }
}

/// <summary>アイコン＋短い文字（質量 12、O₂ 100% など）。アイコン名 "O2" は O₂ の丸（ui-guidelines 4章：酸素のアイコンは無いので文字）</summary>
public class DesignStat
{
    public RectTransform Root;
    public Image Icon;
    public TextMeshProUGUI IconText, Text;

    public static DesignStat Make(Transform p, string icon, string text, TextStyle st, float iconSize, Color c)
    {
        var s = new DesignStat();
        s.Root = UiKit.Row(p, T.S1, TextAnchor.MiddleLeft);
        s.Root.name = "Stat_" + icon;
        if (icon == "O2")
        {
            s.Icon = UiKit.Pill(s.Root, c, iconSize, "O2");
            UiKit.Fixed(s.Icon, iconSize, iconSize);
            s.IconText = UiKit.Text(s.Icon.transform, "O₂", TextStyle.Tag, T.Panel, TextAlignmentOptions.Center, false);
            s.IconText.fontSize = iconSize * 0.5f;
            UiKit.Fill(s.IconText.rectTransform); UiKit.Ignore(s.IconText);
        }
        else s.Icon = UiKit.Icon(s.Root, icon, iconSize, c);
        s.Text = UiKit.Text(s.Root, text, st, c, TextAlignmentOptions.MidlineLeft);
        s.SetColor(c);
        return s;
    }

    public void SetColor(Color c)
    {
        Icon.color = c;
        Text.color = c;
        if (IconText != null) IconText.color = c == T.Panel ? T.Ink : T.Panel;   // 丸の中の文字は面と逆の色
    }
}

/// <summary>± のボタン（円 72、押せる範囲 96）。書体に「−」が無いので、棒を描いて ＋・− にする</summary>
public class DesignStepButton
{
    public RectTransform Root, Body;
    public Button Button;
    public Image Frame, Face;
    readonly List<Image> glyph = new List<Image>();

    public static DesignStepButton Make(Transform p, bool plus, Action on)
    {
        var b = new DesignStepButton();
        b.Root = Ui.Rect(plus ? "Plus" : "Minus", p);
        var hit = b.Root.gameObject.AddComponent<Image>(); hit.color = T.Clear;
        UiKit.Fixed(b.Root, T.Hit, T.Hit);

        var sh = UiKit.Pill(b.Root, T.Shadow, D.StepD, "Shadow");
        UiKit.Center(sh.rectTransform, D.StepD, D.StepD); sh.rectTransform.anchoredPosition = new Vector2(0f, -T.ShadowY);
        b.Body = Ui.Rect("Body", b.Root); UiKit.Center(b.Body, D.StepD, D.StepD);
        b.Frame = UiKit.Pill(b.Body, T.Ink, D.StepD, "Frame"); UiKit.Fill(b.Frame.rectTransform);
        b.Face = UiKit.Pill(b.Body, T.Panel, D.StepD - T.Border * 2f, "Face"); UiKit.Fill(b.Face.rectTransform, T.Border);
        b.glyph.Add(Bar(b.Body, D.StepGlyphLen, D.StepGlyphThick));
        if (plus) b.glyph.Add(Bar(b.Body, D.StepGlyphThick, D.StepGlyphLen));
        b.Button = DesignUi.Press(b.Root, b.Body, b.Face, on);
        return b;
    }

    static Image Bar(Transform p, float w, float h)
    {
        var img = UiKit.Pill(p, T.Ink, Mathf.Min(w, h), "Bar");
        UiKit.Center(img.rectTransform, w, h);
        return img;
    }

    /// <summary>押せないとき（最小・最大に届いた）：枠と棒を line にする</summary>
    public void SetEnabled(bool on)
    {
        Button.interactable = on;
        Frame.color = on ? T.Ink : T.Line;
        foreach (var g in glyph) g.color = on ? T.Ink : T.Line;
    }
}

/// <summary>部品のカード：アイコン＋名前／1行の説明／質量・費用（・科学の値）、右に − 個数 ＋。積んでいる部品は枠が go</summary>
public class DesignPartCard
{
    public string Id;
    public int Min, Max;
    public RectTransform Root;
    public Image Frame;
    public TextMeshProUGUI Name, Line, Count;
    public DesignStepButton Minus, Plus;

    public static DesignPartCard Make(Transform p, DesignPartCopy copy, string line, string mass, string cost, string science,
        int min, int max, Action<int> onDelta)
    {
        var c = new DesignPartCard { Id = copy.Id, Min = min, Max = max };
        c.Root = UiKit.Row(p, T.S1, TextAnchor.MiddleLeft, D.PartPadX, D.PartPadY);
        c.Root.name = "Part_" + copy.Id;
        var le = Ui.Size(c.Root, w: 0f, h: D.PartRowH, flexW: 1f); le.minHeight = D.PartRowH; le.minWidth = 0f;
        DesignUi.Shadow(c.Root, T.Round(T.RadiusM), T.RadiusM);
        c.Frame = UiKit.Round(c.Root, T.RadiusM, D.CardOff, "Frame"); UiKit.Fill(c.Frame.rectTransform); UiKit.Ignore(c.Frame);
        var face = UiKit.Round(c.Root, T.RadiusM - T.Border, T.Panel, "Face"); UiKit.Fill(face.rectTransform, T.Border); UiKit.Ignore(face);

        // 左：アイコン＋名前／説明／数字
        var col = UiKit.Col(c.Root, 0f, TextAnchor.MiddleLeft, 0f, 0f, true);
        var cle = Ui.Size(col, w: 0f, flexW: 1f); cle.minWidth = 0f;
        var head = UiKit.Row(col, T.S1, TextAnchor.MiddleLeft);
        Ui.Size(head, h: D.PartNameH);
        DesignStat.Make(head, copy.Icon, "", TextStyle.Label, T.IconM, T.Ink).Text.gameObject.SetActive(false);   // アイコンだけ
        c.Name = DesignUi.Text(head, copy.Name, TextStyle.Body, T.Ink, TextAlignmentOptions.MidlineLeft, D.PartNameH, shrink: true);
        c.Line = DesignUi.Text(col, line, TextStyle.Label, T.InkSub, TextAlignmentOptions.MidlineLeft, D.PartLineH, ellipsis: true);
        var stats = UiKit.Row(col, D.StatGap, TextAnchor.MiddleLeft);
        Ui.Size(stats, h: D.PartLineH);
        DesignStat.Make(stats, "mass", mass, TextStyle.Label, T.IconS, T.Ink);
        DesignStat.Make(stats, "cost", cost, TextStyle.Label, T.IconS, T.Ink);
        if (science != null) DesignStat.Make(stats, "science", science, TextStyle.Label, T.IconS, T.Ink);
        Ui.Size(UiKit.Space(stats, 0f), flexW: 1f);

        // 右：− 個数 ＋（押せる範囲 96 どうしを詰めて並べる）
        var step = UiKit.Row(c.Root, 0f, TextAnchor.MiddleCenter);
        c.Minus = DesignStepButton.Make(step, false, () => onDelta(-1));
        c.Count = UiKit.Text(step, "0", TextStyle.Number, T.Ink, TextAlignmentOptions.Center);
        UiKit.Fixed(c.Count, D.CountW, T.TextNumber * T.HeadingLineHeight);
        c.Plus = DesignStepButton.Make(step, true, () => onDelta(+1));
        return c;
    }

    public void Set(int n)
    {
        Count.text = n.ToString();
        Count.color = n > 0 ? T.Ink : T.InkSub;
        Frame.color = n > 0 ? D.CardOnFrame : D.CardOff;
        Minus.SetEnabled(n > Min);
        Plus.SetEnabled(n < Max);
    }
}

/// <summary>重さ・費用のゲージ：溝（line）、中身（go。超えたら danger＋斜め縞）、上限の線（ink）。線より右は「だめな所」を薄い danger で塗る。
/// 右端は上限×GaugeHeadroom なので、上限を超えた分も見える（色は描くだけ。超えたかどうかはサーバーの判定を渡す）</summary>
public class DesignGauge
{
    public RectTransform Root, Fill;
    public Image FillImg, Stripes;

    public static DesignGauge Make(Transform p)
    {
        var g = new DesignGauge();
        float limit = 1f / D.GaugeHeadroom;
        g.Root = Ui.Rect("Gauge", p);
        var le = Ui.Size(g.Root, h: D.GaugeH + D.LimitLineOver * 2f); le.minHeight = le.preferredHeight;

        var track = UiKit.Pill(g.Root, T.Line, D.GaugeH, "Track");
        UiKit.MiddleBand(track.rectTransform, D.GaugeH);
        track.gameObject.AddComponent<Mask>().showMaskGraphic = true;   // 中身を溝のピルの形で切る

        var zone = DesignUi.Rect(track.transform, D.OverZone, "OverZone").rectTransform;
        zone.anchorMin = new Vector2(limit, 0f); zone.anchorMax = Vector2.one; zone.offsetMin = zone.offsetMax = Vector2.zero;

        g.FillImg = DesignUi.Rect(track.transform, T.Go, "Fill");
        g.Fill = g.FillImg.rectTransform;
        g.Fill.anchorMin = Vector2.zero; g.Fill.anchorMax = new Vector2(0f, 1f); g.Fill.offsetMin = g.Fill.offsetMax = Vector2.zero;

        g.Stripes = UiKit.Shape(g.Fill, T.Theme.stripes, D.StripeTint, 0f, "Stripes");
        g.Stripes.type = Image.Type.Tiled; g.Stripes.pixelsPerUnitMultiplier = D.StripeScale;
        UiKit.Fill(g.Stripes.rectTransform);
        g.Stripes.enabled = false;

        var line = DesignUi.Rect(g.Root, T.Ink, "Limit").rectTransform;
        line.anchorMin = line.anchorMax = new Vector2(limit, 0.5f); line.pivot = new Vector2(0.5f, 0.5f);
        line.sizeDelta = new Vector2(D.LimitLineW, D.GaugeH + D.LimitLineOver * 2f); line.anchoredPosition = Vector2.zero;
        return g;
    }

    /// <param name="over">サーバーの判定（超えている）</param>
    public void Set(float value, float limit, bool over)
    {
        float full = limit * D.GaugeHeadroom;
        Fill.anchorMax = new Vector2(full > 0f ? Mathf.Clamp01(value / full) : 0f, 1f);
        FillImg.color = over ? T.Danger : T.Go;
        Stripes.enabled = over;
    }
}

/// <summary>2つから1つ選ぶピル（ロケット・軌道）。選んだほうは ink の面に白い文字</summary>
public class DesignChoice
{
    public string Key;
    public RectTransform Root, Body;
    public Image Face;
    public TextMeshProUGUI Name;
    public readonly List<DesignStat> Stats = new List<DesignStat>();

    public static DesignChoice Make(Transform p, DesignChoiceCopy copy, Func<string, string> fill, Action on)
    {
        var c = new DesignChoice { Key = copy.Key };
        c.Root = Ui.Rect("Choice_" + copy.Key, p);
        var hit = c.Root.gameObject.AddComponent<Image>(); hit.color = T.Clear;
        var le = Ui.Size(c.Root, w: 0f, h: D.ChoiceH, flexW: 1f); le.minHeight = D.ChoiceH; le.minWidth = 0f;

        var sh = UiKit.Pill(c.Root, T.Shadow, D.ChoiceH, "Shadow"); UiKit.Fill(sh.rectTransform);
        sh.rectTransform.offsetMin = new Vector2(0f, -T.ShadowY); sh.rectTransform.offsetMax = new Vector2(0f, -T.ShadowY);
        c.Body = Ui.Rect("Body", c.Root); UiKit.Fill(c.Body);
        c.Face = UiKit.Pill(c.Body, T.Bg, D.ChoiceH, "Face"); UiKit.Fill(c.Face.rectTransform);

        var col = UiKit.Col(c.Body, 0f, TextAnchor.MiddleCenter, T.S2, 0f, true);
        UiKit.Fill(col);
        c.Name = DesignUi.Text(col, copy.Name, TextStyle.Body, T.Ink, TextAlignmentOptions.Center, D.PartNameH, shrink: true);
        var row = UiKit.Row(col, D.StatGap, TextAnchor.MiddleCenter);
        Ui.Size(row, h: D.PartLineH);
        foreach (var s in copy.Stats) c.Stats.Add(DesignStat.Make(row, s.Icon, fill(s.Text), TextStyle.Label, T.IconS, T.InkSub));

        DesignUi.Press(c.Root, c.Body, c.Face, on);
        return c;
    }

    public void SetSelected(bool on)
    {
        Face.color = on ? T.Ink : T.Bg;
        Name.color = on ? T.Panel : T.Ink;
        foreach (var s in Stats) s.SetColor(on ? T.Panel : T.InkSub);
    }
}

/// <summary>要件の札（Req 1〜4）：番号・アイコン・短い言葉。合格は go のチェック、不合格は danger の面にバツ。まだ決まらないものは印なし</summary>
public class DesignGoalChip
{
    public RectTransform Root;
    public Image Face, Icon, Mark;
    public TextMeshProUGUI Number, Text;

    public static DesignGoalChip Make(Transform p, DesignGoalCopy copy, string text)
    {
        var g = new DesignGoalChip();
        g.Root = UiKit.Row(p, T.S1, TextAnchor.MiddleCenter, T.S1);   // 4枚が iPhone の横でも1行に入るよう、端は詰める
        g.Root.name = "Goal_" + copy.Number;
        var le = Ui.Size(g.Root, h: D.GoalChipH); le.minHeight = D.GoalChipH; le.flexibleWidth = 0f;
        g.Face = UiKit.Pill(g.Root, T.Panel, D.GoalChipH, "Face"); UiKit.Fill(g.Face.rectTransform); UiKit.Ignore(g.Face);
        g.Number = UiKit.Text(g.Root, copy.Number, TextStyle.Tag, T.Ink);
        g.Icon = UiKit.Icon(g.Root, copy.Icon, T.IconS, T.Ink);
        g.Text = UiKit.Text(g.Root, text, TextStyle.Label, T.Ink);
        g.Mark = UiKit.Icon(g.Root, "check", T.IconS, T.Go);
        g.Set(0);
        return g;
    }

    /// <param name="state">1＝合格、-1＝不合格、0＝まだ分からない</param>
    public void Set(int state)
    {
        bool bad = state < 0;
        Face.color = bad ? T.Danger : T.Panel;
        var fg = bad ? T.Panel : T.Ink;
        Number.color = Text.color = Icon.color = fg;
        Mark.gameObject.SetActive(state != 0);
        Mark.sprite = T.Icon(bad ? "cross" : "check");
        Mark.color = bad ? T.Panel : T.Go;
    }
}
