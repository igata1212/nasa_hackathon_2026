// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
//
// LookDev（#55）で使う素材を Unity に作らせる。どれも「なければ作る・あれば値を上書き」（GUID は変えない）。
//   - 画像の取り込み設定（空・煙）：AssetPostprocessor で毎回決める
//   - 空のマテリアル     Assets/Art/Sky/M_Sky_Space.mat（Skybox/Panoramic、画像は art/make_sky.py で作る）
//   - 煙のマテリアル     Assets/Art/VFX/M_Smoke_Gas.mat（URP Particles/Unlit、透明・アルファ。加算ではない）
//   - 煙のプレハブ       Assets/Art/VFX/GasLeak.prefab（Tank_Oxygen2_Broken の .Slot_Leak の子に置く。局所 +Y に噴く）
//   - ランプの発光       Assets/Art/Materials/M_Lamp_{Red,Yellow,Green}.mat（URP Lit＋Emission。LampGlow が .Lamp_ に当てる）
//   - 仮の床             Assets/Art/LookDev/LookDev_Floor.asset（円盤のメッシュ。UV はパレットの1マスの中心＝共通マテリアルの色）
// メニュー：Tools/LookDev/素材を作り直す（シーンを作り直すときにも呼ばれる）
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class LookDevAssets
{
    public const string ArtRoot = "Assets/Art";
    public const string SkyTexture = "Assets/Art/Sky/Sky_Space_Navy.png";
    public const string SkyMaterial = "Assets/Art/Sky/M_Sky_Space.mat";
    public const string SmokeTexture = "Assets/Art/VFX/Smoke_Puff_2x2.png";
    public const string SmokeMaterial = "Assets/Art/VFX/M_Smoke_Gas.mat";
    public const string GasLeakPrefab = "Assets/Art/VFX/GasLeak.prefab";
    public const string LampMaterialFormat = "Assets/Art/Materials/M_Lamp_{0}.mat";
    public const string FloorMesh = "Assets/Art/LookDev/LookDev_Floor.asset";

    // palette.json の色（ランプ）。発光は「色 × 2^EV」（リニア）。Bloom の閾値 0.9 を超えて、ランプがにじむ強さ
    public static readonly Color LampRed = Hex("#FF3B30"), LampYellow = Hex("#FFD60A"), LampGreen = Hex("#2ECC71");
    public const float LampEmissionEV = 2.5f;

    // 煙の色（酸素ガス。白にごく薄い青）
    public static readonly Color SmokeColor = new Color(0.93f, 0.97f, 1f, 0.85f);

    public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    /// <summary>palette.json の色名 → マスの中心の UV（4×4、上の行から 船内／席／無彩色／黒紺＋ランプ）</summary>
    public static Vector2 PaletteUV(int col, int row) => new Vector2(0.125f + 0.25f * col, 0.875f - 0.25f * row);

    [MenuItem("Tools/LookDev/素材を作り直す")]
    public static void BuildAll()
    {
        EnsureFolder("Assets/Art/Sky"); EnsureFolder("Assets/Art/VFX"); EnsureFolder("Assets/Art/Materials"); EnsureFolder("Assets/Art/LookDev");
        AssetDatabase.ImportAsset(SkyTexture); AssetDatabase.ImportAsset(SmokeTexture);
        BuildSkyMaterial();
        BuildLampMaterials();
        BuildSmokeMaterial();
        BuildGasLeakPrefab();
        BuildFloorMesh(3f, 0.06f, 64, PaletteUV(2, 2));   // 直径 6m、gray #8B93A1
        AssetDatabase.SaveAssets();
        Debug.Log("[LookDev] 素材を作り直した（空・煙・ランプ・床）");
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static Material LoadOrCreateMaterial(string path, string shaderName)
    {
        var shader = Shader.Find(shaderName);
        if (shader == null) throw new Exception("[LookDev] シェーダー " + shaderName + " がない");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(m, path);
        }
        else if (m.shader != shader) m.shader = shader;
        return m;
    }

    /// <summary>URP のマテリアル画面と同じやり方でキーワードなどを整える（ShaderGUI.ValidateMaterial。URP のクラスは internal なので名前で探す）</summary>
    static void Validate(Material m, string shaderGuiType)
    {
        foreach (var t in TypeCache.GetTypesDerivedFrom<ShaderGUI>())
        {
            if (t.FullName != shaderGuiType) continue;
            var gui = (ShaderGUI)Activator.CreateInstance(t, true);
            gui.ValidateMaterial(m);
            return;
        }
        Debug.LogWarning("[LookDev] " + shaderGuiType + " が見つからないので " + m.name + " のキーワードは手で付けたまま");
    }

    public static Material BuildSkyMaterial()
    {
        var m = LoadOrCreateMaterial(SkyMaterial, "Skybox/Panoramic");
        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(SkyTexture));
        m.SetFloat("_Mapping", 1f);      // Latitude Longitude Layout
        m.SetFloat("_ImageType", 0f);    // 360 度
        m.SetFloat("_MirrorOnBack", 0f);
        m.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 1f));   // 0.5 で元の色のまま
        m.SetFloat("_Exposure", 1.8f);
        m.SetFloat("_Rotation", 200f);   // 銀河が断面カメラの奥に少し見える向き
        EditorUtility.SetDirty(m);
        return m;
    }

    public static Material LampMaterial(string color) => AssetDatabase.LoadAssetAtPath<Material>(string.Format(LampMaterialFormat, color));

    static void BuildLampMaterials()
    {
        BuildLamp("Red", LampRed); BuildLamp("Yellow", LampYellow); BuildLamp("Green", LampGreen);
    }

    static void BuildLamp(string name, Color c)
    {
        var m = LoadOrCreateMaterial(string.Format(LampMaterialFormat, name), "Universal Render Pipeline/Lit");
        m.SetColor("_BaseColor", c);
        m.SetColor("_Color", c);
        m.SetFloat("_Smoothness", 0.1f);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_SpecularHighlights", 0f);       // 白いハイライトが乗るとピンクに見えるので消す
        m.SetFloat("_EnvironmentReflections", 0f);
        m.SetColor("_EmissionColor", Emission(c, LampEmissionEV));
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        Validate(m, "UnityEditor.Rendering.Universal.ShaderGUI.LitShader");
        m.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(m);
    }

    /// <summary>発光の色。そのまま「色 × 2^EV」にすると弱い成分（赤なら緑・青）まで持ち上がり、ピンクに見える。
    /// そこで成分ごとに、強い成分ほど大きく持ち上げる（いちばん強い成分 × 2^EV、弱い成分はほぼそのまま）。</summary>
    public static Color Emission(Color srgb, float ev)
    {
        var l = srgb.linear;
        float max = Mathf.Max(l.r, Mathf.Max(l.g, l.b, 1e-4f));
        float k = Mathf.Pow(2f, ev);
        return new Color(l.r * Mathf.Pow(k, l.r / max), l.g * Mathf.Pow(k, l.g / max), l.b * Mathf.Pow(k, l.b / max), 1f);
    }

    static void BuildSmokeMaterial()
    {
        var m = LoadOrCreateMaterial(SmokeMaterial, "Universal Render Pipeline/Particles/Unlit");
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SmokeTexture);
        m.SetTexture("_BaseMap", tex);
        m.SetTexture("_MainTex", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1f);   // Transparent
        m.SetFloat("_Blend", 0f);     // Alpha（加算ではない）
        m.SetFloat("_ColorMode", 0f); // Multiply（パーティクルの色をかける）
        m.SetFloat("_SoftParticlesEnabled", 0f);
        m.SetFloat("_CameraFadingEnabled", 0f);
        m.SetFloat("_FlipbookBlending", 0f);
        Validate(m, "UnityEditor.Rendering.Universal.ShaderGUI.ParticlesUnlitShader");
        EditorUtility.SetDirty(m);
    }

    /// <summary>酸素ガスの漏れ。ルートを .Slot_Leak に重ねて置く（ルートの局所 +Y＝穴の外向き）。</summary>
    static void BuildGasLeakPrefab()
    {
        var root = new GameObject("GasLeak");
        try
        {
            var smoke = new GameObject("Smoke");
            smoke.transform.SetParent(root.transform, false);
            smoke.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // パーティクルは局所 +Z に出るので、+Y（穴の外向き）に向ける
            var ps = smoke.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 2f;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.6f, 3.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.13f, 0.208f);   // 1.3 倍（元は 0.10〜0.16）
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = SmokeColor;
            main.gravityModifier = 0f;   // 無重力：昇らない（#64）
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 120;

            var em = ps.emission;
            em.rateOverTime = 28f;

            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 55f;   // 穴の向きに弱く出て、四方に広がる（#64）
            sh.radius = 0.03f;

            // 勢いよく出て、すぐ減速して漂う
            var lv = ps.limitVelocityOverLifetime;
            lv.enabled = true;
            lv.limit = 100f;
            lv.drag = 1.6f;
            lv.multiplyDragByParticleSize = false;
            lv.multiplyDragByParticleVelocity = false;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.5f), new Keyframe(0.4f, 2.0f), new Keyframe(1f, 4.2f)));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.88f, 0.93f, 1f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.5f, 0.55f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var rol = ps.rotationOverLifetime;
            rol.enabled = true;
            rol.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.05f;
            noise.frequency = 0.8f;
            noise.scrollSpeed = 0.3f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            // 2×2 のアトラスから1枚をランダムに選ぶ（寿命の間は同じ絵）
            var tsa = ps.textureSheetAnimation;
            tsa.enabled = true;
            tsa.mode = ParticleSystemAnimationMode.Grid;
            tsa.numTilesX = 2;
            tsa.numTilesY = 2;
            tsa.animation = ParticleSystemAnimationType.WholeSheet;
            tsa.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f);
            tsa.cycleCount = 1;

            var pr = smoke.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(SmokeMaterial);
            pr.sortMode = ParticleSystemSortMode.Distance;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            pr.maxParticleSize = 2f;

            PrefabUtility.SaveAsPrefabAsset(root, GasLeakPrefab);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    /// <summary>円盤（上面が y=0）。UV は全部パレットの1マスの中心なので、共通マテリアルでその色になる。</summary>
    static void BuildFloorMesh(float radius, float thickness, int seg, Vector2 uv)
    {
        var verts = new System.Collections.Generic.List<Vector3>();
        var norms = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();
        // 上面
        verts.Add(Vector3.zero); norms.Add(Vector3.up);
        for (int i = 0; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            verts.Add(new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius)); norms.Add(Vector3.up);
        }
        for (int i = 0; i < seg; i++) { tris.Add(0); tris.Add(i + 2); tris.Add(i + 1); }
        // 側面
        int s0 = verts.Count;
        for (int i = 0; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            var n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            verts.Add(n * radius); norms.Add(n);
            verts.Add(n * radius + Vector3.down * thickness); norms.Add(n);
        }
        for (int i = 0; i < seg; i++)
        {
            int a = s0 + i * 2, b = a + 2;
            tris.Add(a); tris.Add(b); tris.Add(a + 1);
            tris.Add(b); tris.Add(b + 1); tris.Add(a + 1);
        }
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(FloorMesh);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh { name = "LookDev_Floor" };
        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        var uvs = new Vector2[verts.Count];
        for (int i = 0; i < uvs.Length; i++) uvs[i] = uv;
        mesh.uv = uvs;
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        if (isNew) AssetDatabase.CreateAsset(mesh, FloorMesh); else EditorUtility.SetDirty(mesh);
    }
}

/// <summary>Assets/Art の画像の取り込み設定（空：ミップなし・横だけ繰り返し、煙：アルファを透明として扱う）</summary>
public class LookDevTextureImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        var ti = (TextureImporter)assetImporter;
        if (assetPath == LookDevAssets.SkyTexture)
        {
            ti.textureType = TextureImporterType.Default;
            ti.textureShape = TextureImporterShape.Texture2D;
            ti.sRGBTexture = true;
            ti.mipmapEnabled = false;            // 正距円筒の継ぎ目に線が出ないように
            ti.wrapModeU = TextureWrapMode.Repeat;
            ti.wrapModeV = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.npotScale = TextureImporterNPOTScale.None;
        }
        else if (assetPath == LookDevAssets.SmokeTexture)
        {
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.maxTextureSize = 512;
            ti.textureCompression = TextureImporterCompression.Compressed;
        }
    }
}
