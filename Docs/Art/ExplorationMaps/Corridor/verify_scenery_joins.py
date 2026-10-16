"""Check the repaired finite corridor's scenery join and complete sky coverage."""
from pathlib import Path
import json
from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
JOIN = 2172
TOP, HEIGHT = 160, 724


def inspect(folder):
    report = {}
    for name in ('corridor-far', 'corridor-near'):
        image = Image.open(folder / f'{name}.png').convert('RGBA')
        assert image.size == (4096, 916), (name, image.size)
        left = image.crop((JOIN - 1, 0, JOIN, 916))
        right = image.crop((JOIN, 0, JOIN + 1, 916))
        mismatches = sum(a != b for a, b in zip(left.get_flattened_data(), right.get_flattened_data()))
        report[name] = {'join_mismatched_rows': mismatches,
                        'alpha_extrema': image.getchannel('A').getextrema()}
        assert mismatches == 0, f'{name}: {mismatches} rows jump at the added scenery join'
        assert set(image.getchannel('A').get_flattened_data()).issubset({0, 255}), name
        assert image.resize((1024, 229), Image.Resampling.NEAREST).resize(
            image.size, Image.Resampling.NEAREST).tobytes() == image.tobytes(), name
        source = Image.open(HERE / 'Runtime' / f'{name}.png').convert('RGBA')
        assert source.tobytes() == image.tobytes(), f'{name}: packaged PNG differs'
        authored = Image.open(HERE / 'Authored' / f'{name}.png').convert('RGBA')
        assert image.crop((0, TOP, 4096, TOP + HEIGHT)).tobytes() == authored.tobytes(), name
        if name == 'corridor-far':
            assert image.getchannel('A').getextrema() == (255, 255), 'sky must cover the full canvas'
    interior = Image.open(folder / 'corridor-interior.png').convert('RGBA')
    exported = Image.open(HERE / 'Runtime/corridor-interior.png').convert('RGBA')
    authored = Image.open(HERE / 'Authored/corridor-interior.png').convert('RGBA')
    native = Image.open(ROOT / 'Docs/Art/SchoolCorridor/Applied/corridor-interior.png').convert('RGBA')
    assert interior.tobytes() == exported.tobytes(), 'packaged floor differs'
    assert interior.crop((0, TOP, 4096, TOP + HEIGHT)).tobytes() == authored.tobytes(), 'floor framing differs'
    # Restored omitted source23 now meets its actual neighbor24. Before repair,
    # unrelated source542 met24 and cut the diagonal sun projection vertically.
    assert authored.crop((2168, 436, 2176, HEIGHT)).tobytes() == native.crop((92, 436, 100, HEIGHT)).tobytes(), 'floor projection is cut at the architectural join'
    report['corridor-interior'] = {'native_floor_columns_continuous': True,
                                    'authored_patch_bounds': [2088, 436, 2172, 724]}
    return report


if __name__ == '__main__':
    result = inspect(ROOT / 'Assets/Game/Resources/ExplorationMaps/Corridor')
    print(json.dumps(result, indent=2))
    print('PASS: scenery and native floor joins are continuous; full sky coverage, exact 4x grid and packaged art match.')
