// 書いた人：Claude ／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で CabinHud・GameFlow から分けた）
// 作る Issue：#79（同時押しの合図と点火・再突入の準備）
//
// 中央：同時押しの結果（press イベント：waiting／together／failed）を文字で出し、少し後に消す。見た目は仮
// 点火のボタン自体は席のパネル（#80・#82）。ボタンは Ctx.SendDual("ignite", seat) を呼ぶだけで、合図の表示はここ
using UnityEngine;
using UnityEngine.UI;

public class BurnCheck : HudPart
{
    const float ShowSeconds = 2.5f;   // 結果の文字を出しておく秒（見た目の値。前は GameFlow.ClearPressLater）
    Text pressText;
    float clearAt;

    protected override void Build(RectTransform root)
    {
        pressText = Ui.Label(root, "", 30, Ui.Orange, TextAnchor.MiddleCenter, FontStyle.Bold); Ui.Fill(pressText.rectTransform);
        var ol = pressText.gameObject.AddComponent<Outline>();   // 船室の明るい壁の上でも読めるように縁取り
        ol.effectColor = new Color(0.03f, 0.05f, 0.12f, 0.95f); ol.effectDistance = new Vector2(2f, -2f);
    }

    public override void OnEvent(GameEvent e)
    {
        if (e.kind == "press") Show(e);
    }

    public void Show(GameEvent e)
    {
        if (e.status == "waiting") pressText.text = e.cmd.ToUpper() + ": WAITING FOR PARTNER...";
        else if (e.status == "together") pressText.text = e.cmd.ToUpper() + ": TOGETHER!";
        else if (e.status == "failed") pressText.text = e.cmd.ToUpper() + ": MISSED. TRY AGAIN";
        clearAt = Time.unscaledTime + ShowSeconds;
    }

    public void Clear() { pressText.text = ""; }

    public override void Tick(float now)
    {
        if (clearAt > 0 && now > clearAt) { clearAt = 0; Clear(); }
    }
}
