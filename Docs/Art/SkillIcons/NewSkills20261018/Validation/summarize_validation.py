"""Check the isolated Unity result and compose its native-size QA sheet."""
from pathlib import Path
import hashlib
import json
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
CAPTURES = HERE / 'Captures'

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

state = json.loads((HERE / 'process-state-render.json').read_text(encoding='utf-8-sig'))
assert state['state'] == 'exited' and state['exitCode'] == 0, state
report = json.loads((CAPTURES / 'report.json').read_text(encoding='utf-8'))
inputs = json.loads((HERE / 'input-hashes.json').read_text(encoding='utf-8'))
for name, record in inputs.items():
    assert sha(ROOT / record['source']) == record['sha256'], record['source']
    assert sha(HERE / 'Assets/Resources/SkillRoles' / name) == record['sha256'], name
assert len(report['icons']) == 4 and len(report['shots']) == 16
assert report['graphics'] == 'Direct3D11' and report['colorSpace'] == 'Linear'
assert {row['role'] for row in report['icons']} == {36, 37, 38, 39}
for row in report['icons']:
    assert (row['textureWidth'], row['textureHeight'], row['rectWidth'], row['rectHeight']) == (192,) * 4
    assert row['pivotX'] == row['pivotY'] == 96 and row['pixelsPerUnit'] == 100
    assert row['filter'] == 'Point' and row['wrap'] == 'Clamp' and row['meshType'] == 'FullRect'
    assert not row['mipmaps'] and row['compression'] == 'Uncompressed'
    assert row['meshVertexCount'] == 4 and row['exactFourTimesClusters'] and row['binaryAlpha']
    assert row['opaqueColorCount'] <= 48
for row in report['shots']:
    assert row['foreignPalettePixels'] == row['fractionalAlphaPixels'] == 0
    assert Image.open(CAPTURES / row['file']).size == (96, 96)

font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 16)
small = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 12)
sheet = Image.new('RGB', (736, 600), '#26221d')
draw = ImageDraw.Draw(sheet)
draw.text((16, 12), 'Unity 실제 SpriteRenderer 렌더 · 각 아이콘 실제 표시 크기', font=font, fill='#e4d6b9')
for col, size in enumerate((40, 44, 48, 52)):
    draw.text((232 + col * 144, 46), str(size) + 'px', font=font, fill='#e4d6b9', anchor='mt')
for index, (role, name) in enumerate(((36, '돌파'), (37, '추격'), (38, '기세 훔치기'), (39, '연습 반격'))):
    top = 80 + index * 124
    draw.text((18, top + 38), name, font=font, fill='#e4d6b9')
    draw.text((18, top + 65), f'role{role}', font=small, fill='#b9aa8b')
    for col, size in enumerate((40, 44, 48, 52)):
        x = 184 + col * 144
        capture = Image.open(CAPTURES / f'role{role}-{size}px.png').convert('RGBA')
        draw.rectangle((x - 8, top - 6, x + 103, top + 102), fill='#342e26', outline='#755d3b')
        sheet.paste(capture, (x, top), capture)
sheet.save(CAPTURES / 'unity-icon-render-native.png')

log = (HERE / 'unity-validation-render.log').read_text(encoding='utf-8', errors='replace')
assert 'SKILL ICON RENDER VALIDATION PASS' in log
assert 'error CS' not in log and 'Shader error' not in log and 'Exception:' not in log
summary = {
    'status': 'PASS', 'os_exit_code': state['exitCode'], 'native_imported_sprites': 4,
    'native_d3d11_render_captures': 16, 'displayed_sizes': [40, 44, 48, 52],
    'original_and_copied_inputs_unchanged': True, 'binary_render_alpha': True,
    'source_palette_preserved_with_gpu_roundtrip_tolerance': 2,
    'zero_foreign_palette_pixels': True, 'unity': report['unity'],
    'graphics': report['graphics'], 'device': report['device'],
    'pipeline': report['pipeline'], 'color_space': report['colorSpace'],
    'scope': report['scope'], 'preliminary_failures': [
        'Sandbox launch: license channel refused; owned PID verified then terminated, OS exit -1.',
        'First approved launch: exited before import while original sandbox PID held project, OS exit 1.',
        'Next approved launch: isolated harness used unavailable direct TextureImporter.spriteMeshType API; compilation failed, OS exit 1.',
        'Harness API corrected to ReadTextureSettings; equivalent compiler invocation passed before final native run.'
    ],
    'final_log_licensing_diagnostics': 'Three handshake/access-token error lines preceded successful fallback licensing and actual render execution. No C# compile, shader or exception errors in the passing run.'
}
(CAPTURES / 'summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('PASS: OS exit0; 4 actual resource sprites; 16 D3D11 captures; all original/copy input hashes unchanged.')
print(CAPTURES / 'unity-icon-render-native.png')
