// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-15、#168 3D の設計の画面：タブ・お財布・打ち上げチェック・吹き出しの文。#190 大文字をやめた）
// 設計の画面（#73・#168）に出る英語の文言と、部品ごとのアイコン・タブの分け方。文言を直すときはここだけ触る（Taiga・Fable の見直し用）。
// 数字は書かない：{key} の所に parameters.json・parts.json・サーバーの totals の値が入る（DesignScreen の DesignValues）。
//   {power_per_battery} など＝parameters.json の key、{rocket_small_mass}・{budget} など＝parts.json の limits、
//   {mass.battery}・{cost.rocket_large}＝parts.json の部品の質量・費用、{mass}・{mass_limit}・{cost}・{capacity}・{oxygen}・{fuel}＝サーバーの totals、{rocket}＝いま選んでいるロケットの名前
// 部品の説明は parts.json の benefit を使わない：#136 の前の数字のまま（例：カメラ 10 data/min → 今は 15）で、高校生には長いため
// #190：大文字だけの言葉にしない（文の頭と固有名詞だけ大文字。ボタンは文末に !）。ui-guidelines 2章
// #168：説明の文は画面に常に出さず、ヒューストンの吹き出しで言う（タブを開いたとき・部品が付いたとき。超えた瞬間はサーバーの H01・H02）
using System.Collections.Generic;

public class DesignStatCopy
{
    public string Icon, Text;   // Icon が "O2" なら O₂ の丸（Ionicons に酸素が無い。ui-guidelines 4章）
    public DesignStatCopy(string icon, string text) { Icon = icon; Text = text; }
}

public class DesignPartCopy
{
    public string Id, Name, Icon, Science;   // Science：観測機器なら科学の値（{sci_camera} など）。それ以外は null。効き目は吹き出しで言う（PartSay）
    public DesignPartCopy(string id, string name, string icon, string science = null) { Id = id; Name = name; Icon = icon; Science = science; }
}

public class DesignChoiceCopy
{
    public string Key, Name, Line;     // Key：サーバーに送る名前（small / large、free_return / science）。Line：選んだとき吹き出しで言う1行
    public DesignStatCopy[] Stats;
    public DesignChoiceCopy(string key, string name, string line, params DesignStatCopy[] stats) { Key = key; Name = name; Line = line; Stats = stats; }
}

/// <summary>右の棚のタブ。Parts の部品のカードを出す（FLIGHT は部品の代わりにロケットと軌道）。Say はタブを開いたとき吹き出しで言う1行</summary>
public class DesignTabCopy
{
    public string Key, Name, Icon, Say;
    public string[] Parts;
    public DesignTabCopy(string key, string name, string icon, string say, params string[] parts) { Key = key; Name = name; Icon = icon; Say = say; Parts = parts; }
}

/// <summary>打ち上げチェックの1行（SPEC 8章の Req 1〜4）</summary>
public class DesignCheckCopy
{
    public string Key, Text;
    public DesignCheckCopy(string key, string text) { Key = key; Text = text; }
}

public static class DesignCopy
{
    // ------------------------------------------------------------------ 左上の小見出し
    public const string Title = "Build your ship";

    // ------------------------------------------------------------------ 右の棚のタブ（この順に並べる。FLIGHT を開くとカメラが引いてロケットの全身を見せる）
    public const string FlightTab = "flight";
    public static readonly DesignTabCopy[] Tabs =
    {
        new DesignTabCopy("power", "Power", "power", "Batteries are your power. Each one adds {power_per_battery}%.", "battery", "solar_panel"),
        new DesignTabCopy("air", "Air & fuel", "fuel", "Spare tanks give you more oxygen and fuel.", "spare_o2_tank", "extra_fuel_tank"),
        new DesignTabCopy("science", "Science", "science", "Tools earn science. You need {req_science_min} points.", "camera", "spectrometer", "radar", "high_gain_antenna"),
        new DesignTabCopy(FlightTab, "Flight", "rocket", "A bigger rocket lifts more, but costs {cost.rocket_large} more."),
    };

    // ------------------------------------------------------------------ 材料の行（名前・アイコン。parts.json にあってどのタブにも無い部品は出さない）
    // 名前は Nunito 30 で約 190px まで（− ＋ の行。トグルの行は約 300px）。長いとはみ出す
    public static readonly DesignPartCopy[] Parts =
    {
        new DesignPartCopy("battery", "Battery", "battery"),
        new DesignPartCopy("solar_panel", "Solar panel", "sun"),
        new DesignPartCopy("spare_o2_tank", "Spare O₂ tank", "O2"),
        new DesignPartCopy("extra_fuel_tank", "Extra fuel", "fuel"),
        new DesignPartCopy("camera", "Camera", "camera", "{sci_camera}"),
        new DesignPartCopy("spectrometer", "Spectrometer", "spectrometer", "{sci_spectrometer}"),
        new DesignPartCopy("radar", "Radar", "radar", "{sci_radar}"),
        new DesignPartCopy("high_gain_antenna", "Big antenna", "comms"),
    };

    // ------------------------------------------------------------------ Flight のタブ（選択肢の吹き出し2つ。言葉だけで分かるので見出しは付けない）
    public static readonly DesignChoiceCopy[] Rockets =
    {
        new DesignChoiceCopy("small", "Small rocket", null, new DesignStatCopy("mass", "{rocket_small_mass}"), new DesignStatCopy("cost", "+{cost.rocket_small}")),
        new DesignChoiceCopy("large", "Large rocket", null, new DesignStatCopy("mass", "{rocket_large_mass}"), new DesignStatCopy("cost", "+{cost.rocket_large}")),
    };

    public static readonly DesignChoiceCopy[] Paths =
    {
        // 自由帰還でも帰りの点火（ignite）は要る。要らないのは軌道修正（correction_burn）だけ（SPEC 6・7章）
        new DesignChoiceCopy("free_return", "Free-return", "Free-return: safe and simple.", new DesignStatCopy("science", "x1")),
        new DesignChoiceCopy("science", "Science path", "Science path: more science, but it costs {fuel_correction_burn}% fuel.",
            new DesignStatCopy("science", "x{sci_trajectory_mult}"), new DesignStatCopy("fuel", "-{fuel_correction_burn}%")),
    };

    // ------------------------------------------------------------------ 右上：お財布と重さのピル（残りはサーバーの totals の budget_left・mass_left）
    public const string Left = "left", Over = "over";
    // 古い記録の再生などで budget_left・mass_left が無いとき
    public const string CostFallback = "{cost} / {budget}", MassFallback = "{mass} / {mass_limit}";

    // ------------------------------------------------------------------ 打ち上げチェック（3・4 は設計で決まる。1・2 は飛んでから決まるので点線の丸）
    public const string CheckTitle = "Launch check";
    public const string InFlightTitle = "In flight";
    public static readonly DesignCheckCopy CheckMass = new DesignCheckCopy("mass", "Light enough");
    public static readonly DesignCheckCopy CheckCost = new DesignCheckCopy("cost", "In budget");
    public static readonly DesignCheckCopy CheckCrew = new DesignCheckCopy("crew", "Crew home");
    public static readonly DesignCheckCopy CheckScience = new DesignCheckCopy("science", "Science {req_science_min}+");

    // ------------------------------------------------------------------ ヒューストンの吹き出し（id は声のファイル名。無ければ字幕だけ）
    // 部品が付いたとき（効き目とはじめの量は常には出さず、ここで言う）
    public static readonly Dictionary<string, string> PartSay = new Dictionary<string, string>
    {
        ["battery"] = "Each battery adds {power_per_battery}%. Now {capacity}% power.",
        ["solar_panel"] = "Solar panels make power, but only while you face the Sun.",
        ["spare_o2_tank"] = "Each spare tank adds {oxygen_per_spare_tank}%. Now {oxygen}% oxygen.",
        ["extra_fuel_tank"] = "Each extra tank adds {fuel_per_extra_tank}%. Now {fuel}% fuel.",
        ["camera"] = "The camera photographs the far side. Science +{sci_camera}.",
        ["spectrometer"] = "The spectrometer studies Moon rocks. Science +{sci_spectrometer}.",
        ["radar"] = "The radar looks under the surface. Science +{sci_radar}.",
        ["high_gain_antenna"] = "The big antenna sends your data home faster.",
    };
    // 観測機器が1つも無いまま FLIGHT を開いたとき（打ち上げを押す前）
    public const string NoScience = "No science tools yet. Add one in Science to earn points.";

    // 超えたときの直し方（NO GO のカード）：いま選んでいるロケットごと（大きいロケットは重くても飛べるが高い）。
    // 超えた瞬間の吹き出しはサーバーの H01・H02（声つき）が言う
    public static readonly Dictionary<string, string> MassHint = new Dictionary<string, string>
    {
        ["small"] = "Take parts off, or pick the Large rocket.",
        ["large"] = "Take some parts off.",
    };
    public static readonly Dictionary<string, string> BudgetHint = new Dictionary<string, string>
    {
        ["small"] = "Take some parts off.",
        ["large"] = "Take parts off, or pick the Small rocket.",
    };

    public const string Launch = "Ready for launch!";

    // 打ち上げを押して、だめだったとき（M02）
    public const string NoGo = "No go";
    public const string NoGoMass = "Too heavy for the {rocket} rocket";
    public const string NoGoBudget = "Over budget";
    public const string NoGoMassDetail = "Mass {mass} is more than the limit of {mass_limit}.";
    public const string NoGoBudgetDetail = "Cost {cost} is more than the budget of {budget}.";
    public const string NoGoBack = "Fix the ship!";

    public static string Hint(Dictionary<string, string> table, string rocket) =>
        rocket != null && table.TryGetValue(rocket, out var s) ? s : table["small"];
}
