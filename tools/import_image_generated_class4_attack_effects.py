#!/usr/bin/env python3
from __future__ import annotations

import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageEnhance, ImageFilter


REPO_ROOT = Path(__file__).resolve().parents[1]
UNITY_ROOT = REPO_ROOT / "WitchTowerGame"
SOURCE_DIR = REPO_ROOT / "tools" / "generated_attack_effects" / "class4_sources"
OUTPUT_DIR = UNITY_ROOT / "Assets" / "Resources" / "BattleEffects" / "Monster"

CLASS4_EFFECT_KEYS = (
    "mecha_dragon_valdrake",
    "drag_gaia",
    "dragon_sword_saint_agito",
    "abyss_dragon_mage_valflare",
    "fortress_machine_gigafort",
    "mecha_sword_saint_gransaber",
    "dark_magic_machine_god_merchion",
    "rock_knight_gaius",
    "astral_eclipse_golem",
    "magic_sword_saint_luciel",
    "seraph_michael",
    "spirit_queen_titania",
    "spirit_queen_titania_staff_beam",
)

HORIZONTAL_BEAM_EFFECT_KEYS = {
    "spirit_queen_titania_staff_beam",
}

FRAME_STEPS = (
    {"scale": 0.68, "opacity": 0.48, "brightness": 0.86, "blur": 0.0},
    {"scale": 0.86, "opacity": 0.78, "brightness": 1.00, "blur": 0.0},
    {"scale": 1.00, "opacity": 1.00, "brightness": 1.14, "blur": 0.0},
    {"scale": 1.16, "opacity": 0.54, "brightness": 1.04, "blur": 0.55},
)

BEAM_FRAME_STEPS = (
    {"opacity": 0.74, "brightness": 0.92, "blur": 0.00, "vertical_scale": 0.96},
    {"opacity": 0.96, "brightness": 1.04, "blur": 0.00, "vertical_scale": 1.00},
    {"opacity": 1.00, "brightness": 1.16, "blur": 0.18, "vertical_scale": 1.04},
    {"opacity": 0.82, "brightness": 1.00, "blur": 0.32, "vertical_scale": 0.98},
)


def build_luminance_mask(image: Image.Image, threshold: int = 8) -> Image.Image:
    rgb = image.convert("RGB")
    channels = rgb.split()
    max_channel = Image.fromarray(
        np.maximum.reduce([np.asarray(channel) for channel in channels]).astype(np.uint8),
        "L",
    )
    return max_channel.point(lambda value: 255 if value > threshold else 0)


def crop_to_effect(image: Image.Image) -> Image.Image:
    mask = build_luminance_mask(image)
    bbox = mask.getbbox()
    if bbox is None:
        return image

    padding = 34
    left = max(0, bbox[0] - padding)
    top = max(0, bbox[1] - padding)
    right = min(image.width, bbox[2] + padding)
    bottom = min(image.height, bbox[3] + padding)
    return image.crop((left, top, right, bottom))


def fit_to_canvas(image: Image.Image, canvas_size: int = 512, effect_size: int = 430) -> Image.Image:
    ratio = effect_size / max(image.width, image.height)
    resized = image.resize(
        (max(1, round(image.width * ratio)), max(1, round(image.height * ratio))),
        Image.Resampling.LANCZOS,
    )
    canvas = Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 255))
    canvas.alpha_composite(
        resized,
        ((canvas_size - resized.width) // 2, (canvas_size - resized.height) // 2),
    )
    return canvas


def fit_to_horizontal_beam_canvas(
    image: Image.Image,
    canvas_width: int = 768,
    canvas_height: int = 192,
    effect_width: int = 744,
    effect_height: int = 172,
) -> Image.Image:
    ratio = min(effect_width / image.width, effect_height / image.height)
    resized = image.resize(
        (max(1, round(image.width * ratio)), max(1, round(image.height * ratio))),
        Image.Resampling.LANCZOS,
    )
    canvas = Image.new("RGBA", (canvas_width, canvas_height), (0, 0, 0, 255))
    canvas.alpha_composite(
        resized,
        ((canvas_width - resized.width) // 2, (canvas_height - resized.height) // 2),
    )
    return canvas


def black_to_alpha(image: Image.Image) -> Image.Image:
    arr = np.asarray(image.convert("RGBA")).astype(np.float32)
    rgb = arr[:, :, :3]
    max_channel = rgb.max(axis=2)
    alpha = np.clip((max_channel - 4.0) / 251.0, 0.0, 1.0)
    alpha = np.clip(np.power(alpha, 0.66) * 1.14, 0.0, 1.0)
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
    frame.alpha_composite(
        content,
        ((base.width - content.width) // 2, (base.height - content.height) // 2),
    )
    return frame


def transform_beam_frame(
    base: Image.Image,
    opacity: float,
    brightness: float,
    blur: float,
    vertical_scale: float,
) -> Image.Image:
    alpha = base.getchannel("A")
    bbox = alpha.getbbox()
    if bbox is None:
        return Image.new("RGBA", base.size, (0, 0, 0, 0))

    content = base.crop(bbox)
    target_size = (
        base.width,
        max(1, round(content.height * vertical_scale)),
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
    frame.alpha_composite(
        content,
        (0, (base.height - content.height) // 2),
    )
    return frame


def write_effect_frames(key: str) -> None:
    source_path = SOURCE_DIR / f"{key}.png"
    if not source_path.exists():
        raise FileNotFoundError(f"Missing Image2 class 4 source: {source_path}")

    source = Image.open(source_path).convert("RGBA")
    cropped_source = crop_to_effect(source)
    if key in HORIZONTAL_BEAM_EFFECT_KEYS:
        base = black_to_alpha(fit_to_horizontal_beam_canvas(cropped_source))
        steps = BEAM_FRAME_STEPS
        transform = transform_beam_frame
    else:
        base = black_to_alpha(fit_to_canvas(cropped_source))
        steps = FRAME_STEPS
        transform = transform_frame

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    for index, step in enumerate(steps):
        frame = transform(base, **step)
        frame.save(OUTPUT_DIR / f"fx_{key}_attack_{index}.png")


def main() -> None:
    keys = tuple(sys.argv[1:]) if len(sys.argv) > 1 else CLASS4_EFFECT_KEYS
    unknown_keys = sorted(set(keys) - set(CLASS4_EFFECT_KEYS))
    if unknown_keys:
        raise ValueError(f"Unknown class 4 Image2 effect key(s): {', '.join(unknown_keys)}")

    for key in keys:
        write_effect_frames(key)
        print(f"Imported Image2 class 4 attack effect: {key}")


if __name__ == "__main__":
    main()
