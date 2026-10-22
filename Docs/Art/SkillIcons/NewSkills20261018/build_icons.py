"""Normalize generated symbols and preserve the approved native badge frames."""
from __future__ import annotations
import argparse
from collections import Counter
import csv
import hashlib
import json
from pathlib import Path
import re
import uuid
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
OUT = HERE / 'Output'
UNITY = ROOT / 'Assets/Game/Resources/SkillRoles'
NATIVE = HERE.parent / 'Unified/Output/48'
SKILLS = [(60, 36, '돌파', 'Slash'), (61, 37, '추격', 'Thrust'),
          (62, 38, '기세 훔치기', 'Thrust'), (600, 39, '연습 반격', 'Slash')]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def luma(c):
    return .299 * c[0] + .587 * c[1] + .114 * c[2]


def load(path):
    return Image.open(path).convert('RGBA')


def preserved_art():
    baseline = json.loads((HERE / 'baseline-existing-art.json').read_text(encoding='utf-8'))
    for relative, expected in baseline['files'].items():
        assert sha(ROOT / relative) == expected, f'Existing art changed: {relative}'
    return len(baseline['files'])


def palette_for(base, family, role):
    colors = sorted({p[:3] for p in base.get_flattened_data() if p[3]})
    reference = load(NATIVE / ('role10.png' if family == 'Slash' else 'role4.png'))
    steel = Counter(p[:3] for p in reference.get_flattened_data()
                    if p[3] and luma(p) > 65 and max(p[:3]) - min(p[:3]) < 65)
    outline = Counter(p[:3] for p in reference.get_flattened_data() if p[3] and luma(p) < 19)
    accent_role = 15 if role == 36 else 16
    accent = Counter(p[:3] for p in load(NATIVE / f'role{accent_role}.png').get_flattened_data()
                     if p[3] and luma(p) > 50 and (p[1] > p[0] * 1.1 if accent_role == 16
                                   else p[1] > p[2] * 1.35 and p[0] < p[1] * 1.3))
    extra = [p for p, _ in steel.most_common(10)] + [p for p, _ in outline.most_common(2)]
    extra += [p for p, _ in accent.most_common(6)]
    for p in extra:
        if p not in colors and len(colors) < 48:
            colors.append(p)
    assert len(colors) <= 48
    return colors, [p for p, _ in accent.most_common(6) if p in colors]


def fit_symbol(raw, mask):
    hard_alpha = raw.getchannel('A').point(lambda a: 255 if a >= 128 else 0)
    bounds = hard_alpha.getbbox()
    assert bounds, 'Empty generated symbol'
    raw.putalpha(hard_alpha)
    crop = raw.crop(bounds)
    for extent in range(34, 13, -1):
        scale = extent / max(crop.size)
        dims = (max(1, round(crop.width * scale)), max(1, round(crop.height * scale)))
        small = crop.resize(dims, Image.Resampling.NEAREST)
        for dx, dy in ((0, 0), (0, 1), (-1, 0), (1, 0), (0, -1)):
            left, top = (48 - dims[0]) // 2 + dx, (48 - dims[1]) // 2 + dy
            points = [(x, y) for y in range(dims[1]) for x in range(dims[0]) if small.getpixel((x, y))[3]]
            if all(mask.getpixel((left + x, top + y)) for x, y in points):
                output = Image.new('RGBA', (48, 48))
                output.alpha_composite(small, (left, top))
                return output, {'generated_bbox': bounds, 'symbol_size': dims,
                                'placement': [left, top], 'clipped_symbol_pixels': 0}
    raise AssertionError('Generated motif does not fit the native frame')


def recolor(symbol, palette, accents, role):
    result = Image.new('RGBA', symbol.size)
    cache = {}
    for y in range(48):
        for x in range(48):
            pixel = symbol.getpixel((x, y))
            if not pixel[3]:
                continue
            rgb = pixel[:3]
            if rgb not in cache:
                r, g, b = rgb
                cool = g > r * 1.1 and b > r * 1.1 and g - r > 12
                olive = role == 36 and g > 60 and r < g * 1.2 and b < g * .8
                choices = accents if accents and (cool or olive) else palette
                cache[rgb] = min(choices, key=lambda p: sum((rgb[i] - p[i]) ** 2 for i in range(3)))
            result.putpixel((x, y), (*cache[rgb], 255))
    return result


def preview(icons):
    font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 18)
    tiny = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 13)
    sheet = Image.new('RGB', (800, 442), '#26221d')
    draw = ImageDraw.Draw(sheet)
    for i, (_, role, name, family) in enumerate(SKILLS):
        x = i * 200
        draw.rectangle((x + 5, 6, x + 194, 437), fill='#342e26', outline='#755d3b')
        ref = load(NATIVE / ('role10.png' if family == 'Slash' else 'role4.png'))
        ref = ref.resize((144, 144), Image.Resampling.NEAREST)
        sheet.paste(ref, (x + 28, 29), ref)
        draw.text((x + 100, 9), '기존 기준', font=tiny, fill='#b9aa8b', anchor='mt')
        enlarged = icons[role].resize((144, 144), Image.Resampling.NEAREST)
        sheet.paste(enlarged, (x + 28, 190), enlarged)
        draw.text((x + 100, 340), name, font=font, fill='#e4d6b9', anchor='mt')
        for offset, size in ((20, 40), (73, 44), (130, 48)):
            icon = icons[role].resize((size, size), Image.Resampling.NEAREST)
            sheet.paste(icon, (x + offset, 378 + (48 - size) // 2), icon)
    sheet.save(OUT / 'new-skills-preview.png')
    actual = Image.new('RGB', (256, 68), '#26221d')
    for i, (_, role, _, _) in enumerate(SKILLS):
        actual.paste(icons[role], (i * 64 + 8, 10), icons[role])
    actual.save(OUT / 'actual-48px.png')
    finished = Image.new('RGB', (800, 234), '#26221d')
    labels = ImageDraw.Draw(finished)
    for i, (_, role, name, _) in enumerate(SKILLS):
        icon = icons[role].resize((192, 192), Image.Resampling.NEAREST)
        finished.paste(icon, (i * 200 + 4, 4), icon)
        labels.text((i * 200 + 100, 203), name, font=font, fill='#e4d6b9', anchor='mt')
    finished.save(OUT / 'new-icons.png')


def apply(icons):
    template = (UNITY / 'role35.png.meta').read_text(encoding='utf-8-sig')
    for _, role, _, _ in SKILLS:
        target = UNITY / f'role{role}.png'
        target.write_bytes((OUT / 'Runtime192' / target.name).read_bytes())
        meta = target.with_name(target.name + '.meta')
        if not meta.exists():
            guid = uuid.uuid4().hex
            text = re.sub(r'(?m)^guid: [0-9a-f]{32}$', 'guid: ' + guid, template)
            text = re.sub(r'(?m)^    spriteID: [0-9a-f]{32}$', '    spriteID: ' + guid, text)
            meta.write_text(text, encoding='utf-8')
    csv_path = ROOT / 'Assets/Game/Resources/Skills/skills.csv'
    raw = csv_path.read_bytes()
    bom = b'\xef\xbb\xbf' if raw.startswith(b'\xef\xbb\xbf') else b''
    text = raw.decode('utf-8-sig')
    for skill_id, role, _, _ in SKILLS:
        pattern = rf'(?m)(^{skill_id},(?:[^,\r\n]*,){{9}})([^,\r\n]*)'
        text, count = re.subn(pattern, lambda m: m[1] + str(role), text)
        assert count == 1, skill_id
    csv_path.write_bytes(bom + text.encode('utf-8'))
    loader = ROOT / 'Assets/Game/Presentation/LegacyDuelArt.cs'
    raw = loader.read_bytes()
    assert b'SkillIconCount = 35;' in raw or b'SkillIconCount = 39;' in raw
    loader.write_bytes(raw.replace(b'SkillIconCount = 35;', b'SkillIconCount = 39;'))
    assert all(sha(UNITY / f'role{role}.png') == sha(OUT / 'Runtime192' / f'role{role}.png')
               for _, role, _, _ in SKILLS)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    old_count = preserved_art()
    for folder in ('48', 'Symbols48', 'Runtime192'):
        (OUT / folder).mkdir(parents=True, exist_ok=True)
    icons, report = {}, []
    for skill_id, role, name, family in SKILLS:
        base = load(HERE / 'References/CleanBases' / f'{family}.png')
        mask = Image.open(HERE / 'References/Masks' / f'{family}-editable-interior.png').convert('L')
        symbol, details = fit_symbol(load(HERE / 'Generated' / f'role{role}.png'), mask)
        palette, accents = palette_for(base, family, role)
        symbol = recolor(symbol, palette, accents, role)
        final = Image.alpha_composite(base, symbol)
        for y in range(48):
            for x in range(48):
                if not mask.getpixel((x, y)):
                    assert final.getpixel((x, y)) == base.getpixel((x, y)), role
        assert final.size == (48, 48)
        assert set(final.getchannel('A').get_flattened_data()).issubset({0, 255})
        bbox = final.getchannel('A').getbbox()
        assert max(bbox[2] - bbox[0], bbox[3] - bbox[1]) <= 44
        colors = len({p[:3] for p in final.get_flattened_data() if p[3]})
        assert colors <= 48
        symbol.save(OUT / 'Symbols48' / f'role{role}.png')
        final.save(OUT / '48' / f'role{role}.png')
        final.resize((192, 192), Image.Resampling.NEAREST).save(OUT / 'Runtime192' / f'role{role}.png')
        icons[role] = final
        report.append(dict(skill_id=skill_id, role=role, name=name, family=family, opaque_colors=colors,
                           protected_frame_mismatches=0, **details))
    assert len({im.tobytes() for im in icons.values()}) == 4
    preview(icons)
    if args.apply:
        apply(icons)
    preserved_art()
    (OUT / 'manifest.json').write_text(json.dumps(dict(logical_size=48, runtime_size=192,
        preserved_existing_files=old_count, applied=args.apply, icons=report), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'PASS: four distinct48px motifs, native frames unchanged, exact4x output; {old_count} existing files preserved.')
    if args.apply:
        print('APPLIED: role36..39 resources and skill60/61/62/600 mappings; loader count39.')


if __name__ == '__main__':
    main()
