// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// エンディング（#85）と事後レポート（#86）の文言・アイコン・並びの表。直すときはこのファイルだけ見ればよい（Fable の採点・Taiga の文言）。
// 調子：帰還の演出（ReturnStage の SPLASHDOWN!・LostStage の Mission lost）と合わせる。ED-C は責めず静かに、「もう一度考えよう」で終える（赤は使わない）。
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
        public string Kicker, Title, Short, Icon;
        public Color Face, OnFace;   // 見出しの左の丸（と、レポートの結末のチップ）の面と、その上の色
    }

    public static readonly Dictionary<string, Style> Endings = new Dictionary<string, Style>
    {
        ["ED-A"] = new Style { Kicker = "SPLASHDOWN!", Title = "WELCOME HOME", Short = "WELCOME HOME", Icon = "check", Face = T.Go, OnFace = T.Panel },
        ["ED-B"] = new Style { Kicker = "SPLASHDOWN!", Title = "HOME, JUST BARELY", Short = "JUST BARELY", Icon = "caution", Face = T.Accent, OnFace = T.Ink },
        ["ED-C"] = new Style { Kicker = "MISSION LOST", Title = "LET'S TRY AGAIN", Short = "MISSION LOST", Icon = "earth", Face = T.Ink, OnFace = T.Panel },
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
        res == Oxygen ? "OXYGEN" : res == Power ? "POWER" : res == Temp ? "TEMPERATURE" : "FUEL";
    public static string ResourceIcon(string res) =>
        res == Power ? "power" : res == Temp ? "temperature" : res == Fuel ? "fuel" : null;   // 酸素は O2 の文字の丸
    public static string ResourceUnit(string res) => res == Temp ? "°C" : "%";
    public static string EndMinKey(string res) =>
        res == Oxygen ? "end_oxygen_min" : res == Power ? "end_power_min" : res == Temp ? "end_temp_min" : "";

    // 状態の1行。{0} = 要る量（生還ライン、単位つき）
    public const string StatusOk = "OK  (need {0})";
    public const string StatusLow = "TOO LOW  (need {0})";
    public const string StatusOkNoLine = "OK";
    public const string StatusRanOut = "RAN OUT";
    public const string StatusLeft = "STILL LEFT";

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
    public const string HoustonTag = "HOUSTON";
    public const string ScoreLabel = "SCORE";
    public const string SeeReport = "SEE THE REPORT";
    public const string SeeReportIcon = "enter";
    public const string FlyAgain = "REDESIGN AND FLY AGAIN";
    public const string FlyAgainIcon = "rocket";

    // ================================================================== 事後レポート：見出し
    public const string ReportTitle = "MISSION REPORT";
    public const string GoalsTitle = "GOALS", GoalsIcon = "check";
    public const string ScoreTitle = "SCORE", ScoreIcon = "score";
    public const string MomentsTitle = "TIMELINE", MomentsIcon = "clock";

    // ================================================================== 事後レポート：要件4つ（SPEC 8章。キーは result.requirements）
    public class Req
    {
        public string Key, Label, Hint;      // Hint：✗ のとき横に出す「次はこれ」のアイコン
        public Func<EndingInfo, string> Detail;   // 名前の下の細かい数字（null なら出さない）
        public bool DetailWhenPass;               // ✓ のときも数字を出すか（パネルの高さが足りないので、出すのは科学だけ）
    }

    public static readonly Req[] Reqs =
    {
        new Req { Key = "req1_crew_alive", Label = "Bring the crew home", Hint = "power", Detail = null },
        new Req { Key = "req2_science", Label = "Bring back science", Hint = "camera", DetailWhenPass = true,
                  Detail = i => Of("{0} science", i.Science, "need", EndingParams.Get("req_science_min")) },
        new Req { Key = "req3_mass", Label = "Light enough to launch", Hint = "mass",
                  Detail = i => Of("Mass {0}", i.Mass, "max", i.MassLimit) },
        new Req { Key = "req4_budget", Label = "Within the budget", Hint = "cost",
                  Detail = i => Of("Cost {0}", i.Cost, "max", i.Budget) },
    };

    /// <summary>「Mass 111  (max 140)」。上限が分からなければ前半だけ</summary>
    static string Of(string fmt, float v, string word, float limit) =>
        string.Format(fmt, Num(v)) + (float.IsNaN(limit) ? "" : "  (" + word + " " + Num(limit) + ")");

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
        public string Cmd, Label, Icon;
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
        new Moment { Cmd = "switch", Label = "Heater off to save power", Icon = "temperature", Priority = 2, Match = c => Arg(c, "name", "heater") && On(c, false) },
        new Moment { Cmd = "swap_filter", Label = "Swapped the CO2 filter", Icon = "filter", Priority = 2, Match = c => Ok(c) },
        new Moment { Cmd = "switch", Label = "Science instruments on", Icon = "camera", Priority = 2,
                     Match = c => On(c, true) && (Arg(c, "name", "camera") || Arg(c, "name", "spectrometer") || Arg(c, "name", "radar")) },
        new Moment { Cmd = "switch", Label = "Lights off to save power", Icon = "switch", Priority = 3, Match = c => Arg(c, "name", "lights") && On(c, false) },
        new Moment { Cmd = "switch", Label = "Navigation off to save power", Icon = "switch", Priority = 3, Match = c => Arg(c, "name", "nav") && On(c, false) },
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
    public const string ApolloTag = "REAL APOLLO 13";
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

    static bool AllPass(EndingInfo i)
    {
        foreach (var r in Reqs) if (!i.Pass(r.Key)) return false;
        return true;
    }
}
