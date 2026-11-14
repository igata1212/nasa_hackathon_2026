// 書いた人：Claude ／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で CabinHud から分けた）
// 作る Issue：#84（練習の案内とかき混ぜ）
//
// 中央上の案内：練習（M03、練習した席の数 n/4）とかき混ぜ（M04）。見た目は仮
// 練習した席は Model.Practiced（practice イベントの done。GameFlow が入れる）。「まだ試していない操作」を光らせるのは SeatPanel.Highlight
// かき混ぜのボタンは LifeSupportPanel.BuildStir
using UnityEngine;
using UnityEngine.UI;

public class PracticeGuide : HudPart
{
    Text banner;

    protected override void Build(RectTransform root)
    {
        banner = Ui.Label(root, "", 34, Ui.Yellow, TextAnchor.MiddleCenter, FontStyle.Bold); Ui.Fill(banner.rectTransform);
        var ol = banner.gameObject.AddComponent<Outline>();   // 船室の明るい壁の上でも読めるように縁取り
        ol.effectColor = new Color(0.03f, 0.05f, 0.12f, 0.95f); ol.effectDistance = new Vector2(2f, -2f);
    }

    public override void Refresh(GameState s)
    {
        var m = Ctx.Model;
        if (m.MySeats.Count == 0) return;
        string ph = m.Phase;
        banner.text = ph == "practice" ? "PRACTICE: one control per seat  (" + m.Practiced.Count + "/4)"
                    : ph == "stir" ? "LIFE SUPPORT: STIR THE OXYGEN TANK!" : "";
    }
}
