// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
//
// 見た目確認のシーン Assets/Scenes/LookDev.unity を組み立てる（#55）。シーンは手で直さず、ここを直して作り直す。
//   メニュー Tools/LookDev/シーンを作り直す   … 素材 → URP の設定 → 光 → 船室・4席・クルー・タンク・煙・警報・パイプ・カメラ・地球 → 保存
//   メニュー Tools/LookDev/スクリーンショット … 俯瞰・断面・寄り（16:9）と俯瞰（4:3）を docs/design/lookdev/ に PNG で保存（撮る前の3枚は docs/reference/lookdev-before/ へ）
//
// 並べ方（船室 Ship_Interior.fbx の Slot。modeling/interior/layout.json）：
//   - 親「Ship」を Y 180° 回し、その下に船室を位置0・回転0で置く（奥の壁は Unity の −Z、LookDev の光は +Z へ進むため）
//   - コンソール・クルー・タンク・パイプ・警報・窓などは、対応する Slot の子に位置0・回転0で置く。向きは Slot が持つ
//   - 警報の Point Light と室内灯は Slot の子に置く（「今のシーンに光を入れる」は Lighting の子を全部消すため、Lighting の下には置かない）
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class LookDevScene
{
    public const string ScenePath = "Assets/Scenes/LookDev.unity";
    public const string ShotFolder = "docs/design/lookdev";   // リポジトリのルートから
    public const int ShotWidth = 1600, ShotHeight = 900;       // 16:9
    public const int Shot43Height = 1200;                      // 俯瞰の 4:3（1600×1200）

    const string ShipRootName = "Ship";                        // 船室を Y 180° 回す親
    const string ShipModel = "Ship/Ship_Interior";
    const string EarthModel = "Bodies/Body_Earth";

    // クルーの置き方（Slot_Crew_* の局所。向きは Slot が持つので、クルーは回さない）
    public const float CrewFloatHeight = 0.16f;   // 局所 Y（m）。CrewFloat は Play のときだけ揺れる
    public const float CrewForward = 0.03f;       // 局所 +Z（コンソール側）へ寄せる量（m）
    public const float ArmPitch = -85f;           // 肩の X まわり（−90 で真っすぐ前。少し下げて手を天板に乗せる）
    public const float LookLimit = 110f;           // LookAtCamera の2人：首の左右の上限（度）

    // 席：コンソール・クルー・目・頭の向き（Normal のとき）・取り付ける操作部品（Slot 番号 → 部品）
    // 頭：左側の席（Pilot・LifeSupport）は Y −60、右側（CommsPower・MissionPlanner）は +60（部屋の中心側へ振る）
    struct Station { public string seat, eyes; public bool lookAtCamera; public Vector3 normalEuler; public (int slot, string ctrl)[] ctrls; }
    static readonly Station[] Stations =
    {
        new Station { seat = "Pilot", eyes = "Eyes_Normal", lookAtCamera = false, normalEuler = new Vector3(-15f, -60f, 0f),
            ctrls = new[] { (1, "Ctrl_Toggle_Nav"), (2, "Ctrl_Ignite"), (4, "Ctrl_AttitudeStick"), (6, "Ctrl_Button") } },
        new Station { seat = "LifeSupport", eyes = "Eyes_Panic", lookAtCamera = true, normalEuler = new Vector3(-15f, -60f, 0f),
            ctrls = new[] { (2, "Ctrl_Stir"), (4, "Ctrl_Toggle_Heater") } },
        new Station { seat = "CommsPower", eyes = "Eyes_Panic", lookAtCamera = true, normalEuler = new Vector3(-15f, 60f, 0f),
            ctrls = new[] { (1, "Ctrl_Toggle_Comms"), (2, "Ctrl_Toggle_Lights"), (4, "Ctrl_RouteLever"), (6, "Ctrl_Transmit") } },
        new Station { seat = "MissionPlanner", eyes = "Eyes_Normal", lookAtCamera = false, normalEuler = new Vector3(-15f, 60f, 0f),
            ctrls = new[] { (2, "Ctrl_Instr_Camera"), (3, "Ctrl_Instr_Spectrometer"), (5, "Ctrl_Instr_Radar") } },
    };

    // カメラ（縦の画角 30°。位置と向きは船室の Slot_Cam_* をそのまま写す）
    public const float CamFov = 30f;
    // 窓の外の地球：俯瞰カメラから窓の延長線上、窓から EarthDistance m 先。EarthDrop m 下げて、地球の縁を窓に入れる
    public const float EarthDistance = 40f;
    public const float EarthRadius = 18f;         // スケール（m）
    public const float EarthDrop = 22.8f;         // 計算値：窓の中ほどに地球の上端が来る量（m）。少しずらす程度では縁は窓に入らない

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

        // 船室：親を Y 180° 回し、その下に Ship_Interior.fbx を位置0・回転0で置く
        var ship = new GameObject(ShipRootName);
        ship.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        var interiorGo = Place(ShipModel, ship.transform, Vector3.zero, 0f);
        var interior = interiorGo.transform;
        interiorGo.AddComponent<ScreenGlow>();                  // 壁の画面
        SetRoof(interior, false);                               // 初期は俯瞰（手前の壁と屋根、奥の屋根を消す）
        SetShown(interior, "Pipe_Intact", false);               // 事故のあと：折れたパイプに替える

        // 4席：コンソールとクルーは Slot の子に（位置0・回転0）
        Transform pilotCrew = null;
        for (int i = 0; i < Stations.Length; i++)
        {
            var s = Stations[i];
            var console = Place("Console/Console_" + s.seat, Slot(interior, "Slot_Console_" + s.seat), Vector3.zero, 0f);
            console.AddComponent<ScreenGlow>();
            foreach (var (slot, ctrl) in s.ctrls)
            {
                var slotT = FindPart(console.transform, ".Slot_" + slot);
                if (slotT == null) { Debug.LogWarning(LookDevSettings.Tag + console.name + " に .Slot_" + slot + " がない"); continue; }
                Place("Controls/" + ctrl, slotT, Vector3.zero, 0f);
            }

            var crew = Place("Crew/Crew_" + s.seat, Slot(interior, "Slot_Crew_" + s.seat), new Vector3(0f, CrewFloatHeight, CrewForward), 0f);
            PoseArms(crew.transform);
            var look = crew.AddComponent<CrewLook>();
            look.normalEuler = s.normalEuler;
            look.mode = s.lookAtCamera ? CrewLook.Mode.LookAtCamera : CrewLook.Mode.Normal;
            if (s.lookAtCamera) look.lookLimit = LookLimit;
            look.SetEyes(s.eyes);                               // 目は必ず入れる（入れないと既定の Eyes_Normal）
            look.enabled = false;                               // 後から入れた値で姿を決め直す（非 Play は OnEnable で反映）
            look.enabled = true;
            crew.AddComponent<CrewFloat>().phase = i * 0.27f;
            if (s.seat == "Pilot") pilotCrew = crew.transform;
        }

        // タンク・煙・パイプ（事故のあと。Slot_Tank_2 は破れた酸素タンク）
        var ox1 = Place("Tank/Tank_Oxygen1", Slot(interior, "Slot_Tank_1"), Vector3.zero, 0f);
        var valveSlot1 = FindPart(ox1.transform, ".Slot_Valve");
        if (valveSlot1 != null) Place("Controls/Ctrl_Valve", valveSlot1, Vector3.zero, 0f);
        var broken = Place("Tank/Tank_Oxygen2_Broken", Slot(interior, "Slot_Tank_2"), Vector3.zero, 0f);
        var valveSlot = FindPart(broken.transform, ".Slot_Valve");
        if (valveSlot != null) Place("Controls/Ctrl_Valve", valveSlot, Vector3.zero, 0f);
        var leak = FindPart(broken.transform, ".Slot_Leak");
        if (leak != null)
        {
            var gas = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LookDevAssets.GasLeakPrefab), leak);
            gas.transform.localPosition = Vector3.zero;
            gas.transform.localRotation = Quaternion.identity;
        }
        Place("Fx/Fx_BrokenPipe", Slot(interior, "Slot_Pipe"), Vector3.zero, 0f);

        // 無重力の小物（#64）：船室の局所で、床の中央の上や席の間の空中（高さは船室の原点から）。それぞれ傾けて FloatProp で漂わせる
        var props = new GameObject("Props").transform;
        props.SetParent(interior, false);
        var propList = new (string model, Vector3 pos, Vector3 tilt)[]
        {
            ("Prop_Pen", new Vector3(-0.55f, 1.55f, 0.25f), new Vector3(20f, 40f, 70f)),
            ("Prop_Pen", new Vector3(0.95f, 1.95f, -0.35f), new Vector3(-30f, 120f, 15f)),
            ("Prop_Checklist", new Vector3(0.30f, 1.40f, 0.65f), new Vector3(25f, -20f, 15f)),
            ("Prop_Checklist", new Vector3(-1.10f, 1.40f, 0.70f), new Vector3(-15f, 60f, -25f)),
            ("Prop_FoodPack", new Vector3(-0.15f, 1.85f, -0.20f), new Vector3(10f, 30f, 35f)),
            ("Prop_FoodPack", new Vector3(0.95f, 1.30f, 0.55f), new Vector3(-25f, -50f, 20f)),
            ("Prop_DuctTape", new Vector3(-0.75f, 1.25f, 0.55f), new Vector3(35f, 0f, 20f)),
        };
        for (int i = 0; i < propList.Length; i++)
        {
            var p = Place("Props/" + propList[i].model, props, propList[i].pos, 0f);
            p.transform.localRotation = Quaternion.Euler(propList[i].tilt);
            p.AddComponent<FloatProp>().seed = i + 1;
        }

        // 警報ランプ：Slot_Alarm_1・_2 の両方。赤い Point Light つき
        int lamps = 0;
        foreach (var slotName in new[] { "Slot_Alarm_1", "Slot_Alarm_2" })
        {
            var alarm = Place("Fx/Fx_Alarm", Slot(interior, slotName), Vector3.zero, 0f);
            var glow = alarm.AddComponent<LampGlow>();
            glow.red = LookDevAssets.LampMaterial("Red");
            glow.yellow = LookDevAssets.LampMaterial("Yellow");
            glow.green = LookDevAssets.LampMaterial("Green");
            lamps += LampGlow.Apply(alarm, glow.red, glow.yellow, glow.green);
            var lightGo = new GameObject("Alarm Light");
            lightGo.transform.SetParent(alarm.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            var pl = lightGo.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = LookDevAssets.LampRed;
            pl.range = LookDevSettings.AlarmLightRange;
            pl.intensity = LookDevSettings.AlarmLightIntensity;
            pl.shadows = LightShadows.None;
            lightGo.AddComponent<UniversalAdditionalLightData>();
            glow.glowLight = pl;
        }

        // 室内灯：Slot_Center の上（Lighting の下には置かない）
        var roomGo = new GameObject("Interior Light");
        roomGo.transform.SetParent(Slot(interior, "Slot_Center"), false);
        roomGo.transform.localPosition = new Vector3(0f, LookDevSettings.InteriorLightHeight, 0f);
        var room = roomGo.AddComponent<Light>();
        room.type = LightType.Point;
        room.color = LookDevSettings.InteriorLightColor;
        room.intensity = LookDevSettings.InteriorLightIntensity;
        room.range = LookDevSettings.InteriorLightRange;
        room.shadows = LightShadows.None;
        roomGo.AddComponent<UniversalAdditionalLightData>();

        // カメラ：船室の Slot_Cam_* の位置・向きをそのまま写す（俯瞰は Slot_Cam_Topdown_Close）
        var cams = new GameObject("Cameras").transform;
        var topSlot = Slot(interior, "Slot_Cam_Topdown_Close");
        var top = NewCamera(cams, "Cam_Top (俯瞰 50°)", CamFov, (topSlot.position, topSlot.rotation), true);
        top.tag = "MainCamera";
        // メインカメラができたので、カメラを向くクルーの頭を決め直す
        foreach (var lk in Object.FindObjectsByType<CrewLook>(FindObjectsInactive.Include)) { lk.enabled = false; lk.enabled = true; }
        var cutSlot = Slot(interior, "Slot_Cam_Cutaway");
        NewCamera(cams, "Cam_Cutaway (断面 15°)", CamFov, (cutSlot.position, cutSlot.rotation), false);
        // 寄り：Pilot の顔を斜め前から（顔はコンソールの方を向いているので、コンソールの横から）
        var head = pilotCrew.position + Vector3.up * 0.95f;
        var closeDir = (Quaternion.Euler(0f, -62f, 0f) * pilotCrew.forward).normalized;   // 顔の向きから、手前（カメラ側）へ回す
        var closePos = head + closeDir * 2.9f + Vector3.up * 0.8f;
        NewCamera(cams, "Cam_Close (寄り)", CamFov, Pose(closePos, head + Vector3.down * 0.3f), false);

        // 窓の外の地球：俯瞰カメラ→窓の延長線上、窓から EarthDistance m。少し下げて縁を窓に入れる
        var window = Slot(interior, "Slot_Window").position;
        var viewDir = (window - topSlot.position).normalized;
        var outside = new GameObject("Outside").transform;
        var earth = Place(EarthModel, outside, Vector3.zero, 0f);
        earth.transform.position = window + viewDir * EarthDistance + Vector3.down * EarthDrop;
        earth.transform.localScale = Vector3.one * EarthRadius;

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

    // 船室の Slot（名前が一致、または「ルート.名前」）。なければ例外（Ship_Interior.fbx を確かめる）
    static Transform Slot(Transform interior, string name)
    {
        var t = FindNamed(interior, name);
        if (t == null) throw new System.Exception(LookDevSettings.Tag + name + " がない（Ship_Interior.fbx を確かめる）");
        return t;
    }

    static Transform FindNamed(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name || t.name.EndsWith("." + name)) return t;
        return null;
    }

    // 手前の壁と屋根は消す。奥の屋根は断面のときだけ出す
    static void SetRoof(Transform interior, bool cutaway)
    {
        SetShown(interior, "Wall_Front", false);
        SetShown(interior, "Roof_Front", false);
        SetShown(interior, "Roof_Back", cutaway);
    }

    static void SetShown(Transform root, string name, bool shown)
    {
        var t = FindNamed(root, name);
        if (t != null) t.gameObject.SetActive(shown);
    }

    // "ルート.部品" の部品を、名前の終わりで探す（孫まで）
    public static Transform FindPart(Transform root, string suffix)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t != root && t.name.EndsWith(suffix)) return t;
        return null;
    }

    static void PoseArms(Transform crew)
    {
        foreach (var arm in new[] { ".Arm_L", ".Arm_R" })
        {
            var t = FindPart(crew, arm);
            if (t != null) t.localRotation = Quaternion.Euler(ArmPitch, 0f, 0f);
        }
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

    // 撮る順：（カメラの名前の頭、出す PNG、幅、高さ、断面か）。断面のときは奥の屋根を出す
    static readonly (string cam, string file, int w, int h, bool cutaway)[] Shots =
    {
        ("Cam_Top", "lookdev_top.png", ShotWidth, ShotHeight, false),
        ("Cam_Cutaway", "lookdev_side.png", ShotWidth, ShotHeight, true),
        ("Cam_Close", "lookdev_close.png", ShotWidth, ShotHeight, false),
        ("Cam_Top", "lookdev_top_4x3.png", ShotWidth, Shot43Height, false),
    };
    static readonly string[] BeforeFiles = { "lookdev_top.png", "lookdev_side.png", "lookdev_close.png" };

    [MenuItem("Tools/LookDev/スクリーンショット")]
    public static void Screenshots()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var outDir = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Application.dataPath)), ShotFolder);
        Directory.CreateDirectory(outDir);
        BackupBefore(outDir);

        var ship = GameObject.Find(ShipRootName);
        var interior = ship != null && ship.transform.childCount > 0 ? ship.transform.GetChild(0) : null;

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
            foreach (var shot in Shots)
            {
                Camera c = null;
                foreach (var cc in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
                    if (cc.name.StartsWith(shot.cam)) c = cc;
                if (c == null) { Debug.LogWarning(LookDevSettings.Tag + shot.cam + " がない"); continue; }
                if (interior != null) SetRoof(interior, shot.cutaway);
                var path = Path.Combine(outDir, shot.file);
                Render(c, path, shot.w, shot.h);
                saved.Append(path).Append('\n');
            }
        }
        finally
        {
            QualitySettings.SetQualityLevel(prevQuality, true);
            if (interior != null) SetRoof(interior, false);   // 俯瞰の状態に戻す
        }
        Debug.Log(LookDevSettings.Tag + "スクリーンショット（品質 " + (mobile >= 0 ? "Mobile" : QualitySettings.names[prevQuality]) + "）\n" + saved);
    }

    // 撮る前の3枚を docs/reference/lookdev-before/ へ写す（docs/design は最新だけにするため）。before にすでにあるファイルは上書きしない
    static void BackupBefore(string outDir)
    {
        var beforeDir = Path.GetFullPath(Path.Combine(outDir, "..", "..", "reference", "lookdev-before"));
        Directory.CreateDirectory(beforeDir);
        foreach (var file in BeforeFiles)
        {
            var src = Path.Combine(outDir, file);
            var dst = Path.Combine(beforeDir, file);
            if (File.Exists(src) && !File.Exists(dst)) File.Copy(src, dst);
        }
    }

    static void Render(Camera cam, string path, int w, int h)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = LookDevSettings.MsaaSamples };
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;
        try
        {
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
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
