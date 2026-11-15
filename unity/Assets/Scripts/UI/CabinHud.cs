// 書いた人：Claude ／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で土台と部品に分けた）／ 更新：iizuka（Opus 5.5、2026-11-14、#146 統合で領域の割合を直した・大きい地図の slot）
// Cabin：船内（3D）の上に重ねる HUD の土台。土台がするのは3つだけ：
//   ① 画面の領域（HudSlot）を作る。置き場所（割合）は下の Layout の1か所で決める（HUD の割り付けが決まったらここだけ直す）
//   ② 部品（HudPart）に状態・イベント・照明 OFF・毎フレームを配る
//   ③ 席タブで今の席を選び、その席の SeatPanel を作り直す（席・段階（stir かどうか）・席の数が変わったとき）
// 部品の中身は Scripts/UI/Hud/（1部品1ファイル。どの Issue が作るかは各ファイルの先頭）。部品の約束は Hud/HudPart.cs
// GameFlow から見える形：コンストラクタ・Refresh()・OnEvent(e)・Tick(now)・ShowPress(e)・ClearPress()
// 載せ先（#167）：parent は画面の Canvas でも、3D の中の world-space の Canvas の RectTransform でもよい。割合は parent の大きさに対するもの
//   部品は slot の RectTransform の中だけに作る（Screen の大きさ・CanvasScaler は使わない）。席や部品を別の Canvas に分けたいときは Layout の slot を差し替える
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>HUD の画面の領域。どの部品がどこに入るかは CabinHud のコンストラクタ</summary>
public enum HudSlot { Overlay, TopCenter, Left, TopRight, CenterTop, CenterMid, Right, Bottom, MapBig }

public class CabinHud
{
    public readonly GameObject Root;
    readonly HudContext ctx;
    readonly Dictionary<HudSlot, RectTransform> slots = new Dictionary<HudSlot, RectTransform>();
    readonly List<HudPart> parts = new List<HudPart>();
    readonly BurnCheck burn;
    readonly SeatTabBar tabs;
    readonly RectTransform controls;
    readonly Text offlineNote;
    SeatPanel seatPanel;
    string activeSeat;
    string lastBuiltSeat; int lastBuiltPhaseKey = -1;
    bool lightsOff;

    // 画面の領域（Cabin 全体に対する割合 x0, y0, x1, y1）。並び = 描く順（後ろほど手前）
    // #146：各部品の大きさに合わせた（1600×900 で：上の帯 81・左 459・真ん中 288・下 207）。ヒューストンの吹き出し（HoustonStyle.Cabin：下 0.25〜約 0.47、
    //   横 0.22〜0.78）・先生の小窓（吹き出しの左下 192×120）と重ならないように、真ん中は 0.47 から上、左は 0.39 から上
    static readonly (HudSlot slot, float x0, float y0, float x1, float y1)[] Layout =
    {
        (HudSlot.Overlay,   0f,    0f,    1f,    1f),      // 暗幕・霜（3D の上、ほかの部品の下）・SLOW の札（右下）
        (HudSlot.TopCenter, 0f,    0.91f, 1f,    1f),      // タイムラインの帯（時計・月の裏側・噴射窓・次までのチップ 64）
        (HudSlot.Left,      0.01f, 0.39f, 0.2f,  0.9f),    // 状態（ゲージ。高さ 424 が等倍で入る）
        (HudSlot.TopRight,  0.79f, 0.64f, 0.99f, 0.9f),    // 軌道の地図（小窓）
        (HudSlot.CenterTop, 0.22f, 0.79f, 0.78f, 0.9f),    // 練習の案内・危機の帯（同時には出ない。危機の帯は上端から 72／96 下がる）
        (HudSlot.CenterMid, 0.22f, 0.42f, 0.78f, 0.915f),   // 同時押しの合図・点火の準備（上端から下へ伸びる）
        (HudSlot.Right,     0.79f, 0.45f, 0.99f, 0.63f),   // 要件トラッカー（#90。まだ空）。オフライン再生の操作（GameFlow）・切断の札（ConnectionUi）も地図の下に出る
        (HudSlot.Bottom,    0.01f, 0.01f, 0.99f, 0.24f),   // 席タブ（48）＋席のパネル（143）
        (HudSlot.MapBig,    0f,    0f,    1f,    0.79f),  // 大きく開いた軌道の地図（#212：全画面。上の帯と「いま何をするか」の行は隠さない。いちばん手前）
    };
    const float TabH = 48f, TabGapTop = 4f, ControlsPadBottom = 8f, PadX = 10f;   // 下の領域の中（px）

    public CabinHud(Transform parent, GameFlow f, RenderTexture trajectoryRt = null)
    {
        ctx = new HudContext(f, trajectoryRt) { Hud = this };
        var root = Ui.Rect("Cabin", parent); Root = root.gameObject; Ui.Fill(root);
        foreach (var l in Layout)
        {
            var r = Ui.Rect("Slot_" + l.slot, root);
            Ui.Anchor(r, l.x0, l.y0, l.x1, l.y1);
            slots[l.slot] = r;
        }

        // 部品（同じ slot に2つ入れたら、後に足した方が手前）
        Add(new CabinOverlay(), HudSlot.Overlay);
        Add(new TimelineBar(), HudSlot.TopCenter);
        Add(new StatusPanel(), HudSlot.Left);
        Add(new MapWidget(), HudSlot.TopRight);
        Add(new PracticeGuide(), HudSlot.CenterTop);
        Add(new CrisisBanner(), HudSlot.CenterTop);
        burn = Add(new BurnCheck(), HudSlot.CenterMid);
        Add(new ReqTracker(), HudSlot.Right);
        Add(new GmatGauges(), HudSlot.Overlay);   // #167（Taiga）：NASA GMAT の軌道の値のメーター3つ。左のポップアップ（MapWidget の後に足す：その値を読む）
        Add(new BottomFold(), HudSlot.Overlay);   // #167（Taiga）：下の席のパネルを矢印のつまみでしまう・出す（Bottom の slot を上下に動かすだけ）

        offlineNote = Ui.Label(root, "", 18, new Color(1, 1, 1, 0.55f), TextAnchor.UpperRight);
        Ui.Anchor(offlineNote.rectTransform, 0.79f, 0.47f, 0.99f, 0.535f);   // 地図の下・オフライン再生の操作（GameFlow の debugBar）の下
        offlineNote.resizeTextForBestFit = true; offlineNote.resizeTextMinSize = 12; offlineNote.resizeTextMaxSize = 18;

        // 下：上の段に席タブ、下の段に今の席のパネル
        var bottom = Ui.PanelImg(slots[HudSlot.Bottom], new Color(0.07f, 0.1f, 0.2f, 0.78f), "SeatPanel"); Ui.Fill(bottom.rectTransform);
        var tabRect = Ui.Rect("Tabs", bottom.transform);
        Ui.Anchor(tabRect, 0, 1, 1, 1, PadX, -(TabGapTop + TabH), PadX, TabGapTop);
        tabs = Add(new SeatTabBar(), tabRect);
        controls = Ui.Rect("Controls", bottom.transform);
        Ui.Anchor(controls, 0, 0, 1, 1, PadX, ControlsPadBottom, PadX, TabGapTop * 2f + TabH);
    }

    T Add<T>(T part, HudSlot slot) where T : HudPart => Add(part, slots[slot]);

    /// <summary>画面の領域（部品が slot の外に出すもの：大きい地図など）。#167 で載せ先を替えるときもここから取る</summary>
    internal RectTransform Slot(HudSlot slot) => slots[slot];

    /// <summary>#167：slot を中の部品ごと別の載せ先へ移す（3D の計器に置いた world-space の Canvas の RectTransform など）。
    /// 載せ先いっぱいに広げる。部品は slot の大きさに合わせて組むので、載せ先の大きさ（px）が元の slot と近いと同じ見た目になる</summary>
    public void MoveSlot(HudSlot slot, RectTransform parent)
    {
        var r = slots[slot];
        r.SetParent(parent, false);
        Ui.Fill(r);
        Refresh();
    }
    T Add<T>(T part, RectTransform slot) where T : HudPart
    {
        part.Attach(ctx, slot);
        parts.Add(part);
        return part;
    }

    /// <summary>今ある部品全部（今の席のパネルを含む）。配っている途中に席のパネルが作り直されても困らないように、毎回写しを返す</summary>
    internal List<HudPart> AllParts()
    {
        var list = new List<HudPart>(parts);
        if (seatPanel != null) list.Add(seatPanel);
        return list;
    }

    // ------------------------------------------------------------------ 配る
    /// <summary>状態が来たとき・段階が変わったとき（GameFlow が呼ぶ）。練習・かき混ぜの間は State が null</summary>
    public void Refresh()
    {
        var s = ctx.State;
        UpdateLights(s);
        bool plan = ctx.ShowPlannerView;   // 地図・タイムラインは Planner の席の人だけ（#157、#200）。席が変わるたびにここで見直す
        slots[HudSlot.TopCenter].gameObject.SetActive(plan);
        slots[HudSlot.TopRight].gameObject.SetActive(plan);
        foreach (var p in parts) p.Refresh(s);
        RefreshSeat(s);
    }

    /// <summary>サーバーのイベント全部（GameFlow が Model を直した後に呼ぶ）</summary>
    public void OnEvent(GameEvent e)
    {
        foreach (var p in AllParts()) p.OnEvent(e);
        if (e.kind == "practice") Refresh();   // 練習した席（Model.Practiced）が変わった
    }

    /// <summary>毎フレーム（GameFlow.Update が Time.unscaledTime で呼ぶ）</summary>
    public void Tick(float now)
    {
        foreach (var p in parts) p.Tick(now);
        seatPanel?.Tick(now);
    }

    /// <summary>#212：大きい地図が開いている間は下の席の盆を隠す（MapWidget が呼ぶ）</summary>
    internal void SetMapOpen(bool open) => slots[HudSlot.Bottom].gameObject.SetActive(!open);

    public void ShowPress(GameEvent e) { foreach (var p in parts) if (p is MapWidget m) m.CloseBig(); burn.Show(e); }   // #212：同時押しの合図が出るときは地図を閉じる
    public void ClearPress() => burn.Clear();

    void UpdateLights(GameState s)
    {
        bool off = s != null && s.sw.TryGetValue("lights", out var on) && !on;
        if (off == lightsOff) return;
        lightsOff = off; ctx.LightsOff = off;
        foreach (var p in AllParts()) p.SetLightsOff(off);
    }

    // ------------------------------------------------------------------ 席
    /// <summary>席タブで選んでいる席（#167：SeatView がその席のモニターとカメラを選ぶ）。席がまだ無ければ null</summary>
    public string ActiveSeat => activeSeat;
    internal void SelectSeat(string seat) { activeSeat = seat; Refresh(); }

    void RefreshSeat(GameState s)
    {
        var seats = ctx.Model.MySeats;
        if (seats.Count == 0) return;
        if (activeSeat == null || !seats.Contains(activeSeat)) activeSeat = seats[0];
        ctx.ActiveSeat = activeSeat;
        int key = ctx.Model.Phase == "stir" ? 1 : 0;
        offlineNote.text = ctx.Offline && ctx.Flow.showReplayControls ? "OFFLINE REPLAY: controls are ignored" : "";   // #146（Fable）：再生の操作を出しているときだけ
        if (activeSeat != lastBuiltSeat || key != lastBuiltPhaseKey || tabs.Count != seats.Count)
        {
            lastBuiltSeat = activeSeat; lastBuiltPhaseKey = key;
            tabs.Rebuild();
            BuildSeatPanel();
        }
        seatPanel?.Refresh(s);
    }

    void BuildSeatPanel()
    {
        for (int i = controls.childCount - 1; i >= 0; i--) { var c = controls.GetChild(i); c.SetParent(null); Object.Destroy(c.gameObject); }
        seatPanel = SeatPanel.Create(activeSeat);
        if (seatPanel == null) return;
        seatPanel.Attach(ctx, controls);
        if (lightsOff) seatPanel.SetLightsOff(true);
    }
}
