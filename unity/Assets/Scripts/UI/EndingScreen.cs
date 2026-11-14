// 書いた人：Claude ／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#130）／ 更新：iizuka（Opus 5.5、2026-11-14、#85）
// エンディング（ED-A・ED-B・ED-C、#85）。帰還の演出（ReturnStage の SPLASHDOWN!・LostStage の Mission lost）のあとに出る。
// サーバーは ending から 16 秒で report に進む（SEE THE REPORT を誰かが押しても進む）。
// 並び（EndingLayout）：
//   上  ：丸のアイコン＋（SPLASHDOWN! ／ 結末の見出し ／ いつ着いたか・何が尽きたか）
//   中  ：資源のカード。ED-A・ED-B は生還ライン（end_*_min）とくらべた酸素・電力・温度、ED-C は酸素・電力（どちらが尽きたか）
//         ＋ 判定が厳しくなった理由（strict_reason）
//   HoustonBox の所：ヒューストンの台詞（ED-A/B/C）。HoustonBox も同じ台詞を同じ所に出すので、ほかの物とは重ならない
//   下  ：スコア＋SEE THE REPORT（主ボタン）
// 文言・アイコンは EndingCopy、寸法は EndingTokens、置き場所は EndingLayout。UI は判定をしない（結末はサーバーの ending）。
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using T = UiTokens;
using C = EndingCopy;

public class EndingScreen
{
    public readonly GameObject Root;
    readonly GameFlow f;
    readonly EndingFeed feed;
    readonly GameObject body;
    readonly UiDot hero;
    readonly TextMeshProUGUI kicker, title, sub, strictText, score;
    readonly GameObject strictChip;
    readonly Dictionary<string, UiResourceCard> cards = new Dictionary<string, UiResourceCard>();
    readonly UiQuote houston;

    public EndingScreen(Transform parent, GameFlow f)
    {
        this.f = f;
        var bg = Ui.PanelImg(parent, T.Bg, "Ending"); Root = bg.gameObject; Ui.Fill(bg.rectTransform);   // 後ろ（船内）を押せないように面で受ける
        feed = EndingFeed.For(parent, f);
        feed.Changed += () => { if (Root.activeInHierarchy) Refresh(); };

        var bodyRect = Ui.Rect("Body", bg.transform); UiKit.Fill(bodyRect); body = bodyRect.gameObject;
        var safe = UiKit.SafeArea(bodyRect);

        // 上：結末の見出し
        var top = EndingWidgets.FillRow(EndingWidgets.Zone(safe, EndingLayout.EndTop, "Top"), T.S3, TextAnchor.MiddleCenter, T.ScreenEdge, T.S3, 0f);
        hero = EndingWidgets.Dot(top, "check", T.Go, T.Panel, EndingTokens.HeroDot, T.IconL);
        var words = UiKit.Col(top, 0f, TextAnchor.MiddleLeft);
        kicker = UiKit.Text(words, "", TextStyle.Sub, T.InkSub, TextAlignmentOptions.MidlineLeft);
        title = UiKit.Text(words, "", TextStyle.Heading, T.Ink, TextAlignmentOptions.MidlineLeft);
        sub = UiKit.Text(words, "", TextStyle.Body, T.InkSub, TextAlignmentOptions.MidlineLeft);

        // 中：資源のカード＋判定が厳しくなった理由
        var mid = EndingWidgets.FillCol(EndingWidgets.Zone(safe, EndingLayout.EndMid, "Middle"), T.S2, TextAnchor.MiddleCenter, T.ScreenEdge, 0f, 0f, false);
        var cardRow = UiKit.Row(mid, T.S3);
        foreach (var res in new[] { C.Oxygen, C.Power, C.Temp }) cards[res] = EndingWidgets.ResourceCard(cardRow, res);
        var chip = UiKit.Chip(mid, T.Panel, EndingTokens.ChipH, "Strict");
        UiKit.Icon(chip, "caution", T.IconM, T.Accent);
        strictText = UiKit.Text(chip, "", TextStyle.Body, T.Ink);
        strictChip = chip.gameObject;

        // HoustonBox の所：ヒューストンの台詞
        houston = EndingWidgets.Quote(bodyRect, EndingLayout.HoustonSlot, C.HoustonTag);

        // 下：スコア＋レポートへ
        var bottom = EndingWidgets.FillRow(EndingWidgets.Zone(safe, EndingLayout.EndBottom, "Bottom"), T.S3, TextAnchor.LowerCenter, T.ScreenEdge, 0f, T.ScreenEdge);   // 下に寄せる（上の HoustonBox の所に入らない）
        score = EndingWidgets.Chip(bottom, T.Ink, T.Panel, "score", C.ScoreLabel, TextStyle.Number, EndingTokens.BigChipH, out _, out _);
        UiKit.PrimaryButton(bottom, C.SeeReport, C.SeeReportIcon, () => f.Client?.Send(Cmd.Type("next")));

        body.SetActive(false);
    }

    public void Refresh()
    {
        var i = EndingInfo.From(f, feed);
        body.SetActive(i != null);   // 段階が ending になってから result が届くまでの間は空（前の回の値を出さない）
        if (i == null) return;

        var st = C.For(i.Ending);
        hero.Face.color = st.Face;
        hero.Icon.sprite = T.Icon(st.Icon); hero.Icon.color = st.OnFace;
        kicker.text = st.Kicker;
        title.text = st.Title;
        string when = Ui.Mmss(i.TEnd), cause = i.Cause();
        sub.text = i.Survived ? string.Format(i.Route == "" ? C.SubHomeNoRoute : C.SubHome, when, i.Route.ToUpperInvariant())
                 : string.Format(cause == "" ? C.SubLostOther : C.SubLost, C.CauseName(cause), when);

        var show = new HashSet<string>(i.Survived ? C.HomeCards : C.LostCards);
        foreach (var kv in cards)
        {
            kv.Value.Root.gameObject.SetActive(show.Contains(kv.Key));
            if (show.Contains(kv.Key)) SetCard(kv.Value, i);
        }

        float margin = EndingParams.Get("end_strict_margin");
        string why = i.Strict ? string.Format(C.StrictText(i.StrictReason, i.Survived), C.Num(margin)) : "";
        strictChip.SetActive(why != "");
        strictText.text = why;

        houston.Set(C.Clean(feed.Line(i.Ending)));
        score.text = C.Num(i.Score);
    }

    /// <summary>ED-A・ED-B：生還ラインとくらべる（足りない物だけ輪とオレンジ）。ED-C：尽きた物に輪（赤は使わない）</summary>
    static void SetCard(UiResourceCard c, EndingInfo i)
    {
        string value = C.Num(i.Value(c.Res));
        if (!i.Survived)
        {
            bool gone = i.RanOut(c.Res);
            c.Set(value, gone ? C.StatusRanOut : C.StatusLeft, gone ? "cross" : "check", gone ? T.Ink : T.InkSub, gone ? T.Ink : (Color?)null);
            return;
        }
        float need = i.Need(c.Res);
        if (float.IsNaN(need)) { c.Set(value, C.StatusOkNoLine, "check", T.Go, null); return; }
        bool low = i.Below(c.Res);
        string line = C.Num(need) + C.ResourceUnit(c.Res);
        c.Set(value, string.Format(low ? C.StatusLow : C.StatusOk, line), low ? "caution" : "check", low ? T.Accent : T.Go, low ? T.Accent : (Color?)null);
    }
}
