"""Build the finite academy corridor from the approved battle backdrop.

The small canvas is the source of truth for new drawing. Every authored pixel
becomes a solid 4x4 square in the exported PNGs. No resampling/interpolation.
"""

from __future__ import annotations

from collections import Counter
from math import sqrt
from pathlib import Path

from PIL import Image, ImageDraw


HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
SOURCE = REPO / "Docs/Art/SchoolCorridor/Applied"
LAYER_NAMES = ("corridor-far", "corridor-near", "corridor-interior")
W, H, SCALE = 1024, 181, 4
AUTHORED = HERE / "Authored"
RUNTIME = HERE / "Runtime"
PREVIEW = HERE / "Preview"
TOP, BOTTOM = 160, 32


def rgba(color: str) -> tuple[int, int, int, int]:
    color = color.lstrip("#")
    return (*bytes.fromhex(color), 255)


def color_from_palette(palette: set[tuple[int, ...]], target: str) -> tuple[int, ...]:
    rgb = rgba(target)
    return min((c for c in palette if c[3] == 255), key=lambda c: sum((c[i] - rgb[i]) ** 2 for i in range(3)))


def hnoise(x: int, y: int, salt: int = 0) -> int:
    n = (x * 73856093) ^ (y * 19349663) ^ ((salt + 1) * 83492791)
    n ^= n >> 13
    n = (n * 1274126177) & 0xFFFFFFFF
    return (n ^ (n >> 16)) & 0xFFFF


def rectangle(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], color: tuple[int, ...]):
    draw.rectangle(box, fill=color)


def arch_top(x: int, center: int, half_width: int, crown: int, spring: int) -> int:
    t = min(1.0, abs(x - center) / half_width)
    return crown + round((spring - crown) * (1 - sqrt(max(0.0, 1 - t * t))))


def draw_end_wall(interior: Image.Image, palette: set[tuple[int, ...]]) -> None:
    p = lambda target: color_from_palette(palette, target)
    stone = p("D4A873")
    stone_mid = p("D1A26C")
    stone_light = p("DCB27C")
    stone_sun = p("F1BB6B")
    stone_shade = p("C3965C")
    stone_deep = p("AB7C4C")
    grout = p("966A41")
    wood_dark = p("54321C")
    wood = p("673B1D")
    wood_mid = p("80481E")
    wood_light = p("8C4F1E")
    gold = p("F3C172")
    iron = p("4B2D1A")
    shadow = p("4F301C")
    d = ImageDraw.Draw(interior)

    # Continue the existing floor from the precise source column where the
    # last full window bay ends. This gives a continuous tile rhythm.
    src = Image.open(SOURCE / "corridor-interior.png").convert("RGBA")
    for y in range(108, H):
        for x in range(763, W):
            interior.putpixel((x, y), src.getpixel(((244 + x - 763) * SCALE, y * SCALE)))

    # Hand drawn terminus wall: quiet ashlar, with a broad arch/alcove in its
    # own bay. Its first visible window finishes more than 600 px earlier.
    for y in range(108):
        for x in range(763, W):
            grain = hnoise(x // 3, y // 2)
            if y < 7:
                tone = stone_shade if grain % 7 else stone_mid
            elif y < 88:
                tone = stone
                if grain % 13 == 0:
                    tone = stone_mid
                elif grain % 19 == 0:
                    tone = stone_light
            elif y < 92:
                tone = stone_shade
            else:
                tone = stone_mid if grain % 8 else stone_shade
            interior.putpixel((x, y), tone)

    # Limestone courses and inset walnut lower panels.
    rectangle(d, (806, 5, 1023, 6), stone_deep)
    rectangle(d, (807, 7, 1023, 9), stone_light)
    rectangle(d, (806, 85, 1023, 86), stone_light)
    rectangle(d, (806, 87, 1023, 88), stone_deep)
    for y, offset in ((20, 0), (53, 25), (78, 0)):
        for x in range(807, 1024):
            if x < 884 or x > 976:
                if hnoise(x // 4, y) % 5 != 0:
                    interior.putpixel((x, y), stone_shade)
        for x in range(807 + offset, 1024, 74):
            for yy in range(max(10, y - 25), y):
                if x < 884 or x > 976:
                    interior.putpixel((x, yy), stone_shade)

    rectangle(d, (805, 98, 884, 106), wood_dark)
    rectangle(d, (810, 100, 876, 104), wood_mid)
    rectangle(d, (814, 101, 872, 103), wood)
    rectangle(d, (977, 98, 1015, 106), wood_dark)
    rectangle(d, (981, 100, 1011, 104), wood_mid)
    rectangle(d, (985, 101, 1007, 103), wood)
    rectangle(d, (802, 106, 1023, 108), grout)
    rectangle(d, (803, 108, 1023, 109), stone_light)

    # Pilaster hiding the architectural transition from the last window bay.
    # The source bay begins its next window at this edge. This darker return
    # face covers the entire next-window sliver and wood sill before the wide
    # terminal pier turns into the new end wall.
    rectangle(d, (750, 0, 765, 3), stone_shade)
    rectangle(d, (751, 4, 765, 6), stone_light)
    rectangle(d, (750, 7, 766, 96), stone_mid)
    rectangle(d, (750, 8, 752, 96), stone_deep)
    rectangle(d, (753, 8, 763, 96), stone)
    rectangle(d, (763, 8, 765, 96), stone_light)
    rectangle(d, (750, 95, 766, 98), stone_shade)
    rectangle(d, (748, 99, 767, 108), stone_mid)
    rectangle(d, (749, 106, 768, 109), stone_deep)
    rectangle(d, (756, 0, 807, 2), stone_shade)
    rectangle(d, (759, 3, 805, 5), stone_light)
    rectangle(d, (764, 6, 803, 96), stone)
    rectangle(d, (764, 8, 766, 96), stone_deep)
    rectangle(d, (767, 8, 769, 96), stone_shade)
    rectangle(d, (799, 8, 801, 96), stone_light)
    rectangle(d, (766, 38, 801, 39), stone_shade)
    rectangle(d, (765, 94, 804, 97), stone_shade)
    rectangle(d, (761, 98, 806, 107), stone_mid)
    rectangle(d, (762, 106, 807, 109), stone_deep)
    rectangle(d, (760, 109, 809, 110), stone_light)

    # Carry the existing gas-lamp silhouette into this terminal bay. Copy
    # only its dark metal and luminous glass pixels, never a hard rectangle
    # of the source wall, so the new masonry remains continuous.
    sx = 844
    for yy in range(42, 61):
        width = max(0, 10 - abs(yy - 51))
        for xx in range(sx - width, sx + width + 1):
            if 806 <= xx < 884:
                interior.putpixel((xx, yy), stone_light if yy < 53 else stone)
    for sy in range(25, 66):
        for source_x in range(108, 127):
            color = src.getpixel((source_x * SCALE, sy * SCALE))
            r, g, b, _ = color
            metal = r <= 185 and g <= 132 and b <= 85
            flame = r >= 249 and g >= 197 and b >= 100
            if (metal or flame) and abs(source_x - 117) < 10:
                interior.putpixel((sx + source_x - 117, sy), color)

    # A second terminal buttress closes the 3-screen map at the right edge.
    rectangle(d, (1009, 0, 1023, 106), stone_mid)
    rectangle(d, (1010, 0, 1012, 106), stone_deep)
    rectangle(d, (1013, 0, 1015, 106), stone_shade)
    rectangle(d, (1017, 0, 1020, 106), stone_light)
    rectangle(d, (1007, 95, 1023, 110), stone_shade)

    # Broad recessed stone arch. Crown at y=22 logical (88 export px);
    # walnut doors are 60 logical px = 240 output px wide.
    cx = 930
    for x in range(888, 973):
        top = arch_top(x, cx, 42, 22, 45)
        for y in range(top, 110):
            interior.putpixel((x, y), stone_shade if x < 930 else stone_light)
        for y in range(top, min(top + 3, 110)):
            interior.putpixel((x, y), stone_light if y == top else stone_mid)
        if (x - 888) % 10 in (0, 1):
            for y in range(top + 2, min(top + 8, 108)):
                interior.putpixel((x, y), stone_deep)
        if x % 13 == 0:
            for y in range(top + 4, min(top + 10, 108)):
                interior.putpixel((x, y), stone_mid)

    # Deep reveal; right and left jambs pick up opposite illumination.
    for x in range(896, 965):
        top = arch_top(x, cx, 34, 30, 47)
        for y in range(top, 111):
            interior.putpixel((x, y), shadow)
    rectangle(d, (895, 48, 899, 109), stone_deep)
    rectangle(d, (961, 48, 965, 109), stone_sun)
    rectangle(d, (899, 47, 900, 111), iron)
    rectangle(d, (960, 47, 961, 111), iron)

    # Two substantial timber leaves with a central joint and inset boards.
    for x in range(901, 960):
        top = arch_top(x, cx, 30, 33, 48)
        for y in range(top, 111):
            grain = hnoise(x // 2, y // 5, 2)
            c = wood_mid if (x // 3) % 4 == 0 else wood
            if grain % 17 == 0:
                c = wood_light
            elif grain % 13 == 0:
                c = wood_dark
            interior.putpixel((x, y), c)
        if (x - 902) % 10 in (0, 1):
            for y in range(max(top + 3, 50), 109):
                interior.putpixel((x, y), wood_dark)

    rectangle(d, (900, 50, 902, 110), wood_dark)
    rectangle(d, (958, 50, 960, 110), wood_dark)
    rectangle(d, (929, 34, 931, 111), wood_dark)
    rectangle(d, (931, 49, 932, 109), wood_light)
    # Upper and lower carved rectangular panels, with small brass hardware.
    for xl, xr in ((906, 925), (936, 954)):
        for yt, yb in ((56, 77), (84, 105)):
            rectangle(d, (xl, yt, xr, yb), wood_dark)
            rectangle(d, (xl + 2, yt + 2, xr - 2, yb - 2), wood_mid)
            rectangle(d, (xl + 3, yt + 3, xr - 3, yb - 3), wood)
            rectangle(d, (xl + 2, yt + 2, xr - 3, yt + 3), wood_light)
            # Uneven, low contrast grain drawn as connected strokes.
            for gx in (xl + 5, xl + 9, xr - 5):
                rectangle(d, (gx, yt + 5, gx, yb - 5), wood_mid)
                rectangle(d, (gx + 1, yt + 8, gx + 1, yb - 8), wood_light)
    rectangle(d, (924, 78, 927, 84), iron)
    rectangle(d, (933, 78, 936, 84), iron)
    rectangle(d, (925, 79, 926, 82), gold)
    rectangle(d, (934, 79, 935, 82), gold)
    for yy in (60, 99):
        rectangle(d, (901, yy, 904, yy + 2), iron)
        rectangle(d, (956, yy, 959, yy + 2), iron)
        rectangle(d, (901, yy, 902, yy), gold)
        rectangle(d, (958, yy, 959, yy), gold)

    # Faceted stone sill over the belt floor, usable contact area at y=122.
    rectangle(d, (894, 109, 966, 111), stone_deep)
    rectangle(d, (891, 112, 969, 115), stone_light)
    rectangle(d, (888, 116, 972, 118), stone_mid)
    rectangle(d, (888, 119, 972, 120), stone_shade)
    rectangle(d, (892, 121, 968, 122), grout)
    rectangle(d, (897, 123, 963, 124), stone_light)
    # The joint ends at the wooden base; the light threshold stays continuous.


def draw_left_cap(interior: Image.Image, palette: set[tuple[int, ...]]) -> None:
    p = lambda target: color_from_palette(palette, target)
    d = ImageDraw.Draw(interior)
    shade, mid, light, deep = p("AB7C4C"), p("D4A873"), p("DCB27C"), p("54321C")
    rectangle(d, (0, 0, 5, 106), deep)
    rectangle(d, (6, 0, 9, 106), shade)
    rectangle(d, (10, 0, 14, 106), mid)
    rectangle(d, (15, 1, 16, 104), light)
    rectangle(d, (0, 106, 18, 110), shade)
    rectangle(d, (1, 109, 19, 111), light)


def main() -> None:
    for directory in (AUTHORED, RUNTIME, PREVIEW):
        directory.mkdir(parents=True, exist_ok=True)
    source = {name: Image.open(SOURCE / f"{name}.png").convert("RGBA") for name in LAYER_NAMES}
    for name, image in source.items():
        assert image.size == (2172, 724), (name, image.size)
        assert all(image.getpixel((x * 4, y * 4)) == image.getpixel((x * 4 + 3, y * 4 + 3)) for y in range(181) for x in range(543)), name
    small = {name: source[name].resize((543, 181), Image.Resampling.NEAREST) for name in LAYER_NAMES}
    target = {name: Image.new("RGBA", (W, H), (0, 0, 0, 0)) for name in LAYER_NAMES}
    for name in LAYER_NAMES:
        # Exact original, then two whole bays. The original's first 24 columns
        # are an extra partial pier, so omit them at the join.
        target[name].paste(small[name], (0, 0))
        target[name].paste(small[name].crop((24, 0, 244, H)), (543, 0))
    palette = set(small["corridor-interior"].get_flattened_data())
    draw_left_cap(target["corridor-interior"], palette)
    draw_end_wall(target["corridor-interior"], palette)

    enlarged = {}
    for name in LAYER_NAMES:
        image = target[name].resize((W * SCALE, H * SCALE), Image.Resampling.NEAREST)
        image.save(AUTHORED / f"{name}.png")
        enlarged[name] = image
        padded = Image.new("RGBA", (W * SCALE, H * SCALE + TOP + BOTTOM), (0, 0, 0, 0))
        padded.paste(image, (0, TOP))
        padded.paste(image.crop((0, 0, W * SCALE, 1)).resize((W * SCALE, TOP), Image.Resampling.NEAREST), (0, 0))
        padded.paste(image.crop((0, H * SCALE - 1, W * SCALE, H * SCALE)).resize((W * SCALE, BOTTOM), Image.Resampling.NEAREST), (0, TOP + H * SCALE))
        padded.save(RUNTIME / f"{name}.png")

    composite = Image.alpha_composite(Image.alpha_composite(enlarged["corridor-far"], enlarged["corridor-near"]), enlarged["corridor-interior"])
    composite.save(PREVIEW / "corridor-composite.png")
    composite.resize((W, H), Image.Resampling.NEAREST).save(PREVIEW / "corridor-logical-1x.png")

    # The old 64-color, binary-alpha, 4x-grid contract is preserved per layer.
    for name, image in enlarged.items():
        colors = Counter(image.get_flattened_data())
        assert len(colors) <= 65, (name, len(colors))
        assert all(c[3] in (0, 255) for c in colors), name
        assert all(image.getpixel((x * 4, y * 4)) == image.getpixel((x * 4 + 3, y * 4 + 3)) for y in range(H) for x in range(W)), name
        runtime = Image.open(RUNTIME / f"{name}.png").convert("RGBA")
        assert runtime.crop((0, TOP, W * SCALE, TOP + H * SCALE)).tobytes() == image.tobytes(), name
    (HERE / "qa-report.txt").write_text(
        "PASS: 3 authored 4096x724 layers and 3 padded 4096x916 runtime layers.\n"
        "PASS: exact 4x grid; binary alpha; each layer at most 64 opaque palette colors.\n"
        "PASS: runtime y=160..883 exactly equals authored image, top=160 and bottom=32.\n"
        "Door: authored x=3600..3840, arch x=3552..3888, center x=3720; threshold y=432..499.\n"
        "Floor/wall seam y=432; finite left and right caps; last full window ends before x=3000.\n",
        encoding="utf-8",
    )
    print((HERE / "qa-report.txt").read_text(encoding="utf-8"))


if __name__ == "__main__":
    main()
