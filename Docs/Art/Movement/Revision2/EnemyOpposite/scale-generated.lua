local root=assert(app.params.root):gsub('\\','/')
local dir=root..'/Docs/Art/Movement/Revision2/EnemyOpposite'
local pc=app.pixelColor
local src=Image{fromFile=dir..'/imagegen-crossed-reference.png'}
print('generated',src.width,src.height)
local out=Image(256,224,ColorMode.RGB)
for p in out:pixels() do
  local x=math.floor((p.x+0.5)*src.width/256)
  local y=math.floor((p.y+0.5)*src.height/224)
  p(src:getPixel(x,y))
end
out:saveAs(dir..'/imagegen-crossed-256.png')
local big=Image(768,672,ColorMode.RGB)
for p in big:pixels() do
  local c=out:getPixel(math.floor(p.x/3),math.floor(p.y/3))
  if pc.rgbaA(c)<128 then c=pc.rgba(34,31,39,255) end
  p(c)
end
big:saveAs(dir..'/imagegen-crossed-256-preview.png')
