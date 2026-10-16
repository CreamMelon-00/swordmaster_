-- Aseprite batch pixel repair. Run from the repository root, or pass --script-param root=PATH.
-- Only adds the reviewed forward-shin/shoe pixels to EnemyStudent slash-2 02/03.
local root=app.params['root'] or app.fs.currentPath
local pc=app.pixelColor
local patch=Image{fromFile=root..'/Docs/Art/CharacterAnimationAudit/Repairs/enemy-slash2-front-leg.patch.png'}
assert(patch.width==256 and patch.height==224,'Unexpected patch size')
for _,name in ipairs({'frame-02.png','frame-03.png'}) do
 local path=root..'/Assets/Game/Resources/EnemyStudent/Animations/slash-2/'..name
 local before=Image{fromFile=path};local repaired=Image(before);local added=0
 assert(before.width==256 and before.height==224,'Unexpected target')
 for p in patch:pixels() do
  local pixel=p()
  if pc.rgbaA(pixel)>0 then
   local old=before:getPixel(p.x,p.y)
   assert(pc.rgbaA(old)==0 or old==pixel,'Patch would overwrite existing artwork at '..p.x..','..p.y)
   repaired:drawPixel(p.x,p.y,pixel)
   if pc.rgbaA(old)==0 then added=added+1 end
  end
 end
 repaired:saveAs(path)
 print(name..': restored '..added..' transparent pixels')
end

