// 書いた人：Claude ／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#130）
// iizuka の手描きスケッチ（docs/design/storyboard.md）の場面を、段階の変わり目に短い演出として流す（#124）。
//   design → practice（打ち上げが通った）  … LaunchStage：地上のクルーとロケット → 打ち上げ → 月へ向かう船（約 8 秒）
//   flight → ending（ED-A・ED-B）          … ReturnStage：再突入 → パラシュート → 着水「SPLASHDOWN!」（約 6.7 秒。#130）
//   flight → ending（ED-C）                … LostStage：静かに暗くなって「Mission lost」（約 4.5 秒）
// サーバーの時間は止まらないので、演出は短くし、SKIP で飛ばせる。演出の間は全画面の絵を GameFlow の UI の上に重ねる
// （UI は消さない。重ねた絵がクリックも受け止める）。その間に来たヒューストンの台詞は、絵の下に字幕で出す。
// GameFlow は変えない：シーンに置いて flow を指すだけ。GameFlow.Client が差し替わったら付け直す。
// 船内の場面はそのまま。舞台は船内から遠く（Editor/Story/StoryScene.cs が組む）に置き、専用のカメラで RenderTexture に描く。
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StoryDirector : MonoBehaviour
{
    public GameFlow flow;
    public Camera storyCamera;           // 舞台を撮るカメラ（演出の間だけ有効。RenderTexture に描く）
    public LaunchStage launch;
    public ReturnStage landing;
    public LostStage lost;
    public bool playLaunch = true, playReturn = true;
    [Tooltip("確認用：0 以上なら、演出の時間をこの秒で止める（スクリーンショット用）")] public float debugFreezeAt = -1f;

    IGameClient hooked;
    string phase = "";
    Coroutine running;
    StoryStage current;
    bool closing;
    float t;
    RenderTexture rt;

    GameObject overlay;
    RawImage view;
    Image fade;
    Text caption, sub, houstonLine;
    GameObject houstonBar, skipBtn, bars;

    public bool Playing => running != null;
    public string CurrentShow => current != null ? current.name : "";
    public float ShowTime => t;
    /// <summary>今まで流した演出の名前（確認用。改行区切り）</summary>
    public string History { get; private set; } = "";

    void Awake()
    {
        BuildOverlay();
        if (storyCamera != null) storyCamera.enabled = false;
        foreach (var s in new StoryStage[] { launch, landing, lost }) if (s != null) s.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        Unhook();
        if (rt != null) { rt.Release(); Destroy(rt); }
    }

    void Update()
    {
        var c = flow != null ? flow.Client : null;
        if (c != hooked)
        {
            Unhook();
            Stop();
            hooked = c; phase = "";
            if (c != null) { c.OnPhase += OnPhase; c.OnEvent += OnEvent; }
        }
    }

    void Unhook()
    {
        if (hooked == null) return;
        hooked.OnPhase -= OnPhase; hooked.OnEvent -= OnEvent;
        hooked = null;
    }

    // ------------------------------------------------------------------ 段階の変化
    void OnPhase(string p)
    {
        string prev = phase; phase = p;
        if (Playing)
        {
            // 飛ばさないと困る段階に入ったら、演出をすぐやめる
            bool keep = current == launch ? (p == "launch" || p == "practice" || p == "stir" || p == "flight") : p == "ending";
            if (!keep) Stop();
        }
        if (Playing) return;
        // サーバーの段階に "launch" がある場合はそこで、無い場合（今のサーバー）は design → practice で始める
        // （付け直した直後で design を見ていなくても、直前に打ち上げ判定 ok が来ていれば流す）
        bool launched = prev == "design" || (prev == "" && Time.unscaledTime - launchOkAt < 2f);
        if (playLaunch && launch != null && (p == "launch" || (p == "practice" && launched))) Play(launch);
        else if (playReturn && p == "ending" && prev == "flight") StartCoroutine(PlayReturnWhenKnown());
    }

    void OnEvent(GameEvent e)
    {
        if (e.kind == "launch" && e.status == "ok") launchOkAt = Time.unscaledTime;
        if (e.kind != "houston" || string.IsNullOrEmpty(e.text)) return;
        lastHouston = e.text; lastHoustonAt = Time.unscaledTime;
        if (!string.IsNullOrEmpty(e.id)) lines[e.id] = e.text;   // 結末の台詞（ED-A/B/C）を舞台が時刻を決めて出すため（#130）
        if (Playing && !closing) ShowHouston(e.text);
    }
    string lastHouston = ""; float lastHoustonAt = -99f, launchOkAt = -99f;

    /// <summary>結末（ED-A/B/C）は room の直後の result で届くので、少し待ってから選ぶ</summary>
    IEnumerator PlayReturnWhenKnown()
    {
        SetFade(1f); overlay.SetActive(true); view.enabled = false;   // 待つ間は黒（エンディングの画面をちらつかせない）
        float w = 0f;
        string e = Ending();
        while (e == "" && w < 1f) { yield return null; w += Time.unscaledDeltaTime; e = Ending(); }
        if (phase != "ending") { overlay.SetActive(false); yield break; }
        if (e == "ED-C") { if (lost != null) Play(lost); else overlay.SetActive(false); }
        else if (landing != null) Play(landing);
        else overlay.SetActive(false);
    }

    public string Ending()
    {
        if (flow == null) return "";
        string e = J.Str(flow.Model.Result, "ending");
        if (e == "" && flow.Model.State != null && !string.IsNullOrEmpty(flow.Model.State.ending)) e = flow.Model.State.ending;
        return e;
    }

    /// <summary>設計で選んだロケット（"small" / "large"）</summary>
    public string Rocket() => flow != null ? J.Str(flow.Model.Design, "rocket", "small") : "small";

    // ------------------------------------------------------------------ 再生
    /// <summary>確認用：名前で演出を流す（"launch" "return" "lost"）</summary>
    public void PlayShow(string name)
    {
        var s = name == "launch" ? (StoryStage)launch : name == "return" ? landing : name == "lost" ? (StoryStage)lost : null;
        if (s != null) Play(s);
    }

    void Play(StoryStage s)
    {
        Stop();
        current = s;
        History += s.name + "\n";
        Debug.Log("[Story] 演出を始める: " + s.name + "（段階 " + phase + "）");
        EnsureTarget();
        overlay.SetActive(true);
        view.enabled = true;
        skipBtn.SetActive(true);
        bars.SetActive(true);
        Caption("", "");
        houstonBar.SetActive(false);
        if (Time.unscaledTime - lastHoustonAt < 1.5f) ShowHouston(lastHouston);   // 直前の台詞も拾う（打ち上げの M03 など）
        storyCamera.enabled = true;
        s.Begin(this);
        t = 0f;
        s.Pose(this, 0f);
        running = StartCoroutine(Run(s));
    }

    IEnumerator Run(StoryStage s)
    {
        t = 0f;
        while (t < s.Length)
        {
            yield return null;
            t += Time.unscaledDeltaTime;
            if (debugFreezeAt >= 0f && t > debugFreezeAt) t = debugFreezeAt;
            s.Pose(this, Mathf.Min(t, s.Length));
        }
        yield return Close();
    }

    /// <summary>黒にしてから舞台を片づけ、船内（UI）へ戻す</summary>
    IEnumerator Close()
    {
        closing = true;
        skipBtn.SetActive(false);
        yield return FadeTo(1f, 0.2f);
        Cleanup();
        yield return FadeTo(0f, 0.35f);
        overlay.SetActive(false);
        closing = false;
        running = null;
    }

    public void Skip()
    {
        if (!Playing || closing) return;
        StopCoroutine(running);
        running = StartCoroutine(Close());
    }

    /// <summary>すぐやめる（フェードなし）</summary>
    public void Stop()
    {
        if (running != null) { StopCoroutine(running); running = null; }
        closing = false;
        Cleanup();
        if (overlay != null) overlay.SetActive(false);
    }

    void Cleanup()
    {
        if (current != null) { current.End(this); current = null; }
        if (storyCamera != null) storyCamera.enabled = false;
        if (view != null) view.enabled = false;
        Caption("", "");
        EndingLayout(false);
        if (houstonBar != null) houstonBar.SetActive(false);
        if (bars != null) bars.SetActive(false);
    }

    IEnumerator FadeTo(float a, float dur)
    {
        float a0 = fade.color.a, k = 0f;
        while (k < dur) { k += Time.unscaledDeltaTime; SetFade(Mathf.Lerp(a0, a, k / dur)); yield return null; }
        SetFade(a);
    }

    void EnsureTarget()
    {
        int w = Mathf.Max(640, Screen.width), h = Mathf.Max(360, Screen.height);
        if (rt != null && rt.width == w && rt.height == h) return;
        if (rt != null) { storyCamera.targetTexture = null; rt.Release(); Destroy(rt); }
        rt = new RenderTexture(w, h, 24) { name = "StoryView", antiAliasing = 2 };
        storyCamera.targetTexture = rt;
        view.texture = rt;
    }

    // ------------------------------------------------------------------ 舞台から使う
    public Camera Cam => storyCamera;
    public void SetFade(float a) { if (fade != null) fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(a)); }
    public void SetBackground(Color c) { storyCamera.clearFlags = CameraClearFlags.SolidColor; storyCamera.backgroundColor = c; }
    public void UseSkybox() { storyCamera.clearFlags = CameraClearFlags.Skybox; }

    /// <summary>大きい字幕と小さい字幕。空文字で消す</summary>
    public void Caption(string big, string small, Color? color = null, float scale = 1f)
    {
        if (caption == null) return;
        caption.text = big; sub.text = small;
        caption.color = color ?? Ui.Cream;
        caption.rectTransform.localScale = Vector3.one * scale;
    }

    void ShowHouston(string text)
    {
        houstonLine.text = CleanLine(text);
        houstonBar.SetActive(true);
    }

    // ------------------------------------------------------------------ 重ねる画面
    void BuildOverlay()
    {
        overlay = new GameObject("StoryCanvas");
        overlay.transform.SetParent(transform, false);
        var canvas = overlay.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;   // GameFlow（10）より上
        var scaler = overlay.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 0.5f;
        overlay.AddComponent<GraphicRaycaster>();
        var root = (RectTransform)overlay.transform;

        var vr = Ui.Rect("View", root); Ui.Fill(vr);
        view = vr.gameObject.AddComponent<RawImage>(); view.color = Color.white; view.raycastTarget = true;   // 下の UI を押せないようにする

        // 映画のような上下の黒い帯
        var barRoot = Ui.Rect("Bars", root); Ui.Fill(barRoot); bars = barRoot.gameObject;
        Ui.Anchor(Ui.PanelImg(barRoot, Color.black, "Top").rectTransform, 0, 0.92f, 1, 1);
        Ui.Anchor(Ui.PanelImg(barRoot, Color.black, "Bottom").rectTransform, 0, 0, 1, 0.08f);

        var fr = Ui.Rect("Fade", root); Ui.Fill(fr);
        fade = fr.gameObject.AddComponent<Image>(); fade.color = Color.black; fade.raycastTarget = true;

        caption = Ui.Label(root, "", 120, Ui.Cream, TextAnchor.MiddleCenter, FontStyle.Bold);
        Ui.Anchor(caption.rectTransform, 0, 0.45f, 1, 0.7f);
        caption.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(4, -4);
        sub = Ui.Label(root, "", 36, Ui.Cream, TextAnchor.MiddleCenter, FontStyle.Bold);
        Ui.Anchor(sub.rectTransform, 0, 0.36f, 1, 0.45f);
        sub.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2, -2);

        var hb = Ui.PanelImg(root, new Color(0.05f, 0.08f, 0.16f, 0.85f), "HoustonLine"); houstonBar = hb.gameObject;
        Ui.Anchor(hb.rectTransform, 0.18f, 0.79f, 0.82f, 0.9f);   // 上（下の帯の上はクルーや海が映るので）
        var who = Ui.Label(hb.transform, "HOUSTON", 18, Ui.Orange, TextAnchor.UpperLeft, FontStyle.Bold);
        Ui.Anchor(who.rectTransform, 0, 0.7f, 1, 1, 14, 0, 8, 4);
        houstonLine = Ui.Label(hb.transform, "", 22, Ui.Cream, TextAnchor.UpperLeft);
        Ui.Anchor(houstonLine.rectTransform, 0, 0, 1, 0.74f, 14, 4, 14, 0);
        houstonLine.resizeTextForBestFit = true; houstonLine.resizeTextMinSize = 12; houstonLine.resizeTextMaxSize = 22;
        houstonLine.verticalOverflow = VerticalWrapMode.Truncate;   // 長い台詞が帯の外にはみ出さない（#130）
        houstonBar.SetActive(false);

        var skip = Ui.Btn(root, "SKIP  >>", new Color(1f, 1f, 1f, 0.85f), Skip, 26, Ui.Navy);
        Ui.Anchor((RectTransform)skip.transform, 0.86f, 0.095f, 0.98f, 0.165f);
        skipBtn = skip.gameObject;

        view.enabled = false;
        bars.SetActive(false);
        overlay.SetActive(false);
    }

    // ------------------------------------------------------------------ 帰還・ED-C の字幕の並べ方（#130）
    // 大きい字幕を上へ、小さい字幕をその下へ、ヒューストンの帯を下へ（下には海か暗い宇宙しか映らない）。見出しは Baloo 2、navy の縁。
    // この間、ヒューストンの帯は舞台（ReturnStage・LostStage）が毎フレーム HoustonBar() で決める（結果を先に言わないため）。
    // Cleanup で元に戻すので、打ち上げの演出の並べ方は変わらない。
    [Header("帰還・ED-C の字幕（#130）")]
    [Tooltip("見出しの書体（Baloo 2 ExtraBold の ttf）。StoryScene が入れる。空なら既定の書体")] public Font headingFont;
    [Tooltip("大きい字幕の位置（anchor の x0, y0, x1, y1）")] public Vector4 endingBig = new Vector4(0f, 0.62f, 1f, 0.84f);
    [Tooltip("小さい字幕の位置")] public Vector4 endingSmall = new Vector4(0f, 0.53f, 1f, 0.61f);
    [Tooltip("下の短い字幕（RE-ENTRY など）の位置")] public Vector4 endingLow = new Vector4(0f, 0.11f, 1f, 0.19f);
    [Tooltip("ヒューストンの帯の位置")] public Vector4 endingBar = new Vector4(0.20f, 0.10f, 0.80f, 0.19f);
    public int endingSmallSize = 40, endingLowSize = 34;
    public Vector2 endingOutline = new Vector2(3f, -3f);

    readonly Dictionary<string, string> lines = new Dictionary<string, string>();
    Text low;
    Outline[] outlines;
    Vector4 defBig, defSmall, defBar;
    int defSmallSize;

    /// <summary>帰還・ED-C の並べ方にする（on）／元に戻す（off）</summary>
    public void EndingLayout(bool on)
    {
        if (caption == null || (!on && low == null)) return;
        if (low == null)
        {
            defBig = AnchorOf(caption.rectTransform); defSmall = AnchorOf(sub.rectTransform); defBar = AnchorOf((RectTransform)houstonBar.transform);
            defSmallSize = sub.fontSize;
            low = Ui.Label(overlay.transform, "", endingLowSize, Ui.Cream, TextAnchor.MiddleCenter, FontStyle.Bold);
            low.name = "LowCaption";
            SetAnchor(low.rectTransform, endingLow);
            outlines = new[] { caption.gameObject.AddComponent<Outline>(), sub.gameObject.AddComponent<Outline>(), low.gameObject.AddComponent<Outline>() };
            foreach (var o in outlines) { o.effectColor = Ui.Navy; o.effectDistance = endingOutline; }
        }
        SetAnchor(caption.rectTransform, on ? endingBig : defBig);
        SetAnchor(sub.rectTransform, on ? endingSmall : defSmall);
        SetAnchor((RectTransform)houstonBar.transform, on ? endingBar : defBar);
        sub.fontSize = on ? endingSmallSize : defSmallSize;
        bool baloo = on && headingFont != null;
        foreach (var x in new[] { caption, sub, low }) { x.font = baloo ? headingFont : Ui.Font; x.fontStyle = baloo ? FontStyle.Normal : FontStyle.Bold; }
        foreach (var o in outlines) o.enabled = on;
        low.text = "";
        low.gameObject.SetActive(on);
    }

    /// <summary>下の短い字幕（EndingLayout の間だけ）。空文字で消す</summary>
    public void LowCaption(string s) { if (low != null) low.text = s; }

    /// <summary>ヒューストンの帯を出す（空文字なら隠す）。EndingLayout の間、舞台が毎フレーム呼ぶ</summary>
    public void HoustonBar(string text)
    {
        if (houstonBar == null) return;
        bool on = !string.IsNullOrEmpty(text);
        if (on) houstonLine.text = CleanLine(text);
        if (houstonBar.activeSelf != on) houstonBar.SetActive(on);
    }

    /// <summary>その id（ED-A など）の台詞のうち、最後に届いたもの。まだ届いていなければ空</summary>
    public string Line(string id) => id != null && lines.TryGetValue(id, out var s) ? s : "";

    /// <summary>台本の「(pause)」は読み上げの間なので、字幕には出さない</summary>
    static string CleanLine(string s) => s.Replace("(pause) ", "").Replace("(pause)", "");

    static Vector4 AnchorOf(RectTransform r) => new Vector4(r.anchorMin.x, r.anchorMin.y, r.anchorMax.x, r.anchorMax.y);
    static void SetAnchor(RectTransform r, Vector4 a) => Ui.Anchor(r, a.x, a.y, a.z, a.w);
}
