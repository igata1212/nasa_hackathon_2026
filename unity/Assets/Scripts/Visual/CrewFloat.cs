// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
//
// クルーを無重力で浮かせる（#55）。置いた位置（localPosition）を中心に、ゆっくり上下に揺れて、少しだけ向きが揺れる。
// 浮かせる高さは置く側が決める（LookDev では、腕を前に約 −90° 回した姿で手がコンソールの天板 0.80m に届く高さ。
// LookDevScene.CrewFloatHeight）。この部品は Play のときだけ動き、エディタで置いた姿はそのまま残る。
using UnityEngine;

public class CrewFloat : MonoBehaviour
{
    [Tooltip("上下の揺れ幅（m、片側）")] public float amplitude = 0.03f;
    [Tooltip("上下に1往復する秒数")] public float period = 3.2f;
    [Tooltip("左右に向きが揺れる角度（度、片側）")] public float yawSwing = 4f;
    [Tooltip("横に傾く角度（度、片側）")] public float rollSwing = 2f;
    [Tooltip("揺れの位相（0〜1）。4人がそろって揺れないように、置く側でずらす")] [Range(0f, 1f)] public float phase;

    Vector3 basePos;
    Quaternion baseRot;

    void Start()
    {
        basePos = transform.localPosition;
        baseRot = transform.localRotation;
    }

    void Update()
    {
        float t = (Time.time / Mathf.Max(period, 0.1f) + phase) * Mathf.PI * 2f;
        transform.localPosition = basePos + Vector3.up * (Mathf.Sin(t) * amplitude);
        transform.localRotation = baseRot * Quaternion.Euler(0f, Mathf.Sin(t * 0.5f + 1.3f) * yawSwing, Mathf.Sin(t * 0.7f) * rollSwing);
    }
}
