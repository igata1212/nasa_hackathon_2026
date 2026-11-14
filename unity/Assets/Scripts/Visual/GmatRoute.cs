// 書いた人：Claude（#65）／ 最終更新：2026-11-14
// GMAT の帰還ルート（A または B）1本ぶん。data/gmat/route_*.csv と time_map.json から
// メニュー Tools/GMAT/軌道を取り込む（Editor/GmatImport.cs）が作る。手で書き換えない。
using System;
using UnityEngine;

[Serializable]
public struct GmatKnot
{
    public float t;        // ゲームの時間（事故からの秒）
    public float h;        // GMAT の経過時間（時間。CSV の最初の行から）
    public string label;   // accident / los / aos / burn / reentry
}

public class GmatRoute : ScriptableObject
{
    public string routeId;            // "a" / "b"
    public double originMjd;          // 最初の行の UTCModJulian（日）
    public float[] hours;             // 行ごとの経過時間（時間）。単調増加。行の間隔は一定でない
    public Vector3[] posKm;           // EarthMoonRot の (X, Y, Z)（km）。地球が原点、月は +X の約 404,000 km にほぼ止まる
    public GmatKnot[] knots;          // ゲームの t と GMAT の時間の対応（time_map.json）
    public float moonCenterXKm;       // 月の中心（EarthMoonRot の X、km）

    public float EndT => knots[knots.Length - 1].t;

    /// <summary>ゲームの t（秒）→ GMAT の経過時間（時間）。区切りごとに線形、範囲外は端に止める</summary>
    public float HoursAt(float t)
    {
        if (t <= knots[0].t) return knots[0].h;
        for (int i = 1; i < knots.Length; i++)
            if (t <= knots[i].t)
                return Mathf.Lerp(knots[i - 1].h, knots[i].h, Mathf.InverseLerp(knots[i - 1].t, knots[i].t, t));
        return knots[knots.Length - 1].h;
    }

    /// <summary>経過時間（時間）に最も近い手前の行の番号（0〜hours.Length-2）</summary>
    public int IndexAt(float h)
    {
        int lo = 0, hi = hours.Length - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (hours[mid] <= h) lo = mid; else hi = mid;
        }
        return lo;
    }

    /// <summary>経過時間（時間）の位置（km）。行の間は線形に補間する</summary>
    public Vector3 PosAtHours(float h)
    {
        h = Mathf.Clamp(h, hours[0], hours[hours.Length - 1]);
        int i = IndexAt(h);
        return Vector3.Lerp(posKm[i], posKm[i + 1], Mathf.InverseLerp(hours[i], hours[i + 1], h));
    }
}
