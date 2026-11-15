// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-15、#190 あつ森の会話の吹き出し【12】【02】）
// 案内の吹き出し（#153。タイトルと部屋で、ヒューストンが「次に何をするか」を1つだけ話す）。
// #190：blob_bubble（下の辺が波打つ形）、影・しっぽなし（【12】【02】）。左上に名前タグ（Houston の blob_pill、右上がりに 6°、高さの 4 割をかぶせる）。
//   文は台詞の大きさ 36・行間 1.5・茶。吹き出しの大きさは呼ぶ側（TitleLook）が決めるので、入らないときは 22 まで自動で縮める。
// 文は Say() で替える。替わるたびに小さく弾み、1文字ずつ出る（TitleLook.TypeCps）。開いたときは delay 秒あとにぽんと出る。
// 外から使うのは Create と Say だけ（Tail は前の呼び方のまま受ける。しっぽは描かず、弾むときの中心にだけ使う）
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;
using L = TitleLook;
using S = HoustonStyle;

public class UiHintBubble : MonoBehaviour
{
    public enum Tail { Down, Left }

    RectTransform body;
    TextMeshProUGUI text;
    string current = "";
    float delay, shown, popT, popFrom;
    bool first;

    /// <param name="w">幅</param><param name="h">高さ</param>
    /// <param name="tailAt">指す物の位置（Down：左からの px。Left：使わない＝縦の中央）。弾むときの中心にする</param>
    /// <param name="delay">画面が開いてから出るまでの秒数</param>
    public static UiHintBubble Create(Transform p, float w, float h, Tail tail, float tailAt, float delay)
    {
        var root = Ui.Rect("HintBubble", p);
        UiKit.Fixed(root, w, h);
        root.sizeDelta = new Vector2(w, h);
        var b = root.gameObject.AddComponent<UiHintBubble>();
        b.delay = delay;

        // 弾むときの中心は指す物の側（指す物から出てくるように見える）
        b.body = Ui.Rect("Body", root); UiKit.Fill(b.body);
        Vector2 tip = tail == Tail.Down ? new Vector2(tailAt, 0f) : new Vector2(0f, h * 0.5f);
        b.body.pivot = new Vector2(tip.x / w, tip.y / h);
        b.body.anchoredPosition = Vector2.zero;
        b.body.offsetMin = Vector2.zero; b.body.offsetMax = Vector2.zero;

        // 面：blob_bubble（影・しっぽなし）。上下の縁（96＋96）が高さに入るように縮める
        var face = UiKit.Blob(b.body, T.BlobBubble, T.Panel, T.BubbleCornerX, "Face"); UiKit.Fill(face.rectTransform);
        var s = face.sprite;
        if (s != null && face.type == Image.Type.Sliced && s.border.y + s.border.w > 0f)
            face.pixelsPerUnitMultiplier = 1f / Mathf.Min(1f, h * S.BubbleFit / (s.border.y + s.border.w));

        // 文：台詞 36・行間 1.5・茶。内側は吹き出しの大きさに合わせて縮める（大きい吹き出しでは 72／36）。上は名前タグの下まで空ける
        b.text = UiKit.Text(b.body, "", TextStyle.Talk, T.Text, TextAlignmentOptions.MidlineLeft, false);
        b.text.textWrappingMode = TextWrappingModes.Normal;
        b.text.richText = true;
        var fi = b.text.font.faceInfo;
        b.text.lineSpacing = Mathf.Max(0f, (T.TalkLineHeight - fi.lineHeight / fi.pointSize) * 100f);
        b.text.enableAutoSizing = true; b.text.fontSizeMin = T.TextLabel; b.text.fontSizeMax = T.TextTalk;
        b.text.overflowMode = TextOverflowModes.Ellipsis;
        float padX = Mathf.Min(T.BubblePadX, w * 0.06f), padY = Mathf.Min(T.BubblePadY, h * 0.15f);
        float padTop = Mathf.Max(padY, T.NameTagH * S.TagOverlap + T.S1);   // 名前タグのかぶる分の下から
        var tr = b.text.rectTransform; UiKit.Fill(tr);
        tr.offsetMin = new Vector2(padX, padY); tr.offsetMax = new Vector2(-padX, -padTop);

        SpeakerTag(b.body);
        b.Hide();
        return b;
    }

    /// <summary>左上の名前タグ（HoustonBubble と同じ作り：blob_pill、右上がり 6°、高さの 4 割をかぶせる【12】）</summary>
    static void SpeakerTag(RectTransform parent)
    {
        var tag = UiKit.Row(parent, T.S1, TextAnchor.MiddleCenter, S.TagPadX);
        tag.name = "Speaker";
        tag.anchorMin = tag.anchorMax = new Vector2(0f, 1f);
        tag.pivot = new Vector2(0f, 0.5f);
        tag.anchoredPosition = new Vector2(L.SpeakerInsetX, T.NameTagH * (0.5f - S.TagOverlap));
        tag.localRotation = Quaternion.Euler(0f, 0f, S.TagTilt);
        var fit = tag.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var face = UiKit.Blob(tag, T.BlobPill, S.TagColor(""), T.NameTagH * 0.5f, "Face"); UiKit.Fill(face.rectTransform); UiKit.Ignore(face);
        UiKit.Icon(tag, L.SpeakerIcon, T.IconS, T.Panel);
        var name = UiKit.Text(tag, S.Name, TextStyle.Hint, T.Panel);
        name.font = T.FontStrong;
        var le = Ui.Size(tag, h: T.NameTagH); le.minHeight = T.NameTagH;
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
