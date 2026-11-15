// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-15、#168・#190 あつ森の作り：数のチップ・タブの帯・材料の行・選択肢の吹き出し・指さしの手・チェックの丸・見出しの帯）
// 設計の画面（#73・#168）の小さな部品。見た目の決まりは docs/design/ui-guidelines.md（#190）の 1〜3章、【NN】は 0章の基準の画像。
//   影は付けない（原則 2）／明るい面の文字は茶（DesignTokens.Text）／大文字だけの言葉にしない／選んだ印は手・マーカー・チェック・名前の札（紺で塗らない）
// 色と大きさは UiTokens・DesignTokens、形（sprite）は DesignShapes から読む（ここに色コードや px を書かない）。数字は計算しない（サーバーの totals をそのまま出す）。
// #190 の UiSelect.cs（手・品名の札・チェック・マーカー）ができたら、DesignHand・DesignUi.CheckBadge・DesignChoiceBubble のマーカーをそれに替える
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;
using D = DesignTokens;
using S = DesignShapes;

public static class DesignUi
{
    /// <summary>言葉（丸い書体 Nunito。#190：見出し・ボタン・名前も同じ太さ1種類。Nunito ExtraBold ができるまで Bold）。h を渡すと高さを固定</summary>
    public static TextMeshProUGUI Words(Transform p, string s, float size, Color c, TextAlignmentOptions a = TextAlignmentOptions.MidlineLeft,
        float h = -1f, bool ellipsis = false, bool wrap = false)
    {
        var t = UiKit.Text(p, s, TextStyle.Body, c, a, false);
        t.fontSize = size;
        if (ellipsis) t.overflowMode = TextOverflowModes.Ellipsis;
        if (wrap) t.textWrappingMode = TextWrappingModes.Normal;
        var le = Ui.Size(t);
        if (h >= 0f) { le.preferredHeight = h; le.minHeight = h; }
        if (ellipsis || wrap) { le.preferredWidth = 0f; le.flexibleWidth = 1f; le.minWidth = 0f; }
        return t;
    }

    /// <summary>押せる面。UiKit のボタンと同じ押し心地（ColorTint と UiPress）</summary>
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

    /// <summary>丸（blob_disc）。d の大きさで親の中央に置く（レイアウトの外）</summary>
    public static Image Disc(Transform p, Color c, float d, string name = "Disc")
    {
        var img = UiKit.Shape(p, S.Disc, c, d * 0.5f, name);
        UiKit.Center(img.rectTransform, d, d);
        UiKit.Ignore(img);
        return img;
    }

    /// <summary>アイコンの台（【01】）：色つきの丸に panel の絵（台の 60%）。"O2" は O₂ の文字。レイアウトの中では d×d</summary>
    public static RectTransform Platform(Transform p, string icon, float d, Color face, out Image disc, out Graphic glyph)
    {
        var r = Ui.Rect("Platform_" + icon, p);
        UiKit.Fixed(r, d, d);
        r.sizeDelta = new Vector2(d, d);
        disc = Disc(r, face, d);
        if (icon == "O2")
        {
            var tx = UiKit.Text(r, "O₂", TextStyle.Tag, T.Panel, TextAlignmentOptions.Center, false);
            tx.fontSize = d * 0.42f;
            UiKit.Fill(tx.rectTransform); UiKit.Ignore(tx);
            glyph = tx;
        }
        else
        {
            var ic = UiKit.Icon(r, icon, d * 0.6f, T.Panel);
            UiKit.Center(ic.rectTransform, d * 0.6f, d * 0.6f); UiKit.Ignore(ic);
            glyph = ic;
        }
        return r;
    }

    /// <summary>チェックの丸（【09】）：go の丸に白い ✓。親の右上の角にかぶせる</summary>
    public static RectTransform CheckBadge(RectTransform owner, float d)
    {
        var r = Ui.Rect("Check", owner);
        r.anchorMin = r.anchorMax = Vector2.one; r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(d, d); r.anchoredPosition = new Vector2(-d * 0.3f, -d * 0.05f);   // 右の言葉に重ならないよう、台の内側に寄せる
        Disc(r, T.Go, d);
        var ic = UiKit.Icon(r, "check", d * 0.62f, T.Panel);
        UiKit.Center(ic.rectTransform, d * 0.62f, d * 0.62f); UiKit.Ignore(ic);
        Ui.Size(r).ignoreLayout = true;
        return r;
    }

    /// <summary>横の点線（【11】の材料の行・見出しの下）。太さ 3、長さ 8・間 8</summary>
    public static UiDashed Dots(Transform p, Color c, bool flex)
    {
        var d = UiKit.DashedLine(p, c);
        d.dash = D.DashLen; d.gap = D.DashGap; d.thickness = D.DashThick;
        var le = Ui.Size(d);
        if (flex) { le.preferredWidth = 0f; le.flexibleWidth = 1f; le.minWidth = T.S2; }
        return d;
    }

    /// <summary>パネル（【05】）：明るい内ふち＋面（＋横縞）。影なし。返す root に上から足す（VerticalLayoutGroup。内側＝内ふち＋pad）。後ろへの押下は止める</summary>
    public static RectTransform Panel(Transform p, Color face, bool bands, float spacing, float pad, string name)
    {
        var r = Ui.Rect(name, p);
        var rim = UiKit.Shape(r, S.Panel, D.Rim, S.PanelRadius, "Rim"); UiKit.Fill(rim.rectTransform); UiKit.Ignore(rim);
        rim.raycastTarget = true;
        var f = UiKit.Shape(r, S.Panel, face, S.PanelRadius - D.RimPanel, "Face"); UiKit.Fill(f.rectTransform, D.RimPanel); UiKit.Ignore(f);
        if (bands)
        {
            f.gameObject.AddComponent<Mask>().showMaskGraphic = true;   // 横縞を面の形で切る
            var br = Ui.Rect("Bands", f.transform); UiKit.Fill(br);
            var raw = br.gameObject.AddComponent<RawImage>();
            raw.texture = S.Bands; raw.color = D.PanelBand; raw.raycastTarget = false;
            UiTick.On(br.gameObject, _ => { float h = Mathf.Max(1f, br.rect.height); raw.uvRect = new Rect(0f, 0f, 1f, h / 128f); });
        }
        var l = r.gameObject.AddComponent<VerticalLayoutGroup>();
        int inset = Mathf.RoundToInt(D.RimPanel + pad);
        l.padding = new RectOffset(inset, inset, inset, inset);
        l.spacing = spacing; l.childAlignment = TextAnchor.UpperLeft;
        l.childControlWidth = l.childControlHeight = true;
        l.childForceExpandWidth = true; l.childForceExpandHeight = false;
        return r;
    }
}

/// <summary>アイコン＋小さな数字（値段・重さ。【09】品物の下の値段）。アイコン名 "O2" は O₂ の丸</summary>
public class DesignStat
{
    public RectTransform Root;
    public Image Icon;
    public TextMeshProUGUI IconText, Text;

    public static DesignStat Make(Transform p, string icon, string text, float size, Color c)
    {
        var s = new DesignStat();
        s.Root = UiKit.Row(p, T.S1 * 0.5f, TextAnchor.MiddleLeft);
        s.Root.name = "Stat_" + icon;
        if (icon == "O2")
        {
            s.Icon = UiKit.Pill(s.Root, c, size, "O2");
            UiKit.Fixed(s.Icon, size, size);
            s.IconText = UiKit.Text(s.Icon.transform, "O₂", TextStyle.Tag, T.Panel, TextAlignmentOptions.Center, false);
            s.IconText.fontSize = size * 0.5f;
            UiKit.Fill(s.IconText.rectTransform); UiKit.Ignore(s.IconText);
        }
        else s.Icon = UiKit.Icon(s.Root, icon, size, c);
        s.Text = UiKit.Text(s.Root, text, TextStyle.Body, c, TextAlignmentOptions.MidlineLeft, false);
        s.Text.fontSize = size;
        s.SetColor(c);
        return s;
    }

    public void SetColor(Color c)
    {
        Icon.color = c;
        Text.color = c;
        if (IconText != null) IconText.color = T.Panel;
    }
}

/// <summary>− ＋ のボタン：panel の丸 56 に bgDeep のふち 5、茶の棒（影なし）。レイアウトの幅は 64、押せる範囲は 96（両どなりへはみ出す）</summary>
public class DesignStepButton
{
    public RectTransform Root, Body;
    public Button Button;
    public Image Ring, Face;
    readonly List<Image> glyph = new List<Image>();

    public static DesignStepButton Make(Transform p, bool plus, Action on)
    {
        var b = new DesignStepButton();
        b.Root = Ui.Rect(plus ? "Plus" : "Minus", p);
        UiKit.Fixed(b.Root, D.StepSlotW, D.StepSlotW);
        var hit = Ui.Rect("Hit", b.Root).gameObject.AddComponent<Image>(); hit.color = T.Clear;
        UiKit.Center(hit.rectTransform, T.Hit, T.Hit);
        b.Body = Ui.Rect("Body", b.Root); UiKit.Center(b.Body, D.StepD, D.StepD);
        b.Ring = DesignUi.Disc(b.Body, D.BgDeep, D.StepD, "Ring");
        b.Face = DesignUi.Disc(b.Body, T.Panel, D.StepD - D.RimButton * 2f, "Face");
        b.glyph.Add(Bar(b.Body, D.StepGlyphLen, D.StepGlyphThick));
        if (plus) b.glyph.Add(Bar(b.Body, D.StepGlyphThick, D.StepGlyphLen));
        b.Button = DesignUi.Press(b.Root, b.Body, b.Face, on);
        return b;
    }

    static Image Bar(Transform p, float w, float h)
    {
        var img = UiKit.Pill(p, D.Text, Mathf.Min(w, h), "Bar");
        UiKit.Center(img.rectTransform, w, h);
        return img;
    }

    /// <summary>押せないとき（最小・最大に届いた）：棒を line に、面を行の帯の色に</summary>
    public void SetEnabled(bool on)
    {
        Button.interactable = on;
        Face.color = on ? T.Panel : D.PanelBand;
        foreach (var g in glyph) g.color = on ? D.Text : T.Line;
    }
}

/// <summary>材料の行（【11】）：角丸の淡い帯に、アイコンの台＋名前（下に値段）＋点線＋（色の箱の数 / 上限 と − ＋）か トグル。
/// 1個以上積んだ行は、台の右上にチェックの丸（【09】）。枠や影で囲まない</summary>
public class DesignRow
{
    public string Id;
    public int Min, Max;
    public RectTransform Root;
    public TextMeshProUGUI Name, Count;
    public DesignStepButton Minus, Plus;
    public UiToggle Toggle;
    RectTransform check;

    /// <param name="onDelta">± を押したとき（+1／−1）</param>
    /// <param name="onSet">トグルを押したとき（なりたい個数 0／1）</param>
    public static DesignRow Make(Transform p, DesignPartCopy copy, Color platform, string mass, string cost, string science,
        int min, int max, Action<int> onDelta, Action<int> onSet)
    {
        var c = new DesignRow { Id = copy.Id, Min = min, Max = max };
        c.Root = Ui.Rect("Part_" + copy.Id, p);
        var le = Ui.Size(c.Root, w: 0f, h: D.RowStepH, flexW: 1f); le.minHeight = D.RowStepMinH; le.minWidth = 0f; le.flexibleHeight = 0f;
        var strip = UiKit.Round(c.Root, D.RowRadius, D.PanelBand, "Strip"); UiKit.Fill(strip.rectTransform);

        var row = UiKit.Row(c.Root, T.S1, TextAnchor.MiddleLeft, D.RowPadX, 0f); UiKit.Fill(row);
        var plat = DesignUi.Platform(row, copy.Icon, D.RowIconD, platform, out _, out _);
        c.check = DesignUi.CheckBadge(plat, D.CheckD * 0.8f);

        var col = UiKit.Col(row, 0f, TextAnchor.MiddleLeft, 0f, 0f, true);
        c.Name = DesignUi.Words(col, copy.Name, D.TextRow, D.Text, TextAlignmentOptions.MidlineLeft, D.RowNameH);
        var price = UiKit.Row(col, T.S2, TextAnchor.MiddleLeft);
        Ui.Size(price, h: D.RowPriceH);
        DesignStat.Make(price, "mass", mass, D.TextPrice, D.TextSub);
        DesignStat.Make(price, "cost", cost, D.TextPrice, D.TextSub);
        if (science != null) DesignStat.Make(price, "science", science, D.TextPrice, D.TextSub);

        DesignUi.Dots(row, D.BgDeep, true);

        if (max == 1)
        {
            c.Toggle = HudKit.Toggle(row, null, "", on => onSet(on ? 1 : 0));
            c.Toggle.Label.gameObject.SetActive(false);
            UiKit.Fixed(c.Toggle.Root, T.ToggleW, T.Hit);
        }
        else
        {
            var step = UiKit.Row(row, T.S1 * 0.5f, TextAnchor.MiddleCenter);
            c.Minus = DesignStepButton.Make(step, false, () => onDelta(-1));
            var box = Ui.Rect("Count", step);
            UiKit.Fixed(box, D.CountBoxW, D.CountBoxW);
            var boxFace = UiKit.Round(box, D.NumBoxRadius, D.Mark, "Box"); UiKit.Fill(boxFace.rectTransform);
            c.Count = UiKit.Text(box, "0", TextStyle.Number, D.Text, TextAlignmentOptions.Center, false);
            c.Count.fontSize = D.TextRow;
            UiKit.Fill(c.Count.rectTransform);
            var of = DesignUi.Words(step, "/ " + max, D.TextRow, D.TextSub, TextAlignmentOptions.MidlineLeft);
            UiKit.Fixed(of, D.MaxW, D.RowNameH);
            c.Plus = DesignStepButton.Make(step, true, () => onDelta(+1));
        }
        return c;
    }

    public void Set(int n)
    {
        check.gameObject.SetActive(n > 0);
        if (Toggle != null) { Toggle.Set(n > 0); return; }
        Count.text = n.ToString();
        Minus.SetEnabled(n > Min);
        Plus.SetEnabled(n < Max);
    }
}

/// <summary>選択肢の吹き出し（【10】）：黄みの面に、選択肢の言葉だけを縦に（行の右に小さな値段）。選んだ行は文字の後ろにマーカー。手は DesignHand が左の外に置く。押すとすぐ決まる</summary>
public class DesignChoiceBubble
{
    class Option { public string Key; public RectTransform Row; public TextMeshProUGUI Text; public Image Marker; }
    public RectTransform Root;
    readonly List<Option> options = new List<Option>();
    public string Selected { get; private set; } = "";

    public static DesignChoiceBubble Make(Transform p, DesignChoiceCopy[] list, Func<string, string> fill, Action<string> onPick)
    {
        var b = new DesignChoiceBubble();
        b.Root = Ui.Rect("Choice", p);
        var face = UiKit.Shape(b.Root, S.Panel, D.Choice, S.PanelRadius, "Face"); UiKit.Fill(face.rectTransform); UiKit.Ignore(face);
        face.raycastTarget = true;
        var l = b.Root.gameObject.AddComponent<VerticalLayoutGroup>();
        int padX = Mathf.RoundToInt(D.ChoicePad), padY = Mathf.RoundToInt(D.ChoicePad * 0.5f);
        l.padding = new RectOffset(padX, padX, padY, padY);
        l.childControlWidth = l.childControlHeight = true; l.childForceExpandWidth = true; l.childForceExpandHeight = false;

        foreach (var c in list)
        {
            var o = new Option { Key = c.Key };
            o.Row = Ui.Rect("Option_" + c.Key, b.Root);
            var le = Ui.Size(o.Row, h: D.ChoiceRowH); le.minHeight = D.ChoiceRowMinH;
            var hit = o.Row.gameObject.AddComponent<Image>(); hit.color = T.Clear;
            o.Marker = UiKit.Round(o.Row, T.RadiusS, D.Mark, "Marker");
            var mr = o.Marker.rectTransform;
            mr.anchorMin = mr.anchorMax = new Vector2(0f, 0.5f); mr.pivot = new Vector2(0f, 0.5f);
            var line = UiKit.Row(o.Row, T.S2, TextAnchor.MiddleLeft); UiKit.Fill(line);
            o.Text = DesignUi.Words(line, c.Name, D.TextChoice, D.Text);
            Ui.Size(UiKit.Space(line, 0f), flexW: 1f);
            foreach (var s in c.Stats) DesignStat.Make(line, s.Icon, fill(s.Text), D.TextPrice, D.TextSub);
            string key = c.Key;
            DesignUi.Press(o.Row, line, hit, () => onPick(key));
            b.options.Add(o);
        }
        b.Select("");
        return b;
    }

    /// <summary>選んだ行にマーカー（文字の幅＋左右 16、高さは文字の 0.8、少し下）。手を置く先（選んだ行のマーカー）を返す</summary>
    public RectTransform Select(string key)
    {
        Selected = key;
        RectTransform at = null;
        foreach (var o in options)
        {
            bool on = o.Key == key;
            o.Marker.enabled = on;
            if (!on) continue;
            float w = o.Text.GetPreferredValues(o.Text.text).x + T.S2 * 2f;
            o.Marker.rectTransform.sizeDelta = new Vector2(w, D.TextChoice * 0.8f);
            o.Marker.rectTransform.anchoredPosition = new Vector2(-T.S2, -D.TextChoice * 0.12f);
            at = o.Marker.rectTransform;   // 手はマーカーの左の端を指す
        }
        return at;
    }
}

/// <summary>タブの帯（【09】【11】）：丸を少しずつ重ねた1本のもこもこの帯（bgDeep）にアイコン。選んだタブはアイコンを accent の台に載せ、上に名前の札</summary>
public class DesignTabBand
{
    class Tab { public string Key, Name; public RectTransform Slot; public Image On, Icon; }
    public RectTransform Root;
    readonly List<Tab> tabs = new List<Tab>();
    RectTransform band, tag;
    TextMeshProUGUI tagText;

    public static DesignTabBand Make(Transform p, DesignTabCopy[] list, Action<string> onPick)
    {
        var b = new DesignTabBand();
        b.Root = Ui.Rect("Tabs", p);
        float h = D.TabBumpD + D.TabLabelH * 0.5f;
        var le = Ui.Size(b.Root, h: h); le.minHeight = h;
        int n = list.Length;
        float step = D.TabBumpD - D.TabOverlap, w = step * (n - 1) + D.TabBumpD;
        b.band = Ui.Rect("Band", b.Root);
        b.band.anchorMin = b.band.anchorMax = b.band.pivot = new Vector2(0.5f, 0f);
        b.band.sizeDelta = new Vector2(w, D.TabBumpD); b.band.anchoredPosition = Vector2.zero;
        for (int i = 0; i < n; i++)
        {
            var d = UiKit.Shape(b.band, S.Disc, D.BgDeep, D.TabBumpD * 0.5f, "Bump");
            Place(d.rectTransform, i, step);
        }
        for (int i = 0; i < n; i++)
        {
            var c = list[i];
            var t = new Tab { Key = c.Key, Name = c.Name };
            t.Slot = Ui.Rect("Tab_" + c.Key, b.band);
            Place(t.Slot, i, step);
            var hit = t.Slot.gameObject.AddComponent<Image>(); hit.sprite = S.Disc; hit.color = T.Clear;
            var body = Ui.Rect("Body", t.Slot); UiKit.Fill(body);
            t.On = DesignUi.Disc(body, T.Accent, D.TabOnD, "On");
            t.Icon = UiKit.Icon(body, c.Icon, D.TabIcon, D.TextSub);
            UiKit.Center(t.Icon.rectTransform, D.TabIcon, D.TabIcon);
            string key = c.Key;
            DesignUi.Press(t.Slot, body, hit, () => onPick(key));
            b.tabs.Add(t);
        }
        b.tag = UiKit.Chip(b.Root, T.Accent, D.TabLabelH, "Name");
        b.tag.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(Mathf.RoundToInt(T.S2), Mathf.RoundToInt(T.S2), 0, 0);
        b.tag.anchorMin = b.tag.anchorMax = new Vector2(0.5f, 0f); b.tag.pivot = new Vector2(0.5f, 0.5f);
        b.tag.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        b.tag.sizeDelta = new Vector2(0f, D.TabLabelH);
        b.tagText = DesignUi.Words(b.tag, "", D.TextTag, T.Panel, TextAlignmentOptions.Center);
        return b;
    }

    static void Place(RectTransform r, int i, float step)
    {
        r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(D.TabBumpD, D.TabBumpD);
        r.anchoredPosition = new Vector2(D.TabBumpD * 0.5f + step * i, 0f);
    }

    /// <summary>選んだタブ：アイコンを accent の台に白で、上に名前の札。そのタブの RectTransform を返す</summary>
    public RectTransform Select(string key)
    {
        RectTransform at = null;
        float step = D.TabBumpD - D.TabOverlap, w = step * (tabs.Count - 1) + D.TabBumpD;
        for (int i = 0; i < tabs.Count; i++)
        {
            var t = tabs[i];
            bool on = t.Key == key;
            t.On.enabled = on;
            t.Icon.color = on ? T.Panel : D.TextSub;
            if (!on) continue;
            at = t.Slot;
            tagText.text = t.Name;
            tag.anchoredPosition = new Vector2(-w * 0.5f + D.TabBumpD * 0.5f + step * i, D.TabBumpD);
        }
        return at;
    }
}

/// <summary>数のチップ（【09】【10】右上の残高）：茶のピルに明るい数字（Baloo 40、幅固定で右寄せ）、上にアイコンの台を半分かぶせ、右下に単位の札（left／over）。
/// 足りないときは danger の面、左右に揺れる。数字が変わると数え上げ／下げ、弾み、右上に −10／+10 のチップが浮いて消える。値はサーバーの totals のまま（引き算しない）</summary>
public class DesignCounter
{
    public RectTransform Root, Body;
    Image face, platDisc;
    Graphic platGlyph;
    TextMeshProUGUI number, unit;
    RectTransform unitTag, chip;
    TextMeshProUGUI chipText; CanvasGroup chipCg;
    readonly Color platFace;
    LayoutElement numberLe;
    bool has, over, known;
    float shown, from, target, countT = 99f, bounceT = 99f, shakeT = 99f, chipT = 99f;

    DesignCounter(Color platFace) { this.platFace = platFace; }

    public static DesignCounter Make(Transform p, string icon, Color platFace, Action onTap)
    {
        var w = new DesignCounter(platFace);
        w.Root = Ui.Rect("Counter_" + icon, p);
        var hit = w.Root.gameObject.AddComponent<Image>(); hit.color = T.Clear;
        var le = Ui.Size(w.Root, w: 0f, h: D.CounterH, flexW: 1f); le.minHeight = D.CounterH; le.minWidth = 0f;
        w.Body = Ui.Rect("Body", w.Root); UiKit.Fill(w.Body);
        w.face = UiKit.Shape(w.Body, S.Pill, D.Text, D.CounterH * 0.5f, "Face"); UiKit.Fill(w.face.rectTransform);

        var row = UiKit.Row(w.Body, T.S1, TextAnchor.MiddleCenter, T.S3); UiKit.Fill(row);
        w.number = UiKit.Text(row, "", TextStyle.Number, T.Panel, TextAlignmentOptions.MidlineRight);
        w.numberLe = UiKit.Fixed(w.number, D.CounterNumberW, T.TextNumber * T.HeadingLineHeight);

        // 上にアイコンの台を半分かぶせる（左寄り）
        var plat = DesignUi.Platform(w.Body, icon, D.CounterIconD, platFace, out w.platDisc, out w.platGlyph);
        plat.anchorMin = plat.anchorMax = new Vector2(0.22f, 1f); plat.pivot = new Vector2(0.5f, 0.5f); plat.anchoredPosition = Vector2.zero;
        UiKit.Ignore(plat);

        // 右下に単位の札（panel の小さなピル、−4°）
        w.unitTag = UiKit.Chip(w.Body, T.Panel, D.CounterUnitH, "Unit");
        w.unitTag.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(Mathf.RoundToInt(T.S2), Mathf.RoundToInt(T.S2), 0, 0);
        w.unitTag.anchorMin = w.unitTag.anchorMax = new Vector2(1f, 0f); w.unitTag.pivot = new Vector2(1f, 0.5f);
        w.unitTag.anchoredPosition = new Vector2(-T.S2, T.S1 * 0.5f);
        w.unitTag.localRotation = Quaternion.Euler(0f, 0f, D.CounterUnitTilt);
        w.unitTag.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        w.unitTag.sizeDelta = new Vector2(0f, D.CounterUnitH);
        w.unit = DesignUi.Words(w.unitTag, DesignCopy.Left, D.TextTag, D.Text, TextAlignmentOptions.Center);

        // 右上から浮く −10／+10（レイアウトの外）
        w.chip = UiKit.Chip(w.Root, T.Accent, D.DeltaChipH, "Delta");
        w.chip.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(Mathf.RoundToInt(T.S2), Mathf.RoundToInt(T.S2), 0, 0);
        w.chip.anchorMin = w.chip.anchorMax = new Vector2(0.78f, 1f); w.chip.pivot = new Vector2(0.5f, 0.5f);
        w.chip.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        w.chip.sizeDelta = new Vector2(0f, D.DeltaChipH);
        w.chipText = DesignUi.Words(w.chip, "", D.TextPrice, T.Panel, TextAlignmentOptions.Center);
        w.chipCg = w.chip.gameObject.AddComponent<CanvasGroup>();
        w.chipCg.blocksRaycasts = false; w.chipCg.interactable = false;
        w.chip.gameObject.SetActive(false);

        if (onTap != null) DesignUi.Press(w.Root, w.Body, w.face, onTap);
        UiTick.On(w.Root.gameObject, w.Tick);
        return w;
    }

    /// <param name="left">残り（負なら超過）。サーバーが送らない（古い記録）なら null</param>
    /// <param name="overNow">サーバーの判定（over_budget・over_mass）</param>
    /// <param name="fallback">left が無いときに出す文（"{cost} / {budget}" を埋めたもの）</param>
    /// <param name="animate">false なら数え上げ・チップ・弾みなしで置く（画面を開いた直後）</param>
    public void Set(float? left, bool overNow, string fallback, bool animate)
    {
        bool wasOver = over;
        over = overNow;
        has = left.HasValue;
        unitTag.gameObject.SetActive(has);
        numberLe.preferredWidth = numberLe.minWidth = has ? D.CounterNumberW : -1f;
        if (!has) { number.text = fallback; known = false; Paint(); return; }

        float v = left.Value;
        if (!known || !animate) { shown = from = target = v; countT = 99f; }
        else if (!Mathf.Approximately(v, target))
        {
            float delta = v - target;
            from = shown; target = v; countT = 0f; bounceT = 0f;
            chipText.text = (delta > 0f ? "+" : "−") + DesignValues.Fmt(Mathf.Abs(delta));
            chipT = 0f; chip.gameObject.SetActive(true);
        }
        known = true;
        if (over && !wasOver && animate) shakeT = 0f;
        Draw();
        Paint();
    }

    void Draw()
    {
        number.text = DesignValues.Fmt(Mathf.Round(Mathf.Abs(shown)));
        unit.text = shown < 0f || over ? DesignCopy.Over : DesignCopy.Left;
    }

    void Paint()
    {
        face.color = over ? T.Danger : D.Text;
        platDisc.color = over ? T.Panel : platFace;
        platGlyph.color = over ? T.Danger : T.Panel;
        unit.color = over ? T.Danger : D.Text;
    }

    void Tick(float dt)
    {
        dt = Mathf.Min(dt, TitleLook.MaxStep);   // 止まったフレームのあとで一気に終わらないように（UiEase.Dt と同じ）
        if (countT < D.PillCountSec)
        {
            countT += dt;
            shown = Mathf.Lerp(from, target, UiEase.OutCubic(countT / D.PillCountSec));
            Draw();
        }
        bounceT += dt;
        float s = HudKit.Bounce(bounceT);
        Body.localScale = new Vector3(s, s, 1f);
        float x = 0f;
        if (shakeT < D.PillShakeSec) { shakeT += dt; float p = shakeT / D.PillShakeSec; x = D.PillShakePx * Mathf.Sin(p * D.PillShakeSec * D.PillShakeHz * Mathf.PI * 2f) * (1f - p); }
        Body.anchoredPosition = new Vector2(x, Body.anchoredPosition.y);
        if (chipT < D.DeltaSec)
        {
            chipT += dt;
            float p = Mathf.Clamp01(chipT / D.DeltaSec);
            chip.anchoredPosition = new Vector2(0f, D.DeltaRise * UiEase.OutCubic(p));
            chipCg.alpha = p < 0.6f ? 1f : 1f - (p - 0.6f) / 0.4f;
            if (chipT >= D.DeltaSec) chip.gameObject.SetActive(false);
        }
    }
}

/// <summary>打ち上げチェックの1行：丸 40＋本文 28。✓＝go の丸に白、✗＝danger の丸に白、飛んでから決まる物は淡い丸（【10】の空きの枠）、届かない見込みは accent の丸に白い !。変わった行だけ弾む</summary>
public class DesignCheckRow
{
    public RectTransform Root, Body;
    Image disc, mark;
    TextMeshProUGUI text;
    int state = int.MinValue;
    float bounceT = 99f;

    public const int Ok = 1, Bad = -1, Pending = 0, Warn = 2;

    public static DesignCheckRow Make(Transform p, string s)
    {
        var r = new DesignCheckRow();
        r.Root = Ui.Rect("Check", p);
        var le = Ui.Size(r.Root, h: D.CheckRowH); le.minHeight = D.CheckRowH;
        r.Body = UiKit.Row(r.Root, T.S2, TextAnchor.MiddleLeft); UiKit.Fill(r.Body);
        var slot = Ui.Rect("Mark", r.Body); UiKit.Fixed(slot, D.CheckD, D.CheckD);
        r.disc = DesignUi.Disc(slot, D.BgDeep, D.CheckD);
        r.mark = UiKit.Icon(slot, "check", D.CheckD * 0.62f, T.Panel);
        UiKit.Center(r.mark.rectTransform, D.CheckD * 0.62f, D.CheckD * 0.62f); UiKit.Ignore(r.mark);
        r.text = DesignUi.Words(r.Body, s, D.TextCheck, D.Text);
        UiTick.On(r.Root.gameObject, r.Tick);
        return r;
    }

    public void SetText(string s) => text.text = s;

    /// <param name="animate">false なら弾まない（画面を開いた直後）</param>
    public void Set(int s, bool animate)
    {
        if (s == state) return;
        if (state != int.MinValue && animate) bounceT = 0f;
        state = s;
        disc.color = s == Ok ? T.Go : s == Bad ? T.Danger : s == Warn ? T.Accent : D.BgDeep;
        mark.gameObject.SetActive(s != Pending);
        mark.sprite = T.Icon(s == Ok ? "check" : s == Bad ? "cross" : "caution");
        text.color = s == Pending ? D.TextSub : D.Text;
    }

    void Tick(float dt)
    {
        if (bounceT > T.BounceSec + 0.1f) return;
        bounceT += Mathf.Min(dt, TitleLook.MaxStep);
        float k = HudKit.Bounce(bounceT);
        Body.localScale = new Vector3(k, k, 1f);
    }
}

/// <summary>画面の見出しの帯（【11】左上）：色の面に白い言葉 36、右の端と下の辺がもこもこ。画面の左の端につける</summary>
public static class DesignRibbon
{
    public static RectTransform Make(Transform p, string text, Color color)
    {
        var r = Ui.Rect("Ribbon", p);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        var body = UiKit.Round(r, T.RadiusS, color, "Body");
        var t = DesignUi.Words(r, text, D.TextRibbon, T.Panel, TextAlignmentOptions.MidlineLeft);
        float tw = t.GetPreferredValues(text).x;
        float w = T.ScreenEdge + tw + T.S4, h = D.RibbonH, bump = h * 0.42f;
        r.sizeDelta = new Vector2(w, h);
        var br = body.rectTransform;   // 左の角は画面の外に出す
        br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.offsetMin = new Vector2(-T.RadiusS * 2f, 0f); br.offsetMax = Vector2.zero;
        // 右の端：丸を縦に3つ（もこもこ）
        for (int i = 0; i < 3; i++)
        {
            var d = UiKit.Shape(r, S.Disc, color, bump * 0.5f, "BumpR");
            var dr = d.rectTransform;
            dr.anchorMin = dr.anchorMax = new Vector2(1f, 0f); dr.pivot = new Vector2(0.5f, 0.5f);
            dr.sizeDelta = new Vector2(bump, bump); dr.anchoredPosition = new Vector2(0f, h * (0.2f + 0.3f * i));
        }
        // 下の辺：小さな丸を並べる（→ wave_edge）
        float small = bump * 0.7f;
        for (float x = small * 0.5f; x < w; x += small * 1.25f)
        {
            var d = UiKit.Shape(r, S.Disc, color, small * 0.5f, "BumpB");
            var dr = d.rectTransform;
            dr.anchorMin = dr.anchorMax = Vector2.zero; dr.pivot = new Vector2(0.5f, 0.5f);
            dr.sizeDelta = new Vector2(small, small); dr.anchoredPosition = new Vector2(x, small * 0.15f);
        }
        t.transform.SetAsLastSibling();
        var tr = t.rectTransform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(T.ScreenEdge, 0f); tr.offsetMax = Vector2.zero;
        UiKit.Ignore(t);
        return r;
    }
}

/// <summary>指さしの手（1画面に1つ。【09】【10】【11】）。Point した物の左の外から右を指す。ゆっくり上下し、乗り換えるときは素早く動く。
/// glove_point（#190 の sprite）が無い間は、宇宙服のグローブを丸とカプセルで組む（白い面＋茶の輪郭＋手首にオレンジの輪）</summary>
public class DesignHand
{
    public RectTransform Root;
    readonly RectTransform layer;
    RectTransform target;
    Vector2 from, cur;
    float moveT = 99f;
    bool placed;

    DesignHand(RectTransform layer) { this.layer = layer; }

    public static DesignHand Make(RectTransform layer)
    {
        var h = new DesignHand(layer);
        h.Root = Ui.Rect("Hand", layer);
        h.Root.anchorMin = h.Root.anchorMax = new Vector2(0.5f, 0.5f);
        h.Root.pivot = new Vector2(1f, 0.5f);   // 指の先
        h.Root.sizeDelta = new Vector2(D.CursorSize, D.CursorSize);
        if (S.Glove != null)
        {
            var img = h.Root.gameObject.AddComponent<Image>();
            img.sprite = S.Glove; img.preserveAspect = true; img.raycastTarget = false;
        }
        else BuildGlove(h.Root);
        UiTick.On(layer.gameObject, h.Tick);
        h.Root.gameObject.SetActive(false);
        return h;
    }

    /// <summary>右向きのグローブ（72×72 の箱の中。中心からの位置）。輪郭を先に、面を後に描く</summary>
    static void BuildGlove(RectTransform r)
    {
        float k = D.CursorSize / 72f, o = 3f * k;
        void Part(Vector2 at, Vector2 size, Color c, string n)
        {
            var img = UiKit.Shape(r, T.Theme.pill, c, Mathf.Min(size.x, size.y) * 0.5f, n);
            var ir = img.rectTransform;
            ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0.5f, 0.5f);
            ir.sizeDelta = size; ir.anchoredPosition = at;
        }
        var shapes = new (Vector2 at, Vector2 size)[]
        {
            (new Vector2(14f, 7f) * k, new Vector2(42f, 20f) * k),    // 人さし指
            (new Vector2(-6f, -2f) * k, new Vector2(38f, 38f) * k),   // 手のひら
            (new Vector2(-2f, 15f) * k, new Vector2(18f, 16f) * k),   // 親指のこぶ
            (new Vector2(8f, -10f) * k, new Vector2(20f, 16f) * k),   // にぎった指
            (new Vector2(0f, -17f) * k, new Vector2(18f, 14f) * k),
        };
        foreach (var s in shapes) Part(s.at, s.size + new Vector2(o * 2f, o * 2f), D.Text, "Outline");
        Part(new Vector2(-26f, -2f) * k, new Vector2(16f, 36f) * k + new Vector2(o * 2f, o * 2f), D.Text, "WristOutline");
        Part(new Vector2(-26f, -2f) * k, new Vector2(16f, 36f) * k, T.Accent, "Wrist");
        foreach (var s in shapes) Part(s.at, s.size, T.Panel, "Glove");
    }

    /// <summary>手を t の左の外へ（null なら隠す）</summary>
    public void Point(RectTransform t)
    {
        if (t == target) return;
        target = t;
        from = cur;
        moveT = placed ? 0f : 99f;
    }

    void Tick(float dt)
    {
        bool show = target != null && target.gameObject.activeInHierarchy;
        if (Root.gameObject.activeSelf != show) Root.gameObject.SetActive(show);
        if (!show) return;
        var r = target.rect;
        Vector2 want = layer.InverseTransformPoint(target.TransformPoint(new Vector3(r.xMin, r.center.y, 0f)));
        want.x += T.S1 * 0.5f;   // 指の先を少し重ねる
        if (!placed) { cur = want; placed = true; }
        moveT += Mathf.Min(dt, TitleLook.MaxStep);
        cur = moveT < D.HandMoveSec ? Vector2.LerpUnclamped(from, want, UiEase.OutBack(moveT / D.HandMoveSec)) : want;
        float bob = Mathf.Sin(UiEase.Now / D.HandBobSec * Mathf.PI * 2f) * D.HandBobPx;
        Root.anchoredPosition = cur + new Vector2(0f, bob);
    }
}
