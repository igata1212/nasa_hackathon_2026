// 書いた人：iizuka（作成：Claude）／ 最終更新：2026-11-14
// UI のデザイントークン（docs/design/ui-guidelines.md の 2章）。色コードと px はここにだけ書く。
// px は Canvas の基準 1600×900（GameFlow の CanvasScaler）での値。部品は UiKit.cs、書体とスプライトは UiTheme（Resources/UiTheme.asset）
using TMPro;
using UnityEngine;

public static class UiTokens
{
    // ------------------------------------------------------------------ 色（modeling/core/palette.json の値。danger だけ UI 専用）
    public static readonly Color Bg = Hex("F2E3C6");       // cream：全画面の背景
    public static readonly Color Panel = Hex("FBF7EE");    // white：パネル・カード。色面の上の「白」もこれ
    public static readonly Color Ink = Hex("1D2D50");      // navy：文字・アイコン
    public static readonly Color InkSub = Hex("4B5263");   // gray_dark：補助ラベル
    public static readonly Color Line = Hex("C8CCD4");     // gray_light：区切り・枠・押せないボタン
    public static readonly Color Go = Hex("2A9D8F");       // teal：主ボタン・安全・完了
    public static readonly Color Accent = Hex("F28C28");   // orange：注意・合言葉
    public static readonly Color Danger = Hex("C81E4A");   // crimson：危険・失敗（UI 専用）
    public static readonly Color Shadow = WithAlpha(Ink, 0.18f);
    public static readonly Color Scrim = WithAlpha(Ink, 0.45f);        // 開発用パネルの後ろ
    public static readonly Color Selection = WithAlpha(Go, 0.3f);      // 入力欄の選択範囲
    public static readonly Color Clear = new Color(0f, 0f, 0f, 0f);    // 押せる範囲だけの透明な面

    // 席の色（席バッジの面だけ。文字色には使わない）
    public static readonly Color SeatRed = Hex("E4572E"), SeatGreen = Hex("6BBF59"), SeatYellow = Hex("FFC857"), SeatBlue = Hex("3A86FF");

    // ボタンを押したときの色（Button の ColorTint。面の色に掛ける）
    public static readonly Color TintHover = new Color(0.96f, 0.96f, 0.96f, 1f);
    public static readonly Color TintPressed = new Color(0.9f, 0.9f, 0.9f, 1f);
    public const float TintFade = 0.08f;

    // ------------------------------------------------------------------ 形
    public const float RadiusS = 16;      // チップ・入力欄
    public const float RadiusM = 28;      // カード
    public const float RadiusL = 40;      // パネル・吹き出し
    public const float ShadowY = 6;       // 影は下に 6 ずらす（ぼかさない）
    public const float ShadowPressedY = 2; // 押したら 2 に縮む
    public const float Border = 3;        // 入力欄・副ボタンの枠
    public const float SelfRing = 4;      // 自分の席の輪
    public const float DashLen = 10, DashGap = 10;  // 点線（太さは Border）

    // ------------------------------------------------------------------ 余白（8 の倍数）
    public const float S1 = 8, S2 = 16, S3 = 24, S4 = 32;
    public const float PanelPad = 24;     // パネルの内側
    public const float ScreenEdge = 32;   // 画面の端から

    // ------------------------------------------------------------------ 文字
    public const float TextTitle = 112;   // タイトル（Baloo 2）
    public const float TextHeading = 72;  // 見出し（Baloo 2）
    public const float TextSub = 32;      // 小見出し・ボタン（Baloo 2）
    public const float TextNumber = 40;   // 数字（Baloo 2）
    public const float TextBig = 56;      // 大きい数字（カウントダウン・合言葉。Baloo 2）
    public const float TextTag = 24;      // 名前タグ・席バッジの文字（Baloo 2）
    public const float TextBody = 28;     // 本文（Nunito Bold）
    public const float TextLabel = 22;    // 補助ラベル（Nunito Bold）。これより小さくしない
    public const float HeadingLineHeight = 1.15f;  // Baloo 2 は行の高さが大きいので、並べるときの高さを字の大きさ×これにする
    public const float BodyLineHeight = 1.3f;

    // ------------------------------------------------------------------ アイコン
    public const float IconS = 24, IconM = 40, IconL = 64, IconInButton = 48;

    // ------------------------------------------------------------------ 部品の大きさ
    public const float Hit = 96;          // 押せる所の最小（見た目が小さい部品も、押せる範囲はこれ）
    public const float PrimaryH = 96, PrimaryMinW = 320;
    public const float SecondaryH = 72;
    public const float InputH = 72;
    public const float IconButtonD = 96;
    public const float BadgeS = 56, BadgeL = 96;
    public const float BadgeShapeScale = 0.5f;   // 席バッジの円に対する形（▲●■◆）の大きさ
    public const float TagH = 44;               // 名前タグ（YOU など）
    public const float ChipH = 96;              // 合言葉のチップ

    // ------------------------------------------------------------------ 画面ごとの寸法（#72）
    public const float TitleFormW = 880;   // タイトルの入力パネルの幅
    public const float DevPanelW = 720;    // 開発用パネルの幅
    public const int NameMaxChars = 12, RoomCodeChars = 4;

    // ------------------------------------------------------------------ 部屋の画面の 3D（LobbyStage。m・度・秒）
    public static readonly Vector3 StageOrigin = new Vector3(0f, 3000f, 0f);   // 船室・軌道の地図から遠く
    public static readonly Color StageBg = Ink;                               // 背景（宇宙）
    public const float StageSpacing = 1.45f;      // クルーの間隔（横長の 844×390 でも頭の上のタグがぶつからない広さ）
    public const float PadDiameter = 1.0f, PadHeight = 0.08f;
    public const float CrewLift = 0.06f;          // 台の上に浮かせる高さ（CrewFloat で揺れる）
    public const float CrewBob = 0.025f;          // 上下の揺れ幅
    public const float StageFov = 30f;
    public const float StageCamDistance = 6.6f, StageCamHeight = 1.35f, StageLookHeight = 0.72f;
    public const float StageAspect = 16f / 9f;    // これより縦長の画面では、4人が入るようにカメラを引く
    public const float TagAboveHead = 0.12f;      // 名前のタグを頭の上どれだけに置くか
    public const float PadGlowSelf = 2.4f, PadGlowOther = 0.6f, PadGlowOpen = 0.05f;   // 台の光（Emission の強さ）
    public const float WaveEvery = 7f, WaveTime = 2.2f, WaveRaise = 150f, WaveSwing = 22f, WaveHz = 2.2f;   // 手を振る
    public static readonly Vector3 EarthPos = new Vector3(7.5f, -5.5f, 16f);  // 後ろの地球（StageOrigin から）。右下に半分だけ見せる（大きく真後ろに置くとクルーが埋もれた）
    public const float EarthSize = 14f;
    public static readonly Vector3 KeyLightPos = new Vector3(0f, 3.4f, -3.7f); // 手前上からの明かり（StageOrigin から）
    public const float KeyLightIntensity = 30f, KeyLightAngle = 70f, KeyLightRange = 25f, StageFar = 60f;

    // ------------------------------------------------------------------ 書体とスプライト
    static UiTheme theme;
    public static UiTheme Theme
    {
        get
        {
            if (theme == null) theme = Resources.Load<UiTheme>("UiTheme");
            if (theme == null) Debug.LogError("[UiTokens] Resources/UiTheme.asset が無い。Tools/UI/素材を設定し直す を実行する");
            return theme;
        }
    }
    public static TMP_FontAsset FontHeading => Theme.heading;
    public static TMP_FontAsset FontBody => Theme.body;
    public static TMP_FontAsset FontShapes => Theme.shapes;
    public static Sprite Icon(string name) => Theme.Icon(name);

    /// <summary>角丸の半径に合うスプライト（小・中・大）</summary>
    public static Sprite Round(float radius) => radius <= RadiusS ? Theme.round16 : radius <= RadiusM ? Theme.round28 : Theme.round40;

    // ------------------------------------------------------------------ 席（色＋形。art-direction.md と同じ）
    public static Color SeatColor(string seat) =>
        seat == Seats.Pilot ? SeatRed : seat == Seats.Life ? SeatGreen : seat == Seats.Comms ? SeatYellow : SeatBlue;

    /// <summary>席の色の面に載せる形・文字の色。黄はクリーム系の白では読めないので ink</summary>
    public static Color OnSeat(string seat) => seat == Seats.Comms ? Ink : Panel;

    public static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
    public static Color WithAlpha(Color c, float a) { c.a = a; return c; }
}
