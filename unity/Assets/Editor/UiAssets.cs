// 書いた人：iizuka（作成：Claude）／ 最終更新：2026-11-14
// UI の素材の Unity 側の設定（#72。UI ガイドライン 5章）。手で直さず、ここを直して作り直す。
//   メニュー Tools/UI/素材を設定し直す
//   1) Art/UI/Sprites と Icons を Sprite（2D and UI）にする。角丸とピルは 9-slice の border（art/ui/README.md の表）、Mesh Type は Full Rect
//   2) TextMeshPro の Font Asset を作る（無いときだけ）。Baloo 2・Nunito は Static（ASCII＋記号を焼く）、Zen Maru Gothic は Dynamic で両方の Fallback
//   3) Assets/Resources/UiTheme.asset に書体とスプライト、部屋の画面の 3D（クルー・地球・光る台 Art/UI/Lobby/M_LobbyPad.mat）の参照を入れる（UiTokens.Theme が読む）
// TMP Essential Resources（Assets/TextMesh Pro）は先に取り込んでおく（Window/TextMeshPro/Import TMP Essential Resources）
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class UiAssets
{
    const string Sprites = "Assets/Art/UI/Sprites", Icons = "Assets/Art/UI/Icons", Fonts = "Assets/Art/UI/Fonts/";
    const string ThemePath = "Assets/Resources/UiTheme.asset";
    const string PadPath = "Assets/Art/UI/Lobby/M_LobbyPad.mat";
    static readonly string[] CrewModels = { "Crew_Pilot", "Crew_LifeSupport", "Crew_CommsPower", "Crew_MissionPlanner" };   // Seats.All の順
    static readonly Dictionary<string, int> Borders = new Dictionary<string, int> { { "round16", 16 }, { "round28", 28 }, { "round40", 40 }, { "pill", 63 } };
    // #190 の形の 9-slice（左・下・右・上。art/ui/README.md の表）
    static readonly Dictionary<string, Vector4> Borders190 = new Dictionary<string, Vector4> {
        { "blob_bubble", new Vector4(120, 96, 120, 96) }, { "blob_panel", new Vector4(72, 72, 72, 72) }, { "blob_pill", new Vector4(48, 0, 48, 0) } };
    static readonly HashSet<string> Repeat = new HashSet<string> { "stripes64", "wave_edge", "bands64" };
    const string Extra = "°₂→✓×▲●■◆▶…";   // ASCII のほかに焼く記号（ガイドライン 5章。… は長い名前を切るとき TMP の Ellipsis が使う）。書体に無いものは Fallback で出る
    const string SeatShapes = "▲●■◆";

    [MenuItem("Tools/UI/素材を設定し直す")]
    public static void Rebuild()
    {
        ImportSprites();
        var zen = FontAsset("ZenMaruGothic-Bold", 64, 6, false, SeatShapes, null);
        var baloo = FontAsset("Baloo2-ExtraBold", 80, 8, true, Ascii() + Extra, zen);
        var nunito = FontAsset("Nunito-Bold", 72, 8, true, Ascii() + Extra, zen);
        var strong = FontAsset("Nunito-ExtraBold", 72, 8, true, Ascii() + Extra, zen);   // #190
        BuildTheme(baloo, nunito, zen, strong);
        AssetDatabase.SaveAssets();
        Debug.Log("[UiAssets] UI の素材を設定し直した");
    }

    static void ImportSprites()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Sprites, Icons }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            string name = Path.GetFileNameWithoutExtension(path);
            bool icon = path.StartsWith(Icons);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 100;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = icon;   // アイコンは 128 → 24〜64 に縮めるのでミップマップを使う
            ti.wrapMode = Repeat.Contains(name) ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteGenerateFallbackPhysicsShape = false;
            ti.SetTextureSettings(s);
            ti.spriteBorder = Borders190.TryGetValue(name, out var b4) ? b4 : Borders.TryGetValue(name, out var b) ? new Vector4(b, b, b, b) : Vector4.zero;
            ti.SaveAndReimport();
        }
    }

    static string Ascii()
    {
        var s = "";
        for (int c = 32; c < 127; c++) s += (char)c;
        return s;
    }

    /// <summary>Font Asset を作る。もうあれば作り直さず（GUID を変えないため）、足りない字だけ焼き足す。Fallback は入れ直す</summary>
    static TMP_FontAsset FontAsset(string ttf, int size, int pad, bool isStatic, string chars, TMP_FontAsset fallback)
    {
        string path = Fonts + ttf + " SDF.asset";
        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        var font = AssetDatabase.LoadAssetAtPath<Font>(Fonts + ttf + ".ttf");
        if (fa == null)
        {
            fa = TMP_FontAsset.CreateFontAsset(font, size, pad, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, !isStatic);
            fa.name = ttf + " SDF";
            AssetDatabase.CreateAsset(fa, path);
            var tex = fa.atlasTextures[0]; tex.name = fa.name + " Atlas"; AssetDatabase.AddObjectToAsset(tex, fa);
            var mat = fa.material; mat.name = fa.name + " Material"; AssetDatabase.AddObjectToAsset(mat, fa);
        }
        // まだ焼いていない字を足す（Font.HasCharacter は OS の書体も見るので、書体に無い字も試す。焼けない字は Fallback で出る）
        string add = new string(chars.Where(ch => !fa.HasCharacter(ch) && font.HasCharacter(ch)).ToArray());
        if (add.Length > 0)
        {
            fa.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fa.TryAddCharacters(add, out var missing);
            if (!string.IsNullOrEmpty(missing)) Debug.Log("[UiAssets] " + ttf + " に無い字（Fallback で出る）: " + missing);
        }
        if (isStatic) fa.atlasPopulationMode = AtlasPopulationMode.Static;
        if (fallback != null) fa.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
        EditorUtility.SetDirty(fa);
        return fa;
    }

    static void BuildTheme(TMP_FontAsset heading, TMP_FontAsset body, TMP_FontAsset shapes, TMP_FontAsset strong)
    {
        var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
        if (theme == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));
            theme = ScriptableObject.CreateInstance<UiTheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
        }
        theme.heading = heading; theme.body = body; theme.shapes = shapes; theme.strong = strong;
        theme.round16 = Sprite("round16"); theme.round28 = Sprite("round28"); theme.round40 = Sprite("round40");
        theme.pill = Sprite("pill"); theme.caretDown = Sprite("caret_down"); theme.stripes = Sprite("stripes64");
        theme.blobBubble = Sprite("blob_bubble"); theme.blobPanel = Sprite("blob_panel"); theme.blobPill = Sprite("blob_pill"); theme.blobDisc = Sprite("blob_disc");
        theme.waveEdge = Sprite("wave_edge"); theme.caretSoft = Sprite("caret_soft"); theme.bands64 = Sprite("bands64"); theme.glovePoint = Sprite("glove_point");
        theme.crew = CrewModels.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Crew/" + n + ".fbx")).ToArray();
        theme.earth = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Bodies/Body_Earth.fbx");
        theme.pad = PadMaterial();
        theme.icons = AssetDatabase.FindAssets("t:Sprite", new[] { Icons })
            .Select(g => AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(s => s != null).OrderBy(s => s.name).ToArray();
        EditorUtility.SetDirty(theme);
    }

    /// <summary>部屋の画面の光る台（URP Lit・Emission。色は LobbyStage が MaterialPropertyBlock で入れる）</summary>
    static Material PadMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(PadPath);
        if (mat != null) return mat;
        Directory.CreateDirectory(Path.GetDirectoryName(PadPath));
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_LobbyPad" };
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.7f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", Color.white);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        AssetDatabase.CreateAsset(mat, PadPath);
        return mat;
    }

    static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + "/" + name + ".png");
}
