local root=assert(app.params.root):gsub('\\','/')
local img=Image{fromFile=root..'/Docs/Art/Movement/Revision2/EnemyStudent/pose-sheet-study.png'}
local pc=app.pixelColor
local w,h=img.width,img.height
local visited={}
local components={}
local function alpha(x,y) return pc.rgbaA(img:getPixel(x,y))>128 end
for y=0,h-1 do for x=0,w-1 do
  local key=y*w+x+1
  if not visited[key] and alpha(x,y) then
    local qx,qy={x},{y}
    visited[key]=true
    local head,tail=1,1
    local minx,miny,maxx,maxy=x,y,x,y
    while head<=tail do
      local px,py=qx[head],qy[head]
      head=head+1
      minx=math.min(minx,px); maxx=math.max(maxx,px)
      miny=math.min(miny,py); maxy=math.max(maxy,py)
      for yy=py-1,py+1 do for xx=px-1,px+1 do
        if xx>=0 and xx<w and yy>=0 and yy<h then
          local k=yy*w+xx+1
          if not visited[k] and alpha(xx,yy) then
            visited[k]=true
            tail=tail+1
            qx[tail],qy[tail]=xx,yy
          end
        end
      end end
    end
    if tail>200 then components[#components+1]={size=tail,minx=minx,miny=miny,maxx=maxx,maxy=maxy} end
  end
end end
table.sort(components,function(a,b)return a.size>b.size end)
print('components',#components)
for i=1,math.min(20,#components) do
  local c=components[i]
  print(i,c.size,c.minx,c.miny,c.maxx,c.maxy)
end
