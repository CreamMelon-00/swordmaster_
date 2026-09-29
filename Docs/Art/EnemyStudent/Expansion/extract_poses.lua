local out='C:/Users/user/Documents/SwordMasterStory/Aseprite_Character/Enemy_Expansion'
local pc=app.pixelColor;local W,H=256,224
local native=app.open('C:/Users/user/Documents/SwordMasterStory/Aseprite_Character/Enemy_Rework/Enemy_Uniform_PixelMatched.aseprite')
local ready=Image{fromFile='C:/Users/user/Documents/SwordMasterStory/Aseprite_Character/Enemy_Rework/enemy-uniform-pixel-matched.png'}
local palette={}
for i=1,#native.palettes[1]-1 do local c=native.palettes[1]:getColor(i);palette[#palette+1]={c.red,c.green,c.blue,pc.rgba(c.red,c.green,c.blue,255)} end
local cache={}
local function nearest(r,g,b)
 local key=math.floor(r)*65536+math.floor(g)*256+math.floor(b);if cache[key] then return cache[key] end
 local best,dist=palette[1][4],math.huge
 for _,c in ipairs(palette) do local d=2*(r-c[1])^2+3*(g-c[2])^2+2*(b-c[3])^2;if d<dist then best,dist=c[4],d end end
 cache[key]=best;return best
end

local specs=dofile(out..'/pose_specs.lua')
local axes=dofile(out..'/weapon_axes.lua')
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
 assert(#cc==#specs[key],key..' unexpected components '..#cc)
 table.sort(cc,function(a,b) local ra=a.y1>sh*.7 and 1 or 0;local rb=b.y1>sh*.7 and 1 or 0;return ra==rb and a.x0<b.x0 or ra<rb end)
 local poses={}
 for i,c in ipairs(cc) do
  local s=specs[key][i];local hx,hy,hw,hh,gx,gy,tx,ty,cx,top=table.unpack(s)
  local scale=52/hw;local neck=top+46
  local axis=axes[key][i]
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
     local gripX,gripY=gx-axis[1],gy-axis[2]
     local gripL=math.sqrt(gripX*gripX+gripY*gripY);gripX=gripX/gripL;gripY=gripY/gripL
     local ga=(sx-axis[1])*gripX+(sy-axis[2])*gripY
     local gb=math.abs((sx-axis[1])*gripY-(sy-axis[2])*gripX)
     local oldBlade=along>-5 and along<sl+12 and across<28
     local oldGrip=ga>-26 and ga<gripL+30 and gb<30
     local oldGuard=(sx-gx)^2+(sy-gy)^2<55^2
     local oldPommel=(sx-axis[1])^2+(sy-axis[2])^2<27^2
     local blade=(oldBlade or oldGrip or oldGuard or oldPommel) and rr<195 and bb>gg*1.04 and bb>rr*.88
     if not blade then r=r+rr;g=g+gg;b=b+bb;n=n+1 end
    end
   end end
   if n>=2 then im:drawPixel(x,y,nearest(r/n,g/n,b/n)) end
  end end
  -- Whole rigid weapon: one axis shared by pommel, grip, guard and blade.
  local sword=Image(W,H,ColorMode.RGB);local ngX,ngY=mapX(axis[5]),mapY(axis[6])
  local pX,pY=mapX(axis[3]),mapY(axis[4])
  local dx,dy=ngX-pX,ngY-pY;local gripLength=math.sqrt(dx*dx+dy*dy);dx=dx/gripLength;dy=dy/gripLength
  local length=84
  local tipX,tipY=ngX+dx*length,ngY+dy*length
  assert(tipX>1 and tipX<W-2 and tipY>1 and tipY<H-2,key..'/'..i..' weapon tip outside canvas '..tipX..','..tipY)
  for y=0,H-1 do for x=0,W-1 do
   local a=(x-ngX)*dx+(y-ngY)*dy;local b=-(x-ngX)*dy+(y-ngY)*dx
   local half=a>length-10 and math.max(0,(length-a)*.36) or 3.4
   local r,g,bl
   if a>=1 and a<=length and math.abs(b)<=half then
    r,g,bl=40,32,47
    if math.abs(b)<half-1 then if b<0 then r,g,bl=128,101,128 else r,g,bl=77,56,81 end end
   elseif a>=-gripLength and a<0 and math.abs(b)<=2 then
    r,g,bl=40,32,47;if math.abs(b)<1.2 then r,g,bl=100,75,102 end
   end
   if math.abs(a)<1.7 and math.abs(b)<8 then r,g,bl=40,32,47;if math.abs(a)<.8 and math.abs(b)<6.5 then r,g,bl=100,75,102 end end
   local dist=(a+gripLength)^2+b*b
   if dist<9 then r,g,bl=40,32,47;if dist<4 then r,g,bl=100,75,102 end end
   if r then sword:drawPixel(x,y,nearest(r,g,bl)) end
  end end
  -- Keep the rigid weapon visible in front of the uniform, then restore the gripping fingers/cuffs.
  local character=Image(im)
  im:drawImage(sword)
  for y=0,H-1 do for x=0,W-1 do
   local a=(x-ngX)*dx+(y-ngY)*dy;local b=-(x-ngX)*dy+(y-ngY)*dx
   if a>=-gripLength-4 and a<=4 and math.abs(b)<7 then
    local p=character:getPixel(x,y)
    if pc.rgbaA(p)>0 and pc.rgbaR(p)>145 and pc.rgbaG(p)>90 and pc.rgbaB(p)>60 then im:drawPixel(x,y,p) end
   end
  end end
  -- Discard detached remnants of the source guard/pommel after the weapon redraw.
  local todo,biggest={},{}
  for p in im:pixels() do if pc.rgbaA(p())>0 then todo[p.y*W+p.x]=true end end
  while next(todo) do
   local first=next(todo);local q={first};todo[first]=nil;local at=1
   while at<=#q do local n=q[at];at=at+1;local yy=math.floor(n/W);local xx=n-yy*W
    for oy=-1,1 do for ox=-1,1 do local nx,ny=xx+ox,yy+oy;local k=ny*W+nx
     if nx>=0 and nx<W and ny>=0 and ny<H and todo[k] then todo[k]=nil;q[#q+1]=k end
    end end
   end
   if #q>#biggest then biggest=q end
  end
  local clean=Image(W,H,ColorMode.RGB)
  for _,n in ipairs(biggest) do local y=math.floor(n/W);local x=n-y*W;clean:drawPixel(x,y,im:getPixel(x,y)) end
  im=clean;poses[i]=im
  print(key..' pose '..i..' source '..c.x0..','..c.y0..'-'..c.x1..','..c.y1..' guard '..string.format('%.1f,%.1f',ngX,ngY))
 end
 return poses
end

return extract
