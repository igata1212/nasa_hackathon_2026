// 書いた人：Claude ／ 最終更新：2026-11-14
// 演出3'：帰れなかったとき（#124。ED-C）。約 4.4 秒。悲しくなりすぎないよう、静かに見せる。
//   0.0  宇宙を漂う船と、遠くの地球（ゆっくり回る。音も字幕の色も強くしない）
//   0.8  少しずつ暗くなる → 1.0「Mission lost」→ 1.6「一緒に考えて、もう一度」→ 4.4 黒 → エンディングの画面
using UnityEngine;

public class LostStage : StoryStage
{
    public Transform ship, earth;

    public const float TEnd = 4.4f;

    public override float Length => TEnd;

    public override void Begin(StoryDirector d)
    {
        base.Begin(d);
        d.Cam.fieldOfView = 38f;
    }

    public override void Pose(StoryDirector d, float t)
    {
        Vector3 Q = transform.position;
        d.UseSkybox();
        d.SetFade(Mathf.Max(Mathf.Max(1f - Win(t, 0f, 0.6f), 0.4f * Win(t, 0.8f, 2.6f)), Win(t, TEnd - 0.5f, TEnd)));
        ship.position = Q + new Vector3(-2.4f + t * 0.2f, 1.7f + t * 0.04f, 2f);
        ship.rotation = Quaternion.Euler(25f + t * 6f, 30f + t * 10f, 15f + t * 4f);
        if (earth != null) earth.rotation = Quaternion.Euler(0f, 160f + t * 4f, 0f);
        d.Cam.transform.position = Q + new Vector3(0f, 0.6f, -10f - t * 0.4f);
        d.Cam.transform.LookAt(Q + new Vector3(0.3f, -0.6f, 10f));
        var soft = new Color(0.95f, 0.89f, 0.78f, Win(t, 1.0f, 1.6f));
        if (t > 1.0f && t < TEnd - 0.3f) d.Caption("Mission lost", t > 1.6f ? "Let's think it through together, and try again." : "", soft, 0.75f);
        else d.Caption("", "");
    }
}
