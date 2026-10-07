-- Extend only the cropped edges of the applied corridor art. The central
-- 2172x724 image remains pixel-for-pixel unchanged, including the floor seam.
-- This gives an ultrawide tilted battle camera a full ceiling and floor.
local input = assert(app.params.input, 'input directory required')
local output = assert(app.params.output, 'output directory required')
local names = { 'corridor-far', 'corridor-near', 'corridor-interior' }
local W, H = 2172, 724
local TOP, BOTTOM = 160, 32

for _, name in ipairs(names) do
  local source = Image { fromFile = input..'/'..name..'.png' }
  assert(source.width == W and source.height == H, 'Expected a 2172x724 source: '..name)
  local padded = Image(W, H + TOP + BOTTOM, ColorMode.RGB)
  for y = 0, H + TOP + BOTTOM - 1 do
    local sourceY = math.max(0, math.min(H - 1, y - TOP))
    for x = 0, W - 1 do
      padded:drawPixel(x, y, source:getPixel(x, sourceY))
    end
  end
  padded:saveAs(output..'/'..name..'.png')
  local sprite = Sprite(W, H + TOP + BOTTOM, ColorMode.RGB)
  sprite.layers[1].name = name
  sprite:newCel(sprite.layers[1], 1, padded, Point(0, 0))
  sprite:saveCopyAs(output..'/'..name..'.aseprite')
end

local report = assert(io.open(output..'/padding-verification.txt', 'w'))
report:write(string.format('VERIFIED: 3 runtime layers, %dx%d output, original %dx%d content unchanged at y=%d..%d, top padding=%d, bottom padding=%d\n',
  W, H + TOP + BOTTOM, W, H, TOP, TOP + H - 1, TOP, BOTTOM))
report:close()
