local root='C:/Users/user/Documents/SwordMasterStory/Aseprite_Character'
local out=root..'/Blunt_Expansion'
local sourceNative='C:/Fork/swordmaster_/Docs/Art/SwordGirl/PierceExpansion/SwordGirl_Slash3_Pierce3.aseprite'
local W,H=256,224;local ground,footX=202,56;local pc=app.pixelColor
local sprite=app.open(sourceNative);assert(#sprite.frames==96)
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
local poses={extract('blunt-2',.28),extract('blunt-3',.28)}
local cleanup=dofile(out..'/cleanup_motion.lua')
local timing={140,100,120,60,70,90,70,100,90,90,130,200}
local function oldFrame(n)
 local im=Image(W,H,ColorMode.RGB);local cel=character:cel(n);im:drawImage(cel.image,cel.position);return im
end
local ready=oldFrame(1);local verticalGuard=oldFrame(41)
for variant,p in ipairs(poses) do
 local key='blunt-'..(variant+1)
 local indices=variant==1 and {1,1,2,3,4,4,3,5,5,6,1,1} or {1,1,2,3,4,4,5,5,5,6,1,1}
 local tops=variant==1 and {73,75,78,75,72,72,74,76,74,73,73,73} or {73,73,72,82,90,92,88,81,76,74,73,73}
 local centers=variant==1 and {99,98,97,101,105,106,102,100,99,99,99,99} or {99,99,98,104,108,109,106,103,100,99,99,99}
 local stances=variant==1 and {89,89,91,92,94,94,92,90,89,89,89,89} or {89,89,91,96,100,100,98,94,91,89,89,89}
 local frames={ready}
 for i=2,10 do
  frames[i]=cleanup.clean(i==9 and verticalGuard or p[indices[i]],{label=key..'/'..i,top=tops[i],headX=centers[i],stance=stances[i]})
 end
 frames[11]=ready;frames[12]=ready
 local first=#sprite.frames+1
 for i,im in ipairs(frames) do
  for px in im:pixels() do local c=px()
   if pc.rgbaA(c)>0 then
    assert(px.x>0 and px.x<W-1 and px.y>0 and px.y<H-1,key..' frame '..i..' clipped')
    if i>1 and i<11 then px(nearest(pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c))) end
   end
  end
  local f=#sprite.frames+1;sprite:newEmptyFrame(f);sprite.frames[f].duration=timing[i]/1000
  sprite:newCel(character,f,im,Point(0,0))
  im:saveAs(out..'/Resources/'..key..'/frame-'..string.format('%02d',i)..'.png')
 end
 local tag=sprite:newTag(first,#sprite.frames);tag.name='Blunt'..(variant+1);ranges[tag.name]={first,#sprite.frames}
 local sheet=Image(W*6,H*2,ColorMode.RGB)
 for it in sheet:pixels() do it(pc.rgba(37,34,39,255)) end
 for i,im in ipairs(frames) do sheet:drawImage(im,Point((i-1)%6*W,math.floor((i-1)/6)*H)) end
 sheet:saveAs(out..'/'..key..'-frames.png')
end
for _,tag in ipairs(sprite.tags) do tag.fromFrame=ranges[tag.name][1];tag.toFrame=ranges[tag.name][2] end
local old=app.open(sourceNative)
for i=1,96 do local a=Image(W,H,ColorMode.RGB);local b=Image(W,H,ColorMode.RGB)
 a:drawSprite(sprite,i);b:drawSprite(old,i);assert(a:isEqual(b),'Previous frame changed: '..i)
end
assert(#sprite.frames==120 and #sprite.tags==14)
sprite:saveAs(out..'/SwordGirl_AllAttacks3.aseprite')
local manifest={clips={{key='idle',impactFrame=0,durationsMs={180,140,160,140,160,140,160,180}}}}
for _,key in ipairs({'slash','pierce','blunt','slash-2','slash-3','pierce-2','pierce-3','blunt-2','blunt-3'}) do manifest.clips[#manifest.clips+1]={key=key,impactFrame=5,durationsMs=timing} end
local file=assert(io.open(out..'/Resources/timing.json','w'));file:write(json.encode(manifest));file:close()
local preview=Sprite(W*3,H,ColorMode.RGB);preview:setPalette(sprite.palettes[1]);preview.layers[1].name='Three blunt attacks'
for i=1,12 do
 if i>1 then preview:newEmptyFrame(i) end;preview.frames[i].duration=timing[i]/1000
 local im=Image(W*3,H,ColorMode.RGB)
 for px in im:pixels() do px(pc.rgba(37,34,39,255)) end
 for column,start in ipairs({33,97,109}) do local cel=character:cel(start+i-1);im:drawImage(cel.image,Point((column-1)*W+cel.position.x,cel.position.y)) end
 preview:newCel(preview.layers[1],i,im,Point(0,0))
 if i==5 then local contact=Image(im);contact:resize(W*6,H*2);contact:saveAs(out..'/blunt-three-contacts.png') end
end
preview:saveAs(out..'/blunt-three-preview.aseprite')
print('VERIFIED: 120 frames, 14 tags; previous 96 frames unchanged; 24 new character PNGs; contact frame 5.')
