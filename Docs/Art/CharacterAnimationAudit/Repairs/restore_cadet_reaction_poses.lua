-- Surgical Aseprite pixel patches for the reviewed Cadet guard/hurt poses.
-- Pass --script-param root=PATH when the working directory is not the repository.
local root=app.params['root'] or app.fs.currentPath
local pc=app.pixelColor
local repairs=root..'/Docs/Art/CharacterAnimationAudit/Repairs/'
for _,cadet in ipairs({'CadetA','CadetB'}) do
 for _,pose in ipairs({'block','block-2','hurt','hurt-2'}) do
  local path=root..'/Assets/Game/Resources/EnemyVariants/'..cadet..'/Animations/poses/'..pose..'.png'
  local image=Image{fromFile=path}
  local patch=Image{fromFile=repairs..cadet..'-'..pose..'.patch.png'}
  local mask=Image{fromFile=repairs..cadet..'-'..pose..'.mask.png'}
  assert(image.width==256 and image.height==224 and patch.width==256 and patch.height==224,'Unexpected image dimensions')
  local changed=0
  for point in mask:pixels() do
   local color=patch:getPixel(point.x,point.y)
   if pc.rgbaA(point())>0 then
    if image:getPixel(point.x,point.y)~=color then changed=changed+1 end
    image:drawPixel(point.x,point.y,color)
   end
  end
  image:saveAs(path)
  print(cadet..'/'..pose..': '..changed..' pixels repaired')
 end
end

