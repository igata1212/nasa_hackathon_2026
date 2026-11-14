// 書いた人：Claude ／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#73 UI ガイドラインの見た目にし、足りない理由が分かるようにした）
// Design（M01・M02）：部品の ± と合計、ロケット・軌道、打ち上げ判定。#93 の決定：手描きの「素材の画面」はこの画面、「使う／使わない」は部品の ±。
// 割り付け（1600×900）：左＝見出し・4つの要件・部品のカード 2列×4段（左下はヒューストンの吹き出しのために空ける）／右＝重さと費用のゲージ・ロケット・
//   はじめの電力・酸素・燃料・軌道・判定・READY FOR LAUNCH。入りきらない画面（iPhone の横）では、部品と右の列の中身が縦にずれる。
// UI は計算・判定をしない：合計・上限・判定（status）はサーバーの design メッセージの totals、打ち上げの結果は launch イベント（GameFlow が Model に入れる）。
// 部品の質量・費用・個数の範囲は Data/Sim/parts.json、説明の数字は parameters.json（UrgencyFx が持つものを借りる）。文言は DesignCopy、大きさは DesignTokens
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;
using D = DesignTokens;

/// <summary>文言の {key} に入れる値：parameters.json・parts.json（読むだけ）と、サーバーの totals</summary>
public class DesignValues
{
    public readonly List<Dictionary<string, object>> Parts = new List<Dictionary<string, object>>();
    readonly Dictionary<string, float> fixedVals = new Dictionary<string, float>();
    readonly HashSet<string> warned = new HashSet<string>();
    public Dictionary<string, object> Totals;
    public string Rocket = "";
    static readonly Regex Key = new Regex(@"\{([A-Za-z0-9_.]+)\}");

    public DesignValues(TextAsset partsJson, TextAsset paramsJson)
    {
        if (partsJson != null)
        {
            var cat = MiniJson.ParseObject(partsJson.text);
            foreach (var o in J.Arr(cat, "parts") ?? new List<object>())
            {
                if (!(o is Dictionary<string, object> p)) continue;
                Parts.Add(p);
                string id = J.Str(p, "id");
                if (J.Has(p, "mass")) fixedVals["mass." + id] = J.Num(p, "mass");
                if (J.Has(p, "cost")) fixedVals["cost." + id] = J.Num(p, "cost");
            }
            var lim = J.Obj(cat, "limits");
            if (lim != null) foreach (var kv in lim) if (kv.Value is double d) fixedVals[kv.Key] = (float)d;
        }
        else Debug.LogWarning("[DesignScreen] parts.json が割り当てられていない（GameFlow.partsJson）");
        if (paramsJson != null)
        {
            foreach (var o in J.Arr(MiniJson.ParseObject(paramsJson.text), "params") ?? new List<object>())
                if (o is Dictionary<string, object> d && J.Has(d, "key")) fixedVals[J.Str(d, "key")] = J.Num(d, "value");
        }
        else Debug.LogWarning("[DesignScreen] parameters.json が見つからない（UrgencyFx.parametersJson を借りている）。説明の数字は - になる");
    }

    public Dictionary<string, object> Part(string id)
    {
        foreach (var p in Parts) if (J.Str(p, "id") == id) return p;
        return null;
    }

    /// <summary>"{mass} / {mass_limit}" → "111 / 140"。サーバーの値が先、無ければ JSON の値。無いものは "-"</summary>
    public string Fill(string template)
    {
        if (string.IsNullOrEmpty(template)) return "";
        return Key.Replace(template, m =>
        {
            string k = m.Groups[1].Value;
            if (k == "rocket") return Rocket;
            if (J.Has(Totals, k)) return Fmt(J.Num(Totals, k));
            if (fixedVals.TryGetValue(k, out var v)) return Fmt(v);
            if (Totals != null && warned.Add(k)) Debug.LogWarning("[DesignScreen] 文言の {" + k + "} に入れる値が無い");
            return "-";
        });
    }

    public static string Fmt(float v) =>
        Mathf.Approximately(v, Mathf.Round(v)) ? Mathf.RoundToInt(v).ToString(CultureInfo.InvariantCulture) : v.ToString("0.##", CultureInfo.InvariantCulture);
}

public class DesignScreen
{
    public readonly GameObject Root;
    readonly GameFlow f;
    readonly DesignValues values;
    readonly List<DesignPartCard> cards = new List<DesignPartCard>();
    readonly List<DesignChoice> rockets = new List<DesignChoice>(), paths = new List<DesignChoice>();
    readonly List<(DesignStat stat, string text)> supplies = new List<(DesignStat stat, string text)>();
    readonly DesignGoalChip[] goals = new DesignGoalChip[DesignCopy.Goals.Length];
    DesignGauge massGauge, costGauge;
    TextMeshProUGUI massValue, costValue, pathLine;
    Image verdictFace, verdictIcon;
    TextMeshProUGUI verdictTitle, verdictHint;
    GameObject noGo;
    TextMeshProUGUI noGoReason, noGoDetail, noGoHint;
    bool noGoClosed;

    public DesignScreen(Transform parent, GameFlow f)
    {
        this.f = f;
        values = new DesignValues(f.partsJson, FindParams());

        var bg = Ui.PanelImg(parent, T.Bg, "Design"); Root = bg.gameObject; Ui.Fill(bg.rectTransform);
        var safe = UiKit.SafeArea(bg.transform);
        BuildLeft(safe);
        BuildRight(safe);
        BuildNoGo(bg.transform);
    }

    /// <summary>parameters.json は GameFlow に無いので、同じシーンの UrgencyFx が持つものを借りる（シーンと GameFlow を変えないため）。
    /// GameParams（P0-a）ができたらそちらに替える</summary>
    static TextAsset FindParams()
    {
        var u = Object.FindAnyObjectByType<UrgencyFx>(FindObjectsInactive.Include);
        return u != null ? u.parametersJson : null;
    }

    void Send(Dictionary<string, object> msg) => f.Client?.Send(msg);

    // ================================================================== 左：見出し・要件・部品
    void BuildLeft(RectTransform safe)
    {
        var left = UiKit.Col(safe, T.S1, TextAnchor.UpperLeft, 0f, 0f, true);
        left.name = "Left";
        left.anchorMin = new Vector2(0f, D.HoustonReserve); left.anchorMax = Vector2.one;
        left.offsetMin = new Vector2(T.ScreenEdge, T.S2);
        left.offsetMax = new Vector2(-(T.ScreenEdge + D.RightW + D.ColGap), -T.ScreenEdge);

        var title = UiKit.Row(left, T.S3, TextAnchor.MiddleLeft);
        Ui.Size(title, h: D.TitleRowH);
        UiKit.Text(title, DesignCopy.Title, TextStyle.Heading, T.Ink, TextAlignmentOptions.MidlineLeft);
        DesignUi.Text(title, DesignCopy.Subtitle, TextStyle.Body, T.InkSub, TextAlignmentOptions.MidlineLeft, wrap: true);

        var goalRow = UiKit.Row(left, T.S1, TextAnchor.MiddleLeft);
        Ui.Size(goalRow, h: D.GoalChipH);
        UiKit.Text(goalRow, DesignCopy.GoalsLabel, TextStyle.Tag, T.InkSub);
        for (int i = 0; i < goals.Length; i++)
            goals[i] = DesignGoalChip.Make(goalRow, DesignCopy.Goals[i], values.Fill(DesignCopy.Goals[i].Text));

        UiKit.Space(left, T.S1);
        var note = UiKit.Row(left, T.S2, TextAnchor.MiddleLeft);
        Ui.Size(note, h: D.PartLineH);
        DesignUi.Text(note, DesignCopy.PartsHint, TextStyle.Label, T.Ink, ellipsis: true);
        UiKit.Text(note, DesignCopy.BodyNote, TextStyle.Label, T.InkSub, TextAlignmentOptions.MidlineRight);
        DesignStat.Make(note, "mass", values.Fill("{mass.bus}"), TextStyle.Label, T.IconS, T.InkSub);
        DesignStat.Make(note, "cost", values.Fill("{cost.bus}"), TextStyle.Label, T.IconS, T.InkSub);

        var list = DesignUi.Scroll(left, D.PartGapY, "Parts");
        RectTransform row = null;
        foreach (var copy in PartOrder())
        {
            var p = values.Part(copy.Id);
            if (row == null || row.childCount >= D.PartColumns)
            {
                row = UiKit.Row(list, D.PartGapX, TextAnchor.MiddleLeft);
                Ui.Size(row, h: D.PartRowH);
            }
            string id = copy.Id;
            var card = DesignPartCard.Make(row, copy, values.Fill(copy.Line),
                values.Fill("{mass." + id + "}"), values.Fill("{cost." + id + "}"), copy.Science == null ? null : values.Fill(copy.Science),
                J.Int(p, "min"), J.Int(p, "max"), d => Send(Cmd.DesignPart(id, d)));
            cards.Add(card);
        }
        if (row != null) while (row.childCount < D.PartColumns) Ui.Size(UiKit.Space(row, 0f), w: 0f, flexW: 1f);   // 奇数のとき右を空ける
    }

    /// <summary>DesignCopy.Parts の順。parts.json にあって DesignCopy に無い部品は、カタログの名前と benefit で後ろに足す</summary>
    IEnumerable<DesignPartCopy> PartOrder()
    {
        var known = new HashSet<string>();
        foreach (var c in DesignCopy.Parts)
        {
            known.Add(c.Id);
            if (values.Part(c.Id) != null) yield return c;
        }
        foreach (var p in values.Parts)
        {
            string id = J.Str(p, "id");
            if (known.Contains(id) || id == "bus" || id.StartsWith("trajectory_") || id.StartsWith("rocket_")) continue;
            yield return new DesignPartCopy(id, J.Str(p, "name"), "create", J.Str(p, "benefit"));
        }
    }

    // ================================================================== 右：重さ・費用・ロケット・はじめの量・軌道・判定・打ち上げ
    void BuildRight(RectTransform safe)
    {
        var right = UiKit.Panel(safe, D.SectionGap, "Check");
        right.anchorMin = new Vector2(1f, 0f); right.anchorMax = Vector2.one; right.pivot = new Vector2(1f, 0.5f);
        right.offsetMin = new Vector2(-(T.ScreenEdge + D.RightW), T.ScreenEdge);
        right.offsetMax = new Vector2(-T.ScreenEdge, -T.ScreenEdge);

        var body = DesignUi.Scroll(right, D.SectionGap, "Body");

        var gauges = UiKit.Row(body, T.S3, TextAnchor.UpperLeft);
        massGauge = GaugeBlock(gauges, "mass", DesignCopy.MassLabel, out massValue);
        costGauge = GaugeBlock(gauges, "cost", DesignCopy.CostLabel, out costValue);

        var rr = UiKit.Row(Section(body, "rocket", DesignCopy.RocketLabel), T.S2, TextAnchor.MiddleCenter);
        foreach (var c in DesignCopy.Rockets) { string k = c.Key; rockets.Add(DesignChoice.Make(rr, c, values.Fill, () => Send(Cmd.DesignRocket(k)))); }

        var sr = UiKit.Row(Section(body, null, DesignCopy.SuppliesLabel), T.S1, TextAnchor.MiddleCenter);
        foreach (var s in DesignCopy.Supplies)
        {
            var chip = UiKit.Row(sr, 0f, TextAnchor.MiddleCenter, T.S1);
            var le = Ui.Size(chip, w: 0f, h: D.SupplyChipH, flexW: 1f); le.minHeight = D.SupplyChipH;
            var face = UiKit.Pill(chip, T.Bg, D.SupplyChipH, "Face"); UiKit.Fill(face.rectTransform); UiKit.Ignore(face);
            supplies.Add((DesignStat.Make(chip, s.Icon, "", TextStyle.Body, T.IconM, T.Ink), s.Text));
        }

        var path = Section(body, "route", DesignCopy.PathLabel);
        var pr = UiKit.Row(path, T.S2, TextAnchor.MiddleCenter);
        foreach (var c in DesignCopy.Paths) { string k = c.Key; paths.Add(DesignChoice.Make(pr, c, values.Fill, () => Send(Cmd.DesignTrajectory(k)))); }
        pathLine = DesignUi.Text(path, "", TextStyle.Label, T.InkSub, TextAlignmentOptions.MidlineLeft, D.PartLineH, ellipsis: true);

        // 判定（いまの設計で打ち上げられるか。サーバーの totals.status）
        var verdict = UiKit.Row(right, T.S2, TextAnchor.MiddleLeft, T.S2, T.S1);
        verdict.name = "Verdict";
        var vle = Ui.Size(verdict, h: D.VerdictMinH); vle.minHeight = D.VerdictMinH;
        verdictFace = UiKit.Round(verdict, T.RadiusM, D.VerdictOkFace, "Face"); UiKit.Fill(verdictFace.rectTransform); UiKit.Ignore(verdictFace);
        verdictIcon = UiKit.Icon(verdict, "check", T.IconM, T.Go);
        var vcol = UiKit.Col(verdict, 0f, TextAnchor.MiddleLeft, 0f, 0f, true);
        var vcle = Ui.Size(vcol, w: 0f, flexW: 1f); vcle.minWidth = 0f;
        verdictTitle = UiKit.Text(vcol, "", TextStyle.Sub, T.Go, TextAlignmentOptions.MidlineLeft);
        verdictHint = DesignUi.Text(vcol, "", TextStyle.Label, T.Ink, TextAlignmentOptions.TopLeft, wrap: true);

        UiKit.PrimaryButton(right, DesignCopy.Launch, "rocket", Launch);
    }

    DesignGauge GaugeBlock(Transform p, string icon, string label, out TextMeshProUGUI value)
    {
        var col = UiKit.Col(p, 0f, TextAnchor.UpperLeft, 0f, 0f, true);
        var le = Ui.Size(col, w: 0f, flexW: 1f); le.minWidth = 0f;
        SectionLabel(col, icon, label);
        value = UiKit.Text(col, "-", TextStyle.Sub, T.Ink, TextAlignmentOptions.MidlineLeft);
        return DesignGauge.Make(col);
    }

    static void SectionLabel(Transform p, string icon, string text)
    {
        var r = UiKit.Row(p, T.S1, TextAnchor.MiddleLeft);
        Ui.Size(r, h: D.PartLineH);
        if (icon != null) UiKit.Icon(r, icon, T.IconS, T.InkSub);
        UiKit.Text(r, text, TextStyle.Label, T.InkSub, TextAlignmentOptions.MidlineLeft);
    }

    /// <summary>見出し（アイコン＋補助ラベル）と中身のまとまり。中身は返す Col に足す（見出しとの間は空けない）</summary>
    static RectTransform Section(Transform p, string icon, string text)
    {
        var col = UiKit.Col(p, 0f, TextAnchor.UpperLeft, 0f, 0f, true);
        SectionLabel(col, icon, text);
        return col;
    }

    void Launch()
    {
        noGoClosed = false;   // 同じ理由でもう一度押したら、結果をもう一度出す
        Send(Cmd.Type("launch"));
    }

    // ================================================================== 打ち上げ判定の結果（NO GO）。M02 でだめだったときだけ重ねる
    void BuildNoGo(Transform parent)
    {
        var scrim = Ui.PanelImg(parent, T.Scrim, "NoGo"); Ui.Fill(scrim.rectTransform);
        noGo = scrim.gameObject;
        var close = scrim.gameObject.AddComponent<Button>();
        close.transition = Selectable.Transition.None;
        close.onClick.AddListener(CloseNoGo);

        var card = UiKit.Panel(scrim.transform, T.S2, "NoGoCard");
        UiKit.Center(card, D.OverlayW, 0f);
        card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var top = UiKit.Row(card, 0f);
        var badge = UiKit.Pill(top, T.Danger, D.OverlayIconD, "Badge");
        UiKit.Fixed(badge, D.OverlayIconD, D.OverlayIconD);
        var icon = UiKit.Icon(badge.transform, "danger", T.IconL, T.Panel);
        UiKit.Center(icon.rectTransform, T.IconL, T.IconL);
        UiKit.Text(card, DesignCopy.NoGo, TextStyle.Heading, T.Danger, TextAlignmentOptions.Center);
        noGoReason = UiKit.Text(card, "", TextStyle.Sub, T.Ink, TextAlignmentOptions.Center);
        noGoDetail = DesignUi.Text(card, "", TextStyle.Body, T.Ink, TextAlignmentOptions.Center, wrap: true);
        noGoHint = DesignUi.Text(card, "", TextStyle.Body, T.InkSub, TextAlignmentOptions.Center, wrap: true);
        UiKit.Space(card, T.S1);
        var bottom = UiKit.Row(card, 0f);
        UiKit.PrimaryButton(bottom, DesignCopy.NoGoBack, "settings", CloseNoGo);
        noGo.SetActive(false);
    }

    void CloseNoGo() { noGoClosed = true; noGo.SetActive(false); }

    // ================================================================== 表示の更新（design メッセージ・launch イベント・段階が design になったとき）
    public void Refresh()
    {
        var m = f.Model;
        var tt = m.Totals;
        values.Totals = tt;
        string rocket = m.Design != null ? J.Str(m.Design, "rocket") : "";
        string traj = m.Design != null ? J.Str(m.Design, "trajectory") : "";
        values.Rocket = ChoiceName(DesignCopy.Rockets, rocket);

        var parts = m.Design != null ? J.Obj(m.Design, "parts") : null;
        foreach (var c in cards) c.Set(J.Int(parts, c.Id));
        foreach (var c in rockets) c.SetSelected(c.Key == rocket);
        foreach (var c in paths) c.SetSelected(c.Key == traj);
        pathLine.text = ChoiceLine(DesignCopy.Paths, traj);

        // 判定はサーバーのものだけ。status は質量を先に見るので、両方オーバーでも over_mass だけが来る（totals に over_mass・over_budget の真偽が来れば両方赤くなる）
        string status = J.Str(tt, "status");
        bool overMass = Over(tt, "over_mass", status), overCost = Over(tt, "over_budget", status);
        bool massOk = J.Has(tt, "over_mass") ? !overMass : status == "ok" || status == "over_budget";
        bool costOk = J.Has(tt, "over_budget") ? !overCost : status == "ok";

        massValue.text = values.Fill(DesignCopy.MassValue);
        massValue.color = overMass ? T.Danger : T.Ink;
        massGauge.Set(J.Num(tt, "mass"), J.Num(tt, "mass_limit"), overMass);
        costValue.text = values.Fill(DesignCopy.CostValue);
        costValue.color = overCost ? T.Danger : T.Ink;
        costGauge.Set(J.Num(tt, "cost"), J.Num(tt, "budget"), overCost);
        foreach (var (stat, text) in supplies) stat.Text.text = values.Fill(text);

        goals[2].Set(overMass ? -1 : massOk ? 1 : 0);
        goals[3].Set(overCost ? -1 : costOk ? 1 : 0);

        SetVerdict(tt == null ? "" : status, rocket);
        SetNoGo(m.LaunchStatus, rocket);
    }

    static bool Over(Dictionary<string, object> tt, string flag, string status) => J.Has(tt, flag) ? J.Bool(tt, flag) : status == flag;

    static string ChoiceName(DesignChoiceCopy[] list, string key)
    {
        foreach (var c in list) if (c.Key == key) return c.Name;
        return key;
    }

    static string ChoiceLine(DesignChoiceCopy[] list, string key)
    {
        foreach (var c in list) if (c.Key == key) return c.Line ?? "";
        return "";
    }

    void SetVerdict(string status, string rocket)
    {
        bool ok = status == "ok";
        bool known = ok || status == "over_mass" || status == "over_budget";
        verdictFace.color = ok || !known ? D.VerdictOkFace : D.VerdictBadFace;
        verdictIcon.sprite = T.Icon(ok ? "check" : "danger");
        verdictIcon.color = ok ? T.Go : T.Danger;
        verdictIcon.gameObject.SetActive(known);
        verdictTitle.color = ok ? T.Go : T.Danger;
        verdictTitle.text = ok ? DesignCopy.OkTitle : status == "over_mass" ? DesignCopy.MassTitle : status == "over_budget" ? DesignCopy.BudgetTitle : "";
        verdictHint.text = ok ? DesignCopy.OkHint
            : status == "over_mass" ? DesignCopy.Hint(DesignCopy.MassHint, rocket)
            : status == "over_budget" ? DesignCopy.Hint(DesignCopy.BudgetHint, rocket) : "";
    }

    /// <summary>launch イベントでだめだったとき（GameFlow が Model.LaunchStatus に入れる。次の design メッセージで "" に戻る）</summary>
    void SetNoGo(string launchStatus, string rocket)
    {
        bool bad = launchStatus == "over_mass" || launchStatus == "over_budget";
        if (!bad) { noGoClosed = false; noGo.SetActive(false); return; }
        if (noGoClosed) return;
        bool mass = launchStatus == "over_mass";
        noGoReason.text = values.Fill(mass ? DesignCopy.NoGoMass : DesignCopy.NoGoBudget);
        noGoDetail.text = values.Fill(mass ? DesignCopy.NoGoMassDetail : DesignCopy.NoGoBudgetDetail);
        noGoHint.text = DesignCopy.Hint(mass ? DesignCopy.MassHint : DesignCopy.BudgetHint, rocket);
        noGo.SetActive(true);
        noGo.transform.SetAsLastSibling();
    }
}
