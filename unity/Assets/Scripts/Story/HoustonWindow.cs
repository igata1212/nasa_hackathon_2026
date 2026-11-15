// 書いた人：Claude ／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-14、#146 吹き出しは HoustonBox.Current から探す）／ 更新：iizuka（Opus 5.5、2026-11-15、#189 下の帯の文字を 22 に）
// 演出2：管制室の先生の小窓（#124。storyboard.md の場面4「ここが先生」）。
// 管制室（Scene_MissionControl）と先生（Crew_Houston）・管制官を船内から遠くに組み、専用のカメラで RenderTexture に描いて、
// ヒューストンの字幕の窓（HoustonBox。GameCanvas/Houston）の左に小窓で出す。字幕が出ている間だけ描く。
// 口：字幕の文字が変わったら、文字数に応じた時間だけ口を動かす（Face のブレンドシェイプ MouthOpen など。modeling/scene/README.md）。
//     Talk(true/false) で外から動かすこともできる（自動の判定より優先）。
// HoustonBox は変えない：GameCanvas/Houston を名前で探し、その子として小窓を足す（字幕と一緒に出入りし、置き場所の変更にも付いていく）。
// 設計の画面（字幕の窓が左下に寄る）では、左に場所がないので出さない。
// 下の帯（#189）：船内などの小窓は「ON EARTH」、ブリーフィングの大きい窓は「MISSION CONTROL」。どちらも補助ラベル 22（ui-guidelines 2章：これより小さくしない）。
//   吹き出しの名前タグ HOUSTON と合わせて「地球にいるヒューストン」といつも読める（「Mission Control on Earth」の説明は最初の 20 秒だけなので、こちらは消さない）
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HoustonWindow : MonoBehaviour
{
    public GameFlow flow;
    public StoryDirector director;
    public Camera cam;                   // Slot_PopupCamera のカメラ
    public GameObject set;               // 管制室の舞台（出す間だけ有効）
    public SkinnedMeshRenderer face;     // Crew_Houston.Face
    public Transform head;               // Crew_Houston.Head
    public SkinnedMeshRenderer[] crowdFaces;   // うしろの管制官（まばたきだけ）
    public bool autoTalk = true;

    RectTransform panel, frame;
    Text line, noSignal;
    TextMeshProUGUI caption;
    RawImage img;
    RenderTexture rt;
    string lastText = "";
    float talkUntil, nextBlink, blinkUntil;
    bool forced, forcedOn;
    int iOpen = -1, iO = -1, iWide = -1, iBlink = -1, iSmile = -1, iWorried = -1;
    Quaternion headRest = Quaternion.identity;

    /// <summary>口を動かす（true）・止める（false）。呼ぶと自動の判定より優先する。ReleaseTalk() で自動に戻す</summary>
    public void Talk(bool on) { forced = true; forcedOn = on; }
    public void ReleaseTalk() { forced = false; }
    public bool Talking { get; private set; }
    public bool Visible { get; private set; }

    void Start()
    {
        if (face != null && face.sharedMesh != null)
        {
            var m = face.sharedMesh;
            iOpen = m.GetBlendShapeIndex("MouthOpen"); iO = m.GetBlendShapeIndex("MouthO"); iWide = m.GetBlendShapeIndex("MouthWide");
            iBlink = m.GetBlendShapeIndex("Blink"); iSmile = m.GetBlendShapeIndex("Smile"); iWorried = m.GetBlendShapeIndex("BrowsWorried");
            if (iOpen < 0) Debug.LogWarning("[Story] Crew_Houston.Face に MouthOpen のブレンドシェイプが無い");
        }
        if (head != null) headRest = head.localRotation;
        rt = new RenderTexture(384, 240, 24) { name = "HoustonWindow", antiAliasing = 2 };
        if (cam != null) { cam.targetTexture = rt; cam.enabled = false; }
        if (set != null) set.SetActive(false);
    }

    void OnDestroy() { if (rt != null) { rt.Release(); Destroy(rt); } }

    void Attach()
    {
        // 字幕の吹き出し（HoustonBox.Current.Root）に付く。どこに載せても付いていく（#167）。無いときだけ前の名前で探す
        Transform p = HoustonBox.Current != null && HoustonBox.Current.Root != null ? HoustonBox.Current.Root.transform : null;
        if (p == null && flow != null) p = flow.transform.Find("GameCanvas/Houston");
        if (p == null) return;
        panel = (RectTransform)p;
        foreach (var tx in panel.GetComponentsInChildren<Text>(true))   // 見出し「HOUSTON」の次が台詞（HoustonBox の作り順）
            if (tx.text != "HOUSTON") { line = tx; break; }

        var fbg = Ui.PanelImg(panel, Ui.Orange, "HoustonWindow"); frame = fbg.rectTransform;
        fbg.raycastTarget = false;
        var ir = Ui.Rect("View", frame); Ui.Fill(ir); ir.offsetMin = new Vector2(3, 3); ir.offsetMax = new Vector2(-3, -3);
        img = ir.gameObject.AddComponent<RawImage>(); img.texture = rt; img.raycastTarget = false;
        noSignal = Ui.Label(frame, "NO SIGNAL", 22, Ui.Red, TextAnchor.MiddleCenter, FontStyle.Bold);
        Ui.Fill(noSignal.rectTransform);
        // 下の帯：紺の帯に白の補助ラベル 22（3D の絵の上でも読めるように）。文字は Update で窓の大きさに合わせて替える
        var strip = Ui.PanelImg(frame, UiTokens.WithAlpha(UiTokens.Ink, HoustonStyle.WindowCaptionAlpha), "Caption");
        strip.raycastTarget = false;
        var sr = strip.rectTransform;
        sr.anchorMin = Vector2.zero; sr.anchorMax = new Vector2(1f, 0f); sr.pivot = new Vector2(0.5f, 0f);
        sr.offsetMin = new Vector2(3f, 3f); sr.offsetMax = new Vector2(-3f, 3f + HoustonStyle.WindowCaptionH);
        caption = UiKit.Text(strip.transform, HoustonStyle.WindowTagSmall, TextStyle.Label, UiTokens.Panel, TMPro.TextAlignmentOptions.Center, false);
        UiKit.Fill(caption.rectTransform);
    }

    void Update()
    {
        if (panel == null) { Attach(); if (panel == null) return; }
        bool docked = panel.anchorMin.x < 0.1f;   // 設計の画面（左下に寄せた窓）
        bool show = panel.gameObject.activeInHierarchy && !docked && !(director != null && director.Playing);
        frame.gameObject.SetActive(show);
        if (show != Visible)
        {
            Visible = show;
            if (set != null) set.SetActive(show);
            if (cam != null) cam.enabled = show;
        }
        if (!show) { Talking = false; return; }

        // 字幕の窓の左（下をそろえる）
        frame.anchorMin = frame.anchorMax = new Vector2(0f, 0f);
        frame.pivot = new Vector2(1f, 0f);
        bool brief = HoustonBox.Current != null && HoustonBox.Current.BriefingPlaying;   // #146：ブリーフィングは大きく（大きさは HoustonStyle）
        frame.sizeDelta = brief ? HoustonStyle.WindowBriefing : HoustonStyle.WindowSmall;
        frame.anchoredPosition = new Vector2(-(brief ? HoustonStyle.WindowGapBriefing : HoustonStyle.WindowGap), 0f);
        string tag = brief ? HoustonStyle.WindowTagBriefing : HoustonStyle.WindowTagSmall;
        if (caption.text != tag) caption.text = tag;
        frame.SetAsLastSibling();

        string txt = line != null ? line.text : "";
        bool lost = txt == "NO SIGNAL" || txt == "COMMS OFF";
        noSignal.gameObject.SetActive(lost);
        img.color = lost ? new Color(0.25f, 0.25f, 0.3f, 1f) : Color.white;
        if (txt != lastText)
        {
            lastText = txt;
            talkUntil = lost || txt == "" ? 0f : Time.unscaledTime + Mathf.Clamp(1.0f + txt.Length * 0.045f, 1.5f, 6f);
        }
        Talking = forced ? forcedOn : (autoTalk && Time.unscaledTime < talkUntil);
        Animate(Talking, lost);
    }

    void Animate(bool talking, bool lost)
    {
        float now = Time.unscaledTime;
        if (face != null)
        {
            // 口：1秒に 6 回ほど開け閉め。ときどき「お」「い」を混ぜる（口のキーは足し合わせない）
            float open = 0f, o = 0f, wide = 0f;
            if (talking)
            {
                float syl = Mathf.Abs(Mathf.Sin(now * 19f)) * (0.55f + 0.45f * Mathf.PerlinNoise(now * 3f, 0.3f));
                int kind = Mathf.FloorToInt(now * 3.1f) % 5;
                if (kind == 3) o = syl * 90f; else if (kind == 4) wide = syl * 70f; else open = syl * 95f;
            }
            Set(iOpen, open); Set(iO, o); Set(iWide, wide);
            Set(iSmile, talking || lost ? 0f : 60f);
            Set(iWorried, lost ? 80f : 0f);
            // まばたき：3〜5 秒に1回、0.1 秒
            if (now > nextBlink) { blinkUntil = now + 0.1f; nextBlink = now + Random.Range(3f, 5f); }
            float blink = now < blinkUntil ? 100f : 0f;
            Set(iBlink, blink);
            if (crowdFaces != null)
                for (int i = 0; i < crowdFaces.Length; i++)
                    if (crowdFaces[i] != null && crowdFaces[i].sharedMesh != null)
                    {
                        int b = crowdFaces[i].sharedMesh.GetBlendShapeIndex("Blink");
                        if (b >= 0) crowdFaces[i].SetBlendShapeWeight(b, Mathf.Repeat(now + i * 0.77f, 4.1f) < 0.1f ? 100f : 0f);
                    }
        }
        if (head != null)
        {
            // 話している間は少しうなずく
            float nod = talking ? Mathf.Sin(now * 4.3f) * 4f : 0f, yaw = Mathf.Sin(now * 0.7f) * (talking ? 6f : 3f);
            head.localRotation = headRest * Quaternion.Euler(nod, yaw, 0f);
        }
    }

    void Set(int i, float w) { if (i >= 0) face.SetBlendShapeWeight(i, w); }
}
