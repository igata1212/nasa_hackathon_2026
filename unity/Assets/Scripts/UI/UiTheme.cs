// 書いた人：iizuka（作成：Claude）／ 最終更新：2026-11-14
// UI の書体とスプライトの参照（#72）。実体は Assets/Resources/UiTheme.asset（Tools/UI/素材を設定し直す で作る）。
// 画面はコードで組むので、シーンに参照を置かず Resources から読む（UiTokens.Theme）。GameFlow を変えずに済む。
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UiTheme : ScriptableObject
{
    [Header("書体（TextMeshPro の Font Asset）")]
    public TMP_FontAsset heading;   // Baloo 2 ExtraBold：見出し・ボタン・数字
    public TMP_FontAsset body;      // Nunito Bold：本文・補助ラベル・入力欄
    public TMP_FontAsset shapes;    // Zen Maru Gothic Bold：席の形 ▲●■◆（Baloo に ● が無いので4つともこれでそろえる）。日本語の Fallback も兼ねる
    public TMP_FontAsset strong;    // Nunito ExtraBold：見出し・ボタン・名前・札・選択肢（#190。UiTokens.FontStrong）

    [Header("白い形（Art/UI/Sprites。色は Image の color）")]
    public Sprite round16, round28, round40, pill, caretDown, stripes;

    [Header("#190 あつ森に寄せた形（art/ui/make_sprites.py。UiTokens.Art が欄の名前で引く：blob_panel → blobPanel）")]
    public Sprite blobBubble, blobPanel, blobPill, blobDisc, waveEdge, caretSoft, bands64, glovePoint;

    [Header("アイコン（Art/UI/Icons。名前はファイル名＝概念名）")]
    public Sprite[] icons;

    [Header("部屋の画面の 3D（LobbyStage）")]
    public GameObject[] crew;       // Models/Crew の4人。並びは Seats.All（pilot・life_support・comms_power・mission_planner）
    public GameObject earth;        // Models/Bodies/Body_Earth（後ろに大きく置く）
    public Material pad;            // 足元の光る台（URP Lit・Emission。色は MaterialPropertyBlock で席ごとに変える）

    Dictionary<string, Sprite> byName;

    public Sprite Icon(string name)
    {
        if (byName == null)
        {
            byName = new Dictionary<string, Sprite>();
            if (icons != null) foreach (var s in icons) if (s != null) byName[s.name] = s;
        }
        if (byName.TryGetValue(name, out var sp)) return sp;
        Debug.LogWarning("[UiTheme] アイコンが無い: " + name);
        return null;
    }
}
