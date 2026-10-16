-- Authored pixel correction for slash-3 frame 06, callable from the original generator.
-- Image edits use the SwordGirl palette and preserve canvas, body, pose and timing.
local M={}
function M.apply(source)
 local im=Image(source)
local pc=app.pixelColor
local function c(r,g,b) return pc.rgba(r,g,b,255) end
local P={
 B=c(51,25,30), b=c(70,38,41), m=c(109,66,61),
 W=c(250,243,236), C=c(198,186,191), D=c(124,108,122),
 H=c(253,217,201), S=c(253,213,196), s=c(248,187,170),
 d=c(243,168,152), e=c(163,112,89),
}
local function row(y,x,str)
 for i=1,#str do
  local k=str:sub(i,i)
  if k=='_' then im:drawPixel(x+i-1,y,0)
  elseif k~='.' then assert(P[k],k);im:drawPixel(x+i-1,y,P[k]) end
 end
end
-- End the sleeve at a cuff and give the wrist a closed, shaded hand.
-- Knuckles are on the guard side; the thumb crosses the near side of the fist.
row(113,126,'mmb')
row(114,125,'DCBBB')
row(115,125,'DWHHSsb')
row(116,125,'CWHSSSsb')
row(117,124,'DCWHSSSSb')
row(118,124,'DWSsdSSsb')
row(119,124,'DWSssSSsb')
row(120,123,'DCWsdSSdb')
row(121,122,'bDCsssdBB')
row(122,121,'bDessdeB')
-- Remove the previous drooping cuff/skin tail below the new fist.
row(123,121,'bBBBBBB____')
row(124,122,'_________')
row(125,123,'________')
row(126,125,'______')
-- Rebuild only the straight rigid blade. Its 72 px projection at 10 degrees
-- bridges frame 05 (73 px / 0 deg) and frame 07 (70 px / 20 deg).
for y=115,125 do for x=137,175 do im:drawPixel(x,y,0) end end
-- Also clear a regenerated full-length blade before drawing its exact contour.
for y=114,135 do for x=138,213 do im:drawPixel(x,y,0) end end
local ax,ay=138,118
local angle=math.rad(10);local ux,uy=math.cos(angle),math.sin(angle)
local length=72
local border=c(70,38,41)
local light=c(250,243,236)
local mid=c(198,186,191)
local shade=c(124,108,122)
for y=114,135 do for x=137,212 do
 local dx,dy=x-ax,y-ay
 local along=dx*ux+dy*uy
 local across=-dx*uy+dy*ux
 if along>=-1 and along<=length+1 then
  local half=2.1*math.max(0,math.min(1,(length+1-along)/9))
  if math.abs(across)<=half+1 then
   local color=border
   if along>=0 and along<=length and math.abs(across)<=half then
    color=across<-.2 and light or (across<.9 and mid or shade)
   end
   im:drawPixel(x,y,color)
  end
 end
end end
 return im
end
return M
