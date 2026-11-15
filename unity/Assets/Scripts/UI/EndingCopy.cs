// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-15、#189 SCORE を整数に・出来事の文を短く）／ 更新：iizuka（Opus 5.5、2026-11-15、#190 大文字をやめる・台とカードの色）
// エンディング（#85）と事後レポート（#86）の文言・アイコン・並びの表。直すときはこのファイルだけ見ればよい（Fable の採点・Taiga の文言）。
// 調子：帰還の演出（ReturnStage の SPLASHDOWN!・LostStage の Mission lost）と合わせる。ED-C は責めず静かに、「もう一度考えよう」で終える（赤は使わない。帯は plum）。
// #190：大文字だけの言葉は使わない（ui-guidelines 原則 12。文の頭と固有名詞だけ大文字。例外は頭字語とルートの A/B）。
// 画面の文字は英語。フォントに無い字（— や ・）は使わない（Baloo 2・Nunito の Static に焼いたのは ASCII と °₂→✓×▲●■◆▶ だけ）。
// {0} などは string.Format で埋める。アイコンの名前は Art/UI/Icons のファイル名。
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using T = UiTokens;

public static class EndingCopy
{
    // ================================================================== 結末ごとの見出し
    public class Style
    {
        public string Title, Short, Icon;
        public Color Face;   // エンディングの見出しの帯と、レポートの結末の札の面（文字と絵は panel）
    }

    public static readonly Dictionary<string, Style> Endings = new Dictionary<string, Style>
    {
        ["ED-A"] = new Style { Title = "Welcome home!", Short = "Welcome home", Icon = "check", Face = T.Go },
        ["ED-B"] = new Style { Title = "Home, just barely", Short = "Just barely", Icon = "caution", Face = T.Accent },
        ["ED-C"] = new Style { Title = "Let's try again", Short = "Mission lost", Icon = "earth", Face = T.Plum },   // 静かに：赤にしない
    };

    public static Style For(string ending) => ending != null && Endings.TryGetValue(ending, out var s) ? s : Endings["ED-C"];

    /// <summary>見出しの下の1行。{0} = 事故からの時間（m:ss）、{1} = ルート（A/B）</summary>
    public const string SubHome = "Splashdown {0} after the accident, on route {1}.";
    public const string SubHomeNoRoute = "Splashdown {0} after the accident.";
    /// <summary>ED-C。{0} = 何が尽きたか（CauseName）、{1} = 事故からの時間</summary>
    public const string SubLost = "{0} ran out {1} after the accident.";
    public const string SubLostOther = "The ship could not make it home ({1} after the accident).";   // 電力も酸素も残っていた（燃料が足りなかった）

    public static string CauseName(string cause) =>
        cause == Power ? "The power" : cause == Oxygen ? "The oxygen" : cause == "both" ? "Power and oxygen" : "";

    // ================================================================== 資源のカード（エンディング）
    public const string Oxygen = "oxygen", Power = "power", Temp = "temp", Fuel = "fuel";
    public static readonly string[] HomeCards = { Oxygen, Power, Temp };   // ED-A・ED-B：生還ラインがある3つ（SPEC 8章）
    public static readonly string[] LostCards = { Oxygen, Power };         // ED-C：尽きると終わる2つ
    public const string O2Text = "O2";

    public static string ResourceName(string res) =>
        res == Oxygen ? "Oxygen" : res == Power ? "Power" : res == Temp ? "Temperature" : "Fuel";
    /// <summary>カードの色（【11】の色違いのカード。隣どうしを同じ色にしない。状態の色 go・accent は状態の台に使うので避ける）</summary>
    public static Color ResourceFace(string res) => res == Oxygen ? T.Ink : res == Power ? T.Plum : T.Text;
    public static string ResourceIcon(string res) =>
        res == Power ? "power" : res == Temp ? "temperature" : res == Fuel ? "fuel" : null;   // 酸素は O2 の文字の丸
    public static string ResourceUnit(string res) => res == Temp ? "°C" : "%";
    public static string EndMinKey(string res) =>
        res == Oxygen ? "end_oxygen_min" : res == Power ? "end_power_min" : res == Temp ? "end_temp_min" : "";

    // 状態の1行。{0} = 要る量（生還ライン、単位つき）
    public const string StatusOk = "OK (need {0})";
    public const string StatusLow = "Too low (need {0})";
    public const string StatusOkNoLine = "OK";
    public const string StatusRanOut = "Ran out";
    public const string StatusLeft = "Still left";

    // ================================================================== 判定が厳しくなった理由（strict_reason）。{0} = 厳しくする幅
    public static string StrictText(string reason, bool survived)
    {
        if (reason == "missed_burn")
            return survived ? "You missed the burn window, so you needed {0} more power and oxygen at splashdown."
                            : "You missed the burn window, so the ship took the long way home.";
        if (reason == "reentry_not_ready")
            return survived ? "Re-entry was not ready (heat shield + navigation), so you needed {0} more power and oxygen."
                            : "Re-entry was not ready (heat shield + navigation).";
        return survived ? "The landing check got stricter: you needed {0} more power and oxygen." : "";
    }

    // ================================================================== ボタン・名前タグ
    public const string HoustonTag = "Houston";
    public const string ScoreIcon = "score", ScoreUnit = "pts";   // 得点の数のチップ（トロフィーの台、右下に pts の札。【09】）
    public const string SeeReport = "See the report!";
    public const string SeeReportIcon = "enter";
    public const string FlyAgain = "Fly again!";   // again：設計は前回のまま、全員が設計へ（ui-guidelines 3章）
    public const string FlyAgainIcon = "rocket";

    // ================================================================== 事後レポート：見出し
    public const string ReportTitle = "Mission report", ReportIcon = "rocket";
    public const string GoalsTitle = "Goals", GoalsIcon = "check";
    public const string ScoreTitle = "Score";
    public const string MomentsTitle = "Timeline", MomentsIcon = "clock";
    // パネルの見出しの台の色（隣どうしを同じ色にしない。go・accent は行の ✓/✗ に使うので避ける）
    public static readonly Color GoalsFace = T.Text, ScoreFace = T.Plum, MomentsFace = T.Ink;

    // ================================================================== 事後レポート：要件4つ（SPEC 8章。キーは result.requirements）
    // #190：材料の行（【11】「名前 ---- 0 / 2」）にしたので、名前は短く、数は「いま / 要る量・上限」で右に出す
    public class Req
    {
        public string Key, Label, Hint;      // Hint：✗ のとき右に出す「次はこれ」のアイコン
        public Func<EndingInfo, string> Value;   // 右の数（null なら出さない）
    }

    public static readonly Req[] Reqs =
    {
        new Req { Key = "req1_crew_alive", Label = "Crew home", Hint = "power", Value = null },
        new Req { Key = "req2_science", Label = "Science", Hint = "camera", Value = i => Of(i.Science, EndingParams.Get("req_science_min")) },
        new Req { Key = "req3_mass", Label = "Mass", Hint = "mass", Value = i => Of(i.Mass, i.MassLimit) },
        new Req { Key = "req4_budget", Label = "Budget", Hint = "cost", Value = i => Of(i.Cost, i.Budget) },
    };

    /// <summary>「111 / 140」。上限が分からなければ前だけ</summary>
    static string Of(float v, float limit) => Num(v) + (float.IsNaN(limit) ? "" : " / " + Num(limit));

    /// <summary>Req 1 が ✗ のときのアイコンは、何が尽きたかで変える</summary>
    public static string CrewHint(string cause) => cause == Oxygen ? "valve" : cause == "" ? "ignition" : "power";

    // ================================================================== 事後レポート：スコアの内訳（キーは report.score_breakdown。並びは SPEC 8章の式の順）
    public static readonly string[,] ScoreRows =
    {
        { "ending", "Coming home", "earth" },
        { "science", "Science", "science" },
        { "resources", "Supplies left", "battery" },
        { "teamwork", "Teamwork", "crew" },
        { "budget", "Budget left", "cost" },
        { "all_requirements", "All 4 goals", "check" },
    };

    // ================================================================== 事後レポート：主な出来事（report.commands から選ぶ）
    // 1つの決まりは最初の1回だけ出す。Priority の小さい順に MaxMoments 個まで選び、時刻順に並べる。
    public class Moment
    {
        public string Cmd, Label, Icon;   // 台の色はどれも MomentFace
        public string Show;   // Label の {0} に入れる args のキー（大文字にする）。null なら入れない
        public int Priority;
        public Func<Dictionary<string, object>, bool> Match;   // args（と ok）を見る。null なら cmd だけで決める
    }

    static bool Ok(Dictionary<string, object> c) => J.Bool(c, "ok");
    static bool Arg(Dictionary<string, object> c, string k, string v) => J.Str(J.Obj(c, "args"), k) == v;
    static bool On(Dictionary<string, object> c, bool on) => J.Bool(J.Obj(c, "args"), "on") == on;

    public static readonly Moment[] Moments =
    {
        new Moment { Cmd = "close_valve", Label = "Stopped the leak", Icon = "valve", Priority = 1, Match = c => Ok(c) },
        new Moment { Cmd = "close_valve", Label = "Closed the wrong valve", Icon = "valve", Priority = 4, Match = c => J.Has(c, "ok") && !Ok(c) },
        new Moment { Cmd = "ignite", Label = "Burn for home", Icon = "ignition", Priority = 1, Match = c => Ok(c) },
        // #189（Fable）：「… to save power」はレポートの列（約 300 px）に入らず「po…」と切れたので短く。Lights・Navigation も同じ長さなのでそろえた
        new Moment { Cmd = "switch", Label = "Heater off", Icon = "temperature", Priority = 2, Match = c => Arg(c, "name", "heater") && On(c, false) },
        new Moment { Cmd = "swap_filter", Label = "Swapped the CO2 filter", Icon = "filter", Priority = 2, Match = c => Ok(c) },
        new Moment { Cmd = "switch", Label = "Science instruments on", Icon = "camera", Priority = 2,
                     Match = c => On(c, true) && (Arg(c, "name", "camera") || Arg(c, "name", "spectrometer") || Arg(c, "name", "radar")) },
        new Moment { Cmd = "switch", Label = "Lights off", Icon = "switch", Priority = 3, Match = c => Arg(c, "name", "lights") && On(c, false) },
        new Moment { Cmd = "switch", Label = "Navigation off", Icon = "switch", Priority = 3, Match = c => Arg(c, "name", "nav") && On(c, false) },
        new Moment { Cmd = "set_attitude", Label = "Pointed at the Moon", Icon = "moon", Priority = 3, Match = c => Arg(c, "mode", "moon") },
        new Moment { Cmd = "set_transmit", Label = "Sent data to Earth", Icon = "send", Priority = 3, Match = c => On(c, true) },
        new Moment { Cmd = "set_attitude", Label = "Heat shield forward", Icon = "heat_shield", Priority = 3, Match = c => Arg(c, "mode", "heatshield") },
        new Moment { Cmd = "correction_burn", Label = "Course correction", Icon = "ignition", Priority = 4, Match = c => Ok(c) },
        new Moment { Cmd = "set_route", Label = "Chose route {0}", Icon = "route", Priority = 4, Match = null, Show = "route" },
    };

    // 決まった出来事（コマンドではないもの）
    public const string MomentAccident = "Oxygen tank leak!", MomentAccidentIcon = "alarm";
    public const string MomentSplash = "Splashdown", MomentSplashIcon = "earth";
    public const string MomentLost = "{0} ran out", MomentLostIcon = "caution";
    public const string MomentLostOther = "Could not make it home";
    public const string MomentMissedBurn = "Missed the burn window", MomentMissedBurnIcon = "clock";
    public const string MomentFarSide = "Far side: no signal", MomentFarSideIcon = "comms_lost";
    public const string MomentSignal = "Signal back", MomentSignalIcon = "comms";
    // 出来事の台の色：操作＝plum、事故・逃した窓＝accent（注意）、時刻の目印＝ink、着水＝go、尽きた＝text（ED-C は静かに。赤にしない）
    public static readonly Color MomentFace = T.Plum, MomentAlertFace = T.Accent, MomentTimelineFace = T.Ink, MomentSplashFace = T.Go, MomentLostFace = T.Text;
    public static readonly Color ScoreRowFace = T.Go;   // 内訳の台（0 以下の行は line）
    public const int PriorityFixed = 0, PriorityMissed = 1, PriorityTimeline = 5;   // 時刻の目印は、行が余ったときだけ

    // ================================================================== 事後レポート：ヒューストンのまとめ（2行：よかったこと／次に変えること）
    public const string GoodAll = "All four goals met. That was a great mission!";
    public const string GoodLeak = "Good: you stopped the leak {0} s after the accident.";
    public const string GoodHome = "Good: everyone made it home.";
    public const string GoodLasted = "Good: you kept the crew going for {0}.";
    public const string NextLostPower = "Next time: switch off the heater, lights and navigation early to save power.";
    public const string NextLostOxygen = "Next time: close the leaking valve (Tank 2) as soon as the alarm sounds.";
    public const string NextMissedBurn = "Next time: burn for home while the burn window is open.";
    public const string NextReentry = "Next time: before re-entry, turn the heat shield forward and navigation on.";
    public const string NextLowOxygen = "Next time: close the leak sooner to keep more oxygen.";
    public const string NextLowPower = "Next time: switch off more to save power for the trip home.";
    public const string NextLowTemp = "Next time: turn the heater on for a while so the cabin stays warm.";
    public const string NextScience = "Next time: carry an instrument and point it at the Moon on the far side.";
    public const string NextMore = "Next time: send more data home and keep more power for a higher score.";

    /// <summary>何がよくて、何が足りなかったか（1行ずつ）。上の決まりの最初に当たったもの</summary>
    public static string Takeaway(EndingInfo i)
    {
        float leak = i.LeakStoppedAt();
        string good = i.Survived && AllPass(i) ? GoodAll
                    : leak >= 0f ? string.Format(GoodLeak, Num(leak))
                    : i.Survived ? GoodHome
                    : string.Format(GoodLasted, Ui.Mmss(i.TEnd));
        string cause = i.Cause();
        string next = !i.Survived && cause == Power ? NextLostPower
                    : !i.Survived && cause == Oxygen ? NextLostOxygen
                    : i.StrictReason == "missed_burn" || !i.Survived ? NextMissedBurn
                    : i.StrictReason == "reentry_not_ready" ? NextReentry
                    : i.Below(Oxygen) ? NextLowOxygen
                    : i.Below(Power) ? NextLowPower
                    : i.Below(Temp) ? NextLowTemp
                    : !i.Pass("req2_science") ? NextScience
                    : NextMore;
        return good + "\n" + next;
    }

    // ================================================================== 事後レポート：本物のアポロ13号（#89 で増やす。今は #107 で決まった1つだけ）
    public const string ApolloTag = "Real Apollo 13";
    public static readonly string[] ApolloFacts =
    {
        // #107（Taiga、2026-11-14）。出典 docs/reference/apollo13-data.md 9章（RB p.149-152、MR p.64）
        "In the real mission, closing the valves did NOT stop the leak. In this game, the right valve does stop it.",
    };

    // ================================================================== 台詞
    /// <summary>EndingFeed が拾う台詞の id（結末とレポート）</summary>
    public static bool IsEndingLine(string id) => id == "ED-A" || id == "ED-B" || id == "ED-C" || id == "R01";

    /// <summary>台本の「(pause)」は読み上げの間なので、字幕には出さない（StoryDirector.CleanLine と同じ）</summary>
    public static string Clean(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("(pause) ", "").Replace("(pause)", "");

    // ================================================================== 数字の書き方
    /// <summary>小数1桁まで（1281.6・100）。内訳の合計がスコアと合うように、丸めすぎない</summary>
    public static string Num(float v) => float.IsNaN(v) ? "?" : v.ToString("0.#", CultureInfo.InvariantCulture);
    public static string Plus(float v) => (v > 0f ? "+" : "") + Num(v);
    /// <summary>SCORE と内訳だけ整数（#189：1281.6 → 1282。四捨五入）。内訳はそれぞれ丸めるので、足すと合計と 1〜2 ずれることがある</summary>
    public static string Score(float v) => float.IsNaN(v) ? "?" : Round(v).ToString(CultureInfo.InvariantCulture);
    public static string ScorePlus(float v)
    {
        if (float.IsNaN(v)) return "?";
        int n = Round(v);
        return (n > 0 ? "+" : "") + n.ToString(CultureInfo.InvariantCulture);
    }
    static int Round(float v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);

    static bool AllPass(EndingInfo i)
    {
        foreach (var r in Reqs) if (!i.Pass(r.Key)) return false;
        return true;
    }
}
