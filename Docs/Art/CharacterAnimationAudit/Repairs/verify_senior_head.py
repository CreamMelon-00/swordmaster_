"""Verify destination art outside the two authored head/neck repairs."""
from pathlib import Path
from PIL import Image, ImageDraw
import hashlib
import json

root = Path(__file__).resolve().parents[4]
before = Path("C:/Users/User/.codex/visualizations/2026/10/16/01a1451f-e124-7450-9281-6a5fa77f3745/senior-before")
base = root / "Assets/Game/Resources/MobStudent/Animations/blunt-1"
docs = root / "Docs/Art/Animations/MobStudentAttacks/head-turn-v2/blunt-1"
out = Path(__file__).parent
results = []
for frame in (2, 3):
    path = base / f"upper-{frame}.png"
    original = Image.open(before / path.name).convert("RGBA")
    repaired = Image.open(path).convert("RGBA")
    points = [(x, y) for y in range(768) for x in range(1024)
              if original.getpixel((x, y)) != repaired.getpixel((x, y))]
    changed_white = [(x, y) for y in range(378, 411) for x in range(400, 490)
                     if min(original.getpixel((x, y))[:3]) > 100
                     and max(original.getpixel((x, y))[:3]) - min(original.getpixel((x, y))[:3]) < 30
                     and original.getpixel((x, y)) != repaired.getpixel((x, y))]
    weapon_right = 350 if frame == 2 else 360
    # The head itself extends left of weapon_right above y367/373. Only the
    # blade/hilt region below those levels is reserved for the weapon.
    weapon_top = 367 if frame == 2 else 373
    combined = Image.open(docs / f"combined-{frame}.png").convert("RGBA")
    combined_original = Image.open(before / f"combined-{frame}.png").convert("RGBA")
    point_set = set(points)
    check = {
        "frame": frame,
        "changedPixels": len(points),
        "boundsInclusive": [min(x for x, y in points), min(y for x, y in points),
                            max(x for x, y in points), max(y for x, y in points)],
        "beforeSha256": hashlib.sha256((before / path.name).read_bytes()).hexdigest(),
        "afterSha256": hashlib.sha256(path.read_bytes()).hexdigest(),
        "bodyBelowY400PixelsIdentical": original.crop((0, 400, 1024, 768)).tobytes() == repaired.crop((0, 400, 1024, 768)).tobytes(),
        "bladeAndGuardPixelsIdentical": original.crop((0, weapon_top, weapon_right + 1, 768)).tobytes() == repaired.crop((0, weapon_top, weapon_right + 1, 768)).tobytes(),
        "collarWhiteChangedPixels": len(changed_white),
        "collarWhiteChangedLocations": changed_white,
        "docsUpperPixelsIdentical": repaired.tobytes() == Image.open(docs / path.name).convert("RGBA").tobytes(),
        "docsCombinedRepairedPixelsIdentical": all(combined.getpixel(point) == repaired.getpixel(point) for point in points),
        "docsCombinedOutsideRepairPixelsIdentical": all(combined.getpixel((x, y)) == combined_original.getpixel((x, y))
                                                       for y in range(768) for x in range(1024) if (x, y) not in point_set),
    }
    results.append(check)
(out / "senior-validation.json").write_text(json.dumps(results, indent=2))
print(json.dumps(results))

# Compact before / after contact sheet, using nearest-neighbor enlargement.
sheet = Image.new("RGB", (4 * 480, 700), (38, 43, 53))
draw = ImageDraw.Draw(sheet)
for column, (frame, source) in enumerate(((2, before), (2, base), (3, before), (3, base))):
    im = Image.open(source / f"upper-{frame}.png").convert("RGBA")
    crop = im.crop((310, 190, 650, 580))
    bg = Image.new("RGBA", crop.size, (38, 43, 53, 255))
    bg.alpha_composite(crop)
    sheet.paste(bg.resize((480, 550), Image.Resampling.NEAREST), (column * 480, 50))
    draw.text((column * 480 + 15, 15), f"upper-{frame} {'before' if source == before else 'after'}", fill="white")
sheet.save(out / "senior-head-comparison.png")

frames = []
for frame in range(1, 5):
    im = Image.open(docs / f"combined-{frame}.png").convert("RGBA")
    bg = Image.new("RGBA", im.size, (38, 43, 53, 255))
    bg.alpha_composite(im)
    frames.append(bg.resize((768, 576), Image.Resampling.NEAREST))
frames[0].save(out / "senior-pommel-after.gif", save_all=True, append_images=frames[1:],
               duration=[120, 80, 100, 140], loop=0, disposal=2)
