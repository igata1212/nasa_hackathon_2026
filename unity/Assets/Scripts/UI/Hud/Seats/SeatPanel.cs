// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// 作る Issue：#147（席のパネルの基底）。中身は #80 Pilot・#81 Life Support・#82 Comms & Power・#83 Mission Planner。Highlight は #84 が使う口
//
// 1席ぶんの操作のパネル。CabinHud が席タブで選んだ席のパネルを作り、席・段階（stir かどうか）が変わるたびに作り直す。
// 兼任の人は席タブで1席ずつ切り替える（#102）。同時押し（点火・フィルター交換）は Ctx.SendDual で送る。
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public abstract class SeatPanel : HudPart
{
    /// <summary>この席（Seats.Pilot など）</summary>
    public abstract string Seat { get; }
    /// <summary>この席のパネルが送るコマンド（server/game/seats.py の seats_for）。#84 が「まだ試していない操作」を探すのに使う</summary>
    public abstract IEnumerable<string> Commands { get; }
    protected Color Tint => Seats.Tint(Seat);

    /// <summary>その席のパネルを作る（CabinHud が呼ぶ）。知らない席なら null</summary>
    public static SeatPanel Create(string seat)
    {
        switch (seat)
        {
            case Seats.Pilot: return new PilotPanel();
            case Seats.Life: return new LifeSupportPanel();
            case Seats.Comms: return new CommsPowerPanel();
            case Seats.Planner: return new PlannerPanel();
            default: return null;
        }
    }

    protected override void Build(RectTransform root)
    {
        var row = Ui.HBox(root, 12, 0, TextAnchor.MiddleLeft, true); Ui.Fill(row);
        if (Ctx.Model.Phase == "stir") BuildStir(row);   // かき混ぜ（M04）の間は、かき混ぜのボタンだけ
        else BuildControls(row);
    }

    /// <summary>practice・flight のときの操作（2〜3 かたまり、#102）</summary>
    protected abstract void BuildControls(RectTransform row);
    /// <summary>stir の段階のときの操作。Life Support だけがかき混ぜのボタンを出す。ほかの席は空</summary>
    protected virtual void BuildStir(RectTransform row) { }

    /// <summary>このコマンドの操作を光らせる／戻す（#84 が練習の間に「まだ試していない操作」に使う）。今は何もしない</summary>
    public virtual void Highlight(string cmd, bool on) { }

    public override void Refresh(GameState s)
    {
        if (s == null) return;   // 練習・かき混ぜの間は state が無い。ボタンは作ったときの見た目のまま
        RefreshToggles(s);
    }

    // ------------------------------------------------------------------ ON/OFF のボタン（仮。#71 の Toggle に替える）
    class ToggleBtn { public Button btn; public string label; public Color tint; public Func<GameState, bool> isOn; }
    readonly List<ToggleBtn> toggles = new List<ToggleBtn>();

    /// <summary>押すと今と逆の値を送るボタン。表示は Refresh で「LABEL  ON／OFF」と席の色／灰色にする</summary>
    protected Button AddToggle(Transform parent, string label, Func<GameState, bool> isOn, Func<bool, Dictionary<string, object>> make)
    {
        var t = new ToggleBtn { label = label, tint = Tint, isOn = isOn };
        t.btn = Ui.Btn(parent, label, Ui.Gray, () => { var st = Ctx.State; Ctx.Send(make(!(st != null && isOn(st)))); }, 22, Ui.Cream);
        t.btn.name = label;
        toggles.Add(t);
        return t.btn;
    }

    void RefreshToggles(GameState s)
    {
        foreach (var t in toggles)
        {
            bool on = t.isOn(s);
            Ui.SetBtn(t.btn, t.label + (on ? "  ON" : "  OFF"), on ? t.tint : Ui.Gray, on ? Ui.Navy : Ui.Cream);
        }
    }

    /// <summary>点火のボタン（Pilot と Comms & Power）。1人で両席なら「both seats」で2通送る</summary>
    protected Button IgniteButton(Transform row, string partnerLabel)
    {
        string me = Seat;
        string label = Ctx.SoloPair("ignite") != null ? "IGNITE (both seats)" : "IGNITE (" + partnerLabel + ")";
        var b = Ui.Btn(row, label, Tint, () => Ctx.SendDual("ignite", me), 24);
        Ui.Size(b, flexW: 1);
        return b;
    }
}
