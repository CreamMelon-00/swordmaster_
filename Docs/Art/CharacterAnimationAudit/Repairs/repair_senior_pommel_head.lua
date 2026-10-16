-- Direct Aseprite pixel repair of MobStudent blunt-1's two pommel-strike heads.
-- The head silhouettes are authored per pose. Arms, hilt, blade and collar remain
-- from each destination pose; no generated artwork or whole-body mirroring.
local root=app.params.root or 'C:/Users/User/Documents/swordmaster_'
local runtime=root..'/Assets/Game/Resources/MobStudent/Animations/blunt-1'
local docs=root..'/Docs/Art/Animations/MobStudentAttacks/head-turn-v2/blunt-1'
local before=app.params.before or runtime
local pc=app.pixelColor
local function inside(x,y,p)
 local hit=false
 local j=#p
 for i=1,#p do
  local a,b=p[i],p[j]
  if (a[2]>y)~=(b[2]>y) and x<(b[1]-a[1])*(y-a[2])/(b[2]-a[2])+a[1] then hit=not hit end
  j=i
 end
 return hit
end
local src1={{320,205},{565,205},{565,378},{548,389},{532,393},{510,394},{488,389},{479,384},{458,383},{452,386},{447,389},{441,386},{432,385},{422,382},{414,381},{408,384},{392,386},{365,385},{346,381},{331,371},{325,354}}
local src4={{320,210},{555,210},{555,378},{539,389},{522,393},{502,394},{481,389},{467,383},{455,382},{448,385},{443,389},{436,391},{428,387},{421,386},{416,382},{410,382},{404,386},{384,388},{365,387},{345,379},{331,373},{325,351}}
local old2={{327,205},{615,205},{615,383},{594,383},{572,385},{552,387},{526,389},{504,389},{482,386},{466,384},{459,384},{451,380},{444,381},{438,378},{430,379},{423,383},{413,388},{394,391},{376,390},{361,385},{351,379},{348,364},{330,350}}
local old3={{326,210},{550,210},{550,381},{531,385},{511,389},{493,389},{477,389},{461,388},{450,388},{443,388},{436,385},{427,384},{420,381},{413,382},{405,384},{394,386},{381,388},{366,391},{352,390},{343,384},{338,376},{332,363},{326,350}}
local specs={
 {frame=2,source=1,sourceMask=src1,oldMask=old2,dx=12,dy=-3,weaponX=350,weaponY=367,
  neckOuter={{441,379},{454,379},{461,384},{461,389},{450,397},{442,391}},
  neckInner={{445,381},{454,381},{458,385},{457,389},{450,393},{445,389}},sampleX=449,sampleY=385},
 {frame=3,source=4,sourceMask=src4,oldMask=old3,dx=-5,dy=1,weaponX=360,weaponY=373,
  neckOuter={{432,384},{445,384},{450,389},{449,393},{440,399},{433,394}},
  neckInner={{436,386},{444,386},{447,390},{445,394},{440,395},{436,392}},sampleX=440,sampleY=390},
}
local function save(im,path)
 local sprite=Sprite(im.width,im.height,ColorMode.RGB)
 sprite:newCel(sprite.layers[1],1,im,Point(0,0))
 sprite:saveAs(path)
 sprite:close()
end
for _,s in ipairs(specs) do
 local file='upper-'..s.frame..'.png'
 local original=Image{fromFile=before..'/'..file}
 local head=Image{fromFile=runtime..'/upper-'..s.source..'.png'}
 assert(original.width==1024 and original.height==768)
 local out=Image(original)
 local function weapon(x,y) return x<=s.weaponX and y>=s.weaponY end
 for y=200,401 do for x=310,615 do
  if inside(x+.5,y+.5,s.oldMask) and not weapon(x,y) then out:drawPixel(x,y,0) end
 end end
 -- Hand-authored neck extension behind the replacement head. The small triangle
 -- uses skin sampled from this destination's neck; protect its existing collar
 -- and the dark two-pixel seam alongside the white cloth.
 local function collar(x,y)
  for yy=y-2,y+2 do for xx=x-2,x+2 do
   local p=original:getPixel(xx,yy)
   local r,g,b=pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p)
   if pc.rgbaA(p)>0 and math.min(r,g,b)>100 and math.max(r,g,b)-math.min(r,g,b)<30 then return true end
  end end
  return false
 end
 for y=378,400 do for x=430,463 do
  if inside(x+.5,y+.5,s.neckOuter) and not collar(x,y) then
   local color=pc.rgba(10,0,3,253)
   if inside(x+.5,y+.5,s.neckInner) then color=original:getPixel(s.sampleX,s.sampleY) end
   out:drawPixel(x,y,color)
  end
 end end
 for y=200,399 do for x=310,565 do
  local nx,ny=x+s.dx,y+s.dy
  if inside(x+.5,y+.5,s.sourceMask) and not weapon(nx,ny) then
   out:drawPixel(nx,ny,head:getPixel(x,y))
  end
 end end
 -- The original collar sits in front of the neck. Restore its white cloth and
 -- adjoining dark seam exactly after placing the head silhouette.
 for y=378,410 do for x=400,490 do
  if collar(x,y) then out:drawPixel(x,y,original:getPixel(x,y)) end
 end end
 -- Keep the destination neck triangle, collar and every non-head pixel below
 -- the small per-pose join. Head black outlines meet this triangle directly.
 save(out,runtime..'/'..file)
 save(out,docs..'/'..file)
 local combinedPath=docs..'/combined-'..s.frame..'.png'
 local combinedInput=before..'/combined-'..s.frame..'.png'
 local test=io.open(combinedInput,'rb')
 if test then test:close() else combinedInput=combinedPath end
 local combined=Image{fromFile=combinedInput}
 assert(combined.width==original.width and combined.height==original.height)
 for y=200,401 do for x=310,615 do
  if original:getPixel(x,y)~=out:getPixel(x,y) then combined:drawPixel(x,y,out:getPixel(x,y)) end
 end end
 save(combined,combinedPath)
 print('Repaired MobStudent blunt-1/'..file..' head from upper-'..s.source..', shift '..s.dx..','..s.dy)
end
