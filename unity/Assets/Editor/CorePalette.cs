// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
//
// 共通マテリアル M_Core_Palette.mat（#38）。パレットで塗ったモデル（地球・月と NASA を除く Assets/Models/ の FBX）は、
// FBX の中のマテリアル M_Core_Palette を、取り込みのときにこの1つに差し替える（差し替えは CorePaletteImport.cs）。
//
// このファイルの役目：
//   - 共通マテリアルがなければ作る（CorePaletteImport が、取り込みが終わったあと OnPostprocessAllAssets で呼ぶ）。
//     URP Lit、Base Map＝palette.png、Base Color 白、Smoothness 0、Metallic 0。
//     作ると、それを待っていた FBX（取り込み時に context.DependsOnSourceAsset で登録）を Unity が自動で読み直す。
//   - メニュー Tools/Models/Core Palette を確かめる：全モデルの Renderer のマテリアルを調べて "[CorePalette]" でログに出す。
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class CorePalette
{
    public const string Tag = "[CorePalette] ";
    public const string ModelsFolder = "Assets/Models/";
    public const string MaterialPath = "Assets/Models/Core/M_Core_Palette.mat";
    public const string TexturePath = "Assets/Models/Core/palette.png";
    public const string MaterialName = "M_Core_Palette";   // FBX の中のマテリアル名（modeling/core/palette.json の "material"）
    const string ShaderName = "Universal Render Pipeline/Lit";

    // 差し替えないフォルダ（地球・月は自分の画像、NASA は midori の物）
    static readonly string[] Excluded = { "Assets/Models/NASA/", "Assets/Models/Bodies/" };

    /// <summary>共通で差し替える FBX か（Assets/Models/ の .fbx、NASA と Bodies を除く）</summary>
    public static bool InScope(string path)
    {
        if (!path.StartsWith(ModelsFolder) || !path.ToLowerInvariant().EndsWith(".fbx")) return false;
        foreach (var ex in Excluded) if (path.StartsWith(ex)) return false;
        return true;
    }

    public static bool MaterialExists() =>
        !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(MaterialPath, AssetPathToGUIDOptions.OnlyExistingAssets));

    /// <summary>共通マテリアルがなければ作る。取り込みの最中（OnPreprocess〜）には呼ばない（CorePaletteImport.OnPostprocessAllAssets から呼ぶ）。</summary>
    public static void CreateMaterialIfMissing()
    {
        if (MaterialExists()) return;
        var shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError(Tag + "シェーダー " + ShaderName + " が見つからないので " + MaterialPath + " を作れない");
            return;
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (tex == null) Debug.LogWarning(Tag + TexturePath + " がないので、画像なしで作る");
        var mat = new Material(shader) { name = MaterialName };
        mat.SetTexture("_BaseMap", tex);
        mat.SetTexture("_MainTex", tex);         // URP のマテリアル画面と同じく、古い名前にも入れておく
        mat.SetColor("_BaseColor", Color.white);
        mat.SetColor("_Color", Color.white);
        mat.SetFloat("_Smoothness", 0f);
        mat.SetFloat("_Metallic", 0f);
        AssetDatabase.CreateAsset(mat, MaterialPath);
        Debug.Log(Tag + MaterialPath + " を作った（差し替えを待っている FBX はこのあと自動で読み直される）");
    }

    [MenuItem("Tools/Models/Core Palette を確かめる")]
    public static void Report()
    {
        var shared = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var sb = new StringBuilder();
        int ok = 0, ng = 0, other = 0;
        sb.Append(Tag).Append("共通マテリアル ").Append(MaterialPath).Append(": ");
        if (shared == null) sb.Append("なし\n");
        else
        {
            var bm = shared.GetTexture("_BaseMap");
            sb.Append("shader=").Append(shared.shader.name)
              .Append(" baseMap=").Append(bm != null ? AssetDatabase.GetAssetPath(bm) : "(none)")
              .Append(" baseColor=").Append(shared.GetColor("_BaseColor"))
              .Append(" smoothness=").Append(shared.GetFloat("_Smoothness"))
              .Append(" metallic=").Append(shared.GetFloat("_Metallic")).Append('\n');
            var ti = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (ti != null)
                sb.Append(Tag).Append(TexturePath).Append(": filter=").Append(ti.filterMode)
                  .Append(" mipmap=").Append(ti.mipmapEnabled).Append(" compression=").Append(ti.textureCompression)
                  .Append(" sRGB=").Append(ti.sRGBTexture).Append('\n');
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsFolder.TrimEnd('/') }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Assets/Models/NASA/")) continue;   // midori の物は見ない
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            var names = new SortedSet<string>();
            int renderers = 0, onShared = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                renderers++;
                bool allShared = r.sharedMaterials.Length > 0;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) { names.Add("(none)"); allShared = false; continue; }
                    var mp = AssetDatabase.GetAssetPath(m);
                    names.Add(m.name + (mp == path ? "(FBX内)" : "(" + mp + ")"));
                    if (m != shared) allShared = false;
                }
                if (allShared) onShared++;
            }
            string state;
            if (InScope(path)) { bool good = shared != null && renderers > 0 && onShared == renderers; state = good ? "OK" : "NG"; if (good) ok++; else ng++; }
            else { state = "対象外"; other++; }
            sb.Append(Tag).Append(state).Append(' ').Append(path).Append("  renderers=").Append(renderers)
              .Append(" 共通=").Append(onShared).Append(" mats=").Append(string.Join(" ", names)).Append('\n');
        }
        sb.Append(Tag).Append("合計 OK=").Append(ok).Append(" NG=").Append(ng).Append(" 対象外=").Append(other);
        if (ng > 0) Debug.LogError(sb.ToString()); else Debug.Log(sb.ToString());
    }
}
