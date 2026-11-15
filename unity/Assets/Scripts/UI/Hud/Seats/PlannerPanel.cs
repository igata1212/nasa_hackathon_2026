// 書いた人：Claude ／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で CabinHud から分けた）／ 更新：iizuka（Opus 5.5、2026-11-14、#83）／ 更新：iizuka（Opus 5.5、2026-11-15、#190 あつ森に寄せた）
// 作る Issue：#83（Mission Planner のパネル）
//
// Mission Planner（◆）の席。かたまりは2つ＋手伝い1つ（#102「1席 2〜3 個」）：
//   ① 観測：設計で積んだ機器のスイッチだけ（材料の行につまみ。ON は電力を使う）。横に観測の条件2つ（月の裏側にいるか・月を向いているか）と集めたデータ
//      向きが違うと電力だけ減ってデータは増えない（SPEC 5章 補足）ことが、✗ の行で分かる
//   ② 帰り道：ルート A（速い・燃料を多く使う）／B（遅い・酸素と電力が長くもつ必要）。M08（t ≥ m06_delay_s）まで鍵、点火したら決まり
//      #190（ui-guidelines 3章「カードの選択」【11】）：色違いのカード2枚（A＝go、B＝plum）に A/B の大きな字・名前・家までの時間・燃料。
//      選んだカードは 1.12 倍＋右上にチェック＋手。点火して決まったら、選ばなかった方を line に。鍵の間は2枚とも line に鍵
//   ③ 手伝い：CO2 が注意の値を超えたら「Help swap」（Life Support と同時押しのフィルター交換。主ボタンで息をする）。Life Support を自分で持っているときは出さない
// 今できるかたまりには指さしの手（月の裏側の間は観測、それ以外でルート未選択なら帰り道）。終わったかたまりは薄くする
// 文言は下の Txt、部品と大きさは LifeSupportPanel.cs の LpKit と SeatPanel.cs の SeatLook
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
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
            ("camera", "camera", "Camera"),
            ("spectrometer", "spectrometer", "Spectrometer"),
            ("radar", "radar", "Radar"),
        };
        public const string On = "On: uses power", Off = "Off";
        public const string NoInstruments = "No science\ninstruments\non board.";
        public const string FarSideIn = "Far side in {0}", FarSideNow = "Behind the Moon", FarSideOver = "Far side is over";
        public const string FarSidePractice = "Only behind the Moon";
        public const string Facing = "Facing the Moon", NotFacing = "Pilot: face the Moon";
        public const string Data = "Data {0}";
        public const string Route = "Way home";
        // ルート：A は速いが燃料を多く使う、B は遅いので酸素と電力が長くもつ必要がある（M08 の台詞と同じ）。燃料の数字・帰りの分は parameters.json
        public static readonly (string id, string letter, string name, string timeKey, string fuelKey)[] Routes =
        {
            ("a", "A", "Fast", "time_p4_return_a_min", "fuel_burn_a"),
            ("b", "B", "Slow", "time_p4_return_b_min", "fuel_burn_b"),
        };
        public const string Pick = "Pick one", RouteLocked = "Opens soon", RouteFinal = "Locked in";
        public const string Help = "Help swap", HelpSub = "CO2 filter, with Life Support";
    }
    // カードの色（ガイドライン 3章「ルート A/B」。地図の A・B と同じ）
    static readonly Color[] RouteColors = { T.Go, T.Plum };

    /// <summary>ルートのカード1枚</summary>
    class RouteCard
    {
        public string Id;
        public UiCard Card;
        public Button Button;
        public readonly List<Graphic> Ink = new List<Graphic>();   // 面の上の文字とアイコン（鍵の間は textSoft）
        public TextMeshProUGUI Time;
        public bool Picked, Muted;
    }

    SeatStrip strip;
    LpCard sci, routeCard, help;
    readonly Dictionary<string, UiRow> rows = new Dictionary<string, UiRow>();
    readonly Dictionary<string, SeatKnob> knobs = new Dictionary<string, SeatKnob>();
    readonly List<RouteCard> routes = new List<RouteCard>();
    readonly HashSet<string> practiceOn = new HashSet<string>();   // 練習の間（state が無い）に押したスイッチ。見た目だけ
    string practiceRoute;                                          // 練習の間に押したルート。見た目だけ
    LpLine lineWindow, lineFacing, lineData;
    TextMeshProUGUI routeHead, routeNote;
    LpButton helpBtn;
    string feedback; float feedbackUntil;
    readonly HashSet<string> lit = new HashSet<string>();   // Highlight（#84 が「次に押す物」にしているコマンド）

    // ------------------------------------------------------------------ 作る
    protected override void BuildControls(RectTransform row)
    {
        strip = SeatLook.Strip(row, SeatLook.CursorRoom);   // 先頭の行を右向きの手で指すので、左に手の場所を空ける

        // ① 観測：材料の行（1列に2行まで）＋条件の3行
        sci = LpKit.Card(strip.Rect, "Science");
        RectTransform col = null;
        int n = 0;
        foreach (var ins in Txt.Instruments)
        {
            if (!Ctx.HasPart(ins.id)) continue;
            string id = ins.id;
            if (n++ % 2 == 0) col = UiKit.Col(sci.Root, T.S1, TextAnchor.MiddleLeft, 0f, 0f, true);
            var r = UiKit.Row(col, ins.icon, ins.name, null, T.Plum);
            SeatLook.Tight(r.Name);
            r.Sub.fontSize = T.TextLabel;
            SeatLook.Tight(r.Sub);
            r.Sub.gameObject.SetActive(true);
            SeatLook.ReserveWidth(r.Sub, Txt.On, Txt.Off);
            r.Dash.gameObject.SetActive(false);
            knobs[id] = SeatLook.Knob(r.Right);
            var hit = Ui.Rect("Hit", r.Root); UiKit.Fill(hit, -T.S1 / 2f);
            var hi = hit.gameObject.AddComponent<Image>(); hi.color = T.Clear; UiKit.Ignore(hi);
            hit.SetAsFirstSibling();
            SeatLook.Press(r.Root, r.Band, () => Toggle(id));
            rows[id] = r;
        }
        if (rows.Count == 0) LpKit.Note(sci.Root, Txt.NoInstruments, TextStyle.Label, T.TextSoft);
        else
        {
            var lines = UiKit.Col(sci.Root, 0f, TextAnchor.MiddleLeft);
            lineWindow = LpKit.Line(lines); lineFacing = LpKit.Line(lines); lineData = LpKit.Line(lines);
            SeatLook.ReserveWidth(lineWindow.Text, string.Format(Txt.FarSideIn, "0:00"), Txt.FarSideNow, Txt.FarSideOver, Txt.FarSidePractice);
            SeatLook.ReserveWidth(lineFacing.Text, Txt.Facing, Txt.NotFacing);
            SeatLook.ReserveWidth(lineData.Text, string.Format(Txt.Data, 9999));
        }

        // ② 帰り道：見出し＋カード2枚
        routeCard = LpKit.Card(strip.Rect, "Route");
        var head = UiKit.Col(routeCard.Root, 0f, TextAnchor.MiddleLeft);
        routeHead = UiKit.Text(head, Txt.Route, TextStyle.RowName, T.Text, TextAlignmentOptions.MidlineLeft);
        SeatLook.Tight(routeHead);
        routeNote = UiKit.Text(head, "", TextStyle.Label, T.TextSoft, TextAlignmentOptions.MidlineLeft);
        SeatLook.ReserveWidth(routeNote, Txt.Pick, Txt.RouteLocked, Txt.RouteFinal);
        var pair = UiKit.Row(routeCard.Root, T.S3, TextAnchor.MiddleLeft);
        pair.name = "Cards";
        for (int i = 0; i < Txt.Routes.Length; i++) routes.Add(MakeCard(pair, Txt.Routes[i], RouteColors[i % RouteColors.Length]));

        // ③ 手伝い（CO2 が高いときだけ出す。盆の右端なので、出ても消えてもほかのボタンの位置は動かない）
        SeatLook.Spacer(strip.Rect);
        help = LpKit.Card(strip.Rect, "Help");
        helpBtn = LpKit.Button(help.Root, "filter", Txt.Help, "", () => Ctx.SendDual("swap_filter", Seat));   // #210 補助の行は押した後の結果だけ（"" で作る）
        foreach (var sub in new[] { LpKit.PressWaiting, LpKit.PressNow, LpKit.PressTogether, LpKit.PressFailed }) helpBtn.Reserve(Txt.Help, sub);
        help.Root.gameObject.SetActive(false);
    }

    /// <summary>ルートのカード：A/B の大きな字＋名前、その下に時計と家までの時間・燃料（【11】のカード、【09】の値段）。
    /// UiCard.SetPicked は手前に出すため並びの最後へ移すので、1枚ずつ入れ物に入れて A・B の順を保つ</summary>
    RouteCard MakeCard(Transform pair, (string id, string letter, string name, string timeKey, string fuelKey) r, Color face)
    {
        var rc = new RouteCard { Id = r.id };
        var holder = UiKit.Row(pair, 0f, TextAnchor.MiddleCenter);
        holder.name = "Route_" + r.letter;
        rc.Card = UiKit.Card(holder, face);
        rc.Card.Root.GetComponent<VerticalLayoutGroup>().padding = new RectOffset((int)T.S3, (int)T.S3, (int)T.S2, (int)T.S2);
        var top = UiKit.Row(rc.Card.Content, T.S2, TextAnchor.MiddleLeft);
        var letter = UiKit.Text(top, r.letter, TextStyle.Big, T.Panel);
        var col = UiKit.Col(top, 0f, TextAnchor.MiddleLeft);
        var name = UiKit.Text(col, r.name, TextStyle.RowName, T.Panel, TextAlignmentOptions.MidlineLeft);
        SeatLook.Tight(name);
        var facts = UiKit.Row(col, T.S1 / 2f, TextAnchor.MiddleLeft);
        var clock = UiKit.Icon(facts, "clock", T.IconS, T.Panel);
        rc.Time = UiKit.Text(facts, "", TextStyle.Price, T.Panel, TextAlignmentOptions.MidlineLeft);
        SeatLook.ReserveWidth(rc.Time, "0:00");
        UiKit.Fixed(Ui.Rect("Gap", facts), T.S1, 0f);
        var fuelIc = UiKit.Icon(facts, "fuel", T.IconS, T.Panel);
        var fuel = UiKit.Text(facts, Mathf.RoundToInt(GameParams.Get(r.fuelKey)).ToString(), TextStyle.Price, T.Panel, TextAlignmentOptions.MidlineLeft);
        rc.Ink.AddRange(new Graphic[] { letter, name, clock, rc.Time, fuelIc, fuel });
        string id = r.id;
        rc.Button = SeatLook.Press(rc.Card.Root, rc.Card.Face, () => PickRoute(id), squish: false);   // 大きさは SetPicked（1.12 倍）が動かす
        return rc;
    }

    void PickRoute(string id)
    {
        Ctx.Send(Cmd.Route(Seat, id));
        if (Ctx.State == null) practiceRoute = id;   // 練習の間は state が来ないので、押したカードを見た目だけ選ぶ
        Refresh(Ctx.State);
    }

    void Toggle(string id)
    {
        var s = Ctx.State;
        bool cur = s != null ? s.Switch(id) : practiceOn.Contains(id);
        Ctx.Send(Cmd.Switch(Seat, id, !cur));
        if (s == null) { if (cur) practiceOn.Remove(id); else practiceOn.Add(id); }
        // 押した手ごたえ：先に見た目を変える（次の state で正しい値に戻る。記録の再生では戻る）
        ShowToggle(id, !cur);
        UiSelect.CursorOf(Root).Poke();
    }

    void ShowToggle(string id, bool on)
    {
        knobs[id].Set(on);
        rows[id].Sub.text = on ? Txt.On : Txt.Off;
    }

    // ------------------------------------------------------------------ 状態・イベント
    public override void Refresh(GameState s)
    {
        base.Refresh(s);
        if (sci == null) return;   // stir の段階（この席は何も出さない）
        bool flight = s != null;

        // ① 観測
        foreach (var kv in rows) ShowToggle(kv.Key, flight ? s.Switch(kv.Key) : practiceOn.Contains(kv.Key));
        if (lineWindow != null)
        {
            if (!flight)
            {
                lineWindow.Set("moon", T.Text, Txt.FarSidePractice);
                lineFacing.Set("attitude", T.Text, Txt.Facing);
                lineData.Set("science", T.Text, string.Format(Txt.Data, 0));
            }
            else
            {
                if (s.inFarside) lineWindow.Set("check", T.Go, Txt.FarSideNow);
                else if (s.t < s.tLos) lineWindow.Set("cross", T.TextSoft, string.Format(Txt.FarSideIn, Ui.Mmss(s.tLos - s.t)));
                else lineWindow.Set("cross", T.TextSoft, Txt.FarSideOver);
                bool facing = s.attitude == "moon";
                lineFacing.Set(facing ? "check" : "cross", facing ? T.Go : T.TextSoft, facing ? Txt.Facing : Txt.NotFacing);
                lineData.Set("science", T.Text, string.Format(Txt.Data, Mathf.FloorToInt(s.dataCollected)));
            }
        }

        // ② 帰り道：M08 まで（t < m06_delay_s）と点火のあとは押せない。サーバーも同じ所で locked を返す
        bool early = flight && s.t < GameParams.Get("m06_delay_s");
        bool final = flight && s.burnDone;
        string chosen = flight ? s.route : practiceRoute;
        RouteCard chosenCard = null;
        foreach (var rc in routes)
        {
            bool picked = !string.IsNullOrEmpty(chosen) && chosen == rc.Id;
            if (picked) chosenCard = rc;
            SetCard(rc, picked && !early, early || (final && !picked), early);
            rc.Button.interactable = !early && !final;
            var r = System.Array.Find(Txt.Routes, x => x.id == rc.Id);
            float home = flight ? s.RouteSplashdown(rc.Id) - s.t : GameParams.Get(r.timeKey) * 60f;   // 練習の間は parameters.json の帰りの分
            rc.Time.text = home > 0f ? Ui.Mmss(home) : "--";
        }
        routeNote.text = early ? Txt.RouteLocked : final ? Txt.RouteFinal : chosenCard == null ? Txt.Pick : "";
        routeNote.gameObject.SetActive(routeNote.text.Length > 0);

        // 今できるかたまり：月の裏側の間は観測。それ以外でルートを選べてまだ選んでいなければ帰り道
        bool sciFocus = (flight && rows.Count > 0 && s.inFarside) || lit.Contains("switch");
        bool routeFocus = (flight && !early && !final && string.IsNullOrEmpty(s.route) && !(rows.Count > 0 && s.inFarside)) || lit.Contains("set_route");
        sci.Set(sciFocus, flight && (rows.Count == 0 || s.t >= s.tAos));
        routeCard.Set(routeFocus, early);   // 決まった後は選んだカードを薄くしない（選ばなかった方だけ line）

        // ③ 手伝い：CO2 が注意の値を超え、フィルターが残っていて、Life Support を自分で持っていないとき
        bool showHelp = flight && s.filters > 0 && s.co2 >= Co2Warn(s) && !Ctx.Model.MySeats.Contains(Seats.Life);
        if (help.Root.gameObject.activeSelf != showHelp) help.Root.gameObject.SetActive(showHelp);
        help.Set(showHelp, false);
        helpBtn.Set(LpLook.Primary);
        helpBtn.SetNow(showHelp);
        if (feedback == null) helpBtn.SetText(null, "");   // #210 押す前は1行（null は「変えない」なので "" で消す）

        // 手（1画面に1つ）：手伝い ＞ 観測（まだ OFF の機器）＞ ルートを選ぶ（見出し。A と B のどちらかを勧めない）＞ 選んだカード
        if (showHelp) Point(helpBtn.Root, CursorDir.UpLeft, true);
        else if (sciFocus && rows.Count > 0) Point(FirstOff(s), CursorDir.Right, true);
        else if (routeFocus && chosenCard == null) Point(routeHead.rectTransform, CursorDir.Right, true);
        else if (chosenCard != null && !early) Point(chosenCard.Card.Root, CursorDir.UpLeft, routeFocus);
        else Point(null, CursorDir.UpLeft);
    }

    /// <summary>観測の行で、まだ OFF の最初の物（全部 ON なら最初の行）</summary>
    RectTransform FirstOff(GameState s)
    {
        RectTransform first = null;
        foreach (var ins in Txt.Instruments)
        {
            if (!rows.TryGetValue(ins.id, out var r)) continue;
            if (first == null) first = r.Root;
            bool on = s != null ? s.Switch(ins.id) : practiceOn.Contains(ins.id);
            if (!on) return r.Root;
        }
        return first;
    }

    /// <summary>カードの見た目：picked＝1.12 倍＋チェック、muted＝line の面（文字は textSoft）、locked＝右上に鍵。変わったときだけ（弾みを出し直さない）</summary>
    static void SetCard(RouteCard rc, bool picked, bool muted, bool locked)
    {
        if (picked != rc.Picked) { rc.Picked = picked; rc.Card.SetPicked(picked); }
        if (muted != rc.Muted)
        {
            rc.Muted = muted;
            rc.Card.SetMuted(muted);
            foreach (var g in rc.Ink) g.color = muted ? T.TextSoft : T.Panel;
        }
        SeatLook.Lock(rc.Card.Root, locked);
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

    /// <summary>#84：練習の間「まだ試していない操作」を「次に押す物」にする（switch → 観測、set_route → 帰り道）</summary>
    public override void Highlight(string cmd, bool on)
    {
        if (on ? !lit.Add(cmd) : !lit.Remove(cmd)) return;   // #84 は毎フレーム呼ぶので、変わったときだけ描き直す
        Refresh(Ctx.State);
    }

    public override void Tick(float now)
    {
        if (feedback != null && now > feedbackUntil) { feedback = null; Refresh(Ctx.State); }
        strip?.Fit();
        PokeCursor(now);
    }
}
