// 書いた人：Claude ／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-14、#130）／ 更新：iizuka（Opus 5.5、2026-11-14、#85）／ 更新：iizuka（Opus 5.5、2026-11-15、#189 SCORE は整数）／ 更新：iizuka（Opus 5.5、2026-11-15、#190 あつ森に寄せる）
// エンディング（ED-A・ED-B・ED-C、#85）。帰還の演出（ReturnStage の SPLASHDOWN!・LostStage の Mission lost）のあとに出る。
// サーバーは ending から 16 秒で report に進む（See the report! を誰かが押しても進む）。
// 並び（EndingLayout。#190：ui-guidelines 3章「画面ごとの選択の替え方」のエンディング）：
//   上  ：左に見出しの帯（結末の色＋アイコン、下の縁が波。【11】）＋いつ着いたか・何が尽きたかの1行／右に得点の数のチップ（トロフィーの台、pts の札。【09】）
//   中  ：資源のカード（色違いのカード。【11】）。ED-A・ED-B は生還ライン（end_*_min）とくらべた酸素・電力・温度、ED-C は酸素・電力（どちらが尽きたか）
//         ＋ 判定が厳しくなった理由（strict_reason）の札
//   EndHouston：ヒューストンの台詞（ED-A/B/C。【12】の吹き出し）
//   下  ：See the report!（主ボタン。右下に手。【05】）
// ED-C は静かに：帯は plum、尽きた物は panel の台に ✗（赤い大見出しにしない）。
// 文言・色・アイコンは EndingCopy、寸法は EndingTokens、置き場所は EndingLayout。UI は判定をしない（結末はサーバーの ending）。
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
    readonly UiEndBanner banner;
    readonly TextMeshProUGUI sub;
    readonly UiEndPill strict;
    readonly UiCounter score;
    readonly Dictionary<string, UiResourceCard> cards = new Dictionary<string, UiResourceCard>();
    readonly UiQuote houston;

    public EndingScreen(Transform parent, GameFlow f)
    {
        this.f = f;
        var bg = Ui.PanelImg(parent, T.Bg, "Ending"); Root = bg.gameObject; Ui.Fill(bg.rectTransform); TitleBackdrop.Motifs(bg.rectTransform);   // 後ろ（船内）を押せないように面で受ける
        feed = EndingFeed.For(parent, f);
        feed.Changed += () => { if (Root.activeInHierarchy) Refresh(); };

        var bodyRect = Ui.Rect("Body", bg.transform); UiKit.Fill(bodyRect); body = bodyRect.gameObject;
        var safe = UiKit.SafeArea(bodyRect);

        // 上：見出しの帯＋1行（左）、得点（右。台が上に半分出るので上を空ける）
        var top = EndingWidgets.FillRow(EndingWidgets.Zone(safe, EndingLayout.EndTop, "Top"), T.S3, TextAnchor.MiddleLeft, T.ScreenEdge, T.S3, 0f);
        var words = UiKit.Col(top, T.S2, TextAnchor.MiddleLeft);   // 帯の下の波（12）の分あける
        banner = EndingWidgets.Banner(words, "", T.Go, "check");
        sub = UiKit.Text(words, "", TextStyle.Body, T.TextSoft, TextAlignmentOptions.MidlineLeft);
        EndingWidgets.Spacer(top);
        score = UiKit.Counter(top, C.ScoreIcon, C.ScoreUnit);

        // 中：資源のカード＋判定が厳しくなった理由
        var mid = EndingWidgets.FillCol(EndingWidgets.Zone(safe, EndingLayout.EndMid, "Middle"), T.S3, TextAnchor.MiddleCenter, T.ScreenEdge, 0f, 0f, false);
        var cardRow = UiKit.Row(mid, T.S2);
        foreach (var res in new[] { C.Oxygen, C.Power, C.Temp }) cards[res] = EndingWidgets.ResourceCard(cardRow, res);
        strict = EndingWidgets.Pill(mid, T.Panel, "caution", T.Accent, TextStyle.Body, T.Text);

        // ヒューストンの台詞
        houston = EndingWidgets.Quote(safe, EndingLayout.EndHouston, C.HoustonTag);

        // 下：レポートへ（右下。手を乗せる）
        var bottom = EndingWidgets.FillRow(EndingWidgets.Zone(safe, EndingLayout.EndBottom, "Bottom"), T.S3, TextAnchor.MiddleRight, T.ScreenEdge, 0f, EndingTokens.BottomPad);
        var go = UiKit.PrimaryButton(bottom, C.SeeReport, C.SeeReportIcon, () => f.Client?.Send(Cmd.Type("next")));
        UiSelect.Cursor(go.Root, CursorDir.UpLeft);   // 選ぶ物が無い画面なので、手は主ボタンに（ui-guidelines 3章「選ぶ」）

        body.SetActive(false);
    }

    public void Refresh()
    {
        var i = EndingInfo.From(f, feed);
        body.SetActive(i != null);   // 段階が ending になってから result が届くまでの間は空（前の回の値を出さない）
        if (i == null) return;

        var st = C.For(i.Ending);
        banner.Set(st.Title, st.Face, st.Icon);
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
        strict.Root.gameObject.SetActive(why != "");
        strict.Label.text = why;

        houston.Set(C.Clean(feed.Line(i.Ending)));
        score.Set(C.Score(i.Score));   // #189：整数
    }

    /// <summary>ED-A・ED-B：生還ラインとくらべる（足りない物は accent の台に注意）。ED-C：尽きた物は panel の台に ✗（赤は使わない）</summary>
    static void SetCard(UiResourceCard c, EndingInfo i)
    {
        string value = C.Num(i.Value(c.Res));
        if (!i.Survived)
        {
            bool gone = i.RanOut(c.Res);
            if (gone) c.Set(value, C.StatusRanOut, "cross", T.Panel, C.ResourceFace(c.Res));
            else c.Set(value, C.StatusLeft, "check", T.Go);
            return;
        }
        float need = i.Need(c.Res);
        if (float.IsNaN(need)) { c.Set(value, C.StatusOkNoLine, "check", T.Go); return; }
        bool low = i.Below(c.Res);
        string line = C.Num(need) + C.ResourceUnit(c.Res);
        c.Set(value, string.Format(low ? C.StatusLow : C.StatusOk, line), low ? "caution" : "check", low ? T.Accent : T.Go);
    }
}
