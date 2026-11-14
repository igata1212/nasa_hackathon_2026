// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// タイトルにもクルーが顔を出す（#153）。4人（船室・部屋の画面と同じモデル）を画面の左右の下から上半身だけ出し、
// 開いたときは下から順にぽんと出て、その後ときどき1人ずつ伸び上がって手を振る（目は笑顔）。
// 部屋の画面（LobbyStage）と同じく、遠くに組んで専用のカメラで RenderTexture に描く。背景は透明にして、タイトルの背景の上に重ねる。
// 透明が残らない環境（後処理・HDR の設定しだい）では、最初の数フレームで確かめて、クルーの層を出さない（タイトルの見た目は崩れない）。
// タイトルの Root に付けるので、画面が出ている間だけ描く。置き場所・動きの値は TitleLook
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using T = UiTokens;
using L = TitleLook;

public class TitleStage : MonoBehaviour
{
    RawImage view;
    Transform root;
    Camera cam;
    RenderTexture rt;
    Transform[] slots, arms;
    CrewLook[] looks;
    bool[] smiling;
    float crewH = 1f, dist = 5f, start;
    int frames;
    bool alphaChecked, broken;

    public void Init(RawImage view)
    {
        this.view = view;
        view.raycastTarget = false;
        view.enabled = false;   // RenderTexture ができるまで出さない
        var th = T.Theme;
        int n = Seats.All.Length;
        slots = new Transform[n]; arms = new Transform[n]; looks = new CrewLook[n]; smiling = new bool[n];
        root = new GameObject("TitleStage").transform;
        root.position = L.TitleStageOrigin;

        for (int i = 0; i < n; i++)
        {
            slots[i] = new GameObject("Slot_" + Seats.All[i]).transform;
            slots[i].SetParent(root, false);
            if (th == null || th.crew == null || i >= th.crew.Length || th.crew[i] == null) continue;
            var c = Instantiate(th.crew[i], slots[i]);
            c.name = "TitleCrew_" + Seats.All[i];
            c.transform.localPosition = Vector3.zero;
            looks[i] = c.AddComponent<CrewLook>();
            looks[i].normalEuler = Vector3.zero;
            var fl = c.AddComponent<CrewFloat>();
            fl.amplitude = L.TitleCrewBob; fl.phase = i / (float)n;
            foreach (var t in c.GetComponentsInChildren<Transform>(true)) if (t.name.EndsWith(".Arm_R")) arms[i] = t;
            foreach (var r in c.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            float top = float.MinValue;
            foreach (var r in c.GetComponentsInChildren<Renderer>()) top = Mathf.Max(top, r.bounds.max.y);
            if (top > float.MinValue) crewH = Mathf.Max(0.1f, top - c.transform.position.y);
            // 真ん中（タイトル）の方へ体を向ける（左の2人は右へ、右の2人は左へ）
            float yaw = L.CrewSlots[i].x < 0.5f ? 180f - L.CrewTurnIn : 180f + L.CrewTurnIn;
            c.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // 全身の高さが画面の CrewScreenH になる距離
        dist = crewH / (L.CrewScreenH * 2f * Mathf.Tan(L.TitleFov * 0.5f * Mathf.Deg2Rad));

        var lg = new GameObject("TitleKeyLight");
        lg.transform.SetParent(root, false);
        lg.transform.localPosition = L.TitleLightPos;
        var light = lg.AddComponent<Light>();
        light.type = LightType.Point; light.range = L.TitleLightRange; light.intensity = L.TitleLightIntensity;
        light.shadows = LightShadows.None;

        // カメラ：まっすぐ前（+Z）を見る。背景は透明（色は bg なので、透明が効かなくても目立たない）。後処理と HDR は切る（透明を残すため）
        var cg = new GameObject("TitleCamera");
        cg.transform.SetParent(root, false);
        cg.transform.localPosition = new Vector3(0f, 0f, -dist);
        cam = cg.AddComponent<Camera>();
        cam.fieldOfView = L.TitleFov; cam.nearClipPlane = 0.1f; cam.farClipPlane = dist + 10f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = T.WithAlpha(T.Bg, 0f);
        cam.depth = -20f; cam.allowHDR = false; cam.allowMSAA = true;
        var data = cg.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = false; data.renderShadows = false;

        Show(isActiveAndEnabled);
    }

    void OnEnable() { start = UiEase.Now; Show(true); }
    void OnDisable() => Show(false);

    void Show(bool on)
    {
        if (root != null) root.gameObject.SetActive(on && !broken);
        if (cam != null) cam.enabled = on && !broken;
    }

    void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
        if (rt != null) { rt.Release(); Destroy(rt); }
    }

    void LateUpdate()
    {
        if (cam == null || broken) return;
        int w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
        if (rt == null || rt.width != w || rt.height != h)
        {
            if (rt != null) { cam.targetTexture = null; rt.Release(); Destroy(rt); }
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "TitleStage", antiAliasing = 4 };
            cam.targetTexture = rt;
            view.texture = rt;
            view.color = Color.white;
            frames = 0;
        }
        if (!alphaChecked && ++frames >= L.AlphaCheckFrame) CheckAlpha();
        if (broken) return;
        view.enabled = alphaChecked;   // 確かめるまでは出さない（透明が効かないと、一瞬クリームの四角が出るため）

        float now = UiEase.Now - start;
        for (int i = 0; i < slots.Length; i++)
        {
            var s = L.CrewSlots[i];
            // 頭のてっぺんを画面の (x, y) に。足はその crewH 下（画面の下に隠れる）
            var head = cam.ViewportToWorldPoint(new Vector3(s.x, s.y, dist + s.z));
            float y = head.y - crewH;

            // 開いたとき：下から順にぽんと出る
            float ip = (now - L.CrewIntroDelay - i * L.CrewIntroStagger) / L.CrewIntroDur;
            y -= (1f - UiEase.OutBack(ip)) * L.CrewIntroDrop;

            // ときどき1人ずつ伸び上がって手を振る
            float c = now - L.CrewIntroDelay - L.CrewIntroDur - i * L.PeekEvery / slots.Length;
            float rise = 0f, wave = -1f;
            if (c > 0f)
            {
                c = Mathf.Repeat(c, L.PeekEvery);
                if (c < L.PeekUp) rise = UiEase.OutBack(c / L.PeekUp);
                else if (c < L.PeekUp + L.PeekHold) { rise = 1f; wave = c - L.PeekUp; }
                else if (c < L.PeekUp + L.PeekHold + L.PeekDown) rise = 1f - UiEase.InOutSine((c - L.PeekUp - L.PeekHold) / L.PeekDown);
            }
            y += rise * L.PeekRise;
            slots[i].position = new Vector3(head.x, y, head.z);

            bool smile = rise > 0.5f;
            if (looks[i] != null && smile != smiling[i]) { smiling[i] = smile; looks[i].SetEyes(smile ? "Eyes_Relief" : "Eyes_Normal"); }
            if (arms[i] != null)
            {
                float z = 0f;
                if (wave >= 0f)
                {
                    float k = UiEase.Bump(wave / L.PeekHold);   // 上げて、下ろす
                    z = k * (T.WaveRaise + Mathf.Sin(wave * T.WaveHz * Mathf.PI * 2f) * T.WaveSwing);
                }
                arms[i].localRotation = Quaternion.Euler(0f, 0f, z);
            }
        }
    }

    /// <summary>描いた絵の上の真ん中（クルーのいない所）が透明かを1回だけ見る。透明でなければクルーの層をやめる</summary>
    void CheckAlpha()
    {
        alphaChecked = true;
        Texture2D px = null;
        var prev = RenderTexture.active;
        try
        {
            RenderTexture.active = rt;
            px = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            px.ReadPixels(new Rect(rt.width / 2, rt.height - 2, 1, 1), 0, 0);
            px.Apply();
            if (px.GetPixel(0, 0).a > 0.5f)
            {
                broken = true;
                view.enabled = false;
                Show(false);
                Debug.LogWarning("[TitleStage] RenderTexture に透明が残らないので、タイトルのクルーを出さない（カメラの後処理・HDR を確かめる）");
            }
        }
        catch (System.Exception e) { Debug.LogWarning("[TitleStage] 透明の確認ができなかった（そのまま出す）: " + e.Message); }
        finally
        {
            RenderTexture.active = prev;
            if (px != null) Destroy(px);
        }
    }
}
