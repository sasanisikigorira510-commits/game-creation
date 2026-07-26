#!/usr/bin/env python3
"""Generate production-priority SE assets for WitchTowerGame.

The project can later replace these WAV files with DAW renders without code
changes, as long as the filenames stay the same.
"""

from __future__ import annotations

import hashlib
import math
import os
import random
import struct
import wave
from typing import Callable


SAMPLE_RATE = 44100
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SE_DIR = os.path.join(ROOT, "WitchTowerGame", "Assets", "Resources", "Audio", "SE")

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


StereoBuffer = tuple[list[float], list[float]]
Builder = Callable[[StereoBuffer], None]


def note_frequency(note: str) -> float:
    name = note[:-1]
    octave = int(note[-1])
    semitone_from_a4 = NOTE_INDEX[name] + (octave - 4) * 12
    return 440.0 * (2.0 ** (semitone_from_a4 / 12.0))


def smoothstep(x: float) -> float:
    x = max(0.0, min(1.0, x))
    return x * x * (3.0 - 2.0 * x)


def sine(freq: float, t: float, phase: float = 0.0) -> float:
    return math.sin((2.0 * math.pi * freq * t) + phase)


def soft_clip(value: float) -> float:
    return math.tanh(value * 1.28) / math.tanh(1.28)


def pan_gains(pan: float) -> tuple[float, float]:
    pan = max(0.0, min(1.0, pan))
    return math.cos(pan * math.pi * 0.5), math.sin(pan * math.pi * 0.5)


def envelope(age: float, duration: float, attack: float, release: float, decay: float) -> float:
    if age < 0.0 or age > duration:
        return 0.0
    attack_value = smoothstep(age / max(0.001, attack))
    release_value = smoothstep((duration - age) / max(0.001, release))
    return attack_value * release_value * math.exp(-age * decay)


def add_tone(
    buffer: StereoBuffer,
    start: float,
    duration: float,
    start_freq: float,
    end_freq: float | None = None,
    gain: float = 0.2,
    pan: float = 0.5,
    attack: float = 0.006,
    release: float = 0.035,
    decay: float = 0.0,
    phase: float = 0.0,
    harmonics: tuple[tuple[float, float], ...] = ((1.0, 1.0),),
) -> None:
    left, right = buffer
    end_freq = start_freq if end_freq is None else end_freq
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + duration) * SAMPLE_RATE))
    lgain, rgain = pan_gains(pan)
    sweep = end_freq - start_freq
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        local = age / max(0.001, duration)
        base_phase = 2.0 * math.pi * (start_freq * age + 0.5 * sweep * age * local) + phase
        sample = 0.0
        for multiple, amount in harmonics:
            sample += math.sin(base_phase * multiple) * amount
        sample *= envelope(age, duration, attack, release, decay) * gain
        left[i] += sample * lgain
        right[i] += sample * rgain


def add_chime(
    buffer: StereoBuffer,
    start: float,
    note: str,
    gain: float,
    pan: float,
    duration: float = 1.1,
    decay: float = 4.5,
) -> None:
    freq = note_frequency(note)
    add_tone(
        buffer,
        start,
        duration,
        freq,
        gain=gain,
        pan=pan,
        attack=0.004,
        release=0.11,
        decay=decay,
        harmonics=((1.0, 0.82), (2.01, 0.18), (3.02, 0.08), (4.98, 0.035)),
    )


def add_noise(
    buffer: StereoBuffer,
    start: float,
    duration: float,
    gain: float,
    pan: float,
    seed: int,
    attack: float = 0.002,
    release: float = 0.03,
    decay: float = 10.0,
    color: str = "bright",
) -> None:
    left, right = buffer
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + duration) * SAMPLE_RATE))
    lgain, rgain = pan_gains(pan)
    rng = random.Random(seed)
    low = 0.0
    previous = 0.0
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        raw = rng.random() * 2.0 - 1.0
        low = (low * 0.90) + (raw * 0.10)
        if color == "low":
            sample = low
        elif color == "body":
            sample = (raw * 0.28) + (low * 0.72)
        else:
            sample = raw - previous * 0.72
        previous = raw
        sample *= envelope(age, duration, attack, release, decay) * gain
        left[i] += sample * lgain
        right[i] += sample * rgain


def add_reverse_swell(
    buffer: StereoBuffer,
    start: float,
    duration: float,
    note: str,
    gain: float,
    pan: float,
) -> None:
    left, right = buffer
    freq = note_frequency(note)
    start_index = max(0, int(start * SAMPLE_RATE))
    end_index = min(len(left), int((start + duration) * SAMPLE_RATE))
    lgain, rgain = pan_gains(pan)
    for i in range(start_index, end_index):
        t = i / SAMPLE_RATE
        age = t - start
        p = smoothstep(age / duration)
        env = p * smoothstep((duration - age) / 0.08)
        tone = sine(freq, age) * 0.58 + sine(freq * 1.5, age, 0.7) * 0.21
        sample = tone * env * gain
        left[i] += sample * lgain
        right[i] += sample * rgain


def apply_delay(buffer: StereoBuffer, delay_seconds: float, gain: float, cross: float = 0.30) -> None:
    left, right = buffer
    delay = int(delay_seconds * SAMPLE_RATE)
    if delay <= 0:
        return
    source_left = left[:]
    source_right = right[:]
    for i in range(delay, len(left)):
        left[i] += ((source_left[i - delay] * (1.0 - cross)) + (source_right[i - delay] * cross)) * gain
        right[i] += ((source_right[i - delay] * (1.0 - cross)) + (source_left[i - delay] * cross)) * gain


def master(buffer: StereoBuffer, target_peak: float) -> StereoBuffer:
    left, right = buffer
    left_mean = sum(left) / max(1, len(left))
    right_mean = sum(right) / max(1, len(right))
    for i in range(len(left)):
        left[i] = soft_clip(left[i] - left_mean)
        right[i] = soft_clip(right[i] - right_mean)

    peak = max(max(abs(value) for value in left), max(abs(value) for value in right), 0.0001)
    scale = min(target_peak / peak, 5.0)
    for i in range(len(left)):
        left[i] = max(-1.0, min(1.0, left[i] * scale))
        right[i] = max(-1.0, min(1.0, right[i] * scale))
    return buffer


def write_wav(path: str, buffer: StereoBuffer) -> None:
    left, right = buffer
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as wav:
        wav.setnchannels(2)
        wav.setsampwidth(2)
        wav.setframerate(SAMPLE_RATE)
        frames = bytearray()
        for l_value, r_value in zip(left, right):
            frames += struct.pack("<h", int(max(-1.0, min(1.0, l_value)) * 32767))
            frames += struct.pack("<h", int(max(-1.0, min(1.0, r_value)) * 32767))
        wav.writeframes(frames)


def write_meta(path: str) -> None:
    rel = os.path.relpath(path, ROOT).replace(os.sep, "/")
    guid = hashlib.md5(("witchtower-se:" + rel).encode("utf-8")).hexdigest()
    meta = f"""fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 2
    quality: 1
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
    with open(path + ".meta", "w", encoding="utf-8") as handle:
        handle.write(meta)


def render(name: str, duration: float, builder: Builder, target_peak: float = 0.86, delay: bool = True) -> str:
    sample_count = int(duration * SAMPLE_RATE)
    buffer: StereoBuffer = ([0.0] * sample_count, [0.0] * sample_count)
    builder(buffer)
    if delay:
        apply_delay(buffer, 0.045, 0.13, 0.42)
        apply_delay(buffer, 0.092, 0.07, 0.55)
    buffer = master(buffer, target_peak)
    path = os.path.join(SE_DIR, name + ".wav")
    write_wav(path, buffer)
    write_meta(path)
    return path


def ui_click(buffer: StereoBuffer) -> None:
    add_chime(buffer, 0.000, "D6", 0.10, 0.48, duration=0.10, decay=20.0)
    add_noise(buffer, 0.000, 0.040, 0.035, 0.52, 10, decay=32.0)


def ui_confirm(buffer: StereoBuffer) -> None:
    add_chime(buffer, 0.000, "E5", 0.11, 0.42, duration=0.18, decay=11.0)
    add_chime(buffer, 0.054, "A5", 0.10, 0.58, duration=0.18, decay=10.0)
    add_chime(buffer, 0.094, "C6", 0.055, 0.50, duration=0.22, decay=8.0)


def ui_cancel(buffer: StereoBuffer) -> None:
    add_tone(buffer, 0.000, 0.11, note_frequency("C5"), note_frequency("G4"), 0.12, 0.52, decay=7.5)
    add_noise(buffer, 0.005, 0.075, 0.030, 0.50, 11, decay=18.0, color="body")


def error(buffer: StereoBuffer) -> None:
    add_tone(buffer, 0.000, 0.09, 180.0, 160.0, 0.16, 0.50, decay=6.0, harmonics=((1.0, 1.0), (1.98, 0.35)))
    add_tone(buffer, 0.078, 0.10, 150.0, 118.0, 0.14, 0.50, decay=6.0, harmonics=((1.0, 1.0), (1.98, 0.35)))
    add_noise(buffer, 0.000, 0.18, 0.030, 0.50, 12, decay=10.0, color="body")


def attack_swing(buffer: StereoBuffer) -> None:
    add_noise(buffer, 0.000, 0.13, 0.115, 0.38, 20, attack=0.001, release=0.026, decay=14.0, color="bright")
    add_tone(buffer, 0.010, 0.12, 980.0, 330.0, 0.070, 0.62, attack=0.002, release=0.025, decay=6.0)
    add_tone(buffer, 0.024, 0.08, 210.0, 138.0, 0.055, 0.50, attack=0.002, release=0.035, decay=8.0)


def hit_impact(buffer: StereoBuffer) -> None:
    add_tone(buffer, 0.000, 0.16, 112.0, 72.0, 0.30, 0.50, attack=0.001, release=0.040, decay=12.0)
    add_noise(buffer, 0.000, 0.12, 0.18, 0.49, 21, attack=0.001, release=0.025, decay=20.0, color="body")
    add_noise(buffer, 0.025, 0.09, 0.065, 0.58, 22, decay=17.0, color="bright")


def critical_hit(buffer: StereoBuffer) -> None:
    hit_impact(buffer)
    add_chime(buffer, 0.010, "A5", 0.13, 0.35, duration=0.45, decay=6.5)
    add_chime(buffer, 0.030, "E6", 0.105, 0.68, duration=0.42, decay=7.5)
    add_tone(buffer, 0.018, 0.20, 1560.0, 920.0, 0.080, 0.52, attack=0.002, release=0.055, decay=9.0)
    add_noise(buffer, 0.012, 0.19, 0.080, 0.50, 23, decay=15.0, color="bright")


def skill_cast(buffer: StereoBuffer) -> None:
    add_reverse_swell(buffer, 0.000, 0.20, "D4", 0.11, 0.48)
    add_tone(buffer, 0.070, 0.25, 360.0, 1280.0, 0.17, 0.50, attack=0.010, release=0.055, decay=2.5)
    for offset, note, pan in ((0.10, "A5", 0.36), (0.15, "D6", 0.64), (0.20, "F6", 0.45)):
        add_chime(buffer, offset, note, 0.045, pan, duration=0.36, decay=7.0)
    add_noise(buffer, 0.060, 0.28, 0.055, 0.52, 30, decay=6.0, color="bright")


def battle_start(buffer: StereoBuffer) -> None:
    add_tone(buffer, 0.000, 0.22, 86.0, 58.0, 0.24, 0.50, attack=0.003, release=0.045, decay=8.0)
    add_chime(buffer, 0.040, "D4", 0.10, 0.43, duration=0.55, decay=4.2)
    add_chime(buffer, 0.150, "A4", 0.10, 0.57, duration=0.54, decay=4.5)
    add_chime(buffer, 0.260, "D5", 0.095, 0.50, duration=0.50, decay=5.0)
    add_noise(buffer, 0.000, 0.36, 0.035, 0.50, 31, decay=4.5, color="body")


def victory_fanfare(buffer: StereoBuffer) -> None:
    notes = ("D5", "F5", "A5", "D6", "F6")
    for i, note in enumerate(notes):
        add_chime(buffer, i * 0.105, note, 0.105 - i * 0.006, 0.35 + (i % 2) * 0.30, duration=0.82, decay=3.9)
    add_tone(buffer, 0.000, 0.88, note_frequency("D3"), note_frequency("D3"), 0.075, 0.50, attack=0.030, release=0.18, decay=1.2)
    add_tone(buffer, 0.210, 0.48, note_frequency("A3"), note_frequency("A3"), 0.060, 0.50, attack=0.025, release=0.14, decay=1.6)


def defeat(buffer: StereoBuffer) -> None:
    for i, note in enumerate(("A4", "F4", "D4", "A3")):
        add_chime(buffer, i * 0.12, note, 0.090, 0.50, duration=0.62, decay=3.6)
    add_tone(buffer, 0.010, 0.55, 190.0, 82.0, 0.12, 0.50, attack=0.030, release=0.12, decay=2.2)
    add_noise(buffer, 0.030, 0.42, 0.035, 0.50, 32, decay=4.0, color="body")


def reward(buffer: StereoBuffer) -> None:
    for offset, note, pan in ((0.00, "A5", 0.35), (0.07, "C6", 0.65), (0.14, "E6", 0.50)):
        add_chime(buffer, offset, note, 0.095, pan, duration=0.42, decay=6.2)
    add_noise(buffer, 0.000, 0.20, 0.040, 0.50, 33, decay=8.0, color="bright")


def level_up(buffer: StereoBuffer) -> None:
    for i, note in enumerate(("D5", "F5", "A5", "C6", "D6", "F6")):
        add_chime(buffer, i * 0.085, note, 0.082, 0.32 + (i % 3) * 0.18, duration=0.65, decay=4.8)
    add_tone(buffer, 0.000, 0.72, note_frequency("D4"), note_frequency("A4"), 0.080, 0.50, attack=0.025, release=0.14, decay=1.5)


def equipment_drop(buffer: StereoBuffer) -> None:
    add_chime(buffer, 0.000, "E5", 0.135, 0.42, duration=0.55, decay=5.5)
    add_chime(buffer, 0.030, "B5", 0.085, 0.58, duration=0.45, decay=7.0)
    add_noise(buffer, 0.000, 0.18, 0.075, 0.50, 34, decay=10.0, color="bright")
    add_tone(buffer, 0.010, 0.30, 310.0, 220.0, 0.055, 0.50, decay=4.0)


def mission_complete(buffer: StereoBuffer) -> None:
    for i, note in enumerate(("G5", "B5", "D6", "G6")):
        add_chime(buffer, i * 0.095, note, 0.088, 0.38 + (i % 2) * 0.24, duration=0.55, decay=5.0)
    add_tone(buffer, 0.020, 0.48, note_frequency("G3"), note_frequency("G3"), 0.055, 0.50, attack=0.020, release=0.12, decay=1.7)


def daily_reward(buffer: StereoBuffer) -> None:
    for i, note in enumerate(("C5", "E5", "G5", "C6", "E6")):
        add_chime(buffer, i * 0.085, note, 0.078, 0.34 + (i % 2) * 0.30, duration=0.58, decay=4.7)
    add_noise(buffer, 0.030, 0.40, 0.030, 0.50, 35, decay=4.5, color="bright")


def summon_start(buffer: StereoBuffer) -> None:
    add_tone(buffer, 0.000, 0.56, 82.0, 118.0, 0.145, 0.50, attack=0.030, release=0.12, decay=1.8)
    add_reverse_swell(buffer, 0.050, 0.42, "D4", 0.120, 0.46)
    add_tone(buffer, 0.200, 0.38, 230.0, 720.0, 0.115, 0.54, attack=0.020, release=0.10, decay=1.0)
    add_noise(buffer, 0.060, 0.50, 0.060, 0.50, 40, decay=3.0, color="body")
    add_chime(buffer, 0.420, "A5", 0.075, 0.50, duration=0.50, decay=5.0)


def summon_reveal(buffer: StereoBuffer) -> None:
    add_reverse_swell(buffer, 0.000, 0.28, "A4", 0.105, 0.50)
    for offset, note, pan in ((0.22, "D5", 0.36), (0.32, "A5", 0.64), (0.43, "D6", 0.50)):
        add_chime(buffer, offset, note, 0.090, pan, duration=0.60, decay=4.9)
    add_noise(buffer, 0.200, 0.34, 0.052, 0.52, 41, decay=5.5, color="bright")


def summon_rare(buffer: StereoBuffer) -> None:
    add_reverse_swell(buffer, 0.000, 0.38, "F4", 0.135, 0.50)
    for offset, note, pan, gain in (
        (0.30, "F5", 0.32, 0.11),
        (0.40, "A5", 0.68, 0.10),
        (0.50, "C6", 0.42, 0.095),
        (0.62, "F6", 0.58, 0.085),
    ):
        add_chime(buffer, offset, note, gain, pan, duration=0.72, decay=4.2)
    add_tone(buffer, 0.360, 0.44, 360.0, 980.0, 0.080, 0.50, attack=0.020, release=0.11, decay=1.2)
    add_noise(buffer, 0.260, 0.54, 0.058, 0.50, 42, decay=4.4, color="bright")


def summon_legendary(buffer: StereoBuffer) -> None:
    add_tone(buffer, 0.000, 0.70, 64.0, 94.0, 0.135, 0.50, attack=0.040, release=0.16, decay=1.2)
    add_reverse_swell(buffer, 0.030, 0.55, "D4", 0.135, 0.50)
    for offset, note, pan, gain in (
        (0.44, "D5", 0.30, 0.11),
        (0.54, "F5", 0.70, 0.10),
        (0.64, "A5", 0.40, 0.10),
        (0.76, "D6", 0.60, 0.09),
        (0.90, "A6", 0.50, 0.075),
    ):
        add_chime(buffer, offset, note, gain, pan, duration=0.90, decay=3.8)
    add_tone(buffer, 0.500, 0.50, 420.0, 1320.0, 0.095, 0.50, attack=0.020, release=0.13, decay=1.0)
    add_noise(buffer, 0.390, 0.65, 0.065, 0.50, 43, decay=3.8, color="bright")


def fusion_start(buffer: StereoBuffer) -> None:
    add_tone(buffer, 0.000, 0.58, 72.0, 104.0, 0.165, 0.50, attack=0.030, release=0.12, decay=1.4)
    add_tone(buffer, 0.110, 0.46, 180.0, 520.0, 0.095, 0.46, attack=0.020, release=0.12, decay=1.2, harmonics=((1.0, 1.0), (1.5, 0.22)))
    add_reverse_swell(buffer, 0.120, 0.42, "G3", 0.11, 0.58)
    add_noise(buffer, 0.040, 0.50, 0.050, 0.50, 50, decay=3.8, color="body")


def fusion_mix(buffer: StereoBuffer) -> None:
    for offset, freq, pan in ((0.00, 120.0, 0.42), (0.18, 160.0, 0.58), (0.36, 220.0, 0.46)):
        add_tone(buffer, offset, 0.36, freq, freq * 1.45, 0.105, pan, attack=0.014, release=0.10, decay=2.7)
    add_reverse_swell(buffer, 0.260, 0.36, "D4", 0.090, 0.50)
    add_noise(buffer, 0.000, 0.72, 0.040, 0.50, 51, decay=2.6, color="body")


def fusion_success(buffer: StereoBuffer) -> None:
    add_tone(buffer, 0.000, 0.52, 82.0, 148.0, 0.130, 0.50, attack=0.030, release=0.14, decay=1.5)
    add_reverse_swell(buffer, 0.060, 0.42, "D4", 0.105, 0.50)
    for i, note in enumerate(("D5", "A5", "D6", "F6", "A6")):
        add_chime(buffer, 0.36 + i * 0.09, note, 0.096 - i * 0.006, 0.34 + (i % 2) * 0.30, duration=0.78, decay=4.0)
    add_noise(buffer, 0.310, 0.50, 0.055, 0.50, 52, decay=4.0, color="bright")


def upgrade_success(buffer: StereoBuffer) -> None:
    for offset, note, pan in ((0.00, "E5", 0.36), (0.075, "A5", 0.64), (0.15, "E6", 0.50)):
        add_chime(buffer, offset, note, 0.085, pan, duration=0.42, decay=5.8)
    add_noise(buffer, 0.000, 0.18, 0.036, 0.50, 60, decay=7.5, color="bright")


def upgrade_fail(buffer: StereoBuffer) -> None:
    add_tone(buffer, 0.000, 0.22, 170.0, 118.0, 0.155, 0.50, attack=0.006, release=0.060, decay=4.8)
    add_noise(buffer, 0.000, 0.18, 0.055, 0.50, 61, decay=8.0, color="body")


def upgrade_break(buffer: StereoBuffer) -> None:
    add_noise(buffer, 0.000, 0.15, 0.185, 0.46, 62, attack=0.001, release=0.040, decay=15.0, color="bright")
    add_noise(buffer, 0.080, 0.16, 0.105, 0.58, 63, attack=0.001, release=0.050, decay=10.0, color="bright")
    add_tone(buffer, 0.060, 0.42, 360.0, 74.0, 0.125, 0.50, attack=0.004, release=0.13, decay=3.6)


def enemy_defeat(buffer: StereoBuffer) -> None:
    add_noise(buffer, 0.000, 0.22, 0.120, 0.50, 70, attack=0.001, release=0.060, decay=8.0, color="body")
    add_tone(buffer, 0.010, 0.34, 520.0, 120.0, 0.100, 0.50, attack=0.003, release=0.095, decay=4.6)
    add_chime(buffer, 0.060, "D5", 0.035, 0.62, duration=0.34, decay=8.0)


def ally_defeat(buffer: StereoBuffer) -> None:
    for i, note in enumerate(("D4", "C4", "A3")):
        add_chime(buffer, i * 0.12, note, 0.085, 0.50, duration=0.55, decay=4.0)
    add_tone(buffer, 0.020, 0.48, 150.0, 74.0, 0.105, 0.50, attack=0.020, release=0.14, decay=2.2)
    add_noise(buffer, 0.050, 0.38, 0.032, 0.50, 71, decay=4.0, color="body")


ASSETS: tuple[tuple[str, float, Builder, float, bool], ...] = (
    ("ui_click", 0.12, ui_click, 0.70, False),
    ("ui_confirm", 0.24, ui_confirm, 0.76, True),
    ("ui_cancel", 0.22, ui_cancel, 0.70, False),
    ("error", 0.26, error, 0.76, False),
    ("attack_swing", 0.20, attack_swing, 0.78, False),
    ("hit_impact", 0.24, hit_impact, 0.82, False),
    ("critical_hit", 0.42, critical_hit, 0.86, True),
    ("skill_cast", 0.46, skill_cast, 0.82, True),
    ("battle_start", 0.62, battle_start, 0.84, True),
    ("victory_fanfare", 1.02, victory_fanfare, 0.84, True),
    ("defeat", 0.72, defeat, 0.78, True),
    ("reward", 0.50, reward, 0.78, True),
    ("level_up", 0.92, level_up, 0.84, True),
    ("equipment_drop", 0.56, equipment_drop, 0.80, True),
    ("mission_complete", 0.68, mission_complete, 0.82, True),
    ("daily_reward", 0.72, daily_reward, 0.82, True),
    ("summon_start", 0.80, summon_start, 0.84, True),
    ("summon_reveal", 0.82, summon_reveal, 0.84, True),
    ("summon_rare", 1.12, summon_rare, 0.86, True),
    ("summon_legendary", 1.34, summon_legendary, 0.88, True),
    ("fusion_start", 0.82, fusion_start, 0.84, True),
    ("fusion_mix", 0.86, fusion_mix, 0.82, True),
    ("fusion_success", 1.14, fusion_success, 0.86, True),
    ("upgrade_success", 0.48, upgrade_success, 0.80, True),
    ("upgrade_fail", 0.34, upgrade_fail, 0.74, False),
    ("upgrade_break", 0.62, upgrade_break, 0.82, True),
    ("enemy_defeat", 0.46, enemy_defeat, 0.78, True),
    ("ally_defeat", 0.64, ally_defeat, 0.76, True),
)


def main() -> None:
    os.makedirs(SE_DIR, exist_ok=True)
    outputs = []
    for name, duration, builder, peak, use_delay in ASSETS:
        outputs.append(render(name, duration, builder, target_peak=peak, delay=use_delay))

    print(f"Wrote {len(outputs)} SE assets to {SE_DIR}")
    for path in outputs:
        print(path)


if __name__ == "__main__":
    main()
