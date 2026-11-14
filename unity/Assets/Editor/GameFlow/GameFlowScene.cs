// 書いた人：Claude ／ 最終更新：2026-11-14
// ゲームの流れのシーン Assets/Scenes/Game.unity を組み立てる（#96）。シーンは手で直さず、ここを直して作り直す。
//   メニュー Tools/GameFlow/シーンを作り直す … LookDev.unity（船内・光・クルー）を開いて Game.unity として別名保存し、GameFlow と EventSystem を足す
//   LookDev.unity 自体は変えない（別名保存。LookDev の組み立ては Tools/LookDev/シーンを作り直す）
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public static class GameFlowScene
{
    public const string ScenePath = "Assets/Scenes/Game.unity";
    const string LookDevPath = "Assets/Scenes/LookDev.unity";
    const string PartsPath = "Assets/Data/Sim/parts.json";
    const string TimelinePath = "Assets/Data/Sim/timeline_t1.json";

    [MenuItem("Tools/GameFlow/シーンを作り直す")]
    public static void BuildSceneMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildScene();
    }

    /// <summary>確認なしで作り直す（UnityMCP から呼ぶとき用）</summary>
    public static void BuildScene()
    {
        var scene = EditorSceneManager.OpenScene(LookDevPath, OpenSceneMode.Single);

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();

        var go = new GameObject("GameFlow");
        var flow = go.AddComponent<GameFlow>();
        flow.partsJson = AssetDatabase.LoadAssetAtPath<TextAsset>(PartsPath);
        flow.timelineJson = AssetDatabase.LoadAssetAtPath<TextAsset>(TimelinePath);
        flow.wsUrl = "ws://localhost:8765";

        EditorSceneManager.SaveScene(scene, ScenePath, false);   // 別名で保存（LookDev.unity は触らない）
        var list = EditorBuildSettings.scenes.ToList();
        if (!list.Any(s => s.path == ScenePath)) { list.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = list.ToArray(); }
        Debug.Log("[GameFlow] " + ScenePath + " を作り直した");
    }

    /// <summary>確認用：Play 直後にオフライン再生へ入るようにして保存する（MCP から使う）</summary>
    public static void SetAutoOffline(bool on)
    {
        var flow = Object.FindAnyObjectByType<GameFlow>();
        if (flow == null) return;
        flow.autoOffline = on;
        EditorUtility.SetDirty(flow);
        EditorSceneManager.SaveOpenScenes();
    }
}
