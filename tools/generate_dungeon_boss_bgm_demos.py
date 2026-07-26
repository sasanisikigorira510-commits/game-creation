#!/usr/bin/env python3
"""Generate dungeon-specific boss BGM loops for WitchTowerGame."""

from __future__ import annotations

import importlib.util
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
BASE_SCRIPT = ROOT / "tools" / "generate_dungeon_bgm_demos.py"


def load_base_module():
    spec = importlib.util.spec_from_file_location("dungeon_bgm_base", BASE_SCRIPT)
    module = importlib.util.module_from_spec(spec)
    if spec.loader is None:
        raise RuntimeError(f"Cannot load {BASE_SCRIPT}")
    spec.loader.exec_module(module)
    return module


BOSS_TRACKS = (
    {
        "key": "dungeon_blight_cavern_boss",
        "seed": 1101,
        "bpm": 118,
        "bars": 16,
        "brightness": 0.50,
        "density": 0.72,
        "pulse": 0.68,
        "percussion": 0.078,
        "metal": 0.030,
        "air": 0.007,
        "rootGain": 0.36,
        "choirGain": 0.30,
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
        ),
        "motif": ((0.0, "D5", 0.050, 0.32), (1.0, "A5", 0.040, 0.68), (2.0, "F5", 0.040, 0.42), (3.0, "E5", 0.034, 0.58)),
        "pluckNotes": ("D4", "F4", "A4", "C5"),
    },
    {
        "key": "dungeon_gear_crypt_boss",
        "seed": 1202,
        "bpm": 128,
        "bars": 16,
        "brightness": 0.58,
        "density": 0.78,
        "pulse": 0.74,
        "percussion": 0.088,
        "metal": 0.150,
        "air": 0.006,
        "rootGain": 0.38,
        "choirGain": 0.30,
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
        ),
        "motif": ((0.0, "G4", 0.046, 0.38), (1.0, "D5", 0.038, 0.64), (2.0, "F5", 0.038, 0.42), (3.0, "C5", 0.034, 0.60)),
        "pluckNotes": ("G3", "D4", "F4", "Bb4"),
    },
    {
        "key": "dungeon_curse_library_boss",
        "seed": 1303,
        "bpm": 104,
        "bars": 16,
        "brightness": 0.55,
        "density": 0.76,
        "pulse": 0.56,
        "percussion": 0.060,
        "metal": 0.050,
        "air": 0.010,
        "rootGain": 0.34,
        "choirGain": 0.34,
        "highNote": "B5",
        "choirNote": "E4",
        "pianoNote": "B4",
        "sweepStart": "E4",
        "sweepEnd": "B5",
        "chords": (
            ("E2", ("E3", "G3", "B3", "F4")),
            ("F2", ("F3", "A3", "C4", "E4")),
            ("D2", ("D3", "F3", "A3", "E4")),
            ("B1", ("B2", "D3", "F3", "A3")),
        ),
        "motif": ((0.0, "E5", 0.048, 0.32), (1.0, "B5", 0.038, 0.68), (2.0, "F5", 0.038, 0.50), (3.0, "G5", 0.034, 0.42)),
        "pluckNotes": ("E4", "G4", "B4", "F5"),
    },
    {
        "key": "dungeon_ember_drake_pass_boss",
        "seed": 1404,
        "bpm": 136,
        "bars": 16,
        "brightness": 0.62,
        "density": 0.86,
        "pulse": 0.78,
        "percussion": 0.100,
        "metal": 0.065,
        "air": 0.006,
        "rootGain": 0.42,
        "choirGain": 0.30,
        "highNote": "E5",
        "choirNote": "A4",
        "pianoNote": "E5",
        "sweepStart": "A3",
        "sweepEnd": "E5",
        "chords": (
            ("A1", ("A2", "C3", "E3", "G3")),
            ("F2", ("F3", "A3", "C4", "E4")),
            ("G2", ("G3", "Bb3", "D4", "F4")),
            ("A1", ("A2", "C3", "E3", "G3")),
        ),
        "motif": ((0.0, "A4", 0.046, 0.36), (1.0, "E5", 0.038, 0.64), (2.0, "C5", 0.040, 0.44), (3.0, "G5", 0.035, 0.58)),
        "pluckNotes": ("A3", "C4", "E4", "G4"),
    },
    {
        "key": "dungeon_star_ore_citadel_boss",
        "seed": 1505,
        "bpm": 116,
        "bars": 16,
        "brightness": 0.72,
        "density": 0.78,
        "pulse": 0.62,
        "percussion": 0.068,
        "metal": 0.120,
        "air": 0.007,
        "rootGain": 0.34,
        "choirGain": 0.34,
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
        ),
        "motif": ((0.0, "F5", 0.050, 0.30), (1.0, "C6", 0.040, 0.70), (2.0, "A5", 0.040, 0.42), (3.0, "G5", 0.036, 0.60)),
        "pluckNotes": ("F4", "A4", "C5", "G5"),
    },
    {
        "key": "dungeon_abyssal_grimoire_spire_boss",
        "seed": 1606,
        "bpm": 108,
        "bars": 16,
        "brightness": 0.66,
        "density": 0.88,
        "pulse": 0.68,
        "percussion": 0.076,
        "metal": 0.070,
        "air": 0.011,
        "rootGain": 0.44,
        "choirGain": 0.38,
        "highNote": "C#6",
        "choirNote": "F#4",
        "pianoNote": "C#6",
        "sweepStart": "F#3",
        "sweepEnd": "C#6",
        "chords": (
            ("F#1", ("F#2", "A2", "C#3", "E3")),
            ("D2", ("D3", "F3", "A3", "C4")),
            ("E2", ("E3", "G3", "B3", "D4")),
            ("C#2", ("C#3", "E3", "G#3", "B3")),
        ),
        "motif": ((0.0, "F#5", 0.050, 0.34), (1.0, "C#6", 0.040, 0.66), (2.0, "A5", 0.042, 0.48), (3.0, "E5", 0.036, 0.56)),
        "pluckNotes": ("F#3", "A3", "C#4", "E4"),
    },
)


def main() -> None:
    base = load_base_module()
    for track in BOSS_TRACKS:
        path, duration = base.build_track(track)
        print(f"Wrote {path}")
        print(f"Duration: {duration:.2f}s, sample rate: {base.SAMPLE_RATE}")


if __name__ == "__main__":
    main()
