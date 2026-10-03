local root=assert(app.params.root):gsub('\\','/')
local dir=root..'/Docs/Art/Movement/Revision2/EnemyOpposite'
local pc=app.pixelColor
local src=Image{fromFile=root..'/Docs/Art/Movement/Revision2/EnemyStudent/Candidate-4-v4/frame-04.png'}
local img=Image(256,224,ColorMode.RGB)
img:drawImage(src,Point(0,0))
local C={
  clear=pc.rgba(0,0,0,0), outline=pc.rgba(51,25,30,255),
  skin=pc.rgba(253,217,201,255), skin2=pc.rgba(253,213,196,255), skinShadow=pc.rgba(251,206,185,255),
  sock=pc.rgba(66,51,68,255), sockLight=pc.rgba(77,56,81,255), sockShadow=pc.rgba(56,43,62,255),
  boot=pc.rgba(117,64,49,255), bootLight=pc.rgba(122,74,63,255), bootShadow=pc.rgba(85,43,49,255),
  hem=pc.rgba(180,124,77,255)
}
-- Existing feet and thighs only; leave the head, skirt, hands and sword alone.
for y=151,204 do
  for x=67,163 do
    if (x<145 or y>=173) then img:drawPixel(x,y,C.clear) end
  end
end
local function poly(points,color)
  local minx,maxx,miny,maxy=255,0,223,0
  for _,p in ipairs(points) do
    if p[1]<minx then minx=p[1] end
    if p[1]>maxx then maxx=p[1] end
    if p[2]<miny then miny=p[2] end
    if p[2]>maxy then maxy=p[2] end
  end
  for y=miny,maxy do for x=minx,maxx do
    local inside=false
    local j=#points
    for i=1,#points do
      local a,b=points[i],points[j]
      if ((a[2]>y)~=(b[2]>y)) and (x<(b[1]-a[1])*(y-a[2])/(b[2]-a[2])+a[1]) then inside=not inside end
      j=i
    end
    if inside then img:drawPixel(x,y,color) end
  end end
end
-- Far LEFT leg is left at the hip and trails to screen-right. Draw it first.
poly({{94,149},{108,149},{114,154},{121,160},{129,165},{131,171},{123,174},{117,168},{108,162},{100,159},{94,158}},C.outline)
poly({{97,151},{107,151},{113,155},{121,162},{128,166},{128,170},{122,171},{115,166},{107,160},{99,157},{96,156}},C.skinShadow)
poly({{99,151},{105,151},{111,157},{120,163},{122,167},{116,164},{106,157},{100,156}},C.skin2)
poly({{122,169},{131,168},{138,175},{146,185},{148,193},{141,198},{133,191},{124,179},{120,172}},C.outline)
poly({{124,172},{129,171},{136,177},{143,186},{145,192},{140,194},{135,189},{128,179}},C.sock)
poly({{128,174},{131,174},{140,185},{142,188},{139,187}},C.sockLight)
poly({{139,188},{149,186},{153,191},{156,197},{154,202},{136,202},{133,198}},C.outline)
poly({{140,190},{148,189},{152,193},{153,198},{151,200},{137,200},{136,197}},C.boot)
poly({{141,191},{148,190},{150,193},{145,195},{137,196}},C.bootLight)
poly({{136,199},{153,199},{152,201},{137,201}},C.bootShadow)
-- Near RIGHT leg now crosses in front and plants at screen-left.
poly({{116,149},{132,149},{132,155},{124,163},{112,171},{103,174},{95,170},{96,162},{106,157}},C.outline)
poly({{118,151},{129,151},{129,155},{121,161},{110,168},{102,171},{98,168},{99,163},{108,159}},C.skin2)
poly({{119,151},{127,151},{125,156},{116,163},{108,167},{103,168},{109,161}},C.skin)
poly({{98,166},{105,170},{103,176},{99,185},{94,194},{85,196},{80,191},{87,178},{92,170}},C.outline)
poly({{97,169},{102,171},{100,177},{95,187},{91,193},{85,193},{83,190},{89,178},{94,171}},C.sock)
poly({{97,171},{99,172},{95,182},{90,190},{86,190},{91,178}},C.sockLight)
poly({{81,187},{93,189},{98,194},{97,201},{69,202},{67,198},{71,193}},C.outline)
poly({{80,190},{91,191},{95,195},{94,199},{70,200},{70,198},{75,194}},C.boot)
poly({{79,190},{88,191},{91,193},{82,195},{71,196},{75,193}},C.bootLight)
poly({{70,199},{95,199},{94,201},{70,201}},C.bootShadow)
-- Reconnect the top of each thigh to the original hem without altering the sword.
poly({{94,147},{106,148},{110,152},{103,154},{95,152}},C.skin2)
poly({{116,148},{131,148},{130,153},{119,153}},C.skin2)
img:saveAs(dir..'/opposite-contact-v1.png')
local sheet=Image(1024,448,ColorMode.RGB)
for p in sheet:pixels() do p(pc.rgba(34,31,39,255)) end
local frames={src,img}
for i,frame in ipairs(frames) do
  for y=0,223 do for x=0,255 do
    local c=frame:getPixel(x,y)
    if pc.rgbaA(c)>0 then
      for dy=0,1 do for dx=0,1 do sheet:drawPixel((i-1)*512+x*2+dx,y*2+dy,c) end end
    end
  end end
end
sheet:saveAs(dir..'/comparison-v1.png')
print('opposite contact v1 written')
