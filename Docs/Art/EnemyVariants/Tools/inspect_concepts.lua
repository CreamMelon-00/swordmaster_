local root = 'C:/Users/User/Documents/swordmaster_'
local pixel = app.pixelColor
local files = {
  root .. '/Assets/Game/Resources/EnemyStudent/Animations/idle/frame-01.png',
  root .. '/Docs/Art/EnemyVariants/Concepts/cadet-a-generated.png',
  root .. '/Docs/Art/EnemyVariants/Concepts/cadet-b-generated.png',
  root .. '/Docs/Art/EnemyVariants/Concepts/cadet-a-idle-concept-256x224.png',
  root .. '/Docs/Art/EnemyVariants/Concepts/cadet-b-idle-concept-256x224.png',
}

for _, path in ipairs(files) do
  local image = Image { fromFile = path }
  local left, top, right, bottom = image.width, image.height, -1, -1
  local semi = 0
  local colors = {}
  local brightest = 0
  for point in image:pixels() do
    local value = point()
    local alpha = pixel.rgbaA(value)
    if alpha > 0 and alpha < 255 then semi = semi + 1 end
    if alpha >= 128 then
      colors[value] = true
      brightest = math.max(brightest, pixel.rgbaR(value) + pixel.rgbaG(value) + pixel.rgbaB(value))
      left = math.min(left, point.x)
      top = math.min(top, point.y)
      right = math.max(right, point.x)
      bottom = math.max(bottom, point.y)
    end
  end
  local colorCount = 0
  for _ in pairs(colors) do colorCount = colorCount + 1 end
  print(path .. ' ' .. image.width .. 'x' .. image.height .. ' bbox=' ..
    left .. ',' .. top .. '..' .. right .. ',' .. bottom .. ' semi=' .. semi ..
    ' colors=' .. colorCount .. ' brightest=' .. brightest)
  if path:find('idle%-concept') then
    assert(image.width == 256 and image.height == 224, 'Concept canvas changed')
    assert(left == 68 and top == 72 and bottom == 201, 'Concept baseline changed')
    assert(semi == 0 and colorCount <= 80, 'Concept pixels need a clean 80-color palette')
  end
end

for _, label in ipairs({ 'cadet-a', 'cadet-b' }) do
  local native = app.open(root .. '/Docs/Art/EnemyVariants/Concepts/' ..
    label .. '-idle-concept.aseprite')
  assert(native.width == 256 and native.height == 224 and #native.frames == 1)
  local rendered = Image(256, 224, ColorMode.RGB)
  rendered:drawSprite(native, 1)
  local png = Image { fromFile = root .. '/Docs/Art/EnemyVariants/Concepts/' ..
    label .. '-idle-concept-256x224.png' }
  assert(rendered:isEqual(png), label .. ' native cel differs from preview PNG')
  native:close()
end
print('VERIFIED: both native cels match the 80-color pixel-clean PNG previews.')
