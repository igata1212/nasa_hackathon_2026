// 書いた人：Claude ／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で CabinHud から分けた）／ 更新：iizuka（Opus 5.5、2026-11-15、#190 盆と名前タグ）
// 作る Issue：#71（席タブの見た目：SeatTabs）・#94（ほかの人が切れたときの印）・#190（あつ森に寄せる）
//
// 下のパネルの上の段：自分の席のタブ（兼任の人は席を切り替える、#102）。押すと Ctx.SelectSeat で CabinHud が席のパネルを作り直す。
// 作り直す時期は CabinHud が決める（席・段階が変わったとき、席の数が変わったとき）。Count はそのための今のタブの数
// #190（ui-guidelines 1章「席のパネル」、【04】【12】）：
//   ・下の帯（CabinHud が作る紺の四角）をクリームの盆に替える（SeatLook.Tray。CabinHud を触らないので、ここで紺を透明にして盆を敷く）
//   ・タブは傾いた名前タグ（blob_pill、高さ 48、白文字 26、−6°）。選んでいる席＝席の色の面、ほかの席＝bgDeep の面に席の色の丸（席＝色＋形）
//   ・名前タグの真ん中が盆の上端に乗る（【12】の名前タグが吹き出しの角にかぶさる作り）
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using T = UiTokens;

public class SeatTabBar : HudPart
{
    RectTransform row;
    /// <summary>今あるタブの数</summary>
    public int Count => row != null ? row.childCount : 0;

    protected override void Build(RectTransform root)
    {
        SeatLook.Tray(root);
        row = UiKit.Row(root, T.S2, TextAnchor.MiddleLeft, T.S2, 0f); UiKit.Fill(row);
    }

    /// <summary>自分の席（Model.MySeats）のタブを作り直す。選んでいる席（Ctx.ActiveSeat）を席の色にする</summary>
    public void Rebuild()
    {
        Clear(row);
        foreach (var s in Ctx.Model.MySeats)
        {
            string seat = s;
            NameTag(row, seat, seat == Ctx.ActiveSeat, () => Ctx.SelectSeat(seat));
        }
    }

    /// <summary>名前タグ1つ（row の子は1つだけ。Count が席の数になる）。押せる範囲は上へ T.Hit まで広げる（下は席のパネルなので広げない）</summary>
    static void NameTag(Transform p, string seat, bool on, System.Action select)
    {
        var r = UiKit.Row(p, T.S1, TextAnchor.MiddleCenter, T.S2, 0f);
        r.name = "Tab_" + seat;
        var le = Ui.Size(r, h: T.NameTagH); le.minHeight = T.NameTagH; le.flexibleWidth = 0f;
        r.localRotation = Quaternion.Euler(0f, 0f, T.NameTagTilt);

        var hit = Ui.Rect("Hit", r); UiKit.Fill(hit); hit.offsetMax = new Vector2(0f, T.Hit - T.NameTagH);
        var hi = hit.gameObject.AddComponent<Image>(); hi.color = T.Clear; UiKit.Ignore(hi);
        var face = UiKit.Blob(r, T.BlobPill, on ? T.SeatColor(seat) : T.BgDeep, T.NameTagH * 0.5f, "Face");
        UiKit.Fill(face.rectTransform); UiKit.Ignore(face);

        var badge = UiKit.SeatBadge(r, seat, T.NameTagH - T.S2);   // 席の色の丸に形。選んでいるタグでは面と同じ色なので形だけが見える
        var label = UiKit.Text(r, Title(seat), TextStyle.Hint, on ? T.OnSeat(seat) : T.Text, TextAlignmentOptions.MidlineLeft);
        label.font = T.FontStrong;

        var b = SeatLook.Press(r, face, on ? null : select, squish: false);   // UiSquish は傾きを 0 に戻すので付けない
        b.transition = Selectable.Transition.None;
    }

    /// <summary>Seats.Title（PILOT・LIFE SUPPORT …）を語の頭だけ大文字に（ガイドライン 2章「大文字は使わない」。席の名前は固有名詞）</summary>
    static string Title(string seat)
    {
        var words = Seats.Title(seat).Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            var w = words[i];
            if (w.Length > 0) words[i] = char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant();
        }
        return string.Join(" ", words);
    }
}
