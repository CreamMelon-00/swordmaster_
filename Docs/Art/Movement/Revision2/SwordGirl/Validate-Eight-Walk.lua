local root=assert(app.params.root):gsub('\\','/')
local dir=root..'/Docs/Art/Movement/Revision2/SwordGirl'
local pc=app.pixelColor
local idle=Image{fromFile=root..'/Assets/Game/Resources/SwordGirl/Animations/idle/frame-01.png'}
local palette={}
for p in idle:pixels() do if pc.rgbaA(p())>0 then palette[p()]=true end end
local sprite=app.open(dir..'/SwordGirl-walk-eight.aseprite')
assert(sprite.width==256 and sprite.height==224)
assert(#sprite.frames==8 and #sprite.tags==1 and sprite.tags[1].name=='Move')
local frames={}
local function components(im)
  local visited={}
  local sizes={}
  for y=0,223 do for x=0,255 do
    local id=y*256+x
    if not visited[id] and pc.rgbaA(im:getPixel(x,y))>0 then
      local qx,qy={x},{y}
      local head=1
      visited[id]=true
      local size=0
      while head<=#qx do
        local cx,cy=qx[head],qy[head]
        head=head+1
        size=size+1
        for dy=-1,1 do for dx=-1,1 do
          local nx,ny=cx+dx,cy+dy
          if nx>=0 and nx<256 and ny>=0 and ny<224 then
            local nextId=ny*256+nx
            if not visited[nextId] and pc.rgbaA(im:getPixel(nx,ny))>0 then
              visited[nextId]=true
              qx[#qx+1]=nx;qy[#qy+1]=ny
            end
          end
        end end
      end
      sizes[#sizes+1]=size
    end
  end end
  table.sort(sizes,function(a,b) return a>b end)
  return sizes
end
for i=1,8 do
  assert(math.abs(sprite.frames[i].duration-.12)<.001,'Frame duration '..i)
  local actual=Image(256,224,ColorMode.RGB)
  actual:drawSprite(sprite,i)
  local png=Image{fromFile=dir..string.format('/frame-%02d.png',i)}
  assert(actual:isEqual(png),'PNG/source mismatch '..i)
  frames[i]=png
  local l,t,r,b=256,224,-1,-1
  local colors={}
  local ground=0
  for p in png:pixels() do
    local c=p()
    local a=pc.rgbaA(c)
    assert(a==0 or a==255,'Semitransparent pixel '..i)
    if a>0 then
      assert(palette[c],'Non-original palette color '..i)
      colors[c]=true
      l=math.min(l,p.x);t=math.min(t,p.y)
      r=math.max(r,p.x);b=math.max(b,p.y)
      if p.y==202 then ground=ground+1 end
    end
  end
  local count=0
  for _ in pairs(colors) do count=count+1 end
  assert(b==202 and ground>=8,'Lost support-foot baseline '..i)
  local groups=components(png)
  assert(#groups==1,'Disconnected sprite silhouette '..i..' groups='..#groups)
  print(string.format('%02d bbox=%d,%d..%d,%d colors=%d ground=%d connected=%d',i,l,t,r,b,count,ground,groups[1]))
end
for i=1,8 do
  local nextFrame=frames[i%8+1]
  assert(not frames[i]:isEqual(nextFrame),'Duplicate adjacent frames '..i)
end
print('All eight 256x224 native frames pass palette/alpha/support/continuity/round-trip checks.')
