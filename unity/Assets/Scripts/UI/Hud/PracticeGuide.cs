// 書いた人：Claude ／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で CabinHud から分けた）／ 更新：iizuka（Opus 5.5、2026-11-14、#84）／ 更新：iizuka（Opus 5.5、2026-11-15、#190 あつ森に寄せる）
// 作る Issue：#84（練習の案内とかき混ぜ）
//
// 中央上の案内の帯（クリームのピル1本）。練習（M03）とかき混ぜ（M04）の間だけ出す。高校生が初めてでも「何を押すか」で迷わないのが一番
//   練習：[PRACTICE n/4]  1行目＝自分の席でまず押すもの／2行目＝何も壊れない  [4席のバッジ：済んだ席は色＋✓、まだは灰色]
//         今の席の「まず押すもの」1つを SeatPanel.Highlight(cmd, true) で光らせ、帯の下の ▼ で席のパネルを指す。済んだら消す
//         兼任の人は、今の席が済んだら少し待って、まだの席へ自動で切り替える（帯を押しても切り替わる）
//   かき混ぜ：[HOUSTON]  Life Support の人 → Tap STIR TANKS（Highlight("stir")）／ほかの人 → Waiting for LIFE SUPPORT
//         Life Support を持っている人は、始まったらその席へ自動で切り替える。M04 の繰り返し（サーバーが 20 秒ごと）で帯を弾ませる
// 直す所は1か所ずつ：文言は Words、どの操作を光らせるかは TryCmd、大きさ・時間は下の定数。共通の部品（#71 UiKitHud）ができたら Tag・バッジを差し替える
// 練習した席は Model.Practiced（practice イベントの done。GameFlow が入れる）。UI は判定しない（済んだかはサーバーの done だけを見る）
// 練習の残り秒は出さない：締め切り（server/game/config.py の PRACTICE_MAX_S = 90）をサーバーが配っていない
// 席・段階は Refresh より後に CabinHud が決める（Ctx.ActiveSeat は部品の Refresh の後に入る）ので、表示と光らせるのは毎フレームの Tick で合わせる
// #190（ui-guidelines 3章【12】【09】）：帯は波打つピル（blob_pill の panel、影なし）。札は名前タグの形（blob_pill、Nunito ExtraBold 26、Houston は傾ける）、
//   ▼ は角の丸い三角（caret_soft）、済んだ席はバッジの右上にチェック（UiSelect.Check。【09】）。文字は茶、言葉は小文字まじり。
//   席のパネルのボタンの名前（Close valve・Stir tanks）は先頭だけ大文字で書く（席のパネルの担当と合わせる）
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;

public class PracticeGuide : HudPart
{
    // ================================================================== 文言（画面の文字は英語。Fable の採点で直すのはここ）
    static class Words
    {
        public const string PracticeTag = "Practice {0}/{1}";          // {0} 済んだ席の数 {1} 席の数
        public const string HoustonTag = "Houston";
        public const string TryAny = "Tap any button below";           // TryCmd に無い席
        public const string Safe = "Nothing breaks yet. Try it!";
        public const string NiceNext = "Nice! Next: {0}";              // {0} 次の席の名前
        public const string Switching = "Switching seats...";
        public const string NextSeat = "Next: try {0}";
        public const string TapToSwitch = "Tap here to switch seats";
        public const string WaitOthers = "Nice! Wait for the others";
        public const string WaitOthersSub = "Every seat tries one button";
        public const string StirDo = "Tap Stir tanks";
        public const string StirDoSub = "Mission control asks: stir the oxygen tanks";
        public const string StirGo = "Go to {0}";                      // {0} = LIFE SUPPORT
        public const string StirGoSub = "Tap here. Mission control needs you";
        public const string StirWait = "Waiting for {0}";
        public const string StirWaitSub = "Mission control asked them to stir the tanks";
        public const string StirAgain = "Mission control is asking again!";
    }

    // 席ごとの「まず押すもの」：光らせるコマンド（SeatPanel.Commands の名前）と1行目の文（席の仕事：押すもの）
    static readonly Dictionary<string, (string cmd, string hint)> TryCmd = new Dictionary<string, (string, string)>
    {
        { Seats.Pilot,   ("set_attitude", "Turn the ship: tap a direction") },
        { Seats.Life,    ("close_valve",  "Guard the air: tap Close valve") },
        { Seats.Comms,   ("switch",       "Run the power: flip a switch") },
        { Seats.Planner, ("set_route",    "Plan the trip: pick a route") },
    };
    const string StirCmd = "stir";
    const string Practice = "practice", Stir = "stir";

    // ================================================================== 大きさ・時間（UiTokens に移す候補）
    const float BannerH = T.SecondaryH;        // 帯の高さの目安（ピルの角の半径 = これの半分。slot が低いときは角が縮む）
    const float BadgeD = T.TagH;               // 席バッジ（slot の高さ 72 に入る大きさ）
    const float CaretW = T.CaretW, CaretH = T.CaretH;   // 帯の下の ▼（#190：吹き出しと同じ角の丸い三角 32×22・accent。【12】）
    const float CaretBob = T.CaretBob;         // ▼ の上下の幅（6）
    const float BobPeriod = T.CaretPeriod;     // ▼ が1往復する秒
    const float HoustonTilt = T.NameTagTilt;   // Houston の名前タグの傾き（度、反時計回り。#190：吹き出しの名前タグと同じ −6°。【12】）
    const float NiceHold = 1.5f;               // 今の席が済んでから、次の席へ自動で切り替えるまで（秒）
    const float AgainHold = 6f;                // M04 の繰り返しのあと「asking again」を出す秒
    const float PopTime = 0.35f, PopScale = 0.3f;        // 済んだ席のバッジが弾む
    const float BounceTime = 0.4f, BounceScale = 0.06f;  // 帯が弾む（押すものが変わったとき・M04 の繰り返し）

    enum View { Hidden, Try, NiceNext, NextManual, WaitOthers, StirDo, StirGo, StirWait }

    // ================================================================== 部品
    RectTransform box, tag;
    Image face, tagFace, caret;
    Button tap;
    TextMeshProUGUI tagText, line1, line2;
    readonly UiSeatBadge[] badges = new UiSeatBadge[4];

    readonly float[] popEnd = new float[4];

    // ================================================================== 流れ
    string lastPhase;
    int lastKey = -1;
    View view;
    string niceSeat; float niceUntil;   // 今の席が済んだ直後（「Nice!」を見せて次の席へ）
    float againUntil, bounceEnd;
    bool stirJump;                      // かき混ぜが始まったら Life Support の席へ切り替える（1回）
    string tapTo;                       // 帯を押したときに切り替える席（null なら押せない）
    SeatPanel litPanel; string litCmd;  // 光らせている席のパネルとコマンド

    protected override void Build(RectTransform root)
    {
        box = Ui.Rect("Banner", root); UiKit.Fill(box);
        face = UiKit.Blob(box, T.BlobPill, T.Panel, BannerH * 0.5f, "Face");   // #190：波打つ帯（影なし）。角の丸さは Tick で帯の高さに合わせる
        UiKit.Fill(face.rectTransform); UiKit.Ignore(face);
        tap = box.gameObject.AddComponent<Button>();
        tap.targetGraphic = face;
        tap.navigation = new Navigation { mode = Navigation.Mode.None };
        var cb = tap.colors;
        cb.normalColor = cb.selectedColor = cb.disabledColor = Color.white;
        cb.highlightedColor = T.TintHover; cb.pressedColor = T.TintPressed; cb.fadeDuration = T.TintFade;
        tap.colors = cb;
        tap.onClick.AddListener(() => { if (tapTo != null) Ctx.SelectSeat(tapTo); });

        var row = UiKit.Row(box, T.S2, TextAnchor.MiddleCenter, T.S2); UiKit.Fill(row);
        tag = NameTag(row, Words.HoustonTag, out tagFace, out tagText);

        var col = UiKit.Col(row, 0f, TextAnchor.MiddleCenter, 0f, 0f, true);
        var le = Ui.Size(col, w: 0f, flexW: 1f); le.minWidth = 0f;   // 残りの幅を全部もらう
        line1 = Line(col, TextStyle.Body, T.Text);
        line1.enableAutoSizing = true; line1.fontSizeMin = T.TextLabel; line1.fontSizeMax = T.TextBody;   // 長い文は 22 まで縮める
        line2 = Line(col, TextStyle.Label, T.TextSoft);

        // 帯の下の ▼：席のパネル（画面の下）を指す。押すものがあるときだけ
        var sp = T.Art(T.CaretSoft) ?? (T.Theme != null ? T.Theme.caretDown : null);
        caret = UiKit.Shape(col, sp, T.Accent, 0f, "Caret");
        caret.preserveAspect = true;
        UiKit.Ignore(caret);
        var cr = caret.rectTransform;
        cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0f); cr.pivot = new Vector2(0.5f, 1f);
        cr.sizeDelta = new Vector2(CaretW, CaretH);
        caret.gameObject.SetActive(false);

        var bRow = UiKit.Row(row, T.S1, TextAnchor.MiddleCenter);
        for (int i = 0; i < Seats.All.Length; i++)
        {
            badges[i] = UiKit.SeatBadge(bRow, Seats.All[i], BadgeD);
            UiSelect.Check(badges[i].Root, false);   // 済んだ印（右上のチェック。【09】）
        }
        box.gameObject.SetActive(false);
    }

    static TextMeshProUGUI Line(Transform col, TextStyle st, Color c)
    {
        var t = UiKit.Text(col, "", st, c, TextAlignmentOptions.Midline);
        t.overflowMode = TextOverflowModes.Ellipsis;
        var le = Ui.Size(t, h: UiKit.Size(st) * T.BodyLineHeight); le.minWidth = 0f;
        return t;
    }

    /// <summary>名前タグ（#190：blob_pill、高さ 48、Nunito ExtraBold 26。【12】）。色と傾きは Apply で</summary>
    static RectTransform NameTag(Transform p, string text, out Image face, out TextMeshProUGUI label)
    {
        var r = UiKit.Row(p, 0f, TextAnchor.MiddleCenter, T.S2);
        r.name = "Tag";
        face = UiKit.Blob(r, T.BlobPill, T.Accent, T.TagH * 0.5f, "Face"); UiKit.Fill(face.rectTransform); UiKit.Ignore(face);
        label = UiKit.Text(r, text, TextStyle.Hint, T.Ink);
        label.font = T.FontStrong;
        var le = Ui.Size(r, h: T.TagH); le.minHeight = T.TagH; le.flexibleWidth = 0f;
        return r;
    }

    /// <summary>波打つ帯の丸い端を、帯の高さの半分に合わせる（帯は slot いっぱいなので高さが画面で変わる）</summary>
    void FitFace()
    {
        var sp = face.sprite;
        float h = box.rect.height;
        if (sp == null || sp.border.x <= 0f || h <= 0f) return;
        float m = sp.border.x / (h * 0.5f);
        if (!Mathf.Approximately(face.pixelsPerUnitMultiplier, m)) face.pixelsPerUnitMultiplier = m;
    }

    // ================================================================== イベント
    public override void OnEvent(GameEvent e)
    {
        float now = Time.unscaledTime;
        if (e.kind == "practice" && e.seat != null)
        {
            int i = System.Array.IndexOf(Seats.All, e.seat);
            if (i >= 0) popEnd[i] = now + PopTime;
            if (e.seat == Active && Ctx.Model.MySeats.Contains(e.seat)) { niceSeat = e.seat; niceUntil = now + NiceHold; }
        }
        else if (e.kind == "houston" && e.id == "M04" && e.repeat)
        {
            againUntil = now + AgainHold; bounceEnd = now + BounceTime;
        }
    }

    // ================================================================== 毎フレーム
    public override void Tick(float now)
    {
        var m = Ctx.Model;
        string ph = m.Phase;
        if (ph != lastPhase)
        {
            lastPhase = ph; niceSeat = null; againUntil = 0f; lastKey = -1;
            stirJump = ph == Stir;
        }
        bool show = m.MySeats.Count > 0 && (ph == Practice || ph == Stir);
        if (!show)
        {
            if (box.gameObject.activeSelf) box.gameObject.SetActive(false);
            SyncHighlight(View.Hidden);
            return;
        }
        if (!box.gameObject.activeSelf) box.gameObject.SetActive(true);

        AutoSwitch(now);
        bool again = now < againUntil;
        var v = Decide(out string other);
        int key = Key(v, other, again);
        if (key != lastKey)
        {
            if (v != view && (v == View.Try || v == View.StirDo || v == View.StirGo)) bounceEnd = now + BounceTime;
            lastKey = key; view = v;
            Apply(v, other, again);
        }
        Animate(now);
        SyncHighlight(v);
    }

    /// <summary>今の席（CabinHud が選んだ席。まだ無ければ自分の最初の席）</summary>
    string Active
    {
        get
        {
            var mine = Ctx.Model.MySeats;
            return Ctx.ActiveSeat != null && mine.Contains(Ctx.ActiveSeat) ? Ctx.ActiveSeat : mine.Count > 0 ? mine[0] : null;
        }
    }

    /// <summary>自分の席のうち、今の席の次にあるまだ試していない席。無ければ null</summary>
    string NextUntried()
    {
        var mine = Ctx.Model.MySeats; var done = Ctx.Model.Practiced; string act = Active;
        int start = Mathf.Max(0, mine.IndexOf(act));
        for (int k = 1; k <= mine.Count; k++)
        {
            string s = mine[(start + k) % mine.Count];
            if (s != act && !done.Contains(s)) return s;
        }
        return null;
    }

    void AutoSwitch(float now)
    {
        var m = Ctx.Model;
        if (stirJump)
        {
            stirJump = false;
            if (m.MySeats.Contains(Seats.Life) && Active != Seats.Life) { Ctx.SelectSeat(Seats.Life); return; }
        }
        if (niceSeat != null && now >= niceUntil)
        {
            string from = niceSeat; niceSeat = null;
            if (m.Phase == Practice && Active == from)
            {
                string next = NextUntried();
                if (next != null) Ctx.SelectSeat(next);
            }
        }
    }

    View Decide(out string other)
    {
        var m = Ctx.Model; other = null;
        string act = Active;
        if (m.Phase == Stir)
        {
            if (!m.MySeats.Contains(Seats.Life)) return View.StirWait;
            if (act == Seats.Life) return View.StirDo;
            other = Seats.Life; return View.StirGo;
        }
        if (!m.Practiced.Contains(act)) return View.Try;
        other = NextUntried();
        if (niceSeat == act) return other != null ? View.NiceNext : View.WaitOthers;
        return other != null ? View.NextManual : View.WaitOthers;
    }

    /// <summary>見た目を決めるものを1つの int に（変わったときだけ文字を入れ直す）</summary>
    int Key(View v, string other, bool again)
    {
        var m = Ctx.Model; int done = 0, mine = 0;
        for (int i = 0; i < Seats.All.Length; i++)
        {
            if (m.Practiced.Contains(Seats.All[i])) done |= 1 << i;
            if (m.MySeats.Contains(Seats.All[i])) mine |= 1 << i;
        }
        int act = System.Array.IndexOf(Seats.All, Active) + 1, oth = System.Array.IndexOf(Seats.All, other) + 1;
        return (int)v | (again ? 1 << 3 : 0) | done << 4 | mine << 8 | act << 12 | oth << 15;
    }

    void Apply(View v, string other, bool again)
    {
        var m = Ctx.Model;
        bool stir = v == View.StirDo || v == View.StirGo || v == View.StirWait;

        int n = 0; foreach (var s in Seats.All) if (m.Practiced.Contains(s)) n++;
        tagText.text = stir ? Words.HoustonTag : string.Format(Words.PracticeTag, n, Seats.All.Length);
        tagFace.color = stir ? T.Go : T.Accent;
        tagText.color = stir ? T.Panel : T.Ink;
        tag.localRotation = Quaternion.Euler(0f, 0f, stir ? HoustonTilt : 0f);

        string life = Seats.Title(Seats.Life), l1, l2;
        switch (v)
        {
            case View.Try: l1 = TryCmd.TryGetValue(Active, out var t) ? t.hint : Words.TryAny; l2 = Words.Safe; break;
            case View.NiceNext: l1 = string.Format(Words.NiceNext, Seats.Title(other)); l2 = Words.Switching; break;
            case View.NextManual: l1 = string.Format(Words.NextSeat, Seats.Title(other)); l2 = Words.TapToSwitch; break;
            case View.WaitOthers: l1 = Words.WaitOthers; l2 = Words.WaitOthersSub; break;
            case View.StirDo: l1 = Words.StirDo; l2 = again ? Words.StirAgain : Words.StirDoSub; break;
            case View.StirGo: l1 = string.Format(Words.StirGo, life); l2 = again ? Words.StirAgain : Words.StirGoSub; break;
            default: l1 = string.Format(Words.StirWait, life); l2 = Words.StirWaitSub; break;
        }
        line1.text = l1; line2.text = l2;
        line2.gameObject.SetActive(!string.IsNullOrEmpty(l2));

        caret.gameObject.SetActive(caret.sprite != null && (v == View.Try || v == View.StirDo));
        tapTo = v == View.NiceNext || v == View.NextManual || v == View.StirGo ? other : null;
        tap.interactable = tapTo != null;
        face.raycastTarget = tapTo != null;   // 押せないときは後ろへ通す

        for (int i = 0; i < Seats.All.Length; i++)
        {
            string s = Seats.All[i];
            badges[i].Root.gameObject.SetActive(!stir || s == Seats.Life);   // かき混ぜの間は Life Support だけ
            bool done = !stir && m.Practiced.Contains(s);
            badges[i].Set(s, m.MySeats.Contains(s), !stir && !done);         // まだの席は灰色、済んだ席は席の色＋✓
            UiSelect.Check(badges[i].Root, done);
        }
    }

    void Animate(float now)
    {
        FitFace();
        float b = 1f + BounceScale * Ease(bounceEnd - now, BounceTime);
        box.localScale = new Vector3(b, b, 1f);
        for (int i = 0; i < badges.Length; i++)
        {
            float p = 1f + PopScale * Ease(popEnd[i] - now, PopTime);
            badges[i].Root.localScale = new Vector3(p, p, 1f);
        }
        if (caret.gameObject.activeSelf)
            caret.rectTransform.anchoredPosition = new Vector2(0f, -CaretBob * (0.5f + 0.5f * Mathf.Sin(now * 2f * Mathf.PI / BobPeriod)));
    }

    /// <summary>残り left 秒（全体 dur）を 1→0 の弾み（はじめ大きく、すぐ戻る）にする</summary>
    static float Ease(float left, float dur)
    {
        if (left <= 0f) return 0f;
        float x = Mathf.Clamp01(left / dur);
        return x * x;
    }

    // ================================================================== 光らせる（SeatPanel.Highlight。中身は #80〜#83 の各パネル）
    void SyncHighlight(View v)
    {
        bool want = v == View.Try || v == View.StirDo;
        if (!want && litCmd == null) return;
        SeatPanel panel = null;
        foreach (var p in Ctx.Parts) if (p is SeatPanel sp) { panel = sp; break; }
        string cmd = null;
        if (want && panel != null && panel.Seat == Active)
            cmd = v == View.StirDo ? (panel.Seat == Seats.Life ? StirCmd : null) : CmdToTry(panel);
        if (panel == litPanel && cmd == litCmd) return;
        if (litCmd != null && litPanel != null && litPanel == panel) litPanel.Highlight(litCmd, false);   // 作り直された古いパネルはもう無いので消さなくてよい
        if (cmd != null) panel.Highlight(cmd, true);
        litPanel = cmd != null ? panel : null; litCmd = cmd;
    }

    /// <summary>その席でまず押すコマンド。TryCmd の物がパネルに無ければ、かき混ぜ以外の最初の物</summary>
    static string CmdToTry(SeatPanel p)
    {
        string first = null;
        bool known = TryCmd.TryGetValue(p.Seat, out var t);
        foreach (var c in p.Commands)
        {
            if (c == StirCmd) continue;
            if (known && c == t.cmd) return c;
            if (first == null) first = c;
        }
        return first;
    }
}
