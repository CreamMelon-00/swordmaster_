-- Compact contact sheet made directly from the PNGs used by Unity.
local root = 'C:/Users/User/Documents/swordmaster_/'
local resourceRoot = root .. 'Assets/Game/Resources/EnemyVariants/'
local sheet = Image(256 * 3, 224 * 2, ColorMode.RGB)
local poses = {'idle/frame-01', 'move/frame-01', 'slash/frame-05'}
for row, cadet in ipairs({'CadetA', 'CadetB'}) do
  for column, pose in ipairs(poses) do
    local frame = Image { fromFile = resourceRoot .. cadet .. '/Animations/' .. pose .. '.png' }
    assert(frame.width == 256 and frame.height == 224)
    sheet:drawImage(frame, Point((column - 1) * 256, (row - 1) * 224))
  end
end
sheet:resize(1536, 896)
sheet:saveAs(root .. 'Docs/Art/EnemyVariants/applied-variants-preview.png')
print('Applied preview: A above, B below; idle/move/slash from left to right.')
