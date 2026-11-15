// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-15
// 「選ぶ」の部品（#190。ui-guidelines 3章「選ぶ（いちばん大事）」）：指さしの手（1画面に1つ）・品名の札・チェック・マーカー・選んだ物の弾み。
// 色と px は UiTokens。手の動きの値は仮（UiTokens の「動き」。静止画に基準が無い）
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;

/// <summary>手の向き。Right＝選択肢と縦のリスト（左の外から右へ指す）、UpLeft＝グリッド・カード・ボタン（右下から左上へ指す）</summary>
public enum CursorDir { Right, UpLeft }

public static class UiSelect
{
    static readonly Dictionary<Transform, UiCursor> cursors = new Dictionary<Transform, UiCursor>();

    /// <summary>手を target へ動かす（画面に1つ。初めて呼んだとき、target の画面の Root＝Canvas の直下に作る）。target が null なら何もしない（隠すのは HideCursor）</summary>
    public static UiCursor Cursor(RectTransform target, CursorDir dir = CursorDir.Right)
    {
        if (target == null) return null;
        var c = CursorOf(target);
        c.Point(target, dir);
        return c;
    }

    /// <summary>その画面の手を隠す（any はその画面の中の何か）</summary>
    public static void HideCursor(Transform any)
    {
        if (any == null) return;
        var host = ScreenRoot(any);
        if (cursors.TryGetValue(host, out var c) && c != null) c.Point(null, c.Dir);
    }

    /// <summary>その画面の手（無ければ作る）</summary>
    public static UiCursor CursorOf(Transform any)
    {
        var host = ScreenRoot(any);
        if (cursors.TryGetValue(host, out var c) && c != null) return c;
        c = UiCursor.Make(host);
        cursors[host] = c;
        return c;
    }

    /// <summary>Canvas の直下の祖先（画面の Root）。Canvas が無ければ一番上</summary>
    static Transform ScreenRoot(Transform t)
    {
        var canvas = t.GetComponentInParent<Canvas>(true);
        var top = canvas != null ? canvas.rootCanvas.transform : null;
        while (t.parent != null && t.parent != top) t = t.parent;
        return t;
    }

    /// <summary>品名の札（go の blob_pill、高さ 64、白文字 32、下に小さな三角、−3°）を target の上に出す。もうあれば文字を替える</summary>
    public static UiItemTag ItemTag(RectTransform target, string text)
    {
        var old = target.Find("ItemTag");
        UiItemTag tag = old != null ? old.GetComponent<UiItemTagHost>().item : null;
        if (tag == null) tag = UiItemTag.Make(target);
        tag.Show(text);
        return tag;
    }

    /// <summary>target の右上にチェック（go の丸 40 に白い ✓）。on=false で隠す。返すのは丸</summary>
    public static Image Check(RectTransform target, bool on = true)
    {
        var old = target.Find("Check");
        Image disc;
        if (old != null) disc = old.GetComponent<Image>();
        else
        {
            disc = UiKit.Blob(target, T.BlobDisc, T.Go, T.CheckD * 0.5f, "Check");
            var r = disc.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(T.CheckD, T.CheckD); r.anchoredPosition = new Vector2(-T.CheckD * 0.2f, -T.CheckD * 0.2f);
            UiKit.Ignore(disc);
            var ic = UiKit.Icon(r, "check", T.IconS, T.Panel);
            UiKit.Center(ic.rectTransform, T.IconS, T.IconS);
        }
        disc.transform.SetAsLastSibling();
        if (on && !disc.gameObject.activeSelf) { disc.gameObject.SetActive(true); Pop(disc); }
        disc.gameObject.SetActive(on);
        return disc;
    }

    /// <summary>文字の後ろのマーカー（mark の帯。文字の幅＋左右 16、文字の下半分〜全体）。返す UiMarker の SetOn で出し入れ</summary>
    public static UiMarker Marker(TMP_Text text)
    {
        var r = Ui.Rect("Marker", text.transform.parent);
        r.SetSiblingIndex(text.transform.GetSiblingIndex());   // 文字の後ろ
        var img = r.gameObject.AddComponent<Image>();
        img.sprite = T.Theme.pill; img.type = Image.Type.Sliced; img.color = T.Mark; img.raycastTarget = false;
        UiKit.Ignore(img);
        var m = r.gameObject.AddComponent<UiMarker>();
        m.Init(text, img);
        return m;
    }

    /// <summary>ぽんと出し直す（UiPopIn を1つだけ付けて、最初から）</summary>
    public static void Pop(Component c)
    {
        var p = c.GetComponent<UiPopIn>();
        if (p == null) { UiPopIn.Add(c, 0f); return; }
        p.enabled = false; p.enabled = true;   // OnEnable で最初から
    }

    /// <summary>選んだ物が 1.0 → 1.12 → 1.0 と弾む（hold=true なら 1.12 のまま。カードの選択）</summary>
    public static void Pick(Component c, bool hold = false)
    {
        var p = c.GetComponent<UiPick>();
        if (p == null) p = c.gameObject.AddComponent<UiPick>();
        p.Play(hold);
    }

    /// <summary>Pick の 1.12 のままをやめて 1.0 に戻す</summary>
    public static void Unpick(Component c)
    {
        var p = c.GetComponent<UiPick>();
        if (p != null) p.Release();
    }
}

/// <summary>指さしの手（画面に1つ）。target の左の外（Right）か右下（UpLeft）に指先を置き、上下にゆれる。Poke で指す方へ突く</summary>
public class UiCursor : MonoBehaviour
{
    public RectTransform Target { get; private set; }
    public CursorDir Dir { get; private set; }
    RectTransform rt, hand;
    Vector2 from, shown;
    float moveT = 1f, pokeT = -1f;
    bool placed;

    internal static UiCursor Make(Transform host)
    {
        var r = Ui.Rect("Cursor", host);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = Vector2.zero;
        UiKit.Ignore(r.gameObject.AddComponent<LayoutElement>());
        var c = r.gameObject.AddComponent<UiCursor>();
        c.rt = r;
        c.hand = Ui.Rect("Hand", r);
        c.hand.anchorMin = c.hand.anchorMax = new Vector2(0.5f, 0.5f);
        c.hand.pivot = new Vector2(1f, 0.5f);   // 指先（右の真ん中）が原点
        c.hand.sizeDelta = new Vector2(T.CursorSize, T.CursorSize);
        c.hand.anchoredPosition = Vector2.zero;
        var glove = T.Art(T.Glove);
        if (glove != null)
        {
            var img = c.hand.gameObject.AddComponent<Image>();
            img.sprite = glove; img.preserveAspect = true; img.raycastTarget = false;
            c.hand.pivot = new Vector2(0.94f, 0.61f);   // 指の先 (136, 56)（art/ui/README.md）
        }
        else FallbackHand(c.hand);
        c.hand.gameObject.SetActive(false);
        return c;
    }

    /// <summary>glove_point が無いときの仮の手（丸い手のひら＋人さし指＋親指、text の輪郭、accent の手首）</summary>
    static void FallbackHand(RectTransform h)
    {
        float o = T.CursorSize / 24f;   // 輪郭の太さ（144 で 6 → 72 で 3）
        // x0,y0,x1,y1（手の 0〜1）
        float[][] parts = { new[] { 0.42f, 0.38f, 1.0f, 0.62f }, new[] { 0.12f, 0.2f, 0.62f, 0.8f }, new[] { 0.36f, 0.6f, 0.58f, 0.86f } };
        var wrist = UiKit.Pill(h, T.Accent, T.CursorSize * 0.25f, "Wrist");
        Ui.Anchor(wrist.rectTransform, 0f, 0.26f, 0.2f, 0.74f);
        foreach (var p in parts) { var e = UiKit.Pill(h, T.Text, T.CursorSize * 0.25f, "Edge"); Ui.Anchor(e.rectTransform, p[0], p[1], p[2], p[3], -o, -o, -o, -o); }
        foreach (var p in parts) { var f = UiKit.Pill(h, T.Panel, T.CursorSize * 0.25f, "Fill"); Ui.Anchor(f.rectTransform, p[0], p[1], p[2], p[3]); }
    }

    /// <summary>乗せる先と向きを替える（null で隠す）。前の場所から CursorMoveSec で動く</summary>
    public void Point(RectTransform target, CursorDir dir)
    {
        if (target == Target && dir == Dir && (target == null) == !hand.gameObject.activeSelf) return;
        Target = target; Dir = dir;
        hand.localRotation = Quaternion.Euler(0f, 0f, dir == CursorDir.UpLeft ? 135f : 0f);   // 左上向きは Z = 135（art/ui/README.md）
        if (target == null) { hand.gameObject.SetActive(false); placed = false; return; }
        from = shown; moveT = placed ? 0f : 1f;
        rt.SetAsLastSibling();
        hand.gameObject.SetActive(true);
    }

    /// <summary>押したとき：指す方へ CursorPoke 突いて戻る</summary>
    public void Poke() => pokeT = 0f;

    void LateUpdate()
    {
        if (Target == null) return;
        bool live = Target.gameObject.activeInHierarchy;
        if (hand.gameObject.activeSelf != live) hand.gameObject.SetActive(live);
        if (!live) { placed = false; return; }

        var parent = (RectTransform)rt.parent;
        Rect r = Target.rect;
        Vector2 tipLocal = Dir == CursorDir.Right
            ? new Vector2(r.xMin - T.S1, r.center.y)
            : new Vector2(r.xMax - r.width * 0.2f, r.yMin + r.height * 0.2f);
        Vector2 to = parent.InverseTransformPoint(Target.TransformPoint(tipLocal));
        if (!placed) { from = to; moveT = 1f; placed = true; }

        float dt = UiEase.Dt;
        moveT += dt;
        Vector2 pos = moveT >= T.CursorMoveSec ? to : Vector2.LerpUnclamped(from, to, UiEase.OutBack(moveT / T.CursorMoveSec));
        shown = pos;

        Vector2 point = Dir == CursorDir.Right ? Vector2.right : new Vector2(-0.7071f, 0.7071f);
        float bob = Mathf.Sin(UiEase.Now * Mathf.PI * 2f / T.CursorPeriod) * T.CursorBob * 0.5f;
        float poke = 0f;
        if (pokeT >= 0f)
        {
            pokeT += dt;
            poke = UiEase.Bump(pokeT / T.CursorPokeSec) * T.CursorPoke;
            if (pokeT >= T.CursorPokeSec) pokeT = -1f;
        }
        rt.anchoredPosition = pos + Vector2.up * bob + point * poke - parent.rect.center;
    }
}

/// <summary>品名の札。target の子（レイアウトの外）。Show で文字を替えてぽんと出す</summary>
public class UiItemTag
{
    public RectTransform Root;
    public TextMeshProUGUI Label;
    public Image Face;

    internal static UiItemTag Make(RectTransform target)
    {
        var t = new UiItemTag();
        t.Root = UiKit.Row(target, 0f, TextAnchor.MiddleCenter, T.S3);
        t.Root.name = "ItemTag";
        t.Root.anchorMin = t.Root.anchorMax = new Vector2(0.5f, 1f);
        t.Root.pivot = new Vector2(0.5f, 0f);
        t.Root.anchoredPosition = new Vector2(0f, -T.ItemTagH * 0.25f);
        t.Root.localRotation = Quaternion.Euler(0f, 0f, T.ItemTagTilt);
        UiKit.Ignore(t.Root.gameObject.AddComponent<LayoutElement>());
        var fit = t.Root.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        t.Face = UiKit.Blob(t.Root, T.BlobPill, T.Go, T.ItemTagH * 0.5f, "Face"); UiKit.Fill(t.Face.rectTransform); UiKit.Ignore(t.Face);
        var caret = UiKit.Shape(t.Root, T.Art(T.CaretSoft) ?? T.Theme.caretDown, T.Go, 0f, "Caret");
        var cr = caret.rectTransform;
        cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0f); cr.pivot = new Vector2(0.5f, 1f);
        cr.sizeDelta = new Vector2(T.CaretW * 0.75f, T.CaretH * 0.75f); cr.anchoredPosition = new Vector2(0f, T.S1 * 0.5f);
        UiKit.Ignore(caret);
        t.Label = UiKit.Text(t.Root, "", TextStyle.Choice, T.Panel);
        Ui.Size(t.Root, h: T.ItemTagH).minHeight = T.ItemTagH;
        t.Root.sizeDelta = new Vector2(t.Root.sizeDelta.x, T.ItemTagH);
        t.Root.gameObject.AddComponent<UiItemTagHost>().item = t;
        return t;
    }

    public void Show(string text)
    {
        Label.text = text;
        Root.SetAsLastSibling();
        if (!Root.gameObject.activeSelf) Root.gameObject.SetActive(true);
        UiSelect.Pop(Root);
    }

    public void Hide() => Root.gameObject.SetActive(false);
}

/// <summary>札を target から見つけるための入れ物</summary>
public class UiItemTagHost : MonoBehaviour { public UiItemTag item; }

/// <summary>文字の後ろのマーカー。毎フレーム、文字の幅に合わせる</summary>
public class UiMarker : MonoBehaviour
{
    public bool On { get; private set; } = true;
    TMP_Text text;
    Image img;
    RectTransform rt;

    internal void Init(TMP_Text t, Image i) { text = t; img = i; rt = (RectTransform)transform; Fit(); }

    public void SetOn(bool on)
    {
        On = on;
        img.enabled = on;
        if (on) Fit();
    }

    void LateUpdate() { if (On) Fit(); }

    void Fit()
    {
        if (text == null) return;
        float w = text.GetPreferredValues(text.text).x + T.MarkPadX * 2f;
        float h = text.fontSize * 0.62f;
        var parent = (RectTransform)rt.parent;
        var tr = text.rectTransform;
        // 文字の帯の中心（横は揃えに合わせる。縦は字の下寄り）
        float x = text.alignment == TextAlignmentOptions.MidlineLeft || text.alignment == TextAlignmentOptions.Left
            ? tr.rect.xMin + w * 0.5f - T.MarkPadX
            : tr.rect.center.x;
        Vector2 c = parent.InverseTransformPoint(tr.TransformPoint(new Vector2(x, tr.rect.center.y - text.fontSize * 0.12f)));
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = c - parent.rect.center;
        img.pixelsPerUnitMultiplier = img.sprite != null && img.sprite.border.x > 0f ? img.sprite.border.x / (h * 0.5f) : 1f;
    }
}

/// <summary>選んだ物の弾み（PickScale・PickSec）。hold なら大きいまま</summary>
public class UiPick : MonoBehaviour
{
    float t = -1f;
    bool hold;

    public void Play(bool hold) { this.hold = hold; t = 0f; enabled = true; }
    public void Release() { hold = false; t = -1f; transform.localScale = Vector3.one; }

    void Update()
    {
        if (t < 0f) return;
        t += UiEase.Dt;
        float p = Mathf.Clamp01(t / T.PickSec);
        float s = hold ? Mathf.LerpUnclamped(1f, T.PickScale, UiEase.OutBack(p)) : 1f + (T.PickScale - 1f) * UiEase.Bump(p);
        transform.localScale = new Vector3(s, s, 1f);
        if (p >= 1f) t = -1f;
    }

    void OnDisable() { if (!hold) transform.localScale = Vector3.one; }
}
