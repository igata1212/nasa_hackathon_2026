// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// parameters.json の表を読む（#147）。正は docs/design/parameters.json。Unity が読むのは写しの Assets/Data/Sim/parameters.json
//   UI の部品は数値をコードに書かず、ここから読む：GameParams.TryGet("co2_warn", out var v)
//   読み込みは GameFlow.Awake が1回（GameFlow.parametersJson。シーンを作り直すまでは空なので、そのときは UrgencyFx.parametersJson を使う）
//   形は UrgencyFx.LoadParams と同じ（{"params":[{"key","value","unit",...}]}）。UrgencyFx はまだ自分で読んでいる（#147 では触らない）
//   サーバーが送る閾値（#148、state の thresholds）は GameState.Threshold で読む
using System.Collections.Generic;
using UnityEngine;

public static class GameParams
{
    static readonly Dictionary<string, float> values = new Dictionary<string, float>();
    static readonly Dictionary<string, string> units = new Dictionary<string, string>();
    static readonly HashSet<string> warned = new HashSet<string>();

    public static bool Loaded { get; private set; }

    public static void Load(TextAsset json)
    {
        if (json == null) { Clear(); Debug.LogWarning("[GameParams] parameters.json が割り当てられていない"); return; }
        LoadText(json.text);
    }

    public static void LoadText(string text)
    {
        Clear();
        var arr = J.Arr(MiniJson.ParseObject(text), "params");
        if (arr == null) { Debug.LogWarning("[GameParams] parameters.json に params が無い"); return; }
        foreach (var o in arr)
        {
            if (!(o is Dictionary<string, object> d) || !J.Has(d, "key")) continue;
            string k = J.Str(d, "key");
            values[k] = J.Num(d, "value");
            units[k] = J.Str(d, "unit");
        }
        Loaded = true;
    }

    static void Clear() { values.Clear(); units.Clear(); warned.Clear(); Loaded = false; }

    public static bool Has(string key) => values.ContainsKey(key);
    public static bool TryGet(string key, out float v) => values.TryGetValue(key, out v);

    /// <summary>値。無ければ警告を1回だけ出して 0（数字を足さないで済むように。ちゃんと分けたいときは TryGet）</summary>
    public static float Get(string key)
    {
        if (values.TryGetValue(key, out var v)) return v;
        if (warned.Add(key)) Debug.LogWarning("[GameParams] parameters.json に無い: " + key);
        return 0f;
    }

    /// <summary>単位（"%"、"min"、"C" など）。無ければ ""</summary>
    public static string Unit(string key) => units.TryGetValue(key, out var u) ? u : "";
}
