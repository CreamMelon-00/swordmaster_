local root=assert(app.params.root):gsub('\\','/')
local pc=app.pixelColor
local img=Image{fromFile=root..'/Assets/Game/Resources/EnemyStudent/Animations/move/frame-01.png'}
local regions={
  {'skin',95,155,148,177},
  {'sockL',78,174,108,195},
  {'sockR',143,174,163,195},
  {'bootL',64,190,109,202},
  {'bootR',140,188,166,202},
  {'skirt',97,141,160,166},
}
for _,r in ipairs(regions) do
  local hist={}
  for y=r[3],r[5] do for x=r[2],r[4] do
    local c=img:getPixel(x,y)
    if pc.rgbaA(c)>0 then
      local key=string.format('%d,%d,%d',pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c))
      hist[key]=(hist[key] or 0)+1
    end
  end end
  local sorted={}
  for key,count in pairs(hist) do sorted[#sorted+1]={key,count} end
  table.sort(sorted,function(a,b)return a[2]>b[2] end)
  print(r[1])
  for i=1,math.min(12,#sorted) do print(sorted[i][1],sorted[i][2]) end
end
