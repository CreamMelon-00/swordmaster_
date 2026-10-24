"""Import the approved PixelLab exports without resampling or editing source art."""
import json
import shutil
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path('C:/Users/user/Documents/SwordMasterStory/Aseprite_Character/Player_Final_AnimationCanvas_20261023')
DEST = ROOT / 'Assets/Game/Resources/SwordGirl/FinalPlayer'

def main():
    template = (ROOT / 'Assets/Game/Resources/SwordGirl/Animations/idle/frame-01.png.meta').read_text(encoding='utf-8')
    clips = []
    inputs = [
        ('idle', 'Idle_PixelLab', 'idle.json', 0),
        ('slash', 'Slash_01_PixelLab/NoTrail', 'slash.json', 4),
        ('pierce', 'Pierce_01_PixelLab/TrailFix/NoTrail', 'pierce.json', 5),
        ('beat', 'Strike_SkillFamilies_20261024/01_BladeBeat', 'animation.json', 7),
        ('loaded', 'Strike_SkillFamilies_20261024/02_LoadedStrike', 'animation.json', 4),
        ('measured', 'Strike_SkillFamilies_20261024/03_MeasuredGuard', 'animation.json', 3),
        ('evade', 'Strike_SkillFamilies_20261024/04_EvadeCounter', 'animation.json', 9),
    ]
    for key, folder, manifest, impact in inputs:
        source = SOURCE / folder
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
        clips.append(dict(key=key, durationsMs=durations, impactIndex=impact))
    (DEST / 'timing.json').write_text(json.dumps(dict(clips=clips), indent=2), encoding='utf-8')
    print(json.dumps({c['key']: len(c['durationsMs']) for c in clips}))

if __name__ == '__main__':
    main()
