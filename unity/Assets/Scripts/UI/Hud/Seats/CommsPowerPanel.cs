// 書いた人：Claude ／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で CabinHud から分けた）
// 作る Issue：#82（Comms & Power のパネル）
//
// Comms & Power：船のスイッチ4つ（1枚のパネル、#102）・送信（TRANSMIT）・点火（IGNITE、Pilot と同時押し）。見た目は仮
using System.Collections.Generic;
using UnityEngine;

public class CommsPowerPanel : SeatPanel
{
    public override string Seat => Seats.Comms;
    public override IEnumerable<string> Commands => new[] { "switch", "set_transmit", "ignite" };

    static readonly string[] ShipSwitches = { "heater", "lights", "nav", "comms" };   // server/game/seats.py の SHIP_SWITCHES

    protected override void BuildControls(RectTransform row)
    {
        string me = Seat;
        var sw = Ui.HBox(row, 6, 0, TextAnchor.MiddleLeft, true); Ui.Size(sw, flexW: 3);
        foreach (var n in ShipSwitches) AddToggle(sw, n.ToUpper(), st => st.Switch(n), v => Cmd.Switch(me, n, v));
        AddToggle(row, "TRANSMIT", st => st.transmit, v => Cmd.Transmit(me, v));
        IgniteButton(row, "with Pilot");
    }
}
