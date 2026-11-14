// 書いた人：Claude ／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#130）
// Ending（結末 ED-A/B/C・要件4つ）と Report（スコアの内訳）。見た目は仮
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EndingScreen
{
    public readonly GameObject Root;
    readonly GameFlow f;
    readonly Text title, subtitle, reqs, score;

    public EndingScreen(Transform parent, GameFlow f)
    {
        this.f = f;
        var bg = Ui.PanelImg(parent, new Color(0.05f, 0.08f, 0.16f, 0.82f), "Ending"); Root = bg.gameObject; Ui.Fill(bg.rectTransform);
        title = Ui.Label(bg.transform, "", 84, Ui.Cream, TextAnchor.MiddleCenter, FontStyle.Bold);
        Ui.Anchor(title.rectTransform, 0, 0.7f, 1, 0.9f);
        subtitle = Ui.Label(bg.transform, "", 34, Ui.Orange, TextAnchor.MiddleCenter);
        Ui.Anchor(subtitle.rectTransform, 0, 0.62f, 1, 0.7f);
        reqs = Ui.Label(bg.transform, "", 34, Ui.Cream, TextAnchor.UpperLeft);
        Ui.Anchor(reqs.rectTransform, 0.3f, 0.25f, 0.8f, 0.6f);
        score = Ui.Label(bg.transform, "", 44, Ui.Yellow, TextAnchor.MiddleCenter, FontStyle.Bold);
        Ui.Anchor(score.rectTransform, 0, 0.15f, 1, 0.25f);
        var go = Ui.Btn(bg.transform, "SEE REPORT", Ui.Orange, () => f.Client.Send(Cmd.Type("next")), 34);
        Ui.Anchor((RectTransform)go.transform, 0.38f, 0.04f, 0.62f, 0.13f);
    }

    public void Refresh()
    {
        var r = f.Model.Result;
        if (r == null) return;
        string e = J.Str(r, "ending");
        title.text = e == "ED-A" ? "MISSION SUCCESS" : e == "ED-B" ? "CREW SURVIVED" : "CREW LOST";
        title.color = e == "ED-A" ? Ui.Green : e == "ED-B" ? Ui.Yellow : Ui.Red;
        subtitle.text = e == "ED-C" ? e : e + "   splashdown at T+" + Ui.Mmss(J.Num(r, "t_end"));   // ED-C は着水していない（#130）
        reqs.text = ReqText(r);
        score.text = "SCORE  " + J.Num(r, "score").ToString("0.0");
    }

    public static string ReqText(Dictionary<string, object> r)
    {
        var q = J.Obj(r, "requirements");
        if (q == null) return "";
        string Line(string k, string label) => (J.Bool(q, k) ? "[PASS] " : "[FAIL] ") + label + "\n";
        return Line("req1_crew_alive", "Crew returns alive") + Line("req2_science", "Science target met") +
               Line("req3_mass", "Within mass limit") + Line("req4_budget", "Within budget");
    }
}

public class ReportScreen
{
    public readonly GameObject Root;
    readonly GameFlow f;
    readonly Text left, right;

    public ReportScreen(Transform parent, GameFlow f)
    {
        this.f = f;
        var bg = Ui.PanelImg(parent, Ui.Navy, "Report"); Root = bg.gameObject; Ui.Fill(bg.rectTransform);
        var t = Ui.Label(bg.transform, "MISSION REPORT", 56, Ui.Cream, TextAnchor.MiddleCenter, FontStyle.Bold);
        Ui.Anchor(t.rectTransform, 0, 0.88f, 1, 1);
        left = Ui.Label(bg.transform, "", 32, Ui.Cream, TextAnchor.UpperLeft);
        Ui.Anchor(left.rectTransform, 0.06f, 0.2f, 0.5f, 0.86f);
        right = Ui.Label(bg.transform, "", 32, Ui.Cream, TextAnchor.UpperLeft);
        Ui.Anchor(right.rectTransform, 0.52f, 0.2f, 0.95f, 0.86f);
        var go = Ui.Btn(bg.transform, "PLAY AGAIN", Ui.Orange, () => f.Client.Send(Cmd.Type("again")), 34);
        Ui.Anchor((RectTransform)go.transform, 0.38f, 0.05f, 0.62f, 0.15f);
    }

    public void Refresh()
    {
        var rep = f.Model.Report;
        if (rep == null) return;
        var r = J.Obj(rep, "result");
        left.text = "REQUIREMENTS\n" + EndingScreen.ReqText(r) +
                    "\nFINAL\nPower " + J.Num(r, "power") + "   Oxygen " + J.Num(r, "oxygen") + "\nFuel " + J.Num(r, "fuel") + "   Science " + J.Num(r, "science");
        var b = J.Obj(rep, "score_breakdown");
        string s = "SCORE BREAKDOWN\n";
        if (b != null)
            foreach (var kv in b) s += Pretty(kv.Key) + "  " + ((double)kv.Value).ToString("0.0") + "\n";
        s += "\nTOTAL  " + J.Num(r, "score").ToString("0.0");
        right.text = s;
    }

    static string Pretty(string k) => k == "all_requirements" ? "All requirements" : char.ToUpper(k[0]) + k.Substring(1);
}
