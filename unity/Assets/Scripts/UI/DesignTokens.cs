// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// 設計の画面（#73）だけで使う大きさと色。色は UiTokens の色から作る（色コードを書かない）。px は Canvas の基準 1600×900。
// 並びや大きさを直すときはここだけ触る。ほかの画面でも使うなら UiTokens に移す（#71 が UiTokens を触っている間は、ぶつからないようここに置く）
using UnityEngine;
using T = UiTokens;

public static class DesignTokens
{
    // ------------------------------------------------------------------ 画面の割り付け
    public const float RightW = 464;          // 右の列（重さ・費用・ロケット・軌道・打ち上げ）の幅。主ボタン READY FOR LAUNCH が入る幅
    public const float ColGap = T.S3;         // 左右の列の間
    public const float HoustonReserve = 0.19f; // 左下はヒューストンの吹き出し（HoustonBox.Dock(true) は画面の下 17%）。安全領域の高さに対する割合で空ける
    public const float TitleRowH = 84;        // 見出しの行（Baloo 72 の詰めた高さ＋α）
    public const float GoalChipH = T.TagH;    // 4つの要件の札

    // ------------------------------------------------------------------ 部品のカード（2列×4段）
    public const int PartColumns = 2;
    public const float PartRowH = 110;        // カード1枚の高さ（名前 40＋説明 31＋数字 31＋上下 4）
    public const float PartGapX = T.S2, PartGapY = T.S1;
    public const float PartPadX = 12, PartPadY = 4;
    public const float PartNameH = 40;        // 名前の行（アイコン 40 と同じ）
    public const float PartLineH = 31;        // 説明と数字の行（Nunito 22 が入る高さ）
    public const float StatGap = T.S2;        // 「質量 12  費用 10」の間

    // ± のボタン（見た目は円 72、押せる範囲は 96）
    public const float StepD = 72;
    public const float StepGlyphLen = 28, StepGlyphThick = 7;   // ＋と−の棒（書体に「−」が無いので棒を描く）
    public const float CountW = 48;           // ± の間の個数

    // ------------------------------------------------------------------ ゲージ（重さ・費用）
    public const float GaugeH = 28;
    public const float GaugeHeadroom = 1.25f; // ゲージの右端＝上限×これ。上限の線は 1/1.25＝80% の所。上限を超えた分も見える
    public const float LimitLineW = 4, LimitLineOver = 6;       // 上限の線（UI ガイドライン 3章のゲージの「生還ライン」と同じ形）
    public const float StripeScale = 2f;      // 斜め縞のタイルの細かさ（pixelsPerUnitMultiplier）

    // ------------------------------------------------------------------ 右の列
    public const float ChoiceH = 96;          // ロケット・軌道の選択（押せる範囲＝見た目）
    public const float SupplyChipH = 48;
    public const float VerdictMinH = 112;     // 判定の箱（見出し 37＋2行 58＋上下 16）
    public const float SectionGap = T.S2;

    // ------------------------------------------------------------------ 打ち上げ判定の結果（NO GO のカード）
    public const float OverlayW = 760;
    public const float OverlayIconD = 112;

    // ------------------------------------------------------------------ 色
    public static Color CardOff => T.Panel;                     // 積んでいない部品のカード
    public static Color CardOnFrame => T.Go;                    // 積んでいる部品の枠
    public static Color OverZone => T.WithAlpha(T.Danger, 0.16f);   // ゲージの上限より右（ここに入るとだめ）
    public static Color StripeTint => T.WithAlpha(T.Panel, 0.4f);   // 危険の面に重ねる斜め縞（Sprites/README.md：白の 40%）
    public static Color VerdictOkFace => T.WithAlpha(T.Go, 0.12f);
    public static Color VerdictBadFace => T.WithAlpha(T.Danger, 0.12f);
}
