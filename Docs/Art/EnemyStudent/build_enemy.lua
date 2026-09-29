local out='C:/Users/user/Documents/SwordMasterStory/Aseprite_Character/Enemy_Rework'
local pc=app.pixelColor;local W,H=256,224
local native=app.open(out..'/Enemy_Uniform_PixelMatched.aseprite')
local ready=Image{fromFile=out..'/enemy-uniform-pixel-matched.png'}
local palette={}
for i=1,#native.palettes[1]-1 do local c=native.palettes[1]:getColor(i);palette[#palette+1]={c.red,c.green,c.blue,pc.rgba(c.red,c.green,c.blue,255)} end
local cache={}
local function nearest(r,g,b)
 local key=math.floor(r)*65536+math.floor(g)*256+math.floor(b);if cache[key] then return cache[key] end
 local best,dist=palette[1][4],math.huge
 for _,c in ipairs(palette) do local d=2*(r-c[1])^2+3*(g-c[2])^2+2*(b-c[3])^2;if d<dist then best,dist=c[4],d end end
 cache[key]=best;return best
end
-- Measured head boxes and crossguard/blade endpoints in the source sheets.
-- Source weapon pixels are replaced with one rigid, fixed-length blade, preserving hand/body occlusion.
local specs={
 slash={
  {155,136,174,141,318,178,450,28,122,73},
  {666,144,174,130,759,105,957,20,122,72},
  {1201,153,178,141,1194,344,970,407,115,77},
  {186,622,179,133,262,851,54,969,115,81},
  {671,631,174,134,867,846,1085,973,120,76},
  {1236,634,168,126,1292,738,1266,537,122,73}},
 pierce={
  {164,85,178,150,357,337,584,484,122,73},
  {750,85,179,150,810,278,574,278,123,74},
  {1282,85,178,150,1261,269,1042,266,118,76},
  {239,571,180,144,222,725,12,725,115,86},
  {783,574,177,145,773,730,561,728,119,81},
  {1190,574,180,145,1372,807,1524,940,122,75}},
 blunt={
  {165,116,172,139,349,361,548,489,122,73},
  {645,115,175,141,777,238,967,63,122,75},
  {1123,116,176,139,1170,258,1470,98,117,77},
  {163,598,173,131,180,712,501,551,113,82},
  {645,602,175,136,777,722,967,557,118,77},
  {1127,605,175,134,1313,843,1512,969,122,74}}
}
local keys={}
local function extract(key)
 local source=Image{fromFile=out..'/'..key..'_source.png'};local sw,sh=source.width,source.height
 local mask,labels,cc={},{},{}
 for p in source:pixels() do if pc.rgbaA(p())>=230 then mask[p.y*sw+p.x]=true end end
 for y=0,sh-1 do for x=0,sw-1 do local z=y*sw+x
  if mask[z] then
   local q={z};mask[z]=nil;local qi=1;local c={x0=x,x1=x,y0=y,y1=y}
   while qi<=#q do local n=q[qi];qi=qi+1;local yy=math.floor(n/sw);local xx=n-yy*sw
    c.x0=math.min(c.x0,xx);c.x1=math.max(c.x1,xx);c.y0=math.min(c.y0,yy);c.y1=math.max(c.y1,yy)
    for dy=-1,1 do for dx=-1,1 do local nx,ny=xx+dx,yy+dy;local k=ny*sw+nx
     if nx>=0 and nx<sw and ny>=0 and ny<sh and mask[k] then mask[k]=nil;q[#q+1]=k end
    end end
   end
   if #q>10000 then c.id=#cc+1;for _,n in ipairs(q) do labels[n]=c.id end;cc[#cc+1]=c end
  end
 end end
 assert(#cc==6,key..' expected 6 components, got '..#cc)
 table.sort(cc,function(a,b) local ra=a.y1>sh*.7 and 1 or 0;local rb=b.y1>sh*.7 and 1 or 0;return ra==rb and a.x0<b.x0 or ra<rb end)
 local poses={}
 for i,c in ipairs(cc) do
  local s=specs[key][i];local hx,hy,hw,hh,gx,gy,tx,ty,cx,top=table.unpack(s)
  local scale=52/hw;local neck=top+46
  local function mapY(y) if y<hy+hh then return top+(y-hy)*46/hh else return neck+(y-hy-hh)*(201-neck)/(c.y1-hy-hh) end end
  local function mapX(x) return cx+(x-hx-hw/2)*scale end
  local function unY(y) if y<neck then return hy+(y-top)*hh/46 else return hy+hh+(y-neck)*(c.y1-hy-hh)/(201-neck) end end
  local ux,uy=tx-gx,ty-gy;local sl=math.sqrt(ux*ux+uy*uy);ux=ux/sl;uy=uy/sl
  local im=Image(W,H,ColorMode.RGB)
  for y=0,H-1 do for x=0,W-1 do local r,g,b,n=0,0,0,0
   for _,oy in ipairs({.25,.75}) do for _,ox in ipairs({.25,.75}) do
    local sx=math.floor(hx+hw/2+(x+ox-cx)/scale);local sy=math.floor(unY(y+oy))
    if sx>=0 and sx<sw and sy>=0 and sy<sh and labels[sy*sw+sx]==c.id then
     local p=source:getPixel(sx,sy);local rr,gg,bb=pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p)
     local along=(sx-gx)*ux+(sy-gy)*uy;local across=math.abs((sx-gx)*uy-(sy-gy)*ux)
     local blade=along>3 and along<sl+10 and across<25 and rr<195 and bb>gg*1.04
     if not blade then r=r+rr;g=g+gg;b=b+bb;n=n+1 end
    end
   end end
   if n>=2 then im:drawPixel(x,y,nearest(r/n,g/n,b/n)) end
  end end
  -- Sword on a separate layer beneath the gripping hands and head; one continuous axis.
  local sword=Image(W,H,ColorMode.RGB);local ngX,ngY=mapX(gx),mapY(gy)
  local dx,dy=mapX(tx)-ngX,mapY(ty)-ngY;local dl=math.sqrt(dx*dx+dy*dy);dx=dx/dl;dy=dy/dl
  local length=84
  for y=0,H-1 do for x=0,W-1 do
   local a=(x-ngX)*dx+(y-ngY)*dy;local b=-(x-ngX)*dy+(y-ngY)*dx
   local half=a>length-10 and math.max(0,(length-a)*.36) or 3.6
   if a>=1 and a<=length and math.abs(b)<=half then
    local r,g,bl=56,43,62
    if math.abs(b)<half-1 then if b<0 then r,g,bl=128,101,128 else r,g,bl=77,56,81 end end
    sword:drawPixel(x,y,nearest(r,g,bl))
   end
  end end
  sword:drawImage(im);im=sword
  poses[i]=im
  print(key..' pose '..i..' source '..c.x0..','..c.y0..'-'..c.x1..','..c.y1..' guard '..string.format('%.1f,%.1f',ngX,ngY))
 end
 return poses
end
for _,key in ipairs({'slash','pierce','blunt'}) do keys[key]=extract(key) end
local sprite=Sprite(W,H,ColorMode.RGB);sprite.layers[1].name='Enemy - pixel cels';sprite:setPalette(native.palettes[1])
local durations={140,100,120,60,70,90,70,100,90,90,130,200};local idleDur={180,140,160,140,160,140,160,180}
local sequences={};local tags={};local total=0
local function append(key,frames,times,tagName)
 local start=total+1
 for i,im in ipairs(frames) do
  total=total+1;if total>1 then sprite:newEmptyFrame(total) end
  sprite.frames[total].duration=times[i]/1000;sprite:newCel(sprite.layers[1],total,im,Point(0,0))
  local path=key=='poses' and '/poses/block.png' or '/'..key..'/frame-'..string.format('%02d',i)..'.png'
  im:saveAs(out..'/Resources'..path)
  local count=0
  for p in im:pixels() do if pc.rgbaA(p())>0 then count=count+1;assert(p.x>0 and p.x<W-1 and p.y>0 and p.y<H-1,key..' clipped frame '..i);assert(pc.rgbaA(p())==255) end end
  assert(count>2500,key..' empty frame')
 end
 tags[#tags+1]={tagName,start,total};sequences[key]=frames
end
local idle={}
for i,bob in ipairs({0,0,-1,-1,0,0,1,1}) do local im=Image(W,H,ColorMode.RGB)
 for y=0,H-1 do for x=0,W-1 do local sy=math.floor(y-bob*math.min(1,math.max(0,(201-y)/70))+.5)
  if sy>=0 and sy<H then im:drawPixel(x,y,ready:getPixel(x,sy)) end
 end end;idle[i]=im
end
append('idle',idle,idleDur,'Idle')
local orders={slash={0,1,2,2,3,3,4,4,5,5,0,0},pierce={0,2,2,3,4,4,5,5,6,6,0,0},blunt={0,2,2,3,4,4,3,5,5,6,0,0}}
for _,key in ipairs({'slash','pierce','blunt'}) do local frames={}
 for i,n in ipairs(orders[key]) do frames[i]=n==0 and ready or keys[key][n] end
 append(key,frames,durations,key:sub(1,1):upper()..key:sub(2))
end
append('poses',{keys.slash[6]},{200},'Block')
for _,t in ipairs(tags) do local tag=sprite:newTag(t[2],t[3]);tag.name=t[1] end
assert(total==45)
sprite:saveAs(out..'/Enemy_Uniform_Animations.aseprite')
local function scaled(im,n) local r=Image(im.width*n,im.height*n,ColorMode.RGB);for p in r:pixels() do p(im:getPixel(math.floor(p.x/n),math.floor(p.y/n))) end;return r end
local board=Image(W*3,H*2,ColorMode.RGB)
for p in board:pixels() do p(pc.rgba(38,35,39,255)) end
for j,key in ipairs({'slash','pierce','blunt'}) do board:drawImage(sequences[key][3],Point((j-1)*W,0));board:drawImage(sequences[key][5],Point((j-1)*W,H)) end
scaled(board,2):saveAs(out..'/enemy-attacks-contacts.png')
local preview=Sprite(W*3,H,ColorMode.RGB)
for i=1,12 do if i>1 then preview:newEmptyFrame(i) end;preview.frames[i].duration=durations[i]/1000
 local im=Image(W*3,H,ColorMode.RGB);for p in im:pixels() do p(pc.rgba(38,35,39,255)) end
 for j,key in ipairs({'slash','pierce','blunt'}) do im:drawImage(sequences[key][i],Point((j-1)*W,0)) end
 preview:newCel(preview.layers[1],i,im,Point(0,0))
end
preview:saveAs(out..'/enemy-attacks-preview.aseprite')
preview:saveCopyAs(out..'/enemy-attacks-preview.gif')
for key,frames in pairs(keys) do local sheet=Image(W*3,H*2,ColorMode.RGB);for p in sheet:pixels() do p(pc.rgba(38,35,39,255)) end
 for i,im in ipairs(frames) do sheet:drawImage(im,Point((i-1)%3*W,math.floor((i-1)/3)*H)) end
 scaled(sheet,2):saveAs(out..'/'..key..'-keys.png')
end
local check=app.open(out..'/Enemy_Uniform_Animations.aseprite');assert(#check.frames==45 and #check.tags==5)
print('VERIFIED 45 frames, 5 tags, 256x224, opaque alpha, no clipping; approved idle cel retained.')
