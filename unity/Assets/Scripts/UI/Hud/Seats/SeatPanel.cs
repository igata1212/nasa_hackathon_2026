// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-15、#190 あつ森に寄せた：盆・手・主ボタンの見た目を下の SeatLook に）
// 作る Issue：#147（席のパネルの基底）。中身は #80 Pilot・#81 Life Support・#82 Comms & Power・#83 Mission Planner。Highlight は #84 が使う口
//
// 1席ぶんの操作のパネル。CabinHud が席タブで選んだ席のパネルを作り、席・段階（stir かどうか）が変わるたびに作り直す。
// 兼任の人は席タブで1席ずつ切り替える（#102）。同時押し（点火・フィルター交換）は Ctx.SendDual で送る。
// #190（ui-guidelines 1章「船内の計器は濃紺のまま」・3章「画面ごとの選択の替え方」。基準【04】【09】【10】【11】）：
//   ・下の帯はクリームの盆（上端が波の縁）。作るのは SeatTabBar（CabinHud を触らないため、紺の帯を透明にして盆を敷く）
//   ・中身は横1列の SeatStrip。入りきらない画面では全体を縮める。1回押す物（Ignite など）は盆の右端
//   ・光る輪はやめた（7章「光る枠」）。「次に押す物」は指さしの手（1画面に1つ）を乗せて、ときどき突く。主ボタンは息をする
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;

public abstract class SeatPanel : HudPart
{
    /// <summary>この席（Seats.Pilot など）</summary>
    public abstract string Seat { get; }
    /// <summary>この席のパネルが送るコマンド（server/game/seats.py の seats_for）。#84 が「まだ試していない操作」を探すのに使う</summary>
    public abstract IEnumerable<string> Commands { get; }

    /// <summary>その席のパネルを作る（CabinHud が呼ぶ）。知らない席なら null</summary>
    public static SeatPanel Create(string seat)
    {
        switch (seat)
        {
            case Seats.Pilot: return new PilotPanel();
            case Seats.Life: return new LifeSupportPanel();
            case Seats.Comms: return new CommsPowerPanel();
            case Seats.Planner: return new PlannerPanel();
            default: return null;
        }
    }

    protected override void Build(RectTransform root)
    {
        var row = Ui.HBox(root, 12, 0, TextAnchor.MiddleLeft, true); Ui.Fill(row);
        if (Ctx.Model.Phase == "stir") BuildStir(row);   // かき混ぜ（M04）の間は、かき混ぜのボタンだけ
        else BuildControls(row);
    }

    /// <summary>practice・flight のときの操作（2〜3 かたまり、#102）</summary>
    protected abstract void BuildControls(RectTransform row);
    /// <summary>stir の段階のときの操作。Life Support だけがかき混ぜのボタンを出す。ほかの席は空</summary>
    protected virtual void BuildStir(RectTransform row) { }

    /// <summary>このコマンドの操作を「次に押す物」にする／戻す（#84 が練習の間に「まだ試していない操作」に使う）</summary>
    public virtual void Highlight(string cmd, bool on) { }

    /// <summary>手をまだ置いていなければ隠す（作り直す前のパネルを指したまま残さない。stir の段階など、手を使わないパネルのため）。
    /// 各パネルは base.Refresh のあとで Point する</summary>
    public override void Refresh(GameState s)
    {
        if (!cursorSet) Point(null, CursorDir.UpLeft);
    }

    // ------------------------------------------------------------------ 指さしの手（#190。1画面に1つ。UiSelect）
    RectTransform cursorAt; CursorDir cursorDir; bool cursorSet, cursorNow;
    float nextPoke;

    /// <summary>手を target へ（null で隠す）。now＝「いま押す物」（ときどき突く）。変わったときだけ動かす（ほかの部品が手を使っても毎回取り返さない）</summary>
    protected void Point(RectTransform target, CursorDir dir, bool now = false)
    {
        cursorNow = now && target != null;
        if (cursorSet && target == cursorAt && dir == cursorDir) return;
        var prev = cursorAt;
        cursorSet = true; cursorAt = target; cursorDir = dir;
        if (target != null) { UiSelect.Cursor(target, dir); return; }
        var c = UiSelect.CursorOf(Root);
        if (c.Target == null || c.Target == prev) UiSelect.HideCursor(Root);   // 作り直す前のパネルを指したまま残さない。ほかの部品の手は消さない
    }

    /// <summary>Tick から：「いま押す物」を指しているあいだ、SeatLook.PokeEvery ごとに手を突く</summary>
    protected void PokeCursor(float now)
    {
        if (!cursorNow || now < nextPoke) return;
        nextPoke = now + SeatLook.PokeEvery;
        var c = UiSelect.CursorOf(Root);
        if (c.Target == cursorAt) c.Poke();
    }
}

// ======================================================================
// SeatLook：4つの席のパネルが使う、#190 の小さな部品（共通の部品 UiKit に無い物だけ）。色・px は UiTokens から
// ======================================================================

/// <summary>ピルのボタンの見た目。Main＝主ボタン（go＋流れる縞＋goLight のふち）／Plain＝副（panel＋bgDeep のふち）／Off＝押せない（line＋鍵）</summary>
public enum SeatTone { Main, Plain, Off }

/// <summary>ピルのボタンの面（ふち＋面＋縞）。Paint で見た目を替え、文字とアイコンの色を返す</summary>
public class SeatFace
{
    public Image Rim, Face;
    public Graphic Stripes;
    public SeatTone Tone { get; private set; } = (SeatTone)(-1);

    public static SeatFace Pill(RectTransform body, float h)
    {
        var f = new SeatFace();
        f.Rim = UiKit.Blob(body, T.BlobPill, T.BgDeep, h * 0.5f, "Rim"); UiKit.Fill(f.Rim.rectTransform); UiKit.Ignore(f.Rim);
        f.Face = UiKit.Blob(body, T.BlobPill, T.Panel, h * 0.5f - T.RimButton, "Face"); UiKit.Fill(f.Face.rectTransform, T.RimButton); UiKit.Ignore(f.Face);
        f.Stripes = SeatLook.Stripes(f.Face);
        f.Paint(SeatTone.Plain);
        return f;
    }

    public void Paint(SeatTone t)
    {
        Tone = t;
        Rim.color = t == SeatTone.Main ? T.GoLight : T.BgDeep;
        Face.color = t == SeatTone.Main ? T.Go : t == SeatTone.Off ? T.Line : T.Panel;
        if (Stripes != null) Stripes.enabled = t == SeatTone.Main;
    }

    /// <summary>この見た目の上の文字（主の言葉）と補助の行の色</summary>
    public static void Ink(SeatTone t, out Color fg, out Color sub)
    {
        if (t == SeatTone.Main) { fg = sub = T.Panel; return; }
        fg = t == SeatTone.Off ? T.TextSoft : T.Text;
        sub = T.TextSoft;
    }
}

/// <summary>席のパネルの中身を横1列に並べる帯。高さは中身に合わせ、入りきらない画面（幅・高さ）では全体を縮める。
/// 余った幅は Spacer が受け取る（盆の右端に主ボタンを置くため）</summary>
public class SeatStrip
{
    public RectTransform Rect, Area;

    public void Fit()
    {
        float aw = Area.rect.width, ah = Area.rect.height;
        if (aw <= 0f || ah <= 0f) return;
        float pw = LayoutUtility.GetPreferredWidth(Rect), ph = LayoutUtility.GetPreferredHeight(Rect);
        if (pw <= 0f || ph <= 0f) return;
        float k = Mathf.Min(1f, aw / pw, ah / ph);
        var size = new Vector2(aw / k, ph);
        if ((Rect.sizeDelta - size).sqrMagnitude > 0.25f) Rect.sizeDelta = size;
        if (!Mathf.Approximately(Rect.localScale.x, k)) Rect.localScale = new Vector3(k, k, 1f);
    }
}

/// <summary>ON/OFF のつまみ（材料の行の右に置く）。ON＝go の溝・丸が右・「ON」、OFF＝bgDeep の溝・丸が左・「OFF」（色だけに頼らない）</summary>
public class SeatKnob
{
    public RectTransform Root, Dot;
    public Image Track;
    public TextMeshProUGUI Label;

    public void Set(bool on)
    {
        Track.color = on ? T.Go : T.BgDeep;
        Label.text = on ? SeatLook.On : SeatLook.Off;
        Label.color = on ? T.Panel : T.Text;
        float pad = SeatLook.KnobPad, d = SeatLook.KnobH;
        Dot.anchorMin = Dot.anchorMax = Dot.pivot = new Vector2(on ? 1f : 0f, 0.5f);
        Dot.anchoredPosition = new Vector2(on ? -pad : pad, 0f);
        var lr = Label.rectTransform;   // 文字は丸の反対側
        lr.offsetMin = new Vector2(on ? pad : d, 0f);
        lr.offsetMax = new Vector2(on ? -d : -pad, 0f);
    }
}

/// <summary>盆の上端の波の縁（wave_edge.png が読めないときの代わり）：半径 WaveR のこぶを横に並べる。下の WaveH − こぶの高さ は面</summary>
public class SeatWave : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = rectTransform.rect;
        float rad = SeatLook.WaveR, cap = SeatLook.WaveH * 0.5f, baseY = r.yMin + (r.height - cap);
        int seg = SeatLook.WaveSeg;
        float period = rad * 2f;
        // こぶ1つ＝円の頭（高さ cap）。円の中心は baseY − (rad − cap)
        float cy = baseY - (rad - cap), half = Mathf.Sqrt(rad * rad - (rad - cap) * (rad - cap));
        for (float x0 = r.xMin; x0 < r.xMax; x0 += period)
        {
            float cx = x0 + period * 0.5f;
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(cx, r.yMin), color, Vector4.zero);
            for (int i = 0; i <= seg; i++)
            {
                float x = Mathf.Lerp(cx - half, cx + half, i / (float)seg);
                float y = cy + Mathf.Sqrt(Mathf.Max(0f, rad * rad - (x - cx) * (x - cx)));
                vh.AddVert(new Vector3(Mathf.Clamp(x, r.xMin, r.xMax), Mathf.Max(y, baseY)), color, Vector4.zero);
            }
            for (int i = 0; i < seg; i++) vh.AddTriangle(start, start + 1 + i, start + 2 + i);
            // こぶの下（baseY まで）の四角
            int q = vh.currentVertCount;
            vh.AddVert(new Vector3(Mathf.Max(x0, r.xMin), r.yMin), color, Vector4.zero);
            vh.AddVert(new Vector3(Mathf.Max(x0, r.xMin), baseY), color, Vector4.zero);
            vh.AddVert(new Vector3(Mathf.Min(x0 + period, r.xMax), baseY), color, Vector4.zero);
            vh.AddVert(new Vector3(Mathf.Min(x0 + period, r.xMax), r.yMin), color, Vector4.zero);
            vh.AddTriangle(q, q + 1, q + 2); vh.AddTriangle(q, q + 2, q + 3);
        }
    }
}

public static class SeatLook
{
    // ------------------------------------------------------------------ 大きさ（UiTokens から。px は Canvas の基準 1600×900）
    public const float GroupGap = T.S4;               // かたまりの間 32
    public const float Gap = T.S2;                    // かたまりの中 16
    public const float BtnH = T.SecondaryH;           // ピルのボタンの見た目の高さ 72（押せる範囲は T.Hit）
    public const float MainH = T.PrimaryH;            // 主ボタン（Ignite など）96
    public const float ActionW = T.PrimaryMinW + T.S3;   // 主ボタンの置き場の幅 344（2行目の文が入る）
    public const float KnobW = T.Hit - T.S1, KnobH = T.CheckD, KnobPad = T.S1 / 2f;   // つまみ 88×40
    public const float WaveH = T.S3, WaveR = T.S3;    // 盆の上の波の縁（wave_edge.png の 96×24、こぶの半径 24・高さ 12 と同じ）
    public const int WaveSeg = 12;                    // 代わりの波の、こぶ1つの分け方
    public const float TileH = 128;                   // bands64.png の縦の周期（上 64 が透明・下 64 が白）
    public const float LockD = T.CheckD;              // 押せない物の右上の鍵の台
    public const float CursorRoom = T.CursorSize + T.S1;   // 帯の先頭を右向きの手で指すときの左の空き 80
    // ------------------------------------------------------------------ 動き（仮の値。静止画に基準が無い。iizuka が見て決める）
    public const float PokeEvery = 1.2f;              // 「いま押す物」を指す手が突く間隔（秒）
    public const string On = "ON", Off = "OFF";

    /// <summary>盆（席タブと席のパネルの下の帯）：CabinHud の紺の帯（tabBar の親の親の Image）を透明にして、その後ろにクリームの盆を敷く。
    /// 盆の上端は席タブ（名前タグ）の高さの真ん中、左右と下は画面の端まで。上端に wave_edge の波（【04】）、中に bands64 の横縞（【05】）</summary>
    public static void Tray(RectTransform tabBar)
    {
        var tabs = tabBar.parent as RectTransform;
        var band = tabs != null ? tabs.parent as RectTransform : null;
        if (band == null) return;
        var old = band.GetComponent<Image>();
        if (old != null) old.color = T.Clear;

        var tray = Ui.Rect("Tray", band); tray.SetAsFirstSibling();
        // 左右と下は画面の端まで（band は slot いっぱい。slot の割合から外へ広げる。world-space の載せ先なら 0〜1 のまま）
        var slot = band.parent as RectTransform;
        Vector2 a0 = slot != null ? slot.anchorMin : Vector2.zero, a1 = slot != null ? slot.anchorMax : Vector2.one;
        float sw = Mathf.Max(0.0001f, a1.x - a0.x), sh = Mathf.Max(0.0001f, a1.y - a0.y);
        tray.anchorMin = new Vector2(-a0.x / sw, -a0.y / sh);
        tray.anchorMax = new Vector2((1f - a0.x) / sw, 1f);
        tray.pivot = new Vector2(0.5f, 1f);
        float top = (tabs.offsetMin.y + tabs.offsetMax.y) * 0.5f;   // 席タブの段の真ん中（band の上端から。負）
        tray.offsetMin = Vector2.zero; tray.offsetMax = new Vector2(0f, top);

        var face = tray.gameObject.AddComponent<Image>(); face.color = T.Panel; face.raycastTarget = true;   // 盆の上を押しても 3D に抜けない
        var bands = T.Art(T.Bands);
        if (bands != null)
        {
            var clip = Ui.Rect("Bands", tray); UiKit.Fill(clip);
            clip.gameObject.AddComponent<RectMask2D>();
            var bi = UiKit.Shape(clip, bands, T.PanelBand, 0f, "Tiles");
            bi.type = Image.Type.Tiled;
            var br = bi.rectTransform;   // 上端を縞の周期の切れ目に合わせる（一番上は透明＝panel。波の縁と同じ色でつながる）
            br.anchorMin = Vector2.zero; br.anchorMax = new Vector2(1f, 0f); br.pivot = new Vector2(0.5f, 0f);
            br.sizeDelta = new Vector2(0f, TileH * 2f); br.anchoredPosition = Vector2.zero;
        }

        var wr = Ui.Rect("WaveEdge", tray);
        wr.anchorMin = new Vector2(0f, 1f); wr.anchorMax = Vector2.one; wr.pivot = new Vector2(0.5f, 0f);
        wr.sizeDelta = new Vector2(0f, WaveH); wr.anchoredPosition = new Vector2(0f, -WaveH * 0.5f);   // 下半分は面にかぶせる
        var wave = T.Art(T.WaveEdge);
        if (wave != null)
        {
            var wi = wr.gameObject.AddComponent<Image>();
            wi.sprite = wave; wi.type = Image.Type.Tiled; wi.color = T.Panel; wi.raycastTarget = false;
        }
        else { var g = wr.gameObject.AddComponent<SeatWave>(); g.color = T.Panel; g.raycastTarget = false; }
    }

    /// <summary>帯：SeatPanel の行（Ui.HBox）の中に、レイアウトの外として左の真ん中に置く。
    /// padLeft：先頭の物を右向きの手で指すときは CursorRoom（手が盆の外にはみ出さないように）</summary>
    public static SeatStrip Strip(RectTransform row, float padLeft = 0f)
    {
        var r = UiKit.Row(row, GroupGap, TextAnchor.MiddleLeft);
        r.GetComponent<HorizontalLayoutGroup>().padding.left = Mathf.RoundToInt(padLeft);
        r.name = "Strip";
        Ui.Size(r).ignoreLayout = true;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 0.5f);
        r.anchoredPosition = Vector2.zero;
        return new SeatStrip { Rect = r, Area = row };
    }

    /// <summary>余った幅を受け取る（この後ろの物は盆の右端へ寄る）</summary>
    public static RectTransform Spacer(Transform strip)
    {
        var r = Ui.Rect("Spacer", strip);
        var le = Ui.Size(r, w: 0f, flexW: 1f); le.minWidth = 0f;
        return r;
    }

    /// <summary>主ボタンの斜めの縞（stripes64 を goLight で、面の形で切って横に流す。UiKit の主ボタンと同じ作り）</summary>
    public static Graphic Stripes(Image face)
    {
        var tex = T.Theme.stripes != null ? T.Theme.stripes.texture : null;
        if (tex == null) return null;
        face.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        var r = Ui.Rect("Stripes", face.transform); UiKit.Fill(r);
        var raw = r.gameObject.AddComponent<RawImage>();
        raw.texture = tex; raw.raycastTarget = false;
        raw.color = T.WithAlpha(T.GoLight, T.StripeAlpha / 0.4f);   // stripes64 は白の 40%
        r.gameObject.AddComponent<UiStripes>();
        return raw;
    }

    /// <summary>押せる所（Button。ふつうの色の変化＋押した音＋押すと縮む弾み）。弾みは UiKit.Breathe で付ける（UiSquish を2つ付けない）。
    /// squish=false：大きさを別の物が動かすとき（選んだカードの 1.12 倍など）</summary>
    public static Button Press(RectTransform root, Graphic target, System.Action on, bool squish = true)
    {
        var b = root.gameObject.AddComponent<Button>();
        b.targetGraphic = target;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        var cb = b.colors;
        cb.normalColor = cb.selectedColor = cb.disabledColor = Color.white;
        cb.highlightedColor = T.TintHover; cb.pressedColor = T.TintPressed; cb.fadeDuration = T.TintFade;
        b.colors = cb;
        if (on != null) b.onClick.AddListener(() => on());
        root.gameObject.AddComponent<UiPress>().Init(root, b);
        if (squish) Breathe(root, b, false);
        return b;
    }

    /// <summary>主ボタンが息をする（いま押す物）／止める。UiSquish は UiKit.Breathe が1つだけ付ける</summary>
    public static void Breathe(RectTransform root, Button b, bool on) =>
        UiKit.Breathe(new UiButton { Root = root, Button = b }, on ? TitleLook.BreatheAmp : 0f);

    /// <summary>ON/OFF のつまみ</summary>
    public static SeatKnob Knob(Transform p)
    {
        var k = new SeatKnob();
        k.Root = Ui.Rect("Knob", p);
        UiKit.Fixed(k.Root, KnobW, KnobH);
        k.Track = UiKit.Blob(k.Root, T.BlobPill, T.BgDeep, KnobH * 0.5f, "Track"); UiKit.Fill(k.Track.rectTransform);
        float dd = KnobH - KnobPad * 2f;
        var dot = UiKit.Blob(k.Root, T.BlobDisc, T.Panel, dd * 0.5f, "Dot");
        k.Dot = dot.rectTransform; k.Dot.sizeDelta = Vector2.one * dd;
        k.Label = UiKit.Text(k.Root, Off, TextStyle.Label, T.Text, TextAlignmentOptions.Center);
        var lr = k.Label.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        UiKit.Ignore(k.Label);
        k.Set(false);
        return k;
    }

    /// <summary>押せない物の右上の鍵（textSoft の小さな台に白い lock。ガイドライン 3章「押せない物」）。on=false で隠す</summary>
    public static void Lock(RectTransform target, bool on)
    {
        var old = target.Find("Lock");
        if (old == null)
        {
            if (!on) return;
            var d = UiKit.Disc(target, "lock", T.TextSoft, LockD);
            d.Root.name = "Lock";
            UiKit.Ignore(d.Root);
            d.Root.anchorMin = d.Root.anchorMax = new Vector2(1f, 1f); d.Root.pivot = new Vector2(0.5f, 0.5f);
            d.Root.anchoredPosition = new Vector2(-LockD * 0.2f, -LockD * 0.2f);
            old = d.Root;
        }
        old.gameObject.SetActive(on);
        if (on) old.SetAsLastSibling();
    }

    /// <summary>文字の高さを字の大きさの HeadingLineHeight 倍に詰める（Nunito の行は高いので、2行を 64・96 の行に入れるとき）</summary>
    public static void Tight(TextMeshProUGUI t) => Ui.Size(t, h: t.fontSize * T.HeadingLineHeight).minHeight = t.fontSize * T.HeadingLineHeight;

    /// <summary>どの文になっても幅が変わらないように、一番広い文の幅を取っておく</summary>
    public static void ReserveWidth(TextMeshProUGUI t, params string[] samples)
    {
        float w = 0f;
        foreach (var s in samples) w = Mathf.Max(w, t.GetPreferredValues(s).x);
        var le = Ui.Size(t, w: w); le.minWidth = w;
    }

    /// <summary>数のチップの単位の札を替える（UiKit.Counter は作ったときの文字で札の幅を決めるので、幅も合わせる）</summary>
    public static void SetUnit(UiCounter c, string unit)
    {
        var tx = c.Unit.GetComponentInChildren<TextMeshProUGUI>();
        if (tx == null || tx.text == unit) return;
        tx.text = unit;
        c.Unit.sizeDelta = new Vector2(Mathf.Max(T.TagH, tx.GetPreferredValues(unit).x + T.S4), T.TagH);
        c.Unit.gameObject.SetActive(!string.IsNullOrEmpty(unit));
    }
}
