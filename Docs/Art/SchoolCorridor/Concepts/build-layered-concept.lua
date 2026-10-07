-- Compose the revision as actual Aseprite layers: distant exterior, nearby
-- trees, and a fixed interior with transparent window openings.
local input = assert(app.params.input, 'input directory required')
local output = assert(app.params.output, 'output directory required')
local version = app.params.version or 'v2'
local farRaise = tonumber(app.params.farRaise or '240')
local nearRaise = tonumber(app.params.nearRaise or '0')
assert(version:match('^v[0-9]+$'), 'version must be vN')
assert(farRaise and farRaise >= 0 and farRaise <= 400, 'farRaise out of range')
assert(nearRaise and nearRaise >= 0 and nearRaise <= 200, 'nearRaise out of range')
local scene = Image { fromFile = input..'/corridor-source.png' }
local guide = Image { fromFile = input..'/interior-alpha-guide.png' }
local farSource = Image { fromFile = input..'/exterior-far-source.png' }
local nearSource = Image { fromFile = input..'/exterior-near-source.png' }
local W, H = 2172, 724
for _, im in ipairs { scene, guide, farSource, nearSource } do
  assert(im.width == W and im.height == H, 'All sources must be 2172x724')
end

local pc = app.pixelColor
local clear = pc.rgba(0, 0, 0, 0)
local interior = Image(W, H, ColorMode.RGB)
local far = Image(W, H, ColorMode.RGB)
local near = Image(W, H, ColorMode.RGB)
local holes, fixed = 0, 0
for y = 0, H - 1 do
  local farY = math.min(H - 1, y + farRaise)
  local nearY = math.min(H - 1, y + nearRaise)
  for x = 0, W - 1 do
    far:drawPixel(x, y, farSource:getPixel(x, farY))
    near:drawPixel(x, y, nearSource:getPixel(x, nearY))

    local original = scene:getPixel(x, y)
    local alpha = pc.rgbaA(guide:getPixel(x, y))
    local r, g, b = pc.rgbaR(original), pc.rgbaG(original), pc.rgbaB(original)
    -- ImageGen identified the pane openings but partially erased their wood
    -- muntins. Preserve solid architecture and dark warm-brown frame pixels
    -- from the original picture, never the colored fringes in the guide.
    local wood = alpha >= 80 and r < 175 and r > g * 1.12 and g > b * 1.12
    if alpha >= 235 or wood then
      interior:drawPixel(x, y, pc.rgba(r, g, b, 255))
      fixed = fixed + 1
    else
      interior:drawPixel(x, y, clear)
      holes = holes + 1
    end
  end
end
assert(holes > 100000 and fixed > 900000, 'Window mask is implausible')

far:saveAs(output..'/exterior-far.png')
near:saveAs(output..'/exterior-near.png')
interior:saveAs(output..'/interior-windows.png')

local sprite = Sprite(W, H, ColorMode.RGB)
sprite.layers[1].name = 'Far sky and campus'
sprite:newCel(sprite.layers[1], 1, far, Point(0, 0))
local nearLayer = sprite:newLayer()
nearLayer.name = 'Nearby courtyard trees'
sprite:newCel(nearLayer, 1, near, Point(0, 0))
local wallLayer = sprite:newLayer()
wallLayer.name = 'Interior wall windows floor'
sprite:newCel(wallLayer, 1, interior, Point(0, 0))
sprite:saveCopyAs(output..'/../yellow-stone-corridor-'..version..'.aseprite')
sprite:saveCopyAs(output..'/../yellow-stone-corridor-'..version..'.png')

local report = assert(io.open(output..'/layer-verification.txt', 'w'))
report:write(string.format('LAYERED CONCEPT %s: %dx%d | transparent interior pixels %d | fixed interior pixels %d | far up %d px | near up %d px\n', version, W, H, holes, fixed, farRaise, nearRaise))
report:close()
