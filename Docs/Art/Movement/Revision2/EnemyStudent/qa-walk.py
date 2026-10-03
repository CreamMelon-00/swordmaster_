"""Read-only sprite checks for a staged EnemyStudent walking candidate."""

from collections import deque
from pathlib import Path
import sys

from PIL import Image


def components(alpha):
    width, height = alpha.size
    visible = {(x, y) for y in range(height) for x in range(width) if alpha.getpixel((x, y))}
    parts = []
    while visible:
        start = visible.pop()
        queue = deque([start])
        pixels = [start]
        while queue:
            x, y = queue.popleft()
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    neighbor = (x + dx, y + dy)
                    if neighbor in visible:
                        visible.remove(neighbor)
                        queue.append(neighbor)
                        pixels.append(neighbor)
        parts.append(pixels)
    return sorted(parts, key=len, reverse=True)


def spans(xs):
    intervals = []
    for x in sorted(xs):
        if intervals and x <= intervals[-1][1] + 1:
            intervals[-1] = (intervals[-1][0], x)
        else:
            intervals.append((x, x))
    return intervals


def main():
    folder = Path(sys.argv[1])
    original = Path(sys.argv[2])
    palette = set(Image.open(original).convert("RGBA").get_flattened_data())
    palette.discard((0, 0, 0, 0))
    frame_paths = sorted(folder.glob("frame-??.png"))
    assert frame_paths, "No frame PNGs found"
    images = []
    for path in frame_paths:
        img = Image.open(path).convert("RGBA")
        assert img.size == (256, 224), (path, img.size)
        pixels = list(img.get_flattened_data())
        alpha = {c[3] for c in pixels}
        assert alpha <= {0, 255}, (path, sorted(alpha))
        off_palette = {c for c in pixels if c[3] and c not in palette}
        assert not off_palette, (path, len(off_palette))
        groups = components(img.getchannel("A"))
        leftovers = [(len(group), min(x for x, _ in group), min(y for _, y in group)) for group in groups[1:]]
        ground = [x for x in range(165) if img.getpixel((x, 201))[3]]
        lower = img.getchannel("A").crop((50, 175, 165, 210))
        shoes = components(lower)
        shoe_info = []
        for group in shoes[:2]:
            shoe_info.append((len(group), round(sum(x for x, _ in group) / len(group) + 50, 1), max(y for _, y in group) + 175))
        print(path.name, "bbox", img.getbbox(), "components", len(groups), "detached", leftovers[:8], "ground", spans(ground), "lower", shoe_info)
        assert ground, (path, "No planted boot at y=201")
        images.append(img)
    assert len(set(image.tobytes() for image in images)) == len(images), "Duplicated frame pixels"
    print("Frames:", len(images), "all distinct, binary alpha, original palette, 256x224")


if __name__ == "__main__":
    main()
