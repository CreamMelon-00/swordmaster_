"""Import the approved PixelLab exports without resampling or editing source art."""
import json
import shutil
import uuid
import csv
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path('C:/Users/user/Documents/SwordMasterStory/Aseprite_Character/Player_Final_AnimationCanvas_20261023')
DEST = ROOT / 'Assets/Game/Resources/SwordGirl/FinalPlayer'

def main():
    template = (ROOT / 'Assets/Game/Resources/SwordGirl/Animations/idle/frame-01.png.meta').read_text(encoding='utf-8')
    clips = []
    inputs = [
        ('idle', 'Lobby_Refinement_20261024/idle', 'animation.json', 0),
        ('walk', 'Lobby_Refinement_20261024/walk', 'animation.json', 0),
        ('run', 'Lobby_Refinement_20261024/run', 'animation.json', 0),
        ('slash', 'Slash_01_PixelLab', 'slash.json', 4),
        ('pierce', 'Pierce_01_PixelLab/TrailFix', 'pierce.json', 5),
        ('beat', 'Strike_SkillFamilies_20261024/01_BladeBeat', 'animation.json', 7),
        ('loaded', 'Strike_SkillFamilies_20261024/02_LoadedStrike', 'animation.json', 4),
        ('measured', 'Strike_SkillFamilies_20261024/03_MeasuredGuard', 'animation.json', 3),
        ('evade', 'Strike_SkillFamilies_20261024/04_EvadeCounter', 'animation.json', 8),
    ]
    for key, folder, manifest, impact in inputs:
        source = SOURCE / folder
        if key == 'loaded':
            trail_source = source.parent / 'Loaded_FullBladeSweep_20261024'
            if trail_source.exists():
                source = trail_source
        data = json.loads((source / manifest).read_text(encoding='utf-8-sig'))
        frames = data['frames']
        durations = [f.get('durationMs', data.get('frameDurationMs', 100)) for f in frames]
        for i, frame in enumerate(frames):
            target = DEST / key / f'frame-{i:02}.png'
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source / frame['file'], target)
            meta = Path(str(target) + '.meta')
            if not meta.exists():
                import re
                text = re.sub(r'guid: [a-f0-9]+', 'guid: ' + uuid.uuid4().hex, template, count=1)
                text = text.replace('spritePixelsToUnits: 40', 'spritePixelsToUnits: 36')
                text = re.sub(r'spritePivot: .*', 'spritePivot: {x: 0.4453125, y: 0.022321428571428572}', text)
                meta.write_text('\n'.join(line.rstrip() for line in text.splitlines()).rstrip() + '\n', encoding='utf-8')
            if key in ('idle', 'walk', 'run'):
                import re
                original_text = meta.read_text(encoding='utf-8')
                text = original_text
                pivot = data['pivotPixels']
                text = re.sub(r'spritePixelsToUnits: .*', f"spritePixelsToUnits: {data['ppu']}", text)
                text = re.sub(r'spritePivot: .*', f"spritePivot: {{x: {pivot[0] / 256}, y: {pivot[1] / 224}}}", text)
                normalized = '\n'.join(line.rstrip() for line in text.splitlines()).rstrip() + '\n'
                if normalized != original_text:
                    meta.write_text(normalized, encoding='utf-8')
        # Original idle remains in the source archive; obsolete active cels must not remain in Resources.
        if key == 'idle':
            for stale in (DEST / key).glob('frame-*.png'):
                if int(stale.stem.split('-')[-1]) >= len(frames):
                    stale.unlink()
                    Path(str(stale) + '.meta').unlink(missing_ok=True)
        clips.append(dict(key=key, durationsMs=durations, impactIndex=impact))
    # Static guard PNGs are separate from attack timelines. Each uses its own foot origin.
    guards = SOURCE / 'Guard_ThreePoses_20261024'
    if guards.exists():
        from PIL import Image
        import re
        for index, head_y in enumerate([53, 64, 85], 1):
            key = f'guard-{index:02}'
            source = guards / (key + '.png')
            image = Image.open(source).convert('RGBA')
            floor = image.getbbox()[3] - 1
            foot = image.getchannel('A').crop((0, floor - 8, image.width, floor + 1)).getbbox()
            pivot_x = (foot[0] + foot[2] - 1) / 2
            ppu = (floor - head_y + 1) / (125 / 36)
            target = DEST / key / 'frame-00.png'
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)
            meta = Path(str(target) + '.meta')
            text = meta.read_text(encoding='utf-8') if meta.exists() else re.sub(r'guid: [a-f0-9]+', 'guid: ' + uuid.uuid4().hex, template, count=1)
            text = re.sub(r'spritePixelsToUnits: .*', f'spritePixelsToUnits: {ppu}', text)
            text = re.sub(r'spritePivot: .*', f'spritePivot: {{x: {pivot_x / 256}, y: {(224 - floor - 1) / 224}}}', text)
            meta.write_text('\n'.join(line.rstrip() for line in text.splitlines()).rstrip() + '\n', encoding='utf-8')
            clips.append(dict(key=key, durationsMs=[100], impactIndex=0))
    import re
    for folder in ['Reactions_PixelLab_20261024']:
        static_source = SOURCE / folder
        static_import = json.loads((static_source / 'import.json').read_text(encoding='utf-8'))
        for pose in static_import['poses']:
            key = pose['key']
            target = DEST / key / 'frame-00.png'
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(static_source / (key + '.png'), target)
            meta = Path(str(target) + '.meta')
            text = meta.read_text(encoding='utf-8') if meta.exists() else re.sub(r'guid: [a-f0-9]+', 'guid: ' + uuid.uuid4().hex, template, count=1)
            text = re.sub(r'spritePixelsToUnits: .*', f"spritePixelsToUnits: {pose['ppu']}", text)
            text = re.sub(r'spritePivot: .*', f"spritePivot: {{x: {pose['pivotX'] / 256}, y: {(224 - pose['groundY'] - 1) / 224}}}", text)
            meta.write_text('\n'.join(line.rstrip() for line in text.splitlines()).rstrip() + '\n', encoding='utf-8')
            clips.append(dict(key=key, durationsMs=[100], impactIndex=0))
    (DEST / 'timing.json').write_text(json.dumps(dict(clips=clips), indent=2), encoding='utf-8')
    families = {5:'beat', 48:'beat', 52:'beat', 6:'loaded', 55:'loaded', 68:'loaded', 10:'measured', 53:'measured', 54:'evade'}
    bindings = []
    with (ROOT / 'Assets/Game/Resources/Skills/skills.csv').open(encoding='utf-8-sig', newline='') as stream:
        for row in csv.DictReader(stream):
            if row['종류'] != '공격':
                continue
            skill_id = int(row['ID'])
            property_id = {'참격':0, '타격':1, '관통':2}[row['속성']]
            key = 'slash' if property_id == 0 else 'pierce' if property_id == 2 else families.get(skill_id, 'beat')
            bindings.append(dict(id=skill_id, property=property_id, key=key))
    (DEST / 'bindings.json').write_text(json.dumps(dict(bindings=bindings), indent=2), encoding='utf-8')
    print(json.dumps({c['key']: len(c['durationsMs']) for c in clips}))

if __name__ == '__main__':
    main()
