-- Native Aseprite retargeting. Preserve the drawn weapon and its occlusion behind
-- the head; the visible blade fragment is not a separate complete weapon.
local M={};local pc=app.pixelColor
local function clamp(v,a,b) return math.max(a,math.min(b,v)) end
local function round(v) return math.floor(v+.5) end
local function blonde(p)
 local r,g,b=pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p)
 return pc.rgbaA(p)>0 and r>160 and g>125 and b>85 and r>g and g>b
end
function M.clean(im,options)
 local top
 for y=50,125 do local count=0
  for x=55,150 do if blonde(im:getPixel(x,y)) then count=count+1 end end
  if count>=8 then top=y;break end
 end
 assert(top,'Missing head landmark: '..options.label)
 local x0,x1=999,0
 for y=top+6,top+25 do for x=55,150 do if blonde(im:getPixel(x,y)) then x0=math.min(x0,x);x1=math.max(x1,x) end end end
 local left,right=999,0
 for y=197,202 do for x=0,255 do if pc.rgbaA(im:getPixel(x,y))>0 then left=math.min(left,x);right=math.max(right,x) end end end
 assert(right>left,'Missing foot landmarks: '..options.label)
 local center=(x0+x1)/2;local neck=top+40;local targetNeck=options.top+40
 local stanceScale=clamp((options.stance or 89)/(right-left),.84,1.16)
 local out=Image(256,224,ColorMode.RGB)
 for y=0,223 do
  local sy=y<=targetNeck and y-options.top+top or neck+(y-targetNeck)*(202-neck)/(202-targetNeck)
  local t=clamp((sy-neck)/(202-neck),0,1);local scale=1+(stanceScale-1)*t
  local shift=(options.headX-center)*(1-t)+(56-left*stanceScale)*t
  for x=0,255 do local sx,yy=round((x-shift)/scale),round(sy)
   if sx>=0 and sx<256 and yy>=0 and yy<224 then out:drawPixel(x,y,im:getPixel(sx,yy)) end
  end
 end
 print(string.format('CLEAN %s: head %d -> %d, center %.1f -> %.1f, feet %d..%d -> span %d; weapon occlusion retained',options.label,top,options.top,center,options.headX,left,right,options.stance or 89))
 return out
end
return M
