// 書いた人：Claude ／ 最終更新：2026-11-14
// 演出3：帰還（#124。storyboard.md の場面6〜7。ED-A・ED-B のとき）。約 5.4 秒。
//   0.0  熱でオレンジに光る司令船（Ship_CM）が斜めに降りる（RE-ENTRY）
//   1.7  パラシュート3つ（Fx_Parachute）が開く → ゆっくり降りる
//   3.7  海に着水（白いしぶきと波）→ 3.8「SPLASHDOWN!」→ 5.4 黒 → エンディングの画面
// サーバーの ending は 8 秒で report に進むので、短めにしてある。実寸（司令船 直径 3.9m）。海は原点の高さ（transform.position.y）。
using UnityEngine;

public class ReturnStage : StoryStage
{
    public Transform cm;                 // 司令船（ルートの原点＝熱シールドの底）
    public Transform chute;              // パラシュート（cm の子。DockRing の上）
    public Transform glow;               // 熱シールドの下の光る塊（cm の子）
    public Light heatLight;
    public ParticleSystem heatTrail, splash, foam;

    public const float T1 = 1.6f, TChute = 1.7f, TSplash = 3.7f, TText = 3.8f, TEnd = 5.4f;
    static readonly Color High = Ui.Hex("2B2550"), Sky = Ui.Hex("8FC1E3");

    Vector3 glowScale, chuteScale;
    bool splashed;

    public override float Length => TEnd;

    public override void Begin(StoryDirector d)
    {
        base.Begin(d);
        if (glowScale == Vector3.zero && glow != null) glowScale = glow.localScale;
        if (chuteScale == Vector3.zero && chute != null) chuteScale = chute.localScale;
        splashed = false;
        d.Cam.fieldOfView = 40f;
    }

    public override void Pose(StoryDirector d, float t)
    {
        Vector3 R = transform.position;
        d.SetFade(Mathf.Max(1f - Win(t, 0f, 0.3f), Win(t, TEnd - 0.3f, TEnd)));

        // 司令船の位置：再突入は斜めに速く、パラシュートのあとはゆっくり、着水したら浮かぶ
        Vector3 p;
        if (t < T1) p = Vector3.Lerp(new Vector3(-34f, 85f, 12f), new Vector3(-12f, 46f, 10f), t / T1);
        else if (t < TSplash)
        {
            float u = Win(t, T1, TSplash);
            p = new Vector3(Mathf.Lerp(-12f, -5f, u), Mathf.Lerp(46f, -0.8f, EaseOut(u)), 10f);
        }
        else p = new Vector3(-5f, -0.8f + Mathf.Sin((t - TSplash) * 3f) * 0.12f, 10f);
        cm.position = R + p;
        float tilt = Mathf.Lerp(-28f, 0f, Ease(Win(t, T1 - 0.2f, T1 + 0.8f)));
        cm.rotation = Quaternion.Euler(0f, 20f, tilt + (t >= TSplash ? Mathf.Sin((t - TSplash) * 2.3f) * 4f : 0f));

        // 熱：光る塊・光・炎の尾
        float heat = 1f - Win(t, T1 - 0.4f, T1 + 0.1f);
        glow.gameObject.SetActive(heat > 0.01f);
        glow.localScale = glowScale * heat * (1f + 0.08f * Mathf.Sin(t * 31f));
        heatLight.intensity = 7f * heat * (1f + 0.15f * Mathf.Sin(t * 23f));
        PlayOnce(heatTrail, heat > 0.3f);

        // パラシュート：ぽんと開き、着水したら横に倒れてしぼむ
        bool open = t >= TChute;
        chute.gameObject.SetActive(open);
        if (open)
        {
            float pop = EaseOutBack(Win(t, TChute, TChute + 0.5f));
            float fall = Win(t, TSplash + 0.1f, TSplash + 1.3f);
            chute.localScale = new Vector3(chuteScale.x * pop, chuteScale.y * pop * (1f - 0.6f * fall), chuteScale.z * pop);
            chute.localRotation = Quaternion.Euler(0f, 0f, -75f * Ease(fall));
        }

        // 着水：しぶきと白い波（1回だけ）
        if (t >= TSplash && !splashed)
        {
            splashed = true;
            var at = new Vector3(cm.position.x, R.y + 0.05f, cm.position.z);
            splash.transform.position = at; foam.transform.position = at;
            splash.Play(true); foam.Play(true);
        }
        if (t < TSplash && splashed) { splashed = false; splash.Clear(true); foam.Clear(true); }

        // カメラ：再突入は船を追い、そのあと海の近くから見上げる
        Vector3 c1 = cm.position + new Vector3(9f, -4f, -22f), l1 = cm.position;
        Vector3 c2 = R + new Vector3(4f, 5f, -24f), l2 = cm.position + Vector3.up * 5f;
        if (t >= TSplash - 0.5f) l2 = Vector3.Lerp(l2, R + new Vector3(-5f, 4f, 10f), Ease(Win(t, TSplash - 0.5f, TSplash + 0.5f)));
        float k = Ease(Win(t, T1 - 0.3f, T1 + 0.9f));
        d.Cam.transform.position = Vector3.Lerp(c1, c2, k);
        d.Cam.transform.LookAt(Vector3.Lerp(l1, l2, k));

        d.SetBackground(Color.Lerp(High, Sky, Ease(Win(t, 0.4f, 2.4f))));

        // 字幕
        string e = d.Ending();
        if (t < T1 - 0.2f && t > 0.25f) d.Caption("", "RE-ENTRY");
        else if (t >= TText) d.Caption("SPLASHDOWN!", e == "ED-B" ? "WELCOME HOME  -  THAT WAS CLOSE!" : "WELCOME HOME, CREW!", Ui.Yellow, EaseOutBack(Win(t, TText, TText + 0.35f)));
        else d.Caption("", "");
    }
}
