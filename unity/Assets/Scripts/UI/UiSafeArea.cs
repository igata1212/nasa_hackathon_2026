// 書いた人：iizuka（作成：Claude）／ 最終更新：2026-11-14
// 付けた RectTransform を Screen.safeArea（iPhone の切り欠き・ホームバーを避けた範囲）に合わせる（UI ガイドライン 2章「余白」）。
// 画面の端からの余白（UiTokens.ScreenEdge）は、この中にさらに取る
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class UiSafeArea : MonoBehaviour
{
    Rect applied;
    Vector2Int screen;

    void OnEnable() => Apply();
    void Update() { if (Screen.safeArea != applied || screen.x != Screen.width || screen.y != Screen.height) Apply(); }

    void Apply()
    {
        applied = Screen.safeArea;
        screen = new Vector2Int(Screen.width, Screen.height);
        if (screen.x <= 0 || screen.y <= 0) return;
        var r = (RectTransform)transform;
        r.anchorMin = new Vector2(applied.xMin / screen.x, applied.yMin / screen.y);
        r.anchorMax = new Vector2(applied.xMax / screen.x, applied.yMax / screen.y);
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}
