"""Editable 48 px gold enemy icon drafts. Run with Pillow, then import PNG to Aseprite."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[3]
C = dict(ink="#180f0c", leather="#251a15", grain="#31221a",
         copper_dark="#713719", copper="#bd672e", copper_hi="#ed9b4b",
         rivet="#b7834b", gold_dark="#9b5e13", gold="#f0b629",
         gold_hi="#ffe17a", ivory="#fbf0d0", steel_dark="#5e6871",
         steel="#b7c0c0", red_dark="#521812", red="#b92f20", red_hi="#ed5d2d")

def badge():
    im = Image.new("RGBA", (48, 48))
    d = ImageDraw.Draw(im)
    for points, color in [
        ([(24,1),(47,24),(24,47),(1,24)], "ink"),
        ([(24,3),(45,24),(24,45),(3,24)], "copper_dark"),
        ([(24,5),(43,24),(24,43),(5,24)], "copper"),
        ([(24,8),(40,24),(24,40),(8,24)], "ink"),
        ([(24,9),(39,24),(24,39),(9,24)], "leather")]:
        d.polygon(points, fill=C[color])
    for points, color in [
        ([(24,4),(5,23)],"copper_hi"), ([(25,5),(43,23)],"copper_hi"),
        ([(43,25),(25,43)],"copper_dark"), ([(23,43),(5,25)],"copper_dark")]:
        d.line(points, fill=C[color])
    for y in range(11,38):
        for x in range(11,38):
            if abs(x-24)+abs(y-24)<14 and (x*17+y*31)%29==0:
                im.putpixel((x,y),(49,34,26,255))
    for x,y in ((24,4),(44,24),(24,44),(4,24)):
        d.ellipse((x-3,y-3,x+3,y+3),fill=C["ink"])
        d.ellipse((x-2,y-2,x+2,y+2),fill=C["rivet"])
        d.point((x-1,y-1),fill=C["ivory"])
        d.point((x+1,y+1),fill=C["copper_dark"])
    return im

def laudare():
    im=badge(); d=ImageDraw.Draw(im)
    for pts in ([(12,17),(16,13),(22,11),(27,13)],
                [(10,23),(13,19),(18,17)]):
        d.line(pts,fill=C["copper_dark"],width=4)
        d.line(pts,fill=C["copper_hi"],width=2)
    for start,end,color in ((58,122,"gold_hi"),(208,282,"gold")):
        d.arc((14,14,34,34),start,end,fill=C["gold_dark"],width=5)
        d.arc((14,14,34,34),start,end,fill=C[color],width=3)
    d.line([(12,36),(19,29)],fill=C["ink"],width=6)
    d.line([(12,36),(19,29)],fill=C["copper"],width=3)
    d.ellipse((9,35,13,39),fill=C["ink"])
    d.point((11,37),fill=C["gold_hi"])
    d.polygon([(17,29),(32,16),(39,11),(35,19),(21,33)],fill=C["ink"])
    d.polygon([(19,29),(33,16),(37,13),(34,18),(21,31)],fill=C["steel_dark"])
    d.polygon([(20,28),(34,16),(37,13),(33,19),(21,30)],fill=C["steel"])
    d.line([(20,29),(35,16)],fill=C["ivory"])
    d.line([(14,27),(22,35)],fill=C["ink"],width=5)
    d.line([(14,27),(22,35)],fill=C["gold_dark"],width=3)
    d.line([(15,27),(21,33)],fill=C["gold_hi"])
    d.polygon([(37,10),(39,13),(42,13),(39,16),(38,19),(36,16),(33,15),(36,13)],fill=C["gold"])
    d.polygon([(37,12),(38,14),(40,14),(38,15),(37,17),(36,15),(35,14)],fill=C["gold_hi"])
    # Two separated break fragments sit in front so they remain legible at 1x.
    d.polygon([(32,26),(37,27),(35,32),(31,32)],fill=C["ink"])
    d.polygon([(33,27),(36,28),(34,31),(32,30)],fill=C["gold_hi"])
    d.polygon([(10,25),(16,26),(15,31),(11,30)],fill=C["ink"])
    d.polygon([(11,26),(15,27),(14,30),(12,29)],fill=C["gold"])
    return im

def praedicare():
    im=badge(); d=ImageDraw.Draw(im)
    d.ellipse((15,36,33,41),fill=C["red_dark"])
    d.arc((15,36,33,41),9,171,fill=C["red_hi"],width=2)
    d.arc((15,36,33,41),194,348,fill=C["red"],width=2)
    d.ellipse((20,38,28,40),fill=C["leather"])
    d.polygon([(24,27),(27,31),(34,31),(29,34),(31,37),(24,35),
               (17,37),(19,34),(14,31),(21,31)],fill=C["gold_dark"])
    d.polygon([(24,29),(26,32),(31,32),(27,34),(28,35),(24,34),
               (20,35),(21,34),(17,32),(22,32)],fill=C["gold"])
    d.line([(24,30),(24,35)],fill=C["gold_hi"],width=2)
    d.polygon([(21,7),(27,7),(27,15),(31,15),(33,19),(28,20),
               (27,31),(24,37),(21,31),(20,20),(15,19),(17,15),(21,15)],fill=C["ink"])
    d.rectangle((22,8,26,16),fill=C["copper_dark"])
    d.rectangle((23,9,24,15),fill=C["gold_hi"])
    d.rectangle((21,7,27,9),fill=C["gold"])
    d.point((22,8),fill=C["ivory"])
    d.polygon([(17,16),(31,16),(29,18),(19,18)],fill=C["gold_dark"])
    d.line([(19,16),(29,16)],fill=C["gold_hi"])
    d.polygon([(21,19),(27,19),(26,30),(24,35),(22,30)],fill=C["steel_dark"])
    d.polygon([(23,19),(26,19),(25,30),(24,34),(23,30)],fill=C["steel"])
    d.line([(24,20),(24,32)],fill=C["ivory"])
    # Two broad gold impact streaks flank the single dark sword.
    d.polygon([(12,25),(20,28),(19,31),(14,29)],fill=C["gold_dark"])
    d.polygon([(14,25),(20,28),(18,29),(14,27)],fill=C["gold_hi"])
    d.polygon([(36,25),(28,28),(29,31),(34,29)],fill=C["gold_dark"])
    d.polygon([(34,25),(28,28),(30,29),(34,27)],fill=C["gold_hi"])
    return im

def preview(icons):
    oldroot=ROOT/"Assets"/"Game"/"Resources"/"SkillRoles"
    old={n:Image.open(oldroot/f"role{n}.png").convert("RGBA").resize(
        (48,48),Image.Resampling.NEAREST) for n in (18,20)}
    sheet=Image.new("RGBA",(668,312),(31,24,21,255))
    d=ImageDraw.Draw(sheet); font=ImageFont.load_default()
    for col,n in enumerate((18,20)):
        x=12+col*328
        d.text((x,5),f"role{n}  ORIGINAL / DRAFT",fill=C["ivory"],font=font)
        sheet.alpha_composite(old[n],(x+62,26))
        sheet.alpha_composite(icons[n],(x+218,26))
        d.text((x,79),"48 px: actual 1x",fill=C["gold_hi"],font=font)
        d.text((x,105),"before                 draft",fill=C["ivory"],font=font)
        sheet.alpha_composite(old[n].resize((144,144),Image.Resampling.NEAREST),(x,134))
        sheet.alpha_composite(icons[n].resize((144,144),Image.Resampling.NEAREST),(x+164,134))
        d.text((x,284),"3x nearest-neighbor preview",fill=C["gold_hi"],font=font)
    sheet.save(OUT/"gold-refinement-preview.png")

def fit44(im):
    art=im.crop(im.getbbox()).resize((44,44),Image.Resampling.NEAREST)
    out=Image.new("RGBA",(48,48))
    out.alpha_composite(art,(2,2))
    return out

def contact20(icons):
    base=ROOT/"Docs"/"Art"/"SkillIcons"/"Unified"/"Output"/"48"
    all_icons={n:Image.open(base/f"role{n}.png").convert("RGBA") for n in range(1,21)}
    all_icons.update(icons)
    sheet=Image.new("RGB",(440,344),(37,32,28))
    d=ImageDraw.Draw(sheet); font=ImageFont.load_default()
    d.text((8,6),"Gold refinement with all 20 icons / 48 px",fill=(232,212,168),font=font)
    for n in range(1,21):
        col,row=(n-1)%5,(n-1)//5
        x,y=col*88,24+row*80
        d.rectangle((x+3,y+3,x+83,y+75),fill=(52,45,38),outline=(109,84,55))
        icon=all_icons[n]
        sheet.paste(icon,(x+19,y+6),icon.getchannel("A"))
        d.text((x+27,y+58),f"role{n:02d}",fill=(224,208,174),font=font)
    sheet.save(OUT/"gold-refinement-20-contact.png")


if __name__=="__main__":
    icons={18:fit44(laudare()),20:fit44(praedicare())}
    for n,im in icons.items():
        im.save(OUT/f"role{n}-draft.png")
        im.save(OUT/f"role{n}.png")
    preview(icons)
    contact20(icons)
