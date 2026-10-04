#!/usr/bin/env python3
"""Synthesises the Poing Mort sound effects (all procedural, created for the project).

Usage: python3 tools/audio/generate_audio.py
Output: Assets/PoingMort/Art/Audio/*.wav (44.1 kHz, mono, 16-bit)
"""
import math
import os
import wave

import numpy as np
from scipy import signal

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "PoingMort", "Art", "Audio")
SR = 44100
rng = np.random.default_rng(7)


def write(name, x, peak=0.9):
    x = np.asarray(x, dtype=np.float64)
    m = np.max(np.abs(x)) or 1.0
    x = x / m * peak
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())
    print("wrote", os.path.relpath(path, ROOT), f"{len(x) / SR:.2f}s")


def t(dur):
    return np.arange(int(dur * SR)) / SR


def env(n, attack, decay, sustain=0.0, release=None):
    a = int(attack * SR)
    e = np.ones(n)
    if a > 0:
        e[:a] = np.linspace(0, 1, a)
    k = np.arange(n - a)
    e[a:] = sustain + (1 - sustain) * np.exp(-k / max(1, decay * SR))
    return e


def lowpass(x, cutoff, order=4):
    b, a = signal.butter(order, cutoff / (SR / 2), "low")
    return signal.lfilter(b, a, x)


def highpass(x, cutoff, order=2):
    b, a = signal.butter(order, cutoff / (SR / 2), "high")
    return signal.lfilter(b, a, x)


def bandpass(x, lo, hi, order=2):
    b, a = signal.butter(order, [lo / (SR / 2), hi / (SR / 2)], "band")
    return signal.lfilter(b, a, x)


def punch(seed, heavy=False):
    r = np.random.default_rng(seed)
    dur = 0.45 if heavy else 0.3
    tt = t(dur)
    n = len(tt)
    f0 = (70 if heavy else 95) * r.uniform(0.9, 1.1)
    sweep = f0 * (1 + 2.5 * np.exp(-tt * 40))
    thump = np.sin(2 * np.pi * np.cumsum(sweep) / SR) * env(n, 0.001, 0.07 if heavy else 0.045)
    slap = bandpass(r.normal(0, 1, n), 900, 5200) * env(n, 0.0005, 0.012)
    body = lowpass(r.normal(0, 1, n), 1200) * env(n, 0.001, 0.05)
    x = thump * 1.0 + slap * (0.55 if heavy else 0.75) + body * 0.35
    return x


def swing(seed, dur=0.28):
    r = np.random.default_rng(seed)
    tt = t(dur)
    n = len(tt)
    noise = r.normal(0, 1, n)
    # Band sweeping up then down, like air pushed by a fist
    out = np.zeros(n)
    chunk = 256
    for i in range(0, n, chunk):
        p = i / n
        centre = 500 + 2200 * math.sin(p * math.pi)
        seg = noise[max(0, i - 512):i + chunk]
        f = bandpass(seg, max(80, centre * 0.6), min(SR / 2 - 100, centre * 1.4))
        out[i:i + chunk] = f[-min(chunk, n - i):]
    e = np.sin(np.linspace(0, np.pi, n)) ** 1.5
    return out * e


def block(seed):
    r = np.random.default_rng(seed)
    tt = t(0.25)
    n = len(tt)
    thud = np.sin(2 * np.pi * 140 * tt * (1 + 0.6 * np.exp(-tt * 30))) * env(n, 0.001, 0.035)
    cloth = lowpass(r.normal(0, 1, n), 2500) * env(n, 0.001, 0.03)
    return thud + cloth * 0.6


def body_fall():
    tt = t(0.9)
    n = len(tt)
    x = np.zeros(n)
    for k, (start, gain, f) in enumerate(((0.0, 1.0, 60), (0.12, 0.6, 75), (0.3, 0.35, 90))):
        i = int(start * SR)
        seg = tt[: n - i]
        thump = np.sin(2 * np.pi * f * seg * (1 + 1.5 * np.exp(-seg * 25))) * np.exp(-seg / 0.09)
        grit = lowpass(rng.normal(0, 1, len(seg)), 1800) * np.exp(-seg / 0.06)
        x[i:] += gain * (thump + 0.5 * grit)
    return x


def engine_loop(dur=2.0, rpm=900):
    """Seamless idle-ish engine hum (pitch is raised in game with speed)."""
    n = int(dur * SR)
    tt = np.arange(n) / SR
    f = rpm / 60 * 2   # firing frequency (4-cylinder: 2 pulses per revolution)
    cycles = round(f * dur)
    f = cycles / dur
    x = np.zeros(n)
    for h, a in ((1, 1.0), (2, 0.55), (3, 0.35), (4, 0.2), (6, 0.12), (8, 0.06)):
        x += a * np.sin(2 * np.pi * f * h * tt + h * 0.7)
    # Combustion roughness, periodic so the loop is seamless
    rough = np.sin(2 * np.pi * (f / 2) * tt) * 0.15
    x *= 1 + rough
    noise = lowpass(rng.normal(0, 1, n * 2), 600)[n // 2: n // 2 + n]
    fade = np.minimum(1, np.minimum(np.arange(n), n - np.arange(n)) / (0.05 * SR))
    x = lowpass(x, 900) + noise * 0.08 * fade
    return x


def horn():
    tt = t(0.55)
    n = len(tt)
    x = signal.square(2 * np.pi * 415 * tt, 0.5) * 0.6 + signal.square(2 * np.pi * 520 * tt, 0.5) * 0.5
    x = lowpass(x, 2500)
    e = env(n, 0.01, 10.0, 1.0)
    e[-int(0.06 * SR):] *= np.linspace(1, 0, int(0.06 * SR))
    return x * e


def door(open_=True):
    tt = t(0.35)
    n = len(tt)
    click = bandpass(rng.normal(0, 1, n), 1500, 6000) * env(n, 0.0005, 0.008)
    clunk = np.sin(2 * np.pi * (110 if open_ else 85) * tt) * env(n, 0.002, 0.06 if open_ else 0.09)
    rattle = lowpass(rng.normal(0, 1, n), 800) * env(n, 0.002, 0.05)
    if open_:
        return click * 0.8 + clunk * 0.5 + rattle * 0.3
    return clunk * 1.0 + click * 0.4 + rattle * 0.5


def whistle():
    """Short two-finger whistle used to hail a car."""
    tt = t(0.6)
    n = len(tt)
    f = 2300 + 500 * np.sin(np.minimum(tt / 0.25, 1) * np.pi / 2) - 200 * np.maximum(0, tt - 0.4) / 0.2
    x = np.sin(2 * np.pi * np.cumsum(f) / SR)
    breath = bandpass(rng.normal(0, 1, n), 1800, 3500) * 0.15
    e = env(n, 0.03, 10, 1.0)
    e[-int(0.12 * SR):] *= np.linspace(1, 0, int(0.12 * SR))
    return (x + breath) * e


def ui_click(freq=1200):
    tt = t(0.06)
    n = len(tt)
    return np.sin(2 * np.pi * freq * tt) * env(n, 0.0005, 0.012) + bandpass(rng.normal(0, 1, n), 2000, 8000) * env(n, 0.0002, 0.004) * 0.3


def city_ambience(dur=24.0):
    """Late-afternoon city bed: low rumble, distant traffic swells, a bit of wind. Seamless loop."""
    n = int(dur * SR)
    tt = np.arange(n) / SR
    rumble = lowpass(rng.normal(0, 1, n + SR), 140)[SR // 2: SR // 2 + n]
    wind = bandpass(rng.normal(0, 1, n + SR), 300, 1400)[SR // 2: SR // 2 + n]
    swell = 0.5 + 0.5 * np.sin(2 * np.pi * tt / dur * 3) * np.sin(2 * np.pi * tt / dur * 2 + 1)
    gust = 0.4 + 0.6 * (0.5 + 0.5 * np.sin(2 * np.pi * tt / dur * 5 + 2))
    x = rumble * 1.0 * (0.7 + 0.3 * swell) + wind * 0.08 * gust
    # Crossfade the ends so the loop is seamless
    fade = int(1.0 * SR)
    head = x[:fade].copy()
    x[-fade:] = x[-fade:] * np.linspace(1, 0, fade) + head * np.linspace(0, 1, fade)
    return x[:-fade] if False else x


def main():
    for i in range(3):
        write(f"SFX_Punch_Hit_{i + 1}", punch(10 + i))
    for i in range(2):
        write(f"SFX_Punch_Heavy_{i + 1}", punch(20 + i, heavy=True))
    for i in range(3):
        write(f"SFX_Swing_{i + 1}", swing(30 + i, 0.22 + 0.04 * i), peak=0.6)
    for i in range(2):
        write(f"SFX_Block_{i + 1}", block(40 + i), peak=0.8)
    write("SFX_KO_BodyFall", body_fall())
    write("SFX_Engine_Loop", engine_loop(), peak=0.7)
    write("SFX_Horn", horn(), peak=0.7)
    write("SFX_Door_Open", door(True), peak=0.7)
    write("SFX_Door_Close", door(False), peak=0.8)
    write("SFX_Whistle", whistle(), peak=0.6)
    write("SFX_UI_Click", ui_click(1300), peak=0.5)
    write("SFX_UI_Hover", ui_click(900), peak=0.3)
    write("AMB_City_Loop", city_ambience(), peak=0.5)


if __name__ == "__main__":
    main()
