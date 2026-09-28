local root='C:/Users/user/Documents/SwordMasterStory/Aseprite_Character'
local out=root..'/Defense_Hurt'
local pc=app.pixelColor
local sprite=app.open(root..'/Delivery/SwordGirl.aseprite')
assert(sprite.width==256 and sprite.height==224 and #sprite.frames==44)
local character
for _,layer in ipairs(sprite.layers) do if layer.name=='Character - pixel cels' then character=layer end end
assert(character)
local colors={}
for i=1,#sprite.palettes[1]-1 do
 local c=sprite.palettes[1]:getColor(i)
 if c.alpha>0 then colors[#colors+1]={c.red,c.green,c.blue,pc.rgba(c.red,c.green,c.blue,255)} end
end
local cache={}
local function nearest(r,g,b)
 local key=r*65536+g*256+b
 if cache[key] then return cache[key] end
 local best,dist=nil,math.huge
 for _,c in ipairs(colors) do
  local d=2*(r-c[1])^2+3*(g-c[2])^2+2*(b-c[3])^2
  if d<dist then best,dist=c[4],d end
 end
 cache[key]=best; return best
end
local function make(name,scale)
 local src=Image{fromFile=out..'/'..name..'_source.png'}
 local bottom,foot=-1,src.width
 for it in src:pixels() do if pc.rgbaA(it())>=180 then bottom=math.max(bottom,it.y) end end
 for y=bottom-10,bottom do for x=0,src.width-1 do
  if pc.rgbaA(src:getPixel(x,y))>=180 then foot=math.min(foot,x) end
 end end
 local im=Image(256,224,ColorMode.RGB)
 for y=0,223 do for x=0,255 do
  local r,g,b,n=0,0,0,0
  for _,oy in ipairs({.25,.75}) do for _,ox in ipairs({.25,.75}) do
   local sx=math.floor(foot+(x+ox-56)/scale)
   local sy=math.floor(bottom+(y+oy-202)/scale)
   if sx>=0 and sx<src.width and sy>=0 and sy<src.height then
    local p=src:getPixel(sx,sy)
    if pc.rgbaA(p)>=180 then r=r+pc.rgbaR(p);g=g+pc.rgbaG(p);b=b+pc.rgbaB(p);n=n+1 end
   end
  end end
  if n>=2 then im:drawPixel(x,y,nearest(math.floor(r/n+.5),math.floor(g/n+.5),math.floor(b/n+.5))) end
 end end
 local count=0
 for it in im:pixels() do if pc.rgbaA(it())>0 then
  count=count+1;assert(it.x>0 and it.x<255 and it.y>0 and it.y<223,'Clipping')
 end end
 assert(count>3500,'Insufficient pose pixels')
 im:saveAs(out..'/'..name..'.png')
 local index=#sprite.frames+1
 sprite:newEmptyFrame(index);sprite.frames[index].duration=.16
 sprite:newCel(character,index,im,Point(0,0))
 local names={block='Block',hurt='Hurt',['block-2']='Block2',['hurt-2']='Hurt2'}
 local tag=sprite:newTag(index,index);tag.name=names[name]
 print(name..': source foot='..foot..','..bottom..'; scale='..scale..'; opaque pixels='..count)
 return im
end
local block=make('block',.1325)
local hurt=make('hurt',.12)
local block2=make('block-2',.1325)
local hurt2=make('hurt-2',.12)
-- Inserting a frame at a tag boundary can extend the previous tag in Aseprite.
local ranges={Idle={1,8},Slash={9,20},Pierce={21,32},Blunt={33,44},Block={45,45},Hurt={46,46},Block2={47,47},Hurt2={48,48}}
for _,tag in ipairs(sprite.tags) do
 local r=ranges[tag.name];assert(r)
 tag.fromFrame=r[1];tag.toFrame=r[2]
end
sprite:saveAs(out..'/SwordGirl_Defense_Hurt.aseprite')
local preview=Image(512,448,ColorMode.RGB)
for it in preview:pixels() do it(pc.rgba(37,34,39,255)) end
preview:drawImage(block,Point(0,0));preview:drawImage(block2,Point(256,0))
preview:drawImage(hurt,Point(0,224));preview:drawImage(hurt2,Point(256,224))
preview:resize(1024,896);preview:saveAs(out..'/preview.png')
sprite:close()
local check=app.open(out..'/SwordGirl_Defense_Hurt.aseprite')
assert(#check.frames==48 and #check.tags==8)
for _,tag in ipairs(check.tags) do
 local r=ranges[tag.name]
 assert(tag.fromFrame.frameNumber==r[1] and tag.toFrame.frameNumber==r[2],tag.name..' wrong range')
end
local original=app.open(root..'/Delivery/SwordGirl.aseprite')
for i=1,44 do
 local a=Image(256,224,ColorMode.RGB);a:drawSprite(check,i)
 local b=Image(256,224,ColorMode.RGB);b:drawSprite(original,i)
 assert(a:isEqual(b),'Existing frame changed: '..i)
end
print('VERIFIED: 48 frames; 8 tags; four static poses; original 44 frames unchanged; existing palette reused.')
