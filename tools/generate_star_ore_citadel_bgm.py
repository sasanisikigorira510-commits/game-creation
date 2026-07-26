#!/usr/bin/env python3
"""Regenerate Star Ore Citadel as a light romantic phone-call BGM.

The target is a soft drama-score mood: electric piano, small bells, mellow
syncopation, warm bass, and tiny telephone-like details. It uses original
phrases and chord movement, with no copied melody.
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
    value = math.sin((index + seed * 521) * 12.9898) * 43758.5453
    return (value - math.floor(value)) * 2.0 - 1.0


def pan_gains(pan: float) -> tuple[float, float]:
    pan = max(0.0, min(1.0, pan))
    return math.cos(pan * math.pi * 0.5), math.sin(pan * math.pi * 0.5)


def add(left: list[float], right: list[float], index: int, sample: float, pan: float) -> None:
    lgain, rgain = pan_gains(pan)
    left[index] += sample * lgain
    right[index] += sample * rgain


def edge_env(t: float, duration: float, fade: float = 0.80) -> float:
    return smoothstep(t / fade) * smoothstep((duration - t) / fade)


def hit_env(age: float, duration: float, attack: float, release: float, decay: float) -> float:
    if age < 0.0 or age > duration:
        return 0.0
    return smoothstep(age / max(0.001, attack)) * smoothstep((duration - age) / max(0.001, release)) * math.exp(-age * decay)


def add_electric_piano(
    left: list[float],
    right: list[float],
    start: float,
    note: str,
    gain: float,
    pan: float,
    seed: int,
    soft: bool = False,
) -> None:
    freq = note_frequency(note)
    duration = 2.10 if soft else 1.45
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        age = i / SAMPLE_RATE - start
        env = hit_env(age, duration, 0.006, 0.26, 1.55 if soft else 2.35)
        tine = math.exp(-age * 4.0) * (sine(freq * 2.01, age, 0.34) * 0.16 + sine(freq * 3.99, age, 0.9) * 0.035)
        body = sine(freq, age) * 0.44 + sine(freq * 2.0, age, 0.12) * 0.12 + sine(freq * 0.5, age, 0.3) * 0.035
        key = noise(i, seed) * math.exp(-age * 28.0) * 0.018
        add(left, right, i, math.tanh((body + tine + key) * 1.18) * env * gain, pan)


def add_ep_chord(left: list[float], right: list[float], start: float, notes: tuple[str, ...], gain: float, pan: float, seed: int) -> None:
    for offset, note in enumerate(notes):
        add_electric_piano(left, right, start + offset * 0.018, note, gain * (1.0 - offset * 0.045), pan + (offset - 1.5) * 0.022, seed + offset, soft=True)


def add_star_mallet(left: list[float], right: list[float], start: float, note: str, gain: float, pan: float, seed: int) -> None:
    freq = note_frequency(note)
    duration = 1.55
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        age = i / SAMPLE_RATE - start
        env = hit_env(age, duration, 0.004, 0.22, 2.4)
        strike = noise(i, seed) * math.exp(-age * 40.0) * 0.022
        tone = (
            sine(freq, age) * 0.42
            + sine(freq * 2.0, age, 0.45) * 0.11
            + sine(freq * 2.97, age, 1.1) * 0.055
            + strike
        )
        add(left, right, i, tone * env * gain, pan)


def add_soft_bass(left: list[float], right: list[float], start: float, note: str, gain: float, pan: float) -> None:
    freq = note_frequency(note)
    duration = 1.35
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        age = i / SAMPLE_RATE - start
        env = hit_env(age, duration, 0.012, 0.24, 1.85)
        tone = sine(freq, age) * 0.55 + sine(freq * 2.0, age, 0.5) * 0.13 + sine(freq * 0.5, age, 0.2) * 0.10
        add(left, right, i, tone * env * gain, pan)


def add_soft_kick(left: list[float], right: list[float], start: float, gain: float, pan: float, seed: int) -> None:
    duration = 0.24
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        age = i / SAMPLE_RATE - start
        env = hit_env(age, duration, 0.003, 0.045, 10.0)
        drop = 86.0 - 34.0 * smoothstep(age / duration)
        thump = sine(drop, age) * 0.58 + sine(drop * 0.5, age, 0.3) * 0.20 + noise(i, seed) * 0.040
        add(left, right, i, thump * env * gain, pan)


def add_snap(left: list[float], right: list[float], start: float, gain: float, pan: float, seed: int) -> None:
    duration = 0.105
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    previous = 0.0
    for i in range(start_i, end_i):
        age = i / SAMPLE_RATE - start
        raw = noise(i, seed)
        bright = raw - previous * 0.76
        previous = raw
        env = hit_env(age, duration, 0.001, 0.018, 22.0)
        click = sine(1420.0, age) * 0.08
        add(left, right, i, (bright * 0.34 + click) * env * gain, pan)


def add_brush_hat(left: list[float], right: list[float], start: float, gain: float, pan: float, seed: int) -> None:
    duration = 0.070
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    previous = 0.0
    for i in range(start_i, end_i):
        age = i / SAMPLE_RATE - start
        raw = noise(i, seed)
        bright = raw - previous * 0.86
        previous = raw
        env = hit_env(age, duration, 0.001, 0.010, 28.0)
        add(left, right, i, bright * env * gain, pan)


def add_phone_blip(left: list[float], right: list[float], start: float, note: str, gain: float, pan: float, seed: int) -> None:
    freq = note_frequency(note)
    duration = 0.20
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        age = i / SAMPLE_RATE - start
        env = hit_env(age, duration, 0.002, 0.040, 8.5)
        carrier = sine(freq, age) * 0.34 + sine(freq * 2.0, age, 0.4) * 0.08
        tiny_static = noise(i, seed) * math.exp(-age * 12.0) * 0.030
        add(left, right, i, (carrier + tiny_static) * env * gain, pan)


def add_warm_pad(left: list[float], right: list[float], start: float, note: str, duration: float, gain: float, pan: float) -> None:
    freq = note_frequency(note)
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        t = i / SAMPLE_RATE
        age = t - start
        env = smoothstep(age / 0.72) * smoothstep((duration - age) / 0.72)
        tone = sine(freq, t, 0.4) * 0.30 + sine(freq * 1.002, t, 1.7) * 0.16 + sine(freq * 2.0, t, 0.8) * 0.035
        add(left, right, i, tone * env * gain, pan)


def add_room_air(left: list[float], right: list[float], start: float, duration: float, gain: float, pan: float, seed: int) -> None:
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    low = 0.0
    previous = 0.0
    for i in range(start_i, end_i):
        age = i / SAMPLE_RATE - start
        raw = noise(i, seed)
        low = low * 0.94 + raw * 0.06
        bright = raw - previous * 0.84
        previous = raw
        env = smoothstep(age / 0.45) * smoothstep((duration - age) / 0.55)
        add(left, right, i, (low * 0.44 + bright * 0.14) * env * gain, pan)


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
            l_sample = int(max(-1.0, min(1.0, math.tanh(l_value * scale * 1.05))) * 32767)
            r_sample = int(max(-1.0, min(1.0, math.tanh(r_value * scale * 1.05))) * 32767)
            frames += struct.pack("<h", l_sample)
            frames += struct.pack("<h", r_sample)
        wav.writeframes(frames)


def star_phone_progression() -> tuple[dict[str, tuple[str, ...] | str], ...]:
    return (
        {"bass": "F#2", "chord": ("F#3", "A3", "C#4", "E4"), "color": ("G#4", "C#5"), "melody": ("C#5", "B4", "G#4", "A4")},
        {"bass": "D2", "chord": ("D3", "F#3", "A3", "C#4"), "color": ("E4", "A4"), "melody": ("A4", "F#4", "E4", "C#5")},
        {"bass": "A2", "chord": ("A3", "C#4", "E4", "B4"), "color": ("C#5", "E5"), "melody": ("B4", "C#5", "E5", "C#5")},
        {"bass": "E2", "chord": ("E3", "G#3", "B3", "C#4"), "color": ("F#4", "B4"), "melody": ("G#4", "B4", "C#5", "B4")},
        {"bass": "B1", "chord": ("B2", "D3", "F#3", "A3"), "color": ("C#4", "F#4"), "melody": ("A4", "F#4", "D4", "F#4")},
        {"bass": "C#2", "chord": ("C#3", "E3", "G#3", "B3"), "color": ("D#4", "G#4"), "melody": ("G#4", "B4", "D#5", "C#5")},
        {"bass": "F#2", "chord": ("F#3", "A3", "C#4", "E4"), "color": ("G#4", "B4"), "melody": ("E5", "C#5", "B4", "A4")},
        {"bass": "E2", "chord": ("E3", "G#3", "B3", "D4"), "color": ("F#4", "G#4"), "melody": ("B4", "G#4", "F#4", "E4")},
    )


def render_star_ore_citadel() -> Path:
    bpm = 96.0
    bars = 24
    beat = 60.0 / bpm
    bar = beat * 4.0
    duration = bars * bar
    count = int(duration * SAMPLE_RATE)
    left = [0.0] * count
    right = [0.0] * count
    progression = star_phone_progression()
    bass_freqs = [note_frequency(chord["bass"]) for chord in progression]
    pad_freqs = [[note_frequency(note) for note in chord["chord"]] for chord in progression]

    for i in range(count):
        t = i / SAMPLE_RATE
        b = min(bars - 1, int(t / bar))
        section = min(2, b // 8)
        local = t - b * bar
        env = edge_env(t, duration, 0.8) * [0.76, 0.94, 1.03][section]
        bass = bass_freqs[b % len(progression)]
        chord = pad_freqs[b % len(progression)]
        side = 0.72 + 0.28 * smoothstep((beat * 0.22 - (local % beat)) / 0.11)
        low = (sine(bass * 0.5, t, 0.5) * 0.13 + sine(bass, t, 1.3) * 0.035) * side
        pad = (
            sine(chord[0], t, 0.2) * 0.012
            + sine(chord[1], t, 1.1) * 0.011
            + sine(chord[2], t, 2.0) * 0.010
            + sine(chord[3], t, 0.8) * 0.008
        )
        air = noise(i, 77) * 0.0014
        add(left, right, i, (low + pad + air) * env, 0.5 + 0.025 * sine(0.05, t))

    for b in range(bars):
        base = b * bar
        section = min(2, b // 8)
        chord = progression[b % len(progression)]
        chord_notes = chord["chord"]
        melody = chord["melody"]
        color = chord["color"]
        add_ep_chord(left, right, base + 0.02, chord_notes, 0.014 + section * 0.002, 0.47, 100 + b)
        add_ep_chord(left, right, base + beat * 2.05, chord_notes[1:] + color[:1], 0.008 + section * 0.0015, 0.53, 130 + b)
        add_soft_bass(left, right, base, chord["bass"], 0.030 + section * 0.006, 0.50)
        if section >= 1:
            add_soft_bass(left, right, base + beat * 2.5, chord["bass"], 0.020 + section * 0.004, 0.52)
        add_soft_kick(left, right, base, 0.033 + section * 0.005, 0.49, 170 + b)
        if b % 2 == 0:
            add_soft_kick(left, right, base + beat * 2.5, 0.018 + section * 0.004, 0.53, 180 + b)
        add_snap(left, right, base + beat * 2.0, 0.018 + section * 0.005, 0.57, 210 + b)
        for step in (1, 3, 5, 7):
            add_brush_hat(left, right, base + step * beat * 0.5 + 0.018, 0.006 + section * 0.0025, 0.35 + (step % 4) * 0.08, 240 + b * 7 + step)
        for step, note in enumerate(melody):
            start = base + (0.75 + step * 0.75) * beat
            add_star_mallet(left, right, start, note, 0.036 + section * 0.006, 0.58 if step % 2 else 0.42, 300 + b * 11 + step)
            if step in (0, 3):
                add_phone_blip(left, right, start + beat * 0.18, note, 0.010 + section * 0.002, 0.64 if step else 0.36, 335 + b * 11 + step)
        if b % 4 == 3:
            add_phone_blip(left, right, base + beat * 3.05, color[0], 0.014 + section * 0.003, 0.68, 360 + b)
            add_phone_blip(left, right, base + beat * 3.38, color[1], 0.011 + section * 0.002, 0.62, 390 + b)
        if b % 8 == 7:
            add_room_air(left, right, base + beat, beat * 2.5, 0.014 + section * 0.004, 0.68, 450 + b)

    path = BGM_DIR / "dungeon_star_ore_citadel_loop.wav"
    write_wav(path, left, right)
    return path


def render_star_ore_citadel_boss() -> Path:
    bpm = 112.0
    bars = 16
    beat = 60.0 / bpm
    bar = beat * 4.0
    duration = bars * bar
    count = int(duration * SAMPLE_RATE)
    left = [0.0] * count
    right = [0.0] * count
    progression = star_phone_progression()[:4]
    bass_freqs = [note_frequency(chord["bass"]) for chord in progression]
    pad_freqs = [[note_frequency(note) for note in chord["chord"]] for chord in progression]

    for i in range(count):
        t = i / SAMPLE_RATE
        b = min(bars - 1, int(t / bar))
        local = t - b * bar
        env = edge_env(t, duration, 0.50)
        bass = bass_freqs[b % len(progression)]
        chord = pad_freqs[b % len(progression)]
        pulse = 0.55 + 0.45 * smoothstep((beat * 0.18 - (local % beat)) / 0.060)
        low = (sine(bass * 0.5, t, 0.5) * 0.20 + sine(bass, t, 1.2) * 0.070) * pulse
        pad = sine(chord[0], t, 0.2) * 0.014 + sine(chord[2], t, 1.4) * 0.014 + sine(chord[3], t, 0.8) * 0.011
        soft_drive = math.tanh((noise(i, 910) * 0.16 + sine(bass * 3.0, t)) * 0.9) * 0.010
        add(left, right, i, (low + pad + soft_drive) * env, 0.5 + 0.025 * sine(0.07, t))

    for b in range(bars):
        base = b * bar
        chord = progression[b % len(progression)]
        chord_notes = chord["chord"]
        melody = chord["melody"]
        color = chord["color"]
        add_ep_chord(left, right, base + 0.015, chord_notes, 0.020, 0.47, 1000 + b)
        add_ep_chord(left, right, base + beat * 1.55, chord_notes[1:] + color[:1], 0.012, 0.56, 1020 + b)
        add_ep_chord(left, right, base + beat * 3.0, chord_notes[:2] + color, 0.010, 0.45, 1040 + b)
        add_soft_bass(left, right, base, chord["bass"], 0.046, 0.50)
        add_soft_bass(left, right, base + beat * 2.5, chord["bass"], 0.032, 0.52)
        add_soft_kick(left, right, base, 0.050, 0.49, 1070 + b)
        add_soft_kick(left, right, base + beat * 2.5, 0.032, 0.53, 1080 + b)
        add_snap(left, right, base + beat * 1.5, 0.024, 0.43, 1110 + b)
        add_snap(left, right, base + beat * 3.5, 0.030, 0.58, 1140 + b)
        for step in range(8):
            add_brush_hat(left, right, base + step * beat * 0.5 + 0.014, 0.011 if step % 2 else 0.007, 0.34 + (step % 4) * 0.09, 1170 + b * 9 + step)
        for step, note in enumerate(melody):
            start = base + (0.45 + step * 0.72) * beat
            add_star_mallet(left, right, start, note, 0.042, 0.58 if step % 2 else 0.42, 1230 + b * 13 + step)
            if step in (0, 3):
                add_phone_blip(left, right, start + beat * 0.16, note, 0.014, 0.64 if step else 0.36, 1265 + b * 13 + step)
        if b % 2 == 1:
            add_phone_blip(left, right, base + beat * 3.04, color[0], 0.019, 0.68, 1300 + b)
            add_phone_blip(left, right, base + beat * 3.34, color[1], 0.015, 0.62, 1320 + b)
            add_room_air(left, right, base + beat * 1.0, beat * 1.7, 0.016, 0.70 if b % 4 == 1 else 0.30, 1340 + b)

    path = BGM_DIR / "dungeon_star_ore_citadel_boss_loop.wav"
    write_wav(path, left, right)
    return path


def main() -> None:
    for path in (render_star_ore_citadel(), render_star_ore_citadel_boss()):
        print(f"Wrote {path}")


if __name__ == "__main__":
    main()
