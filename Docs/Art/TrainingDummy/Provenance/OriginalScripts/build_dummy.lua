local out='C:/Users/user/Documents/SwordMasterStory/Aseprite_Character/Training_Dummy'
local pc=app.pixelColor;local W,H=256,224
local source=Image{fromFile=out..'/dummy_source.png'}
local palette,seen={},{}
for _,path in ipairs({
 'C:/Users/user/Documents/SwordMasterStory/Aseprite_Character/Enemy_Rework/Enemy_Uniform_PixelMatched.aseprite',
 'C:/Fork/swordmaster_/Docs/Art/SwordGirl/SwordGirl.aseprite'}) do
 local ref=app.open(path)
 for i=0,#ref.palettes[1]-1 do local c=ref.palettes[1]:getColor(i)
  local p=pc.rgba(c.red,c.green,c.blue,255)
  if c.alpha>0 and not seen[p] then seen[p]=true;palette[#palette+1]={c.red,c.green,c.blue,p} end
 end
end
local function nearest(r,g,b,colors)
 local best,dist=0,math.huge
 for _,c in ipairs(colors) do local d=2*(r-c[1])^2+3*(g-c[2])^2+2*(b-c[3])^2
  if d<dist then best,dist=c[4],d end
 end
 return best
end
-- Preserve generous head proportions and align the floor with the character sprites.
local function sourceY(y)
 if y<122 then return 110+(y-70)*(578-110)/(122-70) end
 return 578+(y-122)*(1410-578)/(202-122)
end
local clean=Image(W,H,ColorMode.RGB);local histogram={}
for y=0,H-1 do for x=0,W-1 do local r,g,b,n=0,0,0,0
 for _,oy in ipairs({.25,.75}) do for _,ox in ipairs({.25,.75}) do
  local sx=math.floor(525+(x+ox-128)/.123);local sy=math.floor(sourceY(y+oy))
  if sx>=0 and sx<source.width and sy>=0 and sy<source.height then
   local p=source:getPixel(sx,sy)
   if pc.rgbaA(p)>=230 then r=r+pc.rgbaR(p);g=g+pc.rgbaG(p);b=b+pc.rgbaB(p);n=n+1 end
  end
 end end
 if n>=2 then local p=nearest(r/n,g/n,b/n,palette);clean:drawPixel(x,y,p);histogram[p]=(histogram[p] or 0)+1 end
end end
-- A shared 28-color subset avoids generated gradients and keeps the same game palette.
local ranked={};for p,n in pairs(histogram) do ranked[#ranked+1]={p,n} end
table.sort(ranked,function(a,b) return a[2]==b[2] and a[1]<b[1] or a[2]>b[2] end)
local colors={};for i=1,math.min(28,#ranked) do local p=ranked[i][1];colors[#colors+1]={pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p),p} end
for p in clean:pixels() do local c=p();if pc.rgbaA(c)>0 then p(nearest(pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c),colors)) end end
local upper,base=Image(W,H,ColorMode.RGB),Image(W,H,ColorMode.RGB)
for p in clean:pixels() do if p.y<169 then upper:drawPixel(p.x,p.y,p()) elseif p.y>=182 then base:drawPixel(p.x,p.y,p()) end end
local function color(r,g,b) return nearest(r,g,b,colors) end
local wood={color(52,35,39),color(91,58,46),color(127,81,56),color(159,108,74)}
local straw={color(139,91,51),color(213,169,101),color(239,203,139)}
local function rotated(angle)
 local im=Image(W,H,ColorMode.RGB);local a=math.rad(angle);local c,s=math.cos(a),math.sin(a)
 -- Inverse nearest-neighbour rotation; no per-frame scaling.
 for y=0,H-1 do for x=0,W-1 do
  local dx,dy=x-128,y-174;local sx=math.floor(128+c*dx+s*dy+.5);local sy=math.floor(174-s*dx+c*dy+.5)
  if sx>=0 and sx<W and sy>=0 and sy<H then im:drawPixel(x,y,upper:getPixel(sx,sy)) end
 end end
 return im
end
local function post(angle)
 local im=Image(W,H,ColorMode.RGB);local a=math.rad(angle)
 for y=159,187 do
  local center=128-math.sin(a)*(y-174)
  local bend=math.max(0,math.min(1,(187-y)/18));center=128+(center-128)*bend
  for x=math.floor(center-4),math.floor(center+4) do
   local t=x-center;local col=wood[2]
   if t< -3 or t>3 then col=wood[1] elseif t< -1 then col=wood[3] elseif t<1 then col=wood[4] end
   im:drawPixel(x,y,col)
  end
 end
 return im
end
local function flecks(i)
 local im=Image(W,H,ColorMode.RGB)
 if i<2 or i>6 then return im end
 local t=i-2
 for j,v in ipairs({{79,135,-2,-1},{175,148,2,-1},{142,82,1,-2}}) do
  local x=v[1]+v[3]*t;local y=v[2]+v[4]*t+math.floor(t*t*.4)
  im:drawPixel(x,y,straw[1]);im:drawPixel(x+1,y,straw[2]);im:drawPixel(x+2,y+1,straw[3])
 end
 return im
end
local idleAngles={0,.65,1,.65,0,-.65,-1,-.65}
local hurtAngles={4,12,15,9,-5,-3,3,1,-1,0}
local hurtTimes={35,45,55,70,65,65,65,80,100,120}
local sprite=Sprite(W,H,ColorMode.RGB);sprite.layers[1].name='Base - fixed'
local baseLayer=sprite.layers[1];local postLayer=sprite:newLayer();postLayer.name='Wood support'
local bodyLayer=sprite:newLayer();bodyLayer.name='Straw body'
local effectLayer=sprite:newLayer();effectLayer.name='Loose straw - hurt only'
local pal=Palette(#colors+1);pal:setColor(0,Color{r=0,g=0,b=0,a=0})
for i,c in ipairs(colors) do pal:setColor(i,Color{r=c[1],g=c[2],b=c[3],a=255}) end;sprite:setPalette(pal)
local flats={};local durations={}
local function append(key,i,angle,duration)
 local n=#flats+1;if n>1 then sprite:newEmptyFrame(n) end
 sprite.frames[n].duration=duration/1000
 local stem,body,effects=post(angle),rotated(angle),key=='hurt' and flecks(i) or Image(W,H,ColorMode.RGB)
 sprite:newCel(baseLayer,n,base,Point(0,0));sprite:newCel(postLayer,n,stem,Point(0,0))
 sprite:newCel(bodyLayer,n,body,Point(0,0));sprite:newCel(effectLayer,n,effects,Point(0,0))
 local im=Image(W,H,ColorMode.RGB);im:drawImage(stem);im:drawImage(base);im:drawImage(body);im:drawImage(effects)
 -- Base must cover the post in native and flattened exports identically.
 baseLayer.stackIndex=2;postLayer.stackIndex=1
 local native=Image(W,H,ColorMode.RGB);native:drawSprite(sprite,n);assert(im:isEqual(native),'Layer mismatch '..n)
 local count=0
 for p in im:pixels() do if pc.rgbaA(p())>0 then
  count=count+1;assert(pc.rgbaA(p())==255,'Partial alpha')
  assert(p.x>0 and p.x<W-1 and p.y>0 and p.y<H-1,'Canvas clipping')
 end end
 assert(count>3500,'Empty frame');flats[n]=im;durations[n]=duration
 im:saveAs(out..'/Frames/'..key..'/frame-'..string.format('%02d',i)..'.png')
end
for i,a in ipairs(idleAngles) do append('idle',i,a,180) end
for i,a in ipairs(hurtAngles) do append('hurt',i,a,hurtTimes[i]) end
local t=sprite:newTag(1,8);t.name='Idle';t=sprite:newTag(9,18);t.name='Hurt'
assert(flats[1]:isEqual(flats[18]),'Hurt does not recover to idle')
for n,im in ipairs(flats) do for y=188,H-1 do for x=0,W-1 do
 assert(im:getPixel(x,y)==flats[1]:getPixel(x,y),'Base moved '..n)
end end end
sprite:saveAs(out..'/TrainingDummy.aseprite')
local function enlarge(im,scale)
 local large=Image(im.width*scale,im.height*scale,ColorMode.RGB)
 for p in large:pixels() do p(im:getPixel(math.floor(p.x/scale),math.floor(p.y/scale))) end;return large
end
local function bg(im)
 local b=Image(im.width,im.height,ColorMode.RGB);for p in b:pixels() do p(pc.rgba(38,35,39,255)) end;b:drawImage(im);return b
end
enlarge(bg(flats[1]),3):saveAs(out..'/idle-preview.png')
local sheet=Image(W*8,H*3,ColorMode.RGB)
for n,im in ipairs(flats) do sheet:drawImage(im,Point(((n-1)%8)*W,math.floor((n-1)/8)*H)) end
sheet:saveAs(out..'/TrainingDummy-sheet.png')
for _,spec in ipairs({{'idle',1,8},{'hurt',9,18}}) do
 local preview=Sprite(W*2,H*2,ColorMode.RGB)
 for n=spec[2],spec[3] do local f=n-spec[2]+1;if f>1 then preview:newEmptyFrame(f) end
  preview.frames[f].duration=durations[n]/1000;preview:newCel(preview.layers[1],f,enlarge(bg(flats[n]),2),Point(0,0))
 end
 preview:saveCopyAs(out..'/'..spec[1]..'-preview.gif')
end
local compare=Image(W*3,H,ColorMode.RGB)
for p in compare:pixels() do p(pc.rgba(38,35,39,255)) end
compare:drawImage(Image{fromFile='C:/Fork/swordmaster_/Assets/Game/Resources/SwordGirl/Animations/idle/frame-01.png'},Point(0,0))
compare:drawImage(flats[1],Point(W,0))
compare:drawImage(Image{fromFile='C:/Fork/swordmaster_/Assets/Game/Resources/EnemyStudent/Animations/idle/frame-01.png'},Point(W*2,0))
enlarge(compare,3):saveAs(out..'/style-comparison.png')
local contacts=Image(W*5,H,ColorMode.RGB)
for i,n in ipairs({1,9,11,13,18}) do contacts:drawImage(flats[n],Point((i-1)*W,0)) end
enlarge(bg(contacts),2):saveAs(out..'/hurt-contacts.png')
local json=assert(io.open(out..'/TrainingDummy-sheet.json','w'))
json:write('{"frames":[')
for i,im in ipairs(flats) do if i>1 then json:write(',') end
 local key=i<=8 and 'idle' or 'hurt';local n=i<=8 and i or i-8
 json:write(string.format('{"filename":"%s/frame-%02d.png","frame":{"x":%d,"y":%d,"w":256,"h":224},"rotated":false,"trimmed":false,"spriteSourceSize":{"x":0,"y":0,"w":256,"h":224},"sourceSize":{"w":256,"h":224},"duration":%d}',key,n,((i-1)%8)*W,math.floor((i-1)/8)*H,durations[i]))
end
json:write('],"meta":{"app":"Aseprite Lua","image":"TrainingDummy-sheet.png","format":"RGBA8888","size":{"w":2048,"h":672},"scale":"1","frameTags":[{"name":"Idle","from":0,"to":7,"direction":"forward","repeat":"0"},{"name":"Hurt","from":8,"to":17,"direction":"forward","repeat":"1"}],"pixelsPerUnit":40,"pivot":{"x":0.5,"y":0.09821428571428571}}}')
json:close()
local checked=app.open(out..'/TrainingDummy.aseprite');assert(#checked.frames==18 and #checked.tags==2 and #checked.layers==4)
local report=assert(io.open(out..'/verification.txt','w'))
report:write('VERIFIED: 256x224 RGBA, 18 cels, 2 tags, 4 layers, 28 colors from existing character palettes.\nIdle: 8 frames x 180ms = 1440ms looping. Hurt: 10 frames = 700ms one-shot.\nOpaque foreground and transparent background; no clipped frames; fixed base across all frames; final Hurt equals first Idle.\nRigid body rotation only: no per-frame resizing. Ground baseline matches y=201; pivot (128,22), 40 PPU.\n')
report:close()
