local root=assert(app.params.root):gsub('\\','/')
local dir=root..'/Docs/Art/Movement/Revision2/SwordGirl'
local pc=app.pixelColor
local names={'contact-a','down-a','pass-a','swing-a','contact-b','down-b','pass-b','swing-b'}
local function settleSwingToe(im)
  -- The generated last swing stops eight pixels above the line. Extend the
  -- continuous forward shin by three pixels, leaving the support leg and
  -- pelvis unchanged; next-frame contact then has a small landing distance.
  local out=Image(im)
  local start,oldEnd,newEnd=164,194,197
  for y=start,223 do for x=112,255 do
    local sy
    if y<=newEnd then sy=math.floor(start+(y-start)*(oldEnd-start)/(newEnd-start)+.5)
    else sy=y-(newEnd-oldEnd) end
    out:drawPixel(x,y,im:getPixel(x,sy))
  end end
  return out
end
local frames={}
for i,name in ipairs(names) do
  local im=Image{fromFile=dir..'/key-'..name..'.png'}
  assert(im.width==256 and im.height==224)
  if i==8 then im=settleSwingToe(im) end
  frames[i]=im
  im:saveAs(dir..string.format('/frame-%02d.png',i))
end

local sheet=Image(3072,1344,ColorMode.RGB)
for p in sheet:pixels() do p(pc.rgba(53,50,56,255)) end
for i,im in ipairs(frames) do
  for p in im:pixels() do
    local c=p()
    if pc.rgbaA(c)>0 then
      local ox=((i-1)%4)*768+p.x*3
      local oy=math.floor((i-1)/4)*672+p.y*3
      for yy=0,2 do for xx=0,2 do sheet:drawPixel(ox+xx,oy+yy,c) end end
    end
  end
end
sheet:saveAs(dir..'/SwordGirl-walk-poses.png')
local sprite=Sprite(256,224,ColorMode.RGB)
local layer=sprite.layers[1]
layer.name='Full body'
local cel=layer:cel(1)
if cel then sprite:deleteCel(cel) end
for i,im in ipairs(frames) do
  if i>1 then sprite:newEmptyFrame(i) end
  sprite:newCel(layer,i,im,Point(0,0))
  sprite.frames[i].duration=.12
end
local tag=sprite:newTag(1,8)
tag.name='Move'
sprite:saveAs(dir..'/SwordGirl-walk-eight.aseprite')
sprite:saveAs(dir..'/SwordGirl-walk-native.gif')

local animated=Sprite(1024,896,ColorMode.RGB)
local animLayer=animated.layers[1]
animLayer.name='Loop preview'
local empty=animLayer:cel(1)
if empty then animated:deleteCel(empty) end
for i,im in ipairs(frames) do
  local large=Image(1024,896,ColorMode.RGB)
  for p in large:pixels() do
    local c=im:getPixel(math.floor(p.x/4),math.floor(p.y/4))
    p(pc.rgbaA(c)>0 and c or pc.rgba(53,50,56,255))
  end
  if i>1 then animated:newEmptyFrame(i) end
  animated:newCel(animLayer,i,large,Point(0,0))
  animated.frames[i].duration=.12
end
animated:saveAs(dir..'/SwordGirl-walk-enlarged.gif')

local opened=app.open(dir..'/SwordGirl-walk-eight.aseprite')
assert(#opened.frames==8 and #opened.tags==1)
for i,expected in ipairs(frames) do
  local actual=Image(256,224,ColorMode.RGB)
  actual:drawSprite(opened,i)
  assert(actual:isEqual(expected),'Source round-trip mismatch '..i)
end
print('Eight frames, 120ms each, source round-trip passed')
