// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-15
// 設計の画面を 3D にした舞台（#168。DesignStage）の値：空の色・カメラの2つの構え・台の回り方・部品の大きさ・動きの秒・取り付け位置の表。
// 舞台の見た目を直すときはここだけ触る（色は UiTokens の色から作る。色コードは Sky だけ。3D は m・度・秒、2D は Canvas の基準 1600×900 の px）。
// 角度は Blender の度（機械船の真上から見て反時計回り。0°＝+X、−90°＝正面 −Y）。高さ z は Blender の m（Ship_Stack の原点＝ノズルの出口から）。
// Blender → Unity の向きは modeling/core/README.md 4章（位置 (x, y, z) → (−x, z, −y)、正面 −Y → +Z）
using UnityEngine;
using T = UiTokens;

/// <summary>部品の向き。部品の上（Blender の +Z）と正面（Blender の −Y）を、取り付け位置の何に向けるか</summary>
public enum MountAxis { Out, In, Up, Down, Cw, Ccw }

/// <summary>取り付け位置の表の1行：部品 id の index 個目（0 から）をどこに・どの向きで付けるか</summary>
public readonly struct DesignMount
{
    public readonly string Id;
    public readonly int Index;
    public readonly float Angle, Z;          // Blender の度・m
    public readonly MountAxis Up, Front;     // 部品の上・正面をどこへ向けるか
    public readonly float Tilt;              // 取り付けたあと、横（接線）の軸まわりに外へ倒す度（アンテナを外下へ向ける）
    public readonly float Scale;             // 実寸に対する倍率（おもちゃの見え方）

    public DesignMount(string id, int index, float angle, float z, MountAxis up, MountAxis front, float scale, float tilt = 0f)
    { Id = id; Index = index; Angle = angle; Z = z; Up = up; Front = front; Scale = scale; Tilt = tilt; }
}

public static class DesignLook
{
    // ================================================================== 空・地面・台
    // 背景の明暗はこの1つで替える（#146 の判断に合わせる。既定は昼の空＝打ち上げの演出と同じ空）
    public static readonly Color Sky = T.Hex("9DCBEA");
    public static readonly Color Ground = T.Bg;        // 丸い地面（cream）
    public static readonly Color Table = T.Line;       // 回る台（gray_light）
    public static readonly Color Rim = T.Go;           // 台のふちの輪（teal）。重すぎのときは RimOver
    public static readonly Color RimOver = T.Danger;
    public static readonly Vector3 Origin = new Vector3(600f, 3000f, 0f);   // 船室・軌道の地図・部屋の舞台（UiTokens.StageOrigin）から離す
    public const float GroundR = 220f, GroundH = 0.1f;   // 地平線が遠くに見える広さ
    public const float TableR = 3.2f, TableH = 0.5f;   // 台の上面が y=0（船のノズルの出口がここに載る）
    public const float RimR = 3.55f, RimH = 0.36f, RimTop = -0.12f;   // 台の下に敷く少し大きい円柱。ふちが輪に見える
    public const float GroundY = -TableH;              // 地面の上面（台は地面に置く）
    public const float Far = 500f;

    // ================================================================== 明かり
    // 船室の明かり（Key Light。影つき）が舞台にも届く。その光が手前右上から当たるよう、舞台ごと StageYaw だけ回して置く。
    // 影の側が暗くなりすぎないよう、前左から弱い明かりを足す（舞台が出ている間だけ）
    public const float StageYaw = 180f;
    public static readonly Vector3 FillEuler = new Vector3(25f, 210f, 0f);  // 舞台のローカル。カメラ（+Z）の左上から
    public const float FillIntensity = 0.5f;
    public static readonly Color FillColor = T.Panel;

    // ================================================================== カメラ（2つの構えを ZoomSec で行き来する）
    public const float Fov = 30f;
    // 〈寄り〉部品のタブ：機械船の真ん中（Blender z 5.1）を、画面の左寄り（ShipScreenX px）・高さ ScreenY に
    public const float CloseDist = 24f, ClosePitch = 5f, CloseLookY = 5.4f, CloseScreenY = 0.5f;   // 浅く見下ろす（船の後ろが空になり、白い船が地面に埋もれない）
    // 〈引き〉FLIGHT：後ろ左に立つロケットの全身が入る
    public const float WideDist = 66f, WidePitch = 6f, WideLookY = 13f, WideScreenY = 0.5f;
    public const float WideLookX = 5f;                 // 引きのとき、船とロケットの間を見る（台の中心から画面の左へ m）
    public const float ShipScreenX = 400f;             // 船の中心の画面の位置（左から px。基準 1600。右の列は右に寄っている）
    public const float ZoomSec = 0.5f;

    // ================================================================== 台の回り方
    public const float IdleSpin = 6f;                  // 何もしないとき（度/秒）
    public const float DragDegPerPx = 0.35f;           // 舞台をなぞって回す（横だけ）
    public const float ResumeSec = 3f;                 // 離して（付いて）から自動に戻るまで
    public const float FaceSec = 0.4f;                 // 部品が付くとき、その位置がカメラの正面に来るまで
    public const float FaceAngleBias = 10f;            // カメラの真正面からこれだけずらして見せる（真正面だと出っ張りが分からない）
    public const float StartYaw = -55f;                // 開いたときの台の向き（Blender の度。この向きをカメラへ）

    // ================================================================== 付く・外れる・入れ替わる
    public const float AttachSec = 0.35f;              // 棚（画面の右）から弧を描いて飛ぶ
    public const float AttachArc = 2.2f;               // 弧の高さ（m）
    public const float AttachOvershoot = 1.15f;        // 大きさ 0 → 1.15 → 1
    public static readonly Vector2 AttachFrom = new Vector2(0.78f, 0.62f);   // 飛び始め（画面の割合。右の棚のあたり）
    public const float SquashScale = 0.97f, SquashSec = 0.18f;               // 付いた瞬間、船が一瞬つぶれる
    public const float DetachSec = 0.3f, DetachHop = 1.4f, DetachSpin = 540f, DetachOut = 1.2f;   // 浮いて回りながら落ちて消える
    public const float ShakeSec = 0.5f, ShakeAmp = 0.22f, ShakeHz = 9f;      // NO GO のとき船がぶるっと揺れる

    // ================================================================== ロケット（後ろ左に立つ。実寸の RocketScale 倍）
    public const float RocketScale = 0.27f;            // Large が引きの絵で画面の高さの約 75%、Small が約 48%（計画の 0.2 では船より低く見えた）
    public static readonly Vector3 RocketPos = new Vector3(12f, 0f, -8f);     // 台の中心から（Unity。+X が画面の左、−Z が奥）。y は地面
    public const float RocketOutSec = 0.25f, RocketInSec = 0.5f, RocketInOvershoot = 1.05f;

    // ================================================================== 取り付け位置の表
    public const float HullR = 1.95f;                  // 機械船の半径（modeling/ship/build_ship.py の SM_R）
    public const float HullSink = 0.03f;               // 部品を外板に少し埋める（浮いて見えないように）
    // 外板4（−120°〜−60°）は空ける（事故で飛ぶ所）。小型噴射器は ±45°・±135° の z 6.3 にある
    public static readonly DesignMount[] Mounts =
    {
        // 電力：電池は下の段に立てる（稲妻が外を向く）。1個目は正面の右（開いた絵で見える）
        new DesignMount("battery", 0, -35f, 3.05f, MountAxis.Up, MountAxis.Out, 1.8f),
        new DesignMount("battery", 1, -145f, 3.05f, MountAxis.Up, MountAxis.Out, 1.8f),
        new DesignMount("battery", 2, 25f, 3.05f, MountAxis.Up, MountAxis.Out, 1.8f),
        new DesignMount("battery", 3, 155f, 3.05f, MountAxis.Up, MountAxis.Out, 1.8f),
        // ソーラーパネル：左右の翼（板の面が正面、長い辺が外へ）
        new DesignMount("solar_panel", 0, 0f, 5.1f, MountAxis.Cw, MountAxis.Up, 1.25f),
        new DesignMount("solar_panel", 1, 180f, 5.1f, MountAxis.Ccw, MountAxis.Up, 1.25f),
        // 酸素・燃料：立てる（船と平行）
        new DesignMount("spare_o2_tank", 0, 45f, 4.55f, MountAxis.Up, MountAxis.Out, 1.8f),
        new DesignMount("spare_o2_tank", 1, 135f, 4.55f, MountAxis.Up, MountAxis.Out, 1.8f),
        new DesignMount("extra_fuel_tank", 0, 62f, 3.05f, MountAxis.Up, MountAxis.Out, 1.6f),
        new DesignMount("extra_fuel_tank", 1, 118f, 3.05f, MountAxis.Up, MountAxis.Out, 1.6f),
        // 観測機器：上の段に外向き（レンズ・プリズムが外）
        new DesignMount("camera", 0, -15f, 6.0f, MountAxis.Out, MountAxis.Up, 2.0f),
        new DesignMount("spectrometer", 0, -165f, 6.0f, MountAxis.Out, MountAxis.Up, 2.0f),
        new DesignMount("radar", 0, 90f, 5.8f, MountAxis.Out, MountAxis.Cw, 1.6f),
        // 大きいアンテナ：後ろの下（アポロと同じ所）。外下へ 45° 倒す
        new DesignMount("high_gain_antenna", 0, 90f, 3.2f, MountAxis.Out, MountAxis.Up, 1.8f, 45f),
    };

    public static bool FindMount(string id, int index, out DesignMount m)
    {
        foreach (var x in Mounts) if (x.Id == id && x.Index == index) { m = x; return true; }
        m = default;
        return false;
    }

    // ================================================================== 向きの計算（Blender の角度 → 機械船のローカル（Unity））
    /// <summary>外向き（Blender (cos a, sin a, 0) → Unity (−cos a, 0, −sin a)）</summary>
    public static Vector3 Out(float angle)
    {
        float r = angle * Mathf.Deg2Rad;
        return new Vector3(-Mathf.Cos(r), 0f, -Mathf.Sin(r));
    }

    public static Vector3 Axis(MountAxis a, float angle)
    {
        float r = angle * Mathf.Deg2Rad;
        switch (a)
        {
            case MountAxis.Out: return Out(angle);
            case MountAxis.In: return -Out(angle);
            case MountAxis.Up: return Vector3.up;
            case MountAxis.Down: return Vector3.down;
            case MountAxis.Ccw: return new Vector3(Mathf.Sin(r), 0f, -Mathf.Cos(r));   // Blender (−sin a, cos a, 0)
            default: return new Vector3(-Mathf.Sin(r), 0f, Mathf.Cos(r));               // Cw：Blender (sin a, −cos a, 0)
        }
    }

    /// <summary>部品の向き（機械船のローカル）。部品の上＝Up、正面＝Front。Tilt は接線の軸まわりに外へ倒す</summary>
    public static Quaternion Rotation(DesignMount m)
    {
        var q = Quaternion.LookRotation(Axis(m.Front, m.Angle), Axis(m.Up, m.Angle));
        if (Mathf.Abs(m.Tilt) > 0.01f)
        {
            var side = Vector3.Cross(Vector3.up, Out(m.Angle));
            q = Quaternion.AngleAxis(m.Tilt, side) * q;
        }
        return q;
    }
}
