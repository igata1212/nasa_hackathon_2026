// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// 演出（LaunchStage・ReturnStage・LostStage）から時刻で音を鳴らす小さな API（#141）。実体は GameAudio（無ければ何もしない）。
// 舞台の動きは「始まってからの秒 t」だけで決まる（Pose(t)）ので、音も t で決める：
//   StoryAudio.At(2.4f, "splash")            t が 2.4 秒をまたいだフレームに1回鳴らす（止めて同じ t を繰り返しても鳴り直さない）
//   StoryAudio.Loop("burn", "thruster_burn_loop", vol, pitch)   毎フレーム大きさを決める（0 で止まる。t から計算するので巻き戻しても同じ）
//   StoryAudio.Hush()                        演出が鳴らした単発を全部止める（点火の直前の「溜め」）
//   StoryAudio.Music("flight", 0.5f)         演出の途中から BGM を入れる（用途の名前。GameAudio.Slot）。演出が終わったら段階の決まりに戻る
//   StoryAudio.EndingJingle()                エンディングのジングル（ED-A／B／C で選ぶ）
// StoryDirector が Begin（始め）・Frame（毎フレーム、Pose の前）・FadeOut（飛ばして黒にする間）・End（片づけ）を呼ぶ。
public static class StoryAudio
{
    static float prev = -1f, cur = -1f;

    /// <summary>演出が始まる（StoryDirector.Play）</summary>
    public static void Begin()
    {
        prev = cur = -1f;
        var a = GameAudio.I; if (a != null) a.StoryBegin();
    }

    /// <summary>このフレームの t（StoryDirector が Pose の前に呼ぶ）。t が戻ったら最初から数え直す</summary>
    public static void Frame(float t)
    {
        prev = t < cur ? -1f : cur;
        cur = t;
    }

    /// <summary>飛ばしたとき、黒になる間にループを消す</summary>
    public static void FadeOut(float seconds) { var a = GameAudio.I; if (a != null) a.StoryFadeOut(seconds); }

    /// <summary>片づけ（演出の音をすぐ止める）</summary>
    public static void End()
    {
        prev = cur = -1f;
        var a = GameAudio.I; if (a != null) a.StoryStop();
    }

    /// <summary>このフレームで t が at 秒をまたいだか</summary>
    public static bool Crossed(float at) => prev < at && at <= cur;

    /// <summary>t が at 秒をまたいだフレームに1回鳴らす</summary>
    public static void At(float at, string clip, float vol = 1f, float pitch = 1f) { if (Crossed(at)) Play(clip, vol, pitch); }

    public static void Play(string clip, float vol = 1f, float pitch = 1f) { var a = GameAudio.I; if (a != null) a.StoryPlay(clip, vol, pitch); }

    /// <summary>ループの大きさ（毎フレーム呼ぶ。0 で止める）。key ごとに1つ（同じ clip を違うピッチで2つ鳴らすときは key を変える）</summary>
    public static void Loop(string key, string clip, float vol, float pitch = 1f) { var a = GameAudio.I; if (a != null) a.StoryLoop(key, clip, vol, pitch); }

    public static void Hush() { var a = GameAudio.I; if (a != null) a.StoryHush(); }

    public static void Music(string slot, float fadeS = 0.5f) { var a = GameAudio.I; if (a != null) a.StoryMusic(slot, fadeS); }

    public static void EndingJingle() { var a = GameAudio.I; if (a != null) a.EndingJingle(); }
}
