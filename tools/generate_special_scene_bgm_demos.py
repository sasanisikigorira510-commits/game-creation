#!/usr/bin/env python3
"""Generate draft BGM loops for summon and fusion scenes.

The outputs are intentionally reproducible procedural WAV files so they can be
iterated without external sound libraries.
"""

from __future__ import annotations

import math
import os
import random
import struct
import wave


SAMPLE_RATE = 44100
BEATS_PER_BAR = 4

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BGM_DIR = os.path.join(ROOT, "WitchTowerGame", "Assets", "Resources", "Audio", "BGM")

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


def edge_envelope(t: float, duration: float, fade: float) -> float:
    return smoothstep(t / fade) * smoothstep((duration - t) / fade)


def pulse_envelope(age: float, duration: float, decay: float) -> float:
    if age < 0.0 or age > duration:
        return 0.0
    return smoothstep(age / 0.014) * smoothstep((duration - age) / 0.045) * math.exp(-age * decay)


def bell_envelope(age: float, duration: float, decay: float) -> float:
    if age < 0.0 or age > duration:
        return 0.0
    return smoothstep(age / 0.018) * smoothstep((duration - age) / 0.18) * math.exp(-age * decay)


def add_bell(
    left: list[float],
    right: list[float],
    start: float,
    note: str,
    gain: float,
    pan: float,
    decay: float = 2.3,
) -> None:
    freq = note_frequency(note)
    duration = 3.2
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        env = bell_envelope(age, duration, decay)
        tone = (
            sine(freq, age) * 0.72
            + sine(freq * 2.01, age, 0.4) * 0.22
            + sine(freq * 3.02, age, 1.2) * 0.07
        )
        sample = tone * env * gain
        left[i] += sample * (1.0 - pan)
        right[i] += sample * pan


def add_pluck(left: list[float], right: list[float], start: float, note: str, gain: float, pan: float) -> None:
    freq = note_frequency(note)
    duration = 0.95
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        env = pulse_envelope(age, duration, 4.4)
        tone = sine(freq, age) * 0.62 + sine(freq * 2.0, age, 0.25) * 0.14 + noise(i) * 0.01
        sample = tone * env * gain
        left[i] += sample * (1.0 - pan)
        right[i] += sample * pan


def add_ritual_drum(left: list[float], right: list[float], start: float, freq: float, gain: float, pan: float) -> None:
    duration = 0.42
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        env = pulse_envelope(age, duration, 7.2)
        sweep = freq * (1.0 - 0.30 * smoothstep(age / duration))
        tone = sine(sweep, age) * 0.72 + sine(sweep * 1.5, age, 0.3) * 0.12
        sample = (tone + noise(i) * 0.035) * env * gain
        left[i] += sample * (1.0 - pan)
        right[i] += sample * pan


def add_sweep(
    left: list[float],
    right: list[float],
    start: float,
    duration: float,
    start_note: str,
    end_note: str,
    gain: float,
    pan: float,
) -> None:
    start_freq = note_frequency(start_note)
    end_freq = note_frequency(end_note)
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        p = smoothstep(age / duration)
        freq = start_freq + (end_freq - start_freq) * p
        env = smoothstep(age / 0.14) * smoothstep((duration - age) / 0.22)
        tone = sine(freq, age) * 0.42 + sine(freq * 1.5, age, 0.7) * 0.11 + noise(i) * 0.012
        sample = tone * env * gain
        left[i] += sample * (1.0 - pan)
        right[i] += sample * pan


def write_wav(path: str, left: list[float], right: list[float], normalize: float = 0.88) -> None:
    peak = max(max(abs(x) for x in left), max(abs(x) for x in right), 0.0001)
    scale = min(normalize / peak, 3.0)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as wav:
        wav.setnchannels(2)
        wav.setsampwidth(2)
        wav.setframerate(SAMPLE_RATE)
        frames = bytearray()
        for l_value, r_value in zip(left, right):
            left_sample = int(max(-1.0, min(1.0, l_value * scale)) * 32767)
            right_sample = int(max(-1.0, min(1.0, r_value * scale)) * 32767)
            frames += struct.pack("<h", left_sample)
            frames += struct.pack("<h", right_sample)
        wav.writeframes(frames)


def generate_summon_chamber() -> tuple[str, float]:
    random.seed(202607011)
    bpm = 72
    bars = 24
    seconds_per_beat = 60.0 / bpm
    bar_seconds = seconds_per_beat * BEATS_PER_BAR
    duration = bars * bar_seconds
    sample_count = int(duration * SAMPLE_RATE)
    left = [0.0] * sample_count
    right = [0.0] * sample_count

    chords = [
        ("D2", ["D3", "F3", "A3", "C4"]),
        ("C2", ["C3", "E3", "G3", "Bb3"]),
        ("Bb1", ["Bb2", "D3", "F3", "A3"]),
        ("A1", ["A2", "C3", "E3", "G3"]),
        ("G1", ["G2", "Bb2", "D3", "F3"]),
        ("Bb1", ["Bb2", "D3", "F3", "A3"]),
        ("C2", ["C3", "E3", "G3", "D4"]),
        ("D2", ["D3", "F3", "A3", "E4"]),
    ]

    for i in range(sample_count):
        t = i / SAMPLE_RATE
        bar = min(bars - 1, int(t / bar_seconds))
        local = t - bar * bar_seconds
        section = bar // 8
        root_note, chord_notes = chords[bar % len(chords)]
        section_gain = [0.88, 1.05, 0.96][section]
        env = edge_envelope(t, duration, 0.9) * section_gain

        root = note_frequency(root_note)
        low = sine(root * 0.5, t, 0.2) * 0.23 + sine(root, t) * 0.16
        pad = 0.0
        for note_i, note in enumerate(chord_notes):
            freq = note_frequency(note)
            drift = 1.0 + sine(0.015 + note_i * 0.004, t) * 0.0018
            pad += sine(freq * drift, t, note_i * 0.42) * 0.24
            pad += sine(freq * 2.0 * drift, t, note_i * 0.7) * 0.045
        pad /= len(chord_notes)

        ring = sine(note_frequency("A5"), t, 0.6) * 0.015 * (0.5 + 0.5 * sine(0.045, t))
        air = noise(i) * 0.007
        gate = 0.72 + 0.28 * smoothstep((bar_seconds - local) / 0.9)
        sample = (low * 0.28 + pad * 0.34 + ring + air) * gate * env
        sway = 0.5 + 0.10 * sine(0.026, t)
        left[i] += sample * (1.02 - sway)
        right[i] += sample * (0.98 + sway)

    motif = [
        (0.12, "D5", 0.060, 0.32),
        (seconds_per_beat * 2.0, "A4", 0.050, 0.68),
        (bar_seconds + seconds_per_beat * 1.0, "F5", 0.050, 0.40),
        (bar_seconds * 2.0 + seconds_per_beat * 3.0, "C5", 0.046, 0.64),
        (bar_seconds * 3.0 + seconds_per_beat * 1.5, "E5", 0.044, 0.36),
    ]
    for section in range(3):
        section_offset = section * 8 * bar_seconds
        gain_scale = [0.84, 1.08, 0.92][section]
        for start, note, gain, pan in motif:
            add_bell(left, right, section_offset + start, note, gain * gain_scale, pan, decay=2.0)
        for bar in range(8):
            base = section_offset + bar * bar_seconds
            if section >= 1:
                add_pluck(left, right, base + seconds_per_beat * 1.5, ["D4", "F4", "A4", "C5"][bar % 4], 0.026, 0.30 + (bar % 2) * 0.40)
            if bar in (3, 7):
                add_sweep(left, right, base + seconds_per_beat * 2.0, 1.5, "D4", "A5", 0.024 + section * 0.004, 0.52)

    path = os.path.join(BGM_DIR, "summon_chamber_loop.wav")
    write_wav(path, left, right, normalize=0.86)
    return path, duration


def generate_fusion_ritual() -> tuple[str, float]:
    random.seed(202607012)
    bpm = 96
    bars = 32
    seconds_per_beat = 60.0 / bpm
    bar_seconds = seconds_per_beat * BEATS_PER_BAR
    duration = bars * bar_seconds
    sample_count = int(duration * SAMPLE_RATE)
    left = [0.0] * sample_count
    right = [0.0] * sample_count

    chords = [
        ("G1", ["G2", "Bb2", "D3", "F3"]),
        ("G1", ["G2", "Bb2", "D3", "F3"]),
        ("D2", ["D3", "F3", "A3", "C4"]),
        ("Eb2", ["Eb3", "G3", "Bb3", "D4"]),
        ("C2", ["C3", "Eb3", "G3", "Bb3"]),
        ("Bb1", ["Bb2", "D3", "F3", "A3"]),
        ("D2", ["D3", "F3", "A3", "C4"]),
        ("G1", ["G2", "Bb2", "D3", "F3"]),
    ]

    for i in range(sample_count):
        t = i / SAMPLE_RATE
        bar = min(bars - 1, int(t / bar_seconds))
        local = t - bar * bar_seconds
        section = min(3, bar // 8)
        root_note, chord_notes = chords[bar % len(chords)]
        env = edge_envelope(t, duration, 0.65) * [0.82, 0.96, 1.08, 0.94][section]

        root = note_frequency(root_note)
        pulse = 0.58 + 0.42 * smoothstep((seconds_per_beat * 0.5 - (local % seconds_per_beat)) / 0.12)
        bass = (sine(root * 0.5, t) * 0.22 + sine(root, t, 0.2) * 0.20 + sine(root * 2.0, t, 0.7) * 0.06) * pulse
        pad = 0.0
        for note_i, note in enumerate(chord_notes):
            freq = note_frequency(note)
            pad += sine(freq, t, note_i * 0.3) * (0.18 + 0.015 * section)
            pad += sine(freq * 1.5, t, note_i * 0.8) * 0.035
        pad /= len(chord_notes)

        high = sine(note_frequency("G5"), t, 1.4) * 0.014 * (0.4 + 0.6 * sine(0.07, t))
        grit = noise(i) * (0.006 + section * 0.0015)
        sample = (bass * 0.30 + pad * 0.34 + high + grit) * env
        sway = 0.5 + 0.07 * sine(0.043, t)
        left[i] += sample * (1.04 - sway)
        right[i] += sample * (0.96 + sway)

    for bar in range(bars):
        base = bar * bar_seconds
        section = min(3, bar // 8)
        root_note, _ = chords[bar % len(chords)]
        add_ritual_drum(left, right, base, note_frequency(root_note), 0.070 + section * 0.006, 0.48)
        add_ritual_drum(left, right, base + seconds_per_beat * 2.0, note_frequency(root_note) * 1.5, 0.052 + section * 0.005, 0.55)
        if section >= 1:
            add_pluck(left, right, base + seconds_per_beat * 1.0, ["G3", "Bb3", "D4", "F4"][bar % 4], 0.030, 0.34)
            add_pluck(left, right, base + seconds_per_beat * 3.0, ["D4", "F4", "A4", "C5"][bar % 4], 0.028, 0.66)
        if section >= 2 and bar % 4 == 3:
            add_sweep(left, right, base + seconds_per_beat * 2.25, 1.2, "G3", "D5", 0.038, 0.50)
        if section == 3 and bar % 8 in (0, 4):
            add_bell(left, right, base + 0.05, "G5", 0.046, 0.54, decay=2.4)

    path = os.path.join(BGM_DIR, "fusion_ritual_loop.wav")
    write_wav(path, left, right, normalize=0.88)
    return path, duration


def main() -> None:
    outputs = [generate_summon_chamber(), generate_fusion_ritual()]
    for path, duration in outputs:
        print(f"Wrote {path}")
        print(f"Duration: {duration:.2f}s, sample rate: {SAMPLE_RATE}")


if __name__ == "__main__":
    main()
