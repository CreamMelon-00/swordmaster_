-- Preview-only native Aseprite sizing. The generated designs remain the concept sources;
-- animation production must redraw/verify every pose at the game's 256x224 canvas.
local root = 'C:/Users/User/Documents/swordmaster_'
local folder = root .. '/Docs/Art/EnemyVariants/Concepts/'
local pixel = app.pixelColor
local original = Image { fromFile = root .. '/Assets/Game/Resources/EnemyStudent/Animations/idle/frame-01.png' }
local corridor = Image { fromFile = root .. '/Assets/Game/Resources/SchoolCorridor/corridor-composite.png' }
local sources = {
  Image { fromFile = folder .. 'cadet-a-generated.png' },
  Image { fromFile = folder .. 'cadet-b-generated.png' },
}

local function bounds(image)
  local left, top, right, bottom = image.width, image.height, -1, -1
  for point in image:pixels() do
    if pixel.rgbaA(point()) >= 128 then
      left = math.min(left, point.x)
      top = math.min(top, point.y)
      right = math.max(right, point.x)
      bottom = math.max(bottom, point.y)
    end
  end
  assert(right >= left and bottom >= top, 'Empty concept image')
  return left, top, right, bottom
end

local function fitToGameCanvas(source)
  local left, top, right, bottom = bounds(source)
  local width, height = right - left + 1, bottom - top + 1
  local output = Image(256, 224, ColorMode.RGB)
  local targetHeight = 130
  local targetWidth = math.floor(width * targetHeight / height + .5)
  assert(targetWidth <= 165, 'Concept extends beyond the current enemy silhouette width')
  for y = 0, targetHeight - 1 do
    for x = 0, targetWidth - 1 do
      local sx = math.min(right, left + math.floor((x + .5) * width / targetWidth))
      local sy = math.min(bottom, top + math.floor((y + .5) * height / targetHeight))
      local color = source:getPixel(sx, sy)
      if pixel.rgbaA(color) >= 128 then
        output:drawPixel(68 + x, 72 + y,
          pixel.rgba(pixel.rgbaR(color), pixel.rgbaG(color), pixel.rgbaB(color), 255))
      end
    end
  end
  return output
end

local function limitedPalette(source, colorLimit)
  -- Weighted median cut keeps outlines, skin, cloth and sword bands readable at 1x.
  local histogram, entries = {}, {}
  for point in source:pixels() do
    local color = point()
    if pixel.rgbaA(color) > 0 then
      local item = histogram[color]
      if item == nil then
        item = { color = color, r = pixel.rgbaR(color), g = pixel.rgbaG(color),
          b = pixel.rgbaB(color), weight = 0 }
        histogram[color] = item
        entries[#entries + 1] = item
      end
      item.weight = item.weight + 1
    end
  end
  local function dimensions(box)
    local r0, g0, b0, r1, g1, b1, weight = 255, 255, 255, 0, 0, 0, 0
    for _, item in ipairs(box) do
      r0, g0, b0 = math.min(r0, item.r), math.min(g0, item.g), math.min(b0, item.b)
      r1, g1, b1 = math.max(r1, item.r), math.max(g1, item.g), math.max(b1, item.b)
      weight = weight + item.weight
    end
    local ranges = { r1 - r0, g1 - g0, b1 - b0 }
    local channel = ranges[1] >= ranges[2] and ranges[1] >= ranges[3] and 'r'
      or ranges[2] >= ranges[3] and 'g' or 'b'
    return math.max(ranges[1], ranges[2], ranges[3]) * math.sqrt(weight), channel, weight
  end
  local firstBox = {}
  for _, item in ipairs(entries) do firstBox[#firstBox + 1] = item end
  local boxes = { firstBox }
  while #boxes < colorLimit do
    local selected, score, channel, total = nil, -1, nil, nil
    for index, box in ipairs(boxes) do
      if #box > 1 then
        local value, axis, weight = dimensions(box)
        if value > score then selected, score, channel, total = index, value, axis, weight end
      end
    end
    if selected == nil then break end
    local box = boxes[selected]
    table.sort(box, function(a, b) return a[channel] < b[channel] end)
    local half, weight, cut = total / 2, 0, 1
    for index, item in ipairs(box) do
      weight = weight + item.weight
      if weight >= half then cut = math.min(index, #box - 1); break end
    end
    local other = {}
    for index = cut + 1, #box do other[#other + 1] = box[index] end
    for index = #box, cut + 1, -1 do box[index] = nil end
    boxes[#boxes + 1] = other
  end
  local palette = {}
  for _, box in ipairs(boxes) do
    local r, g, b, weight = 0, 0, 0, 0
    for _, item in ipairs(box) do
      r, g, b = r + item.r * item.weight, g + item.g * item.weight,
        b + item.b * item.weight
      weight = weight + item.weight
    end
    palette[#palette + 1] = {
      r = math.floor(r / weight + .5), g = math.floor(g / weight + .5),
      b = math.floor(b / weight + .5),
    }
  end
  local mapped = {}
  for _, item in ipairs(entries) do
    local best, distance = palette[1], math.huge
    for _, color in ipairs(palette) do
      local dr, dg, db = item.r - color.r, item.g - color.g, item.b - color.b
      local value = 2 * dr * dr + 3 * dg * dg + 2 * db * db
      if value < distance then best, distance = color, value end
    end
    mapped[item.color] = pixel.rgba(best.r, best.g, best.b, 255)
  end
  local result = Image(source.width, source.height, ColorMode.RGB)
  for point in source:pixels() do
    local color = point()
    if pixel.rgbaA(color) > 0 then result:drawPixel(point.x, point.y, mapped[color]) end
  end
  return result
end

local rawA, rawB = fitToGameCanvas(sources[1]), fitToGameCanvas(sources[2])
local cadets = { limitedPalette(rawA, 80), limitedPalette(rawB, 80) }
for index, image in ipairs(cadets) do
  local label = index == 1 and 'cadet-a' or 'cadet-b'
  image:saveAs(folder .. label .. '-idle-concept-256x224.png')
  local sprite = Sprite(256, 224, ColorMode.RGB)
  sprite.layers[1].name = 'Design concept - idle only'
  sprite.cels[1].image = image
  sprite:saveAs(folder .. label .. '-idle-concept.aseprite')
  sprite:close()
end

local preview = Image(1280, 720, ColorMode.RGB)
for point in preview:pixels() do
  point(corridor:getPixel(point.x + 446, point.y))
end
local appearances = { original, cadets[1], cadets[2] }
local offsets = { -75, 365, 805 }
for index, sprite in ipairs(appearances) do
  local left, top = offsets[index], 103
  -- Integer enlargement retains the pixel grid. All three sprites share one baseline.
  for y = 0, sprite.height - 1 do
    for x = 0, sprite.width - 1 do
      local color = sprite:getPixel(x, y)
      if pixel.rgbaA(color) > 0 then
        for yy = 0, 1 do
          for xx = 0, 1 do
            local tx, ty = left + x * 2 + xx, top + y * 2 + yy
            if tx >= 0 and tx < preview.width and ty >= 0 and ty < preview.height then
              preview:drawPixel(tx, ty, color)
            end
          end
        end
      end
    end
  end
end
preview:saveAs(folder .. 'corridor-variants-preview.png')
print('PREVIEW: Iia / cadet A / cadet B at 2x integer pixels; no runtime assets changed.')
