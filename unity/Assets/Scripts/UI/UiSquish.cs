// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// 押したときの気持ちよさ（#153）：押すと縮み、離すとばねで少し大きくなって戻る。マウスが乗ると少し大きく。
// 主ボタンは breathe でゆっくり息をする（1画面に1個の主ボタンへ目を向ける）。押せないときに押したら首を振り、onBlocked を呼ぶ。
// ボタンの Root（影ごと）の大きさと傾きだけを変える。UiPress（Body を下げて影を縮める）と一緒に使える。値は TitleLook
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UiSquish : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    public float breathe;          // 0 なら息をしない
    public Action onBlocked;       // 押せないときに押した

    Selectable target;
    bool down, over;
    float x = 1f, v, shakeT = -1f;

    public static UiSquish Add(UiButton b, float breathe = 0f, Action onBlocked = null)
    {
        var s = b.Root.gameObject.AddComponent<UiSquish>();
        s.target = b.Button; s.breathe = breathe; s.onBlocked = onBlocked;
        return s;
    }

    bool Live => target == null || target.IsInteractable();

    public void OnPointerDown(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        if (!Live) { shakeT = 0f; onBlocked?.Invoke(); return; }
        down = true;
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!down) return;
        down = false;
        v += TitleLook.ReleaseKick;
    }

    public void OnPointerEnter(PointerEventData e) => over = true;
    public void OnPointerExit(PointerEventData e) { over = false; down = false; }

    void OnDisable()
    {
        down = over = false; x = 1f; v = 0f; shakeT = -1f;
        transform.localScale = Vector3.one; transform.localRotation = Quaternion.identity;
    }

    void Update()
    {
        float dt = UiEase.Dt;
        bool live = Live;
        float goal = down && live ? TitleLook.PressScale : over && live ? TitleLook.HoverScale : 1f;
        // ばね：x'' = -w²(x - goal) - 2ζw x'
        float w = TitleLook.SpringHz * Mathf.PI * 2f;
        v += (-w * w * (x - goal) - 2f * TitleLook.SpringDamping * w * v) * dt;
        x += v * dt;

        float b = breathe > 0f && live && !down && !over ? 1f + breathe * Mathf.Sin(UiEase.Now * TitleLook.BreatheHz * Mathf.PI * 2f) : 1f;
        transform.localScale = new Vector3(x * b, x * b, 1f);

        float z = 0f;
        if (shakeT >= 0f)
        {
            shakeT += dt;
            float p = shakeT / TitleLook.ShakeDur;
            if (p >= 1f) shakeT = -1f;
            else z = Mathf.Sin(shakeT * TitleLook.ShakeHz * Mathf.PI * 2f) * TitleLook.ShakeDeg * (1f - p);
        }
        transform.localRotation = Quaternion.Euler(0f, 0f, z);
    }
}
