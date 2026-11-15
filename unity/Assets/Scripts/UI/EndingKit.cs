// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-14、#146 EndingParams は GameParams から）／ 更新：iizuka（Opus 5.5、2026-11-15、#190 あつ森に寄せる：カード・材料の行・見出しの帯・吹き出し）
// エンディング（#85）と事後レポート（#86）が共通で使うもの。
//   EndingTokens  … この2画面だけの寸法（UiTokens の値を組み合わせる。px はここにだけ書く）
//   EndingLayout  … 画面のどこに何を置くか（親に対する割合）。並べ替えはここだけ直す
//   EndingFeed    … サーバーの値のうち GameModel に入らないもの（result の strict_reason、ED-A/B/C・R01 の台詞）を拾う
//   EndingInfo    … 2画面が読む値をまとめる。値の出どころはここ1か所（#147 が入ったら Model.ResultMsg・GameState.Threshold に替える）
//   EndingWidgets … 小さな部品（資源のカード＝UiKit.Card、要件・内訳・出来事の行＝UiKit.Row、見出しの帯＝UiKit.Ribbon＋波の縁、ヒューストンの吹き出し、小さな札）
// 文言とアイコンの割り当ては EndingCopy.cs。UI は判定をしない（結末・要件の合否・スコアはサーバーの値をそのまま出す）。
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;

// ====================================================================== 寸法
public static class EndingTokens
{
    public const float CardW = 360, CardH = 192;   // 資源のカード（【11】の色違いのカード）。状態の1行「Too low (need 12.5°C)」が入る幅
    public const float CardPad = T.S2;             // カードの内側（内側の線 6+2 の内に 8。844×390 で高さが足りるように 24 から詰めた）
    public const float StatusD = T.HintDiscD;      // カードの状態の台（40）
    public const float QuoteCorner = 64;           // ヒューストンの吹き出しの縁（blob_bubble の border 120 を 64 に描く。高さ 120 前後でも上下の縁が収まる）
    public const float QuotePadX = T.S4 + T.S2, QuotePadTop = T.S3, QuotePadBottom = T.S2;   // 吹き出しの内側（台詞 36 の吹き出しは 72・36。高さが 120 しかないので詰めた）
    public const float WaveH = 24;                 // 見出しの帯の下の波（wave_edge の高さ。半分を帯に重ねる）
    public const float PillH = T.HintH;            // 小さな札（結末・判定が厳しくなった理由。56）
    public const float ApolloH = T.PrimaryH;       // アポロ13号のカードの高さ（主ボタンと並ぶ）
    public const float BottomPad = T.S4 * 2f;      // 主ボタンの下（右下の手が画面の外に出ない）
    // レポートのパネルの行（UiKit.Row を詰めて使う。844×390 に 6 行＋見出しを入れるため。ガイドラインの行は 64）
    public const int MaxMoments = 6;               // 出来事の行の数
    public const float RowH = 48, RowMinH = 40;    // 行の高さ（足りなければ 40 まで縮む）
    public const float RowDiscD = T.HintDiscD;     // 行の左の台（40）
    public const float RowGap = 4;                 // 行の間
    public const float RowDashMin = T.S4;          // 点線の最小の長さ（名前と数がくっつかない）
    public const float HeadDiscD = T.HintDiscD;    // パネルの見出しの台（ガイドライン 3章：40）
    public const float PanelPadX = T.S3, PanelPadTop = T.S3, PanelPadBottom = T.S2;   // パネルの内側（ガイドラインは 32。行を入れるために詰めた）
}

// ====================================================================== 置き場所（親に対する割合 x0, y0, x1, y1）
/// <summary>画面のどこに何を置くか。#146 から ending・report の間は HoustonBox を出さない（GameFlow が Push しない）ので、
/// ヒューストンの台詞は EndHouston・RepHouston だけに出る。ほかの物をそこに置かない</summary>
public static class EndingLayout
{
    // エンディング（#85）
    public static readonly Vector4 EndTop = new Vector4(0f, 0.80f, 1f, 1f);          // 見出しの帯＋1行（左）／得点の数のチップ（右）
    public static readonly Vector4 EndMid = new Vector4(0f, 0.42f, 1f, 0.79f);       // 資源のカード＋判定が厳しくなった理由
    public static readonly Vector4 EndHouston = new Vector4(0.15f, 0.215f, 0.85f, 0.395f);
    public static readonly Vector4 EndBottom = new Vector4(0f, 0f, 1f, 0.20f);       // See the report!（右下）

    // 事後レポート（#86）
    public static readonly Vector4 RepTop = new Vector4(0f, 0.89f, 1f, 1f);          // 見出しの帯＋結末の札
    public static readonly Vector4 RepMid = new Vector4(0f, 0.365f, 1f, 0.89f);      // 要件・スコアの内訳・主な出来事の3枚
    public static readonly Vector4 RepHouston = new Vector4(0.12f, 0.205f, 0.88f, 0.345f);
    public static readonly Vector4 RepBottom = new Vector4(0f, 0f, 1f, 0.20f);       // アポロ13号のカード＋合計＋Fly again!
}

// ====================================================================== サーバーの値を拾う
/// <summary>GameModel に入らない値を拾う：result の strict・strict_reason（GameFlow は result の中身だけ持つ）と、
/// ED-A/B/C・R01 の台詞（HoustonBox は台詞を持たない）。StoryDirector と同じく、GameFlow.Client が替わったら付け直す。
/// GameFlow は変えない（キャンバスの下に見えない GameObject を1つ置く）</summary>
public class EndingFeed : MonoBehaviour
{
    public GameFlow flow;
    public bool HasResultMsg { get; private set; }
    public bool Strict { get; private set; }
    public string StrictReason { get; private set; } = "";
    public event Action Changed;

    readonly Dictionary<string, string> lines = new Dictionary<string, string>();
    IGameClient hooked;

    /// <summary>キャンバスに1つだけ作る（EndingScreen と ReportScreen で共有）</summary>
    public static EndingFeed For(Transform canvas, GameFlow f)
    {
        var have = canvas.GetComponentInChildren<EndingFeed>(true);
        if (have != null) return have;
        var go = new GameObject("EndingFeed");
        go.transform.SetParent(canvas, false);
        var feed = go.AddComponent<EndingFeed>();
        feed.flow = f;
        return feed;
    }

    /// <summary>その id（ED-A・R01 など）の台詞。まだ届いていなければ空</summary>
    public string Line(string id) => id != null && lines.TryGetValue(id, out var s) ? s : "";

    void Update()
    {
        var c = flow != null ? flow.Client : null;
        if (c == hooked) return;
        Unhook();
        Clear();
        hooked = c;
        if (c != null) { c.OnMessage += OnMessage; c.OnEvent += OnEvent; c.OnPhase += OnPhase; }
    }

    void OnDestroy() => Unhook();

    void Unhook()
    {
        if (hooked == null) return;
        hooked.OnMessage -= OnMessage; hooked.OnEvent -= OnEvent; hooked.OnPhase -= OnPhase;
        hooked = null;
    }

    void Clear() { HasResultMsg = false; Strict = false; StrictReason = ""; lines.Clear(); }

    void OnPhase(string p) { if (p == "design") Clear(); }   // もう一度（again）で前の結末を消す

    void OnMessage(Dictionary<string, object> m)
    {
        if (J.Str(m, "type") != "result") return;
        HasResultMsg = true;
        Strict = J.Bool(m, "strict");
        StrictReason = J.Str(m, "strict_reason");
        Changed?.Invoke();
    }

    void OnEvent(GameEvent e)
    {
        if (e.kind != "houston" || string.IsNullOrEmpty(e.id) || !EndingCopy.IsEndingLine(e.id)) return;
        lines[e.id] = e.text;
        Changed?.Invoke();
    }
}

/// <summary>parameters.json の値（生還ライン・Req 2 の最小値・厳しくする幅）。GameParams から読む（#146。前は UrgencyFx.parametersJson を借りていた）。無ければ NaN</summary>
public static class EndingParams
{
    public static float Get(string key) => GameParams.TryGet(key, out var v) ? v : float.NaN;
}

/// <summary>2画面が読む値。サーバーの値をそのまま入れる（足し算・判定はしない。例外は Need と Below：下を見よ）</summary>
public class EndingInfo
{
    public string Ending = "";                    // "ED-A" / "ED-B" / "ED-C"
    public float TEnd, Power, Oxygen, Temp, Fuel, Science, Score, Mass, Cost, MassLimit, Budget;
    public string Route = "";
    public bool Strict;
    public string StrictReason = "";              // "" / "missed_burn" / "reentry_not_ready"
    public Dictionary<string, object> Requirements, Breakdown;
    public List<object> Commands;
    public GameState LastState;                   // 最後に届いた state（時刻 t_los などを出来事の行に使う）

    public bool Survived => Ending == "ED-A" || Ending == "ED-B";
    public bool Pass(string req) => J.Bool(Requirements, req);

    /// <summary>エンディング（Model.Result）かレポート（Model.Report）から作る。結末がまだ無ければ null</summary>
    public static EndingInfo From(GameFlow f, EndingFeed feed)
    {
        var m = f.Model;
        var rep = m.Report;
        var r = m.Result ?? J.Obj(rep, "result");
        if (r == null || !J.Has(r, "ending")) return null;
        var i = new EndingInfo
        {
            Ending = J.Str(r, "ending"), TEnd = J.Num(r, "t_end"), Route = J.Str(r, "route"),
            Power = J.Num(r, "power"), Oxygen = J.Num(r, "oxygen"), Temp = J.Num(r, "temp"), Fuel = J.Num(r, "fuel"),
            Science = J.Num(r, "science"), Score = J.Num(r, "score"), Mass = J.Num(r, "mass"), Cost = J.Num(r, "cost"),
            MassLimit = J.Num(m.Totals, "mass_limit", float.NaN), Budget = J.Num(m.Totals, "budget", float.NaN),
            Requirements = J.Obj(r, "requirements"),
            Breakdown = J.Obj(rep, "score_breakdown"), Commands = J.Arr(rep, "commands"),
            LastState = m.State,
        };
        // strict の出どころ：report の外側 → result の外側（EndingFeed）→ 最後の state（理由は分からない）
        if (rep != null && J.Has(rep, "strict")) { i.Strict = J.Bool(rep, "strict"); i.StrictReason = J.Str(rep, "strict_reason"); }
        else if (feed != null && feed.HasResultMsg) { i.Strict = feed.Strict; i.StrictReason = feed.StrictReason; }
        else if (m.State != null) i.Strict = m.State.strict;
        return i;
    }

    public float Value(string res) => res == EndingCopy.Oxygen ? Oxygen : res == EndingCopy.Power ? Power : res == EndingCopy.Temp ? Temp : Fuel;

    /// <summary>着水のときに要る量（生還ライン）。strict なら酸素・電力だけ厳しくする幅を足す（SPEC 8章）。分からなければ NaN。
    /// 表示のための足し算。サーバーが「どれが足りなかったか」を送るようになったら、そちらに替える</summary>
    public float Need(string res)
    {
        float v = EndingParams.Get(EndingCopy.EndMinKey(res));
        if (Strict && res != EndingCopy.Temp) v += EndingParams.Get("end_strict_margin");
        return v;
    }

    /// <summary>生還ラインの下か（ED-B で「どれが足りなかったか」を見せるため）。ラインが分からなければ false</summary>
    public bool Below(string res) { float n = Need(res); return !float.IsNaN(n) && Value(res) < n; }

    public bool RanOut(string res) => Value(res) <= 0f;

    /// <summary>ED-C で何が尽きたか（"power" / "oxygen" / "both" / ""＝燃料などで帰れなかった）</summary>
    public string Cause()
    {
        bool p = RanOut(EndingCopy.Power), o = RanOut(EndingCopy.Oxygen);
        return p && o ? "both" : p ? EndingCopy.Power : o ? EndingCopy.Oxygen : "";
    }

    /// <summary>漏れを止めた時刻（正しいバルブを閉じた最初の cmd）。止めていなければ -1</summary>
    public float LeakStoppedAt()
    {
        if (Commands == null) return -1f;
        foreach (var o in Commands)
            if (o is Dictionary<string, object> c && J.Str(c, "cmd") == "close_valve" && J.Bool(c, "ok")) return J.Num(c, "t");
        return -1f;
    }
}

// ====================================================================== 小さな部品
/// <summary>ヒューストンの吹き出し（blob_bubble、名前タグ）</summary>
public class UiQuote
{
    public RectTransform Root;
    public TextMeshProUGUI Line;
    public void Set(string s) { Line.text = s ?? ""; Root.gameObject.SetActive(!string.IsNullOrEmpty(s)); }
}

/// <summary>見出しの帯（UiKit.Ribbon）に、左のアイコンと下の波の縁（【11】）を足したもの</summary>
public class UiEndBanner
{
    public UiRibbon Ribbon;
    public Image Icon, Wave;

    public void Set(string text, Color face, string icon)
    {
        Ribbon.SetText(text);
        Ribbon.Face.color = face;
        if (Wave != null) Wave.color = face;
        Icon.sprite = icon == null ? null : T.Icon(icon);
        Icon.gameObject.SetActive(Icon.sprite != null);
    }
}

/// <summary>小さな札（blob_pill に台＋文字）。結末の札・判定が厳しくなった理由</summary>
public class UiEndPill
{
    public RectTransform Root;
    public Image Face;
    public UiIconDisc Disc;
    public TextMeshProUGUI Label;
}

/// <summary>資源のカード（UiKit.Card）：上に資源の台と名前、大きい数字＋単位、下に状態の台＋1行</summary>
public class UiResourceCard
{
    public string Res;
    public UiCard Card;
    public RectTransform Root;
    public UiIconDisc Status;
    public TextMeshProUGUI Value, Unit, StatusText;

    /// <summary>statusFace：状態の台の面。iconOnFace：台の上の絵の色（null なら panel）</summary>
    public void Set(string value, string status, string statusIcon, Color statusFace, Color? iconOnFace = null)
    {
        Value.text = value;
        StatusText.text = status;
        Status.Set(statusIcon, statusFace);
        Status.Icon.color = iconOnFace ?? T.Panel;
    }
}

/// <summary>要件の行（UiKit.Row）：台に ✓/✗、名前、点線、右に数（3 / 2 など）。✗ のときだけ一番右に「次はこれ」の台</summary>
public class UiReqRow
{
    public UiRow Row;
    public UiIconDisc Hint;

    public void Set(bool pass, string label, string value, string hintIcon)
    {
        Row.Disc.Set(pass ? "check" : "cross", pass ? T.Go : T.Accent);
        Row.Name.text = label;
        Row.SetValue(value);
        bool hint = !pass && hintIcon != null;
        Row.Right.gameObject.SetActive(hint);
        if (hint) Hint.Set(hintIcon, T.Text);
    }
}

/// <summary>レポートの一覧の行（UiKit.Row）：台＋名前＋点線＋右寄せの数（スコアの内訳・出来事の時刻）</summary>
public class UiLineRow
{
    public UiRow Row;

    public void Set(string icon, Color face, string label, string value, bool dim = false)
    {
        Row.Disc.Set(icon, dim ? T.Line : face);
        Row.Name.text = label ?? "";
        Row.Name.color = dim ? T.TextSoft : T.Text;
        Row.SetValue(value);
        Row.Value.color = dim ? T.TextSoft : T.Text;
        Row.Root.gameObject.SetActive(true);
    }

    public void Hide() => Row.Root.gameObject.SetActive(false);
}

public static class EndingWidgets
{
    /// <summary>親の割合 a（x0, y0, x1, y1）の範囲に入れ物を置く</summary>
    public static RectTransform Zone(Transform p, Vector4 a, string name)
    {
        var r = Ui.Rect(name, p);
        Ui.Anchor(r, a.x, a.y, a.z, a.w);
        return r;
    }

    /// <summary>入れ物いっぱいの縦の並び（左右・上下に余白）。stretch=false なら子は自分の幅のまま a に寄せる</summary>
    public static RectTransform FillCol(RectTransform zone, float spacing, TextAnchor a, float padX, float padTop, float padBottom, bool stretch = true)
    {
        var c = UiKit.Col(zone, spacing, a, padX, 0f, stretch);
        var l = c.GetComponent<VerticalLayoutGroup>();
        l.padding.top = Mathf.RoundToInt(padTop); l.padding.bottom = Mathf.RoundToInt(padBottom);
        UiKit.Fill(c);
        return c;
    }

    /// <summary>入れ物いっぱいの横の並び</summary>
    public static RectTransform FillRow(RectTransform zone, float spacing, TextAnchor a, float padX, float padTop, float padBottom)
    {
        var r = UiKit.Row(zone, spacing, a, padX, 0f);
        var l = r.GetComponent<HorizontalLayoutGroup>();
        l.padding.top = Mathf.RoundToInt(padTop); l.padding.bottom = Mathf.RoundToInt(padBottom);
        UiKit.Fill(r);
        return r;
    }

    /// <summary>折り返す文字（幅は親が決める）</summary>
    public static TextMeshProUGUI Wrap(Transform p, string s, TextStyle st, Color c, TextAlignmentOptions a = TextAlignmentOptions.TopLeft)
    {
        var t = UiKit.Text(p, s, st, c, a, false);
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    public static void Spacer(Transform row) { var le = Ui.Size(Ui.Rect("Flex", row), w: 0f, flexW: 1f); le.minWidth = 0f; }

    /// <summary>見出しの帯（画面の左上。【11】）：UiKit.Ribbon の左にアイコン、下の辺に波の縁（wave_edge を上下に返して半分重ねる）。
    /// 波の sprite が無ければ帯だけ</summary>
    public static UiEndBanner Banner(Transform p, string text, Color face, string icon)
    {
        var b = new UiEndBanner { Ribbon = UiKit.Ribbon(p, text, face) };
        var root = b.Ribbon.Root;
        root.GetComponent<HorizontalLayoutGroup>().spacing = T.S2;
        b.Icon = UiKit.Icon(root, icon, T.IconM, T.Panel);
        b.Icon.transform.SetSiblingIndex(b.Ribbon.Label.transform.GetSiblingIndex());
        var wave = T.Art(T.WaveEdge);
        if (wave != null)
        {
            b.Wave = UiKit.Shape(root, wave, face, 0f, "Wave");
            b.Wave.type = Image.Type.Tiled;
            UiKit.Ignore(b.Wave);
            var w = b.Wave.rectTransform;
            w.anchorMin = new Vector2(0f, 0f); w.anchorMax = new Vector2(1f, 0f); w.pivot = new Vector2(0.5f, 0.5f);
            w.sizeDelta = new Vector2(-T.RibbonH, EndingTokens.WaveH);   // 両端の丸みの内側だけ
            w.anchoredPosition = Vector2.zero;
            w.localScale = new Vector3(1f, -1f, 1f);   // こぶを下に（art/ui/README.md）
            w.SetSiblingIndex(b.Ribbon.Face.transform.GetSiblingIndex() + 1);
        }
        b.Set(text, face, icon);
        return b;
    }

    /// <summary>小さな札：face の blob_pill（高さ 56）に台（disc の面）と文字</summary>
    public static UiEndPill Pill(Transform p, Color face, string icon, Color disc, TextStyle st, Color fg)
    {
        var u = new UiEndPill();
        u.Root = UiKit.Row(p, T.S2, TextAnchor.MiddleLeft, T.S3);
        u.Root.name = "Pill_" + icon;
        var le = Ui.Size(u.Root, h: EndingTokens.PillH); le.minHeight = EndingTokens.PillH; le.flexibleWidth = 0f;
        u.Face = UiKit.Blob(u.Root, T.BlobPill, face, EndingTokens.PillH * 0.5f, "Face"); UiKit.Fill(u.Face.rectTransform); UiKit.Ignore(u.Face);
        u.Disc = UiKit.Disc(u.Root, icon, disc, T.HintDiscD);
        u.Label = UiKit.Text(u.Root, "", st, fg, TextAlignmentOptions.MidlineLeft);
        // 台を左の丸みに寄せる（左の余白を詰める）
        u.Root.GetComponent<HorizontalLayoutGroup>().padding.left = Mathf.RoundToInt((EndingTokens.PillH - T.HintDiscD) * 0.5f);
        return u;
    }

    /// <summary>資源の台：panel の丸に、カードの色の絵。酸素は O2 の文字（ui-guidelines 4章：しずくは燃料と重なる）</summary>
    public static UiIconDisc ResourceDisc(Transform p, string res, Color fg, float d)
    {
        var u = UiKit.Disc(p, EndingCopy.ResourceIcon(res), T.Panel, d);
        u.Icon.color = fg;
        if (res == EndingCopy.Oxygen)
        {
            var t = UiKit.Text(u.Root, EndingCopy.O2Text, TextStyle.Tag, fg, TextAlignmentOptions.Center, false);
            UiKit.Fill(t.rectTransform);
        }
        return u;
    }

    /// <summary>ヒューストンの吹き出し（blob_bubble・panel、影なし。左上に go の名前タグを傾けてかぶせる。【12】）。
    /// 親の割合 a に置く。文は 36 から 22 まで縮めて収める</summary>
    public static UiQuote Quote(Transform p, Vector4 a, string who)
    {
        var q = new UiQuote();
        q.Root = Zone(p, a, "HoustonQuote");
        var face = UiKit.Blob(q.Root, T.BlobBubble, T.Panel, EndingTokens.QuoteCorner, "Face");
        UiKit.Fill(face.rectTransform);
        q.Line = UiKit.Text(q.Root, "", TextStyle.Talk, T.Text, TextAlignmentOptions.MidlineLeft, false);
        q.Line.textWrappingMode = TextWrappingModes.Normal;
        q.Line.enableAutoSizing = true; q.Line.fontSizeMin = T.TextLabel; q.Line.fontSizeMax = T.TextTalk;
        q.Line.lineSpacing = 0f;
        var lr = q.Line.rectTransform; UiKit.Fill(lr);
        lr.offsetMin = new Vector2(EndingTokens.QuotePadX, EndingTokens.QuotePadBottom);
        lr.offsetMax = new Vector2(-EndingTokens.QuotePadX, -EndingTokens.QuotePadTop);

        var tag = UiKit.Row(q.Root, 0f, TextAnchor.MiddleCenter, T.S3);
        tag.name = "NameTag";
        tag.anchorMin = tag.anchorMax = new Vector2(0f, 1f); tag.pivot = new Vector2(0f, 0.5f);
        tag.anchoredPosition = new Vector2(EndingTokens.QuotePadX, 0f);   // 上の縁に半分かぶせる
        tag.localEulerAngles = new Vector3(0f, 0f, T.NameTagTilt);
        var tface = UiKit.Blob(tag, T.BlobPill, T.Go, T.NameTagH * 0.5f, "Face"); UiKit.Fill(tface.rectTransform); UiKit.Ignore(tface);
        UiKit.Text(tag, who, TextStyle.Hint, T.Panel);
        var tle = Ui.Size(tag, h: T.NameTagH); tle.minHeight = T.NameTagH;
        var fit = tag.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        q.Root.gameObject.SetActive(false);
        return q;
    }

    /// <summary>資源のカード（【11】の色違いのカード）：資源ごとの色の面、文字は panel。状態は台（色＋アイコン）と1行</summary>
    public static UiResourceCard ResourceCard(Transform p, string res)
    {
        var c = new UiResourceCard { Res = res };
        var face = EndingCopy.ResourceFace(res);
        c.Card = UiKit.Card(p, face);
        c.Root = c.Card.Root;
        c.Root.name = "Card_" + res;
        UiKit.Fixed(c.Root, EndingTokens.CardW, EndingTokens.CardH);
        int pad = Mathf.RoundToInt(EndingTokens.CardPad);
        c.Root.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(pad, pad, pad, pad);

        var head = UiKit.Row(c.Root, T.S1);
        ResourceDisc(head, res, face, T.HintDiscD);
        UiKit.Text(head, EndingCopy.ResourceName(res), TextStyle.RowName, T.Panel);
        var val = UiKit.Row(c.Root, T.S1, TextAnchor.LowerCenter);
        c.Value = UiKit.Text(val, "", TextStyle.Big, T.Panel);
        c.Unit = UiKit.Text(val, EndingCopy.ResourceUnit(res), TextStyle.Sub, T.Panel);
        var st = UiKit.Row(c.Root, T.S1);
        c.Status = UiKit.Disc(st, "check", T.Go, EndingTokens.StatusD);
        c.StatusText = UiKit.Text(st, "", TextStyle.Hint, T.Panel);
        c.Set("", "", "check", T.Go);
        return c;
    }

    /// <summary>レポートのパネル（UiKit.Panel）。幅は中身の幅＋残りを3枚で分ける（行の名前が切れない。#189 の残り）</summary>
    public static RectTransform ReportPanel(Transform row, string name)
    {
        var p = UiKit.Panel(row, EndingTokens.RowGap, name);
        var l = p.GetComponent<VerticalLayoutGroup>();
        l.padding = new RectOffset(Mathf.RoundToInt(EndingTokens.PanelPadX), Mathf.RoundToInt(EndingTokens.PanelPadX),
                                   Mathf.RoundToInt(EndingTokens.PanelPadTop), Mathf.RoundToInt(EndingTokens.PanelPadBottom));
        Ui.Size(p, flexW: 1f, flexH: 1f);
        return p;
    }

    /// <summary>パネルの見出し：台 40 ＋ 名前 30、下に点線（【11】）</summary>
    public static void PanelHead(Transform panel, string title, string icon, Color disc)
    {
        var head = UiKit.Row(panel, T.S2, TextAnchor.MiddleLeft, T.S1, 0f);
        UiKit.Disc(head, icon, disc, EndingTokens.HeadDiscD);
        UiKit.Text(head, title, TextStyle.RowName, T.Text, TextAlignmentOptions.MidlineLeft);
        UiKit.DashedLine(panel, T.BgDeep);
    }

    /// <summary>レポートの行：UiKit.Row（材料の行）を、3枚を横に並べられるよう詰めて使う（高さ 48・台 40・間 8）。右の欄は隠す</summary>
    public static UiRow ListRow(Transform p)
    {
        var r = UiKit.Row(p, (string)null, "");
        var le = Ui.Size(r.Root, h: EndingTokens.RowH); le.minHeight = EndingTokens.RowMinH;
        var l = r.Root.GetComponent<HorizontalLayoutGroup>();
        l.spacing = T.S1; l.padding.left = l.padding.right = Mathf.RoundToInt(T.S1);
        float d = EndingTokens.RowDiscD;
        r.Disc.Root.sizeDelta = new Vector2(d, d);
        UiKit.Fixed(r.Disc.Root, d, d);
        UiKit.Center(r.Disc.Icon.rectTransform, d * 0.6f, d * 0.6f);
        Ui.Size(r.Dash).minWidth = EndingTokens.RowDashMin;
        r.Right.gameObject.SetActive(false);
        return r;
    }

    /// <summary>要件の行（右の欄に「次はこれ」の台）</summary>
    public static UiReqRow ReqRow(Transform p)
    {
        var u = new UiReqRow { Row = ListRow(p) };
        u.Hint = UiKit.Disc(u.Row.Right, null, T.Text, EndingTokens.RowDiscD);
        return u;
    }

    public static UiLineRow LineRow(Transform p) => new UiLineRow { Row = ListRow(p) };
}
