// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// 音（#141）。効果音・BGM・本物の交信の声を、GameFlow.Client の段階・状態・イベントの変化（前の状態との差）で鳴らす。
//   ヒューストンの台詞            quindar_in →（字幕）→ 少しして quindar_out。演出の間とエンディングは鳴らさない（演出の音は舞台が StoryAudio で鳴らす）
//   事故（stir → flight／M05）    voice_problem（本物の「Houston, we've had a problem」）。M05 の quindar とマスターアラームはその声のあと
//   警告（UrgencyFx の段階）       1・2 に上がった：alarm_soft を1回／3：alarm_master をループ（下がったら止める。長く続いたら小さくする）
//   漏れ（leak_open）             gas_hiss_loop。閉めた（true → false）：valve_turn → ui_confirm
//   通信途絶（has_signal = false） radio_static_loop（月の裏側と再突入。通信のスイッチが ON のときだけ。OFF なら無線は黙る）
//   噴射（軌道修正・帰還の噴射）    thruster_burn_loop を burnS 秒（サーバーでは一瞬なので長さはここで決める）
//   再突入（t ≥ t_reentry）        reentry_rumble_loop
//   スイッチ（sw・transmit）       ui_switch_on / ui_switch_off。練習で操作した：ui_confirm（小さく）。打ち上げ不可・同時押しの失敗：ui_error
//   ボタン                        ui_click：UiKit のボタンは UiPress が鳴らす。古い Ui.Btn（設計画面など）はここで拾う（ClickFallback。船内は除く）
//   ジングル（Kenney Music Jingles）  部屋に人が増えた：jingle_join／打ち上げの LIFTOFF! のあと：jingle_liftoff（舞台が鳴らす）
//                                 エンディング：ED-A jingle_success・ED-B jingle_close・ED-C jingle_fail（舞台が SPLASHDOWN!／Mission lost で鳴らす。
//                                 演出を飛ばした・流さなかったときは、エンディングに入って少ししてからここで鳴らす）
//                                 事故：jingle_crisis は本物の声と重なるので、声をミュートしているときだけ代わりに鳴らす
//   BGM                           設計・練習・飛行で space_calm_01 を小さくループ。危機（段階 2 以上）で下げる。演出の間・エンディングは止める
//                                 （打ち上げの演出だけは、月へ向かうカットで舞台が BGM を入れ、そのまま船内へ続ける）。曲は用途ごとの枠で差し替えられる
// 音量は BGM・効果音・声の3つ（SoundPrefs。タイトルの DEV パネルでミュート）。AudioListener はメインカメラのものを使う（無いときだけ足す）。
// WebGL は最初のタップ（クリック）まで音が出ない（ブラウザの決まり。仕様）。
// GameFlow・UrgencyFx は変えない：シーンに置いて指すだけ（Editor/Audio/AudioScene.cs が組む）。GameFlow.Client が差し替わったら付け直す。
// BGM の作者表記（Spacedman は CC BY 4.0 で表記が必須）：タイトル画面の下に小さく1行（musicCredit）。TitleLobbyScreens は触らず、ここで別の Canvas に出す。
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum SoundBus { Music, Sfx, Voice }

/// <summary>3つの音量（0〜1）とミュート。PlayerPrefs に残す</summary>
public static class SoundPrefs
{
    static readonly string[] Names = { "music", "sfx", "voice" };
    static float[] vol;
    static bool[] mute;

    static void Load()
    {
        if (vol != null) return;
        vol = new float[3]; mute = new bool[3];
        for (int i = 0; i < 3; i++)
        {
            vol[i] = PlayerPrefs.GetFloat("audio_vol_" + Names[i], 1f);
            mute[i] = PlayerPrefs.GetInt("audio_mute_" + Names[i], 0) != 0;
        }
    }

    public static float Volume(SoundBus b) { Load(); return vol[(int)b]; }
    public static bool Muted(SoundBus b) { Load(); return mute[(int)b]; }
    /// <summary>実際に掛ける音量（ミュートなら 0）</summary>
    public static float Gain(SoundBus b) { Load(); return mute[(int)b] ? 0f : vol[(int)b]; }

    public static void SetVolume(SoundBus b, float v) { Load(); vol[(int)b] = Mathf.Clamp01(v); PlayerPrefs.SetFloat("audio_vol_" + Names[(int)b], vol[(int)b]); PlayerPrefs.Save(); }
    public static void SetMuted(SoundBus b, bool m) { Load(); mute[(int)b] = m; PlayerPrefs.SetInt("audio_mute_" + Names[(int)b], m ? 1 : 0); PlayerPrefs.Save(); }
}

public class GameAudio : MonoBehaviour
{
    public static GameAudio I { get; private set; }

    public GameFlow flow;
    [Tooltip("警告の段階（OnLevelChanged と Level を読むだけ）")] public UrgencyFx urgency;
    [Tooltip("演出の間は船内の音を止める")] public StoryDirector story;

    [Header("効果音（ファイル名で呼ぶ。AudioScene が Assets/Audio/Sfx から入れる）")]
    public AudioClip[] clips = new AudioClip[0];

    [Header("BGM（用途ごとの枠。空の枠は Tooltip の代わりを使う）")]
    [Tooltip("Spacedman（Shane Ivers, CC BY 4.0。CREDITS.md）")] public AudioClip musicCalm;
    [Tooltip("タイトル・部屋（空なら無音）")] public AudioClip musicTitle;
    [Tooltip("設計（空なら musicCalm）")] public AudioClip musicDesign;
    [Tooltip("練習・飛行（空なら musicCalm）")] public AudioClip musicFlight;
    [Tooltip("月の裏側（空なら飛行の曲）")] public AudioClip musicFarside;
    [Tooltip("危機の段階 2 以上（空なら今の曲を crisisDuck まで下げる）")] public AudioClip musicCrisis;
    [Tooltip("エンディング・報告（空なら無音）")] public AudioClip musicEnding;
    [Tooltip("タイトル画面の下に出す作者表記（CC BY 4.0 で必須。空なら出さない）")]
    public string musicCredit = "Music: Spacedman by Shane Ivers - silvermansound.com";
    [Range(0f, 1f)] public float musicLevel = 0.35f;
    [Range(0f, 1f)] public float crisisDuck = 0.55f;
    public float musicFadeS = 1.5f;

    [Header("大きさ（効果音・声の音量に掛ける）")]
    [Range(0f, 1f)] public float quindarVol = 0.45f;
    [Range(0f, 1f)] public float voiceVol = 1f;
    [Range(0f, 1f)] public float alarmSoftVol = 0.6f, alarmMasterVol = 0.45f;
    [Tooltip("マスターアラームがこの秒より長く続いたら alarmQuietVol まで下げる（鳴りっぱなしで疲れないように）")] public float alarmQuietAfterS = 10f;
    [Range(0f, 1f)] public float alarmQuietVol = 0.2f;
    [Range(0f, 1f)] public float hissVol = 0.35f, staticVol = 0.15f, thrusterVol = 0.6f, rumbleVol = 0.5f;
    [Range(0f, 1f)] public float valveVol = 0.8f, switchVol = 0.5f, clickVol = 0.45f, confirmVol = 0.5f, errorVol = 0.5f;
    [Range(0f, 1f)] public float jingleVol = 0.6f;
    [Tooltip("演出がエンディングのジングルを鳴らさなかったとき、エンディングに入ってこの秒で鳴らす")] public float endingJingleFallbackS = 1.5f;
    [Tooltip("噴射の音を鳴らす秒")] public float burnS = 2.5f;
    [Tooltip("quindar_out までの秒 = 最小 + 文字数 × 1文字（最大まで）。声は無いので、読む速さの目安")]
    public float quindarOutMinS = 1.2f, quindarOutPerChar = 0.025f, quindarOutMaxS = 3f;
    public float loopFadeS = 0.4f;

    // ---- 単発（同じ音を違うピッチで重ねられるよう、AudioSource を使い回す）
    class Shot { public AudioSource src; public SoundBus bus; public float vol, until; public bool story; }
    // ---- ループ（名前ごとに1つ）
    class Loop { public AudioSource src; public SoundBus bus; public float vol, target, rate; public bool playing, story; }
    // ---- 少しあとに鳴らす単発（quindar_out など）
    class Later { public float at; public string clip, tag; public float vol, pitch; public bool story; }
    // ---- BGM（2本で入れ替える）
    class Track { public AudioSource src; public float vol, target, rate; public bool playing; }

    const int MaxShots = 12;
    readonly Dictionary<string, AudioClip> byName = new Dictionary<string, AudioClip>();
    readonly HashSet<string> missing = new HashSet<string>();
    readonly List<Shot> shots = new List<Shot>();
    readonly Dictionary<string, Loop> loops = new Dictionary<string, Loop>();
    readonly List<Later> later = new List<Later>();
    Track[] tracks;
    int cur;
    AudioClip storyMusic;
    float storyMusicFade = 0.5f;

    IGameClient hooked;
    UrgencyFx hookedUrgency;
    string phase = "";
    GameState prevState;
    float accidentAt = -99f, burnUntil, alarmSince, endingAt;
    bool endingJingleDone = true;
    int lastPlayers = -1;

    void Awake()
    {
        I = this;
        foreach (var c in clips) if (c != null) byName[c.name] = c;
        tracks = new Track[2];
        for (int i = 0; i < 2; i++) tracks[i] = new Track { src = NewSource(true, 64) };
        if (FindAnyObjectByType<AudioListener>() == null)
        {
            Debug.LogWarning("[Audio] AudioListener が無いので足した（ふだんはメインカメラのものを使う）");
            gameObject.AddComponent<AudioListener>();
        }
        BuildCredit();
    }

    GameObject credit;

    /// <summary>BGM の作者表記。GameFlow の画面（sortingOrder 10）の上に重ねる別の Canvas。押せない（raycast しない）</summary>
    void BuildCredit()
    {
        if (string.IsNullOrEmpty(musicCredit)) return;
        var cgo = new GameObject("MusicCredit");
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 11;
        var scaler = cgo.AddComponent<CanvasScaler>();   // GameFlow の GameCanvas と同じ
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 0.5f;
        var t = UiKit.Text(UiKit.SafeArea(cgo.transform), musicCredit, TextStyle.Label, UiTokens.InkSub, TextAlignmentOptions.Bottom);
        var r = t.rectTransform;
        r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(1f, 0f); r.pivot = new Vector2(0.5f, 0f);
        r.anchoredPosition = new Vector2(0f, UiTokens.ScreenEdge * 0.5f); r.sizeDelta = new Vector2(0f, UiTokens.TextLabel * 1.5f);
        credit = cgo;
        credit.SetActive(false);
    }

    void OnDestroy()
    {
        Unhook();
        if (hookedUrgency != null) hookedUrgency.OnLevelChanged -= OnLevel;
        if (I == this) I = null;
    }

    AudioSource NewSource(bool loop, int priority)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false; s.loop = loop; s.spatialBlend = 0f; s.priority = priority;
        return s;
    }

    // ------------------------------------------------------------------ つなぐ
    void Hook()
    {
        var c = flow != null ? flow.Client : null;
        if (c != hooked)
        {
            Unhook();
            hooked = c; phase = flow != null ? flow.Model.Phase : ""; prevState = null; burnUntil = 0f; lastPlayers = -1; endingJingleDone = true;
            later.Clear();
            if (c != null) { c.OnPhase += OnPhase; c.OnState += OnState; c.OnEvent += OnEvent; c.OnMessage += OnMessage; }
        }
        if (urgency != hookedUrgency)
        {
            if (hookedUrgency != null) hookedUrgency.OnLevelChanged -= OnLevel;
            hookedUrgency = urgency;
            if (urgency != null) urgency.OnLevelChanged += OnLevel;
        }
    }

    void Unhook()
    {
        if (hooked == null) return;
        hooked.OnPhase -= OnPhase; hooked.OnState -= OnState; hooked.OnEvent -= OnEvent; hooked.OnMessage -= OnMessage;
        hooked = null;
    }

    bool StoryPlaying => story != null && story.Playing;
    /// <summary>ヒューストンの交信の音（quindar）を鳴らす段階</summary>
    bool RadioPhase => phase == "design" || phase == "practice" || phase == "stir" || phase == "flight";
    float VoiceLen => Clip("voice_problem") is AudioClip v ? v.length : 1.1f;

    // ------------------------------------------------------------------ 受信
    void OnPhase(string p)
    {
        string prev = phase; phase = p;
        prevState = null;      // 段階が変わったら差を取り直す（最初の状態では鳴らさない）
        burnUntil = 0f;
        if (prev == "stir" && p == "flight") Accident();   // UrgencyFx が大きく揺らす所と同じ判定
        if (p == "ending") { endingAt = Time.unscaledTime; endingJingleDone = false; }
        else endingJingleDone = true;
    }

    /// <summary>部屋の人数が増えた：jingle_join（入った直後の最初の人数では鳴らさない）</summary>
    void OnMessage(Dictionary<string, object> m)
    {
        if (J.Str(m, "type") != "room") return;
        var arr = J.Arr(m, "players");
        int n = arr != null ? arr.Count : 0;
        if (lastPlayers >= 0 && n > lastPlayers && !StoryPlaying) Play("jingle_join", jingleVol, 1f, false);
        lastPlayers = n;
    }

    void OnState(GameState s)
    {
        var p = prevState; prevState = s;
        if (p == null || s.t < p.t || StoryPlaying) return;
        float now = Time.unscaledTime;
        if (p.leakOpen && !s.leakOpen) { Sfx("valve_turn", valveVol); After(0.55f, "ui_confirm", confirmVol); }
        if ((!p.correctionDone && s.correctionDone) || (!p.burnDone && s.burnDone)) burnUntil = now + burnS;
        int on = 0, off = 0;
        foreach (var kv in s.sw)
            if (p.sw.TryGetValue(kv.Key, out var was) && was != kv.Value) { if (kv.Value) on++; else off++; }
        if (p.transmit != s.transmit) { if (s.transmit) on++; else off++; }
        if (on > 0) Sfx("ui_switch_on", switchVol);        // 同じ秒に何個変わっても1回
        else if (off > 0) Sfx("ui_switch_off", switchVol);
    }

    void OnEvent(GameEvent e)
    {
        switch (e.kind)
        {
            case "houston":
                if (e.id == "M05") Accident();
                if (!string.IsNullOrEmpty(e.text)) HoustonLine(e.text);
                break;
            case "launch":
                if (!string.IsNullOrEmpty(e.status) && e.status != "ok") Sfx("ui_error", errorVol);
                break;
            case "press":
                if (e.status == "failed") Sfx("ui_error", errorVol);
                break;
            case "practice":
                if (!StoryPlaying) Sfx("ui_confirm", confirmVol * 0.6f);
                break;
        }
    }

    /// <summary>台詞が来た：quindar_in → 少しして quindar_out。前の台詞の途中なら in は鳴らさず、out を延ばす</summary>
    void HoustonLine(string text)
    {
        if (!RadioPhase || StoryPlaying) return;
        float start = Time.unscaledTime - accidentAt < 0.5f ? VoiceLen + 0.15f : 0f;   // 事故の声のあとに入る
        float speak = Mathf.Clamp(quindarOutMinS + text.Length * quindarOutPerChar, quindarOutMinS, quindarOutMaxS);
        bool talking = later.RemoveAll(x => x.tag == "quindar_out") > 0;
        if (!talking) After(start, "quindar_in", quindarVol, 1f, "quindar_in");
        After(start + speak, "quindar_out", quindarVol, 1f, "quindar_out");
    }

    /// <summary>事故の瞬間：本物の「Houston, we've had a problem」。同じ事故で2回鳴らさない</summary>
    void Accident()
    {
        if (Time.unscaledTime - accidentAt < 2f) return;
        accidentAt = Time.unscaledTime;
        if (StoryPlaying) return;
        Play("voice_problem", voiceVol, 1f, false);
        if (SoundPrefs.Gain(SoundBus.Voice) <= 0f) Play("jingle_crisis", jingleVol, 1f, false);   // 声が聞こえないときの代わりの合図
    }

    /// <summary>エンディングのジングル（ED-A／B／C で選ぶ）。1回のエンディングで1回だけ。舞台が字幕に合わせて呼ぶ</summary>
    public void EndingJingle()
    {
        if (endingJingleDone) return;
        string e = story != null ? story.Ending() : flow != null ? J.Str(flow.Model.Result, "ending") : "";
        if (e == "") return;
        endingJingleDone = true;
        Play(e == "ED-C" ? "jingle_fail" : e == "ED-B" ? "jingle_close" : "jingle_success", jingleVol, 1f, false);   // 飛ばしても最後まで鳴らす
    }

    void OnLevel(int prev, int now)
    {
        if (now >= 3 && prev < 3) alarmSince = Time.unscaledTime;
        if (StoryPlaying || now <= prev) return;
        if (now == 1) Sfx("alarm_soft", alarmSoftVol);
        else if (now == 2) Sfx("alarm_soft", alarmSoftVol, 1.15f);
    }

    // ------------------------------------------------------------------ 毎フレーム
    void Update()
    {
        Hook();
        float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
        if (credit != null) { bool show = flow != null && flow.Model.Phase == "title"; if (credit.activeSelf != show) credit.SetActive(show); }
        ClickFallback();

        for (int i = later.Count - 1; i >= 0; i--)
        {
            var x = later[i];
            if (now < x.at) continue;
            later.RemoveAt(i);
            Play(x.clip, x.vol, x.pitch, x.story);
        }

        UpdateCabin(now);
        UpdateMusic();
        if (!endingJingleDone && phase == "ending" && !StoryPlaying && now - endingAt > endingJingleFallbackS) EndingJingle();

        foreach (var s in shots) if (s.src.isPlaying) s.src.volume = s.vol * SoundPrefs.Gain(s.bus);
        foreach (var l in loops.Values)
        {
            l.vol = l.rate <= 0f ? l.target : Mathf.MoveTowards(l.vol, l.target, l.rate * dt);
            l.src.volume = l.vol * SoundPrefs.Gain(l.bus);
            if (l.playing && l.vol <= 0.002f && l.target <= 0.002f) { l.src.Stop(); l.playing = false; l.vol = 0f; }
        }
        foreach (var t in tracks)
        {
            t.vol = Mathf.MoveTowards(t.vol, t.target, t.rate * dt);
            t.src.volume = t.vol * SoundPrefs.Gain(SoundBus.Music);
            if (t.playing && t.vol <= 0f && t.target <= 0f) { t.src.Stop(); t.playing = false; }
        }
    }

    /// <summary>船内のループ（漏れ・無線の雑音・再突入・噴射・マスターアラーム）。flight の間だけ、演出の間は止める</summary>
    void UpdateCabin(float now)
    {
        var s = flow != null ? flow.Model.State : null;
        bool cabin = s != null && phase == "flight" && !StoryPlaying;
        SetLoop("hiss", "gas_hiss_loop", cabin && s.leakOpen ? hissVol : 0f, 1f, loopFadeS);
        SetLoop("static", "radio_static_loop", cabin && !s.hasSignal && s.Switch("comms") ? staticVol : 0f, 1f, loopFadeS);
        SetLoop("rumble", "reentry_rumble_loop", cabin && s.tReentry > 0f && s.t >= s.tReentry ? rumbleVol : 0f, 1f, 1f);
        SetLoop("thruster", "thruster_burn_loop", !StoryPlaying && now < burnUntil ? thrusterVol : 0f, 1f, 0.5f);
        bool alarm = cabin && urgency != null && urgency.Level >= 3 && now - accidentAt > VoiceLen;   // 事故の声に重ねない
        SetLoop("alarm", "alarm_master", alarm ? (now - alarmSince > alarmQuietAfterS ? alarmQuietVol : alarmMasterVol) : 0f, 1f, 0.15f);
    }

    void UpdateMusic()
    {
        AudioClip clip = null;
        float level = musicLevel, fade = musicFadeS;
        if (StoryPlaying) { clip = storyMusic; fade = clip != null ? storyMusicFade : 0.3f; }
        else switch (phase)
        {
            case "title": case "lobby": clip = musicTitle; break;
            case "design": clip = Slot("design"); break;
            case "practice": case "stir": case "flight":
                var s = flow != null ? flow.Model.State : null;
                clip = phase == "flight" && s != null && s.inFarside ? Slot("farside") : Slot("flight");
                if (urgency != null && urgency.Level >= 2) { if (musicCrisis != null) clip = musicCrisis; else level *= crisisDuck; }
                break;
            case "ending": case "report": clip = musicEnding; break;
        }
        WantMusic(clip, level, fade);
    }

    /// <summary>用途の名前（"title" "design" "flight" "farside" "crisis" "ending"）→ 曲。空の枠は代わりを使う</summary>
    public AudioClip Slot(string slot)
    {
        switch (slot)
        {
            case "title": return musicTitle;
            case "design": return musicDesign != null ? musicDesign : musicCalm;
            case "flight": return musicFlight != null ? musicFlight : musicCalm;
            case "farside": return musicFarside != null ? musicFarside : Slot("flight");
            case "crisis": return musicCrisis != null ? musicCrisis : Slot("flight");
            case "ending": return musicEnding;
            default: return null;
        }
    }

    /// <summary>この曲をこの大きさで流す（同じ曲なら続ける。違えば2本で入れ替える。null で止める）</summary>
    void WantMusic(AudioClip clip, float level, float fadeS)
    {
        float rate = 1f / Mathf.Max(0.05f, fadeS);
        Track a = tracks[cur], b = tracks[1 - cur];
        if (clip == null) { a.target = b.target = 0f; a.rate = b.rate = rate; return; }
        if (a.src.clip != clip && b.src.clip == clip && b.playing) { cur = 1 - cur; (a, b) = (b, a); }   // 消えかけの曲に戻る
        if (a.src.clip != clip)
        {
            if (a.playing) { a.target = 0f; a.rate = rate; (a, b) = (b, a); cur = 1 - cur; }   // 今の曲は消して、もう1本で入れる
            if (a.playing) a.src.Stop();
            a.src.clip = clip; a.vol = 0f; a.playing = false;
        }
        if (!a.playing) { a.src.Play(); a.playing = true; }
        a.target = level; a.rate = rate;
        if (b != a) { b.target = 0f; b.rate = rate; }
    }

    // ------------------------------------------------------------------ 鳴らす
    AudioClip Clip(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (byName.TryGetValue(name, out var c)) return c;
        if (missing.Add(name)) Debug.LogWarning("[Audio] 音が無い: " + name + "（Tools/Audio/音を組み立てる で入れ直す）");
        return null;
    }

    /// <summary>交信の音（quindar_*）と声（voice_*）は「声」、ジングル（jingle_*）は「BGM」、それ以外は「効果音」</summary>
    static SoundBus BusOf(string clip) =>
        clip.StartsWith("quindar") || clip.StartsWith("voice") ? SoundBus.Voice : clip.StartsWith("jingle") ? SoundBus.Music : SoundBus.Sfx;

    void Sfx(string clip, float vol, float pitch = 1f) => Play(clip, vol, pitch, false);

    void Play(string clip, float vol, float pitch, bool fromStory)
    {
        var c = Clip(clip);
        if (c == null) return;
        float now = Time.unscaledTime;
        Shot s = null;
        foreach (var x in shots) if (now >= x.until) { s = x; break; }
        if (s == null && shots.Count < MaxShots) { s = new Shot { src = NewSource(false, 96) }; shots.Add(s); }
        if (s == null) { s = shots[0]; foreach (var x in shots) if (x.until < s.until) s = x; }   // 全部鳴っていたら、いちばん早く終わるものを使う
        s.bus = BusOf(clip); s.vol = vol; s.story = fromStory;
        s.until = now + c.length / Mathf.Max(0.05f, pitch);
        s.src.Stop();
        s.src.clip = c; s.src.pitch = pitch; s.src.volume = vol * SoundPrefs.Gain(s.bus);
        s.src.Play();
    }

    void After(float delay, string clip, float vol, float pitch = 1f, string tag = null, bool fromStory = false)
    {
        later.Add(new Later { at = Time.unscaledTime + delay, clip = clip, vol = vol, pitch = pitch, tag = tag, story = fromStory });
    }

    /// <summary>ループの大きさを決める（0 で止める）。fadeS が 0 以下ならすぐその大きさにする</summary>
    void SetLoop(string key, string clip, float target, float pitch, float fadeS, bool fromStory = false)
    {
        if (!loops.TryGetValue(key, out var l))
        {
            if (target <= 0.002f) return;
            var c = Clip(clip);
            if (c == null) return;
            l = new Loop { src = NewSource(true, 112), bus = BusOf(clip), story = fromStory };
            l.src.clip = c;
            loops[key] = l;
        }
        if (l.src.clip == null || l.src.clip.name != clip)
        {
            var c = Clip(clip);
            if (c == null) return;
            l.src.Stop(); l.playing = false; l.src.clip = c; l.bus = BusOf(clip);
        }
        l.target = Mathf.Max(0f, target);
        l.rate = fadeS <= 0f ? 0f : 1f / fadeS;
        l.src.pitch = pitch;
        if (l.rate <= 0f)   // すぐその大きさにする（演出の「ぴたりと止める」を1フレーム遅らせない）
        {
            l.vol = l.target;
            if (l.playing && l.vol <= 0.002f) { l.src.Stop(); l.playing = false; l.vol = 0f; }
        }
        l.src.volume = l.vol * SoundPrefs.Gain(l.bus);
        if (l.target > 0.002f && !l.playing) { l.src.Play(); l.playing = true; }
    }

    // ------------------------------------------------------------------ ボタン・演出から（StoryAudio 経由）
    /// <summary>ボタンを押した音（UiPress から）</summary>
    public static void Click() { var a = I; if (a != null) a.Sfx("ui_click", a.clickVol); }

    readonly List<RaycastResult> clickHits = new List<RaycastResult>();

    /// <summary>古いボタン（Ui.Btn。設計画面の ±・READY FOR LAUNCH など。UiPress が無い）を押したときも ui_click。
    /// 船内（練習・飛行）のボタンはスイッチの音が状態の差で鳴るので鳴らさない。UiPress の付いたボタンは UiPress が鳴らす</summary>
    void ClickFallback()
    {
        if (phase == "practice" || phase == "stir" || phase == "flight" || StoryPlaying) return;
        var es = EventSystem.current;
        if (es == null) return;
        Vector2 pos;
        var m = Mouse.current; var ts = Touchscreen.current;
        if (m != null && m.leftButton.wasPressedThisFrame) pos = m.position.ReadValue();
        else if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) pos = ts.primaryTouch.position.ReadValue();
        else return;
        clickHits.Clear();
        es.RaycastAll(new PointerEventData(es) { position = pos }, clickHits);
        if (clickHits.Count == 0) return;
        var go = clickHits[0].gameObject;
        var sel = go.GetComponentInParent<Selectable>();
        if (sel == null || !sel.IsInteractable() || go.GetComponentInParent<UiPress>() != null) return;
        Sfx("ui_click", clickVol);
    }

    /// <summary>演出が始まる：演出の音を消し、船内の交信の音（待っている quindar）をやめる。BGM は止める（舞台が入れるまで）</summary>
    public void StoryBegin()
    {
        StoryStop();
        storyMusic = null;
        later.RemoveAll(x => x.tag == "quindar_in" || x.tag == "quindar_out");
    }

    /// <summary>演出が終わった：演出の音をすぐ止める（BGM は段階の決まりに戻る）。
    /// 演出の BGM（storyMusic）は残す：舞台は片づけたあと 0.35 秒かけて船内へ戻り、その間も Playing なので、ここで消すと BGM が一度切れて頭から鳴り直す</summary>
    public void StoryStop()
    {
        StoryHush();
        foreach (var l in loops.Values) if (l.story) { l.target = l.vol = 0f; l.src.Stop(); l.playing = false; }
        later.RemoveAll(x => x.story);
    }

    public void StoryFadeOut(float s)
    {
        foreach (var l in loops.Values) if (l.story) { l.target = 0f; l.rate = 1f / Mathf.Max(0.05f, s); }
    }

    /// <summary>演出が鳴らした単発を全部止める（打ち上げの点火の直前の「溜め」）</summary>
    public void StoryHush()
    {
        foreach (var s in shots) if (s.story) { s.src.Stop(); s.until = 0f; }
    }

    public void StoryPlay(string clip, float vol, float pitch) => Play(clip, vol, pitch, true);
    public void StoryLoop(string key, string clip, float vol, float pitch) => SetLoop("story:" + key, clip, vol, pitch, 0f, true);
    public void StoryMusic(string slot, float fadeS) { storyMusic = Slot(slot); storyMusicFade = fadeS; }
}
