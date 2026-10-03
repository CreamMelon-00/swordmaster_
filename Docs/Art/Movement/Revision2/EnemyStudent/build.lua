-- Eight whole-body, low-stride walk frames derived from the shipped character.
-- No external reference pixels are copied. Run through Aseprite 1.2 batch mode.
local root=assert(app.params.root):gsub('\\','/')
local dir=root..'/Docs/Art/Movement/Revision2/EnemyStudent'
local pc=app.pixelColor
local source=Image{fromFile=root..'/Assets/Game/Resources/EnemyStudent/Animations/move/frame-01.png'}
local idle=Image{fromFile=root..'/Assets/Game/Resources/EnemyStudent/Animations/idle/frame-01.png'}
assert(source.width==256 and source.height==224)

-- Each support foot moves back relative to the advancing actor over its four
-- frames; the other passes at low clearance. The second half swaps support.
local poses={
  {name='Contact L', lx=94, ly=201, rx=153, ry=199, bob=0, hx=0, hip=-1},
  {name='Down L',    lx=109,ly=201, rx=143, ry=197, bob=1, hx=0, hip=-1},
  {name='Pass L',    lx=126,ly=201, rx=124, ry=196, bob=-1,hx=1, hip=0},
  {name='Swing R',   lx=144,ly=201, rx=104, ry=198, bob=-1,hx=1, hip=1},
  {name='Contact R', lx=154,ly=199, rx=94,  ry=201, bob=0, hx=0, hip=1},
  {name='Down R',    lx=143,ly=197, rx=109, ry=201, bob=1, hx=0, hip=1},
  {name='Pass R',    lx=124,ly=196, rx=126, ry=201, bob=-1,hx=-1,hip=0},
  {name='Swing L',   lx=104,ly=198, rx=144, ry=201, bob=-1,hx=-1,hip=-1},
}

local function lerp(a,b,t) return a+(b-a)*t end
local function round(n) return math.floor(n+0.5) end
local function opaque(c) return pc.rgbaA(c)==255 end

-- Source-side body-part masks are within this project's own art. The lower
-- limbs sit behind a skirt, which naturally hides the hip joints.
local function isLegPixel(c,x,y,which)
  if not opaque(c) or y<155 or y>201 or x<62 or x>170 then return false end
  if which=='L' then
    if x>127 then return false end
  else
    if x<126 then return false end
  end
  -- Skip brown fabric over the top of the thighs; overlay it as a skirt later.
  if y<=168 then
    local r,g,b=pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c)
    if r<158 and r>g*1.12 and r>b*1.06 then return false end
  end
  return true
end

local function drawLeg(dst,p,which)
  local left=which=='L'
  local srcHip=left and 112 or 138
  local srcFoot=left and 86 or 153
  local srcBottom=left and 201 or 196
  local targetFoot=left and p.lx or p.rx
  local targetBottom=left and p.ly or p.ry
  local hipY=155+p.bob
  local footHeight=targetBottom-hipY
  for y=151,204 do
    local sy=155+(y-hipY)*(srcBottom-155)/footHeight
    local syi=round(sy)
    if syi>=155 and syi<=srcBottom then
      local t=math.max(0,math.min(1,(sy-155)/(srcBottom-155)))
      local shift=lerp((left and -1 or 1)*p.hip, targetFoot-srcFoot,t)
      for x=55,180 do
        local sx=round(x-shift)
        if sx>=0 and sx<256 then
          local c=source:getPixel(sx,syi)
          if isLegPixel(c,sx,syi,which) then dst:drawPixel(x,y,c) end
        end
      end
    end
  end
end

local function drawGroundShoe(dst,p,which)
  -- The original L boot is a flat grounded shoe. For the opposite planted
  -- leg, use those same native shoe pixels below the far-leg stocking.
  local targetX=which=='L' and p.lx or p.rx
  local targetY=which=='L' and p.ly or p.ry
  for y=190,201 do for x=65,107 do
    local c=source:getPixel(x,y)
    if opaque(c) then
      local dx=x+targetX-86
      local dy=y+targetY-201
      if dx>=0 and dx<256 and dy>=0 and dy<224 then dst:drawPixel(dx,dy,c) end
    end
  end end
end

local function keepUpper(c,x,y)
  if not opaque(c) then return false end
  if y<=154 then return true end
  if y<=168 and x>=95 and x<=165 then
    local r,g,b=pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c)
    if r<158 and r>g*1.12 and r>b*1.06 then return true end
    -- This is the hand-drawn dark outline and pleat crease of the skirt.
    if y<=165 and r<75 and g<47 and b<48 then return true end
  end
  -- Blade and guard extend down-right beyond the legs.
  if x>=141 and y>=155 and y<=201 then
    local lineY=145+(x-147)*0.59
    if math.abs(y-lineY)<=7 then return true end
  end
  return false
end

local function drawUpper(dst,p)
  for y=67,205 do for x=60,237 do
    local c=source:getPixel(x,y)
    if keepUpper(c,x,y) then
      local waist=math.max(0,math.min(1,(y-112)/53))
      local dx=round(lerp(p.hx,p.hip,waist))
      local dy=p.bob
      dst:drawPixel(x+dx,y+dy,c)
    end
  end end
end

local frames={}
for i,p in ipairs(poses) do
  local dst=Image(256,224,ColorMode.RGB)
  -- Far thigh is underneath the near thigh. The planted shoe is drawn last.
  drawLeg(dst,p,'R')
  drawLeg(dst,p,'L')
  if i<=4 then drawGroundShoe(dst,p,'L') else drawGroundShoe(dst,p,'R') end
  drawUpper(dst,p)
  frames[i]=dst
  dst:saveAs(dir..'/frame-'..string.format('%02d',i)..'.png')
  print(i,p.name,'feet',p.lx,p.ly,p.rx,p.ry)
end

local sprite=Sprite(256,224,ColorMode.RGB)
local layer=sprite.layers[1]
layer.name='Full character'
local initial=layer:cel(1)
if initial then sprite:deleteCel(initial) end
for i,frame in ipairs(frames) do
  if i>1 then sprite:newEmptyFrame(i) end
  sprite:newCel(layer,i,frame,Point(0,0))
  sprite.frames[i].duration=.11
end
local tag=sprite:newTag(1,#frames)
tag.name='Walk'
sprite:saveAs(dir..'/EnemyStudent-walk.aseprite')

-- Contact sheet at native pixel scale enlarged 3x for visual critique.
local sheet=Image(2048,224,ColorMode.RGB)
for p in sheet:pixels() do p(pc.rgba(34,31,39,255)) end
for i,frame in ipairs(frames) do sheet:drawImage(frame,Point((i-1)*256,0)) end
local enlarged=Image(6144,672,ColorMode.RGB)
for p in enlarged:pixels() do p(sheet:getPixel(math.floor(p.x/3),math.floor(p.y/3))) end
enlarged:saveAs(dir..'/EnemyStudent-walk-contact-sheet.png')

-- Playback preview against the same dark stage, 3x nearest pixels.
local anim=Sprite(768,672,ColorMode.RGB)
local animLayer=anim.layers[1]
animLayer.name='Preview'
local old=animLayer:cel(1)
if old then anim:deleteCel(old) end
for i,frame in ipairs(frames) do
  local render=Image(768,672,ColorMode.RGB)
  for pix in render:pixels() do
    local c=frame:getPixel(math.floor(pix.x/3),math.floor(pix.y/3))
    if not opaque(c) then c=pc.rgba(34,31,39,255) end
    pix(c)
  end
  if i>1 then anim:newEmptyFrame(i) end
  anim:newCel(animLayer,i,render,Point(0,0))
  anim.frames[i].duration=.11
end
anim:saveAs(dir..'/EnemyStudent-walk-preview.gif')

-- Native file, exported frames and GIF must survive an Aseprite round trip.
local native=app.open(dir..'/EnemyStudent-walk.aseprite')
assert(#native.frames==8 and #native.tags==1 and native.tags[1].name=='Walk')
for i,expected in ipairs(frames) do
  assert(math.abs(native.frames[i].duration-.11)<.001)
  local actual=Image(256,224,ColorMode.RGB)
  actual:drawSprite(native,i)
  assert(actual:isEqual(expected),'Native frame mismatch '..i)
end
local gif=app.open(dir..'/EnemyStudent-walk-preview.gif')
assert(#gif.frames==8,'GIF frame count mismatch')
print('8-frame walk, 110ms each, Aseprite round-trip passed')
