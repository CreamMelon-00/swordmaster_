local out='C:/Users/user/Documents/SwordMasterStory/Aseprite_Character/Enemy_Reactions'
local s=app.open(out..'/Enemy_AllAttacks3_Reactions2.aseprite')
local pc=app.pixelColor
local report=assert(io.open(out..'/measurements.txt','w'))
for _,n in ipairs({1,45,118,119,120}) do
 local im=Image(256,224,ColorMode.RGB);im:drawSprite(s,n)
 report:write('Frame '..n..'\n')
 for _,range in ipairs({{65,119},{120,145},{146,166},{167,201}}) do
  local x0,x1,y0,y1,count=256,0,224,0,0
  for y=range[1],range[2] do for x=0,255 do local p=im:getPixel(x,y)
   local r,g,b,a=pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p),pc.rgbaA(p)
   if a>0 and not (b>g*1.04 and b>r*.88) then x0=math.min(x0,x);x1=math.max(x1,x);y0=math.min(y0,y);y1=math.max(y1,y);count=count+1 end
  end end
  report:write(string.format('y %d..%d body width %d; pixels %d\n',range[1],range[2],x1-x0+1,count))
 end
end
report:close()
