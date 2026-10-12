"""Build the finite school-to-forest exploration background.

Run with the bundled Python runtime (Pillow required). All marks are made on the
543x181 / 1024x181 logical pixel grid and enlarged exactly 4x for the game.
The four source layers remain separately editable in Aseprite.
"""

from __future__ import annotations

from collections import Counter
from math import sqrt
from pathlib import Path
import random

from PIL import Image, ImageDraw, ImageOps


HERE = Path(__file__).resolve().parent
SOURCE_FOREST = HERE.parents[1] / "DawnForest" / "Processed"
CORRIDOR_DOOR = HERE.parent / "Corridor" / "Authored" / "corridor-interior.png"
SOURCE_OUT = HERE / "Source"
RUNTIME_OUT = HERE / "Runtime"
PREVIEW_OUT = HERE / "Preview"
W, H, SCALE = 1024, 181, 4
SOURCE_W = 543
NAMES = ("forest-far-mist", "forest-far-trees", "forest-belt-mid", "forest-near")

# Corridor door details contain a few one-pixel lighting colors. Fold only
# those into adjacent tones so the authored belt layer stays at <=64 colors.
PALETTE_FOLD = {
    (233, 179, 104, 255): (243, 193, 114, 255),
    (180, 133, 83, 255): (171, 124, 76, 255),
    (193, 139, 83, 255): (195, 146, 92, 255),
    (231, 173, 94, 255): (223, 171, 79, 255),
    (229, 173, 99, 255): (243, 193, 114, 255),
    (186, 140, 90, 255): (195, 146, 92, 255),
    (191, 140, 85, 255): (195, 146, 92, 255),
    (79, 48, 28, 255): (84, 50, 28, 255),
    (75, 45, 26, 255): (84, 50, 28, 255),
}

# The school stone and walnut reuse the applied corridor's actual colors.
STONE = (212, 168, 115, 255)       # #D4A873
STONE_SUN = (220, 178, 124, 255)   # #DCB27C
STONE_LIGHT = (243, 193, 114, 255)
STONE_MUTED = (195, 146, 92, 255)
STONE_SHADE = (171, 124, 76, 255)
STONE_LINE = (145, 99, 58, 255)
STONE_DARK = (102, 73, 46, 255)
WOOD_DEEP = (84, 50, 28, 255)
WOOD = (103, 59, 29, 255)         # #673B1D
WOOD_MID = (138, 84, 45, 255)     # #8A542D
WOOD_LIGHT = (180, 121, 62, 255)
BRASS = (223, 171, 79, 255)
IRON = (49, 45, 37, 255)
GLASS = (74, 112, 105, 255)
GLASS_LIGHT = (140, 167, 145, 255)


def logical_base(name: str) -> Image.Image:
    src = Image.open(SOURCE_FOREST / f"{name}.png").convert("RGBA")
    if src.size != (SOURCE_W * SCALE, H * SCALE):
        raise ValueError(f"Unexpected dawn forest size: {name} {src.size}")
    src = src.resize((SOURCE_W, H), Image.Resampling.NEAREST)
    out = Image.new("RGBA", (W, H))
    out.paste(src, (0, 0))
    reflected = ImageOps.mirror(src).crop((0, 0, W - SOURCE_W, H))
    out.paste(reflected, (SOURCE_W, 0))
    return out


def draw_paving(layer: Image.Image) -> None:
    """An academy forecourt gradually gives way to the existing warm forest path."""
    d = ImageDraw.Draw(layer)
    d.polygon(
        [(0, 103), (178, 103), (189, 116), (181, 130),
         (193, 144), (191, 159), (205, 180), (0, 180)],
        fill=STONE_MUTED,
    )
    # Shallow sunlight patches tie the paved entrance to the dawn-lit sand.
    d.polygon([(0, 113), (145, 113), (175, 131), (155, 138), (0, 126)], fill=STONE)
    d.polygon([(0, 150), (115, 142), (188, 165), (197, 180), (0, 180)], fill=STONE)
    d.polygon([(0, 168), (82, 151), (140, 153), (124, 161), (27, 180), (0, 180)], fill=STONE_SUN)

    rng = random.Random(44162)
    row_limits = [(112, 124, 181), (124, 138, 186), (138, 154, 187), (154, 169, 192), (169, 181, 199)]
    for row, (y0, y1, x_max) in enumerate(row_limits):
        x = -rng.randrange(9, 25)
        while x < x_max:
            width = rng.randrange(22, 43)
            end = min(x + width, x_max)
            if end > 0:
                # Quiet seams, deliberately less contrast than the door.
                d.line([(max(0, x), y0), (end, y0)], fill=STONE_SHADE, width=1)
                d.line([(end, y0 + 1), (end + rng.randrange(-1, 2), y1 - 1)], fill=STONE_SHADE, width=1)
                if width > 28 and rng.random() < 0.7:
                    d.line([(max(0, x + 3), y0 + 3), (min(end - 3, x + width - 4), y0 + 3)], fill=STONE_SUN, width=1)
                if rng.random() < 0.55:
                    px = max(0, x + rng.randrange(6, max(7, width - 5)))
                    py = rng.randrange(y0 + 3, y1 - 2)
                    d.point((px, py), fill=STONE_SHADE)
            x += width
    # An irregular, feathered seam where paving meets the sandy trail.
    for x, y, length in [(179, 108, 6), (187, 117, 8), (179, 132, 11),
                         (188, 144, 9), (191, 160, 11), (202, 177, 10)]:
        d.line([(x, y), (x + length, y + 1)], fill=STONE_SUN, width=1)


def draw_masonry(d: ImageDraw.ImageDraw) -> None:
    d.rectangle((0, 0, 200, 107), fill=STONE)
    d.polygon([(200, 0), (219, 0), (218, 108), (200, 107)], fill=STONE_SHADE)
    d.line([(200, 0), (200, 107)], fill=STONE_DARK, width=2)

    # Low relief courses with staggered joints. The limited palette follows
    # the corridor stone rather than adding soft antialiasing or stray pixels.
    rng = random.Random(71207)
    for row, y in enumerate(range(14, 104, 13)):
        d.line([(0, y), (199, y)], fill=STONE_MUTED, width=1)
        d.line([(0, y + 1), (198, y + 1)], fill=STONE_SUN, width=1)
        x = -12 if row % 2 else 14
        while x < 199:
            x += rng.randrange(27, 40)
            d.line([(x, y + 2), (x, min(106, y + 12))], fill=STONE_MUTED, width=1)
            if x + 2 < 199:
                d.point((x + 1, y + 2), fill=STONE_SUN)
    # A broad, warm diagonal dawn reflection on the facade.
    d.polygon([(0, 0), (36, 0), (6, 106), (0, 106)], fill=STONE_SUN)
    d.polygon([(155, 0), (196, 0), (111, 104), (93, 104)], fill=STONE_SUN)
    # Sunlit patches remain chunky at the same four-pixel output grid.
    for i in range(165):
        x = rng.randrange(3, 198)
        y = rng.randrange(10, 103)
        if 44 < x < 133 and y > 20:
            continue
        c = STONE_SUN if i % 3 == 0 else STONE_MUTED
        d.point((x, y), fill=c)

    # Cornice continues off the left frame, anchoring the school to a larger
    # building. The darker return on the right establishes a proper corner.
    d.rectangle((0, 0, 220, 4), fill=STONE_DARK)
    d.rectangle((0, 4, 220, 6), fill=STONE_SHADE)
    d.rectangle((0, 7, 215, 10), fill=STONE_SUN)
    d.line([(0, 11), (212, 11)], fill=STONE_MUTED, width=2)
    d.rectangle((0, 101, 201, 104), fill=STONE_SHADE)
    d.rectangle((0, 105, 211, 109), fill=STONE_DARK)
    d.line([(0, 105), (211, 105)], fill=STONE_SUN, width=1)
    d.polygon([(200, 98), (219, 98), (224, 111), (204, 110)], fill=STONE_DARK)

    # Pilasters mirror the heavy stone ribs inside the corridor.
    for x in (0, 137, 195):
        d.rectangle((x, 12, x + 10, 102), fill=STONE_MUTED)
        d.rectangle((x + 1, 13, x + 7, 101), fill=STONE_SUN)
        d.line([(x + 8, 13), (x + 8, 103)], fill=STONE_SHADE, width=1)
        d.rectangle((x - 2, 14, x + 11, 17), fill=STONE)
        d.rectangle((x - 3, 94, x + 12, 100), fill=STONE)
        d.line([(x - 3, 94), (x + 12, 94)], fill=STONE_SUN, width=1)


def draw_matched_corridor_door(layer: Image.Image) -> None:
    """Reproject the actual corridor's carved door and arch at the forest exit.

    Only the arched masonry and threshold pixels are copied, so the exterior
    wall's own stonework continues up to the recess without a rectangular cut.
    This also keeps both sides' door height, panels and brass hardware exact.
    """
    corridor = Image.open(CORRIDOR_DOOR).convert("RGBA")
    if corridor.size != (W * SCALE, H * SCALE):
        raise ValueError(f"Unexpected corridor authored size: {corridor.size}")
    corridor = corridor.resize((W, H), Image.Resampling.NEAREST)
    for src_x in range(888, 973):
        dest_x = src_x - 842  # corridor centre 930 => exterior centre 88
        distance = min(1.0, abs(src_x - 930) / 42)
        top = 22 + round((45 - 22) * (1 - sqrt(max(0.0, 1 - distance * distance))))
        for y in range(top, 111):
            layer.putpixel((dest_x, y), corridor.getpixel((src_x, y)))
    for y, left, right in ((111, 894, 966), (112, 891, 969),
                           (113, 891, 969), (114, 891, 969),
                           (115, 891, 969), (116, 888, 972),
                           (117, 888, 972), (118, 888, 972),
                           (119, 888, 972), (120, 888, 972),
                           (121, 892, 968), (122, 892, 968),
                           (123, 897, 963), (124, 897, 963)):
        for src_x in range(left, right + 1):
            layer.putpixel((src_x - 842, y), corridor.getpixel((src_x, y)))


def draw_window_and_wall_end(d: ImageDraw.ImageDraw) -> None:
    # One small side window echoes the large arched classroom windows but
    # leaves the exterior door unmistakable as the connection point.
    d.polygon([(154, 89), (154, 43), (156, 36), (162, 31), (171, 29),
               (179, 31), (185, 36), (187, 43), (187, 89)], fill=STONE_SHADE)
    d.polygon([(159, 84), (159, 44), (161, 39), (166, 35), (172, 34),
               (178, 35), (183, 39), (184, 44), (184, 84)], fill=WOOD_DEEP)
    d.polygon([(162, 81), (162, 44), (165, 40), (170, 38), (174, 38),
               (180, 41), (181, 45), (181, 81)], fill=GLASS)
    d.polygon([(163, 47), (173, 39), (180, 44), (178, 52), (163, 58)], fill=GLASS_LIGHT)
    d.line([(172, 38), (172, 82)], fill=WOOD_MID, width=2)
    d.line([(162, 59), (182, 59)], fill=WOOD_MID, width=2)
    d.line([(154, 88), (187, 88)], fill=STONE_DARK, width=2)
    d.rectangle((151, 89, 190, 92), fill=STONE_LIGHT)
    d.line([(151, 93), (190, 93)], fill=STONE_SHADE, width=1)

    # The building turns away and ends in a short school boundary wall.
    d.polygon([(206, 82), (269, 85), (275, 105), (206, 107)], fill=STONE_SHADE)
    d.line([(206, 82), (269, 85)], fill=STONE_SUN, width=2)
    d.line([(209, 98), (273, 101)], fill=STONE_DARK, width=1)
    for x, y in ((213, 78), (239, 80), (268, 83)):
        d.rectangle((x - 2, y, x + 5, 106), fill=STONE_DARK)
        d.rectangle((x - 1, y + 1, x + 3, 103), fill=STONE)
        d.rectangle((x - 3, y - 2, x + 6, y + 1), fill=STONE_SUN)
        d.point((x + 2, y + 6), fill=STONE_LIGHT)
    d.polygon([(274, 93), (282, 91), (286, 104), (274, 106)], fill=STONE_MUTED)
    # Sparse overgrowth sits against the end wall, not over the doorway.
    for x, y in ((207, 101), (217, 99), (227, 102), (275, 102), (283, 104)):
        d.line([(x, y + 6), (x, y)], fill=(63, 88, 51, 255), width=2)
        d.point((x - 2, y + 1), fill=(105, 116, 51, 255))
        d.point((x + 2, y - 1), fill=(105, 116, 51, 255))


def draw_school(layer: Image.Image) -> None:
    draw_paving(layer)
    d = ImageDraw.Draw(layer)
    draw_masonry(d)
    draw_window_and_wall_end(d)
    draw_matched_corridor_door(layer)

    # A modest iron gas sconce reinforces the shared academy setting.
    d.rectangle((21, 56, 23, 75), fill=IRON)
    d.polygon([(17, 56), (27, 56), (25, 51), (19, 51)], fill=WOOD_DEEP)
    d.polygon([(19, 50), (25, 50), (26, 41), (23, 38), (20, 41)], fill=BRASS)
    d.polygon([(20, 49), (24, 49), (24, 42), (20, 42)], fill=STONE_LIGHT)
    d.polygon([(18, 40), (23, 35), (28, 40)], fill=IRON)
    d.line([(20, 38), (26, 38)], fill=BRASS, width=1)
    d.line([(23, 76), (30, 80)], fill=STONE_DARK, width=2)


def draw_far_end(layer: Image.Image) -> None:
    """Turn the existing rear iron fence toward the viewer to close the path."""
    d = ImageDraw.Draw(layer)
    # This low boundary occupies only the last ~400 authored pixels. It reads
    # as an estate fence turning across the trail, grounded by the ordinary
    # leafy grove already present at the right of the original forest.
    def fence_y(x: int) -> int:
        return 157 - round((x - 934) * 0.65)

    iron_lit = (63, 88, 51, 255)
    # Two uninterrupted bars establish perspective before upright pickets.
    for front_rise, back_rise in ((5, 3), (12, 8)):
        d.line([(939, fence_y(939) - front_rise), (1023, fence_y(1023) - back_rise)], fill=IRON, width=2)
        d.line([(940, fence_y(940) - front_rise - 1), (1023, fence_y(1023) - back_rise - 1)],
               fill=iron_lit, width=1)
    for x in range(944, 1024, 10):
        y = fence_y(x)
        height = 17 - round((x - 944) * 7 / 80)
        d.line([(x, y - height), (x, y + 1)], fill=IRON, width=2)
        d.line([(x + 1, y - height + 3), (x + 1, y - 2)], fill=iron_lit, width=1)
        d.polygon([(x - 2, y - height), (x, y - height - 3), (x + 2, y - height)], fill=IRON)
    # Front and rear limestone piers match the school's short boundary wall.
    for x, y, half_width, height in ((935, 157, 6, 28), (1018, fence_y(1018), 4, 19)):
        d.polygon([(x - half_width + 1, y - height), (x + half_width - 2, y - height),
                   (x + half_width - 1, y + 2), (x - half_width, y + 2)], fill=STONE_SHADE)
        d.polygon([(x - half_width + 2, y - height + 1), (x, y - height + 1),
                   (x + 1, y), (x - half_width + 2, y)], fill=STONE_MUTED)
        d.rectangle((x - half_width - 1, y - height - 3,
                     x + half_width, y - height), fill=STONE)
        d.polygon([(x - half_width + 1, y - height - 4),
                   (x + half_width - 2, y - height - 4),
                   (x + half_width, y - height - 3),
                   (x - half_width - 1, y - height - 3)], fill=STONE_SUN)
        d.line([(x + half_width - 2, y - height + 1),
                (x + half_width - 2, y)], fill=STONE_DARK, width=1)
        d.line([(x - half_width + 2, y - height + 8),
                (x + half_width - 2, y - height + 8)], fill=STONE_SHADE, width=1)
        d.line([(x - half_width + 2, y - height + 9),
                (x + half_width - 3, y - height + 9)], fill=STONE_SUN, width=1)
        d.line([(x - half_width - 1, y + 3), (x + half_width, y + 3)],
               fill=STONE_DARK, width=1)
        d.line([(x - half_width + 2, y - 7), (x + half_width - 2, y - 7)],
               fill=STONE_MUTED, width=1)
        d.point((x + half_width - 2, y - height + 5), fill=STONE_DARK)
        d.point((x - half_width + 2, y - 2), fill=(63, 88, 51, 255))
    # Grass at the front pier blends into the established near-foreground row.
    for x, y in ((923, 158), (945, 161), (959, 151), (984, 137), (1002, 127)):
        d.line([(x, y + 4), (x - 3, y)], fill=(63, 88, 51, 255), width=2)
        d.line([(x, y + 4), (x + 3, y - 2)], fill=(105, 116, 51, 255), width=2)


def fold_low_frequency_colors(layer: Image.Image) -> None:
    layer.putdata([PALETTE_FOLD.get(pixel, pixel) for pixel in layer.get_flattened_data()])


def save_png(path: Path, image: Image.Image) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, optimize=True)


def verify_four_pixel_blocks(image: Image.Image) -> None:
    # Nearest-neighbour output must be exact, not merely visually sharp.
    reduced = image.resize((W, H), Image.Resampling.NEAREST)
    expanded = reduced.resize((W * SCALE, H * SCALE), Image.Resampling.NEAREST)
    if expanded.tobytes() != image.tobytes():
        raise AssertionError("Pixel grid is not aligned to 4×4 blocks")


def main() -> None:
    logical = {name: logical_base(name) for name in NAMES}
    draw_school(logical["forest-belt-mid"])
    draw_far_end(logical["forest-belt-mid"])
    fold_low_frequency_colors(logical["forest-belt-mid"])
    source = {name: image.resize((W * SCALE, H * SCALE), Image.Resampling.NEAREST)
              for name, image in logical.items()}
    for name, image in source.items():
        verify_four_pixel_blocks(image)
        alphas = set(image.getchannel("A").get_flattened_data())
        if not alphas.issubset({0, 255}):
            raise AssertionError(f"Non-binary alpha in {name}: {alphas}")
        if len(set(image.get_flattened_data())) > 65:
            raise AssertionError(f"Too many colors in {name}")
        save_png(SOURCE_OUT / f"{name}.png", image)
        save_png(RUNTIME_OUT / f"{name}.png", image)

    composite = Image.new("RGBA", (W * SCALE, H * SCALE))
    for name in NAMES:
        composite = Image.alpha_composite(composite, source[name])
    save_png(PREVIEW_OUT / "forest-composite.png", composite)
    # The game camera shows roughly a third of the finished strip at once.
    for index, left in enumerate((0, 1360, 2736), start=1):
        save_png(PREVIEW_OUT / f"forest-view-{index}.png",
                 composite.crop((left, 0, left + 1360, H * SCALE)))

    qa = [f"Authored layer canvas: {W*SCALE}x{H*SCALE} (logical {W}x{H}, exact 4x)",
          "Layer order: far-mist, far-trees, belt-mid, near",
          "All alpha values: 0 or 255", "Door center: x=352px",
          "Door leaves: x=232..472px (240px wide)",
          "Stone arch: x=184..520px (336px), exactly reused from corridor",
          "Door arch crown: y=88px, threshold: y=436..496px",
          "School wall extends to about x=880px; forest path continues right."]
    for name, image in source.items():
        colors = Counter(image.get_flattened_data())
        opaque_colors = sum(1 for color in colors if color[3] == 255)
        qa.append(f"{name}: {opaque_colors} opaque colors, alpha bounds {image.getchannel('A').getbbox()}")
    (HERE / "verification.txt").write_text("\n".join(qa) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
