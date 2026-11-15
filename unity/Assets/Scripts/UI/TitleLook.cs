// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-15、#189 何もしていないのに褒めない）／ 更新：iizuka（Opus 5.5、2026-11-15、#190 あつ森の選択肢・数のチップ・名前の吹き出し・地の大きな柄）
// タイトルと部屋の画面を「あつ森の方向」に寄せるための値（#153）。動き（速さ・弾み）・案内の文・背景の飾りの大きさは、ここだけを直す。
// 色はすべて UiTokens の色から作る（色コードは書かない）。px は Canvas の基準 1600×900、3D は m・度・秒。
// 文の [ ] で囲んだ所は、ボタンの名前などの強調（Rich() で KeyColor の色文字にする）。1行 40 字・2行まで（ui-guidelines 3章 吹き出し）
// #190：大文字だけの言葉をやめた（ui-guidelines 1章の12。頭字語だけ大文字）
using UnityEngine;
using T = UiTokens;

public static class TitleLook
{
    // ================================================================== 画面の言葉（#190。ui-guidelines 3章「画面ごとの選択の替え方」）
    public const string Logo = "Mission Return";
    public const string Subtitle = "Bring the crew home. Together.";
    public const string ChoiceCreate = "Create a room";        // 選択肢の吹き出し（【10】）
    public const string ChoiceJoin = "Join with a code";
    public const string Settings = "Settings";                 // 右下のヒントのピル（【09】。開くと開発用の板。「DEV」は画面に映すので書かない）
    public const string StartMission = "Start mission!";       // 部屋の主ボタン（【05】Craft it! の形）
    public const string RoomUnit = "Room", CrewUnit = "Crew";  // 数のチップの右下の札（【09】）
    public const string Waiting = "Waiting", You = "You", Covering = "Covering this seat too";

    // ================================================================== 案内の吹き出し（ヒューストンが話しかける）
    public const string Speaker = "Houston";
    public const string SpeakerIcon = "comms";

    // タイトル：今の入力の状態で1つだけ出す（TitleScreen.RefreshHint）
    public const string HintName = "Hi! I'm Houston, your team on Earth.\nFirst, type your name below.";
    public const string HintCreate = "Pick [Create a room] to start a new one.\nGot a friend's code? Type it in!";
    public const string HintCode = "A room code has 4 digits.\nKeep typing!";
    public const string HintJoin = "Code ready!\nPick [Join with a code] to meet your crew.";
    public const string HintNeedCode = "Type the 4-digit room code first,\nthen pick [Join with a code].";   // 押せない Join を押したとき

    // 部屋：人数で1つだけ出す（LobbyScreen.Refresh）
    public const string LobbySolo = "Share the code, or press [Start mission!]";
    public const string LobbySome = "All here? Press [Start mission!]";
    public const string LobbyFull = "Full crew! Press [Start mission!]";

    public static readonly Color KeyColor = T.Go;   // [ ] の中の色（読みにくければ T.Text に）

    /// <summary>[ ] で囲んだ所を KeyColor の色文字にする（TMP のリッチテキスト）</summary>
    public static string Rich(string s)
    {
        string open = "<color=#" + ColorUtility.ToHtmlStringRGB(KeyColor) + ">";
        return s.Replace("[", open).Replace("]", "</color>");
    }

    // ================================================================== タイトルの入力と選択肢（#190。【05】のパネル＋【10】の選択肢の吹き出し）
    // 左：パネル（名前 → 合言葉を縦に）、右：選択肢の吹き出し（Create a room → Join with a code）。上下の順で対になる
    public const float FormW = 920;            // パネル＋間＋選択肢の幅（吹き出しも同じ幅）
    public const float FormGap = 48;           // パネルと選択肢の間（手が左の外に出る分）
    public const float LockOffsetX = 8;        // 押せない Join の右上の鍵の台（UiTokens.CheckD）を、行の右の端からどれだけ外へ

    // ================================================================== 部屋の名前の吹き出し（#190。【08】小さな吹き出し：blob_pill＋小さな丸のしっぽ）
    public const float TagPadX = 28, TagPadY = 8;   // ピルの内側（丸い端に文字がかからないように左右を広く）
    public const float TagH = 124;                  // ピルの高さの目安（縁の丸さ＝半分。中身で伸び縮みする）
    public static readonly float[] TagTailD = { 22f, 13f };   // しっぽの丸（大 → 小）の直径
    public static readonly Vector2[] TagTailAt = { new Vector2(-6f, -6f), new Vector2(-20f, -34f) };   // ピルの下の真ん中から、丸の上の端の位置
    public const float TagTailH = 48;               // しっぽの分の高さ（頭の上に合わせる下の端まで）
    public const float TagOpenAlpha = 0.22f;        // 空席の面（宇宙の上で薄く。点線の枠と Waiting の字で見分ける）
    public const float TagRing = 4;                 // 自分の席の輪（go）
    public const float TagAboveHead = 0.03f;        // しっぽの先を頭の上どれだけに置くか（m。しっぽの分だけ前の UiTokens.TagAboveHead 0.12 より下げる）

    // ================================================================== 吹き出しの形
    public const float BubbleTitleW = FormW;   // タイトル：入力パネル＋選択肢と同じ幅
    // #190：台詞は 36・行間 1.5（UiHintBubble。入らないと自動で縮む）。名前タグが上の縁に 4 割かぶるので、上の内側はその下から
    public const float BubbleTitleH = 176;     // 2行（36 × 1.5 × 2 ＝ 108）＋上下の内側。844×390 でもタイトルが縦に入る高さ
    public const float BubbleTitleTailX = 120; // 弾むときの中心（左から）。名前の欄の上
    public const float BubbleLobbyW = 800;     // 部屋：合言葉のチップの右。文は1行（一番長い Share the code, … が 36 で約 640）
    public const float BubbleLobbyH = 104;     // 1行（36 × 1.5）＋上下の内側。2行にするとクルーの名前の吹き出しにかぶるので1行にした
    public const float BubblePadX = 32, BubblePadY = 12;
    public const float BubbleTail = 32;        // しっぽ（45° 回した角丸の四角）の一辺
    public const float BubbleTailRadius = 6;
    public const float SpeakerTilt = -4f;      // 名前タグの傾き（度。ui-guidelines 3章）
    public const float SpeakerInsetX = 32;     // 名前タグの左からの位置
    public const float BubbleGapTop = 24;      // 名前タグが上にはみ出す分の余白（TagH の半分＋α）

    // ================================================================== 動き（秒・倍率・px）
    // 開いたときに出てくる（UiPopIn）
    public const float PopDur = 0.45f;         // ふわっと出る時間
    public const float PopFrom = 0.8f;         // 出る前の大きさ
    public const float BackOvershoot = 1.7f;   // 弾みの強さ（EaseOutBack。0 で弾まない）
    public const float SubtitleDelay = 0.9f, FormDelay = 1.05f, BubbleDelay = 1.4f;     // タイトル
    public const float LobbyChipDelay = 0.1f, LobbyBubbleDelay = 0.5f, LobbyStartDelay = 0.8f;   // 部屋

    // ロゴの文字：1文字ずつ下から弾んで出て、その後ときどき波が通る（UiLetterWave）
    public const float LetterDelay = 0.15f, LetterStagger = 0.05f, LetterDur = 0.5f, LetterDrop = 40f;
    public const float LetterWaveEvery = 5f, LetterWaveStagger = 0.045f, LetterWaveDur = 0.4f, LetterWaveAmp = 14f;

    // 席バッジ：順に出て、ときどき順に跳ねる（UiHop）
    public const float BadgeDelay = 0.05f, BadgeStagger = 0.08f;
    public const float HopEvery = 5f, HopStagger = 0.12f, HopDur = 0.42f, HopH = 18f;
    public const float HopStretch = 0.1f;      // 跳んでいる間は縦に伸びる
    public const float HopSquashDur = 0.16f, HopSquash = 0.14f;   // 着地で横につぶれる

    // 押したとき（UiSquish）：縮んで、離すとばねで少し大きくなって戻る
    public const float PressScale = 0.92f, HoverScale = 1.04f;
    public const float SpringHz = 4.5f, SpringDamping = 0.35f;     // 小さいほどよく弾む（1 で弾まない）
    public const float ReleaseKick = 1.6f;     // 離した瞬間に足す速さ（大きいほど大きく跳ね返る）
    public const float BreatheAmp = 0.03f, BreatheHz = 0.7f;      // 主ボタンがゆっくり息をする
    public const float ShakeDeg = 6f, ShakeHz = 9f, ShakeDur = 0.4f;   // 押せないボタンを押したら首を振る
    public const float MaxStep = 1f / 30f;     // ばねの計算の1回の最大の時間（止まったフレームで暴れない）

    // 吹き出し（UiHintBubble）
    public const float TypeCps = 55f;          // 1秒に出す文字数
    public const float BubblePopFrom = 0.6f, BubbleRePopFrom = 0.92f, BubblePopDur = 0.35f;

    // ================================================================== タイトルの背景（TitleBackdrop）
    // 地の大きな柄（#190。【09】の画面の端の大きな柄の「置き方」：端で切れるくらい大きく、地より少し濃い1色）。
    // 三日月・輪のある惑星・星を bgDeep で。欠けと輪のすき間は地の色（bg）で切る。画面の四隅（下は地球とクルーがいるので左右の端の中ほど）
    public static readonly Color MotifColor = T.BgDeep;
    public static readonly Color MotifCut = T.Bg;
    public const int MotifSegments = 96;
    // 三日月（左上）：丸（MoonD）から、右上へずらした地の色の丸（MoonD × MoonCutScale）を重ねて欠けさせる
    public static readonly Vector2 MoonAt = new Vector2(150f, -150f);     // 左上の角から
    public const float MoonD = 300f, MoonCutScale = 0.9f, MoonTilt = -12f;
    public static readonly Vector2 MoonCutOffset = new Vector2(0.15f, 0.09f);   // 欠けの丸のずれ（MoonD の割合）
    // 輪のある惑星（右上）：奥の輪（上半分）→ 惑星 → 手前の輪（下半分。惑星との間に地の色のすき間）
    public static readonly Vector2 PlanetAt = new Vector2(-230f, -200f);  // 右上の角から
    public const float PlanetD = 210f, PlanetTilt = -18f;
    public static readonly Vector2 RingOuter = new Vector2(2.05f, 0.56f), RingInner = new Vector2(1.5f, 0.32f);   // 輪の半径（惑星の半径の倍。横・縦）
    public const float RingGap = 9f;           // 手前の輪と惑星のすき間（px）
    // 大きな星（左右の端の中ほど）：x・y＝anchor（画面の割合）、z＝大きさ、w＝傾き（度）。位置は anchor からの px（StarsAt）
    public static readonly Vector4[] BigStars =
    {
        new Vector4(0f, 0.56f, 190f, 10f), new Vector4(0f, 0.56f, 84f, -8f),
        new Vector4(1f, 0.52f, 170f, -6f), new Vector4(1f, 0.52f, 76f, 14f),
    };
    public static readonly Vector2[] BigStarsAt = { new Vector2(70f, -10f), new Vector2(200f, -80f), new Vector2(-80f, 30f), new Vector2(-215f, -70f) };

    // 細かい模様の地（星と点が斜めにゆっくり流れる）
    public const int PatternTex = 64;          // 模様の1マスの画像（px）
    public const float PatternTile = 56;       // 画面の上での1マス
    public static readonly Color PatternColor = T.WithAlpha(T.Text, 0.06f);
    public static readonly Vector2 PatternSpeed = new Vector2(10f, 6f);   // px/秒

    // またたく星
    public const int StarCount = 16, StarSeed = 7;
    public const float StarMin = 16, StarMax = 34;
    public const float StarHzMin = 0.25f, StarHzMax = 0.6f, StarDim = 0.45f;   // 一番小さいときの大きさ（倍）
    public static readonly Rect StarArea = new Rect(0.03f, 0.3f, 0.94f, 0.66f);  // 画面の割合（左下が 0,0）
    public static readonly Color StarColorA = T.Accent;
    public static readonly Color StarColorB = T.WithAlpha(T.Text, 0.35f);
    public const float SparkleExp = 2.2f;      // 星の形（2 でひし形、大きいほど細い）

    // ときどき空を横切るロケット
    public const float RocketEvery = 16f, RocketDur = 8f, RocketFirst = 3f;
    public const float RocketSize = 44, RocketY = 0.8f, RocketArc = 50f;   // RocketY は画面の高さの割合
    public static readonly Color RocketColor = T.WithAlpha(T.Text, 0.5f);
    public const float RocketIconDeg = 45f;    // アイコン（Ionicons rocket）の先の向き（右上）
    public const int RocketTrail = 4;
    public const float RocketTrailGap = 0.012f, RocketTrailSize = 10;

    // 下の地球（丸い地平線）。雲が流れ、陸がゆっくり回る
    public const float EarthD = 2600, EarthRise = 150, AtmosphereW = 24;
    public static readonly Color AtmosphereColor = Color.Lerp(T.Bg, T.Go, 0.18f);
    public static readonly Color EarthColor = Color.Lerp(T.Bg, T.Go, 0.42f);
    public static readonly Color LandColor = Color.Lerp(T.Bg, T.Go, 0.62f);
    public static readonly Color CloudColor = T.Panel;
    public const int LandCount = 6, CloudCount = 5;
    public const float LandSpeed = 8f, CloudSpeed = 18f;   // px/秒
    public const float CloudMargin = 160f;     // 雲が画面の外へ出てから反対側に戻るまでの余白（px）
    public const int DiscSegments = 160;       // 大きい円の角の数
    public const float Feather = 1.5f;         // 円の縁のぼかし（px）

    // ================================================================== タイトルの 3D のクルー（TitleStage）
    public static readonly Vector3 TitleStageOrigin = new Vector3(400f, 3000f, 0f);   // 部屋の画面の 3D（UiTokens.StageOrigin）からも離す
    public const float TitleFov = 30f;
    public const float CrewScreenH = 0.46f;     // クルーの全身の高さ（画面の高さの割合）
    // 席の順（Seats.All）に、頭のてっぺんを置く場所（画面の割合 x・y）と奥行き（m。奥ほど後ろ）。足は画面の下に隠れる
    public static readonly Vector3[] CrewSlots =
    {
        new Vector3(0.085f, 0.40f, 0.6f), new Vector3(0.19f, 0.31f, 0f),
        new Vector3(0.81f, 0.31f, 0f), new Vector3(0.915f, 0.40f, 0.6f),
    };
    public const float CrewTurnIn = 22f;        // 真ん中（タイトル）の方へ体を向ける（度）
    public const float CrewIntroDelay = 0.6f, CrewIntroStagger = 0.15f, CrewIntroDur = 0.6f, CrewIntroDrop = 0.9f;   // 下から順に出てくる（m）
    public const float PeekEvery = 9f, PeekUp = 0.4f, PeekHold = 2.2f, PeekDown = 0.5f, PeekRise = 0.22f;   // ときどき1人ずつ伸び上がって手を振る（m）
    public const float TitleCrewBob = 0.03f;
    public static readonly Vector3 TitleLightPos = new Vector3(0f, 1.2f, -3.5f);   // 手前上からの明かり（TitleStageOrigin から。カメラは原点の手前、クルーは画面の下の方）
    public const float TitleLightIntensity = 14f, TitleLightRange = 14f;
    public const int AlphaCheckFrame = 3;       // 何フレーム目に「透明が残るか」を確かめるか

    // ================================================================== 部屋の画面の 3D（LobbyStage）
    public const int LobbyStarCount = 70, LobbyStarSeed = 11;
    public const float LobbyStarDepth = 28f;    // StageOrigin からの奥行き（地球より奥）
    public static readonly Vector2 LobbyStarSpread = new Vector2(22f, 11f);   // 横・縦の広がり（m、片側）
    public const float LobbyStarYCenter = 2f;
    public const float LobbyStarMin = 0.08f, LobbyStarMax = 0.22f;   // m
    public static readonly Color LobbyStarColor = T.Panel;
    public const float LobbyEarthSpin = 3f;     // 地球の回る速さ（度/秒）
    public const float JoinPopDur = 0.5f, JoinHopH = 0.35f, JoinPopFrom = 0.3f;   // 入ってきたクルーがぽんと出て跳ねる（m）
    public const float JoinStagger = 0.12f, JoinOpenWindow = 0.3f;   // 開いた直後（JoinOpenWindow 秒）は左から順に
    public const float TagTilt = 1.5f, TagSwayDeg = 1.2f, TagSwayHz = 0.25f;     // 名前のタグを少し傾けて、ゆっくり揺らす（度）
}
