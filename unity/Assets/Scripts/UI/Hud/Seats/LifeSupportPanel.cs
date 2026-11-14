// 書いた人：Claude ／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で CabinHud から分けた）
// 作る Issue：#81（Life Support のパネル）。かき混ぜのボタン（BuildStir）の見た目は #84 と合わせる
//
// Life Support：バルブを閉じる（タンク 1 か 2、#100）・CO2 フィルター交換（ほかの1席と同時押し）。stir の段階はかき混ぜ（STIR）だけ。見た目は仮
using System.Collections.Generic;
using UnityEngine;

public class LifeSupportPanel : SeatPanel
{
    public override string Seat => Seats.Life;
    public override IEnumerable<string> Commands => new[] { "close_valve", "swap_filter", "stir" };

    protected override void BuildControls(RectTransform row)
    {
        string me = Seat;
        Ui.Size(Ui.Btn(row, "CLOSE VALVE 1", Tint, () => Ctx.Send(Cmd.Valve(me, 1)), 26), flexW: 1);
        Ui.Size(Ui.Btn(row, "CLOSE VALVE 2", Tint, () => Ctx.Send(Cmd.Valve(me, 2)), 26), flexW: 1);
        string label = Ctx.SoloPair("swap_filter") != null ? "SWAP FILTER (both seats)" : "SWAP CO2 FILTER (with a partner)";
        Ui.Size(Ui.Btn(row, label, Tint, () => Ctx.SendDual("swap_filter", me), 22), flexW: 1);
    }

    protected override void BuildStir(RectTransform row)
    {
        string me = Seat;
        Ui.Btn(row, "STIR", Tint, () => Ctx.Send(Cmd.Simple("stir", me)), 40);
    }
}
