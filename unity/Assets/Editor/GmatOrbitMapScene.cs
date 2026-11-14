// 書いた人：Claude（#65）／ 最終更新：2026-11-14
// 軌道の地図の確認用シーン Assets/Scenes/OrbitMap.unity を組み立て、スクリーンショットを撮る。
//   Tools/GMAT/軌道の地図シーンを作り直す     … シーン作成（先に「軌道を取り込む」を内部で呼ぶ）
//   Tools/GMAT/軌道の地図のスクリーンショット … docs/design/lookdev/orbit_map.png（t が違う4枚を並べる）と、1枚ずつの PNG
// 光と空は LookDevSettings と同じ。
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class GmatOrbitMapScene
{
    public const string ScenePath = "Assets/Scenes/OrbitMap.unity";
    const string Tag = "[OrbitMap] ";
    const string ShotFolder = "docs/design/lookdev";
    const int W = 960, H = 540;

    // 撮る時点：（ファイル名、t、route）
    static readonly (string name, float t, string route)[] Shots =
    {
        ("orbit_map_1_accident", 10f, ""),
        ("orbit_map_2_far_side", 210f, ""),
        ("orbit_map_3_after_burn_A", 400f, "a"),
        ("orbit_map_4_after_burn_B", 450f, "b"),
    };

    static GameObject Place(string path, Transform parent, string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        return go;
    }

    static Bounds LocalBounds(GameObject go)
    {
        var b = new Bounds(Vector3.zero, Vector3.zero);
        bool first = true;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var rb = r.bounds;
            if (first) { b = rb; first = false; } else b.Encapsulate(rb);
        }
        return b;
    }

    [MenuItem("Tools/GMAT/軌道の地図シーンを作り直す")]
    public static void BuildScene()
    {
        GmatImport.Import();
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var lighting = new GameObject("Lighting");
        LookDevSettings.ApplyLighting(lighting.transform);

        var root = new GameObject("TrajectoryView");
        var view = BuildRig(root.transform);

        // カメラ：斜め上から全体
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        camGo.transform.SetPositionAndRotation(new Vector3(20.5f, 32f, -28f), Quaternion.identity);
        camGo.transform.LookAt(new Vector3(20.5f, 0f, 0.5f));
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 35f; cam.nearClipPlane = 0.5f; cam.farClipPlane = 400f;
        cam.clearFlags = CameraClearFlags.Skybox; cam.allowHDR = true; cam.allowMSAA = true;
        var data = camGo.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.None;
        camGo.AddComponent<AudioListener>();

        view.t = 0f;
        EditorSceneManager.SaveScene(scene, ScenePath);
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!list.Exists(s => s.path == ScenePath)) { list.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = list.ToArray(); }
        Debug.Log(Tag + ScenePath + " を作り直した");
    }

    /// <summary>軌道の地図（線・地球・月・宇宙船）を root の下に組み立てる。Game.unity の HUD の小窓でも使う</summary>
    public static TrajectoryView BuildRig(Transform root)
    {
        var view = root.gameObject.AddComponent<TrajectoryView>();
        view.routeA = AssetDatabase.LoadAssetAtPath<GmatRoute>(GmatImport.OutFolder + "/Route_A.asset");
        view.routeB = AssetDatabase.LoadAssetAtPath<GmatRoute>(GmatImport.OutFolder + "/Route_B.asset");
        view.lineMaterial = GmatImport.EnsureLineMaterial();

        // 地球と月：球の中心が原点。半径は見栄えの大きさ（TrajectoryView の earthVisualKm・moonVisualKm）
        var earth = Place("Assets/Models/Bodies/Body_Earth.fbx", root, "Earth");
        var moon = Place("Assets/Models/Bodies/Body_Moon.fbx", root, "Moon");
        FitSphere(earth, view.earthVisualKm * view.unitsPerKm, Vector3.zero);
        FitSphere(moon, view.moonVisualKm * view.unitsPerKm, new Vector3(view.routeA.moonCenterXKm, 0f, 0f) * view.unitsPerKm);
        view.earth = earth.transform; view.moon = moon.transform;

        // 宇宙船：長さ約 2 unit（実際より大きい）。+Z が進行方向
        var shipPivot = new GameObject("Ship").transform;
        shipPivot.SetParent(root, false);
        var stack = Place("Assets/Models/Ship/Ship_Stack.fbx", shipPivot, "Ship_Stack");
        var sb = LocalBounds(stack);
        float len = Mathf.Max(sb.size.x, sb.size.y, sb.size.z);
        stack.transform.localScale = Vector3.one * (2f / len);
        stack.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // モデルの長軸は Unity の +Y。+Z（進行方向）へ倒す
        Debug.Log(Tag + "Ship_Stack の大きさ " + sb.size + "、原点からの中心 " + (sb.center - stack.transform.position));
        view.ship = shipPivot;

        view.t = 0f;
        return view;
    }

    static void FitSphere(GameObject go, float radiusUnits, Vector3 center)
    {
        // center は親（軌道の地図の根。回転・拡大なし）から見た位置。根がどこに置かれていても合うように local で置く
        go.transform.localPosition = Vector3.zero; go.transform.localScale = Vector3.one;
        var b = LocalBounds(go);
        float r = Mathf.Max(b.extents.x, b.extents.y, b.extents.z);
        go.transform.localScale = Vector3.one * (radiusUnits / r);
        go.transform.localPosition = center - (b.center - go.transform.position) * (radiusUnits / r);
    }

    [MenuItem("Tools/GMAT/軌道の地図のスクリーンショット")]
    public static void Screenshots()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var view = Object.FindAnyObjectByType<TrajectoryView>();
        var cam = Camera.main;
        var outDir = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Application.dataPath)), ShotFolder);
        Directory.CreateDirectory(outDir);
        int prevQ = QualitySettings.GetQualityLevel();
        int mobile = System.Array.IndexOf(QualitySettings.names, "Mobile");
        if (mobile >= 0) QualitySettings.SetQualityLevel(mobile, true);
        var sheet = new Texture2D(W * 2, H * 2, TextureFormat.RGB24, false);
        float prevT = view.t; string prevRoute = view.route;
        try
        {
            for (int i = 0; i < Shots.Length; i++)
            {
                var s = Shots[i];
                view.SetState(s.t, s.route);
                var tex = Render(cam, W, H);
                File.WriteAllBytes(Path.Combine(outDir, s.name + ".png"), tex.EncodeToPNG());
                sheet.SetPixels((i % 2) * W, (1 - i / 2) * H, W, H, tex.GetPixels());
                Object.DestroyImmediate(tex);
            }
            File.WriteAllBytes(Path.Combine(outDir, "orbit_map.png"), sheet.EncodeToPNG());
        }
        finally
        {
            Object.DestroyImmediate(sheet);
            view.SetState(prevT, prevRoute);
            QualitySettings.SetQualityLevel(prevQ, true);
        }
        Debug.Log(Tag + "スクリーンショットを " + outDir + " に出した");
    }

    static Texture2D Render(Camera cam, int w, int h)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = LookDevSettings.MsaaSamples };
        var prevActive = RenderTexture.active;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        cam.targetTexture = null;
        RenderTexture.active = prevActive;
        rt.Release(); Object.DestroyImmediate(rt);
        return tex;
    }
}
