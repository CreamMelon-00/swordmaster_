local root=assert(app.params.root):gsub('\\','/')
local pc=app.pixelColor
local img=Image{fromFile=root..'/Docs/Art/Movement/Revision2/EnemyStudent/Candidate-4-v2/frame-02.png'}
for y=158,173 do
  local row=''
  for x=145,171 do
    local c=img:getPixel(x,y)
    if pc.rgbaA(c)==0 then row=row..' ' else
      local r,g,b=pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c)
      if b>r then row=row..'P' elseif r>150 then row=row..'S' else row=row..'B' end
    end
  end
  print(y,row)
  if y>=164 then
    for x=145,171 do
      local c=img:getPixel(x,y)
      if pc.rgbaA(c)>0 then print(x,y,pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c)) end
    end
  end
end
print('below-blade outliers')
for y=163,185 do for x=145,205 do
  local c=img:getPixel(x,y)
  if pc.rgbaA(c)>0 then
    local line=145+(x-147)*.59
    if y>line+13 and (x>155 or y<177) then
      print(x,y,pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c))
    end
  end
end end
