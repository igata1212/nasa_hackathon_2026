// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// 音（#141。GameAudio）を Game.unity に組み立てる。シーンは手で直さず、ここを直して作り直す。
//   メニュー Tools/Audio/音を組み立てる … Game.unity を開いて「Audio」を作り直して保存する（GameFlow が要る）
//   GameFlowScene.BuildScene（Tools/GameFlow/シーンを作り直す）からは BuildInto(flow) を呼ぶ（Story・Urgency の後。StoryDirector・UrgencyFx を拾う）
// 音の参照はここで AssetDatabase から入れる：効果音は Assets/Audio/Sfx の全部、ジングルは Assets/Audio/Music の jingle_*、BGM は space_calm_01。
// 出典は CREDITS.md と各フォルダの SOURCES.md。AudioListener は足さない（メインカメラのものを使う）。
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AudioScene
{
    const string Tag = "[Audio] ";
    public const string SfxFolder = "Assets/Audio/Sfx";
    public const string MusicFolder = "Assets/Audio/Music";
    const string CalmPath = MusicFolder + "/space_calm_01.mp3";   // Spacedman（Shane Ivers, CC BY 4.0）

    [MenuItem("Tools/Audio/音を組み立てる")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    /// <summary>確認なしで Game.unity に組み立てて保存する（UnityMCP から呼ぶとき用）</summary>
    public static void Build()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != GameFlowScene.ScenePath) scene = EditorSceneManager.OpenScene(GameFlowScene.ScenePath, OpenSceneMode.Single);
        var flow = Object.FindAnyObjectByType<GameFlow>();
        if (flow == null) { Debug.LogError(Tag + "GameFlow が無い。先に Tools/GameFlow/シーンを作り直す"); return; }
        BuildInto(flow);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(Tag + GameFlowScene.ScenePath + " に音を組み立てた");
    }

    /// <summary>今のシーンに「Audio」を作り直す（保存はしない）</summary>
    public static GameAudio BuildInto(GameFlow flow)
    {
        var old = GameObject.Find("Audio");
        if (old != null) Object.DestroyImmediate(old);

        var go = new GameObject("Audio");
        var a = go.AddComponent<GameAudio>();
        a.flow = flow;
        a.urgency = Object.FindAnyObjectByType<UrgencyFx>();
        a.story = Object.FindAnyObjectByType<StoryDirector>();

        var list = new List<AudioClip>();
        list.AddRange(Clips(SfxFolder, null));
        list.AddRange(Clips(MusicFolder, "jingle_"));
        a.clips = list.OrderBy(c => c.name).ToArray();
        a.musicCalm = AssetDatabase.LoadAssetAtPath<AudioClip>(CalmPath);

        if (a.urgency == null) Debug.LogWarning(Tag + "UrgencyFx が無い（警報の音は鳴らない）。UrgencyScene.BuildInto の後に呼ぶ");
        if (a.story == null) Debug.LogWarning(Tag + "StoryDirector が無い（演出の音は鳴らない）。StoryScene.BuildInto の後に呼ぶ");
        if (a.musicCalm == null) Debug.LogWarning(Tag + "BGM が無い: " + CalmPath);
        if (Object.FindAnyObjectByType<AudioListener>(FindObjectsInactive.Include) == null) Debug.LogWarning(Tag + "AudioListener が無い（実行時に Audio に足す）");
        Debug.Log(Tag + "組み立てた：効果音・ジングル " + a.clips.Length + " 個、BGM " + (a.musicCalm != null ? a.musicCalm.name : "なし"));
        return a;
    }

    static IEnumerable<AudioClip> Clips(string folder, string prefix)
    {
        if (!AssetDatabase.IsValidFolder(folder)) { Debug.LogWarning(Tag + "フォルダが無い: " + folder); yield break; }
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (c != null && (prefix == null || c.name.StartsWith(prefix))) yield return c;
        }
    }
}

/// <summary>
/// 音の取り込みの設定（最初に取り込むときだけ。あとで Inspector で変えたものは残す）。
/// BGM（Assets/Audio/Music の mp3。3分半）は Decompress On Load だと 37MB ほどメモリを使うので、Compressed In Memory・裏で読み込む。
/// 効果音とジングルは短いので Unity の既定のまま。
/// </summary>
public class AudioImport : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(AudioScene.MusicFolder + "/") || !assetPath.EndsWith(".mp3")) return;
        var ai = (AudioImporter)assetImporter;
        if (!ai.importSettingsMissing) return;
        var s = ai.defaultSampleSettings;
        s.loadType = AudioClipLoadType.CompressedInMemory;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = 0.6f;
        ai.defaultSampleSettings = s;
        ai.loadInBackground = true;
    }
}
