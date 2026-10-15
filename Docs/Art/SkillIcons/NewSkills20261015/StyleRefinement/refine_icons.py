"""Native pixel cleanup: canonical frame/leather, generated symbol masks and existing palettes."""
from __future__ import annotations
import argparse
import hashlib
import json
import math
from collections import Counter
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = Path(__file__).resolve().parent
BASE = HERE.parent
UNIFIED = BASE.parent / 'Unified/Output/48'
FAMILIES = {'Slash':10,'Thrust':4,'Hit':5,'Defence':15}
ROLES = [(21,'자세 잡기','Defence'),(22,'연마','Slash'),(23,'꿰뚫기','Thrust'),
 (24,'받아치기','Hit'),(25,'비껴서기','Defence'),(26,'끝내기','Thrust'),
 (27,'에스토카다','Thrust'),(28,'바트망','Hit'),(29,'카덴스','Hit'),
 (30,'에스키브','Hit'),(31,'콩트르탕','Hit'),(32,'두블레','Thrust'),
 (33,'잔걸음','Slash'),(34,'숫돌','Defence'),(35,'갈아내기','Slash')]

def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def luma(p): return .299*p[0]+.587*p[1]+.114*p[2]
def rgbdist(a,b): return sum((a[i]-b[i])**2 for i in range(3))
def group(n): return 'Group'+('A' if n<=25 else 'B' if n<=30 else 'C')
def load(path): return Image.open(path).convert('RGBA')

def canonical(family):
    template=load(UNIFIED/f'role{FAMILIES[family]}.png')
    inside=template.getchannel('A').filter(ImageFilter.MinFilter(7))
    if family=='Hit':
        inside=Image.new('L',(48,48));ImageDraw.Draw(inside).rectangle((9,9,38,38),fill=255)
    # The protected frame is copied unchanged. Interior leather is rebuilt only
    # from dark colors already present in the exact canonical template.
    dark=Counter(p[:3] for p in template.get_flattened_data() if p[3] and 18<luma(p)<48)
    dominant=dark.most_common(1)[0][0]
    leather=[p for p,_ in dark.most_common() if rgbdist(p,dominant)<=100][:3]
    bg=template.copy()
    for y in range(48):
        for x in range(48):
            if inside.getpixel((x,y)):
                bg.putpixel((x,y),(*leather[(x*5+y*3+x*y)%len(leather)],255))
    protected=inside.point(lambda v:0 if v else 255)
    # A few original blade-tip/glint pixels overlap the metal at the horizontal
    # contact edge. They are not frame material and must never survive as a
    # second blade tip. Restore these from neighboring native metal pixels.
    exclusions=[]
    if family=='Thrust':
        # Replace the two full old-weapon contact strips with a clean native
        # vertical-frame row. This removes the old hilt/tip RGBA protrusions,
        # including black ink/alpha that a bright-color mask cannot identify.
        for xa,xb in ((0,11),(38,47)):
            for y in range(18,31):
                for x in range(xa,xb+1):
                    bg.putpixel((x,y),template.getpixel((x,16)))
                    protected.putpixel((x,y),0);exclusions.append((x,y))
    if family in ('Slash','Thrust'):
        for y in range(18,31):
            for x in range(5,45):
                p=template.getpixel((x,y))
                if inside.getpixel((x,y)) or not p[3]: continue
                if min(p[:3])>75 and p[0]>=p[1]*.97:
                    candidates=[]
                    for yy in range(max(0,y-6),min(48,y+7)):
                        q=template.getpixel((x,yy))
                        colored=(q[1]>q[0]*1.1) if family=='Thrust' else (q[0]>q[1]*1.3 and q[1]>q[2]*1.2)
                        if q[3] and colored: candidates.append((abs(yy-y),q))
                    if candidates:
                        bg.putpixel((x,y),min(candidates,key=lambda q:q[0])[1])
                        protected.putpixel((x,y),0);exclusions.append((x,y))
    return template,bg,inside,protected,exclusions

def source_geometry(family):
    m=Image.new('L',(48,48)); d=ImageDraw.Draw(m)
    if family=='Slash': d.polygon([(24,7),(41,24),(24,41),(7,24)],fill=255)
    elif family=='Thrust': d.polygon([(24,7),(39,15),(39,33),(24,41),(9,33),(9,15)],fill=255)
    elif family=='Hit': d.rectangle((11,11,36,36),fill=255)
    else: d.polygon([(24,8),(35,13),(34,29),(24,39),(14,29),(13,13)],fill=255)
    return m

def foreground(im,family):
    geometry=source_geometry(family)
    safe=im.getchannel('A').filter(ImageFilter.MinFilter(9))
    bright=Image.new('L',(48,48))
    for y in range(48):
        for x in range(48):
            p=im.getpixel((x,y)); r,g,b,_=p
            steel=max(p[:3])-min(p[:3])<35 and luma(p)>60 and r>=g*.9
            ivory=luma(p)>100 and min(p[:3])>55 and max(p[:3])-min(p[:3])<70 and r>=g*.9 and g>=b*.9
            cool=((g>r*1.1 and g-r>12) or b>r*1.25) and max(g,b)>65 and (family!='Thrust' or safe.getpixel((x,y)))
            warm=r>110 and r>g*1.45 and g>45 and b<90 and safe.getpixel((x,y))
            crimson=r>75 and r>g*2.4 and r>b*2 and safe.getpixel((x,y))
            if p[3] and geometry.getpixel((x,y)) and (steel or ivory or cool or warm or crimson):
                bright.putpixel((x,y),255)
    outline=bright.filter(ImageFilter.MaxFilter(3))
    out=Image.new('RGBA',(48,48))
    for y in range(48):
        for x in range(48):
            if not geometry.getpixel((x,y)) or not outline.getpixel((x,y)): continue
            p=im.getpixel((x,y))
            # Original generated pixels are retained; the 1px surrounding ink
            # prevents disconnected bright shards after palette matching.
            out.putpixel((x,y),p if bright.getpixel((x,y)) else (12,12,10,255))
    return out

def paste_piece(dst,src,box,target,size=None):
    part=src.crop(box)
    if size: part=part.resize(size,Image.Resampling.NEAREST)
    dst.alpha_composite(part,target)

def tidy_symbol(n,im,family):
    center_group='GroupA' if n in (23,24) else 'GroupB'
    center_path=BASE/'CenterRefinement'/center_group/'48'/f'role{n}.png'
    # Revised center-only native PNGs already contain clean alpha and preserve
    # their blade, small cadence arrow and restrained breath marks as authored.
    revised=n in (23,24,29,31) and center_path.is_file()
    fg=load(center_path) if revised else foreground(im,family)
    if n==23:
        # Preserve the approved native4 blade thickness/brightness and guard.
        # The generated center contributes only its two restrained trails.
        old=load(UNIFIED/'role4.png');out=Image.new('RGBA',(48,48))
        paste_piece(out,fg,(14,19,33,22),(14,19))
        paste_piece(out,fg,(14,27,33,29),(14,27))
        for yy in range(20,30):
            for xx in range(8,12): out.putpixel((xx,yy),old.getpixel((xx,yy)))
        for yy in range(23,26):
            for xx in range(12,34): out.putpixel((xx,yy),old.getpixel((xx,yy)))
        steel=old.getpixel((26,24));shade=old.getpixel((26,25))
        for xx in (34,35):
            out.putpixel((xx,24),steel)
            if xx==34: out.putpixel((xx,25),shade)
        fg=out
    if n==24:
        # The native approved5 main weapon supplies its broad steel clusters.
        # A thin right-side opposing blade/contact from the generated center
        # makes this the parry/counter skill rather than the base hit symbol.
        old=load(UNIFIED/'role5.png');out=Image.new('RGBA',(48,48))
        mask=Image.new('L',(48,48));ImageDraw.Draw(mask).polygon(
            [(12,10),(18,14),(20,17),(24,20),(29,29),(32,32),(32,37),
             (27,38),(23,34),(24,30),(20,27),(17,23),(13,20),(12,17),(14,14)],fill=255)
        for yy in range(48):
            for xx in range(48):
                if mask.getpixel((xx,yy)):out.putpixel((xx,yy),old.getpixel((xx,yy)))
        paste_piece(out,fg,(28,16,40,29),(28,16))
        fg=out
    # Width/height are constrained to match established weapon scale, rather
    # than the inflated generated symbol that previously crowded the frame.
    if n==28:
        # A detached source glint beneath the contact is too small to read.
        for p in ((34,34),(34,35),(35,35),(36,35),(35,36),(36,36)): fg.putpixel(p,(0,0,0,0))
    if n==30:
        # Reconnect the original generated missed-attack stroke. Only this
        # existing secondary line is polished; its angle and extent stay fixed.
        d=ImageDraw.Draw(fg);d.rectangle((11,10,36,20),fill=(0,0,0,0))
        d.line([(12,12),(31,18)],fill=(12,12,10,255),width=3)
        d.line([(12,12),(31,18)],fill=(178,175,155,255),width=1)
    extent={21:(24,31),22:(27,27),23:(35,22),24:(31,32),25:(27,27),
      26:(33,22),27:(30,28),28:(27,29),29:(33,25),30:(27,29),
      31:(29,31),32:(33,25),33:(29,28),34:(27,29),35:(29,30)}[n]
    bounds=fg.getbbox()
    crop=fg.crop(bounds)
    scale=min(extent[0]/crop.width,extent[1]/crop.height,1.0)
    dims=(max(1,round(crop.width*scale)),max(1,round(crop.height*scale)))
    crop=crop.resize(dims,Image.Resampling.NEAREST)
    out=Image.new('RGBA',(48,48)); out.alpha_composite(crop,((48-dims[0])//2,(48-dims[1])//2))
    if n in (23,29):
        # Native finishing of the horizontal blade tip: the generated23 center
        # ended in a flat two-row rectangle. Taper the last two existing rows
        # to a single ivory point, staying within the cleaned frame opening.
        y=23
        if n==29: y=23
        for xx in range(36,41):
            for yy in range(y-1,y+4): out.putpixel((xx,yy),(0,0,0,0))
        steel=load(UNIFIED/'role4.png').getpixel((26,24))
        shade=load(UNIFIED/'role4.png').getpixel((26,25))
        out.putpixel((36,y),steel);out.putpixel((36,y+1),shade)
        out.putpixel((37,y),steel)
    return out

def apply_palette(symbol,template,inside):
    # All RGB samples originate in approved old pixel assets. Frame colors are
    # locked; the remaining budget adds six native teal/crimson accent colors.
    frame_colors={template.getpixel((x,y))[:3] for y in range(48) for x in range(48)
                  if template.getpixel((x,y))[3] and not inside.getpixel((x,y))}
    native=Counter(p[:3] for p in template.get_flattened_data() if p[3])
    palette=sorted(frame_colors)
    for p,_ in native.most_common():
        if p not in palette and len(palette)<42: palette.append(p)
    accents=[];cool_palette=[];red_palette=[]
    for role in (16,20):
        vals=Counter(p[:3] for p in load(UNIFIED/f'role{role}.png').get_flattened_data()
                     if p[3] and ((p[1]>p[0]*1.2 and p[1]>110) or (p[0]>p[1]*2.4 and p[0]>90 and p[2]<65)))
        selected=[p for p,_ in vals.most_common(3)]
        accents += selected
        if role==16: cool_palette=selected
        else: red_palette=selected
    for p in accents:
        if p not in palette and len(palette)<48: palette.append(p)
    out=symbol.copy()
    cache={}
    for y in range(48):
        for x in range(48):
            p=out.getpixel((x,y))
            if not p[3]: continue
            if p[:3] not in cache:
                r,g,b=p[:3]
                choices=palette
                if ((g>r*1.1 and g-r>12) or b>r*1.25) and max(g,b)>65:
                    choices=[q for q in cool_palette if q in palette] or palette
                elif r>75 and r>g*2.4 and r>b*2:
                    choices=[q for q in red_palette if q in palette] or palette
                cache[p[:3]]=min(choices,key=lambda q:rgbdist(p,q))
            out.putpixel((x,y),(*cache[p[:3]],255))
    return out,palette

def refine(n,family):
    srcpath=BASE/group(n)/'48'/f'role{n}.png'; src=load(srcpath)
    center_group='GroupA' if n in (23,24) else 'GroupB'
    center_path=BASE/'CenterRefinement'/center_group/'48'/f'role{n}.png'
    effective_source=center_path if n in (23,24,29,31) and center_path.is_file() else srcpath
    template,bg,inside,frame_mask,exclusions=canonical(family)
    symbol=tidy_symbol(n,src,family)
    symbol,palette=apply_palette(symbol,template,inside)
    result=bg.copy()
    symbol_over_frame=[]
    for y in range(48):
        for x in range(48):
            p=symbol.getpixel((x,y))
            tip_exception=n in (23,29) and 35<=x<=38 and 21<=y<=27
            if p[3] and (inside.getpixel((x,y)) or tip_exception) and bg.getpixel((x,y))[3]:
                result.putpixel((x,y),p)
                if not inside.getpixel((x,y)): symbol_over_frame.append((x,y))
    protected=[(x,y) for y in range(48) for x in range(48) if frame_mask.getpixel((x,y)) and (x,y) not in symbol_over_frame]
    assert all(result.getpixel(p)==template.getpixel(p) for p in protected)
    assert result.getchannel('A').tobytes()==bg.getchannel('A').tobytes()
    colors={p[:3] for p in result.get_flattened_data() if p[3]}
    assert len(colors)<=48,(n,len(colors))
    assert set(result.getchannel('A').get_flattened_data())<={0,255}
    return result,dict(template_role=FAMILIES[family],template_sha256=sha(UNIFIED/f'role{FAMILIES[family]}.png'),
      source=str(srcpath.relative_to(BASE)),source_sha256=sha(srcpath),
      effective_center_source=str(effective_source.relative_to(BASE)),effective_center_sha256=sha(effective_source),
      native_weapon_template_role=4 if n==23 else 5 if n==24 else None,
      protected_frame_pixels=len(protected),protected_opaque_frame_pixels=sum(bool(template.getpixel(p)[3]) for p in protected),frame_mismatches=0,clean_frame_alpha_exact=True,
      original_template_alpha_exact=result.getchannel('A').tobytes()==template.getchannel('A').tobytes(),
      original_weapon_contact_exclusions=exclusions,
      authored_blade_tip_overlay=symbol_over_frame,
      opaque_colors=len(colors),symbol_bbox=list(symbol.getbbox()),logical_canvas=[48,48],runtime_canvas=[192,192],hard_alpha=True,runtime_scale='nearest 4x')

def compare(rows,images,path):
    width,height=768, len(rows)*192+32
    sheet=Image.new('RGB',(width,height),'#29251f'); d=ImageDraw.Draw(sheet)
    font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',14)
    for x,label in ((8,'기존 기준'),(200,'수정 전'),(392,'양식 수정'),(584,'실제 48px')):
        d.text((x,5),label,fill='#e8d6b6',font=font)
    for row,(n,name,fam) in enumerate(rows):
        y=32+row*192
        old=load(UNIFIED/f'role{FAMILIES[fam]}.png')
        before=load(BASE/group(n)/'48'/f'role{n}.png')
        after=images[n]
        for x,im in ((16,old),(208,before),(400,after)):
            im=im.resize((144,144),Image.Resampling.NEAREST);sheet.paste(im,(x,y),im)
        sheet.paste(after,(616,y+36),after)
        d.text((400,y+151),f'{n} {name} / {fam}',fill='#e8d6b6',font=font)
    sheet.save(path)

def contact(rows,images,path,scale):
    cw=48*scale+32;ch=48*scale+44
    sheet=Image.new('RGB',(cw*5,ch*3),'#29251f');d=ImageDraw.Draw(sheet)
    font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',12 if scale==1 else 16)
    for i,(n,name,family) in enumerate(rows):
        x=i%5*cw;y=i//5*ch
        im=images[n].resize((48*scale,48*scale),Image.Resampling.NEAREST)
        sheet.paste(im,(x+16,y+4),im)
        d.text((x+cw/2,y+48*scale+14),f'{n} {name}',anchor='mt',fill='#e8d6b6',font=font)
    sheet.save(path)

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--samples',action='store_true');args=ap.parse_args()
    rows=[r for r in ROLES if r[0] in (22,26,28,34)] if args.samples else ROLES
    for sub in ('48','Runtime192','Masks','CleanBases'): (HERE/sub).mkdir(parents=True,exist_ok=True)
    reports={};images={}
    for n,name,family in rows:
        im,report=refine(n,family); images[n]=im
        p=HERE/'48'/f'role{n}.png';im.save(p)
        runtime=im.resize((192,192),Image.Resampling.NEAREST);runtime.save(HERE/'Runtime192'/p.name)
        assert load(HERE/'Runtime192'/p.name).tobytes()==runtime.tobytes()
        report.update(name=name,family=family,logical_sha256=sha(p),runtime_sha256=sha(HERE/'Runtime192'/p.name))
        reports[f'role{n}']=report
        template,bg,inside,frame_mask,exclusions=canonical(family)
        inside.save(HERE/'Masks'/f'{family}-editable-interior.png')
        frame_mask.save(HERE/'Masks'/f'{family}-protected-frame.png')
        for p in report['authored_blade_tip_overlay']: frame_mask.putpixel(tuple(p),0)
        frame_mask.save(HERE/'Masks'/f'role{n}-protected-frame.png')
        bg.save(HERE/'CleanBases'/f'{family}.png')
    compare(rows,images,HERE/('family-samples.png' if args.samples else 'before-after-family-comparison.png'))
    if not args.samples:
        contact(rows,images,HERE/'contact-48-native.png',1)
        contact(rows,images,HERE/'contact-144.png',3)
    (HERE/('sample-report.json' if args.samples else 'report.json')).write_text(json.dumps(reports,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(f'PASS: {len(rows)} icons. Protected canonical metal RGBA is pixel-identical; original weapon-contact strips cleaned.')

if __name__=='__main__': main()
