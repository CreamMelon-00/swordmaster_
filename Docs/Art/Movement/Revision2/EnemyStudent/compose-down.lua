local root=assert(app.params.root):gsub('\\','/')
local dir=root..'/Docs/Art/Movement/Revision2/EnemyStudent'
local pc=app.pixelColor
local src=Image{fromFile=dir..'/opposite-down-reference.png'}
local scaled=Image(256,224,ColorMode.RGB)
for p in scaled:pixels() do
  local x=math.floor((p.x+.5)*src.width/256)
  local y=math.floor((p.y+.5)*src.height/224)
  p(src:getPixel(x,y))
end
scaled:saveAs(dir..'/opposite-down-raw-256.png')
local orig=Image{fromFile=dir..'/Candidate-4-v4/frame-04.png'}
local idle=Image{fromFile=root..'/Assets/Game/Resources/EnemyStudent/Animations/idle/frame-01.png'}
local pal,seen={},{ } 
for p in idle:pixels() do local c=p(); if pc.rgbaA(c)==255 and not seen[c] then seen[c]=true; pal[#pal+1]=c end end
local function near(c)
  local rr,gg,bb=pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c)
  local best,bestd=nil,math.huge
  for _,v in ipairs(pal) do
    local dr,dg,db=rr-pc.rgbaR(v),gg-pc.rgbaG(v),bb-pc.rgbaB(v)
    local d=3*dr*dr+5*dg*dg+2*db*db
    if d<bestd then best,bestd=v,d end
  end
  return best
end
local out=Image(256,224,ColorMode.RGB)
out:drawImage(orig,Point(0,0))
local clear=pc.rgba(0,0,0,0)
for y=150,205 do for x=63,166 do
  if x<142 or y>=174 then out:drawPixel(x,y,clear) end
end end
-- Keep the original torso, skirt and exact sword. The generated full body is
-- used only as a reference for its coherent crossed lower-body down pose.
for y=150,201 do for x=64,165 do
  if x<142 or y>=174 then
    local gy=math.floor(150+(y-150)*54/51+.5)
    local c=scaled:getPixel(x,gy)
    if pc.rgbaA(c)>=128 then out:drawPixel(x,y,near(c)) end
  end
end end
for y=143,151 do for x=80,145 do
  local c=orig:getPixel(x,y)
  if pc.rgbaA(c)>0 then out:drawPixel(x,y,c) end
end end
for y=163,170 do for x=150,160 do out:drawPixel(x,y,clear) end end
-- Finish the grounded sole on the shared y=201 baseline. The rear heel stays
-- raised one pixel in this weight-bearing frame.
for x=90,125 do
  local c=out:getPixel(x,200)
  if pc.rgbaA(c)>0 then out:drawPixel(x,201,c) end
end
out:saveAs(dir..'/opposite-down-v1.png')
local contact=Image{fromFile=root..'/Docs/Art/Movement/Revision2/EnemyOpposite/opposite-contact-v2.png'}
local sheet=Image(1536,448,ColorMode.RGB)
for p in sheet:pixels() do p(pc.rgba(34,31,39,255)) end
for i,img in ipairs({contact,out}) do
  for y=0,223 do for x=0,255 do
    local c=img:getPixel(x,y)
    if pc.rgbaA(c)>0 then
      for dy=0,1 do for dx=0,1 do sheet:drawPixel((i-1)*512+x*2+dx,y*2+dy,c) end end
    end
  end end
end
sheet:saveAs(dir..'/opposite-contact-down-compare.png')
print('down frame draft written')
