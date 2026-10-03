local root=assert(app.params.root):gsub('\\','/')
local base=root..'/Docs/Art/Movement/Revision2/EnemyStudent'
local source=base..'/Review-5'
local out=base..'/Candidate-8-final'
local pc=app.pixelColor
local idle=Image{fromFile=root..'/Assets/Game/Resources/EnemyStudent/Animations/idle/frame-01.png'}
local palette={}
for p in idle:pixels() do
  local c=p()
  if pc.rgbaA(c)==255 then palette[c]=true end
end

local frames={}
for i=1,8 do
  local img=Image{fromFile=source..'/frame-'..string.format('%02d',i)..'.png'}
  assert(img.width==256 and img.height==224,'Wrong canvas')
  local grounded=false
  for p in img:pixels() do
    local c=p()
    assert(pc.rgbaA(c)==0 or pc.rgbaA(c)==255,'Nonbinary alpha')
    if pc.rgbaA(c)==255 then
      assert(palette[c],'Off-palette pixel')
      if p.y==201 and p.x<165 then grounded=true end
    end
  end
  assert(grounded,'No grounded boot')
  for j=1,i-1 do assert(not frames[j]:isEqual(img),'Duplicate frame') end
  frames[i]=img
  img:saveAs(out..'/frame-'..string.format('%02d',i)..'.png')
end

local native=Sprite(256,224,ColorMode.RGB)
local layer=native.layers[1]
layer.name='Full character'
for i,img in ipairs(frames) do
  if i>1 then native:newEmptyFrame(i) end
  local existing=layer:cel(i)
  if existing then native:deleteCel(existing) end
  native:newCel(layer,i,img,Point(0,0))
  native.frames[i].duration=.12
end
native:newTag(1,8).name='Walk'
native:saveAs(out..'/EnemyStudent-walk-8.aseprite')
local reopened=app.open(out..'/EnemyStudent-walk-8.aseprite')
assert(#reopened.frames==8 and reopened.tags[1].name=='Walk','Invalid Aseprite source')
for i,img in ipairs(frames) do
  local check=Image(256,224,ColorMode.RGB)
  check:drawSprite(reopened,i)
  assert(check:isEqual(img),'Native source roundtrip mismatch')
end

local background=pc.rgba(34,31,39,255)
local sheet=Image(3072,1344,ColorMode.RGB)
for p in sheet:pixels() do p(background) end
for i,img in ipairs(frames) do
  local col,row=(i-1)%4,math.floor((i-1)/4)
  for y=0,223 do for x=0,255 do
    local c=img:getPixel(x,y)
    if pc.rgbaA(c)>0 then
      for dy=0,2 do for dx=0,2 do
        sheet:drawPixel(col*768+x*3+dx,row*672+y*3+dy,c)
      end end
    end
  end end
end
sheet:saveAs(out..'/EnemyStudent-walk-8-sheet.png')
local wrap=Image(3072,672,ColorMode.RGB)
for p in wrap:pixels() do p(background) end
for slot,index in ipairs({7,8,1,2}) do
  local img=frames[index]
  for y=0,223 do for x=0,255 do
    local c=img:getPixel(x,y)
    if pc.rgbaA(c)>0 then
      for dy=0,2 do for dx=0,2 do
        wrap:drawPixel((slot-1)*768+x*3+dx,y*3+dy,c)
      end end
    end
  end end
end
wrap:saveAs(out..'/EnemyStudent-walk-8-wrap.png')

local function makeGif(scale,path)
  local preview=Sprite(256*scale,224*scale,ColorMode.RGB)
  local previewLayer=preview.layers[1]
  previewLayer.name='Review preview'
  for i,img in ipairs(frames) do
    if i>1 then preview:newEmptyFrame(i) end
    local old=previewLayer:cel(i)
    if old then preview:deleteCel(old) end
    local render=Image(256*scale,224*scale,ColorMode.RGB)
    for p in render:pixels() do
      local c=img:getPixel(math.floor(p.x/scale),math.floor(p.y/scale))
      p(pc.rgbaA(c)>0 and c or background)
    end
    preview:newCel(previewLayer,i,render,Point(0,0))
    preview.frames[i].duration=.12
  end
  preview:saveAs(path)
  local gif=app.open(path)
  assert(#gif.frames==8,'GIF frame count mismatch')
end
makeGif(1,out..'/EnemyStudent-walk-8-native.gif')
makeGif(3,out..'/EnemyStudent-walk-8-enlarged.gif')
print('Built 8 distinct 256x224 frames, 120ms each; native roundtrip verified')
