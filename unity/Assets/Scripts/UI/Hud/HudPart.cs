// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// 作る Issue：#147（土台。HUD の部品の約束。部品の中身は各ファイルの Issue が作る）
//
// HUD の部品の基底。CabinHud（土台）が画面の領域（HudSlot）を作り、部品をそこに置いて、状態・イベントを配る。
// 部品が実装するもの（どれも任意。要るものだけ override する）：
//   Build(root)        1回だけ。root は slot いっぱいの、この部品専用の RectTransform（名前 = 型の名前）
//   Refresh(s)         状態が来たとき・段階が変わったとき・席を変えたとき。s は null のことがある（練習・かき混ぜの間は state が来ない）
//   OnEvent(e)         サーバーのイベント全部（houston・warning・press・practice・cmd・launch）。GameFlow が Model を直した後に来る
//   SetLightsOff(off)  照明 OFF（state の sw.lights）が変わったとき（#87）
//   Tick(now)          毎フレーム（Time.unscaledTime）。点滅・残り秒の表示・消える時刻に使う
// 守ること：
//   ・自分のファイルだけを触る。ほかの部品は直接さわらない（要るなら Ctx.Parts から型で探す）
//   ・数値（閾値など）はコードに書かない。GameParams（parameters.json）か GameState（サーバーの値）から読む
//   ・コマンドは Ctx.Send / Ctx.SendDual で送る（1人で両席を持つときの2通は SendDual がやる）
//   ・照明 OFF で暗くしてよい物は Detail（CanvasGroup）の下に入れる。暗くする量は #87（CabinOverlay）が決める
using UnityEngine;

public abstract class HudPart
{
    protected HudContext Ctx { get; private set; }
    /// <summary>この部品の一番上（slot いっぱい）。隠すときは Root.gameObject.SetActive(false)</summary>
    public RectTransform Root { get; private set; }
    /// <summary>照明 OFF で暗くしてよい物の入れ物（#87）。null なら暗くしない（警告・同時押しの合図など）</summary>
    public CanvasGroup Detail { get; protected set; }

    /// <summary>CabinHud が呼ぶ。slot の下に Root を作って Build を呼ぶ</summary>
    public void Attach(HudContext ctx, RectTransform slot)
    {
        Ctx = ctx;
        Root = Ui.Rect(GetType().Name, slot); Ui.Fill(Root);
        Build(Root);
    }

    protected abstract void Build(RectTransform root);
    public virtual void Refresh(GameState s) { }
    public virtual void OnEvent(GameEvent e) { }
    public virtual void SetLightsOff(bool off) { }
    public virtual void Tick(float now) { }

    /// <summary>Root 全体を Detail（照明 OFF で暗くしてよい物）にする</summary>
    protected CanvasGroup RootAsDetail()
    {
        var g = Root.GetComponent<CanvasGroup>();
        if (g == null) g = Root.gameObject.AddComponent<CanvasGroup>();
        Detail = g;
        return g;
    }

    /// <summary>子を全部消す（作り直すとき）。SetParent(null) で childCount をすぐ減らす</summary>
    protected static void Clear(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--) { var c = t.GetChild(i); c.SetParent(null); Object.Destroy(c.gameObject); }
    }
}
