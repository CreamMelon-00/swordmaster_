local out='C:/Users/user/Documents/SwordMasterStory/Aseprite_Character/Enemy_Reactions'
local original='C:/Fork/swordmaster_/Docs/Art/EnemyStudent/Expansion/Enemy_AllAttacks3_Hurt.aseprite'
local W,H=256,224; local pc=app.pixelColor
local extract=dofile(out..'/extract_poses.lua')
local sprite=app.open(original); assert(#sprite.frames==118)
local old=app.open(original)
local poses={}
for _,key in ipairs({'block-2','hurt-2'}) do
 local im=extract(key)[1]; local count=0
 for p in im:pixels() do if pc.rgbaA(p())>0 then
  count=count+1;assert(pc.rgbaA(p())==255,key..' partial alpha')
  assert(p.x>0 and p.x<W-1 and p.y>0 and p.y<H-1,key..' clipped')
 end end
 assert(count>2000,key..' empty');poses[key]=im
 local f=#sprite.frames+1;sprite:newEmptyFrame(f);sprite.frames[f].duration=.16
 sprite:newCel(sprite.layers[1],f,im,Point(0,0))
 local tag=sprite:newTag(f,f);tag.name=key=='block-2' and 'Block-2' or 'Hurt-2'
 im:saveAs(out..'/Resources/poses/'..key..'.png')
end
local corrected=extract('hurt')[1]
for p in corrected:pixels() do if pc.rgbaA(p())>0 then
 assert(pc.rgbaA(p())==255,'corrected hurt partial alpha')
 assert(p.x>0 and p.x<W-1 and p.y>0 and p.y<H-1,'corrected hurt clipped')
end end
sprite:deleteCel(sprite.layers[1]:cel(118));sprite:newCel(sprite.layers[1],118,corrected,Point(0,0))
corrected:saveAs(out..'/Resources/poses/hurt.png')
for i=1,117 do local a=Image(W,H,ColorMode.RGB);local b=Image(W,H,ColorMode.RGB)
 a:drawSprite(sprite,i);b:drawSprite(old,i);assert(a:isEqual(b),'Existing frame changed '..i)
end
sprite:saveAs(out..'/Enemy_AllAttacks3_Reactions2.aseprite')
local preview=Image(W*4,H,ColorMode.RGB)
for p in preview:pixels() do p(pc.rgba(38,35,39,255)) end
for i,frame in ipairs({45,119,118,120}) do
 local im=Image(W,H,ColorMode.RGB);im:drawSprite(sprite,frame);preview:drawImage(im,Point((i-1)*W,0))
end
local large=Image(preview.width*2,preview.height*2,ColorMode.RGB)
for p in large:pixels() do p(preview:getPixel(math.floor(p.x/2),math.floor(p.y/2))) end
large:saveAs(out..'/reactions-comparison.png')
local beforeAfter=Image(W*3,H,ColorMode.RGB)
for p in beforeAfter:pixels() do p(pc.rgba(38,35,39,255)) end
for i,n in ipairs({1,118,118}) do
 local im=Image(W,H,ColorMode.RGB);im:drawSprite(i==2 and old or sprite,n);beforeAfter:drawImage(im,Point((i-1)*W,0))
end
local enlarged=Image(W*9,H*3,ColorMode.RGB)
for p in enlarged:pixels() do p(beforeAfter:getPixel(math.floor(p.x/3),math.floor(p.y/3))) end
enlarged:saveAs(out..'/hurt-scale-before-after.png')
local checked=app.open(out..'/Enemy_AllAttacks3_Reactions2.aseprite')
assert(#checked.frames==120 and #checked.tags==14)
local report=assert(io.open(out..'/verification.txt','w'))
report:write('VERIFIED 120 frames, 14 tags; first 117 frames unchanged; original Hurt frame 118 corrected; Block-2/Hurt-2 appended.\nAll three edited poses: opaque foreground, transparent background, no canvas-edge clipping.\n')
report:close()
