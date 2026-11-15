// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-15
// 設計の画面の 3D の舞台（#168。DesignStage）が使うモデルの参照。実体は Assets/Resources/DesignStage.asset
// （メニュー Tools/UI/Design Stage を作る。Editor/DesignStageAssetsMenu.cs。シーンを作り直すときにも作る）。
// 画面はコードで組むので、シーンに参照を置かず Resources から読む（UiTheme と同じ作り）
using System;
using UnityEngine;

public class DesignStageAssets : ScriptableObject
{
    [Serializable]
    public struct Part
    {
        public string id;            // parts.json の部品 id（battery・camera など）
        public GameObject prefab;    // Models/Part・Models/Tank の FBX
    }

    public GameObject ship;          // Models/Ship/Ship_Stack
    public GameObject rocketSmall, rocketLarge;   // Models/Ship/Ship_Rocket*
    public Part[] parts;
    public Material surface;         // 地面・台・ふちの輪（URP Lit・白。色は MaterialPropertyBlock で入れる）

    public GameObject Prefab(string id)
    {
        if (parts != null) foreach (var p in parts) if (p.id == id) return p.prefab;
        return null;
    }

    static DesignStageAssets loaded;
    public static DesignStageAssets Load()
    {
        if (loaded == null) loaded = Resources.Load<DesignStageAssets>("DesignStage");
        if (loaded == null) Debug.LogError("[DesignStage] Resources/DesignStage.asset が無い。Tools/UI/Design Stage を作る を実行する");
        return loaded;
    }
}
