local root=assert(app.params.root):gsub('\\','/')
local dir=root..'/Docs/Art/Movement/Revision2/EnemyOpposite'
local pc=app.pixelColor
local orig=Image{fromFile=root..'/Docs/Art/Movement/Revision2/EnemyStudent/Candidate-4-v4/frame-04.png'}
local gen=Image{fromFile=dir..'/imagegen-crossed-256.png'}
local idle=Image{fromFile=root..'/Assets/Game/Resources/EnemyStudent/Animations/idle/frame-01.png'}
local pal,seen={},{}
for p in idle:pixels() do
  local c=p()
  if pc.rgbaA(c)==255 and not seen[c] then seen[c]=true; pal[#pal+1]=c end
end
for p in orig:pixels() do
  local c=p()
  if pc.rgbaA(c)==255 and not seen[c] then seen[c]=true; pal[#pal+1]=c end
end
local function near(c)
  local rr,gg,bb=pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c)
  local best,bestd=nil,1000000
  for _,v in ipairs(pal) do
    local dr,dg,db=rr-pc.rgbaR(v),gg-pc.rgbaG(v),bb-pc.rgbaB(v)
    local d=dr*dr+dg*dg+db*db
    if d<bestd then best,bestd=v,d end
  end
  return best
end
local out=Image(256,224,ColorMode.RGB)
out:drawImage(orig,Point(0,0))
-- Remove only the old legs. The guard and blade stay from the exact original art.
for y=150,205 do for x=63,166 do
  if x<142 or y>=174 then out:drawPixel(x,y,pc.rgba(0,0,0,0)) end
end end
-- Fit the generated crossed-leg study to the original y201 contact line.
for y=150,201 do for x=64,165 do
  if x<142 or y>=174 then
    local gy=math.floor(150+(y-150)*54/51+0.5)
    local c=gen:getPixel(x,gy)
    if pc.rgbaA(c)>=128 then out:drawPixel(x,y,near(c)) end
  end
end end
-- Restore the original skirt/upper-body silhouette above the cut.
for y=143,151 do for x=80,145 do
  local c=orig:getPixel(x,y)
  if pc.rgbaA(c)>0 then out:drawPixel(x,y,c) end
end end
-- Narrow the near boot to match the original boot's compact silhouette.
local clear=pc.rgba(0,0,0,0)
local edge=pc.rgba(51,25,30,255)
for y=195,201 do for x=72,73 do out:drawPixel(x,y,clear) end end
for y=193,201 do for x=98,100 do out:drawPixel(x,y,clear) end end
for x=74,82 do out:drawPixel(x,201,clear); out:drawPixel(x,200,edge) end
for x=94,97 do out:drawPixel(x,201,clear); out:drawPixel(x,200,edge) end
out:saveAs(dir..'/opposite-contact-v2.png')
local frame1=Image{fromFile=root..'/Docs/Art/Movement/Revision2/EnemyStudent/Candidate-4-v4/frame-01.png'}
local sheet=Image(2304,672,ColorMode.RGB)
for p in sheet:pixels() do p(pc.rgba(34,31,39,255)) end
local frames={frame1,orig,out}
for i,img in ipairs(frames) do
  for y=0,223 do for x=0,255 do
    local c=img:getPixel(x,y)
    if pc.rgbaA(c)>0 then
      for dy=0,2 do for dx=0,2 do sheet:drawPixel((i-1)*768+x*3+dx,y*3+dy,c) end end
    end
  end end
end
sheet:saveAs(dir..'/comparison-v2.png')
print('palette',#pal,'contact v2')
