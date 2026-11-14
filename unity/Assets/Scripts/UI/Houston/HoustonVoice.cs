// 書いた人：iizuka（作成：Opus 5.5）／ 最終更新：2026-11-14
// ヒューストンの声を差し込む口（#75。声を入れるかは #150 で検討中。今は字幕だけで、声のファイルは無い）。
// HoustonBox は台詞を出すたびに Voice.Play(id, 文) を呼ぶ。鳴らせたら秒数を返し、吹き出しのページ送りをその長さに合わせる。
// 声が無い（0 を返す）ときは、これまでどおり文字数から読む時間を決める。
//
// 差し込み方（どれか1つ）：
//   1) 音声ファイルを Assets/Resources/Voice/Houston/<id>.wav（.ogg・.mp3 も可）に置く。id は M05・H07・B01 など。これだけで鳴る
//   2) 別の置き場所から引くなら HoustonClipVoice.Lookup = id => ... を差し替える（AudioClip を返す。無ければ null）
//   3) 鳴らし方ごと替えるなら IHoustonVoice を実装して HoustonBox.Voice に入れる（音声合成をその場で呼ぶなど）
// 音量は HoustonClipVoice.Gain（既定 1）。#141 の SoundPrefs ができたら Gain = () => SoundPrefs.Gain(SoundBus.Voice) をつなぐ。
// 口の動き：鳴っている間は管制室の小窓（HoustonWindow）の口を動かす（HoustonBox が Talk を呼ぶ）。
using System;
using UnityEngine;

public interface IHoustonVoice
{
    /// <summary>台詞の声を鳴らす。id はサーバーの台詞の id（無いときは空）。鳴らした長さ（秒）を返す。鳴らせなければ 0</summary>
    float Play(string id, string text);
    void Stop();
    bool Playing { get; }
}

/// <summary>何も鳴らさない（字幕だけ）</summary>
public class HoustonNoVoice : IHoustonVoice
{
    public float Play(string id, string text) => 0f;
    public void Stop() { }
    public bool Playing => false;
}

/// <summary>id で AudioClip を引いて鳴らす。既定は Resources/Voice/Houston/&lt;id&gt;</summary>
public class HoustonClipVoice : IHoustonVoice
{
    public const string ResourceFolder = "Voice/Houston/";
    public static Func<string, AudioClip> Lookup = id => string.IsNullOrEmpty(id) ? null : Resources.Load<AudioClip>(ResourceFolder + id);
    public static Func<float> Gain = () => 1f;

    readonly AudioSource src;

    /// <param name="host">AudioSource を付ける GameObject（いつも有効なもの。吹き出しは消えるので使わない）</param>
    public HoustonClipVoice(GameObject host)
    {
        var go = new GameObject("HoustonVoice");
        go.transform.SetParent(host.transform, false);
        src = go.AddComponent<AudioSource>();
        src.playOnAwake = false; src.loop = false; src.spatialBlend = 0f;
    }

    public float Play(string id, string text)
    {
        Stop();
        var clip = Lookup != null ? Lookup(id) : null;
        if (clip == null) return 0f;
        src.clip = clip;
        src.volume = Mathf.Clamp01(Gain != null ? Gain() : 1f);
        src.Play();
        return clip.length;
    }

    public void Stop() { if (src != null && src.isPlaying) src.Stop(); }
    public bool Playing => src != null && src.isPlaying;
}
