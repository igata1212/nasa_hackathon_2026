// 書いた人：Claude ／ 最終更新：2026-11-15 ／ 更新：iizuka（Opus 5.5、2026-11-14、#75 吹き出しにした・ブリーフィング・声の口）／ 更新：iizuka（Opus 5.5、2026-11-14、#146 載せ先と flow を渡せる）／ 更新：iizuka（Opus 5.5、2026-11-15、#190 数字を accent に・Skip をヒントのピルに）
// ヒューストン（地上の管制室の先生）の台詞の吹き出し。いつ・何を・どこに出すかを決める（見た目は Houston/HoustonBubble.cs、
// 文言・置き場所・時間は Houston/HoustonStyle.cs、台詞の下ごしらえは Houston/HoustonScript.cs、声は Houston/HoustonVoice.cs）。
//   ・台詞は1本ずつ。新しい台詞が来たら古いものを捨てる。表示中のほうが優先度が高い（milestone ＞ critical ＞ high）ときだけ残す（#127）
//   ・長い台詞は文の切れ目でページに分け、時間で送る。吹き出しを押すと次のページへ（最後のページなら閉じる）。「(pause)」は出さない
//   ・名前タグの横に「Mission Control on Earth」（地球の管制室だと一目で分かるように）。最初の1回だけ「…: they guide you by radio」
//   ・通信途絶中は NO SIGNAL（月の裏側なら「月が電波をさえぎっている」）、通信 OFF なら COMMS OFF（■ Comms & Power の席に ON を頼む）
//   ・声なしの警告（event kind=warning）は吹き出しの上の赤い帯に文字だけ
//   ・ブリーフィング（PlayBriefing）：台詞の列を1つずつ出し、押すと次へ。画面の右下に Skip（ヒントのピル）。その間に来たサーバーの台詞（M01 など）は待たせ、終わってから出す
//   ・置き場所は段階で変える（船内は下の真ん中、設計は左下、エンディングは右の列（SCORE に重ねない）、レポートは右下）
// GameFlow から見える形は前と同じ：コンストラクタ・Root・Dock・Warn・Push・Clear・SetLink・Tick。足したもの：Push(id,…)・PlayBriefing・SkipBriefing・Next・読み取り
// #146：声なしの警告（Warn）は GameFlow から呼ばない（危機の帯 CrisisBanner に寄せた。帯は1本）。Warn は残してある
using System;
using System.Collections.Generic;
using UnityEngine;
using S = HoustonStyle;

public enum HoustonLink { Ok, NoSignal, CommsOff }

public class HoustonBox
{
    public readonly GameObject Root;
    /// <summary>最後に作った吹き出し（GameFlow の外から台詞やブリーフィングを流すとき・確認用。例：HoustonBox.Current.PlayBriefing((TextAsset)null, null)）</summary>
    public static HoustonBox Current { get; private set; }
    /// <summary>声の口（既定は Resources/Voice/Houston/&lt;id&gt; を鳴らす。ファイルが無ければ字幕だけ）。HoustonVoice.cs</summary>
    public IHoustonVoice Voice;

    readonly HoustonBubble view;
    readonly GameObject scrim;
    readonly GameFlow flow;
    HoustonWindow window; bool windowSearched, talking;

    // 今の台詞
    HoustonLine cur;
    int curRank;
    List<string> pages = new List<string>();
    int page;
    float pageUntil, voiceLen;
    // ブリーフィング
    List<HoustonLine> briefing;
    int bIndex;
    Action bDone;
    HoustonLine pending; int pendingRank;   // ブリーフィングの間に来た台詞（いちばん優先度の高いもの1本）
    // 通信・警告・置き場所
    bool noSignal, commsOff, designDock;
    float warnUntil;
    HoustonPlace place;
    static bool introShown;   // 最初の1回の説明（アプリを起動してから1回）
    float introUntil;
    bool dirty = true;

    /// <param name="parent">載せ先。画面の Canvas でも、3D の中の world-space の Canvas でもよい（#167。置き場所 HoustonStyle の割合は parent の大きさに対するもの）</param>
    /// <param name="flow">段階・状態を読む先。省くと parent の親から探す（world-space に載せるときは渡す）</param>
    public HoustonBox(Transform parent, GameFlow flow = null)
    {
        this.flow = flow != null ? flow : parent.GetComponentInParent<GameFlow>();
        var sc = Ui.PanelImg(parent, S.BriefingScrimColor, "HoustonScrim"); UiKit.Fill(sc.rectTransform);
        var sb = sc.gameObject.AddComponent<UnityEngine.UI.Button>();
        sb.transition = UnityEngine.UI.Selectable.Transition.None;
        sb.onClick.AddListener(Next);
        scrim = sc.gameObject; scrim.SetActive(false);

        view = new HoustonBubble(parent);
        Root = view.Root.gameObject;
        view.OnTap += Next;
        view.OnSkip += SkipBriefing;
        Voice = new HoustonClipVoice(parent.gameObject);
        place = S.Cabin; view.Place(place);
        Current = this;
    }

    /// <summary>#167：吹き出し（と暗幕・先生の小窓）を別の載せ先へ移す（3D の中のモニターの world-space の Canvas など）。
    /// 置き場所（HoustonStyle の割合）は新しい載せ先の大きさに対して当てる</summary>
    public void MoveTo(Transform parent)
    {
        scrim.transform.SetParent(parent, false);
        Root.transform.SetParent(parent, false);
        view.Place(place);
        dirty = true;
    }

    // ================================================================== 読み取り（HoustonWindow などが使う）
    /// <summary>吹き出しが出ているか</summary>
    public bool Showing => Root.activeSelf;
    /// <summary>今出している文（色の印なし）。通信できないときは "NO SIGNAL" / "COMMS OFF"。何も無ければ空</summary>
    public string CurrentText => view.ProbeText;
    public string CurrentId => cur != null ? cur.id : "";
    public HoustonLink Link => noSignal ? HoustonLink.NoSignal : commsOff ? HoustonLink.CommsOff : HoustonLink.Ok;
    public bool BriefingPlaying => briefing != null;
    /// <summary>声が鳴っているか（口を動かす目安）</summary>
    public bool Speaking => Voice != null && Voice.Playing;

    // ================================================================== 置き場所
    /// <summary>設計の画面では左下に寄せる（GameFlow が段階の切り替えで呼ぶ）。ほかの段階の置き場所は GameFlow の段階から決める</summary>
    public void Dock(bool designScreen) { designDock = designScreen; UpdatePlace(); }

    string Phase => flow != null && flow.Model != null ? flow.Model.Phase : (designDock ? "design" : "");

    void UpdatePlace()
    {
        var want = briefing != null ? S.Briefing : designDock ? S.Design : S.PlaceFor(Phase);
        if (briefing == null && want.name == S.Cabin.name && SeatView.FirstPerson) want = S.CabinFirst;   // #210：一人称は低く・2 行
        if (want.name == place.name) return;
        place = want;
        view.Place(place);
        if (cur != null)
        {
            pages = HoustonScript.Pages(cur.text, place.pageChars);   // 置き場所で1ページの文字数が変わる
            page = Mathf.Clamp(page, 0, Mathf.Max(0, pages.Count - 1));
        }
        dirty = true;
    }

    // ================================================================== 台詞
    /// <summary>台詞を出す（GameFlow：case "houston"）。id が無いと声を引けない</summary>
    public void Push(string text, string priority = null) => Push("", text, priority);

    public void Push(string id, string text, string priority)
    {
        if (string.IsNullOrEmpty(text)) return;
        var line = new HoustonLine(id, text, priority);
        int r = S.Rank(priority);
        if (briefing != null)
        {
            if (pending == null || r >= pendingRank) { pending = line; pendingRank = r; }   // ブリーフィングのあとに出す
            return;
        }
        if (cur != null && curRank > r) return;
        Begin(line, r);
    }

    void Begin(HoustonLine line, int rank)
    {
        float now = Time.unscaledTime;
        cur = line; curRank = rank;
        pages = HoustonScript.Pages(line.text, place.pageChars);
        page = 0;
        if (pages.Count == 0) { End(); return; }
        voiceLen = 0f;
        Voice?.Stop();
        if (briefing != null || Link == HoustonLink.Ok) voiceLen = Voice != null ? Voice.Play(line.id, HoustonScript.Clean(line.text)) : 0f;
        if (!introShown && S.IsHouston(line.speaker)) { introShown = true; introUntil = now + S.RoleFirstS; }
        StartPage(now);
    }

    void StartPage(float now)
    {
        if (briefing != null) { pageUntil = float.PositiveInfinity; dirty = true; return; }   // ブリーフィングは押すまで待つ
        string p = pages[page];
        float sec = HoustonScript.ReadSeconds(p);
        if (voiceLen > 0f)
        {
            int total = 0; foreach (var x in pages) total += x.Length;
            sec = voiceLen * p.Length / Mathf.Max(1, total);
            if (page == pages.Count - 1) sec += S.VoiceTailS;
        }
        pageUntil = now + sec;
        dirty = true;
    }

    void End()
    {
        cur = null; pages.Clear(); page = 0; voiceLen = 0f;
        Voice?.Stop();
        dirty = true;
    }

    /// <summary>次のページへ（吹き出し・暗幕を押したとき）。最後のページなら閉じる。ブリーフィングなら次の台詞へ</summary>
    public void Next()
    {
        if (cur == null) return;
        if (voiceLen > 0f) { Voice?.Stop(); voiceLen = 0f; }   // 自分で読み進めたら、声は止めて文字だけにする
        if (page + 1 < pages.Count) { page++; StartPage(Time.unscaledTime); return; }
        if (briefing != null) NextBriefingLine();
        else End();
    }

    /// <summary>声なしの警告（パネルの文字だけ）。数秒で消える</summary>
    public void Warn(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        view.ShowWarning(HoustonScript.Clean(text));
        warnUntil = Time.unscaledTime + S.WarnS;
        dirty = true;
    }

    /// <summary>台詞・通信の状態・警告を消す（ブリーフィングは止めない。止めるのは SkipBriefing）</summary>
    public void Clear()
    {
        if (briefing == null) { End(); pending = null; }   // ブリーフィング中は、出しているブリーフィングの台詞を消さない
        noSignal = commsOff = false;
        warnUntil = 0f; view.ShowWarning(null);
        dirty = true;
        Draw();
    }

    public void SetLink(bool signalLost, bool commsSwitchOff)
    {
        if (signalLost == noSignal && commsSwitchOff == commsOff) return;
        noSignal = signalLost; commsOff = commsSwitchOff;
        if ((noSignal || commsOff) && briefing == null) { Voice?.Stop(); voiceLen = 0f; }   // 通信できない間は声を出さない（字幕も隠す）
        dirty = true;
        Draw();
    }

    // ================================================================== ブリーフィング（#152 の前提知識）
    /// <summary>台詞の列を1つずつ出す。押すと次、Skip で終わり。終わったら（飛ばしても）onDone。台詞が無ければ何も出さずにすぐ onDone</summary>
    public void PlayBriefing(IList<HoustonLine> lines, Action onDone)
    {
        if (briefing != null) FinishBriefing();
        var list = new List<HoustonLine>();
        if (lines != null) foreach (var l in lines) if (l != null && !string.IsNullOrEmpty(HoustonScript.Clean(l.text))) list.Add(l);
        if (list.Count == 0) { onDone?.Invoke(); return; }
        if (cur != null) { pending = cur; pendingRank = curRank; End(); }   // 出ていた台詞は、ブリーフィングのあとに出し直す
        briefing = list; bIndex = 0; bDone = onDone;
        UpdatePlace();
        Begin(briefing[0], int.MaxValue);
        Draw();
    }

    /// <summary>JSON（TextAsset）から読んで流す。Editor では null でも Assets/Data/Story/briefing.json を読む。流したら true</summary>
    public bool PlayBriefing(TextAsset json, Action onDone)
    {
        var lines = HoustonScript.LoadBriefing(json);
        PlayBriefing(lines, onDone);
        return lines.Count > 0;
    }

    public void SkipBriefing() { if (briefing != null) FinishBriefing(); }

    void NextBriefingLine()
    {
        bIndex++;
        if (bIndex >= briefing.Count) { FinishBriefing(); return; }
        Begin(briefing[bIndex], int.MaxValue);
    }

    void FinishBriefing()
    {
        var done = bDone;
        briefing = null; bDone = null;
        End();
        UpdatePlace();
        if (pending != null) { var p = pending; int r = pendingRank; pending = null; Begin(p, r); }
        Draw();
        done?.Invoke();
    }

    // ================================================================== 毎フレーム
    public void Tick(float now)
    {
        UpdatePlace();
        if (view.WarningOn && now >= warnUntil) { view.ShowWarning(null); dirty = true; }
        if (cur != null && briefing == null && now >= pageUntil)
        {
            if (page + 1 < pages.Count) { page++; StartPage(now); }
            else End();
        }
        if (introUntil > 0f && now >= introUntil) { introUntil = 0f; dirty = true; }
        if (briefing != null) KeepScrimBelow();
        UpdateTalk();
        if (dirty) Draw();
        view.Animate(now);
    }

    void Draw()
    {
        dirty = false;
        bool brief = briefing != null && cur != null;
        bool hushed = !brief && noSignal && !commsOff && SeatView.FirstPerson;   // #210：一人称の電波なしは吹き出しを出さない（盤の先生の画面に印）
        bool lost = !brief && (noSignal || commsOff) && !hushed;
        bool line = brief || (!lost && !hushed && cur != null && pages.Count > 0);
        bool warnOn = view.WarningOn;

        if (scrim.activeSelf != (brief && S.BriefingScrim)) scrim.SetActive(brief && S.BriefingScrim);
        view.ShowSkip(brief);
        if (line)
        {
            string p = pages[Mathf.Clamp(page, 0, pages.Count - 1)];
            bool more = page + 1 < pages.Count || brief;
            string foot = brief ? (bIndex + 1) + " / " + briefing.Count
                                : pages.Count > 1 ? (page + 1) + " / " + pages.Count : "";
            view.ShowLine(S.SpeakerName(cur.speaker), S.IsHouston(cur.speaker), S.TagColor(cur.priority), introUntil > 0f && !brief,
                          HoustonScript.Mark(p, S.Highlight, S.NumberColor), p, S.SeatsIn(cur.text), more, foot);
        }
        else if (lost) view.ShowLink(noSignal, noSignal ? (FarSide ? S.NoSignalFarSide : S.NoSignalOther) : S.CommsOffHint);
        else view.ShowNothing();
        view.SetVisible(line || lost || warnOn, line || lost);
        if (brief) KeepScrimBelow();
    }

    bool FarSide => flow != null && flow.Model != null && flow.Model.State != null && flow.Model.State.inFarside;

    /// <summary>暗幕を吹き出しのすぐ下に（GameFlow が段階の切り替えで吹き出しを一番上へ移すので、毎フレーム合わせる）</summary>
    void KeepScrimBelow()
    {
        var st = scrim.transform; var rt = Root.transform;
        int ri = rt.GetSiblingIndex(), si = st.GetSiblingIndex();
        if (si == ri - 1) return;
        st.SetSiblingIndex(si < ri ? ri - 1 : ri);
    }

    /// <summary>声が鳴っている間は管制室の小窓の口を動かす（声が無いときは、小窓が字幕の文字から自分で動かす）</summary>
    void UpdateTalk()
    {
        bool on = Speaking;
        if (on == talking) return;
        talking = on;
        if (!windowSearched) { windowSearched = true; window = UnityEngine.Object.FindFirstObjectByType<HoustonWindow>(); }
        if (window == null) return;
        if (on) window.Talk(true); else window.ReleaseTalk();
    }
}
