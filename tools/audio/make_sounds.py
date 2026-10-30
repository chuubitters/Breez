"""
Breez: สร้างเพลงประกอบ (BGM) และเสียงเอฟเฟกต์ทั้งหมดด้วยโค้ด (ไม่มีลิขสิทธิ์ของคนอื่น)
ผลลัพธ์: Assets/Sound/Breez/*.ogg  (BGM วนซ้ำได้ไม่มีรอยต่อ)

  pip install numpy scipy      (ต้องมี ffmpeg สำหรับแปลงเป็น .ogg)
  python tools/audio/make_sounds.py Assets/Sound/Breez
"""
import os, subprocess, sys, tempfile
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt, fftconvolve

SR = 44100
OUT = sys.argv[1] if len(sys.argv) > 1 else 'Assets/Sound/Breez'
rng = np.random.default_rng(7)

# ---------------------------------------------------------------- helpers
def t_of(sec): return np.arange(int(sec * SR)) / SR
def midi(n): return 440.0 * 2 ** ((n - 69) / 12)
NOTE = {'C': 0, 'C#': 1, 'Db': 1, 'D': 2, 'D#': 3, 'Eb': 3, 'E': 4, 'F': 5, 'F#': 6, 'Gb': 6, 'G': 7, 'G#': 8, 'Ab': 8, 'A': 9, 'A#': 10, 'Bb': 10, 'B': 11}
def n(name):  # 'C4'
    return midi(12 * (int(name[-1]) + 1) + NOTE[name[:-1]])

def env_adsr(length, a, d, s, r, sustain_len=None):
    N = length; e = np.zeros(N)
    A, D, R = int(a * SR), int(d * SR), int(r * SR)
    S = max(0, N - A - D - R)
    i = 0
    e[i:i + A] = np.linspace(0, 1, A, endpoint=False) if A else []; i += A
    e[i:i + D] = np.linspace(1, s, D, endpoint=False) if D else []; i += D
    e[i:i + S] = s; i += S
    e[i:i + R] = np.linspace(s, 0, min(R, N - i)) if R else []
    return e

def lp(x, hz, order=2):
    return sosfilt(butter(order, hz / (SR / 2), 'low', output='sos'), x)
def hp(x, hz, order=2):
    return sosfilt(butter(order, hz / (SR / 2), 'high', output='sos'), x)
def bp(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo / (SR / 2), hi / (SR / 2)], 'band', output='sos'), x)

def reverb(x, seconds=2.2, mix=0.3, seed=1):
    L = int(seconds * SR)
    r = np.random.default_rng(seed)
    ir = r.standard_normal((L, 2)) * np.exp(-np.linspace(0, 6, L))[:, None]
    ir[:, 0] = lp(ir[:, 0], 6000); ir[:, 1] = lp(ir[:, 1], 6000)
    ir /= np.sqrt((ir ** 2).sum(0))
    if x.ndim == 1: x = np.stack([x, x], 1)
    wet = np.stack([fftconvolve(x[:, c], ir[:, c])[:len(x) + L] for c in range(2)], 1)
    dry = np.zeros_like(wet); dry[:len(x)] = x
    return dry * (1 - mix) + wet * mix

def place(buf, sig, at):
    i = int(at * SR)
    if sig.ndim == 1: sig = np.stack([sig, sig], 1)
    j = min(len(buf), i + len(sig))
    if i < len(buf): buf[i:j] += sig[:j - i]

def pan(sig, p):  # p -1..1
    l = np.cos((p + 1) * np.pi / 4); r = np.sin((p + 1) * np.pi / 4)
    return np.stack([sig * l, sig * r], 1)

# ---------------------------------------------------------------- instruments
def pad(freqs, dur, bright=1800, detune=0.25, a=1.2, r=1.6):
    t = t_of(dur); out = np.zeros(len(t))
    for f in freqs:
        for d in (-detune, 0, detune):
            ff = f * 2 ** (d / 1200 * 10)
            ph = rng.uniform(0, 2 * np.pi)
            saw = 2 * ((ff * t + ph / (2 * np.pi)) % 1) - 1
            out += saw * 0.33 + np.sin(2 * np.pi * ff * t + ph) * 0.5
    out = lp(out, bright, 2) / (len(freqs) * 3)
    out *= env_adsr(len(t), a, 0.5, 0.85, r)
    return out

def bell(f, dur=2.5, amp=1.0):
    t = t_of(dur)
    partials = [(1, 1), (2.0, 0.45), (3.01, 0.22), (4.2, 0.12), (5.4, 0.06)]
    s = sum(a * np.sin(2 * np.pi * f * k * t) * np.exp(-t * (2.2 + k * 1.3)) for k, a in partials)
    s *= np.minimum(1, t / 0.004)
    return s * amp * 0.35

def pluck(f, dur=0.6, amp=1.0, bright=4000):   # marimba / kalimba-ish
    t = t_of(dur)
    s = np.sin(2 * np.pi * f * t) * np.exp(-t * 7) + 0.35 * np.sin(2 * np.pi * f * 4 * t) * np.exp(-t * 26) + 0.15 * np.sin(2 * np.pi * f * 3 * t) * np.exp(-t * 14)
    s *= np.minimum(1, t / 0.002)
    return lp(s, bright) * amp * 0.5

def sub_bass(f, dur, amp=1.0):
    t = t_of(dur)
    return np.sin(2 * np.pi * f * t) * env_adsr(len(t), 0.01, 0.2, 0.6, 0.15) * amp * 0.4

def shaker(dur=0.08, amp=1.0):
    s = rng.standard_normal(int(dur * SR)) * np.exp(-np.linspace(0, 9, int(dur * SR)))
    return hp(s, 6000) * amp * 0.12

def noise(sec): return rng.standard_normal(int(sec * SR))

def soft_kick(amp=1.0):
    t = t_of(0.35); f = 110 * np.exp(-t * 18) + 45
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 9) * amp * 0.5

# ---------------------------------------------------------------- loop maker
def make_loop(length, render, tail=4.0, rev=(2.4, 0.32), gain_db=-14):
    """render(buf) เขียนลงบัฟเฟอร์ยาว length+tail แล้วพับหางกลับมาต้นเพลง => วนซ้ำไม่มีรอยต่อ"""
    buf = np.zeros((int((length + tail) * SR), 2))
    render(buf)
    if rev: buf = reverb(buf, *rev)[:len(buf)]
    L = int(length * SR)
    loop = buf[:L].copy()
    loop[:len(buf) - L] += buf[L:]
    return normalize(loop, gain_db)

def normalize(x, db):
    peak = np.max(np.abs(x)) + 1e-9
    rms = np.sqrt(np.mean(x ** 2)) + 1e-9
    x = x / rms * 10 ** (db / 20)
    if np.max(np.abs(x)) > 0.95: x = x / np.max(np.abs(x)) * 0.95
    return x

def finish_sfx(x, db=-9, rev=(0.9, 0.18)):
    if x.ndim == 1: x = np.stack([x, x], 1)
    if rev: x = reverb(x, *rev)
    fade = int(0.01 * SR); x[-fade:] *= np.linspace(1, 0, fade)[:, None]
    peak = np.max(np.abs(x)) + 1e-9
    return x / peak * 10 ** (db / 20)

def save(name, x, quality=5):
    os.makedirs(OUT, exist_ok=True)
    tmp = os.path.join(tempfile.gettempdir(), name + '.wav')
    wavfile.write(tmp, SR, (np.clip(x, -1, 1) * 32767).astype(np.int16))
    dst = os.path.join(OUT, name + '.ogg')
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', tmp, '-c:a', 'libvorbis', '-q:a', str(quality), dst], check=True)
    os.remove(tmp)
    print('✓', dst, f'{len(x) / SR:.1f}s')

# ================================================================= BGM
def chords_render(prog, beat, bars_per_chord, arps, bell_oct=5, bell_amp=0.5, pad_bright=1600, bass=True, seed=0):
    r = np.random.default_rng(seed)
    def render(buf):
        t = 0.0
        for chord in prog:
            dur = beat * 4 * bars_per_chord
            freqs = [n(x) for x in chord]
            place(buf, pan(pad(freqs, dur + 1.5, pad_bright), 0) * 0.9, t)
            if bass: place(buf, sub_bass(freqs[0] / 2, dur, 0.6) , t)
            steps = int(dur / (beat / 2))
            for k in range(steps):
                if r.random() < arps:
                    f = freqs[r.integers(len(freqs))] * 2 ** (bell_oct - 4)
                    place(buf, pan(bell(f, 2.5, bell_amp * r.uniform(0.6, 1)), r.uniform(-0.6, 0.6)), t + k * beat / 2)
            t += dur
    return render

def bgm_menu():
    prog = [['C3', 'G3', 'B3', 'E4'], ['A2', 'E3', 'G3', 'C4'], ['F2', 'C3', 'E3', 'A3'], ['G2', 'D3', 'F3', 'B3']] * 2
    beat = 60 / 72
    return make_loop(beat * 4 * 2 * len(prog), chords_render(prog, beat, 2, 0.32, 5, 0.45, 1500, seed=1))

def bgm_calm():
    prog = [['F2', 'C3', 'E3', 'G3', 'A3'], ['Db3', 'Ab3', 'C4', 'F4'], ['Bb2', 'F3', 'A3', 'D4'], ['C3', 'G3', 'Bb3', 'E4']] * 2
    beat = 60 / 60
    return make_loop(beat * 4 * 2 * len(prog), chords_render(prog, beat, 2, 0.18, 5, 0.38, 1200, seed=2), rev=(3.2, 0.4), gain_db=-16)

def bgm_windy():
    prog = [['D3', 'A3', 'C4', 'E4', 'F4'], ['Bb2', 'F3', 'A3', 'D4'], ['G2', 'D3', 'F3', 'A3', 'C4'], ['A2', 'E3', 'G3', 'C#4']] * 2
    beat = 60 / 58
    length = beat * 4 * 2 * len(prog)
    base = chords_render(prog, beat, 2, 0.12, 5, 0.3, 1100, seed=3)
    def render(buf):
        base(buf)
        w = noise(len(buf) / SR + 0.1)[:len(buf)]
        w = bp(w, 250, 1400)
        mod = 0.5 + 0.5 * np.sin(2 * np.pi * np.arange(len(w)) / SR / (length / 3))
        buf[:, 0] += w * mod * 0.05; buf[:, 1] += np.roll(w, 900) * mod * 0.05
    return make_loop(length, render, rev=(3.0, 0.38), gain_db=-16)

def bgm_power():
    beat = 60 / 112
    prog = [('C', ['C3', 'E3', 'G3']), ('G', ['G2', 'B2', 'D3']), ('Am', ['A2', 'C3', 'E3']), ('F', ['F2', 'A2', 'C3'])] * 2
    mel = ['E5', 'G5', 'C6', 'G5', 'D5', 'G5', 'B5', 'G5', 'C5', 'E5', 'A5', 'E5', 'C5', 'F5', 'A5', 'C6']
    def render(buf):
        t = 0.0
        for ci, (_, ch) in enumerate(prog):
            fr = [n(x) for x in ch]
            for b in range(8):   # 2 bars, 8 beats
                place(buf, sub_bass(fr[0] / 2 if b % 4 == 0 else fr[0], beat * 0.9, 0.7), t + b * beat)
                place(buf, soft_kick(0.6 if b % 2 == 0 else 0.0), t + b * beat)
                place(buf, pan(shaker(0.08, 1 if b % 2 else 0.6), 0.4), t + b * beat + beat / 2)
                place(buf, pan(pluck(fr[(b + 1) % 3] * 2, 0.5, 0.5), -0.3), t + b * beat + beat / 2)
            for k in range(4):
                f = n(mel[(ci * 4 + k) % len(mel)])
                place(buf, pan(pluck(f, 0.8, 0.8, 6000), 0.2), t + k * beat * 2)
            place(buf, pan(pad(fr + [fr[0] * 2], beat * 8 + 0.5, 1300), 0) * 0.35, t)
            t += beat * 8
    return make_loop(beat * 8 * len(prog), render, rev=(1.6, 0.22), gain_db=-15)

def bgm_focus():
    beat = 60 / 92
    prog = [['A2', 'E3', 'A3', 'C4', 'E4'], ['F2', 'C3', 'F3', 'A3', 'C4'], ['C3', 'G3', 'C4', 'E4', 'G4'], ['G2', 'D3', 'G3', 'B3', 'D4']] * 2
    def render(buf):
        t = 0.0
        r = np.random.default_rng(5)
        for ch in prog:
            fr = [n(x) for x in ch]
            place(buf, pan(pad(fr[:4], beat * 8 + 1, 1400), 0) * 0.55, t)
            pattern = [1, 2, 3, 4, 3, 2, 4, 3]
            for k in range(16):
                f = fr[pattern[k % 8]] * 2
                place(buf, pan(pluck(f, 0.7, 0.55 + 0.2 * (k % 4 == 0), 5000), r.uniform(-0.5, 0.5)), t + k * beat / 2)
            place(buf, sub_bass(fr[0], beat * 8, 0.5), t)
            t += beat * 8
    return make_loop(beat * 8 * len(prog), render, rev=(2.2, 0.3), gain_db=-15)

def amb_wind():
    length = 12.0
    def render(buf):
        w = noise(len(buf) / SR + 0.1)[:len(buf)]
        x = np.arange(len(w)) / SR
        gust = 0.55 + 0.45 * np.sin(2 * np.pi * x / length * 2) * np.sin(2 * np.pi * x / length * 3 + 1)
        lo = bp(w, 180, 900) * 0.8; hi = bp(np.roll(w, 4000), 700, 2600) * 0.35
        buf[:, 0] += (lo + hi) * gust; buf[:, 1] += (np.roll(lo, 1500) + hi) * gust
    return make_loop(length, render, tail=1.0, rev=None, gain_db=-14)

def amb_rain():
    length = 10.0
    def render(buf):
        r = np.random.default_rng(11)
        w = noise(len(buf) / SR + 0.1)[:len(buf)]
        bed = bp(w, 500, 7000) * 0.35
        buf[:, 0] += bed; buf[:, 1] += np.roll(bed, 700)
        for _ in range(int(length * 70)):
            d = int(r.uniform(0.003, 0.012) * SR); f = r.uniform(2500, 6500)
            tt = np.arange(d) / SR
            drop = np.sin(2 * np.pi * f * tt) * np.exp(-tt * 600) * r.uniform(0.05, 0.25)
            place(buf, pan(drop, r.uniform(-1, 1)), r.uniform(0, length))
    return make_loop(length, render, tail=0.5, rev=(0.6, 0.15), gain_db=-15)

# ================================================================= SFX
def sfx_click():
    t = t_of(0.07); f = 900 * np.exp(-t * 18) + 500
    return finish_sfx(np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 55), -10, (0.4, 0.1))

def sfx_back():
    a = bell(n('E6'), 0.25, 1); b = bell(n('B5'), 0.35, 1)
    x = np.zeros(int(0.45 * SR)); x[:len(a)] += a; x[int(0.07 * SR):int(0.07 * SR) + len(b)] += b
    return finish_sfx(x, -11, (0.5, 0.12))

def sfx_tick():
    t = t_of(0.12)
    s = np.sin(2 * np.pi * 1050 * t) * np.exp(-t * 45) + 0.4 * noise(0.12) * np.exp(-t * 120)
    return finish_sfx(lp(s, 5000), -9, (0.4, 0.1))

def sfx_go():
    x = np.zeros(int(1.6 * SR))
    for i, nn in enumerate(['C5', 'E5', 'G5', 'C6']):
        b = bell(n(nn), 1.4, 1); s = int(i * 0.06 * SR); x[s:s + len(b)] += b[:len(x) - s]
    return finish_sfx(x, -8)

def sfx_breath(up=True):
    dur = 1.4; t = t_of(dur)
    f0, f1 = (n('G4'), n('C5')) if up else (n('C5'), n('G4'))
    f = f0 + (f1 - f0) * (t / dur)
    tone = np.sin(2 * np.pi * np.cumsum(f) / SR) * 0.5 + np.sin(2 * np.pi * np.cumsum(f * 2) / SR) * 0.15
    air = bp(noise(dur), 600, 3000) * 0.15
    e = np.sin(np.pi * np.minimum(1, t / dur)) ** 1.5
    return finish_sfx((tone + air) * e, -12, (1.4, 0.3))

def sfx_inflate():
    dur = 0.22; t = t_of(dur); f = 260 + 220 * t / dur
    s = np.sign(np.sin(2 * np.pi * np.cumsum(f) / SR)) * 0.3 + np.sin(2 * np.pi * np.cumsum(f) / SR)
    s = lp(s, 1800) * np.sin(np.pi * t / dur)
    return finish_sfx(s, -14, None)

def sfx_pop():
    t = t_of(0.35)
    burst = noise(0.35) * np.exp(-t * 40)
    thump = np.sin(2 * np.pi * (150 * np.exp(-t * 20) + 60) * t) * np.exp(-t * 18)
    return finish_sfx(hp(burst, 400) * 0.9 + thump * 0.7, -6, (0.5, 0.12))

def sfx_point():
    x = np.zeros(int(0.7 * SR)); a = bell(n('E6'), 0.6, 1); b = bell(n('A6'), 0.6, 0.9)
    x[:len(a)] += a; s = int(0.07 * SR); x[s:s + len(b)] += b[:len(x) - s]
    return finish_sfx(x, -10)

def sfx_hit():
    t = t_of(0.6)
    thud = np.sin(2 * np.pi * (120 * np.exp(-t * 8) + 50) * t) * np.exp(-t * 7)
    fall = np.sin(2 * np.pi * np.cumsum(500 * np.exp(-t * 3)) / SR) * np.exp(-t * 5) * 0.4
    return finish_sfx(thud + fall + lp(noise(0.6), 900) * np.exp(-t * 20) * 0.5, -7, (0.7, 0.15))

def sfx_whoosh():
    dur = 0.35; t = t_of(dur)
    w = noise(dur); out = np.zeros_like(w)
    for i, (lo, hi) in enumerate([(400, 1200), (900, 2400), (1600, 4000)]):
        out += bp(w, lo, hi) * np.exp(-((t / dur - (0.3 + i * 0.2)) ** 2) / 0.02)
    return finish_sfx(out * np.sin(np.pi * t / dur), -14, None)

def sfx_bloom():
    x = np.zeros(int(2.2 * SR))
    for i, nn in enumerate(['C6', 'E6', 'G6', 'B6', 'D7']):
        b = bell(n(nn), 1.8, 0.8); s = int(i * 0.08 * SR); x[s:s + len(b)] += b[:len(x) - s]
    shimmer = hp(noise(2.2), 7000) * np.exp(-t_of(2.2) * 2.5) * 0.05
    return finish_sfx(x + shimmer, -10, (1.8, 0.35))

def sfx_plant():
    return finish_sfx(pluck(n('G5'), 0.7, 1) + pluck(n('C6'), 0.7, 0.6), -11, (0.8, 0.2))

def jingle(notes, step=0.13, last=1.6, db=-8):
    x = np.zeros(int((step * len(notes) + last) * SR))
    for i, nn in enumerate(notes):
        b = bell(n(nn), last, 1); pl = pluck(n(nn) / 2, 0.6, 0.5); b[:len(pl)] += pl
        s = int(i * step * SR); x[s:s + len(b)] += b[:len(x) - s]
    return finish_sfx(x, db, (1.6, 0.3))

def sfx_mood():
    return finish_sfx(bell(n('A5'), 1.2, 1) + bell(n('E6'), 1.2, 0.5), -11)

# ================================================================= main
if __name__ == '__main__':
    save('bgm_menu', bgm_menu())
    save('bgm_calm_flow', bgm_calm())
    save('bgm_power_release', bgm_power())
    save('bgm_breathe_focus', bgm_focus())
    save('bgm_windy_mountain', bgm_windy())
    save('amb_wind', amb_wind())
    save('amb_rain', amb_rain())
    save('ui_click', sfx_click())
    save('ui_back', sfx_back())
    save('count_tick', sfx_tick())
    save('count_go', sfx_go())
    save('breath_in', sfx_breath(True))
    save('breath_out', sfx_breath(False))
    save('balloon_inflate', sfx_inflate())
    save('balloon_pop', sfx_pop())
    save('focus_point', sfx_point())
    save('focus_hit', sfx_hit())
    save('focus_whoosh', sfx_whoosh())
    save('flower_bloom', sfx_bloom())
    save('flower_plant', sfx_plant())
    save('result_win', jingle(['C5', 'E5', 'G5', 'C6']))
    save('result_end', jingle(['G5', 'E5', 'C5'], 0.18, 1.8, -10))
    save('result_calm', jingle(['F5', 'A5', 'C6', 'E6'], 0.2, 2.2, -10))
    save('mood_select', sfx_mood())
