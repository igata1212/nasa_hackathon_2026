// 書いた人：Taiga（Claude） ／ 最終更新：2026-11-15（#222）
// WebGL のビルド（Vercel に置く用）。Game.unity だけを入れて unity/Builds/WebGL に出す（コミットしない）。
//   メニュー Tools/Build/WebGL (Vercel)
//   バッチ：Unity.exe -batchmode -quit -projectPath unity -buildTarget WebGL -executeMethod WebGlBuild.Build -logFile -
// Vercel に置くときは tools/vercel/make_bundle.py --webgl が、これをサーバーと一緒のフォルダにまとめる（tools/vercel/README.md）。
// 圧縮は gzip ＋ Decompression Fallback（サーバーが Content-Encoding を付けなくても読める。ファイル名は .unityweb）。
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class WebGlBuild
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    const string Template = "PROJECT:MissionReturn";
    const string ProductName = "Mission Return";

    static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "WebGL"));

    [MenuItem("Tools/Build/WebGL (Vercel)")]
    public static void Build()
    {
        // ProjectSettings は書き換えたままにしない（ビルドの間だけ）
        var oldCompression = PlayerSettings.WebGL.compressionFormat;
        bool oldFallback = PlayerSettings.WebGL.decompressionFallback;
        string oldTemplate = PlayerSettings.WebGL.template;
        string oldName = PlayerSettings.productName;
        try
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.template = Template;
            PlayerSettings.productName = ProductName;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            var sum = report.summary;
            if (sum.result != BuildResult.Succeeded)
            {
                Debug.LogError("[WebGlBuild] 失敗: " + sum.result + "（エラー " + sum.totalErrors + "）");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Debug.Log("[WebGlBuild] できた: " + OutDir + "（" + (sum.totalSize / (1024 * 1024)) + " MB、" + sum.totalTime + "）");
        }
        finally
        {
            PlayerSettings.WebGL.compressionFormat = oldCompression;
            PlayerSettings.WebGL.decompressionFallback = oldFallback;
            PlayerSettings.WebGL.template = oldTemplate;
            PlayerSettings.productName = oldName;
        }
    }
}
