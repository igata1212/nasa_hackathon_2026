// 書いた人：iizuka（作成：Claude Opus 5.5）／ 最終更新：2026-11-14
// 押した・離したを関数で受ける（#71。長押しボタンが使う）。指が外へ出たときと、非表示になったときも「離した」にする
using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class UiPointer : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public Action Down, Up;
    bool down;

    public void OnPointerDown(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        down = true;
        Down?.Invoke();
    }

    public void OnPointerUp(PointerEventData e) => Release();
    public void OnPointerExit(PointerEventData e) => Release();
    void OnDisable() => Release();

    void Release()
    {
        if (!down) return;
        down = false;
        Up?.Invoke();
    }
}
