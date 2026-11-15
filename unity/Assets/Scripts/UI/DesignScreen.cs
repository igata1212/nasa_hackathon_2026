// 書いた人：Claude ／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-14、#73 UI ガイドラインの見た目にし、足りない理由が分かるようにした）／ 更新：iizuka（Opus 5.5、2026-11-14、#146 parameters.json は GameParams から）／ 更新：iizuka（Opus 5.5、2026-11-15、#168 3D の舞台・右の棚・お財布・打ち上げチェック・説明は吹き出し。#190 あつ森の作り）
// Design（M01・M02）：部品の ON/OFF・±、ロケット・軌道、打ち上げ判定。#93 の決定：手描きの「素材の画面」はこの画面、「使う／使わない」は部品の ON/OFF・±。
// 割り付け（1600×900。#168、tmp/handoff/168-plan.md）：
//   全面＝3D の舞台（DesignStage。昼の発射場で回る台に載った宇宙船。部品は船に「ぽん」と付く。FLIGHT ではカメラが引いてロケットの全身）
//   右の列＝数のチップ（お財布・重さ）→ タブの帯 → 棚（材料の行 最大4本。Flight は選択肢の吹き出し2つ）→ Ready for launch!。その左上＝打ち上げチェック。
//   左上＝見出しの帯。左下＝ヒューストンの吹き出し。指さしの手は1つ（最後に触った行・選んだ選択肢）。#190（ui-guidelines 1〜3章）の作り
//   説明の文は画面に常に出さず、ヒューストンの吹き出しで言う（タブを開いたとき・部品が付いたとき。超えた瞬間はサーバーの H01・H02）
// UI は計算・判定をしない：合計・残り・上限・判定はサーバーの design メッセージの totals（budget_left・mass_left・over_*・science_possible）、
//   打ち上げの結果は launch イベント（GameFlow が Model に入れる）。部品が船に付く動きも、サーバーの design が届いてから（4人とも同じ動き）。
// 部品の質量・費用・個数の範囲は Data/Sim/parts.json、説明の数字は parameters.json（GameParams。#146）。文言は DesignCopy、2D の大きさは DesignTokens、3D は DesignLook
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

    public DesignValues(TextAsset partsJson)
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
        if (GameParams.Loaded) foreach (var kv in GameParams.All) fixedVals[kv.Key] = kv.Value;   // parameters.json（GameFlow.Awake が GameParams に読む。#146）
        else Debug.LogWarning("[DesignScreen] parameters.json が読めていない（GameParams）。説明の数字は - になる");
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
    readonly DesignStage stage;
    readonly List<DesignRow> rows = new List<DesignRow>();
    readonly Dictionary<string, RectTransform> pages = new Dictionary<string, RectTransform>();
    DesignTabBand tabBand;
    DesignChoiceBubble rocketPick, pathPick;
    DesignCounter wallet, weight;
    DesignCheckRow checkMass, checkCost, checkCrew, checkScience;
    DesignHand hand;
    string tab = "", lastTouched = "", lastChoice = "rocket";
    // 前の design（変わった所だけ吹き出しで言う）
    readonly Dictionary<string, int> prevParts = new Dictionary<string, int>();
    string prevTraj, prevRocket, lastSaid;
    bool prevOverMass, prevOverCost, hadDesign, sciencePossible = true;
    GameObject noGo;
    TextMeshProUGUI noGoReason, noGoDetail, noGoHint;
    bool noGoClosed;

    public DesignScreen(Transform parent, GameFlow f)
    {
        this.f = f;
        values = new DesignValues(f.partsJson);

        // 全面：3D の舞台（DesignStage が RenderTexture に描く。できるまでは空の色）。舞台をなぞると台が回る
        var view = Ui.Rect("Design", parent).gameObject.AddComponent<RawImage>();
        Root = view.gameObject; Ui.Fill(view.rectTransform);
        view.color = DesignLook.Sky; view.raycastTarget = true;

        var safe = UiKit.SafeArea(view.transform);
        BuildTitle(safe);
        BuildTray(safe);
        BuildCheck(safe);
        var handLayer = Ui.Rect("HandLayer", safe); Ui.Fill(handLayer);   // 手はいちばん上（1画面に1つ）
        hand = DesignHand.Make(handLayer);
        BuildNoGo(view.transform);

        stage = Root.AddComponent<DesignStage>();
        stage.Init(view);
        SelectTab(DesignCopy.Tabs[0].Key, false);
    }

    void Send(Dictionary<string, object> msg) => f.Client?.Send(msg);

    // ================================================================== 左上：見出しの帯（【11】）
    void BuildTitle(RectTransform safe)
    {
        var r = DesignRibbon.Make(safe, DesignCopy.Title, D.Plum);
        r.anchoredPosition = new Vector2(0f, -T.ScreenEdge);
    }

    // ================================================================== 右の列：数のチップ → タブの帯 → 棚 → 打ち上げ
    void BuildTray(RectTransform safe)
    {
        var col = UiKit.Col(safe, T.S1, TextAnchor.UpperCenter, 0f, 0f, true);
        col.name = "Tray";
        col.anchorMin = new Vector2(1f, 0f); col.anchorMax = Vector2.one; col.pivot = new Vector2(1f, 0.5f);
        col.offsetMin = new Vector2(-(T.ScreenEdge + D.RightW), T.ScreenEdge);
        col.offsetMax = new Vector2(-T.ScreenEdge, -(T.ScreenEdge * 0.5f));
        col.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(0, 0, Mathf.RoundToInt(D.TrayTop), 0);

        var counters = UiKit.Row(col, T.S2, TextAnchor.MiddleCenter);
        var cle = Ui.Size(counters, h: D.CounterH); cle.minHeight = D.CounterH;
        wallet = DesignCounter.Make(counters, "cost", T.Accent, null);
        weight = DesignCounter.Make(counters, "mass", D.Plum, () => SelectTab(DesignCopy.FlightTab, true));   // 重すぎの直し方＝大きいロケット

        tabBand = DesignTabBand.Make(col, DesignCopy.Tabs, key => SelectTab(key, true));

        // 棚は中身の高さ（行が2本のタブでは短くなり、後ろの舞台が見える）。画面が低いと行が RowStepMinH まで縮む
        var shelf = DesignUi.Panel(col, T.Panel, false, D.RowGap, D.ShelfPad, "Shelf");
        foreach (var t in DesignCopy.Tabs)
        {
            var page = UiKit.Col(shelf, D.RowGap, TextAnchor.UpperCenter, 0f, 0f, true);
            page.name = "Page_" + t.Key;
            pages[t.Key] = page;
            if (t.Key == DesignCopy.FlightTab) BuildFlight(page);
            else for (int i = 0; i < t.Parts.Length; i++) AddRow(page, t.Parts[i], D.PlatformColors[i % D.PlatformColors.Length]);
        }

        UiKit.Space(col, 0f, true);
        UiKit.PrimaryButton(col, DesignCopy.Launch, "rocket", Launch);
    }

    void AddRow(Transform page, string id, Color platform)
    {
        DesignPartCopy copy = null;
        foreach (var c in DesignCopy.Parts) if (c.Id == id) copy = c;
        var p = values.Part(id);
        if (copy == null || p == null) { Debug.LogWarning("[DesignScreen] 部品が parts.json か DesignCopy に無い: " + id); return; }
        var row = DesignRow.Make(page, copy, platform,
            values.Fill("{mass." + id + "}"), values.Fill("{cost." + id + "}"), copy.Science == null ? null : values.Fill(copy.Science),
            J.Int(p, "min"), J.Int(p, "max"),
            d => { Touch(id); Send(Cmd.DesignPart(id, d)); },
            n => { Touch(id); Send(Cmd.DesignPartSet(id, n)); });
        rows.Add(row);
    }

    void BuildFlight(Transform page)
    {
        rocketPick = DesignChoiceBubble.Make(page, DesignCopy.Rockets, values.Fill, k => { lastChoice = "rocket"; Send(Cmd.DesignRocket(k)); UpdateHand(); });
        pathPick = DesignChoiceBubble.Make(page, DesignCopy.Paths, values.Fill, k => { lastChoice = "path"; Send(Cmd.DesignTrajectory(k)); UpdateHand(); });
    }

    void Touch(string id) { lastTouched = id; UpdateHand(); }

    void SelectTab(string key, bool say)
    {
        if (key == tab) { if (say) SayTab(key); return; }
        tab = key;
        tabBand.Select(key);
        foreach (var kv in pages) kv.Value.gameObject.SetActive(kv.Key == key);
        if (stage != null) stage.Wide(key == DesignCopy.FlightTab);
        if (say) SayTab(key);
        UpdateHand();
    }

    /// <summary>手は1つ：Flight では最後に触った選択肢の選んだ行、ほかのタブでは最後に触った行（まだなら1本目）</summary>
    void UpdateHand()
    {
        if (hand == null) return;
        if (tab == DesignCopy.FlightTab)
        {
            var b = lastChoice == "path" ? pathPick : rocketPick;
            hand.Point(b != null ? b.Select(b.Selected) : null);
            return;
        }
        DesignRow first = null, touched = null;
        foreach (var r in rows)
        {
            if (!r.Root.IsChildOf(pages[tab])) continue;
            if (first == null) first = r;
            if (r.Id == lastTouched) touched = r;
        }
        var at = touched ?? first;
        hand.Point(at != null ? at.Root : null);
    }

    void SayTab(string key)
    {
        if (key == DesignCopy.FlightTab && !sciencePossible) { Say("design_no_science", DesignCopy.NoScience, null); return; }   // 打ち上げを押す前に
        foreach (var t in DesignCopy.Tabs) if (t.Key == key) Say("design_tab_" + key, values.Fill(t.Say), null);
    }

    // ================================================================== 打ち上げチェック（右の列のすぐ左・上。パネル【05】）
    void BuildCheck(RectTransform safe)
    {
        var card = DesignUi.Panel(safe, T.Panel, true, 0f, T.S2 * 0.5f, "LaunchCheck");
        card.anchorMin = card.anchorMax = card.pivot = Vector2.one;
        card.anchoredPosition = new Vector2(-(T.ScreenEdge + D.RightW + D.ColGap), -T.ScreenEdge);
        card.sizeDelta = new Vector2(D.CheckW, 0f);
        card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var head = UiKit.Row(card, T.S1, TextAnchor.MiddleLeft);
        Ui.Size(head, h: D.CheckRowH);
        DesignUi.Platform(head, "rocket", D.CheckTitleIcon, T.Go, out _, out _);
        DesignUi.Words(head, DesignCopy.CheckTitle, D.TextRow, D.Text);
        DesignUi.Dots(card, D.BgDeep, false);
        checkMass = DesignCheckRow.Make(card, DesignCopy.CheckMass.Text);
        checkCost = DesignCheckRow.Make(card, DesignCopy.CheckCost.Text);
        DesignUi.Words(card, DesignCopy.InFlightTitle, D.TextTag, D.TextSub, TextAlignmentOptions.MidlineLeft, D.RowPriceH);
        checkCrew = DesignCheckRow.Make(card, DesignCopy.CheckCrew.Text);
        checkScience = DesignCheckRow.Make(card, values.Fill(DesignCopy.CheckScience.Text));
        foreach (var r in new[] { checkMass, checkCost, checkCrew, checkScience }) r.Set(DesignCheckRow.Pending, false);
    }

    void Launch()
    {
        noGoClosed = false;   // 同じ理由でもう一度押したら、結果をもう一度出す
        Send(Cmd.Type("launch"));
    }

    // ================================================================== 打ち上げ判定の結果（No go）。M02 でだめだったときだけ重ねる
    void BuildNoGo(Transform parent)
    {
        var scrim = Ui.PanelImg(parent, T.Scrim, "NoGo"); Ui.Fill(scrim.rectTransform);
        noGo = scrim.gameObject;
        var close = scrim.gameObject.AddComponent<Button>();
        close.transition = Selectable.Transition.None;
        close.onClick.AddListener(CloseNoGo);

        var card = DesignUi.Panel(scrim.transform, T.Panel, true, T.S2, T.S3, "NoGoCard");
        UiKit.Center(card, D.OverlayW, 0f);
        card.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
        card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var top = UiKit.Row(card, 0f);
        DesignUi.Platform(top, "danger", D.OverlayIconD, T.Danger, out _, out _);
        DesignUi.Words(card, DesignCopy.NoGo, D.TextRibbon, T.Danger, TextAlignmentOptions.Center);
        noGoReason = DesignUi.Words(card, "", D.TextRow, D.Text, TextAlignmentOptions.Center);
        noGoDetail = DesignUi.Words(card, "", T.TextBody, D.Text, TextAlignmentOptions.Center, wrap: true);
        noGoHint = DesignUi.Words(card, "", T.TextBody, D.TextSub, TextAlignmentOptions.Center, wrap: true);
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
        bool animate = hadDesign && Root.activeInHierarchy && !stage.Fresh;   // 画面を開いた直後の1回は、数え上げ・吹き出し・弾みなしで置く

        var partsObj = m.Design != null ? J.Obj(m.Design, "parts") : null;
        var parts = new Dictionary<string, int>();
        if (partsObj != null) foreach (var kv in partsObj) parts[kv.Key] = J.Int(partsObj, kv.Key);
        foreach (var r in rows) r.Set(parts.TryGetValue(r.Id, out var n) ? n : 0);
        rocketPick.Select(rocket);
        pathPick.Select(traj);

        // 判定はサーバーのものだけ。古い記録で over_* が無ければ status から（status は質量を先に見る）
        string status = J.Str(tt, "status");
        bool overMass = Over(tt, "over_mass", status), overCost = Over(tt, "over_budget", status);
        bool massOk = J.Has(tt, "over_mass") ? !overMass : status == "ok" || status == "over_budget";
        bool costOk = J.Has(tt, "over_budget") ? !overCost : status == "ok";
        sciencePossible = !J.Has(tt, "science_possible") || J.Bool(tt, "science_possible");

        wallet.Set(J.Has(tt, "budget_left") ? J.Num(tt, "budget_left") : (float?)null, overCost, values.Fill(DesignCopy.CostFallback), animate);
        weight.Set(J.Has(tt, "mass_left") ? J.Num(tt, "mass_left") : (float?)null, overMass, values.Fill(DesignCopy.MassFallback), animate);
        checkMass.Set(tt == null ? DesignCheckRow.Pending : overMass ? DesignCheckRow.Bad : massOk ? DesignCheckRow.Ok : DesignCheckRow.Pending, animate);
        checkCost.Set(tt == null ? DesignCheckRow.Pending : overCost ? DesignCheckRow.Bad : costOk ? DesignCheckRow.Ok : DesignCheckRow.Pending, animate);
        checkScience.SetText(values.Fill(DesignCopy.CheckScience.Text));
        checkScience.Set(sciencePossible ? DesignCheckRow.Pending : DesignCheckRow.Warn, animate);

        stage.Set(parts, rocket, overMass);
        if (m.Design != null)
        {
            if (animate) SayChanges(parts, traj, rocket, overMass, overCost);
            prevParts.Clear(); foreach (var kv in parts) prevParts[kv.Key] = kv.Value;
            prevTraj = traj; prevRocket = rocket; prevOverMass = overMass; prevOverCost = overCost;
            hadDesign = Root.activeInHierarchy;
        }
        UpdateHand();
        SetNoGo(m.LaunchStatus, rocket);
    }

    /// <summary>前の design から変わった所を1つだけ吹き出しで言う（部品が付いた ＞ 軌道を替えた）。
    /// 超えた瞬間はサーバーが H01・H02（声つき、priority high）を送るので、ここでは言わない（同じ時に出すと上書きし合う）</summary>
    void SayChanges(Dictionary<string, int> parts, string traj, string rocket, bool overMass, bool overCost)
    {
        if (overMass && !prevOverMass || overCost && !prevOverCost) return;
        foreach (var kv in DesignCopy.PartSay)
        {
            int now = parts.TryGetValue(kv.Key, out var a) ? a : 0, before = prevParts.TryGetValue(kv.Key, out var b) ? b : 0;
            if (now > before) { Say("design_part_" + kv.Key, values.Fill(kv.Value), null); return; }
        }
        if (prevTraj != null && traj != prevTraj) Say("design_path_" + traj, values.Fill(ChoiceLine(DesignCopy.Paths, traj)), null);
    }

    /// <summary>ヒューストンの吹き出しへ。同じ文を続けて出さない</summary>
    void Say(string id, string text, string priority)
    {
        if (string.IsNullOrEmpty(text) || text == lastSaid || HoustonBox.Current == null) return;
        lastSaid = text;
        HoustonBox.Current.Push(id, text, priority);
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

    /// <summary>launch イベントでだめだったとき（GameFlow が Model.LaunchStatus に入れる。次の design メッセージで "" に戻る）。船もぶるっと揺れる</summary>
    void SetNoGo(string launchStatus, string rocket)
    {
        bool bad = launchStatus == "over_mass" || launchStatus == "over_budget";
        if (!bad) { noGoClosed = false; noGo.SetActive(false); return; }
        if (noGoClosed) return;
        bool mass = launchStatus == "over_mass";
        noGoReason.text = values.Fill(mass ? DesignCopy.NoGoMass : DesignCopy.NoGoBudget);
        noGoDetail.text = values.Fill(mass ? DesignCopy.NoGoMassDetail : DesignCopy.NoGoBudgetDetail);
        noGoHint.text = DesignCopy.Hint(mass ? DesignCopy.MassHint : DesignCopy.BudgetHint, rocket);
        if (!noGo.activeSelf) stage.Shake();
        noGo.SetActive(true);
        noGo.transform.SetAsLastSibling();
    }
}
