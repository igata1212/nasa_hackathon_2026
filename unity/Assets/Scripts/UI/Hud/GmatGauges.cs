// 書いた人：Taiga（作成：Opus 5.5）／ 最終更新：2026-11-15
// 作る Issue：#167（NASA GMAT の軌道の値を、針のメーターで読む）
//
// 大きい地図（MapWidget）を開いている間だけ、その左に出すパネル。ふだんは出さない（#210。収納のつまみは無くした）。
// 元の説明：画面の左に出すポップアップ。NASA GMAT で計算した帰還ルート（data/gmat/route_a.csv・route_b.csv）の、今の船の位置から
//   ① 地球までの距離（km）  ② 月までの距離（km）  ③ 速さ（km/s）
// を針のメーター3つで見せる。
// 船の位置は軌道の地図（MapWidget）と同じ値を読む（ShipRoute・ShipH）。この部品は計算しない。
//   距離：GmatRoute.posKm は EarthMoonRot（地球が原点、月は +X の moonCenterXKm）。地球までは |pos|、月までは |pos − 月|
//   速さ：位置の差（回る座標の速さ）に、座標の回転のぶん ω×r を足して、止まった座標（慣性系）の速さにする
// #210：地図は Mission Planner の席だけ（#200）。メーターは地図について出入りするので、ほかの席には出ない。
// 大きさ・色・文言・メーターの目盛りは、このファイルの上の「直しやすい所」の1か所。
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;

public class GmatGauges : HudPart
{
    // ================================================================== 直しやすい所
    static class Look
    {
        public const float W = 260f;                  // パネルの幅（1600×900 基準）
        public const float Pad = 14f, Gap = 8f;
        public const float HeadH = 34f;
        public const float RowH = 104f;
        public const float Dial = 92f;                // メーターの直径
        public const float NeedleW = 5f, NeedleL = 38f, Hub = 14f;
        public const float TickW = 3f, TickL = 9f;
        public const float Margin = 14f;              // 大きい地図の左の端の外へ
        public const float CenterY = 0.5f;            // パネルのまん中の高さ（大きい地図の割合）
        public const float Sweep = 120f;              // 針の振れ幅（±度。0 が真上）
        public const float NeedleSpeed = 4f;
    }

    static class Words
    {
        public const string Title = "NASA GMAT";
        public const string Earth = "To Earth", Moon = "To Moon", Speed = "Speed";
        public const string Km = "{0} km", Kms = "{0} km/s";
    }

    // メーター：(見出し, アイコン, 目盛りの最大)
    const float MaxKm = 450000f, MaxKms = 12f;
    const float OmegaRad = 2f * Mathf.PI / (27.321661f * 86400f);   // 月の公転（EarthMoonRot の回る速さ、rad/s）

    // ================================================================== 中身
    class Gauge { public RectTransform needle; public TextMeshProUGUI value; public float angle = -Look.Sweep; }

    RectTransform panel;
    readonly Gauge[] gauges = new Gauge[3];
    readonly float[] target = new float[3];
    MapWidget map;

    protected override void Build(RectTransform root)
    {
        panel = Ui.Rect("GmatGauges", root);
        panel.anchorMin = panel.anchorMax = new Vector2(0f, Look.CenterY);
        panel.pivot = new Vector2(0f, 0.5f);
        float h = Look.Pad * 2f + Look.HeadH + Look.Gap + Look.RowH * 3f;
        panel.sizeDelta = new Vector2(Look.W, h);
        UiKit.Surface(panel, T.RadiusM, T.HudFace);
        Detail = panel.gameObject.AddComponent<CanvasGroup>();   // 照明 OFF で暗くしてよい

        // 見出し
        var head = UiKit.Text(panel, Words.Title, TextStyle.Label, T.HudSub, TextAlignmentOptions.MidlineLeft, false);
        var hr = head.rectTransform;
        hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f); hr.pivot = new Vector2(0.5f, 1f);
        hr.offsetMin = new Vector2(Look.Pad + 4f, -(Look.Pad + Look.HeadH)); hr.offsetMax = new Vector2(-Look.Pad, -Look.Pad);

        string[] labels = { Words.Earth, Words.Moon, Words.Speed };
        string[] icons = { "earth", "moon", "rocket" };
        for (int i = 0; i < 3; i++)
        {
            float top = Look.Pad + Look.HeadH + Look.Gap + Look.RowH * i;
            gauges[i] = BuildGauge(panel, top, labels[i], icons[i]);
        }
        panel.gameObject.SetActive(false);   // #210：大きい地図が開くまで出さない（開くとき地図の中に移す）
    }

    Gauge BuildGauge(RectTransform parent, float top, string label, string icon)
    {
        var g = new Gauge();
        var row = Ui.Rect("Gauge_" + label, parent);
        row.anchorMin = new Vector2(0f, 1f); row.anchorMax = new Vector2(1f, 1f); row.pivot = new Vector2(0.5f, 1f);
        row.offsetMin = new Vector2(Look.Pad, -(top + Look.RowH)); row.offsetMax = new Vector2(-Look.Pad, -top);

        // メーター：白い面＋目盛り5本＋針＋軸
        var dial = Ui.Rect("Dial", row);
        dial.anchorMin = dial.anchorMax = new Vector2(0f, 0.5f); dial.pivot = new Vector2(0f, 0.5f);
        dial.sizeDelta = new Vector2(Look.Dial, Look.Dial); dial.anchoredPosition = Vector2.zero;
        var faceImg = UiKit.Pill(dial, T.Panel, Look.Dial, "Face"); UiKit.Fill(faceImg.rectTransform);
        faceImg.raycastTarget = false;
        for (int k = 0; k < 5; k++)
        {
            float a = -Look.Sweep + 2f * Look.Sweep * k / 4f;
            var tk = Ui.Rect("Tick" + k, dial);
            tk.anchorMin = tk.anchorMax = new Vector2(0.5f, 0.5f); tk.pivot = new Vector2(0.5f, 1f);
            tk.sizeDelta = new Vector2(Look.TickW, Look.TickL);
            tk.localRotation = Quaternion.Euler(0f, 0f, -a);
            tk.anchoredPosition = (Vector2)(Quaternion.Euler(0f, 0f, -a) * new Vector3(0f, Look.Dial * 0.5f - 6f, 0f));
            var ti = tk.gameObject.AddComponent<Image>(); ti.color = T.InkSub; ti.raycastTarget = false;
        }
        var ic = UiKit.Icon(dial, icon, 22f, T.InkSub);
        ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        ic.rectTransform.sizeDelta = new Vector2(22f, 22f); ic.rectTransform.anchoredPosition = new Vector2(0f, -24f);
        g.needle = Ui.Rect("Needle", dial);
        g.needle.anchorMin = g.needle.anchorMax = new Vector2(0.5f, 0.5f); g.needle.pivot = new Vector2(0.5f, 0.12f);
        g.needle.sizeDelta = new Vector2(Look.NeedleW, Look.NeedleL); g.needle.anchoredPosition = Vector2.zero;
        var ni = g.needle.gameObject.AddComponent<Image>(); ni.color = T.Accent; ni.raycastTarget = false;
        g.needle.localRotation = Quaternion.Euler(0f, 0f, Look.Sweep);
        var hub = UiKit.Pill(dial, T.Ink, Look.Hub, "Hub");
        hub.rectTransform.anchorMin = hub.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        hub.rectTransform.sizeDelta = new Vector2(Look.Hub, Look.Hub); hub.raycastTarget = false;

        // 右：見出しと数字
        var name = UiKit.Text(row, label, TextStyle.Label, T.HudSub, TextAlignmentOptions.BottomLeft, false);
        var nr = name.rectTransform;
        nr.anchorMin = new Vector2(0f, 0.5f); nr.anchorMax = new Vector2(1f, 1f);
        nr.offsetMin = new Vector2(Look.Dial + 12f, 2f); nr.offsetMax = new Vector2(0f, -10f);
        g.value = UiKit.Text(row, "", TextStyle.Sub, T.HudText, TextAlignmentOptions.TopLeft, false);
        g.value.enableAutoSizing = true; g.value.fontSizeMin = 16f; g.value.fontSizeMax = 26f;
        var vr = g.value.rectTransform;
        vr.anchorMin = new Vector2(0f, 0f); vr.anchorMax = new Vector2(1f, 0.5f);
        vr.offsetMin = new Vector2(Look.Dial + 12f, 10f); vr.offsetMax = new Vector2(0f, -2f);
        return g;
    }

    // ================================================================== 値
    public override void Refresh(GameState s) => Compute();

    void Compute()
    {
        if (map == null) foreach (var p in Ctx.Parts) if (p is MapWidget m) { map = m; break; }
        var r = map != null ? map.ShipRoute : null;
        if (r == null || r.posKm == null || r.posKm.Length < 2)
        {
            for (int i = 0; i < 3; i++) { target[i] = 0f; gauges[i].value.text = "—"; }
            return;
        }
        float h = map.ShipH;
        Vector3 pos = r.PosAtHours(h);
        float earth = pos.magnitude;
        float moon = (pos - new Vector3(r.moonCenterXKm, 0f, 0f)).magnitude;
        const float dh = 0.05f;                                          // 3 分（時間）
        Vector3 vRot = (r.PosAtHours(h + dh) - r.PosAtHours(h - dh)) / (2f * dh * 3600f);   // km/s（回る座標）
        Vector3 vIn = vRot + new Vector3(-OmegaRad * pos.y, OmegaRad * pos.x, 0f);          // ＋ ω×r（ω は +Z）
        float speed = vIn.magnitude;

        target[0] = Mathf.Clamp01(earth / MaxKm);
        target[1] = Mathf.Clamp01(moon / MaxKm);
        target[2] = Mathf.Clamp01(speed / MaxKms);
        var ci = CultureInfo.InvariantCulture;
        gauges[0].value.text = string.Format(Words.Km, Mathf.RoundToInt(earth).ToString("N0", ci));
        gauges[1].value.text = string.Format(Words.Km, Mathf.RoundToInt(moon).ToString("N0", ci));
        gauges[2].value.text = string.Format(Words.Kms, speed.ToString("0.00", ci));
    }

    // ================================================================== 毎フレーム
    public override void Tick(float now)
    {
        // #210：大きい地図が開いている間だけ、その左（地図の枠の外）に出す
        if (map == null) foreach (var p in Ctx.Parts) if (p is MapWidget m) { map = m; break; }
        bool show = map != null && map.BigOpen && map.BigRect != null;
        if (show && panel.parent != map.BigRect)
        {
            panel.SetParent(map.BigRect, false);
            // #210 地図の枠の外の左に出す（中に置くと地球・A の札・軌道に重なる）
            panel.anchorMin = panel.anchorMax = new Vector2(0f, Look.CenterY);
            panel.pivot = new Vector2(1f, 0.5f);
            panel.anchoredPosition = new Vector2(-Look.Margin, 0f);
        }
        if (show) panel.SetAsLastSibling();
        if (panel.gameObject.activeSelf != show) panel.gameObject.SetActive(show);
        if (!show) return;

        float dt = Time.unscaledDeltaTime;
        float k = 1f - Mathf.Exp(-Look.NeedleSpeed * dt);
        for (int i = 0; i < 3; i++)
        {
            var g = gauges[i];
            float a = -Look.Sweep + 2f * Look.Sweep * target[i];
            g.angle = Mathf.Lerp(g.angle, a, k);
            g.needle.localRotation = Quaternion.Euler(0f, 0f, -g.angle);   // UI の z は反時計回りが＋。針は時計回りが＋
        }
    }
}
