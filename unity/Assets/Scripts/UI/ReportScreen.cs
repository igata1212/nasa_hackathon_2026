// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-15、#189 SCORE と内訳は整数）／ 更新：iizuka（Opus 5.5、2026-11-15、#190 あつ森に寄せる）
// 事後レポート（R01、#86）。エンディングのあと。高校生が「何がよくて、何が足りなかったか」を1画面で分かるようにする。
// 並び（EndingLayout。#190：ui-guidelines 3章のレポート）：
//   上  ：見出しの帯 Mission report（左上、下の縁が波。【11】）＋ 結末の札
//   中  ：3枚のパネル（【05】）。各行は材料の行（台＋名前＋点線＋右寄せの数。【11】）
//         要件4つ（台に ✓/✗、右に「いま / 要る量」、✗ には「次はこれ」の台）／スコアの内訳（score_breakdown、SPEC 8章の式の順）／
//         主な出来事と時刻（commands から EndingCopy.Moments の決まりで選ぶ）
//   RepHouston：ヒューストンのまとめ（よかったこと／次に変えること）
//   下  ：本物のアポロ13号のカード（#89 で増やす。今は #107 で決まった1つ）＋ 得点の合計（数のチップ。【09】の合計のピル）＋
//         Fly again!（主ボタン、右下に手。again：設計は前回のまま、全員が設計へ）
// パネルの幅は中身の幅＋残りを3枚で分ける（#189 の残り：出来事の列が狭く「Missed the burn window」が切れた）。
// ランキングは作らない（#9）。数字はサーバーの値をそのまま出す（内訳の足し算もしない）。SCORE と内訳だけ整数に丸めて出す（#189。EndingCopy.Score）
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;
using C = EndingCopy;

public class ReportScreen
{
    public readonly GameObject Root;
    readonly GameFlow f;
    readonly EndingFeed feed;
    readonly GameObject body;
    readonly UiEndPill ending;
    readonly UiCounter total;
    readonly TextMeshProUGUI apollo;
    readonly UiReqRow[] reqs = new UiReqRow[C.Reqs.Length];
    readonly UiLineRow[] scoreRows = new UiLineRow[C.ScoreRows.GetLength(0)];
    readonly UiLineRow[] moments = new UiLineRow[EndingTokens.MaxMoments];
    readonly UiQuote houston;

    public ReportScreen(Transform parent, GameFlow f)
    {
        this.f = f;
        var bg = Ui.PanelImg(parent, T.Bg, "Report"); Root = bg.gameObject; Ui.Fill(bg.rectTransform);
        feed = EndingFeed.For(parent, f);
        feed.Changed += () => { if (Root.activeInHierarchy) Refresh(); };

        var bodyRect = Ui.Rect("Body", bg.transform); UiKit.Fill(bodyRect); body = bodyRect.gameObject;
        var safe = UiKit.SafeArea(bodyRect);

        // 上：見出しの帯＋結末の札
        var top = EndingWidgets.FillRow(EndingWidgets.Zone(safe, EndingLayout.RepTop, "Top"), T.S3, TextAnchor.MiddleLeft, T.ScreenEdge, 0f, 0f);
        EndingWidgets.Banner(top, C.ReportTitle, T.Plum, C.ReportIcon);
        ending = EndingWidgets.Pill(top, T.Go, "check", T.Panel, TextStyle.Choice, T.Panel);
        EndingWidgets.Spacer(top);

        // 中：3枚のパネル
        var row = EndingWidgets.FillRow(EndingWidgets.Zone(safe, EndingLayout.RepMid, "Panels"), T.S2, TextAnchor.UpperCenter, T.S3, 0f, T.S1);
        var goals = EndingWidgets.ReportPanel(row, "Goals");
        EndingWidgets.PanelHead(goals, C.GoalsTitle, C.GoalsIcon, C.GoalsFace);
        for (int k = 0; k < reqs.Length; k++) reqs[k] = EndingWidgets.ReqRow(goals);

        var sc = EndingWidgets.ReportPanel(row, "Score");
        EndingWidgets.PanelHead(sc, C.ScoreTitle, C.ScoreIcon, C.ScoreFace);
        for (int k = 0; k < scoreRows.Length; k++) scoreRows[k] = EndingWidgets.LineRow(sc);

        var mo = EndingWidgets.ReportPanel(row, "Moments");
        EndingWidgets.PanelHead(mo, C.MomentsTitle, C.MomentsIcon, C.MomentsFace);
        for (int k = 0; k < moments.Length; k++) moments[k] = EndingWidgets.LineRow(mo);

        // ヒューストンのまとめ
        houston = EndingWidgets.Quote(safe, EndingLayout.RepHouston, C.HoustonTag);

        // 下：本物のアポロ13号のカード＋合計＋もう一度（右下に手）
        var bottom = EndingWidgets.FillRow(EndingWidgets.Zone(safe, EndingLayout.RepBottom, "Bottom"), T.S3, TextAnchor.MiddleRight, T.ScreenEdge, 0f, EndingTokens.BottomPad);
        var card = UiKit.Card(bottom, T.Ink);   // 本当の宇宙の話は紺のカード（【11】の色違いのカード）
        card.Root.name = "Apollo13";
        int pad = Mathf.RoundToInt(T.S2);
        card.Root.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(Mathf.RoundToInt(T.S3), Mathf.RoundToInt(T.S3), pad, pad);
        var cle = Ui.Size(card.Root, w: 0f, h: EndingTokens.ApolloH, flexW: 1f); cle.minWidth = 0f; cle.minHeight = EndingTokens.ApolloH;
        var line = UiKit.Row(card.Content, T.S3, TextAnchor.MiddleLeft);
        UiKit.Tag(line, C.ApolloTag, T.Accent, T.Panel);
        apollo = EndingWidgets.Wrap(line, "", TextStyle.Label, T.Panel, TextAlignmentOptions.MidlineLeft);
        var ale = Ui.Size(apollo, w: 0f, flexW: 1f); ale.minWidth = 0f;

        total = UiKit.Counter(bottom, C.ScoreIcon, C.ScoreUnit);
        TotalLook(total);
        var again = UiKit.PrimaryButton(bottom, C.FlyAgain, C.FlyAgainIcon, () => f.Client?.Send(Cmd.Type("again")));
        UiSelect.Cursor(again.Root, CursorDir.UpLeft);   // 最後に押すボタンに手（【05】）

        body.SetActive(false);
    }

    /// <summary>合計のピル（【09】下）：数のチップのトロフィーの台を、上ではなくピルの中の左に置く（上に出すと吹き出しにかかる）</summary>
    static void TotalLook(UiCounter c)
    {
        float d = T.CounterIconD;
        var dr = c.Disc.Root;
        dr.anchorMin = dr.anchorMax = new Vector2(0f, 0.5f); dr.pivot = new Vector2(0.5f, 0.5f);
        dr.anchoredPosition = new Vector2(T.S2 + d * 0.5f, 0f);
        c.Root.GetComponent<HorizontalLayoutGroup>().padding.left = Mathf.RoundToInt(T.S2 * 2f + d);
    }

    public void Refresh()
    {
        var i = EndingInfo.From(f, feed);
        body.SetActive(i != null);
        if (i == null) return;

        var st = C.For(i.Ending);
        ending.Face.color = st.Face;
        ending.Disc.Set(st.Icon, T.Panel);
        ending.Disc.Icon.color = st.Face;
        ending.Label.text = st.Short;
        total.Set(C.Score(i.Score));

        string cause = i.Cause();
        for (int k = 0; k < reqs.Length; k++)
        {
            var r = C.Reqs[k];
            string hint = r.Key == "req1_crew_alive" ? C.CrewHint(cause) : r.Hint;
            reqs[k].Set(i.Pass(r.Key), r.Label, r.Value?.Invoke(i), hint);
        }

        for (int k = 0; k < scoreRows.Length; k++)
        {
            string key = C.ScoreRows[k, 0];
            if (!J.Has(i.Breakdown, key)) { scoreRows[k].Hide(); continue; }
            float v = J.Num(i.Breakdown, key);
            scoreRows[k].Set(C.ScoreRows[k, 2], C.ScoreRowFace, C.ScoreRows[k, 1], C.ScorePlus(v), v <= 0f);
        }

        var list = PickMoments(i);
        for (int k = 0; k < moments.Length; k++)
        {
            if (k < list.Count) moments[k].Set(list[k].icon, list[k].face, list[k].label, Ui.Mmss(list[k].t));
            else moments[k].Hide();
        }

        houston.Set(C.Takeaway(i));
        apollo.text = C.ApolloFacts.Length > 0 ? C.ApolloFacts[0] : "";
    }

    /// <summary>主な出来事：事故・結末（必ず）＋ commands から決まりに当たったもの ＋ 時刻の目印。優先度の高い順に選んで時刻順に並べる</summary>
    static List<(float t, int pr, string icon, Color face, string label)> PickMoments(EndingInfo i)
    {
        var all = new List<(float t, int pr, string icon, Color face, string label)> { (0f, C.PriorityFixed, C.MomentAccidentIcon, C.MomentAlertFace, C.MomentAccident) };
        var used = new HashSet<int>();
        if (i.Commands != null)
            foreach (var o in i.Commands)
            {
                if (!(o is Dictionary<string, object> c)) continue;
                string cmd = J.Str(c, "cmd");
                for (int k = 0; k < C.Moments.Length; k++)
                {
                    var m = C.Moments[k];
                    if (used.Contains(k) || m.Cmd != cmd || (m.Match != null && !m.Match(c))) continue;
                    used.Add(k);
                    string label = m.Show == null ? m.Label : string.Format(m.Label, J.Str(J.Obj(c, "args"), m.Show).ToUpperInvariant());
                    all.Add((J.Num(c, "t"), m.Priority, m.Icon, C.MomentFace, label));
                    break;
                }
            }
        var s = i.LastState;
        if (s != null)
        {
            if (i.StrictReason == "missed_burn") all.Add((s.tBurnClose, C.PriorityMissed, C.MomentMissedBurnIcon, C.MomentAlertFace, C.MomentMissedBurn));
            if (s.tLos > 0f && i.TEnd > s.tLos) all.Add((s.tLos, C.PriorityTimeline, C.MomentFarSideIcon, C.MomentTimelineFace, C.MomentFarSide));
            if (s.tAos > 0f && i.TEnd > s.tAos) all.Add((s.tAos, C.PriorityTimeline, C.MomentSignalIcon, C.MomentTimelineFace, C.MomentSignal));
        }
        string cause = i.Cause();
        if (i.Survived) all.Add((i.TEnd, C.PriorityFixed, C.MomentSplashIcon, C.MomentSplashFace, C.MomentSplash));
        else all.Add((i.TEnd, C.PriorityFixed, C.MomentLostIcon, C.MomentLostFace, cause == "" ? C.MomentLostOther : string.Format(C.MomentLost, C.CauseName(cause))));

        return all.OrderBy(x => x.pr).ThenBy(x => x.t).Take(EndingTokens.MaxMoments)
                  .OrderBy(x => x.t).ThenBy(x => x.pr).ToList();
    }
}
