// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-15、#190 数字は accent・固有名詞は go）
// ヒューストンの台詞のデータと、字幕にする前の下ごしらえ（#75）。
//   HoustonLine           台詞1本（id・話し手・文）。サーバーの houston イベントも、ブリーフィングの1行も同じ形
//   HoustonScript.Parse   台詞の列の JSON を読む。形は [{"id":"B01","speaker":"houston","text":"..."}]（#152 の briefing.json）
//                         {"lines":[...]} で包んであっても読む。text が無ければ line（houston_lines.json の書き方）を読む
//   HoustonScript.Clean   「(pause)」などの読み上げ用の印を消す
//   HoustonScript.Pages   長い台詞を文の切れ目で分ける（1ページ＝吹き出しの3行ほど）
//   HoustonScript.Mark    固有名詞と数字だけ色文字にする（TextMeshPro のリッチテキスト）
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

[System.Serializable]
public class HoustonLine
{
    public string id = "", speaker = "houston", text = "", priority = "";

    public HoustonLine() { }
    public HoustonLine(string id, string text, string priority = "", string speaker = "houston")
    { this.id = id ?? ""; this.text = text ?? ""; this.priority = priority ?? ""; this.speaker = string.IsNullOrEmpty(speaker) ? "houston" : speaker; }
}

public static class HoustonScript
{
    /// <summary>ブリーフィングの台詞の置き場所（#152 が作る）。ビルドでは GameFlow などが TextAsset で渡す。Editor ではここから直接読める</summary>
    public const string BriefingPath = "Assets/Data/Story/briefing.json";

    // ================================================================== 読む
    /// <summary>台詞の列の JSON を読む。読めない・空なら空のリスト（エラーにしない）</summary>
    public static List<HoustonLine> Parse(string json)
    {
        var r = new List<HoustonLine>();
        if (string.IsNullOrWhiteSpace(json)) return r;
        object root;
        try { root = MiniJson.Parse(json); }
        catch (System.Exception e) { Debug.LogWarning("[Houston] 台詞の JSON が読めない: " + e.Message); return r; }
        var list = root as List<object>;
        if (list == null && root is Dictionary<string, object> d) list = J.Arr(d, "lines");
        if (list == null) return r;
        foreach (var o in list)
        {
            if (!(o is Dictionary<string, object> m)) continue;
            string text = J.Str(m, "text");
            if (text == "") text = J.Str(m, "line");
            if (text == "") continue;
            r.Add(new HoustonLine(J.Str(m, "id"), text, J.Str(m, "priority"), J.Str(m, "speaker", "houston")));
        }
        return r;
    }

    public static List<HoustonLine> Load(TextAsset ta) => ta != null ? Parse(ta.text) : new List<HoustonLine>();

    /// <summary>ブリーフィングを読む。ta が無ければ、Editor では BriefingPath から読む（ビルドでは空）。ファイルが無ければ空</summary>
    public static List<HoustonLine> LoadBriefing(TextAsset ta = null)
    {
#if UNITY_EDITOR
        if (ta == null) ta = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(BriefingPath);
#endif
        return Load(ta);
    }

    // ================================================================== 下ごしらえ
    static readonly Regex Spaces = new Regex(@"\s{2,}");

    public static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        foreach (var tok in HoustonStyle.StripTokens) s = s.Replace(tok, " ");
        s = s.Replace("<", "(").Replace(">", ")");   // 台詞の中の < > をリッチテキストのタグにしない
        return Spaces.Replace(s, " ").Trim();
    }

    static readonly Regex SentenceEnd = new Regex(@"(?<=[.!?…])\s+");

    /// <summary>文の切れ目で、1ページ budget 文字ほどに分ける。1文が長すぎるときは語の切れ目で分ける</summary>
    public static List<string> Pages(string text, int budget)
    {
        var pages = new List<string>();
        text = Clean(text);
        if (text == "") return pages;
        budget = Mathf.Max(20, budget);
        var sb = new StringBuilder();
        foreach (var sentence in SentenceEnd.Split(text))
        {
            if (sentence.Length == 0) continue;
            foreach (var piece in Chop(sentence, budget))
            {
                if (sb.Length > 0 && sb.Length + 1 + piece.Length > budget) { pages.Add(sb.ToString()); sb.Clear(); }
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(piece);
            }
        }
        if (sb.Length > 0) pages.Add(sb.ToString());
        return pages;
    }

    /// <summary>1文が budget を超えるとき、語の切れ目で分ける（なるべく「, 」「: 」の後ろで切る）</summary>
    static IEnumerable<string> Chop(string s, int budget)
    {
        while (s.Length > budget)
        {
            int cut = -1;
            for (int i = budget; i > budget / 2; i--)
                if (i < s.Length && s[i] == ' ' && (s[i - 1] == ',' || s[i - 1] == ':' || s[i - 1] == ';')) { cut = i; break; }
            if (cut < 0) cut = s.LastIndexOf(' ', budget);
            if (cut <= 0) cut = budget;
            yield return s.Substring(0, cut).Trim();
            s = s.Substring(cut).Trim();
        }
        if (s.Length > 0) yield return s;
    }

    // ================================================================== 色文字
    static Regex marker;
    static Regex Marker
    {
        get
        {
            if (marker != null) return marker;
            var words = new List<string>(HoustonStyle.ProperNouns);
            words.Sort((a, b) => b.Length.CompareTo(a.Length));   // 長いものから（「Mission Control」を「Mission」より先に）
            var alt = new StringBuilder();
            foreach (var w in words) { if (alt.Length > 0) alt.Append('|'); alt.Append(Regex.Escape(w)); }
            // 固有名詞 ｜ 数字（小数・%・℃ も） ｜ ルートの A・B（「A:」「B:」「A or B?」の形だけ。冠詞の a には付けない）
            marker = new Regex(@"\b(?:" + alt + @")\b|\b\d+(?:\.\d+)?(?:\s?%|°C)?|(?-i:\b[AB](?=:|\?|\s+or\b))", RegexOptions.IgnoreCase);
            return marker;
        }
    }

    /// <summary>固有名詞と数字を色文字にした TextMeshPro のリッチテキスト（どちらも同じ色）</summary>
    public static string Mark(string page, Color c) => Mark(page, c, c);

    /// <summary>固有名詞は noun、数字は number の色にする（#190。ui-guidelines 3章「会話の吹き出し」【03】：固有名詞は go、数字は accent）</summary>
    public static string Mark(string page, Color noun, Color number)
    {
        if (string.IsNullOrEmpty(page)) return "";
        string nh = ColorUtility.ToHtmlStringRGB(noun), dh = ColorUtility.ToHtmlStringRGB(number);
        return Marker.Replace(page, m => "<color=#" + (char.IsDigit(m.Value[0]) ? dh : nh) + ">" + m.Value + "</color>");
    }

    /// <summary>読む時間（秒）の目安</summary>
    public static float ReadSeconds(string page) =>
        Mathf.Max(HoustonStyle.PageMinS, HoustonStyle.PageBaseS + (page?.Length ?? 0) * HoustonStyle.PagePerCharS);
}
