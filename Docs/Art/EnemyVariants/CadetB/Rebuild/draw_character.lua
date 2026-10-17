-- New Cadet B artwork. No source sprite, transferred head or recolored body.
-- Contours, costume seams, hair clusters, hands and weapon are drawn here in
-- Aseprite at native pixel resolution. Coordinates describe the new body.
local M={}
local pc=app.pixelColor
local function rgb(r,g,b) return pc.rgba(r,g,b,255) end
local C={
 outline=rgb(34,19,27), ink=rgb(54,33,39),
 coat0=rgb(65,41,42),coat1=rgb(83,53,48),coat2=rgb(106,72,62),coat3=rgb(129,91,72),coat4=rgb(153,112,82),
 gold0=rgb(103,83,55),gold1=rgb(153,124,76),gold2=rgb(196,163,99),gold3=rgb(224,200,139),
 olive0=rgb(71,70,51),olive1=rgb(111,109,72),olive2=rgb(150,145,95),
 shirt0=rgb(145,132,132),shirt1=rgb(207,199,190),shirt2=rgb(241,226,203),
 trouser0=rgb(48,42,52),trouser1=rgb(65,57,67),trouser2=rgb(88,77,85),trouser3=rgb(115,101,112),trouser4=rgb(139,124,133),
 boot0=rgb(53,33,37),boot1=rgb(76,48,45),boot2=rgb(105,69,57),boot3=rgb(139,96,73),boot4=rgb(170,125,89),
 hair0=rgb(85,57,46),hair1=rgb(119,79,57),hair2=rgb(157,109,72),hair3=rgb(191,142,97),hair4=rgb(218,173,122),hair5=rgb(239,200,146),
 skin0=rgb(133,81,66),skin1=rgb(183,118,85),skin2=rgb(226,163,117),skin3=rgb(250,200,157),skin4=rgb(255,222,188),
 steel0=rgb(73,65,80),steel1=rgb(116,108,129),steel2=rgb(177,172,189),steel3=rgb(229,228,235),steel4=rgb(251,243,228),
 grip0=rgb(41,32,40),grip1=rgb(82,64,67),grip2=rgb(122,97,89),eye=rgb(84,87,59),
}
M.colors=C
local function round(x) return math.floor(x+.5) end
local function put(im,x,y,c) x,y=round(x),round(y);if x>=0 and x<im.width and y>=0 and y<im.height then im:drawPixel(x,y,c) end end
local function line(im,a,b,c)
 assert(a[1]==a[1] and a[2]==a[2] and b[1]==b[1] and b[2]==b[2],'Invalid contour coordinate')
 local x,y,x2,y2=round(a[1]),round(a[2]),round(b[1]),round(b[2])
 local dx,dy=math.abs(x2-x),-math.abs(y2-y);local sx=x<x2 and 1 or -1;local sy=y<y2 and 1 or -1;local e=dx+dy
 while true do put(im,x,y,c);if x==x2 and y==y2 then break end;local q=2*e;if q>=dy then e=e+dy;x=x+sx end;if q<=dx then e=e+dx;y=y+sy end end
end
local function polygon(im,pts,c,outline)
 local y1,y2=999,-999
 for _,q in ipairs(pts) do y1=math.min(y1,q[2]);y2=math.max(y2,q[2]) end
 for y=math.ceil(y1),math.floor(y2) do
  local xs={};local yy=y+.01
  for i=1,#pts do local a,b=pts[i],pts[i%#pts+1]
   if (a[2]<=yy and b[2]>yy) or (b[2]<=yy and a[2]>yy) then xs[#xs+1]=a[1]+(yy-a[2])*(b[1]-a[1])/(b[2]-a[2]) end
  end
  table.sort(xs);for i=1,#xs-1,2 do for x=math.ceil(xs[i]),math.floor(xs[i+1]) do put(im,x,y,c) end end
 end
 if outline then for i=1,#pts do line(im,pts[i],pts[i%#pts+1],outline) end end
end
local function transformed(pts,f) local r={};for i,p in ipairs(pts) do r[i]=f(p[1],p[2]) end;return r end
local function poly(im,f,pts,c,o) polygon(im,transformed(pts,f),c,o) end
local function ln(im,f,a,b,c) line(im,f(a[1],a[2]),f(b[1],b[2]),c) end
local function lerp(a,b,t) return {a[1]+(b[1]-a[1])*t,a[2]+(b[2]-a[2])*t} end
local function vec(a,b) local x,y=b[1]-a[1],b[2]-a[2];local l=math.sqrt(x*x+y*y);return x/l,y/l,l end
local function segmentFrame(a,b)
 local ux,uy,l=vec(a,b)
 return function(x,y) return {a[1]+ux*y-uy*x,a[2]+uy*y+ux*x} end,l
end
local function limb(im,a,b,w1,w2,base,shade,light)
 local f,l=segmentFrame(a,b)
 poly(im,f,{{-w1,0},{w1,0},{w2,l-2},{w2-1,l+1},{-w2+1,l+1},{-w2,l-2}},base,C.outline)
 poly(im,f,{{-w1+1,1},{-w1+3,2},{-w2+3,l-2},{-w2+1,l-1}},shade)
 poly(im,f,{{1,2},{w1-1,3},{w2-1,l*.65},{w2-3,l*.76}},light)
end
local function leg(im,hip,knee,ankle,far)
 limb(im,hip,knee,8,6,far and C.trouser1 or C.trouser2,C.trouser0,far and C.trouser2 or C.trouser3)
 local f,l=segmentFrame(hip,knee)
 ln(im,f,{-5,5},{-3,l-4},C.trouser0)
 ln(im,f,{3,6},{2,l*.62},far and C.trouser2 or C.trouser4)
 ln(im,f,{-2,l-4},{3,l-2},C.trouser1)
 local top=lerp(knee,ankle,.18)
 limb(im,knee,top,6,5,far and C.trouser1 or C.trouser2,C.trouser0,C.trouser3)
 limb(im,top,ankle,6,5,far and C.boot1 or C.boot2,C.boot0,far and C.boot2 or C.boot3)
 local bf,bl=segmentFrame(top,ankle)
 poly(im,bf,{{-6,0},{6,0},{6,3},{-6,3}},C.boot0,C.outline)
 ln(im,bf,{-4,1},{4,1},C.boot3)
 ln(im,bf,{-2,5},{-1,bl-5},C.boot1)
 ln(im,bf,{3,5},{2,bl-5},far and C.boot2 or C.boot4)
 ln(im,bf,{-1,bl-6},{3,bl-8},C.boot1)
 local x,y=ankle[1],ankle[2]
 local ff=function(a,b) return {x+a,y+b} end
 poly(im,ff,{{-4,-6},{4,-5},{5,0},{8,3},{7,5},{-11,5},{-12,3},{-9,0},{-6,-2}},C.boot1,C.outline)
 poly(im,ff,{{-7,0},{2,-2},{5,1},{-1,2},{-9,3}},far and C.boot2 or C.boot3)
 ln(im,ff,{-10,4},{6,4},C.boot0)
 ln(im,ff,{-7,1},{-2,0},C.boot4)
end
local function solveElbow(shoulder,wrist,len1,len2,side)
 local ux,uy,d=vec(shoulder,wrist)
 assert(d<len1+len2-.0001,'Joint cannot reach endpoint: '..d..' / '..(len1+len2))
 local u=(len1*len1-len2*len2+d*d)/(2*d);local h=math.sqrt(math.max(0,len1*len1-u*u))
 return {shoulder[1]+ux*u-uy*h*side,shoulder[2]+uy*u+ux*h*side}
end
local function arm(im,a,e,w,far)
 local ux,uy=vec(a,e);local vx,vy=vec(e,w)
 local n1={-uy,ux};local n2={-vy,vx};local mx,my=n1[1]+n2[1],n1[2]+n2[2];local ml=math.sqrt(mx*mx+my*my)
 if ml<.001 then mx,my=n1[1],n1[2] else mx,my=mx/ml,my/ml end
 local pts={{a[1]+n1[1]*6,a[2]+n1[2]*6},{e[1]+mx*5,e[2]+my*5},{w[1]+n2[1]*4,w[2]+n2[2]*4},
 {w[1]-n2[1]*4,w[2]-n2[2]*4},{e[1]-mx*5,e[2]-my*5},{a[1]-n1[1]*6,a[2]-n1[2]*6}}
 polygon(im,pts,far and C.coat1 or C.coat2,C.outline)
 polygon(im,{{a[1]+n1[1]*5,a[2]+n1[2]*5},{e[1]+mx*4,e[2]+my*4},{w[1]+n2[1]*3,w[2]+n2[2]*3},{w[1]+n2[1],w[2]+n2[2]},{e[1]+mx,e[2]+my}},C.coat0)
 local f,l=segmentFrame(a,e)
 poly(im,f,{{-3,3},{0,3},{1,l*.65},{-2,l-3}},far and C.coat2 or C.coat3)
 local ff,ll=segmentFrame(e,w)
 poly(im,ff,{{-3,2},{0,3},{1,ll-4},{-2,ll-4}},far and C.coat2 or C.coat3)
 ln(im,ff,{-3,3},{1,5},C.coat0)
 ln(im,ff,{-2,6},{1,7},C.coat1)
 poly(im,ff,{{-4,ll-5},{4,ll-5},{4,ll-2},{-4,ll-2}},C.olive0,C.ink)
 ln(im,ff,{-3,ll-4},{3,ll-4},C.olive2)
 poly(im,ff,{{-4,ll-1},{4,ll-1},{4,ll+1},{-4,ll+1}},C.shirt1,C.ink)
end
local function body(im,pose)
 local x,y=pose.waist[1],pose.waist[2];local lean=pose.lean or 0;local turn=pose.turn or 0
 local f=function(a,b) return {x+a+(-b/32)*lean,y+b} end
 -- Short tailored jacket: its hem ends above the fork of the trousers.
 poly(im,f,{{-13,-34},{-5,-37},{8,-36},{16,-31},{15,-18},{12,-8},{13,0},{3,2},{-3,0},{-13,1},{-15,-8},{-12,-20}},C.coat2,C.outline)
 poly(im,f,{{8,-32},{15,-30},{13,-18},{10,-7},{12,-1},{4,0},{1,-13},{3,-26}},C.coat0)
 poly(im,f,{{-12,-29},{-8,-32},{-6,-20},{-7,-7},{-11,-2},{-13,-7}},C.coat3)
 poly(im,f,{{-7,-35},{0,-37},{7,-34},{2,-18},{-3,-21}},C.shirt1,C.ink)
 poly(im,f,{{-6,-35},{-2,-36},{-1,-29},{-5,-30}},C.shirt2)
 poly(im,f,{{0,-34},{5,-34},{3,-27},{-1,-30}},C.shirt2)
 poly(im,f,{{-1,-32},{2,-31},{2,-27},{1,-24},{2,-19},{0,-16},{-3,-20},{-2,-27}},C.olive0,C.ink)
 ln(im,f,{0,-27},{0,-20},C.olive2)
 poly(im,f,{{-9,-35},{-5,-32},{-3,-23},{1,-17},{-6,-21},{-12,-28}},C.olive0,C.ink)
 ln(im,f,{-9,-33},{-7,-27},C.gold2);ln(im,f,{-7,-27},{-2,-21},C.gold1)
 poly(im,f,{{6,-35},{10,-31},{8,-26},{2,-17},{-1,-16},{3,-24}},C.olive1,C.ink)
 ln(im,f,{7,-33},{7,-28},C.gold2);ln(im,f,{7,-28},{1,-19},C.gold1)
 ln(im,f,{1+turn,-16},{0+turn,-3},C.coat0)
 for _,yy in ipairs({-13,-7}) do local a=f(2+turn,yy);put(im,a[1],a[2],C.gold2);put(im,a[1]+1,a[2]+1,C.gold0) end
 ln(im,f,{-10,-9},{-5,-10},C.coat0);ln(im,f,{-9,-8},{-5,-9},C.coat4)
 ln(im,f,{6,-10},{10,-9},C.coat3)
 ln(im,f,{-11,-1},{-4,-1},C.gold1);ln(im,f,{4,0},{11,-1},C.gold0)
 -- Shoulder tab is a small rectangular insignia, never a skirt border.
 poly(im,f,{{9,-34},{15,-32},{14,-30},{9,-32}},C.coat1,C.ink)
 ln(im,f,{10,-33},{14,-32},C.gold2)
end
local function head(im,pose)
 local x,y=pose.head[1],pose.head[2];local tilt=math.rad(pose.headTilt or 0);local ct,st=math.cos(tilt),math.sin(tilt)
 local f=function(a,b) return {x+a*ct-b*st,y+a*st+b*ct} end
 poly(im,f,{{-4,16},{8,15},{7,27},{3,30},{-5,25}},C.skin1,C.outline)
 poly(im,f,{{-3,19},{3,20},{3,26},{-3,24}},C.skin3)
 local silhouette={{-22,-4},{-18,-12},{-11,-13},{-6,-20},{-1,-17},{10,-19},{8,-14},{19,-12},{16,-7},{25,-2},{19,2},{24,7},{17,8},{20,14},{13,13},{13,20},{6,18},{0,22},{-10,18},{-19,12},{-22,5},{-26,5}}
 poly(im,f,silhouette,C.hair1,C.outline)
 poly(im,f,{{-18,-2},{-11,-10},{2,-10},{11,-1},{10,15},{2,21},{-6,22},{-14,19},{-18,15},{-20,13},{-22,12},{-22,10},{-20,7},{-20,2}},C.skin3,C.outline)
 poly(im,f,{{5,-5},{11,1},{10,15},{2,21},{-3,21},{-1,18},{5,14}},C.skin1)
 poly(im,f,{{-19,-1},{-10,-6},{0,-3},{-1,7},{-6,12},{-17,14},{-21,11},{-18,8}},C.skin4)
 ln(im,f,{-17,17},{-12,19},C.skin2)
 poly(im,f,{{6,6},{11,5},{13,8},{11,14},{7,15},{5,11}},C.skin2,C.ink)
 ln(im,f,{8,8},{10,8},C.skin4);ln(im,f,{10,8},{8,12},C.skin0)
 if pose.expression=='blink' then
  ln(im,f,{-19,7},{-13,7},C.ink)
  ln(im,f,{-19,1},{-13,2},C.hair0)
 elseif pose.expression=='hurt' then
  ln(im,f,{-18,7},{-14,9},C.ink);ln(im,f,{-14,9},{-10,6},C.ink)
  ln(im,f,{-17,3},{-12,4},C.hair0)
  ln(im,f,{-17,17},{-13,16},C.skin0)
 else
  poly(im,f,{{-18,5},{-12,4},{-12,8},{-15,10},{-18,8}},C.shirt2)
  poly(im,f,{{-17,5},{-15,5},{-15,9},{-17,8}},C.eye)
  ln(im,f,{-17,6},{-17,8},C.ink);local q=f(-16,5);put(im,q[1],q[2],C.shirt2)
  ln(im,f,{-19,5},{-12,4},C.ink)
  ln(im,f,{-19,1},{-13,2},C.hair0)
  ln(im,f,{-20,12},{-18,13},C.skin2)
  ln(im,f,{-18,16},{-14,16},C.skin0)
 end
 -- Deliberately placed hair masses, locks and highlights, rather than a
 -- transferred bitmap head. Bangs stay above the visible eye.
 poly(im,f,{{-22,-4},{-17,-11},{-10,-12},{-5,-18},{-2,-14},{7,-17},{6,-12},{16,-10},{13,-6},{21,-2},{15,1},{19,5},{13,5},{15,11},{9,10},{7,17},{2,16},{1,7},{-3,-2},{-9,-5},{-12,0},{-16,-3},{-18,5},{-21,4}},C.hair3,C.hair0)
 poly(im,f,{{-17,-9},{-10,-10},{-5,-15},{-4,-11},{2,-12},{0,-9},{-7,-6},{-12,-5}},C.hair4)
 poly(im,f,{{-3,-9},{6,-13},{5,-9},{11,-8},{7,-5},{15,-2},{8,-2},{3,-3}},C.hair4)
 poly(im,f,{{-16,-8},{-11,-9},{-10,-7},{-15,-5}},C.hair5)
 poly(im,f,{{1,-8},{5,-10},{4,-7},{1,-5},{-2,-5}},C.hair5)
 poly(im,f,{{10,0},{16,2},{13,4},{17,7},{11,5},{8,7},{8,2}},C.hair2)
 poly(im,f,{{-8,-4},{-4,-3},{0,4},{2,13},{0,15},{-2,6}},C.hair1)
 ln(im,f,{-19,-3},{-19,1},C.hair2);ln(im,f,{-12,-7},{-15,-2},C.hair2)
 ln(im,f,{4,1},{6,8},C.hair4);ln(im,f,{12,9},{9,12},C.hair0)
 ln(im,f,{17,-6},{12,-4},C.hair1);ln(im,f,{3,-13},{0,-11},C.hair2)
end
local function weaponFrame(pose)
 local a=math.rad(pose.angle);local dx,dy=math.cos(a),math.sin(a)
 local x,y=pose.guard[1],pose.guard[2]
 local depth=pose.depth or 1
 return function(t,n) return {x+dx*t*depth-dy*n,y+dy*t*depth+dx*n} end
end
local function sword(im,pose)
 local f=weaponFrame(pose)
 poly(im,f,{{-21,-2},{-2,-2},{-2,2},{-21,2}},C.grip0,C.outline)
 ln(im,f,{-19,-1},{-3,-1},C.grip1)
 for t=-18,-3,3 do ln(im,f,{t,-1},{t+1,1},C.grip2) end
 poly(im,f,{{-24,-2},{-22,-4},{-19,-3},{-18,0},{-20,3},{-23,2}},C.gold0,C.outline)
 ln(im,f,{-23,-2},{-20,-2},C.gold2)
 poly(im,f,{{1,-3},{62,-2},{73,0},{62,3},{1,3}},C.steel1,C.outline)
 poly(im,f,{{2,-2},{61,-1},{70,0},{3,0}},C.steel3)
 ln(im,f,{3,-2},{60,-1},C.steel4)
 poly(im,f,{{3,1},{67,0},{60,2},{3,2}},C.steel0)
 poly(im,f,{{-2,-10},{1,-10},{3,-7},{1,-2},{1,3},{3,8},{1,11},{-2,10},{-3,7},{-2,2},{-2,-3},{-4,-8}},C.gold0,C.outline)
 ln(im,f,{-1,-8},{-1,8},C.gold2);ln(im,f,{0,-7},{0,-3},C.gold3)
end
local function hand(im,pose,t,far)
 local f0=weaponFrame(pose);local f=function(a,b) return f0(t+a,b) end
 poly(im,f,{{-4,-3},{-1,-5},{3,-4},{4,-1},{3,4},{0,5},{-4,3}},far and C.skin2 or C.skin3,C.outline)
 poly(im,f,{{-3,-2},{0,-4},{2,-3},{2,0},{-2,1}},far and C.skin3 or C.skin4)
 poly(im,f,{{-3,2},{0,3},{2,2},{2,4},{-1,4}},C.skin1)
 ln(im,f,{1,-1},{3,0},C.skin1);ln(im,f,{-2,2},{-1,3},C.skin0)
end
function M.draw(pose)
 local im=Image(256,224,ColorMode.RGB)
 local waist=pose.waist or {123,154};pose.waist=waist
 local lean=pose.lean or -3;pose.lean=lean
 pose.head=pose.head or {waist[1]+lean-1,waist[2]-58}
 local farHip={waist[1]+7,waist[2]+5};local nearHip={waist[1]-7,waist[2]+5}
 polygon(im,{{waist[1]-12,waist[2]-2},{waist[1]+11,waist[2]-2},{waist[1]+12,waist[2]+8},{waist[1]+5,waist[2]+12},{waist[1],waist[2]+10},{waist[1]-5,waist[2]+13},{waist[1]-12,waist[2]+6}},C.trouser2)
 local farAnkle=pose.farAnkle or {165,197};local nearAnkle=pose.nearAnkle or {80,197}
 local farKnee=solveElbow(farHip,farAnkle,25,29,-1)
 local nearKnee=solveElbow(nearHip,nearAnkle,25,29,1)
 leg(im,farHip,farKnee,farAnkle,true)
 leg(im,nearHip,nearKnee,nearAnkle,false)
 line(im,{waist[1]+1,waist[2]+4},{waist[1],waist[2]+10},C.trouser0)
 line(im,{waist[1]-10,waist[2]+2},{waist[1]-6,waist[2]+5},C.trouser3)
 local wf=weaponFrame(pose)
 local fw,nw=wf(-6,0),wf(-15,0)
 local fs={waist[1]+lean-10,waist[2]-29};local ns={waist[1]+lean+10,waist[2]-28}
 local fe=solveElbow(fs,fw,20,21,pose.farElbow or 1)
 local ne=solveElbow(ns,nw,21,22,pose.nearElbow or -1)
 arm(im,fs,fe,fw,true)
 body(im,pose)
 if pose.weaponBehind then sword(im,pose) end
 head(im,pose)
 if not pose.weaponBehind then sword(im,pose) end
 arm(im,ns,ne,nw,false)
 hand(im,pose,-6,true);hand(im,pose,-15,false)
 return im,{farShoulder=fs,farElbow=fe,farWrist=fw,nearShoulder=ns,nearElbow=ne,nearWrist=nw,head=pose.head,bladeTip=wf(73,0),guard=pose.guard,nearHip=nearHip,nearKnee=nearKnee,nearAnkle=nearAnkle,farHip=farHip,farKnee=farKnee,farAnkle=farAnkle}
end
return M
