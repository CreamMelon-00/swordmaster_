local root=assert(app.params.root):gsub('\\','/')
local dir=root..'/Docs/Art/Movement/SinglePose/EnemyStudent'
local pc=app.pixelColor
local idle=Image{fromFile=root..'/Assets/Game/Resources/EnemyStudent/Animations/idle/frame-01.png'}
assert(idle.width==256 and idle.height==224,'Unexpected source canvas')

-- Preserve each original pixel/color while leaning the connected silhouette
-- toward the left. The floor stays anchored, so this is a travel pose rather
-- than a whole-sprite translation.
local function shiftAt(y)
  if y<=58 then return -23 end
  if y<=145 then return math.floor(-23+(y-58)*16/87+.5) end
  if y<=201 then return math.floor(-7+(y-145)*7/56+.5) end
  return 0
end
local out=Image(256,224,ColorMode.RGB)
for p in idle:pixels() do
  local c=p()
  if pc.rgbaA(c)>0 then
    local x=p.x+shiftAt(p.y)
    if x>=0 and x<256 then out:drawPixel(x,p.y,c) end
  end
end
out:saveAs(dir..'/EnemyStudent-travel.png')

local native=Sprite(256,224,ColorMode.RGB)
local layer=native.layers[1]
layer.name='Full body travel pose'
local old=layer:cel(1)
if old then native:deleteCel(old) end
native:newCel(layer,1,out,Point(0,0))
native:saveAs(dir..'/EnemyStudent-travel.aseprite')
local reopened=app.open(dir..'/EnemyStudent-travel.aseprite')
local check=Image(256,224,ColorMode.RGB)
check:drawSprite(reopened,1)
assert(check:isEqual(out),'Aseprite roundtrip mismatch')

local bg=pc.rgba(34,31,39,255)
local scale=4
local preview=Image(256*scale*2,224*scale,ColorMode.RGB)
for p in preview:pixels() do
  local source=(p.x<256*scale) and idle or out
  local sx=math.floor((p.x%(256*scale))/scale)
  local sy=math.floor(p.y/scale)
  local c=source:getPixel(sx,sy)
  p(pc.rgbaA(c)>0 and c or bg)
end
preview:saveAs(dir..'/EnemyStudent-travel-comparison.png')
local palette={}
for p in idle:pixels() do
  local c=p()
  if pc.rgbaA(c)==255 then palette[c]=true end
end
local n=0
for p in out:pixels() do
  local c=p()
  assert(pc.rgbaA(c)==0 or pc.rgbaA(c)==255,'Nonbinary alpha')
  if pc.rgbaA(c)==255 then
    assert(palette[c],'Off-palette pixel')
    n=n+1
  end
end
local function components(img)
  local seen={}
  local count,largest=0,0
  for y=0,223 do for x=0,255 do
    local key=y*256+x
    if not seen[key] and pc.rgbaA(img:getPixel(x,y))>0 then
      count=count+1
      local qx,qy={x},{y}
      local head,size=1,0
      seen[key]=true
      while head<=#qx do
        local cx,cy=qx[head],qy[head]
        head=head+1
        size=size+1
        for _,v in ipairs({{cx-1,cy},{cx+1,cy},{cx,cy-1},{cx,cy+1}}) do
          local nx,ny=v[1],v[2]
          local nk=ny*256+nx
          if nx>=0 and nx<256 and ny>=0 and ny<224 and not seen[nk] and pc.rgbaA(img:getPixel(nx,ny))>0 then
            seen[nk]=true
            qx[#qx+1],qy[#qy+1]=nx,ny
          end
        end
      end
      if size>largest then largest=size end
    end
  end end
  return count,largest
end
local oldComponents,oldLargest=components(idle)
local newComponents,newLargest=components(out)
assert(newComponents==oldComponents and newLargest==oldLargest,'Silhouette connectivity changed')
local ground=0
for x=0,255 do if pc.rgbaA(out:getPixel(x,201))>0 then ground=ground+1 end end
assert(ground>0,'Ground line lost')
print('Native pixel QA passed; opaque pixels='..n..'; components='..newComponents..'; ground pixels='..ground)
