// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// 事後レポート（R01、#86）。エンディングのあと。高校生が「何がよくて、何が足りなかったか」を1画面で分かるようにする。
// 並び（EndingLayout）：
//   上  ：MISSION REPORT ＋ 結末のチップ ＋ スコア
//   中  ：3枚のパネル。要件4つの ✓/✗（✗ には「次はこれ」のアイコン）／スコアの内訳（score_breakdown、SPEC 8章の式の順）／
//         主な出来事の時刻（commands から EndingCopy.Moments の決まりで選ぶ）
//   HoustonBox の所：ヒューストンのまとめ（よかったこと／次に変えること）。最初の数秒は HoustonBox の R01 がかぶさり、消えるとこれが見える
//   下  ：本物のアポロ13号の枠（#89 で増やす。今は #107 で決まった1つ）＋ REDESIGN AND FLY AGAIN（主ボタン。again：設計は前回のまま、全員が設計へ）
// ランキングは作らない（#9）。数字はサーバーの値をそのまま出す（内訳の足し算もしない）。
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
    readonly Image endFace, endIcon;
    readonly TextMeshProUGUI endText, total, scoreTotal, apollo;
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

        // 上：題・結末・スコア
        var top = EndingWidgets.FillRow(EndingWidgets.Zone(safe, EndingLayout.RepTop, "Top"), T.S3, TextAnchor.MiddleLeft, T.ScreenEdge, T.S3, 0f);
        UiKit.Text(top, C.ReportTitle, TextStyle.Big, T.Ink, TextAlignmentOptions.MidlineLeft);
        var chip = UiKit.Chip(top, T.Go, EndingTokens.ChipH, "Ending");
        endFace = chip.Find("Face").GetComponent<Image>();
        endIcon = UiKit.Icon(chip, "check", T.IconM, T.Panel);
        endText = UiKit.Text(chip, "", TextStyle.Sub, T.Panel);
        EndingWidgets.Spacer(top);
        total = EndingWidgets.Chip(top, T.Ink, T.Panel, "score", C.ScoreLabel, TextStyle.Number, EndingTokens.ChipH, out _, out _);

        // 中：3枚のパネル
        var row = EndingWidgets.FillRow(EndingWidgets.Zone(safe, EndingLayout.RepMid, "Panels"), T.S3, TextAnchor.UpperCenter, T.ScreenEdge, T.S1, T.S2);
        var goals = Panel(row, "Goals", EndingLayout.RepPanelFlex[0]);
        EndingWidgets.PanelHead(goals, C.GoalsTitle, C.GoalsIcon);
        for (int k = 0; k < reqs.Length; k++) reqs[k] = EndingWidgets.ReqRow(goals);

        var sc = Panel(row, "Score", EndingLayout.RepPanelFlex[1]);
        scoreTotal = EndingWidgets.PanelHead(sc, C.ScoreTitle, C.ScoreIcon, true);
        for (int k = 0; k < scoreRows.Length; k++) scoreRows[k] = EndingWidgets.LineRow(sc, false, true);

        var mo = Panel(row, "Moments", EndingLayout.RepPanelFlex[2]);
        EndingWidgets.PanelHead(mo, C.MomentsTitle, C.MomentsIcon);
        for (int k = 0; k < moments.Length; k++) moments[k] = EndingWidgets.LineRow(mo, true, false);

        // HoustonBox の所：ヒューストンのまとめ
        houston = EndingWidgets.Quote(bodyRect, EndingLayout.HoustonSlot, C.HoustonTag);

        // 下：本物のアポロ13号＋もう一度
        var bottom = EndingWidgets.FillRow(EndingWidgets.Zone(safe, EndingLayout.RepBottom, "Bottom"), T.S3, TextAnchor.LowerCenter, T.ScreenEdge, 0f, T.ScreenEdge);   // 下に寄せる（上の HoustonBox の所に入らない）
        var card = UiKit.Row(bottom, T.S2, TextAnchor.MiddleLeft, T.S3, T.S1);
        card.name = "Apollo13";
        UiKit.Surface(card, T.RadiusM, T.Panel);
        var cle = Ui.Size(card, w: 0f, h: EndingTokens.ApolloH, flexW: 1f); cle.minWidth = 0f; cle.minHeight = EndingTokens.ApolloH;
        UiKit.Tag(card, C.ApolloTag, T.Ink, T.Panel);
        apollo = EndingWidgets.Wrap(card, "", TextStyle.Label, T.Ink, TextAlignmentOptions.MidlineLeft);
        var ale = Ui.Size(apollo, w: 0f, flexW: 1f); ale.minWidth = 0f;
        UiKit.PrimaryButton(bottom, C.FlyAgain, C.FlyAgainIcon, () => f.Client?.Send(Cmd.Type("again")));

        body.SetActive(false);
    }

    static RectTransform Panel(Transform row, string name, float flex)
    {
        var p = UiKit.Panel(row, 0f, name);   // 行の間は 0（行の高さで十分あく）
        var l = p.GetComponent<VerticalLayoutGroup>();
        l.padding.top = l.padding.bottom = Mathf.RoundToInt(EndingTokens.ListPadY);
        var le = Ui.Size(p, w: 0f, flexW: flex, flexH: 1f); le.minWidth = 0f;
        return p;
    }

    public void Refresh()
    {
        var i = EndingInfo.From(f, feed);
        body.SetActive(i != null);
        if (i == null) return;

        var st = C.For(i.Ending);
        endFace.color = st.Face;
        endIcon.sprite = T.Icon(st.Icon); endIcon.color = st.OnFace;
        endText.text = st.Short; endText.color = st.OnFace;
        total.text = C.Num(i.Score);
        scoreTotal.text = C.Num(i.Score);

        string cause = i.Cause();
        for (int k = 0; k < reqs.Length; k++)
        {
            var r = C.Reqs[k];
            string hint = r.Key == "req1_crew_alive" ? C.CrewHint(cause) : r.Hint;
            bool pass = i.Pass(r.Key);
            reqs[k].Set(pass, r.Label, pass && !r.DetailWhenPass ? null : r.Detail?.Invoke(i), hint);
        }

        for (int k = 0; k < scoreRows.Length; k++)
        {
            string key = C.ScoreRows[k, 0];
            bool has = J.Has(i.Breakdown, key);
            scoreRows[k].Root.gameObject.SetActive(has);
            if (!has) continue;
            float v = J.Num(i.Breakdown, key);
            scoreRows[k].Set(null, C.ScoreRows[k, 2], C.ScoreRows[k, 1], C.Plus(v), v <= 0f);
        }

        var list = PickMoments(i);
        for (int k = 0; k < moments.Length; k++)
        {
            if (k < list.Count) moments[k].Set(Ui.Mmss(list[k].t), list[k].icon, list[k].label, null);
            else moments[k].Root.gameObject.SetActive(false);
        }

        houston.Set(C.Takeaway(i));
        apollo.text = C.ApolloFacts.Length > 0 ? C.ApolloFacts[0] : "";
    }

    /// <summary>主な出来事：事故・結末（必ず）＋ commands から決まりに当たったもの ＋ 時刻の目印。優先度の高い順に選んで時刻順に並べる</summary>
    static List<(float t, int pr, string icon, string label)> PickMoments(EndingInfo i)
    {
        var all = new List<(float t, int pr, string icon, string label)> { (0f, C.PriorityFixed, C.MomentAccidentIcon, C.MomentAccident) };
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
                    all.Add((J.Num(c, "t"), m.Priority, m.Icon, label));
                    break;
                }
            }
        var s = i.LastState;
        if (s != null)
        {
            if (i.StrictReason == "missed_burn") all.Add((s.tBurnClose, C.PriorityMissed, C.MomentMissedBurnIcon, C.MomentMissedBurn));
            if (s.tLos > 0f && i.TEnd > s.tLos) all.Add((s.tLos, C.PriorityTimeline, C.MomentFarSideIcon, C.MomentFarSide));
            if (s.tAos > 0f && i.TEnd > s.tAos) all.Add((s.tAos, C.PriorityTimeline, C.MomentSignalIcon, C.MomentSignal));
        }
        string cause = i.Cause();
        if (i.Survived) all.Add((i.TEnd, C.PriorityFixed, C.MomentSplashIcon, C.MomentSplash));
        else all.Add((i.TEnd, C.PriorityFixed, C.MomentLostIcon, cause == "" ? C.MomentLostOther : string.Format(C.MomentLost, C.CauseName(cause))));

        return all.OrderBy(x => x.pr).ThenBy(x => x.t).Take(EndingTokens.MaxMoments)
                  .OrderBy(x => x.t).ThenBy(x => x.pr).ToList();
    }
}
