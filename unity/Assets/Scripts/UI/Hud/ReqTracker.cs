// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// 作る Issue：#90（要件トラッカー。一部未決定：全員に出すか Mission Planner だけか）
//
// Req 1〜4 のいまの状況。今は空（#147 で場所だけ作った：右の地図の下）。並びとアイコンは #71 の ReqRow（#73・#86 と同じ）
// Req 2 の見込みは state の science_now（#148 でサーバーが送る。GameState.scienceNow）。UI では計算しない
using UnityEngine;

public class ReqTracker : HudPart
{
    protected override void Build(RectTransform root) { }
}
