// 書いた人：iizuka（作成：Sonnet 5.5）／ 最終更新：2026-11-14
//
// 船内に浮いている小物を、ゆっくり回しながら漂わせる（#64）。置いた位置・向きを中心に動く。Play のときだけ動き、エディタで置いた姿はそのまま残る。
// 位置は軸ごとに周期（4〜8 秒）と位相をずらして数 cm〜十数 cm 揺れ、回転は毎秒 5〜15° でランダムな軸のまわりを回り続ける。
using UnityEngine;

public class FloatProp : MonoBehaviour
{
    [Tooltip("個体差の種。違う値にすると動きが変わる")] public int seed = 1;
    [Tooltip("揺れ幅の最大（m、片側）")] public float maxAmplitude = 0.12f;

    Vector3 basePos;
    Quaternion baseRot;
    Vector3 amp, period, phase, spinAxis;
    float spinSpeed;

    void Start()
    {
        basePos = transform.localPosition;
        baseRot = transform.localRotation;
        var rng = new System.Random(seed * 7919 + 13);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        amp = new Vector3(R(0.04f, maxAmplitude), R(0.04f, maxAmplitude), R(0.04f, maxAmplitude));
        period = new Vector3(R(4f, 8f), R(4f, 8f), R(4f, 8f));
        phase = new Vector3(R(0f, 6.283f), R(0f, 6.283f), R(0f, 6.283f));
        spinAxis = new Vector3(R(-1f, 1f), R(-1f, 1f), R(-1f, 1f)).normalized;
        if (spinAxis.sqrMagnitude < 0.01f) spinAxis = Vector3.up;
        spinSpeed = R(5f, 15f);
    }

    void Update()
    {
        float t = Time.time;
        var off = new Vector3(
            Mathf.Sin(t / period.x * Mathf.PI * 2f + phase.x) * amp.x,
            Mathf.Sin(t / period.y * Mathf.PI * 2f + phase.y) * amp.y,
            Mathf.Sin(t / period.z * Mathf.PI * 2f + phase.z) * amp.z);
        transform.localPosition = basePos + off;
        transform.localRotation = Quaternion.AngleAxis(t * spinSpeed, spinAxis) * baseRot;
    }
}
