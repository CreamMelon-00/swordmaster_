from pathlib import Path
import importlib.util
import json
import shutil
import hashlib
from PIL import Image, ImageDraw

HERE=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('unified', HERE.parents[2]/'Unified'/'build_icons.py')
unified=importlib.util.module_from_spec(spec)
spec.loader.exec_module(unified)

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    recipes=json.loads((HERE/'prompts.json').read_text(encoding='utf-8'))
    result={'center_only':True,'canvas':48,'glyph_extent':32,'roles':{}}
    for directory in ('Generated','48','Runtime192'):
        (HERE/directory).mkdir(parents=True,exist_ok=True)
    contact=Image.new('RGB',(500,270),(32,29,25))
    draw=ImageDraw.Draw(contact)
    for idx,(role,record) in enumerate(recipes['roles'].items()):
        raw=HERE/'Generated'/f'role{role}.png'
        if not raw.exists(): shutil.copyfile(record['generated_original'],raw)
        original=Image.open(raw).convert('RGBA')
        icon,report=unified.normalize(original,48,32,48)
        out48=HERE/'48'/f'role{role}.png'
        out192=HERE/'Runtime192'/f'role{role}.png'
        icon.save(out48)
        enlarged=icon.resize((192,192),Image.Resampling.NEAREST)
        enlarged.save(out192)
        assert set(icon.getchannel('A').get_flattened_data())<={0,255}
        assert report['opaque_colors']<=48
        assert 0 in set(original.getchannel('A').get_flattened_data())
        x=idx*250
        contact.paste(enlarged,(x+24,8),enlarged)
        contact.paste(icon,(x+24,208),icon)
        draw.text((x+85,225),f'role{role} ID{record["id"]}',fill=(231,220,192))
        result['roles'][role]=dict(id=record['id'],name=record['name'],**report,
                                  original_sha256=sha(raw),logical_sha256=sha(out48),runtime_sha256=sha(out192))
    contact.save(HERE/'contact.png')
    (HERE/'manifest.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(result,ensure_ascii=True,indent=2))

if __name__=='__main__': main()
