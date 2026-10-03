local root=assert(app.params.root):gsub('\\','/')
local dir=root..'/Docs/Art/Movement/Revision2/SwordGirl'
local pc=app.pixelColor
local idle=Image{fromFile=root..'/Assets/Game/Resources/SwordGirl/Animations/idle/frame-01.png'}
local palette,seen={},{}
for p in idle:pixels() do
  local c=p()
  if pc.rgbaA(c)>0 and not seen[c] then
    seen[c]=true
    table.insert(palette,{c=c,r=pc.rgbaR(c),g=pc.rgbaG(c),b=pc.rgbaB(c)})
  end
end
assert(#palette==64)
local cache={}
local function nearest(r,g,b)
  local key=math.floor(r/4)*65536+math.floor(g/4)*256+math.floor(b/4)
  if cache[key] then return cache[key] end
  local best,dist=nil,math.huge
  for _,v in ipairs(palette) do
    local dr,dg,db=r-v.r,g-v.g,b-v.b
    local d=2*dr*dr+3*dg*dg+db*db
    if d<dist then best,dist=v.c,d end
  end
  cache[key]=best
  return best
end
local function analyze(im)
  local l,t,r,b=im.width,im.height,-1,-1
  for p in im:pixels() do
    if pc.rgbaA(p())>=160 then
      l=math.min(l,p.x);r=math.max(r,p.x)
      t=math.min(t,p.y);b=math.max(b,p.y)
    end
  end
  local hl,hr=im.width,-1
  for y=t,math.min(t+300,b) do for x=l,math.min(800,r) do
    if pc.rgbaA(im:getPixel(x,y))>=160 then
      hl=math.min(hl,x);hr=math.max(hr,x)
    end
  end end
  assert(r>=l and hr>=hl)
  return l,t,r,b,(hl+hr)/2
end
local function normalize(name)
  local im=Image{fromFile=dir..'/concept-'..name..'.png'}
  local l,t,r,b,headX=analyze(im)
  local xs=.17
  -- Subtle coherent head/pelvis bob without splicing the sprite: down sinks
  -- one native pixel; swing rises one. Support soles remain at y=202.
  local top=64
  if name:find('down') then top=65 elseif name:find('swing') then top=63 end
  local ys=(202-top)/(b-t)
  local out=Image(256,224,ColorMode.RGB)
  for y=0,223 do for x=0,255 do
    local srcx=headX+(x-96)/xs
    local srcy=b-(202-y)/ys
    local rr,gg,bb,aa=0,0,0,0
    for jy=-1,1 do for jx=-1,1 do
      local px=math.floor(srcx+jx/(3*xs)+.5)
      local py=math.floor(srcy+jy/(3*ys)+.5)
      if px>=0 and px<im.width and py>=0 and py<im.height then
        local c=im:getPixel(px,py)
        local a=pc.rgbaA(c)
        rr=rr+a*pc.rgbaR(c);gg=gg+a*pc.rgbaG(c);bb=bb+a*pc.rgbaB(c);aa=aa+a
      end
    end end
    if aa>=9*128 then out:drawPixel(x,y,nearest(rr/aa,gg/aa,bb/aa)) end
  end end
  out:saveAs(dir..'/key-'..name..'.png')
  print(name,'source bounds',l,t,r,b,'head center',headX,'scale',xs,ys)
  return out
end
local names={'contact-a','down-a','pass-a','swing-a','contact-b','down-b','pass-b','swing-b'}
local frames={}
for _,name in ipairs(names) do table.insert(frames,normalize(name)) end
local preview=Image(4*768,2*672,ColorMode.RGB)
for p in preview:pixels() do p(pc.rgba(53,50,56,255)) end
for i,im in ipairs(frames) do
  for p in im:pixels() do
    local c=p()
    if pc.rgbaA(c)>0 then
      local ox=((i-1)%4)*768+p.x*3
      local oy=math.floor((i-1)/4)*672+p.y*3
      for dy=0,2 do for dx=0,2 do preview:drawPixel(ox+dx,oy+dy,c) end end
    end
  end
end
preview:saveAs(dir..'/key-poses-preview.png')
