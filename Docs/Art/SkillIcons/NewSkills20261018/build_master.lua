-- Keep the native badge and the new motif separately editable for each role.
local root = assert(app.params.root, "Pass root=<absolute artwork directory>")
local roles = {36, 37, 38, 39}
local families = {"Slash", "Thrust", "Thrust", "Slash"}
local sprite = Sprite(48, 48, ColorMode.RGB)
local frameLayer = sprite.layers[1]
frameLayer.name = "Approved native frame and leather"
local symbolLayer = sprite:newLayer()
symbolLayer.name = "New skill motif"
for i, role in ipairs(roles) do
  if i > 1 then sprite:newEmptyFrame(i) end
  sprite:newCel(frameLayer, i, Image{fromFile=root.."/References/CleanBases/"..families[i]..".png"}, Point(0, 0))
  sprite:newCel(symbolLayer, i, Image{fromFile=root.."/Output/Symbols48/role"..role..".png"}, Point(0, 0))
  sprite.frames[i].duration = .1
end
-- Add tags after all frames exist: inserting frames can expand existing tags.
for i, role in ipairs(roles) do sprite:newTag(i, i).name = "role"..role end
local path = root.."/NewSkillIcons-48.aseprite"
sprite:saveAs(path)
sprite:close()
local saved = assert(app.open(path))
assert(#saved.frames == 4 and #saved.tags == 4 and #saved.layers == 2)
for i, role in ipairs(roles) do
  assert(saved.tags[i].name == "role"..role)
  local actual = Image(48, 48, ColorMode.RGB)
  actual:drawSprite(saved, i)
  local expected = Image{fromFile=root.."/Output/48/role"..role..".png"}
  assert(actual:isEqual(expected), "Saved master differs: role"..role)
end
print("MASTER PASS: four tagged frames, native badge and motif layers, exact final PNG pixels.")
