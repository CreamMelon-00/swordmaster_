local root=assert(app.params.root):gsub('\\','/')
local dir=root..'/Docs/Art/Movement/RunPose/SwordGirl'
local pc=app.pixelColor
local idle=Image{fromFile=root..'/Assets/Game/Resources/SwordGirl/Animations/idle/frame-01.png'}
local concept=Image{fromFile=dir..'/concept-running-final.png'}
assert(idle.width==256 and idle.height==224)
assert(concept.width==1341 and concept.height==1173)

local palette,seen={},{}
for p in idle:pixels() do
  local c=p()
  if pc.rgbaA(c)==255 and not seen[c] then
    seen[c]=true
    palette[#palette+1]={c=c,r=pc.rgbaR(c),g=pc.rgbaG(c),b=pc.rgbaB(c)}
  end
end
assert(#palette==64)
local cache={}
local function nearest(r,g,b)
  local key=math.floor(r/4)*65536+math.floor(g/4)*256+math.floor(b/4)
  if cache[key] then return cache[key] end
  local chosen,best=nil,math.huge
  for _,v in ipairs(palette) do
    local dr,dg,db=r-v.r,g-v.g,b-v.b
    local distance=2*dr*dr+3*dg*dg+db*db
    if distance<best then chosen,best=v.c,distance end
  end
  cache[key]=chosen
  return chosen
end

-- The generated image supplies the newly illustrated anatomy. This step
-- samples its native pixels onto the game's 256x224 grid and maps colors to
-- the existing 64-color sprite palette; it does not deform an old game cel.
local out=Image(256,224,ColorMode.RGB)
local scaleX,scaleY=.19,.195
local sourceCenterX,sourceFootY=742,1054
local targetCenterX,targetFootY=128,202
for y=0,223 do for x=0,255 do
  local sourceX=sourceCenterX+(x-targetCenterX)/scaleX
  local sourceY=sourceFootY-(targetFootY-y)/scaleY
  local rr,gg,bb,aa=0,0,0,0
  for j=-1,1 do for i=-1,1 do
    local sx=math.floor(sourceX+i*1.5+.5)
    local sy=math.floor(sourceY+j*1.5+.5)
    if sx>=0 and sx<concept.width and sy>=0 and sy<concept.height then
      local c=concept:getPixel(sx,sy)
      local alpha=pc.rgbaA(c)
      aa=aa+alpha
      rr=rr+alpha*pc.rgbaR(c)
      gg=gg+alpha*pc.rgbaG(c)
      bb=bb+alpha*pc.rgbaB(c)
    end
  end end
  if aa>=9*128 then out:drawPixel(x,y,nearest(rr/aa,gg/aa,bb/aa)) end
end end
out:saveAs(dir..'/candidate-01.png')

local bg=pc.rgba(53,50,56,255)
local comparison=Image(256*4*2,224*4,ColorMode.RGB)
for p in comparison:pixels() do
  local src=(p.x<1024) and idle or out
  local c=src:getPixel(math.floor((p.x%1024)/4),math.floor(p.y/4))
  p(pc.rgbaA(c)==255 and c or bg)
end
comparison:saveAs(dir..'/candidate-01-comparison.png')

local sprite=Sprite(256,224,ColorMode.RGB)
local layer=sprite.layers[1]
layer.name='New illustrated forward run'
local old=layer:cel(1)
if old then sprite:deleteCel(old) end
sprite:newCel(layer,1,out,Point(0,0))
sprite:saveAs(dir..'/candidate-01.aseprite')

print('candidate generated',#palette)
