// 書いた人：iizuka（作成：Claude）／ 最終更新：2026-11-15 ／ 更新：iizuka（作成：Claude Opus 5.5、#71 HUD の部品の寸法を足した。2026-11-14）／ 更新：iizuka（Opus 5.5、2026-11-15、#190 あつ森に寄せる色・形・文字・動き）
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
    public static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
    // #190 あつ森に寄せる（ui-guidelines 2章）。明るい面の文字は Text、Ink は暗い面（計器・部屋の宇宙）の色
    public static readonly Color Text = Hex("5C4A3A");      // 茶：明るい面の文字とアイコン、数のチップの面
    public static readonly Color TextSoft = Hex("705C49");  // 補助ラベル・値段（plan の TextSub。TextSub は文字の大きさ 32 の名前で使っているのでこの名前）
    public static readonly Color BgDeep = Hex("E7D4AE");    // 地の柄・淡い丸い枠・点線・副ボタンのふち
    public static readonly Color PanelBand = Hex("F4EDDC"); // パネルの横縞・材料の行の帯・入力欄の面
    public static readonly Color Choice = Hex("FBEFC0");    // 選択肢の吹き出しの面
    public static readonly Color GoLight = Hex("4DB8AA");   // 主ボタンの縞とふち
    public static readonly Color Plum = Hex("8A6BBE");      // 台・カード・見出しの帯の色違い
    public static readonly Color Mark = WithAlpha(Accent, 0.45f);   // 選んだ選択肢のマーカー・数の色の箱
    public static readonly Color Rim = WithAlpha(Color.white, 0.55f); // パネルの内ふち
    // 船内の計器（ゲージ・地図・タイムラインの帯）だけは暗い面（ui-guidelines 1章の6・2章。Fable 5.2、2026-11-14 のダーク/ライトの判断）。
    //   声（吹き出し）・操作（ボタン・カード）・警告の帯は明るいまま。タイトル・設計・レポートも明るいまま
    public static readonly Color HudFace = WithAlpha(Ink, 0.94f);      // 計器の面（色空間が Linear なので 0.94 でほぼ紺）
    public static readonly Color HudText = Panel;                      // 計器の上の文字・数字・生還ライン
    public static readonly Color HudSub = WithAlpha(Panel, 0.72f);     // 計器の上の補助
    public static readonly Color HudGroove = WithAlpha(Panel, 0.22f);  // 計器の上の溝
    public static readonly Color HudSeg = Hex("5E6B8C");               // タイムラインの帯の静かな区間（再突入）    // 押せる範囲だけの透明な面

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
    public const float DashLen = 8, DashGap = 8;    // 点線（太さは Border。#190 で 10 → 8）
    // #190 形（ui-guidelines 2章「形・余白・文字」）
    public const float RimPanel = 12, RimButton = 5;           // 内ふち（パネル・ボタン）
    public const float CardRadius = 16, CardLine = 2, CardLineInset = 6;   // カード（角丸・内側の線の太さ・内側へ）
    public const float RowH = 64, RowStepH = 96, RowRadius = 12;  // 材料の行（− ＋ が付く行は 96）
    public const float RowDiscD = 48;                           // 行の左の台
    public const float NumBoxRadius = 8;                        // 数の色の箱
    public const float SlotD = 96, SlotGap = 24;               // 丸い枠
    public const float CursorSize = 72;                         // 指さしの手
    public const float NameTagH = 48, NameTagTilt = 6;          // 吹き出しの名前タグ（度、反時計回り）
    public const float ItemTagH = 64, ItemTagTilt = 3;          // 品名の札
    public const float CheckD = 40;                             // チェックの丸
    public const float ChoiceRowH = 80, ChoicePad = 32;         // 選択肢の吹き出し
    public const float MarkPadX = 16;                           // マーカーの左右のはみ出し
    public const float HintH = 56, HintDiscD = 40;              // ヒントのピル（左の小さな丸）
    public const float CounterH = 88, CounterIconD = 48, CounterUnitTilt = 4;   // 数のチップ
    public const float TabBumpD = 112, TabOverlap = 16, TabLabelH = 40, TabPickD = 80;   // タブの帯
    public const float RibbonH = 80;                            // 見出しの帯
    public const float StripeAlpha = 0.35f;                     // 主ボタンの縞（GoLight の 35%）
    public const float BandH = 64;                              // パネルの中の横縞の周期

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
    // #190 文字（Nunito。見出し・ボタン・名前・札・選択肢は ExtraBold、台詞・本文は Bold。Baloo 2 はロゴと数字だけ）
    public const float TextTalk = 36, TalkLineHeight = 1.5f;   // 台詞（最大3行）
    public const float TextChoice = 32;   // 選択肢・品名の札・ボタン
    public const float TextRow = 30;      // 行の名前・パネルの見出し・副ボタン
    public const float TextRibbon = 36;   // 画面の見出しの帯
    public const float TextHint = 26;     // ヒントのピル・名前タグ
    public const float TextPrice = 24;    // 値段

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
    public const float TagH = 48;               // 名前タグ（YOU など。#190 で 44 → 48）
    public const float ChipH = 96;              // 合言葉のチップ

    // ------------------------------------------------------------------ HUD の部品（#71。UiKitHud.cs。ガイドライン 3章）
    public const float CanvasRefW = 1600, CanvasRefH = 900, CanvasMatch = 0.5f;   // CanvasScaler（GameFlow と同じ。見本の画面で使う）
    // ゲージ
    public const float GaugeH = 28;             // 溝と中身（ピル）
    public const float GaugeW = 480;            // 1行の幅（アイコン・数字の箱・状態のアイコンを含む）。親が広げてよい
    public const float GaugeMinTrackW = 120;    // 溝の最小の幅
    public const float GaugeNumberW = 128;      // 数字の箱（右寄せ。桁ぶれを防ぐ）
    public const float LifelineW = 4, LifelineOver = 6;   // 生還ラインの縦線（幅・上下のはみ出し）
    // トグル（ON/OFF を色・つまみの位置・文字で）
    public const float ToggleW = 128, ToggleH = 64, ToggleKnob = 48;
    public const float ToggleSlideSec = 0.12f;  // つまみが動く時間
    // 長押しボタン・同時押しの合図の輪
    public const float HoldD = 96;              // 長押しボタンの円（アイコンボタンと同じ）
    public const float RingW = 8;               // 進み具合の輪の幅
    public const float HoldBaseSec = 0.5f;      // 長押しの基本の時間（SPEC 6章「基本0.5秒 ×（1 + slow/100）」。parameters.json に無いのでここに置く）
    public const float HoldDrainSec = 0.15f;    // 途中で離したとき、輪が空に戻るまで
    public const float DoneFlashSec = 0.6f;     // できたとき ✓ を出しておく時間
    public const float MarkD = 40;              // 右上の小さな印（足りない条件・切断など。中のアイコンは IconS）
    // カウントダウンのチップ
    public const float CountdownH = 64, CountdownIcon = 32, CountdownNumberW = 112;
    public const float CountdownWarnSec = 10;   // 残りこれ以下で danger にして、1秒ごとに弾む
    public const float BounceScale = 1.08f, BounceSec = 0.25f;   // 弾む（大きさ・時間）
    // 警告の帯（画面上の中央。同時に1本）
    public const float BannerH = 72, BannerPadX = 40;
    // 吹き出し
    public const float BubbleW = 1040;          // 幅（1600 の 65%。#190 で 880 → 1040）。船内は BubbleWCabin
    public const float BubbleWCabin = 880;      // 船内（3D を見せる）
    public const float BubbleMinH = 150, BubbleMaxH = 260, BubbleMaxHCabin = 180;
    public const float BubblePadX = 72, BubblePadY = 36;
    public const float BubbleTagTilt = 4;       // 名前タグの傾き（度、反時計回り。ガイドラインの「−4°」は CSS の向き）
    public const int BubbleMaxLines = 3;
    public const float CaretW = 32, CaretH = 22, CaretY = 8, CaretBob = 6, CaretPeriod = 0.6f;   // ▼（大きさ・下からの位置・上下の幅・周期）
    public static readonly Color BubbleEm = Go; // 台詞の固有名詞と数字の色。accent は 32px 以上の太字だけなので、本文 28 では go にする
    // 席タブ（1人で複数の席を持つとき）
    public const float TabH = 72;               // 面の高さ（押せる範囲は Hit）

    // ------------------------------------------------------------------ 動き（#190。仮の値：静止画に基準が無い。iizuka が見て決める）
    public const float CursorBob = 6, CursorPeriod = 0.6f;     // 手の上下（px・秒）
    public const float CursorMoveSec = 0.12f;                  // 手が次の物へ動く（OutBack）
    public const float CursorPoke = 10, CursorPokeSec = 0.15f; // 押すと指す方へ突いて戻る
    public const float PickScale = 1.12f, PickSec = 0.25f;     // 選んだ物が 1.0 → 1.12 → 1.0（カードは 1.12 のまま）
    public const float StripeSpeed = 24;                       // 主ボタンの縞が流れる（px/秒）

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
    /// <summary>Nunito ExtraBold（見出し・ボタン・名前・札・選択肢）。UiTheme に strong が登録されるまでは FontBody（Nunito Bold）で代用</summary>
    public static TMP_FontAsset FontStrong => (ArtOf<TMP_FontAsset>("strong") ?? FontBody);
    public static Sprite Icon(string name) => Theme.Icon(name);

    /// <summary>角丸の半径に合うスプライト（小・中・大）</summary>
    public static Sprite Round(float radius) => radius <= RadiusS ? Theme.round16 : radius <= RadiusM ? Theme.round28 : Theme.round40;

    // #190 の新しい形の名前（art/ui/make_sprites.py。Art/UI/Sprites/<名前>.png）
    public const string BlobBubble = "blob_bubble", BlobPanel = "blob_panel", BlobPill = "blob_pill", BlobDisc = "blob_disc",
        WaveEdge = "wave_edge", CaretSoft = "caret_soft", Bands = "bands64", Glove = "glove_point";

    static readonly System.Collections.Generic.Dictionary<string, Object> art = new System.Collections.Generic.Dictionary<string, Object>();

    /// <summary>名前で形を引く（"blob_panel" など）。無ければ null（呼ぶ側が round40 などで代わりにする）。
    /// 探す順：UiTheme の同じ名前の欄（blob_panel → blobPanel。UiAssets で登録する）→ エディタなら Art/UI/Sprites/&lt;名前&gt;.png（Sprite として読み込めるときだけ）</summary>
    public static Sprite Art(string name) => ArtOf<Sprite>(name);

    static TA ArtOf<TA>(string name) where TA : Object
    {
        if (art.TryGetValue(name, out var hit)) return hit as TA;
        TA found = null;
        var th = Theme;
        if (th != null)
        {
            var f = typeof(UiTheme).GetField(Camel(name));
            if (f != null && typeof(TA).IsAssignableFrom(f.FieldType)) found = f.GetValue(th) as TA;
        }
#if UNITY_EDITOR
        if (found == null && typeof(TA) == typeof(Sprite))
            found = UnityEditor.AssetDatabase.LoadAssetAtPath<TA>("Assets/Art/UI/Sprites/" + name + ".png");
#endif
        if (found != null || Application.isPlaying) art[name] = found;   // 編集中は見つかるまで探し直す
        return found;
    }

    static string Camel(string snake)
    {
        var parts = snake.Split('_');
        var sb = new System.Text.StringBuilder(parts[0]);
        for (int i = 1; i < parts.Length; i++) if (parts[i].Length > 0) sb.Append(char.ToUpperInvariant(parts[i][0])).Append(parts[i].Substring(1));
        return sb.ToString();
    }

    // ------------------------------------------------------------------ 席（色＋形。art-direction.md と同じ）
    public static Color SeatColor(string seat) =>
        seat == Seats.Pilot ? SeatRed : seat == Seats.Life ? SeatGreen : seat == Seats.Comms ? SeatYellow : SeatBlue;

    /// <summary>席の色の面に載せる形・文字の色。黄はクリーム系の白では読めないので ink</summary>
    public static Color OnSeat(string seat) => seat == Seats.Comms ? Ink : Panel;

    public static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
    public static Color WithAlpha(Color c, float a) { c.a = a; return c; }
}
