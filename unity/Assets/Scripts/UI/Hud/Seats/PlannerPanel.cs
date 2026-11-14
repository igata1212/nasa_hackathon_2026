// 書いた人：Claude ／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で CabinHud から分けた）／ 更新：iizuka（Opus 5.5、2026-11-14、#83）
// 作る Issue：#83（Mission Planner のパネル）
//
// Mission Planner（◆）の席。かたまりは2つ＋手伝い1つ（#102「1席 2〜3 個」）：
//   ① 観測：設計で積んだ機器のスイッチだけ（ON は電力を使う）。横に観測の条件2つ（月の裏側にいるか・月を向いているか）と集めたデータ
//      向きが違うと電力だけ減ってデータは増えない（SPEC 5章 補足）ことが、✗ の行で分かる
//   ② 帰り道：ルート A（速い・燃料を多く使う）／B（遅い・酸素と電力が長くもつ必要）。M08（t ≥ m06_delay_s）まで鍵、点火したら決まり
//   ③ 手伝い：CO2 が注意の値を超えたら「HELP SWAP」（Life Support と同時押しのフィルター交換）。Life Support を自分で持っているときは出さない
// 今できるかたまりだけオレンジの輪で光らせる（月の裏側の間は観測、それ以外でルート未選択なら帰り道）。終わったかたまりは薄くする
// 文言は下の Txt、部品と大きさは LifeSupportPanel.cs の LpKit（仮。#71 の HudKit ができたら替える）
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using T = UiTokens;

public class PlannerPanel : SeatPanel
{
    public override string Seat => Seats.Planner;
    public override IEnumerable<string> Commands => new[] { "switch", "set_route" };

    // ------------------------------------------------------------------ 文言（Fable・Taiga が直す所。画面の文字は英語。ASCII だけ）
    static class Txt
    {
        // 観測機器（id は reference_sim の INSTRUMENTS。名前は設計画面の部品名に合わせた短い形）
        public static readonly (string id, string icon, string name)[] Instruments =
        {
            ("camera", "camera", "CAMERA"),
            ("spectrometer", "spectrometer", "SPECTROMETER"),
            ("radar", "radar", "RADAR"),
        };
        public const string On = "ON, uses power", Off = "OFF";
        public const string NoInstruments = "No science\ninstruments\non board.";
        public const string FarSideIn = "Far side in {0}", FarSideNow = "Behind the Moon", FarSideOver = "Far side is over";
        public const string FarSidePractice = "Only behind the Moon";
        public const string Facing = "Facing the Moon", NotFacing = "Pilot: face the Moon";
        public const string Data = "Data {0}";
        public const string Route = "WAY\nHOME";
        // ルート：A は速いが燃料を多く使う、B は遅いので酸素と電力が長くもつ必要がある（M08 の台詞と同じ）。燃料の数字は parameters.json
        public static readonly (string id, string letter, string name, string sub, string fuelKey)[] Routes =
        {
            ("a", "A", "FAST", "Uses more fuel", "fuel_burn_a"),
            ("b", "B", "SLOW", "Uses more O2 & power", "fuel_burn_b"),
        };
        public const string RouteLocked = "Opens soon", RouteFinal = "Locked in";
        public const string Help = "HELP SWAP", HelpSub = "CO2 filter, with Life Support";
    }

    LpStrip strip;
    LpCard sci, routeCard, help;
    readonly Dictionary<string, LpButton> toggles = new Dictionary<string, LpButton>();
    readonly Dictionary<string, LpButton> routes = new Dictionary<string, LpButton>();
    readonly HashSet<string> practiceOn = new HashSet<string>();   // 練習の間（state が無い）に押したスイッチ。見た目だけ
    LpLine lineWindow, lineFacing, lineData;
    LpButton helpBtn;
    string feedback; float feedbackUntil;
    readonly HashSet<string> lit = new HashSet<string>();   // Highlight（#84 が光らせているコマンド）

    // ------------------------------------------------------------------ 作る
    protected override void BuildControls(RectTransform row)
    {
        RootAsDetail();
        strip = LpKit.Strip(row);

        // ① 観測
        sci = LpKit.Card(strip.Rect, "Science");
        foreach (var ins in Txt.Instruments)
        {
            if (!Ctx.HasPart(ins.id)) continue;
            string id = ins.id;
            var b = LpKit.Button(sci.Root, ins.icon, null, ins.name, Txt.Off, () => Toggle(id), TextStyle.Tag);
            b.Reserve(ins.name, Txt.On);
            toggles[id] = b;
        }
        if (toggles.Count == 0) LpKit.Note(sci.Root, Txt.NoInstruments, TextStyle.Label, T.InkSub);
        else
        {
            var col = UiKit.Col(sci.Root, 0f, TextAnchor.MiddleLeft);
            lineWindow = LpKit.Line(col); lineFacing = LpKit.Line(col); lineData = LpKit.Line(col);
            LpKit.ReserveWidth(lineWindow.Text, string.Format(Txt.FarSideIn, "0:00"), Txt.FarSideNow, Txt.FarSideOver, Txt.FarSidePractice);
            LpKit.ReserveWidth(lineFacing.Text, Txt.Facing, Txt.NotFacing);
            LpKit.ReserveWidth(lineData.Text, string.Format(Txt.Data, 9999));
        }

        // ② 帰り道
        routeCard = LpKit.Card(strip.Rect, "Route");
        LpKit.Note(routeCard.Root, Txt.Route, TextStyle.Tag, T.Ink);
        foreach (var r in Txt.Routes)
        {
            string id = r.id;
            var b = LpKit.Button(routeCard.Root, null, r.letter, r.name, r.sub, () => Ctx.Send(Cmd.Route(Seat, id)));
            LpKit.Chip(b, "fuel", Mathf.RoundToInt(GameParams.Get(r.fuelKey)).ToString());
            b.AddMark();
            b.Reserve(r.name, Txt.RouteLocked);
            b.Reserve(r.name, Txt.RouteFinal);
            routes[id] = b;
        }

        // ③ 手伝い（CO2 が高いときだけ出す。右の端なので、出ても消えてもほかのボタンの位置は動かない）
        help = LpKit.Card(strip.Rect, "Help");
        helpBtn = LpKit.Button(help.Root, "filter", null, Txt.Help, Txt.HelpSub, () => Ctx.SendDual("swap_filter", Seat));
        foreach (var sub in new[] { LpKit.PressWaiting, LpKit.PressNow, LpKit.PressTogether, LpKit.PressFailed }) helpBtn.Reserve(Txt.Help, sub);
        help.Root.gameObject.SetActive(false);
    }

    void Toggle(string id)
    {
        var s = Ctx.State;
        bool cur = s != null ? s.Switch(id) : practiceOn.Contains(id);
        Ctx.Send(Cmd.Switch(Seat, id, !cur));
        if (s == null) { if (cur) practiceOn.Remove(id); else practiceOn.Add(id); }
        // 押した手ごたえ：先に見た目を変える（次の state で正しい値に戻る。記録の再生では戻る）
        ShowToggle(toggles[id], !cur);
    }

    static void ShowToggle(LpButton b, bool on)
    {
        b.SetText(null, on ? Txt.On : Txt.Off);
        b.Set(on ? LpLook.On : LpLook.Secondary);
    }

    // ------------------------------------------------------------------ 状態・イベント
    public override void Refresh(GameState s)
    {
        base.Refresh(s);
        if (sci == null) return;   // stir の段階（この席は何も出さない）
        bool flight = s != null;

        // ① 観測
        foreach (var kv in toggles) ShowToggle(kv.Value, flight ? s.Switch(kv.Key) : practiceOn.Contains(kv.Key));
        if (lineWindow != null)
        {
            if (!flight)
            {
                lineWindow.Set("moon", T.Ink, Txt.FarSidePractice);
                lineFacing.Set("attitude", T.Ink, Txt.Facing);
                lineData.Set("science", T.Ink, string.Format(Txt.Data, 0));
            }
            else
            {
                if (s.inFarside) lineWindow.Set("check", T.Go, Txt.FarSideNow);
                else if (s.t < s.tLos) lineWindow.Set("cross", T.InkSub, string.Format(Txt.FarSideIn, Ui.Mmss(s.tLos - s.t)));
                else lineWindow.Set("cross", T.InkSub, Txt.FarSideOver);
                bool facing = s.attitude == "moon";
                lineFacing.Set(facing ? "check" : "cross", facing ? T.Go : T.InkSub, facing ? Txt.Facing : Txt.NotFacing);
                lineData.Set("science", T.Ink, string.Format(Txt.Data, Mathf.FloorToInt(s.dataCollected)));
            }
        }

        // ② 帰り道：M08 まで（t < m06_delay_s）と点火のあとは押せない。サーバーも同じ所で locked を返す
        bool locked = flight && (s.t < GameParams.Get("m06_delay_s") || s.burnDone);
        foreach (var r in Txt.Routes)
        {
            var b = routes[r.id];
            bool chosen = flight && s.route == r.id;
            b.Set(chosen ? LpLook.Selected : locked ? LpLook.Off : LpLook.Secondary, !locked);
            b.SetMark(chosen ? "check" : locked ? "lock" : null);
            b.SetText(null, flight && s.burnDone && chosen ? Txt.RouteFinal : locked && !s.burnDone ? Txt.RouteLocked : r.sub);
        }

        // 今できるかたまり：月の裏側の間は観測。それ以外でルートを選べてまだ選んでいなければ帰り道
        bool sciFocus = (flight && toggles.Count > 0 && s.inFarside) || lit.Contains("switch");
        bool routeFocus = (flight && !locked && string.IsNullOrEmpty(s.route) && !(toggles.Count > 0 && s.inFarside)) || lit.Contains("set_route");
        sci.Set(sciFocus, flight && (toggles.Count == 0 || s.t >= s.tAos));
        routeCard.Set(routeFocus, locked);

        // ③ 手伝い：CO2 が注意の値を超え、フィルターが残っていて、Life Support を自分で持っていないとき
        bool showHelp = flight && s.filters > 0 && s.co2 >= Co2Warn(s) && !Ctx.Model.MySeats.Contains(Seats.Life);
        if (help.Root.gameObject.activeSelf != showHelp) help.Root.gameObject.SetActive(showHelp);
        help.Set(showHelp, false);
        helpBtn.Set(LpLook.Primary);
        if (feedback == null) helpBtn.SetText(null, Txt.HelpSub);
    }

    static float Co2Warn(GameState s) => s.Threshold("co2", "warn", out var v) ? v : GameParams.Get("co2_warn");

    public override void OnEvent(GameEvent e)
    {
        if (e.kind != "press" || e.cmd != "swap_filter" || helpBtn == null || !help.Root.gameObject.activeSelf) return;
        feedback = LpKit.PressText(e, Seat);
        if (feedback == null) return;
        feedbackUntil = Time.unscaledTime + LpKit.FeedbackSec;
        helpBtn.SetText(null, feedback);
    }

    /// <summary>#84：練習の間「まだ試していない操作」を光らせる（switch → 観測、set_route → 帰り道）</summary>
    public override void Highlight(string cmd, bool on)
    {
        if (on) lit.Add(cmd); else lit.Remove(cmd);
        Refresh(Ctx.State);
    }

    public override void Tick(float now)
    {
        sci?.Tick(now); routeCard?.Tick(now); help?.Tick(now);
        if (feedback != null && now > feedbackUntil) { feedback = null; Refresh(Ctx.State); }
        strip?.Fit();
    }
}
