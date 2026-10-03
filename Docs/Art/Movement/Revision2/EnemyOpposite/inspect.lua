local root=assert(app.params.root):gsub('\\','/')
local dir=root..'/Docs/Art/Movement/Revision2/EnemyOpposite'
local pc=app.pixelColor
local target=app.params.target or 'opposite-contact-v2'
local img=Image{fromFile=dir..'/'..target..'.png'}
local big=Image(606,456,ColorMode.RGB)
for p in big:pixels() do
  local c=img:getPixel(64+math.floor(p.x/6),130+math.floor(p.y/6))
  if pc.rgbaA(c)==0 then c=pc.rgba(34,31,39,255) end
  p(c)
end
big:saveAs(dir..'/crop-legs-'..target..'.png')
local regions={{'skin',85,145,140,170},{'sock',70,165,160,196},{'boot',67,186,162,203},{'skirt',85,135,155,154}}
for _,r in ipairs(regions) do
  local hist={}
  for y=r[3],r[5] do for x=r[2],r[4] do
    local c=img:getPixel(x,y)
    if pc.rgbaA(c)==255 then
      local key=string.format('%d,%d,%d',pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c))
      hist[key]=(hist[key] or 0)+1
    end
  end end
  local sorted={}
  for key,count in pairs(hist) do sorted[#sorted+1]={key,count} end
  table.sort(sorted,function(a,b)return a[2]>b[2] end)
  print(r[1])
  for i=1,math.min(10,#sorted) do print(sorted[i][1],sorted[i][2]) end
end
