-- Assemble 20 approved 48px PNGs into one editable, tagged Aseprite master.
local root=assert(app.params.root,'Pass --script-param root=<absolute workspace path>')
local base=root..'/Docs/Art/SkillIcons/Unified/'
local sprite=Sprite(48,48,ColorMode.RGB)
sprite.layers[1].name='Unified skill icons (one editable frame per role)'
for n=1,20 do
  local image=Image{fromFile=base..'Output/48/role'..n..'.png'}
  assert(image.width==48 and image.height==48,'role'..n..' dimensions')
  if n>1 then sprite:newEmptyFrame(n) end
  sprite.frames[n].duration=.1
  sprite:newCel(sprite.layers[1],n,image,Point(0,0))
end
-- Add tags after every frame exists; Aseprite otherwise stretches early tags.
for n=1,20 do sprite:newTag(n,n).name='role'..n end
assert(#sprite.frames==20 and #sprite.tags==20)
sprite:saveAs(base..'UnifiedSkillIcons-48.aseprite')
print('ICON MASTER: 20 frames and 20 single-frame role tags saved.')

