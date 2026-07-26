#!/usr/bin/env python3
"""Regenerate Curse Library music as a fast heroic grimoire battle theme.

The target is "duel on a cursed bridge in a forbidden library": rapid ostinato,
harpsichord-like plucks, organ lead, page flutter, and ritual percussion. It
does not copy any existing melody.
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
    value = math.sin((index + seed * 131) * 12.9898) * 43758.5453
    return (value - math.floor(value)) * 2.0 - 1.0


def pan_gains(pan: float) -> tuple[float, float]:
    pan = max(0.0, min(1.0, pan))
    return math.cos(pan * math.pi * 0.5), math.sin(pan * math.pi * 0.5)


def add(left: list[float], right: list[float], index: int, sample: float, pan: float) -> None:
    lgain, rgain = pan_gains(pan)
    left[index] += sample * lgain
    right[index] += sample * rgain


def edge_env(t: float, duration: float, fade: float = 0.42) -> float:
    return smoothstep(t / fade) * smoothstep((duration - t) / fade)


def hit_env(age: float, duration: float, attack: float, release: float, decay: float) -> float:
    if age < 0.0 or age > duration:
        return 0.0
    return smoothstep(age / max(0.001, attack)) * smoothstep((duration - age) / max(0.001, release)) * math.exp(-age * decay)


def add_harpsichord(left: list[float], right: list[float], start: float, note: str, gain: float, pan: float) -> None:
    freq = note_frequency(note)
    duration = 0.34
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        t = i / SAMPLE_RATE
        age = t - start
        env = hit_env(age, duration, 0.002, 0.045, 8.2)
        pluck = (
            sine(freq, age) * 0.48
            + sine(freq * 2.0, age, 0.25) * 0.18
            + sine(freq * 3.0, age, 0.70) * 0.06
            + noise(i, 12) * 0.018
        )
        add(left, right, i, pluck * env * gain, pan)


def add_organ(left: list[float], right: list[float], start: float, note: str, duration: float, gain: float, pan: float) -> None:
    freq = note_frequency(note)
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        t = i / SAMPLE_RATE
        age = t - start
        env = hit_env(age, duration, 0.018, 0.050, 0.16)
        vibrato = 1.0 + 0.0032 * sine(5.3, age)
        f = freq * vibrato
        lead = (
            sine(f, age) * 0.58
            + sine(f * 2.0, age, 0.1) * 0.18
            + sine(f * 3.0, age, 0.4) * 0.07
            + math.tanh(sine(f * 0.5, age) * 1.5) * 0.08
        )
        add(left, right, i, lead * env * gain, pan)


def add_low_string(left: list[float], right: list[float], start: float, note: str, duration: float, gain: float) -> None:
    freq = note_frequency(note)
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        t = i / SAMPLE_RATE
        age = t - start
        env = hit_env(age, duration, 0.040, 0.090, 0.55)
        bow = sine(freq, t) * 0.40 + sine(freq * 2.0, t, 0.7) * 0.11 + sine(freq * 0.5, t, 1.1) * 0.16
        add(left, right, i, bow * env * gain, 0.5)


def add_book_slam(left: list[float], right: list[float], start: float, gain: float, pan: float, seed: int) -> None:
    duration = 0.18
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        t = i / SAMPLE_RATE
        age = t - start
        env = hit_env(age, duration, 0.002, 0.035, 15.0)
        thump = sine(92.0 * (1.0 - 0.35 * smoothstep(age / duration)), age) * 0.50
        paper = noise(i, seed) * 0.28
        add(left, right, i, (thump + paper) * env * gain, pan)


def add_page_flutter(left: list[float], right: list[float], start: float, duration: float, gain: float, pan: float, seed: int) -> None:
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    low = 0.0
    previous = 0.0
    for i in range(start_i, end_i):
        t = i / SAMPLE_RATE
        age = t - start
        raw = noise(i, seed)
        low = low * 0.88 + raw * 0.12
        bright = raw - previous * 0.68
        previous = raw
        flutter_gate = 0.52 + 0.48 * max(0.0, sine(9.0 + seed * 0.01, t))
        env = smoothstep(age / 0.12) * smoothstep((duration - age) / 0.18)
        add(left, right, i, (bright * 0.30 + low * 0.42) * flutter_gate * env * gain, pan)


def add_bell(left: list[float], right: list[float], start: float, note: str, gain: float, pan: float) -> None:
    freq = note_frequency(note)
    duration = 1.10
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        t = i / SAMPLE_RATE
        age = t - start
        env = hit_env(age, duration, 0.006, 0.16, 3.2)
        tone = sine(freq, age) * 0.58 + sine(freq * 2.01, age, 0.5) * 0.18 + sine(freq * 3.04, age, 1.2) * 0.07
        add(left, right, i, tone * env * gain, pan)


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
            l_sample = int(max(-1.0, min(1.0, math.tanh(l_value * scale * 1.06))) * 32767)
            r_sample = int(max(-1.0, min(1.0, math.tanh(r_value * scale * 1.06))) * 32767)
            frames += struct.pack("<h", l_sample)
            frames += struct.pack("<h", r_sample)
        wav.writeframes(frames)


def render_curse_library() -> Path:
    bpm = 148.0
    bars = 40
    beat = 60.0 / bpm
    bar = beat * 4.0
    duration = bars * bar
    count = int(duration * SAMPLE_RATE)
    left = [0.0] * count
    right = [0.0] * count

    roots = ("E2", "C2", "D2", "B1", "G1", "D2", "C2", "B1")
    chords = (
        ("E3", "G3", "B3", "F#4"),
        ("C3", "E3", "G3", "B3"),
        ("D3", "F#3", "A3", "E4"),
        ("B2", "D#3", "F#3", "A3"),
        ("G2", "B2", "D3", "F#3"),
        ("D3", "F#3", "A3", "E4"),
        ("C3", "E3", "G3", "B3"),
        ("B2", "D#3", "F#3", "A3"),
    )
    root_freqs = [note_frequency(n) for n in roots]
    chord_freqs = [[note_frequency(n) for n in group] for group in chords]

    for i in range(count):
        t = i / SAMPLE_RATE
        b = min(bars - 1, int(t / bar))
        section = min(4, b // 8)
        local = t - b * bar
        env = edge_env(t, duration, 0.6) * [0.82, 0.98, 1.08, 1.00, 0.90][section]
        root = root_freqs[b % 8]
        chord = chord_freqs[b % 8]
        gallop = 0.52 + 0.48 * smoothstep((beat * 0.35 - (local % (beat * 0.5))) / 0.045)
        bass = (sine(root * 0.5, t) * 0.36 + sine(root, t, 0.5) * 0.17 + sine(root * 2.0, t) * 0.05) * gallop
        forbidden_choir = (
            sine(chord[0], t, 0.2) * 0.14
            + sine(chord[2], t, 1.1) * 0.13
            + sine(chord[3] * 1.5, t, 0.7) * 0.055
        )
        page_air = noise(i, 44) * 0.0035
        add(left, right, i, (bass * 0.38 + forbidden_choir * 0.34 + page_air) * env, 0.5 + 0.04 * sine(0.07, t))

    ostinato = ("E4", "B3", "G4", "B3", "F#4", "B3", "G4", "B3")
    answer = ("B4", "D5", "E5", "G5", "F#5", "E5", "D5", "B4")
    bridge_lead = (
        ("E5", 0.0), ("G5", 0.5), ("A5", 1.0), ("B5", 1.5),
        ("D6", 2.0), ("B5", 2.5), ("A5", 3.0), ("G5", 3.5),
        ("F#5", 4.0), ("A5", 4.5), ("B5", 5.0), ("D6", 5.5),
        ("E6", 6.0), ("D6", 6.5), ("B5", 7.0), ("A5", 7.5),
    )

    for b in range(bars):
        base = b * bar
        section = min(4, b // 8)
        for step in range(16):
            note = ostinato[step % len(ostinato)]
            pan = 0.36 if step % 2 == 0 else 0.64
            add_harpsichord(left, right, base + step * beat * 0.25, note, 0.024 + section * 0.003, pan)
        if section >= 1:
            for step in range(8):
                add_harpsichord(left, right, base + (step * 0.5 + 0.25) * beat, answer[(step + b) % len(answer)], 0.018 + section * 0.002, 0.58)
        add_book_slam(left, right, base, 0.070 + section * 0.008, 0.48, 100 + b)
        add_book_slam(left, right, base + beat * 2.0, 0.052 + section * 0.006, 0.58, 200 + b)
        if b % 2 == 1:
            add_page_flutter(left, right, base + beat * 1.25, 0.72, 0.018 + section * 0.003, 0.68, 300 + b)
        if b % 8 in (0, 4):
            add_low_string(left, right, base, roots[b % 8], bar * 2.0, 0.052 + section * 0.004)
            add_bell(left, right, base + beat * 3.0, "B5" if b % 8 == 0 else "E6", 0.040 + section * 0.003, 0.42)
        if section >= 2 and b % 8 in (2, 6):
            add_bell(left, right, base + beat * 1.5, "F#6", 0.032, 0.72)

    for phrase in range(5):
        phrase_start = phrase * 8 * bar
        lead_gain = 0.045 + phrase * 0.004
        for note, offset in bridge_lead:
            add_organ(left, right, phrase_start + offset * beat, note, beat * 0.44, lead_gain, 0.48)

    path = BGM_DIR / "dungeon_curse_library_loop.wav"
    write_wav(path, left, right)
    return path


def render_curse_library_boss() -> Path:
    bpm = 164.0
    bars = 20
    beat = 60.0 / bpm
    bar = beat * 4.0
    duration = bars * bar
    count = int(duration * SAMPLE_RATE)
    left = [0.0] * count
    right = [0.0] * count
    roots = ("E2", "D2", "C2", "B1")
    root_freqs = [note_frequency(n) for n in roots]
    chords = (
        ("E4", "G4", "B4"),
        ("D4", "F#4", "A4"),
        ("C4", "E4", "G4"),
        ("B3", "D#4", "F#4"),
    )
    chord_freqs = [[note_frequency(note) for note in group] for group in chords]

    for i in range(count):
        t = i / SAMPLE_RATE
        b = min(bars - 1, int(t / bar))
        local = t - b * bar
        env = edge_env(t, duration, 0.35)
        root = root_freqs[b % 4]
        chord = chord_freqs[b % 4]
        gallop = 0.48 + 0.52 * smoothstep((beat * 0.28 - (local % (beat * 0.5))) / 0.035)
        bass = (sine(root * 0.5, t) * 0.44 + sine(root, t, 0.4) * 0.20 + sine(root * 2.0, t) * 0.08) * gallop
        choir = sine(chord[1], t, 0.5) * 0.055 + sine(chord[2], t, 1.1) * 0.060
        grit = math.tanh((noise(i, 700) * 0.18 + sine(root * 3.0, t)) * 1.3) * 0.035
        add(left, right, i, (bass * 0.45 + choir * 0.30 + grit) * env, 0.5)

    ostinatos = (
        ("E4", "B3", "G4", "B3", "D5", "B3", "F#4", "B3"),
        ("D4", "A3", "F#4", "A3", "C5", "A3", "E4", "A3"),
        ("C4", "G3", "E4", "G3", "B4", "G3", "D4", "G3"),
        ("B3", "F#3", "D#4", "F#3", "A4", "F#3", "C#4", "F#3"),
    )
    boss_lead = (
        ("E5", 0.0), ("G5", 0.5), ("B5", 1.0), ("D6", 1.5),
        ("E6", 2.0), ("D6", 2.5), ("B5", 3.0), ("A5", 3.5),
    )
    for b in range(bars):
        base = b * bar
        ostinato = ostinatos[b % 4]
        for step in range(16):
            add_harpsichord(left, right, base + step * beat * 0.25, ostinato[step % 8], 0.030, 0.34 if step % 2 == 0 else 0.66)
        add_book_slam(left, right, base, 0.100, 0.46, 800 + b)
        add_book_slam(left, right, base + beat * 1.5, 0.058, 0.62, 850 + b)
        add_book_slam(left, right, base + beat * 2.5, 0.082, 0.52, 900 + b)
        if b % 2 == 0:
            add_page_flutter(left, right, base + beat * 2.0, 0.82, 0.030, 0.70, 920 + b)
        if b % 4 == 3:
            add_bell(left, right, base + beat * 3.0, "E6", 0.052, 0.42)
        if b % 4 == 0:
            for note, offset in boss_lead:
                add_organ(left, right, base + offset * beat, note, beat * 0.42, 0.058, 0.48)

    path = BGM_DIR / "dungeon_curse_library_boss_loop.wav"
    write_wav(path, left, right)
    return path


def main() -> None:
    for path in (render_curse_library(), render_curse_library_boss()):
        print(f"Wrote {path}")


if __name__ == "__main__":
    main()
