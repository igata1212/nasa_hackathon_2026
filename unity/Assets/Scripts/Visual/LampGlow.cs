// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
//
// 警報ランプなどの「.Lamp_〜」の部品を光らせる（#55。modeling/core/README.md 4章「次にやること」）。
// モデルの中のランプは共通マテリアル M_Core_Palette（光らない）で塗ってあるので、
// この部品を付けたオブジェクトの子の、名前に ".Lamp_" を含む Renderer のマテリアルを、色ごとの発光マテリアルに差し替える。
//   部品名の "Lamp_" の後ろが Red／Yellow／Green で始まるかで色を選ぶ（例：Fx_Alarm.Lamp_Red → red）。
// 点滅（blink）は Play のときだけ。発光の強さを MaterialPropertyBlock で変える（マテリアル自体は書き換えない）。
// 近くを照らす Point Light（glowLight）があれば、それも一緒に点滅させる。
// 差し替えは取り込み時ではなくこの部品でする（ゲームの中で状態に合わせて切り替えられるように）。
using UnityEngine;

public class LampGlow : MonoBehaviour
{
    [Header("発光マテリアル（Assets/Art/Materials/M_Lamp_*.mat）")]
    public Material red;
    public Material yellow;
    public Material green;

    [Header("点滅")]
    public bool blink = true;
    [Tooltip("1回の点滅の秒数")] public float period = 0.9f;
    [Tooltip("消えているときの明るさ（点いているとき＝1）")] [Range(0f, 1f)] public float offLevel = 0.06f;
    public Light glowLight;

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    Renderer[] lamps = new Renderer[0];
    Color[] baseEmission = new Color[0];
    float baseLightIntensity;
    MaterialPropertyBlock mpb;

    /// <summary>root の子の ".Lamp_" の部品のマテリアルを発光マテリアルに差し替える（エディタからも呼べる）。差し替えた数を返す。</summary>
    public static int Apply(GameObject root, Material red, Material yellow, Material green)
    {
        int n = 0;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            var mat = Pick(r.name, red, yellow, green);
            if (mat == null) continue;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
            n++;
        }
        return n;
    }

    static Material Pick(string partName, Material red, Material yellow, Material green)
    {
        int i = partName.IndexOf(".Lamp_", System.StringComparison.Ordinal);
        if (i < 0) return null;
        string c = partName.Substring(i + 6);
        if (c.StartsWith("Red")) return red;
        if (c.StartsWith("Yellow")) return yellow;
        if (c.StartsWith("Green")) return green;
        return null;
    }

    void Awake()
    {
        Apply(gameObject, red, yellow, green);
        var list = new System.Collections.Generic.List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            if (r.name.Contains(".Lamp_")) list.Add(r);
        lamps = list.ToArray();
        baseEmission = new Color[lamps.Length];
        for (int i = 0; i < lamps.Length; i++)
            baseEmission[i] = lamps[i].sharedMaterial != null && lamps[i].sharedMaterial.HasProperty(EmissionId)
                ? lamps[i].sharedMaterial.GetColor(EmissionId) : Color.black;
        if (glowLight != null) baseLightIntensity = glowLight.intensity;
        mpb = new MaterialPropertyBlock();
    }

    void Update()
    {
        float level = 1f;
        if (blink)
        {
            // 0〜1 の三角波を少し四角に寄せる（パッと点いて、パッと消える。ただし角は丸める）
            float s = Mathf.Repeat(Time.time / Mathf.Max(period, 0.05f), 1f);
            float on = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((0.5f - Mathf.Abs(s - 0.5f)) * 6f - 0.5f));
            level = Mathf.Lerp(offLevel, 1f, on);
        }
        for (int i = 0; i < lamps.Length; i++)
        {
            lamps[i].GetPropertyBlock(mpb);
            mpb.SetColor(EmissionId, baseEmission[i] * level);
            lamps[i].SetPropertyBlock(mpb);
        }
        if (glowLight != null) glowLight.intensity = baseLightIntensity * level;
    }
}
