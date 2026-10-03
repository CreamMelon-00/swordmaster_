local root=assert(app.params.root):gsub('\\','/')
local pc=app.pixelColor
for i=1,8 do
  local path=string.format('%s/Docs/Art/Movement/Revision2/EnemyStudent/sheet-frame-%02d.png',root,i)
  local img=Image{fromFile=path}
  local a,b=0,0
  local ax,bx=0,0
  for y=180,215 do for x=50,175 do
    local c=img:getPixel(x,y)
    if pc.rgbaA(c)>0 then
      if x<120 and y>a then a,ax=y,x end
      if x>=120 and y>b then b,bx=y,x end
    end
  end end
  print(i,'L',a,ax,'R',b,bx)
  local n=0
  for y=130,220 do for x=175,240 do if pc.rgbaA(img:getPixel(x,y))>0 then n=n+1 end end end
  print('blade-half pixels',n)
  print('speck x155y164 alpha',pc.rgbaA(img:getPixel(155,164)))
end
