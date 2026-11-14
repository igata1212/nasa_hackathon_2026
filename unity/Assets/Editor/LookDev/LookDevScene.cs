// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
//
// 見た目確認のシーン Assets/Scenes/LookDev.unity を組み立てる（#55）。シーンは手で直さず、ここを直して作り直す。
//   メニュー Tools/LookDev/シーンを作り直す   … 素材 → URP の設定 → シーン（光・床・4席・クルー・タンク・煙・警報・パイプ・カメラ）→ 保存
//   メニュー Tools/LookDev/スクリーンショット … 3台のカメラ（俯瞰・断面・寄り）を 1600×900 で docs/design/lookdev/ に PNG で保存
// 船室（#40）ができたら、BuildScene の「床」をそれに差し替える（Stations と Props は船室の床の上に置く）。
//
// 並べ方（Unity の座標。カメラは −Z 側から +Z を見る）：
//   - コンソール4台を、原点を中心とする半径 StationRadius の半円（奥側）に、正面（+Z）を中心に向けて置く。左から Pilot・Life Support・Comms & Power・Mission Planner
//   - クルーはコンソールの正面から CrewDistance 離れた所に、コンソールの方を向いて浮かせる。腕は肩で X まわり ArmPitch 回して前に出す
//   - 手前にタンク3つ（破れたタンクの穴はカメラ向き、煙つき）、左手前に破れたパイプ、Life Support の画面の上に警報ランプ
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class LookDevScene
{
    public const string ScenePath = "Assets/Scenes/LookDev.unity";
    public const string ShotFolder = "docs/design/lookdev";   // リポジトリのルートから
    public const int ShotWidth = 1600, ShotHeight = 900;

    // 並べ方
    public const float StationRadius = 2.0f;            // 中心からコンソールの原点まで
    static readonly float[] StationAngles = { -67.5f, -22.5f, 22.5f, 67.5f };   // 奥（+Z）から測った角度
    public const float CrewDistance = 0.58f;            // コンソールの原点からクルーの原点まで（コンソールの正面の方向）
    public const float CrewFloatHeight = 0.28f;         // 肩 0.53m ＋ 0.28m ≒ 0.81m。腕を前に出した手が天板 0.80m に届く
    public const float ArmPitch = -85f;                 // 肩の X まわり（−90 で真っすぐ前。少し下げて手を天板に乗せる）

    // 席：コンソール、クルー、目、取り付ける操作部品（Slot 番号 → 部品）。modeling/core/render_all.py の並べ方と同じ
    struct Station { public string console, crew, eyes; public (int slot, string ctrl)[] ctrls; }
    static readonly Station[] Stations =
    {
        new Station { console = "Console_Pilot", crew = "Crew_Pilot", eyes = "Eyes_Panic",
            ctrls = new[] { (1, "Ctrl_Toggle_Nav"), (2, "Ctrl_Ignite"), (4, "Ctrl_AttitudeStick"), (6, "Ctrl_Button") } },
        new Station { console = "Console_LifeSupport", crew = "Crew_LifeSupport", eyes = "Eyes_Normal",
            ctrls = new[] { (2, "Ctrl_Stir"), (4, "Ctrl_Toggle_Heater") } },
        new Station { console = "Console_CommsPower", crew = "Crew_CommsPower", eyes = "Eyes_Relief",
            ctrls = new[] { (1, "Ctrl_Toggle_Comms"), (2, "Ctrl_Toggle_Lights"), (4, "Ctrl_RouteLever"), (6, "Ctrl_Transmit") } },
        new Station { console = "Console_MissionPlanner", crew = "Crew_MissionPlanner", eyes = "Eyes_Panic",
            ctrls = new[] { (2, "Ctrl_Instr_Camera"), (3, "Ctrl_Instr_Spectrometer"), (5, "Ctrl_Instr_Radar") } },
    };

    // カメラ（俯瞰はゲームの想定：見下ろし 50°・縦の画角 30°）
    public const float TopPitch = 50f, TopFov = 30f, TopDistance = 8.0f;
    static readonly Vector3 TopTarget = new Vector3(0f, 0.5f, 0.35f);
    public const float SidePitch = 6f, SideFov = 30f, SideDistance = 9.5f;
    static readonly Vector3 SideTarget = new Vector3(0f, 0.75f, 0.3f);

    [MenuItem("Tools/LookDev/シーンを作り直す")]
    public static void BuildSceneMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildScene();
    }

    /// <summary>確認なしで作り直す（今開いているシーンの保存していない変更は捨てる。UnityMCP から呼ぶとき用）</summary>
    public static void BuildScene()
    {
        LookDevAssets.BuildAll();
        LookDevSettings.ApplyUrp();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var lighting = new GameObject("Lighting");
        LookDevSettings.ApplyLighting(lighting.transform);

        // 仮の床（船室 #40 ができたら差し替える）
        var floor = new GameObject("Floor (仮)");
        floor.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(LookDevAssets.FloorMesh);
        floor.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(CorePalette.MaterialPath);

        // 4席
        var stations = new GameObject("Stations").transform;
        Transform pilotCrew = null, lifeSupportConsole = null;
        for (int i = 0; i < Stations.Length; i++)
        {
            var s = Stations[i];
            float a = StationAngles[i] * Mathf.Deg2Rad;
            var st = new GameObject("Station_" + s.console.Substring("Console_".Length)).transform;
            st.SetParent(stations, false);
            st.localPosition = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * StationRadius;
            st.localRotation = Quaternion.Euler(0f, StationAngles[i] + 180f, 0f);   // 正面（+Z）を中心へ

            var console = Place("Console/" + s.console, st, Vector3.zero, 0f);
            foreach (var (slot, ctrl) in s.ctrls)
            {
                var slotT = FindPart(console.transform, ".Slot_" + slot);
                if (slotT == null) { Debug.LogWarning(LookDevSettings.Tag + s.console + " に .Slot_" + slot + " がない"); continue; }
                Place("Controls/" + ctrl, slotT, Vector3.zero, 0f);
            }

            var crew = Place("Crew/" + s.crew, st, new Vector3(0f, CrewFloatHeight, CrewDistance), 180f);
            PoseCrew(crew.transform, s.eyes);
            var fl = crew.AddComponent<CrewFloat>();
            fl.phase = i * 0.27f;
            if (i == 0) pilotCrew = crew.transform;
            if (i == 1) lifeSupportConsole = console.transform;
        }

        // タンク・煙・警報・パイプ
        var props = new GameObject("Props").transform;
        Place("Tank/Tank_Oxygen1", props, new Vector3(0.95f, 0f, -1.35f), 10f);
        var tank2 = Place("Tank/Tank_Oxygen2", props, new Vector3(1.75f, 0f, -0.95f), -15f);
        var valveSlot = FindPart(tank2.transform, ".Slot_Valve");
        if (valveSlot != null) Place("Controls/Ctrl_Valve", valveSlot, Vector3.zero, 0f);
        var broken = Place("Tank/Tank_Oxygen2_Broken", props, new Vector3(0.15f, 0f, -0.95f), 200f);   // 穴（.Slot_Leak）をカメラの方へ
        var leak = FindPart(broken.transform, ".Slot_Leak");
        if (leak != null)
        {
            var gas = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LookDevAssets.GasLeakPrefab), leak);
            gas.transform.localPosition = Vector3.zero;
            gas.transform.localRotation = Quaternion.identity;
        }
        Place("Fx/Fx_BrokenPipe", props, new Vector3(-1.4f, 0f, -1.05f), -25f);

        // 警報ランプ：Life Support のコンソールの画面板の上（板の上端 y≈1.11、奥 z≈-0.25、右の画面の上）
        var alarm = Place("Fx/Fx_Alarm", lifeSupportConsole, new Vector3(-0.42f, 1.11f, -0.25f), 180f);
        var glow = alarm.AddComponent<LampGlow>();
        glow.red = LookDevAssets.LampMaterial("Red");
        glow.yellow = LookDevAssets.LampMaterial("Yellow");
        glow.green = LookDevAssets.LampMaterial("Green");
        int lamps = LampGlow.Apply(alarm, glow.red, glow.yellow, glow.green);
        var lightGo = new GameObject("Alarm Light");
        lightGo.transform.SetParent(alarm.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        var pl = lightGo.AddComponent<Light>();
        pl.type = LightType.Point;
        pl.color = LookDevAssets.LampRed;
        pl.range = 1.8f;
        pl.intensity = 0.6f;
        pl.shadows = LightShadows.None;
        lightGo.AddComponent<UniversalAdditionalLightData>();
        glow.glowLight = pl;

        // カメラ
        var cams = new GameObject("Cameras").transform;
        var top = NewCamera(cams, "Cam_Top (俯瞰 50°)", TopFov, Orbit(TopTarget, TopPitch, 0f, TopDistance), true);
        top.tag = "MainCamera";
        NewCamera(cams, "Cam_Side (断面・参考)", SideFov, Orbit(SideTarget, SidePitch, 0f, SideDistance), false);
        // 寄り：Pilot のクルーの顔を斜め前から（顔はコンソールの方を向いているので、コンソールの横から）
        var head = pilotCrew.position + Vector3.up * 0.95f;
        var face = pilotCrew.forward;
        var closeDir = (Quaternion.Euler(0f, -62f, 0f) * face).normalized;   // 顔の向きから、手前（カメラ側）へ回す
        var closePos = head + closeDir * 2.9f + Vector3.up * 0.8f;
        var close = NewCamera(cams, "Cam_Close (寄り)", 30f, Pose(closePos, head + Vector3.down * 0.3f), false);

        EditorSceneManager.SaveScene(scene, ScenePath);
        BakeEnvironment();
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        Debug.Log(LookDevSettings.Tag + ScenePath + " を作り直した（ランプ " + lamps + " 個を発光に差し替え）");
    }

    /// <summary>環境光（Gradient）と空の映り込みを焼いて LightingData に残す（焼かないとビルドで環境光が既定の空のものになる）。
    /// ライトマップは作らない（光は全部リアルタイム）。</summary>
    static void BakeEnvironment()
    {
        const string path = "Assets/Art/LookDev/LookDev_Lighting.lighting";
        var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
        if (ls == null) { ls = new LightingSettings { name = "LookDev_Lighting" }; AssetDatabase.CreateAsset(ls, path); }
        ls.bakedGI = false;
        ls.realtimeGI = false;
        EditorUtility.SetDirty(ls);
        Lightmapping.lightingSettings = ls;
        Lightmapping.Bake();
    }

    static GameObject Place(string model, Transform parent, Vector3 localPos, float yaw)
    {
        var path = "Assets/Models/" + model + ".fbx";
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) throw new System.Exception(LookDevSettings.Tag + path + " がない");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return go;
    }

    /// <summary>"&lt;ルート&gt;.&lt;部品&gt;" の部品を、名前の終わりで探す（孫まで）</summary>
    public static Transform FindPart(Transform root, string suffix)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t != root && t.name.EndsWith(suffix)) return t;
        return null;
    }

    static void PoseCrew(Transform crew, string eyes)
    {
        foreach (var arm in new[] { ".Arm_L", ".Arm_R" })
        {
            var t = FindPart(crew, arm);
            if (t != null) t.localRotation = Quaternion.Euler(ArmPitch, 0f, 0f);
        }
        foreach (var e in new[] { "Eyes_Normal", "Eyes_Panic", "Eyes_Relief" })
        {
            var t = FindPart(crew, "." + e);
            if (t != null) t.gameObject.SetActive(e == eyes);
        }
    }

    static (Vector3 pos, Quaternion rot) Orbit(Vector3 target, float pitch, float yaw, float distance)
    {
        var rot = Quaternion.Euler(pitch, yaw, 0f);
        return (target - rot * Vector3.forward * distance, rot);
    }

    static (Vector3 pos, Quaternion rot) Pose(Vector3 pos, Vector3 lookAt) => (pos, Quaternion.LookRotation(lookAt - pos, Vector3.up));

    static Camera NewCamera(Transform parent, string name, float fov, (Vector3 pos, Quaternion rot) pose, bool enabled)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pose.pos, pose.rot);
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = fov;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100f;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.allowMSAA = true;
        cam.allowHDR = true;
        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.renderShadows = true;
        data.antialiasing = AntialiasingMode.None;   // MSAA 2x（URP の設定）だけ
        cam.enabled = enabled;
        if (enabled) go.AddComponent<AudioListener>();
        return cam;
    }

    static void AddToBuildSettings(string path)
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (list.Exists(s => s.path == path)) return;
        list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    // ---------------- スクリーンショット ----------------

    [MenuItem("Tools/LookDev/スクリーンショット")]
    public static void Screenshots()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var outDir = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Application.dataPath)), ShotFolder);
        Directory.CreateDirectory(outDir);

        // 煙は止まっているので、2.5 秒ぶん進めてから撮る
        foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude))
            if (ps.transform.parent == null || ps.transform.parent.GetComponent<ParticleSystem>() == null)
                ps.Simulate(2.5f, true, true, true);

        // WebGL・iOS と同じ品質（Mobile）で撮る。終わったら元に戻す
        int prevQuality = QualitySettings.GetQualityLevel();
        int mobile = System.Array.IndexOf(QualitySettings.names, "Mobile");
        if (mobile >= 0) QualitySettings.SetQualityLevel(mobile, true);
        var saved = new System.Text.StringBuilder();
        try
        {
            foreach (var (cam, file) in new[] { ("Cam_Top", "lookdev_top.png"), ("Cam_Side", "lookdev_side.png"), ("Cam_Close", "lookdev_close.png") })
            {
                Camera c = null;
                foreach (var cc in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
                    if (cc.name.StartsWith(cam)) c = cc;
                if (c == null) { Debug.LogWarning(LookDevSettings.Tag + cam + " がない"); continue; }
                var path = Path.Combine(outDir, file);
                Render(c, path);
                saved.Append(path).Append('\n');
            }
        }
        finally
        {
            QualitySettings.SetQualityLevel(prevQuality, true);
        }
        Debug.Log(LookDevSettings.Tag + "スクリーンショット（品質 " + (mobile >= 0 ? "Mobile" : QualitySettings.names[prevQuality]) + "）\n" + saved);
    }

    static void Render(Camera cam, string path)
    {
        var rt = new RenderTexture(ShotWidth, ShotHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = LookDevSettings.MsaaSamples };
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;
        try
        {
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(ShotWidth, ShotHeight, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, ShotWidth, ShotHeight), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        finally
        {
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
