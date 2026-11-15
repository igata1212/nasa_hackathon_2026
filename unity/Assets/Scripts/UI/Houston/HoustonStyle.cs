// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-14、#146 船内の高さを席のパネルに合わせた）／ 更新：iizuka（Opus 5.5、2026-11-15、#189 先生の小窓の帯）
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
    public int pageChars;         // 1ページに入れる文字数の目安（文の切れ目で分ける）

    public HoustonPlace(string name, float x0, float x1, float anchorY, float pivotY, float offsetY, float height, int pageChars)
    { this.name = name; this.x0 = x0; this.x1 = x1; this.anchorY = anchorY; this.pivotY = pivotY; this.offsetY = offsetY; this.height = height; this.pageChars = pageChars; }
}

public static class HoustonStyle
{
    // ================================================================== 文言（画面の文字は英語）
    public const string Name = "HOUSTON";
    /// <summary>名前タグの横の補助ラベル。高校生が「ヒューストン＝地上の管制室」と一目で分かるように（記号は書体に無いことがあるので ASCII だけ）</summary>
    public const string Role = "Mission Control on Earth";
    /// <summary>最初の1回だけ出す、少し長い説明（補助ラベルの代わりに出す）</summary>
    public const string RoleFirst = "Mission Control on Earth: they guide you by radio";
    public const bool RoleAlways = false;  // true なら補助ラベルをいつも出す。#146（Fable）：文字を減らすため、最初の RoleFirstS 秒とブリーフィングだけ。
                                           //   #189：そのかわり先生の小窓の下の帯に「ON EARTH」をいつも出す（WindowTagSmall。本人の「ヒューストンが分からん」）
    public const float RoleFirstS = 20f;   // 長い説明を出しておく秒（最初の台詞が出てから）
    public const string RoleIcon = "earth";

    public const string NoSignal = "NO SIGNAL";
    public const string NoSignalFarSide = "The Moon is blocking the radio. Houston can't hear you.";
    public const string NoSignalOther = "The radio is cut off for now. Houston can't hear you.";   // 再突入の熱など
    public const string NoSignalIcon = "comms_lost";
    public const string CommsOff = "COMMS OFF";
    public const string CommsOffHint = "Comms & Power: switch the radio ON to hear Houston.";
    public const string CommsOffIcon = "comms";

    public const string WarnIcon = "danger";   // 声なしの警告（通信できない間）の帯
    public const string Skip = "SKIP";
    public const string TapNext = "Tap for next";
    public const string TapDone = "Tap to close";

    /// <summary>台本の読み上げ用の印。字幕には出さない</summary>
    public static readonly string[] StripTokens = { "(pause)" };

    // ================================================================== 話し手（ブリーフィングの speaker）
    /// <summary>名前タグの文字。houston 以外は席の名前、それ以外は大文字にしてそのまま</summary>
    public static string SpeakerName(string speaker)
    {
        if (string.IsNullOrEmpty(speaker) || speaker == "houston") return Name;
        foreach (var s in Seats.All) if (speaker == s) return Seats.Title(s);
        return speaker.ToUpperInvariant();
    }

    public static bool IsHouston(string speaker) => string.IsNullOrEmpty(speaker) || speaker == "houston";

    // ================================================================== 強調（固有名詞と数字だけ色文字。ui-guidelines 1章の5）
    public static Color Highlight => T.Go;   // panel の上で約 3:1（Nunito Bold 28 なので大きい文字の基準を満たす）
    /// <summary>色を付ける固有名詞（長いものから順に当てる）。席の名前は Seats.Title から足す</summary>
    public static readonly string[] ProperNouns =
    {
        "Mission Control", "Apollo 13", "Houston", "Texas", "Earth", "Moon", "NASA", "Tank 1", "Tank 2",
        "Pilot", "Life Support", "Comms & Power", "Mission Planner",
    };

    // ================================================================== 置き場所（段階ごと）
    // 船内：画面の下の席パネル（CabinHud.Layout の Bottom：0〜0.24）の上、真ん中。左に先生の小窓（HoustonWindow、192×120）が出るので x0 は 0.1 より右
    //   上の端は名前タグを入れて約 0.47。CabinHud の真ん中（CenterMid：0.47 から上）・左（Left：0.39 から上）と重ならない（#146）
    public static readonly HoustonPlace Cabin = new HoustonPlace("cabin", 0.22f, 0.78f, 0.25f, 0f, 0f, 140f, 100);   // #146（Fable）：2行ぶんに低くした（船室が見えるように）
    // 設計：部品の一覧（0.2 より上）と打ち上げのボタン（右下）を避けて左下に小さく。先生の小窓は出ない（x0 < 0.1）
    public static readonly HoustonPlace Design = new HoustonPlace("design", 0.03f, 0.6f, 0.02f, 0f, 0f, 124f, 80);
    // エンディング：題・要件・SCORE・ボタンが真ん中の列にあるので、右の列に出す（ED-C の台詞が SCORE に重ならない）
    public static readonly HoustonPlace Ending = new HoustonPlace("ending", 0.64f, 0.98f, 0.3f, 0f, 0f, 200f, 90);
    // レポート：PLAY AGAIN（下の真ん中）の右の隅
    public static readonly HoustonPlace Report = new HoustonPlace("report", 0.64f, 0.98f, 0.02f, 0f, 0f, 150f, 70);
    // ブリーフィング：真ん中より少し下に大きく。後ろを暗くする。先生の小窓が左に出る
    public static readonly HoustonPlace Briefing = new HoustonPlace("briefing", 0.33f, 0.9f, 0.36f, 0f, 0f, 220f, 120);   // #146（Fable）：左に先生の大きい小窓（WindowBriefing）が入るぶん右へ

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
    public const string WindowTagSmall = "ON EARTH", WindowTagBriefing = "MISSION CONTROL";
    public const float WindowCaptionH = 30f;          // 帯の高さ（補助ラベル 22 × 行間 1.3 ＝ 約 29）
    public const float WindowCaptionAlpha = 0.85f;    // 帯（ink）の濃さ。3D の絵の上で白い字が読めるように
    public static readonly Color BriefingScrimColor = T.WithAlpha(T.Ink, 0.97f);   // #146（Fable）：色空間が Linear なので 0.97 でやっと暗い紺に見える。後ろの設計の画面の文字が読めないくらい暗く（先生と台詞だけを見せる）
    public static bool BriefingScrim = true;   // ブリーフィングの間、後ろを暗くする（後ろは押せない。どこを押しても次へ）

    // ================================================================== 大きさ（px。基準 1600×900）
    public const float PadX = 40;            // 左右の内側
    public const float PadTop = 30;          // 上の内側（名前タグの下半分がかぶるぶん）
    public const float PadBottom = 20;
    public const float TagTilt = -4f;        // 名前タグの傾き（度）
    public const float TextMin = T.TextLabel; // 長い文は自動で縮めるが、補助ラベルの大きさより小さくしない
    public const float LinkIcon = T.IconL;   // NO SIGNAL・COMMS OFF のアイコン
    public const float SeatBadge = T.TagH;   // 台詞に出てきた席のバッジ（名前タグと同じ高さ）
    public const float WarnH = 72;           // 声なしの警告の帯（警告の帯と同じ高さ）
    public const float WarnGap = 30;         // 吹き出しの上の端（名前タグのはみ出しの上）からの隙間
    public const float CaretSize = T.IconS;
    public const float SkipGap = T.S2;       // SKIP と吹き出しの隙間

    // ================================================================== 時間（秒）
    public const float PageBaseS = 1.6f;     // 1ページを出しておく秒 = 基本 + 文字数 × 1文字
    public const float PagePerCharS = 0.06f; // 約 16 文字／秒（高校生がゆっくり読める速さ）
    public const float PageMinS = 3f;
    public const float VoiceTailS = 0.6f;    // 声が終わってから次のページへ行くまで
    public const float WarnS = 6f;           // 声なしの警告の帯を出しておく秒
    public const float CaretBobS = 0.6f, CaretBobPx = 6f;   // ▼ が上下する周期と幅

    // ================================================================== 優先度（#127：表示中のほうが高いときだけ新しい台詞を捨てる）
    public static int Rank(string priority) =>
        priority == "milestone" ? 3 : priority == "critical" ? 2 : priority == "high" ? 1 : 0;

    /// <summary>名前タグの面の色（ui-guidelines 3章：critical のときだけ danger）</summary>
    public static Color TagColor(string priority) => priority == "critical" ? T.Danger : T.Go;

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
