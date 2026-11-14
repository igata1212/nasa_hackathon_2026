// 書いた人：iizuka（作成：Fable 5.2）／ 最終更新：2026-11-14
//
// Assets/Models/ 以下の FBX を取り込むたびに、ルートと全部の子の位置・回転・スケールと、メッシュの大きさを
// Console と Editor.log に出す道具。Blender から書き出した FBX が「ルートのスケール1・回転0」「可動部の原点が軸の上」
// 「寸法が m 単位」で入ったかを、目で数値を見て確かめるために使う（#34）。モデルには何も書き込まない。
//
// 見かた：Editor.log（unity/Logs/ か ~/Library/Logs/Unity/）で "[ModelImportReport]" を探す。
//   pos / rot / scale は親から見た値（localPosition / localEulerAngles / localScale）。
//   mesh は、その部品のメッシュの箱（部品の原点から見た中心と大きさ）と三角数。
//   world は、ルートから見たモデル全体の箱。
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

public class ModelImportReport : AssetPostprocessor
{
    const string Tag = "[ModelImportReport] ";
    const string Folder = "Assets/Models/";

    void OnPostprocessModel(GameObject root)
    {
        if (!assetPath.StartsWith(Folder) || !assetPath.ToLowerInvariant().EndsWith(".fbx")) return;

        var imp = (ModelImporter)assetImporter;
        var sb = new StringBuilder();
        sb.Append(Tag).Append(assetPath)
          .Append("  globalScale=").Append(F(imp.globalScale))
          .Append(" useFileScale=").Append(imp.useFileScale)
          .Append(" bakeAxisConversion=").Append(imp.bakeAxisConversion).Append('\n');

        bool any = false;
        var total = new Bounds();
        int tris = 0;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            sb.Append(Tag).Append("  ").Append(PathOf(t, root.transform))
              .Append("  pos=").Append(V(t.localPosition))
              .Append(" rot=").Append(V(t.localEulerAngles))
              .Append(" scale=").Append(V(t.localScale));
            var mf = t.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                var m = mf.sharedMesh;
                int n = m.triangles.Length / 3;
                tris += n;
                sb.Append("  mesh center=").Append(V(m.bounds.center))
                  .Append(" size=").Append(V(m.bounds.size))
                  .Append(" tris=").Append(n);
                var r = t.GetComponent<Renderer>();
                if (r != null)
                {
                    sb.Append(" mat=");
                    foreach (var mat in r.sharedMaterials) sb.Append(mat != null ? mat.name : "(none)").Append(' ');
                }
                // メッシュの箱の8つの角を、ルートから見た座標に直して全体の箱に足す
                var toRoot = root.transform.worldToLocalMatrix * t.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = m.bounds.center + Vector3.Scale(m.bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = toRoot.MultiplyPoint3x4(c);
                    if (!any) { total = new Bounds(p, Vector3.zero); any = true; }
                    else total.Encapsulate(p);
                }
            }
            sb.Append('\n');
        }
        if (any)
            sb.Append(Tag).Append("  world min=").Append(V(total.min)).Append(" max=").Append(V(total.max))
              .Append(" size=").Append(V(total.size)).Append(" tris=").Append(tris).Append('\n');
        Debug.Log(sb.ToString());
    }

    static string PathOf(Transform t, Transform root)
    {
        if (t == root) return t.name + " (root)";
        string s = t.name;
        for (var p = t.parent; p != null && p != root; p = p.parent) s = p.name + "/" + s;
        return s;
    }

    static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);
    static string V(Vector3 v) => "(" + F(v.x) + ", " + F(v.y) + ", " + F(v.z) + ")";
}
