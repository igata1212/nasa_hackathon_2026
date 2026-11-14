// 書いた人：Claude ／ 最終更新：2026-11-14
// ヒューストンの台詞の窓（字幕）。通信途絶中は NO SIGNAL、通信 OFF のときは COMMS OFF
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HoustonBox
{
    public readonly GameObject Root;
    readonly Text who, line;
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
        Root.SetActive(false);
    }

    public void Push(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        queue.Enqueue(text);
        while (queue.Count > 2) queue.Dequeue();   // 早送りで溜まったら古いものを捨てる
    }

    public void Clear() { queue.Clear(); current = ""; until = 0; noSignal = commsOff = false; Draw(); }

    public void SetLink(bool signalLost, bool commsSwitchOff) { noSignal = signalLost; commsOff = commsSwitchOff; Draw(); }

    public void Tick(float now)
    {
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
