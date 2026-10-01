-- Convert one 2172x724 candidate into a 543x181 editable Aseprite source and
-- an exact 4x nearest-neighbor PNG. The reduction uses one alpha-weighted
-- color per 4x4 source block; no dithering or interpolation is used.
local input = assert(app.params.input, 'Pass --script-param input=<PNG>')
local output = assert(app.params.output, 'Pass --script-param output=<directory>')
local name = assert(app.params.name, 'Pass --script-param name=<stem>')
local opaque = app.params.opaque == 'true'
local maxColors = tonumber(app.params.colors or '64')
assert(maxColors and maxColors >= 2 and maxColors <= 128, 'colors must be 2..128')

local source = Image { fromFile = input }
local W, H, SCALE = 543, 181, 4
assert(source.width == W * SCALE and source.height == H * SCALE,
  'Expected a 2172x724 candidate')
local pc = app.pixelColor
local floor, sqrt = math.floor, math.sqrt
local lowKeys, bins, rowVisible = {}, {}, {}
local visible, transparent = 0, 0
local alphaSumMin, alphaSumMax = 16 * 255, 0

-- Force alpha to 0 or 255 after combining each 4x4 block. RGB is weighted by
-- source alpha so matte colors hidden in translucent pixels cannot bleed in.
for y = 0, H - 1 do
  rowVisible[y + 1] = 0
  for x = 0, W - 1 do
    local sr, sg, sb, sa = 0, 0, 0, 0
    for oy = 0, SCALE - 1 do
      for ox = 0, SCALE - 1 do
        local p = source:getPixel(x * SCALE + ox, y * SCALE + oy)
        local a = pc.rgbaA(p)
        sr = sr + pc.rgbaR(p) * a
        sg = sg + pc.rgbaG(p) * a
        sb = sb + pc.rgbaB(p) * a
        sa = sa + a
      end
    end
    if sa < alphaSumMin then alphaSumMin = sa end
    if sa > alphaSumMax then alphaSumMax = sa end
    local i = y * W + x + 1
    if opaque then assert(sa == 16 * 255, 'Opaque input contains alpha at '..x..','..y) end
    if opaque or sa >= 16 * 128 then
      local r = floor(sr / sa + .5)
      local g = floor(sg / sa + .5)
      local b = floor(sb / sa + .5)
      local key = floor(r / 8) * 1024 + floor(g / 8) * 32 + floor(b / 8)
      lowKeys[i] = key
      local entry = bins[key]
      if not entry then
        entry = { r = 0, g = 0, b = 0, n = 0, key = key }
        bins[key] = entry
      end
      entry.r, entry.g, entry.b, entry.n =
        entry.r + r, entry.g + g, entry.b + b, entry.n + 1
      visible = visible + 1
      rowVisible[y + 1] = rowVisible[y + 1] + 1
    else
      lowKeys[i] = false
      transparent = transparent + 1
    end
  end
end
assert(visible > 0, 'No visible pixels in candidate')
local firstVisible, lastVisible, solidBase = H, -1, H
for y = 0, H - 1 do
  if rowVisible[y + 1] > 0 then
    if y < firstVisible then firstVisible = y end
    lastVisible = y
  end
end
for y = H - 1, 0, -1 do
  if rowVisible[y + 1] == W then solidBase = y else break end
end
if name == 'forest-near' then
  assert(firstVisible >= floor(H * .75), 'Near layer rises too high')
elseif name == 'forest-belt-mid' then
  assert(solidBase <= floor(H * .65), 'Midground floor is not continuously opaque')
elseif name == 'forest-far-trees' then
  assert(lastVisible < floor(H * .75), 'Far trees extend too low')
end

-- Weighted median cut, working on 5-bit RGB histogram bins. This chooses a
-- compact palette without ordered/error-diffusion dither or random texture.
local entries = {}
for _, entry in pairs(bins) do
  entry.r, entry.g, entry.b = entry.r / entry.n, entry.g / entry.n, entry.b / entry.n
  entries[#entries + 1] = entry
end
local function bounds(box)
  local minR, minG, minB = 256, 256, 256
  local maxR, maxG, maxB, count = 0, 0, 0, 0
  for _, e in ipairs(box) do
    if e.r < minR then minR = e.r end
    if e.g < minG then minG = e.g end
    if e.b < minB then minB = e.b end
    if e.r > maxR then maxR = e.r end
    if e.g > maxG then maxG = e.g end
    if e.b > maxB then maxB = e.b end
    count = count + e.n
  end
  local ranges = { maxR - minR, (maxG - minG) * 1.1, maxB - minB }
  local axis = 1
  if ranges[2] > ranges[axis] then axis = 2 end
  if ranges[3] > ranges[axis] then axis = 3 end
  return axis, ranges[axis] * sqrt(count), count
end
local boxes = { entries }
while #boxes < maxColors do
  local chosen, axis, best = nil, nil, -1
  for i, box in ipairs(boxes) do
    if #box > 1 then
      local a, score = bounds(box)
      if score > best then chosen, axis, best = i, a, score end
    end
  end
  if not chosen then break end
  local box = boxes[chosen]
  local channel = ({ 'r', 'g', 'b' })[axis]
  table.sort(box, function(a, b)
    if a[channel] == b[channel] then return a.key < b.key end
    return a[channel] < b[channel]
  end)
  local _, _, population = bounds(box)
  local halfway, split = 0, 1
  for i = 1, #box - 1 do
    halfway = halfway + box[i].n
    split = i
    if halfway >= population / 2 then break end
  end
  local left, right = {}, {}
  for i = 1, split do left[#left + 1] = box[i] end
  for i = split + 1, #box do right[#right + 1] = box[i] end
  boxes[chosen] = left
  boxes[#boxes + 1] = right
end

local paletteColors = {}
for _, box in ipairs(boxes) do
  local r, g, b, n = 0, 0, 0, 0
  for _, e in ipairs(box) do
    r, g, b, n = r + e.r * e.n, g + e.g * e.n, b + e.b * e.n, n + e.n
  end
  paletteColors[#paletteColors + 1] = {
    floor(r / n + .5), floor(g / n + .5), floor(b / n + .5)
  }
end

local mapped = {}
for key, e in pairs(bins) do
  local best, distance = 1, math.huge
  for i, c in ipairs(paletteColors) do
    local dr, dg, db = e.r - c[1], e.g - c[2], e.b - c[3]
    local d = 2 * dr * dr + 3 * dg * dg + 2 * db * db
    if d < distance then best, distance = i, d end
  end
  local c = paletteColors[best]
  mapped[key] = pc.rgba(c[1], c[2], c[3], 255)
end

local low = Image(W, H, ColorMode.RGB)
local clear = pc.rgba(0, 0, 0, 0)
for y = 0, H - 1 do
  for x = 0, W - 1 do
    local key = lowKeys[y * W + x + 1]
    low:drawPixel(x, y, key and mapped[key] or clear)
  end
end

local sprite = Sprite(W, H, ColorMode.RGB)
sprite.layers[1].name = 'Dawn forest - '..name
sprite:newCel(sprite.layers[1], 1, low, Point(0, 0))
local palette = Palette(#paletteColors + 1)
palette:setColor(0, Color { r = 0, g = 0, b = 0, a = 0 })
for i, c in ipairs(paletteColors) do
  palette:setColor(i, Color { r = c[1], g = c[2], b = c[3], a = 255 })
end
sprite:setPalette(palette)
local nativePath = output..'/'..name..'.aseprite'
local pngPath = output..'/'..name..'.png'
sprite:saveCopyAs(nativePath)

local enlarged = Image(W * SCALE, H * SCALE, ColorMode.RGB)
for y = 0, H - 1 do
  for x = 0, W - 1 do
    local c = low:getPixel(x, y)
    for oy = 0, SCALE - 1 do
      for ox = 0, SCALE - 1 do
        enlarged:drawPixel(x * SCALE + ox, y * SCALE + oy, c)
      end
    end
  end
end
enlarged:saveAs(pngPath)

-- Reopen both on-disk files and compare every exported pixel to its source
-- cell. Aseprite's PNG encoder is included in this check.
local checkNative = app.open(nativePath)
assert(checkNative and checkNative.width == W and checkNative.height == H,
  'Aseprite source dimensions did not survive saving')
local checkPng = Image { fromFile = pngPath }
assert(checkPng.width == W * SCALE and checkPng.height == H * SCALE,
  'Exported PNG dimensions are wrong')
local checked, alphaErrors = 0, 0
for pixel in checkPng:pixels() do
  local c = pixel()
  local expected = low:getPixel(floor(pixel.x / SCALE), floor(pixel.y / SCALE))
  assert(c == expected, '4x block mismatch at '..pixel.x..','..pixel.y)
  local a = pc.rgbaA(c)
  if a ~= 0 and a ~= 255 then alphaErrors = alphaErrors + 1 end
  checked = checked + 1
end
assert(alphaErrors == 0, 'Nonbinary alpha in exported PNG')
if opaque then assert(transparent == 0, 'Opaque output has transparent pixels') end

local report = assert(io.open(output..'/'..name..'.verify.txt', 'w'))
report:write(string.format(
  'VERIFIED: %s | input %dx%d | source %dx%d | output %dx%d | scale %dx nearest | palette %d colors | visible %d | transparent %d | first visible row %d | last visible row %d | continuous opaque base starts %d | checked %d | alpha 0/255 | source alpha sum %d..%d\n',
  name, source.width, source.height, W, H, checkPng.width, checkPng.height,
  SCALE, #paletteColors, visible, transparent, firstVisible, lastVisible,
  solidBase, checked, alphaSumMin, alphaSumMax))
report:close()
