// 書いた人：Claude ／ 最終更新：2026-11-14
// 演出1：打ち上げ（#124。storyboard.md の場面1〜3）。約 7.6 秒。
//   0.0  地面と発射台、設計で選んだロケット、手前にクルー4人。カウントダウン 3・2・1。カメラが寄る。腕が離れる
//   2.4  点火：煙・炎・オレンジの光   3.0  上がる（空が濃紺の宇宙へ）。クルーは手を挙げて喜ぶ
//   5.3  月へ向かう宇宙船（Ship_Stack・小さな月・遠くの地球）   7.6  黒 → 船内へ
// 地上の物（地面・発射台・ロケット）は実寸（ロケット 70〜110m）なので、ground ごと 0.05 倍に縮めてクルー（1.2m）と並べる。
// 組み立ては Editor/Story/StoryScene.cs。向き：舞台は 180° 回してあり、カメラは −Z 側から +Z を見る（主光源が前から当たる）。
using UnityEngine;

public class LaunchStage : StoryStage
{
    public Transform padCenter;                    // 舗装の上面の中心（ロケットの真下）
    public Transform rocketSmall, rocketLarge;
    public Transform[] arms;                       // 発射台の腕（Move_Arm_1〜3）
    public Transform[] crew;
    public Transform engine;                       // 炎・光・噴煙の親（毎フレーム、ロケットの根元へ動かす）
    public Transform flame;
    public Light fireLight;
    public ParticleSystem padSmoke, trail;
    public Transform ground, space;                // 地上の舞台・宇宙の舞台
    public Transform ship, moon, earth;

    public const float TIgnite = 2.4f, TLift = 3.0f, TCut = 5.0f, TSpace = 5.3f, TEnd = 7.6f;
    const float Accel = 7f;                        // 上がる加速度（m/s²、縮めた世界で）
    static readonly Color DaySky = Ui.Hex("9DCBEA"), Navy = Ui.Hex("1D2D50"), SpaceDeep = Ui.Hex("0E1630");

    Transform rocket;
    Vector3 rocketBase;
    float rocketH = 4f, flameW = 0.3f;
    Vector3 flameScale;

    public override float Length => TEnd;

    public override void Begin(StoryDirector d)
    {
        base.Begin(d);
        bool large = d.Rocket() == "large";
        if (rocketSmall != null) rocketSmall.gameObject.SetActive(!large);
        if (rocketLarge != null) rocketLarge.gameObject.SetActive(large);
        rocket = large ? rocketLarge : rocketSmall;
        if (rocket == null) rocket = rocketLarge != null ? rocketLarge : rocketSmall;
        rocketBase = padCenter.position;
        if (rocket != null)
        {
            rocket.localPosition = Vector3.zero;
            rocketBase = rocket.position;
            var b = new Bounds(rocket.position, Vector3.zero);
            foreach (var r in rocket.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            rocketH = b.size.y; flameW = b.size.x * 0.55f;
        }
        if (flame != null) flameScale = new Vector3(flameW, flameW * 3.2f, flameW);
        d.Cam.fieldOfView = 40f;
    }

    public override void Pose(StoryDirector d, float t)
    {
        bool inSpace = t >= TSpace;
        ground.gameObject.SetActive(!inSpace);
        space.gameObject.SetActive(inSpace);
        d.SetFade(Mathf.Max(1f - Win(t, 0f, 0.35f), Mathf.Max(Win(t, TCut - 0.3f, TCut) * (1f - Win(t, TSpace, TSpace + 0.3f)), Win(t, TEnd - 0.3f, TEnd))));
        if (inSpace) PoseSpace(d, t); else PoseGround(d, t);
    }

    void PoseGround(StoryDirector d, float t)
    {
        Vector3 P = padCenter.position;

        // 腕を逃がす（Unity では localEulerAngles.y = 0 → 90）
        float arm = Ease(Win(t, 0.9f, 2.0f)) * 90f;
        foreach (var a in arms) if (a != null) a.localRotation = Quaternion.Euler(0f, arm, 0f);

        // ロケット：点火で揺れて、TLift から加速して上がる
        float lt = Mathf.Max(0f, t - TLift);
        float h = 0.5f * Accel * lt * lt;
        Vector3 shake = t >= TIgnite ? new Vector3(Mathf.Sin(t * 53f), 0f, Mathf.Cos(t * 47f)) * 0.012f * (1f - Win(t, TLift, TLift + 1f)) : Vector3.zero;
        if (rocket != null) rocket.position = rocketBase + Vector3.up * h + shake;

        // 炎・光・噴煙
        bool lit = t >= TIgnite;
        engine.position = rocketBase + Vector3.up * h;
        flame.gameObject.SetActive(lit);
        if (lit)
        {
            float k = Mathf.Lerp(0.5f, 1f, Win(t, TIgnite, TLift)) * (1f + 0.12f * Mathf.Sin(t * 41f));
            flame.localScale = flameScale * k;
            flame.localPosition = Vector3.down * flameScale.y * k * 0.45f;
        }
        fireLight.intensity = lit ? Mathf.Lerp(0f, 6f, Win(t, TIgnite, TIgnite + 0.4f)) * (1f + 0.15f * Mathf.Sin(t * 37f)) : 0f;
        PlayOnce(padSmoke, lit && t < TCut);
        PlayOnce(trail, t >= TLift);

        // クルー：上がったら手を挙げて喜ぶ
        for (int i = 0; i < crew.Length; i++)
        {
            var c = crew[i];
            if (c == null) continue;
            float cheer = Win(t, TLift - 0.2f, TLift + 0.3f);
            float wave = Mathf.Sin(t * 11f + i * 1.7f) * 18f;
            float a = Mathf.Lerp(Mathf.Sin(t * 2f + i) * 4f, -150f + wave, cheer);
            float b = Mathf.Lerp(Mathf.Sin(t * 2f + i + 1f) * 4f, -150f - wave, cheer);
            var l = Deep(c, ".Arm_L"); var r = Deep(c, ".Arm_R");
            if (l != null) l.localRotation = Quaternion.Euler(a, 0f, 0f);
            if (r != null) r.localRotation = Quaternion.Euler(b, 0f, 0f);
            Eyes(c, t >= TLift ? "Eyes_Relief" : "Eyes_Normal");
        }

        // カメラ：引きの絵（左に手前のクルー、右にロケット）から寄る → 上がるロケットを見上げる
        float push = Ease(Win(t, 0.2f, TIgnite));
        float ly = 0.9f + rocketH * 0.35f;   // 小型 約 2.1m、大型 約 2.8m（ロケットの頂上と足元のクルーが両方入る高さ）
        Vector3 cam = Vector3.Lerp(P + new Vector3(-1.0f, 2.0f, -11.5f), P + new Vector3(-0.7f, 1.7f, -8.2f), push);
        Vector3 look = Vector3.Lerp(P + new Vector3(-0.8f, ly, 0f), P + new Vector3(-0.6f, ly - 0.3f, 0f), push);
        float follow = Ease(Win(t, TLift, TLift + 1.3f));
        look = Vector3.Lerp(look, rocketBase + Vector3.up * (h + rocketH * 0.5f), follow);
        cam += Vector3.up * h * 0.3f + shake * 0.6f;
        d.Cam.transform.position = cam;
        d.Cam.transform.LookAt(look);

        // 空：昼の空 → 濃紺 → 宇宙
        float up = Mathf.Clamp01(h / 16f);
        d.SetBackground(up < 0.6f ? Color.Lerp(DaySky, Navy, up / 0.6f) : Color.Lerp(Navy, SpaceDeep, (up - 0.6f) / 0.4f));

        // 字幕
        if (t < 0.2f) d.Caption("", "");
        else if (t < 0.9f) d.Caption("3", "APOLLO 13  -  APRIL 11, 1970");
        else if (t < 1.6f) d.Caption("2", "APOLLO 13  -  APRIL 11, 1970");
        else if (t < 2.3f) d.Caption("1", "APOLLO 13  -  APRIL 11, 1970");
        else if (t < TIgnite + 0.1f) d.Caption("", "");
        else if (t < TCut - 0.4f) d.Caption("LIFTOFF!", "", Ui.Yellow, EaseOutBack(Win(t, TIgnite + 0.1f, TIgnite + 0.45f)));
        else d.Caption("", "");
    }

    void PoseSpace(StoryDirector d, float t)
    {
        Vector3 S = space.position;
        float s = Win(t, TSpace, TEnd);
        Vector3 from = S + new Vector3(-3.4f, -1.7f, 0f), to = S + new Vector3(1.0f, 0.6f, 0f);
        Vector3 dir = (to - from).normalized;
        ship.position = Vector3.Lerp(from, to, s);
        ship.rotation = Quaternion.FromToRotation(Vector3.up, dir) * Quaternion.Euler(0f, 40f + t * 25f, 0f);   // 進む向き＝船の +Y、ゆっくり回る
        if (moon != null) moon.Rotate(0f, Time.unscaledDeltaTime * 3f, 0f, Space.Self);
        if (earth != null) earth.Rotate(0f, Time.unscaledDeltaTime * 6f, 0f, Space.Self);
        d.Cam.transform.position = S + new Vector3(-0.6f + s * 0.9f, -0.2f + s * 0.4f, -12f);
        d.Cam.transform.LookAt(S + new Vector3(1.5f, 1.2f, 20f));
        d.UseSkybox();
        if (t > TSpace + 0.3f && t < TEnd - 0.25f) d.Caption("", "TO THE MOON");
        else d.Caption("", "");
        fireLight.intensity = 0f;
        PlayOnce(padSmoke, false);
        PlayOnce(trail, false);
    }

    public override void End(StoryDirector d)
    {
        foreach (var a in arms) if (a != null) a.localRotation = Quaternion.identity;
        if (rocket != null) rocket.localPosition = Vector3.zero;
        base.End(d);
    }
}
