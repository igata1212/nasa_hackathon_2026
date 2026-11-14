// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
//
// 光と後処理の設定（#55）。値はここに集めて、docs/design/art-direction.md の「光と後処理」の表と合わせる。
//   1. URP の設定（PC・Mobile の両方の品質。WebGL と iOS は Mobile を使う）：Forward、影、MSAA、HDR、SSAO
//      メニュー Tools/LookDev/URP の設定をそろえる
//   2. シーンの光（主光源・リム光・環境光・空・後処理の Volume）：ApplyLighting(親) を今のシーンに作る
//      メニュー Tools/LookDev/今のシーンに光を入れる（船室のシーンができたら、そこでも使える）
// 後処理の値は Assets/Art/LookDev/LookDev_Volume.asset（Volume Profile）。シーンの Global Volume がこれを指す。
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class LookDevSettings
{
    public const string Tag = "[LookDev] ";
    public const string VolumeProfilePath = "Assets/Art/LookDev/LookDev_Volume.asset";
    static readonly string[] PipelineAssets = { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" };
    static readonly string[] RendererAssets = { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" };

    // ---- URP（両方の品質で同じ値） ----
    public const int MsaaSamples = 2;
    public const int ShadowResolution = 1024;
    public const int ShadowCascades = 1;
    public const float ShadowDistance = 15f;     // カメラから約 7〜10m。船室（#40）も入る距離
    public const float ShadowDepthBias = 0.6f;
    public const float ShadowNormalBias = 0.6f;
    public const SoftShadowQuality SoftShadows = SoftShadowQuality.Medium;
    // SSAO（Renderer Feature）
    public const float SsaoIntensity = 0.4f;
    public const float SsaoRadius = 0.25f;
    public const float SsaoDirect = 0.25f;

    // ---- 光 ----
    public static readonly Color KeyColor = LookDevAssets.Hex("#FFE2B8");
    public const float KeyIntensity = 1.4f;
    public static readonly Vector3 KeyEuler = new Vector3(55f, -15f, 0f);
    public const float KeyShadowStrength = 0.6f;
    public static readonly Color RimColor = LookDevAssets.Hex("#9DB4FF");
    public const float RimIntensity = 0.4f;
    public static readonly Vector3 RimEuler = new Vector3(25f, 150f, 0f);
    // 環境光（Gradient）：上は暖かく、横は中間、下は暗く
    public static readonly Color AmbientSky = LookDevAssets.Hex("#B8A898");
    public static readonly Color AmbientEquator = LookDevAssets.Hex("#8A7F86");
    public static readonly Color AmbientGround = LookDevAssets.Hex("#5A5058");
    public const float ReflectionIntensity = 0.4f;

    // ---- 室内の点光源（H2 が LookDevScene で置く。影は付けない：追加ライトの影は ApplyUrp で切っている） ----
    public const float AlarmLightIntensity = 2.0f;   // 警報ランプ（Slot_Alarm_1・_2）の Point Light
    public const float AlarmLightRange = 3.5f;       // m
    public static readonly Color InteriorLightColor = LookDevAssets.Hex("#FFE2B8");   // 室内灯（Slot_Center の上）
    public const float InteriorLightIntensity = 1.5f;
    public const float InteriorLightRange = 5f;      // m
    public const float InteriorLightHeight = 2.2f;   // Slot_Center からの高さ（m）

    // ---- 後処理 ----
    public const float BloomThreshold = 0.9f;
    public const float BloomIntensity = 0.7f;
    public const float BloomScatter = 0.6f;
    public const float Contrast = 12f;
    public const float Saturation = 16f;
    public const float PostExposure = 0f;
    public const float VignetteIntensity = 0.12f;
    public const float VignetteSmoothness = 0.45f;
    public const float WhiteBalanceTemperature = 15f;

    [MenuItem("Tools/LookDev/URP の設定をそろえる")]
    public static void ApplyUrp()
    {
        var log = new System.Text.StringBuilder(Tag + "URP の設定\n");
        foreach (var path in PipelineAssets)
        {
            var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (a == null) { Debug.LogError(Tag + path + " がない"); continue; }
            a.msaaSampleCount = MsaaSamples;
            a.supportsHDR = true;
            a.mainLightShadowmapResolution = ShadowResolution;
            a.shadowCascadeCount = ShadowCascades;
            a.shadowDistance = ShadowDistance;
            a.shadowDepthBias = ShadowDepthBias;
            a.shadowNormalBias = ShadowNormalBias;
            var so = new SerializedObject(a);
            so.FindProperty("m_MainLightShadowsSupported").boolValue = true;
            so.FindProperty("m_SoftShadowsSupported").boolValue = true;
            so.FindProperty("m_SoftShadowQuality").intValue = (int)SoftShadows;
            so.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;   // 影は主光源だけ
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(a);
            log.Append(path).Append(": msaa=").Append(a.msaaSampleCount).Append(" hdr=").Append(a.supportsHDR)
               .Append(" shadowRes=").Append(a.mainLightShadowmapResolution).Append(" cascades=").Append(a.shadowCascadeCount)
               .Append(" dist=").Append(a.shadowDistance).Append(" soft=").Append(SoftShadows).Append(" renderScale=").Append(a.renderScale).Append('\n');
        }

        ScriptableRendererFeature ssaoSource = null;
        foreach (var path in RendererAssets)
        {
            var d = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (d == null) { Debug.LogError(Tag + path + " がない"); continue; }
            d.renderingMode = RenderingMode.Forward;   // Forward+ は WebGL で不具合報告がある
            var ssao = d.rendererFeatures.FirstOrDefault(f => f is ScreenSpaceAmbientOcclusion);
            if (ssao == null && ssaoSource != null) ssao = AddFeatureCopy(d, ssaoSource);
            if (ssao != null)
            {
                ssaoSource = ssaoSource ?? ssao;
                ssao.SetActive(true);
                var so = new SerializedObject(ssao);
                var s = so.FindProperty("m_Settings");
                s.FindPropertyRelative("Downsample").boolValue = true;
                s.FindPropertyRelative("Source").enumValueIndex = 1;          // DepthNormals
                s.FindPropertyRelative("NormalSamples").enumValueIndex = 1;   // Medium
                s.FindPropertyRelative("Samples").enumValueIndex = 1;         // Medium（Low はざらつきが目立った）
                s.FindPropertyRelative("BlurQuality").enumValueIndex = 0;     // High（ぼかしでざらつきを消す）
                s.FindPropertyRelative("Intensity").floatValue = SsaoIntensity;
                s.FindPropertyRelative("Radius").floatValue = SsaoRadius;
                s.FindPropertyRelative("DirectLightingStrength").floatValue = SsaoDirect;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(ssao);
            }
            EditorUtility.SetDirty(d);
            log.Append(path).Append(": mode=").Append(d.renderingMode).Append(" ssao=").Append(ssao != null).Append('\n');
        }
        AssetDatabase.SaveAssets();
        Debug.Log(log.ToString());
    }

    /// <summary>ほかの Renderer の Feature を写して足す（シェーダーや青ノイズの画像の参照ごと写る）</summary>
    static ScriptableRendererFeature AddFeatureCopy(UniversalRendererData d, ScriptableRendererFeature src)
    {
        var f = Object.Instantiate(src);
        f.name = src.name;
        AssetDatabase.AddObjectToAsset(f, d);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(f, out string _, out long localId);
        var so = new SerializedObject(d);
        var list = so.FindProperty("m_RendererFeatures");
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = f;
        var map = so.FindProperty("m_RendererFeatureMap");
        map.arraySize++;
        map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
        so.ApplyModifiedPropertiesWithoutUndo();
        return f;
    }

    [MenuItem("Tools/LookDev/今のシーンに光を入れる")]
    public static void ApplyLightingToActiveScene()
    {
        LookDevAssets.BuildAll();
        var parent = GameObject.Find("Lighting") ?? new GameObject("Lighting");
        ApplyLighting(parent.transform);
        EditorSceneManager.MarkSceneDirty(parent.scene);
    }

    /// <summary>親の下に主光源・リム光・Global Volume を作り直し、環境光と空を今のシーンに設定する。主光源を返す。</summary>
    public static Light ApplyLighting(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--) Object.DestroyImmediate(parent.GetChild(i).gameObject);

        var key = NewLight(parent, "Key Light", KeyColor, KeyIntensity, KeyEuler, LightShadows.Soft);
        key.shadowStrength = KeyShadowStrength;
        var keyData = key.GetComponent<UniversalAdditionalLightData>() ?? key.gameObject.AddComponent<UniversalAdditionalLightData>();
        keyData.softShadowQuality = SoftShadowQuality.UsePipelineSettings;
        NewLight(parent, "Rim Light", RimColor, RimIntensity, RimEuler, LightShadows.None);

        var vgo = new GameObject("Global Volume");
        vgo.transform.SetParent(parent, false);
        var vol = vgo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 10f;
        vol.sharedProfile = BuildVolumeProfile();

        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(LookDevAssets.SkyMaterial);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = AmbientSky;
        RenderSettings.ambientEquatorColor = AmbientEquator;
        RenderSettings.ambientGroundColor = AmbientGround;
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = ReflectionIntensity;
        RenderSettings.sun = key;
        RenderSettings.fog = false;
        DynamicGI.UpdateEnvironment();
        return key;
    }

    static Light NewLight(Transform parent, string name, Color c, float intensity, Vector3 euler, LightShadows shadows)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localRotation = Quaternion.Euler(euler);
        var l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.color = c;
        l.intensity = intensity;
        l.shadows = shadows;
        go.AddComponent<UniversalAdditionalLightData>();
        return l;
    }

    /// <summary>後処理の Volume Profile を作る（あれば中身を作り直す。GUID は変えない）</summary>
    public static VolumeProfile BuildVolumeProfile()
    {
        LookDevAssets.EnsureFolder("Assets/Art/LookDev");
        var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (p == null)
        {
            p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, VolumeProfilePath);
        }
        foreach (var c in p.components.ToArray()) Object.DestroyImmediate(c, true);
        p.components.Clear();

        var tm = Add<Tonemapping>(p);
        tm.mode.Override(TonemappingMode.Neutral);

        var bloom = Add<Bloom>(p);
        bloom.threshold.Override(BloomThreshold);
        bloom.intensity.Override(BloomIntensity);
        bloom.scatter.Override(BloomScatter);
        bloom.tint.Override(Color.white);
        bloom.highQualityFiltering.Override(false);
        bloom.dirtIntensity.Override(0f);

        var ca = Add<ColorAdjustments>(p);
        ca.postExposure.Override(PostExposure);
        ca.contrast.Override(Contrast);
        ca.saturation.Override(Saturation);
        ca.colorFilter.Override(Color.white);
        ca.hueShift.Override(0f);

        var wb = Add<WhiteBalance>(p);
        wb.temperature.Override(WhiteBalanceTemperature);
        wb.tint.Override(0f);

        var vg = Add<Vignette>(p);
        vg.intensity.Override(VignetteIntensity);
        vg.smoothness.Override(VignetteSmoothness);
        vg.color.Override(Color.black);
        vg.rounded.Override(false);

        // 既定の Profile（SampleSceneProfile）に入っている物を止める
        var mb = Add<MotionBlur>(p);
        mb.intensity.Override(0f);

        EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();
        return p;
    }

    static T Add<T>(VolumeProfile p) where T : VolumeComponent
    {
        var c = p.Add<T>(false);
        c.name = typeof(T).Name;
        c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(c, p);
        return c;
    }
}
