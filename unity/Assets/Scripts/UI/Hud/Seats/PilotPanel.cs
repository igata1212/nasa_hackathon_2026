// 書いた人：Claude ／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で CabinHud から分けた）
// 作る Issue：#80（Pilot のパネル）
//
// Pilot：船の向き5つ（選択1つ、#102）・軌道修正（CORRECTION BURN）・点火（IGNITE、Comms & Power と同時押し）。見た目は仮
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PilotPanel : SeatPanel
{
    public override string Seat => Seats.Pilot;
    public override IEnumerable<string> Commands => new[] { "set_attitude", "correction_burn", "ignite" };

    static readonly string[] Attitudes = { "earth", "moon", "sun", "burn", "heatshield" };
    readonly Dictionary<string, Button> attBtns = new Dictionary<string, Button>();

    protected override void BuildControls(RectTransform row)
    {
        string me = Seat;
        var att = Ui.HBox(row, 6, 0, TextAnchor.MiddleLeft, true); Ui.Size(att, flexW: 3);
        foreach (var a in Attitudes) { string aa = a; attBtns[a] = Ui.Btn(att, a.ToUpper(), Ui.Gray, () => Ctx.Send(Cmd.Attitude(me, aa)), 20, Ui.Cream); }
        Ui.Size(Ui.Btn(row, "CORRECTION BURN", Tint, () => Ctx.Send(Cmd.Simple("correction_burn", me)), 24), flexW: 1);
        IgniteButton(row, "with Comms");
    }

    public override void Refresh(GameState s)
    {
        base.Refresh(s);
        if (s == null) return;
        foreach (var kv in attBtns) { bool on = s.attitude == kv.Key; Ui.SetBtn(kv.Value, kv.Key.ToUpper(), on ? Tint : Ui.Gray, on ? Ui.Navy : Ui.Cream); }
    }
}
