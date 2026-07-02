#!/usr/bin/env python3
"""Generate a draft normal battle BGM loop for WitchTowerGame."""

from __future__ import annotations

import math
import os
import random
import struct
import wave


SAMPLE_RATE = 44100
BPM = 120
BARS = 32
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
    "battle_normal_loop.wav",
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


def smoothstep(x: float) -> float:
    x = max(0.0, min(1.0, x))
    return x * x * (3.0 - 2.0 * x)


def noise(sample_index: int) -> float:
    value = math.sin(sample_index * 12.9898) * 43758.5453
    return (value - math.floor(value)) * 2.0 - 1.0


def edge_envelope(t: float) -> float:
    fade = 0.35
    return smoothstep(t / fade) * smoothstep((DURATION - t) / fade)


def pulse_envelope(age: float, duration: float, decay: float) -> float:
    if age < 0.0 or age > duration:
        return 0.0
    return smoothstep(age / 0.012) * smoothstep((duration - age) / 0.035) * math.exp(-age * decay)


def add_kick(left: list[float], right: list[float], start: float, gain: float) -> None:
    duration = 0.34
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        env = pulse_envelope(age, duration, 8.5)
        freq = 92.0 - 42.0 * smoothstep(age / duration)
        sample = sine(freq, age) * env * gain
        left[i] += sample * 0.52
        right[i] += sample * 0.48


def add_tom(left: list[float], right: list[float], start: float, freq: float, gain: float, pan: float) -> None:
    duration = 0.28
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        env = pulse_envelope(age, duration, 7.0)
        sample = (sine(freq, age) * 0.72 + sine(freq * 1.5, age) * 0.12) * env * gain
        left[i] += sample * (1.0 - pan)
        right[i] += sample * pan


def add_hat(left: list[float], right: list[float], start: float, gain: float, pan: float) -> None:
    duration = 0.07
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        env = pulse_envelope(age, duration, 25.0)
        metallic = sine(6400.0, age) * 0.16 + sine(8700.0, age, 0.4) * 0.12
        sample = (noise(i) * 0.50 + metallic) * env * gain
        left[i] += sample * (1.0 - pan)
        right[i] += sample * pan


def add_pluck(left: list[float], right: list[float], start: float, note: str, gain: float, pan: float) -> None:
    freq = note_frequency(note)
    duration = 0.62
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        env = pulse_envelope(age, duration, 4.5)
        bite = sine(freq * 2.0, age, 0.2) * 0.16
        body = sine(freq, age) * 0.58 + sine(freq * 0.5, age) * 0.12
        sample = (body + bite + noise(i) * 0.015) * env * gain
        left[i] += sample * (1.0 - pan)
        right[i] += sample * pan


def add_bell(left: list[float], right: list[float], start: float, note: str, gain: float, pan: float) -> None:
    freq = note_frequency(note)
    duration = 1.8
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        env = pulse_envelope(age, duration, 2.6)
        tone = sine(freq, age) * 0.72 + sine(freq * 2.01, age, 0.5) * 0.18
        sample = tone * env * gain
        left[i] += sample * (1.0 - pan)
        right[i] += sample * pan


def write_wav(path: str, left: list[float], right: list[float]) -> None:
    peak = max(max(abs(x) for x in left), max(abs(x) for x in right), 0.0001)
    scale = min(0.88 / peak, 2.8)
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
    random.seed(20260630)
    sample_count = int(DURATION * SAMPLE_RATE)
    left = [0.0] * sample_count
    right = [0.0] * sample_count

    chords = [
        ("D2", ["D3", "F3", "A3", "E4"]),
        ("D2", ["D3", "F3", "A3", "E4"]),
        ("Bb1", ["Bb2", "D3", "F3", "A3"]),
        ("C2", ["C3", "E3", "G3", "D4"]),
        ("G1", ["G2", "Bb2", "D3", "F3"]),
        ("Bb1", ["Bb2", "D3", "F3", "A3"]),
        ("A1", ["A2", "C3", "E3", "G3"]),
        ("D2", ["D3", "F3", "A3", "E4"]),
    ]

    for i in range(sample_count):
        t = i / SAMPLE_RATE
        bar_index = min(BARS - 1, int(t / BAR_SECONDS))
        section = bar_index // 8
        local_bar = bar_index % 8
        local = t - bar_index * BAR_SECONDS
        root_note, chord_notes = chords[local_bar]
        section_gain = [0.88, 1.00, 1.08, 0.96][section]
        env = edge_envelope(t) * section_gain

        root = note_frequency(root_note)
        bass_gate = 0.58 + 0.42 * smoothstep((SECONDS_PER_BEAT * 0.5 - (local % SECONDS_PER_BEAT)) / 0.11)
        bass = (
            sine(root, t) * 0.32
            + sine(root * 2.0, t, 0.3) * 0.10
            + sine(root * 0.5, t, 0.1) * 0.16
        ) * bass_gate

        pad = 0.0
        for note_i, note in enumerate(chord_notes):
            freq = note_frequency(note)
            pad += sine(freq, t, note_i * 0.37) * (0.21 + section * 0.018)
            pad += sine(freq * 1.997, t, note_i * 0.6) * 0.045
        pad /= len(chord_notes)

        tension = sine(note_frequency("A4"), t, 0.8) * 0.025 * (0.5 + 0.5 * sine(0.065, t))
        wind = noise(i) * 0.006
        sample = (bass * 0.26 + pad * 0.30 + tension + wind) * env

        sway = 0.5 + 0.06 * sine(0.055, t)
        left[i] += sample * (1.03 - sway)
        right[i] += sample * (0.97 + sway)

    melody = ["D5", "A4", "F4", "G4", "D5", "C5", "A4", "E5"]
    counter = ["A3", "D4", "F4", "E4"]
    for bar in range(BARS):
        start = bar * BAR_SECONDS
        section = bar // 8
        local_bar = bar % 8
        root_note, _ = chords[local_bar]

        add_kick(left, right, start, 0.12 + section * 0.010)
        add_kick(left, right, start + SECONDS_PER_BEAT * 2.0, 0.098 + section * 0.008)
        if section >= 1:
            add_tom(left, right, start + SECONDS_PER_BEAT * 3.0, note_frequency(root_note) * 1.5, 0.055, 0.64)
        if section >= 2 and local_bar in (3, 7):
            add_tom(left, right, start + SECONDS_PER_BEAT * 1.5, note_frequency("A2"), 0.050, 0.38)

        for step in range(8):
            hat_time = start + step * (SECONDS_PER_BEAT / 2.0)
            gain = 0.018 if step % 2 else 0.026
            add_hat(left, right, hat_time, gain, 0.42 if step % 2 else 0.58)

        if local_bar % 2 == 0:
            add_bell(left, right, start + 0.06, melody[local_bar], 0.035 + section * 0.004, 0.35)
        if section >= 1:
            add_pluck(left, right, start + SECONDS_PER_BEAT * 1.0, counter[local_bar % len(counter)], 0.040, 0.68)
            add_pluck(left, right, start + SECONDS_PER_BEAT * 2.5, melody[(local_bar + 2) % len(melody)], 0.030, 0.32)
        if section == 3 and local_bar in (0, 4):
            add_bell(left, right, start + SECONDS_PER_BEAT * 3.0, "D5", 0.045, 0.50)

    write_wav(OUTPUT, left, right)
    print(f"Wrote {OUTPUT}")
    print(f"Duration: {DURATION:.2f}s, BPM: {BPM}, bars: {BARS}, sample rate: {SAMPLE_RATE}")


if __name__ == "__main__":
    main()
