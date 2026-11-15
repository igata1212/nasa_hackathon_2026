// 書いた人：Taiga（作成：Opus 5.5）／ 最終更新：2026-11-15
// 作る Issue：#167（船内を広く見せる：下の席のパネルをしまえるようにする）
//
// 下の領域（CabinHud.Layout の Bottom：席タブ＋今の席の操作の盆）を、矢印のつまみで画面の下へしまう・出す。
// つまみは Bottom の slot の上の端の外（右寄り）に付いていて、盆と一緒に動く。しまうと、つまみだけが画面の下の端に残る。
// 同時押しの合図（press）が来たら自分から出す（操作がしまわれたままだと押せないので）。
// この部品は Bottom の slot を上下に動かすだけで、中の部品（SeatTabBar・SeatPanel）には触らない。
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;

public class BottomFold : HudPart
{
    // ------------------------------------------------------------------ 直しやすい所
    const float TabW = 72f, TabH = 30f;      // つまみ（1600×900 基準）
    const float TabX = 0.94f;                // つまみの横の位置（盆の幅の割合）
    const float Speed = 5f;                  // 開け閉めの速さ（1/秒）

    RectTransform slot, tab, caret;
    bool open = true;
    float slide = 1f;   // 1 = 出ている、0 = しまっている

    protected override void Build(RectTransform root)
    {
        slot = Ctx.Hud != null ? Ctx.Hud.Slot(HudSlot.Bottom) : null;
        if (slot == null) return;

        tab = Ui.Rect("FoldTab", slot);
        tab.anchorMin = tab.anchorMax = new Vector2(TabX, 1f); tab.pivot = new Vector2(0.5f, 0f);
        tab.sizeDelta = new Vector2(TabW, TabH); tab.anchoredPosition = new Vector2(0f, -2f);
        var face = UiKit.Round(tab, 12f, T.HudFace, "Face"); UiKit.Fill(face.rectTransform);
        face.raycastTarget = true;
        var btn = tab.gameObject.AddComponent<Button>();
        btn.targetGraphic = face; btn.transition = Selectable.Transition.None;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        btn.onClick.AddListener(() => open = !open);
        var sp = T.Theme != null ? T.Theme.caretDown : null;
        var ci = UiKit.Shape(tab, sp, T.HudText, 0f, "Arrow");
        caret = ci.rectTransform;
        caret.anchorMin = caret.anchorMax = new Vector2(0.5f, 0.5f); caret.pivot = new Vector2(0.5f, 0.5f);
        caret.sizeDelta = new Vector2(22f, 13f); caret.anchoredPosition = Vector2.zero;
        ci.raycastTarget = false;
    }

    public override void OnEvent(GameEvent e)
    {
        if (e.kind == "press") open = true;   // 同時押しは下のボタンで押すので、しまっていたら出す
    }

    public override void Tick(float now)
    {
        if (slot == null) return;
        slide = Mathf.MoveTowards(slide, open ? 1f : 0f, Time.unscaledDeltaTime * Speed);
        float e = slide * slide * (3f - 2f * slide);
        // しまうとき：盆の上の端が画面の下の端に来るまで下げる（盆の高さ＋下のすき間）
        var parent = slot.parent as RectTransform;
        float below = parent != null ? slot.anchorMin.y * parent.rect.height : 0f;
        float drop = (slot.rect.height + below) * (1f - e);
        var p = slot.anchoredPosition;
        if (!Mathf.Approximately(p.y, -drop)) slot.anchoredPosition = new Vector2(p.x, -drop);
        caret.localRotation = Quaternion.Euler(0f, 0f, open ? 0f : 180f);   // 出ているとき ▼（しまう）、しまっているとき ▲（出す）
    }
}
