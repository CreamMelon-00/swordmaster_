from collections import Counter
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[5]
candidate = Image.open(Path(__file__).with_name('frame-01.png')).convert('RGBA')
idle = Image.open(root / 'Assets/Game/Resources/SwordGirl/Animations/idle/frame-01.png').convert('RGBA')
assert candidate.size == idle.size == (256, 224)
colors = {pixel for pixel in idle.get_flattened_data() if pixel[3] == 255}
opaque = {pixel for pixel in candidate.get_flattened_data() if pixel[3] == 255}
alpha = Counter(pixel[3] for pixel in candidate.get_flattened_data())
assert set(alpha) <= {0, 255}, alpha
assert opaque <= colors, opaque - colors

pixels = {(x, y) for y in range(224) for x in range(256)
          if candidate.getpixel((x, y))[3] == 255}
remaining = pixels.copy()
components = []
while remaining:
    pending = [remaining.pop()]
    points = []
    while pending:
        x, y = pending.pop()
        points.append((x, y))
        for point in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
            if point in remaining:
                remaining.remove(point)
                pending.append(point)
    components.append((len(points), (min(x for x,y in points),
                                     min(y for x,y in points),
                                     max(x for x,y in points),
                                     max(y for x,y in points))))

print('bbox', candidate.getbbox())
print('alpha', alpha)
print('palette', len(opaque), 'of', len(colors))
print('components', sorted(components, reverse=True))
print('ground x', [x for x in range(256) if (x,202) in pixels])
