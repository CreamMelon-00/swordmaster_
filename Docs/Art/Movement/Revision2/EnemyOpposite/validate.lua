local root=assert(app.params.root):gsub('\\','/')
local dir=root..'/Docs/Art/Movement/Revision2/EnemyOpposite'
local pc=app.pixelColor
local img=Image{fromFile=dir..'/opposite-contact-v2.png'}
local base=Image{fromFile=root..'/Docs/Art/Movement/Revision2/EnemyStudent/Candidate-4-v4/frame-04.png'}
local idle=Image{fromFile=root..'/Assets/Game/Resources/EnemyStudent/Animations/idle/frame-01.png'}
local first=Image{fromFile=root..'/Docs/Art/Movement/Revision2/EnemyStudent/Candidate-4-v4/frame-01.png'}
assert(img.width==256 and img.height==224)
local palette={}
for p in idle:pixels() do local c=p(); if pc.rgbaA(c)==255 then palette[c]=true end end
for p in base:pixels() do local c=p(); if pc.rgbaA(c)==255 then palette[c]=true end end
local count,foot,maxFoot=0,0,0
for p in img:pixels() do
  local c,a=p(),pc.rgbaA(p())
  assert(a==0 or a==255,'non-binary alpha at '..p.x..','..p.y)
  if a==255 then
    count=count+1
    assert(palette[c],'off-palette pixel at '..p.x..','..p.y)
    if p.y>=180 and p.x<170 then maxFoot=math.max(maxFoot,p.y) end
    if p.y==201 and p.x<170 then foot=foot+1 end
  end
  if p.y<143 then assert(c==base:getPixel(p.x,p.y),'upper sprite changed') end
end
assert(foot>0 and maxFoot==201,'foot baseline incorrect: '..maxFoot)
local seen={}
local components={}
local function id(x,y) return y*256+x end
for y=0,223 do for x=0,255 do
  local c=img:getPixel(x,y)
  if pc.rgbaA(c)==255 and not seen[id(x,y)] then
    local q={{x,y}}
    seen[id(x,y)]=true
    local qi=1
    while qi<=#q do
      local cur=q[qi];qi=qi+1
      for dy=-1,1 do for dx=-1,1 do
        if dx~=0 or dy~=0 then
          local nx,ny=cur[1]+dx,cur[2]+dy
          if nx>=0 and nx<256 and ny>=0 and ny<224 and pc.rgbaA(img:getPixel(nx,ny))==255 and not seen[id(nx,ny)] then
            seen[id(nx,ny)]=true
            q[#q+1]={nx,ny}
          end
        end
      end end
    end
    components[#components+1]=#q
  end
end end
table.sort(components,function(a,b)return a>b end)
assert(#components<=3,'too many disconnected components: '..#components)
print('pixels',count,'foot pixels at y201',foot,'components',#components,table.concat(components,','))
for y=188,201 do
  local lo,hi=255,0
  for x=60,110 do if pc.rgbaA(img:getPixel(x,y))==255 then lo=math.min(lo,x);hi=math.max(hi,x) end end
  print('frontboot',y,lo,hi)
  local flo,fhi=255,0
  for x=60,110 do if pc.rgbaA(first:getPixel(x,y))==255 then flo=math.min(flo,x);fhi=math.max(fhi,x) end end
  print('firstboot',y,flo,fhi)
end
local sprite=Sprite(256,224,ColorMode.RGB)
local layer=sprite.layers[1]
layer.name='Right foot contact'
local old=layer:cel(1)
if old then sprite:deleteCel(old) end
sprite:newCel(layer,1,img,Point(0,0))
sprite.frames[1].duration=.12
sprite:saveAs(dir..'/EnemyStudent-right-contact.aseprite')
local reopened=app.open(dir..'/EnemyStudent-right-contact.aseprite')
local check=Image(256,224,ColorMode.RGB)
check:drawSprite(reopened,1)
assert(check:isEqual(img),'Aseprite round trip differed')
print('Aseprite round trip valid')
