// 書いた人：iizuka（作成：Claude）／ 最終更新：2026-11-14
// 部屋の画面の 3D（#72）：4人のクルー（船室と同じモデル）を手前に横一列で立たせ、足元に光る台、後ろに地球を置く。
// 船室から遠く（UiTokens.StageOrigin）に組み、専用のカメラで RenderTexture に描いて、部屋の画面の RawImage に出す。
// 部屋の画面の Root に付けるので、画面が出ている間だけ描く（GameFlow は変えない）。名前のタグ（2D）は頭の上に毎フレーム合わせる
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using T = UiTokens;

public class LobbyStage : MonoBehaviour
{
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), EmissionColor = Shader.PropertyToID("_EmissionColor");

    RawImage view;
    Transform root;
    Camera cam;
    RenderTexture rt;
    RectTransform[] tags;
    GameObject[] crew;
    Renderer[] pads;
    Transform[] arms;
    CrewLook[] looks;
    MaterialPropertyBlock mpb;
    float crewTop;

    /// <summary>組み立てる。tags は席の順（Seats.All）の名前のタグ。頭の上に合わせて動かす</summary>
    public void Init(RawImage view, RectTransform[] tags)
    {
        this.view = view; this.tags = tags;
        var th = T.Theme;
        int n = Seats.All.Length;
        crew = new GameObject[n]; pads = new Renderer[n]; arms = new Transform[n]; looks = new CrewLook[n];
        mpb = new MaterialPropertyBlock();
        root = new GameObject("LobbyStage").transform;
        root.position = T.StageOrigin;

        for (int i = 0; i < n; i++)
        {
            float x = X(i);
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(pad.GetComponent<Collider>());
            pad.name = "Pad_" + Seats.All[i];
            pad.transform.SetParent(root, false);
            pad.transform.localPosition = new Vector3(x, -T.PadHeight * 0.5f, 0f);
            pad.transform.localScale = new Vector3(T.PadDiameter, T.PadHeight * 0.5f, T.PadDiameter);   // Cylinder は高さ 2
            pads[i] = pad.GetComponent<Renderer>();
            pads[i].sharedMaterial = th.pad;
            pads[i].shadowCastingMode = ShadowCastingMode.Off;

            if (th.crew == null || i >= th.crew.Length || th.crew[i] == null) continue;
            var c = Instantiate(th.crew[i], root);
            c.name = "LobbyCrew_" + Seats.All[i];
            c.transform.localPosition = new Vector3(x, T.CrewLift, 0f);
            c.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // 正面（+Z）をカメラ（−Z 側）へ
            looks[i] = c.AddComponent<CrewLook>();
            looks[i].normalEuler = Vector3.zero;                          // 頭はまっすぐ前（カメラ）
            var fl = c.AddComponent<CrewFloat>();
            fl.amplitude = T.CrewBob; fl.phase = i / (float)n;
            foreach (var t in c.GetComponentsInChildren<Transform>(true)) if (t.name.EndsWith(".Arm_R")) arms[i] = t;
            crew[i] = c;
            if (crewTop <= 0f) crewTop = Top(c) - c.transform.position.y;
        }

        if (th.earth != null)
        {
            var e = Instantiate(th.earth, root);
            e.name = "LobbyEarth";
            e.transform.localPosition = T.EarthPos;
            float size = 0f;
            foreach (var r in e.GetComponentsInChildren<Renderer>()) size = Mathf.Max(size, r.bounds.size.x);
            if (size > 0f) e.transform.localScale *= T.EarthSize / size;
        }

        // 手前上からの明かり（船室の明かりは届かない）
        var lg = new GameObject("LobbyKeyLight");
        lg.transform.SetParent(root, false);
        lg.transform.localPosition = T.KeyLightPos;
        lg.transform.LookAt(root.TransformPoint(new Vector3(0f, T.StageLookHeight, 0f)));
        var light = lg.AddComponent<Light>();
        light.type = LightType.Spot; light.range = T.KeyLightRange; light.spotAngle = T.KeyLightAngle; light.intensity = T.KeyLightIntensity;
        light.shadows = LightShadows.Soft;

        var cg = new GameObject("LobbyCamera");
        cg.transform.SetParent(root, false);
        cam = cg.AddComponent<Camera>();
        cam.fieldOfView = T.StageFov; cam.nearClipPlane = 0.1f; cam.farClipPlane = T.StageFar;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = T.StageBg;
        cam.depth = -20f; cam.allowHDR = true; cam.allowMSAA = true;
        var data = cg.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true; data.renderShadows = true;

        Show(isActiveAndEnabled);
    }

    static float X(int i) => (i - (Seats.All.Length - 1) * 0.5f) * T.StageSpacing;

    static float Top(GameObject g)
    {
        float top = float.MinValue;
        foreach (var r in g.GetComponentsInChildren<Renderer>()) top = Mathf.Max(top, r.bounds.max.y);
        return top;
    }

    /// <summary>席の見た目。座っている人がいなければクルーを消して台を暗く、自分の席は台を明るく（go）</summary>
    public void SetSeat(int i, bool occupied, bool self)
    {
        if (pads == null) return;
        if (crew[i] != null) crew[i].SetActive(occupied);
        if (looks[i] != null) looks[i].SetEyes(self ? "Eyes_Relief" : "Eyes_Normal");
        Color c = !occupied ? T.InkSub : self ? T.Go : T.Panel;   // 空席は暗く（line だと明かりで白く見えた）
        float glow = !occupied ? T.PadGlowOpen : self ? T.PadGlowSelf : T.PadGlowOther;
        mpb.SetColor(BaseColor, c);
        mpb.SetColor(EmissionColor, c * glow);
        pads[i].SetPropertyBlock(mpb);
    }

    void OnEnable() => Show(true);
    void OnDisable() => Show(false);

    void Show(bool on)
    {
        if (root != null) root.gameObject.SetActive(on);
        if (cam != null) cam.enabled = on;
    }

    void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
        if (rt != null) { rt.Release(); Destroy(rt); }
    }

    void LateUpdate()
    {
        if (cam == null) return;
        // 画面の大きさに合わせて RenderTexture を作り直す。縦長の画面ではカメラを引いて4人を入れる
        int w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
        if (rt == null || rt.width != w || rt.height != h)
        {
            if (rt != null) { cam.targetTexture = null; rt.Release(); Destroy(rt); }
            rt = new RenderTexture(w, h, 24) { name = "LobbyStage", antiAliasing = 4 };
            cam.targetTexture = rt;
            view.texture = rt;
            view.color = Color.white;
        }
        float d = T.StageCamDistance * Mathf.Max(1f, T.StageAspect / (w / (float)h));
        cam.transform.localPosition = new Vector3(0f, T.StageCamHeight, -d);
        cam.transform.LookAt(root.TransformPoint(new Vector3(0f, T.StageLookHeight, 0f)));

        // 手を振る：席ごとに時間をずらして、WaveEvery 秒に1回、WaveTime 秒だけ右手を上げて振る
        for (int i = 0; i < arms.Length; i++)
        {
            if (arms[i] == null) continue;
            float t = Mathf.Repeat(Time.time + i * T.WaveEvery / arms.Length, T.WaveEvery);
            float z = 0f;
            if (t < T.WaveTime)
            {
                float k = Mathf.Sin(t / T.WaveTime * Mathf.PI);   // 上げて、下ろす
                z = k * (T.WaveRaise + Mathf.Sin(t * T.WaveHz * Mathf.PI * 2f) * T.WaveSwing);
            }
            arms[i].localRotation = Quaternion.Euler(0f, 0f, z);
        }

        // 名前のタグを頭の上へ（RawImage は画面いっぱいなので、ビューポートの座標がそのまま anchor になる）
        if (tags == null) return;
        for (int i = 0; i < tags.Length && i < Seats.All.Length; i++)
        {
            var p = cam.WorldToViewportPoint(root.TransformPoint(new Vector3(X(i), T.CrewLift + crewTop + T.TagAboveHead, 0f)));
            tags[i].anchorMin = tags[i].anchorMax = new Vector2(p.x, p.y);
            tags[i].anchoredPosition = Vector2.zero;
        }
    }
}
