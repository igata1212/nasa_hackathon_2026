// 書いた人：Claude ／ 最終更新：2026-11-14
// 演出1本ぶんの舞台（#124）。船内から遠く離れた所に組んだ物を、StoryDirector のカメラで撮る。
// 動きは「始まってからの秒 t」だけで決める（Pose(t)）。同じ t なら同じ絵になるので、止めてスクリーンショットも撮れる。
using UnityEngine;

public abstract class StoryStage : MonoBehaviour
{
    /// <summary>演出の長さ（秒）。最後は真っ黒（フェード 1）で終える</summary>
    public abstract float Length { get; }

    /// <summary>始める前の準備（表示する・初期の姿に戻す）</summary>
    public virtual void Begin(StoryDirector d) { gameObject.SetActive(true); }

    /// <summary>t 秒の姿にする（カメラ・物・字幕・フェード）</summary>
    public abstract void Pose(StoryDirector d, float t);

    /// <summary>終わり（飛ばしたときも呼ぶ）。隠して粒を消す</summary>
    public virtual void End(StoryDirector d)
    {
        foreach (var ps in GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        gameObject.SetActive(false);
    }

    // ---- 共通の小道具 ----
    /// <summary>a〜b 秒を 0〜1 にする（外は 0 か 1）</summary>
    public static float Win(float t, float a, float b) => Mathf.Clamp01((t - a) / (b - a));
    public static float Ease(float x) => x * x * (3f - 2f * x);
    public static float EaseOut(float x) => 1f - (1f - x) * (1f - x);
    public static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float y = x - 1f;
        return 1f + c3 * y * y * y + c1 * y * y;
    }

    /// <summary>粒を、t がしきい値を越えたときに1回だけ始める</summary>
    protected static void PlayOnce(ParticleSystem ps, bool on)
    {
        if (ps == null) return;
        if (on && !ps.isPlaying) ps.Play(true);
        if (!on && ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    /// <summary>クルーの目を1つだけ出す（Eyes_Normal・Eyes_Panic・Eyes_Relief）</summary>
    protected static void Eyes(Transform crew, string which)
    {
        if (crew == null) return;
        foreach (var r in crew.GetComponentsInChildren<Renderer>(true))
            if (r.name.Contains(".Eyes_")) r.enabled = r.name.EndsWith(which);
    }

    protected static Transform Deep(Transform t, string suffix)
    {
        if (t == null) return null;
        if (t.name.EndsWith(suffix)) return t;
        foreach (Transform c in t) { var f = Deep(c, suffix); if (f != null) return f; }
        return null;
    }
}
