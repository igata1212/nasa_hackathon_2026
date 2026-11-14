// 書いた人：Claude ／ 最終更新：2026-11-14 ／ 更新：iizuka（Opus 5.5、2026-11-14、#130）
// 演出3：帰還（#124。storyboard.md の場面6〜7。ED-A・ED-B のとき）。約 6.7 秒。#130 で Fable の採点をもとに作り直した。
//   0.0〜2.0  再突入（RE-ENTRY）：熱シールドを前に、燃えながら斜めに落ちる。光る熱シールド・炎の尾・流れる筋・カメラの遅れと揺れ。海は隠す
//            カメラは真横から（Unity で見て直した：正面寄りだと熱シールドが「黄色い円盤」に見える。包む光の層も板に見えたので作らない）
//   2.1       パラシュート3つ（Fx_Parachute）が開く「ガクン」（船が少し持ち上がり、画角が開く）→ 振り子に揺れながら雲の前を降りる
//   4.3       海に約 8 m/s で着水：沈んで浮き上がる、しぶきの冠と柱、白い輪、カメラの揺れ
//   4.5       「SPLASHDOWN!」（ED-A だけ席の4色の紙吹雪）→ 余韻 → 6.3〜6.7 黒 → エンディングの画面
// ヒューストンの帯は自動では出さない（結果を先に言わないため）。ED-A は傘が開くと同時に「We see parachutes!」、ED-B は着水のあと。
// サーバーの ending は ENDING_HOLD_S（16 秒）で report に進む。実寸（司令船 直径 3.9m）。海は原点の高さ（transform.position.y）。
// 揺れは Random ではなく Mathf.Sin で作る（同じ t なら同じ絵）。
using UnityEngine;

public class ReturnStage : StoryStage
{
    public Transform cm;                 // 司令船（ルートの原点＝熱シールドの底）
    public Transform chute;              // パラシュート（cm の子。DockRing の上）
    public Transform glow;               // 熱シールドの下の光る芯（cm の子）
    public Transform glowShell;          // 芯を包む、透ける光（cm の子。加算）。今は作らない（板に見えた）。null でよい
    public Renderer ocean;               // 再突入の間は隠す（高さ 85m で下に海が見えると大きさが分からなくなる）
    public Light heatLight;
    public ParticleSystem heatTrail, speedLines, splash, splashColumn, foam, foamStay, confetti;

    // 時刻表（秒）
    public const float T1 = 2.0f, HeatOff0 = 1.6f, HeatOff1 = 2.1f, TChute = 2.1f, TSplash = 4.3f, TText = 4.5f, TEnd = 6.7f;
    const float JoltS = 0.35f;           // 傘が開く「ガクン」の長さ

    [Header("道（ReturnStage の原点からの m）")]
    public Vector3 entryFrom = new Vector3(-34f, 85f, 12f);
    public Vector3 entryTo = new Vector3(-12f, 46f, 10f);
    public Vector3 splashAt = new Vector3(-5f, -0.5f, 10f);   // 着水したときの船の位置（y は浮かぶ高さ）
    [Tooltip("再突入の傾き（度）。頂点が左上・熱シールドが進む右下を向く")] public float entryTilt = 28f;
    public float joltUp = 1.5f, swingDeg = 7f, swingX = 0.8f, swingRate = 4.2f;

    [Header("カメラ")]
    public Vector3 entryCam = new Vector3(1f, -1f, -18f);      // 船からの位置（0.15 秒遅れて追う）。ほぼ真横＝熱シールドが縁から見え、尾と筋が斜めに流れる
    public float camLag = 0.15f, entryShake = 0.35f, splashShake = 0.5f;
    public Vector3 descentCam = new Vector3(6f, 3f, -30f);     // 原点からの位置
    public float descentLookUp = 8.5f;                         // 船の上この高さを見る（傘まで1枚に収める）
    public Vector3 splashLook = new Vector3(-5f, 6f, 10f);     // 着水の前後に見る所（船が画面の下 1/3 に来る）
    public float fov = 40f, joltFov = 4f, splashFov = 3f;

    [Header("着水のあとの傘")]
    [Tooltip("倒れる角度（度）。カメラの奥（+Z）へ倒す。90 を越えると傘が水面に寝る")] public float chuteTipDeg = 100f;
    [Tooltip("しぼんだときの縦の倍率")] public float chuteSag = 0.7f;

    static readonly Color High = Ui.Hex("2B2550"), Sky = Ui.Hex("8FC1E3");

    Vector3 glowScale, shellScale, chuteScale;
    bool splashed, partied;

    public override float Length => TEnd;
    /// <summary>ヒューストンの帯は Pose が決める（Run が拾った台詞を最初のフレームに一瞬出さないように）</summary>
    public override bool AllowHouston(float t) => false;

    public override void Begin(StoryDirector d)
    {
        base.Begin(d);
        if (glowScale == Vector3.zero && glow != null) glowScale = glow.localScale;
        if (shellScale == Vector3.zero && glowShell != null) shellScale = glowShell.localScale;
        if (chuteScale == Vector3.zero && chute != null) chuteScale = chute.localScale;
        splashed = partied = false;
        d.Cam.fieldOfView = fov;
        d.EndingLayout(true);
    }

    /// <summary>船の道（原点から）。再突入は斜めにまっすぐ、傘のあとは減速しながら降り、水面に約 8 m/s で当たって沈み、浮き上がる</summary>
    Vector3 ShipPath(float t)
    {
        if (t < T1) return Vector3.LerpUnclamped(entryFrom, entryTo, t / T1);
        if (t < TSplash)
        {
            float u = Win(t, T1, TSplash);
            float y = Mathf.Lerp(entryTo.y, splashAt.y, 0.6f * EaseOut(u) + 0.4f * u);
            y += joltUp * Mathf.Sin(Mathf.PI * Win(t, TChute, TChute + JoltS));
            return new Vector3(Mathf.Lerp(entryTo.x, splashAt.x, u) + swingX * Swing(t), y, Mathf.Lerp(entryTo.z, splashAt.z, u));
        }
        float s = t - TSplash;
        return new Vector3(splashAt.x, splashAt.y - 1.5f * Mathf.Exp(-2.5f * s) * Mathf.Sin(5.5f * s) + 0.12f * Mathf.Sin(3f * s), splashAt.z);
    }

    /// <summary>傘の下の振り子（-1〜1）。開いてから入り、だんだん小さくなり、着水の手前で止まる</summary>
    float Swing(float t)
    {
        if (t < TChute || t >= TSplash) return 0f;
        float u = Win(t, T1, TSplash);
        return Mathf.Sin((t - TChute) * swingRate) * (1f - 0.6f * u) * Win(t, TChute, TChute + 0.3f) * (1f - Win(t, TSplash - 0.4f, TSplash));
    }

    public override void Pose(StoryDirector d, float t)
    {
        Vector3 R = transform.position;
        string e = d.Ending();
        bool edA = e != "ED-B";                 // 確認用で結末が分からないときは ED-A として見せる
        float s = t - TSplash;                  // 着水からの秒（前は負）
        d.SetFade(Mathf.Max(1f - Win(t, 0f, 0.3f), Win(t, TEnd - 0.4f, TEnd)));

        // 司令船：位置と傾き（再突入の傾き → まっすぐ、振り子、着水の揺れ）
        cm.position = R + ShipPath(t);
        float tilt = Mathf.Lerp(entryTilt, 0f, Ease(Win(t, T1 - 0.2f, T1 + 0.8f)));
        float rock = s >= 0f ? 10f * Mathf.Exp(-1.5f * s) * Mathf.Sin(4f * s) + 3f * Mathf.Sin(2.3f * s) : 0f;
        cm.rotation = Quaternion.Euler(0f, 20f, tilt + swingDeg * Swing(t) + rock);

        // 熱：芯と包む光・光・炎の尾・流れる筋
        float heat = 1f - Win(t, HeatOff0, HeatOff1);
        glow.gameObject.SetActive(heat > 0.01f);
        glow.localScale = glowScale * heat * (1f + 0.08f * Mathf.Sin(t * 31f));
        if (glowShell != null)
        {
            glowShell.gameObject.SetActive(heat > 0.01f);
            glowShell.localScale = shellScale * heat * (1f + 0.08f * Mathf.Sin(t * 31f + 1.3f));
        }
        heatLight.intensity = 7f * heat * (1f + 0.15f * Mathf.Sin(t * 23f));
        PlayOnce(heatTrail, heat > 0.3f);
        PlayOnce(speedLines, heat > 0.3f);
        if (t >= TChute && speedLines != null && speedLines.particleCount > 0) speedLines.Clear(true);   // 傘の「ガクン」で流れる筋が止まる
        if (ocean != null) ocean.enabled = t >= T1 - 0.3f;

        // パラシュート：先に細長く伸びてから、ぼんと広がる。着水したら奥へ倒れて水面に寝ながら、少ししぼむ
        //（Unity で見て直した：先に縦を 0.25 倍にしぼませると船の真上に平たい板が浮き、横（Z 軸）に倒すと傘が手前に来て船を隠した）
        bool open = t >= TChute;
        chute.gameObject.SetActive(open);
        if (open)
        {
            float wide = Mathf.LerpUnclamped(0.2f, 1f, EaseOutBack(Win(t, TChute + 0.1f, TChute + 0.6f)));
            float tall = Mathf.Lerp(0.6f, 1f, EaseOut(Win(t, TChute, TChute + 0.3f)));
            float sag = Ease(Win(t, TSplash + 0.05f, TSplash + 0.6f));
            float tip = Ease(Win(t, TSplash + 0.1f, TSplash + 0.9f));
            wide *= Mathf.Lerp(1f, 1.1f, sag);
            tall *= Mathf.Lerp(1f, chuteSag, sag);
            chute.localScale = new Vector3(chuteScale.x * wide, chuteScale.y * tall, chuteScale.z * wide);
            chute.localRotation = Quaternion.Euler(chuteTipDeg * tip, 0f, 0f);
        }

        // 着水：しぶきの冠と柱・白い輪（1回だけ）と、船のまわりに残る泡（2 秒）
        if (s >= 0f && !splashed)
        {
            splashed = true;
            var at = new Vector3(cm.position.x, R.y + 0.05f, cm.position.z);
            foreach (var ps in new[] { splash, splashColumn, foam, foamStay })
                if (ps != null) ps.transform.position = at;
            foreach (var ps in new[] { splash, splashColumn, foam })
                if (ps != null) ps.Play(true);
        }
        if (s < 0f && splashed)
        {
            splashed = false;
            foreach (var ps in new[] { splash, splashColumn, foam, foamStay })
                if (ps != null) ps.Clear(true);
        }
        PlayOnce(foamStay, s >= 0f && s < 2f);

        // ED-A だけ：席の4色の紙吹雪（ED-B は出さない。「ぎりぎり」との差）
        if (edA && t >= TText && !partied && confetti != null)
        {
            partied = true;
            confetti.transform.position = new Vector3(cm.position.x, R.y + 1.5f, cm.position.z);
            confetti.Play(true);
        }
        if (t < TText && partied) { partied = false; confetti.Clear(true); }

        // カメラ：再突入は 0.15 秒前の船の位置から今の船を見る（船が少し先に出て、進んで見える）。熱の間と着水の瞬間は揺れる
        Vector3 shake = new Vector3(Mathf.Sin(t * 47f), Mathf.Sin(t * 61f + 1f), 0f) * entryShake * heat;
        if (s >= 0f) shake += new Vector3(Mathf.Sin(t * 53f), Mathf.Sin(t * 67f), 0f) * splashShake * Mathf.Exp(-6f * s);
        Vector3 c1 = R + ShipPath(t - camLag) + entryCam, l1 = cm.position;
        Vector3 c2 = R + descentCam, l2 = cm.position + Vector3.up * descentLookUp;
        l2 = Vector3.Lerp(l2, R + splashLook, Ease(Win(t, TSplash - 0.5f, TSplash + 0.5f)));
        float k = Ease(Win(t, T1 - 0.3f, T1 + 0.9f));
        d.Cam.transform.position = Vector3.Lerp(c1, c2, k) + shake;
        d.Cam.transform.LookAt(Vector3.Lerp(l1, l2, k));
        // 画角：傘の「ガクン」で開き、着水の瞬間に少し寄る
        float jolt = Mathf.Sin(Mathf.PI * Win(t, TChute, TChute + JoltS));
        float punch = Win(s, 0f, 0.12f) * (1f - Ease(Win(s, 0.12f, 0.62f)));
        d.Cam.fieldOfView = fov + joltFov * jolt - splashFov * punch;

        // 空：上空の紫のまま 1.2 秒まで、そのあと 2.6 秒までに水色へ
        d.SetBackground(Color.Lerp(High, Sky, Ease(Win(t, 1.2f, 2.6f))));

        // 字幕：下の短い字幕 → ヒューストンの帯（下）→ SPLASHDOWN!（上）
        string lowText = "";
        if (t > 0.25f && t < T1 - 0.2f) lowText = "RE-ENTRY  -  HEAT SHIELD FIRST";
        else if (t >= TChute && t < TChute + (edA ? 0.5f : 0.8f)) lowText = "MAIN CHUTES OPEN";
        d.LowCaption(lowText);
        d.HoustonBar(t >= (edA ? TChute + 0.5f : TText + 0.3f) ? d.Line(edA ? "ED-A" : "ED-B") : "");
        if (t >= TText) d.Caption("SPLASHDOWN!", edA ? "WELCOME HOME, CREW!" : "WELCOME HOME  -  THAT WAS CLOSE!", Ui.Yellow, EaseOutBack(Win(t, TText, TText + 0.35f)));
        else d.Caption("", "");
    }
}
