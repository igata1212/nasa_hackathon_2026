// 書いた人：Claude ／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で CabinHud から分けた）
// 作る Issue：#78（軌道の地図）
//
// 右上の軌道の小窓。GameFlow.trajectory（TrajectoryView、船内から遠くに置いた GMAT の地図）を専用カメラで描いた RenderTexture を出す。
// カメラが無ければ（Ctx.TrajectoryRt == null）何も出さない。線の色・船の位置は Visual/TrajectoryView.cs。見た目は仮
// Detail：小窓全体（照明 OFF で暗くしてよい）
using UnityEngine;
using UnityEngine.UI;

public class MapWidget : HudPart
{
    protected override void Build(RectTransform root)
    {
        if (Ctx.TrajectoryRt == null) return;
        RootAsDetail();
        var frame = Ui.PanelImg(root, new Color(0.07f, 0.1f, 0.2f, 0.8f), "TrajectoryMini"); Ui.Fill(frame.rectTransform);
        var img = Ui.Rect("Map", frame.transform); Ui.Anchor(img, 0, 0, 1, 1, 4, 4, 4, 22);
        img.gameObject.AddComponent<RawImage>().texture = Ctx.TrajectoryRt;
        var cap = Ui.Label(frame.transform, "TRAJECTORY", 16, Ui.Orange, TextAnchor.UpperLeft, FontStyle.Bold);
        Ui.Anchor(cap.rectTransform, 0, 0.88f, 1, 1, 8, 0, 8, 2);
    }
}
