-- Assemble the approved Cadet B PNGs into one editable, tagged Aseprite source.
local root='C:/Users/User/Documents/swordmaster_'
local input=root..'/Assets/Game/Resources/EnemyVariants/CadetB/Animations/'
local output=root..'/Docs/Art/EnemyVariants/CadetB/CadetB-AllAnimations.aseprite'
local clips={
  {'idle',8},{'slash',12},{'slash-2',12},{'slash-3',12},
  {'pierce',12},{'pierce-2',12},{'pierce-3',12},
  {'blunt',12},{'blunt-2',12},{'blunt-3',12},
}
local poses={'poses/block','poses/block-2','poses/hurt','poses/hurt-2','move/frame-01'}
local sprite=Sprite(256,224,ColorMode.RGB)
sprite.layers[1].name='Cadet B - sandy hair, olive collar, trousers'
local index=0
local function add(relative)
  local image=Image{fromFile=input..relative..'.png'}
  assert(image.width==256 and image.height==224,relative..' changed dimensions')
  index=index+1
  if index>1 then sprite:newEmptyFrame(index) end
  sprite.frames[index].duration=relative:find('^idle/') and .16 or .10
  sprite:newCel(sprite.layers[1],index,image,Point(0,0))
end
for _,clip in ipairs(clips) do
  local first=index+1
  for frame=1,clip[2] do add(clip[1]..'/'..string.format('frame-%02d',frame)) end
  sprite:newTag(first,index).name=clip[1]
end
for _,pose in ipairs(poses) do
  local first=index+1
  add(pose)
  sprite:newTag(first,first).name=pose:gsub('/','-')
end
assert(index==121 and #sprite.tags==15)
sprite:saveAs(output)
print('CADET B: saved one editable Aseprite source with '..index..' frames and 15 tagged clips.')
