"""Preview imported lobby walk/run at their real clip times with one exact GIF palette."""
import json
import math
import re
from bisect import bisect_right
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / 'Assets/Game/Resources/SwordGirl/FinalPlayer'
OUT = ROOT / 'Docs/Art/FinalPlayer/GIF'

def main():
    OUT.mkdir(parents=True, exist_ok=True)
    manifest = json.loads((ART / 'timing.json').read_text())
    font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 18)
    clips = {}
    for key, label in [('walk', '걷기'), ('run', '뛰기')]:
        clip = next(c for c in manifest['clips'] if c['key'] == key)
        frames, ends, total = [], [], 0
        for i, duration in enumerate(clip['durationsMs']):
            image = Image.open(ART / key / f'frame-{i:02}.png').convert('RGBA')
            tile = Image.new('RGB', (320, 330), (37, 35, 37))
            meta = (ART / key / f'frame-{i:02}.png.meta').read_text()
            ppu = float(re.search(r'spritePixelsToUnits: (.+)', meta).group(1))
            scale = 72 / ppu
            image = image.resize((round(256 * scale), round(224 * scale)), Image.Resampling.NEAREST)
            tile.paste(image, (round(160 - 114 * scale), round(313 - 216 * scale)), image)
            draw = ImageDraw.Draw(tile); draw.fontmode = '1'
            draw.text((10, 5), label, font=font, fill=(232, 219, 197))
            frames.append(tile); total += duration; ends.append(total)
        clips[key] = dict(frames=frames, durations=clip['durationsMs'], ends=ends, total=total)
        save_gif(OUT / f'lobby-{key}.gif', frames, clip['durationsMs'])
    combined_total = math.lcm(*(c['total'] for c in clips.values()))
    cuts = sorted({t for c in clips.values() for start in range(0, combined_total, c['total'])
                   for t in [start] + [start + n for n in c['ends']] if t <= combined_total})
    frames, durations = [], []
    for start, end in zip(cuts, cuts[1:]):
        canvas = Image.new('RGB', (640, 330), (37, 35, 37))
        for i, clip in enumerate(clips.values()):
            index = min(bisect_right(clip['ends'], start % clip['total']), len(clip['frames']) - 1)
            canvas.paste(clip['frames'][index], (320 * i, 0))
        frames.append(canvas); durations.append(end - start)
    save_gif(OUT / 'lobby-walk-run.gif', frames, durations)
    idle = next(c for c in manifest['clips'] if c['key'] == 'idle')
    frames = []
    for i in range(len(idle['durationsMs'])):
        image = Image.open(ART / 'idle' / f'frame-{i:02}.png').convert('RGBA')
        image = image.crop((45, 75, 205, 224)).resize((320, 298), Image.Resampling.NEAREST)
        tile = Image.new('RGB', (320, 330), (37, 35, 37))
        tile.paste(image, (0, 32), image)
        ImageDraw.Draw(tile).text((10, 5), 'Idle', font=font, fill=(232, 219, 197))
        frames.append(tile)
    save_gif(OUT / 'idle-calm.gif', frames, idle['durationsMs'])
    print(f"walk{clips['walk']['total']}ms; run{clips['run']['total']}ms; combined{combined_total}ms; game PPU display scale")

def save_gif(path, frames, durations):
    colors = sorted({c for frame in frames for _, c in frame.getcolors(frame.width * frame.height)})
    assert len(colors) <= 256
    palette = Image.new('P', (1, 1)); values = [v for color in colors for v in color]
    palette.putpalette(values + [0] * (768 - len(values)))
    indexed = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    indexed[0].save(path, save_all=True, append_images=indexed[1:], duration=durations,
                    loop=0, optimize=False, disposal=2)
    gif = Image.open(path); total = 0
    for i in range(gif.n_frames):
        gif.seek(i); total += gif.info['duration']
    assert total == sum(durations)

if __name__ == '__main__':
    main()
