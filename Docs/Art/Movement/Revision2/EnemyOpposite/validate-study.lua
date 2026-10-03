local root=assert(app.params.root):gsub('\\','/')
local dir=root..'/Docs/Art/Movement/Revision2/EnemyOpposite'
local name=assert(app.params.name)
local img=Image{fromFile=dir..'/'..name..'.png'}
local base=Image{fromFile=root..'/'..assert(app.params.base)}
local idle=Image{fromFile=root..'/Assets/Game/Resources/EnemyStudent/Animations/idle/frame-01.png'}
local pc=app.pixelColor
assert(img.width==256 and img.height==224)
local pal={}
for _,im in ipairs({idle,base}) do for p in im:pixels() do
  local c=p();if pc.rgbaA(c)==255 then pal[c]=true end
end end
local foot,maxFoot=0,0
for p in img:pixels() do
  local c,a=p(),pc.rgbaA(p())
  assert(a==0 or a==255,'alpha at '..p.x..','..p.y)
  if a==255 then
    assert(pal[c],'palette at '..p.x..','..p.y)
    if p.x<170 and p.y>=180 then maxFoot=math.max(maxFoot,p.y) end
    if p.x<170 and p.y==201 then foot=foot+1 end
  end
  if p.y<143 or p.x>=166 then assert(c==base:getPixel(p.x,p.y),'base sprite changed at '..p.x..','..p.y) end
end
assert(foot>0 and maxFoot==201,'foot line '..maxFoot)
local seen,components={},{}
local function id(x,y)return y*256+x end
for y=0,223 do for x=0,255 do
  if pc.rgbaA(img:getPixel(x,y))==255 and not seen[id(x,y)] then
    local q={{x,y}};seen[id(x,y)]=true;local qi=1
    while qi<=#q do
      local cur=q[qi];qi=qi+1
      for dy=-1,1 do for dx=-1,1 do
        if dx~=0 or dy~=0 then
          local xx,yy=cur[1]+dx,cur[2]+dy
          if xx>=0 and yy>=0 and xx<256 and yy<224 and pc.rgbaA(img:getPixel(xx,yy))==255 and not seen[id(xx,yy)] then
            seen[id(xx,yy)]=true;q[#q+1]={xx,yy}
          end
        end
      end end
    end
    components[#components+1]=#q
  end
end end
table.sort(components,function(a,b)return a>b end)
assert(#components==1,'disconnected components '..#components)
local sprite=Sprite(256,224,ColorMode.RGB)
local layer=sprite.layers[1]
layer.name='Full character'
local old=layer:cel(1);if old then sprite:deleteCel(old) end
sprite:newCel(layer,1,img,Point(0,0))
sprite.frames[1].duration=.12
sprite:saveAs(dir..'/'..name..'.aseprite')
local check=app.open(dir..'/'..name..'.aseprite')
local render=Image(256,224,ColorMode.RGB)
render:drawSprite(check,1)
assert(render:isEqual(img),'Aseprite mismatch')
print(name,'valid',foot,'contact pixels',components[1],'connected pixels')
