// iPhone 用のビルド（#223）。Xcode のプロジェクトを unity/Builds/iOS に書き出す（コミットしない）。
//   メニュー Tools/Build/iOS (Xcode project)
//   コマンド：Unity -batchmode -quit -projectPath unity -buildTarget iOS -executeMethod IOSBuild.Build -logFile -
// 署名（Signing & Capabilities の Team）と実機への Run は Xcode で本人が行う。
// サーバーへは LAN の ws://<Mac の IP>:8765 でつなぐので、平文の通信を許し、ローカルネットワークの許可の文を Info.plist に足す。
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

public static class IOSBuild
{
    const string OutDir = "Builds/iOS";
    const string Scene = "Assets/Scenes/Game.unity";
    const string BundleId = "com.enryonasa.missionreturn";
    const string IconPath = "Assets/Art/AppIcon/AppIcon_1024.png";   // 仮のアイコン（クルーのレンダーから切り出し。#224）
    const string LocalNetworkText = "Connects to the Mission Return game server on your local network.";

    [MenuItem("Tools/Build/iOS (Xcode project)")]
    public static void BuildFromMenu() => Run(false);

    /// <summary>-batchmode から呼ぶ。失敗したら終了コード 1</summary>
    public static void Build() => Run(true);

    static void Run(bool batch)
    {
        ApplySettings();
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { Scene },
            locationPathName = OutDir,
            target = BuildTarget.iOS,
            targetGroup = BuildTargetGroup.iOS,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(opts);
        var s = report.summary;
        Debug.Log($"[IOSBuild] {s.result} → {Path.GetFullPath(OutDir)}（{s.totalTime.TotalSeconds:0} 秒、警告 {s.totalWarnings}、エラー {s.totalErrors}）");
        if (batch && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    /// <summary>iPhone 用の設定。名前・バンドル ID・横向きだけ・平文の ws:// を許す</summary>
    static void ApplySettings()
    {
        PlayerSettings.productName = "Mission Return";
        PlayerSettings.companyName = "Enryo no NASA";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;   // LAN の ws://（TLS なし）
        // TestFlight に上げるたびに増やす番号。IOS_BUILD_NUMBER があればそれを使う（#224）
        var buildNumber = System.Environment.GetEnvironmentVariable("IOS_BUILD_NUMBER");
        if (!string.IsNullOrEmpty(buildNumber)) PlayerSettings.iOS.buildNumber = buildNumber;
        // アイコン（1024 の App Store 用もここから作られる。無いとアップロードで弾かれる）
        AssetDatabase.ImportAsset(IconPath);
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon != null) PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        AssetDatabase.SaveAssets();
    }

#if UNITY_IOS
    /// <summary>Info.plist に、ローカルネットワークを使う理由と平文の通信の許可を足す（iOS 14 以降、LAN の Mac につなぐのに要る）</summary>
    [PostProcessBuild(100)]
    static void AddPlistKeys(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;
        string plistPath = Path.Combine(path, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        var root = plist.root;
        root.SetString("NSLocalNetworkUsageDescription", LocalNetworkText);
        root.SetBoolean("ITSAppUsesNonExemptEncryption", false);   // 標準の暗号（https/wss）だけ。TestFlight の輸出の質問を省く
        var ats = root["NSAppTransportSecurity"] as PlistElementDict ?? root.CreateDict("NSAppTransportSecurity");
        ats.SetBoolean("NSAllowsArbitraryLoads", true);
        ats.SetBoolean("NSAllowsLocalNetworking", true);
        plist.WriteToFile(plistPath);
    }
#endif
}
