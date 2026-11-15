// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-15
// 設計の画面の 3D の舞台（#168）の参照 Assets/Resources/DesignStage.asset と、地面・台のマテリアルを作る。
//   メニュー Tools/UI/Design Stage を作る。GameFlowScene.BuildScene（シーンを作り直す）からも呼ぶ
// 予備の酸素タンクは Tank_OxygenSpare1（2個とも同じ物。#168 の計画 5章）
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DesignStageAssetsMenu
{
    const string AssetPath = "Assets/Resources/DesignStage.asset";
    const string SurfacePath = "Assets/Art/UI/Design/M_DesignStage.mat";

    static readonly (string id, string path)[] Parts =
    {
        ("battery", "Assets/Models/Part/Part_Battery.fbx"),
        ("solar_panel", "Assets/Models/Part/Part_SolarPanel.fbx"),
        ("spare_o2_tank", "Assets/Models/Tank/Tank_OxygenSpare1.fbx"),
        ("extra_fuel_tank", "Assets/Models/Part/Part_FuelTank.fbx"),
        ("camera", "Assets/Models/Part/Part_Camera.fbx"),
        ("spectrometer", "Assets/Models/Part/Part_Spectrometer.fbx"),
        ("radar", "Assets/Models/Part/Part_RadarSounder.fbx"),
        ("high_gain_antenna", "Assets/Models/Part/Part_HighGainAntenna.fbx"),
    };

    [MenuItem("Tools/UI/Design Stage を作る")]
    public static void Build()
    {
        var a = AssetDatabase.LoadAssetAtPath<DesignStageAssets>(AssetPath);
        if (a == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            a = ScriptableObject.CreateInstance<DesignStageAssets>();
            AssetDatabase.CreateAsset(a, AssetPath);
        }
        a.ship = Model("Assets/Models/Ship/Ship_Stack.fbx");
        a.rocketSmall = Model("Assets/Models/Ship/Ship_RocketSmall.fbx");
        a.rocketLarge = Model("Assets/Models/Ship/Ship_RocketLarge.fbx");
        a.parts = new DesignStageAssets.Part[Parts.Length];
        for (int i = 0; i < Parts.Length; i++) a.parts[i] = new DesignStageAssets.Part { id = Parts[i].id, prefab = Model(Parts[i].path) };
        a.surface = Surface();
        EditorUtility.SetDirty(a);
        AssetDatabase.SaveAssets();
        Debug.Log("[DesignStage] " + AssetPath + " を作った");
    }

    static GameObject Model(string path)
    {
        var g = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (g == null) Debug.LogWarning("[DesignStage] モデルが無い: " + path);
        return g;
    }

    /// <summary>地面・台・ふちの輪（URP Lit。色は DesignStage が MaterialPropertyBlock で入れる）</summary>
    static Material Surface()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(SurfacePath);
        if (mat != null) return mat;
        Directory.CreateDirectory(Path.GetDirectoryName(SurfacePath));
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_DesignStage" };
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.15f);
        mat.SetFloat("_Metallic", 0f);
        AssetDatabase.CreateAsset(mat, SurfacePath);
        return mat;
    }
}
