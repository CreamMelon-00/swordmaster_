-- Aseprite-only cleanup. Keep the head at its drawn scale, retarget body height and
-- foot spacing separately, and rebuild a rigid blade after the body warp.
local M={};local pc=app.pixelColor
local function clamp(v,a,b) return math.max(a,math.min(b,v)) end
local function round(v) return math.floor(v+.5) end
local function silver(p)
 local r,g,b=pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p)
 return pc.rgbaA(p)>0 and r>100 and g>90 and b>g and math.abs(r-b)<45
end
local function blade(im)
 local w,h=im.width,im.height;local mask={};local candidates={}
 for it in im:pixels() do if silver(it()) then mask[it.y*w+it.x]=true end end
 for y=0,h-1 do for x=0,w-1 do local k=y*w+x
  if mask[k] then local q={k};mask[k]=nil;local j=1;local sx,sy=0,0
   while j<=#q do local z=q[j];j=j+1;local yy=math.floor(z/w);local xx=z-yy*w;sx=sx+xx;sy=sy+yy
    for dy=-1,1 do for dx=-1,1 do local nx,ny=xx+dx,yy+dy;local n=ny*w+nx
     if nx>=0 and nx<w and ny>=0 and ny<h and mask[n] then mask[n]=nil;q[#q+1]=n end
    end end
   end
   if #q>=20 then
    local cx,cy=sx/#q,sy/#q;local xx,xy,yy=0,0,0
    for _,z in ipairs(q) do local py=math.floor(z/w);local px=z-py*w;local a,b=px-cx,py-cy;xx=xx+a*a;xy=xy+a*b;yy=yy+b*b end
    local angle=.5*math.atan(2*xy,xx-yy);local ux,uy=math.cos(angle),math.sin(angle);local lo,hi=999,-999
    for _,z in ipairs(q) do local py=math.floor(z/w);local px=z-py*w;local t=(px-cx)*ux+(py-cy)*uy;lo=math.min(lo,t);hi=math.max(hi,t) end
    if hi-lo>25 then candidates[#candidates+1]={cx=cx,cy=cy,ux=ux,uy=uy,lo=lo,hi=hi,len=hi-lo} end
   end
  end
 end end
 table.sort(candidates,function(a,b) return a.len>b.len end)
 local c=assert(candidates[1],'Cannot identify blade')
 local ax,ay=c.cx+c.lo*c.ux,c.cy+c.lo*c.uy;local bx,by=c.cx+c.hi*c.ux,c.cy+c.hi*c.uy
 if (ax-103)^2+(ay-139)^2>(bx-103)^2+(by-139)^2 then ax,ay,bx,by=bx,by,ax,ay end
 local len=math.sqrt((bx-ax)^2+(by-ay)^2)
 return {x=ax,y=ay,ux=(bx-ax)/len,uy=(by-ay)/len,len=len}
end
local function headLandmarks(im)
 local top
 for y=50,115 do local count=0
  for x=55,145 do local p=im:getPixel(x,y);local r,g,b=pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p)
   if pc.rgbaA(p)>0 and r>160 and g>125 and b>85 and r>g and g>b then count=count+1 end
  end
  if count>=8 then top=y;break end
 end
 assert(top,'Missing head landmark')
 local x0,x1=999,0
 for y=top+6,top+25 do for x=55,145 do local p=im:getPixel(x,y);local r,g,b=pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p)
  if pc.rgbaA(p)>0 and r>160 and g>125 and b>85 and r>g and g>b then x0=math.min(x0,x);x1=math.max(x1,x) end
 end end
 local left,right=999,0
 for y=197,202 do for x=0,255 do if pc.rgbaA(im:getPixel(x,y))>0 then left=math.min(left,x);right=math.max(right,x) end end end
 return top,(x0+x1)/2,left,right
end
function M.clean(im,options)
 local w,h=im.width,im.height;local weapon=blade(im);local body=Image(im)
 -- Remove silver blade and its dark contour, retaining the gold crossguard and hands.
 for it in body:pixels() do local dx,dy=it.x-weapon.x,it.y-weapon.y
  local along=dx*weapon.ux+dy*weapon.uy;local across=dx*(-weapon.uy)+dy*weapon.ux
  if along>=-1.3 and along<=weapon.len+8 and math.abs(across)<(options.foreshortened and 10 or 6) then it(0) end
 end
 local headTop,headX,footLeft,footRight=headLandmarks(body)
 local neck=headTop+40;local targetTop=options.top or 73;local targetNeck=targetTop+40
 local stanceScale=clamp(89/(footRight-footLeft),.84,1.16)
 local headTarget=options.headX or 99
 local function forwardY(y) return y<=neck and y-headTop+targetTop or targetNeck+(y-neck)*(202-targetNeck)/(202-neck) end
 local function transformX(x,y)
  local t=clamp((y-neck)/(202-neck),0,1)
  local scale=1+(stanceScale-1)*t
  local shift=(headTarget-headX)*(1-t)+(56-footLeft*stanceScale)*t
  return x*scale+shift
 end
 local out=Image(w,h,ColorMode.RGB)
 for y=0,h-1 do
  local sy=y<=targetNeck and y-targetTop+headTop or neck+(y-targetNeck)*(202-neck)/(202-targetNeck)
  local t=clamp((sy-neck)/(202-neck),0,1);local scale=1+(stanceScale-1)*t
  local shift=(headTarget-headX)*(1-t)+(56-footLeft*stanceScale)*t
  for x=0,w-1 do local sx=round((x-shift)/scale);local yy=round(sy)
   if sx>=0 and sx<w and yy>=0 and yy<h then out:drawPixel(x,y,body:getPixel(sx,yy)) end
  end
 end
 local ax,ay=transformX(weapon.x,weapon.y),forwardY(weapon.y)
 local angle=options.angle and math.rad(options.angle) or math.atan(weapon.uy,weapon.ux)
 local ux,uy=math.cos(angle),math.sin(angle);local length=options.length or 73
 local shade=pc.rgba(118,104,120,255);local border=pc.rgba(58,43,52,255)
 local light=pc.rgba(246,235,224,255);local mid=pc.rgba(188,175,188,255)
 local width=options.foreshortened and 5.2 or 2.1
 for y=0,h-1 do for x=0,w-1 do
  local dx,dy=x-ax,y-ay;local along=dx*ux+dy*uy;local across=-dx*uy+dy*ux
  if along>=-1 and along<=length+1 then
   local half=width*clamp((length+1-along)/9,0,1)
   if math.abs(across)<=half+1 then
    local col=border
    if along>=0 and along<=length and math.abs(across)<=half then col=across<-.2 and light or (across<.9 and mid or shade) end
    out:drawPixel(x,y,col)
   end
  end
 end end
 -- Drop disconnected remnants of the replaced blade, never leave a second tip.
 local mask,components={},{ }
 for it in out:pixels() do if pc.rgbaA(it())>0 then mask[it.y*w+it.x]=true end end
 for y=0,h-1 do for x=0,w-1 do local k=y*w+x
  if mask[k] then local q={k};mask[k]=nil;local j=1
   while j<=#q do local z=q[j];j=j+1;local yy=math.floor(z/w);local xx=z-yy*w
    for dy=-1,1 do for dx=-1,1 do local nx,ny=xx+dx,yy+dy;local n=ny*w+nx
     if nx>=0 and nx<w and ny>=0 and ny<h and mask[n] then mask[n]=nil;q[#q+1]=n end
    end end
   end
   components[#components+1]=q
  end
 end end
 table.sort(components,function(a,b) return #a>#b end)
 for i=2,#components do
  assert(#components[i]<100,'Unexpected disconnected body or blade: '..options.label)
  for _,z in ipairs(components[i]) do out:drawPixel(z%w,math.floor(z/w),0) end
 end
 print(string.format('CLEAN %s: head %d -> %d, center %.1f -> %.1f, feet %d..%d, blade %.1fdeg length %d',options.label,headTop,targetTop,headX,headTarget,footLeft,footRight,math.deg(angle),length))
 return out
end
return M
