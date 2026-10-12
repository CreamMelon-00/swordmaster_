local root = assert(app.params.root, "root directory required")
local names = {
  "forest-far-mist", "forest-far-trees", "forest-belt-mid", "forest-near"
}
local sprite = Sprite(4096, 724, ColorMode.RGB)
for i, name in ipairs(names) do
  local image = Image { fromFile = root.."/Source/"..name..".png" }
  assert(image.width == 4096 and image.height == 724,
         "unexpected forest source dimensions: "..name)
  local layer = i == 1 and sprite.layers[1] or sprite:newLayer()
  layer.name = name
  sprite:newCel(layer, 1, image, Point(0, 0))
end
sprite:saveCopyAs(root.."/forest-master.aseprite")
