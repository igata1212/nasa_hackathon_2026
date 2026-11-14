// 書いた人：Claude ／ 最終更新：2026-11-14
// 手描きスケッチの演出（#124）の舞台を Game.unity に組み立てる。シーンは手で直さず、ここを直して作り直す。
//   メニュー Tools/Story/演出を組み立てる … Game.unity を開いて「Story」を作り直して保存する（GameFlow が要る）
//   GameFlowScene.BuildScene（Tools/GameFlow/シーンを作り直す）からは BuildInto(flow) を呼ぶ
// 舞台はどれも船内から遠く（y = -10000）に置き、ふだんは隠しておく。カメラは StoryDirector の1台（演出の間だけ）と、管制室の小窓の1台。
//   LaunchStage   (0, -10000, 0)     地面・発射台・ロケット（0.05 倍）とクルー4人。宇宙の舞台は +3000（Z）先
//   ReturnStage   (0, -10000, 6000)  海（y＝原点）と司令船・パラシュート
//   LostStage     (0, -10000, 9000)  漂う船と地球
//   MissionControl(0, -10000, -3000) 管制室・先生・管制官
// 向き：地上と管制室は 180° 回して、カメラが −Z 側から +Z を見るようにした（主光源 KeyEuler(55,-15) が前から当たる）。
// 素材（マテリアル）は Assets/Art/Story/ に作る。煙は Assets/Art/VFX/M_Smoke_Gas.mat（GasLeak と同じ）を使う。
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class StoryScene
{
    const string Tag = "[Story] ";
    const string Models = "Assets/Models/";
    public const string ArtFolder = "Assets/Art/Story";
    const string FireMat = ArtFolder + "/M_Story_Fire.mat";
    const string OceanMat = ArtFolder + "/M_Story_Ocean.mat";
    const string SmokeMat = "Assets/Art/VFX/M_Smoke_Gas.mat";

    static readonly Vector3 LaunchOrigin = new Vector3(0f, -10000f, 0f);
    static readonly Vector3 SpaceOffset = new Vector3(0f, 0f, 3000f);
    static readonly Vector3 ReturnOrigin = new Vector3(0f, -10000f, 6000f);
    static readonly Vector3 LostOrigin = new Vector3(0f, -10000f, 9000f);
    static readonly Vector3 ControlOrigin = new Vector3(0f, -10000f, -3000f);

    // 地面（modeling/scene/build_launch.py と同じ値。Blender の m）
    const float GroundScale = 0.05f;      // 実寸のロケット（70〜110m）をクルー（1.2m）と並べるために縮める
    const float GR = 400f, GH = 40f, HardR = 60f, HardTop = 40.3f;
    static float GroundZ(float r) { float sph = (GR * GR + GH * GH) / (2f * GH); return Mathf.Sqrt(sph * sph - r * r) - (sph - GH); }

    static readonly Color Orange = new Color(0.949f, 0.549f, 0.157f);

    [MenuItem("Tools/Story/演出を組み立てる")]
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
        Debug.Log(Tag + GameFlowScene.ScenePath + " に演出を組み立てた");
    }

    /// <summary>今のシーンに「Story」を作り直す（保存はしない）</summary>
    public static StoryDirector BuildInto(GameFlow flow)
    {
        var old = GameObject.Find("Story");
        if (old != null) Object.DestroyImmediate(old);
        EnsureMaterials();

        var root = new GameObject("Story");
        var dir = root.AddComponent<StoryDirector>();
        dir.flow = flow;
        dir.storyCamera = MakeCamera(root.transform, "StoryCamera", 40f, 0.1f, 900f, true);
        dir.launch = BuildLaunch(root.transform);
        dir.landing = BuildReturn(root.transform);
        dir.lost = BuildLost(root.transform);
        BuildHouston(root.transform, flow, dir);
        return dir;
    }

    // ------------------------------------------------------------------ 演出1：打ち上げ
    static LaunchStage BuildLaunch(Transform parent)
    {
        var go = new GameObject("LaunchStage");
        go.transform.SetParent(parent, false);
        go.transform.position = LaunchOrigin;
        var st = go.AddComponent<LaunchStage>();
        st.captionFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/Baloo2-ExtraBold.ttf");   // 3・2・1、LIFTOFF!、TO THE MOON（#129）

        // 地上（0.05 倍・180° 回す）
        var ground = new GameObject("Ground").transform;
        ground.SetParent(go.transform, false);
        ground.localRotation = Quaternion.Euler(0f, 180f, 0f);
        ground.localScale = Vector3.one * GroundScale;
        st.ground = ground;
        var g = Place(Models + "Scene/Scene_Ground.fbx", ground);
        var padSlot = Find(g, "Scene_Ground.Slot_Pad") ?? g;
        var pad = Place(Models + "Scene/Scene_LaunchPad.fbx", padSlot);
        var rocketSlot = Find(pad, "Scene_LaunchPad.Slot_Rocket") ?? pad;
        st.rocketSmall = Place(Models + "Ship/Ship_RocketSmall.fbx", rocketSlot);
        st.rocketLarge = Place(Models + "Ship/Ship_RocketLarge.fbx", rocketSlot);
        st.rocketLarge.gameObject.SetActive(false);
        st.arms = new[] { Find(pad, "Scene_LaunchPad.Move_Arm_1"), Find(pad, "Scene_LaunchPad.Move_Arm_2"), Find(pad, "Scene_LaunchPad.Move_Arm_3") };

        var center = new GameObject("PadCenter").transform;
        center.SetParent(go.transform, false);
        center.position = padSlot.position;
        st.padCenter = center;
        Vector3 P = center.position;

        // クルー4人：ロケットの左手前に、地面に立たせる（舗装の外は丸い地面の高さ）
        var crewRoot = new GameObject("Crew").transform;
        crewRoot.SetParent(go.transform, false);
        string[] names = { "Crew_Pilot", "Crew_LifeSupport", "Crew_CommsPower", "Crew_MissionPlanner" };
        float[] xs = { -2.75f, -2.0f, -1.25f, -0.5f };
        st.crew = new Transform[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            var c = Place(Models + "Crew/" + names[i] + ".fbx", crewRoot);
            var off = new Vector3(xs[i], 0f, -1.75f + (i % 2) * 0.2f);
            float r = new Vector2(off.x, off.z).magnitude;
            float y = r <= HardR * GroundScale ? 0f : (GroundZ(r / GroundScale) - HardTop) * GroundScale;
            c.position = P + off + Vector3.up * y;
            c.rotation = Quaternion.Euler(0f, 160f - i * 5f, 0f);   // カメラ（−Z）のほうを向き、少しロケットへ
            st.crew[i] = c;
        }

        // 炎・光・噴煙（ロケットの根元に付いていく）
        var engine = new GameObject("Engine").transform;
        engine.SetParent(go.transform, false);
        engine.position = P;
        st.engine = engine;
        st.flame = Blob("Flame", engine, Vector3.zero, new Vector3(0.3f, 1f, 0.3f));
        st.fireLight = PointLight(engine, "FireLight", Vector3.down * 0.4f, Orange, 0f, 10f);
        st.trail = Smoke(engine, "Trail", Vector3.zero, new Vector3(90f, 0f, 0f), rate: 70f, life: new Vector2(1.2f, 2.0f),
            speed: new Vector2(2.5f, 4f), size: new Vector2(0.35f, 0.7f), angle: 12f, radius: 0.12f,
            c0: new Color(1f, 0.85f, 0.65f, 0.9f), c1: new Color(0.85f, 0.85f, 0.88f, 0.6f), drag: 1.2f, growth: 3f);
        st.padSmoke = Smoke(go.transform, "PadSmoke", P - go.transform.position + Vector3.up * 0.4f, new Vector3(-90f, 0f, 0f), rate: 90f,
            life: new Vector2(1.8f, 2.8f), speed: new Vector2(2f, 3.5f), size: new Vector2(0.5f, 1.0f), angle: 85f, radius: 0.4f,
            c0: new Color(1f, 0.92f, 0.8f, 0.95f), c1: new Color(0.88f, 0.88f, 0.9f, 0.7f), drag: 1.8f, growth: 2.6f);

        // 宇宙（月へ向かう船）
        var space = new GameObject("Space").transform;
        space.SetParent(go.transform, false);
        space.localPosition = SpaceOffset;
        st.space = space;
        st.ship = Place(Models + "Ship/Ship_Stack.fbx", space);
        st.ship.localScale = Vector3.one * 0.2f;
        st.moon = Place(Models + "Bodies/Body_Moon.fbx", space);
        st.moon.localPosition = new Vector3(18f, 10f, 60f); st.moon.localScale = Vector3.one * 3f;
        st.earth = Place(Models + "Bodies/Body_Earth.fbx", space);
        st.earth.localPosition = new Vector3(-16f, -12f, 25f); st.earth.localScale = Vector3.one * 8f;
        space.gameObject.SetActive(false);

        go.SetActive(false);
        return st;
    }

    // ------------------------------------------------------------------ 演出3：帰還（ED-A・ED-B）
    static ReturnStage BuildReturn(Transform parent)
    {
        var go = new GameObject("ReturnStage");
        go.transform.SetParent(parent, false);
        go.transform.position = ReturnOrigin;
        var st = go.AddComponent<ReturnStage>();

        var ocean = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ocean.name = "Ocean";
        Object.DestroyImmediate(ocean.GetComponent<Collider>());
        ocean.transform.SetParent(go.transform, false);
        ocean.transform.localScale = new Vector3(400f, 1f, 400f);   // 4km 四方（端は遠くの切り捨て 900m より外）
        ocean.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(OceanMat);

        st.cm = Place(Models + "Ship/Ship_CM.fbx", go.transform);
        st.chute = Place(Models + "Fx/Fx_Parachute.fbx", st.cm);
        st.chute.localPosition = new Vector3(0f, 3.4f, 0f);       // DockRing の上
        st.chute.gameObject.SetActive(false);
        st.glow = Blob("HeatGlow", st.cm, new Vector3(0f, 0.1f, 0f), new Vector3(4.4f, 1.6f, 4.4f));
        st.heatLight = PointLight(st.cm, "HeatLight", new Vector3(0f, -1.5f, 0f), Orange, 0f, 12f);
        st.heatTrail = Smoke(st.cm, "HeatTrail", Vector3.zero, new Vector3(-90f, 0f, 0f), rate: 90f, life: new Vector2(0.8f, 1.3f),
            speed: new Vector2(2f, 4f), size: new Vector2(0.8f, 1.6f), angle: 25f, radius: 1.8f,
            c0: new Color(1f, 0.6f, 0.2f, 0.95f), c1: new Color(0.35f, 0.32f, 0.38f, 0.5f), drag: 1f, growth: 2f);
        st.splash = Smoke(go.transform, "Splash", Vector3.zero, new Vector3(-90f, 0f, 0f), rate: 0f, life: new Vector2(0.8f, 1.4f),
            speed: new Vector2(7f, 11f), size: new Vector2(0.6f, 1.3f), angle: 35f, radius: 1.8f,
            c0: Color.white, c1: new Color(0.9f, 0.95f, 1f, 0.6f), drag: 0.5f, growth: 1.8f, burst: 80, gravity: 1.6f);
        st.foam = Smoke(go.transform, "Foam", Vector3.zero, new Vector3(90f, 0f, 0f), rate: 0f, life: new Vector2(1.0f, 1.8f),
            speed: new Vector2(3f, 5f), size: new Vector2(1.2f, 2.2f), angle: 0f, radius: 2.0f,
            c0: Color.white, c1: new Color(0.9f, 0.95f, 1f, 0.5f), drag: 1.5f, growth: 1.6f, burst: 60, circle: true);

        go.SetActive(false);
        return st;
    }

    // ------------------------------------------------------------------ 演出3'：帰れなかった（ED-C）
    static LostStage BuildLost(Transform parent)
    {
        var go = new GameObject("LostStage");
        go.transform.SetParent(parent, false);
        go.transform.position = LostOrigin;
        var st = go.AddComponent<LostStage>();
        st.ship = Place(Models + "Ship/Ship_Stack.fbx", go.transform);
        st.ship.localScale = Vector3.one * 0.15f;
        st.earth = Place(Models + "Bodies/Body_Earth.fbx", go.transform);
        st.earth.localPosition = new Vector3(4f, -9f, 40f); st.earth.localScale = Vector3.one * 14f;
        go.SetActive(false);
        return st;
    }

    // ------------------------------------------------------------------ 演出2：管制室の先生の小窓
    static HoustonWindow BuildHouston(Transform parent, GameFlow flow, StoryDirector dir)
    {
        var go = new GameObject("HoustonWindow");
        go.transform.SetParent(parent, false);
        var hw = go.AddComponent<HoustonWindow>();
        hw.flow = flow; hw.director = dir;

        var set = new GameObject("MissionControl").transform;
        set.SetParent(go.transform, false);
        set.position = ControlOrigin;
        set.rotation = Quaternion.Euler(0f, 180f, 0f);
        hw.set = set.gameObject;

        var room = Place(Models + "Scene/Scene_MissionControl.fbx", set);
        var houston = Place(Models + "Scene/Crew_Houston.fbx", Find(room, "Scene_MissionControl.Slot_Houston") ?? room);
        hw.head = Find(houston, "Crew_Houston.Head");
        var faceT = Find(houston, "Crew_Houston.Face");
        hw.face = faceT != null ? faceT.GetComponent<SkinnedMeshRenderer>() : null;
        if (hw.face == null) Debug.LogWarning(Tag + "Crew_Houston.Face の SkinnedMeshRenderer が無い（口が動かない）");

        // うしろの管制官（3列×6人。A・B・C を順に）
        var faces = new System.Collections.Generic.List<SkinnedMeshRenderer>();
        string[] kinds = { "Crew_Controller_A", "Crew_Controller_B", "Crew_Controller_C" };
        int n = 0;
        for (int row = 1; row <= 3; row++)
            for (int k = 1; k <= 6; k++, n++)
            {
                var slot = Find(room, "Scene_MissionControl.Slot_Seat_" + row + k);
                if (slot == null) continue;
                string kind = kinds[(n + row) % 3];
                var c = Place(Models + "Scene/" + kind + ".fbx", slot);
                var h = Find(c, kind + ".Head");
                if (h != null) h.localRotation = Quaternion.Euler(0f, (n * 37 % 50) - 25f, 0f);   // 少しずつ違う向き
                var f = Find(c, kind + ".Face");
                if (f != null && f.GetComponent<SkinnedMeshRenderer>() != null) faces.Add(f.GetComponent<SkinnedMeshRenderer>());
            }
        hw.crowdFaces = faces.ToArray();

        var camSlot = Find(room, "Scene_MissionControl.Slot_PopupCamera") ?? room;
        var cam = MakeCamera(camSlot, "PopupCamera", 30f, 0.05f, 30f, false);
        cam.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // 先生のほう（モデルの −Z）
        cam.backgroundColor = new Color(0.03f, 0.05f, 0.11f, 1f);
        hw.cam = cam;
        PointLight(camSlot, "Fill", new Vector3(0.6f, 0.9f, 0f), new Color(1f, 0.89f, 0.72f), 1.2f, 8f);

        set.gameObject.SetActive(false);
        return hw;
    }

    // ------------------------------------------------------------------ 部品
    static Transform Place(string path, Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError(Tag + "モデルが無い: " + path);
            var empty = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path) + " (missing)").transform;
            empty.SetParent(parent, false);
            return empty;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    static Transform Find(Transform t, string name)
    {
        if (t == null) return null;
        if (t.name == name) return t;
        foreach (Transform c in t) { var f = Find(c, name); if (f != null) return f; }
        return null;
    }

    static Camera MakeCamera(Transform parent, string name, float fov, float near, float far, bool post)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = fov; cam.nearClipPlane = near; cam.farClipPlane = far;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.114f, 0.176f, 0.314f, 1f);
        cam.depth = -20f;
        cam.allowMSAA = true; cam.allowHDR = true;
        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = post;
        data.renderShadows = true;
        data.antialiasing = AntialiasingMode.None;
        cam.enabled = false;
        return cam;
    }

    static Light PointLight(Transform parent, string name, Vector3 local, Color c, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
        return l;
    }

    /// <summary>光る塊（炎・熱）。球を伸ばしたもの。影なし</summary>
    static Transform Blob(string name, Transform parent, Vector3 local, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        go.transform.localScale = scale;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(FireMat);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        go.SetActive(false);
        return go.transform;
    }

    /// <summary>煙・しぶき（GasLeak の作り方と同じ：URP Particles/Unlit、Kenney の煙4枚のアトラス）。局所 +Z に出る</summary>
    static ParticleSystem Smoke(Transform parent, string name, Vector3 local, Vector3 euler, float rate, Vector2 life, Vector2 speed, Vector2 size,
        float angle, float radius, Color c0, Color c1, float drag, float growth, int burst = 0, float gravity = 0f, bool circle = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        go.transform.localRotation = Quaternion.Euler(euler);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = burst > 0 ? 1f : 2f;
        main.loop = burst == 0;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 400;
        main.useUnscaledTime = true;

        var em = ps.emission;
        em.rateOverTime = rate;
        if (burst > 0) em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });

        var sh = ps.shape;
        sh.enabled = true;
        if (circle) { sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = radius; sh.radiusThickness = 0f; }
        else { sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = angle; sh.radius = radius; }

        var lv = ps.limitVelocityOverLifetime;
        lv.enabled = true; lv.limit = 100f; lv.drag = drag;
        lv.multiplyDragByParticleSize = false; lv.multiplyDragByParticleVelocity = false;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.4f, 1f + growth * 0.4f), new Keyframe(1f, 1f + growth)));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(c0.r, c0.g, c0.b), 0f), new GradientColorKey(new Color(c1.r, c1.g, c1.b), 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(c0.a, 0.08f), new GradientAlphaKey(c1.a, 0.55f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var rol = ps.rotationOverLifetime;
        rol.enabled = true; rol.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);

        var tsa = ps.textureSheetAnimation;
        tsa.enabled = true; tsa.mode = ParticleSystemAnimationMode.Grid; tsa.numTilesX = 2; tsa.numTilesY = 2;
        tsa.animation = ParticleSystemAnimationType.WholeSheet; tsa.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f); tsa.cycleCount = 1;

        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(SmokeMat);
        pr.sortMode = ParticleSystemSortMode.Distance;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;
        pr.maxParticleSize = 3f;
        return ps;
    }

    // ------------------------------------------------------------------ 素材
    static void EnsureMaterials()
    {
        if (!AssetDatabase.IsValidFolder(ArtFolder)) AssetDatabase.CreateFolder("Assets/Art", "Story");
        // 炎・熱：URP Unlit。HDR の明るさで Bloom（しきい値 0.9）に光らせる
        var fire = Mat(FireMat, "Universal Render Pipeline/Unlit");
        fire.SetColor("_BaseColor", new Color(4.0f, 1.7f, 0.4f, 1f));
        EditorUtility.SetDirty(fire);
        // 海：URP Lit。青、少しつや（強いと主光源の照り返しが白く光りすぎる）
        var ocean = Mat(OceanMat, "Universal Render Pipeline/Lit");
        ocean.SetColor("_BaseColor", new Color(0.18f, 0.43f, 0.69f, 1f));
        ocean.SetFloat("_Smoothness", 0.35f);
        ocean.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(ocean);
        AssetDatabase.SaveAssets();
    }

    static Material Mat(string path, string shader)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        var sh = Shader.Find(shader);
        if (sh == null) { Debug.LogError(Tag + "シェーダーが無い: " + shader); sh = Shader.Find("Standard"); }
        m = new Material(sh) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
