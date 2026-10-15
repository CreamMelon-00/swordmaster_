-- Assemble and verify the new skill icons in an editable tagged Aseprite master.
local root=assert(app.params.root,'Pass root=<absolute project path>')
local base=root..'/Docs/Art/SkillIcons/NewSkills20261015/'
local sprite=Sprite(48,48,ColorMode.RGB)
sprite.layers[1].name='New skill icons — one editable frame per role'
for frame=1,15 do
  local role=frame+20
  local image=Image{fromFile=base..'Output/48/role'..role..'.png'}
  assert(image.width==48 and image.height==48,'role'..role..' size')
  if frame>1 then sprite:newEmptyFrame(frame) end
  sprite:newCel(sprite.layers[1],frame,image,Point(0,0))
  sprite.frames[frame].duration=.1
end
for frame=1,15 do sprite:newTag(frame,frame).name='role'..(frame+20) end
local path=base..'NewSkillIcons-48.aseprite'
sprite:saveAs(path)
sprite:close()
local saved=app.open(path)
assert(#saved.frames==15 and #saved.tags==15,'15 frames and tags')
for frame=1,15 do
  local role=frame+20
  assert(saved.tags[frame].name=='role'..role,'tag')
  local expected=Image{fromFile=base..'Output/48/role'..role..'.png'}
  local actual=Image(48,48,ColorMode.RGB)
  actual:drawSprite(saved,frame)
  assert(actual:isEqual(expected),'role'..role..' mismatch')
end
print('NEW ICON MASTER PASS: all 15 tagged frames match logical PNGs exactly.')
