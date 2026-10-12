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
