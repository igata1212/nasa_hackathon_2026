// 書いた人：Claude ／ 最終更新：2026-11-14
// Design：部品の ± と合計、軌道・ロケット、打ち上げ。部品の名前・質量・費用は Data/Sim/parts.json（サーバーと同じ元データ）から
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DesignScreen
{
    public readonly GameObject Root;
    readonly GameFlow f;
    class PartRow { public string id; public Text count; }
    readonly List<PartRow> rows = new List<PartRow>();
    readonly Dictionary<string, Button> trajBtns = new Dictionary<string, Button>(), rocketBtns = new Dictionary<string, Button>();
    readonly Text totals, status;

    public DesignScreen(Transform parent, GameFlow f)
    {
        this.f = f;
        var bg = Ui.PanelImg(parent, Ui.Navy, "Design"); Root = bg.gameObject; Ui.Fill(bg.rectTransform);
        var t = Ui.Label(bg.transform, "DESIGN YOUR SHIP", 54, Ui.Cream, TextAnchor.MiddleCenter, FontStyle.Bold);
        Ui.Anchor(t.rectTransform, 0, 0.9f, 1, 1);

        var catalog = f.partsJson != null ? MiniJson.ParseObject(f.partsJson.text) : new Dictionary<string, object>();
        var list = Ui.VBox(bg.transform, 6, 10);
        Ui.Anchor(list, 0.03f, 0.2f, 0.62f, 0.9f);
        var trajRow = Ui.HBox(list, 10, 0, TextAnchor.MiddleLeft, true); Ui.Size(trajRow, h: 52);
        var rocketRow = Ui.HBox(list, 10, 0, TextAnchor.MiddleLeft, true); Ui.Size(rocketRow, h: 52);
        Ui.Label(trajRow, "TRAJECTORY", 22, Ui.Orange);
        Ui.Label(rocketRow, "ROCKET", 22, Ui.Orange);
        foreach (var o in J.Arr(catalog, "parts") ?? new List<object>())
        {
            var p = (Dictionary<string, object>)o;
            string id = J.Str(p, "id"), name = J.Str(p, "name");
            if (id.StartsWith("trajectory_")) { string k = id.Substring(11); trajBtns[k] = Ui.Btn(trajRow, name, Ui.Gray, () => f.Client.Send(Cmd.DesignTrajectory(k)), 22, Ui.Cream); continue; }
            if (id.StartsWith("rocket_")) { string k = id.Substring(7); rocketBtns[k] = Ui.Btn(rocketRow, name, Ui.Gray, () => f.Client.Send(Cmd.DesignRocket(k)), 22, Ui.Cream); continue; }
            if (id == "bus") continue;
            var row = Ui.HBox(list, 10, 4, TextAnchor.MiddleLeft, false); Ui.Size(row, h: 46);
            var rowBg = row.gameObject.AddComponent<Image>(); rowBg.color = new Color(1, 1, 1, 0.06f);
            var nm = Ui.Label(row, name, 24); Ui.Size(nm, flexW: 1);
            var mc = Ui.Label(row, "m" + J.Num(p, "mass") + "  $" + J.Num(p, "cost"), 20, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleRight); Ui.Size(mc, w: 150);
            var minus = Ui.Btn(row, "-", Ui.Gray, () => f.Client.Send(Cmd.DesignPart(id, -1)), 28, Ui.Cream); Ui.Size(minus, w: 46);
            var cnt = Ui.Label(row, "0", 28, Ui.Cream, TextAnchor.MiddleCenter, FontStyle.Bold); Ui.Size(cnt, w: 40);
            var plus = Ui.Btn(row, "+", Ui.Teal, () => f.Client.Send(Cmd.DesignPart(id, +1)), 28, Ui.Cream); Ui.Size(plus, w: 46);
            rows.Add(new PartRow { id = id, count = cnt });
        }

        var side = Ui.PanelImg(bg.transform, Ui.Panel, "Totals");
        Ui.Anchor(side.rectTransform, 0.65f, 0.2f, 0.97f, 0.9f);
        var sideTitle = Ui.Label(side.transform, "TOTALS", 30, Ui.Orange, TextAnchor.MiddleLeft, FontStyle.Bold);
        Ui.Anchor(sideTitle.rectTransform, 0, 0.88f, 1, 1, 20);
        totals = Ui.Label(side.transform, "", 28, Ui.Cream);
        Ui.Anchor(totals.rectTransform, 0, 0.3f, 1, 0.88f, 20, 0, 10);
        status = Ui.Label(side.transform, "", 32, Ui.Green, TextAnchor.MiddleLeft, FontStyle.Bold);
        Ui.Anchor(status.rectTransform, 0, 0, 1, 0.3f, 20, 0, 10);

        var go = Ui.Btn(bg.transform, "READY FOR LAUNCH", Ui.Orange, () => f.Client.Send(Cmd.Type("launch")), 36);
        Ui.Anchor((RectTransform)go.transform, 0.62f, 0.04f, 0.97f, 0.16f);
    }

    public void Refresh()
    {
        var m = f.Model;
        if (m.Design == null) return;
        var parts = J.Obj(m.Design, "parts");
        foreach (var r in rows) r.count.text = J.Int(parts, r.id).ToString();
        string traj = J.Str(m.Design, "trajectory"), rocket = J.Str(m.Design, "rocket");
        foreach (var kv in trajBtns) Ui.SetBtn(kv.Value, kv.Value.GetComponentInChildren<Text>().text, kv.Key == traj ? Ui.Orange : Ui.Gray, kv.Key == traj ? Ui.Navy : Ui.Cream);
        foreach (var kv in rocketBtns) Ui.SetBtn(kv.Value, kv.Value.GetComponentInChildren<Text>().text, kv.Key == rocket ? Ui.Orange : Ui.Gray, kv.Key == rocket ? Ui.Navy : Ui.Cream);
        var tt = m.Totals;
        if (tt == null) return;
        totals.text = "Mass      " + J.Num(tt, "mass") + " / " + J.Num(tt, "mass_limit") +
                      "\nCost       " + J.Num(tt, "cost") + " / " + J.Num(tt, "budget") +
                      "\nPower      " + J.Num(tt, "capacity") +
                      "\nOxygen     " + J.Num(tt, "oxygen") +
                      "\nFuel         " + J.Num(tt, "fuel");
        string st = m.LaunchStatus != "" ? m.LaunchStatus : J.Str(tt, "status");
        status.text = st == "over_mass" ? "OVER MASS" : st == "over_budget" ? "OVER BUDGET" : "OK";
        status.color = st == "ok" ? Ui.Green : Ui.Red;
    }
}
