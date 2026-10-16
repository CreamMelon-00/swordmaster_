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
local tagRanges={}
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
  tagRanges[#tagRanges+1]={clip[1],first,index}
end
for _,pose in ipairs(poses) do
  local first=index+1
  add(pose)
  tagRanges[#tagRanges+1]={pose:gsub('/','-'),first,first}
end
-- Aseprite 1.2 extends end tags when later frames are appended. Tag only the
-- completed timeline so each animation retains its own exact frame range.
for _,range in ipairs(tagRanges) do
  local tag=sprite:newTag(range[2],range[3]);tag.name=range[1]
  assert(tag.fromFrame.frameNumber==range[2] and tag.toFrame.frameNumber==range[3],
    'Incorrect tag range: '..range[1])
end
assert(index==121 and #sprite.tags==15)
sprite:saveAs(output)
print('CADET B: saved one editable Aseprite source with '..index..' frames and 15 tagged clips.')
