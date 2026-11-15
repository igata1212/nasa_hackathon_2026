// 書いた人：Claude ／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-15、#189 席の仕事 Seats.Job）
// 骨組み用の UGUI 部品（コードで組む。見た目は仮。本番の作り込みは UI の Issue #71〜#94）
using System;
using UnityEngine;
using UnityEngine.UI;

public static class Ui
{
    public static readonly Color Navy = Hex("1D2D50"), Cream = Hex("F2E3C6"), Orange = Hex("F28C28"), Teal = Hex("2A9D8F");
    public static readonly Color Dim = new Color(0.07f, 0.1f, 0.2f, 0.92f), Panel = new Color(0.11f, 0.17f, 0.31f, 0.88f);
    public static readonly Color Red = Hex("E4572E"), Green = Hex("6BBF59"), Yellow = Hex("FFC857"), Blue = Hex("3A86FF"), Gray = Hex("5A6478");

    static Font font;
    public static Font Font => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

    public static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void Fill(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

    /// <summary>親に対する割合で置く。余白は左・下・右・上の内側への量（px）</summary>
    public static RectTransform Anchor(RectTransform r, float x0, float y0, float x1, float y1, float l = 0, float b = 0, float rt = 0, float t = 0)
    {
        r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1);
        r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rt, -t);
        return r;
    }

    public static Image PanelImg(Transform p, Color c, string name = "Panel")
    {
        var r = Rect(name, p);
        var i = r.gameObject.AddComponent<Image>(); i.color = c;
        return i;
    }

    public static Text Label(Transform p, string s, int size = 24, Color? c = null, TextAnchor a = TextAnchor.MiddleLeft, FontStyle st = FontStyle.Normal)
    {
        var r = Rect("Label", p);
        var t = r.gameObject.AddComponent<Text>();
        t.font = Font; t.text = s; t.fontSize = size; t.color = c ?? Cream; t.alignment = a; t.fontStyle = st;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    public static Button Btn(Transform p, string s, Color bg, Action on, int size = 24, Color? fg = null)
    {
        var img = PanelImg(p, bg, "Button");
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var cb = b.colors; cb.highlightedColor = new Color(1f, 1f, 1f, 0.85f); cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); b.colors = cb;
        var t = Label(img.transform, s, size, fg ?? Navy, TextAnchor.MiddleCenter, FontStyle.Bold);
        Fill(t.rectTransform);
        if (on != null) b.onClick.AddListener(() => on());
        return b;
    }

    public static void SetBtn(Button b, string s, Color bg, Color? fg = null)
    {
        b.GetComponent<Image>().color = bg;
        var t = b.GetComponentInChildren<Text>(); t.text = s; if (fg.HasValue) t.color = fg.Value;
    }

    public static RectTransform VBox(Transform p, float spacing = 8, int pad = 0, TextAnchor a = TextAnchor.UpperLeft, bool expand = false)
    {
        var r = Rect("VBox", p);
        var l = r.gameObject.AddComponent<VerticalLayoutGroup>();
        l.spacing = spacing; l.padding = new RectOffset(pad, pad, pad, pad); l.childAlignment = a;
        l.childControlWidth = l.childControlHeight = true; l.childForceExpandWidth = true; l.childForceExpandHeight = expand;
        return r;
    }

    public static RectTransform HBox(Transform p, float spacing = 8, int pad = 0, TextAnchor a = TextAnchor.MiddleLeft, bool expand = false)
    {
        var r = Rect("HBox", p);
        var l = r.gameObject.AddComponent<HorizontalLayoutGroup>();
        l.spacing = spacing; l.padding = new RectOffset(pad, pad, pad, pad); l.childAlignment = a;
        l.childControlWidth = l.childControlHeight = true; l.childForceExpandHeight = true; l.childForceExpandWidth = expand;
        return r;
    }

    public static LayoutElement Size(Component c, float w = -1, float h = -1, float flexW = -1, float flexH = -1)
    {
        var le = c.GetComponent<LayoutElement>(); if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
        if (w >= 0) le.preferredWidth = w; if (h >= 0) le.preferredHeight = h;
        if (flexW >= 0) le.flexibleWidth = flexW; if (flexH >= 0) le.flexibleHeight = flexH;
        return le;
    }

    public static InputField Input(Transform p, string placeholder, string initial)
    {
        var img = PanelImg(p, new Color(1f, 1f, 1f, 0.12f), "Input");
        var f = img.gameObject.AddComponent<InputField>();
        var txt = Label(img.transform, "", 26, Cream); Anchor(txt.rectTransform, 0, 0, 1, 1, 12, 4, 12, 4); txt.raycastTarget = true;
        var ph = Label(img.transform, placeholder, 26, new Color(1f, 1f, 1f, 0.35f)); Anchor(ph.rectTransform, 0, 0, 1, 1, 12, 4, 12, 4);
        f.textComponent = txt; f.placeholder = ph; f.text = initial; f.targetGraphic = img;
        return f;
    }

    public static string Mmss(float sec)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(sec));
        return (s / 60) + ":" + (s % 60).ToString("00");
    }
}

/// <summary>席の色とアイコン（art-direction.md：Pilot 赤▲／Life Support 緑●／Comms &amp; Power 黄■／Mission Planner 青◆）</summary>
public static class Seats
{
    public const string Pilot = "pilot", Life = "life_support", Comms = "comms_power", Planner = "mission_planner";
    public static readonly string[] All = { Pilot, Life, Comms, Planner };

    public static Color Tint(string s) => s == Pilot ? Ui.Red : s == Life ? Ui.Green : s == Comms ? Ui.Yellow : Ui.Blue;
    public static string Icon(string s) => s == Pilot ? "▲" : s == Life ? "●" : s == Comms ? "■" : "◆";
    public static string Title(string s) => s == Pilot ? "PILOT" : s == Life ? "LIFE SUPPORT" : s == Comms ? "COMMS & POWER" : "MISSION PLANNER";
    /// <summary>席の仕事（部屋の名前タグの3行目。#189：役の名前だけでは何をする席か分からなかった。ブリーフィング B06・B07 と同じ中身）</summary>
    public static string Job(string s) => s == Pilot ? "Turns the ship" : s == Life ? "Keeps the air safe" : s == Comms ? "Radio and power" : "Route and science";
}
