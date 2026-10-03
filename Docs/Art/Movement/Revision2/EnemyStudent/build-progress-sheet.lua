local root=assert(app.params.root):gsub('\\','/')
local dir=root..'/Docs/Art/Movement/Revision2/EnemyStudent/Review-5'
local pc=app.pixelColor
local sheet=Image(2304,1344,ColorMode.RGB)
for p in sheet:pixels() do p(pc.rgba(34,31,39,255)) end
for i=1,6 do
  local img=Image{fromFile=dir..'/frame-'..string.format('%02d',i)..'.png'}
  assert(img.width==256 and img.height==224)
  local col=(i-1)%3
  local row=math.floor((i-1)/3)
  for y=0,223 do for x=0,255 do
    local c=img:getPixel(x,y)
    if pc.rgbaA(c)>0 then
      for dy=0,2 do for dx=0,2 do
        sheet:drawPixel(col*768+x*3+dx,row*672+y*3+dy,c)
      end end
    end
  end end
end
sheet:saveAs(dir..'/EnemyStudent-walk-1-6-sheet.png')
local overview=Image(3072,1344,ColorMode.RGB)
for p in overview:pixels() do p(pc.rgba(34,31,39,255)) end
for i=1,8 do
  if i~=7 then
    local img=Image{fromFile=dir..'/frame-'..string.format('%02d',i)..'.png'}
    local col=(i-1)%4
    local row=math.floor((i-1)/4)
    for y=0,223 do for x=0,255 do
      local c=img:getPixel(x,y)
      if pc.rgbaA(c)>0 then
        for dy=0,2 do for dx=0,2 do
          overview:drawPixel(col*768+x*3+dx,row*672+y*3+dy,c)
        end end
      end
    end end
  end
end
overview:saveAs(dir..'/EnemyStudent-walk-1-6-8-overview.png')
print('progress sheets 1..6 and 1..6/8')
