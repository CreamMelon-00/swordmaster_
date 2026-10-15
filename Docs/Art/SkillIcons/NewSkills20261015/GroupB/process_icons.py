"""Normalize the five built-in ImageGen source icons; no creative geometry is drawn here."""
from pathlib import Path
import hashlib
import importlib.util
import json
import shutil
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
spec = importlib.util.spec_from_file_location('unified_icons', ROOT / 'Docs/Art/SkillIcons/Unified/build_icons.py')
unified = importlib.util.module_from_spec(spec)
spec.loader.exec_module(unified)
record = json.loads((HERE / 'prompts.json').read_text(encoding='utf-8'))
for sub in ('Generated', '48', 'Runtime192'):
    (HERE / sub).mkdir(parents=True, exist_ok=True)
names = {26: ('50', '끝내기'), 27: ('51', '에스토카다'), 28: ('52', '바트망'), 29: ('53', '카덴스'), 30: ('54', '에스키브')}
manifest = {'method': 'built-in image_gen + Unified/build_icons.normalize', 'icons': {}}
contact = Image.new('RGB', (5 * 208, 250), '#29251f')
draw = ImageDraw.Draw(contact)
for n, (skill_id, name) in names.items():
    raw = HERE / 'Generated' / f'role{n}.png'
    shutil.copyfile(record['generated'][str(n)], raw)
    source = Image.open(raw).convert('RGBA')
    icon, report = unified.normalize(source, 48, 44, 48)
    path48 = HERE / '48' / f'role{n}.png'
    runtime = HERE / 'Runtime192' / f'role{n}.png'
    icon.save(path48)
    icon.resize((192, 192), Image.Resampling.NEAREST).save(runtime)
    assert set(icon.getchannel('A').get_flattened_data()) <= {0, 255}
    assert report['opaque_colors'] <= 48
    assert Image.open(runtime).tobytes() == icon.resize((192, 192), Image.Resampling.NEAREST).tobytes()
    manifest['icons'][f'role{n}'] = {'skill_id': int(skill_id), 'name': name, 'source_size': list(source.size), **report,
        'source_sha256': hashlib.sha256(raw.read_bytes()).hexdigest(),
        'logical_sha256': hashlib.sha256(path48.read_bytes()).hexdigest(),
        'runtime_sha256': hashlib.sha256(runtime.read_bytes()).hexdigest()}
    x = (n - 26) * 208
    contact.paste(icon.resize((192, 192), Image.Resampling.NEAREST), (x+8, 8), icon.resize((192,192), Image.Resampling.NEAREST))
    contact.paste(icon, (x+80, 202), icon)
    draw.text((x+8, 218), f'role{n} / skill {skill_id}', fill='#ddd1b1')
contact.save(HERE / 'contact.png')
(HERE / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
(HERE / 'prompts.md').write_text('# Group B skill icon prompts\n\nBuilt-in image_gen; true transparent background. Existing role4 and role5 are style references only.\n\n'+ '\n\n'.join(f'## role{n}: {names[n][1]}\n\n{record["prompts"][str(n)]}' for n in names)+'\n', encoding='utf-8')
print(json.dumps(manifest, ensure_ascii=False, indent=2))
