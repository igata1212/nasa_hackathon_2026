// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// タイトルと部屋の画面を「あつ森の方向」に寄せるための値（#153）。動き（速さ・弾み）・案内の文・背景の飾りの大きさは、ここだけを直す。
// 色はすべて UiTokens の色から作る（色コードは書かない）。px は Canvas の基準 1600×900、3D は m・度・秒。
// 文の [ ] で囲んだ所は、ボタンの名前などの強調（Rich() で KeyColor の色文字にする）。1行 40 字・2行まで（ui-guidelines 3章 吹き出し）
using UnityEngine;
using T = UiTokens;

public static class TitleLook
{
    // ================================================================== 案内の吹き出し（ヒューストンが話しかける）
    public const string Speaker = "HOUSTON";
    public const string SpeakerIcon = "comms";

    // タイトル：今の入力の状態で1つだけ出す（TitleScreen.RefreshHint）
    public const string HintName = "Hi! I'm Houston, your team on Earth.\nFirst, type your name below.";
    public const string HintCreate = "Nice! Press [CREATE ROOM] to make a room.\nGot a friend's code? Type it, then [JOIN].";
    public const string HintCode = "A room code has 4 digits.\nKeep typing!";
    public const string HintJoin = "Code ready!\nPress [JOIN] to meet your crew.";
    public const string HintNeedCode = "Type the 4-digit room code first,\nthen press [JOIN].";   // 鍵のかかった JOIN を押したとき

    // 部屋：人数で1つだけ出す（LobbyScreen.Refresh）
    public const string LobbySolo = "Tell your friends the [ROOM] code!\nPlaying alone? Press [START MISSION].";
    public const string LobbySome = "Fewer than 4? Some of you get 2 seats.\nAll here? Press [START MISSION]!";
    public const string LobbyFull = "Full crew, one seat each!\nPress [START MISSION] when ready.";

    public static readonly Color KeyColor = T.Go;   // [ ] の中の色（読みにくければ T.Ink に）

    /// <summary>[ ] で囲んだ所を KeyColor の色文字にする（TMP のリッチテキスト）</summary>
    public static string Rich(string s)
    {
        string open = "<color=#" + ColorUtility.ToHtmlStringRGB(KeyColor) + ">";
        return s.Replace("[", open).Replace("]", "</color>");
    }

    // ================================================================== 吹き出しの形
    public const float BubbleTitleW = 880;     // タイトル：入力パネル（UiTokens.TitleFormW）と同じ幅
    public const float BubbleTitleH = 120;     // 2行の本文（28 × 1.3 × 2）＋上下の内側
    public const float BubbleTitleTailX = 120; // 下の ▼ の場所（左から）。名前の欄を指す
    public const float BubbleLobbyW = 700;     // 部屋：合言葉のチップの右
    public const float BubbleLobbyH = 96;      // 合言葉のチップ（UiTokens.ChipH）と同じ高さ
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
    // 細かい模様の地（星と点が斜めにゆっくり流れる）
    public const int PatternTex = 64;          // 模様の1マスの画像（px）
    public const float PatternTile = 56;       // 画面の上での1マス
    public static readonly Color PatternColor = T.WithAlpha(T.Ink, 0.06f);
    public static readonly Vector2 PatternSpeed = new Vector2(10f, 6f);   // px/秒

    // またたく星
    public const int StarCount = 16, StarSeed = 7;
    public const float StarMin = 16, StarMax = 34;
    public const float StarHzMin = 0.25f, StarHzMax = 0.6f, StarDim = 0.45f;   // 一番小さいときの大きさ（倍）
    public static readonly Rect StarArea = new Rect(0.03f, 0.3f, 0.94f, 0.66f);  // 画面の割合（左下が 0,0）
    public static readonly Color StarColorA = T.Accent;
    public static readonly Color StarColorB = T.WithAlpha(T.Ink, 0.35f);
    public const float SparkleExp = 2.2f;      // 星の形（2 でひし形、大きいほど細い）

    // ときどき空を横切るロケット
    public const float RocketEvery = 16f, RocketDur = 8f, RocketFirst = 3f;
    public const float RocketSize = 44, RocketY = 0.8f, RocketArc = 50f;   // RocketY は画面の高さの割合
    public static readonly Color RocketColor = T.WithAlpha(T.Ink, 0.5f);
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
