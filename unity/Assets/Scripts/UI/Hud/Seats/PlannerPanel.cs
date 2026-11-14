// 書いた人：Claude ／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で CabinHud から分けた）
// 作る Issue：#83（Mission Planner のパネル）
//
// Mission Planner：積んだ観測機器のスイッチ（設計で積んだものだけ）・帰還ルート A／B（選択1つ）。見た目は仮
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlannerPanel : SeatPanel
{
    public override string Seat => Seats.Planner;
    public override IEnumerable<string> Commands => new[] { "switch", "set_route" };

    static readonly string[] Instruments = { "camera", "spectrometer", "radar" };   // reference_sim の INSTRUMENTS
    static readonly string[] Routes = { "a", "b" };
    readonly Dictionary<string, Button> routeBtns = new Dictionary<string, Button>();

    protected override void BuildControls(RectTransform row)
    {
        string me = Seat;
        var ins = Ui.HBox(row, 6, 0, TextAnchor.MiddleLeft, true); Ui.Size(ins, flexW: 3);
        foreach (var n in Instruments)
            if (Ctx.HasPart(n)) AddToggle(ins, n.ToUpper(), st => st.Switch(n), v => Cmd.Switch(me, n, v));
        var rt = Ui.HBox(row, 6, 0, TextAnchor.MiddleLeft, true); Ui.Size(rt, flexW: 2);
        foreach (var r in Routes) { string rr = r; routeBtns[r] = Ui.Btn(rt, "ROUTE " + r.ToUpper(), Ui.Gray, () => Ctx.Send(Cmd.Route(me, rr)), 24, Ui.Cream); }
    }

    public override void Refresh(GameState s)
    {
        base.Refresh(s);
        if (s == null) return;
        foreach (var kv in routeBtns) { bool on = s.route == kv.Key; Ui.SetBtn(kv.Value, "ROUTE " + kv.Key.ToUpper(), on ? Tint : Ui.Gray, on ? Ui.Navy : Ui.Cream); }
    }
}
