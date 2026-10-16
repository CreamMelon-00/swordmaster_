"""Preserve the actual Unity render evidence and build a labeled comparison."""
from pathlib import Path
from shutil import copy2
import json
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
CAPTURES = ROOT / 'Temp/BackgroundLoopValidation/Captures'
SHOTS = ('finite-corridor_xm12p0.png', 'finite-corridor_xm13p7_wide.png',
         'finite-corridor_x10p0.png')

reports = {}
for phase in ('before', 'after'):
    reports[phase] = json.loads((CAPTURES / phase / 'report.json').read_text(encoding='utf-8'))
    copy2(CAPTURES / phase / 'report.json', HERE / f'{phase}-render-report.json')
    for shot, suffix in zip(SHOTS, ('corridor', 'corridor-wide', 'floor')):
        copy2(CAPTURES / phase / shot, HERE / f'{phase}-{suffix}.png')
assert len(reports['before']['shots']) == len(reports['after']['shots']) == 15
assert reports['after']['graphics'] == 'Direct3D11'
assert all(s['uncoveredPixels'] == 0 for s in reports['after']['shots'])
unchanged = []
for shot in reports['before']['shots']:
    if shot['map'] != 'finite-corridor':
        a = (CAPTURES / 'before' / shot['file']).read_bytes()
        b = (CAPTURES / 'after' / shot['file']).read_bytes()
        assert a == b, shot['file']
        unchanged.append(shot['file'])
assert len(unchanged) == 9

# The view and crop are identical; these are display comparisons, not edits to art.
crop = (958, 10, 1428, 510)
font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 21)
canvas = Image.new('RGB', (964, 552), '#26221d')
draw = ImageDraw.Draw(canvas)
for phase, x, label in (('before', 0, '수정 전'), ('after', 494, '수정 후')):
    image = Image.open(HERE / f'{phase}-corridor.png').convert('RGB').crop(crop)
    canvas.paste(image, (x, 42))
    draw.text((x + 235, 8), label, font=font, fill='#e4d6b9', anchor='mt')
canvas.save(HERE / 'window-before-after.png')

floor_crop = (20, 605, 660, 1080)
floor_canvas = Image.new('RGB', (1304, 527), '#26221d')
floor_draw = ImageDraw.Draw(floor_canvas)
for phase, x, label in (('before', 0, '수정 전'), ('after', 664, '수정 후')):
    image = Image.open(HERE / f'{phase}-floor.png').convert('RGB').crop(floor_crop)
    floor_canvas.paste(image, (x, 42))
    floor_draw.text((x + 320, 8), label, font=font, fill='#e4d6b9', anchor='mt')
floor_canvas.save(HERE / 'floor-before-after.png')

summary = {
    'scope': 'isolated Unity project; production backdrop classes and resource importers; no actors or UI',
    'unity': reports['after']['unity'],
    'graphics': reports['after']['graphics'],
    'pipeline': reports['after']['pipeline'],
    'captures_per_phase': 15,
    'after_uncovered_pixels': 0,
    'unchanged_forest_and_battle_corridor_captures': unchanged,
    'wide_before_uncovered_pixels': next(s['uncoveredPixels'] for s in reports['before']['shots']
                                          if s['file'] == SHOTS[1]),
    'wide_after_uncovered_pixels': next(s['uncoveredPixels'] for s in reports['after']['shots']
                                         if s['file'] == SHOTS[1]),
}
(HERE / 'render-summary.json').write_text(json.dumps(summary, indent=2) + '\n', encoding='utf-8')
print('PASS: 15 after captures cover the view; 9 forest/battle captures are byte-identical.')
print('PASS: same-camera before/after comparison preserved; 32:9 uncovered pixels 8348 -> 0.')
