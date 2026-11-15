// 書いた人：Taiga（作成：Fable 5.2）／ 最終更新：2026-11-15
//
// 「V キーで視点を替えられる」の小さい札（キーの形の V ＋ Change view）。SeatView の V キー（一人称 ⇔ 見下ろし）の案内。
// 下の席の盆（CabinHud.Layout の Bottom）の上の端の外・左寄りに付ける（右寄りの BottomFold のつまみと対）。盆と一緒に動く。
// 押せない（案内だけ）。切り替えそのものは SeatView がする。
using TMPro;
using UnityEngine;
using T = UiTokens;

public class ViewHint : HudPart
{
    // ------------------------------------------------------------------ 直しやすい所
    public const string Key = "V", Label = "Change view";
    const float W = 148f, H = 30f;           // 札（1600×900 基準。高さは BottomFold のつまみと同じ）
    const float X = 8f;                      // 盆の左の端からの距離
    const float KeySize = 20f, KeyX = 8f;    // キーの形
    const float FontKey = 15f, FontLabel = 16f;

    protected override void Build(RectTransform root)
    {
        var slot = Ctx.Hud != null ? Ctx.Hud.Slot(HudSlot.Bottom) : null;
        if (slot == null) return;

        var tab = Ui.Rect("ViewHint", slot);
        tab.anchorMin = tab.anchorMax = new Vector2(0f, 1f); tab.pivot = new Vector2(0f, 0f);
        tab.sizeDelta = new Vector2(W, H); tab.anchoredPosition = new Vector2(X, -2f);
        var face = UiKit.Round(tab, 12f, T.HudFace, "Face"); UiKit.Fill(face.rectTransform);
        face.raycastTarget = false;

        var key = UiKit.Round(tab, 6f, T.HudText, "Key"); key.raycastTarget = false;
        var kr = key.rectTransform;
        kr.anchorMin = kr.anchorMax = new Vector2(0f, 0.5f); kr.pivot = new Vector2(0f, 0.5f);
        kr.sizeDelta = new Vector2(KeySize, KeySize); kr.anchoredPosition = new Vector2(KeyX, 0f);
        var k = UiKit.Text(kr, Key, TextStyle.Label, T.HudFace, TextAlignmentOptions.Center, false);
        k.fontSize = FontKey; UiKit.Fill(k.rectTransform);

        var t = UiKit.Text(tab, Label, TextStyle.Label, T.HudText, TextAlignmentOptions.MidlineLeft, false);
        t.fontSize = FontLabel;
        var tr = t.rectTransform;
        tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 1f);
        tr.offsetMin = new Vector2(KeyX + KeySize + 8f, 0f); tr.offsetMax = new Vector2(-6f, 0f);
    }
}
