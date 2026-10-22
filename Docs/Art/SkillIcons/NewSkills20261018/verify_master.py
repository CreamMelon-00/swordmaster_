"""Verify a separately reopened Aseprite sheet export against the final art."""
from pathlib import Path
import json
from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
TEMP = ROOT / 'Temp/SkillIconsBuild20261018'
data = json.loads((TEMP / 'master-export.json').read_text(encoding='utf-8'))
sheet = Image.open(TEMP / 'master-export.png').convert('RGBA')
frames = list(data['frames'].values())
assert len(frames) == 4
for i, item in enumerate(frames):
    r = item['frame']
    actual = sheet.crop((r['x'], r['y'], r['x'] + r['w'], r['y'] + r['h']))
    expected = Image.open(HERE / 'Output/48' / f'role{36 + i}.png').convert('RGBA')
    assert actual.size == (48, 48) and actual.tobytes() == expected.tobytes(), i
tags = data['meta']['frameTags']
assert [tag['name'] for tag in tags] == [f'role{n}' for n in range(36, 40)]
assert all(t['from'] == i and t['to'] == i for i, t in enumerate(tags))
print('PASS: independently exported four frames and tags equal the final PNGs.')
