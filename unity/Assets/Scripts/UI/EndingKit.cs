// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#146 EndingParams は GameParams から）
// エンディング（#85）と事後レポート（#86）が共通で使うもの。
//   EndingTokens  … この2画面だけの寸法（UiTokens の値を組み合わせる。px はここにだけ書く）
//   EndingLayout  … 画面のどこに何を置くか（親に対する割合）。並べ替えはここだけ直す
//   EndingFeed    … サーバーの値のうち GameModel に入らないもの（result の strict_reason、ED-A/B/C・R01 の台詞）を拾う
//   EndingInfo    … 2画面が読む値をまとめる。値の出どころはここ1か所（#147 が入ったら Model.ResultMsg・GameState.Threshold に替える）
//   EndingWidgets … 小さな部品（資源のカード・要件の行・スコアの行・出来事の行・ヒューストンの吹き出し・アイコンの丸）
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
    public const float ChipH = 64;                 // 見出しの横のチップ（結末・スコア）。ui-guidelines のカウントダウンのチップと同じ高さ
    public const float BigChipH = T.PrimaryH;      // エンディングのスコアのチップ（主ボタンと並ぶので同じ高さ）
    public const float HeroDot = T.BadgeL;         // エンディングの見出しの左の丸（96）
    public const float CardW = 320, CardH = 184;   // 資源のカード
    public const float CardPad = T.S2;             // カードの内側（パネルの 24 だと 844×390 で高さが足りない）
    public const float RowIcon = 32;               // 一覧の行のアイコン（補助 24 と標準 40 の間。行の高さを Baloo 32 にそろえる）
    public const float TimeW = 88;                 // 出来事の行の時刻の幅（"10:00" まで）
    public const float HintDot = T.IconM;          // ✗ の要件の横の「次はこれ」の丸（40）
    public const float Ring = T.SelfRing;          // 足りなかったカードの輪の太さ
    public const float QuotePadX = 40, QuotePadTop = 24, QuotePadBottom = 12;  // 吹き出しの内側（ui-guidelines は 28・40。高さが 98 しかないので上下を詰めた）
    public const float TagTilt = -4f;              // 名前タグの傾き（度）
    public const float ApolloH = T.PrimaryH;       // アポロ13号の枠の高さ（主ボタンと並ぶ）
    public const int MaxMoments = 6;               // 出来事の行の数（パネルの高さで決まる）
    public const float ListPadY = T.S2;            // レポートのパネルの上下の内側（844×390 で 6 行が入るよう 24 から詰めた。左右は 24 のまま）
}

// ====================================================================== 置き場所（親に対する割合 x0, y0, x1, y1）
public static class EndingLayout
{
    /// <summary>ヒューストンの吹き出し（画面の中の台詞）。#146 から ending・report の間は HoustonBox を出さない（GameFlow が Push しない）ので、
    /// 台詞はここだけに出る。ほかの物をここに置かない</summary>
    public static readonly Vector4 HoustonSlot = new Vector4(0.22f, 0.22f, 0.78f, 0.34f);

    // エンディング（#85）
    public static readonly Vector4 EndTop = new Vector4(0f, 0.76f, 1f, 1f);      // 結末の見出し
    public static readonly Vector4 EndMid = new Vector4(0f, 0.40f, 1f, 0.75f);   // 資源のカード＋判定が厳しくなった理由
    public static readonly Vector4 EndBottom = new Vector4(0f, 0f, 1f, 0.19f);   // スコア＋REPORT

    // 事後レポート（#86）
    public static readonly Vector4 RepTop = new Vector4(0f, 0.88f, 1f, 1f);      // 題・結末・スコア
    public static readonly Vector4 RepMid = new Vector4(0f, 0.39f, 1f, 0.88f);   // 要件・スコアの内訳・主な出来事の3枚
    public static readonly Vector4 RepBottom = new Vector4(0f, 0f, 1f, 0.20f);   // アポロ13号の枠＋もう一度
    public static readonly float[] RepPanelFlex = { 1.15f, 1f, 1.1f };           // 3枚の幅の比（要件・スコア・出来事）
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
public class UiDot { public RectTransform Root; public Image Face, Icon; public TextMeshProUGUI Text; }

public class UiQuote
{
    public RectTransform Root;
    public TextMeshProUGUI Line;
    public void Set(string s) { Line.text = s ?? ""; Root.gameObject.SetActive(!string.IsNullOrEmpty(s)); }
}

public class UiResourceCard
{
    public string Res;
    public RectTransform Root;
    public Image Ring, StatusIcon;
    public TextMeshProUGUI Value, Unit, Status;

    public void Set(string value, string status, string statusIcon, Color statusColor, Color? ring)
    {
        Value.text = value;
        Status.text = status;
        StatusIcon.sprite = T.Icon(statusIcon); StatusIcon.color = statusColor;
        StatusIcon.gameObject.SetActive(StatusIcon.sprite != null);
        Ring.enabled = ring.HasValue;
        if (ring.HasValue) Ring.color = ring.Value;
    }
}

public class UiReqRow
{
    public RectTransform Root;
    public Image Mark;
    public TextMeshProUGUI Label, Detail;
    public UiDot Hint;

    public void Set(bool pass, string label, string detail, string hintIcon)
    {
        Mark.sprite = T.Icon(pass ? "check" : "cross");
        Mark.color = pass ? T.Go : T.Accent;
        Label.text = label;
        Detail.text = detail ?? "";
        Detail.gameObject.SetActive(!string.IsNullOrEmpty(detail));
        Hint.Root.gameObject.SetActive(!pass && hintIcon != null);
        if (hintIcon != null) Hint.Icon.sprite = T.Icon(hintIcon);
    }
}

public class UiLineRow
{
    public RectTransform Root;
    public Image Icon;
    public TextMeshProUGUI Left, Label, Right;

    public void Set(string left, string icon, string label, string right, bool dim = false)
    {
        if (Left != null) Left.text = left ?? "";
        Icon.sprite = icon == null ? null : T.Icon(icon);
        Icon.color = dim ? T.InkSub : T.Ink;
        Icon.enabled = Icon.sprite != null;
        Label.text = label ?? "";
        Label.color = dim ? T.InkSub : T.Ink;
        if (Right != null) { Right.text = right ?? ""; Right.color = dim ? T.InkSub : T.Ink; }
        Root.gameObject.SetActive(true);
    }
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

    /// <summary>入れ物いっぱいの縦の並び（左右・上下に余白）。stretch=false なら子は自分の幅のまま a に寄せる（チップが横いっぱいに伸びない）</summary>
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

    /// <summary>横の並びで残りの幅を取る文字（長ければ … で切る）</summary>
    public static TextMeshProUGUI Flex(Transform p, string s, TextStyle st, Color c, TextAlignmentOptions a = TextAlignmentOptions.MidlineLeft)
    {
        var t = UiKit.Text(p, s, st, c, a);
        t.overflowMode = TextOverflowModes.Ellipsis;
        var le = Ui.Size(t, w: 0f, flexW: 1f); le.minWidth = 0f;
        return t;
    }

    public static void Spacer(Transform row) { var le = Ui.Size(Ui.Rect("Flex", row), w: 0f, flexW: 1f); le.minWidth = 0f; }

    /// <summary>色の丸にアイコン（または文字）。face の上は fg</summary>
    public static UiDot Dot(Transform p, string icon, Color face, Color fg, float d, float iconSize, string text = null)
    {
        var u = new UiDot();
        u.Root = Ui.Rect("Dot_" + (icon ?? text), p);
        UiKit.Fixed(u.Root, d, d);
        u.Face = UiKit.Pill(u.Root, face, d, "Face"); UiKit.Fill(u.Face.rectTransform);
        if (text != null)
        {
            u.Text = UiKit.Text(u.Root, text, TextStyle.Tag, fg, TextAlignmentOptions.Center, false);
            UiKit.Fill(u.Text.rectTransform);
        }
        else
        {
            u.Icon = UiKit.Icon(u.Root, icon, iconSize, fg);
            UiKit.Center(u.Icon.rectTransform, iconSize, iconSize);
        }
        return u;
    }

    /// <summary>資源のアイコン。酸素は O2 の文字の丸（ui-guidelines 4章：しずくは燃料と重なる）</summary>
    public static void ResourceIcon(Transform p, string res, float size)
    {
        if (res == EndingCopy.Oxygen) Dot(p, null, T.Ink, T.Panel, size, size, EndingCopy.O2Text);
        else UiKit.Icon(p, EndingCopy.ResourceIcon(res), size, T.Ink);
    }

    /// <summary>ヒューストンの吹き出し（panel、名前タグ付き）。親の割合 a に置く。文は最大2〜3行（長いと 22 まで縮める）</summary>
    public static UiQuote Quote(Transform p, Vector4 a, string who)
    {
        var q = new UiQuote();
        q.Root = Zone(p, a, "HoustonQuote");
        UiKit.Surface(q.Root, T.RadiusL, T.Panel);
        q.Line = UiKit.Text(q.Root, "", TextStyle.Body, T.Ink, TextAlignmentOptions.MidlineLeft, false);
        q.Line.textWrappingMode = TextWrappingModes.Normal;
        q.Line.enableAutoSizing = true; q.Line.fontSizeMin = T.TextLabel; q.Line.fontSizeMax = T.TextBody;
        q.Line.lineSpacing = 0f;
        var lr = q.Line.rectTransform; UiKit.Fill(lr);
        lr.offsetMin = new Vector2(EndingTokens.QuotePadX, EndingTokens.QuotePadBottom);
        lr.offsetMax = new Vector2(-EndingTokens.QuotePadX, -EndingTokens.QuotePadTop);

        var tag = UiKit.Tag(q.Root, who, T.Go, T.Panel);
        tag.anchorMin = tag.anchorMax = new Vector2(0f, 1f); tag.pivot = new Vector2(0f, 0.5f);
        tag.anchoredPosition = new Vector2(T.S4, 0f);   // 上の縁に半分かぶせる
        tag.localEulerAngles = new Vector3(0f, 0f, EndingTokens.TagTilt);
        var fit = tag.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        q.Root.gameObject.SetActive(false);
        return q;
    }

    /// <summary>資源のカード：アイコン＋名前／大きい数字＋単位／状態のアイコン＋1行。足りなかったカードは外側に輪</summary>
    public static UiResourceCard ResourceCard(Transform p, string res)
    {
        var c = new UiResourceCard { Res = res };
        c.Root = UiKit.Col(p, T.S1, TextAnchor.MiddleCenter, EndingTokens.CardPad, EndingTokens.CardPad);
        c.Root.name = "Card_" + res;
        UiKit.Fixed(c.Root, EndingTokens.CardW, EndingTokens.CardH);
        c.Ring = UiKit.Round(c.Root, T.RadiusM + EndingTokens.Ring, T.Accent, "Ring");
        UiKit.Fill(c.Ring.rectTransform, -EndingTokens.Ring); UiKit.Ignore(c.Ring);
        UiKit.Surface(c.Root, T.RadiusM, T.Panel);

        var head = UiKit.Row(c.Root, T.S1);
        ResourceIcon(head, res, T.IconM);
        UiKit.Text(head, EndingCopy.ResourceName(res), TextStyle.Label, T.InkSub);
        var val = UiKit.Row(c.Root, T.S1, TextAnchor.LowerCenter);
        c.Value = UiKit.Text(val, "", TextStyle.Big, T.Ink);
        c.Unit = UiKit.Text(val, EndingCopy.ResourceUnit(res), TextStyle.Sub, T.InkSub);
        var st = UiKit.Row(c.Root, T.S1);
        c.StatusIcon = UiKit.Icon(st, "check", T.IconS, T.Go);
        c.Status = UiKit.Text(st, "", TextStyle.Label, T.Ink);
        c.Set("", "", "check", T.Go, null);
        return c;
    }

    /// <summary>パネル（角丸 40）の見出しの行と点線。right は右端の文字（無ければ null）</summary>
    public static TextMeshProUGUI PanelHead(Transform panel, string title, string icon, bool right = false)
    {
        var head = UiKit.Row(panel, T.S2, TextAnchor.MiddleLeft);
        if (icon != null) UiKit.Icon(head, icon, T.IconM, T.Ink);
        Flex(head, title, TextStyle.Sub, T.Ink);
        var r = right ? UiKit.Text(head, "", TextStyle.Sub, T.Ink, TextAlignmentOptions.MidlineRight) : null;
        UiKit.DashedLine(panel, T.Line);
        UiKit.Space(panel, T.S1);
        return r;
    }

    /// <summary>要件の行：✓/✗＋名前（＋細かい数字）＋ ✗ のときだけ「次はこれ」のアイコンの丸</summary>
    public static UiReqRow ReqRow(Transform p)
    {
        var u = new UiReqRow();
        u.Root = UiKit.Row(p, T.S2, TextAnchor.MiddleLeft);
        u.Mark = UiKit.Icon(u.Root, "check", T.IconM, T.Go);
        var col = UiKit.Col(u.Root, 0f, TextAnchor.MiddleLeft, 0f, 0f, true);
        var le = Ui.Size(col, w: 0f, flexW: 1f); le.minWidth = 0f;
        u.Label = UiKit.Text(col, "", TextStyle.Body, T.Ink, TextAlignmentOptions.MidlineLeft);
        u.Label.overflowMode = TextOverflowModes.Ellipsis;
        u.Detail = UiKit.Text(col, "", TextStyle.Label, T.InkSub, TextAlignmentOptions.MidlineLeft);
        u.Hint = Dot(u.Root, "science", T.Accent, T.Ink, EndingTokens.HintDot, T.IconS);
        return u;
    }

    /// <summary>一覧の行：［左の文字］アイコン 名前 ［右の数字］。左右は null なら作らない</summary>
    public static UiLineRow LineRow(Transform p, bool left, bool right)
    {
        var u = new UiLineRow();
        u.Root = UiKit.Row(p, T.S2, TextAnchor.MiddleLeft);
        if (left)
        {
            u.Left = UiKit.Text(u.Root, "", TextStyle.Sub, T.Ink, TextAlignmentOptions.MidlineRight);
            UiKit.Fixed(u.Left, EndingTokens.TimeW, T.TextSub * T.HeadingLineHeight);
        }
        u.Icon = UiKit.Icon(u.Root, null, EndingTokens.RowIcon, T.Ink);
        u.Label = Flex(u.Root, "", TextStyle.Body, T.Ink);
        if (right) u.Right = UiKit.Text(u.Root, "", TextStyle.Sub, T.Ink, TextAlignmentOptions.MidlineRight);
        return u;
    }

    /// <summary>色の面のチップ：アイコン＋小さな名前＋文字。返す Text を書き換える</summary>
    public static TextMeshProUGUI Chip(Transform p, Color face, Color fg, string icon, string label, TextStyle valueStyle, float h, out RectTransform root, out Image iconImg)
    {
        root = UiKit.Chip(p, face, h, "Chip_" + (label ?? icon));
        iconImg = UiKit.Icon(root, icon, T.IconM, fg);
        if (label != null) UiKit.Text(root, label, TextStyle.Label, fg);
        return UiKit.Text(root, "", valueStyle, fg);
    }
}
