local root=assert(app.params.root):gsub('\\','/')
local dir=root..'/Docs/Art/Movement/Revision2/EnemyOpposite'
local name=assert(app.params.name)
local study=Image{fromFile=dir..'/'..assert(app.params.study)}
local base=Image{fromFile=root..'/'..assert(app.params.base)}
local nextImg=Image{fromFile=root..'/'..assert(app.params.next)}
local idle=Image{fromFile=root..'/Assets/Game/Resources/EnemyStudent/Animations/idle/frame-01.png'}
local pc=app.pixelColor
local colors,seen={},{}
for _,img in ipairs({idle,base}) do for p in img:pixels() do
  local c=p()
  if pc.rgbaA(c)==255 and not seen[c] then colors[#colors+1]=c;seen[c]=true end
end end
local function near(c)
  local r,g,b=pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c)
  local best,dmin=nil,1e9
  for _,v in ipairs(colors) do
    local dr,dg,db=r-pc.rgbaR(v),g-pc.rgbaG(v),b-pc.rgbaB(v)
    local d=dr*dr+dg*dg+db*db
    if d<dmin then best,dmin=v,d end
  end
  return best
end
local scaled=Image(256,224,ColorMode.RGB)
for p in scaled:pixels() do
  p(study:getPixel(math.floor((p.x+.5)*study.width/256),math.floor((p.y+.5)*study.height/224)))
end
scaled:saveAs(dir..'/'..name..'-scaled.png')
local maxFoot=0
for y=180,215 do for x=60,166 do
  if pc.rgbaA(scaled:getPixel(x,y))>=128 then maxFoot=math.max(maxFoot,y) end
end end
assert(maxFoot>=198 and maxFoot<=210,'unexpected study foot y: '..maxFoot)
local out=Image(256,224,ColorMode.RGB)
out:drawImage(base,Point(0,0))
for y=150,210 do for x=62,166 do
  if x<142 or y>=174 then out:drawPixel(x,y,pc.rgba(0,0,0,0)) end
end end
for y=150,201 do for x=62,166 do
  if x<142 or y>=174 then
    local sy=math.floor(150+(y-150)*(maxFoot-150)/51+0.5)
    local c=scaled:getPixel(x,sy)
    if pc.rgbaA(c)>=128 then out:drawPixel(x,y,near(c)) end
  end
end end
for y=143,151 do for x=80,145 do
  local c=base:getPixel(x,y)
  if pc.rgbaA(c)>0 then out:drawPixel(x,y,c) end
end end
out:saveAs(dir..'/'..name..'.png')
local sheet=Image(2304,672,ColorMode.RGB)
for p in sheet:pixels() do p(pc.rgba(34,31,39,255)) end
for i,img in ipairs({base,out,nextImg}) do
  for y=0,223 do for x=0,255 do
    local c=img:getPixel(x,y)
    if pc.rgbaA(c)>0 then for dy=0,2 do for dx=0,2 do
      sheet:drawPixel((i-1)*768+x*3+dx,y*3+dy,c)
    end end end
  end end
end
sheet:saveAs(dir..'/'..name..'-comparison.png')
print(name,'source size',study.width,study.height,'foot',maxFoot)
