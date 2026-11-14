// 書いた人：Claude ／ 最終更新：2026-11-14
// ヒューストンの台詞の窓（字幕）。通信途絶中は NO SIGNAL、通信 OFF のときは COMMS OFF
// 声なしの警告（event kind=warning。通信できない間にヒューストンが言うはずだった台詞）は、窓の上の赤い帯に文字だけ出す
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HoustonBox
{
    public readonly GameObject Root;
    readonly Text who, line, warn;
    readonly GameObject warnRoot;
    float warnUntil;
    readonly Image bg;
    readonly Queue<string> queue = new Queue<string>();
    float until;
    string current = "";
    bool noSignal, commsOff;

    public HoustonBox(Transform parent)
    {
        bg = Ui.PanelImg(parent, new Color(0.05f, 0.08f, 0.16f, 0.9f), "Houston"); Root = bg.gameObject;
        Ui.Anchor(bg.rectTransform, 0.22f, 0.22f, 0.78f, 0.34f);
        who = Ui.Label(bg.transform, "HOUSTON", 20, Ui.Orange, TextAnchor.UpperLeft, FontStyle.Bold);
        Ui.Anchor(who.rectTransform, 0, 0.75f, 1, 1, 16, 0, 8, 4);
        line = Ui.Label(bg.transform, "", 26, Ui.Cream, TextAnchor.UpperLeft);
        Ui.Anchor(line.rectTransform, 0, 0, 1, 0.78f, 16, 4, 16, 0);
        line.verticalOverflow = VerticalWrapMode.Truncate;
        line.resizeTextForBestFit = true; line.resizeTextMinSize = 16; line.resizeTextMaxSize = 26;   // 長い台詞（M01 など）が切れないように縮める
        var wbg = Ui.PanelImg(parent, new Color(0.45f, 0.06f, 0.06f, 0.92f), "HoustonWarning"); warnRoot = wbg.gameObject;
        Ui.Anchor(wbg.rectTransform, 0.22f, 0.345f, 0.78f, 0.41f);
        warn = Ui.Label(wbg.transform, "", 22, Ui.Cream, TextAnchor.MiddleLeft, FontStyle.Bold);
        Ui.Anchor(warn.rectTransform, 0, 0, 1, 1, 16, 2, 16, 2);
        warn.verticalOverflow = VerticalWrapMode.Truncate;
        warnRoot.SetActive(false);
        Root.SetActive(false);
    }

    /// <summary>置き場所。設計の画面では部品の一覧に重ならないよう左下に寄せる</summary>
    public void Dock(bool designScreen)
    {
        if (designScreen) Ui.Anchor(bg.rectTransform, 0.03f, 0.02f, 0.6f, 0.17f);
        else Ui.Anchor(bg.rectTransform, 0.22f, 0.22f, 0.78f, 0.34f);
    }

    /// <summary>声なしの警告（パネルの文字だけ）。数秒で消える</summary>
    public void Warn(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        warn.text = "PANEL WARNING  " + text;
        warnUntil = Time.unscaledTime + 6f;
        warnRoot.SetActive(true);
        warnRoot.transform.SetAsLastSibling();
    }

    public void Push(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        queue.Enqueue(text);
        while (queue.Count > 2) queue.Dequeue();   // 早送りで溜まったら古いものを捨てる
    }

    public void Clear() { queue.Clear(); current = ""; until = 0; noSignal = commsOff = false; warnUntil = 0; warnRoot.SetActive(false); Draw(); }

    public void SetLink(bool signalLost, bool commsSwitchOff) { noSignal = signalLost; commsOff = commsSwitchOff; Draw(); }

    public void Tick(float now)
    {
        if (warnRoot.activeSelf && now >= warnUntil) warnRoot.SetActive(false);
        if (current != "" && now >= until) current = "";
        if (current == "" && queue.Count > 0)
        {
            current = queue.Dequeue();
            until = now + 4f + current.Length * 0.05f;   // 読む時間（文字数に比例）
        }
        Draw();
    }

    void Draw()
    {
        if (noSignal) { who.text = "HOUSTON"; line.text = "NO SIGNAL"; line.color = Ui.Red; Root.SetActive(true); return; }
        if (commsOff) { who.text = "HOUSTON"; line.text = "COMMS OFF"; line.color = Ui.Red; Root.SetActive(true); return; }
        line.color = Ui.Cream;
        line.text = current;
        Root.SetActive(current != "");
    }
}
