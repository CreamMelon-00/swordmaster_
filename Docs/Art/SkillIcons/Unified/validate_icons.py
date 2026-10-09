"""Validate staged icon canvases, Unity sprite identity, and 4x runtime art."""
from __future__ import annotations

import json
from pathlib import Path
from PIL import Image
from build_icons import (
    ATLAS, META, OUT, SRC, LOGICAL, EXTENT, RUNTIME, PALETTE,
    sha, sprite_rects,
)


def png(path: Path) -> Image.Image:
    if not path.is_file():
        raise AssertionError(f"Missing {path}")
    return Image.open(path).convert("RGBA")


def check_binary_alpha(icon: Image.Image, label: str) -> None:
    if any(value not in (0, 255) for value in icon.getchannel("A").get_flattened_data()):
        raise AssertionError(f"{label}: antialiased alpha")


def check_atlas(path: Path, icons: dict[int, Image.Image], rects: dict) -> None:
    atlas = png(path)
    for n in range(1, 16):
        rect = rects[f"role{n}"]
        x, y, w, h = (rect[key] for key in ("x", "y", "width", "height"))
        crop = atlas.crop((x, atlas.height-y-h, x+w, atlas.height-y))
        if crop.tobytes() != icons[n].tobytes():
            raise AssertionError(f"{path.name}: role{n} crop mismatch")


def main() -> None:
    manifest = json.loads((OUT / "manifest.json").read_text(encoding="utf-8"))
    if manifest["logical_canvas"] != LOGICAL or manifest["logical_art_extent"] != EXTENT:
        raise AssertionError("Manifest logical specification mismatch")
    if manifest["runtime_canvas"] != RUNTIME:
        raise AssertionError("Manifest runtime scale mismatch")
    for filename, expected in manifest["source_sha256"].items():
        source_path = META if filename == META.name else (
            ATLAS if filename == ATLAS.name else SRC / filename)
        if sha(source_path) != expected:
            raise AssertionError(f"Source changed since generation: {filename}")
    current_rects = sprite_rects(META)
    for n in range(1, 16):
        old = manifest["source_atlas_rects"][str(n)]
        if current_rects[n]["spriteID"] != old["spriteID"]:
            raise AssertionError(f"role{n} spriteID changed")
        if current_rects[n]["internalID"] != old["internalID"]:
            raise AssertionError(f"role{n} internalID changed")

    icons48, icons192 = {}, {}
    for n in range(1, 21):
        icon48 = png(OUT / "48" / f"role{n}.png")
        icon32 = png(OUT / "32-comparison" / f"role{n}.png")
        icon192 = png(OUT / "Runtime192" / f"role{n}.png")
        if icon48.size != (48, 48) or icon32.size != (32, 32) or icon192.size != (192, 192):
            raise AssertionError(f"role{n}: inconsistent canvas size")
        for icon, label in ((icon48, "48"), (icon32, "32"), (icon192, "192")):
            check_binary_alpha(icon, f"role{n}/{label}")
        bbox = icon48.getchannel("A").getbbox()
        if bbox is None or max(bbox[2]-bbox[0], bbox[3]-bbox[1]) > EXTENT:
            raise AssertionError(f"role{n}: 48px silhouette extent invalid")
        if bbox != tuple(manifest["roles"][f"role{n}"]["48"]["output_bbox"]):
            raise AssertionError(f"role{n}: manifest bbox mismatch")
        if icon192.tobytes() != icon48.resize(
                (192, 192), Image.Resampling.NEAREST).tobytes():
            raise AssertionError(f"role{n}: 192px art differs from nearest 4x")
        if "override_sha256" not in manifest["roles"][f"role{n}"]["48"]:
            count = len({p[:3] for p in icon48.get_flattened_data() if p[3]})
            if count > PALETTE:
                raise AssertionError(f"role{n}: palette exceeds {PALETTE} colors")
        icons48[n], icons192[n] = icon48, icon192

    check_atlas(OUT / "proposed-atlas-1-15-48.png", icons48,
                manifest["proposed_atlas_48_rects_bottom_origin"])
    check_atlas(OUT / "proposed-atlas-1-15-192.png", icons192,
                manifest["proposed_atlas_192_rects_bottom_origin"])
    for filename in ("contact-48-native.png", "contact-32-native.png"):
        if not (OUT / filename).is_file():
            raise AssertionError(f"Missing {filename}")
    print("PASS: 20 logical icons, 20 runtime icons, both staged atlases, stable source identity, hard alpha and palette.")


if __name__ == "__main__":
    main()

