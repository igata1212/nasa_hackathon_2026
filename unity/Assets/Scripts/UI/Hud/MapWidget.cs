// 書いた人：Claude ／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で CabinHud から分けた）／ 更新：iizuka（Opus 5.5、2026-11-14、#78）／ 更新：iizuka（Opus 5.5、2026-11-15、#190 あつ森に寄せる）
// 作る Issue：#78（軌道の地図：GMAT の帰還ルート A / B と現在地）
//
// 右上の小さい地図（押すと真ん中に大きく開く。もう一度押すと閉じる。表示だけでコマンドは送らない）。
// 線は GMAT の出力そのまま（GameFlow.trajectory＝TrajectoryView の routeA／routeB＝GmatRoute を読むだけ）を 2D の UGUI で描く：
//   上から見た地球・月の回転座標（EarthMoonRot の X が右、Y が上）。地球と月は見える大きさに太らせ、近くの線は球の外へ押し出す（TrajectoryView と同じ考え）
//   往路（噴射まで A・B 共通）は白、帰還ルート A は go、B は accent。選ぶ前は2本とも点線、選んだら選んだほうを太い実線・もう一方を薄い点線
//   月の裏側（通信が届かない所）を月の右の薄い帯で、噴射する所を炎の印で、船のいまの位置をロケットの印で
// 船の位置：GMAT の時間（h）とゲームの t の対応は time_map.json の区切り（accident・los・aos・burn・reentry）。
//   time_map.json の t は古い時刻のまま（120・300・360・420/510。SPEC 4章は 90・210・240〜280・300/360）なので、区切りの t は state の値に置き換える。
//   点火の前は噴射する所で待ち、点火したら（burn_done を初めて見た t から）そのルートを進む
// 下の比較：A・B それぞれの「家まで何分」（state の t_splashdown_a/b − t、#148）と噴射に使う燃料（parameters.json の fuel_burn_a／b）
// ルートのデータが無ければ、前のように TrajectoryView を描いた RenderTexture を出す（それも無ければ一言）
// Detail：小さい地図の全部（照明 OFF で暗くしてよい）。大きい地図は Detail の明るさをまねる
// #212：小さい地図は右上の丸いボタン（小さい軌道の絵だけ）。押すとボタンの位置から 0.2 秒でふくらんで、下の帯までの全画面（暗い幕＋パネル）。右上の ×・幕を押すと縮んで閉じる。
//   中は 左にメーター（GmatGauges）・まん中に軌道・下に A/B。同時押し（press）・警報（warning・M05）が来たら自動で閉じる。開いている間は小さい地図と下の盆を隠す（CabinHud.SetMapOpen）
// #190（ui-guidelines 1章「船内の計器は濃紺のまま」【06】【01】【07】【09】【11】）：小さい地図は HudFace のまま blob_panel＋星の模様（TimelineKit.GaugeFace）、影なし。
//   A・B の家までの時間は数のチップ（blob_pill。【09】）、地図の上の A・B の札・噴射の印・船は blob_disc の台。大きい地図は明るいパネル（UiKit.PanelFace）、見出しは帯（Ribbon）、
//   閉じるはヒントのピル、比べるのは色違いのカード（UiKit.Card。【11】。選んだ方は 1.12 倍＋チェック、決まったら他方を line）。
//   ルート B の色は accent（注意の色）をやめ、Planner のカード（ui-guidelines 3章：A＝go、B＝plum）にそろえて plum
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;

public class MapWidget : HudPart
{
    // ------------------------------------------------------------------ 文言（Fable の採点で直すときはここだけ。{0} は 分:秒 か数）
    static class Words
    {
        public const string BigTitle = "Two ways home";
        public const string Close = "Close";
        public const string SourceShort = "NASA GMAT";
        public const string Picker = "Mission Planner picks the route";
        public const string Earth = "Earth", Moon = "Moon", NoRadio = "Far side (no radio)", BurnHere = "Burn here";
        public const string HomeIn = "home in {0}";
        public const string HomeUnknown = "";
        public const string Faster = "Faster", Slower = "Slower";
        public const string Fuel = "burn uses {0} fuel";
        public const string Chosen = "Chosen";
        public const string NoMap = "Map not loaded";
    }

    // ------------------------------------------------------------------ 見た目（UiTokens に無いもの。#71 の UiKitHud／UiTokens ができたらそちらへ）
    static Color RouteColor(string r) => r == "a" ? T.Go : T.Plum;     // 席の4色は使わない（ui-guidelines 2章）。#190：B は plum（Planner のカードと同じ。accent は注意の色）
    static Color RouteFg(string r) => T.Panel;                         // ルートの色の上の文字（go・plum の上は白）
    static readonly Color OutColor = T.WithAlpha(T.Panel, 0.85f);      // 往路（A・B 共通）
    static readonly Color Haze = T.WithAlpha(T.Panel, 0.14f);          // 月の裏側の帯
    static readonly Color HazeOn = T.WithAlpha(T.Panel, 0.32f);        // 月の裏側にいる間
    static readonly Color Caption = T.WithAlpha(T.Panel, 0.7f);        // 地図の上の小さい文字
    const float FaintAlpha = 0.35f;     // 選ばなかったルート
    const float SmallStep = 7f, BigStep = 12f;     // 線を区切る長さ（点線の1つぶん）
    const float SmallLine = 3f, BigLine = 5f;      // 線の太さ
    const float ThickMul = 1.8f;                   // 選んだルートの太さ（倍）
    const float EarthFrac = 0.11f, MoonFrac = 0.07f, MinBody = 8f;   // 地球・月の半径（地図の高さに対する割合）
    const float WarpMul = 2.5f;         // 太らせた半径のこの倍までは、線を球の外へ押し出す
    const float FarLen = 2.6f;          // 月の裏側の帯の長さ（月の半径の倍）
    const float SmallShip = 24f, BigShip = 40f;
    const float BigTag = 40f;      // A・B の札（大きい地図だけ。#212）
    const float SmallMark = 22f, BigMark = 34f;    // 噴射の印
    const float SmallW = 136f, SmallH = 104f;      // #212：右上の小さい地図のボタン（押せる範囲 96 以上）
    const float GrowSec = 0.2f, GrowFrom = 0.12f;   // #212：ボタンからふくらむ時間と、はじめの大きさ
    const float ScrimAlpha = 0.8f;                 // #212：後ろの暗い幕
    const float BodyInset = 8f;                     // #212：全画面のパネルと領域の端のすき間
    // 大きく開いたときの置き場所（土台 CabinHud が無いときだけ使う。ふだんは CabinHud.Layout の MapBig）
    static readonly Vector4 BigAnchors = new Vector4(0f, 0f, 1f, 1f);
    static readonly Vector4 PanelBox = new Vector4(0.05f, 0.07f, 0.95f, 0.93f);   // #212：地図のパネル（画面全体に対する割合）
    const float BigHeaderH = T.Hit, BigCompareH = 96f, BigFooterH = 28f;   // #190：見出しは帯（80）、比べるカードは文字2行が入る高さ

    // ------------------------------------------------------------------
    GmatRoute routeA, routeB;
    float hBurn;
    bool hasRoutes;
    MapView small, bigView;
    RectTransform big, body, gaugeColumn, btnRect, closeRect;
    CanvasGroup bigGroup, smallGroup;
    Image scrim;
    float grow;                 // #212：0＝閉じている、1＝開いている（0.2 秒で動く）
    Vector2 growFrom;           // #212：ふくらみはじめる点（小さい地図の中心。パネルの中心からのずれ）
    bool bigOpen;
    float burnSeenT = -1f, lastT = -1f;
    Compare bigA, bigB;

    protected override void Build(RectTransform root)
    {
        RootAsDetail();
        var tv = Ctx.Flow.trajectory;
        routeA = tv != null ? tv.routeA : null;
        routeB = tv != null ? tv.routeB : null;
        hasRoutes = Valid(routeA) && Valid(routeB);
        if (hasRoutes) hBurn = KnotH(routeA, "burn", Mathf.Lerp(routeA.hours[0], routeA.hours[routeA.hours.Length - 1], 0.5f));

        // 小さい地図：右上の丸いボタン（小さい軌道の絵だけ。#212）
        btnRect = Ui.Rect("MapButton", root);
        btnRect.anchorMin = btnRect.anchorMax = btnRect.pivot = new Vector2(1f, 1f);
        btnRect.sizeDelta = new Vector2(SmallW, SmallH); btnRect.anchoredPosition = Vector2.zero;
        smallGroup = btnRect.gameObject.AddComponent<CanvasGroup>();
        var face = TimelineKit.GaugeFace(btnRect, T.RadiusL);   // 計器は暗い面（#190：blob_panel＋星の模様。色は HudFace のまま）
        face.raycastTarget = true;
        var btn = btnRect.gameObject.AddComponent<Button>();
        btn.targetGraphic = face; btn.transition = Selectable.Transition.None;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        btn.onClick.AddListener(Toggle);

        var area = Ui.Rect("Map", btnRect);
        Ui.Anchor(area, 0, 0, 1, 1, T.S1, T.S1, T.S1, T.S1);
        UiKit.Fill(UiKit.Round(area, T.RadiusS, T.Clear, "Space").rectTransform);   // #190：宇宙は面（HudFace）と星の模様をそのまま見せる（0番は地図の線の下の目印）
        if (hasRoutes) small = new MapView(area, false);
        else Fallback(area);
        RefreshCompare(null);
    }

    static bool Valid(GmatRoute r) => r != null && r.posKm != null && r.posKm.Length > 1 && r.hours != null && r.hours.Length == r.posKm.Length && r.knots != null && r.knots.Length > 1;

    static float KnotH(GmatRoute r, string label, float fallback)
    {
        foreach (var k in r.knots) if (k.label == label) return k.h;
        return fallback;
    }

    void Fallback(RectTransform area)
    {
        if (Ctx.TrajectoryRt != null)
        {
            var img = Ui.Rect("Rt", area); UiKit.Fill(img, T.S1);
            img.gameObject.AddComponent<RawImage>().texture = Ctx.TrajectoryRt;
        }
        else
        {
            var t = UiKit.Text(area, Words.NoMap, TextStyle.Label, Caption); UiKit.Fill(t.rectTransform);
        }
    }

    static TextMeshProUGUI FreeText(Transform p, string s, TextStyle st, Color c, Vector2 corner, Vector2 pos)
    {
        var t = UiKit.Text(p, s, st, c, TextAlignmentOptions.Center, false);
        var r = t.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = corner;
        r.sizeDelta = new Vector2(t.GetPreferredValues(s).x + 4f, UiKit.Size(st) * T.BodyLineHeight);
        r.anchoredPosition = pos;
        return t;
    }

    // ------------------------------------------------------------------ 大きい地図
    void Toggle()
    {
        if (!hasRoutes) return;
        SetBig(!bigOpen);
    }

    /// <summary>外から閉じる（同時押し・警報が来たとき。#212）</summary>
    public void CloseBig() => SetBig(false);

    float openT; string openPhase;
    static bool Crossed(float from, float now, float at) => at > 0f && from < at && now >= at;

    void SetBig(bool open)
    {
        if (open == bigOpen) return;
        if (open && big == null) BuildBig();
        bigOpen = open;
        if (open) { openT = Ctx.State != null ? Ctx.State.t : 0f; openPhase = Ctx.Model.Phase; }
        Ctx.Hud?.SetMapOpen(open);   // 下の盆を隠す・出す
        if (big == null) return;
        if (open)
        {
            big.gameObject.SetActive(true); big.SetAsLastSibling();
            // ふくらみはじめる点＝小さい地図の中心（パネルの中心からのずれ）
            body.localScale = Vector3.one; body.anchoredPosition = Vector2.zero;
            Vector3 wc = btnRect.TransformPoint(btnRect.rect.center);
            growFrom = body.InverseTransformPoint(wc);
            bigView.EnsureBuilt(routeA, routeB, hBurn); ApplyViews(Ctx.State); RefreshCompare(Ctx.State);
        }
    }

    void AnimateBig()
    {
        float target = bigOpen ? 1f : 0f;
        if (grow != target) grow = Mathf.MoveTowards(grow, target, Time.unscaledDeltaTime / GrowSec);
        if (big != null)
        {
            float e = UiEase.OutCubic(grow);
            float sc = Mathf.Lerp(GrowFrom, 1f, e);
            body.localScale = new Vector3(sc, sc, 1f);
            body.anchoredPosition = growFrom * (1f - sc);
            var c = scrim.color; c.a = ScrimAlpha * grow; scrim.color = c;
            if (!bigOpen && grow <= 0f && big.gameObject.activeSelf) big.gameObject.SetActive(false);
        }
        // 開いている間は小さい地図のボタンを隠す（縮みきったら戻す）
        bool hide = bigOpen || grow > 0f;
        float a = hide ? 0f : 1f;
        if (smallGroup != null && !Mathf.Approximately(smallGroup.alpha, a)) { smallGroup.alpha = a; smallGroup.blocksRaycasts = !hide; }
    }

    void BuildBig()
    {
        // #212（ユーザー：「地図もっとでかく。デカくっていうか他のものいらんと思うよ。」）：開いたら地図だけ。画面いっぱいの暗い幕の真ん中に、角丸の大きな地図1枚と、その右上の丸い ×
        var slot = Ctx.Hud != null ? Ctx.Hud.Slot(HudSlot.MapBig) : null;
        if (slot != null) { big = Ui.Rect("MapBig", slot); Ui.Fill(big); }
        else
        {
            var cabin = Root.parent != null && Root.parent.parent != null ? Root.parent.parent : Root;
            big = Ui.Rect("MapBig", cabin);
            Ui.Anchor(big, BigAnchors.x, BigAnchors.y, BigAnchors.z, BigAnchors.w);
        }
        // 上の帯・席の盆・Houston の吹き出しより手前に描く（自前の Canvas。60 の接続表示・61 の札より下）
        var cv = big.gameObject.AddComponent<Canvas>(); cv.overrideSorting = true; cv.sortingOrder = 40;
        big.gameObject.AddComponent<GraphicRaycaster>();
        bigGroup = big.gameObject.AddComponent<CanvasGroup>();
        // 暗い幕：押すと閉じる
        scrim = big.gameObject.AddComponent<Image>(); scrim.color = T.WithAlpha(T.Ink, 0f); scrim.raycastTarget = true;
        var sbtn = big.gameObject.AddComponent<Button>();
        sbtn.targetGraphic = scrim; sbtn.transition = Selectable.Transition.None;
        sbtn.navigation = new Navigation { mode = Navigation.Mode.None };
        sbtn.onClick.AddListener(() => SetBig(false));

        // 地図のパネル（ボタンの位置からふくらむ）。押しても閉じない（幕と × だけ）
        body = Ui.Rect("Body", big); Ui.Anchor(body, PanelBox.x, PanelBox.y, PanelBox.z, PanelBox.w);
        var space = UiKit.Round(body, T.RadiusL, T.Ink, "Space"); UiKit.Fill(space.rectTransform);
        space.raycastTarget = true;
        HudStars.Add(space.rectTransform);   // #190：宇宙に小さな星の模様（線の下）
        bigView = new MapView(body, true);

        // 右上の丸い ×（押せる範囲 96。パネルの中）
        var close = UiKit.Disc(body, "cross", T.Text, T.Hit);
        var cr = close.Root; cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f); cr.anchoredPosition = new Vector2(-T.S2, -T.S2);
        UiKit.Ignore(cr);
        close.Face.raycastTarget = true;
        var cb = cr.gameObject.AddComponent<Button>();
        cb.targetGraphic = close.Face; cb.transition = Selectable.Transition.None;
        cb.navigation = new Navigation { mode = Navigation.Mode.None };
        cb.onClick.AddListener(() => SetBig(false));
        closeRect = cr;

        big.gameObject.SetActive(false);
    }

    static void TopBand(RectTransform r, float pad, float h)
    {
        r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 1f);
        r.sizeDelta = new Vector2(-pad * 2f, h); r.anchoredPosition = new Vector2(0f, -pad);
    }

    // ------------------------------------------------------------------ 受け取る
    public override void Refresh(GameState s)
    {
        if (bigOpen && !Ctx.ShowPlannerView) SetBig(false);
        // #212：開いている間に、点火の窓が開いた・帰還の突入が始まった・段階が変わったら自動で閉じる（state を見て決める）
        if (bigOpen && s != null)
        {
            if (Crossed(openT, s.t, s.tBurnOpen) || Crossed(openT, s.t, s.RouteReentry(s.route ?? "")) || Ctx.Model.Phase != openPhase) SetBig(false);
        }   // Planner の席が無い人には大きい地図も出さない（#200）
        if (s != null)
        {
            if (s.t < lastT || !s.burnDone) burnSeenT = -1f;          // 新しいゲーム・まだ点火していない
            if (s.burnDone && burnSeenT < 0f) burnSeenT = s.t;        // 点火を初めて見た t（ここから帰り道を進む）
            lastT = s.t;
        }
        else { burnSeenT = -1f; lastT = -1f; }
        ApplyViews(s);
        RefreshCompare(s);
    }

    public override void OnEvent(GameEvent e)
    {
        // #212：同時押しの合図（press）・警報（warning）・Houston の新しい台詞（houston）が来たら自動で閉じる（点火・警報を見逃さない）
        if (bigOpen && (e.kind == "press" || e.kind == "warning" || e.kind == "houston")) SetBig(false);
    }

    public override void Tick(float now)
    {
        if (!hasRoutes) return;
        AnimateBig();
        bool built = small.EnsureBuilt(routeA, routeB, hBurn);
        if (bigOpen && bigView.EnsureBuilt(routeA, routeB, hBurn)) built = true;
        if (built) ApplyViews(Ctx.State);
        if (bigGroup != null && Detail != null) bigGroup.alpha = Detail.alpha;   // 照明 OFF の暗さ（#87）を大きい地図にも
    }

    /// <summary>今の船の軌道（A／B。噴射の前は A）と、その上の GMAT の経過時間（時間）。地図と同じ値（#167：GmatGauges が読む）。軌道が無ければ null</summary>
    public bool BigOpen => bigOpen;            // #210：GMAT のメーター（GmatGauges）が、大きい地図の開閉について出入りする
    public RectTransform BigRect => big;
    public RectTransform GaugeColumn => gaugeColumn;   // #212：メーターの列。いまは作らない（null）ので GmatGauges は出ない
    public GmatRoute ShipRoute { get; private set; }
    public float ShipH { get; private set; }

    void ApplyViews(GameState s)
    {
        if (!hasRoutes) return;
        string route = s != null ? (s.route ?? "") : "";
        var r = route == "b" ? routeB : routeA;   // 噴射の前は A・B が同じ道
        float h = ShipHours(r, s);
        ShipRoute = r; ShipH = h;   // #167：GMAT のメーター（GmatGauges）が同じ位置を読む
        bool far = s != null && s.inFarside;
        small.Apply(route, far, r, h);
        if (bigOpen) bigView.Apply(route, far, r, h);
    }

    /// <summary>ゲームの t → GMAT の経過時間（時間）。time_map.json の区切りの t を state の時刻に置き換えて、区切りの間は線形</summary>
    float ShipHours(GmatRoute r, GameState s)
    {
        var k = r.knots;
        if (s == null) return k[0].h;
        float prevT = float.NegativeInfinity;
        var ts = new float[k.Length];
        bool usable = true;
        for (int i = 0; i < k.Length; i++)
        {
            ts[i] = KnotT(k[i], s, r.routeId);
            if (ts[i] <= prevT) usable = false;
            prevT = ts[i];
        }
        float h;
        if (!usable) h = r.HoursAt(s.t);   // サーバーの時刻が足りない（古いサーバー）ときは time_map.json のまま
        else if (s.t <= ts[0]) h = k[0].h;
        else
        {
            h = k[k.Length - 1].h;
            for (int i = 1; i < k.Length; i++)
                if (s.t <= ts[i]) { h = Mathf.Lerp(k[i - 1].h, k[i].h, Mathf.InverseLerp(ts[i - 1], ts[i], s.t)); break; }
        }
        if (!s.burnDone) h = Mathf.Min(h, hBurn);   // 点火の前は噴射する所で待つ
        return h;
    }

    float KnotT(GmatKnot n, GameState s, string id)
    {
        switch (n.label)
        {
            case "accident": return 0f;
            case "los": return s.tLos;
            case "aos": return s.tAos;
            case "burn": return burnSeenT >= 0f ? burnSeenT : s.tBurnOpen;
            case "reentry": { float v = s.RouteReentry(id); return v > 0f ? v : s.tReentry; }
            default: return n.t;
        }
    }

    // ------------------------------------------------------------------ 比較（A・B）
    void RefreshCompare(GameState s)
    {
        string route = s != null ? (s.route ?? "") : "";
        // 速い・遅い：state の着水の時刻（無ければ parameters.json の帰りの分）をくらべるだけ
        float ha = s != null && s.tSplashdownA > 0f ? s.tSplashdownA : GameParams.Get("time_p4_return_a_min");
        float hb = s != null && s.tSplashdownB > 0f ? s.tSplashdownB : GameParams.Get("time_p4_return_b_min");
        string adjA = ha <= hb ? Words.Faster : Words.Slower, adjB = ha <= hb ? Words.Slower : Words.Faster;
        string homeA = Home(s, "a"), homeB = Home(s, "b");
        if (bigA != null)
        {
            bigA.Set(route, homeA, adjA, string.Format(Words.Fuel, Mathf.RoundToInt(GameParams.Get("fuel_burn_a"))));
            bigB.Set(route, homeB, adjB, string.Format(Words.Fuel, Mathf.RoundToInt(GameParams.Get("fuel_burn_b"))));
        }
    }

    static string Home(GameState s, string r, string format = Words.HomeIn)
    {
        if (s == null) return Words.HomeUnknown;
        float at = s.RouteSplashdown(r);
        return at > 0f ? string.Format(format, Ui.Mmss(Mathf.Max(0f, at - s.t))) : Words.HomeUnknown;
    }

    /// <summary>A・B の札と「家まで何分」。大きい地図の色違いのカード（#190【11】：速い・遅い、燃料、選んだ方は 1.12 倍＋チェック）</summary>
    class Compare
    {
        string id;
        TextMeshProUGUI main, sub;
        UiCard card;          // 大きい地図
        string shownRoute;

        public static Compare MakeBig(RectTransform parent, string id, float x0, float x1)
        {
            var c = new Compare { id = id };
            c.card = UiKit.Card(parent, RouteColor(id));
            c.card.Face.raycastTarget = false;   // 押すと大きい地図ごと閉じる（カードで止めない）
            var r = c.card.Root;
            r.name = "Card_" + id;
            Ui.Anchor(r, x0, 0, x1, 1, x0 > 0f ? T.S1 : 0f, 0, x1 < 1f ? T.S1 : 0f, 0);
            UiKit.Ignore(r);
            var vl = r.GetComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset((int)T.S3, (int)T.S3, (int)T.S2, (int)T.S2);
            var row = UiKit.Row(c.card.Content, T.S2, TextAnchor.MiddleLeft);
            var letter = UiKit.Text(row, id.ToUpper(), TextStyle.Number, T.Panel);   // A・B の大きな字（【11】のカードの中の大きな絵の代わり）
            UiKit.Fixed(letter, T.TextNumber, T.TextNumber * T.HeadingLineHeight);
            var col = UiKit.Col(row, 0f, TextAnchor.MiddleLeft);
            Ui.Size(col, flexW: 1);
            c.main = UiKit.Text(col, "", TextStyle.Choice, T.Panel, TextAlignmentOptions.MidlineLeft);
            c.sub = UiKit.Text(col, "", TextStyle.Label, T.WithAlpha(T.Panel, 0.85f), TextAlignmentOptions.MidlineLeft);
            return c;
        }

        public void Set(string route, string home, string adj, string fuel)
        {
            bool none = route != "a" && route != "b";
            bool on = none || route == id;
            main.text = adj + (string.IsNullOrEmpty(home) ? "" : " · " + home);
            sub.text = fuel;
            if (route != shownRoute)   // 選んだ・決まったが変わったときだけ（弾みを毎回やり直さない）
            {
                shownRoute = route;
                card.SetPicked(route == id);
                card.SetMuted(!on);
            }
        }
    }

    // ================================================================== 地図1枚（小さい・大きい）
    /// <summary>GMAT の線を UGUI の細いピル（線分）の並びで描く。area の大きさが決まってから作る（大きさが変わったら作り直す）</summary>
    class MapView
    {
        class Seg { public Image Img; public Vector2 A, B; }

        readonly RectTransform area;
        readonly bool isBig;
        RectTransform layer;
        float builtW = -1f, builtH = -1f;
        List<Seg> outbound, lineA, lineB;
        Image haze, tagA, tagB;
        TextMeshProUGUI letterA, letterB;
        RectTransform ship, shipIcon;
        string styledRoute;
        bool styledFar;

        // 写し方（km → px）
        float minX, minY, k, offX, offY, rE, rM, rEReal, rMReal, lineW;
        Vector2 earthPx, moonPx;

        const float EarthRealKm = 6378.14f, MoonRealKm = 1737.4f;

        public MapView(RectTransform area, bool big) { this.area = area; isBig = big; }

        /// <summary>まだ作っていないか、大きさが変わったら作る。作ったら true</summary>
        public bool EnsureBuilt(GmatRoute a, GmatRoute b, float hBurn)
        {
            var r = area.rect;
            if (r.width < 40f || r.height < 30f) return false;
            if (Mathf.Abs(r.width - builtW) < 1f && Mathf.Abs(r.height - builtH) < 1f) return false;
            Build(a, b, hBurn, r.width, r.height);
            builtW = r.width; builtH = r.height;
            styledRoute = null;
            return true;
        }

        Vector2 P(Vector3 km) => new Vector2(offX + (km.x - minX) * k, offY + (km.y - minY) * k);

        Vector2 W(Vector2 p)
        {
            p = Warp(p, earthPx, rEReal, rE + lineW);
            p = Warp(p, moonPx, rMReal, rM + lineW);
            return p;
        }

        static Vector2 Warp(Vector2 p, Vector2 c, float real, float vis)
        {
            var d = p - c;
            float r = d.magnitude, range = vis * WarpMul;
            if (r >= range || r < 1e-4f) return p;
            float g = Mathf.Clamp01((range - r) / Mathf.Max(1e-3f, range - real));
            return c + d / r * (r + (vis - real) * g);
        }

        Vector2 At(GmatRoute r, float h) => W(P(r.PosAtHours(h)));

        void Build(GmatRoute a, GmatRoute b, float hBurn, float w, float h)
        {
            if (layer != null) { layer.SetParent(null); Object.Destroy(layer.gameObject); }
            layer = Ui.Rect("Layer", area); UiKit.Fill(layer);
            layer.SetSiblingIndex(1);   // 宇宙の面（0番）のすぐ上。地図の上の小さい文字（MAP・出典）より下

            // 範囲（両ルートの全部の点）
            minX = minY = float.PositiveInfinity; float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
            foreach (var rt in new[] { a, b })
                foreach (var p in rt.posKm) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
            rE = Mathf.Max(MinBody, EarthFrac * h); rM = Mathf.Max(MinBody * 0.75f, MoonFrac * h);
            lineW = isBig ? BigLine : SmallLine;
            float label = isBig ? T.TextLabel * T.BodyLineHeight : 0f;
            float padL = rE + lineW * 2f, padR = rM * (1f + FarLen) + lineW, padY = rE + lineW * 2f + label;
            float dx = Mathf.Max(1f, maxX - minX), dy = Mathf.Max(1f, maxY - minY);
            float aw = w - padL - padR, ah = h - padY * 2f;
            k = Mathf.Max(1e-6f, Mathf.Min(aw / dx, ah / dy));
            offX = padL + (aw - dx * k) * 0.5f; offY = padY + (ah - dy * k) * 0.5f;
            earthPx = P(Vector3.zero);
            moonPx = P(new Vector3(a.moonCenterXKm, 0f, 0f));
            rEReal = EarthRealKm * k; rMReal = MoonRealKm * k;

            var blocked = new List<Rect>();   // 文字を置いてはいけない所（#212：ほかの文字・月）
            // 月の裏側（通信が届かない所）：月の右の薄い帯
            haze = UiKit.Pill(layer, Haze, rM * 2f, "FarSide");
            Place(haze.rectTransform, moonPx, new Vector2(rM * (1f + FarLen), rM * 2f), new Vector2(0f, 0.5f));
            if (isBig)
            {
                var ic = UiKit.Icon(layer, "comms_lost", T.IconS, Caption);
                Place(ic.rectTransform, moonPx + new Vector2(rM * (1f + FarLen * 0.55f), 0f), new Vector2(T.IconS, T.IconS), new Vector2(0.5f, 0.5f));
                // #212：月の名前は月の下なので、電波の注意は月の上（右端）へ
                var fr = LabelRect(Words.NoRadio, new Vector2(w - T.S1, moonPx.y + rM + T.S1), new Vector2(1f, 0f));
                Label(Words.NoRadio, new Vector2(w - T.S1, moonPx.y + rM + T.S1), new Vector2(1f, 0f), Caption);
                blocked.Add(fr);
            }

            // 線：往路（噴射まで共通）→ B → A
            float step = isBig ? BigStep : SmallStep;
            outbound = Line(Pts(a, a.hours[0], hBurn), step, lineW, OutColor);
            lineB = Line(Pts(b, hBurn, b.hours[b.hours.Length - 1]), step, lineW, RouteColor("b"));
            lineA = Line(Pts(a, hBurn, a.hours[a.hours.Length - 1]), step, lineW, RouteColor("a"));

            // 地球・月
            Body(earthPx, rE, T.Go, "earth", T.Panel);
            Body(moonPx, rM, T.Line, "moon", T.Text);
            if (isBig)
            {
                Label(Words.Earth, earthPx + new Vector2(0f, -rE - T.S1 * 0.5f), new Vector2(0.5f, 1f), Caption);
                // #212：月の名前は月の印の下。噴射の印（月のそば）と反対の側へ寄せて、印に隠れないように
                bool burnLeft = At(a, hBurn).x < moonPx.x;
                var mlPv = burnLeft ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
                var mlPos = moonPx + new Vector2(burnLeft ? -rM * 0.2f : rM * 0.2f, -rM - T.S1 * 0.5f);
                Label(Words.Moon, mlPos, mlPv, Caption);
                blocked.Add(LabelRect(Words.Moon, mlPos, mlPv));
                blocked.Add(new Rect(moonPx.x - rM, moonPx.y - rM, rM * 2f, rM * 2f));
                blocked.Add(new Rect(earthPx.x - rE, earthPx.y - rE, rE * 2f, rE * 2f));
            }

            // 噴射する所
            var bp = At(a, hBurn);
            float md = isBig ? BigMark : SmallMark;
            var outDir = (bp - moonPx).sqrMagnitude > 1e-4f ? (bp - moonPx).normalized : Vector2.down;
            var mp = bp + outDir * (md * 0.5f);
            var mark = UiKit.Blob(layer, T.BlobDisc, T.Accent, md * 0.5f, "Burn");   // #190：色つきの台に白い絵【01】
            Place(mark.rectTransform, mp, new Vector2(md, md), new Vector2(0.5f, 0.5f));
            var fl = UiKit.Icon(mark.transform, "ignition", md * 0.6f, T.Panel); UiKit.Center(fl.rectTransform, md * 0.6f, md * 0.6f);
            if (isBig) PlaceBurnLabel(mp, md, w, h, blocked);

            // A・B の札：帰り道のまん中あたり。2本が離れる向きに少しずらす
            tagA = tagB = null; letterA = letterB = null;
            if (isBig)   // 小さい地図は絵だけ（#212）
            {
                var ma = At(a, Mathf.Lerp(hBurn, a.hours[a.hours.Length - 1], 0.5f));
                var mb = At(b, Mathf.Lerp(hBurn, b.hours[b.hours.Length - 1], 0.5f));
                var sep = (ma - mb).sqrMagnitude > 1e-4f ? (ma - mb).normalized : Vector2.up;
                tagA = Tag("a", ma + sep * BigTag * 0.75f, BigTag, out letterA);
                tagB = Tag("b", mb - sep * BigTag * 0.75f, BigTag, out letterB);
            }

            // 船
            float sd = isBig ? BigShip : SmallShip;
            var sh = UiKit.Blob(layer, T.BlobDisc, T.Panel, sd * 0.5f, "Ship");   // #190：明るい台に text の絵
            ship = sh.rectTransform;
            Place(ship, earthPx, new Vector2(sd, sd), new Vector2(0.5f, 0.5f));
            var ri = UiKit.Icon(ship, "rocket", sd * 0.7f, T.Text);
            shipIcon = ri.rectTransform; UiKit.Center(shipIcon, sd * 0.7f, sd * 0.7f);
        }

        Rect LabelRect(string s, Vector2 at, Vector2 pivot)
        {
            var t = UiKit.Text(layer, s, TextStyle.Label, Caption, TextAlignmentOptions.Center, false);
            var size = new Vector2(t.GetPreferredValues(s).x + 4f, T.TextLabel * T.BodyLineHeight);
            Object.Destroy(t.gameObject);
            return new Rect(at - Vector2.Scale(size, pivot), size);
        }

        /// <summary>#212：「Burn here」は線・月・ほかの文字・枠の外に出る置き方を 4 つの中から選ぶ（下・上・右・左のうち、いちばん重ならない所）</summary>
        void PlaceBurnLabel(Vector2 mp, float md, float w, float h, List<Rect> blocked)
        {
            float gap = T.S1;
            var cands = new[]
            {
                (at: mp + new Vector2(0f, -md * 0.5f - gap), pv: new Vector2(0.5f, 1f)),
                (at: mp + new Vector2(0f, md * 0.5f + gap), pv: new Vector2(0.5f, 0f)),
                (at: mp + new Vector2(md * 0.5f + gap, 0f), pv: new Vector2(0f, 0.5f)),
                (at: mp + new Vector2(-md * 0.5f - gap, 0f), pv: new Vector2(1f, 0.5f)),
            };
            int best = 0; float bestScore = float.MaxValue;
            for (int i = 0; i < cands.Length; i++)
            {
                var r = LabelRect(Words.BurnHere, cands[i].at, cands[i].pv);
                var rr = new Rect(r.xMin - 4f, r.yMin - 4f, r.width + 8f, r.height + 8f);
                float score = i * 0.1f;   // 同点なら下を先に
                if (r.xMin < 0f || r.yMin < 0f || r.xMax > w || r.yMax > h) score += 1000f;
                foreach (var b in blocked) if (b.Overlaps(rr)) score += 500f;
                score += Hits(outbound, rr) + Hits(lineA, rr) + Hits(lineB, rr);
                if (score < bestScore) { bestScore = score; best = i; }
            }
            Label(Words.BurnHere, cands[best].at, cands[best].pv, T.Accent);
        }

        static int Hits(List<Seg> line, Rect r)
        {
            int n = 0;
            foreach (var s in line)
            {
                if (r.Contains(s.A) || r.Contains(s.B) || r.Contains((s.A + s.B) * 0.5f)) n++;
            }
            return n;
        }

        Image Tag(string id, Vector2 at, float d, out TextMeshProUGUI letter)
        {
            var img = UiKit.Blob(layer, T.BlobDisc, RouteColor(id), d * 0.5f, "Tag_" + id);   // #190：A・B の台
            Place(img.rectTransform, at, new Vector2(d, d), new Vector2(0.5f, 0.5f));
            letter = UiKit.Text(img.transform, id.ToUpper(), TextStyle.Tag, RouteFg(id)); UiKit.Fill(letter.rectTransform);
            return img;
        }

        void Body(Vector2 at, float r, Color face, string icon, Color fg)
        {
            var img = UiKit.Pill(layer, face, r * 2f, "Body_" + icon);
            Place(img.rectTransform, at, new Vector2(r * 2f, r * 2f), new Vector2(0.5f, 0.5f));
            if (isBig && r * 1.2f >= T.IconS) { var ic = UiKit.Icon(img.transform, icon, r * 1.2f, fg); UiKit.Center(ic.rectTransform, r * 1.2f, r * 1.2f); }
        }

        void Label(string s, Vector2 at, Vector2 pivot, Color c)
        {
            var t = UiKit.Text(layer, s, TextStyle.Label, c, TextAlignmentOptions.Center, false);
            Place(t.rectTransform, at, new Vector2(t.GetPreferredValues(s).x + 4f, T.TextLabel * T.BodyLineHeight), pivot);
        }

        static void Place(RectTransform r, Vector2 at, Vector2 size, Vector2 pivot)
        {
            r.anchorMin = r.anchorMax = Vector2.zero; r.pivot = pivot;
            r.sizeDelta = size; r.anchoredPosition = at;
        }

        List<Vector2> Pts(GmatRoute r, float h0, float h1)
        {
            var list = new List<Vector2> { At(r, h0) };
            for (int i = 0; i < r.hours.Length; i++)
                if (r.hours[i] > h0 && r.hours[i] < h1) list.Add(W(P(r.posKm[i])));
            list.Add(At(r, h1));
            return list;
        }

        /// <summary>折れ線を同じ長さ（step）の線分に切り直して、1本ずつピルで置く（点線は1つおきに隠す）</summary>
        List<Seg> Line(List<Vector2> pts, float step, float width, Color c)
        {
            var q = new List<Vector2> { pts[0] };
            float carry = 0f;
            for (int i = 1; i < pts.Count; i++)
            {
                Vector2 a = pts[i - 1], b = pts[i];
                float len = (b - a).magnitude;
                if (len < 1e-4f) continue;
                float pos = step - carry;
                while (pos <= len) { q.Add(Vector2.Lerp(a, b, pos / len)); pos += step; }
                carry = len - (pos - step);
            }
            if ((q[q.Count - 1] - pts[pts.Count - 1]).sqrMagnitude > 0.25f) q.Add(pts[pts.Count - 1]);
            var segs = new List<Seg>();
            for (int i = 0; i + 1 < q.Count; i++)
            {
                var s = new Seg { Img = UiKit.Pill(layer, c, width, "Seg"), A = q[i], B = q[i + 1] };
                PlaceSeg(s, width);
                segs.Add(s);
            }
            return segs;
        }

        static void PlaceSeg(Seg s, float w)
        {
            var r = s.Img.rectTransform;
            var d = s.B - s.A;
            float len = d.magnitude;
            var dir = len > 1e-4f ? d / len : Vector2.right;
            r.anchorMin = r.anchorMax = Vector2.zero; r.pivot = new Vector2(0f, 0.5f);
            r.sizeDelta = new Vector2(len + w, w);
            r.anchoredPosition = s.A - dir * (w * 0.5f);
            r.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            var sp = s.Img.sprite;
            if (sp != null && sp.border.x > 0f) s.Img.pixelsPerUnitMultiplier = sp.border.x / Mathf.Max(0.5f, w * 0.5f);
        }

        static void Style(List<Seg> line, Color c, float alpha, float w, bool dashed)
        {
            c.a = alpha;
            for (int i = 0; i < line.Count; i++)
            {
                var s = line[i];
                s.Img.color = c;
                s.Img.enabled = !dashed || i % 2 == 0;
                if (Mathf.Abs(s.Img.rectTransform.sizeDelta.y - w) > 0.01f) PlaceSeg(s, w);
            }
        }

        /// <summary>選んだルート・月の裏側にいるか・船の位置（r の上の h 時間）</summary>
        public void Apply(string route, bool farside, GmatRoute r, float h)
        {
            if (layer == null) return;
            if (route != styledRoute)
            {
                bool none = route != "a" && route != "b";
                Style(lineA, RouteColor("a"), none || route == "a" ? 1f : FaintAlpha, route == "a" ? lineW * ThickMul : lineW, route != "a");
                Style(lineB, RouteColor("b"), none || route == "b" ? 1f : FaintAlpha, route == "b" ? lineW * ThickMul : lineW, route != "b");
                if (tagA != null) Fade(tagA, letterA, "a", none || route == "a");
                if (tagB != null) Fade(tagB, letterB, "b", none || route == "b");
                styledRoute = route;
            }
            if (farside != styledFar || haze.color != (farside ? HazeOn : Haze)) { haze.color = farside ? HazeOn : Haze; styledFar = farside; }

            var p = At(r, h);
            ship.anchoredPosition = p;
            const float dh = 0.05f;
            var ahead = At(r, h + dh) - At(r, h - dh);
            if (ahead.sqrMagnitude > 1e-6f)
                shipIcon.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(ahead.y, ahead.x) * Mathf.Rad2Deg - 45f);   // rocket のアイコンは右上（45°）を向いている
        }

        static void Fade(Image tag, TextMeshProUGUI letter, string id, bool on)
        {
            var c = RouteColor(id); c.a = on ? 1f : FaintAlpha; tag.color = c;
            var f = RouteFg(id); f.a = on ? 1f : FaintAlpha; letter.color = f;
        }
    }
}
