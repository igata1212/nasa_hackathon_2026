// 書いた人：Claude ／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#130・#141）
// 演出3'：帰れなかったとき（#124。ED-C）。約 4.4 秒。悲しくなりすぎないよう、静かに見せる。
//   0.0  宇宙を漂う船と、遠くの地球（ゆっくり回る。音も字幕の色も強くしない）
//   0.8  少しずつ暗くなる → 1.0「Mission lost」（暗い空の上）→ 1.6 原因を1行（OXYGEN / POWER RAN OUT）と、
//        下の帯にヒューストンの台詞（ED-C）→ 4.4 黒 → エンディングの画面
// 音（#141）：無線の雑音 → quindar_out（0.8 秒）→ 無音 →「Mission lost」と一緒に jingle_fail。警報やエラー音は入れない。
// #130：小さい字幕とヒューストンの帯が同じことを言っていたので、小さい字幕は原因にした。帯は自動では出さず、ここで時刻を決める。
using UnityEngine;

public class LostStage : StoryStage
{
    public Transform ship, earth;

    public const float TEnd = 4.4f, TTitle = 1.0f, TLine = 1.6f;
    [Tooltip("船の高さ（原点からの m）。上の黒帯で切れないように")] public float shipY = 1.0f;
    [Tooltip("船の横の位置（原点からの m）。「Mission lost」の左に置く（Unity で見て直した：-2.4 だと字に重なった。右へ流れるので、終わりでも重ならない所から）")] public float shipX = -4.6f;

    public override float Length => TEnd;
    /// <summary>ヒューストンの帯は Pose が決める（Run が拾った台詞を最初のフレームに一瞬出さないように）</summary>
    public override bool AllowHouston(float t) => false;

    public override void Begin(StoryDirector d)
    {
        base.Begin(d);
        d.Cam.fieldOfView = 38f;
        d.EndingLayout(true);
    }

    public override void Pose(StoryDirector d, float t)
    {
        Vector3 Q = transform.position;
        d.UseSkybox();
        d.SetFade(Mathf.Max(Mathf.Max(1f - Win(t, 0f, 0.6f), 0.4f * Win(t, 0.8f, 2.6f)), Win(t, TEnd - 0.5f, TEnd)));
        ship.position = Q + new Vector3(shipX + t * 0.2f, shipY + t * 0.04f, 2f);
        ship.rotation = Quaternion.Euler(25f + t * 6f, 30f + t * 10f, 15f + t * 4f);
        if (earth != null) earth.rotation = Quaternion.Euler(0f, 160f + t * 4f, 0f);
        d.Cam.transform.position = Q + new Vector3(0f, 0.6f, -10f - t * 0.4f);
        d.Cam.transform.LookAt(Q + new Vector3(0.3f, -0.6f, 10f));
        var soft = new Color(0.95f, 0.89f, 0.78f, Win(t, TTitle, TLine));
        bool shown = t > TTitle && t < TEnd - 0.3f;
        if (shown) d.Caption("Mission lost", t > TLine ? Cause(d) : "", soft, 0.75f);
        else d.Caption("", "");
        d.HoustonBar(shown && t > TLine ? d.Line("ED-C") : "");
        StoryAudio.Loop("static", "radio_static_loop", t < 0.8f ? 0.2f * Win(t, 0f, 0.15f) : 0f);   //「応答なし」
        StoryAudio.At(0.8f, "quindar_out", 0.5f);
        if (StoryAudio.Crossed(TTitle)) StoryAudio.EndingJingle();
    }

    /// <summary>何が尽きたか（サーバーの result の残りから。分からないときは空）</summary>
    static string Cause(StoryDirector d)
    {
        var m = d.flow != null ? d.flow.Model : null;
        if (m == null) return "";
        float o2 = 1f, power = 1f;
        if (m.Result != null) { o2 = J.Num(m.Result, "oxygen", 1f); power = J.Num(m.Result, "power", 1f); }
        else if (m.State != null) { o2 = m.State.oxygen; power = m.State.power; }
        if (o2 <= 0f && power <= 0f) return "Power and oxygen ran out";
        if (o2 <= 0f) return "Oxygen ran out";
        if (power <= 0f) return "Power ran out";
        return "";   // 燃料が足りず帰る噴射ができなかったとき（result では区別できない）
    }
}
