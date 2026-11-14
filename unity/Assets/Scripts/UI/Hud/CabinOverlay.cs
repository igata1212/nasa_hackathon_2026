// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// 作る Issue：#87（照明 OFF の見え方）・#88（操作が遅くなる表示）
//
// 画面全体に重ねる物：照明 OFF の暗幕（#87）、寒さ・CO2 の霜・もや・SLOW の札（#88、state の slow_pct）。今は空（#147 で場所だけ作った）
// 置き場所は HUD のいちばん後ろ（3D の上、ほかの部品の下）。押す所の邪魔をしないように raycastTarget は false にする
// ほかの部品を暗くするときは Ctx.Parts の Detail（CanvasGroup）の alpha を変える（各部品のファイルは触らない）
using UnityEngine;

public class CabinOverlay : HudPart
{
    protected override void Build(RectTransform root) { }
}
