#!/usr/bin/env python3
"""Generate a draft home BGM loop for WitchTowerGame.

This is intentionally simple and reproducible: no external audio libraries,
just procedural synthesis written to a Unity-importable WAV file.
"""

from __future__ import annotations

import math
import os
import random
import struct
import wave


SAMPLE_RATE = 44100
BPM = 80
BARS = 24
BEATS_PER_BAR = 4
SECONDS_PER_BEAT = 60.0 / BPM
BAR_SECONDS = BEATS_PER_BAR * SECONDS_PER_BEAT
DURATION = BARS * BAR_SECONDS

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUTPUT = os.path.join(
    ROOT,
    "WitchTowerGame",
    "Assets",
    "Resources",
    "Audio",
    "BGM",
    "home_theme_loop.wav",
)


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
    semitone_from_a4 = NOTE_INDEX[name] + (octave - 4) * 12
    return 440.0 * (2.0 ** (semitone_from_a4 / 12.0))


def sine(freq: float, t: float, phase: float = 0.0) -> float:
    return math.sin((2.0 * math.pi * freq * t) + phase)


def soft_square(freq: float, t: float) -> float:
    base = sine(freq, t)
    third = sine(freq * 3.0, t) / 3.0
    fifth = sine(freq * 5.0, t) / 5.0
    return math.tanh((base + third + fifth) * 0.68)


def smoothstep(x: float) -> float:
    x = max(0.0, min(1.0, x))
    return x * x * (3.0 - 2.0 * x)


def chord_envelope(local_time: float, attack_seconds: float = 0.85, release_seconds: float = 1.05) -> float:
    attack = smoothstep(local_time / attack_seconds)
    release = smoothstep((BAR_SECONDS - local_time) / release_seconds)
    return attack * release


def global_edge_envelope(t: float) -> float:
    # Keeps the loop boundary click-free. This draft accepts a small breath at
    # the loop point; the finished DAW render should use a true seamless loop.
    fade = 0.72
    return smoothstep(t / fade) * smoothstep((DURATION - t) / fade)


def bell_envelope(age: float, decay: float) -> float:
    if age < 0.0:
        return 0.0
    return math.exp(-age * decay) * smoothstep(age / 0.012)


def add_bell(left: list[float], right: list[float], start: float, note: str, gain: float, pan: float) -> None:
    freq = note_frequency(note)
    max_age = 3.1
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + max_age) * SAMPLE_RATE))
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        env = bell_envelope(age, 2.2)
        tone = (
            sine(freq, age) * 0.76
            + sine(freq * 2.01, age, 0.3) * 0.22
            + sine(freq * 3.02, age, 1.1) * 0.09
        )
        shimmer = sine(freq * 0.5, age) * 0.08
        sample = (tone + shimmer) * env * gain
        left[i] += sample * (1.0 - pan)
        right[i] += sample * pan


def add_soft_pluck(left: list[float], right: list[float], start: float, note: str, gain: float, pan: float) -> None:
    freq = note_frequency(note)
    max_age = 1.15
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + max_age) * SAMPLE_RATE))
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        env = bell_envelope(age, 4.7)
        tone = sine(freq, age) * 0.64 + sine(freq * 2.0, age, 0.4) * 0.16
        sample = tone * env * gain
        left[i] += sample * (1.0 - pan)
        right[i] += sample * pan


def add_low_pulse(left: list[float], right: list[float], start: float, freq: float, gain: float) -> None:
    max_age = 0.42
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + max_age) * SAMPLE_RATE))
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        env = math.exp(-age * 7.5) * smoothstep(age / 0.02)
        sample = sine(freq, age) * env * gain
        left[i] += sample * 0.52
        right[i] += sample * 0.48


def write_wav(path: str, left: list[float], right: list[float]) -> None:
    peak = max(max(abs(x) for x in left), max(abs(x) for x in right), 0.0001)
    scale = min(0.88 / peak, 3.2)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as wav:
        wav.setnchannels(2)
        wav.setsampwidth(2)
        wav.setframerate(SAMPLE_RATE)
        frames = bytearray()
        for l, r in zip(left, right):
            frames += struct.pack("<h", int(max(-1.0, min(1.0, l * scale)) * 32767))
            frames += struct.pack("<h", int(max(-1.0, min(1.0, r * scale)) * 32767))
        wav.writeframes(frames)


def main() -> None:
    random.seed(62030)
    sample_count = int(DURATION * SAMPLE_RATE)
    left = [0.0] * sample_count
    right = [0.0] * sample_count

    base_chords = [
        ("D2", ["D3", "F3", "A3", "E4"]),
        ("D2", ["D3", "F3", "A3", "E4"]),
        ("Bb2", ["Bb2", "D3", "F3", "A3"]),
        ("C3", ["C3", "E3", "G3", "D4"]),
        ("G2", ["G2", "Bb2", "D3", "F3"]),
        ("Bb2", ["Bb2", "D3", "F3", "A3"]),
        ("A2", ["A2", "C3", "E3", "G3", "Bb3"]),
        ("D2", ["D3", "F3", "A3", "E4"]),
    ]
    section_chords = [
        base_chords,
        [
            ("F2", ["F3", "A3", "C4", "E4"]),
            ("D2", ["D3", "F3", "A3", "E4"]),
            ("Bb2", ["Bb2", "D3", "F3", "C4"]),
            ("C3", ["C3", "E3", "G3", "D4"]),
            ("G2", ["G2", "Bb2", "D3", "A3"]),
            ("Bb2", ["Bb2", "D3", "F3", "A3"]),
            ("A2", ["A2", "C3", "E3", "G3", "Bb3"]),
            ("D2", ["D3", "F3", "A3", "E4"]),
        ],
        [
            ("D2", ["D3", "F3", "A3", "E4"]),
            ("A2", ["A2", "C3", "E3", "G3"]),
            ("Bb2", ["Bb2", "D3", "F3", "A3"]),
            ("C3", ["C3", "E3", "G3", "D4"]),
            ("G2", ["G2", "Bb2", "D3", "F3"]),
            ("Bb2", ["Bb2", "D3", "F3", "A3"]),
            ("A2", ["A2", "C3", "E3", "G3", "Bb3"]),
            ("D2", ["D3", "F3", "A3", "E4"]),
        ],
    ]

    for i in range(sample_count):
        t = i / SAMPLE_RATE
        bar_index = min(BARS - 1, int(t / BAR_SECONDS))
        section = min(2, bar_index // 8)
        section_position = bar_index % 8
        local = t - (bar_index * BAR_SECONDS)
        root_note, chord_notes = section_chords[section][section_position]
        section_gain = [0.92, 1.05, 0.96][section]
        env = chord_envelope(local) * global_edge_envelope(t) * section_gain

        root = note_frequency(root_note)
        drone = (
            sine(root, t) * 0.36
            + sine(root * 0.5, t, 0.2) * 0.30
            + sine(root * 2.0, t, 0.5) * 0.08
        )

        pad = 0.0
        for note_i, note in enumerate(chord_notes):
            freq = note_frequency(note)
            detune = 1.0 + (note_i - 1.5) * 0.0015
            slow = 0.74 + 0.08 * sine(0.05 + note_i * 0.013, t)
            pad += soft_square(freq * detune, t + note_i * 0.021) * slow
            pad += sine(freq * 1.997, t, note_i * 0.4) * 0.10
        pad /= max(1, len(chord_notes))

        choir = 0.0
        if section >= 1:
            choir_freq = note_frequency("A3" if section == 1 else "D4")
            choir = (
                sine(choir_freq, t, 0.25) * 0.16
                + sine(choir_freq * 1.5, t, 1.1) * 0.08
            ) * (0.5 + 0.5 * sine(0.025, t))

        wind = (random.random() * 2.0 - 1.0) * 0.010
        shimmer = sine(note_frequency("D5"), t, 0.6) * 0.012 * (0.5 + 0.5 * sine(0.09, t))
        sample = (drone * 0.18 + pad * 0.24 + choir * 0.18 + wind + shimmer) * env

        pan_sway = 0.5 + 0.08 * sine(0.035, t)
        left[i] += sample * (1.04 - pan_sway)
        right[i] += sample * (0.96 + pan_sway)

    motif = [
        (0.10, "D5", 0.110, 0.38),
        (BAR_SECONDS * 1.0 + 1.50, "A4", 0.075, 0.66),
        (BAR_SECONDS * 2.0 + 0.00, "F4", 0.080, 0.30),
        (BAR_SECONDS * 3.0 + 1.50, "G4", 0.066, 0.70),
        (BAR_SECONDS * 4.0 + 0.00, "D4", 0.075, 0.44),
        (BAR_SECONDS * 5.0 + 1.50, "A4", 0.070, 0.62),
        (BAR_SECONDS * 6.0 + 0.00, "C5", 0.070, 0.33),
        (BAR_SECONDS * 7.0 + 1.50, "E4", 0.060, 0.60),
    ]
    for section in range(3):
        section_offset = section * 8 * BAR_SECONDS
        gain_scale = [0.92, 1.12, 0.86][section]
        for start, note, gain, pan in motif:
            add_bell(left, right, section_offset + start, note, gain * gain_scale, pan)

    pluck_patterns = [
        [],
        [("D4", 0.75), ("F4", 1.50), ("A4", 2.25), ("E5", 3.00)],
        [("A4", 0.75), ("F4", 1.50), ("D4", 2.25)],
    ]
    for section, pattern in enumerate(pluck_patterns):
        section_offset = section * 8 * BAR_SECONDS
        for bar in range(8):
            for note, beat in pattern:
                pan = 0.34 if (bar + len(note)) % 2 == 0 else 0.66
                add_soft_pluck(left, right, section_offset + bar * BAR_SECONDS + beat * SECONDS_PER_BEAT, note, 0.024, pan)

    for bar in range(BARS):
        start = bar * BAR_SECONDS
        section = min(2, bar // 8)
        section_position = bar % 8
        chord_root = section_chords[section][section_position][0]
        add_low_pulse(left, right, start, note_frequency(chord_root) * 0.5, 0.050 + section * 0.006)
        if section_position in (3, 6):
            add_low_pulse(left, right, start + BAR_SECONDS * 0.5, note_frequency("A1"), 0.030)

    write_wav(OUTPUT, left, right)
    print(f"Wrote {OUTPUT}")
    print(f"Duration: {DURATION:.2f}s, BPM: {BPM}, bars: {BARS}, sample rate: {SAMPLE_RATE}")


if __name__ == "__main__":
    main()
