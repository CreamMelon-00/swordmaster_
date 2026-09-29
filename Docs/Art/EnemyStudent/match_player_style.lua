local root='C:/Users/user/Documents/SwordMasterStory/Aseprite_Character'
local out=root..'/Enemy_Rework'
local pc=app.pixelColor
local player=Image{fromFile='C:/Fork/swordmaster_/Assets/Game/Resources/SwordGirl/Animations/idle/frame-01.png'}
local master=app.open(root..'/Blunt_Expansion/SwordGirl_AllAttacks3.aseprite')
local source=Image{fromFile=out..'/enemy-uniform-concept.png'}
local shared={}
for i=1,#master.palettes[1]-1 do local c=master.palettes[1]:getColor(i)
 if c.alpha>0 then shared[#shared+1]={c.red,c.green,c.blue} end
end
local hair={{57,34,42},{85,43,49},{117,55,60},{148,71,73},{178,94,88},{202,119,104},{221,146,120}}
local green={{33,56,45},{43,84,55},{65,116,68},{95,151,85},{137,179,110},{180,204,146}}
local blade={{40,32,47},{56,43,62},{77,56,81},{100,75,102},{128,101,128},{155,131,154}}
local function nearest(r,g,b,colors)
 local best,dist=colors[1],math.huge
 for _,c in ipairs(colors) do local d=2*(r-c[1])^2+3*(g-c[2])^2+2*(b-c[3])^2
  if d<dist then best,dist=c,d end
 end
 return pc.rgba(best[1],best[2],best[3],255)
end
local W,H=256,224
local im=Image(W,H,ColorMode.RGB)
local scale=130/914
local ground=202
for y=0,H-1 do for x=0,W-1 do
 local r,g,b,n,sxx,syy=0,0,0,0,0,0
 for _,oy in ipairs({.25,.75}) do for _,ox in ipairs({.25,.75}) do
  local sx=math.floor((x+ox-20)/scale)
  local sy=math.floor(970+(y+oy-ground)/scale)
  if sx>=0 and sx<source.width and sy>=0 and sy<source.height then
   local p=source:getPixel(sx,sy)
   if pc.rgbaA(p)>=230 then r=r+pc.rgbaR(p);g=g+pc.rgbaG(p);b=b+pc.rgbaB(p);n=n+1;sxx=sxx+sx;syy=syy+sy end
  end
 end end
 if n>=2 then
  r=r/n;g=g/n;b=b/n;sxx=sxx/n;syy=syy/n
  local colors=shared
  if g>r*1.08 and g>b*1.08 then colors=green
  elseif syy<337 and r>g*1.22 and r>b*1.1 and r<228 then colors=hair
  elseif sxx>930 and syy>490 and b>=r*.94 then colors=blade end
  -- Warm the uniform's purple midtones into the same brown cloth ramp as the player.
  if colors==shared and syy>=320 and b>g*1.02 and r<170 then r=r*1.08;g=g*1.07;b=b*.88 end
  im:drawPixel(x,y,nearest(r,g,b,colors))
 end
end end
-- Match the player's slightly larger head while retaining the 130px silhouette and planted feet.
local proportional=Image(W,H,ColorMode.RGB)
for y=72,201 do for x=0,W-1 do
 local sy,sx
 if y<118 then sy=72+(y-72)/1.15;sx=122+(x-122)/1.08
 else sy=112+(y-118)*89/83;sx=x end
 sx=math.floor(sx+.5);sy=math.floor(sy+.5)
 if sx>=0 and sx<W and sy>=0 and sy<H then proportional:drawPixel(x,y,im:getPixel(sx,sy)) end
end end
im=proportional
-- Remove isolated resampling specks; keep the deliberately drawn connected silhouette.
local copy=Image(im)
for y=1,H-2 do for x=1,W-2 do if pc.rgbaA(copy:getPixel(x,y))>0 then
 local count=0
 for dy=-1,1 do for dx=-1,1 do if (dx~=0 or dy~=0) and pc.rgbaA(copy:getPixel(x+dx,y+dy))>0 then count=count+1 end end end
 if count==0 then im:drawPixel(x,y,0) end
end end end
local sprite=Sprite(W,H,ColorMode.RGB)
sprite.layers[1].name='Enemy - native pixel cel - shared player palette'
sprite.cels[1].image=im
local palette=Palette(1+#shared+#hair+#green+#blade)
palette:setColor(0,Color{r=0,g=0,b=0,a=0})
local index=1
for _,ramp in ipairs({shared,hair,green,blade}) do for _,c in ipairs(ramp) do
 palette:setColor(index,Color{r=c[1],g=c[2],b=c[3],a=255});index=index+1
end end
sprite:setPalette(palette)
local tag=sprite:newTag(1,1);tag.name='Idle_Base'
sprite:saveAs(out..'/Enemy_Uniform_PixelMatched.aseprite')
im:saveAs(out..'/enemy-uniform-pixel-matched.png')
local previous=Image(W,H,ColorMode.RGB)
for y=0,H-1 do for x=0,W-1 do
 local sx=math.floor((x+.5-20)/scale);local sy=math.floor(970+(y+.5-ground)/scale)
 if sx>=0 and sx<source.width and sy>=0 and sy<source.height then local p=source:getPixel(sx,sy)
  if pc.rgbaA(p)>=230 then previous:drawPixel(x,y,pc.rgba(pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p),255)) end
 end
end end
local compare=Image(440,168,ColorMode.RGB)
for px in compare:pixels() do px(pc.rgba(38,35,39,255)) end
compare:drawImage(player,Point(-30,-48))
compare:drawImage(im,Point(176,-48))
local function enlarge(src,factor)
 local dest=Image(src.width*factor,src.height*factor,ColorMode.RGB)
 for px in dest:pixels() do px(src:getPixel(math.floor(px.x/factor),math.floor(px.y/factor))) end
 return dest
end
enlarge(compare,3):saveAs(out..'/player-enemy-style-comparison.png')
enlarge(im,3):saveAs(out..'/enemy-uniform-pixel-matched-3x.png')
local colors={};local count=0;local ymin,ymax=H,0
for px in im:pixels() do if pc.rgbaA(px())>0 then
 assert(pc.rgbaA(px())==255,'Partial alpha')
 assert(px.x>0 and px.x<W-1 and px.y>0 and px.y<H-1,'Clipped sprite')
 colors[px()]=true;count=count+1;ymin=math.min(ymin,px.y);ymax=math.max(ymax,px.y)
end end
local n=0;for _ in pairs(colors) do n=n+1 end
local check=app.open(out..'/Enemy_Uniform_PixelMatched.aseprite')
assert(check.width==W and check.height==H and #check.frames==1)
assert(check.cels[1].image:isEqual(im),'Saved cel differs')
print('VERIFIED native 256x224; '..count..' opaque pixels; '..n..' used colors; silhouette y='..ymin..'..'..ymax..'; saved cel unchanged; no clipping.')
