// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// 設計の画面（#73）に出る英語の文言と、部品ごとのアイコン・並び順。文言を直すときはここだけ触る（Taiga・Fable の見直し用）。
// 数字は書かない：{key} の所に parameters.json・parts.json・サーバーの totals の値が入る（DesignScreen の Values）。
//   {power_per_battery} など＝parameters.json の key、{rocket_small_mass}・{budget} など＝parts.json の limits、
//   {mass.battery}・{cost.rocket_large}＝parts.json の部品の質量・費用、{mass}・{mass_limit}・{cost}・{capacity}・{oxygen}・{fuel}＝サーバーの totals、{rocket}＝いま選んでいるロケットの名前
// 部品の説明は parts.json の benefit を使わない：#136 の前の数字のまま（例：カメラ 10 data/min → 今は 15）で、高校生には長いため
using System.Collections.Generic;

public class DesignStatCopy
{
    public string Icon, Text;   // Icon が "O2" なら O₂ の丸（Ionicons に酸素が無い。ui-guidelines 4章）
    public DesignStatCopy(string icon, string text) { Icon = icon; Text = text; }
}

public class DesignPartCopy
{
    public string Id, Name, Icon, Line, Science;   // Science：観測機器なら科学の値（{sci_camera} など）。それ以外は null
    public DesignPartCopy(string id, string name, string icon, string line, string science = null) { Id = id; Name = name; Icon = icon; Line = line; Science = science; }
}

public class DesignChoiceCopy
{
    public string Key, Name, Line;     // Key：サーバーに送る名前（small / large、free_return / science）。Line：選んだときに下に出す1行
    public DesignStatCopy[] Stats;
    public DesignChoiceCopy(string key, string name, string line, params DesignStatCopy[] stats) { Key = key; Name = name; Line = line; Stats = stats; }
}

public class DesignGoalCopy
{
    public string Number, Icon, Text;
    public DesignGoalCopy(string number, string icon, string text) { Number = number; Icon = icon; Text = text; }
}

public static class DesignCopy
{
    // ------------------------------------------------------------------ 見出し・要件
    public const string Title = "BUILD YOUR SHIP";
    public const string Subtitle = "";   // #146（Fable）：文字を減らした（題・要件のチップ・右の MASS／COST で足りる）
    public const string GoalsLabel = "GOALS";

    // SPEC 8章の Req 1〜4。3・4 は設計で決まる（サーバーの判定で ✓・✗ が付く）。1・2 は飛んでから決まる
    public static readonly DesignGoalCopy[] Goals =
    {
        new DesignGoalCopy("1", "crew", "Crew home"),
        new DesignGoalCopy("2", "science", "Science {req_science_min}+"),
        new DesignGoalCopy("3", "mass", "Light enough"),
        new DesignGoalCopy("4", "cost", "In budget"),
    };

    // ------------------------------------------------------------------ 部品（この順に 2列で並べる。parts.json にあってここに無い部品は後ろに足す）
    public const string PartsHint = "";
    public const string BodyNote = "Ship body";

    public static readonly DesignPartCopy[] Parts =
    {
        new DesignPartCopy("battery", "Battery", "battery", "+{power_per_battery}% power each"),
        new DesignPartCopy("solar_panel", "Solar panel", "sun", "Power from sunlight"),
        new DesignPartCopy("spare_o2_tank", "Spare O₂ tank", "O2", "+{oxygen_per_spare_tank}% oxygen each"),
        new DesignPartCopy("extra_fuel_tank", "Extra fuel", "fuel", "+{fuel_per_extra_tank}% fuel each"),
        new DesignPartCopy("camera", "Camera", "camera", "Photos the far side", "{sci_camera}"),
        new DesignPartCopy("spectrometer", "Spectrometer", "spectrometer", "Studies Moon rocks", "{sci_spectrometer}"),
        new DesignPartCopy("radar", "Radar", "radar", "Sees underground", "{sci_radar}"),
        new DesignPartCopy("high_gain_antenna", "Big antenna", "comms", "Sends data home faster"),
    };

    // ------------------------------------------------------------------ 右の列
    public const string MassLabel = "MASS", CostLabel = "COST";
    public const string MassValue = "{mass} / {mass_limit}", CostValue = "{cost} / {budget}";

    public const string RocketLabel = "ROCKET";
    public static readonly DesignChoiceCopy[] Rockets =
    {
        new DesignChoiceCopy("small", "Small", null, new DesignStatCopy("mass", "{rocket_small_mass}"), new DesignStatCopy("cost", "+{cost.rocket_small}")),
        new DesignChoiceCopy("large", "Large", null, new DesignStatCopy("mass", "{rocket_large_mass}"), new DesignStatCopy("cost", "+{cost.rocket_large}")),
    };

    public const string SuppliesLabel = "YOU START WITH";
    public static readonly DesignStatCopy[] Supplies =
    {
        new DesignStatCopy("power", "{capacity}%"),
        new DesignStatCopy("O2", "{oxygen}%"),
        new DesignStatCopy("fuel", "{fuel}%"),
    };

    public const string PathLabel = "PATH AROUND THE MOON";
    public static readonly DesignChoiceCopy[] Paths =
    {
        // 自由帰還でも帰りの点火（ignite）は要る。要らないのは軌道修正（correction_burn）だけ（SPEC 6・7章）
        new DesignChoiceCopy("free_return", "Free-return", "Safe and simple.", new DesignStatCopy("science", "x1")),
        new DesignChoiceCopy("science", "Science", "More science. Costs fuel.",
            new DesignStatCopy("science", "x{sci_trajectory_mult}"), new DesignStatCopy("fuel", "-{fuel_correction_burn}%")),
    };

    // ------------------------------------------------------------------ 判定（サーバーの totals.status と launch の結果）
    public const string OkTitle = "GOOD TO GO";
    public const string OkHint = "";
    public const string MassTitle = "TOO HEAVY";
    public const string BudgetTitle = "OVER BUDGET";

    // 直し方：いま選んでいるロケットごと（大きいロケットは重くても飛べるが高い）
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

    public const string Launch = "READY FOR LAUNCH";

    // 打ち上げを押して、だめだったとき（M02）
    public const string NoGo = "NO GO";
    public const string NoGoMass = "Too heavy for the {rocket} rocket";
    public const string NoGoBudget = "Over budget";
    public const string NoGoMassDetail = "Mass {mass} is more than the limit of {mass_limit}.";
    public const string NoGoBudgetDetail = "Cost {cost} is more than the budget of {budget}.";
    public const string NoGoBack = "FIX THE SHIP";

    public static string Hint(Dictionary<string, string> table, string rocket) =>
        rocket != null && table.TryGetValue(rocket, out var s) ? s : table["small"];
}
