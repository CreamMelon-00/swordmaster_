"""Normalize generated Group C bitmap icons using the approved unified recipe."""
from pathlib import Path
import hashlib
import json
import shutil
import sys
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
sys.path.insert(0, str(ROOT / 'Docs/Art/SkillIcons/Unified'))
from build_icons import normalize

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    config = json.loads((HERE / 'prompts.json').read_text(encoding='utf-8'))
    refinements = {}
    for role_number in (32, 35):
        refinement_path = HERE / f'role{role_number}-refinement.json'
        if refinement_path.exists():
            refinements[role_number] = json.loads(refinement_path.read_text(encoding='utf-8'))
    for directory in ('Generated', '48', 'Runtime192'):
        (HERE / directory).mkdir(parents=True, exist_ok=True)
    manifest = {'generator': 'built-in image_gen', 'logical_canvas': 48,
                'logical_art_extent': 44, 'palette_limit': 48,
                'runtime_canvas': 192, 'roles': {}}
    icons = {}
    for key, entry in config['roles'].items():
        n = int(key)
        refinement = refinements.get(n)
        raw = HERE / 'Generated' / f'role{n}.png'
        if not raw.exists():
            source_path = refinement['source_path'] if refinement else entry['source_path']
            shutil.copy2(source_path, raw)
        source = Image.open(raw).convert('RGBA')
        icon, report = normalize(source, 48, 44, 48)
        large = icon.resize((192,192), Image.Resampling.NEAREST)
        small_path = HERE / '48' / f'role{n}.png'
        runtime_path = HERE / 'Runtime192' / f'role{n}.png'
        icon.save(small_path)
        large.save(runtime_path)
        colors = len({p[:3] for p in icon.get_flattened_data() if p[3]})
        assert colors <= 48
        assert set(icon.getchannel('A').get_flattened_data()) <= {0,255}
        bbox = icon.getchannel('A').getbbox()
        assert bbox and max(bbox[2]-bbox[0], bbox[3]-bbox[1]) <= 44
        assert large.tobytes() == icon.resize((192,192), Image.Resampling.NEAREST).tobytes()
        manifest['roles'][f'role{n}'] = dict(skill_name=entry['skill_name'],
            skill_id=entry['skill_id'], family=entry['family'], frame=entry['frame'],
            raw_size=list(source.size), source_sha256=sha(raw),
            logical_sha256=sha(small_path), runtime_sha256=sha(runtime_path), **report)
        if refinement:
            manifest['roles'][f'role{n}']['refinement'] = refinement
        icons[n] = icon
    contact = Image.new('RGB', (5*216, 302), (37,32,28))
    draw = ImageDraw.Draw(contact)
    font = ImageFont.load_default()
    for col,(n,icon) in enumerate(icons.items()):
        x = col*216
        draw.text((x+8,8), f'role{n} / 48 logical, 192 runtime', fill=(232,212,168),font=font)
        contact.paste(icon,(x+84,30),icon.getchannel('A'))
        big=icon.resize((192,192),Image.Resampling.NEAREST)
        contact.paste(big,(x+12,96),big.getchannel('A'))
    contact.save(HERE / 'contact.png')
    (HERE / 'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n', encoding='utf-8')
    print('PASS: five generated Group C icons, 48px / 44px art extent, binary alpha, <=48 colors, exact 4x runtime.')

if __name__ == '__main__':
    main()
