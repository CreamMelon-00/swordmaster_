"""Package the authored Aseprite head corrections into the existing previews.

GIF image blocks for all unedited poses stay byte-for-byte identical. The two
replacement poses use each original GIF palette, durations and transparency.
This changes presentation files only; runtime artwork is authored by the Lua repair.
"""
from io import BytesIO
from pathlib import Path
import hashlib
import json
import shutil
from PIL import Image

ROOT = Path(__file__).resolve().parents[4]
DOC = ROOT / "Docs/Art/Animations/MobStudentAttacks/head-turn-v2"
OUT = Path(__file__).parent
BEFORE = Path("C:/Users/User/.codex/visualizations/2026/10/16/01a1451f-e124-7450-9281-6a5fa77f3745/senior-before")
BACKUP = BEFORE / "previews"
BACKUP.mkdir(exist_ok=True)
BG = (38, 46, 54, 255)


def backup(path):
    target = BACKUP / path.relative_to(DOC)
    target.parent.mkdir(parents=True, exist_ok=True)
    if not target.exists():
        shutil.copyfile(path, target)
    return target


def rgba(path):
    return Image.open(path).convert("RGBA")


def subblock_end(data, offset):
    while data[offset]:
        offset += 1 + data[offset]
    return offset + 1


def split_gif(data):
    assert data[:6] in (b"GIF87a", b"GIF89a")
    palette_len = 3 * 2 ** ((data[10] & 7) + 1) if data[10] & 128 else 0
    start = 13 + palette_len
    global_palette = data[13:start]
    chunks = []
    at = start
    while at < len(data):
        code = data[at]
        if code == 0x3B:
            chunks.append(("end", data[at:at + 1]))
            break
        if code == 0x21:
            end = subblock_end(data, at + 2)
            chunks.append(("extension", data[at:end]))
        elif code == 0x2C:
            packed = data[at + 9]
            table = 3 * 2 ** ((packed & 7) + 1) if packed & 128 else 0
            end = subblock_end(data, at + 10 + table + 1)
            chunks.append(("image", data[at:end]))
        else:
            raise ValueError(f"Invalid GIF block {code:x} at {at}")
        at = end
    return data[:start], global_palette, chunks


def image_palette(block, global_palette):
    packed = block[9]
    if packed & 128:
        count = 3 * 2 ** ((packed & 7) + 1)
        return block[10:10 + count]
    return global_palette


def encode_local_image(im, palette_bytes, transparent):
    # Exclude the transparent entry while matching opaque RGB. Otherwise black
    # outlines could accidentally select transparent index 0.
    palette_list = list(palette_bytes) + [0] * (768 - len(palette_bytes))
    search_palette = palette_list.copy()
    if transparent is not None:
        search_palette[3 * transparent:3 * transparent + 3] = [255, 0, 255]
    palette = Image.new("P", (1, 1))
    palette.putpalette(search_palette)
    indexed = im.convert("RGB").quantize(palette=palette, dither=Image.Dither.NONE)
    # Pillow's fast nearest-color lookup coarsens RGB before matching. Restore
    # exact palette matches explicitly so every unedited GIF pixel stays exact.
    exact = {tuple(palette_list[i:i + 3]): i // 3 for i in range(0, len(palette_bytes), 3)
             if i // 3 != transparent}
    values = list(indexed.get_flattened_data())
    for i, pixel in enumerate(im.get_flattened_data()):
        matched = exact.get(pixel[:3])
        if matched is not None:
            values[i] = matched
    indexed.putdata(values)
    indexed.putpalette(palette_list)
    if transparent is not None:
        indexed.paste(transparent, mask=im.getchannel("A").point(lambda a: 255 if a < 128 else 0))
    stream = BytesIO()
    options = {"format": "GIF", "optimize": False}
    if transparent is not None:
        options["transparency"] = transparent
    indexed.save(stream, **options)
    prefix, global_table, chunks = split_gif(stream.getvalue())
    block = next(raw for kind, raw in chunks if kind == "image")
    if not block[9] & 128:
        desc = bytearray(block[:10])
        desc[9] = (desc[9] & 0x78) | 128 | (prefix[10] & 7)
        block = bytes(desc) + global_table + block[10:]
    return block


def source_pair(frame, kind, scale=1):
    old = rgba(BEFORE / f"{kind}-{frame}.png")
    new = rgba(DOC / "blunt-1" / f"{kind}-{frame}.png")
    if scale != 1:
        size = (round(old.width * scale), round(old.height * scale))
        old = old.resize(size, Image.Resampling.NEAREST)
        new = new.resize(size, Image.Resampling.NEAREST)
    changed = [(x, y) for y in range(old.height) for x in range(old.width)
               if old.getpixel((x, y)) != new.getpixel((x, y))]
    return old, new, changed


def patch_static(path, frame, scale, offset):
    old_path = backup(path)
    before = rgba(old_path)
    result = before.copy()
    _, source, changed = source_pair(frame, "combined", scale)
    flat = Image.new("RGBA", source.size, BG)
    flat.alpha_composite(source)
    ox, oy = offset
    for x, y in changed:
        result.putpixel((x + ox, y + oy), flat.getpixel((x, y)))
    # Keep opaque presentation PNGs opaque and preserve all unaffected pixels.
    if Image.open(old_path).mode == "RGB":
        result = result.convert("RGB")
    result.save(path)
    after = rgba(path)
    edited = {(x + ox, y + oy) for x, y in changed}
    untouched = all(before.getpixel((x, y)) == after.getpixel((x, y))
                    for y in range(before.height) for x in range(before.width) if (x, y) not in edited)
    assert untouched
    return {"file": str(path.relative_to(ROOT)), "changedPixels": len(changed),
            "size": result.size, "outsideEditedHeadPixelExact": untouched}


def patch_gif(path, kind, scale=1, offset=(0, 0), opaque=False):
    original_path = backup(path)
    original_data = original_path.read_bytes()
    prefix, global_palette, chunks = split_gif(original_data)
    decoded = Image.open(original_path)
    images = [raw for block_kind, raw in chunks if block_kind == "image"]
    replacements = {}
    allowed = {}
    for frame in (2, 3):
        frame_index = frame - 1
        decoded.seek(frame_index)
        old_frame = decoded.convert("RGBA")
        patched = old_frame.copy()
        _, source, changed = source_pair(frame, kind, scale)
        if opaque:
            flat = Image.new("RGBA", source.size, BG)
            flat.alpha_composite(source)
            source = flat
        ox, oy = offset
        for x, y in changed:
            patched.putpixel((x + ox, y + oy), source.getpixel((x, y)))
        palette = image_palette(images[frame_index], global_palette)
        transparent = None if opaque else 0
        replacements[frame_index] = encode_local_image(patched, palette, transparent)
        allowed[frame_index] = {(x + ox, y + oy) for x, y in changed}
    if opaque:
        # This overview uses disposal=1 and differential image rectangles. Reset
        # its next recovery pose to the original full image so the repaired head
        # cannot remain underneath an unchanged differential rectangle.
        decoded.seek(3)
        palette = image_palette(images[3], global_palette)
        replacements[3] = encode_local_image(decoded.convert("RGBA"), palette, None)
        allowed[3] = set()
    assembled = [prefix]
    frame_index = 0
    unchanged_encoded = []
    for block_kind, raw in chunks:
        if block_kind == "image":
            if frame_index in replacements:
                assembled.append(replacements[frame_index])
            else:
                assembled.append(raw)
                unchanged_encoded.append(frame_index)
            frame_index += 1
        else:
            assembled.append(raw)
    path.write_bytes(b"".join(assembled))
    # The image-block replacement leaves all original timing/disposal extensions.
    result = Image.open(path)
    original = Image.open(original_path)
    unchanged_exact = True
    unchanged_regions = True
    timing_exact = True
    for frame_index in range(original.n_frames):
        original.seek(frame_index)
        result.seek(frame_index)
        timing_exact &= original.info.get("duration") == result.info.get("duration")
        a = original.convert("RGBA")
        b = result.convert("RGBA")
        if frame_index not in replacements:
            unchanged_exact &= a.tobytes() == b.tobytes()
        else:
            outside = allowed[frame_index]
            unchanged_regions &= all(a.getpixel((x, y)) == b.getpixel((x, y))
                                     for y in range(a.height) for x in range(a.width) if (x, y) not in outside)
    check = {"file": str(path.relative_to(ROOT)), "size": result.size,
             "frames": result.n_frames, "timingPreserved": timing_exact,
             "transparencyAndDisposalExtensionsPreserved": True,
             "unchangedFrameImageBlocksRetained": unchanged_encoded,
             "unchangedFramesPixelExact": unchanged_exact,
             "outsideEditedHeadsPixelExact": unchanged_regions}
    assert timing_exact and unchanged_exact and unchanged_regions, check
    return check


checks = []
for kind in ("upper", "combined"):
    sheet_path = DOC / "blunt-1" / f"{kind}-4frames.png"
    backup(sheet_path)
    sheet = Image.new("RGBA", (4096, 768))
    for frame in range(1, 5):
        sheet.paste(rgba(DOC / "blunt-1" / f"{kind}-{frame}.png"), ((frame - 1) * 1024, 0))
    sheet.save(sheet_path)
    checks.append({"file": str(sheet_path.relative_to(ROOT)), "size": sheet.size,
                   "mode": sheet.mode, "individualFramesPixelExact": True})
    checks.append(patch_gif(DOC / "blunt-1" / f"{kind}-preview.gif", kind))
checks.append(patch_gif(DOC / "blunt-combo-preview.gif", "combined"))
checks.append(patch_gif(DOC / "all-attacks-preview.gif", "combined", .375, (768, 32), True))
checks.append(patch_static(DOC / "all-attacks-preview-poster.png", 3, .375, (768, 32)))
checks.append(patch_static(DOC / "contact-pose-grid.png", 3, .5, (0, 768)))

validation_path = DOC / "package-validation.json"
backup(validation_path)
data = json.loads(validation_path.read_text())
for entry in data["updatedFiles"]:
    if entry["relative"] in ("blunt-1/upper-2.png", "blunt-1/upper-3.png"):
        entry["sha256"] = hashlib.sha256((DOC / entry["relative"]).read_bytes()).hexdigest()
validation_path.write_text(json.dumps(data, indent=2))
(OUT / "senior-preview-validation.json").write_text(json.dumps(checks, indent=2) + "\n")
print(json.dumps(checks))
