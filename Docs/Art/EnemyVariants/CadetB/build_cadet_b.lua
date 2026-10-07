-- Rebuild Cadet B from the approved 256x224 concept and the existing pose library.
-- Run with Aseprite 1.2: aseprite.exe -b --script build_cadet_b.lua
local root = 'C:/Users/User/Documents/swordmaster_'
local sourceRoot = root .. '/Assets/Game/Resources/EnemyStudent/Animations/'
local targetRoot = root .. '/Assets/Game/Resources/EnemyVariants/CadetB/Animations/'
local conceptPath = root .. '/Docs/Art/EnemyVariants/Concepts/cadet-b-idle-concept-256x224.png'
local p = app.pixelColor
local transparent = p.rgba(0, 0, 0, 0)
local concept = Image { fromFile = conceptPath }
assert(concept.width == 256 and concept.height == 224)
local idleSource = Image { fromFile = sourceRoot .. 'idle/frame-01.png' }
local idleHeadMask = {}
for y = 69, 128 do
  for x = 84, 157 do
    local c = idleSource:getPixel(x, y)
    -- The sampled silhouette isolates the original head. Above the collar it
    -- excludes raised arms and blades that occur in attack poses.
    if p.rgbaA(c) > 0 and (y <= 115 or x < 114 or x > 134) then
      idleHeadMask[y * 256 + x] = true
    end
  end
end

local palette, paletteSeen = {}, {}
for q in concept:pixels() do
  local c = q()
  if p.rgbaA(c) > 0 and not paletteSeen[c] then
    paletteSeen[c] = true
    palette[#palette + 1] = c
  end
end
assert(#palette <= 80, 'Approved concept palette changed')

local mapped = {}
local function nearest(c)
  local cached = mapped[c]
  if cached then return cached end
  local r, g, b = p.rgbaR(c), p.rgbaG(c), p.rgbaB(c)
  local best, bestScore = palette[1], math.huge
  for _, v in ipairs(palette) do
    local dr, dg, db = r - p.rgbaR(v), g - p.rgbaG(v), b - p.rgbaB(v)
    local score = 2 * dr * dr + 3 * dg * dg + 2 * db * db
    if score < bestScore then best, bestScore = v, score end
  end
  mapped[c] = best
  return best
end

local pants = {
  light = p.rgba(93, 81, 87, 255),
  mid = p.rgba(73, 64, 72, 255),
  dark = p.rgba(60, 49, 58, 255),
  shade = p.rgba(36, 18, 29, 255),
}
for _, c in pairs(pants) do assert(paletteSeen[c], 'Trouser shade absent from concept palette') end

local function isSkin(c)
  if p.rgbaA(c) == 0 then return false end
  local r, g, b = p.rgbaR(c), p.rgbaG(c), p.rgbaB(c)
  return r >= 180 and g >= 104 and b >= 84 and r > g + 17 and g >= b - 8
end

local function isGarment(c)
  if p.rgbaA(c) == 0 then return false end
  local r, g, b = p.rgbaR(c), p.rgbaG(c), p.rgbaB(c)
  return r >= 65 and r <= 200 and r > g + 8 and r > b + 5 and g > 27
end

local function trouserShade(c)
  local l = .3 * p.rgbaR(c) + .5 * p.rgbaG(c) + .2 * p.rgbaB(c)
  if l >= 185 then return pants.light end
  if l >= 135 then return pants.mid end
  if l >= 92 then return pants.dark end
  return pants.shade
end

local function makeLegMask(src)
  local visited, mask, skins = {}, {}, {}
  for y = 143, 187 do
    for x = 48, 190 do
      local i = y * 256 + x
      if isSkin(src:getPixel(x, y)) then skins[i] = true end
    end
  end
  local neighbours = {-257, -256, -255, -1, 1, 255, 256, 257}
  for index in pairs(skins) do
    if not visited[index] then
      local queue, component = {index}, {}
      visited[index] = true
      local head, low = 1, 0
      while head <= #queue do
        local current = queue[head]; head = head + 1
        component[#component + 1] = current
        low = math.max(low, math.floor(current / 256))
        local cx, cy = current % 256, math.floor(current / 256)
        for _, delta in ipairs(neighbours) do
          local nextIndex = current + delta
          local nx, ny = nextIndex % 256, math.floor(nextIndex / 256)
          if math.abs(nx - cx) <= 1 and math.abs(ny - cy) <= 1 and skins[nextIndex] and not visited[nextIndex] then
            visited[nextIndex] = true; queue[#queue + 1] = nextIndex
          end
        end
      end
      if low >= 166 and #component >= 8 then
        for _, value in ipairs(component) do mask[value] = true end
      end
    end
  end
  return mask
end

local function headOffset(src, folder, name)
  local right, top = -1, 224
  for y = 60, 120 do
    for x = 55, 180 do
      local c = src:getPixel(x, y)
      if p.rgbaA(c) > 0 then
        local r, g, b = p.rgbaR(c), p.rgbaG(c), p.rgbaB(c)
        if g > r * 1.10 and g > b * 1.1 then
          right = math.max(right, x); top = math.min(top, y)
        end
      end
    end
  end
  assert(right > 0, 'Cannot place Cadet B head: missing original ribbon')
  local dx, dy = right - 140, top - 77
  if folder == 'poses' and name == 'hurt-2.png' then dy = 5 end
  return dx, math.max(-8, math.min(15, dy))
end

local function restoreSword(c)
  local r, g, b = p.rgbaR(c), p.rgbaG(c), p.rgbaB(c)
  return p.rgbaA(c) > 0 and b >= r - 2 and b >= g + 9 and r < 200
end

-- The darkest blade outline is the same #28202F used in the old hair.
-- Restore that outline only beside an actual steel-colored blade pixel;
-- otherwise individual old-hair pixels reappear outside the new head and
-- grow into bright stray dots under the in-game break outline.
local function steelCore(c)
  local r, g, b = p.rgbaR(c), p.rgbaG(c), p.rgbaB(c)
  return p.rgbaA(c) > 0 and r >= 52 and r < 200 and
    b >= r - 2 and b >= g + 9
end

local function weaponCoreMask(src)
  local filled,seen,weapon={}, {}, {}
  for q in src:pixels() do
    if steelCore(q()) then filled[q.y*256+q.x]=true end
  end
  for index in pairs(filled) do
    if not seen[index] then
      local queue={index};seen[index]=true
      local first,seed=1,false
      while first<=#queue do
        local current=queue[first];first=first+1
        local x,y=current%256,math.floor(current/256)
        -- The blade always reaches past the body or above the head. Short
        -- backhand cuts stop around x=164; the old >174 seed missed their
        -- entire blade and left its tip floating after the head transfer.
        -- The silver collar stays inside the central torso.
        if y<160 and ((x<82 and y<110) or (x>155 and y<130) or y<62) then
          seed=true
        end
        for yy=math.max(0,y-1),math.min(223,y+1) do
          for xx=math.max(0,x-1),math.min(255,x+1) do
            local nextIndex=yy*256+xx
            if filled[nextIndex] and not seen[nextIndex] then
              seen[nextIndex]=true;queue[#queue+1]=nextIndex
            end
          end
        end
      end
      if seed and #queue>=12 then
        for _,value in ipairs(queue) do weapon[value]=true end
      end
    end
  end
  return weapon
end

local function nearSteel(weapon, x, y, radius)
  for yy = math.max(0, y - radius), math.min(223, y + radius) do
    for xx = math.max(0, x - radius), math.min(255, x + radius) do
      if weapon[yy*256+xx] then return true end
    end
  end
  return false
end

local donorHair = {}
for _,v in ipairs({
  {57,34,42},{85,43,49},{117,55,60},{148,71,73},
  {178,94,88},{202,119,104},{221,146,120},
}) do donorHair[p.rgba(v[1],v[2],v[3],255)] = true end
local donorRibbon = {}
for _,v in ipairs({
  {33,56,45},{43,84,55},{65,116,68},
  {95,151,85},{137,179,110},{180,204,146},
}) do donorRibbon[p.rgba(v[1],v[2],v[3],255)] = true end

local function smallComponents(filled)
  local seen,groups={},{}
  for index in pairs(filled) do
    if not seen[index] then
      local queue={index};seen[index]=true
      local first=1
      while first<=#queue do
        local current=queue[first];first=first+1
        local x,y=current%256,math.floor(current/256)
        for yy=math.max(0,y-1),math.min(223,y+1) do
          for xx=math.max(0,x-1),math.min(255,x+1) do
            local nextIndex=yy*256+xx
            if filled[nextIndex] and not seen[nextIndex] then
              seen[nextIndex]=true;queue[#queue+1]=nextIndex
            end
          end
        end
      end
      if #queue<=3 then groups[#groups+1]=queue end
    end
  end
  return groups
end

local function cleanPixelIntegrity(img,src)
  local filled,sourceFilled={},{}
  for q in img:pixels() do
    local index=q.y*256+q.x
    if p.rgbaA(q())>0 then filled[index]=true end
    if p.rgbaA(src:getPixel(q.x,q.y))>0 then sourceFilled[index]=true end
  end
  local sourceSmall={}
  for _,group in ipairs(smallComponents(sourceFilled)) do
    for _,value in ipairs(group) do sourceSmall[value]=true end
  end
  -- Only remove new one-to-three-pixel islands severed by the transform;
  -- preserve the source animation's intentional detached marks.
  for _,group in ipairs(smallComponents(filled)) do
    local original=false
    for _,value in ipairs(group) do
      if sourceSmall[value] then original=true end
    end
    if not original then
      for _,value in ipairs(group) do
        img:drawPixel(value%256,math.floor(value/256),transparent)
        filled[value]=nil
      end
    end
  end
  -- A one-pixel transparent cavity around the transplanted neck can reveal
  -- the red silhouette beneath. Preserve any original enclosed holes.
  for y=70,145 do
    for x=65,175 do
      local index=y*256+x
      if not filled[index] and filled[index-1] and filled[index+1] and
        filled[index-256] and filled[index+256] and not
        (not sourceFilled[index] and sourceFilled[index-1] and
          sourceFilled[index+1] and sourceFilled[index-256] and
          sourceFilled[index+256]) then
        local counts,best,bestCount={},nil,0
        for yy=y-1,y+1 do
          for xx=x-1,x+1 do
            local c=img:getPixel(xx,yy)
            if p.rgbaA(c)>0 then
              counts[c]=(counts[c] or 0)+1
              if counts[c]>bestCount then best,bestCount=c,counts[c] end
            end
          end
        end
        img:drawPixel(x,y,best)
        filled[index]=true
      end
    end
  end
end

local function opaqueComponents(filled)
  local seen,groups={},{ }
  for index in pairs(filled) do
    if not seen[index] then
      local group,queue={},{index}
      seen[index]=true
      local head=1
      while head<=#queue do
        local current=queue[head];head=head+1
        group[#group+1]=current
        local x,y=current%256,math.floor(current/256)
        for yy=math.max(0,y-1),math.min(223,y+1) do
          for xx=math.max(0,x-1),math.min(255,x+1) do
            local nextIndex=yy*256+xx
            if filled[nextIndex] and not seen[nextIndex] then
              seen[nextIndex]=true;queue[#queue+1]=nextIndex
            end
          end
        end
      end
      groups[#groups+1]=group
    end
  end
  return groups
end

-- A transplanted head can erase one to several contact pixels between the
-- source's continuous sword/arm/body and the new silhouette. Recover the
-- shortest bridge from source artwork, rather than leaving floating blades
-- that the game's red outline makes especially conspicuous.
local function reconnectSourceSilhouette(img,src,relative)
  local sourceFilled,targetFilled={},{ }
  for q in src:pixels() do
    local index=q.y*256+q.x
    if p.rgbaA(q())>0 then sourceFilled[index]=true end
    if p.rgbaA(img:getPixel(q.x,q.y))>0 then targetFilled[index]=true end
  end
  local sourceGroups=opaqueComponents(sourceFilled)
  local targetGroups=opaqueComponents(targetFilled)
  local function largest(groups)
    local best=1
    for i=2,#groups do if #groups[i]>#groups[best] then best=i end end
    return best
  end
  local sourceMain={}
  for _,index in ipairs(sourceGroups[largest(sourceGroups)]) do sourceMain[index]=true end
  local main={}
  local mainIndex=largest(targetGroups)
  for _,index in ipairs(targetGroups[mainIndex]) do main[index]=true end
  for groupIndex,group in ipairs(targetGroups) do
    if groupIndex~=mainIndex and #group>=4 then
      local sourceOverlap=0
      for _,index in ipairs(group) do if sourceMain[index] then sourceOverlap=sourceOverlap+1 end end
      if sourceOverlap>=math.min(4,#group) then
        local queue,parent,seen={},{},{ }
        for _,index in ipairs(group) do queue[#queue+1]=index;seen[index]=true end
        local head,found=1,nil
        while head<=#queue and not found do
          local current=queue[head];head=head+1
          local x,y=current%256,math.floor(current/256)
          for yy=math.max(0,y-1),math.min(223,y+1) do
            for xx=math.max(0,x-1),math.min(255,x+1) do
              local nextIndex=yy*256+xx
              if main[nextIndex] then found=current;break end
              if sourceFilled[nextIndex] and not seen[nextIndex] then
                seen[nextIndex]=true;parent[nextIndex]=current
                queue[#queue+1]=nextIndex
              end
            end
            if found then break end
          end
        end
        assert(found,relative..' has an unbridgeable source-connected fragment')
        local bridge,count={},0
        while found and parent[found] do
          if not targetFilled[found] then
            bridge[#bridge+1]=found;count=count+1
          end
          found=parent[found]
        end
        assert(count<=32,relative..' needs an implausibly long contact bridge: '..count)
        for _,index in ipairs(bridge) do
          local x,y=index%256,math.floor(index/256)
          local c=src:getPixel(x,y)
          local recolor=(y>=149 and y<=169 and isGarment(c)) and
            trouserShade(c) or nearest(c)
          img:drawPixel(x,y,recolor)
          targetFilled[index]=true;main[index]=true
        end
        for _,index in ipairs(group) do main[index]=true end
      end
    end
  end
end

local function fillSourceGap(img,src,x1,y1,x2,y2)
  for y=y1,y2 do
    for x=x1,x2 do
      local c=src:getPixel(x,y)
      if p.rgbaA(c)>0 and p.rgbaA(img:getPixel(x,y))==0 then
        img:drawPixel(x,y,nearest(c))
      end
    end
  end
end

local function process(src, folder, name)
  assert(src.width == 256 and src.height == 224)
  if folder == 'idle' then
    local result = concept:clone()
    local frame = tonumber(name:match('frame%-(%d+)'))
    -- Keep the neck and torso joined. The former one-pixel head translation
    -- severed their shared outline and exposed a horizontal seam in-game.
    if frame == 3 or frame == 4 then
      result:drawPixel(107, 104, p.rgba(70, 42, 43, 255))
      result:drawPixel(108, 104, p.rgba(70, 42, 43, 255))
    elseif frame == 7 or frame == 8 then
      result:drawPixel(116, 84, p.rgba(191, 141, 106, 255))
      result:drawPixel(117, 84, p.rgba(191, 141, 106, 255))
    end
    return result
  end
  local result = Image(256, 224, ColorMode.RGB)
  local legMask = makeLegMask(src)
  for q in src:pixels() do
    local c = q()
    if p.rgbaA(c) > 0 then
      local x, y = q.x, q.y
      if legMask[y * 256 + x] then
        result:drawPixel(x, y, trouserShade(c))
      elseif y >= 149 and y <= 169 and x >= 68 and x <= 180 and isGarment(c) then
        -- Preserve the source silhouette below the coat. Dropping these
        -- pixels made both legs appear severed and left jagged red cavities
        -- once the combat outline was drawn behind the sprite.
        result:drawPixel(x, y, trouserShade(c))
      else
        result:drawPixel(x, y, nearest(c))
      end
    end
  end

  local dx, dy = headOffset(src, folder, name)
  local weapon=weaponCoreMask(src)
  -- Clear the old long hair/ribbon. Its loose strands are wider than the
  -- standing head mask, especially in the running pose.
  for y = 69 + dy, 128 + dy do
    for x = 84 + dx, 157 + dx do
      if x >= 0 and x < 256 and y >= 0 and y < 224 then
        local sx, sy = x - dx, y - dy
        if sy <= 122 or idleHeadMask[sy * 256 + sx] then result:drawPixel(x, y, transparent) end
        local c = src:getPixel(x, y)
        if p.rgbaA(c) > 0 then
          local r,g,b = p.rgbaR(c),p.rgbaG(c),p.rgbaB(c)
          if g > r * 1.10 and g > b * 1.1 then result:drawPixel(x, y, transparent) end
        end
      end
    end
  end
  for y = 70, 127 do
    for x = 84, 146 do
      local c = concept:getPixel(x, y)
      if p.rgbaA(c) > 0 then
        local tx, ty = x + dx, y + dy
        if tx >= 0 and tx < 256 and ty >= 0 and ty < 224 then result:drawPixel(tx, ty, c) end
      end
    end
  end
  -- Sword blades and raised hands cross the head region in guarding and
  -- overhead strikes. Those foreground parts stay on top of the new head.
  for y = 69 + dy, 128 + dy do
    for x = 84 + dx, 157 + dx do
      if x >= 0 and x < 256 and y >= 0 and y < 224 then
        local c = src:getPixel(x, y)
        if (restoreSword(c) and nearSteel(weapon, x, y, 2)) or
          (y < 90 + dy and isSkin(c) and x > 106 + dx and
            nearSteel(weapon, x, y, 5)) then
          result:drawPixel(x, y, nearest(c))
        end
      end
    end
  end
  if folder:find('slash') then
    local highGrip = 0
    for y = 55, 79 do
      for x = 90, 145 do
        if isSkin(src:getPixel(x, y)) then highGrip = highGrip + 1 end
      end
    end
    if highGrip >= 8 then
      -- Overhead cuts bring the sword arm in front of the hair. Follow the
      -- sleeve's diagonal from the lifted hand to its shoulder joint.
      for y = 72 + dy, 131 + dy do
        local center = 117 + dx + (y - (72 + dy)) * .34
        for x = math.floor(center - 7), math.ceil(center + 7) do
          if x >= 0 and x < 256 and y >= 0 and y < 224 then
            local c = src:getPixel(x, y)
            local r, g, b = p.rgbaR(c), p.rgbaG(c), p.rgbaB(c)
            if p.rgbaA(c) > 0 and
              ((isSkin(c) and y < 92 + dy) or
               (r < 145 and r > g + 6 and g < 105 and b < 105)) then
              result:drawPixel(x, y, nearest(c))
            end
          end
        end
      end
    end
    local highLeftBlade = 0
    for y = 35, 94 do for x = 0, 74 do
      if restoreSword(src:getPixel(x, y)) then highLeftBlade = highLeftBlade + 1 end
    end end
    if highLeftBlade >= 15 then
      -- Upper-left cuts put the forward sleeve in front of the new face.
      for y = 94 + dy, 128 + dy do
        local center = 78 + dx + (y - (94 + dy)) * 1.42
        for x = math.floor(center - 9), math.ceil(center + 9) do
          if x >= 0 and x < 256 and y >= 0 and y < 224 then
            local c = src:getPixel(x, y)
            local r, g, b = p.rgbaR(c), p.rgbaG(c), p.rgbaB(c)
            local sleeve = p.rgbaA(c) > 0 and r < 145 and r > g + 6 and
              g < 105 and b < 105 and not donorHair[c]
            local hand = x < 96 + dx and y < 113 + dy and isSkin(c)
            if sleeve or hand then result:drawPixel(x, y, nearest(c)) end
          end
        end
      end
    end
  end
  -- The source animation crosses the old head with the sword and forearms.
  -- Recover the actual foreground pieces and the coat below the collar
  -- where the head transfer cleared them. Keep old hair and ribbon out.
  for y = math.max(54, 69 + dy), math.min(145, 137 + dy) do
    for x = math.max(55, 84 + dx), math.min(190, 157 + dx) do
      local c = src:getPixel(x, y)
      if p.rgbaA(c) > 0 and p.rgbaA(result:getPixel(x, y)) == 0 and
        not donorHair[c] and not donorRibbon[c] then
        local nearBlade = nearSteel(weapon, x, y, 4)
        local garmentBelowHead = y >= 115 + dy and isGarment(c)
        if (nearBlade and (restoreSword(c) or isSkin(c) or isGarment(c))) or
          garmentBelowHead then
          result:drawPixel(x, y, nearest(c))
        end
      end
    end
  end
  if folder=='poses' and name=='hurt.png' then
    -- The source's windblown crown starts four rows before the transplanted
    -- short hair. Remove that old fringe and its adjacent outline, which
    -- otherwise appears as a floating horizontal mark above Cadet B.
    for y=73,76 do
      for x=119,133 do result:drawPixel(x,y,transparent) end
    end
  end
  cleanPixelIntegrity(result,src)
  reconnectSourceSilhouette(result,src,folder..'/'..name)
  if folder=='blunt-3' and
    (name=='frame-08.png' or name=='frame-09.png' or name=='frame-10.png') then
    fillSourceGap(result,src,124,108,134,116)
  elseif folder=='pierce' and
    (name=='frame-05.png' or name=='frame-06.png') then
    fillSourceGap(result,src,87,129,92,135)
  end
  cleanPixelIntegrity(result,src)
  return result
end

local folders = {'idle', 'move', 'poses', 'slash', 'slash-2', 'slash-3',
  'pierce', 'pierce-2', 'pierce-3', 'blunt', 'blunt-2', 'blunt-3'}
local total = 0
for _, folder in ipairs(folders) do
  local names = {}
  if folder == 'poses' then
    names = {'block.png','block-2.png','hurt.png','hurt-2.png'}
  else
    local count = folder == 'idle' and 8 or folder == 'move' and 1 or 12
    for i = 1, count do names[#names + 1] = string.format('frame-%02d.png', i) end
  end
  for _, name in ipairs(names) do
    local src = Image { fromFile = sourceRoot .. folder .. '/' .. name }
    local result = process(src, folder, name)
    result:saveAs(targetRoot .. folder .. '/' .. name)
    total = total + 1
  end
end
print('CADET B: exported ' .. total .. ' PNGs at 256x224 using approved concept palette (' .. #palette .. ' colors).')
