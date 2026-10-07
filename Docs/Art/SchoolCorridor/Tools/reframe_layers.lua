-- Fit the three 4x4-pixel corridor layers to the 12-world-unit battle view.
-- The approved concept's narrow floor starts too low for both the window arch
-- and the fighters' feet to fit in the same camera shot. Compress the wall and
-- extend the floor equally across every layer, preserving a strict 4x4 grid.
local input = assert(app.params.input, 'input directory required')
local output = assert(app.params.output, 'output directory required')
local names = { 'corridor-far', 'corridor-near', 'corridor-interior' }
local W, H, SCALE = 543, 181, 4
local OLD_SEAM, NEW_SEAM = 132, 108
local pc = app.pixelColor

for _, name in ipairs(names) do
  local source = Image { fromFile = input..'/'..name..'.png' }
  assert(source.width == W * SCALE and source.height == H * SCALE,
    'Expected a 2172x724 source: '..name)
  local low = Image(W, H, ColorMode.RGB)
  for y = 0, H - 1 do
    local oldY
    if y < NEW_SEAM then
      oldY = math.floor(y * OLD_SEAM / NEW_SEAM + .5)
    else
      oldY = OLD_SEAM + math.floor((y - NEW_SEAM) * (H - 1 - OLD_SEAM) / (H - 1 - NEW_SEAM) + .5)
    end
    for x = 0, W - 1 do
      low:drawPixel(x, y, source:getPixel(x * SCALE, oldY * SCALE))
    end
  end

  local sprite = Sprite(W, H, ColorMode.RGB)
  sprite.layers[1].name = name
  sprite:newCel(sprite.layers[1], 1, low, Point(0, 0))
  sprite:saveCopyAs(output..'/'..name..'.aseprite')

  local enlarged = Image(W * SCALE, H * SCALE, ColorMode.RGB)
  for y = 0, H - 1 do
    for x = 0, W - 1 do
      local c = low:getPixel(x, y)
      local a = pc.rgbaA(c)
      assert(a == 0 or a == 255, 'Nonbinary alpha in '..name)
      for oy = 0, SCALE - 1 do
        for ox = 0, SCALE - 1 do
          enlarged:drawPixel(x * SCALE + ox, y * SCALE + oy, c)
        end
      end
    end
  end
  enlarged:saveAs(output..'/'..name..'.png')
end

local report = assert(io.open(output..'/reframe-verification.txt', 'w'))
report:write(string.format('VERIFIED: 3 layers, %dx%d output, exact %dx grid, seam %d to %d low pixels (%d to %d output pixels), alpha 0/255\n',
  W*SCALE, H*SCALE, SCALE, OLD_SEAM, NEW_SEAM, OLD_SEAM*SCALE, NEW_SEAM*SCALE))
report:close()
