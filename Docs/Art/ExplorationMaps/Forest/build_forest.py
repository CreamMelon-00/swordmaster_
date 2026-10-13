"""Pack imagegen's approved distant-school entrance into the forest layers.

This performs only grid reduction, palette mapping, foreground separation and
a minimum-error join. All new creative pixels come from the approved concept.
"""
from collections import Counter
from pathlib import Path
import hashlib
from PIL import Image

HERE = Path(__file__).resolve().parent
BASE = HERE / 'Base'
CONCEPT = HERE / 'Concepts' / 'distant-school-entrance-v3.png'
NAMES = ('forest-far-mist', 'forest-far-trees', 'forest-belt-mid', 'forest-near')
W, H, S = 1024, 181, 4
PW, JOIN_LEFT = 340, 300
TREE_SHIFT = 7  # Match the rear grass line with a small vertical reframing of the upper view.
SCHOOL_COLORS = [(89,120,128),(66,109,122),(99,136,148),
                 (205,175,118),(232,196,136),(170,153,111)]

def save(path, im):
    path.parent.mkdir(parents=True, exist_ok=True)
    im.save(path, optimize=True)

def grid(im):
    if im.resize((W,H), Image.Resampling.NEAREST).resize((W*S,H*S), Image.Resampling.NEAREST).tobytes() != im.tobytes():
        raise AssertionError('Not an exact 4x4 grid')

def palette(existing, imported, mask=None, reserved=()):
    colors = sorted({p[:3] for p in existing.get_flattened_data() if p[3]})
    assert len(colors) <= 64
    for color in reserved:
        if color not in colors and len(colors)<64: colors.append(color)
    candidates = imported.quantize(colors=96, method=Image.Quantize.MEDIANCUT).convert('RGB')
    selected = list(mask.get_flattened_data()) if mask is not None else None
    counts = Counter(c for i,c in enumerate(candidates.get_flattened_data()) if selected is None or selected[i])
    while len(colors) < 64 and counts:
        best = max(counts, key=lambda p: counts[p]*min(sum((p[i]-c[i])**2 for i in range(3)) for c in colors))
        counts.pop(best)
        if best not in colors: colors.append(best)
    return colors

def mapped(imported, colors):
    pal = Image.new('P',(1,1))
    pal.putpalette([v for c in colors for v in c]+list(colors[0])*(256-len(colors)))
    return imported.quantize(palette=pal,dither=Image.Dither.NONE).convert('RGBA')

def boundary(imported, old):
    a,b=imported.load(),old.load()
    width=PW-JOIN_LEFT
    parents=[[0]*width for _ in range(H)]
    previous=[0.0]*width
    for y in range(H):
        current=[0.0]*width
        for j in range(width):
            x=JOIN_LEFT+j
            error=sum((a[x,y][c]-b[x,y][c])**2 for c in range(3))
            if 94<y<142: error+=((sum(a[x,y][:3])-sum(b[x,y][:3]))*.7)**2
            if y==0: current[j]=error
            else:
                p=min(range(max(0,j-1),min(width,j+2)),key=lambda k: previous[k]+(0 if k==j else 350))
                current[j]=error+previous[p]+(0 if p==j else 350)
                parents[y][j]=p
        previous=current
    j=min(range(width),key=lambda k:previous[k])
    seam=[0]*H
    for y in range(H-1,-1,-1):
        seam[y]=JOIN_LEFT+j
        j=parents[y][j]
    return seam

def ground_boundary(imported, old):
    """Join within the narrow rear grass band; keep the old lower path intact."""
    a,b=imported.load(),old.load()
    candidates=list(range(96,103))
    previous=[0.0]*len(candidates)
    parents=[[0]*len(candidates) for _ in range(PW)]
    for x in range(PW):
        current=[]
        for j,y in enumerate(candidates):
            error=sum((a[x,y][c]-b[x,y][c])**2 for c in range(3))
            if x==0: current.append(error)
            else:
                p=min(range(max(0,j-1),min(len(candidates),j+2)),key=lambda k:previous[k]+(0 if k==j else 700))
                current.append(error+previous[p]+(0 if p==j else 700))
                parents[x][j]=p
        previous=current
    j=min(range(len(candidates)),key=lambda k:previous[k])
    result=[0]*PW
    for x in range(PW-1,-1,-1):
        result[x]=candidates[j]
        j=parents[x][j]
    return result

def main():
    base={}
    for name in NAMES:
        im=Image.open(BASE/f'{name}.png').convert('RGBA')
        assert im.size==(4096,724)
        grid(im)
        base[name]=im.resize((W,H),Image.Resampling.NEAREST)
    reduced=Image.open(CONCEPT).convert('RGB').resize((PW,H),Image.Resampling.NEAREST)
    imported=reduced.copy()
    imported.paste(reduced.crop((0,0,PW,100-TREE_SHIFT)).resize((PW,100),Image.Resampling.NEAREST),(0,0))
    old=Image.new('RGBA',(W,H))
    for name in NAMES: old=Image.alpha_composite(old,base[name])
    upper_mask=Image.new('L',(PW,H))
    upper_mask.paste(255,(0,0,PW,103))
    main_pixels=mapped(imported,palette(base['forest-belt-mid'],imported,upper_mask,SCHOOL_COLORS))
    seam=boundary(main_pixels,old)
    ground=ground_boundary(main_pixels,old)
    for y in range(H):
        for x in range(seam[y]):
            if y<ground[x]: base['forest-belt-mid'].putpixel((x,y),main_pixels.getpixel((x,y)))
    source={n:im.resize((W*S,H*S),Image.Resampling.NEAREST) for n,im in base.items()}
    report=['Creative reference: Concepts/distant-school-entrance-v3.png',
      'Concept SHA256: '+hashlib.sha256(CONCEPT.read_bytes()).hexdigest(),
      'Authored canvas:4096x724; logical grid:1024x181; exact 4x output',
      'Near school facade and copied corridor door removed',
      'Distant school and entrance: fixed belt-mid; existing broad lower path and near bushes preserved',
      'School palette: reserved slate roof, warm stone, window shadow and light tones',
      f'Irregular joining boundary:x={min(seam)*S}..{max(seam)*S}px',
      'Existing forest pixels beyond x=1360 preserved exactly',
      'Existing lower path at y>=412 and whole near/far layers preserved exactly',
      'Return path opening:approximately x=260..570; interaction reference x=352,y=488',
      'Existing right end fence preserved at x~3740..4096']
    for name,im in source.items():
        grid(im)
        colors=Counter(im.get_flattened_data())
        alpha={p[3] for p in colors}
        opaque=sum(1 for p in colors if p[3])
        assert alpha.issubset({0,255}) and opaque<=64,(name,alpha,opaque)
        old_tail=Image.open(BASE/f'{name}.png').convert('RGBA').crop((1360,0,4096,724))
        assert im.crop((1360,0,4096,724)).tobytes()==old_tail.tobytes(),name
        old_full=Image.open(BASE/f'{name}.png').convert('RGBA')
        if name=='forest-belt-mid':
            assert im.crop((0,412,4096,724)).tobytes()==old_full.crop((0,412,4096,724)).tobytes()
        else:
            assert im.tobytes()==old_full.tobytes(),name
        save(HERE/'Source'/f'{name}.png',im)
        save(HERE/'Runtime'/f'{name}.png',im)
        report.append(f'{name}:{opaque} opaque colors; alpha{sorted(alpha)}; exact grid and unchanged tail PASS')
    composite=Image.new('RGBA',(4096,724))
    for name in NAMES: composite=Image.alpha_composite(composite,source[name])
    assert composite.getchannel('A').getextrema()==(255,255)
    save(HERE/'Preview'/'forest-composite.png',composite)
    for i,left in enumerate((0,1360,2736),1): save(HERE/'Preview'/f'forest-view-{i}.png',composite.crop((left,0,left+1360,724)))
    save(HERE/'Preview'/'forest-join-detail.png',composite.crop((1024,0,1664,724)))
    report.append('Composite coverage:all pixels opaque PASS')
    (HERE/'verification.txt').write_text('\n'.join(report)+'\n',encoding='utf-8')

if __name__=='__main__': main()
