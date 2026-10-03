local root=assert(app.params.root):gsub('\\','/')
local dir=root..'/Docs/Art/Movement/RunPose/EnemyStudent'
local pc=app.pixelColor
local idle=Image{fromFile=root..'/Assets/Game/Resources/EnemyStudent/Animations/idle/frame-01.png'}
local run=Image{fromFile=dir..'/candidate-box.png'}
assert(idle.width==256 and idle.height==224 and run.width==256 and run.height==224)

-- Pixel-level cleanup on the native canvas. This dot is a disconnected
-- remnant behind the bow, and is not part of the trailing ribbon contour.
assert(pc.rgbaA(run:getPixel(127,87))>0,'Expected bow-edge remnant missing')
run:drawPixel(127,87,pc.rgba(0,0,0,0))

-- Keep the stance on the exact shared floor line and enforce the shipped
-- character palette and binary alpha before storing an editable sprite.
local palette={}
for p in idle:pixels() do
  local c=p()
  if pc.rgbaA(c)==255 then palette[c]=true end
end
local grounded=false
for p in run:pixels() do
  local c=p()
  assert(pc.rgbaA(c)==0 or pc.rgbaA(c)==255,'Nonbinary alpha')
  if pc.rgbaA(c)==255 then
    assert(palette[c],'Outside shipped idle palette')
    if p.y==201 and p.x<140 then grounded=true end
  end
end
assert(grounded,'Support boot is not on floor y201')

local visited={}
local components=0
for y=0,223 do for x=0,255 do
  local key=y*256+x
  if not visited[key] and pc.rgbaA(run:getPixel(x,y))>0 then
    components=components+1
    local qx,qy={x},{y}
    local head=1
    visited[key]=true
    while head<=#qx do
      local cx,cy=qx[head],qy[head]
      head=head+1
      for _,v in ipairs({{cx-1,cy},{cx+1,cy},{cx,cy-1},{cx,cy+1}}) do
        local nx,ny=v[1],v[2]
        local nk=ny*256+nx
        if nx>=0 and nx<256 and ny>=0 and ny<224 and not visited[nk]
          and pc.rgbaA(run:getPixel(nx,ny))>0 then
          visited[nk]=true
          qx[#qx+1],qy[#qy+1]=nx,ny
        end
      end
    end
  end
end end
assert(components==1,'Separated pixel-art silhouette: '..components)

run:saveAs(dir..'/frame-01.png')
local sprite=Sprite(256,224,ColorMode.RGB)
local layer=sprite.layers[1]
layer.name='Running travel pose - full body'
local previous=layer:cel(1)
if previous then sprite:deleteCel(previous) end
sprite:newCel(layer,1,run,Point(0,0))
sprite:saveAs(dir..'/EnemyStudent-run.aseprite')
local reopened=app.open(dir..'/EnemyStudent-run.aseprite')
local check=Image(256,224,ColorMode.RGB)
check:drawSprite(reopened,1)
assert(check:isEqual(run),'Aseprite source roundtrip mismatch')

local scale=4
local bg=pc.rgba(35,32,43,255)
local preview=Image(256*scale*2,224*scale,ColorMode.RGB)
for p in preview:pixels() do
  local src=p.x<256*scale and idle or run
  local sx=math.floor((p.x%(256*scale))/scale)
  local sy=math.floor(p.y/scale)
  local c=src:getPixel(sx,sy)
  p(pc.rgbaA(c)>0 and c or bg)
end
preview:saveAs(dir..'/idle-vs-run.png')
print('Running sprite complete: one component, original palette, y201 floor, editable Aseprite roundtrip')
