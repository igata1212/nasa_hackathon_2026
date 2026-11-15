// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-15、#168・#190 あつ森の作り：数のチップ・タブの帯・材料の行・選択肢の吹き出し・指さしの手・チェックの丸・見出しの帯）／ 更新：iizuka（Opus 5.5、2026-11-15、#190 共通の部品 UiKit.Row・Choice・Counter・Ribbon と UiSelect の手・チェックに替えた）
// 設計の画面（#73・#168）の小さな部品。見た目の決まりは docs/design/ui-guidelines.md（#190）の 1〜3章、【NN】は 0章の基準の画像。
//   影は付けない（原則 2）／明るい面の文字は茶（T.Text）／大文字だけの言葉にしない／選んだ印は手・マーカー・チェック・名前の札（紺で塗らない）
// 中身は共通の部品（UiKit.Row＝材料の行、UiKit.Choice＝選択肢の吹き出し、UiKit.Counter＝数のチップ、UiKit.Ribbon＝見出しの帯、UiSelect＝手・チェック）。
//   ここは設計の画面に足りない所だけ（行の名前の下の値段のアイコン、− ＋ の棒、選択肢の右の値段、数のチップの数え上げ、タブの帯）。
// 色と大きさは UiTokens・DesignTokens から読む（ここに色コードや px を書かない）。数字は計算しない（サーバーの totals をそのまま出す）。
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;
using D = DesignTokens;

public static class DesignUi
{
    /// <summary>言葉（丸い書体 Nunito。strong なら ExtraBold＝見出し・札・名前、それ以外は Bold）。h を渡すと高さを固定</summary>
    public static TextMeshProUGUI Words(Transform p, string s, float size, Color c, TextAlignmentOptions a = TextAlignmentOptions.MidlineLeft,
        float h = -1f, bool ellipsis = false, bool wrap = false, bool strong = false)
    {
        var t = UiKit.Text(p, s, strong ? TextStyle.RowName : TextStyle.Body, c, a, false);
        t.fontSize = size;
        if (ellipsis) t.overflowMode = TextOverflowModes.Ellipsis;
        if (wrap) t.textWrappingMode = TextWrappingModes.Normal;
        var le = Ui.Size(t);
        if (h >= 0f) { le.preferredHeight = h; le.minHeight = h; }
        if (ellipsis || wrap) { le.preferredWidth = 0f; le.flexibleWidth = 1f; le.minWidth = 0f; }
        return t;
    }

    /// <summary>押せる面（ColorTint と UiPress の音）。押すと body が UiSelect.Pick で弾む</summary>
    public static Button Press(RectTransform root, RectTransform body, Graphic target, Action on)
    {
        var b = root.gameObject.AddComponent<Button>();
        b.targetGraphic = target;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        var cb = b.colors;
        cb.normalColor = cb.selectedColor = cb.disabledColor = Color.white;
        cb.highlightedColor = T.TintHover; cb.pressedColor = T.TintPressed; cb.fadeDuration = T.TintFade;
        b.colors = cb;
        b.onClick.AddListener(() => { if (body != null) UiSelect.Pick(body); on?.Invoke(); });
        root.gameObject.AddComponent<UiPress>().Init(body, b);
        return b;
    }

    /// <summary>丸（blob_disc）。d の大きさで親の中央に置く（レイアウトの外）</summary>
    public static Image Disc(Transform p, Color c, float d, string name = "Disc")
    {
        var img = UiKit.Blob(p, T.BlobDisc, c, d * 0.5f, name);
        UiKit.Center(img.rectTransform, d, d);
        UiKit.Ignore(img);
        return img;
    }

    /// <summary>アイコンの台（【01】）：色つきの blob_disc に panel の絵（台の 60%）。"O2" は O₂ の文字。レイアウトの中では d×d</summary>
    public static RectTransform Platform(Transform p, string icon, float d, Color face, out Image disc, out Graphic glyph)
    {
        var r = Ui.Rect("Platform_" + icon, p);
        UiKit.Fixed(r, d, d);
        r.sizeDelta = new Vector2(d, d);
        disc = Disc(r, face, d);
        glyph = Glyph(r, icon, d);
        return r;
    }

    /// <summary>台の上の絵（台の 60%、panel）。"O2" は O₂ の文字（Ionicons に酸素が無い。ui-guidelines 4章）</summary>
    public static Graphic Glyph(RectTransform r, string icon, float d)
    {
        if (icon == "O2")
        {
            var tx = UiKit.Text(r, "O₂", TextStyle.Tag, T.Panel, TextAlignmentOptions.Center, false);
            tx.fontSize = d * 0.42f;
            UiKit.Fill(tx.rectTransform); UiKit.Ignore(tx);
            return tx;
        }
        var ic = UiKit.Icon(r, icon, d * 0.6f, T.Panel);
        UiKit.Center(ic.rectTransform, d * 0.6f, d * 0.6f); UiKit.Ignore(ic);
        return ic;
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

    /// <summary>パネル（【05】）：UiKit.PanelFace（blob_panel＋明るい内ふち 12＋横縞）。影なし。3D の上に置くので内ふちは不透明（D.Rim）。
    /// 返す root に上から足す（VerticalLayoutGroup。内側＝内ふち＋pad）。後ろへの押下は止める</summary>
    public static RectTransform Panel(Transform p, Color face, bool bands, float spacing, float pad, string name)
    {
        var r = Ui.Rect(name, p);
        var rim = UiKit.PanelFace(r, face, bands);
        rim.color = D.Rim; rim.raycastTarget = true;
        var l = r.gameObject.AddComponent<VerticalLayoutGroup>();
        int inset = Mathf.RoundToInt(T.RimPanel + pad);
        l.padding = new RectOffset(inset, inset, inset, inset);
        l.spacing = spacing; l.childAlignment = TextAnchor.UpperLeft;
        l.childControlWidth = l.childControlHeight = true;
        l.childForceExpandWidth = true; l.childForceExpandHeight = false;
        return r;
    }

    /// <summary>右寄せの小さな値段の列（選択肢の行の右）。親いっぱいの高さ、右の端に付く（レイアウトの外）</summary>
    public static RectTransform RightStats(RectTransform row, DesignStatCopy[] stats, Func<string, string> fill)
    {
        var st = UiKit.Row(row, T.S2, TextAnchor.MiddleRight);
        st.name = "Stats";
        st.anchorMin = new Vector2(1f, 0f); st.anchorMax = Vector2.one; st.pivot = new Vector2(1f, 0.5f);
        st.anchoredPosition = Vector2.zero; st.sizeDelta = Vector2.zero;
        st.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        foreach (var s in stats) DesignStat.Make(st, s.Icon, fill(s.Text), D.TextPrice, D.TextSub);
        return st;
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
            s.Icon = UiKit.Blob(s.Root, T.BlobDisc, c, size * 0.5f, "O2");
            UiKit.Fixed(s.Icon, size, size);
            s.IconText = UiKit.Text(s.Icon.transform, "O₂", TextStyle.Tag, T.Panel, TextAlignmentOptions.Center, false);
            s.IconText.fontSize = size * 0.5f;
            UiKit.Fill(s.IconText.rectTransform); UiKit.Ignore(s.IconText);
        }
        else s.Icon = UiKit.Icon(s.Root, icon, size, c);
        s.Text = UiKit.Text(s.Root, text, TextStyle.Price, c, TextAlignmentOptions.MidlineLeft, false);
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

/// <summary>− ＋ のボタン：panel の blob_disc 56 に bgDeep のふち 5、茶の棒（影なし）。レイアウトの幅は 64、押せる範囲は 96（両どなりへはみ出す）。
/// UiKit.Row の − ＋ は Baloo の「−」（U+2212）で描くが、Baloo の SDF にその字が無く別の書体に落ちるので、棒で描く</summary>
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

/// <summary>材料の行（【11】）＝ UiKit.Row：panelBand の角丸の帯に、アイコンの台＋名前（下に値段のアイコンと数字。【09】）＋点線＋色の箱の数 ＋「/ 上限」＋ − ＋。
/// 1個までの部品は − ＋ の代わりにトグル。1個以上積んだ行は台の右上にチェック（UiSelect.Check。【09】）。枠や影で囲まない</summary>
public class DesignRow
{
    public string Id;
    public int Min, Max;
    public RectTransform Root;
    public UiRow Row;
    DesignStepButton minus, plus;
    UiToggle toggle;

    /// <param name="onDelta">± を押したとき（+1／−1）</param>
    /// <param name="onSet">トグルを押したとき（なりたい個数 0／1）</param>
    public static DesignRow Make(Transform p, DesignPartCopy copy, Color platform, string mass, string cost, string science,
        int min, int max, Action<int> onDelta, Action<int> onSet)
    {
        var c = new DesignRow { Id = copy.Id, Min = min, Max = max };
        bool o2 = copy.Icon == "O2";
        var u = UiKit.Row(p, o2 ? null : copy.Icon, copy.Name, null, platform);
        c.Row = u; c.Root = u.Root;
        c.Root.name = "Part_" + copy.Id;
        if (o2) DesignUi.Glyph(u.Disc.Root, "O2", T.RowDiscD);
        // − ＋ の行の高さ 96（画面が低いと 80。押せる範囲は 96 のまま）。棚の幅 528 に収まるよう間を詰める
        var le = Ui.Size(u.Root, w: 0f, h: D.RowStepH, flexW: 1f); le.minHeight = D.RowStepMinH; le.minWidth = 0f; le.flexibleHeight = 0f;
        var l = u.Root.GetComponent<HorizontalLayoutGroup>();
        l.spacing = T.S1; l.padding.left = l.padding.right = Mathf.RoundToInt(D.RowPadX);
        UiKit.Fixed(u.Box, D.CountBoxW, D.CountBoxW);
        // 積んだ印（UiSelect.Check）を台 48 に合わせて小さく、台の右上の角へ（絵と名前にかぶらない。【09】）。あとの SetChecked はこの丸を出し入れするだけ
        var chk = UiSelect.Check(u.Disc.Root, false).rectTransform;
        chk.sizeDelta = new Vector2(D.RowCheckD, D.RowCheckD);
        chk.anchoredPosition = new Vector2(-D.RowCheckD * 0.3f, -D.RowCheckD * 0.05f);
        if (chk.Find("Icon_check") is RectTransform tick) tick.sizeDelta = new Vector2(D.RowCheckD * 0.62f, D.RowCheckD * 0.62f);

        // 名前の下の値段：重さ・費用（・科学）の小さなアイコン＋数字（【09】）。UiRow.Sub は文字だけなので使わない
        u.SetSub(null);
        var price = UiKit.Row(u.Name.transform.parent, T.S2, TextAnchor.MiddleLeft);
        price.name = "Price";
        Ui.Size(price, h: D.RowPriceH);
        DesignStat.Make(price, "mass", mass, D.TextPrice, D.TextSub);
        DesignStat.Make(price, "cost", cost, D.TextPrice, D.TextSub);
        if (science != null) DesignStat.Make(price, "science", science, D.TextPrice, D.TextSub);

        if (max == 1)
        {
            c.toggle = HudKit.Toggle(u.Right, null, "", on => onSet(on ? 1 : 0));
            c.toggle.Label.gameObject.SetActive(false);
            UiKit.Fixed(c.toggle.Root, T.ToggleW, T.Hit);
        }
        else
        {
            c.minus = DesignStepButton.Make(u.Right, false, () => onDelta(-1));
            c.plus = DesignStepButton.Make(u.Right, true, () => onDelta(+1));
        }
        return c;
    }

    public void Set(int n)
    {
        Row.SetChecked(n > 0);
        if (toggle != null) { toggle.Set(n > 0); return; }
        Row.SetCount(n, Max);
        minus.SetEnabled(n > Min);
        plus.SetEnabled(n < Max);
    }
}

/// <summary>選択肢の吹き出し（【10】）＝ UiKit.Choice：黄みの blob_panel に選択肢の言葉を縦に、選んだ行の文字の後ろにマーカー。押すとすぐ決まる。
/// 設計の画面で足す所：言葉を左寄せにして行の右に小さな値段、画面が低いと行を 64 まで縮める。手は画面に1つなので DesignScreen が置く（cursor=false）</summary>
public class DesignChoiceBubble
{
    public UiChoice Choice;
    public RectTransform Root => Choice.Root;
    readonly string[] keys;
    public string Selected => Choice.Selected >= 0 ? keys[Choice.Selected] : "";
    /// <summary>手を置く先（選んだ行。まだ選んでいなければ null）</summary>
    public RectTransform SelectedRow => Choice.Selected >= 0 ? Choice.Rows[Choice.Selected] : null;

    DesignChoiceBubble(string[] keys) { this.keys = keys; }

    public static DesignChoiceBubble Make(Transform p, DesignChoiceCopy[] list, Func<string, string> fill, Action<string> onPick)
    {
        var keys = new string[list.Length];
        var names = new string[list.Length];
        for (int i = 0; i < list.Length; i++) { keys[i] = list[i].Key; names[i] = list[i].Name; }
        var b = new DesignChoiceBubble(keys);
        b.Choice = UiKit.Choice(p, names, i => onPick(keys[i]), -1, false);
        for (int i = 0; i < list.Length; i++)
        {
            var row = b.Choice.Rows[i];
            Ui.Size(row).minHeight = D.ChoiceRowMinH;
            b.Choice.Labels[i].alignment = TextAlignmentOptions.MidlineLeft;
            DesignUi.RightStats(row, list[i].Stats, fill);
        }
        return b;
    }

    /// <summary>サーバーの状態を映す（onPick は呼ばない）。知らない key なら何も選ばない</summary>
    public void Select(string key) => Choice.Select(Array.IndexOf(keys, key));
}

/// <summary>タブの帯（【09】【04】【11】）：bgDeep の blob_disc を少しずつ重ね、間を帯で埋めた1本のもこもこの帯にアイコン（textSoft）。
/// 選んだタブはアイコンを accent の台に白で載せて弾ませ、上に名前の札（accent の blob_pill）。タブ1つずつのカードにしない</summary>
public class DesignTabBand
{
    class Tab { public string Key, Name; public RectTransform Slot, Body; public Image On, Icon; }
    public RectTransform Root;
    readonly List<Tab> tabs = new List<Tab>();
    RectTransform band, tag;
    TextMeshProUGUI tagText;
    string current;

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
        // こぶの間の谷を浅くする帯（1本に見せる）
        var neck = UiKit.Blob(b.band, T.BlobPill, D.BgDeep, D.TabNeckH * 0.5f, "Neck");
        UiKit.Center(neck.rectTransform, w - D.TabBumpD + D.TabNeckH, D.TabNeckH);
        for (int i = 0; i < n; i++)
        {
            var d = UiKit.Blob(b.band, T.BlobDisc, D.BgDeep, D.TabBumpD * 0.5f, "Bump");
            Place(d.rectTransform, i, step);
        }
        for (int i = 0; i < n; i++)
        {
            var c = list[i];
            var t = new Tab { Key = c.Key, Name = c.Name };
            t.Slot = Ui.Rect("Tab_" + c.Key, b.band);
            Place(t.Slot, i, step);
            var hit = t.Slot.gameObject.AddComponent<Image>(); hit.color = T.Clear;
            t.Body = Ui.Rect("Body", t.Slot); UiKit.Fill(t.Body);
            t.On = DesignUi.Disc(t.Body, T.Accent, D.TabOnD, "On");
            t.Icon = UiKit.Icon(t.Body, c.Icon, D.TabIcon, D.TextSub);
            UiKit.Center(t.Icon.rectTransform, D.TabIcon, D.TabIcon);
            string key = c.Key;
            DesignUi.Press(t.Slot, null, hit, () => onPick(key));
            b.tabs.Add(t);
        }
        b.tag = UiKit.Chip(b.Root, T.Accent, D.TabLabelH, "Name");
        b.tag.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(Mathf.RoundToInt(T.S2), Mathf.RoundToInt(T.S2), 0, 0);
        b.tag.anchorMin = b.tag.anchorMax = new Vector2(0.5f, 0f); b.tag.pivot = new Vector2(0.5f, 0.5f);
        b.tag.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        b.tag.sizeDelta = new Vector2(0f, D.TabLabelH);
        b.tagText = DesignUi.Words(b.tag, "", D.TextTag, T.Panel, TextAlignmentOptions.Center, strong: true);
        return b;
    }

    static void Place(RectTransform r, int i, float step)
    {
        r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(D.TabBumpD, D.TabBumpD);
        r.anchoredPosition = new Vector2(D.TabBumpD * 0.5f + step * i, 0f);
    }

    /// <summary>選んだタブ：アイコンを accent の台に白で（弾む）、上に名前の札（ぽんと出る）。そのタブの RectTransform を返す</summary>
    public RectTransform Select(string key)
    {
        RectTransform at = null;
        bool changed = key != current;
        current = key;
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
            if (changed) { UiSelect.Pick(t.Body); UiSelect.Pop(tag); }
        }
        return at;
    }
}

/// <summary>数のチップ（【09】【10】右上の残高）＝ UiKit.Counter：茶の blob_pill に明るい数字（Baloo 40、幅 72 の箱に右寄せ）、上にアイコンの台、右下に単位の札（left／over）。
/// 設計の画面で足す所：足りないときは danger の面で左右に揺れる。数字が変わると数え上げ／下げ、弾み、右上に −10／+10 のチップが浮いて消える。
/// 値はサーバーの totals のまま（引き算しない）</summary>
public class DesignCounter
{
    public RectTransform Root;   // レイアウトの中の入れ物（揺れ・弾みは中の UiCounter に掛ける）
    UiCounter c;
    TextMeshProUGUI unit;
    RectTransform chip;
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
        var le = Ui.Size(w.Root, w: 200f, h: D.CounterH, flexW: 0f); le.minHeight = D.CounterH; le.minWidth = 200f;

        w.c = UiKit.Counter(w.Root, icon, DesignCopy.Left);
        UiKit.Fill(w.c.Root);
        w.c.Number.alignment = TextAlignmentOptions.MidlineRight;
        w.numberLe = UiKit.Fixed(w.c.Number, D.CounterNumberW, T.TextNumber * T.HeadingLineHeight);
        w.unit = w.c.Unit.GetComponentInChildren<TextMeshProUGUI>();

        // 右上から浮く −10／+10（レイアウトの外。チップといっしょに弾まない）
        w.chip = UiKit.Chip(w.Root, T.Accent, D.DeltaChipH, "Delta");
        w.chip.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(Mathf.RoundToInt(T.S2), Mathf.RoundToInt(T.S2), 0, 0);
        w.chip.anchorMin = w.chip.anchorMax = new Vector2(0.78f, 1f); w.chip.pivot = new Vector2(0.5f, 0.5f);
        w.chip.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        w.chip.sizeDelta = new Vector2(0f, D.DeltaChipH);
        w.chipText = DesignUi.Words(w.chip, "", D.TextPrice, T.Panel, TextAlignmentOptions.Center, strong: true);
        w.chipCg = w.chip.gameObject.AddComponent<CanvasGroup>();
        w.chipCg.blocksRaycasts = false; w.chipCg.interactable = false;
        w.chip.gameObject.SetActive(false);

        if (onTap != null) DesignUi.Press(w.Root, null, w.c.Face, onTap);
        UiTick.On(w.Root.gameObject, w.Tick);
        w.Paint();
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
        c.Unit.gameObject.SetActive(has);
        numberLe.preferredWidth = numberLe.minWidth = has ? D.CounterNumberW : -1f;
        if (!has) { c.Number.text = fallback; known = false; Paint(); return; }

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
        c.Number.text = DesignValues.Fmt(Mathf.Round(Mathf.Abs(shown)));
        string u = shown < 0f || over ? DesignCopy.Over : DesignCopy.Left;
        if (unit.text == u) return;
        unit.text = u;
        c.Unit.sizeDelta = new Vector2(Mathf.Max(T.TagH, unit.GetPreferredValues(u).x + T.S4), T.TagH);
    }

    void Paint()
    {
        c.Face.color = over ? T.Danger : D.Text;
        c.Disc.Face.color = over ? T.Panel : platFace;
        c.Disc.Icon.color = over ? T.Danger : T.Panel;
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
        c.Root.localScale = new Vector3(s, s, 1f);
        float x = 0f;
        if (shakeT < D.PillShakeSec) { shakeT += dt; float p = shakeT / D.PillShakeSec; x = D.PillShakePx * Mathf.Sin(p * D.PillShakeSec * D.PillShakeHz * Mathf.PI * 2f) * (1f - p); }
        c.Root.anchoredPosition = new Vector2(x, 0f);
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

/// <summary>画面の見出しの帯（【11】左上）＝ UiKit.Ribbon：色の面に白い言葉 36。左の丸みは画面の端へ逃がし、下の辺に波の縁（wave_edge を上下に返して半分重ねる）。
/// 波の sprite が無ければ帯だけ</summary>
public static class DesignRibbon
{
    public static UiRibbon Make(Transform p, string text, Color color)
    {
        var u = UiKit.Ribbon(p, text, color);
        var r = u.Root;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        var l = r.GetComponent<HorizontalLayoutGroup>();
        l.padding.left = Mathf.RoundToInt(T.RibbonH * 0.5f + T.ScreenEdge);   // 言葉は画面の端から 32
        var fit = r.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        r.anchoredPosition = new Vector2(-T.RibbonH * 0.5f, -T.ScreenEdge);
        var wave = T.Art(T.WaveEdge);
        if (wave != null)
        {
            var img = UiKit.Shape(r, wave, color, 0f, "Wave");
            img.type = Image.Type.Tiled;
            UiKit.Ignore(img);
            var w = img.rectTransform;
            w.anchorMin = new Vector2(0f, 0f); w.anchorMax = new Vector2(1f, 0f); w.pivot = new Vector2(0.5f, 0.5f);
            w.sizeDelta = new Vector2(-T.RibbonH, D.WaveH);   // 両端の丸みの内側だけ
            w.anchoredPosition = Vector2.zero;
            w.localScale = new Vector3(1f, -1f, 1f);   // こぶを下に（art/ui/README.md）
            w.SetSiblingIndex(u.Face.transform.GetSiblingIndex() + 1);
        }
        return u;
    }
}
