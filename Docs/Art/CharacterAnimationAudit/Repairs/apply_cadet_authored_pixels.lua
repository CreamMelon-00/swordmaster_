-- Replay the reviewed, individually authored repair pixels after the old
-- Cadet conversion/cleanup passes. These patches belong to specific source
-- poses: inspect and redraw them if the source pose or head design changes.
local root = 'C:/Users/User/Documents/swordmaster_'
local dataPath = root .. '/Docs/Art/CharacterAnimationAudit/Repairs/cadet_authored_pixels.lua'
local authored = dofile(dataPath)
assert(type(authored) == 'table', 'Invalid authored Cadet pixel data: ' .. dataPath)
assert(type(authored.A) == 'table' and type(authored.B) == 'table',
       'Authored Cadet pixel data must contain A and B tables')

local function integer(value, minimum, maximum)
  return type(value) == 'number' and value == math.floor(value) and
         value >= minimum and value <= maximum
end

local function apply(image, relative, kind)
  local character = assert(authored[kind], 'Unknown authored Cadet kind: ' .. tostring(kind))
  assert(type(relative) == 'string' and not relative:find('%.png$'),
         'Authored Cadet key must be a relative path without .png')
  local pixels = character[relative]
  if not pixels then return image end
  assert(type(pixels) == 'table', 'Invalid authored pixels for ' .. kind .. '/' .. relative)
  -- Validate the entire patch before changing its image, including explicit
  -- transparent pixels that remove residual hair/outline artifacts.
  for index, value in ipairs(pixels) do
    assert(type(value) == 'table' and #value == 6 and
           integer(value[1], 0, image.width - 1) and
           integer(value[2], 0, image.height - 1) and
           integer(value[3], 0, 255) and integer(value[4], 0, 255) and
           integer(value[5], 0, 255) and integer(value[6], 0, 255),
           'Invalid authored pixel ' .. index .. ' for ' .. kind .. '/' .. relative)
  end
  local rgba = app.pixelColor.rgba
  for _, value in ipairs(pixels) do
    image:drawPixel(value[1], value[2], rgba(value[3], value[4], value[5], value[6]))
  end
  return image
end

return {apply = apply}
