// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// 案内の吹き出し（#153。タイトルと部屋で、ヒューストンが「次に何をするか」を1つだけ話す）。
// panel の角丸 40・影 6、左上に傾いた名前タグ（go に白文字、comms のアイコン）、しっぽは下か左（指す物の方）。
// 文は Say() で替える。替わるたびに小さく弾み、1文字ずつ出る（TitleLook.TypeCps）。開いたときは delay 秒あとにぽんと出る。
// #71 の共通の吹き出し（UiKitHud の Bubble）ができたら、作る所（Create）をそれに替える。外から使うのは Create と Say だけ
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;
using L = TitleLook;

public class UiHintBubble : MonoBehaviour
{
    public enum Tail { Down, Left }

    RectTransform body;
    TextMeshProUGUI text;
    string current = "";
    float delay, shown, popT, popFrom;
    bool first;

    /// <param name="w">幅</param><param name="h">高さ</param>
    /// <param name="tailAt">しっぽの位置（Down：左からの px。Left：使わない＝縦の中央）</param>
    /// <param name="delay">画面が開いてから出るまでの秒数</param>
    public static UiHintBubble Create(Transform p, float w, float h, Tail tail, float tailAt, float delay)
    {
        var root = Ui.Rect("HintBubble", p);
        UiKit.Fixed(root, w, h);
        root.sizeDelta = new Vector2(w, h);
        var b = root.gameObject.AddComponent<UiHintBubble>();
        b.delay = delay;

        // 弾むときの中心はしっぽの先（指す物から出てくるように見える）
        b.body = Ui.Rect("Body", root); UiKit.Fill(b.body);
        Vector2 tip = tail == Tail.Down ? new Vector2(tailAt, 0f) : new Vector2(0f, h * 0.5f);
        b.body.pivot = new Vector2(tip.x / w, tip.y / h);
        b.body.anchoredPosition = Vector2.zero;
        b.body.offsetMin = Vector2.zero; b.body.offsetMax = Vector2.zero;

        // 影（しっぽ・面）→ しっぽ → 面 の順に重ねる。しっぽの半分は面の下に隠れて三角に見える
        var tailShadow = TailShape(b.body, tip, T.Shadow, "TailShadow");
        tailShadow.anchoredPosition += Vector2.down * T.ShadowY;
        UiKit.Surface(b.body, T.RadiusL, T.Panel);
        var tailFace = TailShape(b.body, tip, T.Panel, "Tail");
        tailFace.SetSiblingIndex(tailShadow.GetSiblingIndex() + 2);   // 影の面の上、面の下

        b.text = UiKit.Text(b.body, "", TextStyle.Body, T.Ink, TextAlignmentOptions.Left, false);
        b.text.textWrappingMode = TextWrappingModes.Normal;
        b.text.richText = true;
        b.text.lineSpacing = 0f;
        var tr = b.text.rectTransform; UiKit.Fill(tr);
        tr.offsetMin = new Vector2(L.BubblePadX, L.BubblePadY); tr.offsetMax = new Vector2(-L.BubblePadX, -L.BubblePadY);

        SpeakerTag(b.body, h);
        b.Hide();
        return b;
    }

    static RectTransform TailShape(RectTransform parent, Vector2 tip, Color c, string name)
    {
        var img = UiKit.Round(parent, L.BubbleTailRadius, c, name);
        var r = img.rectTransform;
        r.anchorMin = r.anchorMax = Vector2.zero; r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(L.BubbleTail, L.BubbleTail);
        r.anchoredPosition = tip;
        r.localRotation = Quaternion.Euler(0f, 0f, 45f);
        return r;
    }

    static void SpeakerTag(RectTransform parent, float h)
    {
        var tag = UiKit.Row(parent, T.S1, TextAnchor.MiddleCenter, T.S2);
        tag.name = "Speaker";
        tag.anchorMin = tag.anchorMax = new Vector2(0f, 1f);
        tag.pivot = new Vector2(0f, 0.5f);                   // 名前タグの中心の線を、吹き出しの上の縁に合わせる（半分かぶせる）
        tag.anchoredPosition = new Vector2(L.SpeakerInsetX, 0f);
        tag.localRotation = Quaternion.Euler(0f, 0f, L.SpeakerTilt);
        var fit = tag.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var face = UiKit.Pill(tag, T.Go, T.TagH, "Face"); UiKit.Fill(face.rectTransform); UiKit.Ignore(face);
        UiKit.Icon(tag, L.SpeakerIcon, T.IconS, T.Panel);
        UiKit.Text(tag, L.Speaker, TextStyle.Tag, T.Panel);
        var le = Ui.Size(tag, h: T.TagH); le.minHeight = T.TagH;
    }

    /// <summary>話す文（リッチテキスト可。TitleLook.Rich を通した物）。同じ文なら何もしない</summary>
    public void Say(string rich)
    {
        if (rich == current) return;
        current = rich;
        text.text = rich;
        shown = 0f;
        text.maxVisibleCharacters = 0;
        if (!first) { popT = 0f; popFrom = L.BubbleRePopFrom; }
    }

    void Hide()
    {
        first = true; popT = -delay; popFrom = L.BubblePopFrom; shown = 0f;
        body.localScale = Vector3.zero;
        if (text != null) text.maxVisibleCharacters = 0;
    }

    void OnEnable() { if (body != null) Hide(); }

    void Update()
    {
        float dt = UiEase.Dt;
        popT += dt;
        if (popT < 0f) { body.localScale = Vector3.zero; return; }
        float p = popT / L.BubblePopDur;
        if (p >= 1f) first = false;
        float s = Mathf.LerpUnclamped(popFrom, 1f, UiEase.OutBack(p));
        body.localScale = new Vector3(s, s, 1f);
        if (shown <= current.Length)
        {
            shown += dt * L.TypeCps;
            text.maxVisibleCharacters = Mathf.FloorToInt(shown);
        }
    }
}
