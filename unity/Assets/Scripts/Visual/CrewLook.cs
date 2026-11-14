// 書いた人：iizuka（作成：Haiku 5.5）／ 最終更新：2026-11-14
//
// クルーの顔を見せる（#55、LookDev の修正）。体と腕はコンソール向きのまま、頭（Helmet）だけを左右に振る。
// Normal：normalEuler の向き（部屋の中心側）。LookAtCamera：メインカメラの方へ左右だけ向く（parts.json の Helmet の軸は1つ＝Blender の Z、Unity では Y）。
// 首の回る範囲は lookLimit（既定 ±40＝parts.json の範囲、最大 70）。normalEuler の Y（±60）は clamp しない。
// Play のときは turnTime でなめらかに、エディタ（非 Play）では OnValidate・OnEnable で姿をすぐ決める。
// 目は SetEyes で1つだけ表示する（Renderer の有効・無効。OnValidate の中で SetActive を呼ぶと警告の元になるので避ける）。
// 名前は「<ルート>.<部品>」（例：Crew_Pilot.Helmet）なので、末尾が「.Helmet」の子孫を探す。
// CrewFloat と同じ物に付けても干渉しない（CrewFloat は体全体、ここは Helmet の局所回転だけを書く）。
using UnityEngine;

[ExecuteAlways]
public class CrewLook : MonoBehaviour
{
    public enum Mode { Normal, LookAtCamera }

    [Tooltip("Normal：normalEuler の向き。LookAtCamera：メインカメラの方へ左右を向く")] public Mode mode = Mode.Normal;
    [Tooltip("向きを変える時間の目安（秒、Play のとき）")] public float turnTime = 0.2f;
    [Tooltip("Normal のときの Helmet の向き（localEulerAngles、度）。部屋の中心側へ振る。左側の席は Y を −60 にする")] public Vector3 normalEuler = new Vector3(-15f, 60f, 0f);
    [Tooltip("LookAtCamera で首が左右に回る角度（度、片側）。parts.json の範囲は ±40、最大 70")] [Range(0f, 120f)] public float lookLimit = 40f;
    [Tooltip("今の目（Eyes_Normal・Eyes_Panic・Eyes_Relief のどれか。SetEyes で変える）")] [SerializeField] string eyes = "Eyes_Normal";

    const string HelmetPart = "Helmet";
    static readonly string[] EyeParts = { "Eyes_Normal", "Eyes_Panic", "Eyes_Relief" };

    Transform helmet;
    bool warnedNoHelmet;
    bool yawReady;       // Play の最初のフレームで、今の左右の向きを読んだか
    float yaw;           // 今の Helmet の左右の向き（度）
    float yawVelocity;   // SmoothDampAngle 用

    void OnEnable()
    {
        yawReady = false;
        ApplyEyes();
        ApplyNow();
    }

    void OnValidate()
    {
        ApplyEyes();
        ApplyNow();
    }

    void Update()
    {
        if (!Application.isPlaying) return;   // エディタでは OnValidate・OnEnable で決める
        Transform h = GetHelmet();
        if (h == null) return;
        if (!yawReady)
        {
            // Play の最初は、エディタで置いた向きから始める（そこから turnTime でなめらかに）
            yaw = CurrentYaw(h);
            yawVelocity = 0f;
            yawReady = true;
        }
        float target;
        if (TargetYaw(h, out target))
        {
            // smoothTime は turnTime の 0.3 倍。turnTime で残り約 1% まで寄る
            yaw = Mathf.SmoothDampAngle(yaw, target, ref yawVelocity, Mathf.Max(turnTime, 0.01f) * 0.3f);
        }
        SetHelmetYaw(h, yaw);
    }

    // 目を1つだけ表示する（name は Eyes_Normal・Eyes_Panic・Eyes_Relief のどれか）
    public void SetEyes(string name)
    {
        eyes = name;
        ApplyEyes();
    }

    // エディタ（非 Play）：向きをすぐ決める（なめらかにしない）。カメラが無いときは何もしない
    void ApplyNow()
    {
        if (Application.isPlaying) return;
        Transform h = GetHelmet();
        if (h == null) return;
        float target;
        if (!TargetYaw(h, out target)) return;
        yaw = target;
        SetHelmetYaw(h, yaw);
    }

    // 目標の左右の向き（度）。LookAtCamera でメインカメラが無いときは false（何もしない）
    bool TargetYaw(Transform h, out float target)
    {
        if (mode == Mode.Normal)
        {
            target = normalEuler.y;
            return true;
        }
        Camera cam = Camera.main;
        if (cam == null)
        {
            target = 0f;
            return false;
        }
        // 向きは親（クルーのルート）の座標で測る。CrewFloat で体が揺れていても、左右の角度がずれない
        Vector3 toCam = h.parent.InverseTransformDirection(cam.transform.position - h.position);
        // 正面は +Z（Blender の −Y。modeling/core/README.md の向きの表）。Y まわりの角度が、そのまま左右の向き
        float aim = Mathf.Atan2(toCam.x, toCam.z) * Mathf.Rad2Deg;
        float limit = Mathf.Clamp(lookLimit, 0f, 120f);
        target = Mathf.Clamp(aim, -limit, limit);
        return true;
    }

    // Helmet の向き。上下（X）と傾き（Z）は normalEuler のまま、左右（Y）だけ変える
    void SetHelmetYaw(Transform h, float y)
    {
        Quaternion q = Quaternion.Euler(normalEuler.x, y, normalEuler.z);
        if (h.localRotation != q) h.localRotation = q;   // 同じ向きなら書かない（シーンを汚さない）
    }

    // 今の左右の向き（度）。前方ベクトルから読む（Euler の読み直しで別の組み合わせになるのを避ける）
    static float CurrentYaw(Transform h)
    {
        Vector3 f = h.localRotation * Vector3.forward;
        return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
    }

    // Helmet を子孫から探して覚える
    Transform GetHelmet()
    {
        if (helmet == null)
        {
            helmet = FindPart(transform, HelmetPart);
            if (helmet == null && !warnedNoHelmet)
            {
                warnedNoHelmet = true;
                Debug.LogWarning(name + "：子孫に Helmet が無いので、頭は動かさない", this);
            }
        }
        return helmet;
    }

    // 目の表示を eyes に合わせる。Renderer の有効・無効で切り替える（SetActive は OnValidate から呼ぶと警告の元になるので使わない）
    void ApplyEyes()
    {
        if (System.Array.IndexOf(EyeParts, eyes) < 0)
        {
            Debug.LogWarning(name + "：目の名前が Eyes_Normal・Eyes_Panic・Eyes_Relief のどれでもない（" + eyes + "）。目はすべて隠す", this);
        }
        foreach (string part in EyeParts)
        {
            Transform eye = FindPart(transform, part);
            if (eye == null) continue;
            bool show = part == eyes;
            foreach (Renderer r in eye.GetComponentsInChildren<Renderer>(true))
            {
                if (r.enabled != show) r.enabled = show;
            }
        }
    }

    // 子孫（非アクティブも含む）から、名前が part と一致する物を探す。「<ルート>.<part>」の形も含む
    static Transform FindPart(Transform root, string part)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root) continue;
            if (t.name == part || t.name.EndsWith("." + part, System.StringComparison.Ordinal)) return t;
        }
        return null;
    }
}
