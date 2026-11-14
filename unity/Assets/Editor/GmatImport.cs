// 書いた人：Claude（#65）／ 最終更新：2026-11-14
// メニュー Tools/GMAT/軌道を取り込む：data/gmat/route_a.csv・route_b.csv と time_map.json を読み、
// Assets/Data/Gmat/ に ScriptableObject（GmatRoute）を作る。CSV は Assets に複製しない（正は data/gmat/）。
// 線のマテリアル Assets/Art/Gmat/M_Trajectory.mat もここで作る。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class GmatImport
{
    public const string Tag = "[GMAT] ";
    public const string OutFolder = "Assets/Data/Gmat";
    public const string LineMaterialPath = "Assets/Art/Gmat/M_Trajectory.mat";

    [Serializable] class TimeMapFile { public double origin_mjd; public float moon_center_x_km; public RouteKnots[] routes; }
    [Serializable] class RouteKnots { public string id; public GmatKnot[] knots; }

    static string RepoRoot => Path.GetDirectoryName(Path.GetDirectoryName(Application.dataPath));

    [MenuItem("Tools/GMAT/軌道を取り込む")]
    public static void Import()
    {
        var gmat = Path.Combine(RepoRoot, "data", "gmat");
        var tm = JsonUtility.FromJson<TimeMapFile>(File.ReadAllText(Path.Combine(gmat, "time_map.json")));
        Directory.CreateDirectory(Path.Combine(RepoRoot, "unity", OutFolder));
        foreach (var id in new[] { "a", "b" })
        {
            var knots = Array.Find(tm.routes, r => r.id == id)?.knots;
            if (knots == null) { Debug.LogError(Tag + "time_map.json に route " + id + " がない"); continue; }
            var route = ReadCsv(Path.Combine(gmat, "route_" + id + ".csv"), id, tm, knots);
            var path = OutFolder + "/Route_" + id.ToUpper() + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<GmatRoute>(path);
            if (existing == null) AssetDatabase.CreateAsset(route, path);
            else { EditorUtility.CopySerialized(route, existing); EditorUtility.SetDirty(existing); }
            Debug.Log(Tag + path + "：" + route.hours.Length + " 行、" + route.hours[route.hours.Length - 1].ToString("F2")
                + " 時間、ゲーム t 0〜" + route.EndT + " 秒");
        }
        EnsureLineMaterial();
        AssetDatabase.SaveAssets();
    }

    static GmatRoute ReadCsv(string file, string id, TimeMapFile tm, GmatKnot[] knots)
    {
        var lines = File.ReadAllLines(file);
        var head = lines[0].Split(',');
        int Col(string suffix) { for (int i = 0; i < head.Length; i++) if (head[i].Trim().EndsWith(suffix)) return i; throw new Exception(file + " に " + suffix + " がない"); }
        int ic = Col("UTCModJulian"), ix = Col("EarthMoonRot.X"), iy = Col("EarthMoonRot.Y"), iz = Col("EarthMoonRot.Z");
        var hours = new List<float>();
        var pos = new List<Vector3>();
        double origin = tm.origin_mjd;
        for (int l = 1; l < lines.Length; l++)
        {
            var f = lines[l].Split(',');
            if (f.Length != head.Length) continue;
            double mjd = double.Parse(f[ic], CultureInfo.InvariantCulture);
            float h = (float)((mjd - origin) * 24.0);
            if (hours.Count > 0 && h <= hours[hours.Count - 1]) continue;   // 時刻が増えない行は捨てる
            hours.Add(h);
            pos.Add(new Vector3(P(f[ix]), P(f[iy]), P(f[iz])));
        }
        var r = ScriptableObject.CreateInstance<GmatRoute>();
        r.name = "Route_" + id.ToUpper(); r.routeId = id; r.originMjd = origin; r.hours = hours.ToArray(); r.posKm = pos.ToArray();
        r.knots = knots; r.moonCenterXKm = tm.moon_center_x_km;
        return r;
    }

    static float P(string s) => float.Parse(s, CultureInfo.InvariantCulture);

    public static Material EnsureLineMaterial()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(LineMaterialPath);
        if (m != null) return m;
        Directory.CreateDirectory(Path.Combine(RepoRoot, "unity", "Assets", "Art", "Gmat"));
        m = new Material(Shader.Find("Sprites/Default")) { name = "M_Trajectory" };   // 頂点カラー（LineRenderer の色）で塗る
        AssetDatabase.CreateAsset(m, LineMaterialPath);
        return m;
    }
}
