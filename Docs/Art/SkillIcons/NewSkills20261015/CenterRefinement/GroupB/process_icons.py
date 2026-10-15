"""Normalize revised centers from built-in ImageGen. Original GroupB/C are immutable."""
from pathlib import Path
import hashlib
import importlib.util
import json
import shutil
from PIL import Image, ImageDraw, ImageFilter

HERE = Path(__file__).resolve().parent
ROOT = next(p for p in HERE.parents if (p/'Assets/Game').is_dir())
spec = importlib.util.spec_from_file_location('unified_icons', ROOT / 'Docs/Art/SkillIcons/Unified/build_icons.py')
unified = importlib.util.module_from_spec(spec)
spec.loader.exec_module(unified)
record = json.loads((HERE / 'prompts.json').read_text(encoding='utf-8'))
for sub in ('Generated', '48', 'Runtime192', 'Framed48', 'FramedRuntime192'):
    (HERE / sub).mkdir(parents=True, exist_ok=True)
names = {29: (53, '카덴스'), 31: (55, '콩트르탕')}
def extract_symbol(im,n):
    """Remove generated brass and leather; preserve generated steel/accent samples plus 1px ink."""
    bright = Image.new('L', (48,48))
    for y in range(9,40 if n==31 else 41):
        for x in range(9 if n==31 else 8,39 if n==31 else 42):
            r,g,b,a = p = im.getpixel((x,y))
            lum=.299*r+.587*g+.114*b
            steel=max(p[:3])-min(p[:3])<50 and lum>60
            ivory=lum>95 and min(p[:3])>60 and b>=r*.55 and g>=r*.75
            cool=g>r*1.1 and g>65
            if a and (steel or ivory or cool): bright.putpixel((x,y),255)
    seen=set()
    for y in range(48):
        for x in range(48):
            if not bright.getpixel((x,y)) or (x,y) in seen: continue
            stack=[(x,y)];component=[];seen.add((x,y))
            while stack:
                p=stack.pop();component.append(p)
                for dy in (-1,0,1):
                    for dx in (-1,0,1):
                        q=(p[0]+dx,p[1]+dy)
                        if 0<=q[0]<48 and 0<=q[1]<48 and q not in seen and bright.getpixel(q):
                            seen.add(q);stack.append(q)
            if len(component)<3:
                for p in component: bright.putpixel(p,0)
    expanded=bright.filter(ImageFilter.MaxFilter(3))
    out=Image.new('RGBA',(48,48))
    for y in range(8,42):
        for x in range(7,43):
            if expanded.getpixel((x,y)):
                out.putpixel((x,y),im.getpixel((x,y)) if bright.getpixel((x,y)) else (12,12,10,255))
    return out
manifest = {'method': 'built-in image_gen + Unified/build_icons.normalize + symbol mask extraction', 'icons': {}}
contact = Image.new('RGB', (416, 250), '#29251f')
draw = ImageDraw.Draw(contact)
for i, (n, (skill_id, name)) in enumerate(names.items()):
    raw = HERE / 'Generated' / f'role{n}.png'
    shutil.copyfile(record['generated'][str(n)], raw)
    source = Image.open(raw).convert('RGBA')
    icon, report = unified.normalize(source, 48, 44, 48)
    icon.save(HERE/'Framed48'/f'role{n}.png')
    icon.resize((192,192),Image.Resampling.NEAREST).save(HERE/'FramedRuntime192'/f'role{n}.png')
    icon = extract_symbol(icon,n)
    path48 = HERE / '48' / f'role{n}.png'
    runtime = HERE / 'Runtime192' / f'role{n}.png'
    icon.save(path48)
    enlarged = icon.resize((192, 192), Image.Resampling.NEAREST)
    enlarged.save(runtime)
    assert set(icon.getchannel('A').get_flattened_data()) <= {0, 255}
    assert report['opaque_colors'] <= 48
    assert Image.open(runtime).tobytes() == enlarged.tobytes()
    manifest['icons'][f'role{n}'] = {'skill_id': skill_id, 'name': name, 'source_size': list(source.size), **report, 'symbol_bbox':list(icon.getbbox()),
        'source_sha256': hashlib.sha256(raw.read_bytes()).hexdigest(),
        'logical_sha256': hashlib.sha256(path48.read_bytes()).hexdigest(),
        'runtime_sha256': hashlib.sha256(runtime.read_bytes()).hexdigest()}
    x = i * 208
    contact.paste(enlarged, (x+8, 8), enlarged)
    contact.paste(icon, (x+80, 202), icon)
    draw.text((x+8, 218), f'role{n} / skill {skill_id}', fill='#ddd1b1')
contact.save(HERE / 'contact.png')
(HERE / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
(HERE / 'prompts.md').write_text('# Center refinement Group B\n\nBuilt-in image_gen. Existing role5 and role13 used as style references. Original GroupB/C images remain immutable.\n\n'+ '\n\n'.join(f'## role{n}: {names[n][1]}\n\n{record["prompts"][str(n)]}' for n in names)+'\n', encoding='utf-8')
print('CENTER B PASS: role29 and role31; alpha/bounds/colors/exact 4x.')
