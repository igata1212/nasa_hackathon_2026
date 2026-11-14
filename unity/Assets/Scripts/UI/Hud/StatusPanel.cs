// 書いた人：Claude ／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#147 で CabinHud から分けた）
// 作る Issue：#74（常に見える状態）＋ #131（ゲージの色の閾値を parameters.json に合わせる）
//
// 左のゲージ5本（電力・酸素・温度・CO2・燃料）。見た目は仮（緑・黄・赤、0.5／0.75 は前のまま。#74 で #109 の色と閾値に替える）
// 閾値は GameParams（parameters.json）か GameState.Threshold（#148 でサーバーが送る）から読む。UrgencyFx の段階と同じ所から取る
// Detail：パネル全体（照明 OFF で暗くしてよい）
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StatusPanel : HudPart
{
    class Gauge { public Image fill; public Text value; public bool inverse; }
    readonly Dictionary<string, Gauge> gauges = new Dictionary<string, Gauge>();

    protected override void Build(RectTransform root)
    {
        RootAsDetail();
        var gp = Ui.VBox(root, 6, 10); Ui.Fill(gp);
        gp.gameObject.AddComponent<Image>().color = new Color(0.07f, 0.1f, 0.2f, 0.65f);
        foreach (var g in new[] { ("power", "POWER", false), ("oxygen", "OXYGEN", false), ("temp", "TEMP", false), ("co2", "CO2", true), ("fuel", "FUEL", false) })
            AddGauge(gp, g.Item1, g.Item2, g.Item3);
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

    public override void Refresh(GameState s)
    {
        if (s == null) return;   // 練習・かき混ぜの間は前の値のまま
        var m = Ctx.Model;
        SetGauge("power", s.power, Mathf.Max(s.capacity, 1f), s.power.ToString("0"));
        SetGauge("oxygen", s.oxygen, m.Max("oxygen", s.oxygen), s.oxygen.ToString("0"));
        SetGauge("temp", s.temp, m.Max("temp", s.temp), s.temp.ToString("0.0") + "C");
        SetGauge("co2", s.co2, 100f, s.co2.ToString("0"));
        SetGauge("fuel", s.fuel, m.Max("fuel", s.fuel), s.fuel.ToString("0"));
    }

    void SetGauge(string key, float v, float max, string text)
    {
        var g = gauges[key];
        float frac = Mathf.Clamp01(v / Mathf.Max(max, 0.0001f));
        g.fill.fillAmount = frac;
        g.value.text = text;
        float bad = g.inverse ? frac : 1f - frac;           // 悪さ（0 = 良い）
        g.fill.color = bad < 0.5f ? Ui.Green : bad < 0.75f ? Ui.Yellow : Ui.Red;
    }
}
