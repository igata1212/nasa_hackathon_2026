// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
//
// 警告のときに画面の緊急性を上げる（#125）。サーバーから来た状態で緊急の段階 Level（0〜3）を決め、
//   ・画面の縁が赤く脈打つ：画面の縁に赤い枠（UI。GameCanvas の下・3D の上に描くので、HUD のゲージは赤に埋もれない）
//     ＋全体の Volume の Vignette（縁を少し沈める。実行時に複製したプロファイルを上書き。共有のアセットは書き換えない）
//     Vignette だけだと、明るい船内では縁が赤黒く沈むだけで赤く見えなかった（Unity で確認、2026-11-14）。
//   ・メインカメラの小さな揺れ（段階 2 から）。事故の瞬間（M05 の台詞／stir → flight）は大きく1回
//   ・警報ランプ（LampGlow）の点滅を速める（LampGlow の period を変えるだけ。LampGlow は変えない）
// 段階の決め方（閾値は新しく決めない。CabinHud のゲージの色と、parameters.json の値を使う）：
//   1 注意：電力が容量の 50% 未満／酸素がゲージの上限の 50% 未満（CabinHud が黄にする所）／CO2 > co2_warn
//   2 危険：電力が容量の 25% 未満／酸素がゲージの上限の 25% 未満（CabinHud が赤にする所）／CO2 >= co2_slow
//   3 危機：電力 < end_power_min／酸素 < end_oxygen_min（生還ラインの下）／事故直後の漏れ（leak_open かつ t < oxygen_leak_max ÷ oxygen_leak_per_s）
// 段階は flight の間だけ。それ以外の段階・演出（StoryDirector）の間は 0 に戻す。
// 見た目だけの値（脈の速さ・色・揺れの大きさ）はこの部品の SerializeField。ほかの部品は Level と OnLevelChanged を使う（警報の音など）。
// GameFlow は変えない：シーンに置いて flow を指すだけ（Editor/Urgency/UrgencyScene.cs が組む）。GameFlow.Client が差し替わったら付け直す。
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class UrgencyFx : MonoBehaviour
{
    public GameFlow flow;
    [Tooltip("Data/Sim/parameters.json（正は docs/design/parameters.json。UrgencyScene が写す）")] public TextAsset parametersJson;
    [Tooltip("全体の Volume（Vignette を持つもの）。実行時にプロファイルを複製して上書きする")] public Volume volume;
    [Tooltip("揺らすカメラ（メインカメラだけ）。空なら Camera.main")] public Camera mainCamera;
    public LampGlow[] lamps = new LampGlow[0];
    [Tooltip("演出の間は止める（任意）")] public StoryDirector story;

    [Header("画面の縁（段階 0〜3 ごと）")]
    [Tooltip("ui-guidelines の danger #C81E4A")] public Color dangerColor = new Color(0.784f, 0.118f, 0.290f);
    [Tooltip("Vignette の強さに足す量（脈の山）")] public float[] vignetteAdd = { 0f, 0.16f, 0.26f, 0.36f };
    [Tooltip("縁の色を赤に寄せる割合")] public float[] colorMix = { 0f, 0.75f, 0.9f, 1f };
    [Tooltip("脈の速さ（回/秒）")] public float[] pulseHz = { 0.4f, 0.55f, 0.95f, 1.6f };
    [Tooltip("脈の谷の強さ（山 = 1）")] [Range(0f, 1f)] public float pulseFloor = 0.35f;
    [Tooltip("赤い枠の濃さ（脈の山の不透明度）。段階1はかすか、段階3でもゲージが読める程度")] public float[] frameAlpha = { 0f, 0.2f, 0.36f, 0.55f };
    [Tooltip("赤い枠の太さ（1600×900 の画面での単位）")] public float[] frameWidth = { 90f, 110f, 150f, 200f };
    [Tooltip("赤い枠の Canvas の sortingOrder（GameCanvas = 10 の下、3D の上）")] public int frameSortingOrder = 9;
    [Tooltip("事故の瞬間に赤い枠を光らせる強さ（不透明度）")] [Range(0f, 1f)] public float impactFlash = 0.6f;
    [Tooltip("段階が上がるときの速さ（段階/秒）")] public float riseRate = 4f;
    [Tooltip("回復して下がるときの速さ（段階/秒）")] public float fallRate = 0.7f;

    [Header("カメラの揺れ（段階 0〜3 ごと）")]
    [Tooltip("ずっと続く小さな揺れ：回転（度）")] public float[] shakeDeg = { 0f, 0f, 0.12f, 0.3f };
    [Tooltip("ずっと続く小さな揺れ：位置（m）")] public float[] shakeMeters = { 0f, 0f, 0.004f, 0.01f };
    public float shakeHz = 7f;
    [Tooltip("事故の瞬間の揺れ：回転（度）")] public float impactDeg = 2.2f;
    [Tooltip("事故の瞬間の揺れ：位置（m）")] public float impactMeters = 0.1f;
    public float impactHz = 22f;
    [Tooltip("事故の揺れが消える速さ（/秒）")] public float impactDecay = 0.9f;
    [Tooltip("段階が 2 以上に上がったときの小さな揺れ（事故 = 1）")] [Range(0f, 1f)] public float levelUpKick = 0.25f;

    [Header("警報ランプ（段階 0〜3 ごと）")]
    [Tooltip("LampGlow の点滅の秒数に掛ける")] public float[] lampPeriodScale = { 1f, 0.75f, 0.5f, 0.3f };

    [Header("確認用")]
    [Tooltip("0〜3 なら段階をこの値に固定する（Play 中に Inspector で変えて見る）")] public int debugLevel = -1;

    /// <summary>今の緊急の段階（0 = ふつう、1 注意、2 危険、3 危機）</summary>
    public int Level { get; private set; }
    /// <summary>段階を決めたもの（"power" "oxygen" "co2" "leak"。0 のときは ""）。危機は1つだけ前面に出す（#76）ときに使う</summary>
    public string Cause { get; private set; } = "";
    /// <summary>段階が変わった（前の段階, 新しい段階）。警報の音などをつなぐ</summary>
    public event Action<int, int> OnLevelChanged;
    /// <summary>なめらかにした段階（0〜3 の小数）。見た目の強さ</summary>
    public float Smoothed => lv;

    readonly Dictionary<string, float> P = new Dictionary<string, float>();
    IGameClient hooked;
    string phase = "";
    float lv, pulsePhase, trauma, impactAt = -99f;

    Vignette vg;
    float baseIntensity; Color baseColor;
    Image frame;
    Sprite frameSprite;
    float[] lampBase = new float[0];
    int lampLevel = -1;

    Transform camT;
    bool shaken;
    Vector3 savedPos; Quaternion savedRot;

    void Awake()
    {
        LoadParams();
        if (volume != null && volume.sharedProfile != null)
        {
            var prof = volume.profile;   // 実行時の複製（sharedProfile のアセットは書き換えない）
            if (!prof.TryGet(out vg)) vg = prof.Add<Vignette>(true);
            baseIntensity = vg.intensity.value;
            baseColor = vg.color.value;
            vg.intensity.overrideState = true; vg.color.overrideState = true;
        }
        else Debug.LogWarning("[Urgency] Vignette を持つ Volume が無い。画面の縁は赤くしない");
        BuildFrame();
        lampBase = new float[lamps.Length];
        for (int i = 0; i < lamps.Length; i++) lampBase[i] = lamps[i] != null ? lamps[i].period : 0f;
    }

    void OnEnable() { RenderPipelineManager.endCameraRendering += OnEndCamera; }
    void OnDisable() { RenderPipelineManager.endCameraRendering -= OnEndCamera; Restore(); }

    void OnDestroy()
    {
        Unhook();
        if (vg != null) { vg.intensity.value = baseIntensity; vg.color.value = baseColor; }
        SetLamps(0);
        if (frameSprite != null) { Destroy(frameSprite.texture); Destroy(frameSprite); }
    }

    // ------------------------------------------------------------------ 赤い枠（実行時に作る。シーンにもアセットにも保存しない）
    const int FrameTex = 130, FrameBorder = 64;   // 9-slice：縁 64px、真ん中 2px（透明）

    void BuildFrame()
    {
        var go = new GameObject("UrgencyFrame", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        var cv = go.GetComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = frameSortingOrder;
        var sc = go.GetComponent<CanvasScaler>();   // GameCanvas と同じ（1600×900、0.5）
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1600f, 900f);
        sc.matchWidthOrHeight = 0.5f;

        var img = new GameObject("Frame", typeof(RectTransform), typeof(Image));
        img.transform.SetParent(go.transform, false);
        var rt = (RectTransform)img.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        frame = img.GetComponent<Image>();
        frame.sprite = frameSprite = MakeFrameSprite();
        frame.type = Image.Type.Sliced;
        frame.fillCenter = false;
        frame.raycastTarget = false;
        frame.color = Color.clear;
        frame.enabled = false;
    }

    /// <summary>縁が白く、内側へ向かって透明になる 9-slice の絵。角は内側を丸める</summary>
    static Sprite MakeFrameSprite()
    {
        var tex = new Texture2D(FrameTex, FrameTex, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "UrgencyFrame" };
        var px = new Color32[FrameTex * FrameTex];
        for (int y = 0; y < FrameTex; y++)
            for (int x = 0; x < FrameTex; x++)
            {
                float dx = Mathf.Min(x + 0.5f, FrameTex - x - 0.5f), dy = Mathf.Min(y + 0.5f, FrameTex - y - 0.5f);
                float ex = Mathf.Clamp01((FrameBorder - dx) / FrameBorder), ey = Mathf.Clamp01((FrameBorder - dy) / FrameBorder);
                float t = Mathf.Clamp01(Mathf.Sqrt(ex * ex + ey * ey));
                float a = t * t * t;   // 縁に寄せる（内側はすぐ消える）
                px[y * FrameTex + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, FrameTex, FrameTex), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                             new Vector4(FrameBorder, FrameBorder, FrameBorder, FrameBorder));
    }

    void UpdateFrame(float k, float tr)
    {
        if (frame == null) return;
        float a = Mathf.Max(Sample(frameAlpha, lv) * k, impactFlash * tr);
        frame.enabled = a > 0.004f;
        if (!frame.enabled) return;
        var c = dangerColor; c.a = a;
        frame.color = c;
        float w = Sample(frameWidth, lv) * Mathf.Lerp(0.85f, 1f, k) + 60f * tr;   // 脈に合わせて少し太る
        frame.pixelsPerUnitMultiplier = FrameBorder / Mathf.Max(1f, w);
    }

    void LoadParams()
    {
        if (parametersJson == null) { Debug.LogWarning("[Urgency] parameters.json が割り当てられていない。CO2 と生還ラインの段階は使わない"); return; }
        var arr = J.Arr(MiniJson.ParseObject(parametersJson.text), "params");
        if (arr == null) return;
        foreach (var o in arr)
            if (o is Dictionary<string, object> d && J.Has(d, "key")) P[J.Str(d, "key")] = J.Num(d, "value");
    }

    bool Param(string key, out float v) => P.TryGetValue(key, out v);

    // ------------------------------------------------------------------ つなぐ
    void Hook()
    {
        var c = flow != null ? flow.Client : null;
        if (c == hooked) return;
        Unhook();
        hooked = c; phase = flow != null ? flow.Model.Phase : "";
        if (c != null) { c.OnPhase += OnPhase; c.OnEvent += OnEvent; }
    }

    void Unhook()
    {
        if (hooked == null) return;
        hooked.OnPhase -= OnPhase; hooked.OnEvent -= OnEvent;
        hooked = null;
    }

    void OnPhase(string p)
    {
        string prev = phase; phase = p;
        if (prev == "stir" && p == "flight") Impact();   // 事故（M05）
    }

    void OnEvent(GameEvent e)
    {
        if (e.kind == "houston" && e.id == "M05") Impact();
    }

    /// <summary>大きく1回揺らす（事故の瞬間）。同じ事故で2回揺らさない</summary>
    [ContextMenu("Impact（事故の揺れ）")]
    public void Impact() => Impact(1f);

    public void Impact(float amount)
    {
        if (amount >= 1f && Time.unscaledTime - impactAt < 2f) return;
        if (amount >= 1f) impactAt = Time.unscaledTime;
        trauma = Mathf.Clamp01(Mathf.Max(trauma, amount));
    }

    // ------------------------------------------------------------------ 段階
    /// <summary>状態から段階を決める（Cause も決める）</summary>
    public int Evaluate(GameState s, GameModel m, out string cause)
    {
        int best = 0; string why = "";
        void Raise(int l, string c) { if (l > best) { best = l; why = c; } }

        // CabinHud.SetGauge と同じ：悪さ（1 − 残り/上限）が 0.5 で黄、0.75 で赤
        if (s.capacity > 0f) Raise(Band(1f - s.power / s.capacity), "power");
        float o2Max = m != null ? m.Max("oxygen", s.oxygen) : 0f;
        if (o2Max > 0f) Raise(Band(1f - s.oxygen / o2Max), "oxygen");
        // CO2：parameters.json（サーバーの H17・H18 と同じ）
        if (Param("co2_slow", out var co2Slow) && s.co2 >= co2Slow) Raise(2, "co2");
        else if (Param("co2_warn", out var co2Warn) && s.co2 > co2Warn) Raise(1, "co2");
        // 生還ライン（ED-A の基準）の下
        if (Param("end_oxygen_min", out var o2Min) && s.oxygen < o2Min) Raise(3, "oxygen");
        if (Param("end_power_min", out var pMin) && s.power < pMin) Raise(3, "power");
        // 事故直後の漏れ：漏れは oxygen_leak_max に届くと止まる（state に leaked が無いので時間で見る）
        if (s.leakOpen && Param("oxygen_leak_max", out var leakMax) && Param("oxygen_leak_per_s", out var leakRate) && leakRate > 0f
            && s.t < leakMax / leakRate) Raise(3, "leak");
        cause = why;
        return best;
    }

    static int Band(float bad) => bad >= 0.75f ? 2 : bad >= 0.5f ? 1 : 0;   // CabinHud.SetGauge の 0.5／0.75

    bool StoryPlaying => story != null && story.Playing;

    void Update()
    {
        Hook();
        float dt = Time.unscaledDeltaTime;

        int target = 0; string cause = "";
        var m = flow != null ? flow.Model : null;
        bool inFlight = m != null && m.Phase == "flight" && m.State != null;
        if (inFlight) target = Evaluate(m.State, m, out cause);
        if (debugLevel >= 0) { target = Mathf.Clamp(debugLevel, 0, 3); cause = "debug"; }
        else if (StoryPlaying) target = 0;
        SetLevel(target, cause);

        // なめらかに：上がるのは速く、回復は遅く。flight を出たら・演出の間はすぐ戻す
        bool snap = debugLevel < 0 && (!inFlight || StoryPlaying);
        float rate = Level > lv ? riseRate : snap ? 8f : fallRate;
        lv = Mathf.MoveTowards(lv, Level, rate * dt);
        if (snap && StoryPlaying) { lv = 0f; trauma = 0f; }

        // 縁の脈
        pulsePhase = Mathf.Repeat(pulsePhase + Sample(pulseHz, lv) * dt, 1f);
        float p01 = 0.5f - 0.5f * Mathf.Cos(pulsePhase * Mathf.PI * 2f);
        float k = Mathf.Lerp(pulseFloor, 1f, p01 * p01);   // 谷を長く、山を短く（どくん、と打つ感じ）
        if (vg != null)
        {
            vg.intensity.value = baseIntensity + Sample(vignetteAdd, lv) * k;
            vg.color.value = Color.Lerp(baseColor, dangerColor, Sample(colorMix, lv) * Mathf.Lerp(0.8f, 1f, k));
        }
        UpdateFrame(k, trauma * trauma);

        trauma = Mathf.Max(0f, trauma - impactDecay * dt);
    }

    void SetLevel(int l, string cause)
    {
        Cause = l > 0 ? cause : "";
        if (l == Level) return;
        int prev = Level;
        Level = l;
        if (l > prev && l >= 2) Impact(levelUpKick);
        SetLamps(l);
        Debug.Log("[Urgency] 段階 " + prev + " → " + l + (Cause != "" ? "（" + Cause + "）" : ""));
        OnLevelChanged?.Invoke(prev, l);
    }

    /// <summary>警報ランプの点滅を速める。段階が変わったときだけ変える（LampGlow は Time.time ÷ period で点滅するので、毎フレーム変えると乱れる）</summary>
    void SetLamps(int l)
    {
        if (l == lampLevel) return;
        lampLevel = l;
        float scale = Sample(lampPeriodScale, l);
        for (int i = 0; i < lamps.Length && i < lampBase.Length; i++)
            if (lamps[i] != null) lamps[i].period = lampBase[i] * scale;
    }

    /// <summary>段階ごとの値の配列を、小数の段階で補間する</summary>
    static float Sample(float[] a, float x)
    {
        if (a == null || a.Length == 0) return 0f;
        x = Mathf.Clamp(x, 0f, a.Length - 1);
        int i = Mathf.FloorToInt(x);
        if (i >= a.Length - 1) return a[a.Length - 1];
        return Mathf.Lerp(a[i], a[i + 1], x - i);
    }

    // ------------------------------------------------------------------ カメラの揺れ（メインカメラだけ。描いたら戻す）
    void LateUpdate()
    {
        Restore();
        var cam = mainCamera != null ? mainCamera : Camera.main;
        if (cam == null || !cam.isActiveAndEnabled) return;
        float t = Time.unscaledTime;
        float hold = Sample(shakeDeg, lv), holdM = Sample(shakeMeters, lv);
        float tr = trauma * trauma;   // 2乗：強い揺れはすぐ弱まる
        float deg = hold + impactDeg * tr, meters = holdM + impactMeters * tr;
        if (deg <= 0.0001f && meters <= 0.00001f) return;
        float f = Mathf.Lerp(shakeHz, impactHz, Mathf.Clamp01(tr * 2f));
        Vector3 r = new Vector3(Noise(t * f, 0f), Noise(t * f, 10f), Noise(t * f, 20f) * 0.5f) * deg;
        Vector3 d = new Vector3(Noise(t * f, 30f), Noise(t * f, 40f), 0f) * meters;

        camT = cam.transform;
        savedPos = camT.localPosition; savedRot = camT.localRotation;
        shaken = true;
        camT.localRotation = savedRot * Quaternion.Euler(r);
        camT.localPosition = savedPos + savedRot * d;
    }

    static float Noise(float x, float seed) => Mathf.PerlinNoise(x, seed + 0.5f) * 2f - 1f;

    void OnEndCamera(ScriptableRenderContext ctx, Camera c)
    {
        if (shaken && camT != null && c.transform == camT) Restore();
    }

    void Restore()
    {
        if (!shaken) return;
        shaken = false;
        if (camT == null) return;
        camT.localPosition = savedPos; camT.localRotation = savedRot;
    }
}
