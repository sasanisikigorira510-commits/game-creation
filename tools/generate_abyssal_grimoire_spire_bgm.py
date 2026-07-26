#!/usr/bin/env python3
"""Regenerate Abyssal Grimoire Spire as a bossa nova battle theme.

The target is a relaxed bossa feel: nylon guitar syncopation, upright-style
bass, rim clicks, brushed shaker, and a vibraphone-like lead. It deliberately
avoids organ/choir-heavy dark fantasy colors.
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
    value = math.sin((index + seed * 661) * 12.9898) * 43758.5453
    return (value - math.floor(value)) * 2.0 - 1.0


def pan_gains(pan: float) -> tuple[float, float]:
    pan = max(0.0, min(1.0, pan))
    return math.cos(pan * math.pi * 0.5), math.sin(pan * math.pi * 0.5)


def add(left: list[float], right: list[float], index: int, sample: float, pan: float) -> None:
    lgain, rgain = pan_gains(pan)
    left[index] += sample * lgain
    right[index] += sample * rgain


def edge_env(t: float, duration: float, fade: float = 0.72) -> float:
    return smoothstep(t / fade) * smoothstep((duration - t) / fade)


def hit_env(age: float, duration: float, attack: float, release: float, decay: float) -> float:
    if age < 0.0 or age > duration:
        return 0.0
    return smoothstep(age / max(0.001, attack)) * smoothstep((duration - age) / max(0.001, release)) * math.exp(-age * decay)


def add_nylon_note(left: list[float], right: list[float], start: float, note: str, gain: float, pan: float, seed: int) -> None:
    freq = note_frequency(note)
    duration = 0.78
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        age = i / SAMPLE_RATE - start
        env = hit_env(age, duration, 0.003, 0.10, 4.5)
        pick = noise(i, seed) * math.exp(-age * 34.0) * 0.030
        body = sine(freq, age) * 0.52 + sine(freq * 2.0, age, 0.24) * 0.13 + sine(freq * 3.0, age, 0.8) * 0.035
        add(left, right, i, math.tanh((body + pick) * 1.25) * env * gain, pan)


def add_guitar_chord(left: list[float], right: list[float], start: float, notes: tuple[str, ...], gain: float, pan: float, seed: int) -> None:
    for offset, note in enumerate(notes):
        add_nylon_note(left, right, start + offset * 0.012, note, gain * (1.0 - offset * 0.055), pan + (offset - 1.5) * 0.018, seed + offset)


def add_upright_bass(left: list[float], right: list[float], start: float, note: str, gain: float, pan: float) -> None:
    freq = note_frequency(note)
    duration = 0.92
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        age = i / SAMPLE_RATE - start
        env = hit_env(age, duration, 0.010, 0.14, 2.9)
        pluck = sine(freq, age) * 0.58 + sine(freq * 2.0, age, 0.55) * 0.14 + sine(freq * 0.5, age, 0.2) * 0.10
        add(left, right, i, pluck * env * gain, pan)


def add_vibraphone(left: list[float], right: list[float], start: float, note: str, gain: float, pan: float, seed: int) -> None:
    freq = note_frequency(note)
    duration = 1.45
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        t = i / SAMPLE_RATE
        age = t - start
        env = hit_env(age, duration, 0.005, 0.22, 2.15)
        trem = 0.86 + 0.14 * sine(6.2, age, 0.4)
        hit = noise(i, seed) * math.exp(-age * 36.0) * 0.020
        tone = (
            sine(freq, age) * 0.42
            + sine(freq * 2.0, age, 0.6) * 0.13
            + sine(freq * 3.03, age, 1.2) * 0.055
            + hit
        )
        add(left, right, i, tone * trem * env * gain, pan)


def add_rim_click(left: list[float], right: list[float], start: float, gain: float, pan: float, seed: int) -> None:
    duration = 0.090
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    previous = 0.0
    for i in range(start_i, end_i):
        age = i / SAMPLE_RATE - start
        raw = noise(i, seed)
        bright = raw - previous * 0.76
        previous = raw
        env = hit_env(age, duration, 0.001, 0.014, 26.0)
        wood = sine(1880.0, age) * 0.11 + sine(720.0, age, 0.4) * 0.045
        add(left, right, i, (bright * 0.23 + wood) * env * gain, pan)


def add_brush(left: list[float], right: list[float], start: float, gain: float, pan: float, seed: int) -> None:
    duration = 0.095
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    previous = 0.0
    for i in range(start_i, end_i):
        age = i / SAMPLE_RATE - start
        raw = noise(i, seed)
        bright = raw - previous * 0.82
        previous = raw
        env = hit_env(age, duration, 0.001, 0.018, 20.0)
        add(left, right, i, bright * env * gain, pan)


def add_soft_kick(left: list[float], right: list[float], start: float, gain: float, pan: float, seed: int) -> None:
    duration = 0.20
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        age = i / SAMPLE_RATE - start
        env = hit_env(age, duration, 0.003, 0.040, 11.0)
        drop = 80.0 - 28.0 * smoothstep(age / duration)
        thump = sine(drop, age) * 0.54 + sine(drop * 0.5, age, 0.3) * 0.16 + noise(i, seed) * 0.040
        add(left, right, i, thump * env * gain, pan)


def add_abyss_chime(left: list[float], right: list[float], start: float, note: str, gain: float, pan: float, seed: int) -> None:
    freq = note_frequency(note)
    duration = 1.80
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        age = i / SAMPLE_RATE - start
        env = hit_env(age, duration, 0.006, 0.32, 2.25)
        tone = sine(freq, age) * 0.38 + sine(freq * 2.02, age, 0.5) * 0.11 + sine(freq * 2.71, age, 1.2) * 0.055
        shimmer = noise(i, seed) * math.exp(-age * 8.0) * 0.012
        add(left, right, i, (tone + shimmer) * env * gain, pan)


def add_room_hiss(left: list[float], right: list[float], start: float, duration: float, gain: float, pan: float, seed: int) -> None:
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    low = 0.0
    previous = 0.0
    for i in range(start_i, end_i):
        age = i / SAMPLE_RATE - start
        raw = noise(i, seed)
        low = low * 0.95 + raw * 0.05
        bright = raw - previous * 0.84
        previous = raw
        env = smoothstep(age / 0.50) * smoothstep((duration - age) / 0.55)
        add(left, right, i, (low * 0.44 + bright * 0.10) * env * gain, pan)


def write_wav(path: Path, left: list[float], right: list[float], peak_target: float = 0.80) -> None:
    peak = max(max(abs(v) for v in left), max(abs(v) for v in right), 0.0001)
    scale = min(peak_target / peak, 14.0)
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


def bossa_progression() -> tuple[dict[str, tuple[str, ...] | str], ...]:
    return (
        {"bass": "F2", "fifth": "C3", "chord": ("F3", "Ab3", "C4", "Eb4"), "melody": ("C5", "Ab4", "G4", "F4")},
        {"bass": "Bb1", "fifth": "F2", "chord": ("Bb2", "D3", "F3", "Ab3"), "melody": ("D5", "C5", "Ab4", "F4")},
        {"bass": "Eb2", "fifth": "Bb2", "chord": ("Eb3", "G3", "Bb3", "D4"), "melody": ("Bb4", "D5", "C5", "G4")},
        {"bass": "Ab1", "fifth": "Eb2", "chord": ("Ab2", "C3", "Eb3", "G3"), "melody": ("C5", "Bb4", "G4", "Eb4")},
        {"bass": "D2", "fifth": "A2", "chord": ("D3", "F3", "A3", "C4"), "melody": ("F4", "A4", "C5", "A4")},
        {"bass": "G1", "fifth": "D2", "chord": ("G2", "B2", "D3", "F3"), "melody": ("B4", "G4", "F4", "D4")},
        {"bass": "C2", "fifth": "G2", "chord": ("C3", "Eb3", "G3", "Bb3"), "melody": ("G4", "Bb4", "D5", "C5")},
        {"bass": "C2", "fifth": "G2", "chord": ("C3", "E3", "G3", "Bb3"), "melody": ("Bb4", "G4", "E4", "C4")},
    )


def render_abyssal_grimoire_spire() -> Path:
    bpm = 96.0
    bars = 24
    beat = 60.0 / bpm
    bar = beat * 4.0
    duration = bars * bar
    count = int(duration * SAMPLE_RATE)
    left = [0.0] * count
    right = [0.0] * count
    progression = bossa_progression()

    for i in range(count):
        t = i / SAMPLE_RATE
        section = min(2, int(t / (8 * bar)))
        env = edge_env(t, duration, 0.75) * [0.80, 0.97, 1.05][section]
        distant_air = noise(i, 51) * 0.0018 + sine(47.0, t, 0.4) * 0.004
        add(left, right, i, distant_air * env, 0.5 + 0.020 * sine(0.031, t))

    chord_offsets = (0.50, 1.18, 2.00, 2.72, 3.45)
    bass_offsets = (0.00, 1.50, 2.00, 3.25)
    for b in range(bars):
        base = b * bar
        section = min(2, b // 8)
        chord = progression[b % len(progression)]
        add_upright_bass(left, right, base + bass_offsets[0] * beat, chord["bass"], 0.036 + section * 0.006, 0.48)
        add_upright_bass(left, right, base + bass_offsets[1] * beat, chord["fifth"], 0.024 + section * 0.004, 0.52)
        add_upright_bass(left, right, base + bass_offsets[2] * beat, chord["bass"], 0.030 + section * 0.005, 0.50)
        add_upright_bass(left, right, base + bass_offsets[3] * beat, chord["fifth"], 0.018 + section * 0.003, 0.54)
        for step, offset in enumerate(chord_offsets):
            gain = (0.022 + section * 0.004) * (1.08 if step in (0, 3) else 0.82)
            add_guitar_chord(left, right, base + offset * beat, chord["chord"], gain, 0.42 if step % 2 == 0 else 0.58, 100 + b * 17 + step)
        add_soft_kick(left, right, base, 0.024 + section * 0.004, 0.49, 200 + b)
        add_rim_click(left, right, base + beat * 1.5, 0.026 + section * 0.004, 0.38, 230 + b)
        add_rim_click(left, right, base + beat * 3.0, 0.020 + section * 0.004, 0.62, 250 + b)
        for step in range(8):
            add_brush(left, right, base + step * beat * 0.5 + 0.012, 0.007 + section * 0.002, 0.34 + (step % 4) * 0.10, 300 + b * 9 + step)
        for step, note in enumerate(chord["melody"]):
            if section >= 1 or step in (0, 2):
                add_vibraphone(left, right, base + (0.75 + step * 0.72) * beat, note, 0.026 + section * 0.005, 0.58 if step % 2 else 0.42, 400 + b * 13 + step)
        if b % 4 == 3:
            add_abyss_chime(left, right, base + beat * 3.05, chord["melody"][0], 0.012 + section * 0.003, 0.70, 500 + b)
            add_room_hiss(left, right, base + beat, beat * 2.2, 0.010 + section * 0.003, 0.66, 530 + b)

    path = BGM_DIR / "dungeon_abyssal_grimoire_spire_loop.wav"
    write_wav(path, left, right)
    return path


def render_abyssal_grimoire_spire_boss() -> Path:
    bpm = 116.0
    bars = 16
    beat = 60.0 / bpm
    bar = beat * 4.0
    duration = bars * bar
    count = int(duration * SAMPLE_RATE)
    left = [0.0] * count
    right = [0.0] * count
    progression = bossa_progression()[:4]

    for i in range(count):
        t = i / SAMPLE_RATE
        env = edge_env(t, duration, 0.48)
        distant_air = noise(i, 701) * 0.0023 + sine(54.0, t, 0.6) * 0.005
        add(left, right, i, distant_air * env, 0.5 + 0.025 * sine(0.046, t))

    chord_offsets = (0.38, 1.00, 1.72, 2.38, 3.10, 3.62)
    for b in range(bars):
        base = b * bar
        chord = progression[b % len(progression)]
        add_upright_bass(left, right, base, chord["bass"], 0.050, 0.48)
        add_upright_bass(left, right, base + beat * 1.35, chord["fifth"], 0.032, 0.52)
        add_upright_bass(left, right, base + beat * 2.0, chord["bass"], 0.042, 0.50)
        add_upright_bass(left, right, base + beat * 3.2, chord["fifth"], 0.026, 0.54)
        for step, offset in enumerate(chord_offsets):
            gain = 0.030 if step in (0, 3, 5) else 0.022
            add_guitar_chord(left, right, base + offset * beat, chord["chord"], gain, 0.40 if step % 2 == 0 else 0.60, 900 + b * 19 + step)
        add_soft_kick(left, right, base, 0.040, 0.49, 1000 + b)
        add_soft_kick(left, right, base + beat * 2.0, 0.030, 0.51, 1015 + b)
        add_rim_click(left, right, base + beat * 1.0, 0.030, 0.38, 1030 + b)
        add_rim_click(left, right, base + beat * 2.65, 0.038, 0.62, 1060 + b)
        for step in range(8):
            add_brush(left, right, base + step * beat * 0.5 + 0.012, 0.011 if step % 2 else 0.008, 0.34 + (step % 4) * 0.10, 1100 + b * 9 + step)
        for step, note in enumerate(chord["melody"]):
            add_vibraphone(left, right, base + (0.48 + step * 0.70) * beat, note, 0.040, 0.58 if step % 2 else 0.42, 1200 + b * 13 + step)
        if b % 2 == 1:
            add_abyss_chime(left, right, base + beat * 3.0, chord["melody"][1], 0.020, 0.70, 1300 + b)
            add_room_hiss(left, right, base + beat * 1.0, beat * 1.8, 0.016, 0.68 if b % 4 == 1 else 0.32, 1330 + b)

    path = BGM_DIR / "dungeon_abyssal_grimoire_spire_boss_loop.wav"
    write_wav(path, left, right)
    return path


def main() -> None:
    for path in (render_abyssal_grimoire_spire(), render_abyssal_grimoire_spire_boss()):
        print(f"Wrote {path}")


if __name__ == "__main__":
    main()
