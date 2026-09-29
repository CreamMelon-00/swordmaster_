local out='C:/Users/user/Documents/SwordMasterStory/Aseprite_Character/Enemy_Expansion'
local original='C:/Fork/swordmaster_/Docs/Art/EnemyStudent/Enemy_Uniform_Animations.aseprite'
local W,H=256,224;local pc=app.pixelColor
local extract=dofile(out..'/extract_poses.lua')
local sprite=app.open(original);assert(#sprite.frames==45)
local layer=sprite.layers[1];local ranges={}
for _,t in ipairs(sprite.tags) do ranges[#ranges+1]={t.name,t.fromFrame.frameNumber,t.toFrame.frameNumber} end
local ready=Image(W,H,ColorMode.RGB);ready:drawSprite(sprite,1)
local times={140,100,120,60,70,90,70,100,90,90,130,200}
local order={0,1,1,2,3,3,3,4,4,4,0,0}
local keys={'slash-2','slash-3','pierce-2','pierce-3','blunt-2','blunt-3'}
local starts={slash=9,pierce=21,blunt=33}
local function check(im,label)
 local count=0
 for p in im:pixels() do if pc.rgbaA(p())>0 then
  count=count+1;assert(pc.rgbaA(p())==255,label..' partial alpha')
  assert(p.x>0 and p.x<W-1 and p.y>0 and p.y<H-1,label..' canvas edge clipping')
 end end
 assert(count>2000,label..' empty cel')
end
for _,key in ipairs(keys) do
 local poses=extract(key);local first=#sprite.frames+1
 for i,n in ipairs(order) do local im=n==0 and ready or poses[n];check(im,key..'/'..i)
  local f=#sprite.frames+1;sprite:newEmptyFrame(f);sprite.frames[f].duration=times[i]/1000
  sprite:newCel(layer,f,im,Point(0,0));im:saveAs(out..'/Resources/'..key..'/frame-'..string.format('%02d',i)..'.png')
 end
 starts[key]=first;ranges[#ranges+1]={key,first,#sprite.frames}
end
local hurt=extract('hurt')[1];check(hurt,'hurt')
sprite:newEmptyFrame();sprite.frames[#sprite.frames].duration=.16
sprite:newCel(layer,#sprite.frames,hurt,Point(0,0));hurt:saveAs(out..'/Resources/poses/hurt.png')
ranges[#ranges+1]={'Hurt',#sprite.frames,#sprite.frames}
while #sprite.tags>0 do sprite:deleteTag(sprite.tags[1]) end
for _,r in ipairs(ranges) do local t=sprite:newTag(r[2],r[3]);t.name=r[1] end
assert(#sprite.frames==118 and #sprite.tags==12)
local old=app.open(original)
for i=1,45 do local a=Image(W,H,ColorMode.RGB);local b=Image(W,H,ColorMode.RGB)
 a:drawSprite(sprite,i);b:drawSprite(old,i);assert(a:isEqual(b),'Original frame changed '..i)
end
sprite:saveAs(out..'/Enemy_AllAttacks3_Hurt.aseprite')
local function frame(n) local im=Image(W,H,ColorMode.RGB);im:drawSprite(sprite,n);return im end
local function enlarge(im,f) local r=Image(im.width*f,im.height*f,ColorMode.RGB);for p in r:pixels() do p(im:getPixel(math.floor(p.x/f),math.floor(p.y/f))) end;return r end
for _,type in ipairs({'slash','pierce','blunt'}) do
 local preview=Sprite(W*3,H,ColorMode.RGB)
 for i=1,12 do if i>1 then preview:newEmptyFrame(i) end;preview.frames[i].duration=times[i]/1000
  local im=Image(W*3,H,ColorMode.RGB);for p in im:pixels() do p(pc.rgba(38,35,39,255)) end
  for v=1,3 do local key=v==1 and type or type..'-'..v;im:drawImage(frame(starts[key]+i-1),Point((v-1)*W,0)) end
  preview:newCel(preview.layers[1],i,im,Point(0,0))
  if i==5 then enlarge(im,2):saveAs(out..'/'..type..'-contacts.png') end
 end
 preview:saveAs(out..'/'..type..'-three.aseprite');preview:saveCopyAs(out..'/'..type..'-three.gif')
end
enlarge(hurt,3):saveAs(out..'/hurt-preview.png')
local checked=app.open(out..'/Enemy_AllAttacks3_Hurt.aseprite');assert(#checked.frames==118 and #checked.tags==12)
print('VERIFIED 118 frames, 12 tags; original 45 frames unchanged; 72 new attack cels and one hurt cel; no clipping.')
