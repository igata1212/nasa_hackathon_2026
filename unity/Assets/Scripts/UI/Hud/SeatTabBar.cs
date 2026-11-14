// 書いた人：Claude ／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で CabinHud から分けた）
// 作る Issue：#71（席タブの見た目：SeatTabs）・#94（ほかの人が切れたときの印）
//
// 下のパネルの上の段：自分の席のタブ（兼任の人は席を切り替える、#102）。押すと Ctx.SelectSeat で CabinHud が席のパネルを作り直す。見た目は仮
// 作り直す時期は CabinHud が決める（席・段階が変わったとき、席の数が変わったとき）。Count はそのための今のタブの数
using UnityEngine;

public class SeatTabBar : HudPart
{
    RectTransform row;
    /// <summary>今あるタブの数</summary>
    public int Count => row != null ? row.childCount : 0;

    protected override void Build(RectTransform root)
    {
        row = Ui.HBox(root, 8, 0, TextAnchor.MiddleLeft, false); Ui.Fill(row);
    }

    /// <summary>自分の席（Model.MySeats）のタブを作り直す。選んでいる席（Ctx.ActiveSeat）を席の色にする</summary>
    public void Rebuild()
    {
        Clear(row);
        foreach (var s in Ctx.Model.MySeats)
        {
            string seat = s;
            bool on = seat == Ctx.ActiveSeat;
            var b = Ui.Btn(row, Seats.Icon(seat) + " " + Seats.Title(seat), on ? Seats.Tint(seat) : Ui.Gray, () => Ctx.SelectSeat(seat), 22, on ? Ui.Navy : Ui.Cream);
            Ui.Size(b, w: 300);
        }
    }
}
