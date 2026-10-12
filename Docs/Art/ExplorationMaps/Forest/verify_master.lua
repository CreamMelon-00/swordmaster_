local root = assert(app.params.root, "root directory required")
local names = {
  "forest-far-mist", "forest-far-trees", "forest-belt-mid", "forest-near"
}
local sprite = assert(app.open(root.."/forest-master.aseprite"), "master did not reopen")
assert(sprite.width == 4096 and sprite.height == 724, "wrong Aseprite canvas")
assert(#sprite.layers == #names, "wrong Aseprite layer count")
for i, name in ipairs(names) do
  local layer = sprite.layers[i]
  assert(layer.name == name, "Aseprite layer order/name changed")
  local cel = assert(layer:cel(1), "missing cel: "..name)
  assert(cel.position.x == 0 and cel.position.y == 0, "offset cel: "..name)
  cel.image:saveAs(root.."/Preview/aseprite-check-"..name..".png")
end
