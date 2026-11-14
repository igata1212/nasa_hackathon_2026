// 書いた人：iizuka（作成：Claude Opus 5.5）／ 最終更新：2026-11-14
// HUD の部品（UiKitHud）の見本を並べる開発用の画面（#71）。ゲームの画面からは使わない。
//   メニュー Tools/UI/部品の見本を開く（1ページ目／2ページ目）… 新しい空のシーンに置いて再生する（Editor/UiKitGalleryMenu.cs）
// 1ページ目：警告の帯・ゲージ・トグル・カウントダウン。2ページ目：席タブ・長押しボタン・同時押しの合図・吹き出し。左上のボタンで切り替える。
// 動きを見せるため、警告の帯・電力のゲージ・カウントダウン・同時押し・吹き出しは時間で状態が変わる。値はすべて見本（ゲームの値ではない）。
// スクショは 1600×900 と 844×390 で、2ページとも撮る
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using T = UiTokens;

public class UiKitGallery : MonoBehaviour
{
    [Tooltip("最初に出すページ（0 か 1）")] public int startPage;
    [Tooltip("見本の Canvas の sortingOrder（ゲームの再生中に出しても、GameCanvas＝10・物語＝50 より前）")] public int sortingOrder = 100;

    // 見本の値（ゲームの値ではない。しきい値は本番では parameters.json・サーバーから）
    const float PowerLoopSec = 12f, PowerCaution = 50f, PowerDanger = 25f, PowerLine = 0.1f;
    const float BurnLoopSec = 45f, LastLoopSec = 13f, LastFrom = 12f;
    const float BannerLoopSec = 8f, BannerLeakUntil = 4f, BannerCo2Until = 7f;
    const float BubbleLoopSec = 10f, BubbleCriticalFrom = 5f;
    const float SlowPctSample = 50f;
    const float MidW = 360, TabsW = 600;   // 見本の画面の列の幅（1600 と 844×390 の両方で、ほかの列とぶつからない幅）
    static readonly float[] DualSteps = { 0f, 1f, 1.3f, 3.3f, 4.3f, 4.8f, 6.8f };   // Reset → 押した → そろった → Reset → 押した → 外れた → （8 秒で最初へ）
    const float DualLoopSec = 8f, DualWindowSec = 0.5f;   // 受付の時間（本番はサーバーの press イベントの window_s）

    readonly RectTransform[] pages = new RectTransform[2];
    int page;
    UiButton next;
    UiBanner banner;
    UiGauge power;
    UiCountdown burn, last;
    UiDualPress dual;
    UiBubble bubble;
    TextMeshProUGUI log;
    float clock;
    int dualStep = -1, bubbleCritical = -1, bannerStep = -1;

    void Awake()
    {
        EnsureCameraAndEvents();
        var cgo = new GameObject("GalleryCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = sortingOrder;
        var sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(T.CanvasRefW, T.CanvasRefH); sc.matchWidthOrHeight = T.CanvasMatch;

        var bg = Ui.PanelImg(cgo.transform, T.Bg, "Bg"); Ui.Fill(bg.rectTransform);
        var safe = UiKit.SafeArea(bg.transform);
        for (int i = 0; i < pages.Length; i++) { pages[i] = Ui.Rect("Page" + (i + 1), safe); UiKit.Fill(pages[i]); }
        BuildStatus(pages[0]);
        BuildControls(pages[1]);

        next = UiKit.SecondaryButton(safe, "", null, () => Show(1 - page));
        Corner(next.Root, new Vector2(0f, 1f));
        Show(0);
    }

    void Start() => Show(Mathf.Clamp(startPage, 0, pages.Length - 1));

    void Show(int i)
    {
        page = i;
        for (int k = 0; k < pages.Length; k++) pages[k].gameObject.SetActive(k == i);
        next.SetText("NEXT PAGE (" + (i + 1) + "/" + pages.Length + ")");
        Corner(next.Root, new Vector2(0f, 1f));
    }

    // ================================================================== 1ページ目：状態
    void BuildStatus(RectTransform p)
    {
        banner = HudKit.Banner(p);

        var gauges = Section(p, "GAUGES", new Vector2(0f, 1f), T.GaugeW + T.PanelPad * 2f);
        power = HudKit.Gauge(gauges, "power");
        power.SetLine(PowerLine);
        var o2 = HudKit.Gauge(gauges, null, "O₂"); o2.Set(0.34f, HudLevel.Caution, "34%"); o2.SetLine(0.2f);
        var temp = HudKit.Gauge(gauges, "temperature"); temp.Set(0.12f, HudLevel.Danger, "4°C"); temp.SetLine(0.25f);
        var co2 = HudKit.Gauge(gauges, "co2"); co2.Set(0.64f, HudLevel.Caution, "64");
        var fuel = HudKit.Gauge(gauges, "fuel"); fuel.Set(0.64f, HudLevel.Safe, "64%");
        Note(gauges, "Safe: teal. Caution: orange + alert icon. Danger: crimson + stripes + warning icon. The dark line is the survival line.");

        var right = Section(p, "SWITCHES", new Vector2(1f, 1f), T.GaugeW + T.PanelPad * 2f);
        HudKit.Toggle(right, "attitude", "NAV", null).Set(true);
        HudKit.Toggle(right, "comms", "COMMS", null).Set(false);
        HudKit.Toggle(right, "temperature", "HEATER", null).SetEnabled(false);

        var mid = Section(p, "COUNTDOWN", new Vector2(0.5f, 1f), MidW);
        var chips = UiKit.Col(mid, T.S2, TextAnchor.UpperCenter);   // パネルいっぱいに伸ばさない
        burn = HudKit.Countdown(chips, "BURN");
        last = HudKit.Countdown(chips);
        Note(mid, "Crimson and bouncing from 10 s.");
    }

    // ================================================================== 2ページ目：操作
    void BuildControls(RectTransform p)
    {
        var tabsBox = Section(p, "SEAT TABS", new Vector2(0f, 1f), TabsW);
        var tabs = HudKit.SeatTabs(tabsBox, new[] { Seats.Pilot, Seats.Comms }, s => Log("Seat: " + Seats.Title(s)));
        tabs.SetMark(Seats.Comms, "caution");
        Note(tabsBox, "One player, two seats. The mark asks for attention.");

        var holdBox = Section(p, "HOLD BUTTONS", new Vector2(0f, 0f), T.GaugeW + T.PanelPad * 2f);
        var holds = UiKit.Row(holdBox, T.S4, TextAnchor.UpperCenter);
        var ignite = HudKit.HoldButton(holds, "ignition", "IGNITE", null, T.Bg);
        ignite.OnDone = () => Log("Done: IGNITE (" + ignite.Seconds.ToString("0.00") + " s)");
        var send = HudKit.HoldButton(holds, "send", "TRANSMIT", null, T.Bg);
        send.Seconds = HudKit.HoldSeconds(SlowPctSample);
        send.OnDone = () => Log("Done: TRANSMIT (" + send.Seconds.ToString("0.00") + " s, slow +" + SlowPctSample + "%)");
        HudKit.HoldButton(holds, "filter", "SWAP FILTER", null, T.Bg).SetEnabled(false, "switch");
        log = Note(holdBox, "Hold to fill the ring. Slow controls hold longer.");

        dual = HudKit.DualPress(p, "ignition", new[] { Seats.Pilot, Seats.Comms });
        dual.SetMine(new[] { Seats.Pilot });
        Corner(dual.Root, new Vector2(1f, 1f));

        bubble = HudKit.Bubble(p);
        var br = bubble.Root;
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(1f, 0f);
        br.anchoredPosition = new Vector2(-T.ScreenEdge, T.ScreenEdge);
    }

    // ================================================================== 動き
    void Update()
    {
        clock += Time.unscaledDeltaTime;

        float v = 100f * (1f - clock % PowerLoopSec / PowerLoopSec);
        power.Set(v / 100f, HudKit.Low(v, PowerCaution, PowerDanger), Mathf.RoundToInt(v) + "%");

        float b = clock % BannerLoopSec;
        int bs = b < BannerLeakUntil ? 0 : b < BannerCo2Until ? 1 : 2;
        if (bs != bannerStep)
        {
            bannerStep = bs;
            if (bs == 0) { banner.Show("co2", "CO2 IS RISING", HudLevel.Caution, 1); banner.Show("leak", "OXYGEN LEAK: CLOSE A VALVE", HudLevel.Danger, 10); }   // 危険が前面、注意は「+1」
            else if (bs == 1) banner.Clear("leak");
            else banner.ClearAll();
        }

        burn.Set(BurnLoopSec - clock % BurnLoopSec);
        last.Set(LastFrom - clock % LastLoopSec);

        float d = clock % DualLoopSec;
        int step = 0;
        while (step + 1 < DualSteps.Length && d >= DualSteps[step + 1]) step++;
        if (step != dualStep)
        {
            dualStep = step;
            var pilot = new[] { Seats.Pilot };
            switch (step)
            {
                case 1: dual.Waiting(pilot, DualWindowSec); break;
                case 2: dual.Together(); break;
                case 4: dual.Waiting(pilot, DualWindowSec); break;
                case 5: dual.Failed(pilot); break;
                default: dual.Reset(); break;
            }
        }

        int crit = clock % BubbleLoopSec >= BubbleCriticalFrom ? 1 : 0;
        if (crit != bubbleCritical)
        {
            bubbleCritical = crit;
            if (crit == 0) bubble.Set("HOUSTON", "Copy that, crew. Close " + HudKit.Em("Tank 2") + " now. You have about " + HudKit.Em("40 seconds") + " of margin.");
            else bubble.Set("HOUSTON", "Burn window opens in " + HudKit.Em("10 seconds") + ". Pilot and Comms, get ready to " + HudKit.Em("IGNITE") + " together!", true);
        }
    }

    // ================================================================== 小道具
    /// <summary>見出しと点線のついたパネル。corner（0/1, 0/1）の隅に置く。上の隅は NEXT PAGE と警告の帯の下にずらす</summary>
    RectTransform Section(RectTransform p, string title, Vector2 corner, float width)
    {
        var panel = UiKit.Panel(p, T.S2, "Section_" + title);
        panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        panel.sizeDelta = new Vector2(width, 0f);
        UiKit.Text(panel, title, TextStyle.Sub, T.Ink, TextAlignmentOptions.MidlineLeft);
        UiKit.DashedLine(panel, T.Line);
        Corner(panel, corner);
        if (corner.y > 0.5f) panel.anchoredPosition += Vector2.down * (T.Hit + T.S3);
        return panel;
    }

    static TextMeshProUGUI Note(Transform p, string s)
    {
        var t = UiKit.Text(p, s, TextStyle.Label, T.InkSub, TextAlignmentOptions.TopLeft);
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    void Log(string s) { if (log != null) log.text = s; }

    /// <summary>親の隅に、画面の端から ScreenEdge 離して置く。大きさは中身の好む大きさ</summary>
    static void Corner(RectTransform r, Vector2 corner)
    {
        r.anchorMin = r.anchorMax = r.pivot = corner;
        float x = corner.x < 0.5f ? T.ScreenEdge : corner.x > 0.5f ? -T.ScreenEdge : 0f;
        r.anchoredPosition = new Vector2(x, corner.y < 0.5f ? T.ScreenEdge : -T.ScreenEdge);
        float w = LayoutUtility.GetPreferredWidth(r), h = LayoutUtility.GetPreferredHeight(r);
        if (r.GetComponent<ContentSizeFitter>() == null) r.sizeDelta = new Vector2(w, h);
    }

    static void EnsureCameraAndEvents()
    {
        if (Camera.main == null)
        {
            var cam = new GameObject("GalleryCamera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = T.Bg;
        }
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }
    }
}
