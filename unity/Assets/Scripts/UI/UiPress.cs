// 書いた人：iizuka（作成：Claude）／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-14、#141）／ 更新：iizuka（Opus 5.5、2026-11-15、#190 音だけにした）
// 押した瞬間に ui_click を鳴らす（#141。GameAudio が無ければ鳴らない。押せないボタンは鳴らさない）。UiKit のボタンが付ける。
// #190：影をやめたので Body は下げない。押したときの弾みは UiSquish（UiKit が全ボタンに付ける）
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UiPress : MonoBehaviour, IPointerDownHandler
{
    Selectable target;

    /// <summary>body は呼び出しの形のために残す（使わない）</summary>
    public void Init(RectTransform body, Selectable target) => this.target = target;

    public void OnPointerDown(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        if (target != null && !target.IsInteractable()) return;
        GameAudio.Click();
    }
}
