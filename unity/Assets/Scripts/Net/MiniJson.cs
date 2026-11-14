// 書いた人：Claude ／ 最終更新：2026-11-14
// 小さな JSON の読み書き。object → Dictionary<string,object>、array → List<object>、数 → double、true/false → bool、null → null。
// JsonUtility は辞書を読めない（sw や observed_s がある）ので自前。依存パッケージなし。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public static class MiniJson
{
    public static object Parse(string json)
    {
        int i = 0;
        var v = ReadValue(json, ref i);
        return v;
    }

    public static Dictionary<string, object> ParseObject(string json) => Parse(json) as Dictionary<string, object>;

    static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

    static object ReadValue(string s, ref int i)
    {
        Ws(s, ref i);
        if (i >= s.Length) throw new FormatException("JSON が途中で終わった");
        char c = s[i];
        if (c == '{')
        {
            var d = new Dictionary<string, object>();
            i++; Ws(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true)
            {
                Ws(s, ref i);
                string k = ReadString(s, ref i);
                Ws(s, ref i);
                if (s[i++] != ':') throw new FormatException("':' がない @" + i);
                d[k] = ReadValue(s, ref i);
                Ws(s, ref i);
                char n = s[i++];
                if (n == '}') return d;
                if (n != ',') throw new FormatException("',' か '}' がない @" + i);
            }
        }
        if (c == '[')
        {
            var l = new List<object>();
            i++; Ws(s, ref i);
            if (s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(ReadValue(s, ref i));
                Ws(s, ref i);
                char n = s[i++];
                if (n == ']') return l;
                if (n != ',') throw new FormatException("',' か ']' がない @" + i);
            }
        }
        if (c == '"') return ReadString(s, ref i);
        if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
        if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
        if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
        if (string.CompareOrdinal(s, i, "NaN", 0, 3) == 0) { i += 3; return double.NaN; }
        int st = i;
        while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
        if (st == i) throw new FormatException("読めない文字 '" + c + "' @" + i);
        return double.Parse(s.Substring(st, i - st), CultureInfo.InvariantCulture);
    }

    static string ReadString(string s, ref int i)
    {
        if (s[i] != '"') throw new FormatException("文字列が '\"' で始まらない @" + i);
        i++;
        var sb = new StringBuilder();
        while (true)
        {
            char c = s[i++];
            if (c == '"') break;
            if (c != '\\') { sb.Append(c); continue; }
            char e = s[i++];
            switch (e)
            {
                case 'n': sb.Append('\n'); break;
                case 't': sb.Append('\t'); break;
                case 'r': sb.Append('\r'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                default: sb.Append(e); break;
            }
        }
        return sb.ToString();
    }

    public static string Serialize(object o)
    {
        var sb = new StringBuilder();
        Write(sb, o);
        return sb.ToString();
    }

    static void Write(StringBuilder sb, object o)
    {
        switch (o)
        {
            case null: sb.Append("null"); break;
            case string str: WriteString(sb, str); break;
            case bool b: sb.Append(b ? "true" : "false"); break;
            case int n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
            case long n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
            case float f: sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); break;
            case double dd: sb.Append(dd.ToString("R", CultureInfo.InvariantCulture)); break;
            case IDictionary<string, object> d:
                sb.Append('{');
                bool first = true;
                foreach (var kv in d)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteString(sb, kv.Key); sb.Append(':'); Write(sb, kv.Value);
                }
                sb.Append('}');
                break;
            case IEnumerable list:
                sb.Append('[');
                bool f1 = true;
                foreach (var x in list) { if (!f1) sb.Append(','); f1 = false; Write(sb, x); }
                sb.Append(']');
                break;
            default: WriteString(sb, o.ToString()); break;
        }
    }

    static void WriteString(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }
}

/// <summary>辞書から型つきで取り出す小道具（無ければ既定値）</summary>
public static class J
{
    public static bool Has(Dictionary<string, object> d, string k) => d != null && d.ContainsKey(k) && d[k] != null;
    public static string Str(Dictionary<string, object> d, string k, string def = "") => Has(d, k) ? d[k].ToString() : def;
    public static float Num(Dictionary<string, object> d, string k, float def = 0f) => Has(d, k) && d[k] is double x ? (float)x : def;
    public static int Int(Dictionary<string, object> d, string k, int def = 0) => Has(d, k) && d[k] is double x ? (int)Math.Round(x) : def;
    public static bool Bool(Dictionary<string, object> d, string k, bool def = false) => Has(d, k) && d[k] is bool b ? b : def;
    public static Dictionary<string, object> Obj(Dictionary<string, object> d, string k) => Has(d, k) ? d[k] as Dictionary<string, object> : null;
    public static List<object> Arr(Dictionary<string, object> d, string k) => Has(d, k) ? d[k] as List<object> : null;
}
