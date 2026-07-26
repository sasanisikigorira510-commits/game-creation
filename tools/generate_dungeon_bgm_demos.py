#!/usr/bin/env python3
"""Generate dungeon-specific BGM loops for WitchTowerGame.

These tracks aim for melancholic dark-fantasy color: airy choir pads, bells,
plucked strings, piano-like attacks, and restrained ritual percussion. They do
not copy melodies or arrangements from any existing soundtrack.
"""

from __future__ import annotations

import hashlib
import math
import os
import random
import struct
import wave
from typing import Dict, Iterable, List, Sequence, Tuple


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


StereoBuffer = Tuple[List[float], List[float]]
Chord = Tuple[str, Sequence[str]]


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


def pseudo_noise(sample_index: int, seed: int) -> float:
    value = math.sin((sample_index + seed * 101) * 12.9898) * 43758.5453
    return (value - math.floor(value)) * 2.0 - 1.0


def pan_gains(pan: float) -> Tuple[float, float]:
    pan = max(0.0, min(1.0, pan))
    return math.cos(pan * math.pi * 0.5), math.sin(pan * math.pi * 0.5)


def edge_envelope(t: float, duration: float, fade: float) -> float:
    return smoothstep(t / fade) * smoothstep((duration - t) / fade)


def hit_envelope(age: float, duration: float, attack: float, release: float, decay: float) -> float:
    if age < 0.0 or age > duration:
        return 0.0

    return (
        smoothstep(age / max(0.001, attack))
        * smoothstep((duration - age) / max(0.001, release))
        * math.exp(-age * decay)
    )


def pad_voice(freq: float, t: float, phase: float, brightness: float) -> float:
    vibrato = 1.0 + 0.0028 * sine(0.16, t, phase)
    f = freq * vibrato
    base = sine(f, t, phase) * 0.62
    octave = sine(f * 2.002, t, phase * 0.7 + 0.4) * (0.15 + brightness * 0.10)
    nasal = sine(f * 3.01, t, phase * 1.2 + 1.1) * (0.05 + brightness * 0.06)
    breath = sine(f * 0.5, t, phase * 0.4) * 0.10
    return math.tanh((base + octave + nasal + breath) * 0.95)


def add_bell(
    buffer: StereoBuffer,
    start: float,
    note: str,
    gain: float,
    pan: float,
    duration: float = 2.4,
    decay: float = 2.6,
) -> None:
    left, right = buffer
    freq = note_frequency(note)
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + duration) * SAMPLE_RATE))
    lgain, rgain = pan_gains(pan)
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        env = hit_envelope(age, duration, 0.012, 0.25, decay)
        tone = (
            sine(freq, age) * 0.72
            + sine(freq * 2.01, age, 0.42) * 0.20
            + sine(freq * 3.02, age, 1.15) * 0.08
            + sine(freq * 5.01, age, 0.20) * 0.025
        )
        sample = tone * env * gain
        left[i] += sample * lgain
        right[i] += sample * rgain


def add_pluck(buffer: StereoBuffer, start: float, note: str, gain: float, pan: float) -> None:
    left, right = buffer
    freq = note_frequency(note)
    duration = 0.82
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + duration) * SAMPLE_RATE))
    lgain, rgain = pan_gains(pan)
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        env = hit_envelope(age, duration, 0.006, 0.09, 5.2)
        tone = sine(freq, age) * 0.58 + sine(freq * 2.0, age, 0.3) * 0.16 + pseudo_noise(i, 17) * 0.012
        sample = tone * env * gain
        left[i] += sample * lgain
        right[i] += sample * rgain


def add_piano(buffer: StereoBuffer, start: float, note: str, gain: float, pan: float) -> None:
    left, right = buffer
    freq = note_frequency(note)
    duration = 1.35
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + duration) * SAMPLE_RATE))
    lgain, rgain = pan_gains(pan)
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        env = hit_envelope(age, duration, 0.004, 0.18, 3.0)
        hammer = pseudo_noise(i, 31) * math.exp(-age * 42.0) * 0.06
        tone = sine(freq, age) * 0.54 + sine(freq * 2.004, age, 0.16) * 0.15 + sine(freq * 3.01, age, 1.4) * 0.045
        sample = (tone + hammer) * env * gain
        left[i] += sample * lgain
        right[i] += sample * rgain


def add_drum(
    buffer: StereoBuffer,
    start: float,
    freq: float,
    gain: float,
    pan: float,
    duration: float = 0.34,
    metallic: float = 0.0,
) -> None:
    left, right = buffer
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + duration) * SAMPLE_RATE))
    lgain, rgain = pan_gains(pan)
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        env = hit_envelope(age, duration, 0.004, 0.055, 7.6)
        sweep = freq * (1.0 - 0.34 * smoothstep(age / duration))
        tone = sine(sweep, age) * 0.68 + sine(sweep * 1.5, age, 0.5) * 0.12
        metal = (sine(1800.0, age) + sine(2410.0, age, 0.7)) * metallic * math.exp(-age * 12.0)
        noise = pseudo_noise(i, 43) * 0.045
        sample = (tone + metal + noise) * env * gain
        left[i] += sample * lgain
        right[i] += sample * rgain


def add_sweep(buffer: StereoBuffer, start: float, duration: float, start_note: str, end_note: str, gain: float, pan: float) -> None:
    left, right = buffer
    start_freq = note_frequency(start_note)
    end_freq = note_frequency(end_note)
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + duration) * SAMPLE_RATE))
    lgain, rgain = pan_gains(pan)
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        p = smoothstep(age / duration)
        freq = start_freq + (end_freq - start_freq) * p
        env = smoothstep(age / 0.18) * smoothstep((duration - age) / 0.24)
        tone = sine(freq, age) * 0.34 + sine(freq * 1.5, age, 0.6) * 0.11 + pseudo_noise(i, 71) * 0.014
        sample = tone * env * gain
        left[i] += sample * lgain
        right[i] += sample * rgain


def write_wav(path: str, buffer: StereoBuffer, normalize: float = 0.84) -> None:
    left, right = buffer
    peak = max(max(abs(x) for x in left), max(abs(x) for x in right), 0.0001)
    scale = min(normalize / peak, 3.6)
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


def write_bgm_meta(path: str) -> None:
    rel = os.path.relpath(path, ROOT).replace(os.sep, "/")
    guid = hashlib.md5(("witchtower-dungeon-bgm:" + rel).encode("utf-8")).hexdigest()
    meta = f"""fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 2
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 0.62
    conversionMode: 0
    preloadAudioData: 0
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 0
  loadInBackground: 1
  ambisonic: 0
  3D: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
    with open(path + ".meta", "w", encoding="utf-8") as handle:
        handle.write(meta)


def build_track(spec: Dict[str, object]) -> Tuple[str, float]:
    seed = int(spec["seed"])
    random.seed(seed)
    bpm = float(spec["bpm"])
    bars = int(spec["bars"])
    seconds_per_beat = 60.0 / bpm
    bar_seconds = seconds_per_beat * BEATS_PER_BAR
    duration = bars * bar_seconds
    sample_count = int(duration * SAMPLE_RATE)
    buffer: StereoBuffer = ([0.0] * sample_count, [0.0] * sample_count)
    left, right = buffer

    chords: Sequence[Chord] = spec["chords"]  # type: ignore[assignment]
    brightness = float(spec.get("brightness", 0.45))
    density = float(spec.get("density", 0.6))
    pulse_amount = float(spec.get("pulse", 0.4))
    percussion = float(spec.get("percussion", 0.04))
    metal = float(spec.get("metal", 0.0))
    air_gain = float(spec.get("air", 0.006))
    root_gain = float(spec.get("rootGain", 0.28))
    choir_gain = float(spec.get("choirGain", 0.28))
    fade = float(spec.get("fade", 0.72))
    two_pi = 2.0 * math.pi
    high_freq = note_frequency(str(spec.get("highNote", "A5")))
    choir_freq = note_frequency(str(spec.get("choirNote", chords[0][1][-1])))
    bar_infos: List[Tuple[float, List[float]]] = []
    for bar_index in range(bars):
        root_note, chord_notes = chords[bar_index % len(chords)]
        bar_infos.append((note_frequency(root_note), [note_frequency(note) for note in chord_notes]))

    for i in range(sample_count):
        t = i / SAMPLE_RATE
        bar_index = min(bars - 1, int(t / bar_seconds))
        section = min(3, bar_index // 8)
        root, chord_freqs = bar_infos[bar_index]
        local = t - bar_index * bar_seconds
        env = edge_envelope(t, duration, fade) * [0.88, 1.00, 1.08, 0.96][section]

        gate = 1.0 - pulse_amount + pulse_amount * smoothstep((seconds_per_beat * 0.62 - (local % seconds_per_beat)) / 0.16)
        drone = (
            sine(root * 0.5, t, 0.1) * 0.26
            + sine(root, t, 0.4) * 0.18
            + sine(root * 2.0, t, 1.0) * 0.045
        ) * gate

        f0 = chord_freqs[0]
        f1 = chord_freqs[min(1, len(chord_freqs) - 1)]
        f2 = chord_freqs[-1]
        slow_vibrato = 1.0 + 0.0025 * math.sin(two_pi * (0.13 + seed * 0.0001) * t)
        pad = (
            math.sin(two_pi * f0 * slow_vibrato * t + 0.2) * 0.44
            + math.sin(two_pi * f1 * (slow_vibrato * 0.999) * t + 1.1) * 0.36
            + math.sin(two_pi * f2 * (slow_vibrato * 1.001) * t + 2.0) * 0.28
            + math.sin(two_pi * f2 * 2.002 * t + 0.6) * (0.045 + brightness * 0.045)
            + math.sin(two_pi * f0 * 0.5 * t + 0.9) * 0.06
        ) * 0.72

        shimmer = sine(high_freq, t, 0.8) * 0.014 * (0.45 + 0.55 * sine(0.055 + seed * 0.001, t))
        breath = pseudo_noise(i, seed) * air_gain
        section_choir = 0.0
        if section >= 1:
            choir_vibrato = 1.0 + 0.0032 * math.sin(two_pi * 0.11 * t + 1.2)
            section_choir = (
                math.sin(two_pi * choir_freq * choir_vibrato * t + 1.2) * 0.55
                + math.sin(two_pi * choir_freq * 2.002 * t + 0.4) * (0.08 + brightness * 0.04)
                + math.sin(two_pi * choir_freq * 0.5 * t + 2.1) * 0.10
            ) * (0.28 + section * 0.04)

        sample = (drone * root_gain + pad * choir_gain + section_choir * 0.15 + shimmer + breath) * env
        sway = 0.5 + 0.07 * sine(0.026 + seed * 0.0007, t)
        left[i] += sample * (1.03 - sway)
        right[i] += sample * (0.97 + sway)

    motif: Sequence[Tuple[float, str, float, float]] = spec["motif"]  # type: ignore[assignment]
    for section in range(max(1, bars // 8)):
        section_offset = section * 8 * bar_seconds
        gain_scale = [0.86, 1.02, 1.10, 0.94][min(3, section)]
        for start_bar, note, gain, pan in motif:
            start = section_offset + start_bar * bar_seconds
            add_bell(buffer, start + 0.06, note, gain * gain_scale, pan, duration=2.6, decay=2.7 - density * 0.4)
        if section >= 1:
            for step in range(4):
                note = str(spec["pluckNotes"][step % len(spec["pluckNotes"])])  # type: ignore[index]
                add_pluck(buffer, section_offset + (step * 2 + 1) * bar_seconds + seconds_per_beat * 0.5, note, 0.024 + density * 0.018, 0.35 + (step % 2) * 0.28)

    for bar in range(bars):
        base = bar * bar_seconds
        root_note, _ = chords[bar % len(chords)]
        if percussion > 0.0:
            add_drum(buffer, base, note_frequency(root_note) * 0.5, percussion, 0.48, metallic=metal)
            if bar % 2 == 1:
                add_drum(buffer, base + seconds_per_beat * 2.0, note_frequency(root_note) * 0.75, percussion * 0.72, 0.58, metallic=metal)
        if bar % 4 == 3:
            add_sweep(
                buffer,
                base + seconds_per_beat * 2.2,
                1.25,
                str(spec.get("sweepStart", root_note)),
                str(spec.get("sweepEnd", spec.get("highNote", "A5"))),
                0.015 + density * 0.013,
                0.50,
            )
        if bar % 8 in (0, 4):
            add_piano(buffer, base + seconds_per_beat * 1.5, str(spec.get("pianoNote", "D5")), 0.030 + density * 0.012, 0.38 + (bar % 8) * 0.035)

    path = os.path.join(BGM_DIR, str(spec["key"]) + "_loop.wav")
    write_wav(path, buffer, normalize=float(spec.get("normalize", 0.84)))
    write_bgm_meta(path)
    return path, duration


TRACKS: Sequence[Dict[str, object]] = (
    {
        "key": "dungeon_blight_cavern",
        "seed": 101,
        "bpm": 86,
        "bars": 24,
        "brightness": 0.42,
        "density": 0.42,
        "pulse": 0.24,
        "percussion": 0.028,
        "air": 0.007,
        "highNote": "A5",
        "choirNote": "D4",
        "pianoNote": "F5",
        "sweepStart": "D4",
        "sweepEnd": "A5",
        "chords": (
            ("D2", ("D3", "F3", "A3", "E4")),
            ("A1", ("A2", "C3", "E3", "G3")),
            ("Bb1", ("Bb2", "D3", "F3", "A3")),
            ("C2", ("C3", "E3", "G3", "D4")),
            ("G1", ("G2", "Bb2", "D3", "F3")),
            ("Bb1", ("Bb2", "D3", "F3", "A3")),
            ("A1", ("A2", "C3", "E3", "G3")),
            ("D2", ("D3", "F3", "A3", "E4")),
        ),
        "motif": ((0.0, "D5", 0.045, 0.34), (2.0, "A4", 0.036, 0.66), (4.0, "F5", 0.038, 0.40), (6.0, "E5", 0.030, 0.58)),
        "pluckNotes": ("D4", "F4", "A4", "C5"),
    },
    {
        "key": "dungeon_gear_crypt",
        "seed": 202,
        "bpm": 104,
        "bars": 32,
        "brightness": 0.52,
        "density": 0.62,
        "pulse": 0.52,
        "percussion": 0.052,
        "metal": 0.10,
        "air": 0.006,
        "highNote": "E5",
        "choirNote": "G4",
        "pianoNote": "C5",
        "sweepStart": "G3",
        "sweepEnd": "D5",
        "chords": (
            ("G1", ("G2", "Bb2", "D3", "F3")),
            ("D2", ("D3", "F3", "A3", "C4")),
            ("Eb2", ("Eb3", "G3", "Bb3", "D4")),
            ("F2", ("F3", "A3", "C4", "E4")),
            ("C2", ("C3", "Eb3", "G3", "Bb3")),
            ("Bb1", ("Bb2", "D3", "F3", "A3")),
            ("D2", ("D3", "F3", "A3", "C4")),
            ("G1", ("G2", "Bb2", "D3", "F3")),
        ),
        "motif": ((0.0, "G4", 0.040, 0.38), (1.5, "D5", 0.030, 0.64), (4.0, "F5", 0.035, 0.42), (6.5, "C5", 0.030, 0.60)),
        "pluckNotes": ("G3", "D4", "F4", "Bb4"),
    },
    {
        "key": "dungeon_curse_library",
        "seed": 303,
        "bpm": 78,
        "bars": 24,
        "brightness": 0.48,
        "density": 0.50,
        "pulse": 0.18,
        "percussion": 0.018,
        "air": 0.009,
        "highNote": "B5",
        "choirNote": "E4",
        "pianoNote": "B4",
        "sweepStart": "E4",
        "sweepEnd": "B5",
        "chords": (
            ("E2", ("E3", "G3", "B3", "F4")),
            ("F2", ("F3", "A3", "C4", "E4")),
            ("D2", ("D3", "F3", "A3", "E4")),
            ("E2", ("E3", "G3", "B3", "F4")),
            ("C2", ("C3", "E3", "G3", "B3")),
            ("A1", ("A2", "C3", "E3", "G3")),
            ("B1", ("B2", "D3", "F3", "A3")),
            ("E2", ("E3", "G3", "B3", "F4")),
        ),
        "motif": ((0.0, "E5", 0.040, 0.32), (2.0, "B4", 0.032, 0.68), (3.5, "F5", 0.034, 0.50), (6.0, "G5", 0.030, 0.42)),
        "pluckNotes": ("E4", "G4", "B4", "F5"),
    },
    {
        "key": "dungeon_ember_drake_pass",
        "seed": 404,
        "bpm": 112,
        "bars": 32,
        "brightness": 0.56,
        "density": 0.72,
        "pulse": 0.62,
        "percussion": 0.070,
        "metal": 0.035,
        "air": 0.006,
        "rootGain": 0.34,
        "highNote": "E5",
        "choirNote": "A4",
        "pianoNote": "E5",
        "sweepStart": "A3",
        "sweepEnd": "E5",
        "chords": (
            ("A1", ("A2", "C3", "E3", "G3")),
            ("A1", ("A2", "C3", "E3", "G3")),
            ("F2", ("F3", "A3", "C4", "E4")),
            ("G2", ("G3", "Bb3", "D4", "F4")),
            ("D2", ("D3", "F3", "A3", "C4")),
            ("F2", ("F3", "A3", "C4", "E4")),
            ("G2", ("G3", "Bb3", "D4", "F4")),
            ("A1", ("A2", "C3", "E3", "G3")),
        ),
        "motif": ((0.0, "A4", 0.038, 0.36), (2.0, "E5", 0.032, 0.64), (4.0, "C5", 0.036, 0.44), (6.0, "G5", 0.030, 0.58)),
        "pluckNotes": ("A3", "C4", "E4", "G4"),
    },
    {
        "key": "dungeon_star_ore_citadel",
        "seed": 505,
        "bpm": 90,
        "bars": 24,
        "brightness": 0.66,
        "density": 0.58,
        "pulse": 0.32,
        "percussion": 0.032,
        "metal": 0.055,
        "air": 0.006,
        "highNote": "C6",
        "choirNote": "F4",
        "pianoNote": "C6",
        "sweepStart": "F4",
        "sweepEnd": "C6",
        "chords": (
            ("F2", ("F3", "A3", "C4", "G4")),
            ("C2", ("C3", "E3", "G3", "D4")),
            ("Eb2", ("Eb3", "G3", "Bb3", "F4")),
            ("Bb1", ("Bb2", "D3", "F3", "C4")),
            ("G2", ("G3", "Bb3", "D4", "A4")),
            ("Eb2", ("Eb3", "G3", "Bb3", "F4")),
            ("C2", ("C3", "E3", "G3", "D4")),
            ("F2", ("F3", "A3", "C4", "G4")),
        ),
        "motif": ((0.0, "F5", 0.044, 0.30), (1.5, "C6", 0.038, 0.70), (4.0, "A5", 0.040, 0.42), (6.0, "G5", 0.034, 0.60)),
        "pluckNotes": ("F4", "A4", "C5", "G5"),
    },
    {
        "key": "dungeon_abyssal_grimoire_spire",
        "seed": 606,
        "bpm": 82,
        "bars": 24,
        "brightness": 0.58,
        "density": 0.76,
        "pulse": 0.42,
        "percussion": 0.042,
        "metal": 0.025,
        "air": 0.010,
        "rootGain": 0.36,
        "choirGain": 0.34,
        "highNote": "C#6",
        "choirNote": "F#4",
        "pianoNote": "C#6",
        "sweepStart": "F#3",
        "sweepEnd": "C#6",
        "chords": (
            ("F#1", ("F#2", "A2", "C#3", "E3")),
            ("D2", ("D3", "F3", "A3", "C4")),
            ("E2", ("E3", "G3", "B3", "D4")),
            ("F#1", ("F#2", "A2", "C#3", "E3")),
            ("B1", ("B2", "D3", "F#3", "A3")),
            ("D2", ("D3", "F3", "A3", "C4")),
            ("C#2", ("C#3", "E3", "G#3", "B3")),
            ("F#1", ("F#2", "A2", "C#3", "E3")),
        ),
        "motif": ((0.0, "F#5", 0.042, 0.34), (2.0, "C#5", 0.034, 0.66), (4.0, "A5", 0.038, 0.48), (6.0, "E5", 0.032, 0.56)),
        "pluckNotes": ("F#3", "A3", "C#4", "E4"),
    },
)


def main() -> None:
    for spec in TRACKS:
        path, duration = build_track(spec)
        print(f"Wrote {path}")
        print(f"Duration: {duration:.2f}s, sample rate: {SAMPLE_RATE}")


if __name__ == "__main__":
    main()
