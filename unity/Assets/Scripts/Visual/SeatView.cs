// 書いた人：Taiga（作成：Fable 5.2）／ 最終更新：2026-11-15
// 作る Issue：#167（宇宙をリアルに：数値は船内の計器で読む、ヒューストンはモニター、カメラを近く、揺れを強く）
//
// 船内（practice・stir・flight）の間、席ごとの計器モニター（Fx_SeatMonitor_<席>。modeling/fx/build_seat_monitor.py）を使って次の4つをする：
//   ① ゲージ（StatusPanel。CabinHud の Left の slot）を、画面の上の表示から、今の席のモニターの上（world-space の Canvas）へ移す。
//      席タブで席を替えたら、Canvas ごとその席のモニターへ移る（CabinHud.MoveSlot は最初の1回だけ）
//   ② ヒューストン（先生）の顔を、同じモニターの右上に映す（HoustonWindow が描く RenderTexture）。その間、字幕の左の小窓は出さない
//   ③ カメラを一人称にする：今の席のクルーの目の位置に置き、そのクルーは腕だけ描く（V キーで今までの見下ろしと切り替え）。席を替えるとなめらかに移る
//   ⑤ 肩ごしの間、手前の壁と屋根を出して船室を閉じる（見下ろしでは今までどおり外す）。船室のモデルは変えない
//   ④ 揺れを強くする（UrgencyFx の値に倍率を掛ける。事故の一発を大きく、続く揺れは少しだけ：数字が読めなくならないように）
// 置き場所・画角・倍率は下の SerializeField（見た目だけの値）。シーンへの組み込みは Editor/SeatMonitor/SeatMonitorScene.cs（手で置かない）。
// GameFlow・CabinHud・HoustonWindow には、読むための口を1つずつ足しただけ（Cabin／ActiveSeat／Texture・hideOverlay）。
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
        public Transform screen;       // その Slot_Screen（+X 右・+Y 上・+Z 画面の奥）
        [Tooltip("カメラを置く肩：+1 でモニターのローカル +X 側、-1 で −X 側")] public float side = 1f;
    }

    public GameFlow flow;
    public HoustonWindow houston;
    public UrgencyFx urgency;
    [Tooltip("空なら Camera.main")] public Camera mainCamera;
    public SeatRef[] seats = new SeatRef[0];

    [Header("カメラ（一人称：自分のクルーの目の位置）")]
    public bool seatViewOnStart = true;
    [Tooltip("一人称の間、自分のクルーは腕だけ描く（ヘルメットと胴が画面をふさぐので）")] public bool hideOwnCrew = true;
    [Tooltip("モニターのルートから見たカメラの位置（m）。x は side を掛ける")] public Vector3 cameraOffset = new Vector3(0f, 1.55f, 0.90f);   // 目の高さ。画面から約 1.1m
    [Tooltip("画面のまん中から、見る点を下げる量（m）。コンソールの操作部品も入るように")] public float lookDown = 0.27f;   // モニターを画面の上半分に入れる（下半分は吹き出しと操作の盆）
    public float fieldOfView = 58f;
    [Tooltip("席を替えたとき・切り替えたときに移る速さ（大きいほど速い）")] public float moveSpeed = 6f;

    [Header("船室を閉じる（狭く感じさせる）")]
    [Tooltip("肩ごしの間、見下ろしのために外してある手前の壁と屋根を出して、船室を閉じる")] public bool closeCabin = true;
    [Tooltip("船室の Wall_Front・Roof_Front・Roof_Back（SeatMonitorScene が入れる）")] public GameObject[] enclosure = new GameObject[0];

    [Header("モニターの上の表示")]
    [Tooltip("Canvas の大きさ（px）。モニターの画面 0.80×0.50m に合わせて縮める")] public Vector2 canvasSize = new Vector2(480f, 300f);
    public float screenWidthMeters = 0.80f;
    [Tooltip("ゲージが使う横の割合（残りがヒューストンの顔）")] [Range(0.4f, 1f)] public float gaugeWidth = 0.62f;

    [Header("揺れ（UrgencyFx の値に掛ける）")]
    public float impactScale = 1.8f;
    public float holdScale = 1.3f;

    public bool SeatViewOn { get; private set; }

    Camera cam;
    Vector3 widePos; Quaternion wideRot; float wideFov;
    bool haveWide, moved, shakeScaled, driving, enclosureShown;
    RectTransform canvasRect, gaugeRect;
    RawImage face;
    GameObject faceBox;
    string shownSeat;

    void Start()
    {
        SeatViewOn = seatViewOnStart;
        cam = mainCamera != null ? mainCamera : Camera.main;
        if (cam != null) { widePos = cam.transform.position; wideRot = cam.transform.rotation; wideFov = cam.fieldOfView; haveWide = true; }
        ScaleShake();
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

    SeatRef Current()
    {
        string id = flow != null && flow.Cabin != null ? flow.Cabin.ActiveSeat : null;
        foreach (var s in seats) if (s != null && s.seat == id && s.monitor != null && s.screen != null) return s;
        foreach (var s in seats) if (s != null && s.monitor != null && s.screen != null) return s;
        return null;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.vKey.wasPressedThisFrame && InCabin) SeatViewOn = !SeatViewOn;

        // 肩ごしの間だけ船室を閉じる（見下ろしに戻したら、また外す）
        bool closed = closeCabin && SeatViewOn && InCabin;
        if (closed != enclosureShown)
        {
            enclosureShown = closed;
            foreach (var g in enclosure) if (g != null) g.SetActive(closed);
        }

        var seat = Current();
        if (seat == null) return;
        UpdateCanvas(seat);
        UpdateCamera(seat);
        UpdateOwnCrew(seat);
    }

    // ------------------------------------------------------------------ 一人称：自分のクルーは腕だけ見せる
    // カメラは自分のクルーの目の位置にいるので、ヘルメットや胴が画面をふさぐ。腕（Arm_L・Arm_R）以外を描かない。
    // Renderer.forceRenderingOff を使う（目の切り替えなど、ほかの部品の SetActive・enabled とぶつからない）
    static readonly System.Collections.Generic.Dictionary<string, string> CrewSlot = new System.Collections.Generic.Dictionary<string, string>
    {
        { Seats.Pilot, "Slot_Crew_Pilot" }, { Seats.Life, "Slot_Crew_LifeSupport" },
        { Seats.Comms, "Slot_Crew_CommsPower" }, { Seats.Planner, "Slot_Crew_MissionPlanner" },
    };
    readonly System.Collections.Generic.List<Renderer> hidden = new System.Collections.Generic.List<Renderer>();
    string hiddenSeat;

    void UpdateOwnCrew(SeatRef seat)
    {
        string want = hideOwnCrew && SeatViewOn && InCabin ? seat.seat : null;
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

    // ------------------------------------------------------------------ モニターの上の表示
    void UpdateCanvas(SeatRef seat)
    {
        if (flow == null || flow.Cabin == null) return;
        if (canvasRect == null) BuildCanvas();
        if (shownSeat != seat.seat)
        {
            shownSeat = seat.seat;
            canvasRect.SetParent(seat.screen, false);
            canvasRect.localPosition = new Vector3(0f, 0f, -0.004f);   // 画面の表の面より少し手前（+Z が奥）
            canvasRect.localRotation = Quaternion.identity;
            float k = screenWidthMeters / canvasSize.x;
            canvasRect.localScale = new Vector3(k, k, k);
        }
        if (!moved) { moved = true; flow.Cabin.MoveSlot(HudSlot.Left, gaugeRect); }

        // ヒューストンの顔：モニターに映している間は、字幕の左の小窓を出さない
        bool onMonitor = SeatViewOn && InCabin;
        if (houston != null)
        {
            houston.hideOverlay = onMonitor;
            if (face.texture == null && houston.Texture != null) face.texture = houston.Texture;
            faceBox.SetActive(houston.Visible && face.texture != null);
        }
    }

    void BuildCanvas()
    {
        var go = new GameObject("SeatMonitorCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam;
        go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;   // 3D の中の文字がぼやけないように
        canvasRect = (RectTransform)go.transform;
        canvasRect.sizeDelta = canvasSize;

        var bg = Ui.Rect("Face", canvasRect); Ui.Fill(bg);
        var bgImg = bg.gameObject.AddComponent<Image>(); bgImg.color = UiTokens.Ink; bgImg.raycastTarget = false;

        gaugeRect = Ui.Rect("Gauges", canvasRect);
        Ui.Anchor(gaugeRect, 0f, 0f, gaugeWidth, 1f, 6f, 6f, 0f, 6f);

        // 右上：ヒューストンの顔（16:10）。下に名前
        var box = Ui.Rect("Houston", canvasRect);
        Ui.Anchor(box, gaugeWidth, 0f, 1f, 1f, 6f, 6f, 6f, 6f);
        faceBox = box.gameObject;
        var view = Ui.Rect("View", box);
        view.anchorMin = new Vector2(0f, 1f); view.anchorMax = new Vector2(1f, 1f); view.pivot = new Vector2(0.5f, 1f);
        float w = canvasSize.x * (1f - gaugeWidth) - 12f;
        view.sizeDelta = new Vector2(0f, w / 1.6f); view.anchoredPosition = Vector2.zero;
        face = view.gameObject.AddComponent<RawImage>(); face.raycastTarget = false;
        var label = UiKit.Text(box, "HOUSTON", TextStyle.Label, UiTokens.HudText, TMPro.TextAlignmentOptions.Top, false);
        var nr = label.rectTransform;
        nr.anchorMin = new Vector2(0f, 1f); nr.anchorMax = new Vector2(1f, 1f); nr.pivot = new Vector2(0.5f, 1f);
        nr.sizeDelta = new Vector2(0f, 28f); nr.anchoredPosition = new Vector2(0f, -(w / 1.6f) - 4f);
        faceBox.SetActive(false);
    }

    // ------------------------------------------------------------------ カメラ
    void UpdateCamera(SeatRef seat)
    {
        if (cam == null || !haveWide) return;
        bool on = SeatViewOn && InCabin;
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
            pos = seat.monitor.TransformPoint(new Vector3(cameraOffset.x * seat.side, cameraOffset.y, cameraOffset.z));
            Vector3 look = seat.screen.position - seat.screen.up * lookDown;
            rot = Quaternion.LookRotation(look - pos, Vector3.up);
            fov = fieldOfView;
        }
        float k = 1f - Mathf.Exp(-moveSpeed * Time.unscaledDeltaTime);
        var t = cam.transform;
        t.position = Vector3.Lerp(t.position, pos, k);
        t.rotation = Quaternion.Slerp(t.rotation, rot, k);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, k);
    }
}
