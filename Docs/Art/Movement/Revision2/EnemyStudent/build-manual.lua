-- Hand-articulated eight-frame low-stride walk. Aseprite 1.2 compatible.
local root=assert(app.params.root):gsub('\\','/')
local dir=root..'/Docs/Art/Movement/Revision2/EnemyStudent'
local pc=app.pixelColor
local source=Image{fromFile=root..'/Assets/Game/Resources/EnemyStudent/Animations/move/frame-01.png'}
local W,H=256,224
assert(source.width==W and source.height==H)
local function C(r,g,b) return pc.rgba(r,g,b,255) end
local ink=C(51,25,30)
local skin=C(251,206,185)
local skinHi=C(253,209,190)
local skinShade=C(219,157,134)
local skinDeep=C(193,132,113)
local sock=C(66,51,68)
local sockHi=C(77,56,81)
local sockShade=C(56,43,62)
local sockDeep=C(40,32,47)

local poses={
  {name='Contact L', lx=94,ly=201, rx=151,ry=200, bob=0, head=0, hip=-1},
  {name='Down L',lx=107,ly=201, rx=142,ry=198, bob=1, head=0, hip=-1},
  {name='Pass L',lx=124,ly=201, rx=126,ry=197, bob=-1,head=1, hip=0},
  {name='Swing R',lx=142,ly=201, rx=108,ry=199, bob=-1,head=1, hip=1},
  {name='Contact R',lx=151,ly=200, rx=94,ry=201, bob=0, head=0, hip=1},
  {name='Down R',lx=142,ly=198, rx=107,ry=201, bob=1, head=0, hip=1},
  {name='Pass R',lx=126,ly=197, rx=124,ry=201, bob=-1,head=-1,hip=0},
  {name='Swing L',lx=108,ly=199, rx=142,ry=201, bob=-1,head=-1,hip=-1},
}
local function round(x) return math.floor(x+0.5) end
local function interp(a,b,t) return a+(b-a)*t end

local function row(img,y,cx,radius,outline,fill,shade,highlight)
  local left=round(cx-radius)
  local right=round(cx+radius)
  for x=left-1,right+1 do
    local col=outline
    if x>=left and x<=right then
      if x>=right-2 then col=shade
      elseif x<=left+2 then col=highlight
      else col=fill end
      if (x+y)%13==0 and x>left+2 and x<right-2 then col=shade end
    end
    if x>=0 and x<W and y>=0 and y<H then img:drawPixel(x,y,col) end
  end
end

local function limb(img,p,which)
  local isL=which=='L'
  local hip=(isL and 111 or 139)+p.hip
  local foot=isL and p.lx or p.rx
  local bottom=isL and p.ly or p.ry
  local ankle=foot+6
  local knee=round(interp(hip,ankle,.50)+(foot<hip and -2 or 2))
  local hy=155+p.bob
  local ky=176+p.bob
  local ay=bottom-9
  -- Skin continues under the hem; stock and skin overlap at the knee cuff.
  for y=hy,ky do
    local t=(y-hy)/(ky-hy)
    local cx=interp(hip,knee,t)
    local radius=interp(8,7,t)
    row(img,y,cx,radius,ink,skin,skinShade,skinHi)
  end
  for y=ky-1,ay do
    local t=(y-(ky-1))/(ay-(ky-1))
    local cx=interp(knee,ankle,t)
    local radius=interp(7,5,t)
    row(img,y,cx,radius,ink,sock,sockShade,sockHi)
  end
  -- Distinct knee band, with the visible leg slightly warmer in front.
  for y=ky-1,ky+1 do
    local cx=interp(knee,ankle,math.max(0,(y-ky+1)/(ay-ky+1)))
    row(img,y,cx,7,ink,sockShade,sockDeep,sockHi)
  end
  -- Grounded native boot pixels keep the existing shoe shape and palette.
  for y=189,201 do for x=68,106 do
    local c=source:getPixel(x,y)
    if pc.rgbaA(c)==255 then
      local dx=x+foot-86
      local dy=y+bottom-201
      if dx>=0 and dx<W and dy>=0 and dy<H then img:drawPixel(dx,dy,c) end
    end
  end end
end

local function upperMask(c,x,y)
  if pc.rgbaA(c)~=255 then return false end
  if y<=154 then return true end
  if y<=167 and x>=95 and x<=162 then
    local r,g,b=pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c)
    if r<165 and g<130 and b<115 then return true end
  end
  if x>=141 and y>=155 and y<=201 then
    local line=145+(x-147)*.59
    if math.abs(y-line)<=7 then return true end
  end
  return false
end

local function upper(img,p)
  for y=67,205 do for x=60,237 do
    local c=source:getPixel(x,y)
    if upperMask(c,x,y) then
      local w=math.max(0,math.min(1,(y-112)/55))
      local shift=round(interp(p.head,p.hip,w))
      img:drawPixel(x+shift,y+p.bob,c)
    end
  end end
end

local frames={}
for i,p in ipairs(poses) do
  local img=Image(W,H,ColorMode.RGB)
  limb(img,p,'R')
  limb(img,p,'L')
  upper(img,p)
  frames[i]=img
  img:saveAs(dir..'/manual-frame-'..string.format('%02d',i)..'.png')
  print(i,p.name)
end
local sprite=Sprite(W,H,ColorMode.RGB)
local layer=sprite.layers[1]
layer.name='Full character'
local init=layer:cel(1)
if init then sprite:deleteCel(init) end
for i,img in ipairs(frames) do
  if i>1 then sprite:newEmptyFrame(i) end
  sprite:newCel(layer,i,img,Point(0,0))
  sprite.frames[i].duration=.11
end
local tag=sprite:newTag(1,8)
tag.name='Walk'
sprite:saveAs(dir..'/EnemyStudent-walk-manual.aseprite')
local sheet=Image(2048,224,ColorMode.RGB)
for p in sheet:pixels() do p(C(34,31,39)) end
for i,img in ipairs(frames) do sheet:drawImage(img,Point((i-1)*256,0)) end
local large=Image(6144,672,ColorMode.RGB)
for p in large:pixels() do p(sheet:getPixel(math.floor(p.x/3),math.floor(p.y/3))) end
large:saveAs(dir..'/EnemyStudent-walk-manual-sheet.png')
print('Manual 8-frame prototype ready')
