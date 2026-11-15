// 書いた人：Taiga（作成：Fable 5.2）／ 最終更新：2026-11-15
// 席ごとの計器モニター（#167。Fx_SeatMonitor_<席>.fbx）を Game.unity の船室に置く。シーンは手で直さず、ここを直して置き直す。
//   メニュー Tools/SeatMonitor/計器モニターを置く … Game.unity を開いて、4席のモニターを置き直して保存する
//   GameFlowScene.BuildScene（Tools/GameFlow/シーンを作り直す）からは BuildInto() を呼ぶ
// 置き方：船室の Slot_Console_<席>（コンソールと同じ Slot）の子に、位置0・回転0。画面をずらす量はモデルに入っている。
// 画面（MonScreen）は ScreenGlow が光らせる。状態ランプ（Lamp_Green／Yellow／Red）は発光マテリアルに差し替えるだけ
// （LampGlow の部品は付けない：付けると UrgencyFx が警報ランプとして点滅させる。状態で点け分けるのは、数値を船内に移すときに作る）。
// あわせて「SeatView」（Scripts/Visual/SeatView.cs）を置いて、4席のモニター・GameFlow・HoustonWindow・UrgencyFx・メインカメラをつなぐ。
// 数値の表示の載せ先は各モニターの Slot_Screen（SeatView が実行時に world-space の Canvas を作る）。
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SeatMonitorScene
{
    const string Tag = "[SeatMonitor] ";
    const string ModelFormat = "Assets/Models/Fx/Fx_SeatMonitor_{0}.fbx";
    static readonly string[] SeatNames = { "Pilot", "LifeSupport", "CommsPower", "MissionPlanner" };
    static readonly string[] SeatIds = { Seats.Pilot, Seats.Life, Seats.Comms, Seats.Planner };
    // 肩ごしのカメラを置く側（モニターのローカル X の符号）。まん中の2席は画面をずらした側と同じ（頭をはさまない）
    static readonly float[] CameraSide = { 1f, 1f, -1f, -1f };

    [MenuItem("Tools/SeatMonitor/計器モニターを置く")]
    public static void BuildMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)   // Play 中はシーンを保存できない（黙って失敗しないように知らせる）
        {
            EditorUtility.DisplayDialog("Seat Monitor", "Play を止めてから、もう一度押してください。\n（Play 中はシーンを保存できません）", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    /// <summary>確認なしで Game.unity に置いて保存する（UnityMCP から呼ぶとき用）</summary>
    public static void Build()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != GameFlowScene.ScenePath) scene = EditorSceneManager.OpenScene(GameFlowScene.ScenePath, OpenSceneMode.Single);
        int n = BuildInto();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(Tag + GameFlowScene.ScenePath + " に計器モニターを " + n + " 台置いた");
    }

    /// <summary>今のシーンに4席のモニターを置き直す（保存はしない）。置いた数を返す</summary>
    public static int BuildInto()
    {
        Material red = LookDevAssets.LampMaterial("Red"), yellow = LookDevAssets.LampMaterial("Yellow"), green = LookDevAssets.LampMaterial("Green");
        if (red == null || yellow == null || green == null) Debug.LogWarning(Tag + "ランプの発光マテリアルが無い（Tools/LookDev/素材を作り直す）");
        int n = 0;
        var refs = new System.Collections.Generic.List<SeatView.SeatRef>();
        for (int si = 0; si < SeatNames.Length; si++)
        {
            string seat = SeatNames[si];
            var slot = FindSlot("Slot_Console_" + seat);
            if (slot == null) { Debug.LogError(Tag + "Slot_Console_" + seat + " が無い（船室 Ship_Interior がシーンに無い）"); continue; }
            string name = "Fx_SeatMonitor_" + seat;
            for (int i = slot.childCount - 1; i >= 0; i--)          // 前に置いた物を消す
                if (slot.GetChild(i).name == name) Object.DestroyImmediate(slot.GetChild(i).gameObject);

            string path = string.Format(ModelFormat, seat);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) { Debug.LogError(Tag + path + " が無い"); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, slot);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.AddComponent<ScreenGlow>();
            if (red != null && yellow != null && green != null) LampGlow.Apply(go, red, yellow, green);
            n++;

            Transform screen = null;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t.name.EndsWith(".Slot_Screen") || t.name == "Slot_Screen") { screen = t; break; }
            if (screen == null) Debug.LogError(Tag + name + " に Slot_Screen が無い");
            refs.Add(new SeatView.SeatRef { seat = SeatIds[si], monitor = go.transform, screen = screen, side = CameraSide[si] });
        }

        // SeatView（#167：ゲージをモニターへ・ヒューストンの顔・肩ごしのカメラ・強い揺れ）。値は作り直しても残るように、前の物から写す
        var oldGo = GameObject.Find("SeatView");
        var old = oldGo != null ? oldGo.GetComponent<SeatView>() : null;
        var viewGo = new GameObject("SeatView");
        var view = viewGo.AddComponent<SeatView>();
        if (old != null) EditorUtility.CopySerialized(old, view);
        if (oldGo != null) Object.DestroyImmediate(oldGo);
        view.flow = Object.FindAnyObjectByType<GameFlow>();
        view.houston = Object.FindAnyObjectByType<HoustonWindow>();
        view.urgency = Object.FindAnyObjectByType<UrgencyFx>();
        var camGo = GameObject.FindGameObjectWithTag("MainCamera");
        view.mainCamera = camGo != null ? camGo.GetComponent<Camera>() : null;
        view.seats = refs.ToArray();
        var parts = new System.Collections.Generic.List<GameObject>();      // 肩ごしの間に出して船室を閉じる部品
        foreach (var part in new[] { "Wall_Front", "Roof_Front", "Roof_Back" })
        {
            var t = FindSlot(part);
            if (t != null) parts.Add(t.gameObject); else Debug.LogWarning(Tag + part + " が無い（船室は閉じない）");
        }
        view.enclosure = parts.ToArray();
        if (view.flow == null) Debug.LogWarning(Tag + "GameFlow が無い（SeatView は動かない）");
        if (view.houston == null) Debug.LogWarning(Tag + "HoustonWindow が無い（モニターに顔は出ない）");
        if (view.urgency == null) Debug.LogWarning(Tag + "UrgencyFx が無い（揺れは変わらない）");
        return n;
    }

    // 船室の Slot（名前が一致、または「ルート.名前」。LookDevScene の Slot と同じ探し方）
    static Transform FindSlot(string name)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == name || t.name.EndsWith("." + name)) return t;
        return null;
    }
}
