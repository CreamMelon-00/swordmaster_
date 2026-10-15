"""Assemble, validate and optionally deploy the fifteen new skill icons."""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import uuid
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
RUNTIME = ROOT / 'Assets/Game/Resources/SkillRoles'
OUT = HERE / 'Output'
SKILLS = [
    (45, 21, '자세 잡기', 'Defence'), (46, 22, '연마', 'Slash'),
    (47, 23, '꿰뚫기', 'Thrust'), (48, 24, '받아치기', 'Hit'),
    (49, 25, '비껴서기', 'Defence'), (50, 26, '끝내기', 'Thrust'),
    (51, 27, '에스토카다', 'Thrust'), (52, 28, '바트망', 'Hit'),
    (53, 29, '카덴스', 'Hit'), (54, 30, '에스키브', 'Hit'),
    (55, 31, '콩트르탕', 'Hit'), (56, 32, '두블레', 'Thrust'),
    (57, 33, '잔걸음', 'Slash'), (58, 34, '숫돌', 'Defence'),
    (59, 35, '갈아내기', 'Slash'),
]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def baseline():
    path = HERE / 'baseline-existing-runtime.json'
    if not path.exists():
        paths = [RUNTIME / 'skill-role-atlas.png', RUNTIME / 'skill-role-atlas.png.meta']
        paths += [RUNTIME / f'role{n}.png{suffix}' for n in range(16, 21) for suffix in ('', '.meta')]
        path.write_text(json.dumps({p.name: sha(p) for p in paths}, indent=2) + '\n', encoding='utf-8')
    saved = json.loads(path.read_text(encoding='utf-8'))
    for name, expected in saved.items():
        assert sha(RUNTIME / name) == expected, f'Existing approved icon changed: {name}'
    return saved


def check_icon(im, label):
    assert im.size == (48, 48), (label, im.size)
    colors = set(im.get_flattened_data())
    assert all(p[3] in (0, 255) for p in colors), f'{label}: alpha'
    bbox = im.getchannel('A').getbbox()
    assert bbox and max(bbox[2] - bbox[0], bbox[3] - bbox[1]) <= 44, (label, bbox)
    count = len({p[:3] for p in colors if p[3]})
    assert count <= 48, (label, count)
    return {'bbox': list(bbox), 'opaque_colors': count}


def contact(icons, path, scale=3):
    cell_w, cell_h = 48 * scale + 32, 48 * scale + 50
    sheet = Image.new('RGB', (cell_w * 5, cell_h * 3), '#26221d')
    d = ImageDraw.Draw(sheet)
    font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 17 if scale > 1 else 12)
    for i, (skill_id, role, name, _) in enumerate(SKILLS):
        x, y = (i % 5) * cell_w, (i // 5) * cell_h
        d.rectangle((x + 5, y + 4, x + cell_w - 6, y + cell_h - 5), fill='#342e26', outline='#755d3b')
        im = icons[role].resize((48 * scale, 48 * scale), Image.Resampling.NEAREST)
        sheet.paste(im, (x + 16, y + 10), im)
        d.text((x + cell_w / 2, y + 48 * scale + 20), name, fill='#e4d6b9', font=font, anchor='mt')
    sheet.save(path)


def display_sizes(icons, path):
    sheet = Image.new('RGB', (920, 312), '#26221d')
    d = ImageDraw.Draw(sheet)
    font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 12)
    for i, (_, role, name, _) in enumerate(SKILLS):
        x, y = i % 5 * 184, i // 5 * 104
        d.rectangle((x + 2, y + 2, x + 181, y + 101), fill='#342e26', outline='#755d3b')
        for left, size in ((9, 40), (60, 48), (120, 52)):
            im = icons[role].resize((size, size), Image.Resampling.NEAREST)
            sheet.paste(im, (x + left, y + 8 + (52 - size) // 2), im)
            d.text((x + left + size / 2, y + 64), str(size), fill='#b9aa8b', font=font, anchor='mt')
        d.text((x + 92, y + 83), name, fill='#e4d6b9', font=font, anchor='mt')
    sheet.save(path)


def style_review(path):
    preview = Image.open(OUT / 'new-skills-preview.png').convert('RGB')
    sheet = Image.new('RGB', (preview.width, preview.height + 240), '#26221d')
    d = ImageDraw.Draw(sheet)
    font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 16)
    d.text((16, 8), '기존 리소스 기준 · 아래 신규 아이콘과 동일 배율', fill='#e4d6b9', font=font)
    references = ((10, '참격'), (4, '관통'), (5, '타격'), (15, '방어'))
    for index, (role, name) in enumerate(references):
        x = 110 + index * 220
        source = HERE.parent / 'Unified/Output/48' / f'role{role}.png'
        icon = Image.open(source).convert('RGBA').resize((144, 144), Image.Resampling.NEAREST)
        sheet.paste(icon, (x - 72, 38), icon)
        d.text((x, 185), name, fill='#b9aa8b', font=font, anchor='mt')
    d.text((16, 215), '신규 15종 · 기존 프레임과 바탕 적용', fill='#e4d6b9', font=font)
    sheet.paste(preview, (0, 240))
    sheet.save(path)


def check_importers():
    guids = []
    for _, role, _, _ in SKILLS:
        meta = (RUNTIME / f'role{role}.png.meta').read_text(encoding='utf-8-sig')
        guids.append(re.search(r'(?m)^guid: ([0-9a-f]{32})$', meta)[1])
        for setting in ('enableMipMap: 0', 'filterMode: 0', 'wrapU: 1', 'wrapV: 1',
                        'spriteMode: 1', 'spriteMeshType: 0', 'spritePixelsToUnits: 100',
                        'spritePivot: {x: 0.5, y: 0.5}', 'textureType: 8'):
            assert setting in meta, (role, setting)
        assert all(value == '0' for value in re.findall(r'(?m)^    textureCompression: (\d+)$', meta)), role
    assert len(set(guids)) == 15, 'Duplicate new sprite GUIDs'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    original_hashes = baseline()
    icons, reports = {}, []
    for folder in ('48', 'Runtime192'):
        (OUT / folder).mkdir(parents=True, exist_ok=True)
    for skill_id, role, name, family in SKILLS:
        group = 'Group' + ('A' if role <= 25 else 'B' if role <= 30 else 'C')
        input_dir = HERE / ('StyleRefinement' if (HERE / 'StyleRefinement' / '48').is_dir() else group)
        source = input_dir / '48' / f'role{role}.png'
        im = Image.open(source).convert('RGBA')
        report = check_icon(im, f'role{role}')
        icons[role] = im
        im.save(OUT / '48' / source.name)
        large = im.resize((192, 192), Image.Resampling.NEAREST)
        candidate = input_dir / 'Runtime192' / source.name
        assert Image.open(candidate).convert('RGBA').tobytes() == large.tobytes(), source.name
        large.save(OUT / 'Runtime192' / source.name)
        reports.append(dict(skill_id=skill_id, role=role, name=name, family=family,
                            source=str(source.relative_to(HERE)), source_sha256=sha(source), **report))
    assert len({im.tobytes() for im in icons.values()}) == 15, 'Duplicate new art'
    contact(icons, OUT / 'new-skills-preview.png')
    contact(icons, OUT / 'contact-48-native.png', scale=1)
    display_sizes(icons, OUT / 'actual-size-40-48-52.png')
    style_review(OUT / 'style-review.png')

    with (ROOT / 'Assets/Game/Resources/Skills/skills.csv').open(encoding='utf-8-sig', newline='') as stream:
        rows = {int(row['ID']): row for row in csv.DictReader(stream)}
    for skill_id, role, name, _ in SKILLS:
        assert rows[skill_id]['이름'] == name and int(rows[skill_id]['그림']) == role, (skill_id, role)
    loader = (ROOT / 'Assets/Game/Presentation/LegacyDuelArt.cs').read_text(encoding='utf-8-sig')
    assert re.search(r'SkillIconCount\s*=\s*35\s*;', loader)

    if args.apply:
        template = (RUNTIME / 'role16.png.meta').read_text(encoding='utf-8-sig')
        for _, role, _, _ in SKILLS:
            target = RUNTIME / f'role{role}.png'
            target.write_bytes((OUT / 'Runtime192' / target.name).read_bytes())
            meta_path = target.with_name(target.name + '.meta')
            if not meta_path.exists():
                guid = uuid.uuid4().hex
                meta = re.sub(r'(?m)^guid: [0-9a-f]{32}$', 'guid: ' + guid, template)
                meta = re.sub(r'(?m)^    spriteID: [0-9a-f]{32}$', '    spriteID: ' + guid, meta)
                # A full rectangular mesh keeps all transparent margins registered identically.
                meta = meta.replace('  spriteMeshType: 1', '  spriteMeshType: 0')
                meta_path.write_text(meta, encoding='utf-8')
            assert sha(target) == sha(OUT / 'Runtime192' / target.name)
        check_importers()
    baseline()
    manifest = dict(logical_canvas=48, visible_extent=44, runtime_canvas=192,
                    existing_runtime_sha256=original_hashes, roles=reports,
                    native_template_refinement=(HERE / 'StyleRefinement' / '48').is_dir(),
                    runtime_applied=args.apply)
    (OUT / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('PASS: 15 unique icons; 48px/44px bounds, <=48 colors, binary alpha and exact 4x output.')
    print('PASS: new skill mappings21..35; existing20 icons/importers preserved.')
    if args.apply:
        print('APPLIED: role21..35 resource sprites; new GUIDs and pixel-art import settings.')


if __name__ == '__main__':
    main()
