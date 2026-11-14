// 書いた人：Claude ／ 最終更新：2026-11-14
// ゲームの流れのシーン Assets/Scenes/Game.unity を組み立てる（#96）。シーンは手で直さず、ここを直して作り直す。
//   メニュー Tools/GameFlow/シーンを作り直す … LookDev.unity（船内・光・クルー）を開いて Game.unity として別名保存し、GameFlow と EventSystem を足す
//   LookDev.unity 自体は変えない（別名保存。LookDev の組み立ては Tools/LookDev/シーンを作り直す）
//   演出（打ち上げ・管制室の小窓・帰還）：最後に StoryScene.BuildInto を呼ぶ（Editor/Story/StoryScene.cs）
//   軌道の小窓：軌道の地図（GmatOrbitMapScene.BuildRig）を船内から遠く（RigOrigin）に置き、専用のカメラで RenderTexture に描いて HUD の隅に出す
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
    const string TimelinePath = "Assets/Data/Sim/timeline_t1_good.json";   // 正は data/sim/（下の CopyTimelines で取り込む）
    const string TimelineFailPath = "Assets/Data/Sim/timeline_t4_fail.json";
    static readonly Vector3 RigOrigin = new Vector3(0f, -5000f, 0f);         // 船内のカメラに映らない遠さ

    [MenuItem("Tools/GameFlow/シーンを作り直す")]
    public static void BuildSceneMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildScene();
    }

    /// <summary>確認なしで作り直す（UnityMCP から呼ぶとき用）</summary>
    public static void BuildScene()
    {
        CopyTimelines();
        var scene = EditorSceneManager.OpenScene(LookDevPath, OpenSceneMode.Single);

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();

        var go = new GameObject("GameFlow");
        var flow = go.AddComponent<GameFlow>();
        flow.partsJson = AssetDatabase.LoadAssetAtPath<TextAsset>(PartsPath);
        flow.timelineJson = AssetDatabase.LoadAssetAtPath<TextAsset>(TimelinePath);
        flow.timelineFailJson = AssetDatabase.LoadAssetAtPath<TextAsset>(TimelineFailPath);
        flow.wsUrl = "ws://localhost:8765";

        // 軌道の小窓
        var rig = new GameObject("TrajectoryMini");
        rig.transform.position = RigOrigin;
        var view = GmatOrbitMapScene.BuildRig(rig.transform);
        view.lineWidth = 0.4f; view.faintWidth = 0.18f;                         // 小窓は小さいので線を太く
        view.ship.localScale = Vector3.one * 2.5f;
        var camGo = new GameObject("TrajectoryMiniCamera");
        camGo.transform.SetParent(rig.transform, false);
        camGo.transform.localPosition = new Vector3(21f, 44f, -22f);
        camGo.transform.LookAt(rig.transform.TransformPoint(new Vector3(21f, 0f, 2f)));
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 34f; cam.nearClipPlane = 0.5f; cam.farClipPlane = 200f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.03f, 0.05f, 0.11f, 1f);
        cam.depth = -10f;
        camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = false;
        flow.trajectory = view;
        flow.trajectoryCamera = cam;

        StoryScene.BuildInto(flow);   // 手描きスケッチの演出（#124）：打ち上げ・管制室の小窓・帰還
        UrgencyScene.BuildInto(flow); // 警告のときの赤い縁・カメラの揺れ・警報ランプ（#125）。StoryScene の後（StoryDirector を拾う）
        AudioScene.BuildInto(flow);   // 音（#141）：効果音・BGM・交信の声。Story・Urgency の後（StoryDirector・UrgencyFx を拾う）

        EditorSceneManager.SaveScene(scene, ScenePath, false);   // 別名で保存（LookDev.unity は触らない）
        var list = EditorBuildSettings.scenes.ToList();
        if (!list.Any(s => s.path == ScenePath)) { list.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = list.ToArray(); }
        Debug.Log("[GameFlow] " + ScenePath + " を作り直した");
    }

    /// <summary>オフライン再生の記録を data/sim/（正）から Assets/Data/Sim/ に写す</summary>
    [MenuItem("Tools/GameFlow/オフライン再生の記録を取り込む")]
    public static void CopyTimelines()
    {
        string repo = System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(Application.dataPath));
        foreach (var dst in new[] { TimelinePath, TimelineFailPath })
        {
            string src = System.IO.Path.Combine(repo, "data/sim", System.IO.Path.GetFileName(dst));
            if (!System.IO.File.Exists(src)) { Debug.LogWarning("[GameFlow] 記録が無い: " + src); continue; }
            System.IO.File.Copy(src, System.IO.Path.Combine(repo, "unity", dst), true);
        }
        AssetDatabase.Refresh();
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
