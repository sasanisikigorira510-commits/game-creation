#!/usr/bin/env python3
"""Regenerate Gear Crypt dungeon music with a distinct industrial-beast identity."""

from __future__ import annotations

import math
import os
import random
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
    semitone_from_a4 = NOTE_INDEX[name] + (octave - 4) * 12
    return 440.0 * (2.0 ** (semitone_from_a4 / 12.0))


def sine(freq: float, t: float, phase: float = 0.0) -> float:
    return math.sin(2.0 * math.pi * freq * t + phase)


def smoothstep(x: float) -> float:
    x = max(0.0, min(1.0, x))
    return x * x * (3.0 - 2.0 * x)


def noise(index: int, seed: int) -> float:
    value = math.sin((index + seed * 997) * 12.9898) * 43758.5453
    return (value - math.floor(value)) * 2.0 - 1.0


def pan_gains(pan: float) -> tuple[float, float]:
    pan = max(0.0, min(1.0, pan))
    return math.cos(pan * math.pi * 0.5), math.sin(pan * math.pi * 0.5)


def hit_env(age: float, duration: float, attack: float, release: float, decay: float) -> float:
    if age < 0.0 or age > duration:
        return 0.0
    return smoothstep(age / attack) * smoothstep((duration - age) / release) * math.exp(-age * decay)


def edge_env(t: float, duration: float, fade: float = 1.0) -> float:
    return smoothstep(t / fade) * smoothstep((duration - t) / fade)


def add(left: list[float], right: list[float], index: int, sample: float, pan: float) -> None:
    lgain, rgain = pan_gains(pan)
    left[index] += sample * lgain
    right[index] += sample * rgain


def add_metal_hit(
    left: list[float],
    right: list[float],
    start: float,
    gain: float,
    pan: float,
    seed: int,
    heavy: bool = False,
) -> None:
    duration = 0.24 if heavy else 0.15
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    base = 190.0 if heavy else 820.0
    ring_a = 1240.0 if heavy else 2140.0
    ring_b = 1750.0 if heavy else 3290.0
    for i in range(start_i, end_i):
        t = i / SAMPLE_RATE
        age = t - start
        env = hit_env(age, duration, 0.002, 0.025, 12.0 if heavy else 18.0)
        clang = (
            sine(base * (1.0 - 0.18 * smoothstep(age / duration)), age) * (0.42 if heavy else 0.18)
            + sine(ring_a, age, 0.2) * 0.18
            + sine(ring_b, age, 1.1) * 0.13
            + noise(i, seed) * (0.18 if heavy else 0.12)
        )
        add(left, right, i, clang * env * gain, pan)


def add_gear_tick(left: list[float], right: list[float], start: float, gain: float, pan: float, seed: int) -> None:
    duration = 0.055
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        t = i / SAMPLE_RATE
        age = t - start
        env = hit_env(age, duration, 0.0015, 0.012, 32.0)
        tick = sine(4200.0, age) * 0.12 + sine(6100.0, age, 0.5) * 0.08 + noise(i, seed) * 0.20
        add(left, right, i, tick * env * gain, pan)


def add_pluck(left: list[float], right: list[float], start: float, note: str, gain: float, pan: float) -> None:
    duration = 0.64
    freq = note_frequency(note)
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        t = i / SAMPLE_RATE
        age = t - start
        env = hit_env(age, duration, 0.004, 0.09, 5.4)
        sample = sine(freq, age) * 0.52 + sine(freq * 2.0, age, 0.35) * 0.16 + noise(i, 30) * 0.012
        add(left, right, i, sample * env * gain, pan)


def add_bent_bell(left: list[float], right: list[float], start: float, note: str, gain: float, pan: float) -> None:
    duration = 1.55
    freq = note_frequency(note)
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        t = i / SAMPLE_RATE
        age = t - start
        env = hit_env(age, duration, 0.008, 0.18, 2.9)
        bend = 1.0 - 0.012 * math.exp(-age * 2.4)
        sample = (
            sine(freq * bend, age) * 0.62
            + sine(freq * 1.997, age, 0.6) * 0.20
            + sine(freq * 2.74, age, 1.4) * 0.09
        )
        add(left, right, i, sample * env * gain, pan)


def add_growl(left: list[float], right: list[float], start: float, duration: float, gain: float, pan: float) -> None:
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    for i in range(start_i, end_i):
        t = i / SAMPLE_RATE
        age = t - start
        p = age / duration
        env = smoothstep(age / 0.16) * smoothstep((duration - age) / 0.24)
        freq = 58.0 + 16.0 * sine(0.31, t) + 6.0 * sine(0.93, t, 0.4)
        throat = sine(freq, t) * 0.42 + sine(freq * 0.5, t, 0.7) * 0.25
        grit = math.tanh((throat + noise(i, 51) * 0.13) * 1.9)
        add(left, right, i, grit * env * gain * (0.75 + 0.25 * sine(1.7, p)), pan)


def add_steam(left: list[float], right: list[float], start: float, duration: float, gain: float, pan: float, seed: int) -> None:
    start_i = max(0, int(start * SAMPLE_RATE))
    end_i = min(len(left), int((start + duration) * SAMPLE_RATE))
    low = 0.0
    previous = 0.0
    for i in range(start_i, end_i):
        t = i / SAMPLE_RATE
        age = t - start
        raw = noise(i, seed)
        low = low * 0.92 + raw * 0.08
        bright = raw - previous * 0.78
        previous = raw
        env = smoothstep(age / 0.18) * smoothstep((duration - age) / 0.34)
        sample = (bright * 0.32 + low * 0.48) * env * gain
        add(left, right, i, sample, pan)


def write_wav(path: Path, left: list[float], right: list[float], peak_target: float = 0.84) -> None:
    peak = max(max(abs(v) for v in left), max(abs(v) for v in right), 0.0001)
    scale = min(peak_target / peak, 4.0)
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as wav:
        wav.setnchannels(2)
        wav.setsampwidth(2)
        wav.setframerate(SAMPLE_RATE)
        frames = bytearray()
        for l_value, r_value in zip(left, right):
            l_sample = int(max(-1.0, min(1.0, math.tanh(l_value * scale * 1.08))) * 32767)
            r_sample = int(max(-1.0, min(1.0, math.tanh(r_value * scale * 1.08))) * 32767)
            frames += struct.pack("<h", l_sample)
            frames += struct.pack("<h", r_sample)
        wav.writeframes(frames)


def render_gear_crypt() -> Path:
    random.seed(22072026)
    bpm = 96.0
    bars = 32
    beat = 60.0 / bpm
    bar = beat * 4.0
    duration = bars * bar
    count = int(duration * SAMPLE_RATE)
    left = [0.0] * count
    right = [0.0] * count

    chord_roots = ("G1", "D2", "Eb2", "F2", "C2", "Bb1", "D2", "G1")
    chord_notes = (
        ("G2", "Bb2", "D3", "F3"),
        ("D3", "F3", "A3", "C4"),
        ("Eb3", "G3", "Bb3", "D4"),
        ("F3", "A3", "C4", "E4"),
        ("C3", "Eb3", "G3", "Bb3"),
        ("Bb2", "D3", "F3", "A3"),
        ("D3", "F3", "A3", "C4"),
        ("G2", "Bb2", "D3", "F3"),
    )
    root_freqs = [note_frequency(n) for n in chord_roots]
    note_freqs = [[note_frequency(n) for n in group] for group in chord_notes]

    for i in range(count):
        t = i / SAMPLE_RATE
        bar_index = min(bars - 1, int(t / bar))
        section = min(3, bar_index // 8)
        local = t - bar_index * bar
        env = edge_env(t, duration, 0.9) * [0.82, 0.98, 1.06, 0.94][section]
        root = root_freqs[bar_index % 8]
        notes = note_freqs[bar_index % 8]
        piston_gate = 0.58 + 0.42 * smoothstep((beat * 0.38 - (local % (beat * 0.75))) / 0.08)
        machine_bass = (
            sine(root * 0.5, t, 0.4) * 0.34
            + sine(root, t, 1.2) * 0.18
            + sine(root * 1.997, t, 0.2) * 0.05
        ) * piston_gate
        pad = (
            sine(notes[0], t, 0.2) * 0.22
            + sine(notes[1] * 0.998, t, 1.1) * 0.19
            + sine(notes[2] * 1.001, t, 2.2) * 0.16
            + sine(notes[3] * 2.0, t, 0.7) * 0.05
        )
        saw_shadow = math.tanh((sine(root * 3.0, t) + sine(root * 4.48, t, 0.3)) * 0.65) * 0.055
        air = noise(i, 70) * (0.004 + section * 0.001)
        sample = (machine_bass * 0.34 + pad * 0.34 + saw_shadow + air) * env
        pan = 0.5 + 0.06 * sine(0.037, t)
        add(left, right, i, sample, pan)

    tick_pattern = (0.0, 0.75, 1.5, 2.25, 2.75, 3.25)
    heavy_pattern = (0.0, 2.5)
    pluck_pattern = (("G3", 0.5), ("Bb3", 1.25), ("D4", 2.0), ("F4", 3.25))
    bell_pattern = (("G4", 0.08), ("D5", 2.0), ("Bb4", 4.0), ("F5", 6.0))

    for b in range(bars):
        base = b * bar
        section = min(3, b // 8)
        for step, offset in enumerate(tick_pattern):
            gain = 0.032 + section * 0.006
            add_gear_tick(left, right, base + offset * beat, gain, 0.34 + (step % 3) * 0.16, 100 + b * 7 + step)
        for step, offset in enumerate(heavy_pattern):
            add_metal_hit(left, right, base + offset * beat, 0.070 + section * 0.010, 0.48 + step * 0.08, 200 + b, heavy=True)
        if section >= 1:
            for note, offset in pluck_pattern:
                add_pluck(left, right, base + offset * beat, note, 0.036 + section * 0.004, 0.36 + (offset % 1.0) * 0.28)
        if b % 8 in (0, 4):
            for note, local_bar in bell_pattern:
                add_bent_bell(left, right, base + local_bar * beat, note, 0.046 + section * 0.004, 0.38 + (local_bar % 2) * 0.18)
        if b % 8 in (3, 7):
            add_steam(left, right, base + 2.1 * beat, 1.35, 0.034 + section * 0.006, 0.68 if b % 8 == 3 else 0.30, 300 + b)
        if b in (7, 15, 23, 31):
            add_growl(left, right, max(0.0, base - 0.2), 1.65, 0.055 + section * 0.010, 0.50)

    path = BGM_DIR / "dungeon_gear_crypt_loop.wav"
    write_wav(path, left, right)
    return path


def render_gear_crypt_boss() -> Path:
    bpm = 132.0
    bars = 16
    beat = 60.0 / bpm
    bar = beat * 4.0
    duration = bars * bar
    count = int(duration * SAMPLE_RATE)
    left = [0.0] * count
    right = [0.0] * count

    roots = [note_frequency(n) for n in ("G1", "D2", "Eb2", "F2")]
    chords = [[note_frequency(n) for n in group] for group in (
        ("G2", "Bb2", "D3", "F3"),
        ("D3", "F3", "A3", "C4"),
        ("Eb3", "G3", "Bb3", "D4"),
        ("F3", "A3", "C4", "E4"),
    )]
    for i in range(count):
        t = i / SAMPLE_RATE
        b = min(bars - 1, int(t / bar))
        local = t - b * bar
        env = edge_env(t, duration, 0.45)
        root = roots[b % 4]
        notes = chords[b % 4]
        drive = 0.54 + 0.46 * smoothstep((beat * 0.31 - (local % (beat * 0.5))) / 0.055)
        bass = (sine(root * 0.5, t) * 0.40 + sine(root, t, 0.6) * 0.20 + sine(root * 2.01, t) * 0.07) * drive
        choir = sine(notes[2], t, 1.4) * 0.19 + sine(notes[3] * 1.5, t, 0.7) * 0.08
        grit = math.tanh((sine(root * 4.0, t) + noise(i, 500) * 0.35) * 1.5) * 0.055
        add(left, right, i, (bass * 0.42 + choir * 0.30 + grit) * env, 0.5 + 0.04 * sine(0.08, t))

    for b in range(bars):
        base = b * bar
        for step in range(8):
            add_gear_tick(left, right, base + step * beat * 0.5, 0.046 if step % 2 == 0 else 0.032, 0.34 + (step % 4) * 0.11, 600 + b * 11 + step)
        add_metal_hit(left, right, base, 0.112, 0.46, 700 + b, heavy=True)
        add_metal_hit(left, right, base + beat * 1.5, 0.070, 0.62, 710 + b, heavy=False)
        add_metal_hit(left, right, base + beat * 2.5, 0.092, 0.54, 720 + b, heavy=True)
        if b % 2 == 0:
            add_bent_bell(left, right, base + beat * 2.0, "D5", 0.052, 0.34)
        if b % 4 == 3:
            add_growl(left, right, base + beat * 1.3, 1.1, 0.082, 0.50)
            add_steam(left, right, base + beat * 2.0, 0.95, 0.050, 0.72, 900 + b)

    path = BGM_DIR / "dungeon_gear_crypt_boss_loop.wav"
    write_wav(path, left, right)
    return path


def main() -> None:
    for path in (render_gear_crypt(), render_gear_crypt_boss()):
        print(f"Wrote {path}")


if __name__ == "__main__":
    main()
