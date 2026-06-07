#!/usr/bin/env python3
from __future__ import annotations

import argparse
import csv
import json
import unicodedata
import uuid
from collections import deque
from pathlib import Path

from PIL import Image, ImageDraw


CLASS4_ENTRIES = [
    ("機竜ヴァルドレイク", "mecha_dragon_valdrake"),
    ("竜岩巨兵ドラグガイア", "drag_gaia"),
    ("竜剣聖アギト", "dragon_sword_saint_agito"),
    ("深淵竜魔導ヴァルフレア", "abyss_dragon_mage_valflare"),
    ("要塞機兵ギガフォート", "fortress_machine_gigafort"),
    ("機甲剣聖グランセイバー", "mecha_sword_saint_gransaber"),
    ("暗黒魔導機神メルキオン", "dark_magic_machine_god_merchion"),
    ("巨岩騎士ガイアス", "rock_knight_gaius"),
    ("星蝕魔像アストラルゴーレム", "astral_eclipse_golem"),
    ("魔剣聖ルシエル", "magic_sword_saint_luciel"),
    ("熾天使ミカエル", "seraph_michael"),
    ("精霊女王ティターニア", "spirit_queen_titania"),
]

POSES = [
    ("待機.png", "idle"),
    ("移動.png", "move"),
    ("攻撃.png", "attack"),
]

VALDRAKE_ATTACK_TARGET_BOUNDS = [
    (165, 184, 602, 568),
    (104, 184, 570, 568),
    (24, 168, 595, 568),
    (121, 185, 545, 569),
]

TITANIA_ATTACK_CANVAS = (675, 675)
TITANIA_ATTACK_BODY_TARGET_HEIGHT = 581
TITANIA_ATTACK_BODY_BOTTOM_MARGIN = 44
TITANIA_ATTACK_CONTENT_MARGIN = 8


def normalize_name(value: str) -> str:
    return (
        unicodedata.normalize("NFC", value)
        .replace("１", "1")
        .replace("２", "2")
        .replace("３", "3")
    )


def find_child(parent: Path, target_name: str) -> Path:
    target = normalize_name(target_name)
    for child in parent.iterdir():
        if child.is_dir() and normalize_name(child.name) == target:
            return child
    raise FileNotFoundError(f"missing directory: {target_name} in {parent}")


def find_pose_image(monster_dir: Path, preferred_name: str) -> Path:
    preferred = monster_dir / preferred_name
    if preferred.exists():
        return preferred
    if preferred_name == "待機.png":
        idle_sheet = monster_dir / "待機スプライト.png"
        if idle_sheet.exists():
            return idle_sheet
    raise FileNotFoundError(f"missing pose image: {preferred_name} in {monster_dir}")


def is_background_candidate(pixel: tuple[int, int, int, int]) -> bool:
    red, green, blue, alpha = pixel
    if alpha == 0:
        return False
    min_rgb = min(red, green, blue)
    max_rgb = max(red, green, blue)
    return min_rgb >= 180 and (max_rgb - min_rgb) <= 72


def clear_edge_background(image: Image.Image, remove_detached: bool = True) -> Image.Image:
    result = image.convert("RGBA")
    pixels = result.load()
    width, height = result.size
    visited = [[False for _ in range(width)] for _ in range(height)]
    queue: deque[tuple[int, int]] = deque()

    def enqueue(x: int, y: int) -> None:
        if x < 0 or y < 0 or x >= width or y >= height or visited[y][x]:
            return
        if is_background_candidate(pixels[x, y]):
            visited[y][x] = True
            queue.append((x, y))

    for x in range(width):
        enqueue(x, 0)
        enqueue(x, height - 1)
    for y in range(height):
        enqueue(0, y)
        enqueue(width - 1, y)

    while queue:
        x, y = queue.popleft()
        pixels[x, y] = (0, 0, 0, 0)
        for neighbor_y in range(max(0, y - 1), min(height - 1, y + 1) + 1):
            for neighbor_x in range(max(0, x - 1), min(width - 1, x + 1) + 1):
                if neighbor_x == x and neighbor_y == y:
                    continue
                enqueue(neighbor_x, neighbor_y)

    if not remove_detached:
        return result

    def clear_pixel(x: int, y: int) -> None:
        pixels[x, y] = (0, 0, 0, 0)

    def touches_transparent(x: int, y: int, radius: int) -> bool:
        for neighbor_y in range(max(0, y - radius), min(height - 1, y + radius) + 1):
            for neighbor_x in range(max(0, x - radius), min(width - 1, x + radius) + 1):
                if neighbor_x == x and neighbor_y == y:
                    continue
                if pixels[neighbor_x, neighbor_y][3] <= 8:
                    return True
        return False

    def is_detached_background_candidate(pixel: tuple[int, int, int, int]) -> bool:
        red, green, blue, alpha = pixel
        if alpha <= 8:
            return False
        min_rgb = min(red, green, blue)
        max_rgb = max(red, green, blue)
        saturation = max_rgb - min_rgb
        return (min_rgb >= 220 and saturation <= 52) or (min_rgb >= 205 and saturation <= 22)

    def collect_detached_component(
        start_x: int,
        start_y: int,
        component_visited: list[list[bool]],
    ) -> dict[str, object]:
        queue: deque[tuple[int, int]] = deque([(start_x, start_y)])
        component_visited[start_y][start_x] = True
        component_pixels: list[tuple[int, int]] = []
        sum_min_rgb = 0
        sum_saturation = 0
        component_touches_transparent = False

        while queue:
            x, y = queue.popleft()
            component_pixels.append((x, y))
            red, green, blue, _ = pixels[x, y]
            min_rgb = min(red, green, blue)
            max_rgb = max(red, green, blue)
            sum_min_rgb += min_rgb
            sum_saturation += max_rgb - min_rgb
            component_touches_transparent = component_touches_transparent or touches_transparent(x, y, 2)

            for neighbor_y in range(max(0, y - 1), min(height - 1, y + 1) + 1):
                for neighbor_x in range(max(0, x - 1), min(width - 1, x + 1) + 1):
                    if neighbor_x == x and neighbor_y == y:
                        continue
                    if component_visited[neighbor_y][neighbor_x]:
                        continue
                    if not is_detached_background_candidate(pixels[neighbor_x, neighbor_y]):
                        continue
                    component_visited[neighbor_y][neighbor_x] = True
                    queue.append((neighbor_x, neighbor_y))

        count = max(1, len(component_pixels))
        return {
            "pixels": component_pixels,
            "touches_transparent": component_touches_transparent,
            "average_min_rgb": sum_min_rgb // count,
            "average_saturation": sum_saturation // count,
        }

    component_visited = [row[:] for row in visited]
    minimum_detached_area = max(12, (width * height) // 20_000)
    for y in range(height):
        for x in range(width):
            if component_visited[y][x] or not is_detached_background_candidate(pixels[x, y]):
                continue
            component = collect_detached_component(x, y, component_visited)
            component_pixels = component["pixels"]
            assert isinstance(component_pixels, list)
            average_min_rgb = int(component["average_min_rgb"])
            average_saturation = int(component["average_saturation"])
            clearly_white_island = (
                len(component_pixels) >= minimum_detached_area
                and average_min_rgb >= 238
                and average_saturation <= 32
            )
            checkerboard_island = (
                len(component_pixels) >= minimum_detached_area
                and average_min_rgb >= 214
                and average_saturation <= 14
            )
            if component["touches_transparent"] or clearly_white_island or checkerboard_island:
                for component_x, component_y in component_pixels:
                    clear_pixel(component_x, component_y)

    def is_white_matte_fringe(pixel: tuple[int, int, int, int]) -> bool:
        red, green, blue, alpha = pixel
        if alpha <= 8:
            return False
        min_rgb = min(red, green, blue)
        max_rgb = max(red, green, blue)
        saturation = max_rgb - min_rgb
        return (min_rgb >= 230 and saturation <= 70) or (
            min_rgb >= 204 and saturation <= 32 and alpha <= 245
        )

    for _ in range(4):
        pixels_to_clear: list[tuple[int, int]] = []
        for y in range(height):
            for x in range(width):
                if is_white_matte_fringe(pixels[x, y]) and touches_transparent(x, y, 2):
                    pixels_to_clear.append((x, y))
        if not pixels_to_clear:
            break
        for x, y in pixels_to_clear:
            clear_pixel(x, y)

    for y in range(height):
        for x in range(width):
            red, green, blue, alpha = pixels[x, y]
            if alpha <= 8:
                clear_pixel(x, y)
                continue
            min_rgb = min(red, green, blue)
            max_rgb = max(red, green, blue)
            if alpha <= 24 and min_rgb >= 215 and (max_rgb - min_rgb) <= 60:
                clear_pixel(x, y)

    return result


def detected_frame_count(width: int, height: int) -> int:
    if width % 4 == 0:
        four_frame_width = width // 4
        cell_aspect = four_frame_width / max(1, height)
        if 0.6 <= cell_aspect <= 1.35:
            return 4

    estimated = max(1, round(width / max(1, height)))
    return estimated


def connected_components(image: Image.Image, alpha_threshold: int = 12) -> list[dict[str, int]]:
    alpha = image.getchannel("A")
    width, height = image.size
    visited = [[False for _ in range(width)] for _ in range(height)]
    components: list[dict[str, int]] = []

    for y in range(height):
        for x in range(width):
            if visited[y][x] or alpha.getpixel((x, y)) <= alpha_threshold:
                continue

            queue = deque([(x, y)])
            visited[y][x] = True
            area = 0
            min_x = max_x = x
            min_y = max_y = y
            pixels: list[tuple[int, int]] = []

            while queue:
                current_x, current_y = queue.popleft()
                pixels.append((current_x, current_y))
                area += 1
                min_x = min(min_x, current_x)
                max_x = max(max_x, current_x)
                min_y = min(min_y, current_y)
                max_y = max(max_y, current_y)

                for neighbor_y in range(max(0, current_y - 1), min(height - 1, current_y + 1) + 1):
                    for neighbor_x in range(max(0, current_x - 1), min(width - 1, current_x + 1) + 1):
                        if visited[neighbor_y][neighbor_x] or alpha.getpixel((neighbor_x, neighbor_y)) <= alpha_threshold:
                            continue
                        visited[neighbor_y][neighbor_x] = True
                        queue.append((neighbor_x, neighbor_y))

            components.append(
                {
                    "area": area,
                    "min_x": min_x,
                    "max_x": max_x,
                    "min_y": min_y,
                    "max_y": max_y,
                    "pixels_index": len(components),
                    "pixels": pixels,
                }
            )

    return components


def remove_neighbor_slivers(image: Image.Image) -> Image.Image:
    result = image.copy()
    pixels = result.load()
    width, _ = result.size
    components = connected_components(result)
    if not components:
        return result

    largest_area = max(component["area"] for component in components)
    for component in components:
        touches_left = component["min_x"] <= 1
        touches_right = component["max_x"] >= width - 2
        touches_horizontal_edge = touches_left or touches_right
        component_width = component["max_x"] - component["min_x"] + 1
        near_left_edge = touches_left and component["max_x"] < width * 0.24
        near_right_edge = touches_right and component["min_x"] > width * 0.76
        small_relative_area = component["area"] < largest_area * 0.18
        narrow_relative_width = component_width < width * 0.24
        is_neighbor_sliver = (
            touches_horizontal_edge
            and (near_left_edge or near_right_edge)
            and small_relative_area
            and narrow_relative_width
        )
        if not is_neighbor_sliver:
            continue
        for x, y in component["pixels"]:
            pixels[x, y] = (0, 0, 0, 0)

    return result


def pad_frame(image: Image.Image, padding: int) -> Image.Image:
    if padding <= 0:
        return image
    width, height = image.size
    result = Image.new("RGBA", (width + padding * 2, height + padding * 2), (0, 0, 0, 0))
    result.alpha_composite(image, (padding, padding))
    return result


def save_texture_meta(path: Path, template_meta: Path | None) -> None:
    meta_path = path.with_suffix(path.suffix + ".meta")
    if meta_path.exists():
        return

    guid = uuid.uuid4().hex
    if template_meta is not None and template_meta.exists():
        text = template_meta.read_text(encoding="utf-8")
        lines = []
        replaced = False
        for line in text.splitlines():
            if line.startswith("guid:"):
                lines.append(f"guid: {guid}")
                replaced = True
            else:
                lines.append(line)
        if replaced:
            meta_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
            return

    meta_path.write_text(
        f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
  isReadable: 1
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  spriteMode: 1
  spritePixelsToUnits: 100
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 8
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
""",
        encoding="utf-8",
    )


def content_bounds(image: Image.Image) -> tuple[int, int, int, int, int, int] | None:
    alpha = image.getchannel("A")
    width, height = image.size
    min_x = width
    min_y = height
    max_x = -1
    max_y = -1
    for y in range(height):
        for x in range(width):
            if alpha.getpixel((x, y)) <= 12:
                continue
            min_x = min(min_x, x)
            min_y = min(min_y, y)
            max_x = max(max_x, x)
            max_y = max(max_y, y)
    if max_x < min_x:
        return None
    return min_x, min_y, max_x, max_y, max_x - min_x + 1, max_y - min_y + 1


def edge_flags_for_bounds(bounds: tuple[int, int, int, int, int, int] | None, size: tuple[int, int]) -> str:
    if bounds is None:
        return "-"

    width, height = size
    flags = ""
    if bounds[0] <= 0:
        flags += "L"
    if bounds[2] >= width - 1:
        flags += "R"
    if bounds[1] <= 0:
        flags += "T"
    if bounds[3] >= height - 1:
        flags += "B"
    return flags or "-"


def edge_margins_for_bounds(bounds: tuple[int, int, int, int, int, int] | None, size: tuple[int, int]) -> tuple[int, int, int, int]:
    if bounds is None:
        return (0, 0, 0, 0)

    width, height = size
    return (
        bounds[0],
        bounds[1],
        width - 1 - bounds[2],
        height - 1 - bounds[3],
    )


def normalize_valdrake_attack_frame(image: Image.Image, frame_index: int) -> Image.Image:
    if frame_index < 0 or frame_index >= len(VALDRAKE_ATTACK_TARGET_BOUNDS):
        return image

    bounds = content_bounds(image)
    if bounds is None:
        return image

    target_min_x, target_min_y, target_max_x, target_max_y = VALDRAKE_ATTACK_TARGET_BOUNDS[frame_index]
    target_width = target_max_x - target_min_x + 1
    target_height = target_max_y - target_min_y + 1
    if target_width <= 0 or target_height <= 0:
        return image

    content = image.crop((bounds[0], bounds[1], bounds[2] + 1, bounds[3] + 1))
    content = content.resize((target_width, target_height), Image.Resampling.LANCZOS)
    normalized = Image.new("RGBA", image.size, (0, 0, 0, 0))
    normalized.alpha_composite(content, (target_min_x, target_min_y))
    return normalized


def largest_component_bounds(image: Image.Image, alpha_threshold: int = 12) -> tuple[int, int, int, int, int, int] | None:
    components = connected_components(image, alpha_threshold=alpha_threshold)
    if not components:
        return None

    largest = max(components, key=lambda component: component["area"])
    return (
        largest["min_x"],
        largest["min_y"],
        largest["max_x"],
        largest["max_y"],
        largest["max_x"] - largest["min_x"] + 1,
        largest["max_y"] - largest["min_y"] + 1,
    )


def clear_columns(image: Image.Image, x_start: int, x_end: int) -> Image.Image:
    result = image.copy()
    pixels = result.load()
    width, height = result.size
    start = max(0, min(width, x_start))
    end = max(start, min(width, x_end))
    for y in range(height):
        for x in range(start, end):
            pixels[x, y] = (0, 0, 0, 0)
    return result


def clear_rows(image: Image.Image, y_start: int, y_end: int) -> Image.Image:
    result = image.copy()
    pixels = result.load()
    width, height = result.size
    start = max(0, min(height, y_start))
    end = max(start, min(height, y_end))
    for y in range(start, end):
        for x in range(width):
            pixels[x, y] = (0, 0, 0, 0)
    return result


def fit_content_inside_canvas(image: Image.Image, margin: int) -> Image.Image:
    bounds = content_bounds(image)
    if bounds is None:
        return image

    canvas_width, canvas_height = image.size
    content_width = bounds[2] - bounds[0] + 1
    content_height = bounds[3] - bounds[1] + 1
    available_width = max(1, canvas_width - (margin * 2))
    available_height = max(1, canvas_height - (margin * 2))
    scale = min(1.0, available_width / content_width, available_height / content_height)
    crop = image.crop((bounds[0], bounds[1], bounds[2] + 1, bounds[3] + 1))
    scaled_size = (
        max(1, round(crop.width * scale)),
        max(1, round(crop.height * scale)),
    )
    if scaled_size != crop.size:
        crop = crop.resize(scaled_size, Image.Resampling.LANCZOS)

    original_center_x = (bounds[0] + bounds[2] + 1) * 0.5
    original_bottom_y = bounds[3] + 1
    paste_x = round(original_center_x - (crop.width * 0.5))
    paste_y = round(original_bottom_y - crop.height)
    paste_x = max(margin, min(canvas_width - margin - crop.width, paste_x))
    paste_y = max(margin, min(canvas_height - margin - crop.height, paste_y))

    fitted = Image.new("RGBA", image.size, (0, 0, 0, 0))
    fitted.alpha_composite(crop, (paste_x, paste_y))
    return fitted


def normalize_titania_attack_frame(image: Image.Image, frame_index: int) -> Image.Image:
    cleaned = image.convert("RGBA")
    if frame_index == 2:
        cleaned = clear_columns(cleaned, 0, 374)

    body_bounds = largest_component_bounds(cleaned)
    full_bounds = content_bounds(cleaned)
    if body_bounds is None or full_bounds is None or body_bounds[5] <= 0:
        return cleaned

    scale = TITANIA_ATTACK_BODY_TARGET_HEIGHT / body_bounds[5]
    full_content = cleaned.crop((full_bounds[0], full_bounds[1], full_bounds[2] + 1, full_bounds[3] + 1))
    scaled_size = (
        max(1, round(full_content.width * scale)),
        max(1, round(full_content.height * scale)),
    )
    scaled_content = full_content.resize(scaled_size, Image.Resampling.LANCZOS)

    body_center_x = ((body_bounds[0] + body_bounds[2] + 1) * 0.5 - full_bounds[0]) * scale
    body_bottom_y = (body_bounds[3] + 1 - full_bounds[1]) * scale
    canvas_width, canvas_height = TITANIA_ATTACK_CANVAS
    target_body_center_x = canvas_width * 0.5
    target_body_bottom_y = canvas_height - TITANIA_ATTACK_BODY_BOTTOM_MARGIN
    paste_x = round(target_body_center_x - body_center_x)
    paste_y = round(target_body_bottom_y - body_bottom_y)

    normalized = Image.new("RGBA", TITANIA_ATTACK_CANVAS, (0, 0, 0, 0))
    normalized.alpha_composite(scaled_content, (paste_x, paste_y))
    if frame_index == 3:
        normalized = clear_rows(
            normalized,
            0,
            canvas_height - TITANIA_ATTACK_BODY_BOTTOM_MARGIN - TITANIA_ATTACK_BODY_TARGET_HEIGHT,
        )
    normalized = fit_content_inside_canvas(normalized, TITANIA_ATTACK_CONTENT_MARGIN)
    return normalized


def remove_stale_frames(output_dir: Path, key: str, pose: str, frame_count: int) -> None:
    prefix = f"mon_{key}_{pose}_"
    for path in output_dir.glob(f"{prefix}*.png"):
        suffix = path.stem.removeprefix(prefix)
        if not suffix.isdigit() or int(suffix) < frame_count:
            continue
        path.unlink()
        meta_path = path.with_suffix(path.suffix + ".meta")
        if meta_path.exists():
            meta_path.unlink()


def repair_sheet(source: Path, output_dir: Path, key: str, pose: str, padding: int, apply: bool) -> dict[str, object]:
    image = clear_edge_background(Image.open(source), remove_detached=False)
    width, height = image.size
    frame_count = detected_frame_count(width, height)
    template_meta = output_dir / f"mon_{key}_{pose}_0.png.meta"
    frame_reports = []

    if apply:
        remove_stale_frames(output_dir, key, pose, frame_count)

    for index in range(frame_count):
        x0 = round(index * width / frame_count)
        x1 = round((index + 1) * width / frame_count)
        frame = image.crop((x0, 0, x1, height))
        frame = remove_neighbor_slivers(clear_edge_background(frame))
        source_bounds = content_bounds(frame)
        source_edge_flags = edge_flags_for_bounds(source_bounds, frame.size)
        source_margins = edge_margins_for_bounds(source_bounds, frame.size)
        frame = pad_frame(frame, padding)
        if key == "mecha_dragon_valdrake" and pose == "attack":
            frame = normalize_valdrake_attack_frame(frame, index)
        if key == "spirit_queen_titania" and pose == "attack":
            frame = normalize_titania_attack_frame(frame, index)
        destination = output_dir / f"mon_{key}_{pose}_{index}.png"
        if apply:
            destination.parent.mkdir(parents=True, exist_ok=True)
            frame.save(destination)
            save_texture_meta(destination, template_meta)

        bounds = content_bounds(frame)
        final_margins = edge_margins_for_bounds(bounds, frame.size)

        frame_reports.append(
            {
                "index": index,
                "width": frame.size[0],
                "height": frame.size[1],
                "source_opaque_bounds": source_bounds,
                "source_edge_flags": source_edge_flags,
                "source_edge_margins": source_margins,
                "opaque_bounds": bounds,
                "edge_flags": edge_flags_for_bounds(bounds, frame.size),
                "edge_margins": final_margins,
            }
        )

    return {
        "key": key,
        "pose": pose,
        "source": str(source),
        "source_width": width,
        "source_height": height,
        "frame_count": frame_count,
        "frames": frame_reports,
    }


def make_montage(report: list[dict[str, object]], output_dir: Path, montage_path: Path) -> None:
    cell_width = 150
    label_height = 28
    padding = 8
    rows: list[Image.Image] = []
    for row in report:
        key = str(row["key"])
        pose = str(row["pose"])
        frame_count = int(row["frame_count"])
        frames: list[Image.Image] = []
        for index in range(frame_count):
            path = output_dir / f"mon_{key}_{pose}_{index}.png"
            if not path.exists():
                continue
            frame = Image.open(path).convert("RGBA")
            checker = Image.new("RGBA", frame.size, (246, 246, 246, 255))
            draw = ImageDraw.Draw(checker)
            checker_size = max(8, frame.size[0] // 16)
            for y in range(0, frame.size[1], checker_size):
                for x in range(0, frame.size[0], checker_size):
                    if (x // checker_size + y // checker_size) % 2:
                        draw.rectangle(
                            [x, y, x + checker_size - 1, y + checker_size - 1],
                            fill=(224, 224, 224, 255),
                        )
            checker.alpha_composite(frame)
            preview_height = max(1, round(frame.size[1] * cell_width / frame.size[0]))
            frames.append(checker.resize((cell_width, preview_height), Image.Resampling.LANCZOS))

        if not frames:
            continue
        row_width = padding + frame_count * (cell_width + padding)
        row_height = label_height + padding + max(frame.height for frame in frames) + padding
        row_image = Image.new("RGBA", (row_width, row_height), (255, 255, 255, 255))
        draw = ImageDraw.Draw(row_image)
        draw.text((padding, 7), f"{key} {pose}", fill=(0, 0, 0, 255))
        x = padding
        y = label_height + padding
        for frame in frames:
            row_image.alpha_composite(frame, (x, y))
            x += cell_width + padding
        rows.append(row_image)

    if not rows:
        return
    montage_width = max(row.width for row in rows)
    montage_height = sum(row.height for row in rows)
    montage = Image.new("RGBA", (montage_width, montage_height), (255, 255, 255, 255))
    y = 0
    for row in rows:
        montage.alpha_composite(row, (0, y))
        y += row.height
    montage_path.parent.mkdir(parents=True, exist_ok=True)
    montage.save(montage_path)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-root", default="../モンスター一覧")
    parser.add_argument("--project-root", default="WitchTowerGame")
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--padding", type=int, default=24)
    parser.add_argument(
        "--keys",
        default="",
        help="Comma-separated monster keys to process. Defaults to every class 4 monster.",
    )
    parser.add_argument(
        "--strict-frame-edges",
        action="store_true",
        help="Exit non-zero when source cells touch a frame edge before padding.",
    )
    args = parser.parse_args()

    repo_root = Path(__file__).resolve().parents[1]
    source_root = (repo_root / args.source_root).resolve()
    project_root = (repo_root / args.project_root).resolve()
    class4_source_root = find_child(source_root, "4クラス")
    battle_output_dir = project_root / "Assets" / "Resources" / "MonsterBattle"
    reports_dir = repo_root / "tools" / "reports"

    selected_keys = {key.strip() for key in args.keys.split(",") if key.strip()}
    report: list[dict[str, object]] = []
    for source_folder, key in CLASS4_ENTRIES:
        if selected_keys and key not in selected_keys:
            continue
        monster_dir = find_child(class4_source_root, source_folder)
        for pose_file, pose in POSES:
            source = find_pose_image(monster_dir, pose_file)
            report.append(repair_sheet(source, battle_output_dir, key, pose, args.padding, args.apply))

    reports_dir.mkdir(parents=True, exist_ok=True)
    json_path = reports_dir / "class4_battle_sprite_repair_report.json"
    json_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

    csv_path = reports_dir / "class4_battle_sprite_repair_report.csv"
    with csv_path.open("w", newline="", encoding="utf-8") as csv_file:
        writer = csv.writer(csv_file)
        writer.writerow(
            [
                "key",
                "pose",
                "sourceWidth",
                "sourceHeight",
                "frameCount",
                "frameIndex",
                "frameWidth",
                "frameHeight",
                "sourceOpaqueWidth",
                "sourceOpaqueHeight",
                "sourceEdgeFlags",
                "sourceMarginLeft",
                "sourceMarginTop",
                "sourceMarginRight",
                "sourceMarginBottom",
                "opaqueWidth",
                "opaqueHeight",
                "edgeFlags",
                "marginLeft",
                "marginTop",
                "marginRight",
                "marginBottom",
            ]
        )
        source_edge_issues = []
        for row in report:
            for frame in row["frames"]:
                bounds = frame["opaque_bounds"]
                source_bounds = frame["source_opaque_bounds"]
                source_margins = frame["source_edge_margins"]
                final_margins = frame["edge_margins"]
                if frame["source_edge_flags"] != "-":
                    source_edge_issues.append((row["key"], row["pose"], frame["index"], frame["source_edge_flags"]))
                writer.writerow(
                    [
                        row["key"],
                        row["pose"],
                        row["source_width"],
                        row["source_height"],
                        row["frame_count"],
                        frame["index"],
                        frame["width"],
                        frame["height"],
                        source_bounds[4] if source_bounds else 0,
                        source_bounds[5] if source_bounds else 0,
                        frame["source_edge_flags"],
                        source_margins[0],
                        source_margins[1],
                        source_margins[2],
                        source_margins[3],
                        bounds[4] if bounds else 0,
                        bounds[5] if bounds else 0,
                        frame["edge_flags"],
                        final_margins[0],
                        final_margins[1],
                        final_margins[2],
                        final_margins[3],
                    ]
                )

    if args.apply:
        make_montage(report, battle_output_dir, reports_dir / "class4_battle_sprite_repair_montage.png")

    mode = "Applied" if args.apply else "Dry-run"
    print(f"{mode} class 4 battle sprite repair for {len(report)} sheets.")
    print(f"Report: {json_path}")
    print(f"CSV: {csv_path}")
    if source_edge_issues:
        print(f"Source frame edge warnings: {len(source_edge_issues)}")
        for key, pose, frame_index, flags in source_edge_issues[:24]:
            print(f"  {key} {pose}[{frame_index}] touches {flags}")
        if len(source_edge_issues) > 24:
            print(f"  ... {len(source_edge_issues) - 24} more")
        if args.strict_frame_edges:
            raise SystemExit(2)


if __name__ == "__main__":
    main()
