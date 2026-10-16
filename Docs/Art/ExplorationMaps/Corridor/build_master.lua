local root = assert(app.params.root, "root directory required")
local names = { "corridor-far", "corridor-near", "corridor-interior" }
local sprite = Sprite(4096, 724, ColorMode.RGB)
for i, name in ipairs(names) do
  local image = Image { fromFile = root.."/Authored/"..name..".png" }
  local layer = i == 1 and sprite.layers[1] or sprite:newLayer()
  layer.name = name
  sprite:newCel(layer, 1, image, Point(0, 0))
end
sprite:saveCopyAs(root.."/corridor-master.aseprite")
sprite:close()
local saved = assert(app.open(root.."/corridor-master.aseprite"))
assert(saved.width == 4096 and saved.height == 724 and #saved.layers == 3)
for i, name in ipairs(names) do
  local layer = saved.layers[i]
  assert(layer.name == name, "Master layer order/name differs: "..name)
  local cel = assert(layer:cel(1), "Missing master cel: "..name)
  local actual = Image(4096, 724, ColorMode.RGB)
  actual:drawImage(cel.image, cel.position)
  local expected = Image { fromFile = root.."/Authored/"..name..".png" }
  assert(actual:isEqual(expected), "Master layer pixels differ: "..name)
end
print("PASS: saved corridor master reopens with three pixel-identical authored layers.")
