#!/usr/bin/env python3
"""Regenerate Ember Drake Pass as a melancholic nylon-string battle theme.

The target is a restrained, minor-key guitar mood: fingerpicked nylon-string
arpeggios, close harmony, soft percussion, and a warm low pulse. The melody and
chord movement are original, with ember air and drake pressure added for the
dungeon identity.
"""

from __future__ import annotations

import math
import struct
import wave
from pathlib import Path


SAMPLE_RATE = 44100
ROOT = Path(__file__).resolve().parents[1]
BGM_DIR = ROOT / "WitchTowerGame" / "Assets" / "Resources" / "Audio" / "BGM"

NOTE_INDEX = {
    "C": -9,
    "C#": -8,
    "Db": -8,
    "D": -7,
    "D#": -6,
    "Eb": -6,
    "E": -5,
    "F": -4,
    "F#": -3,
    "Gb": -3,
    "G": -2,
    "G#": -1,
    "Ab": -1,
    "A": 0,
    "A#": 1,
    "Bb": 1,
    "B": 2,
}


def note_frequency(note: str) -> float:
    name = note[:-1]
    octave = int(note[-1])
    semitone = NOTE_INDEX[name] + (octave - 4) * 12
    return 440.0 * (2.0 ** (semitone / 12.0))


def sine(freq: float, t: float, phase: float = 0.0) -> float:
    return math.sin(2.0 * math.pi * freq * t + phase)


def smoothstep(x: float) -> float:
    x = max(0.0, min(1.0, x))
    return x * x * (3.0 - 2.0 * x)


def noise(index: int, seed: int) -> float:
    value = math.sin((index + seed * 433) * 12.9898) * 43758.5453
    return (value - math.floor(value)) * 2.0 - 1.0


def pan_gains(pan: float) -> tuple[float, float]:
    pan = max(0.0, min(1.0, pan))
    return math.cos(pan * math.pi * 0.5), math.sin(pan * math.pi * 0.5)


def add(left: list[float], right: list[float], index: int, sample: float, pan: float) -> None:
    lgain, rgain = pan_gains(pan)
    left[index] += sample * lgain
    right[index] += sample * rgain


def edge_env(t: float, duration: float, fade: float = 0.9) -> float:
    return smoothstep(t / fade) * smoothstep((duration - t) / fade)


def hit_env(age: float, duration: float, attack: float, release: float, decay: float) -> float:
    if age < 0.0 or age > duration:
        return 0.0
    return smoothstep(age / max(0.001, attack)) * smoothstep((duration - age) / max(0.001, release)) * math.exp(-age * decay)


def add_nylon_pluck(
    left: list[float],
    right: list[float],
    start: float,
    note: str,
    gain: float,
    pan: float,
    seed: int,
    bright: float = 0.0,
) -> None:
    freq = note_frequency(note)
    duration = 1.15
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        age = i / SAMPLE_RATE - start
        env = hit_env(age, duration, 0.003, 0.13, 3.25)
        pick = noise(i, seed) * math.exp(-age * 35.0) * (0.035 + bright * 0.028)
        body = (
            sine(freq, age) * 0.58
            + sine(freq * 2.0, age, 0.22) * (0.16 + bright * 0.04)
            + sine(freq * 3.0, age, 0.70) * (0.045 + bright * 0.026)
            + sine(freq * 0.5, age, 0.35) * 0.035
        )
        add(left, right, i, math.tanh(body * 1.25 + pick) * env * gain, pan)


def add_guitar_roll(left: list[float], right: list[float], start: float, notes: tuple[str, ...], gain: float, pan: float, seed: int) -> None:
    for offset, note in enumerate(notes):
        add_nylon_pluck(left, right, start + offset * 0.026, note, gain * (1.0 - offset * 0.055), pan + (offset - 1.5) * 0.018, seed + offset, 0.22)


def add_soft_bass(left: list[float], right: list[float], start: float, note: str, gain: float, pan: float) -> None:
    freq = note_frequency(note)
    duration = 1.38
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        age = i / SAMPLE_RATE - start
        env = hit_env(age, duration, 0.014, 0.20, 1.95)
        tone = sine(freq, age) * 0.60 + sine(freq * 2.0, age, 0.8) * 0.16 + sine(freq * 0.5, age, 0.2) * 0.14
        add(left, right, i, tone * env * gain, pan)


def add_warm_pad(left: list[float], right: list[float], start: float, note: str, duration: float, gain: float, pan: float) -> None:
    freq = note_frequency(note)
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        t = i / SAMPLE_RATE
        age = t - start
        env = smoothstep(age / 0.62) * smoothstep((duration - age) / 0.72)
        shimmer = (
            sine(freq, t, 0.3) * 0.34
            + sine(freq * 0.997, t, 1.5) * 0.17
            + sine(freq * 2.0, t, 0.9) * 0.055
        )
        add(left, right, i, shimmer * env * gain, pan)


def add_shadow_flute(left: list[float], right: list[float], start: float, note: str, duration: float, gain: float, pan: float, seed: int) -> None:
    freq = note_frequency(note)
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        t = i / SAMPLE_RATE
        age = t - start
        env = smoothstep(age / 0.12) * smoothstep((duration - age) / 0.22)
        vibrato = 1.0 + 0.005 * sine(5.8, age, 0.4)
        breath = noise(i, seed) * 0.020
        tone = sine(freq * vibrato, age) * 0.46 + sine(freq * 2.0 * vibrato, age, 0.6) * 0.08 + breath
        add(left, right, i, tone * env * gain, pan)


def add_hand_tap(left: list[float], right: list[float], start: float, gain: float, pan: float, seed: int) -> None:
    duration = 0.15
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        age = i / SAMPLE_RATE - start
        env = hit_env(age, duration, 0.0015, 0.030, 16.0)
        low = sine(118.0 * (1.0 - 0.22 * smoothstep(age / duration)), age) * 0.42
        nail = noise(i, seed) * 0.22
        add(left, right, i, (low + nail) * env * gain, pan)


def add_ember_shaker(left: list[float], right: list[float], start: float, gain: float, pan: float, seed: int) -> None:
    duration = 0.075
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    previous = 0.0
    for i in range(start_i, end_i):
        age = i / SAMPLE_RATE - start
        raw = noise(i, seed)
        bright = raw - previous * 0.70
        previous = raw
        env = hit_env(age, duration, 0.001, 0.012, 22.0)
        add(left, right, i, bright * env * gain, pan)


def add_lava_breath(left: list[float], right: list[float], start: float, duration: float, gain: float, pan: float, seed: int) -> None:
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    low = 0.0
    previous = 0.0
    for i in range(start_i, end_i):
        t = i / SAMPLE_RATE
        age = t - start
        raw = noise(i, seed)
        low = low * 0.94 + raw * 0.06
        bright = raw - previous * 0.76
        previous = raw
        gate = 0.55 + 0.45 * max(0.0, sine(5.2 + seed * 0.003, t))
        env = smoothstep(age / 0.30) * smoothstep((duration - age) / 0.42)
        add(left, right, i, (low * 0.68 + bright * 0.22) * gate * env * gain, pan)


def add_drake_pulse(left: list[float], right: list[float], start: float, duration: float, gain: float, pan: float, seed: int) -> None:
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        t = i / SAMPLE_RATE
        age = t - start
        p = age / duration
        env = smoothstep(age / 0.18) * smoothstep((duration - age) / 0.30)
        freq = 46.0 + 11.0 * sine(0.8, t) + 4.0 * sine(2.1, t, 0.6)
        throat = sine(freq, t) * 0.36 + sine(freq * 0.5, t, 0.9) * 0.28
        grit = math.tanh((throat + noise(i, seed) * 0.11) * 1.8)
        add(left, right, i, grit * env * gain * (0.82 + 0.18 * smoothstep(1.0 - p)), pan)


def write_wav(path: Path, left: list[float], right: list[float], peak_target: float = 0.80) -> None:
    peak = max(max(abs(v) for v in left), max(abs(v) for v in right), 0.0001)
    scale = min(peak_target / peak, 4.0)
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as wav:
        wav.setnchannels(2)
        wav.setsampwidth(2)
        wav.setframerate(SAMPLE_RATE)
        frames = bytearray()
        for l_value, r_value in zip(left, right):
            l_sample = int(max(-1.0, min(1.0, math.tanh(l_value * scale * 1.04))) * 32767)
            r_sample = int(max(-1.0, min(1.0, math.tanh(r_value * scale * 1.04))) * 32767)
            frames += struct.pack("<h", l_sample)
            frames += struct.pack("<h", r_sample)
        wav.writeframes(frames)


def render_ember_drake_pass() -> Path:
    bpm = 92.0
    bars = 24
    beat = 60.0 / bpm
    bar = beat * 4.0
    duration = bars * bar
    count = int(duration * SAMPLE_RATE)
    left = [0.0] * count
    right = [0.0] * count

    progression = (
        {"bass": "D2", "pad": ("D3", "F3", "A3", "E4"), "roll": ("D3", "A3", "E4", "F4"), "arp": ("D4", "A3", "E4", "F4", "A3", "E4", "D4", "A3")},
        {"bass": "Bb1", "pad": ("Bb2", "D3", "F3", "A3"), "roll": ("Bb2", "F3", "A3", "D4"), "arp": ("Bb3", "F3", "D4", "A3", "F3", "D4", "C4", "F3")},
        {"bass": "F2", "pad": ("F3", "A3", "C4", "G4"), "roll": ("F3", "C4", "G4", "A4"), "arp": ("F4", "C4", "G4", "A4", "C4", "G4", "F4", "C4")},
        {"bass": "C2", "pad": ("C3", "E3", "G3", "D4"), "roll": ("C3", "G3", "D4", "E4"), "arp": ("C4", "G3", "E4", "D4", "G3", "E4", "C4", "G3")},
        {"bass": "G1", "pad": ("G2", "Bb2", "D3", "A3"), "roll": ("G2", "D3", "A3", "Bb3"), "arp": ("G3", "D3", "A3", "Bb3", "D3", "A3", "G3", "D3")},
        {"bass": "Bb1", "pad": ("Bb2", "D3", "F3", "C4"), "roll": ("Bb2", "F3", "C4", "D4"), "arp": ("Bb3", "F3", "C4", "D4", "F3", "C4", "Bb3", "F3")},
        {"bass": "A1", "pad": ("A2", "C#3", "E3", "G3"), "roll": ("A2", "E3", "G3", "C#4"), "arp": ("A3", "E3", "G3", "C#4", "E3", "G3", "A3", "E3")},
        {"bass": "D2", "pad": ("D3", "F3", "A3", "E4"), "roll": ("D3", "A3", "E4", "A4"), "arp": ("D4", "A3", "E4", "A4", "F4", "E4", "D4", "A3")},
    )
    bass_freqs = [note_frequency(chord["bass"]) for chord in progression]
    pad_freqs = [[note_frequency(note) for note in chord["pad"]] for chord in progression]

    for i in range(count):
        t = i / SAMPLE_RATE
        b = min(bars - 1, int(t / bar))
        section = min(2, b // 8)
        local = t - b * bar
        env = edge_env(t, duration, 1.0) * [0.78, 0.96, 1.04][section]
        bass = bass_freqs[b % len(progression)]
        pad = pad_freqs[b % len(progression)]
        ember_gate = 0.62 + 0.38 * sine(0.055, t, 0.7)
        pulse = 0.72 + 0.28 * smoothstep((beat * 0.22 - (local % (beat * 2.0))) / 0.12)
        warmth = sine(bass * 0.5, t, 0.2) * 0.16 * pulse + sine(bass, t, 1.1) * 0.045
        halo = (
            sine(pad[0], t, 0.2) * 0.055
            + sine(pad[1], t, 1.3) * 0.050
            + sine(pad[2], t, 2.2) * 0.046
            + sine(pad[3], t, 0.6) * 0.034
        )
        ash = noise(i, 81) * (0.0018 + 0.001 * section)
        add(left, right, i, (warmth * 0.50 + halo * ember_gate + ash) * env, 0.5 + 0.025 * sine(0.037, t))

    for b in range(bars):
        base = b * bar
        section = min(2, b // 8)
        chord = progression[b % len(progression)]
        add_guitar_roll(left, right, base + beat * 0.02, chord["roll"], 0.034 + section * 0.006, 0.45, 100 + b)
        add_soft_bass(left, right, base, chord["bass"], 0.034 + section * 0.006, 0.50)
        if section >= 1:
            add_soft_bass(left, right, base + beat * 2.5, chord["bass"], 0.020 + section * 0.005, 0.52)
        for step, note in enumerate(chord["arp"]):
            swing = 0.018 if step in (1, 5) else 0.0
            pan = 0.40 if step % 2 == 0 else 0.60
            accent = 1.0 if step in (0, 3, 6) else 0.74
            add_nylon_pluck(left, right, base + step * beat * 0.5 + swing, note, (0.026 + section * 0.005) * accent, pan, 200 + b * 17 + step, 0.28 if section >= 2 else 0.10)
        if section >= 1:
            add_hand_tap(left, right, base + beat * 2.0, 0.026 + section * 0.006, 0.44, 300 + b)
            add_hand_tap(left, right, base + beat * 3.5, 0.018 + section * 0.005, 0.58, 330 + b)
            for step in (1, 3, 5, 7):
                add_ember_shaker(left, right, base + step * beat * 0.5 + 0.02, 0.010 + section * 0.003, 0.38 + 0.06 * (step % 3), 360 + b * 5 + step)
        if b % 4 in (3, 7):
            add_lava_breath(left, right, base + beat * 1.25, beat * 2.0, 0.020 + section * 0.006, 0.67 if b % 8 == 3 else 0.32, 410 + b)
        if b in (7, 15, 23):
            add_drake_pulse(left, right, max(0.0, base - beat * 0.75), beat * 2.1, 0.032 + section * 0.010, 0.50, 500 + b)
        if b % 8 in (0, 4):
            for index, note in enumerate(chord["pad"][:3]):
                add_warm_pad(left, right, base, note, bar * 2.0, 0.010 + section * 0.003, 0.42 + index * 0.06)

    lead = (
        ("E5", 0.0), ("F5", 1.5), ("A5", 3.0), ("G5", 5.5),
        ("E5", 8.0), ("D5", 10.0), ("C5", 12.0), ("A4", 14.0),
        ("C5", 16.5), ("D5", 18.0), ("F5", 20.0), ("E5", 22.0),
    )
    for note, offset in lead:
        add_shadow_flute(left, right, 8 * bar + offset * beat, note, beat * 1.4, 0.020, 0.56, 650 + int(offset * 10))
        add_nylon_pluck(left, right, 16 * bar + offset * beat, note, 0.021, 0.58, 710 + int(offset * 11), 0.35)

    path = BGM_DIR / "dungeon_ember_drake_pass_loop.wav"
    write_wav(path, left, right)
    return path


def render_ember_drake_pass_boss() -> Path:
    bpm = 100.0
    bars = 16
    beat = 60.0 / bpm
    bar = beat * 4.0
    duration = bars * bar
    count = int(duration * SAMPLE_RATE)
    left = [0.0] * count
    right = [0.0] * count

    progression = (
        {"bass": "D2", "pad": ("D3", "F3", "A3", "E4"), "roll": ("D3", "A3", "E4", "F4"), "arp": ("D4", "A3", "E4", "F4", "A3", "E4", "F4", "A3")},
        {"bass": "Bb1", "pad": ("Bb2", "D3", "F3", "A3"), "roll": ("Bb2", "F3", "A3", "D4"), "arp": ("Bb3", "F3", "D4", "A3", "F3", "D4", "A3", "F3")},
        {"bass": "G1", "pad": ("G2", "Bb2", "D3", "A3"), "roll": ("G2", "D3", "A3", "Bb3"), "arp": ("G3", "D3", "A3", "Bb3", "D3", "A3", "Bb3", "D3")},
        {"bass": "A1", "pad": ("A2", "C#3", "E3", "G3"), "roll": ("A2", "E3", "G3", "C#4"), "arp": ("A3", "E3", "G3", "C#4", "E3", "G3", "C#4", "E3")},
    )
    bass_freqs = [note_frequency(chord["bass"]) for chord in progression]
    pad_freqs = [[note_frequency(note) for note in chord["pad"]] for chord in progression]

    for i in range(count):
        t = i / SAMPLE_RATE
        b = min(bars - 1, int(t / bar))
        local = t - b * bar
        env = edge_env(t, duration, 0.58)
        bass = bass_freqs[b % len(progression)]
        pad = pad_freqs[b % len(progression)]
        pulse = 0.58 + 0.42 * smoothstep((beat * 0.18 - (local % beat)) / 0.060)
        warmth = (sine(bass * 0.5, t) * 0.24 + sine(bass, t, 0.9) * 0.08) * pulse
        halo = sine(pad[0], t, 0.3) * 0.050 + sine(pad[2], t, 1.2) * 0.058 + sine(pad[3], t, 0.8) * 0.040
        flame = math.tanh((noise(i, 920) * 0.25 + sine(bass * 3.0, t)) * 1.2) * 0.018
        add(left, right, i, (warmth * 0.62 + halo + flame) * env, 0.5 + 0.025 * sine(0.06, t))

    for b in range(bars):
        base = b * bar
        chord = progression[b % len(progression)]
        add_guitar_roll(left, right, base + 0.012, chord["roll"], 0.046, 0.45, 1000 + b)
        add_soft_bass(left, right, base, chord["bass"], 0.048, 0.50)
        add_soft_bass(left, right, base + beat * 2.5, chord["bass"], 0.030, 0.52)
        for step, note in enumerate(chord["arp"]):
            pan = 0.38 if step % 2 == 0 else 0.62
            add_nylon_pluck(left, right, base + step * beat * 0.5, note, 0.036 if step in (0, 3, 6) else 0.026, pan, 1100 + b * 19 + step, 0.34)
        add_hand_tap(left, right, base + beat * 1.5, 0.036, 0.44, 1200 + b)
        add_hand_tap(left, right, base + beat * 2.0, 0.026, 0.52, 1230 + b)
        add_hand_tap(left, right, base + beat * 3.5, 0.034, 0.58, 1260 + b)
        for step in (1, 3, 5, 7):
            add_ember_shaker(left, right, base + step * beat * 0.5 + 0.02, 0.016, 0.37 + 0.07 * (step % 3), 1300 + b * 7 + step)
        if b % 2 == 1:
            add_lava_breath(left, right, base + beat * 1.1, beat * 1.7, 0.034, 0.68 if b % 4 == 1 else 0.32, 1400 + b)
        if b % 4 == 3:
            add_drake_pulse(left, right, base + beat * 0.4, beat * 2.0, 0.058, 0.50, 1500 + b)
            for index, note in enumerate(chord["pad"][:3]):
                add_warm_pad(left, right, base, note, bar * 1.4, 0.014, 0.42 + index * 0.06)

    lead = (
        ("E5", 0.0), ("F5", 1.5), ("A5", 3.0), ("G5", 4.5),
        ("E5", 6.0), ("D5", 8.0), ("C5", 10.0), ("A4", 12.0),
    )
    for phrase in (4 * bar, 12 * bar):
        for note, offset in lead:
            add_shadow_flute(left, right, phrase + offset * beat, note, beat * 1.05, 0.024, 0.56, 1600 + int(phrase) + int(offset * 10))

    path = BGM_DIR / "dungeon_ember_drake_pass_boss_loop.wav"
    write_wav(path, left, right)
    return path


def main() -> None:
    for path in (render_ember_drake_pass(), render_ember_drake_pass_boss()):
        print(f"Wrote {path}")


if __name__ == "__main__":
    main()
