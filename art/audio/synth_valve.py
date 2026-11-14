"""バルブの音（valve_turn.wav）の作り方。2026-11-14 Claude。実行すると tmp/audio_valve/ に v3_*.wav（候補）を書く。v3_P_ticks_only.wav を unity/Assets/Audio/Sfx/valve_turn.wav にした（#141 で tmp/audio_build/ から移した）。
3回目：細かい共鳴をたくさん重ねる（モード合成）。きれいな倍音にしない＝カリンバに聞こえないように。"""
import os, numpy as np, soundfile as sf, soxr
SR = 44100
REPO = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
OUT = os.path.join(REPO, "tmp", "audio_valve"); os.makedirs(OUT, exist_ok=True)
KEN = os.path.join(REPO, "tmp/space_assets/08-sfx/kenney_sci_fi_sounds_godot/addons/kenney sci-fi sounds/")
RNG = np.random.default_rng(42)

def load(p):
    x, sr = sf.read(p, dtype="float64"); x = x.mean(1) if x.ndim > 1 else x
    return soxr.resample(x, sr, SR) if sr != SR else x

def modal_ir(lo, hi, n_modes, d_lo, d_hi, dur):
    t = np.arange(int(dur * SR)) / SR
    f = np.exp(RNG.uniform(np.log(lo), np.log(hi), n_modes))
    d = np.exp(RNG.uniform(np.log(d_lo), np.log(d_hi), n_modes))
    a = RNG.uniform(0.3, 1.0, n_modes) / np.sqrt(f / lo)
    ir = np.zeros_like(t)
    for fk, dk, ak in zip(f, d, a):
        ir += ak * np.exp(-t / dk) * np.sin(2 * np.pi * fk * t + RNG.random() * 6.28)
    return ir

def conv(x, h):
    n = len(x) + len(h) - 1; N = 1 << int(np.ceil(np.log2(n)))
    return np.fft.irfft(np.fft.rfft(x, N) * np.fft.rfft(h, N), N)[:len(x)]

def norm(x, db=-3.0):
    x = x - x.mean(); return x / (np.abs(x).max() + 1e-12) * 10 ** (db / 20)

def fade(x, a=3, b=40):
    a, b = int(a * SR / 1000), int(b * SR / 1000); x = x.copy()
    x[:a] *= np.linspace(0, 1, a); x[-b:] *= np.linspace(1, 0, b); return x

def burst(n=60):
    return RNG.standard_normal(n) * np.exp(-np.arange(n) / 12)

# 共鳴の箱（ハンドル・配管）
IR_TICK = modal_ir(1800, 9000, 40, 0.004, 0.018, 0.08)    # ラチェットのカチ
IR_CREAK = modal_ir(250, 2200, 40, 0.010, 0.045, 0.15)   # 金属のきしみ
IR_CLANG = modal_ir(180, 6000, 60, 0.03, 0.25, 0.7)      # 閉まったときのガチャン

def ticks(n, gap0, gap1, length):
    x = np.zeros(int(length * SR)); t = 0.02; times = []
    for k in range(n):
        i = int(t * SR); b = burst(); x[i:i+len(b)] += b * (0.7 + 0.3 * RNG.random()); times.append(t)
        t += gap0 + (gap1 - gap0) * k / max(1, n - 1)
    return conv(x, IR_TICK), t

def creak(dur, r0, r1, length):
    x = np.zeros(int(length * SR)); t = 0.02
    while t < dur:
        p = t / dur; r = (r0 + (r1 - r0) * p) * (1 + 0.15 * RNG.standard_normal())
        i = int(t * SR); x[i] += np.sin(np.pi * p) ** 0.7 * (0.5 + 0.5 * RNG.random()); t += 1 / max(r, 20)
    return conv(x, IR_CREAK)

def clang(length):
    x = np.zeros(int(length * SR)); b = burst(200); x[:len(b)] = b
    return conv(x, IR_CLANG)

thud = load(KEN + "impact_metal_001.ogg")
def at(L, parts):
    y = np.zeros(int(L * SR))
    for s, sig, g in parts:
        i = int(s * SR); j = min(len(y), i + len(sig)); y[i:j] += g * norm(sig, 0)[: j - i]
    return y

def w(name, x): sf.write(os.path.join(OUT, name), fade(norm(x)), SR, subtype="PCM_16")

L = 1.1
tk, tend = ticks(6, 0.065, 0.095, L)
ck = creak(0.5, 90, 160, L)
cl = clang(0.7)
w("v3_K_ticks_clang.wav", at(L, [(0, tk, 0.6), (tend, cl, 0.8), (tend, thud, 0.5)]))
w("v3_L_creak_clang.wav", at(L, [(0, ck, 0.7), (0.52, cl, 0.8), (0.52, thud, 0.5)]))
w("v3_M_creak_ticks_clang.wav", at(L, [(0, ck, 0.5), (0, tk, 0.45), (tend, cl, 0.8), (tend, thud, 0.5)]))
tk3, t3 = ticks(3, 0.075, 0.085, 0.8)
w("v3_N_short.wav", at(0.8, [(0, tk3, 0.6), (t3, cl[: int(0.55*SR)], 0.8), (t3, thud, 0.5)]))
w("v3_O_clang_only.wav", at(0.7, [(0, cl, 0.8), (0, thud, 0.5)]))
w("v3_P_ticks_only.wav", at(0.6, [(0, ticks(6, 0.07, 0.08, 0.6)[0], 1.0)]))
w("v3_Q_creak_only.wav", at(0.6, [(0, creak(0.5, 90, 160, 0.6), 1.0)]))
print("ok")
