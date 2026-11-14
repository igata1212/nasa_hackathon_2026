// 書いた人：iizuka（作成：Haiku 5.5）／ 最終更新：2026-11-14
//
// 画面の部品を青緑（teal）の発光マテリアルに差し替える（#55 LookDev。コンソールの画面が暗い板に見えないように）。
//   Console_*.Screen_L・Screen_R（独立した部品。modeling/console/build_console.py）
//   Ship_Interior.MonScreen0〜3（壁の画面。独立した部品。modeling/interior/build_interior.py）
//   部品名は「<ルート>.<部品>」の部品のほう（後ろ）で比べる。
// 共通マテリアル M_Core_Palette は書き換えない（共有なので全モデルが光ってしまう）。
// 発光マテリアルは実行時に URP Lit で作る（Assets には保存しない）。作り方は LampGlow.cs と同じ：名前で選んで sharedMaterials を差し替える。
// [ExecuteAlways]：エディタでも光って見える（LookDev の撮影はエディタで行う）。
using UnityEngine;

[ExecuteAlways]
public class ScreenGlow : MonoBehaviour
{
    // 発光の強さ：teal（linear に直したもの）× 1.5。最大成分が約 0.51 で、Bloom の閾値 0.9 の下
    const float EmissionScale = 1.5f;
    static readonly Color TealSrgb = new Color(42f / 255f, 157f / 255f, 143f / 255f);   // #2A9D8F（sRGB の値）
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    static Material glowMat;   // 全部の ScreenGlow で1つを共有する（実行時に作る。保存しない）

    void OnEnable() { Apply(); }
    void OnValidate() { if (isActiveAndEnabled) Apply(); }

    /// <summary>子孫の画面の部品の sharedMaterials を発光マテリアルにする（変わるものだけ書く）。</summary>
    void Apply()
    {
        var mat = GlowMaterial();
        if (mat == null) return;
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (!IsScreen(r.name)) continue;
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == mat) continue;
                mats[i] = mat;
                changed = true;
            }
            if (changed) r.sharedMaterials = mats;
        }
    }

    /// <summary>画面の部品か。「<ルート>.<部品>」の部品のほうで比べる。</summary>
    static bool IsScreen(string name)
    {
        int dot = name.LastIndexOf('.');
        string part = dot >= 0 ? name.Substring(dot + 1) : name;
        return part == "Screen_L" || part == "Screen_R" || part.StartsWith("MonScreen", System.StringComparison.Ordinal);
    }

    /// <summary>発光マテリアル（URP Lit。BaseColor と Emission が teal）。</summary>
    static Material GlowMaterial()
    {
        if (glowMat != null) return glowMat;
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null)
        {
            Debug.LogWarning("ScreenGlow: Universal Render Pipeline/Lit が見つからない（画面は光らない）");
            return null;
        }
        // teal は sRGB の値。linear に直してから ×1.5 する（sRGB のまま掛けると緑が 0.92 になって Bloom の閾値を超える）
        Color teal = TealSrgb.linear;
        glowMat = new Material(sh) { name = "M_Screen_Glow", hideFlags = HideFlags.DontSave };
        glowMat.SetColor(BaseColorId, teal);
        glowMat.SetColor(EmissionId, teal * EmissionScale);
        glowMat.EnableKeyword("_EMISSION");
        return glowMat;
    }
}
