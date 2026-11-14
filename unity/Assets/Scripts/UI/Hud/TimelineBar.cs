// 書いた人：Claude ／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で CabinHud から分けた）
// 作る Issue：#77（タイムラインの帯）
//
// 上の帯：時計（T+）・月の裏側（通信途絶）のチップ・噴射窓のチップ。見た目は仮
// 時刻は state の t_los・t_aos・t_burn_open・t_burn_close・t_reentry・t_splashdown（#148 で A・B 両方の t_reentry_a など）
// Detail：帯全体（照明 OFF で暗くしてよい）
using UnityEngine;
using UnityEngine.UI;

public class TimelineBar : HudPart
{
    Text clock, chipFar, chipBurn;
    Image chipFarBg, chipBurnBg;

    protected override void Build(RectTransform root)
    {
        RootAsDetail();
        var top = Ui.PanelImg(root, new Color(0.07f, 0.1f, 0.2f, 0.7f), "TopBar"); Ui.Fill(top.rectTransform);
        clock = Ui.Label(top.transform, "T+ 0:00", 36, Ui.Cream, TextAnchor.MiddleLeft, FontStyle.Bold);
        Ui.Anchor(clock.rectTransform, 0.02f, 0, 0.2f, 1);
        chipFarBg = Ui.PanelImg(top.transform, Ui.Gray, "ChipFar");
        Ui.Anchor(chipFarBg.rectTransform, 0.25f, 0.1f, 0.55f, 0.9f);
        chipFar = Ui.Label(chipFarBg.transform, "", 26, Ui.Cream, TextAnchor.MiddleCenter, FontStyle.Bold); Ui.Fill(chipFar.rectTransform);
        chipBurnBg = Ui.PanelImg(top.transform, Ui.Gray, "ChipBurn");
        Ui.Anchor(chipBurnBg.rectTransform, 0.57f, 0.1f, 0.87f, 0.9f);
        chipBurn = Ui.Label(chipBurnBg.transform, "", 26, Ui.Cream, TextAnchor.MiddleCenter, FontStyle.Bold); Ui.Fill(chipBurn.rectTransform);
    }

    public override void Refresh(GameState s)
    {
        if (s == null) { clock.text = "T+ 0:00"; return; }   // 練習・かき混ぜの間（チップは前のまま）
        clock.text = "T+ " + Ui.Mmss(s.t);

        // 月の裏側と噴射窓
        if (s.t < s.tLos) Chip(chipFarBg, chipFar, "FAR SIDE (NO SIGNAL) IN " + Ui.Mmss(s.tLos - s.t), Ui.Gray);
        else if (s.t < s.tAos) Chip(chipFarBg, chipFar, "FAR SIDE  " + Ui.Mmss(s.tAos - s.t) + " LEFT", Ui.Red);
        else Chip(chipFarBg, chipFar, "SIGNAL RESTORED", Ui.Teal);
        if (s.burnDone) Chip(chipBurnBg, chipBurn, "BURN DONE", Ui.Teal);
        else if (s.t < s.tBurnOpen) Chip(chipBurnBg, chipBurn, "BURN WINDOW IN " + Ui.Mmss(s.tBurnOpen - s.t), Ui.Gray);
        else if (s.t < s.tBurnClose) Chip(chipBurnBg, chipBurn, "BURN WINDOW OPEN  " + Ui.Mmss(s.tBurnClose - s.t), Ui.Orange);
        else Chip(chipBurnBg, chipBurn, "BURN WINDOW CLOSED", Ui.Gray);
    }

    static void Chip(Image bg, Text t, string s, Color c) { t.text = s; bg.color = c; }
}
