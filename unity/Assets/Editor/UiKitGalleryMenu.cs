// 書いた人：iizuka（作成：Claude Opus 5.5）／ 最終更新：2026-11-14
// HUD の部品の見本（UiKitGallery、#71）を開く。新しい空のシーン（保存しない）に置いて再生する。Game.unity は触らない。
//   メニュー Tools/UI/部品の見本を開く（1ページ目）／（2ページ目）
//   再生中に選ぶと、今のシーンの前面に出す（止めれば消える）
//   UnityMCP から呼ぶときは OpenForCapture(page)：確認の窓を出さない。保存していないシーンがあれば何もしない
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class UiKitGalleryMenu
{
    const string Tag = "[UiKitGallery] ";

    [MenuItem("Tools/UI/部品の見本を開く（1ページ目）")]
    static void Page1() => Open(0, true);

    [MenuItem("Tools/UI/部品の見本を開く（2ページ目）")]
    static void Page2() => Open(1, true);

    /// <summary>確認の窓を出さずに開く。保存していないシーンがあれば false</summary>
    public static bool OpenForCapture(int page) => Open(page, false);

    static bool Open(int page, bool ask)
    {
        if (EditorApplication.isPlaying)
        {
            var g = new GameObject("UiKitGallery").AddComponent<UiKitGallery>();
            g.startPage = page;   // Awake はもう走っている。ページは Start で切り替わる
            return true;
        }
        if (ask)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        }
        else
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) { Debug.LogWarning(Tag + "保存していないシーンがある: " + SceneManager.GetSceneAt(i).name); return false; }
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var go = new GameObject("UiKitGallery");
        go.AddComponent<UiKitGallery>().startPage = page;
        EditorApplication.isPlaying = true;
        Debug.Log(Tag + "見本の画面を再生する（" + (page + 1) + "ページ目）。止めたら Game.unity を開き直す");
        return true;
    }
}
