// 書いた人：Claude ／ 最終更新：2026-11-14
// Title（接続のフォーム）と Lobby（合言葉・席・開始）。見た目は仮
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TitleScreen
{
    public readonly GameObject Root;
    public TitleScreen(Transform parent, GameFlow f)
    {
        var bg = Ui.PanelImg(parent, Ui.Navy, "Title"); Root = bg.gameObject; Ui.Fill(bg.rectTransform);
        var t = Ui.Label(bg.transform, "MISSION RETURN", 96, Ui.Cream, TextAnchor.MiddleCenter, FontStyle.Bold);
        Ui.Anchor(t.rectTransform, 0, 0.62f, 1, 0.9f);
        var sub = Ui.Label(bg.transform, "Bring the crew home. Together.", 32, Ui.Orange, TextAnchor.MiddleCenter);
        Ui.Anchor(sub.rectTransform, 0, 0.54f, 1, 0.62f);

        var col = Ui.VBox(bg.transform, 12, 0, TextAnchor.UpperCenter);
        Ui.Anchor(col, 0.3f, 0.1f, 0.7f, 0.52f);
        var name = Ui.Input(col, "Your name", "Player"); Ui.Size(name, h: 56);
        var room = Ui.Input(col, "Room code (4 digits)", ""); Ui.Size(room, h: 56);
        var url = Ui.Input(col, "Server URL", f.wsUrl); Ui.Size(url, h: 56);
        var row = Ui.HBox(col, 12, 0, TextAnchor.MiddleCenter, true); Ui.Size(row, h: 64);
        Ui.Btn(row, "CREATE ROOM", Ui.Orange, () => f.StartOnline(url.text, name.text, "", true), 26);
        Ui.Btn(row, "JOIN", Ui.Teal, () => f.StartOnline(url.text, name.text, room.text, false), 26, Ui.Cream);
        var offRow = Ui.HBox(col, 12, 0, TextAnchor.MiddleCenter, true); Ui.Size(offRow, h: 56);
        Ui.Btn(offRow, "REPLAY: SUCCESS (T1)", Ui.Gray, () => f.StartOffline(false), 22, Ui.Cream);
        Ui.Btn(offRow, "REPLAY: FAILURE (T4)", Ui.Gray, () => f.StartOffline(true), 22, Ui.Cream);
    }
}

public class LobbyScreen
{
    public readonly GameObject Root;
    readonly GameFlow f;
    readonly Text roomLabel, hint;
    readonly Text[] seatNames = new Text[4];
    readonly Image[] seatCards = new Image[4];

    public LobbyScreen(Transform parent, GameFlow f)
    {
        this.f = f;
        var bg = Ui.PanelImg(parent, Ui.Navy, "Lobby"); Root = bg.gameObject; Ui.Fill(bg.rectTransform);
        var t = Ui.Label(bg.transform, "LOBBY", 64, Ui.Cream, TextAnchor.MiddleCenter, FontStyle.Bold);
        Ui.Anchor(t.rectTransform, 0, 0.82f, 1, 0.95f);
        roomLabel = Ui.Label(bg.transform, "", 44, Ui.Orange, TextAnchor.MiddleCenter, FontStyle.Bold);
        Ui.Anchor(roomLabel.rectTransform, 0, 0.72f, 1, 0.82f);

        var row = Ui.HBox(bg.transform, 20, 0, TextAnchor.MiddleCenter, true);
        Ui.Anchor(row, 0.08f, 0.3f, 0.92f, 0.7f);
        for (int i = 0; i < 4; i++)
        {
            string seat = Seats.All[i];
            var card = Ui.PanelImg(row, Ui.Panel, "Seat_" + seat); seatCards[i] = card;
            var ic = Ui.Label(card.transform, Seats.Icon(seat), 90, Seats.Tint(seat), TextAnchor.MiddleCenter, FontStyle.Bold);
            Ui.Anchor(ic.rectTransform, 0, 0.45f, 1, 1);
            var nm = Ui.Label(card.transform, Seats.Title(seat), 26, Seats.Tint(seat), TextAnchor.MiddleCenter, FontStyle.Bold);
            Ui.Anchor(nm.rectTransform, 0, 0.28f, 1, 0.45f);
            seatNames[i] = Ui.Label(card.transform, "-", 24, Ui.Cream, TextAnchor.MiddleCenter);
            Ui.Anchor(seatNames[i].rectTransform, 0, 0.05f, 1, 0.28f);
        }
        hint = Ui.Label(bg.transform, "Seats are assigned by the server from the number of players.", 24, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleCenter);
        Ui.Anchor(hint.rectTransform, 0, 0.2f, 1, 0.28f);
        var go = Ui.Btn(bg.transform, "START MISSION", Ui.Orange, () => f.Client.Send(Cmd.Type("start")), 34);
        Ui.Anchor((RectTransform)go.transform, 0.35f, 0.06f, 0.65f, 0.17f);
    }

    public void Refresh()
    {
        roomLabel.text = "ROOM  " + f.Model.Room;
        for (int i = 0; i < 4; i++)
        {
            string seat = Seats.All[i];
            string who = "-";
            foreach (var p in f.Model.Players)
                if (p.seats.Contains(seat)) { who = p.name + (p.id == f.Model.PlayerId ? " (you)" : ""); break; }
            seatNames[i].text = who;
            seatCards[i].color = who == "-" ? Ui.Panel : new Color(Seats.Tint(seat).r * 0.35f, Seats.Tint(seat).g * 0.35f, Seats.Tint(seat).b * 0.35f, 0.95f);
        }
    }
}
