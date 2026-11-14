// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// 警告のときの演出（#125。赤い縁・カメラの揺れ・警報ランプ）を Game.unity に組み立てる。シーンは手で直さず、ここを直して作り直す。
//   メニュー Tools/Urgency/警告の演出を組み立てる … Game.unity を開いて「Urgency」を作り直して保存する（GameFlow が要る）
//   GameFlowScene.BuildScene（Tools/GameFlow/シーンを作り直す）からは BuildInto(flow) を呼ぶ
// 閾値は parameters.json から読む。正は docs/design/parameters.json なので、組み立てるたびに Assets/Data/Sim/ へ写す。
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class UrgencyScene
{
    const string Tag = "[Urgency] ";
    const string ParamsPath = "Assets/Data/Sim/parameters.json";

    [MenuItem("Tools/Urgency/警告の演出を組み立てる")]
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
        Debug.Log(Tag + GameFlowScene.ScenePath + " に警告の演出を組み立てた");
    }

    /// <summary>今のシーンに「Urgency」を作り直す（保存はしない）</summary>
    public static UrgencyFx BuildInto(GameFlow flow)
    {
        var old = GameObject.Find("Urgency");
        if (old != null) Object.DestroyImmediate(old);
        CopyParameters();

        var go = new GameObject("Urgency");
        var fx = go.AddComponent<UrgencyFx>();
        fx.flow = flow;
        fx.parametersJson = AssetDatabase.LoadAssetAtPath<TextAsset>(ParamsPath);
        fx.volume = FindGlobalVolume();
        var camGo = GameObject.FindGameObjectWithTag("MainCamera");
        fx.mainCamera = camGo != null ? camGo.GetComponent<Camera>() : null;
        fx.lamps = Object.FindObjectsByType<LampGlow>(FindObjectsInactive.Include);
        fx.story = Object.FindAnyObjectByType<StoryDirector>();

        if (fx.parametersJson == null) Debug.LogWarning(Tag + ParamsPath + " が無い");
        if (fx.volume == null) Debug.LogWarning(Tag + "Vignette を持つ全体の Volume が無い");
        if (fx.mainCamera == null) Debug.LogWarning(Tag + "MainCamera のタグのカメラが無い（実行時は Camera.main を使う）");
        Debug.Log(Tag + "組み立てた：ランプ " + fx.lamps.Length + " 個、Volume " + (fx.volume != null ? fx.volume.name : "なし")
                  + "、カメラ " + (fx.mainCamera != null ? fx.mainCamera.name : "なし"));
        return fx;
    }

    /// <summary>全体の Volume のうち、Vignette を持つもの（優先度の高い順）</summary>
    static Volume FindGlobalVolume()
    {
        Volume best = null;
        foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include))
        {
            if (!v.isGlobal || v.sharedProfile == null || !v.sharedProfile.Has<Vignette>()) continue;
            if (best == null || v.priority > best.priority) best = v;
        }
        return best;
    }

    /// <summary>docs/design/parameters.json（正）を Assets/Data/Sim/ に写す（中身が同じなら何もしない）</summary>
    [MenuItem("Tools/Urgency/parameters.json を取り込む")]
    public static void CopyParameters()
    {
        string repo = System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(Application.dataPath));
        string src = System.IO.Path.Combine(repo, "docs/design/parameters.json");
        string dst = System.IO.Path.Combine(repo, "unity", ParamsPath);
        if (!System.IO.File.Exists(src)) { Debug.LogWarning(Tag + "parameters.json が無い: " + src); return; }
        if (System.IO.File.Exists(dst) && System.IO.File.ReadAllText(src) == System.IO.File.ReadAllText(dst)) return;
        System.IO.File.Copy(src, dst, true);
        AssetDatabase.ImportAsset(ParamsPath);
        Debug.Log(Tag + "parameters.json を取り込んだ");
    }
}
