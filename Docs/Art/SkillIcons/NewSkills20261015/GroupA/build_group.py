from pathlib import Path
import importlib.util
import json
import shutil
import hashlib
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
UNIFIED = HERE.parents[1] / 'Unified'
spec = importlib.util.spec_from_file_location('unified_build_icons', UNIFIED / 'build_icons.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    recipe = json.loads((HERE/'prompts.json').read_text(encoding='utf-8'))
    report = {'logical_canvas': 48, 'visible_extent': 44, 'palette_limit': 48, 'runtime_canvas': 192, 'roles': {}}
    for folder in ('Generated', '48', 'Runtime192'):
        (HERE / folder).mkdir(parents=True, exist_ok=True)
    contact = Image.new('RGB', (5*216, 260), (35, 31, 26))
    d = ImageDraw.Draw(contact)
    for index, (role, data) in enumerate(recipe['roles'].items()):
        target = HERE / 'Generated' / f'role{role}.png'
        if not target.exists():
            shutil.copyfile(data['generated_original'], target)
        original = Image.open(target).convert('RGBA')
        icon, stats = module.normalize(original, 48, 44, 48)
        path48 = HERE/'48'/f'role{role}.png'
        path192 = HERE/'Runtime192'/f'role{role}.png'
        icon.save(path48)
        enlarged = icon.resize((192,192), Image.Resampling.NEAREST)
        enlarged.save(path192)
        alpha = set(icon.getchannel('A').get_flattened_data())
        assert alpha <= {0, 255}
        assert len({p[:3] for p in icon.get_flattened_data() if p[3]}) <= 48
        assert Image.open(path192).tobytes() == icon.resize((192,192), Image.Resampling.NEAREST).tobytes()
        assert 0 in set(original.getchannel('A').get_flattened_data()), 'raw image must have generated transparency'
        x=index*216
        contact.paste(enlarged, (x+12, 10), enlarged)
        contact.paste(icon, (x+12, 206), icon)
        d.text((x+68,218), f'role{role} ID{data["id"]}', fill=(227,214,183))
        report['roles'][role] = dict(id=data['id'], name=data['name'], **stats,
                                    generated_sha256=digest(target),
                                    logical_sha256=digest(path48), runtime_sha256=digest(path192),
                                    generated_size=list(original.size), alpha_values=sorted(alpha))
    contact.save(HERE/'contact.png')
    (HERE/'manifest.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n',encoding='utf-8')
    print(json.dumps(report, ensure_ascii=False, indent=2))

if __name__ == '__main__':
    main()
