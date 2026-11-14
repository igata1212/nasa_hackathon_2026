#!/usr/bin/env python3
"""アポロ13風の効果音を合成して sfx/ に書き出す（自作・ライセンス制限なし）。

出力: 44.1kHz / 16bit / モノラル WAV。ループ用の音は継ぎ目が出ないように作っている。
実行: python3 art/audio/synth_sfx.py（unity/Assets/Audio/Sfx/ に上書きする。#141 で tmp/audio_build/ から移した）
"""
import os
import wave

import numpy as np

SR = 44100
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "unity", "Assets", "Audio", "Sfx")
RNG = np.random.default_rng(20261114)  # 再実行しても同じ音になるよう固定


def t_axis(dur):
    return np.arange(int(round(dur * SR))) / SR


def fade(x, n_in=0, n_out=0):
    """頭とお尻に余弦フェードをかけてクリックを防ぐ。"""
    x = np.array(x, dtype=np.float64)
    if n_in > 0:
        x[:n_in] *= 0.5 - 0.5 * np.cos(np.pi * np.linspace(0.0, 1.0, n_in))
    if n_out > 0:
        x[-n_out:] *= 0.5 + 0.5 * np.cos(np.pi * np.linspace(0.0, 1.0, n_out))
    return x


def band_mask(f, lo, hi, tr):
    """lo〜hi Hz を通す周波数マスク。端の幅は lo・hi に対する比 tr。"""
    m = np.zeros_like(f)
    m[(f >= lo) & (f <= hi)] = 1.0
    w = lo * tr
    a = (f >= lo - w) & (f < lo)
    m[a] = 0.5 - 0.5 * np.cos(np.pi * (f[a] - (lo - w)) / w)
    w = hi * tr
    b = (f > hi) & (f <= hi + w)
    m[b] = 0.5 + 0.5 * np.cos(np.pi * (f[b] - hi) / w)
    return m


def fft_filter(x, mask, periodic):
    """FFT で帯域を切る。periodic=True は長さを保つ循環フィルタ（ループ用）。"""
    n = len(x)
    N = n if periodic else 1 << int(np.ceil(np.log2(2 * n)))
    f = np.fft.rfftfreq(N, 1.0 / SR)
    y = np.fft.irfft(np.fft.rfft(x, N) * mask(f), N)
    return y[:n]


def loop_noise(dur, mask):
    """周期的なノイズ。先頭と末尾がつながるのでループしても継ぎ目が出ない。"""
    y = fft_filter(RNG.standard_normal(int(round(dur * SR))), mask, periodic=True)
    return y / np.sqrt(np.mean(y ** 2))


def am_periodic(dur, cycles, depth):
    """整数サイクルの振幅変調（ループ長にちょうど収まる）。"""
    t = t_axis(dur)
    return (1.0 - depth) + depth * (0.5 - 0.5 * np.cos(2 * np.pi * cycles * t / dur))


def set_rms(x, db):
    return x * (10 ** (db / 20.0) / np.sqrt(np.mean(x ** 2)))


def alarm_master():
    """マスターアラーム風: 660Hz と 880Hz を交互に。0.6秒周期×3=1.8秒。端は無音なので継ぎ目なし。"""
    period, cycles = 0.6, 3
    out = np.zeros(int(round(period * cycles * SR)))
    n_note = int(round(0.27 * SR))
    for k in range(cycles):
        for f, start in ((660.0, 0.0), (880.0, 0.30)):
            i0 = int(round((k * period + start) * SR))
            tt = np.arange(n_note) / SR
            tone = np.sin(2 * np.pi * f * tt) + 0.12 * np.sin(4 * np.pi * f * tt)
            out[i0:i0 + n_note] += fade(tone, int(0.008 * SR), int(0.03 * SR))
    return out / np.max(np.abs(out)) * 0.25


def alarm_soft():
    """注意レベルの柔らかい警告「ピ・コン」（0.5秒）。"""
    out = np.zeros(int(round(0.5 * SR)))
    for start, f0, f1, length, tau, amp in ((0.0, 1100.0, 1320.0, 0.12, 0.08, 1.0),
                                             (0.15, 1568.0, 1568.0, 0.30, 0.10, 0.8)):
        tt = np.arange(int(round(length * SR))) / SR
        f = f0 + (f1 - f0) * np.minimum(tt / length, 1.0)
        s = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt / tau)
        i0 = int(round(start * SR))
        out[i0:i0 + len(s)] += amp * fade(s, int(0.003 * SR), int(0.01 * SR))
    out = fade(out, 0, int(0.05 * SR))
    return out / np.max(np.abs(out)) * 0.5


def quindar(freq):
    """Quindar トーン（250ms・前後 5ms フェード）。"""
    s = np.sin(2 * np.pi * freq * np.arange(int(round(0.25 * SR))) / SR)
    return fade(s, int(0.005 * SR), int(0.005 * SR))


def gas_hiss():
    """ガス漏れのシューッ: 2.5〜7kHz のノイズ、3秒ループ（1Hz でゆらぐ）。"""
    dur = 3.0
    x = loop_noise(dur, lambda f: band_mask(f, 2500, 7000, 0.15))
    return set_rms(x * am_periodic(dur, 3, 0.25), -20)


def reentry_rumble():
    """再突入のゴーッ: 45〜420Hz の低いノイズ（低音寄り）、3秒ループ（1Hz の唸り）。"""
    dur = 3.0
    x = loop_noise(dur, lambda f: band_mask(f, 45, 420, 0.5) * (100.0 / np.maximum(f, 20.0)) ** 0.6)
    return set_rms(x * am_periodic(dur, 3, 0.3), -15)


def radio_static():
    """通信途絶のザー: 350〜3800Hz の小さなノイズ＋プチプチ、2秒ループ。"""
    dur = 2.0
    n = int(round(dur * SR))
    x = set_rms(loop_noise(dur, lambda f: band_mask(f, 350, 3800, 0.2)), -28)
    for _ in range(30):
        L = int(0.004 * SR)
        click = RNG.standard_normal(L) * np.exp(-np.arange(L) / (0.0008 * SR))
        idx = (int(RNG.integers(0, n)) + np.arange(L)) % n  # 循環させて継ぎ目を作らない
        np.add.at(x, idx, click * RNG.uniform(0.03, 0.10))
    return x


def splash():
    """着水のザブン（1秒）: 低いうなり＋水しぶきノイズ＋余韻＋小さな泡。"""
    dur = 1.0
    n = int(round(dur * SR))
    t = t_axis(dur)
    sweep = 180.0 - 110.0 * np.minimum(t / 0.25, 1.0)
    body = np.sin(2 * np.pi * np.cumsum(sweep) / SR) * np.exp(-t / 0.12) * 0.9
    spray = fft_filter(RNG.standard_normal(n), lambda f: band_mask(f, 800, 6000, 0.3), periodic=False)
    spray = spray * np.exp(-t / 0.18) * np.minimum(t / 0.003, 1.0) * 0.6
    tail = fft_filter(RNG.standard_normal(n), lambda f: band_mask(f, 200, 1500, 0.3), periodic=False)
    tail = tail * np.exp(-t / 0.35) * 0.25
    x = body * np.minimum(t / 0.003, 1.0) + spray + tail
    for st in (0.12, 0.27, 0.41, 0.58, 0.79):
        i0 = int(round(st * SR))
        L = int(0.06 * SR)
        tt = np.arange(L) / SR
        f0, f1 = RNG.uniform(300.0, 500.0), RNG.uniform(600.0, 800.0)
        f = f0 + (f1 - f0) * tt / 0.06
        x[i0:i0 + L] += 0.15 * np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt / 0.02)
    x = fade(x, 0, int(0.1 * SR))
    return x / np.max(np.abs(x)) * 0.8


def write_wav(name, x, peak=None):
    x = np.asarray(x, dtype=np.float64)
    if peak is not None:
        x = x / np.max(np.abs(x)) * peak
    x = np.clip(x, -1.0, 1.0)
    pcm = np.round(x * 32767.0).astype("<i2")
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    print("%-24s %6.3f s  peak %6.1f dBFS  rms %6.1f dBFS" % (
        name, len(x) / SR, 20 * np.log10(np.max(np.abs(x)) + 1e-12), 20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-12)))


def main():
    os.makedirs(OUT, exist_ok=True)
    write_wav("alarm_master.wav", alarm_master())
    write_wav("alarm_soft.wav", alarm_soft())
    write_wav("gas_hiss_loop.wav", gas_hiss())
    write_wav("quindar_in.wav", quindar(2525.0), peak=0.5)
    write_wav("quindar_out.wav", quindar(2475.0), peak=0.5)
    write_wav("radio_static_loop.wav", radio_static())
    write_wav("reentry_rumble_loop.wav", reentry_rumble())
    write_wav("splash.wav", splash())


if __name__ == "__main__":
    main()
