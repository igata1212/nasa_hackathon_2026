// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// ロゴの文字を1文字ずつ動かす（#153）：画面が開くと、左から順に下から弾んで出て（EaseOutBack）、
// その後 TitleLook.LetterWaveEvery 秒ごとに、文字の上を波が通る。TextMeshPro の頂点を毎フレーム書き換える（文字そのものは変えない）
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class UiLetterWave : MonoBehaviour
{
    TMP_Text t;
    float start;

    void OnEnable() { t = GetComponent<TMP_Text>(); start = UiEase.Now; }

    void LateUpdate()
    {
        if (t == null) return;
        float now = UiEase.Now - start;
        t.ForceMeshUpdate();
        var ti = t.textInfo;
        int n = ti.characterCount;
        float introEnd = TitleLook.LetterDelay + n * TitleLook.LetterStagger + TitleLook.LetterDur;
        float wave = now - introEnd;

        for (int i = 0; i < n; i++)
        {
            var ch = ti.characterInfo[i];
            if (!ch.isVisible) continue;
            var v = ti.meshInfo[ch.materialReferenceIndex].vertices;
            int k = ch.vertexIndex;

            // 出てくる：下の中央を中心に 0 → 1（行き過ぎて戻る）、少し下から上がる
            float p = (now - TitleLook.LetterDelay - i * TitleLook.LetterStagger) / TitleLook.LetterDur;
            float s = p <= 0f ? 0f : UiEase.OutBack(p);
            float dy = -(1f - UiEase.OutCubic(p)) * TitleLook.LetterDrop;
            // 波
            if (wave > 0f)
            {
                float w = Mathf.Repeat(wave, TitleLook.LetterWaveEvery) - i * TitleLook.LetterWaveStagger;
                dy += TitleLook.LetterWaveAmp * UiEase.Bump(w / TitleLook.LetterWaveDur);
            }

            var pivot = new Vector3((v[k].x + v[k + 2].x) * 0.5f, v[k].y, 0f);
            for (int j = 0; j < 4; j++) v[k + j] = pivot + (v[k + j] - pivot) * s + Vector3.up * dy;
        }
        t.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }
}
