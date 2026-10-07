local dir = assert(app.params.dir, 'dir required')
local far = Image { fromFile = dir..'/corridor-far.png' }
local near = Image { fromFile = dir..'/corridor-near.png' }
local interior = Image { fromFile = dir..'/corridor-interior.png' }
local W, H = 2172, 724
for _, im in ipairs { far, near, interior } do
  assert(im.width == W and im.height == H, 'Expected 2172x724 layers')
end
local sprite = Sprite(W, H, ColorMode.RGB)
sprite.layers[1].name = 'Far sky and academy grounds'
sprite:newCel(sprite.layers[1], 1, far, Point(0, 0))
local nearLayer = sprite:newLayer()
nearLayer.name = 'Courtyard trees'
sprite:newCel(nearLayer, 1, near, Point(0, 0))
local roomLayer = sprite:newLayer()
roomLayer.name = 'School interior and window frames'
sprite:newCel(roomLayer, 1, interior, Point(0, 0))
sprite:saveCopyAs(dir..'/corridor-composite.aseprite')
sprite:saveCopyAs(dir..'/corridor-composite.png')
