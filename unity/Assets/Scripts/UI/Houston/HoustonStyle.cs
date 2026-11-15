// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-14、#146 船内の高さを席のパネルに合わせた）／ 更新：iizuka（Opus 5.5、2026-11-15、#189 先生の小窓の帯）／ 更新：iizuka（Opus 5.5、2026-11-15、#190 あつ森の吹き出し【12】：文字 36・行間 1.5・大文字をやめる・置き場所の大きさ）
// ヒューストン（地上の管制室の先生）の吹き出し（#75）の文言・置き場所・大きさ・時間を1か所に集めたもの。
// 見た目を直すときはここだけ直す（色は UiTokens の名前で指す。色コードはここにも書かない）。
// 吹き出しの組み立ては HoustonBubble.cs、出す順番と時間は HoustonBox.cs、台詞の読み込みと区切りは HoustonScript.cs、声は HoustonVoice.cs
using System.Collections.Generic;
using UnityEngine;
using T = UiTokens;

/// <summary>吹き出しの置き場所。x は画面に対する割合、y は anchorY の位置から offsetY ずらした所に pivotY を合わせる（px は基準 1600×900）</summary>
public struct HoustonPlace
{
    public string name;
    public float x0, x1;          // 左右（画面の割合）
    public float anchorY, pivotY; // 下に置くなら pivotY=0（anchorY が吹き出しの下の端）、上に置くなら 1
    public float offsetY;         // px
    public float height;          // 吹き出しの高さ（名前タグのはみ出しは含まない）
    public int pageChars;         // 1ページに入れる文字数の目安（文の切れ目で分ける）。台詞がこの置き場所で3行に入るように測って決める（#190）
    public float textSize;        // 台詞の大きさ（ふつう T.TextTalk 36。船内だけ 32）
    public float padTop, padBottom; // 文の上下の内側

    public HoustonPlace(string name, float x0, float x1, float anchorY, float pivotY, float offsetY, float height, int pageChars,
                        float textSize = T.TextTalk, float padTop = T.BubblePadY, float padBottom = T.BubblePadY)
    {
        this.name = name; this.x0 = x0; this.x1 = x1; this.anchorY = anchorY; this.pivotY = pivotY; this.offsetY = offsetY; this.height = height; this.pageChars = pageChars;
        this.textSize = textSize; this.padTop = padTop; this.padBottom = padBottom;
    }
}

public static class HoustonStyle
{
    // ================================================================== 文言（画面の文字は英語）
    public const string Name = "Houston";   // #190：大文字だけの言葉をやめる（ui-guidelines 原則 12）
    /// <summary>名前タグの横の補助ラベル。高校生が「ヒューストン＝地上の管制室」と一目で分かるように（記号は書体に無いことがあるので ASCII だけ）</summary>
    public const string Role = "Mission Control on Earth";
    /// <summary>最初の1回だけ出す、少し長い説明（補助ラベルの代わりに出す）</summary>
    public const string RoleFirst = "Mission Control on Earth: they guide you by radio";
    public const bool RoleAlways = false;  // true なら補助ラベルをいつも出す。#146（Fable）：文字を減らすため、最初の RoleFirstS 秒とブリーフィングだけ。
                                           //   #189：そのかわり先生の小窓の下の帯に「ON EARTH」をいつも出す（WindowTagSmall。本人の「ヒューストンが分からん」）
    public const float RoleFirstS = 20f;   // 長い説明を出しておく秒（最初の台詞が出てから）
    public const string RoleIcon = "earth";

    // NoSignal・CommsOff は先生の小窓（Story/HoustonWindow）が見えない Probe の文字で見分ける合図なので変えない。画面に出す言葉は *Title（#190：大文字をやめた）
    public const string NoSignal = "NO SIGNAL";
    public const string NoSignalTitle = "No signal";
    public const string NoSignalFarSide = "The Moon is blocking the radio. Houston can't hear you.";
    public const string NoSignalOther = "The radio is cut off for now. Houston can't hear you.";   // 再突入の熱など
    public const string NoSignalIcon = "comms_lost";
    public const string CommsOff = "COMMS OFF";
    public const string CommsOffTitle = "Comms off";
    public const string CommsOffHint = "Comms & Power: switch the radio ON to hear Houston.";
    public const string CommsOffIcon = "comms";

    public const string WarnIcon = "danger";   // 声なしの警告（通信できない間）の帯
    public const string Skip = "Skip";        // 右下のヒントのピル（#190。【10】）
    public const string SkipIcon = "enter";
    public const string TapNext = "Tap for next";
    public const string TapDone = "Tap to close";

    /// <summary>台本の読み上げ用の印。字幕には出さない</summary>
    public static readonly string[] StripTokens = { "(pause)" };

    // ================================================================== 話し手（ブリーフィングの speaker）
    /// <summary>名前タグの文字。houston 以外は席の名前、それ以外は頭だけ大文字にしてそのまま（#190：大文字だけにしない）</summary>
    public static string SpeakerName(string speaker)
    {
        if (string.IsNullOrEmpty(speaker) || speaker == "houston") return Name;
        foreach (var s in Seats.All) if (speaker == s) return Seats.Title(s);
        return char.ToUpperInvariant(speaker[0]) + speaker.Substring(1);
    }

    public static bool IsHouston(string speaker) => string.IsNullOrEmpty(speaker) || speaker == "houston";

    // ================================================================== 強調（固有名詞と数字だけ色文字。ui-guidelines 3章「会話の吹き出し」【03】）
    public static Color Highlight => T.Go;      // 固有名詞。panel の上で約 3:1（台詞 32〜36 の太字なので大きい文字の基準）
    public static Color NumberColor => T.Accent; // 数字（accent は 32px 以上の太字だけ。台詞が 36／船内 32 になったので使える）
    /// <summary>色を付ける固有名詞（長いものから順に当てる）。席の名前は Seats.Title から足す</summary>
    public static readonly string[] ProperNouns =
    {
        "Mission Control", "Apollo 13", "Houston", "Texas", "Earth", "Moon", "NASA", "Tank 1", "Tank 2",
        "Pilot", "Life Support", "Comms & Power", "Mission Planner",
    };

    // ================================================================== 置き場所（段階ごと）
    // #190：【12】の大きさに寄せた（幅は画面の 55〜65%・台詞 36・行間 1.5・最大3行・内側 左右 72／上下 36）。
    //   pageChars は今の台詞（docs/design/houston_lines.json・Assets/Data/Story/briefing.json）を Nunito Bold で測って、どのページも行数に入る値にした
    //   （1600 幅で測る。iPhone の横 844×390 は Canvas が 1765 幅になるので、横はもっと余る）
    // 船内：画面の下の席パネル（CabinHud.Layout の Bottom：0〜0.24）の上、真ん中。左に先生の小窓（HoustonWindow、192×120）が出るので x0 は 0.1 より右
    //   幅 880（55%）・高さ 180（3D を見せるため。ui-guidelines 3章）。いちばん長いページ（前の pageChars 100 で 96 字）は 36 でも横は 3 行に入るが、
    //   3 行 × 36 × 1.5 は高さ 180 に入らない（文だけで約 157、内側 36＋36 を足すと 229）。32 にしても 3 行（約 140）は内側を 24／14 に詰めないと入らず、
    //   名前タグと下の縁に文がくっついて窮屈だった（見本を描いて比べた）。だから船内も 36 のまま、1 ページを 2 行（80 字。測ると最長 79 字＝2 行）にした。
    //   32・3 行に戻すなら：new HoustonPlace("cabin", 0.225f, 0.775f, 0.25f, 0f, 0f, T.BubbleMaxHCabin, 100, TextCabin, 24f, 14f)
    //   上の端は名前タグを入れて約 0.48。CabinHud の真ん中（CenterMid：0.42〜。中身は上から下へ伸びる）・左（Left：0.39 から上）と重ならない
    public static readonly HoustonPlace Cabin = new HoustonPlace("cabin", 0.225f, 0.775f, 0.25f, 0f, 0f, T.BubbleMaxHCabin, 80);
    // 設計：部品の一覧（右の列）と打ち上げのボタン（右下）を避けて左下。先生の小窓は出ない（x0 < 0.1）。2 行（1 行 約 44 字）× 36 で高さ 200
    public static readonly HoustonPlace Design = new HoustonPlace("design", 0.03f, 0.6f, 0.02f, 0f, 0f, 200f, 84);
    // エンディング：題・要件・SCORE・ボタンが真ん中の列にあるので、右の列に出す（ED-C の台詞が SCORE に重ならない）。幅が 544 しかないので 1 ページ 36 字（2 行）
    //   ※ GameFlow はエンディングとレポートでは台詞を出さない（前の段階の残りだけ）
    public static readonly HoustonPlace Ending = new HoustonPlace("ending", 0.64f, 0.98f, 0.3f, 0f, 0f, 200f, 36);
    // レポート：PLAY AGAIN（下の真ん中）の右の隅
    public static readonly HoustonPlace Report = new HoustonPlace("report", 0.64f, 0.98f, 0.02f, 0f, 0f, 200f, 36);
    // ブリーフィング：【12】と同じ大きさ（幅 65% = 1040・高さ 260）。真ん中より少し下。後ろを暗くする。左に先生の大きい小窓（352 幅）が入るので x0 = 0.25（小窓の左が画面の端 32）
    public static readonly HoustonPlace Briefing = new HoustonPlace("briefing", 0.25f, 0.9f, 0.36f, 0f, 0f, T.BubbleMaxH, 120);

    /// <summary>段階 → 置き場所</summary>
    public static HoustonPlace PlaceFor(string phase)
    {
        switch (phase)
        {
            case "design": return Design;
            case "ending": return Ending;
            case "report": return Report;
            default: return Cabin;
        }
    }

    /// <summary>先生の小窓（Story/HoustonWindow）の大きさ。ブリーフィングは顔が主役なので大きく（描く絵は 384×240）</summary>
    public static readonly Vector2 WindowSmall = new Vector2(192f, 120f), WindowBriefing = new Vector2(352f, 220f);
    public const float WindowGap = 8f, WindowGapBriefing = 16f;
    /// <summary>小窓の下の帯の文字（#189）。小さい窓（船内・エンディング・レポート）と、ブリーフィングの大きい窓。補助ラベル 22 で入る長さにする</summary>
    public const string WindowTagSmall = "On Earth", WindowTagBriefing = "Mission Control";   // #190：大文字をやめた
    public const float WindowCaptionH = 30f;          // 帯の高さ（補助ラベル 22 × 行間 1.3 ＝ 約 29）
    public const float WindowCaptionAlpha = 0.85f;    // 帯（ink）の濃さ。3D の絵の上で白い字が読めるように
    public static readonly Color BriefingScrimColor = T.WithAlpha(T.Ink, 0.97f);   // #146（Fable）：色空間が Linear なので 0.97 でやっと暗い紺に見える。後ろの設計の画面の文字が読めないくらい暗く（先生と台詞だけを見せる）
    public static bool BriefingScrim = true;   // ブリーフィングの間、後ろを暗くする（後ろは押せない。どこを押しても次へ）

    // ================================================================== 大きさ（px。基準 1600×900。#190 で【12】に寄せた）
    public const float PadX = T.BubblePadX;  // 左右の内側 72（上下は置き場所ごと。HoustonPlace.padTop／padBottom）
    public const float TextCabin = 32;       // 船内を 3 行にするときの台詞の大きさ（今は使っていない。上の Cabin の注記）
    public const float TextMin = T.TextHint; // 思ったより長い文は自動で縮めるが、26 より小さくしない（Ellipsis で切るより読める）
    public const float TagTilt = T.NameTagTilt;   // 名前タグの傾き（度、反時計回り＝右上がり。【12】。ガイドラインの「−6°」は CSS の向き）
    public const float TagX = 36;            // 名前タグの左の端（吹き出しの左から）
    public const float TagOverlap = 0.4f;    // 名前タグの高さのうち吹き出しにかぶせる割合（【12】で約 4 割。残りは上にはみ出す）
    public const float RoleGap = T.S1;       // 名前タグと補助ラベルの間
    public const float TagPadX = T.S3;       // 名前タグの左右の内側
    public const float RoleH = 40;           // 補助ラベル（Mission Control on Earth）の札の高さ。名前タグ 48 より小さく
    public const float PageTagH = 36;        // 右下の縁のページ数の札（文字 22）
    public const float LinkIcon = T.IconL;   // NO SIGNAL・COMMS OFF のアイコン
    public const float SeatBadge = T.TagH;   // 台詞に出てきた席のバッジ（名前タグと同じ高さ）
    public const float WarnH = 72;           // 声なしの警告の帯（警告の帯と同じ高さ）
    public const float WarnGap = 30;         // 吹き出しの上の端（名前タグのはみ出しの上）からの隙間
    public const float SkipEdge = T.ScreenEdge;   // Skip（ヒントのピル）を画面の右下の角からどれだけ離すか
    public const float BubbleFit = 0.8f;     // blob_bubble の上下の縁（96＋96）が高さのこれだけに収まるよう縮める（船内 180 で 0.75 倍）

    // ================================================================== 時間（秒）
    public const float PageBaseS = 1.6f;     // 1ページを出しておく秒 = 基本 + 文字数 × 1文字
    public const float PagePerCharS = 0.06f; // 約 16 文字／秒（高校生がゆっくり読める速さ）
    public const float PageMinS = 3f;
    public const float VoiceTailS = 0.6f;    // 声が終わってから次のページへ行くまで
    public const float WarnS = 6f;           // 声なしの警告の帯を出しておく秒
    public const float CaretBobS = T.CaretPeriod, CaretBobPx = T.CaretBob;   // ▼ が上下する周期と幅

    // ================================================================== 優先度（#127：表示中のほうが高いときだけ新しい台詞を捨てる）
    public static int Rank(string priority) =>
        priority == "milestone" ? 3 : priority == "critical" ? 2 : priority == "high" ? 1 : 0;

    /// <summary>名前タグの面の色（critical のときだけ danger）。#190：ふだんはオレンジ（【12】のタグ。iizuka の指示）。
    /// ※ ui-guidelines 3章「名前タグ」は Houston＝go。食い違いは報告済み。戻すならここだけ</summary>
    public static Color TagColor(string priority) => priority == "critical" ? T.Danger : T.Accent;

    /// <summary>台詞に出てくる席（だれの仕事か）。Seats.Title を大文字小文字を無視して探す</summary>
    public static List<string> SeatsIn(string text)
    {
        var r = new List<string>();
        if (string.IsNullOrEmpty(text)) return r;
        foreach (var s in Seats.All)
            if (text.IndexOf(Seats.Title(s), System.StringComparison.OrdinalIgnoreCase) >= 0) r.Add(s);
        return r;
    }
}
