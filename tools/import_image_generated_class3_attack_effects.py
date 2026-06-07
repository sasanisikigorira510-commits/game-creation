#!/usr/bin/env python3
from __future__ import annotations

import shutil
from pathlib import Path

import numpy as np
from PIL import Image, ImageEnhance, ImageFilter, ImageOps


REPO_ROOT = Path(__file__).resolve().parents[1]
UNITY_ROOT = REPO_ROOT / "WitchTowerGame"
SOURCE_DIR = REPO_ROOT / "tools" / "generated_attack_effects" / "class3_sources"
OUTPUT_DIR = UNITY_ROOT / "Assets" / "Resources" / "BattleEffects" / "Monster"

GENERATED_SOURCES = {
    "abyss_dragon": Path("/Users/andou/.codex/generated_images/019e3304-b74c-78d1-981c-c51f1616032d/ig_04cc4105554ac711016a0b17942a9481918baf9fdf0d7fbaae.png"),
    "omega_leon": Path("/Users/andou/.codex/generated_images/019e3304-b74c-78d1-981c-c51f1616032d/ig_04cc4105554ac711016a0b17f3377881919b5c2c43d51cef5c.png"),
    "cosmic_ore_fortress_golem": Path("/Users/andou/.codex/generated_images/019e3304-b74c-78d1-981c-c51f1616032d/ig_04cc4105554ac711016a0b1840b89c819180d429166f8e944e.png"),
    "sword_saint_alvarez": Path("/Users/andou/.codex/generated_images/019e3304-b74c-78d1-981c-c51f1616032d/ig_04cc4105554ac711016a0b1892c134819186d3a661933f01f7.png"),
    "abyss_grand_mage_seraphis": Path("/Users/andou/.codex/generated_images/019e87ee-7cce-7f92-ac21-53404682c993/ig_0be927bc50a33355016a1ebaa6810081919713d6af399bf6dc.png"),
    "abyss_grand_mage_seraphis_orb": Path("/Users/andou/.codex/generated_images/019e87ee-7cce-7f92-ac21-53404682c993/ig_0be927bc50a33355016a1ec447985c819181b7a61e4c231dea.png"),
}

MIRRORED_SOURCE_KEYS = {"abyss_grand_mage_seraphis_orb"}

FRAME_STEPS = (
    {"scale": 0.70, "opacity": 0.50, "brightness": 0.86, "blur": 0.0},
    {"scale": 0.88, "opacity": 0.78, "brightness": 1.00, "blur": 0.0},
    {"scale": 1.00, "opacity": 1.00, "brightness": 1.12, "blur": 0.0},
    {"scale": 1.15, "opacity": 0.56, "brightness": 1.03, "blur": 0.45},
)


def ensure_local_source(key: str, generated_path: Path) -> Path:
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    local_path = SOURCE_DIR / f"{key}.png"
    if not local_path.exists() or key in MIRRORED_SOURCE_KEYS:
        if not generated_path.exists():
            raise FileNotFoundError(f"Missing generated source for {key}: {generated_path}")
        if key in MIRRORED_SOURCE_KEYS:
            ImageOps.mirror(Image.open(generated_path).convert("RGBA")).save(local_path)
        else:
            shutil.copy2(generated_path, local_path)
    return local_path


def build_luminance_mask(image: Image.Image, threshold: int = 8) -> Image.Image:
    rgb = image.convert("RGB")
    channels = rgb.split()
    max_channel = Image.fromarray(np.maximum.reduce([np.asarray(channel) for channel in channels]).astype(np.uint8), "L")
    return max_channel.point(lambda value: 255 if value > threshold else 0)


def crop_to_effect(image: Image.Image) -> Image.Image:
    mask = build_luminance_mask(image)
    bbox = mask.getbbox()
    if bbox is None:
        return image

    padding = 28
    left = max(0, bbox[0] - padding)
    top = max(0, bbox[1] - padding)
    right = min(image.width, bbox[2] + padding)
    bottom = min(image.height, bbox[3] + padding)
    return image.crop((left, top, right, bottom))


def fit_to_canvas(image: Image.Image, canvas_size: int = 512, effect_size: int = 430) -> Image.Image:
    ratio = effect_size / max(image.width, image.height)
    resized = image.resize((max(1, round(image.width * ratio)), max(1, round(image.height * ratio))), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 255))
    canvas.alpha_composite(resized, ((canvas_size - resized.width) // 2, (canvas_size - resized.height) // 2))
    return canvas


def black_to_alpha(image: Image.Image) -> Image.Image:
    arr = np.asarray(image.convert("RGBA")).astype(np.float32)
    rgb = arr[:, :, :3]
    max_channel = rgb.max(axis=2)
    alpha = np.clip((max_channel - 5.0) / 250.0, 0.0, 1.0)
    alpha = np.clip(np.power(alpha, 0.68) * 1.12, 0.0, 1.0)
    alpha[max_channel < 7.0] = 0.0

    straight_rgb = rgb / np.maximum(alpha[:, :, None], 0.08)
    straight_rgb = np.clip(straight_rgb, 0.0, 255.0)
    straight_rgb[alpha <= 0.0] = 0.0

    out = np.zeros_like(arr)
    out[:, :, :3] = straight_rgb
    out[:, :, 3] = alpha * 255.0
    return Image.fromarray(out.astype(np.uint8), "RGBA")


def transform_frame(base: Image.Image, scale: float, opacity: float, brightness: float, blur: float) -> Image.Image:
    alpha = base.getchannel("A")
    bbox = alpha.getbbox()
    if bbox is None:
        return Image.new("RGBA", base.size, (0, 0, 0, 0))

    content = base.crop(bbox)
    target_size = (
        max(1, round(content.width * scale)),
        max(1, round(content.height * scale)),
    )
    content = content.resize(target_size, Image.Resampling.LANCZOS)
    content = ImageEnhance.Brightness(content).enhance(brightness)

    if blur > 0:
        glow = content.filter(ImageFilter.GaussianBlur(blur))
        content = Image.alpha_composite(glow, content)

    arr = np.asarray(content).astype(np.float32)
    arr[:, :, 3] *= opacity
    content = Image.fromarray(np.clip(arr, 0.0, 255.0).astype(np.uint8), "RGBA")

    frame = Image.new("RGBA", base.size, (0, 0, 0, 0))
    frame.alpha_composite(content, ((base.width - content.width) // 2, (base.height - content.height) // 2))
    return frame


def write_effect_frames(key: str, source_path: Path) -> None:
    source = Image.open(source_path).convert("RGBA")
    base = black_to_alpha(fit_to_canvas(crop_to_effect(source)))

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    for index, step in enumerate(FRAME_STEPS):
        frame = transform_frame(base, **step)
        frame.save(OUTPUT_DIR / f"fx_{key}_attack_{index}.png")


def main() -> None:
    for key, generated_path in GENERATED_SOURCES.items():
        source_path = ensure_local_source(key, generated_path)
        write_effect_frames(key, source_path)
        print(f"Imported image-generated class 3 attack effect: {key}")


if __name__ == "__main__":
    main()
