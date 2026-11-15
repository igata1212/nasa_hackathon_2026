// 書いた人：Claude ／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で CabinHud・GameFlow から分けた）／ 更新：iizuka（Opus 5.5、2026-11-14、#79）／ 更新：iizuka（Opus 5.5、2026-11-15、#189 Showing）／ 更新：iizuka（Opus 5.5、2026-11-15、#190 あつ森に寄せる）
// 作る Issue：#79（同時押しの合図と点火・再突入の準備チェック）
//
// 真ん中のパネル（slot の上端から下へ伸びる）。出すのは次の3つだけで、どれも無いときはパネルごと隠す。
//  ① 点火の準備（信号が戻った T_AOS 〜 噴射窓が閉じる T_BURN_CLOSE、未点火の間）：見出し＋残り秒（タイムラインの帯と同じ「窓の終わり − t」）、
//     まだの項目は大きく（担当の席バッジ・自分の席なら YOU）、できた項目は小さく1列。全部そろったら「3-2-1 と声に出して一緒に押す」
//  ② 再突入の準備（点火のあと 〜 T_REENTRY ＋ reentry_ready_s）：熱シールドの向き（▲）・航法 ON（■）。チェックの時刻のあとは結果を少し出す
//  ③ 同時押しの合図（press イベント：waiting／together／failed）：2席の大きいバッジ。押した席の輪が受付の幅（press_window_s）で一周、
//     相手の席が自分なら「YOU: PRESS NOW!」で弾む。そろったら両方 ✓、だめなら ✗ と一言。点火・フィルター交換の結果（cmd イベントの ok）も一言
// 判定はサーバー。ここは state の値を並べて見せるだけ（燃料の必要量は parameters.json の fuel_burn_a／b）。
// 「相手が押し始めた」はサーバーが送らない（押し始めのイベントが無い）ので、届いた waiting からしか出せない。3-2-1 をサーバーの時刻でそろえるのは未決定（SPEC 12章3）
// 点火のボタン自体は席のパネル（#80・#82。Ctx.SendDual("ignite", seat)）。CabinHud.ShowPress／ClearPress から Show・Clear も呼ばれる
// Detail：なし（照明 OFF でも暗くしない）
// #190（ui-guidelines 3章「パネル」「見出しの帯」「材料の行」【05】【11】【09】）：パネルは UiKit.Panel（blob_panel＋内ふち＋横縞、影なし）。見出しは左上の帯（Ribbon）に台つきのアイコン。
//   準備の項目は材料の行（UiKit.Row。【11】）：まだ＝accent の台に項目のアイコン＋名前＋点線＋右に担当の席と You、できた＝go の台にチェックの小さなピル（bg）を1列。
//   残り秒のチップは明るい面の数のチップの色（text）。言葉は小文字まじり（原則 12）。手（指さし）は出さない（船内の手は席のパネルが使う。1画面に1つ）
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;

public class BurnCheck : HudPart
{
    // ------------------------------------------------------------------ 文言（Fable の採点で直すときはここだけ。{0} は 分:秒 か数）
    static class Words
    {
        // 見出し
        public const string BurnSoon = "Get ready to burn";
        public const string BurnNow = "Burn now!";
        public const string ReentrySoon = "Get ready for re-entry";
        public const string ReentryCheck = "Heat shield check";
        public const string SwapFilter = "Swap the air filter";
        public const string Ignite = "Fire the engine";
        // 見出しの横のチップ
        public const string OpensIn = "opens in {0}";
        public const string Left = "{0} left";
        public const string StartsIn = "starts in {0}";
        public const string CheckIn = "check in {0}";
        // 準備の項目（できた／まだ）。席のパネルのボタンの名前（Nav・Burn・Heat shield・Ignite）は先頭だけ大文字（#190）
        public const string NavOk = "Nav on", NavTodo = "Turn Nav on";
        public const string FacingOk = "Facing Burn", FacingTodo = "Turn the ship to Burn";
        public const string CourseOk = "Course fixed", CourseTodo = "Do the correction burn";
        public const string FuelOk = "Fuel OK", FuelTodo = "Not enough fuel (needs {0})";
        public const string RouteOk = "Route {0}", RouteAuto = "No route yet (auto)";
        public const string ShieldOk = "Shield forward", ShieldTodo = "Turn to Heat shield";
        public const string You = "You";
        public const string AllSet = "Count 3-2-1, then press Ignite together!";
        // 同時押し
        public const string PressTogether = "Press together";
        public const string Together = "Together!";
        public const string Missed = "Missed";
        public const string Pressed = "pressed";
        public const string YourTurn = "You: press now!";
        public const string Waiting = "waiting...";
        public const string WaitHint = "Partner, press now!";
        public const string MissedHint = "Too far apart. Count 3-2-1, then press.";
        public const string MissedFuel = "Too far apart: -{0} fuel. Count 3-2-1, then press.";
        // 結果
        public const string Fired = "Engine fired! Heading home.";
        public const string NotReady = "Not ready: finish the list, then press again.";
        public const string FilterDone = "Air filter swapped!";
        public const string NoFilter = "No spare filters left";
        public const string ReentryReady = "Ready! The heat shield protects you.";
        public const string ReentryNotReady = "Not ready: a bumpy ride home.";
        public const string BurnMissed = "Burn window missed: going home the slow way (B).";
    }

    // ------------------------------------------------------------------ 見た目・時間（UiTokens に無いもの。#71 の UiKitHud ができたらそちらへ）
    const float ShowSeconds = 2.5f;     // 同時押しの結果・コマンドの結果を出しておく秒（実時間。前は GameFlow.ClearPressLater）
    const float WaitExtra = 1.0f;       // waiting のあと何も来なくても消すまでの余り（実時間）
    const float ResultGameS = 4f;       // 再突入のチェック・噴射窓を逃した結果を出しておく秒（ゲームの秒）
    const float TodoBadge = 44f;        // まだの項目の席バッジ（材料の行 64 に入る大きさ）
    const float DoneDisc = 32f;         // できた項目の小さなピルの中の台
    const int TodoPool = 4, DonePool = 5;

    // ------------------------------------------------------------------ 準備の項目（並び = 出す順）
    class Item
    {
        public string Seat;                         // 担当の席（null なら席なし）
        public Func<GameState, bool> Show = _ => true;
        public Func<GameState, bool> Ok;
        public Func<GameState, string> Done, Todo;
        public bool Optional;                        // まだでも「まだの項目」に入れない（ルートは選ばなければサーバーが決める）
        public string DoneIcon = "check";
        public string Icon = "check";               // #190：材料の行の台のアイコン（ui-guidelines 4章）
    }

    List<Item> burnItems, reentryItems;

    // 部品
    RectTransform panel, header, todoCol, doneRow, readyRow, signalRow;
    UiRibbon title;
    UiIconDisc headerDisc;
    TextMeshProUGUI ready, sigTitle, sigHint, result;
    TimelineKit.Countdown chip;
    TimelineKit.BadgeRow readyBadges;
    TimelineKit.DualBadge left, right;
    readonly List<TodoRow> todos = new List<TodoRow>();
    readonly List<DoneChip> dones = new List<DoneChip>();

    // 同時押し（実時間）
    string sigCmd, lastPressStatus;
    float sigUntil, lastPressAt, resultUntil;
    string resultText; Color resultColor;

    /// <summary>カードが出ているか。出ている間、危機の帯（CrisisBanner）は注意（オレンジ）を隠す（#189：点火・再突入で4つ重なった）</summary>
    public bool Showing => panel != null && panel.gameObject.activeSelf;

    CrisisBanner crisis;
    bool BannerShown()
    {
        if (crisis == null) foreach (var p in Ctx.Parts) if (p is CrisisBanner c) { crisis = c; break; }
        return crisis != null && crisis.Showing;
    }

    class TodoRow { public RectTransform Root; public UiRow Row; public UiSeatBadge Badge; public RectTransform You; }
    class DoneChip { public RectTransform Root; public UiIconDisc Disc; public TimelineKit.BadgeRow Badge; public TextMeshProUGUI Text; }

    protected override void Build(RectTransform root)
    {
        MakeItems();

        panel = UiKit.Panel(root, T.S2, "BurnPanel");
        panel.anchorMin = new Vector2(0f, 1f); panel.anchorMax = new Vector2(1f, 1f); panel.pivot = new Vector2(0.5f, 1f);
        panel.offsetMin = new Vector2(0f, panel.offsetMin.y); panel.offsetMax = new Vector2(0f, 0f);
        panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        header = UiKit.Row(panel, T.S2, TextAnchor.MiddleLeft);
        title = TimelineKit.Ribbon(header, "", "ignition", T.Accent, out headerDisc);   // #190：見出しは左上の帯【11】
        Ui.Size(UiKit.Space(header, 0f), flexW: 1);
        chip = TimelineKit.Countdown.Make(header, false, T.Text);   // 明るいパネルの上の数のチップは text の面（【09】）

        todoCol = UiKit.Col(panel, T.S1, TextAnchor.UpperLeft, 0f, 0f, true);
        for (int i = 0; i < TodoPool; i++) todos.Add(MakeTodo(todoCol));
        doneRow = UiKit.Row(panel, T.S1, TextAnchor.MiddleLeft);
        for (int i = 0; i < DonePool; i++) dones.Add(MakeDone(doneRow));

        readyRow = UiKit.Row(panel, T.S2, TextAnchor.MiddleCenter);
        readyBadges = new TimelineKit.BadgeRow(readyRow, T.TagH);
        ready = UiKit.Text(readyRow, Words.AllSet, TextStyle.Body, T.Text);
        ready.textWrappingMode = TextWrappingModes.Normal; Ui.Size(ready, flexW: 1);

        signalRow = UiKit.Row(panel, T.S4, TextAnchor.MiddleCenter);
        left = TimelineKit.DualBadge.Make(signalRow);
        var mid = UiKit.Col(signalRow, T.S1, TextAnchor.MiddleCenter);
        sigTitle = UiKit.Text(mid, "", TextStyle.Choice, T.Text);
        sigHint = UiKit.Text(mid, "", TextStyle.Label, T.TextSoft);
        right = TimelineKit.DualBadge.Make(signalRow);

        result = UiKit.Text(panel, "", TextStyle.Body, T.Go);
        result.textWrappingMode = TextWrappingModes.Normal;

        Rebuild();
    }

    void MakeItems()
    {
        burnItems = new List<Item>
        {
            new Item { Seat = Seats.Comms, Icon = "switch", Ok = s => s.Switch("nav"), Done = _ => Words.NavOk, Todo = _ => Words.NavTodo },
            new Item { Seat = Seats.Pilot, Icon = "attitude", Ok = s => s.attitude == "burn", Done = _ => Words.FacingOk, Todo = _ => Words.FacingTodo },
            // 軌道修正は観測向き軌道のときだけ要る（自由帰還なら最初から correction_done）
            new Item { Seat = Seats.Pilot, Icon = "ignition", Show = s => Trajectory() == "science" || !s.correctionDone, Ok = s => s.correctionDone,
                       Done = _ => Words.CourseOk, Todo = _ => Words.CourseTodo },
            new Item { Seat = Seats.Planner, Icon = "route", Optional = true, Ok = s => !string.IsNullOrEmpty(s.route),
                       Done = s => string.IsNullOrEmpty(s.route) ? Words.RouteAuto : string.Format(Words.RouteOk, s.route.ToUpper()), Todo = _ => Words.RouteAuto },
            new Item { Seat = null, Icon = "fuel", Ok = s => s.fuel >= FuelNeed(s), Done = _ => Words.FuelOk,
                       Todo = s => string.Format(Words.FuelTodo, Mathf.CeilToInt(FuelNeed(s))) },
        };
        reentryItems = new List<Item>
        {
            new Item { Seat = Seats.Pilot, Icon = "heat_shield", Ok = s => s.attitude == "heatshield", Done = _ => Words.ShieldOk, Todo = _ => Words.ShieldTodo },
            new Item { Seat = Seats.Comms, Icon = "switch", Ok = s => s.Switch("nav"), Done = _ => Words.NavOk, Todo = _ => Words.NavTodo },
        };
    }

    string Trajectory() => J.Str(Ctx.Model.Design, "trajectory");

    /// <summary>点火に要る燃料（parameters.json の fuel_burn_a／b）。ルート未選択なら、足りれば A・足りなければ B（SPEC 7章 M11）</summary>
    static float FuelNeed(GameState s)
    {
        float a = GameParams.Get("fuel_burn_a"), b = GameParams.Get("fuel_burn_b");
        if (s.route == "a") return a;
        if (s.route == "b") return b;
        return s.fuel >= a ? a : b;
    }

    /// <summary>再突入のチェックまでの秒（state の thresholds、無ければ parameters.json の reentry_ready_s）</summary>
    static float ReadyS(GameState s) =>
        J.Has(s.thresholds, "reentry_ready_s") ? J.Num(s.thresholds, "reentry_ready_s") : GameParams.Get("reentry_ready_s");

    /// <summary>まだの項目：材料の行（【11】）。accent の台に項目のアイコン＋名前＋点線＋右に担当の席と You</summary>
    TodoRow MakeTodo(Transform p)
    {
        var r = new TodoRow();
        r.Row = UiKit.Row(p, "check", "", null, T.Accent);
        r.Root = r.Row.Root;
        r.Root.name = "Todo";
        r.Badge = UiKit.SeatBadge(r.Row.Right, Seats.Pilot, TodoBadge);
        r.You = UiKit.Tag(r.Row.Right, Words.You, T.Text, T.Panel);
        return r;
    }

    /// <summary>できた項目：bg の小さなピル（go の台にチェック＋担当の席＋名前。パネルの横縞 panelBand の上でも見えるよう bg）。1列に並べる</summary>
    DoneChip MakeDone(Transform p)
    {
        var c = new DoneChip();
        c.Root = UiKit.Chip(p, T.Bg, T.TagH, "Done");
        c.Root.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset((int)T.S1, (int)T.S2, 0, 0);
        c.Root.GetComponent<HorizontalLayoutGroup>().spacing = T.S1;
        c.Disc = UiKit.Disc(c.Root, "check", T.Go, DoneDisc);
        c.Badge = new TimelineKit.BadgeRow(c.Root, TimelineKit.MiniBadge);
        c.Text = UiKit.Text(c.Root, "", TextStyle.Label, T.TextSoft);
        return c;
    }

    // ------------------------------------------------------------------ 受け取る
    public override void Refresh(GameState s) => Rebuild();

    public override void OnEvent(GameEvent e)
    {
        if (e.kind == "press") Show(e);
        else if (e.kind == "cmd") OnCmd(e);
    }

    /// <summary>同時押しの合図（press イベント）。CabinHud.ShowPress からも呼ばれる</summary>
    public void Show(GameEvent e)
    {
        float now = Time.unscaledTime;
        var mine = Ctx.Model.MySeats;
        sigCmd = e.cmd; lastPressStatus = e.status; lastPressAt = now;

        // 組み合わせ（server/game/seats.py の DUAL）：ignite = pilot ＋ comms_power、swap_filter = life_support ＋ ほかのだれか1席
        bool swap = e.cmd == "swap_filter";
        string need = swap ? Seats.Life : Seats.Pilot;
        string partner = swap ? null : Seats.Comms;
        if (swap) foreach (var x in e.seats) if (x != Seats.Life) partner = x;
        left.SetSeat(need, mine.Contains(need));
        right.SetSeat(partner, partner != null && mine.Contains(partner));

        float win = e.windowS > 0f ? e.windowS : GameParams.Get("press_window_s");
        float real = win / Mathf.Max(0.01f, Ctx.Model.Speed);   // 幅はゲームの秒（server/README）。実時間に直す

        if (e.status == "waiting")
        {
            string pressed = e.seats.Count > 0 ? e.seats[0] : need;
            bool leftPressed = pressed == need;
            var pb = leftPressed ? left : right; var ob = leftPressed ? right : left;
            bool otherMine = ob.Seat != null ? mine.Contains(ob.Seat) : mine.Exists(x => x != Seats.Life);
            pb.SetLook(TimelineKit.PressLook.Pressing, Words.Pressed, real);
            ob.SetLook(otherMine ? TimelineKit.PressLook.YourTurn : TimelineKit.PressLook.Waiting, otherMine ? Words.YourTurn : Words.Waiting, real);
            sigTitle.text = Words.PressTogether; sigTitle.color = T.Text;
            sigHint.text = otherMine ? "" : Words.WaitHint;
            sigUntil = now + real + WaitExtra;
        }
        else if (e.status == "together")
        {
            left.SetLook(TimelineKit.PressLook.Done, "", 0f);
            right.SetLook(TimelineKit.PressLook.Done, "", 0f);
            sigTitle.text = Words.Together; sigTitle.color = T.Go;
            sigHint.text = "";
            sigUntil = now + ShowSeconds;
        }
        else if (e.status == "failed")
        {
            bool l = e.seats.Contains(need);
            left.SetLook(l ? TimelineKit.PressLook.Missed : TimelineKit.PressLook.Idle, "", 0f);
            right.SetLook(l ? TimelineKit.PressLook.Idle : TimelineKit.PressLook.Missed, "", 0f);
            sigTitle.text = Words.Missed; sigTitle.color = T.Danger;
            // 点火の不成立で燃料が減るのは、点火の条件がそろっていたときだけ（reference_sim の ignite(False)）
            var s = Ctx.State;
            bool costs = !swap && s != null && BurnReady(s);
            sigHint.text = costs ? string.Format(Words.MissedFuel, Mathf.CeilToInt(GameParams.Get("fuel_burn_retry"))) : Words.MissedHint;
            sigUntil = now + ShowSeconds;
        }
        Rebuild();
    }

    /// <summary>点火・フィルター交換の結果（cmd イベントの ok）</summary>
    void OnCmd(GameEvent e)
    {
        if (!e.ok.HasValue) return;
        bool afterTogether = lastPressStatus == "together" && Time.unscaledTime - lastPressAt < ShowSeconds * 2f;
        if (e.cmd == "ignite")
        {
            if (e.ok.Value) SetResult(Words.Fired, T.Go);
            else if (afterTogether) SetResult(Words.NotReady, T.Danger);
        }
        else if (e.cmd == "swap_filter")
        {
            if (e.ok.Value) SetResult(Words.FilterDone, T.Go);
            else if (afterTogether) SetResult(Words.NoFilter, T.Danger);
        }
    }

    void SetResult(string text, Color c)
    {
        resultText = text; resultColor = c; resultUntil = Time.unscaledTime + ShowSeconds;
        Rebuild();
    }

    public void Clear()
    {
        sigCmd = null; resultUntil = 0f;
        Rebuild();
    }

    public override void Tick(float now)
    {
        left.Tick(now); right.Tick(now); chip.Tick(now);
        bool changed = false;
        if (sigCmd != null && now > sigUntil) { sigCmd = null; changed = true; }
        if (resultUntil > 0f && now > resultUntil) { resultUntil = 0f; changed = true; }
        if (changed) Rebuild();
    }

    // ------------------------------------------------------------------ 描く
    enum Mode { None, Burn, Reentry }

    bool BurnReady(GameState s)
    {
        foreach (var it in burnItems) if (!it.Optional && it.Show(s) && !it.Ok(s)) return false;
        return s.t >= s.tBurnOpen && s.t < s.tBurnClose;
    }

    void Rebuild()
    {
        if (panel == null) return;
        var s = Ctx.State;
        var mine = Ctx.Model.MySeats;
        float now = Time.unscaledTime;

        Mode mode = Mode.None;
        float readyAt = 0f;
        if (s != null)
        {
            readyAt = s.tReentry + ReadyS(s);
            if (!s.burnDone && s.tBurnClose > 0f && s.t >= s.tAos && s.t < s.tBurnClose) mode = Mode.Burn;
            else if (s.burnDone && s.tReentry > 0f && s.t < readyAt) mode = Mode.Reentry;
        }
        bool signal = sigCmd != null;

        // 見出しと残り秒
        header.gameObject.SetActive(mode != Mode.None || signal);
        chip.Root.gameObject.SetActive(mode != Mode.None);
        bool inWindow = false;
        if (mode == Mode.Burn)
        {
            inWindow = s.t >= s.tBurnOpen;
            headerDisc.Set("ignition", T.Accent);
            title.SetText(inWindow ? Words.BurnNow : Words.BurnSoon);
            // 残り秒はタイムラインの帯（TimelineBar）と同じ式
            if (inWindow) chip.Set("timer", Words.Left, s.tBurnClose - s.t, true, null, mine);
            else chip.Set("timer", Words.OpensIn, s.tBurnOpen - s.t, false, null, mine);
        }
        else if (mode == Mode.Reentry)
        {
            headerDisc.Set("heat_shield", T.Go);
            bool started = s.t >= s.tReentry;
            title.SetText(started ? Words.ReentryCheck : Words.ReentrySoon);
            if (started) chip.Set("timer", Words.CheckIn, readyAt - s.t, true, null, mine);
            else chip.Set("timer", Words.StartsIn, s.tReentry - s.t, false, null, mine);
        }
        else if (signal)
        {
            bool swap = sigCmd == "swap_filter";
            headerDisc.Set(swap ? "filter" : "ignition", swap ? T.Go : T.Accent);
            title.SetText(swap ? Words.SwapFilter : Words.Ignite);
        }

        // 準備の項目：まだのものは大きく（担当の席と YOU）、できたものは小さく1列
        int nTodo = 0, nDone = 0;
        var items = mode == Mode.Burn ? burnItems : mode == Mode.Reentry ? reentryItems : null;
        if (items != null)
            foreach (var it in items)
            {
                if (!it.Show(s)) continue;
                bool ok = it.Ok(s);
                if (!ok && !it.Optional && nTodo < todos.Count) SetTodo(todos[nTodo++], it, s, mine);
                else if (nDone < dones.Count) SetDone(dones[nDone++], it, s, ok, mine);
            }
        for (int i = 0; i < todos.Count; i++) todos[i].Root.gameObject.SetActive(i < nTodo);
        for (int i = 0; i < dones.Count; i++) dones[i].Root.gameObject.SetActive(i < nDone);
        todoCol.gameObject.SetActive(nTodo > 0);
        doneRow.gameObject.SetActive(nDone > 0 && nTodo == 0);   // #146（Fable）：まだの項目がある間は、それだけを出す（できた項目の列は全部そろってから）

        // 全部そろった：声に出して一緒に押す
        bool allSet = mode == Mode.Burn && inWindow && nTodo == 0 && !signal;
        readyRow.gameObject.SetActive(allSet);
        if (allSet) readyBadges.Set(new[] { Seats.Pilot, Seats.Comms }, mine);

        // 同時押しの合図
        signalRow.gameObject.SetActive(signal);

        // 結果：コマンドの結果（実時間）＞ 再突入のチェック・噴射窓を逃した（ゲームの秒）
        string rt = null; Color rc = T.Go;
        if (resultUntil > now) { rt = resultText; rc = resultColor; }
        else if (s != null)
        {
            // 噴射窓を逃して strict になっていたら、再突入のチェックの結果は strict_reason から分からないので出さない
            if (s.burnDone && s.tReentry > 0f && s.t >= readyAt && s.t < readyAt + ResultGameS && s.strictReason != "missed_burn")
            {
                bool bad = s.strict && s.strictReason == "reentry_not_ready";
                rt = bad ? Words.ReentryNotReady : Words.ReentryReady; rc = bad ? T.Danger : T.Go;
            }
            else if (s.strictReason == "missed_burn" && s.t >= s.tBurnClose && s.t < s.tBurnClose + ResultGameS)
            {
                rt = Words.BurnMissed; rc = T.Danger;
            }
        }
        result.gameObject.SetActive(rt != null);
        if (rt != null) { result.text = rt; result.color = rc; }

        panel.gameObject.SetActive(mode != Mode.None || signal || rt != null);
        panel.anchoredPosition = new Vector2(0f, (BannerShown() || Ctx.State == null) ? -(T.S3 + 72f) : 0f);   // 帯が出る間だけ帯の下へ（#190 採点 b）
    }

    void SetTodo(TodoRow r, Item it, GameState s, List<string> mine)
    {
        r.Row.Disc.Set(it.Icon, T.Accent);   // まだ＝注意の色の台（状態は色＋アイコン）
        r.Row.Name.text = it.Todo(s);
        r.Badge.Root.gameObject.SetActive(it.Seat != null);
        if (it.Seat != null) r.Badge.Set(it.Seat, mine.Contains(it.Seat), false);
        r.You.gameObject.SetActive(it.Seat != null && mine.Contains(it.Seat));
    }

    void SetDone(DoneChip c, Item it, GameState s, bool ok, List<string> mine)
    {
        // 任意の項目（ルート）がまだのときは、✓ ではなくその項目のアイコンを薄く
        c.Disc.Set(ok ? it.DoneIcon : "route", ok ? T.Go : T.Line);
        c.Badge.Set(it.Seat != null ? new[] { it.Seat } : null, mine);
        c.Text.text = it.Done(s);
    }
}
