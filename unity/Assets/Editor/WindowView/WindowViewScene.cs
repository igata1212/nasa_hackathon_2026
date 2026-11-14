// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// 窓の外の地球・月（#126）を Game.unity に組み立てる。シーンは手で直さず、ここを直して作り直す。
//   メニュー Tools/WindowView/窓の外を組み立てる … Game.unity を開いて「WindowView」を作り直して保存する（GameFlow が要る）
//   GameFlowScene.BuildScene（Tools/GameFlow/シーンを作り直す）からは BuildInto(flow) を呼ぶ
// LookDev が置いた止まった地球（Outside）は、Game.unity の中でだけ隠す（LookDev.unity と LookDevScene.cs は変えない）。
// 地球・月は Assets/Models/Bodies/ の Body_Earth・Body_Moon。大きさと位置は実行時に WindowView が毎フレーム決める。
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class WindowViewScene
{
    const string Tag = "[WindowView] ";
    const string RootName = "WindowView";
    const string EarthModel = "Assets/Models/Bodies/Body_Earth.fbx";
    const string MoonModel = "Assets/Models/Bodies/Body_Moon.fbx";

    [MenuItem("Tools/WindowView/窓の外を組み立てる")]
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
        Debug.Log(Tag + GameFlowScene.ScenePath + " に窓の外を組み立てた");
    }

    /// <summary>今のシーンに「WindowView」を作り直す（保存はしない）</summary>
    public static WindowView BuildInto(GameFlow flow)
    {
        var old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);

        // LookDev の止まった地球：隠す（Play 前の見た目を変えないよう、位置は新しい地球に写す）
        var outside = FindNamed("Outside");
        Transform oldEarth = outside != null && outside.childCount > 0 ? outside.GetChild(0) : null;

        var root = new GameObject(RootName);
        var view = root.AddComponent<WindowView>();
        view.flow = flow;
        view.routeA = AssetDatabase.LoadAssetAtPath<GmatRoute>(GmatImport.OutFolder + "/Route_A.asset");
        view.routeB = AssetDatabase.LoadAssetAtPath<GmatRoute>(GmatImport.OutFolder + "/Route_B.asset");
        if (view.routeA == null || view.routeB == null) Debug.LogWarning(Tag + "GMAT の軌道が無い。Tools/GMAT/軌道を取り込む を先に");
        view.viewCamera = FindCamera("Cam_Top");
        view.window = FindNamed("Slot_Window");
        view.shipCenter = FindNamed("Slot_Center");
        if (view.viewCamera == null) Debug.LogWarning(Tag + "Cam_Top が無い（実行時は Camera.main を使う）");
        if (view.window == null) Debug.LogWarning(Tag + "Slot_Window が無い（Ship_Interior.fbx を確かめる）");

        view.earth = Place(EarthModel, root.transform, "Earth");
        view.moon = Place(MoonModel, root.transform, "Moon");
        if (view.earth != null && oldEarth != null)
        {
            view.earth.SetPositionAndRotation(oldEarth.position, oldEarth.rotation);
            view.earth.localScale = oldEarth.lossyScale;
        }
        else if (view.earth != null) SetRenderers(view.earth, false);
        if (view.moon != null)
        {
            if (view.earth != null) view.moon.position = view.earth.position;
            SetRenderers(view.moon, false);   // Play までは見せない（WindowView が出し入れする）
        }
        if (outside != null) outside.gameObject.SetActive(false);
        return view;
    }

    static Transform Place(string path, Transform parent, string name)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) { Debug.LogError(Tag + path + " が無い"); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.name = name;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
        return go.transform;
    }

    static void SetRenderers(Transform t, bool on)
    {
        foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = on;
    }

    // 名前が一致、または「ルート.名前」（FBX の部品名）。隠れているものも探す
    static Transform FindNamed(string name)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == name || t.name.EndsWith("." + name)) return t;
        return null;
    }

    static Camera FindCamera(string prefix)
    {
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            if (c.name.StartsWith(prefix)) return c;
        return Camera.main;
    }
}
