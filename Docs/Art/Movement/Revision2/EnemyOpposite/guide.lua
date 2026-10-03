local root=assert(app.params.root):gsub('\\','/')
local dir=root..'/Docs/Art/Movement/Revision2/EnemyOpposite'
local src=root..'/Docs/Art/Movement/Revision2/EnemyStudent/Candidate-4-v4/frame-04.png'
local pc=app.pixelColor
local img=Image{fromFile=src}
local red=pc.rgba(237,54,56,255)
local blue=pc.rgba(28,196,248,255)
local yellow=pc.rgba(255,220,40,255)
local function spot(x,y,r,c)
  for yy=math.floor(y-r),math.ceil(y+r) do
    for xx=math.floor(x-r),math.ceil(x+r) do
      if xx>=0 and yy>=0 and xx<img.width and yy<img.height and (xx-x)^2+(yy-y)^2<=r*r then img:drawPixel(xx,yy,c) end
    end
  end
end
local function stroke(points,r,c)
  for k=1,#points-1 do
    local a,b=points[k],points[k+1]
    local steps=math.max(math.abs(b[1]-a[1]),math.abs(b[2]-a[2]))*2
    for j=0,steps do
      local t=j/steps
      spot(a[1]+(b[1]-a[1])*t,a[2]+(b[2]-a[2])*t,r,c)
    end
  end
end
-- Cyan: old left support leg now extends behind to the right.
stroke({{100,143},{113,156},{126,174},{143,194}},3,blue)
stroke({{142,195},{151,197}},3,blue)
-- Red: right hip's trailing leg overtakes the left and lands at screen-left.
stroke({{121,143},{113,153},{102,167},{91,184},{83,195}},3,red)
stroke({{83,197},{73,197}},3,red)
spot(121,143,5,red)
spot(100,143,5,blue)
spot(80,198,5,yellow)
img:saveAs(dir..'/guide-opposite.png')
local big=Image(1024,896,ColorMode.RGB)
for p in big:pixels() do p(img:getPixel(math.floor(p.x/4),math.floor(p.y/4))) end
big:saveAs(dir..'/guide-opposite-4x.png')
print('guide ready')
