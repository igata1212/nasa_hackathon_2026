// 書いた人：Claude ／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で CabinHud から分けた）／ 更新：iizuka（Opus 5.5、2026-11-14、#84）／ 更新：iizuka（Opus 5.5、2026-11-15、#190 あつ森に寄せる）
// 作る Issue：#84（練習の案内とかき混ぜ）
//
// 中央上の案内の帯（クリームのピル1本）。練習（M03）とかき混ぜ（M04）の間だけ出す。高校生が初めてでも「何を押すか」で迷わないのが一番
//   練習：[PRACTICE n/4]  1行目＝自分の席でまず押すもの／2行目＝何も壊れない  [4席のバッジ：済んだ席は色＋✓、まだは灰色]
//         今の席の「まず押すもの」1つを SeatPanel.Highlight(cmd, true) で光らせ、帯の下の ▼ で席のパネルを指す。済んだら消す
//         兼任の人は、今の席が済んだら少し待って、まだの席へ自動で切り替える（帯を押しても切り替わる）
//   かき混ぜ：[HOUSTON]  Life Support の人 → Tap STIR TANKS（Highlight("stir")）／ほかの人 → Waiting for LIFE SUPPORT
//         Life Support を持っている人は、始まったらその席へ自動で切り替える。M04 の繰り返し（サーバーが 20 秒ごと）で帯を弾ませる
// #216（less is more）：席バッジ4つと2行目をやめ、[札] + 1行 だけにする（点火の準備の1行と同じ置き場・同じ形）。
//   済んだら札の右上にチェックがぽんと付き、次の文が右から滑り込んで帯が1回弾む（約 0.3 秒）
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
        public const string NiceNext = "Nice! Next: {0}";              // {0} 次の席の名前
        public const string NextSeat = "Tap here: try {0}";            // #216 2行目（Tap here to switch）を1行に
        public const string WaitOthers = "Nice! Wait for the others";
        public const string StirDo = "Tap Stir tanks";
        public const string StirDoAgain = "Again! Tap Stir tanks";     // #216 M04 の繰り返し（2行目をやめて1行で）
        public const string StirGo = "Tap here: go to {0}";            // {0} = LIFE SUPPORT
        public const string StirWait = "Waiting for {0}";
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
    const float CaretW = T.CaretW, CaretH = T.CaretH;   // 帯の下の ▼（#190：吹き出しと同じ角の丸い三角 32×22・accent。【12】）
    const float CaretBob = T.CaretBob;         // ▼ の上下の幅（6）
    const float BobPeriod = T.CaretPeriod;     // ▼ が1往復する秒
    const float HoustonTilt = T.NameTagTilt;   // Houston の名前タグの傾き（度、反時計回り。#190：吹き出しの名前タグと同じ −6°。【12】）
    const float NiceHold = 1.5f;               // 今の席が済んでから、次の席へ自動で切り替えるまで（秒）
    const float AgainHold = 6f;                // M04 の繰り返しのあと「asking again」を出す秒
    const float BounceTime = 0.3f, BounceScale = 0.05f;  // 帯が弾む（文が変わったとき・M04 の繰り返し。#216 0.4 → 0.3）
    const float SlideTime = 0.22f, SlideX = 48f;         // #216 次の文が右から滑り込む（秒・距離）

    enum View { Hidden, Try, NiceNext, NextManual, WaitOthers, StirDo, StirGo, StirWait }

    // ================================================================== 部品
    RectTransform box, tag;
    Image face, tagFace, caret;
    Button tap;
    TextMeshProUGUI tagText, line1;
    float slideEnd;                     // #216 文が滑り込み終わる時刻

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

        // #216 [札] + 1行（左寄せ。点火の準備の1行と同じ並び）
        var row = UiKit.Row(box, T.S3, TextAnchor.MiddleLeft, T.S3); UiKit.Fill(row);
        tag = NameTag(row, Words.HoustonTag, out tagFace, out tagText);
        UiSelect.Check(tag, false);   // 済んだ印（札の右上。【09】）

        line1 = Line(row, TextStyle.Body, T.Text);
        var le = Ui.Size(line1, w: 0f, flexW: 1f); le.minWidth = 0f;   // 残りの幅を全部もらう
        line1.enableAutoSizing = true; line1.fontSizeMin = T.TextLabel; line1.fontSizeMax = T.TextBody;   // 長い文は 22 まで縮める

        // 帯の下の ▼：席のパネル（画面の下）を指す。押すものがあるときだけ
        var sp = T.Art(T.CaretSoft) ?? (T.Theme != null ? T.Theme.caretDown : null);
        caret = UiKit.Shape(box, sp, T.Accent, 0f, "Caret");
        caret.preserveAspect = true;
        UiKit.Ignore(caret);
        var cr = caret.rectTransform;
        cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0f); cr.pivot = new Vector2(0.5f, 1f);
        cr.sizeDelta = new Vector2(CaretW, CaretH);
        caret.gameObject.SetActive(false);
        box.gameObject.SetActive(false);
    }

    static TextMeshProUGUI Line(Transform col, TextStyle st, Color c)
    {
        var t = UiKit.Text(col, "", st, c, TextAlignmentOptions.MidlineLeft);
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
            lastKey = key; view = v;
            string before = line1.text;
            Apply(v, other, again);
            if (line1.text != before && before.Length > 0) { slideEnd = now + SlideTime; bounceEnd = now + BounceTime; }   // #216 文が変わったら滑り込んで弾む
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

        string life = Seats.Title(Seats.Life), l1;
        switch (v)
        {
            case View.Try: l1 = TryCmd.TryGetValue(Active, out var t) ? t.hint : Words.TryAny; break;
            case View.NiceNext: l1 = string.Format(Words.NiceNext, Seats.Title(other)); break;
            case View.NextManual: l1 = string.Format(Words.NextSeat, Seats.Title(other)); break;
            case View.WaitOthers: l1 = Words.WaitOthers; break;
            case View.StirDo: l1 = again ? Words.StirDoAgain : Words.StirDo; break;
            case View.StirGo: l1 = string.Format(Words.StirGo, life); break;
            default: l1 = string.Format(Words.StirWait, life); break;
        }
        line1.text = l1;
        // #216 今の席が済んだら札にチェック（出るときに UiSelect がぽんと弾ませる）
        UiSelect.Check(tag, !stir && m.Practiced.Contains(Active));

        caret.gameObject.SetActive(caret.sprite != null && (v == View.Try || v == View.StirDo));
        tapTo = v == View.NiceNext || v == View.NextManual || v == View.StirGo ? other : null;
        tap.interactable = tapTo != null;
        face.raycastTarget = tapTo != null;   // 押せないときは後ろへ通す
    }

    void Animate(float now)
    {
        FitFace();
        float b = 1f + BounceScale * Ease(bounceEnd - now, BounceTime);
        box.localScale = new Vector3(b, b, 1f);
        // #216 文の滑り込み：左右の余白をずらす（幅は変えないので、自動の文字の大きさは動かない）。レイアウトに位置を上書きされない
        float k = slideEnd > now ? 1f - UiEase.OutCubic(1f - (slideEnd - now) / SlideTime) : 0f;
        float dx = SlideX * k;
        line1.margin = new Vector4(dx, 0f, -dx, 0f);
        line1.alpha = 1f - k;
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
