// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-15
// 透明度を別の物に合わせる（#190）。手（UiSelect の UiCursor）は画面の Root の直下にあるので、指している部品が
// UiPopIn でふわっと出る間も先に見えてしまう。手の CanvasGroup の alpha を、部品の CanvasGroup（UiPopIn が付ける）に毎フレーム合わせる
using UnityEngine;

public class UiAlphaLink : MonoBehaviour
{
    public CanvasGroup source;
    CanvasGroup cg;

    /// <summary>target の透明度を source（の CanvasGroup。無ければ付ける）に合わせる。もう付いていれば source だけ替える</summary>
    public static UiAlphaLink Add(Component target, Component source)
    {
        if (target == null || source == null) return null;
        var l = target.GetComponent<UiAlphaLink>();
        if (l == null) l = target.gameObject.AddComponent<UiAlphaLink>();
        var s = source.GetComponent<CanvasGroup>();
        if (s == null) s = source.gameObject.AddComponent<CanvasGroup>();
        l.source = s;
        l.LateUpdate();
        return l;
    }

    void LateUpdate()
    {
        if (cg == null) { cg = GetComponent<CanvasGroup>(); if (cg == null) cg = gameObject.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false; }
        cg.alpha = source != null && source.isActiveAndEnabled ? source.alpha : 1f;
    }
}
