"""Validate authored PixelLab hurt poses and preserve reproducible provenance."""
import hashlib
import json
import subprocess
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path('C:/Users/user/Documents/SwordMasterStory/Aseprite_Character/Player_Final_AnimationCanvas_20261023/Hurt_Variants_PixelLab_20261025')
ART = ROOT / 'Assets/Game/Resources/SwordGirl/FinalPlayer'
OUTPUT = ROOT / 'Docs/Art/PlayerHurtVariants_20261025'

def main():
    original = Image.open(ART / 'hurt/frame-00.png').convert('RGBA')
    palette = {p[:3] for p in original.getdata() if p[3]}
    assert len(palette) == 34
    poses = json.loads((SOURCE / 'import.json').read_text())
    for pose in poses['poses']:
        final = SOURCE / pose['file']
        raw = Image.open(SOURCE / pose['file'].replace('.png', '-raw.png')).convert('RGBA')
        image = Image.open(final).convert('RGBA')
        assert image.size == (256, 224)
        assert raw.getchannel('A').tobytes() == image.getchannel('A').tobytes()
        assert {p[:3] for p in image.getdata() if p[3]} <= palette
        assert hashlib.sha256(final.read_bytes()).hexdigest() == pose['sha256']
        assert final.read_bytes() == (ART / f"hurt/frame-{pose['index']:02}.png").read_bytes()
    # Existing art PNGs must remain byte-identical to HEAD: only the two new files are added.
    tracked = subprocess.check_output(['git', 'ls-files', str(ART.relative_to(ROOT)).replace('\\', '/') + '/**/*.png'], cwd=ROOT, text=True).splitlines()
    for path in tracked:
        baseline = subprocess.check_output(['git', 'show', 'HEAD:' + path], cwd=ROOT)
        assert baseline == (ROOT / path).read_bytes(), path
    OUTPUT.mkdir(parents=True, exist_ok=True)
    report = dict(poses=3, added=2, canvas=[256,224], ppu=36,
        originalPaletteColors=len(palette), exactPaletteMatch=True, alphaPreserved=True,
        unchangedExistingPNGs=len(tracked), selection='One independent draw per hurt event; repeats allowed.',
        resistanceBreak='Existing break pose takes priority.')
    (OUTPUT / 'asset-validation.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    (OUTPUT / 'generation.json').write_bytes((SOURCE / 'generation.json').read_bytes())
    (OUTPUT / 'import.json').write_bytes((SOURCE / 'import.json').read_bytes())
    print(json.dumps(report))

if __name__ == '__main__':
    main()
