-- Extract the eight connected, consistent full-body study poses as individual
-- Aseprite cels; align head/feet and map to the shipped 80-color palette.
local root=assert(app.params.root):gsub('\\','/')
local dir=root..'/Docs/Art/Movement/Revision2/EnemyStudent'
local study=Image{fromFile=dir..'/pose-sheet-study.png'}
local idle=Image{fromFile=root..'/Assets/Game/Resources/EnemyStudent/Animations/idle/frame-01.png'}
local sword=Image{fromFile=root..'/Assets/Game/Resources/EnemyStudent/Animations/move/frame-01.png'}
local pc=app.pixelColor
local w,h=study.width,study.height
local palette,seen={},{}
for p in idle:pixels() do
  local c=p()
  if pc.rgbaA(c)==255 and not seen[c] then
    seen[c]=true
    palette[#palette+1]={color=c,r=pc.rgbaR(c),g=pc.rgbaG(c),b=pc.rgbaB(c)}
  end
end
local function match(r,g,b)
  local best,score=nil,math.huge
  for _,v in ipairs(palette) do
    local dr=r-v.r; local dg=g-v.g; local db=b-v.b
    local d=3*dr*dr+5*dg*dg+2*db*db
    if d<score then best,score=v.color,d end
  end
  return best
end
local visited,components={},{}
local function visible(x,y) return pc.rgbaA(study:getPixel(x,y))>128 end
for y=0,h-1 do for x=0,w-1 do
  local key=y*w+x+1
  if not visited[key] and visible(x,y) then
    local qx,qy={x},{y}
    visited[key]=true
    local head,tail=1,1
    local minx,miny,maxx,maxy=x,y,x,y
    while head<=tail do
      local xx,yy=qx[head],qy[head]
      head=head+1
      minx=math.min(minx,xx); miny=math.min(miny,yy)
      maxx=math.max(maxx,xx); maxy=math.max(maxy,yy)
      for ny=yy-1,yy+1 do for nx=xx-1,xx+1 do
        if nx>=0 and nx<w and ny>=0 and ny<h then
          local k=ny*w+nx+1
          if not visited[k] and visible(nx,ny) then
            visited[k]=true
            tail=tail+1
            qx[tail],qy[tail]=nx,ny
          end
        end
      end end
    end
    if tail>10000 then
      components[#components+1]={qx=qx,qy=qy,size=tail,minx=minx,miny=miny,maxx=maxx,maxy=maxy}
    end
  end
end end
assert(#components==8,'Expected eight complete pose components')
table.sort(components,function(a,b)
  if math.abs(a.miny-b.miny)>300 then return a.miny<b.miny end
  return a.minx<b.minx
end)

local frames={}
for i,c in ipairs(components) do
  local headSum,headCount=0,0
  for j=1,c.size do
    local x,y=c.qx[j],c.qy[j]
    if y>=c.miny+25 and y<=c.miny+120 and x<=c.minx+210 then
      headSum=headSum+x; headCount=headCount+1
    end
  end
  assert(headCount>100)
  local headX=headSum/headCount
  local scale=132/(c.maxy-c.miny)
  local bins={}
  for j=1,c.size do
    local sx,sy=c.qx[j],c.qy[j]
    local dx=math.floor(110+(sx-headX)*scale+0.5)
    local dy=math.floor(69+(sy-c.miny)*scale+0.5)
    if dx>=0 and dx<256 and dy>=0 and dy<224 then
      local key=dy*256+dx+1
      local b=bins[key] or {0,0,0,0}
      local col=study:getPixel(sx,sy)
      local a=pc.rgbaA(col)/255
      b[1]=b[1]+a*pc.rgbaR(col)
      b[2]=b[2]+a*pc.rgbaG(col)
      b[3]=b[3]+a*pc.rgbaB(col)
      b[4]=b[4]+a
      bins[key]=b
    end
  end
  local img=Image(256,224,ColorMode.RGB)
  for key,b in pairs(bins) do
    if b[4]>=1.1 then
      local idx=key-1
      local x,y=idx%256,math.floor(idx/256)
      img:drawPixel(x,y,match(b[1]/b[4],b[2]/b[4],b[3]/b[4]))
    end
  end
  frames[i]=img
  print(i,c.size,c.minx,c.miny,c.maxx,c.maxy,'head',headX,'scale',scale)
end

-- The study sheet's lower row repeated its first step. Reverse the complete
-- connected leg silhouette under the skirt for the opposite-support step.
for i=5,8 do
  local img=frames[i]
  local first=frames[i-4]
  for y=158,201 do for x=64,180 do
    local c=first:getPixel(244-x,y)
    if y>=167 then
      img:drawPixel(x,y,c)
    elseif y>=158 then
      local current=img:getPixel(x,y)
      local r,g,b=pc.rgbaR(current),pc.rgbaG(current),pc.rgbaB(current)
      local skirt=pc.rgbaA(current)>0 and r<165 and g<130 and b<115
      if not skirt then img:drawPixel(x,y,c) end
    end
  end end
end

for i,img in ipairs(frames) do
  -- Remove the generated blade beyond the hands, then place one unchanged
  -- native sword. This also repairs truncated tips in 4-column source sheet.
  for y=130,223 do for x=138,255 do
    if (x>=175) or (x>=158 and y<=190) or
       (x>=138 and x<158 and y>=135 and y<=163) then
      img:drawPixel(x,y,pc.rgba(0,0,0,0))
    end
  end end
  for y=136,205 do for x=138,235 do
    local line=145+(x-147)*.59
    if math.abs(y-line)<=13 then
      local c=sword:getPixel(x,y)
      if pc.rgbaA(c)==255 then img:drawPixel(x,y,c) end
    end
  end end
  -- A disconnected three-pixel remnant of the generated blade survives below
  -- the guard after resizing. It is outside the native blade silhouette.
  for y=163,170 do for x=150,160 do
    img:drawPixel(x,y,pc.rgba(0,0,0,0))
  end end
  assert(pc.rgbaA(img:getPixel(155,164))==0,'Disconnected fleck did not clear')
  img:saveAs(dir..'/sheet-frame-'..string.format('%02d',i)..'.png')
end

local sprite=Sprite(256,224,ColorMode.RGB)
local layer=sprite.layers[1]
layer.name='Full character'
local init=layer:cel(1)
if init then sprite:deleteCel(init) end
for i,img in ipairs(frames) do
  if i>1 then sprite:newEmptyFrame(i) end
  sprite:newCel(layer,i,img,Point(0,0))
  sprite.frames[i].duration=.11
end
local tag=sprite:newTag(1,8); tag.name='Walk'
sprite:saveAs(dir..'/EnemyStudent-walk-sheet.aseprite')

local sheet=Image(2048,224,ColorMode.RGB)
for p in sheet:pixels() do p(pc.rgba(34,31,39,255)) end
for i,img in ipairs(frames) do sheet:drawImage(img,Point((i-1)*256,0)) end
local big=Image(6144,672,ColorMode.RGB)
for p in big:pixels() do p(sheet:getPixel(math.floor(p.x/3),math.floor(p.y/3))) end
big:saveAs(dir..'/EnemyStudent-walk-sheet-preview.png')

local anim=Sprite(768,672,ColorMode.RGB)
local animLayer=anim.layers[1]; animLayer.name='Preview'
local old=animLayer:cel(1); if old then anim:deleteCel(old) end
for i,img in ipairs(frames) do
  local render=Image(768,672,ColorMode.RGB)
  for p in render:pixels() do
    local c=img:getPixel(math.floor(p.x/3),math.floor(p.y/3))
    if pc.rgbaA(c)==0 then c=pc.rgba(34,31,39,255) end
    p(c)
  end
  if i>1 then anim:newEmptyFrame(i) end
  anim:newCel(animLayer,i,render,Point(0,0))
  anim.frames[i].duration=.11
end
anim:saveAs(dir..'/EnemyStudent-walk-sheet-preview.gif')
print('Eight full-body sheet frames, 110ms each')
