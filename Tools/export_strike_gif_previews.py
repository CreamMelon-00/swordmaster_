"""Package the currently imported strike cels as GIF previews; never redraw source pixels."""
from pathlib import Path
from bisect import bisect_right
import json
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / 'Assets/Game/Resources/SwordGirl/FinalPlayer'
OUT = ROOT / 'Docs/Art/FinalPlayer/GIF'
SPECS = [('beat', '검 쳐내기'), ('loaded', '힘을 모아 치기'),
         ('measured', '자세·박자 조절'), ('evade', '회피 후 반격')]
BG, INK = (37, 35, 37), (232, 219, 197)

def main():
    OUT.mkdir(parents=True, exist_ok=True)
    manifest = json.loads((ART / 'timing.json').read_text(encoding='utf-8-sig'))
    clips, cuts = {}, {0}
    boxes = []
    for key, label in SPECS:
        definition = next(c for c in manifest['clips'] if c['key'] == key)
        times, frames, ends, total = definition['durationsMs'], [], [], 0
        for index, duration in enumerate(times):
            frame = Image.open(ART / key / f'frame-{index:02}.png').convert('RGBA')
            frames.append(frame); boxes.append(frame.getbbox())
            total += duration; ends.append(total); cuts.add(total)
        clips[key] = dict(frames=frames, times=times, ends=ends, total=total)
    assert len({c['total'] for c in clips.values()}) == 1
    total = next(iter(clips.values()))['total']
    crop = (max(0, min(b[0] for b in boxes) - 10), max(0, min(b[1] for b in boxes) - 8),
            min(256, max(b[2] for b in boxes) + 10), 224)
    scale = 2
    width, height = (crop[2] - crop[0]) * scale, (crop[3] - crop[1]) * scale
    font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 18)

    def tile(key, label, time):
        clip = clips[key]
        index = min(bisect_right(clip['ends'], time), len(clip['frames']) - 1)
        frame = clip['frames'][index].crop(crop).resize((width, height), Image.Resampling.NEAREST)
        canvas = Image.new('RGB', (width, height + 32), BG)
        canvas.paste(frame, (0, 32), frame)
        draw = ImageDraw.Draw(canvas); draw.fontmode = '1'
        draw.text((10, 5), label, font=font, fill=INK)
        return canvas

    def save_gif(name, frames, durations):
        colors = set()
        for frame in frames:
            colors.update(color for _, color in frame.getcolors(frame.width * frame.height))
        assert len(colors) <= 256, len(colors)
        palette = Image.new('P', (1, 1))
        values = [v for color in sorted(colors) for v in color]
        palette.putpalette(values + [0] * (768 - len(values)))
        indexed = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
        path = OUT / name
        indexed[0].save(path, save_all=True, append_images=indexed[1:], duration=durations,
                        loop=0, optimize=False, disposal=2)
        gif = Image.open(path)
        elapsed = 0
        for index in range(gif.n_frames):
            gif.seek(index); elapsed += gif.info['duration']
        assert elapsed == sum(durations), (name, elapsed)
        return dict(file=name, frames=gif.n_frames, durationMs=elapsed, colors=len(colors))

    records = []
    for key, label in SPECS:
        clip = clips[key]
        starts = [0] + clip['ends'][:-1]
        frames = [tile(key, label, t) for t in starts]
        records.append(save_gif(key + '.gif', frames, clip['times'][:-1] + [clip['times'][-1] + 400]))
    times = sorted(cuts)
    comparison = []
    for time in times[:-1]:
        canvas = Image.new('RGB', (width * 2, (height + 32) * 2 + 34), BG)
        draw = ImageDraw.Draw(canvas); draw.fontmode = '1'
        draw.text((10, 5), '타격 4종 · 적용 프레임 / 원본 타이밍', font=font, fill=INK)
        for index, (key, label) in enumerate(SPECS):
            canvas.paste(tile(key, label, time), ((index % 2) * width, 34 + (index // 2) * (height + 32)))
        comparison.append(canvas)
    durations = [b - a for a, b in zip(times, times[1:])]
    durations[-1] += 400
    records.append(save_gif('strike-four-families-with-trails.gif', comparison, durations))
    report = dict(source='Currently imported Unity PNG frames and timing.json', nativeCanvas=[256,224],
                  fixedCrop=list(crop), previewScale=scale, authoredCycleMs=total, endPauseMs=400,
                  note='원본 프레임별 타이밍 미리보기. 게임의 속도 보정·접촉 정지·카메라 연출은 포함하지 않음.', outputs=records)
    (OUT / 'preview-info.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(report, ensure_ascii=True))

if __name__ == '__main__':
    main()
