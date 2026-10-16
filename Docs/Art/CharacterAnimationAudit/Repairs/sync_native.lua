-- Synchronize hand-repaired runtime PNGs into the existing editable Aseprite
-- documents. Run with the project's installed Aseprite 1.2.34.1:
--   Aseprite.exe -b --script Docs/Art/CharacterAnimationAudit/Repairs/sync_native.lua
-- Optional read-only comparison:
--   Aseprite.exe -b --script-param dry-run=true --script <this file>
--
-- Only differing character cels are replaced. Existing frame durations,
-- palette colors, layer properties, optional effects, and tag names/colors/
-- animation directions are retained and checked after saving and reopening.
-- Native frame numbers below are one-based; exported files start at frame-01.

local root = 'C:/Users/User/Documents/swordmaster_/'
local dryRun = app.params['dry-run'] == 'true'
local cases = {
  {
    key = 'EnemyStudent',
    native = 'Docs/Art/EnemyStudent/Reactions/Enemy_AllAttacks3_Reactions2.aseprite',
    runtime = 'Assets/Game/Resources/EnemyStudent/Animations/',
    layer = 'Enemy - pixel cels',
    frames = 120,
    clips = {
      {'idle', 1, 8, 'Idle'}, {'slash', 9, 20, 'Slash'},
      {'pierce', 21, 32, 'Pierce'}, {'blunt', 33, 44, 'Blunt'},
      {'slash-2', 46, 57, 'slash-2'}, {'slash-3', 58, 69, 'slash-3'},
      {'pierce-2', 70, 81, 'pierce-2'}, {'pierce-3', 82, 93, 'pierce-3'},
      {'blunt-2', 94, 105, 'blunt-2'}, {'blunt-3', 106, 117, 'blunt-3'},
    },
    poses = {
      {'block', 45, 'Block'}, {'hurt', 118, 'Hurt'},
      {'block-2', 119, 'Block-2'}, {'hurt-2', 120, 'Hurt-2'},
    },
    fixTagRanges = true,
  },
  {
    key = 'CadetA',
    native = 'Docs/Art/EnemyVariants/CadetA/CadetA-AllAnimations.aseprite',
    runtime = 'Assets/Game/Resources/EnemyVariants/CadetA/Animations/',
    layer = 'Cadet A - black hair, blue scarf, pewter trim',
    frames = 121,
    clips = {
      {'idle', 1, 8, 'idle'}, {'slash', 9, 20, 'slash'},
      {'slash-2', 21, 32, 'slash-2'}, {'slash-3', 33, 44, 'slash-3'},
      {'pierce', 45, 56, 'pierce'}, {'pierce-2', 57, 68, 'pierce-2'},
      {'pierce-3', 69, 80, 'pierce-3'}, {'blunt', 81, 92, 'blunt'},
      {'blunt-2', 93, 104, 'blunt-2'}, {'blunt-3', 105, 116, 'blunt-3'},
    },
    poses = {
      {'block', 117, 'poses-block'}, {'block-2', 118, 'poses-block-2'},
      {'hurt', 119, 'poses-hurt'}, {'hurt-2', 120, 'poses-hurt-2'},
    },
    move = {121, 'move-frame-01'},
    fixTagRanges = true,
  },
  {
    key = 'CadetB',
    native = 'Docs/Art/EnemyVariants/CadetB/CadetB-AllAnimations.aseprite',
    runtime = 'Assets/Game/Resources/EnemyVariants/CadetB/Animations/',
    layer = 'Cadet B - sandy hair, olive collar, trousers',
    frames = 121,
    clips = {
      {'idle', 1, 8, 'idle'}, {'slash', 9, 20, 'slash'},
      {'slash-2', 21, 32, 'slash-2'}, {'slash-3', 33, 44, 'slash-3'},
      {'pierce', 45, 56, 'pierce'}, {'pierce-2', 57, 68, 'pierce-2'},
      {'pierce-3', 69, 80, 'pierce-3'}, {'blunt', 81, 92, 'blunt'},
      {'blunt-2', 93, 104, 'blunt-2'}, {'blunt-3', 105, 116, 'blunt-3'},
    },
    poses = {
      {'block', 117, 'poses-block'}, {'block-2', 118, 'poses-block-2'},
      {'hurt', 119, 'poses-hurt'}, {'hurt-2', 120, 'poses-hurt-2'},
    },
    move = {121, 'move-frame-01'},
    fixTagRanges = true,
  },
  {
    key = 'SwordGirl',
    native = 'Docs/Art/SwordGirl/BluntExpansion/SwordGirl_AllAttacks3.aseprite',
    runtime = 'Assets/Game/Resources/SwordGirl/Animations/',
    layer = 'Character - pixel cels',
    frames = 120,
    clips = {
      {'idle', 1, 8, 'Idle'}, {'slash', 9, 20, 'Slash'},
      {'pierce', 21, 32, 'Pierce'}, {'blunt', 33, 44, 'Blunt'},
      {'slash-2', 49, 60, 'Slash2'}, {'slash-3', 61, 72, 'Slash3'},
      {'pierce-2', 73, 84, 'Pierce2'}, {'pierce-3', 85, 96, 'Pierce3'},
      {'blunt-2', 97, 108, 'Blunt2'}, {'blunt-3', 109, 120, 'Blunt3'},
    },
    poses = {
      {'block', 45, 'Block'}, {'hurt', 46, 'Hurt'},
      {'block-2', 47, 'Block2'}, {'hurt-2', 48, 'Hurt2'},
    },
    fixTagRanges = false,
  },
}

local layerFields = {
  'name', 'opacity', 'blendMode', 'isVisible', 'isEditable',
  'isBackground', 'isGroup', 'isContinuous',
}

local function colorValues(color)
  return {color.red, color.green, color.blue, color.alpha}
end

local function equalValues(a, b, context)
  assert(#a == #b, context .. ': count changed')
  for i, v in ipairs(a) do assert(v == b[i], context .. ': value changed at ' .. i) end
end

local function snapshot(sprite)
  local state = {
    width = sprite.width, height = sprite.height, colorMode = sprite.colorMode,
    durations = {}, layers = {}, tags = {}, palettes = {},
  }
  for i, frame in ipairs(sprite.frames) do state.durations[i] = frame.duration end
  for i, layer in ipairs(sprite.layers) do
    local item = {properties = {}, cels = {}}
    for _, key in ipairs(layerFields) do item.properties[key] = layer[key] end
    for _, cel in ipairs(layer.cels) do
      item.cels[cel.frame.frameNumber] = {
        image = Image(cel.image), x = cel.position.x, y = cel.position.y,
        opacity = cel.opacity,
      }
    end
    state.layers[i] = item
  end
  for i, tag in ipairs(sprite.tags) do
    state.tags[i] = {
      name = tag.name, from = tag.fromFrame.frameNumber,
      to = tag.toFrame.frameNumber, color = colorValues(tag.color),
      aniDir = tag.aniDir,
    }
  end
  -- Aseprite 1.2's palette array raises on the ipairs sentinel index.
  for i = 1, #sprite.palettes do
    local palette = sprite.palettes[i]
    local colors = {}
    for c = 0, #palette - 1 do colors[c + 1] = colorValues(palette:getColor(c)) end
    state.palettes[i] = colors
  end
  return state
end

local function verify(sprite, expected, label)
  assert(sprite.width == expected.width and sprite.height == expected.height,
         label .. ': canvas changed')
  assert(sprite.colorMode == expected.colorMode, label .. ': color mode changed')
  assert(#sprite.frames == #expected.durations, label .. ': frame count changed')
  for i, duration in ipairs(expected.durations) do
    assert(math.abs(sprite.frames[i].duration - duration) < 0.000001,
           label .. ': duration changed on frame ' .. i)
  end
  assert(#sprite.layers == #expected.layers, label .. ': layer count changed')
  for i, item in ipairs(expected.layers) do
    local layer = sprite.layers[i]
    for _, key in ipairs(layerFields) do
      assert(layer[key] == item.properties[key], label .. ': layer ' .. i .. ' ' .. key .. ' changed')
    end
    local count = 0
    for frame, celState in pairs(item.cels) do
      count = count + 1
      local cel = layer:cel(frame)
      assert(cel, label .. ': cel missing, layer ' .. i .. ' frame ' .. frame)
      assert(cel.position.x == celState.x and cel.position.y == celState.y,
             label .. ': cel moved, layer ' .. i .. ' frame ' .. frame)
      assert(cel.opacity == celState.opacity,
             label .. ': cel opacity changed, layer ' .. i .. ' frame ' .. frame)
      assert(cel.image:isEqual(celState.image),
             label .. ': unexpected pixels, layer ' .. i .. ' frame ' .. frame)
    end
    assert(#layer.cels == count, label .. ': cel count changed on layer ' .. i)
  end
  assert(#sprite.tags == #expected.tags, label .. ': tag count changed')
  for i, item in ipairs(expected.tags) do
    local tag = sprite.tags[i]
    assert(tag.name == item.name and tag.aniDir == item.aniDir,
           label .. ': tag name or direction changed at ' .. i)
    assert(tag.fromFrame.frameNumber == item.from and tag.toFrame.frameNumber == item.to,
           label .. ': incorrect range for tag ' .. item.name)
    equalValues(colorValues(tag.color), item.color, label .. ': tag color ' .. item.name)
  end
  assert(#sprite.palettes == #expected.palettes, label .. ': palette count changed')
  for i, colors in ipairs(expected.palettes) do
    local palette = sprite.palettes[i]
    assert(#palette == #colors, label .. ': palette size changed')
    for c = 0, #palette - 1 do
      equalValues(colorValues(palette:getColor(c)), colors[c + 1], label .. ': palette color ' .. c)
    end
  end
end

-- Aseprite 1.2 predates the built-in json module.
local function encode(value)
  local kind = type(value)
  if kind == 'string' then
    local escapes = {['\\']='\\\\', ['"']='\\"', ['\n']='\\n', ['\r']='\\r', ['\t']='\\t'}
    return '"' .. value:gsub('[\\"\n\r\t]', escapes) .. '"'
  elseif kind == 'boolean' or kind == 'number' then
    return tostring(value)
  elseif kind == 'table' then
    local parts = {}
    if #value > 0 or next(value) == nil then
      for i = 1, #value do parts[#parts+1] = encode(value[i]) end
      return '[' .. table.concat(parts, ',') .. ']'
    end
    local keys = {}
    for key in pairs(value) do keys[#keys+1] = key end
    table.sort(keys)
    for _, key in ipairs(keys) do parts[#parts+1] = encode(key) .. ':' .. encode(value[key]) end
    return '{' .. table.concat(parts, ',') .. '}'
  end
  error('Unsupported JSON value: ' .. kind)
end

local summaries = {}
for _, spec in ipairs(cases) do
  local sprite = app.open(root .. spec.native)
  assert(sprite, 'Cannot open ' .. spec.native)
  assert(sprite.width == 256 and sprite.height == 224 and #sprite.frames == spec.frames,
         spec.key .. ': unexpected document dimensions or frame count')
  assert(sprite.layers[1].name == spec.layer, spec.key .. ': character layer changed')
  local character = sprite.layers[1]
  local expected = snapshot(sprite)
  local mappings, bounds = {}, {}
  for _, clip in ipairs(spec.clips) do
    bounds[clip[4]] = {clip[2], clip[3]}
    for frame = clip[2], clip[3] do
      mappings[frame] = clip[1] .. '/frame-' .. string.format('%02d', frame - clip[2] + 1) .. '.png'
    end
  end
  for _, pose in ipairs(spec.poses) do
    mappings[pose[2]] = 'poses/' .. pose[1] .. '.png'
    bounds[pose[3]] = {pose[2], pose[2]}
  end
  if spec.move then
    mappings[spec.move[1]] = 'move/frame-01.png'
    bounds[spec.move[2]] = {spec.move[1], spec.move[1]}
  end

  local summary = {key = spec.key, native = spec.native, changedFrames = {}, changedTags = {}, tags = {}}
  for frame = 1, spec.frames do
    assert(mappings[frame], spec.key .. ': unmapped native frame ' .. frame)
    local file = mappings[frame]
    local image = Image {fromFile = root .. spec.runtime .. file}
    assert(image.width == sprite.width and image.height == sprite.height,
           spec.key .. ': PNG dimensions changed: ' .. file)
    assert(image.colorMode == sprite.colorMode, spec.key .. ': PNG color mode changed: ' .. file)
    local cel = character:cel(frame)
    assert(cel, spec.key .. ': missing character cel on frame ' .. frame)
    assert(cel.position.x == 0 and cel.position.y == 0 and
           cel.image.width == sprite.width and cel.image.height == sprite.height,
           spec.key .. ': expected a full-canvas character cel on frame ' .. frame)
    if not cel.image:isEqual(image) then
      summary.changedFrames[#summary.changedFrames + 1] = {frame = frame, png = file}
      print('CHANGED_FRAME ' .. spec.key .. ' native=' .. frame .. ' png=' .. file)
      if not dryRun then
        -- newCel replaces this cel; its old Lua Cel object becomes invalid.
        local opacity = cel.opacity
        local position = Point(cel.position.x, cel.position.y)
        local replacement = sprite:newCel(character, frame, image, position)
        replacement.opacity = opacity
        expected.layers[1].cels[frame].image = Image(image)
      end
    end
  end

  for i, tag in ipairs(sprite.tags) do
    local range = assert(bounds[tag.name], spec.key .. ': unexpected tag ' .. tag.name)
    if spec.fixTagRanges then
      if tag.fromFrame.frameNumber ~= range[1] or tag.toFrame.frameNumber ~= range[2] then
        summary.changedTags[#summary.changedTags + 1] = {
          name = tag.name, oldFrom = tag.fromFrame.frameNumber,
          oldTo = tag.toFrame.frameNumber, from = range[1], to = range[2],
        }
      end
      if not dryRun then
        -- All frames already exist. Assign ranges only after the complete
        -- timeline is present, avoiding new-frame insertion extending tags.
        tag.fromFrame = range[1]
        tag.toFrame = range[2]
        expected.tags[i].from, expected.tags[i].to = range[1], range[2]
      end
    else
      assert(tag.fromFrame.frameNumber == range[1] and tag.toFrame.frameNumber == range[2],
             spec.key .. ': existing tag range differs from mapped clip ' .. tag.name)
    end
    local finalFrom = spec.fixTagRanges and range[1] or tag.fromFrame.frameNumber
    local finalTo = spec.fixTagRanges and range[2] or tag.toFrame.frameNumber
    summary.tags[#summary.tags + 1] = {name = tag.name, from = finalFrom, to = finalTo}
    print('TAG_RANGE ' .. spec.key .. ' ' .. tag.name .. ' ' .. finalFrom .. ' ' .. finalTo)
  end
  local expectedTagCount = #spec.clips + #spec.poses + (spec.move and 1 or 0)
  assert(#sprite.tags == expectedTagCount, spec.key .. ': mapped tag count differs')

  if not dryRun then
    verify(sprite, expected, spec.key .. ' before save')
    if #summary.changedFrames > 0 or #summary.changedTags > 0 then
      sprite:saveAs(root .. spec.native)
      local reopened = app.open(root .. spec.native)
      verify(reopened, expected, spec.key .. ' after reopening')
      summary.saved = true
      reopened:close()
    else
      summary.saved = false
    end
  end
  summary.dryRun = dryRun
  summary.verifiedFrames = spec.frames
  summary.preservedOtherLayers = #sprite.layers - 1
  summaries[#summaries + 1] = summary
  print('VERIFIED_NATIVE ' .. spec.key .. ' frames=' .. spec.frames ..
        ' changed=' .. #summary.changedFrames .. ' tagFixes=' .. #summary.changedTags ..
        ' dryRun=' .. tostring(dryRun))
  sprite:close()
end
print('NATIVE_SYNC_SUMMARY ' .. encode(summaries))
