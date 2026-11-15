// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-15、#168 3D の設計の画面。#190 のあつ森の作り（数のチップ・タブの帯・材料の行・選択肢の吹き出し・手）に合わせた）
// 設計の画面（#73・#168）だけで使う 2D の大きさと色。px は Canvas の基準 1600×900。3D の舞台の値は DesignLook。
// 見た目の決まりは docs/design/ui-guidelines.md（#190。【NN】は 0章の基準の画像）。
// ★ 差し替える所：下の「#190 の仮の置き場」（色・形）と DesignShapes（sprite）。#190 が UiTokens・UiTheme に同じ物を入れたら、
//   ここを UiTokens・UiTheme を指すように替える（ほかのファイルは触らなくてよい）
using UnityEngine;
using T = UiTokens;

public static class DesignTokens
{
    // ------------------------------------------------------------------ #190 の仮の置き場（tmp/handoff/190-plan.md 2章の値。UiTokens に入るまで）
    // 色（ui-guidelines 2章）。色コードはここだけ。UiTokens に入ったら T.Text などに替える
    public static readonly Color Text = T.Hex("5C4A3A");        // 明るい面の文字とアイコン（茶）、数のチップの面
    public static readonly Color TextSub = T.Hex("705C49");     // 補助・値段
    public static readonly Color BgDeep = T.Hex("E7D4AE");      // タブの帯・淡い丸・点線
    public static readonly Color PanelBand = T.Hex("F4EDDC");   // 材料の行の帯・パネルの横縞
    public static readonly Color Choice = T.Hex("FBEFC0");      // 選択肢の吹き出しの面
    public static readonly Color Plum = T.Hex("8A6BBE");        // 台とカードの色・見出しの帯
    public static Color Mark => T.WithAlpha(T.Accent, 0.45f);   // 選んだ選択肢のマーカー・数の色の箱
    public static Color Rim => Color.Lerp(T.Panel, Color.white, 0.55f);   // パネルの明るい内ふち（3D の空の上に置くので不透明にした）
    // 形（px）
    public const float RimPanel = 12, RimButton = 5;
    public const float RowStepH = 96, RowStepMinH = 80, RowRadius = 12, NumBoxRadius = 8;   // 材料の行（− ＋ つき。画面が低いと 80 まで縮む。押せる範囲は 96 のまま）
    public const float DashLen = 8, DashGap = 8, DashThick = 3;
    public const float CursorSize = 72, CheckD = 40;
    public const float ChoiceRowH = 80, ChoiceRowMinH = 64, ChoicePad = 32;
    public const float CounterH = 88, CounterIconD = 48;
    public const float TabBumpD = 112, TabOverlap = 16, TabLabelH = 40, TabIcon = 56, TabOnD = 80;
    public const float RibbonH = 80;
    public const float TextChoice = 32, TextRow = 30, TextRibbon = 36, TextPrice = 24, TextTag = 22, TextCheck = 28;
    // 動き（仮の値。静止画に基準が無い。iizuka が見て決める）
    public const float HandBobPx = 6f, HandBobSec = 0.6f, HandMoveSec = 0.12f;

    // ------------------------------------------------------------------ 画面の割り付け（全面が 3D の舞台。右に列、その左上に打ち上げチェック）
    public const float RightW = 576;          // 右の列（数のチップ → タブの帯 → 棚（材料の行）→ Ready for launch!）
    public const float ColGap = T.S2;         // 打ち上げチェックと右の列の間
    public const float TrayTop = 24;          // 右の列の上の余白（数のチップの上にかぶせるアイコンの台のぶん）

    // ------------------------------------------------------------------ 数のチップ（右上。お財布＝残りの予算、重さ＝残りの重さ）【09】
    public const float CounterNumberW = 72;   // 数字の箱（Baloo 40。3桁 約 66 が入る幅。右寄せで桁ぶれを防ぐ）
    public const float CounterUnitH = 34;     // 右下の単位の札（left／over）
    public const float CounterUnitTilt = -4f;
    public const float PillCountSec = 0.3f;   // 数字が変わるとき数え上げ／下げる時間
    public const float PillShakePx = 8f, PillShakeSec = 0.45f, PillShakeHz = 7f;   // 超えたとき左右に揺れる
    public const float DeltaChipH = 36;       // チップの上に浮かぶ −10／+10（右上から浮く。左上のアイコンの台と重ならない）
    public const float DeltaRise = 16f, DeltaSec = 0.6f;

    // ------------------------------------------------------------------ 棚（材料の行）
    public const float ShelfPad = 12;         // 内ふちの内側の余白
    public const float RowGap = T.S1;
    public const float RowPadX = 12;
    public const float RowIconD = 48;         // 行の左のアイコンの台
    public const float RowNameH = 38;         // 名前（30）
    public const float RowPriceH = 30;        // 名前の下の値段（24）
    public const float StepD = 56;            // − ＋ の丸（見た目）。押せる範囲は T.Hit（96）で、レイアウトの幅の外へはみ出してよい
    public const float StepSlotW = 64;        // − ＋ のレイアウトの幅
    public const float StepGlyphLen = 24, StepGlyphThick = 6;   // ＋と−の棒（書体に「−」が無いので棒を描く）
    public const float CountBoxW = 44;        // 数の色の箱
    public const float MaxW = 40;             // 「/ 4」

    // ------------------------------------------------------------------ 打ち上げチェック（右の列のすぐ左・上）
    public const float CheckW = 280;
    public const float CheckRowH = 48;
    public const float CheckTitleIcon = 40;

    // ------------------------------------------------------------------ 打ち上げ判定の結果（No go のカード）
    public const float OverlayW = 760;
    public const float OverlayIconD = 112;

    // ------------------------------------------------------------------ 台の色（隣どうしを同じ色にしない。状態の色 accent・danger は使わない）
    public static Color[] PlatformColors => new[] { T.Go, Plum, T.Ink, Text };
}

/// <summary>#190 の sprite の差し替え口。art/ui/make_sprites.py の blob_*・bands64・glove_point が UiTheme に入ったら、ここだけ替える</summary>
public static class DesignShapes
{
    public static Sprite Pill => T.Theme.pill;                 // → blob_pill（名前の札・数のチップ・単位の札）
    public static Sprite Disc => T.Theme.pill;                 // → blob_disc（アイコンの台・タブの帯のこぶ・チェックの丸）
    public static Sprite Panel => T.Round(T.RadiusL);          // → blob_panel（パネル・選択肢の吹き出し）
    public const float PanelRadius = T.RadiusL;
    public static Sprite Glove => null;                        // → glove_point（無い間は DesignHand が丸とカプセルで組む）

    static Texture2D bands;
    /// <summary>→ bands64（4×128、上 64 白・下 64 透明。縦にくり返す）。sprite ができるまで実行時に作る</summary>
    public static Texture2D Bands
    {
        get
        {
            if (bands != null) return bands;
            bands = new Texture2D(4, 128, TextureFormat.RGBA32, false) { name = "bands64_runtime", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            var px = new Color32[4 * 128];
            for (int y = 0; y < 128; y++) for (int x = 0; x < 4; x++) px[y * 4 + x] = y >= 64 ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            bands.SetPixels32(px); bands.Apply(false, true);
            return bands;
        }
    }
}
