local root=assert(app.params.root):gsub('\\','/')
local from=root..'/Docs/Art/Movement/Revision2/EnemyStudent'
local out=from..'/Candidate-4-v4'
local pc=app.pixelColor
local idle=Image{fromFile=root..'/Assets/Game/Resources/EnemyStudent/Animations/idle/frame-01.png'}
local palette={}
for p in idle:pixels() do local c=p(); if pc.rgbaA(c)>0 then palette[c]=true end end
local frames={}
for i=1,4 do
  local img=Image{fromFile=out..'/source-frame-'..string.format('%02d',i)..'.png'}
  assert(img.width==256 and img.height==224)
  assert(pc.rgbaA(img:getPixel(155,164))==0,'Stale source still contains sword fleck')
  assert(pc.rgbaA(img:getPixel(157,168))==0,'Stale source still contains sword fleck')
  local foot=false
  for p in img:pixels() do
    local c=p()
    assert(pc.rgbaA(c)==0 or pc.rgbaA(c)==255,'Alpha is not binary')
    if pc.rgbaA(c)==255 then
      assert(palette[c],'Off-palette pixel')
      if p.y==201 and p.x<175 then foot=true end
    end
  end
  assert(foot,'No planted foot at y201')
  for j=1,i-1 do assert(not frames[j]:isEqual(img),'Repeated frame') end
  frames[i]=img
  img:saveAs(out..'/frame-'..string.format('%02d',i)..'.png')
end
local native=Sprite(256,224,ColorMode.RGB)
local layer=native.layers[1]
layer.name='Full character'
local initial=layer:cel(1)
if initial then native:deleteCel(initial) end
for i,img in ipairs(frames) do
  if i>1 then native:newEmptyFrame(i) end
  native:newCel(layer,i,img,Point(0,0))
  native.frames[i].duration=.12
end
local tag=native:newTag(1,4)
tag.name='Walk'
native:saveAs(out..'/EnemyStudent-walk-4.aseprite')
local reopened=app.open(out..'/EnemyStudent-walk-4.aseprite')
assert(#reopened.frames==4 and reopened.tags[1].name=='Walk')
for i,img in ipairs(frames) do
  local check=Image(256,224,ColorMode.RGB)
  check:drawSprite(reopened,i)
  assert(check:isEqual(img),'Aseprite round trip differed')
end
local sheet=Image(1024,224,ColorMode.RGB)
for p in sheet:pixels() do p(pc.rgba(34,31,39,255)) end
for i,img in ipairs(frames) do sheet:drawImage(img,Point((i-1)*256,0)) end
local big=Image(3072,672,ColorMode.RGB)
for p in big:pixels() do p(sheet:getPixel(math.floor(p.x/3),math.floor(p.y/3))) end
big:saveAs(out..'/EnemyStudent-walk-4-sheet.png')
local anim=Sprite(768,672,ColorMode.RGB)
local animLayer=anim.layers[1]
animLayer.name='Preview'
local old=animLayer:cel(1)
if old then anim:deleteCel(old) end
for i,img in ipairs(frames) do
  local render=Image(768,672,ColorMode.RGB)
  for p in render:pixels() do
    local c=img:getPixel(math.floor(p.x/3),math.floor(p.y/3))
    if pc.rgbaA(c)==0 then c=pc.rgba(34,31,39,255) end
    p(c)
  end
  if i>1 then anim:newEmptyFrame(i) end
  anim:newCel(animLayer,i,render,Point(0,0))
  anim.frames[i].duration=.12
end
anim:saveAs(out..'/EnemyStudent-walk-4.gif')
local gif=app.open(out..'/EnemyStudent-walk-4.gif')
assert(#gif.frames==4)
print('Four distinct full-body walk frames, 120ms each, Aseprite round trip/palette/alpha/footline valid')
