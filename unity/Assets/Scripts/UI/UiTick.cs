// 書いた人：iizuka（作成：Claude Opus 5.5）／ 最終更新：2026-11-14
// 毎フレーム、登録した関数を呼ぶだけ（#71）。HUD の部品（UiKitHud）の動き（輪・弾む・▼の上下・つまみ）に使う。
// 状態は部品のクラスが持ち、ここは時間を渡すだけ。止まっている物（非表示）は呼ばれない。時間は Time.timeScale に左右されない
using System;
using UnityEngine;

public class UiTick : MonoBehaviour
{
    public event Action<float> OnTick;

    void Update() => OnTick?.Invoke(Time.unscaledDeltaTime);

    /// <summary>go に UiTick を付けて（もうあればそれに）f を足す</summary>
    public static UiTick On(GameObject go, Action<float> f)
    {
        var t = go.GetComponent<UiTick>();
        if (t == null) t = go.AddComponent<UiTick>();
        t.OnTick += f;
        return t;
    }
}
