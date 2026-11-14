// 書いた人：Claude ／ 最終更新：2026-11-14
// Cabin：船内（3D）の上に重ねる HUD。ゲージ・時間・カウントダウン・席の操作ボタン。見た目は仮。値は OnState から入れる
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CabinHud
{
    public readonly GameObject Root;
    readonly GameFlow f;

    class Gauge { public Image fill; public Text value; public bool inverse; public float max; }
    readonly Dictionary<string, Gauge> gauges = new Dictionary<string, Gauge>();
    readonly Text clock, chipFar, chipBurn, banner, pressText, offlineNote;
    readonly Image chipFarBg, chipBurnBg;
    readonly RectTransform seatPanel, tabRow;
    string activeSeat;
    string lastBuiltSeat; int lastBuiltPhaseKey = -1;
    readonly Dictionary<string, Button> toggles = new Dictionary<string, Button>();   // "sw:heater" など
    readonly Dictionary<string, Button> attBtns = new Dictionary<string, Button>();
    readonly Dictionary<string, Button> routeBtns = new Dictionary<string, Button>();
    Button swapBtn; string swapLabel;   // フィルター交換。残り回数（state.filters）をラベルに足す

    static readonly string[] Attitudes = { "earth", "moon", "sun", "burn", "heatshield" };

    public CabinHud(Transform parent, GameFlow f, RenderTexture trajectoryRt = null)
    {
        this.f = f;
        var root = Ui.Rect("Cabin", parent); Root = root.gameObject; Ui.Fill(root);

        // 上：時間とカウントダウン
        var top = Ui.PanelImg(root, new Color(0.07f, 0.1f, 0.2f, 0.7f), "TopBar");
        Ui.Anchor(top.rectTransform, 0, 0.93f, 1, 1);
        clock = Ui.Label(top.transform, "T+ 0:00", 36, Ui.Cream, TextAnchor.MiddleLeft, FontStyle.Bold);
        Ui.Anchor(clock.rectTransform, 0.02f, 0, 0.2f, 1);
        chipFarBg = Ui.PanelImg(top.transform, Ui.Gray, "ChipFar");
        Ui.Anchor(chipFarBg.rectTransform, 0.25f, 0.1f, 0.55f, 0.9f);
        chipFar = Ui.Label(chipFarBg.transform, "", 26, Ui.Cream, TextAnchor.MiddleCenter, FontStyle.Bold); Ui.Fill(chipFar.rectTransform);
        chipBurnBg = Ui.PanelImg(top.transform, Ui.Gray, "ChipBurn");
        Ui.Anchor(chipBurnBg.rectTransform, 0.57f, 0.1f, 0.87f, 0.9f);
        chipBurn = Ui.Label(chipBurnBg.transform, "", 26, Ui.Cream, TextAnchor.MiddleCenter, FontStyle.Bold); Ui.Fill(chipBurn.rectTransform);

        // 左：ゲージ
        var gp = Ui.VBox(root, 6, 10);
        Ui.Anchor(gp, 0.01f, 0.45f, 0.2f, 0.92f);
        gp.gameObject.AddComponent<Image>().color = new Color(0.07f, 0.1f, 0.2f, 0.65f);
        foreach (var g in new[] { ("power", "POWER", false), ("oxygen", "OXYGEN", false), ("temp", "TEMP", false), ("co2", "CO2", true), ("fuel", "FUEL", false) })
            AddGauge(gp, g.Item1, g.Item2, g.Item3);

        // 右上：軌道の小窓（GameFlow.trajectory を専用カメラで描いた RenderTexture）
        if (trajectoryRt != null)
        {
            var frame = Ui.PanelImg(root, new Color(0.07f, 0.1f, 0.2f, 0.8f), "TrajectoryMini");
            Ui.Anchor(frame.rectTransform, 0.79f, 0.66f, 0.99f, 0.92f);
            var img = Ui.Rect("Map", frame.transform); Ui.Anchor(img, 0, 0, 1, 1, 4, 4, 4, 22);
            img.gameObject.AddComponent<RawImage>().texture = trajectoryRt;
            var cap = Ui.Label(frame.transform, "TRAJECTORY", 16, Ui.Orange, TextAnchor.UpperLeft, FontStyle.Bold);
            Ui.Anchor(cap.rectTransform, 0, 0.88f, 1, 1, 8, 0, 8, 2);
        }

        // 中央上：段階の案内（練習・かき混ぜ）
        banner = Ui.Label(root, "", 34, Ui.Yellow, TextAnchor.MiddleCenter, FontStyle.Bold);
        Ui.Anchor(banner.rectTransform, 0.25f, 0.84f, 0.75f, 0.92f);
        pressText = Ui.Label(root, "", 30, Ui.Orange, TextAnchor.MiddleCenter, FontStyle.Bold);
        Ui.Anchor(pressText.rectTransform, 0.25f, 0.77f, 0.75f, 0.84f);
        foreach (var tx in new[] { banner, pressText })   // 船室の明るい壁の上でも読めるように縁取り
        {
            var ol = tx.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(0.03f, 0.05f, 0.12f, 0.95f); ol.effectDistance = new Vector2(2f, -2f);
        }
        offlineNote = Ui.Label(root, "", 20, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleRight);
        Ui.Anchor(offlineNote.rectTransform, 0.6f, 0.21f, 0.99f, 0.25f);

        // 下：自分の席の操作
        var bottom = Ui.PanelImg(root, new Color(0.07f, 0.1f, 0.2f, 0.78f), "SeatPanel");
        Ui.Anchor(bottom.rectTransform, 0.01f, 0.01f, 0.99f, 0.2f);
        tabRow = Ui.HBox(bottom.transform, 8, 0, TextAnchor.MiddleLeft, false);
        Ui.Anchor(tabRow, 0, 0.7f, 1, 1, 10, 0, 10, 4);
        seatPanel = Ui.Rect("Controls", bottom.transform);
        Ui.Anchor(seatPanel, 0, 0, 1, 0.7f, 10, 8, 10, 4);
    }

    void AddGauge(Transform parent, string key, string title, bool inverse)
    {
        var row = Ui.Rect("Gauge_" + key, parent); Ui.Size(row, h: 52);
        var t = Ui.Label(row, title, 20, Ui.Cream); Ui.Anchor(t.rectTransform, 0, 0.5f, 0.6f, 1);
        var v = Ui.Label(row, "", 20, Ui.Cream, TextAnchor.MiddleRight); Ui.Anchor(v.rectTransform, 0.5f, 0.5f, 1, 1);
        var bar = Ui.PanelImg(row, new Color(1, 1, 1, 0.15f), "Bar"); Ui.Anchor(bar.rectTransform, 0, 0.08f, 1, 0.45f);
        var fill = Ui.PanelImg(bar.transform, Ui.Green, "Fill"); Ui.Fill(fill.rectTransform);
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
        // Image に sprite が無いと Filled が効かないので、白い 1x1 のスプライトを共用する
        fill.sprite = WhiteSprite();
        gauges[key] = new Gauge { fill = fill, value = v, inverse = inverse };
    }

    static Sprite white;
    static Sprite WhiteSprite()
    {
        if (white != null) return white;
        var tex = Texture2D.whiteTexture;
        white = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        return white;
    }

    // ------------------------------------------------------------------ 更新
    public void Refresh()
    {
        var s = f.Model.State;
        var m = f.Model;
        // 練習・かき混ぜの間は state がまだ来ない（flight から毎秒）。それでも席のボタンは出す（出さないと練習もかき混ぜもできない）
        if (s == null) { clock.text = "T+ 0:00"; RefreshSeat(); return; }
        clock.text = "T+ " + Ui.Mmss(s.t);

        SetGauge("power", s.power, Mathf.Max(s.capacity, 1f), s.power.ToString("0"));
        SetGauge("oxygen", s.oxygen, m.Max("oxygen", s.oxygen), s.oxygen.ToString("0"));
        SetGauge("temp", s.temp, m.Max("temp", s.temp), s.temp.ToString("0.0") + "C");
        SetGauge("co2", s.co2, 100f, s.co2.ToString("0"));
        SetGauge("fuel", s.fuel, m.Max("fuel", s.fuel), s.fuel.ToString("0"));

        // 月の裏側と噴射窓
        if (s.t < s.tLos) Chip(chipFarBg, chipFar, "FAR SIDE (NO SIGNAL) IN " + Ui.Mmss(s.tLos - s.t), Ui.Gray);
        else if (s.t < s.tAos) Chip(chipFarBg, chipFar, "FAR SIDE  " + Ui.Mmss(s.tAos - s.t) + " LEFT", Ui.Red);
        else Chip(chipFarBg, chipFar, "SIGNAL RESTORED", Ui.Teal);
        if (s.burnDone) Chip(chipBurnBg, chipBurn, "BURN DONE", Ui.Teal);
        else if (s.t < s.tBurnOpen) Chip(chipBurnBg, chipBurn, "BURN WINDOW IN " + Ui.Mmss(s.tBurnOpen - s.t), Ui.Gray);
        else if (s.t < s.tBurnClose) Chip(chipBurnBg, chipBurn, "BURN WINDOW OPEN  " + Ui.Mmss(s.tBurnClose - s.t), Ui.Orange);
        else Chip(chipBurnBg, chipBurn, "BURN WINDOW CLOSED", Ui.Gray);

        RefreshSeat();
        RefreshToggles(s);
    }

    void Chip(Image bg, Text t, string s, Color c) { t.text = s; bg.color = c; }

    void SetGauge(string key, float v, float max, string text)
    {
        var g = gauges[key];
        float frac = Mathf.Clamp01(v / Mathf.Max(max, 0.0001f));
        g.fill.fillAmount = frac;
        g.value.text = text;
        float bad = g.inverse ? frac : 1f - frac;           // 悪さ（0 = 良い）
        g.fill.color = bad < 0.5f ? Ui.Green : bad < 0.75f ? Ui.Yellow : Ui.Red;
    }

    public void ShowPress(GameEvent e)
    {
        if (e.status == "waiting") pressText.text = e.cmd.ToUpper() + ": WAITING FOR PARTNER...";
        else if (e.status == "together") pressText.text = e.cmd.ToUpper() + ": TOGETHER!";
        else if (e.status == "failed") pressText.text = e.cmd.ToUpper() + ": MISSED. TRY AGAIN";
        f.ClearPressLater();
    }
    public void ClearPress() { pressText.text = ""; }

    // ------------------------------------------------------------------ 席の操作（席ごとに 2〜3 個）
    void RefreshSeat()
    {
        var seats = f.Model.MySeats;
        if (seats.Count == 0) return;
        if (activeSeat == null || !seats.Contains(activeSeat)) activeSeat = seats[0];
        string ph = f.Model.Phase;
        int key = ph == "stir" ? 1 : 0;
        banner.text = ph == "practice" ? "PRACTICE: one control per seat  (" + f.Model.Practiced.Count + "/4)"
                    : ph == "stir" ? "LIFE SUPPORT: STIR THE OXYGEN TANK!" : "";
        offlineNote.text = f.Client is OfflineClient ? "OFFLINE REPLAY: controls are ignored" : "";
        if (activeSeat != lastBuiltSeat || key != lastBuiltPhaseKey || tabRow.childCount != seats.Count)
        {
            lastBuiltSeat = activeSeat; lastBuiltPhaseKey = key;
            BuildSeat();
        }
    }

    static void Clear(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--) { var c = t.GetChild(i); c.SetParent(null); Object.Destroy(c.gameObject); }
    }

    void BuildSeat()
    {
        Clear(tabRow); Clear(seatPanel);
        toggles.Clear(); attBtns.Clear(); routeBtns.Clear(); swapBtn = null;
        foreach (var s in f.Model.MySeats)
        {
            string seat = s;
            bool on = seat == activeSeat;
            var b = Ui.Btn(tabRow, Seats.Icon(seat) + " " + Seats.Title(seat), on ? Seats.Tint(seat) : Ui.Gray, () => { activeSeat = seat; Refresh(); }, 22, on ? Ui.Navy : Ui.Cream);
            Ui.Size(b, w: 300);
        }
        var row = Ui.HBox(seatPanel, 12, 0, TextAnchor.MiddleLeft, true); Ui.Fill(row);
        string me = activeSeat;
        var mine = f.Model.MySeats;
        bool bothIgnite = mine.Contains(Seats.Pilot) && mine.Contains(Seats.Comms);
        // 同じ人が swap_filter の相手の席（Pilot / Comms / Planner のどれか）も持っているか
        string filterMate = mine.Contains(Seats.Life) ? (mine.Contains(Seats.Pilot) ? Seats.Pilot : mine.Contains(Seats.Comms) ? Seats.Comms : mine.Contains(Seats.Planner) ? Seats.Planner : null) : null;
        Color tint = Seats.Tint(me);
        if (f.Model.Phase == "stir")
        {
            if (me == Seats.Life) Ui.Btn(row, "STIR", tint, () => f.Client.Send(Cmd.Simple("stir", me)), 40);
            return;
        }
        switch (me)
        {
            case Seats.Pilot:
                var att = Ui.HBox(row, 6, 0, TextAnchor.MiddleLeft, true); Ui.Size(att, flexW: 3);
                foreach (var a in Attitudes) { string aa = a; attBtns[a] = Ui.Btn(att, a.ToUpper(), Ui.Gray, () => f.Client.Send(Cmd.Attitude(me, aa)), 20, Ui.Cream); }
                Ui.Size(Ui.Btn(row, "CORRECTION BURN", tint, () => f.Client.Send(Cmd.Simple("correction_burn", me)), 24), flexW: 1);
                if (bothIgnite) Ui.Size(Ui.Btn(row, "IGNITE (both seats)", tint, SendBothIgnite, 24), flexW: 1);
                else Ui.Size(Ui.Btn(row, "IGNITE (with Comms)", tint, () => f.Client.Send(Cmd.Simple("ignite", me)), 24), flexW: 1);
                break;
            case Seats.Life:
                Ui.Size(Ui.Btn(row, "CLOSE VALVE 1", tint, () => f.Client.Send(Cmd.Valve(me, 1)), 26), flexW: 1);
                Ui.Size(Ui.Btn(row, "CLOSE VALVE 2", tint, () => f.Client.Send(Cmd.Valve(me, 2)), 26), flexW: 1);
                if (filterMate != null) { string mate = filterMate; swapLabel = "SWAP FILTER (both seats)"; swapBtn = Ui.Btn(row, swapLabel, tint, () => { f.Client.Send(Cmd.Simple("swap_filter", Seats.Life)); f.Client.Send(Cmd.Simple("swap_filter", mate)); }, 22); }
                else { swapLabel = "SWAP CO2 FILTER (with a partner)"; swapBtn = Ui.Btn(row, swapLabel, tint, () => f.Client.Send(Cmd.Simple("swap_filter", me)), 22); }
                Ui.Size(swapBtn, flexW: 1);
                break;
            case Seats.Comms:
                var sw = Ui.HBox(row, 6, 0, TextAnchor.MiddleLeft, true); Ui.Size(sw, flexW: 3);
                foreach (var n in new[] { "heater", "lights", "nav", "comms" }) AddToggle(sw, "sw:" + n, n.ToUpper(), me, () => { var st = f.Model.State; return st != null && st.Switch(n); }, v => Cmd.Switch(me, n, v), tint);
                AddToggle(row, "tx", "TRANSMIT", me, () => f.Model.State != null && f.Model.State.transmit, v => Cmd.Transmit(me, v), tint);
                if (bothIgnite) Ui.Size(Ui.Btn(row, "IGNITE (both seats)", tint, SendBothIgnite, 24), flexW: 1);
                else Ui.Size(Ui.Btn(row, "IGNITE (with Pilot)", tint, () => f.Client.Send(Cmd.Simple("ignite", me)), 24), flexW: 1);
                break;
            case Seats.Planner:
                var ins = Ui.HBox(row, 6, 0, TextAnchor.MiddleLeft, true); Ui.Size(ins, flexW: 3);
                foreach (var n in new[] { "camera", "spectrometer", "radar" })
                    if (f.HasPart(n)) AddToggle(ins, "sw:" + n, n.ToUpper(), me, () => { var st = f.Model.State; return st != null && st.Switch(n); }, v => Cmd.Switch(me, n, v), tint);
                var rt = Ui.HBox(row, 6, 0, TextAnchor.MiddleLeft, true); Ui.Size(rt, flexW: 2);
                foreach (var r in new[] { "a", "b" }) { string rr = r; routeBtns[r] = Ui.Btn(rt, "ROUTE " + r.ToUpper(), Ui.Gray, () => f.Client.Send(Cmd.Route(me, rr)), 24, Ui.Cream); }
                break;
        }
    }

    void SendBothIgnite() { f.Client.Send(Cmd.Simple("ignite", Seats.Pilot)); f.Client.Send(Cmd.Simple("ignite", Seats.Comms)); }

    void AddToggle(Transform parent, string key, string label, string seat, System.Func<bool> get, System.Func<bool, Dictionary<string, object>> make, Color tint)
    {
        var b = Ui.Btn(parent, label, Ui.Gray, () => f.Client.Send(make(!get())), 22, Ui.Cream);
        toggles[key] = b;
        b.name = label;
        togglesState[key] = (label, tint);
    }
    readonly Dictionary<string, (string label, Color tint)> togglesState = new Dictionary<string, (string, Color)>();

    void RefreshToggles(GameState s)
    {
        foreach (var kv in toggles)
        {
            bool on;
            if (kv.Key == "tx") on = s.transmit;
            else on = s.Switch(kv.Key.Substring(3));
            var (label, tint) = togglesState[kv.Key];
            Ui.SetBtn(kv.Value, label + (on ? "  ON" : "  OFF"), on ? tint : Ui.Gray, on ? Ui.Navy : Ui.Cream);
        }
        if (swapBtn != null)
        {
            bool left = s.filters > 0;
            Ui.SetBtn(swapBtn, left ? swapLabel + "  x" + s.filters : "NO FILTERS LEFT", left ? Seats.Tint(Seats.Life) : Ui.Gray, left ? Ui.Navy : Ui.Cream);
        }
        foreach (var kv in attBtns) { bool on = s.attitude == kv.Key; Ui.SetBtn(kv.Value, kv.Key.ToUpper(), on ? Seats.Tint(Seats.Pilot) : Ui.Gray, on ? Ui.Navy : Ui.Cream); }
        foreach (var kv in routeBtns) { bool on = s.route == kv.Key; Ui.SetBtn(kv.Value, "ROUTE " + kv.Key.ToUpper(), on ? Seats.Tint(Seats.Planner) : Ui.Gray, on ? Ui.Navy : Ui.Cream); }
    }
}
