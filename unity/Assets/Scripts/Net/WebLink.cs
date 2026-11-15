// 書いた人：Taiga（Claude） ／ 最終更新：2026-11-15（#222）
// つなぐ先のサーバー（WebSocket の URL）を決める。上から順に、最初に当たったもの：
//   1. ブラウザで開いたページの ?server=…
//        ?server=wss://xxxx.trycloudflare.com   … そのまま
//        ?server=xxxx.trycloudflare.com         … wss:// を補う（localhost・127.0.0.1 は ws://）
//   2. ブラウザ（WebGL）で https のページ … 同じホストの /api/ws（Vercel にサーバーとビルドを一緒に置く。tools/vercel/README.md）
//   3. エディタ以外のビルド（iOS など）… Resources/server_url.txt の1行（例 wss://<vercel の URL>/api/ws）
//   4. 渡された既定（ws://localhost:8765）
// https のページからは wss:// にしかつなげない（ブラウザの決まり）。
using System;
using UnityEngine;

public static class WebLink
{
    public const string SocketPath = "/api/ws";          // server/app.py の入口
    public const string ConfigResource = "server_url";   // Assets/Resources/server_url.txt

    public static string ServerUrl(string fallback)
    {
        string page = Application.absoluteURL;
        string fromQuery = FromQuery(page);
        if (fromQuery != "") return fromQuery;
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            string same = SameHost(page);
            if (same != "") return same;
        }
        if (!Application.isEditor)
        {
            var ta = Resources.Load<TextAsset>(ConfigResource);
            string url = ta != null ? ta.text.Trim() : "";
            if (url != "") return url;
        }
        return fallback;
    }

    /// <summary>?server=… を WebSocket の URL にして返す（無ければ ""）</summary>
    public static string FromQuery(string pageUrl)
    {
        string v = Query(pageUrl, "server");
        if (v == "") return "";
        if (v.StartsWith("ws://") || v.StartsWith("wss://")) return v;
        if (v.StartsWith("https://")) return "wss://" + v.Substring(8);
        if (v.StartsWith("http://")) return "ws://" + v.Substring(7);
        bool local = v.StartsWith("localhost") || v.StartsWith("127.0.0.1");
        return (local ? "ws://" : "wss://") + v;
    }

    /// <summary>https://host/… → wss://host/api/ws（https でなければ ""）</summary>
    public static string SameHost(string pageUrl)
    {
        if (string.IsNullOrEmpty(pageUrl) || !pageUrl.StartsWith("https://")) return "";
        int end = pageUrl.IndexOfAny(new[] { '/', '?', '#' }, 8);
        string host = end < 0 ? pageUrl.Substring(8) : pageUrl.Substring(8, end - 8);
        return host == "" ? "" : "wss://" + host + SocketPath;
    }

    /// <summary>URL の ?key=value を返す（無ければ ""）</summary>
    public static string Query(string pageUrl, string key)
    {
        if (string.IsNullOrEmpty(pageUrl)) return "";
        int q = pageUrl.IndexOf('?');
        if (q < 0) return "";
        string query = pageUrl.Substring(q + 1);
        int hash = query.IndexOf('#');
        if (hash >= 0) query = query.Substring(0, hash);
        foreach (var pair in query.Split('&'))
        {
            int eq = pair.IndexOf('=');
            if (eq <= 0 || pair.Substring(0, eq) != key) continue;
            return Uri.UnescapeDataString(pair.Substring(eq + 1)).Trim().TrimEnd('/');
        }
        return "";
    }
}
