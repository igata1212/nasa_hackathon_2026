// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// 作る Issue：#76（事故の警報と危機の帯）
//
// 中央上の危機の帯（同時に1本、ui-guidelines 3章）。今は空（#147 で場所だけ作った。練習の案内と同じ場所で、同時には出ない）
// 判定は UrgencyFx の Level・Cause を使う（Object.FindAnyObjectByType<UrgencyFx>()。シーンに参照を足さない）
using UnityEngine;

public class CrisisBanner : HudPart
{
    protected override void Build(RectTransform root) { }
}
