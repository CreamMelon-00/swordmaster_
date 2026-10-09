"""Prepare consistent pixel-art skill icons without modifying Unity Assets.

Reads role1..15 from the Sprite rects in skill-role-atlas.png.meta and
role16..20 from standalone PNGs. Outputs 48px art, 32px comparison,
192px nearest-neighbor runtime candidates, contacts, and a manifest.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
SRC = HERE / "Sources"
RUNTIME_SRC = ROOT / "Assets/Game/Resources/SkillRoles"
ATLAS = SRC / "skill-role-atlas.png"
META = SRC / "skill-role-atlas.png.meta"
OUT = HERE / "Output"
LOGICAL = 48
EXTENT = 44
PALETTE = 48
RUNTIME = 192


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def sprite_rects(path: Path) -> dict[int, dict]:
    text = path.read_text(encoding="utf-8")
    pattern = re.compile(
        r"(?m)^    - serializedVersion: 2\s+name: role(?P<n>\d+)\s+rect:\s+"
        r"serializedVersion: 2\s+x: (?P<x>\d+)\s+y: (?P<y>\d+)\s+"
        r"width: (?P<w>\d+)\s+height: (?P<h>\d+)"
        r"[\s\S]*?spriteID: (?P<sprite_id>[a-f0-9]+)\s+"
        r"internalID: (?P<internal_id>\d+)"
    )
    found = {}
    for match in pattern.finditer(text):
        n = int(match["n"])
        if n in found:
            raise ValueError(f"Duplicate role{n} in atlas metadata")
        found[n] = dict(
            x=int(match["x"]), y=int(match["y"]), width=int(match["w"]),
            height=int(match["h"]), spriteID=match["sprite_id"],
            internalID=int(match["internal_id"])
        )
    if set(found) != set(range(1, 16)):
        raise ValueError(f"Expected role1..role15, found {sorted(found)}")
    if len({v["spriteID"] for v in found.values()}) != 15:
        raise ValueError("Sprite IDs are not unique")
    return found


def read_sources():
    atlas = Image.open(ATLAS).convert("RGBA")
    rects = sprite_rects(META)
    result = {}
    for n, rect in rects.items():
        x, y, w, h = (rect[key] for key in ("x", "y", "width", "height"))
        top = atlas.height - y - h
        if x < 0 or top < 0 or x + w > atlas.width or top + h > atlas.height:
            raise ValueError(f"role{n} rectangle exceeds atlas")
        result[n] = atlas.crop((x, top, x + w, top + h))
    hashes = {ATLAS.name: sha(ATLAS), META.name: sha(META)}
    for n in range(16, 21):
        path = SRC / f"role{n}.png"
        result[n] = Image.open(path).convert("RGBA")
        hashes[path.name] = sha(path)
        hashes[path.name + ".meta"] = sha(SRC / (path.name + ".meta"))
    return result, rects, hashes


def visible_bbox(source: Image.Image, threshold=64):
    bounds = source.getchannel("A").point(
        lambda alpha: 255 if alpha >= threshold else 0
    ).getbbox()
    if bounds is None:
        raise ValueError("Source icon is empty")
    return bounds


def normalize(source: Image.Image, size: int, extent: int, palette: int):
    bounds = visible_bbox(source)
    crop = source.crop(bounds)
    scale = min(extent / crop.width, extent / crop.height)
    dimensions = (max(1, round(crop.width * scale)),
                  max(1, round(crop.height * scale)))
    reduced = crop.resize(dimensions, Image.Resampling.LANCZOS)

    # All icons use one reduction recipe: limited colors, no dithering,
    # binary transparency, then centered in an identical square canvas.
    # The matte limits dark-edge fringes from partially transparent pixels.
    matte = Image.new("RGB", dimensions, (31, 27, 22))
    matte.paste(reduced, mask=reduced.getchannel("A"))
    quantized = matte.quantize(
        colors=palette, method=Image.Quantize.MEDIANCUT,
        dither=Image.Dither.NONE
    ).convert("RGBA")
    quantized.putalpha(reduced.getchannel("A").point(
        lambda alpha: 255 if alpha >= 112 else 0
    ))
    result = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    result.alpha_composite(
        quantized,
        ((size - dimensions[0]) // 2, (size - dimensions[1]) // 2)
    )
    final_bbox = result.getchannel("A").getbbox()
    if final_bbox is None or max(
        final_bbox[2] - final_bbox[0], final_bbox[3] - final_bbox[1]
    ) > extent:
        raise AssertionError("Art exceeds maximum visual extent")
    return result, dict(source_bbox=list(bounds), output_bbox=list(final_bbox),
                        opaque_colors=len({p[:3] for p in result.get_flattened_data() if p[3]}))


def external_override(base: Image.Image, n: int, directory: Path | None):
    if directory is None:
        return base, None
    path = directory / f"role{n}.png"
    if not path.exists():
        return base, None
    icon = Image.open(path).convert("RGBA")
    if icon.size != (LOGICAL, LOGICAL):
        raise ValueError(f"{path} must be 48x48")
    bbox = icon.getchannel("A").getbbox()
    if bbox is None or max(bbox[2]-bbox[0], bbox[3]-bbox[1]) > EXTENT:
        raise ValueError(f"{path} art must fit 44x44")
    if any(a not in (0, 255) for a in icon.getchannel("A").get_flattened_data()):
        raise ValueError(f"{path} alpha must be 0 or 255")
    return icon, sha(path)


def staged_atlas(icons: dict[int, Image.Image], size: int, path: Path):
    gutter = 4 if size == LOGICAL else 16
    width, height = 3 * size + 4 * gutter, 5 * size + 6 * gutter
    result = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    rects = {}
    for n in range(1, 16):
        col, row = (n - 1) % 3, (n - 1) // 3
        x, top = gutter + col * (size + gutter), gutter + row * (size + gutter)
        result.alpha_composite(icons[n], (x, top))
        rects[f"role{n}"] = dict(
            x=x, y=height - top - size, width=size, height=size
        )
    result.save(path)
    return rects


def contact(icons: dict[int, Image.Image], path: Path, title: str):
    image = Image.new("RGB", (5*88, 4*80 + 24), (37, 32, 28))
    draw = ImageDraw.Draw(image)
    font = ImageFont.load_default()
    draw.text((8, 6), title, fill=(232, 212, 168), font=font)
    for n in range(1, 21):
        col, row = (n - 1) % 5, (n - 1) // 5
        x, y = col*88, 24+row*80
        draw.rectangle((x+3, y+3, x+83, y+75), fill=(52, 45, 38),
                       outline=(109, 84, 55))
        icon = icons[n]
        image.paste(icon, (x+19+(48-icon.width)//2, y+6+(48-icon.height)//2),
                    icon.getchannel("A"))
        draw.text((x+27, y+58), f"role{n:02d}", fill=(224, 208, 174),
                  font=font)
    image.save(path)


def gold_display(icons: dict[int, Image.Image], path: Path):
    """Actual 40/48/52px display samples for the two densest gold icons."""
    widths = (40, 48, 52)
    image = Image.new("RGB", (3*94, 2*100 + 24), (37, 32, 28))
    draw = ImageDraw.Draw(image)
    font = ImageFont.load_default()
    draw.text((8, 6), "Gold detail at displayed size", fill=(232, 212, 168), font=font)
    for row, n in enumerate((18, 20)):
        for col, size in enumerate(widths):
            x, y = col*94, 24+row*100
            draw.rectangle((x+3, y+2, x+90, y+94), fill=(52, 45, 38),
                           outline=(109, 84, 55))
            rendered = icons[n].resize((size, size), Image.Resampling.NEAREST)
            image.paste(rendered, (x+(94-size)//2, y+10), rendered.getchannel("A"))
            draw.text((x+20, y+72), f"role{n} {size}px", fill=(224, 208, 174),
                      font=font)
    image.save(path)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--override-dir", type=Path,
                        help="Optional 48x48 roleN.png replacements; intended for refined role18/20")
    args = parser.parse_args()
    directory = args.override_dir.resolve() if args.override_dir else None
    if directory is not None and not directory.is_dir():
        raise ValueError(f"Override directory does not exist: {directory}")

    sources, source_rects, source_hashes = read_sources()
    out48 = OUT / "48"
    out32 = OUT / "32-comparison"
    out192 = OUT / "Runtime192"
    for path in (out48, out32, out192):
        path.mkdir(parents=True, exist_ok=True)

    icons48, icons32, icons192, reports = {}, {}, {}, {}
    for n in range(1, 21):
        icon48, report48 = normalize(sources[n], LOGICAL, EXTENT, PALETTE)
        icon48, override_hash = external_override(icon48, n, directory)
        if override_hash:
            report48["override_sha256"] = override_hash
            report48["output_bbox"] = list(icon48.getchannel("A").getbbox())
            report48["opaque_colors"] = len({p[:3] for p in icon48.get_flattened_data() if p[3]})
        icon32, report32 = normalize(sources[n], 32, 29, 32)
        icon192 = icon48.resize((RUNTIME, RUNTIME), Image.Resampling.NEAREST)
        icons48[n], icons32[n], icons192[n] = icon48, icon32, icon192
        icon48.save(out48 / f"role{n}.png")
        icon32.save(out32 / f"role{n}.png")
        icon192.save(out192 / f"role{n}.png")
        reports[f"role{n}"] = {"48": report48, "32": report32}

    contact(icons48, OUT / "contact-48-native.png",
            "48px logical / 44px art extent")
    contact(icons32, OUT / "contact-32-native.png",
            "32px logical / 29px art extent")
    gold_display(icons48, OUT / "gold-display-40-48-52.png")
    proposed48 = staged_atlas(
        icons48, LOGICAL, OUT / "proposed-atlas-1-15-48.png")
    proposed192 = staged_atlas(
        icons192, RUNTIME, OUT / "proposed-atlas-1-15-192.png")
    manifest = dict(
        source_sha256=source_hashes,
        source_atlas_rects=source_rects,
        logical_canvas=LOGICAL, logical_art_extent=EXTENT,
        base_palette_limit=PALETTE, runtime_canvas=RUNTIME,
        roles=reports,
        proposed_atlas_48_rects_bottom_origin=proposed48,
        proposed_atlas_192_rects_bottom_origin=proposed192
    )
    (OUT / "manifest.json").write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print("Built 20 logical icons, 32px comparisons, 192px runtime candidates, contacts, and staged atlases.")


if __name__ == "__main__":
    main()






