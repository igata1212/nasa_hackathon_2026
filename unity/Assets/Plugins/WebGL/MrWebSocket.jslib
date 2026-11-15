// 書いた人：Taiga（Claude） ／ 最終更新：2026-11-15（#222）
// WebGL のビルドで、ブラウザの WebSocket を C#（Scripts/Net/WsClient.cs）から呼ぶための橋。
// C# 側は毎フレーム MrWsState / MrWsPoll を呼んで取り出す（JS → C# のコールバックは使わない）。
// つなぐのは1本だけ。つなぎ直すと古い接続の知らせは捨てる（gen で見分ける）。
mergeInto(LibraryManager.library, {
  $MrWs: { sock: null, gen: 0, state: 2, inbox: [], error: "" },

  // state：0 つなぎ中 / 1 つながった / 2 閉じた / 3 エラーで閉じた
  MrWsOpen__deps: ["$MrWs"],
  MrWsOpen: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    if (MrWs.sock) { try { MrWs.sock.close(); } catch (e) {} }
    var gen = ++MrWs.gen;
    MrWs.inbox = [];
    MrWs.error = "";
    MrWs.state = 0;
    var s;
    try { s = new WebSocket(url); }
    catch (e) { MrWs.sock = null; MrWs.state = 3; MrWs.error = String(e && e.message ? e.message : e); return; }
    MrWs.sock = s;
    s.onopen = function () { if (gen === MrWs.gen) MrWs.state = 1; };
    s.onmessage = function (ev) { if (gen === MrWs.gen && typeof ev.data === "string") MrWs.inbox.push(ev.data); };
    s.onerror = function () { if (gen === MrWs.gen) MrWs.error = "cannot reach " + url; };
    s.onclose = function () { if (gen === MrWs.gen) MrWs.state = MrWs.error ? 3 : 2; };
  },

  MrWsState__deps: ["$MrWs"],
  MrWsState: function () { return MrWs.state; },

  MrWsSend__deps: ["$MrWs"],
  MrWsSend: function (strPtr) {
    if (!MrWs.sock || MrWs.sock.readyState !== 1) return 0;
    MrWs.sock.send(UTF8ToString(strPtr));
    return 1;
  },

  // 届いたメッセージを1通返す（無ければ null）。返した文字列は Unity 側が解放する
  MrWsPoll__deps: ["$MrWs"],
  MrWsPoll: function () {
    if (MrWs.inbox.length === 0) return 0;
    var str = MrWs.inbox.shift();
    var size = lengthBytesUTF8(str) + 1;
    var buf = _malloc(size);
    stringToUTF8(str, buf, size);
    return buf;
  },

  MrWsError__deps: ["$MrWs"],
  MrWsError: function () {
    var str = MrWs.error || "";
    var size = lengthBytesUTF8(str) + 1;
    var buf = _malloc(size);
    stringToUTF8(str, buf, size);
    return buf;
  },

  MrWsClose__deps: ["$MrWs"],
  MrWsClose: function () {
    MrWs.gen++;
    if (MrWs.sock) { try { MrWs.sock.close(); } catch (e) {} }
    MrWs.sock = null;
    MrWs.state = 2;
    MrWs.inbox = [];
  }
});
