local root='C:/Users/user/Documents/SwordMasterStory/Aseprite_Character'
local out=root..'/Slash_Expansion'
local sourceNative='C:/Fork/swordmaster_/Docs/Art/SwordGirl/SwordGirl_Defense_Hurt.aseprite'
local W,H=256,224;local ground,footX=202,56;local pc=app.pixelColor
local sprite=app.open(sourceNative);assert(#sprite.frames==48)
local character,effects
for _,layer in ipairs(sprite.layers) do
 if layer.name=='Character - pixel cels' then character=layer else effects=layer end
end
assert(character and effects)
local ranges={}
for _,t in ipairs(sprite.tags) do ranges[t.name]={t.fromFrame.frameNumber,t.toFrame.frameNumber} end
local colors={}
for i=1,#sprite.palettes[1]-1 do local c=sprite.palettes[1]:getColor(i)
 if c.alpha>0 then colors[#colors+1]={c.red,c.green,c.blue,pc.rgba(c.red,c.green,c.blue,255)} end
end
local cache={}
local function nearest(r,g,b)
 local key=r*65536+g*256+b;if cache[key] then return cache[key] end
 local chosen,dist=nil,math.huge
 for _,c in ipairs(colors) do local d=2*(r-c[1])^2+3*(g-c[2])^2+2*(b-c[3])^2
  if d<dist then chosen,dist=c[4],d end
 end
 cache[key]=chosen;return chosen
end
local function extract(name,scale)
 local src=Image{fromFile=out..'/'..name..'_source.png'}
 local w,h=src.width,src.height;local mask,labels,cc={},{},{}
 for it in src:pixels() do if pc.rgbaA(it())>=180 then mask[it.y*w+it.x]=true end end
 for y=0,h-1 do for x=0,w-1 do local k=y*w+x
  if mask[k] then
   local q={k};local qi=1;mask[k]=nil;local c={x0=x,x1=x,y0=y,y1=y}
   while qi<=#q do
    local n=q[qi];qi=qi+1;local yy=math.floor(n/w);local xx=n-yy*w
    c.x0=math.min(c.x0,xx);c.x1=math.max(c.x1,xx);c.y0=math.min(c.y0,yy);c.y1=math.max(c.y1,yy)
    for dy=-1,1 do for dx=-1,1 do local nx,ny=xx+dx,yy+dy
     if nx>=0 and nx<w and ny>=0 and ny<h then local z=ny*w+nx
      if mask[z] then mask[z]=nil;q[#q+1]=z end
     end
    end end
   end
   if #q>10000 then
    c.id=#cc+1;c.foot=w;c.pixels=#q
    for _,z in ipairs(q) do labels[z]=c.id;local yy=math.floor(z/w)
     if yy>=c.y1-8 then c.foot=math.min(c.foot,z-yy*w) end
    end
    cc[#cc+1]=c
   end
  end
 end end
 assert(#cc==6,name..': expected six isolated key poses; got '..#cc)
 table.sort(cc,function(a,b)
  local ra=a.y1>h*.7 and 1 or 0;local rb=b.y1>h*.7 and 1 or 0
  return ra==rb and a.x0<b.x0 or ra<rb
 end)
 local poses={}
 for i,c in ipairs(cc) do
  local im=Image(W,H,ColorMode.RGB)
  for y=0,H-1 do for x=0,W-1 do local r,g,b,n=0,0,0,0
   for _,oy in ipairs({.25,.75}) do for _,ox in ipairs({.25,.75}) do
    local sx=math.floor(c.foot+(x+ox-footX)/scale);local sy=math.floor(c.y1+(y+oy-ground)/scale)
    if sx>=0 and sx<w and sy>=0 and sy<h and labels[sy*w+sx]==c.id then
     local p=src:getPixel(sx,sy);r=r+pc.rgbaR(p);g=g+pc.rgbaG(p);b=b+pc.rgbaB(p);n=n+1
    end
   end end
   if n>=2 then im:drawPixel(x,y,nearest(math.floor(r/n+.5),math.floor(g/n+.5),math.floor(b/n+.5))) end
  end end
  local count=0
  for it in im:pixels() do if pc.rgbaA(it())>0 then count=count+1
   assert(it.x>0 and it.x<W-1 and it.y>0 and it.y<H-1,name..' pose '..i..' clipped')
  end end
  assert(count>2800,name..' pose '..i..' too few pixels')
  poses[i]=im;print(name..' pose '..i..': '..count..' pixels, source bounds '..c.x0..','..c.y0..'-'..c.x1..','..c.y1)
 end
 return poses
end
local p2=extract('slash-2',.32);local p3=extract('slash-3-revised',.28)
local inbetweens={extract('slash-2-inbetweens',.32),extract('slash-3-inbetweens',.30)}
local ready=Image(W,H,ColorMode.RGB);ready:drawImage(character:cel(1).image,character:cel(1).position)
local timing={140,100,120,60,70,90,70,100,90,90,130,200}
local cleanup=dofile(out..'/cleanup_motion.lua')
local function fx(kind,frame)
 local im=Image(W,H,ColorMode.RGB)
 if frame<4 or frame>7 then return im end
 local gold=pc.rgba(246,203,125,255);local light=pc.rgba(255,246,215,255)
 local function arc(cx,cy,rx,ry,a,b,col)
  for angle=a,b,.4 do local rad=math.rad(angle)
   local x,y=math.floor(cx+math.cos(rad)*rx+.5),math.floor(cy+math.sin(rad)*ry+.5)
   if x>0 and x<W-1 and y>0 and y<H-1 then im:drawPixel(x,y,col) end
  end
 end
 if kind==2 then
  for i=0,(frame==7 and 0 or 3) do arc(137,137,74-i,68-i,frame==4 and -15 or -70,32,gold) end
  if frame<7 then arc(137,137,76,70,-64,10,light) end
 else
  for i=0,(frame==7 and 0 or 3) do arc(133,135,90-i,29-i,-38,48,gold) end
  if frame<7 then arc(133,135,92,31,-30,38,light) end
 end
 return im
end
for variant,poses in ipairs({p2,p3}) do
 local key='slash-'..(variant+1);local tagName='Slash'..(variant+1)
 local mid=inbetweens[variant]
 local frames
 if variant==1 then
  frames={ready,mid[1],poses[2],poses[3],poses[4],mid[2],poses[5],mid[3],mid[4],mid[5],mid[6],ready}
  local angles={-58,135,135,0,-48,-58,-60,-59,-58,-58,-58,-58}
  local tops={73,76,79,76,73,72,72,73,73,73,73,73}
  local centers={99,98,98,102,103,103,102,101,100,99,99,99}
  for i=2,11 do frames[i]=cleanup.clean(frames[i],{label=key..'/'..i,top=tops[i],headX=centers[i],angle=angles[i]}) end
 else
  frames={ready,mid[1],poses[1],poses[2],poses[3],poses[4],poses[5],poses[6],mid[4],mid[5],mid[6],ready}
  local angles={-58,177,180,32,0,4,20,-46,-51,-55,-58,-58}
  local lengths={73,60,60,39,73,35,70,73,73,73,73,73}
  local centers={99,98,98,99,102,103,102,100,99,99,99,99}
  for i=2,11 do frames[i]=cleanup.clean(frames[i],{label=key..'/'..i,top=73+(i==2 and 1 or 0),headX=centers[i],angle=angles[i],length=lengths[i],foreshortened=i==4}) end
 end
 local unique={}
 for _,image in ipairs(frames) do
  local seen=false;for _,other in ipairs(unique) do if image:isEqual(other) then seen=true;break end end
  if not seen then unique[#unique+1]=image end
 end
 assert(#unique==11,key..' must have eleven distinct drawings')
 local first=#sprite.frames+1
 for i,im in ipairs(frames) do
  if i>1 and i<12 then
   for px in im:pixels() do local c=px()
    if pc.rgbaA(c)>0 then
     assert(px.x>0 and px.x<W-1 and px.y>0 and px.y<H-1,key..' frame '..i..' clips canvas')
     px(nearest(pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c)))
    end
   end
  end
  local f=#sprite.frames+1;sprite:newEmptyFrame(f);sprite.frames[f].duration=timing[i]/1000
  sprite:newCel(character,f,im,Point(0,0));local effect=fx(variant+1,i)
  if not effect:isEmpty() then sprite:newCel(effects,f,effect,Point(0,0)) end
  im:saveAs(out..'/Resources/'..key..'/frame-'..string.format('%02d',i)..'.png')
 end
 local t=sprite:newTag(first,#sprite.frames);t.name=tagName;ranges[tagName]={first,#sprite.frames}
end
for _,tag in ipairs(sprite.tags) do tag.fromFrame=ranges[tag.name][1];tag.toFrame=ranges[tag.name][2] end
sprite:saveAs(out..'/SwordGirl_Slash3.aseprite')
-- Encode plain Lua values: decoded Aseprite JSON userdata must never be copied into a new table.
local manifest={clips={{key='idle',impactFrame=0,durationsMs={180,140,160,140,160,140,160,180}}}}
for _,key in ipairs({'slash','pierce','blunt','slash-2','slash-3'}) do manifest.clips[#manifest.clips+1]={key=key,impactFrame=5,durationsMs=timing} end
local f=assert(io.open(out..'/Resources/timing.json','w'));f:write(json.encode(manifest));f:close()
local old=app.open(sourceNative)
for i=1,48 do local a=Image(W,H,ColorMode.RGB);local b=Image(W,H,ColorMode.RGB)
 a:drawSprite(sprite,i);b:drawSprite(old,i);assert(a:isEqual(b),'Previous frame changed: '..i)
end
assert(#sprite.frames==72 and #sprite.tags==10)
local strip=Image(W*3,H,ColorMode.RGB)
for it in strip:pixels() do it(pc.rgba(37,34,39,255)) end
for col,frame in ipairs({13,53,65}) do local im=Image(W,H,ColorMode.RGB)
 im:drawImage(character:cel(frame).image,character:cel(frame).position);strip:drawImage(im,Point((col-1)*W,0))
end
strip:resize(W*6,H*2);strip:saveAs(out..'/slash-three-contacts.png')
local preview=Sprite(W*3,H,ColorMode.RGB);preview:setPalette(sprite.palettes[1]);preview.layers[1].name='Three slash comparison'
for i=1,12 do
 if i>1 then preview:newEmptyFrame(i) end;preview.frames[i].duration=timing[i]/1000
 local im=Image(W*3,H,ColorMode.RGB)
 for it in im:pixels() do it(pc.rgba(37,34,39,255)) end
 for col,start in ipairs({9,49,61}) do
  local frame=Image(W,H,ColorMode.RGB);local cel=character:cel(start+i-1)
  frame:drawImage(cel.image,cel.position);im:drawImage(frame,Point((col-1)*W,0))
 end
 preview:newCel(preview.layers[1],i,im,Point(0,0))
end
preview:saveAs(out..'/slash-three-preview.aseprite')
print('VERIFIED: 72 frames, 10 tags; previous 48 frames unchanged; 24 new character PNGs; contact on frame 5.')
