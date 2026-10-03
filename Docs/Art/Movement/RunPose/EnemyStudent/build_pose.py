"""Build the new EnemyStudent running pose at the game's native pixel grid.

The pose originates in the ImageGen redrawing study documented in SOURCE.md.
This script projects that new illustration to the existing sprite canvas, maps
all opaque pixels to the shipped idle palette, and makes alpha strictly binary.
"""

from pathlib import Path
from PIL import Image
import numpy as np


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
STUDY = HERE / "run-study.png"
IDLE = ROOT / "Assets/Game/Resources/EnemyStudent/Animations/idle/frame-01.png"


def srgb_to_lab(rgb: np.ndarray) -> np.ndarray:
    a = rgb.astype(np.float64) / 255.0
    linear = np.where(a <= 0.04045, a / 12.92, ((a + 0.055) / 1.055) ** 2.4)
    xyz = linear @ np.array(
        [[0.4124564, 0.3575761, 0.1804375],
         [0.2126729, 0.7151522, 0.0721750],
         [0.0193339, 0.1191920, 0.9503041]]
    ).T
    xyz /= np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), (7.787 * xyz) + (16.0 / 116.0))
    return np.stack((116 * f[..., 1] - 16,
                     500 * (f[..., 0] - f[..., 1]),
                     200 * (f[..., 1] - f[..., 2])), axis=-1)


def project(resample: Image.Resampling, name: str) -> None:
    source = Image.open(STUDY).convert("RGBA")
    # Low-alpha stray pixels are a generation artifact outside the character.
    raw = np.asarray(source)
    hard = raw.copy()
    hard[hard[..., 3] < 128] = 0
    crop = Image.fromarray(hard).crop((307, 311, 1276, 1044))
    small = crop.resize((167, 126), resample)
    pixels = np.asarray(small).copy()
    mask = pixels[..., 3] >= 128

    idle = np.asarray(Image.open(IDLE).convert("RGBA"))
    palette = np.unique(idle[idle[..., 3] > 0, :3], axis=0)
    labs = srgb_to_lab(palette)
    colors = pixels[mask, :3]
    distances = np.sum((srgb_to_lab(colors)[:, None, :] - labs[None, :, :]) ** 2, axis=2)
    mapped = palette[np.argmin(distances, axis=1)]

    out = np.zeros((224, 256, 4), dtype=np.uint8)
    inset = out[76:202, 66:233]
    inset[mask, :3] = mapped
    inset[mask, 3] = 255
    Image.fromarray(out, "RGBA").save(HERE / name)


def preview() -> None:
    bg = (35, 32, 43, 255)
    width = 256 * 4
    board = Image.new("RGBA", (width * 3, 224 * 4), bg)
    for n, path in enumerate((IDLE, HERE / "candidate-box.png", HERE / "candidate-nearest.png")):
        im = Image.open(path).convert("RGBA").resize((width, 224 * 4), Image.Resampling.NEAREST)
        board.alpha_composite(im, (n * width, 0))
    board.convert("RGB").save(HERE / "candidate-comparison.png")


def stats(name: str) -> None:
    data = np.asarray(Image.open(HERE / name).convert("RGBA"))
    mask = data[..., 3] > 0
    ys, xs = np.where(mask)
    idle = np.asarray(Image.open(IDLE).convert("RGBA"))
    palette = {tuple(v) for v in idle[idle[..., 3] > 0, :3]}
    colors = {tuple(v) for v in data[mask, :3]}
    visited = set()
    sizes = []
    for y, x in zip(ys, xs):
        point = (int(y), int(x))
        if point in visited:
            continue
        todo = [point]
        visited.add(point)
        count = 0
        while todo:
            cy, cx = todo.pop()
            count += 1
            for q in ((cy - 1, cx), (cy + 1, cx), (cy, cx - 1), (cy, cx + 1)):
                if 0 <= q[0] < 224 and 0 <= q[1] < 256 and mask[q] and q not in visited:
                    visited.add(q)
                    todo.append(q)
        sizes.append((count, point))
    print(name, "bbox", (xs.min(), ys.min(), xs.max(), ys.max()),
          "opaque", len(xs), "4-components", sorted(sizes, reverse=True)[:20],
          "idle-palette", colors <= palette)


if __name__ == "__main__":
    HERE.mkdir(parents=True, exist_ok=True)
    project(Image.Resampling.BOX, "candidate-box.png")
    project(Image.Resampling.NEAREST, "candidate-nearest.png")
    preview()
    stats("candidate-box.png")
    stats("candidate-nearest.png")
