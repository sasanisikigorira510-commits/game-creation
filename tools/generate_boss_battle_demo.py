#!/usr/bin/env python3
"""Generate a draft boss battle BGM loop for WitchTowerGame."""

from __future__ import annotations

import math
import os
import random
import struct
import wave


SAMPLE_RATE = 44100
BPM = 128
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
    "battle_boss_loop.wav",
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
    fade = 0.42
    return smoothstep(t / fade) * smoothstep((DURATION - t) / fade)


def pulse_envelope(age: float, duration: float, decay: float) -> float:
    if age < 0.0 or age > duration:
        return 0.0
    return smoothstep(age / 0.010) * smoothstep((duration - age) / 0.035) * math.exp(-age * decay)


def add_kick(left: list[float], right: list[float], start: float, gain: float) -> None:
    duration = 0.36
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        env = pulse_envelope(age, duration, 8.2)
        freq = 102.0 - 54.0 * smoothstep(age / duration)
        click = noise(i) * pulse_envelope(age, 0.045, 35.0) * 0.10
        sample = (sine(freq, age) * 0.88 + click) * env * gain
        left[i] += sample * 0.52
        right[i] += sample * 0.48


def add_snare(left: list[float], right: list[float], start: float, gain: float) -> None:
    duration = 0.18
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        env = pulse_envelope(age, duration, 12.5)
        body = sine(185.0, age) * 0.32 + sine(370.0, age, 0.6) * 0.14
        sample = (body + noise(i) * 0.46) * env * gain
        left[i] += sample * 0.48
        right[i] += sample * 0.52


def add_hat(left: list[float], right: list[float], start: float, gain: float, pan: float) -> None:
    duration = 0.055
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        env = pulse_envelope(age, duration, 31.0)
        metallic = sine(7600.0, age) * 0.13 + sine(9800.0, age, 0.4) * 0.10
        sample = (noise(i) * 0.54 + metallic) * env * gain
        left[i] += sample * (1.0 - pan)
        right[i] += sample * pan


def add_low_stab(left: list[float], right: list[float], start: float, note: str, gain: float, pan: float) -> None:
    freq = note_frequency(note)
    duration = 0.62
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        env = pulse_envelope(age, duration, 4.6)
        tone = math.tanh((sine(freq, age) + sine(freq * 2.0, age, 0.22) * 0.38) * 1.55)
        sample = (tone + noise(i) * 0.018) * env * gain
        left[i] += sample * (1.0 - pan)
        right[i] += sample * pan


def add_lead(left: list[float], right: list[float], start: float, note: str, gain: float, pan: float) -> None:
    freq = note_frequency(note)
    duration = 0.90
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        env = pulse_envelope(age, duration, 3.2)
        vibrato = 1.0 + sine(5.8, age) * 0.008
        tone = (
            sine(freq * vibrato, age) * 0.58
            + sine(freq * 2.0 * vibrato, age, 0.35) * 0.22
            + sine(freq * 3.0 * vibrato, age, 1.1) * 0.08
        )
        sample = tone * env * gain
        left[i] += sample * (1.0 - pan)
        right[i] += sample * pan


def add_riser(left: list[float], right: list[float], start: float, duration: float, gain: float) -> None:
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        p = smoothstep(age / duration)
        env = smoothstep(age / 0.18) * smoothstep((duration - age) / 0.22)
        freq = note_frequency("D4") + (note_frequency("A5") - note_frequency("D4")) * p
        sample = (sine(freq, age, 0.5) * 0.26 + noise(i) * 0.10 * p) * env * gain
        left[i] += sample * 0.50
        right[i] += sample * 0.50


def write_wav(path: str, left: list[float], right: list[float]) -> None:
    peak = max(max(abs(x) for x in left), max(abs(x) for x in right), 0.0001)
    scale = min(0.88 / peak, 2.8)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as wav:
        wav.setnchannels(2)
        wav.setsampwidth(2)
        wav.setframerate(SAMPLE_RATE)
        frames = bytearray()
        for l_value, r_value in zip(left, right):
            frames += struct.pack("<h", int(max(-1.0, min(1.0, l_value * scale)) * 32767))
            frames += struct.pack("<h", int(max(-1.0, min(1.0, r_value * scale)) * 32767))
        wav.writeframes(frames)


def main() -> None:
    random.seed(202607013)
    sample_count = int(DURATION * SAMPLE_RATE)
    left = [0.0] * sample_count
    right = [0.0] * sample_count

    chords = [
        ("D2", ["D3", "F3", "A3", "C4"]),
        ("D2", ["D3", "F3", "A3", "C4"]),
        ("Bb1", ["Bb2", "D3", "F3", "A3"]),
        ("C2", ["C3", "E3", "G3", "Bb3"]),
        ("G1", ["G2", "Bb2", "D3", "F3"]),
        ("Bb1", ["Bb2", "D3", "F3", "A3"]),
        ("A1", ["A2", "C3", "E3", "G3"]),
        ("D2", ["D3", "F3", "A3", "C4"]),
    ]

    for i in range(sample_count):
        t = i / SAMPLE_RATE
        bar = min(BARS - 1, int(t / BAR_SECONDS))
        local_bar = bar % len(chords)
        local = t - bar * BAR_SECONDS
        section = min(3, bar // 8)
        root_note, chord_notes = chords[local_bar]
        env = edge_envelope(t) * [0.92, 1.02, 1.12, 0.98][section]

        root = note_frequency(root_note)
        gate = 0.52 + 0.48 * smoothstep((SECONDS_PER_BEAT * 0.5 - (local % SECONDS_PER_BEAT)) / 0.08)
        bass = (
            sine(root * 0.5, t) * 0.22
            + sine(root, t, 0.2) * 0.26
            + sine(root * 2.0, t, 0.6) * 0.08
        ) * gate

        pad = 0.0
        for note_i, note in enumerate(chord_notes):
            freq = note_frequency(note)
            pad += sine(freq, t, note_i * 0.39) * (0.18 + section * 0.012)
            pad += sine(freq * 1.997, t, note_i * 0.8) * 0.040
        pad /= len(chord_notes)

        threat = sine(note_frequency("A4"), t, 0.8) * 0.030 * (0.5 + 0.5 * sine(0.09, t))
        grit = noise(i) * (0.008 + section * 0.001)
        sample = (bass * 0.34 + pad * 0.26 + threat + grit) * env
        sway = 0.5 + 0.065 * sine(0.052, t)
        left[i] += sample * (1.03 - sway)
        right[i] += sample * (0.97 + sway)

    lead_pattern = ["D5", "F5", "E5", "C5", "A4", "C5", "E5", "A5"]
    stab_pattern = ["D2", "D2", "Bb1", "C2", "G1", "Bb1", "A1", "D2"]
    for bar in range(BARS):
        start = bar * BAR_SECONDS
        section = min(3, bar // 8)
        local_bar = bar % 8

        add_kick(left, right, start, 0.130 + section * 0.010)
        add_kick(left, right, start + SECONDS_PER_BEAT * 2.0, 0.104 + section * 0.008)
        add_snare(left, right, start + SECONDS_PER_BEAT * 1.0, 0.050 + section * 0.006)
        add_snare(left, right, start + SECONDS_PER_BEAT * 3.0, 0.060 + section * 0.006)

        for step in range(8):
            add_hat(
                left,
                right,
                start + step * (SECONDS_PER_BEAT / 2.0),
                0.018 if step % 2 else 0.030,
                0.38 if step % 2 else 0.62,
            )

        add_low_stab(left, right, start + 0.02, stab_pattern[local_bar], 0.062 + section * 0.005, 0.45)
        if section >= 1:
            add_low_stab(left, right, start + SECONDS_PER_BEAT * 1.5, stab_pattern[(local_bar + 2) % 8], 0.038, 0.58)
        if local_bar in (0, 2, 4, 6):
            add_lead(left, right, start + SECONDS_PER_BEAT * 0.55, lead_pattern[local_bar], 0.040 + section * 0.004, 0.32)
        if section >= 2 and local_bar in (3, 7):
            add_lead(left, right, start + SECONDS_PER_BEAT * 2.45, lead_pattern[(local_bar + 3) % 8], 0.052, 0.68)
            add_riser(left, right, start + SECONDS_PER_BEAT * 2.0, 1.25, 0.028)

    write_wav(OUTPUT, left, right)
    print(f"Wrote {OUTPUT}")
    print(f"Duration: {DURATION:.2f}s, BPM: {BPM}, bars: {BARS}, sample rate: {SAMPLE_RATE}")


if __name__ == "__main__":
    main()
