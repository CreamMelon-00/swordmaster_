-- Native Aseprite derivation of the approved black-haired school cadet.
-- Every output cel retains the source cel's occupied motion, alpha and foot pivot.
local root = 'C:/Users/User/Documents/swordmaster_'
local sourceRoot = root .. '/Assets/Game/Resources/EnemyStudent/Animations/'
local outputRoot = root .. '/Assets/Game/Resources/EnemyVariants/CadetA/Animations/'
local artRoot = root .. '/Docs/Art/EnemyVariants/CadetA/'
local pixel = app.pixelColor
local concept = Image { fromFile = root .. '/Docs/Art/EnemyVariants/Concepts/cadet-a-idle-concept-256x224.png' }
local transparent = pixel.rgba(0, 0, 0, 0)
local conceptPalette, conceptSeen = {}, {}
for q in concept:pixels() do
  local c = q()
  if pixel.rgbaA(c) > 0 and not conceptSeen[c] then
    conceptSeen[c] = true; conceptPalette[#conceptPalette + 1] = c
  end
end
assert(#conceptPalette <= 80, 'Approved A concept palette changed')
local nearestCache = {}
local function nearest(c)
  local value = nearestCache[c]
  if value then return value end
  local r,g,b = pixel.rgbaR(c),pixel.rgbaG(c),pixel.rgbaB(c)
  local best,bestScore=conceptPalette[1],math.huge
  for _,v in ipairs(conceptPalette) do
    local dr,dg,db=r-pixel.rgbaR(v),g-pixel.rgbaG(v),b-pixel.rgbaB(v)
    local score=2*dr*dr+3*dg*dg+2*db*db
    if score<bestScore then best,bestScore=v,score end
  end
  nearestCache[c]=best
  return best
end

local function rgb(c) return pixel.rgba(c[1], c[2], c[3], 255) end
local function remap(from, to, table)
  assert(#from == #to)
  for index = 1, #from do table[rgb(from[index])] = rgb(to[index]) end
end

local originalHair = {
  {57,34,42}, {85,43,49}, {117,55,60}, {148,71,73},
  {178,94,88}, {202,119,104}, {221,146,120},
}
local blackHair = {
  {25,21,33}, {36,30,46}, {51,44,61}, {69,60,78},
  {88,78,96}, {112,101,117}, {143,132,145},
}
local ribbon = {
  {33,56,45}, {43,84,55}, {65,116,68},
  {95,151,85}, {137,179,110}, {180,204,146},
}
local blueEye = {
  {35,52,63}, {48,75,92}, {68,107,127},
  {93,138,156}, {129,172,184}, {178,204,208},
}
local darkRibbon = {
  {25,21,33}, {36,30,46}, {51,44,61},
  {69,60,78}, {88,78,96}, {112,101,117},
}
local oldBlade = {
  {40,32,47}, {56,43,62}, {77,56,81},
  {100,75,102}, {128,101,128}, {155,131,154},
}
local steelBlade = {
  {30,31,41}, {48,51,64}, {70,76,91},
  {104,111,128}, {147,155,173}, {192,201,213},
}
local oldBrass = {
  {140,86,56}, {170,106,61}, {181,121,72}, {199,132,71},
  {201,144,96}, {226,157,80}, {240,175,94}, {246,203,125},
}
local pewter = {
  {89,92,103}, {105,111,120}, {118,124,136}, {134,140,152},
  {148,154,163}, {164,173,183}, {184,193,202}, {210,216,216},
}
local scarfBlue = {
  {45,53,69}, {53,68,87}, {64,82,105}, {72,94,121},
  {79,102,132}, {103,126,153}, {129,151,177}, {166,187,201},
}

local hairMap, ribbonMap, eyeMap, bladeMap, trimMap, scarfMap = {}, {}, {}, {}, {}, {}
remap(originalHair, blackHair, hairMap)
remap(ribbon, darkRibbon, ribbonMap)
remap(ribbon, blueEye, eyeMap)
remap(oldBlade, steelBlade, bladeMap)
remap(oldBrass, pewter, trimMap)
remap(oldBrass, scarfBlue, scarfMap)

local hairSet, ribbonSet = {}, {}
for _, color in ipairs(originalHair) do hairSet[rgb(color)] = true end
for _, color in ipairs(ribbon) do ribbonSet[rgb(color)] = true end
local darkOutline = {
  [rgb({51,25,30})] = true, [rgb({58,29,33})] = true,
  [rgb({65,34,36})] = true,
}
local idleSource = Image { fromFile = sourceRoot .. 'idle/frame-01.png' }
local idleHeadMask = {}
for y=69,128 do for x=84,157 do
  local c=idleSource:getPixel(x,y)
  if pixel.rgbaA(c)>0 and (y<=115 or x<114 or x>134) then
    idleHeadMask[y*256+x]=true
  end
end end

local function isSkin(c)
  if pixel.rgbaA(c)==0 then return false end
  local r,g,b=pixel.rgbaR(c),pixel.rgbaG(c),pixel.rgbaB(c)
  return r>=180 and g>=104 and b>=84 and r>g+17 and g>=b-8
end

local function isSword(c)
  if pixel.rgbaA(c)==0 then return false end
  local r,g,b=pixel.rgbaR(c),pixel.rgbaG(c),pixel.rgbaB(c)
  return b>=r-2 and b>=g+9 and r<200
end

local function steelCore(c)
  local r,g,b=pixel.rgbaR(c),pixel.rgbaG(c),pixel.rgbaB(c)
  return pixel.rgbaA(c)>0 and r>=52 and r<200 and
    b>=r-2 and b>=g+9
end

local function weaponCoreMask(source)
  local filled,seen,weapon={},{},{}
  for point in source:pixels() do
    if steelCore(point()) then filled[point.y*256+point.x]=true end
  end
  for index in pairs(filled) do
    if not seen[index] then
      local queue={index};seen[index]=true
      local first,seed=1,false
      while first<=#queue do
        local current=queue[first];first=first+1
        local x,y=current%256,math.floor(current/256)
        -- The blade reaches beyond the body or above the head. The old
        -- silver collar uses these same colors but is a separate component.
        if y<160 and (x<65 or x>174 or y<62 or
          (x>145 and y<105)) then seed=true end
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

local function nearWeapon(weapon,x,y,radius)
  for yy=math.max(0,y-radius),math.min(223,y+radius) do
    for xx=math.max(0,x-radius),math.min(255,x+radius) do
      if weapon[yy*256+xx] then return true end
    end
  end
  return false
end

local function alphaComponents(filled)
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
      groups[#groups+1]=queue
    end
  end
  table.sort(groups,function(a,b) return #a>#b end)
  return groups
end

local function cleanPixelIntegrity(image,source,relative)
  local filled,sourceFilled={},{}
  for point in image:pixels() do
    local index=point.y*256+point.x
    if pixel.rgbaA(point())>0 then filled[index]=true end
    if pixel.rgbaA(source:getPixel(point.x,point.y))>0 then sourceFilled[index]=true end
  end
  local sourceSmall={}
  for _,group in ipairs(alphaComponents(sourceFilled)) do
    if #group<=3 then
      for _,value in ipairs(group) do sourceSmall[value]=true end
    end
  end
  for _,group in ipairs(alphaComponents(filled)) do
    if #group<=3 then
      local original=false
      for _,value in ipairs(group) do
        -- The donor blunt back-swing contains one loose black dot beside the
        -- sword; it is source art, but becomes conspicuous beside the shorter
        -- bob and has no role in the weapon silhouette.
        local donorStray=(relative=='blunt/frame-05' or
          relative=='blunt/frame-06') and value==97*256+151
        if sourceSmall[value] and not donorStray then original=true end
      end
      if not original then
        for _,value in ipairs(group) do
          image:drawPixel(value%256,math.floor(value/256),transparent)
          filled[value]=nil
        end
      end
    end
  end
  -- Close single transparent pinholes around the transferred head and neck;
  -- preserve holes already present in the original pose.
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
            local c=image:getPixel(xx,yy)
            if pixel.rgbaA(c)>0 then
              counts[c]=(counts[c] or 0)+1
              if counts[c]>bestCount then best,bestCount=c,counts[c] end
            end
          end
        end
        image:drawPixel(x,y,best)
        filled[index]=true
      end
    end
  end
end

local function alphaMask(image)
  local filled={}
  for point in image:pixels() do
    if pixel.rgbaA(point())>0 then filled[point.y*256+point.x]=true end
  end
  return filled
end

local function repairTopology(image,source,weapon)
  local sourceGroups=alphaComponents(alphaMask(source))
  local allowed=0
  local originalDetached={}
  for group=1,#sourceGroups do
    if #sourceGroups[group]>3 then
      allowed=allowed+1
      if group>1 then
        for _,value in ipairs(sourceGroups[group]) do originalDetached[value]=true end
      end
    end
  end
  for attempt=1,12 do
    local groups=alphaComponents(alphaMask(image))
    if #groups<=allowed then return end
    local detached=nil
    for group=2,#groups do
      local original=false
      for _,value in ipairs(groups[group]) do
        if originalDetached[value] then original=true;break end
      end
      if not original then detached=groups[group];break end
    end
    assert(detached,'No new detached component to repair')
    local main={}
    for _,value in ipairs(groups[1]) do main[value]=true end
    -- Distance from the main silhouette across transparent pixels. The
    -- backtrack is an 8-connected path to the nearest edge, rather than a
    -- hand-picked line for a particular attack frame.
    local queue,parent,distance={}, {}, {}
    for _,value in ipairs(groups[1]) do
      queue[#queue+1]=value;distance[value]=0
    end
    local first=1
    while first<=#queue do
      local current=queue[first];first=first+1
      local d=distance[current]
      if d<32 then
        local x,y=current%256,math.floor(current/256)
        for yy=math.max(0,y-1),math.min(223,y+1) do
          for xx=math.max(0,x-1),math.min(255,x+1) do
            local nextIndex=yy*256+xx
            if distance[nextIndex]==nil then
              distance[nextIndex]=d+1;parent[nextIndex]=current
              queue[#queue+1]=nextIndex
            end
          end
        end
      end
    end
    local start,best=nil,math.huge
    for _,value in ipairs(detached) do
      if distance[value] and distance[value]<best then
        start,best=value,distance[value]
      end
    end
    assert(start and best<=24,'Detached pose part is too far from body')
    local path={start}
    local current=start
    while distance[current]>0 do
      current=parent[current]
      path[#path+1]=current
    end
    local finish=path[#path]
    local ax,ay=start%256,math.floor(start/256)
    local bx,by=finish%256,math.floor(finish/256)
    local swordPixels=0
    for _,value in ipairs(detached) do
      if weapon[value] then swordPixels=swordPixels+1 end
    end
    local blade=swordPixels>=10 and best>=5
    local width=blade and (best>=12 and 2 or 1) or 0
    local length=math.sqrt((bx-ax)^2+(by-ay)^2)
    local nx,ny=length>0 and -(by-ay)/length or 0,
      length>0 and (bx-ax)/length or 0
    local edge=image:getPixel(ax,ay)
    local bladeCenter=nearest(rgb(steelBlade[4]))
    local bladeEdge=nearest(rgb(steelBlade[2]))
    for step=2,#path-1 do
      local value=path[step]
      local x,y=value%256,math.floor(value/256)
      for offset=-width,width do
        local xx,yy=math.floor(x+nx*offset+0.5),math.floor(y+ny*offset+0.5)
        if xx>=0 and xx<256 and yy>=0 and yy<224 and
          pixel.rgbaA(image:getPixel(xx,yy))==0 then
          local color=blade and (offset==0 and bladeCenter or bladeEdge) or edge
          image:drawPixel(xx,yy,color)
        end
      end
    end
    local repaired=alphaComponents(alphaMask(image))
    assert(#repaired<#groups,'Topology bridge did not connect the silhouette')
  end
  assert(false,'Too many detached pose parts to repair')
end

local function headOffset(source, relative)
  local right,top=-1,224
  for y=60,120 do for x=55,180 do
    local c=source:getPixel(x,y)
    if pixel.rgbaA(c)>0 then
      local r,g,b=pixel.rgbaR(c),pixel.rgbaG(c),pixel.rgbaB(c)
      if g>r*1.10 and g>b*1.1 then right=math.max(right,x);top=math.min(top,y) end
    end
  end end
  assert(right>0,'Missing original ribbon: '..relative)
  local dy=top-77
  if relative=='poses/hurt-2' then dy=5 end
  return right-140, math.max(-8,math.min(15,dy))
end

local function idleConcept(relative)
  local result=concept:clone()
  local frame=tonumber(relative:match('frame%-(%d+)'))
  local bob=(frame==3 or frame==4) and -1 or (frame==7 or frame==8) and 1 or 0
  if bob~=0 then
    -- Breathe through the jacket/scarf shading while keeping the approved
    -- silhouette intact. Moving a cropped upper-body patch left a transparent
    -- horizontal tear between the chest and waist in frames 3/4 and 7/8.
    for y=116,143 do for x=91,143 do
      local base=concept:getPixel(x,y)
      local shifted=concept:getPixel(x,y-bob)
      if pixel.rgbaA(base)>0 and pixel.rgbaA(shifted)>0 then
        result:drawPixel(x,y,shifted)
      end
    end end
  end
  return result
end

local function hairBounds(image)
  local left, top, right, bottom = image.width, image.height, -1, -1
  for point in image:pixels() do
    if hairSet[point()] then
      left = math.min(left, point.x); top = math.min(top, point.y)
      right = math.max(right, point.x); bottom = math.max(bottom, point.y)
    end
  end
  assert(right >= left, 'Hair disappeared from source pose')
  return left, top, right, bottom
end

local function transform(source, relative)
  assert(source.width == 256 and source.height == 224)
  if relative:find('^idle/') then return idleConcept(relative) end
  local hairLeft, hairTop = hairBounds(source)
  local output = Image(256, 224, ColorMode.RGB)
  for point in source:pixels() do
    local color = point()
    if pixel.rgbaA(color) > 0 then
      local x, y = point.x, point.y
      local changed = color
      if hairSet[color] then
        changed = hairMap[color]
      elseif ribbonSet[color] then
        if x > hairLeft + 29 or y < hairTop + 28 then changed = ribbonMap[color]
        else changed = eyeMap[color] end
      elseif bladeMap[color] ~= nil then
        changed = bladeMap[color]
      elseif trimMap[color] ~= nil then
        local onNeck = x >= hairLeft + 20 and x <= hairLeft + 54 and
          y >= hairTop + 51 and y <= hairTop + 78
        changed = onNeck and scarfMap[color] or trimMap[color]
      end
      if changed ~= 0 then output:drawPixel(x, y, nearest(changed)) end
    end
  end
  -- A small hair pin stays on the head across the original pose changes.
  local pinX, pinY = hairLeft + 43, hairTop + 18
  local pinColors = {
    {0, 1, {119,84,52}}, {1, 1, {187,141,76}},
    {2, 0, {223,187,119}}, {3, 0, {166,118,65}},
  }
  for _, part in ipairs(pinColors) do
    local x, y = pinX + part[1], pinY + part[2]
    local beneath = output:getPixel(x, y)
    if pixel.rgbaA(beneath) > 0 then output:drawPixel(x, y, nearest(rgb(part[3]))) end
  end
  local body=output:clone()
  local dx,dy=headOffset(source,relative)
  local weapon=weaponCoreMask(source)
  for y=69+dy,128+dy do for x=84+dx,157+dx do
    if x>=0 and x<256 and y>=0 and y<224 then
      local sx,sy=x-dx,y-dy
      if sy<=122 or idleHeadMask[sy*256+sx] then output:drawPixel(x,y,transparent) end
      local c=source:getPixel(x,y)
      if pixel.rgbaA(c)>0 then
        local r,g,b=pixel.rgbaR(c),pixel.rgbaG(c),pixel.rgbaB(c)
        if g>r*1.10 and g>b*1.1 then output:drawPixel(x,y,transparent) end
      end
    end
  end end
  for y=70,127 do for x=84,146 do
    local c=concept:getPixel(x,y)
    if pixel.rgbaA(c)>0 then
      local tx,ty=x+dx,y+dy
      if tx>=0 and tx<256 and ty>=0 and ty<224 then output:drawPixel(tx,ty,c) end
    end
  end end
  for y=69+dy,128+dy do for x=84+dx,157+dx do
    if x>=0 and x<256 and y>=0 and y<224 then
      local c=source:getPixel(x,y)
      if (isSword(c) and nearWeapon(weapon,x,y,2)) or
        (y<90+dy and isSkin(c) and x>106+dx and nearWeapon(weapon,x,y,5)) then
        output:drawPixel(x,y,body:getPixel(x,y))
      end
    end
  end end
  if relative:find('^slash') then
    local highGrip=0
    for y=55,79 do for x=90,145 do
      if isSkin(source:getPixel(x,y)) then highGrip=highGrip+1 end
    end end
    if highGrip>=8 then
      for y=72+dy,131+dy do
        local center=117+dx+(y-(72+dy))*.34
        for x=math.floor(center-7),math.ceil(center+7) do
          if x>=0 and x<256 and y>=0 and y<224 then
            local c=source:getPixel(x,y)
            local r,g,b=pixel.rgbaR(c),pixel.rgbaG(c),pixel.rgbaB(c)
            if pixel.rgbaA(c)>0 and
              ((isSkin(c) and y<92+dy) or (r<145 and r>g+6 and g<105 and b<105)) then
              output:drawPixel(x,y,body:getPixel(x,y))
            end
          end
        end
      end
    end
    local highLeftBlade=0
    for y=35,94 do for x=0,74 do
      if isSword(source:getPixel(x,y)) then highLeftBlade=highLeftBlade+1 end
    end end
    if highLeftBlade>=15 then
      -- The diagonal cut to the upper left puts the forearm across the old
      -- head area. Keep that sleeve and the forward hand in front of the bob.
      for y=94+dy,128+dy do
        local center=78+dx+(y-(94+dy))*1.42
        for x=math.floor(center-9),math.ceil(center+9) do
          if x>=0 and x<256 and y>=0 and y<224 then
            local c=source:getPixel(x,y)
            local r,g,b=pixel.rgbaR(c),pixel.rgbaG(c),pixel.rgbaB(c)
            local sleeve=pixel.rgbaA(c)>0 and r<145 and r>g+6 and
              g<105 and b<105 and not hairSet[c]
            local hand=x<96+dx and y<113+dy and isSkin(c)
            if sleeve or hand then output:drawPixel(x,y,body:getPixel(x,y)) end
          end
        end
      end
    end
  end
  if relative=='poses/hurt' then
    -- The donor's raised hair crown sits four pixels above the approved bob
    -- in this recoil pose. Its narrow cap survives the normal head rectangle
    -- and reads as a floating dark dash over the new head.
    for y=65,76 do
      for x=118,132 do output:drawPixel(x,y,transparent) end
    end
  end
  cleanPixelIntegrity(output,source,relative)
  repairTopology(output,source,weapon)
  cleanPixelIntegrity(output,source,relative)
  return output
end

local clips = {
  {'idle', 8}, {'slash', 12}, {'slash-2', 12}, {'slash-3', 12},
  {'pierce', 12}, {'pierce-2', 12}, {'pierce-3', 12},
  {'blunt', 12}, {'blunt-2', 12}, {'blunt-3', 12},
}
local poseFiles = {'poses/block', 'poses/block-2', 'poses/hurt', 'poses/hurt-2', 'move/frame-01'}
local native = Sprite(256, 224, ColorMode.RGB)
native.layers[1].name = 'Cadet A - black hair, blue scarf, pewter trim'
local frameIndex, contacts = 0, {}
local wanted = {
  ['idle/frame-01'] = true, ['move/frame-01'] = true,
  ['slash/frame-05'] = true, ['slash-2/frame-05'] = true,
  ['slash-3/frame-05'] = true, ['pierce/frame-05'] = true,
  ['blunt/frame-05'] = true, ['poses/block'] = true, ['poses/hurt'] = true,
}
local function writeOne(relative)
  local source = Image { fromFile = sourceRoot .. relative .. '.png' }
  local output = transform(source, relative)
  output:saveAs(outputRoot .. relative .. '.png')
  frameIndex = frameIndex + 1
  if frameIndex > 1 then native:newEmptyFrame(frameIndex) end
  native.frames[frameIndex].duration = relative:find('idle/') and .16 or .10
  native:newCel(native.layers[1], frameIndex, output, Point(0, 0))
  if wanted[relative] then contacts[relative] = output end
end
for _, clip in ipairs(clips) do
  local first = frameIndex + 1
  for index = 1, clip[2] do
    writeOne(clip[1] .. '/frame-' .. string.format('%02d', index))
  end
  native:newTag(first, frameIndex).name = clip[1]
end
for _, name in ipairs(poseFiles) do
  local first = frameIndex + 1
  writeOne(name)
  native:newTag(first, first).name = name:gsub('/', '-')
end
assert(frameIndex == 121)
native:saveAs(artRoot .. 'CadetA-AllAnimations.aseprite')

local order = {
  'idle/frame-01', 'move/frame-01', 'slash/frame-05',
  'slash-2/frame-05', 'slash-3/frame-05', 'pierce/frame-05',
  'blunt/frame-05', 'poses/block', 'poses/hurt',
}
local sheet = Image(256 * 3, 224 * 3, ColorMode.RGB)
for index, name in ipairs(order) do
  sheet:drawImage(contacts[name], Point(((index - 1) % 3) * 256,
    math.floor((index - 1) / 3) * 224))
end
sheet:resize(sheet.width * 2, sheet.height * 2)
sheet:saveAs(artRoot .. 'cadet-a-motion-contact.png')
print('CADET A: 121 frames, 15 tags, 9 contact poses exported.')
