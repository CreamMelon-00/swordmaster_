-- Draw the complete new Cadet B into Rebuild/runtime. This script never reads
-- any existing character image. Runtime publication is a separate step.
local root=app.params['root'] or 'C:/Users/User/Documents/swordmaster_'
local here=root..'/Docs/Art/EnemyVariants/CadetB/Rebuild/'
local draw=dofile(here..'draw_character.lua');local poses=dofile(here..'animation_poses.lua')
local out=here..'runtime/';app.fs.makeAllDirectories(out)
local names={'idle','slash','slash-2','slash-3','pierce','pierce-2','pierce-3','blunt','blunt-2','blunt-3'}
local attacks={140,100,120,60,70,90,70,100,90,90,130,200};local idle={180,140,160,140,160,140,160,180}
local s=Sprite(256,224,ColorMode.RGB);s.layers[1].name='Cadet B - sandy hair, olive collar, trousers'
local ranges={};local number=0;local skeleton={}
local function frame(relative,p,duration)
 local ok,im,joints=pcall(draw.draw,poses.copy(p));assert(ok,relative..': '..tostring(im));number=number+1
 if number>1 then s:newEmptyFrame(number) end
 s.frames[number].duration=duration/1000;s:newCel(s.layers[1],number,im,Point(0,0))
 local dir=app.fs.filePath(out..relative);app.fs.makeAllDirectories(dir);im:saveAs(out..relative..'.png')
 skeleton[#skeleton+1]={path=relative,joints=joints}
 return im
end
for _,key in ipairs(names) do
 local first=number+1;local sheet=Image(256*(key=='idle' and 8 or 12),224,ColorMode.RGB)
 for i,p in ipairs(poses[key]) do
  local im=frame(key..'/frame-'..string.format('%02d',i),p,(key=='idle' and idle or attacks)[i]);sheet:drawImage(im,Point((i-1)*256,0))
 end
 ranges[#ranges+1]={key,first,number};app.fs.makeAllDirectories(here..'sheets');sheet:saveAs(here..'sheets/'..key..'.png')
 print('NEW_CLIP '..key..' '..first..' '..number)
end
for _,key in ipairs({'block','block-2','hurt','hurt-2'}) do local n=number+1;frame('poses/'..key,poses.poses[key],160);ranges[#ranges+1]={'poses-'..key,n,n} end
frame('move/frame-01',poses.move[1],120);ranges[#ranges+1]={'move-frame-01',number,number}
for _,r in ipairs(ranges) do local t=s:newTag(r[2],r[3]);t.name=r[1] end
assert(number==121 and #s.tags==15)
s:saveAs(here..'CadetB-New-AllAnimations.aseprite')
local f=assert(io.open(here..'skeleton.lua','w'));f:write('return {\n')
for _,v in ipairs(skeleton) do
 f:write('["'..v.path..'"]={')
 for k,p in pairs(v.joints) do f:write(k..'={'..p[1]..','..p[2]..'},') end
 f:write('},\n')
end
f:write('}\n');f:close()
print('NEW_CADET_COMPLETE '..number)
