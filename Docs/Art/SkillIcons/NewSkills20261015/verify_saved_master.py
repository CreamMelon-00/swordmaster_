"""Compare an independent Aseprite CLI sheet export with the final logical PNGs."""
import json
from pathlib import Path
from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
TEMP = ROOT / 'Temp/NewSkillIconsBuild'
sheet = Image.open(TEMP / 'master-roundtrip.png').convert('RGBA')
data = json.loads((TEMP / 'master-roundtrip.json').read_text(encoding='utf-8'))
frames = list(data['frames'].values())
assert len(frames) == 15
for index, item in enumerate(frames):
    rect = item['frame']
    actual = sheet.crop((rect['x'], rect['y'], rect['x'] + rect['w'], rect['y'] + rect['h']))
    expected = Image.open(HERE / 'Output/48' / f'role{21 + index}.png').convert('RGBA')
    assert actual.size == (48, 48) and actual.tobytes() == expected.tobytes(), index
tags = data['meta']['frameTags']
assert [t['name'] for t in tags] == [f'role{n}' for n in range(21, 36)]
assert all(t['from'] == i and t['to'] == i for i, t in enumerate(tags))
print('SAVED MASTER PASS: independently reopened/exported15 frames and15 tags match final PNGs exactly.')
