"""Build the finite academy corridor from the approved battle backdrop.

The small canvas is the source of truth for new drawing. Every authored pixel
becomes a solid 4x4 square in the exported PNGs. No resampling/interpolation.
"""

from __future__ import annotations

from collections import Counter
from math import sqrt
from pathlib import Path

from PIL import Image, ImageDraw, ImageOps


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
    stone = p("C3925C")
    stone_mid = p("C9965E")
    stone_light = p("D6A162")
    stone_sun = p("F1BB6B")
    stone_shade = p("B48553")
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

    # Continue the source floor at its exact crop boundary. The exit has a
    # solid wall rather than another window, so remove the extra projected
    # window shapes while retaining the approved marble grain and grout.
    src = Image.open(SOURCE / "corridor-interior.png").convert("RGBA").resize((543, H), Image.Resampling.NEAREST)
    floor_colors = (p("D4A873"), p("D8AD79"), p("DCB27C"), p("DBAD73"))
    neutral = set(src.crop((300, 150, 543, H)).get_flattened_data())
    def ambient_floor(c, x, y):
        if c in neutral and c[0] < 235:
            return c
        # Palette mapping preserves connected source grain; it does not
        # introduce new point noise along the fading window projection.
        return floor_colors[(c[0] + 2 * c[1] + c[2]) % len(floor_colors)]
    for y in range(108, H):
        for x in range(763, W):
            c = src.getpixel((244 + x - 763, y))
            interior.putpixel((x, y), ambient_floor(c, x, y))
        # End the missing next-window projection along a clean diagonal,
        # consistent with sunlight falling down-left from the actual windows.
        for x in range(max(726, 750 - (y - 108) // 2), 763):
            c = interior.getpixel((x, y))
            interior.putpixel((x, y), ambient_floor(c, x, y))

    # A single quiet terminal bay begins after the last complete existing
    # pilaster. Reuse its architecture rather than adding a second wide pier.
    for y in range(108):
        for x in range(750, W):
            grain = hnoise(x // 3, y // 2)
            if y < 7:
                tone = stone_shade if grain % 7 else stone_mid
            elif y < 85:
                # Sample real limestone grain from the quiet source wall,
                # with deterministic patch offsets to prevent visible tiling.
                patch = hnoise(x // 9, y // 7, 24)
                donor_x = 111 + ((x + patch) % 15)
                donor_y = 16 + ((y + patch // 15) % 8)
                tone = src.getpixel((donor_x, donor_y))
                if tone[0] < 160 or tone[1] > 169:
                    tone = stone
                # Soft indirect daylight remains strongest beside the last
                # window, then settles across the uninterrupted solid wall.
                if x < 785 and y > 58 and (x + y) < 849:
                    tone = p("D39C5C") if grain % 4 else p("D6A162")
            elif y < 89:
                tone = stone_shade
            else:
                tone = stone_mid if grain % 8 else stone_shade
            interior.putpixel((x, y), tone)

    # Limestone courses and inset walnut lower panels.
    rectangle(d, (750, 5, 1023, 6), stone_deep)
    rectangle(d, (750, 7, 1023, 8), stone_light)
    rectangle(d, (750, 85, 1023, 86), stone_light)
    rectangle(d, (750, 87, 1023, 88), stone_deep)
    for y, offset in ((20, 0), (53, 25), (78, 0)):
        for x in range(751, 1000):
            if x < 884 or x > 976:
                if hnoise(x // 6, y) % 5 != 0:
                    interior.putpixel((x, y), stone_shade)
        for x in range(767 + offset, 1024, 64):
            for yy in range(max(10, y - 25), y):
                if x < 884 or x > 976:
                    interior.putpixel((x, yy), stone_shade)

    # The wainscot stays at the same height as all seven window bays. Small
    # wood variations and bevels avoid a thin, detached rectangular strip.
    for xl, xr in ((750, 885), (975, 1001)):
        rectangle(d, (xl, 89, xr, 106), wood_dark)
        rectangle(d, (xl + 2, 90, xr - 2, 91), wood_mid)
        rectangle(d, (xl + 3, 92, xr - 3, 104), wood)
        for yy in range(92, 105):
            for xx in range(xl + 3, xr - 2):
                grain = hnoise(xx // 3, yy // 2, 4)
                if grain % 7 == 0:
                    interior.putpixel((xx, yy), wood_mid)
                elif grain % 11 == 0:
                    interior.putpixel((xx, yy), wood_light)
        rectangle(d, (xl + 6, 94, xr - 6, 102), wood_dark)
        rectangle(d, (xl + 8, 95, xr - 8, 101), wood)
        rectangle(d, (xl + 8, 95, xr - 9, 95), wood_mid)
        rectangle(d, (xl + 2, 105, xr - 2, 105), wood_mid)
    rectangle(d, (750, 107, 1023, 108), grout)

    # Carry the last source pilaster all the way to its natural right edge.
    # It is the sole separator between the last window and the door bay.
    interior.paste(src.crop((207, 0, 231, 109)), (726, 0))

    # Carry the existing gas-lamp silhouette into this terminal bay. Copy
    # only its dark metal and luminous glass pixels, never a hard rectangle
    # of the source wall, so the new masonry remains continuous.
    sx = 827
    for yy in range(42, 61):
        width = max(0, 10 - abs(yy - 51))
        for xx in range(sx - width, sx + width + 1):
            if 750 <= xx < 884:
                interior.putpixel((xx, yy), stone_mid if yy < 53 else stone)
    for sy in range(25, 66):
        for source_x in range(108, 127):
            color = src.getpixel((source_x, sy))
            r, g, b, _ = color
            metal = r <= 185 and g <= 132 and b <= 85
            flame = r >= 249 and g >= 197 and b >= 100
            if (metal or flame) and abs(source_x - 117) < 10:
                interior.putpixel((sx + source_x - 117, sy), color)

    # The native textured final pier closes the finite drawing at the edge.
    interior.paste(src.crop((519, 0, 543, 109)), (1000, 0))

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

    # A shallow, textured indoor threshold sits directly on the marble.
    # There is no exterior stair or matching doorway geometry to impose.
    rectangle(d, (894, 109, 966, 110), stone_light)
    rectangle(d, (891, 111, 969, 112), p("D4A873"))
    rectangle(d, (892, 113, 968, 113), stone_shade)
    for xx in range(892, 969):
        if hnoise(xx // 3, 112, 12) % 3 == 0:
            interior.putpixel((xx, 111), p("DCB27C"))
        if hnoise(xx // 4, 114, 12) % 4 != 0:
            interior.putpixel((xx, 114), p("D1A26C"))


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


def extend_scenery(source: Image.Image) -> Image.Image:
    """Continue the view at its own edge, independently of the indoor window bays.

    Scenery moves behind the windows. An architectural crop can therefore become
    visible in the middle of a pane, and a transparent tail cannot be hidden by
    the fixed end wall. Mirrored continuation shares the exact joining column
    and fills the complete finite canvas without inventing or resampling pixels.
    """
    result = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    for index, left in enumerate(range(0, W, source.width)):
        piece = source if index % 2 == 0 else ImageOps.mirror(source)
        result.paste(piece, (left, 0))
    assert result.crop((0, 0, source.width, H)).tobytes() == source.tobytes()
    for y in range(H):
        assert result.getpixel((source.width - 1, y)) == result.getpixel((source.width, y))
    return result


def repair_floor_splice(interior: Image.Image, source: Image.Image) -> dict[str, int]:
    """Quilt the existing floor pixels across the architectural copy's overlap.

    The second group of bays starts at logical x=543 with source column 24.
    Its omitted columns 0..23 belong at x=519..542 and provide a real overlap
    with the first group. Choose a low-error, irregular cut through that narrow
    overlap instead of cutting sunlight and marble at the straight x=543 edge.
    No colour mixing, new drawing, reflection or resampling is involved.
    """
    assert interior.size == (W, H) and source.size == (543, H)
    left, right, top = 519, 543, 109
    width = right - left
    before = interior.copy()
    previous = [0.0] * width
    parents: list[list[int]] = []
    for y in range(top, H):
        costs, choices = [], []
        for j in range(width):
            a, b = before.getpixel((left + j, y)), source.getpixel((j, y))
            error = sum((a[c] - b[c]) ** 2 for c in range(3))
            if y == top:
                parent, cost = j, error
            else:
                parent = min(
                    range(max(0, j - 2), min(width, j + 3)),
                    key=lambda k: previous[k] + abs(k - j) * 100,
                )
                cost = error + previous[parent] + abs(parent - j) * 100
            costs.append(cost)
            choices.append(parent)
        previous = costs
        parents.append(choices)

    column = min(range(width), key=lambda j: previous[j])
    seam = [0] * (H - top)
    for row in range(H - top - 1, -1, -1):
        seam[row] = left + column
        column = parents[row][column]
    assert len(set(seam)) > 1, "Floor overlap must not introduce a straight cut"
    for y, cut in enumerate(seam, top):
        for x in range(cut, right):
            interior.putpixel((x, y), source.getpixel((x - left, y)))

    # Architecture, all other floor areas, and the existing sunlight direction
    # are retained: donor coordinates are translated only, never reflected.
    assert interior.crop((0, 0, W, top)).tobytes() == before.crop((0, 0, W, top)).tobytes()
    assert interior.crop((0, top, left, H)).tobytes() == before.crop((0, top, left, H)).tobytes()
    assert interior.crop((right, top, W, H)).tobytes() == before.crop((right, top, W, H)).tobytes()
    for y in range(top, H):
        assert interior.getpixel((right - 1, y)) == source.getpixel((23, y))
        assert interior.getpixel((right, y)) == source.getpixel((24, y))
    assert set(interior.get_flattened_data()).issubset(
        set(before.get_flattened_data()) | set(source.get_flattened_data())
    )
    return {"left": min(seam) * SCALE, "right": right * SCALE - 1,
            "top": top * SCALE, "bottom": H * SCALE - 1}


def main() -> None:
    for directory in (AUTHORED, RUNTIME, PREVIEW):
        directory.mkdir(parents=True, exist_ok=True)
    source = {name: Image.open(SOURCE / f"{name}.png").convert("RGBA") for name in LAYER_NAMES}
    for name, image in source.items():
        assert image.size == (2172, 724), (name, image.size)
        assert all(image.getpixel((x * 4, y * 4)) == image.getpixel((x * 4 + 3, y * 4 + 3)) for y in range(181) for x in range(543)), name
    small = {name: source[name].resize((543, 181), Image.Resampling.NEAREST) for name in LAYER_NAMES}
    target = {name: Image.new("RGBA", (W, H), (0, 0, 0, 0)) for name in LAYER_NAMES}
    for name in ('corridor-far', 'corridor-near'):
        target[name] = extend_scenery(small[name])
    # Only the fixed architecture joins by whole window bays. Its first 24
    # columns are an extra partial pier, so omit those at the architectural join.
    target['corridor-interior'].paste(small['corridor-interior'], (0, 0))
    target['corridor-interior'].paste(small['corridor-interior'].crop((24, 0, 244, H)), (543, 0))
    palette = set(small["corridor-interior"].get_flattened_data())
    draw_left_cap(target["corridor-interior"], palette)
    draw_end_wall(target["corridor-interior"], palette)
    floor_repair = repair_floor_splice(target['corridor-interior'], small['corridor-interior'])

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
    assert enlarged['corridor-far'].getchannel('A').getextrema() == (255, 255), 'Full-width opaque scenery required for parallax'
    assert composite.getchannel('A').getextrema() == (255, 255)
    composite.save(PREVIEW / "corridor-composite.png")
    composite.resize((W, H), Image.Resampling.NEAREST).save(PREVIEW / "corridor-logical-1x.png")
    composite.crop((2720, 0, W * SCALE, H * SCALE)).save(PREVIEW / "corridor-exit-detail.png")

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
        "PASS: outdoor scenery uses its own mirrored continuation at x=2172; joining columns match exactly.\n"
        "PASS: opaque far scenery fills all 4096px; no transparent tail behind moving windows.\n"
        f"PASS: floor splice uses a native-pixel irregular overlap at x={floor_repair['left']}..{floor_repair['right']}, y={floor_repair['top']}..{floor_repair['bottom']}; architecture unchanged.\n"
        "Door: authored x=3600..3840, arch x=3552..3888, center x=3720; shallow threshold y=436..459.\n"
        "Floor/wall seam y=432; finite left and right caps; last full window ends before x=3000.\n",
        encoding="utf-8",
    )
    print((HERE / "qa-report.txt").read_text(encoding="utf-8"))


if __name__ == "__main__":
    main()
