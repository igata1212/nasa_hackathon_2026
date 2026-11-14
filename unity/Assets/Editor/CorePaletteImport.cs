// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
//
// パレットで塗ったモデルの取り込み設定（#38）。手で設定しないで済むように、取り込みのたびにここで決める。
//   - palette.png：Point フィルタ・ミップマップなし・圧縮なし・sRGB（16色のマスの色がにじまない・変わらないように）
//   - Assets/Models/ の FBX（NASA と Bodies を除く）：FBX の中のマテリアル M_Core_Palette を、
//     共通の Assets/Models/Core/M_Core_Palette.mat に差し替える（ModelImporter.AddRemap。.meta の externalObjects に残る）
// 共通マテリアルがまだないときは：差し替えずに取り込み（灰色のまま）、マテリアルへの依存だけ登録する →
// 取り込みのあと OnPostprocessAllAssets で CorePalette.CreateMaterialIfMissing が作る → Unity がその FBX を読み直し、こんどは差し替わる。
using UnityEditor;
using UnityEngine;

public class CorePaletteImport : AssetPostprocessor
{
    static readonly AssetImporter.SourceAssetIdentifier Key =
        new AssetImporter.SourceAssetIdentifier(typeof(Material), CorePalette.MaterialName);

    void OnPreprocessTexture()
    {
        if (assetPath != CorePalette.TexturePath) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Default;
        ti.sRGBTexture = true;
        ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Point;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.isReadable = false;
    }

    void OnPreprocessModel()
    {
        if (!CorePalette.InScope(assetPath)) return;
        var imp = (ModelImporter)assetImporter;
        var mat = AssetDatabase.LoadAssetAtPath<Material>(CorePalette.MaterialPath);
        if (mat == null)
        {
            // まだ共通マテリアルがない。できたら Unity がこの FBX を読み直すように依存を登録しておく
            context.DependsOnSourceAsset(CorePalette.MaterialPath);
            Debug.Log(CorePalette.Tag + CorePalette.MaterialPath + " がまだないので、" + assetPath + " は差し替えずに取り込む（あとで読み直す）");
            return;
        }
        if (!IsMapped(imp, mat)) imp.AddRemap(Key, mat);
    }

    // 取り込みが終わったあと。パレットの FBX か画像が入ったのに共通マテリアルがなければ作る（取り込みの最中は作れないのでここで）。
    // 共通マテリアルを消したときも作り直す（GUID が変わるので、パレットの FBX を全部読み直して差し替え先を付け直す）。
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (CorePalette.MaterialExists()) return;
        bool wasDeleted = System.Array.IndexOf(deleted, CorePalette.MaterialPath) >= 0
                       || System.Array.IndexOf(movedFrom, CorePalette.MaterialPath) >= 0;
        bool needed = wasDeleted;
        foreach (var p in imported) if (p == CorePalette.TexturePath || CorePalette.InScope(p)) { needed = true; break; }
        if (!needed) return;
        CorePalette.CreateMaterialIfMissing();
        if (!wasDeleted) return;
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { CorePalette.ModelsFolder.TrimEnd('/') }))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            if (CorePalette.InScope(p)) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
        }
    }

    /// <summary>この FBX の M_Core_Palette が、共通マテリアルに差し替わる設定になっているか</summary>
    public static bool IsMapped(ModelImporter imp, Material mat)
    {
        return imp.GetExternalObjectMap().TryGetValue(Key, out var cur) && cur == mat;
    }
}
