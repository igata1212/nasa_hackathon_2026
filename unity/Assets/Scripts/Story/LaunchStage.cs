// 書いた人：Claude ／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#129・#141）
// 演出1：打ち上げ（#124。storyboard.md の場面1〜3）。約 8.2 秒。#129 で Fable の採点（上位6つ）を入れた。
//   0.2  カウントダウン 3・2・1（左上の空に、数字が弾む）。クルー4人がカメラに手を振る。カメラが低い位置へ寄る
//   0.8  クルーが1人ずつ（Pilot から 0.2 秒ずつ）ロケットの根元へ跳んで乗り込む（1.85 で4人とも消える）
//   1.5  腕が離れる   2.4  点火：炎（円錐3枚）・光が一瞬強く・地面を走る煙の輪・カメラがぐっと寄る（画角 40→36）・揺れ
//   3.1  上がる「LIFTOFF!」（機体が縮んで → 伸びる。画角 36→48。カメラは遅れて追うので、機体は画面の上へ抜ける）
//   5.0  直接カット（暗転なし）→ 月へ向かう宇宙船。5.3 から字幕「TO THE MOON」とヒューストンの台詞（M03）
//   8.2  黒 → 船内へ
// 地上の物（地面・発射台・ロケット）は実寸（ロケット 70〜110m）なので、ground ごと 0.05 倍に縮めてクルー（1.2m）と並べる。
// 音（#141）：Sound(t)。Fable の採点の「音を入れる瞬間」の表どおり（秒読み・乗り込み・腕・点火・LIFTOFF!・宇宙へのカットで噴射をぴたりと止める）。
// 組み立ては Editor/Story/StoryScene.cs。向き：舞台は 180° 回してあり、カメラは −Z 側から +Z を見る（主光源が前から当たる）。
// 炎（円錐3枚）・地面の煙の輪・船と月の大きさ・後ろの煙の量は、シーンを作り直さなくて済むよう Begin で作る／変える（#129）。
// 炎の3枚は全部「裏の面だけ描く」（Cull Front）。視線が最初に当たる裏の面＝いちばん内側の円錐になるので、
// 不透明のまま、外から オレンジ → 黄 → クリーム の帯に見える（透明にしないので、深度や煙との前後が崩れない）。
using UnityEngine;
using UnityEngine.UI;

public class LaunchStage : StoryStage
{
    public Transform padCenter;                    // 舗装の上面の中心（ロケットの真下）
    public Transform rocketSmall, rocketLarge;
    public Transform[] arms;                       // 発射台の腕（Move_Arm_1〜3）
    public Transform[] crew;
    public Transform engine;                       // 炎・光・噴煙の親（毎フレーム、ロケットの根元へ動かす）
    public Transform flame;                        // 古い炎（球）。#129 から使わない（材料の元にだけ使う）
    public Light fireLight;
    public ParticleSystem padSmoke, trail;
    public Transform ground, space;                // 地上の舞台・宇宙の舞台
    public Transform ship, moon, earth;

    public const float TIgnite = 2.4f, TLift = 3.1f, TSpace = 5.0f, TEnd = 8.2f;
    /// <summary>秒読みの上の小さい字幕（仮。文言は物語の担当が決める）</summary>
    public const string DateLine = "INSPIRED BY APOLLO 13 (1970)";
    const float Accel = 7f;                        // 上がる加速度（m/s²、縮めた世界で）
    // パレット（modeling/core/palette.json の名前）
    static readonly Color DaySky = Ui.Hex("9DCBEA"), Navy = Ui.Hex("1D2D50"), SpaceDeep = Ui.Hex("0E1630");   // 空・navy・visor
    static readonly Color Orange = Ui.Hex("F28C28"), SeatYellow = Ui.Hex("FFC857"), Cream = Ui.Hex("F2E3C6");   // orange・seat_yellow・cream

    [Header("クルーの乗り込み")]
    [SerializeField] float boardStart = 0.8f, boardStagger = 0.2f, boardTime = 0.45f, boardHop = 0.6f, boardEnterY = 0.8f;
    [SerializeField] float armOpenFrom = 1.5f, armOpenTo = 2.3f;

    [Header("カメラ（寄ったあと：低い位置から見上げる）")]
    [SerializeField] float camLowY = 0.8f, camDist0 = 4.4f, camDistPerH = 1.6f, lookFrac = 0.52f;   // 実機で確認：0.42 だと小型の先端が上の黒帯に切れた（ロケットは舗装の 0.7m 上に立つ）
    [SerializeField] float followK = 3f;           // 追い方 hf = K·(1 − e^(−h/K))。小さいほど早く画面の上へ抜ける
    [SerializeField] float fovStart = 40f, fovPunch = 36f, fovLift = 48f, fovSpace = 40f;

    [Header("揺れ")]
    [SerializeField] float shakeIgnite = 0.05f, shakeLift = 0.09f, shakeFreq = 18f, shakeRoll = 0.6f, rocketShake = 0.02f;

    [Header("機体の縮み → 伸び（元の大きさに掛ける）")]
    [SerializeField] float squashY = 0.96f, squashXZ = 1.02f, stretchY = 1.05f, stretchXZ = 0.98f;

    [Header("炎")]
    [SerializeField] float flameWidthK = 0.7f, flameLenK = 0.5f, flameHdr = 2.5f;
    [Tooltip("外・内・芯の明るさ（flameHdr に掛ける）。外を上げすぎるとトーンマップで黄色に飛び、帯が見えなくなる")]
    [SerializeField] Vector3 flameGlow = new Vector3(0.45f, 0.75f, 1f);
    [SerializeField] float fireFlash = 14f, fireSteady = 6f, fireRange = 14f;

    [Header("煙")]
    [SerializeField] int burstCount = 40;
    [SerializeField] float trailRate = 110f;
    [SerializeField] Vector2 trailLife = new Vector2(2.0f, 3.0f);

    [Header("宇宙のカット")]
    [SerializeField] float shipScale = 0.3f, moonScale = 4.5f;
    [SerializeField] Vector3 shipFrom = new Vector3(-5.4f, -3.0f, 0f), shipTo = new Vector3(-2.4f, -1.1f, 0f);   // 終わりに月を隠さない（実機で確認）

    [Header("字幕（Canvas の 0〜1。x0, y0, x1, y1）")]
    [SerializeField] Vector4 countArea = new Vector4(0.08f, 0.48f, 0.5f, 0.78f), dateArea = new Vector4(0.04f, 0.78f, 0.56f, 0.86f);
    [SerializeField] Vector4 liftoffArea = new Vector4(0f, 0.14f, 1f, 0.34f), moonArea = new Vector4(0f, 0.1f, 1f, 0.24f);
    [SerializeField] int moonFontSize = 56, outlineBig = 4, outlineSmall = 3;
    public Font captionFont;                       // 字幕の書体：Baloo 2 ExtraBold（StoryScene が入れる。無ければ元の書体のまま）

    Transform rocket;
    Vector3 rocketBase, rocketScale0 = Vector3.one;
    float rocketH = 4f, flameThick = 0.4f, flameLen = 2f;

    // 実行時に作る物
    Transform[] flames;                            // 外・内・芯
    static readonly Vector2[] FlameLayer = { new Vector2(1f, 1f), new Vector2(0.55f, 0.65f), new Vector2(0.3f, 0.35f) };   // 太さ・長さの倍率
    Material[] flameMats;
    Mesh coneMesh;
    ParticleSystem padBurst;
    bool burstFired;
    Vector3[] crewHome, crewScale;

    // 字幕の元の姿（End で戻す）
    struct TextHome { public Vector2 aMin, aMax, oMin, oMax; public int size; public Font font; public FontStyle style; }
    TextHome bigHome, smallHome;
    bool captionSaved;
    Outline bigLine, smallLine;
    int lastArea = -1;

    public override float Length => TEnd;
    public override bool AllowHouston(float t) => t >= TSpace + 0.3f;   // M03 は宇宙のカット（月へ向かう絵）に重ねる

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
            rocketScale0 = rocket.localScale;
            rocketBase = rocket.position;
            var b = new Bounds(rocket.position, Vector3.zero);
            foreach (var r in rocket.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            rocketH = b.size.y; flameThick = b.size.x * flameWidthK;
        }
        flameLen = rocketH * flameLenK;

        if (crewHome == null)
        {
            crewHome = new Vector3[crew.Length]; crewScale = new Vector3[crew.Length];
            for (int i = 0; i < crew.Length; i++) if (crew[i] != null) { crewHome[i] = crew[i].position; crewScale[i] = crew[i].localScale; }
        }

        BuildFlames();
        FlameColors();
        BuildBurst();
        burstFired = false;
        if (flame != null) flame.gameObject.SetActive(false);
        if (fireLight != null) fireLight.range = fireRange;
        if (trail != null)
        {
            var em = trail.emission; em.rateOverTime = trailRate;
            var m = trail.main; m.startLifetime = new ParticleSystem.MinMaxCurve(trailLife.x, trailLife.y);
        }
        if (ship != null) ship.localScale = Vector3.one * shipScale;
        if (moon != null) moon.localScale = Vector3.one * moonScale;

        SaveCaptions(d);
        d.Cam.fieldOfView = fovStart;
    }

    public override void Pose(StoryDirector d, float t)
    {
        bool inSpace = t >= TSpace;
        ground.gameObject.SetActive(!inSpace);
        space.gameObject.SetActive(inSpace);
        d.SetFade(Mathf.Max(1f - Win(t, 0f, 0.35f), Win(t, TEnd - 0.3f, TEnd)));   // 地上 → 宇宙は暗転なしで直接切り替える
        if (inSpace) PoseSpace(d, t); else PoseGround(d, t);
        Sound(t);
    }

    // ------------------------------------------------------------------ 音（#141。Fable の採点「音を入れる瞬間」の表。大きさは効果音の音量に掛ける）
    void Sound(float t)
    {
        StoryAudio.At(0.2f, "alarm_soft", 0.5f);                                     // 秒読み 3・2・1（「1」だけ高く）
        StoryAudio.At(0.9f, "alarm_soft", 0.5f);
        StoryAudio.At(1.6f, "alarm_soft", 0.5f, 1.2f);
        for (int i = 0; i < crew.Length; i++)                                        // クルーが1人ずつ乗り込む（消える瞬間）
            StoryAudio.At(boardStart + i * boardStagger + boardTime, "ui_switch_on", 0.4f, 1f + 0.1f * i);
        StoryAudio.At(armOpenFrom, "valve_turn", 0.5f, 0.7f);                        // 腕が離れる（重い機械に聞こえるかは要試聴）
        if (StoryAudio.Crossed(TIgnite - 0.1f)) StoryAudio.Hush();                  // 点火の直前の 0.1 秒は無音（溜め）
        bool burning = t >= TIgnite && t < TSpace;                                   // 宇宙へのカットでぴたりと止める（真空は無音。落差が効く）
        float down = Win(t, TSpace - 0.6f, TSpace);                                  // 機体が画面の上へ抜ける間に 0.3 まで下げる
        float burn = Mathf.Lerp(Mathf.Lerp(0.6f, 1f, Win(t, TIgnite, TLift)), 0.3f, down);
        StoryAudio.Loop("burn", "thruster_burn_loop", burning ? burn : 0f, Mathf.Lerp(1f, 1.15f, Win(t, TLift, TSpace - 0.2f)));
        StoryAudio.Loop("rumble", "reentry_rumble_loop", burning ? Mathf.Lerp(0.8f, 0.3f, down) : 0f);
        StoryAudio.At(TLift, "ui_confirm", 0.6f);                                    // LIFTOFF!
        StoryAudio.At(TLift + 0.3f, "jingle_liftoff", 0.8f);                         // LIFTOFF! の字が弾んだあと
        StoryAudio.At(TSpace, "quindar_in", 0.5f);                                   // 宇宙へのカット：ヒューストンの交信が始まる
        if (StoryAudio.Crossed(TSpace + 0.3f)) StoryAudio.Music("flight", 0.5f);     // M03 の字幕と一緒に BGM（船内へそのまま続ける）
        StoryAudio.At(TEnd - 0.25f, "quindar_out", 0.5f);                            // 字幕が消える
    }

    void PoseGround(StoryDirector d, float t)
    {
        Vector3 P = padCenter.position;

        // 腕を逃がす（Unity では localEulerAngles.y = 0 → 90）。クルーが乗ってから
        float arm = Ease(Win(t, armOpenFrom, armOpenTo)) * 90f;
        foreach (var a in arms) if (a != null) a.localRotation = Quaternion.Euler(0f, arm, 0f);

        // ロケット：点火で揺れて縮み、TLift で伸びて加速して上がる
        float lt = Mathf.Max(0f, t - TLift);
        float h = 0.5f * Accel * lt * lt;
        float rs = t >= TIgnite ? (Mathf.PerlinNoise(t * 23f, 7.3f) - 0.5f) * 2f * rocketShake * (1f - Win(t, TLift, TLift + 1f)) : 0f;
        if (rocket != null)
        {
            rocket.position = rocketBase + Vector3.up * h + Vector3.right * rs;
            float sy = 1f, sxz = 1f;
            if (t < TLift) { float k = Ease(Win(t, TIgnite, TLift)); sy = Mathf.Lerp(1f, squashY, k); sxz = Mathf.Lerp(1f, squashXZ, k); }
            else if (t < TLift + 0.15f) { float k = EaseOut(Win(t, TLift, TLift + 0.15f)); sy = Mathf.Lerp(squashY, stretchY, k); sxz = Mathf.Lerp(squashXZ, stretchXZ, k); }
            else { float k = Ease(Win(t, TLift + 0.15f, TLift + 0.5f)); sy = Mathf.Lerp(stretchY, 1f, k); sxz = Mathf.Lerp(stretchXZ, 1f, k); }
            rocket.localScale = Vector3.Scale(rocketScale0, new Vector3(sxz, sy, sxz));   // 原点はノズルの出口なので、足元は動かない
        }

        // 炎・光・噴煙
        bool lit = t >= TIgnite;
        engine.position = rocketBase + Vector3.up * h + Vector3.right * rs;
        float kl = !lit ? 0f : t < TIgnite + 0.2f ? 0.6f * EaseOutBack(Win(t, TIgnite, TIgnite + 0.2f)) : Mathf.Lerp(0.6f, 1f, Ease(Win(t, TIgnite + 0.2f, TLift)));
        for (int i = 0; i < flames.Length; i++)
        {
            var f = flames[i];
            f.gameObject.SetActive(lit);
            if (!lit) continue;
            float ph = i * 1.3f;
            float fl = 1f + 0.15f * Mathf.Sin(t * 41f + ph) + 0.08f * Mathf.Sin(t * 67f + ph);   // 長いときは細く
            float w = flameThick * FlameLayer[i].x * Mathf.Min(1f, kl * 2f) / Mathf.Sqrt(fl);
            f.localScale = new Vector3(w, flameLen * FlameLayer[i].y * kl * fl, w);
        }
        if (trail != null) trail.transform.localPosition = Vector3.down * flameLen * kl * 0.7f;   // 煙は炎の先から
        float fire = !lit ? 0f
            : t < TIgnite + 0.15f ? Mathf.Lerp(0f, fireFlash, EaseOut(Win(t, TIgnite, TIgnite + 0.15f)))
            : t < TIgnite + 0.45f ? Mathf.Lerp(fireFlash, fireSteady, Ease(Win(t, TIgnite + 0.15f, TIgnite + 0.45f)))
            : fireSteady * (1f + 0.15f * Mathf.Sin(t * 37f));
        fireLight.intensity = fire;
        PlayOnce(padSmoke, lit);
        PlayOnce(trail, t >= TLift);
        if (lit && !burstFired && padBurst != null) { padBurst.Play(true); burstFired = true; }   // 地面を横に走る煙の輪（1回だけ）

        // クルー：手を振って、1人ずつロケットの根元へ跳んで乗り込む
        Vector3 enter = rocketBase + Vector3.up * boardEnterY;
        for (int i = 0; i < crew.Length; i++)
        {
            var c = crew[i];
            if (c == null) continue;
            float k = Win(t, boardStart + i * boardStagger, boardStart + i * boardStagger + boardTime);
            c.gameObject.SetActive(k < 1f);
            if (k >= 1f) continue;
            c.position = Vector3.Lerp(crewHome[i], enter, Ease(k)) + Vector3.up * Mathf.Sin(Mathf.PI * k) * boardHop;
            c.localScale = crewScale[i] * (1f - Win(k, 0.7f, 1f));
            float up = Win(t, 0.2f, 0.35f);
            float wave = Mathf.Sin(t * 11f + i * 1.7f) * 18f;
            float a = Mathf.Lerp(Mathf.Sin(t * 2f + i) * 4f, -150f + wave, up);
            float b = Mathf.Lerp(Mathf.Sin(t * 2f + i + 1f) * 4f, -150f - wave, up);
            var l = Deep(c, ".Arm_L"); var r = Deep(c, ".Arm_R");
            if (l != null) l.localRotation = Quaternion.Euler(a, 0f, 0f);
            if (r != null) r.localRotation = Quaternion.Euler(b, 0f, 0f);
            Eyes(c, "Eyes_Normal");
        }

        // カメラ：引きの絵（左にクルー、右にロケット）から、低い位置へ寄って見上げる → 遅れて追うので機体は画面の上へ抜ける
        float push = Ease(Win(t, 0.2f, TIgnite));
        float ly = 0.9f + rocketH * 0.35f;   // 引きの絵の注視点（ロケットの頂上と足元のクルーが両方入る高さ）
        Vector3 cam = Vector3.Lerp(P + new Vector3(-1.0f, 2.0f, -11.5f), P + new Vector3(-1.6f, camLowY, -(camDist0 + rocketH * camDistPerH)), push);
        Vector3 look = Vector3.Lerp(P + new Vector3(-0.8f, ly, 0f), P + new Vector3(-0.3f, rocketH * lookFrac, 0f), push);
        float hf = followK * (1f - Mathf.Exp(-h / followK));
        look = Vector3.Lerp(look, rocketBase + Vector3.up * (hf + rocketH * lookFrac), Ease(Win(t, TLift, TLift + 1.3f)));
        cam += Vector3.up * hf * 0.3f;
        float amp = !lit ? 0f : t < TLift ? Mathf.Lerp(shakeIgnite, shakeLift, Win(t, TIgnite, TLift)) : shakeLift * (1f - Win(t, TLift, TLift + 1.2f));
        float n1 = Mathf.PerlinNoise(t * shakeFreq, 0f) - 0.5f, n2 = Mathf.PerlinNoise(t * shakeFreq, 5.1f) - 0.5f, n3 = Mathf.PerlinNoise(t * shakeFreq, 9.4f) - 0.5f;
        Vector3 shake = new Vector3(n1, n2, 0f) * 2f * amp;
        d.Cam.transform.position = cam + shake;
        d.Cam.transform.LookAt(look + shake);
        if (shakeLift > 0f) d.Cam.transform.Rotate(0f, 0f, n3 * 2f * shakeRoll * amp / shakeLift, Space.Self);
        float fov = fovStart;
        if (t >= TIgnite) fov = Mathf.Lerp(fovStart, fovPunch, EaseOut(Win(t, TIgnite, TIgnite + 0.15f)));
        if (t >= TLift) fov = Mathf.Lerp(fovPunch, fovLift, Ease(Win(t, TLift, TLift + 1.2f)));
        d.Cam.fieldOfView = fov;

        // 空：昼の空 → 濃紺 → 宇宙（4.8 秒ごろには宇宙の色。次の星空のカットへつながる）
        float sky = Mathf.Clamp01(h / 10f);
        d.SetBackground(sky < 0.6f ? Color.Lerp(DaySky, Navy, sky / 0.6f) : Color.Lerp(Navy, SpaceDeep, (sky - 0.6f) / 0.4f));

        // 字幕：秒読みは左上の空（クルーの上）、LIFTOFF! は下 1/3。navy の縁取り
        if (t < 0.2f) d.Caption("", "");
        else if (t < 2.3f)
        {
            float sw = t < 0.9f ? 0.2f : t < 1.6f ? 0.9f : 1.6f;
            string n = t < 0.9f ? "3" : t < 1.6f ? "2" : "1";
            Area(d, 0);
            d.Caption(n, DateLine, null, Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(Win(t, sw, sw + 0.18f))));
        }
        else if (t < TLift) d.Caption("", "");
        else if (t < TSpace - 0.3f)
        {
            Area(d, 1);
            d.Caption("Liftoff!", "", Orange, EaseOutBack(Win(t, TLift, TLift + 0.35f)));
        }
        else d.Caption("", "");
    }

    void PoseSpace(StoryDirector d, float t)
    {
        Vector3 S = space.position;
        float s = Win(t, TSpace, TEnd);
        Vector3 from = S + shipFrom, to = S + shipTo;
        Vector3 dir = (to - from).normalized;
        ship.position = Vector3.Lerp(from, to, EaseOut(s));   // 速く入って、ゆっくり落ち着く
        ship.rotation = Quaternion.FromToRotation(Vector3.up, dir) * Quaternion.Euler(0f, 40f + t * 25f, 0f);   // 進む向き＝船の +Y、ゆっくり回る
        if (moon != null) moon.Rotate(0f, Time.unscaledDeltaTime * 3f, 0f, Space.Self);
        if (earth != null) earth.Rotate(0f, Time.unscaledDeltaTime * 6f, 0f, Space.Self);
        d.Cam.fieldOfView = fovSpace;
        d.Cam.transform.position = S + new Vector3(-0.6f + s * 0.9f, -0.2f + s * 0.4f, -12f);
        d.Cam.transform.LookAt(S + new Vector3(1.5f, 1.2f, 20f));
        d.UseSkybox();
        Area(d, 2);
        if (t > TSpace + 0.3f && t < TEnd - 0.25f) d.Caption("", "To the Moon!");
        else d.Caption("", "");
        fireLight.intensity = 0f;
        PlayOnce(padSmoke, false);
        PlayOnce(trail, false);
    }

    public override void End(StoryDirector d)
    {
        foreach (var a in arms) if (a != null) a.localRotation = Quaternion.identity;
        if (rocket != null) { rocket.localPosition = Vector3.zero; rocket.localScale = rocketScale0; }
        if (crewHome != null)
            for (int i = 0; i < crew.Length; i++)
                if (crew[i] != null) { crew[i].gameObject.SetActive(true); crew[i].position = crewHome[i]; crew[i].localScale = crewScale[i]; }
        if (trail != null) trail.transform.localPosition = Vector3.zero;
        RestoreCaptions(d);
        base.End(d);
    }

    void OnDestroy()
    {
        if (flameMats != null) foreach (var m in flameMats) if (m != null) Destroy(m);
        if (coneMesh != null) Destroy(coneMesh);
    }

    // ------------------------------------------------------------------ 字幕の置き場所（StoryDirector の字幕を借りて、End で戻す）
    void SaveCaptions(StoryDirector d)
    {
        var big = d.CaptionBig; var small = d.CaptionSmall;
        if (big == null || small == null) return;
        if (!captionSaved) { bigHome = Save(big); smallHome = Save(small); captionSaved = true; }
        bigLine = Line(big, bigLine, outlineBig);
        smallLine = Line(small, smallLine, outlineSmall);
        if (captionFont != null) foreach (var t in new[] { big, small }) { t.font = captionFont; t.fontStyle = FontStyle.Normal; }   // ExtraBold なので太字の合成はしない
        lastArea = -1;
    }

    void RestoreCaptions(StoryDirector d)
    {
        var big = d.CaptionBig; var small = d.CaptionSmall;
        if (!captionSaved || big == null || small == null) return;
        Load(big, bigHome); Load(small, smallHome);
        big.font = bigHome.font; big.fontStyle = bigHome.style; small.font = smallHome.font; small.fontStyle = smallHome.style;
        if (bigLine != null) bigLine.enabled = false;
        if (smallLine != null) smallLine.enabled = false;
        lastArea = -1;
    }

    /// <summary>0＝秒読み（左上）、1＝LIFTOFF!（下 1/3）、2＝TO THE MOON（下、大きめ）</summary>
    void Area(StoryDirector d, int which)
    {
        if (which == lastArea || d.CaptionBig == null) return;
        lastArea = which;
        var big = d.CaptionBig; var small = d.CaptionSmall;
        Load(big, bigHome); Load(small, smallHome);
        if (which == 0) { Put(big, countArea); Put(small, dateArea); }
        else if (which == 1) Put(big, liftoffArea);
        else { Put(small, moonArea); small.fontSize = moonFontSize; }
    }

    static TextHome Save(Text t)
    {
        var r = t.rectTransform;
        return new TextHome { aMin = r.anchorMin, aMax = r.anchorMax, oMin = r.offsetMin, oMax = r.offsetMax, size = t.fontSize, font = t.font, style = t.fontStyle };
    }

    static void Load(Text t, TextHome h)
    {
        var r = t.rectTransform;
        r.anchorMin = h.aMin; r.anchorMax = h.aMax; r.offsetMin = h.oMin; r.offsetMax = h.oMax; t.fontSize = h.size;
    }

    static void Put(Text t, Vector4 a)
    {
        var r = t.rectTransform;
        r.anchorMin = new Vector2(a.x, a.y); r.anchorMax = new Vector2(a.z, a.w); r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static Outline Line(Text t, Outline o, int width)
    {
        if (o == null) o = t.gameObject.AddComponent<Outline>();
        o.effectColor = Navy; o.effectDistance = new Vector2(width, -width); o.enabled = true;
        return o;
    }

    // ------------------------------------------------------------------ 実行時に作る物
    /// <summary>炎：円錐3枚（外 orange・内 seat_yellow・芯 cream。HDR で Bloom に掛ける）。原点は底面＝ノズル側、先は −Y</summary>
    void BuildFlames()
    {
        if (flames != null) return;
        coneMesh = ConeMesh(12);
        var src = flame != null ? flame.GetComponent<Renderer>() : null;
        var baseMat = src != null ? src.sharedMaterial : null;
        string[] names = { "Flame_Outer", "Flame_Inner", "Flame_Core" };
        flames = new Transform[3]; flameMats = new Material[3];
        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject(names[i]);
            go.transform.SetParent(engine, false);
            go.AddComponent<MeshFilter>().sharedMesh = coneMesh;
            var mr = go.AddComponent<MeshRenderer>();
            var m = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.name = names[i];
            m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Front);   // 裏の面だけ描く（上の説明）
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.SetActive(false);
            flames[i] = go.transform; flameMats[i] = m;
        }
    }

    /// <summary>炎の色（外 orange・内 seat_yellow・芯 cream。外ほど暗くして、トーンマップ後も3色の帯に見せる）</summary>
    void FlameColors()
    {
        Color[] cols = { Orange, SeatYellow, Cream };
        for (int i = 0; i < flameMats.Length; i++)
        {
            float k = flameHdr * flameGlow[i];
            flameMats[i].SetColor("_BaseColor", new Color(cols[i].r * k, cols[i].g * k, cols[i].b * k, 1f));
        }
    }

    /// <summary>底面の半径 0.5・高さ 1 の円錐（先が −Y）。先端は尖らせず、高さ 85% の所で半径 0.12 の輪を1段入れて丸める。表は外向き</summary>
    static Mesh ConeMesh(int seg)
    {
        float[] ys = { 0f, -0.85f }, rs = { 0.5f, 0.12f };
        var v = new System.Collections.Generic.List<Vector3>();
        var tri = new System.Collections.Generic.List<int>();
        for (int k = 0; k < 2; k++)
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                v.Add(new Vector3(Mathf.Cos(a) * rs[k], ys[k], Mathf.Sin(a) * rs[k]));
            }
        int tip = v.Count; v.Add(new Vector3(0f, -1f, 0f));
        int cap = v.Count; v.Add(new Vector3(0f, 0.04f, 0f));   // ふたは少し山にする（3枚のふたが同じ面に重なってちらつかないよう、内側ほど低くなる）
        var c = new Vector3(0f, -0.5f, 0f);   // 中の点（表を外へ向ける判定に使う）
        void Tri(int a, int b, int d)
        {
            var n = Vector3.Cross(v[b] - v[a], v[d] - v[a]);
            var mid = (v[a] + v[b] + v[d]) / 3f;
            if (Vector3.Dot(n, mid - c) < 0f) { tri.Add(a); tri.Add(d); tri.Add(b); } else { tri.Add(a); tri.Add(b); tri.Add(d); }
        }
        for (int i = 0; i < seg; i++)
        {
            int j = (i + 1) % seg;
            Tri(i, j, seg + i); Tri(j, seg + j, seg + i);   // 側面
            Tri(seg + i, seg + j, tip);                    // 先の丸み
            Tri(cap, j, i);                                // 底面（ふた）
        }
        var mesh = new Mesh { name = "FlameCone" };
        mesh.SetVertices(v); mesh.SetTriangles(tri, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>点火の煙：padSmoke を写して、地面を横に走る輪（1回だけ 40 個）にする</summary>
    void BuildBurst()
    {
        if (padBurst != null || padSmoke == null) return;
        padBurst = Instantiate(padSmoke, padSmoke.transform.parent);
        padBurst.name = "PadBurst";
        padBurst.transform.localPosition = padSmoke.transform.localPosition + Vector3.down * 0.25f;
        padBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = padBurst.main;
        main.loop = false; main.duration = 1f; main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        var em = padBurst.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });
        var sh = padBurst.shape;   // padSmoke は局所 +Z が上なので、Circle（局所 XY）は地面と平行の輪になる
        sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.5f; sh.radiusThickness = 0f; sh.arc = 360f;
        var lv = padBurst.limitVelocityOverLifetime;
        lv.drag = 3f;
    }
}
