// 書いた人：iizuka（作成：Claude）／ 最終更新：2026-11-14
// 押したら本体を下げて、影を 6 → 2 に縮める（UI ガイドライン 2章「影」）。UiKit のボタンが付ける
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UiPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    RectTransform body;
    Selectable target;
    Vector2 rest;
    bool down;

    public void Init(RectTransform body, Selectable target)
    {
        this.body = body; this.target = target;
        rest = body.anchoredPosition;
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || body == null) return;
        if (target != null && !target.IsInteractable()) return;
        down = true;
        body.anchoredPosition = rest + Vector2.down * (UiTokens.ShadowY - UiTokens.ShadowPressedY);
    }

    public void OnPointerUp(PointerEventData e) => Release();
    public void OnPointerExit(PointerEventData e) => Release();
    void OnDisable() => Release();   // 押したまま画面が切り替わっても、沈んだまま残さない

    void Release()
    {
        if (!down) return;
        down = false;
        body.anchoredPosition = rest;
    }
}
