// 書いた人：Taiga（作成：Fable 5.2）／ 最終更新：2026-11-15 ／ 更新：Taiga（Opus 5.5、2026-11-15、画面から針のメーターに替えた・見やすさの直し）
// 作る Issue：#167（宇宙をリアルに：数値は船内の計器で読む、ヒューストンはモニター、カメラを近く、揺れを強く、船室を狭く）
//
// 船内（practice・stir・flight）の間、席ごとの計器盤（Fx_SeatMonitor_<席>。modeling/fx/build_seat_monitor.py）を使って次をする：
//   ① 計器盤のメーター5つ（電力・酸素・温度・CO2・燃料）の針を、サーバーの値で動かす。メーターの下に数字とアイコン。4席の盤が全部動く
//      針の振れ方は StatusPanel のゲージと同じ割合（満タン＝電力は capacity、酸素・燃料は設計の合計、温度は temp_start、CO2 は 100）
//   ② 盤の状態ランプ：緑＝ふつう、黄＝注意、赤＝危険（UrgencyFx.Level）
//   ③ ヒューストン（先生）の顔を、盤の右下の小さい画面に映す（HoustonWindow の RenderTexture）。その間、字幕の左の小窓は出さない
//   ④ カメラを一人称にする：今の席のクルーの目の位置から盤を正面に見る。そのクルーは腕だけ描く。V キーで今までの見下ろしと切り替え
//      一人称の間は、画面の上の左のゲージ（StatusPanel）を出さない（盤で読む）
//   ⑤ 一人称の間、手前の壁と屋根を出して船室を閉じる（狭く感じさせる）。警報ランプの赤い光は届く範囲を縮める（顔の近くで画面を赤く塗りつぶさないように）
//   ⑥ 揺れを強くする（UrgencyFx の値に倍率を掛ける。事故の一発を大きく、続く揺れは少しだけ：針が読めなくならないように）
// 置き場所・画角・倍率は下の SerializeField（見た目だけの値）。シーンへの組み込みは Editor/SeatMonitor/SeatMonitorScene.cs（手で置かない）。
// 値は判定しない（UI は判定をしない）：サーバーの値を割合にして針を回すだけ。危ない側はメーターのオレンジの帯で見せる。
// GameFlow・CabinHud・HoustonWindow には、読むための口を1つずつ足しただけ（Cabin／ActiveSeat／Texture・hideOverlay）。
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SeatView : MonoBehaviour
{
    [System.Serializable]
    public class SeatRef
    {
        public string seat;            // Seats の id（pilot・life_support・comms_power・mission_planner）
        public Transform monitor;      // Fx_SeatMonitor_<席> のルート（コンソールと同じ Slot の子）
        public Transform screen;       // その Slot_Screen（ヒューストンの画面。+X 右・+Y 上・+Z 盤の奥）
        [Tooltip("使わない（前の版の肩ごしのカメラの側）")] public float side = 1f;
    }

    public GameFlow flow;
    public HoustonWindow houston;
    public UrgencyFx urgency;
    [Tooltip("空なら Camera.main")] public Camera mainCamera;
    public SeatRef[] seats = new SeatRef[0];

    [Header("カメラ（一人称：自分のクルーの目の位置）")]
    public bool seatViewOnStart = true;
    [Tooltip("一人称の間、自分のクルーは腕だけ描く（ヘルメットと胴が画面をふさぐので）")] public bool hideOwnCrew = true;
    [Tooltip("計器盤のまん中の真正面から見た、カメラの位置（盤のルートの向きで m。y は床からの高さ、z は手前へ）")] public Vector3 cameraOffset = new Vector3(0f, 1.55f, 1.15f);   // #210 引いて、針5つを上の1行と吹き出しの間に入れる
    [Tooltip("盤のまん中から、見る点を下げる量（m）")] public float lookDown = 0.18f;   // #210 盤を少し上に（吹き出しの上に針5つ）
    public float fieldOfView = 54f;
    [Tooltip("席を替えたとき・切り替えたときに移る速さ（大きいほど速い）")] public float moveSpeed = 6f;

    [Header("船室を閉じる（狭く感じさせる）")]
    [Tooltip("一人称の間、見下ろしのために外してある手前の壁と屋根を出して、船室を閉じる")] public bool closeCabin = true;
    [Tooltip("船室の Wall_Front・Roof_Front・Roof_Back（SeatMonitorScene が入れる）")] public GameObject[] enclosure = new GameObject[0];
    [Tooltip("一人称の間の、警報ランプの赤い光（Alarm Light）の届く範囲（m）")] public float alarmLightRange = 1.2f;

    [Header("メーター")]
    [Tooltip("針が動く速さ（大きいほど速い）")] public float needleSpeed = 4f;
    public float dialFontSize = 34f;
    public float dialIconSize = 30f;

    [Header("揺れ（UrgencyFx の値に掛ける）")]
    public float impactScale = 1.8f;
    public float holdScale = 1.3f;

    public bool SeatViewOn { get; private set; }
    /// <summary>#210：一人称の間 true（V キーの見下ろしや船内以外では false）。HoustonBox が吹き出しの置き場所を替えるのに読む</summary>
    public static bool FirstPerson { get; private set; }

    // メーターの並び（モデルの Move_Needle_<名前>・Slot_Dial_<名前>）と、アイコン・単位の parameters.json のキー
    static readonly (string part, string icon, string chip, string unitParam)[] Meters =
    {
        ("Power", "power", null, "power_per_battery"), ("Oxygen", null, "O₂", "oxygen_start"),
        ("Temp", "temperature", null, "temp_start"), ("Co2", "co2", null, "co2_start"), ("Fuel", "fuel", null, "fuel_start"),
    };
    const float Sweep = 120f, Co2Scale = 100f;
    const float DialPx = 200f, DialMeters = 0.20f;          // メーターの Canvas（px）と、その大きさ（m）
    const float ScreenPxW = 240f, ScreenPxH = 150f, ScreenMeters = 0.24f;

    class Panel
    {
        public SeatRef seat;
        public Transform panel;                         // Slot_Panel（無ければ screen）
        public Transform[] needles = new Transform[Meters.Length];
        public TextMeshProUGUI[] values = new TextMeshProUGUI[Meters.Length];
        public float[] angle = new float[Meters.Length];
        public Renderer[] lamps = new Renderer[3];      // 緑・黄・赤
        public GameObject face;
    }

    readonly List<Panel> panels = new List<Panel>();
    readonly float[] frac = new float[Meters.Length];
    readonly string[] text = new string[Meters.Length];
    readonly List<(Light light, float range)> alarmLights = new List<(Light, float)>();
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    MaterialPropertyBlock offBlock;
    int lampLevel = -1;

    Camera cam;
    Vector3 widePos; Quaternion wideRot; float wideFov;
    bool haveWide, shakeScaled, driving, enclosureShown, alarmDimmed, hudHidden;

    void Start()
    {
        SeatViewOn = seatViewOnStart;
        cam = mainCamera != null ? mainCamera : Camera.main;
        if (cam != null) { widePos = cam.transform.position; wideRot = cam.transform.rotation; wideFov = cam.fieldOfView; haveWide = true; }
        ScaleShake();
        offBlock = new MaterialPropertyBlock();
        offBlock.SetColor(EmissionId, Color.black);
        foreach (var s in seats) if (s != null && s.monitor != null) panels.Add(BuildPanel(s));
        foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include))
            if (l.name == "Alarm Light") alarmLights.Add((l, l.range));
    }

    void ScaleShake()
    {
        if (shakeScaled || urgency == null) return;
        shakeScaled = true;
        urgency.impactDeg *= impactScale; urgency.impactMeters *= impactScale;
        urgency.levelUpKick = Mathf.Clamp01(urgency.levelUpKick * impactScale);
        for (int i = 0; i < urgency.shakeDeg.Length; i++) urgency.shakeDeg[i] *= holdScale;
        for (int i = 0; i < urgency.shakeMeters.Length; i++) urgency.shakeMeters[i] *= holdScale;
    }

    bool InCabin
    {
        get
        {
            if (flow == null || flow.Model == null) return false;
            string p = flow.Model.Phase;
            return p == "practice" || p == "stir" || p == "flight";
        }
    }

    // ------------------------------------------------------------------ 組み立て（実行時。モデルの部品を名前で探す）
    static Transform Part(Transform root, string part)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == part || t.name.EndsWith("." + part)) return t;
        return null;
    }

    Panel BuildPanel(SeatRef s)
    {
        var p = new Panel { seat = s };
        p.panel = Part(s.monitor, "Slot_Panel") ?? s.screen;
        for (int i = 0; i < Meters.Length; i++)
        {
            var m = Meters[i];
            p.needles[i] = Part(s.monitor, "Move_Needle_" + m.part);
            p.angle[i] = -Sweep;
            var slot = Part(s.monitor, "Slot_Dial_" + m.part);
            if (slot != null) p.values[i] = BuildDial(slot, m.icon, m.chip);
        }
        string[] lampNames = { "Lamp_Green", "Lamp_Yellow", "Lamp_Red" };
        for (int i = 0; i < 3; i++) { var t = Part(s.monitor, lampNames[i]); p.lamps[i] = t != null ? t.GetComponent<Renderer>() : null; }
        if (s.screen != null) p.face = BuildFace(s.screen);
        return p;
    }

    static RectTransform WorldCanvas(string name, Transform parent, Vector2 px, float meters)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;   // 3D の中の文字がぼやけないように
        var r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        r.sizeDelta = px;
        r.localPosition = new Vector3(0f, 0f, -0.004f);   // 面より少し手前（+Z が奥）
        r.localRotation = Quaternion.identity;
        float k = meters / px.x;
        r.localScale = new Vector3(k, k, k);
        return r;
    }

    /// <summary>メーターの下の空き（針が通らない所）に、アイコンと数字</summary>
    TextMeshProUGUI BuildDial(Transform slot, string icon, string chip)
    {
        var c = WorldCanvas("DialText", slot, new Vector2(DialPx, DialPx), DialMeters);
        var row = Ui.Rect("Row", c);
        row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
        row.sizeDelta = new Vector2(150f, 40f);
        row.anchoredPosition = new Vector2(0f, -56f);
        if (icon != null)
        {
            var img = UiKit.Icon(row, icon, dialIconSize, UiTokens.Ink);
            var ir = img.rectTransform; ir.anchorMin = ir.anchorMax = new Vector2(0f, 0.5f); ir.pivot = new Vector2(0f, 0.5f);
            ir.sizeDelta = new Vector2(dialIconSize, dialIconSize); ir.anchoredPosition = new Vector2(4f, 0f);
        }
        else
        {
            var t = UiKit.Text(row, chip, TextStyle.Label, UiTokens.Ink, TextAlignmentOptions.MidlineLeft, false);
            t.fontSize = dialFontSize * 0.75f;
            var tr = t.rectTransform; tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(0f, 1f); tr.pivot = new Vector2(0f, 0.5f);
            tr.sizeDelta = new Vector2(40f, 0f); tr.anchoredPosition = new Vector2(2f, 0f);
        }
        var v = UiKit.Text(row, "", TextStyle.Label, UiTokens.Ink, TextAlignmentOptions.MidlineRight, false);
        v.fontSize = dialFontSize;
        var vr = v.rectTransform; vr.anchorMin = new Vector2(0f, 0f); vr.anchorMax = new Vector2(1f, 1f);
        vr.offsetMin = new Vector2(dialIconSize + 8f, 0f); vr.offsetMax = new Vector2(-4f, 0f);
        return v;
    }

    GameObject BuildFace(Transform screen)
    {
        var c = WorldCanvas("HoustonScreen", screen, new Vector2(ScreenPxW, ScreenPxH), ScreenMeters);
        // #210：話していない間は暗い画面＋小さい待機の印（青緑のべた塗りをやめる）。電波が無いときは印を通信途絶のアイコンに
        var dark = Ui.PanelImg(c, UiTokens.HudFace, "Dark"); dark.raycastTarget = false; Ui.Fill(dark.rectTransform);
        var dot = UiKit.Round(c, 9f, UiTokens.HudSub, "Standby"); dot.raycastTarget = false;
        var dr = dot.rectTransform; dr.anchorMin = dr.anchorMax = dr.pivot = new Vector2(0.5f, 0.5f); dr.sizeDelta = new Vector2(18f, 18f); dr.anchoredPosition = Vector2.zero;
        var sig = UiKit.Icon(c, HoustonStyle.NoSignalIcon, 56f, UiTokens.Danger); sig.name = "NoSignal"; sig.raycastTarget = false;
        sig.rectTransform.anchorMin = sig.rectTransform.anchorMax = sig.rectTransform.pivot = new Vector2(0.5f, 0.5f); sig.rectTransform.anchoredPosition = Vector2.zero;
        sig.gameObject.SetActive(false);
        var view = Ui.Rect("View", c); Ui.Fill(view);
        var img = view.gameObject.AddComponent<RawImage>(); img.raycastTarget = false;
        if (houston != null && houston.Texture != null) img.texture = houston.Texture;
        view.gameObject.SetActive(false);
        c.gameObject.SetActive(false);
        return c.gameObject;
    }

    SeatRef Current()
    {
        string id = flow != null && flow.Cabin != null ? flow.Cabin.ActiveSeat : null;
        foreach (var s in seats) if (s != null && s.seat == id && s.monitor != null) return s;
        foreach (var s in seats) if (s != null && s.monitor != null) return s;
        return null;
    }

    Panel PanelOf(SeatRef s)
    {
        foreach (var p in panels) if (p.seat == s) return p;
        return null;
    }

    // ------------------------------------------------------------------ 毎フレーム
    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.vKey.wasPressedThisFrame && InCabin) SeatViewOn = !SeatViewOn;
        bool on = SeatViewOn && InCabin;
        FirstPerson = on;

        // 船室を閉じる・警報の光を縮める・画面の上の左のゲージを隠す（一人称の間だけ）
        bool closed = closeCabin && on;
        if (closed != enclosureShown)
        {
            enclosureShown = closed;
            foreach (var g in enclosure) if (g != null) g.SetActive(closed);
        }
        if (on != alarmDimmed)
        {
            alarmDimmed = on;
            foreach (var a in alarmLights) if (a.light != null) a.light.range = on ? Mathf.Min(a.range, alarmLightRange) : a.range;
        }
        if (flow != null && flow.Cabin != null && on != hudHidden)
        {
            hudHidden = on;
            flow.Cabin.Slot(HudSlot.Left).gameObject.SetActive(!on);
        }

        UpdateMeters();
        UpdateLamps();
        UpdateFace(on);

        var seat = Current();
        if (seat == null) return;
        UpdateCamera(seat, on);
        UpdateOwnCrew(seat, on);
    }

    // ------------------------------------------------------------------ メーター（StatusPanel のゲージと同じ割合）
    void UpdateMeters()
    {
        if (flow == null || flow.Model == null) return;
        var m = flow.Model;
        var s = m.State;
        if (s != null)
        {
            Set(0, s.power, Mathf.Max(s.capacity, 1f), Floor(s.power, 0));
            Set(1, s.oxygen, m.Max("oxygen", s.oxygen), Floor(s.oxygen, 1));
            float tMax = GameParams.TryGet("temp_start", out var t0) && t0 > 0f ? t0 : m.Max("temp", s.temp);
            Set(2, s.temp, tMax, Floor(s.temp, 2));
            Set(3, s.co2, Co2Scale, Mathf.CeilToInt(s.co2) + GameParams.Unit(Meters[3].unitParam));
            Set(4, s.fuel, m.Max("fuel", s.fuel), Floor(s.fuel, 4));
        }
        else
        {
            // 練習・かき混ぜの間（state が来ない）：設計の合計で満タン（StatusPanel の Quiet と同じ）
            var tot = m.Totals;
            float cap = J.Num(tot, "capacity"), o2 = J.Num(tot, "oxygen"), fuel = J.Num(tot, "fuel");
            GameParams.TryGet("temp_start", out var t0);
            GameParams.TryGet("co2_start", out var c0);
            Set(0, cap, Mathf.Max(cap, 1f), tot != null ? Floor(cap, 0) : "");
            Set(1, o2, Mathf.Max(o2, 1f), tot != null ? Floor(o2, 1) : "");
            Set(2, t0, Mathf.Max(t0, 1f), GameParams.Has("temp_start") ? Floor(t0, 2) : "");
            Set(3, c0, Co2Scale, GameParams.Has("co2_start") ? Mathf.CeilToInt(c0) + GameParams.Unit(Meters[3].unitParam) : "");
            Set(4, fuel, Mathf.Max(fuel, 1f), tot != null ? Floor(fuel, 4) : "");
        }

        float k = 1f - Mathf.Exp(-needleSpeed * Time.unscaledDeltaTime);
        foreach (var p in panels)
            for (int i = 0; i < Meters.Length; i++)
            {
                float target = -Sweep + 2f * Sweep * frac[i];
                p.angle[i] = Mathf.Lerp(p.angle[i], target, k);
                if (p.needles[i] != null) p.needles[i].localRotation = Quaternion.Euler(0f, 0f, p.angle[i]);
                if (p.values[i] != null && p.values[i].text != text[i]) p.values[i].text = text[i];
            }
    }

    void Set(int i, float v, float max, string s)
    {
        frac[i] = Mathf.Clamp01(v / Mathf.Max(max, 0.0001f));
        text[i] = s;
    }

    /// <summary>数字は切り捨て（「少ないほど悪い」値を、実際より良く見せない。StatusPanel と同じ）＋ parameters.json の単位</summary>
    static string Floor(float v, int meter) => Mathf.FloorToInt(v) + GameParams.Unit(Meters[meter].unitParam);

    void UpdateLamps()
    {
        int lv = urgency != null && InCabin ? Mathf.Clamp(urgency.Level, 0, 2) : 0;   // 0 緑・1 黄・2 以上は赤
        if (lv == lampLevel) return;
        lampLevel = lv;
        foreach (var p in panels)
            for (int i = 0; i < 3; i++)
                if (p.lamps[i] != null) p.lamps[i].SetPropertyBlock(i == lv ? null : offBlock);
    }

    void UpdateFace(bool on)
    {
        if (houston == null) return;
        houston.hideOverlay = on;
        // #210：一人称の間は画面を出しっぱなしにする。話している間（小窓が描いている間）だけ顔、そうでなければ暗い画面＋待機の印、電波が無いときは通信途絶の印
        bool talk = houston.Visible && houston.Texture != null;
        bool lost = HoustonBox.Current != null && HoustonBox.Current.Link == HoustonLink.NoSignal;
        foreach (var p in panels)
        {
            if (p.face == null) continue;
            if (p.face.activeSelf != on) p.face.SetActive(on);
            if (!on) continue;
            var img = p.face.GetComponentInChildren<RawImage>(true);
            if (img != null && img.texture == null) img.texture = houston.Texture;
            bool face = talk && !lost;
            if (img != null && img.gameObject.activeSelf != face) img.gameObject.SetActive(face);
            var sb = p.face.transform.Find("Standby"); if (sb != null && sb.gameObject.activeSelf != (!face && !lost)) sb.gameObject.SetActive(!face && !lost);
            var ns = p.face.transform.Find("NoSignal"); if (ns != null && ns.gameObject.activeSelf != lost) ns.gameObject.SetActive(lost);
        }
    }

    // ------------------------------------------------------------------ カメラ（一人称：盤を正面に見る）
    void UpdateCamera(SeatRef seat, bool on)
    {
        if (cam == null || !haveWide) return;
        if (!on && !driving) return;                 // 見下ろしのときはカメラに触らない（ほかの部品の邪魔をしない）
        Vector3 pos = widePos; Quaternion rot = wideRot; float fov = wideFov;
        if (!on && (cam.transform.position - widePos).sqrMagnitude < 0.0001f)
        {
            driving = false;                         // 元の見下ろしに戻りきった
            cam.transform.SetPositionAndRotation(widePos, wideRot); cam.fieldOfView = wideFov;
            return;
        }
        driving = true;
        if (on)
        {
            var p = PanelOf(seat);
            Transform target = p != null && p.panel != null ? p.panel : seat.screen;
            Vector3 local = seat.monitor.InverseTransformPoint(target.position);   // 盤のまん中（盤のルートの向き）。まん中の2席は横にずれている
            pos = seat.monitor.TransformPoint(new Vector3(local.x + cameraOffset.x, cameraOffset.y, cameraOffset.z));
            Vector3 look = target.position - Vector3.up * lookDown;
            rot = Quaternion.LookRotation(look - pos, Vector3.up);
            fov = fieldOfView;
        }
        float k = 1f - Mathf.Exp(-moveSpeed * Time.unscaledDeltaTime);
        var t = cam.transform;
        t.position = Vector3.Lerp(t.position, pos, k);
        t.rotation = Quaternion.Slerp(t.rotation, rot, k);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, k);
    }

    // ------------------------------------------------------------------ 一人称：自分のクルーは腕だけ見せる
    // カメラは自分のクルーの目の位置にいるので、ヘルメットや胴が画面をふさぐ。腕（Arm_L・Arm_R）以外を描かない。
    // Renderer.forceRenderingOff を使う（目の切り替えなど、ほかの部品の SetActive・enabled とぶつからない）
    static readonly Dictionary<string, string> CrewSlot = new Dictionary<string, string>
    {
        { Seats.Pilot, "Slot_Crew_Pilot" }, { Seats.Life, "Slot_Crew_LifeSupport" },
        { Seats.Comms, "Slot_Crew_CommsPower" }, { Seats.Planner, "Slot_Crew_MissionPlanner" },
    };
    readonly List<Renderer> hidden = new List<Renderer>();
    string hiddenSeat;

    void UpdateOwnCrew(SeatRef seat, bool on)
    {
        string want = hideOwnCrew && on ? seat.seat : null;
        if (want == hiddenSeat) return;
        hiddenSeat = want;
        foreach (var r in hidden) if (r != null) r.forceRenderingOff = false;
        hidden.Clear();
        if (want == null || !CrewSlot.TryGetValue(want, out var slotName)) return;
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            bool dual = (want == Seats.Comms || want == Seats.Planner) && t.name.EndsWith("Slot_Crew_Dual_CommsPlanner");   // 3人のとき：2台を兼ねるクルー
            if (!dual && t.name != slotName && !t.name.EndsWith("." + slotName)) continue;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.Contains(".Arm_")) continue;
                r.forceRenderingOff = true; hidden.Add(r);
            }
        }
    }
}
