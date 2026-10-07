local root='C:/Users/User/Documents/swordmaster_'
local base=root..'/Assets/Game/Resources/EnemyVariants/CadetA/Animations/'
local donor=root..'/Assets/Game/Resources/EnemyStudent/Animations/'
local p=app.pixelColor
local function pixelIntegrity(img)
  local filled,seen={},{}
  for q in img:pixels() do
    if p.rgbaA(q())>0 then filled[q.y*256+q.x]=true end
  end
  local singles,small,holes,points,substantial=0,0,0,{},{}
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
      if #queue==1 then
        singles=singles+1
        points[#points+1]=string.format('(%d,%d)',index%256,math.floor(index/256))
      end
      if #queue<=3 then small=small+1 end
      if #queue>3 then substantial[#substantial+1]=#queue end
    end
  end
  for y=70,145 do
    for x=65,175 do
      local index=y*256+x
      if not filled[index] and filled[index-1] and filled[index+1] and
        filled[index-256] and filled[index+256] then holes=holes+1 end
    end
  end
  table.sort(substantial,function(a,b) return a>b end)
  return singles,small,holes,table.concat(points,', '),substantial
end
local order={}
for _,v in ipairs({{'idle',8},{'slash',12},{'slash-2',12},{'slash-3',12},
  {'pierce',12},{'pierce-2',12},{'pierce-3',12},
  {'blunt',12},{'blunt-2',12},{'blunt-3',12}}) do
  for i=1,v[2] do order[#order+1]=v[1]..'/'..string.format('frame-%02d',i) end
end
for _,v in ipairs({'poses/block','poses/block-2','poses/hurt','poses/hurt-2','move/frame-01'}) do
  order[#order+1]=v
end
assert(#order==121)
local native=app.open(root..'/Docs/Art/EnemyVariants/CadetA/CadetA-AllAnimations.aseprite')
assert(#native.frames==121 and #native.tags==15,'Native Aseprite frame/tag count changed')
local globalColors={}
local integrityErrors={}
for index,relative in ipairs(order) do
  local target=Image{fromFile=base..relative..'.png'}
  local source=Image{fromFile=donor..relative..'.png'}
  assert(target.width==256 and target.height==224,relative..' dimensions')
  local rendered=Image(256,224,ColorMode.RGB)
  rendered:drawSprite(native,index)
  assert(rendered:isEqual(target),relative..' native frame differs from Unity PNG')
  local colors,semi={},0
  local minx,miny,maxx,maxy=256,224,-1,-1
  local sourceMinX,sourceMaxX,sourceMaxY=256,-1,-1
  for q in target:pixels() do
    local c=q();local alpha=p.rgbaA(c)
    if alpha>0 and alpha<255 then semi=semi+1 end
    if alpha>0 then
      colors[c]=true;globalColors[c]=true
      minx=math.min(minx,q.x);miny=math.min(miny,q.y)
      maxx=math.max(maxx,q.x);maxy=math.max(maxy,q.y)
    end
    if p.rgbaA(source:getPixel(q.x,q.y))>0 then
      sourceMinX=math.min(sourceMinX,q.x)
      sourceMaxX=math.max(sourceMaxX,q.x)
      sourceMaxY=math.max(sourceMaxY,q.y)
    end
  end
  assert(semi==0,relative..' has semitransparent pixels')
  if relative=='poses/hurt' then
    for y=65,76 do for x=118,132 do
      assert(p.rgbaA(target:getPixel(x,y))==0,
        'Old hair cap reappeared above hurt pose at '..x..','..y)
    end end
  end
  assert(maxy==sourceMaxY,relative..' planted foot baseline changed')
  assert(math.abs(minx-sourceMinX)<=8 and math.abs(maxx-sourceMaxX)<=8,
    relative..' weapon or silhouette extent changed unexpectedly')
  local n=0;for _ in pairs(colors) do n=n+1 end
  assert(n<=80,relative..' >80 colors')
  local singles,small,holes,points,components=pixelIntegrity(target)
  local oldSingles,oldSmall,oldHoles,_,oldComponents=pixelIntegrity(source)
  if singles>oldSingles or small>oldSmall or holes>oldHoles then
    integrityErrors[#integrityErrors+1]=string.format(
      '%s: islands 1px %d>%d, islands <=3px %d>%d, enclosed head holes %d>%d [%s]',
      relative,singles,oldSingles,small,oldSmall,holes,oldHoles,points)
  end
  if #components>#oldComponents then
    integrityErrors[#integrityErrors+1]=string.format(
      '%s: %d substantial components, original has %d (%s versus %s)',
      relative,#components,#oldComponents,table.concat(components,','),
      table.concat(oldComponents,','))
  else
    for component=2,#components do
      if components[component]>oldComponents[component] then
        integrityErrors[#integrityErrors+1]=string.format(
          '%s: detached component %d is %d pixels, original %d',
          relative,component,components[component],oldComponents[component])
      end
    end
  end
end
assert(#integrityErrors==0,'New detached pixels or head holes:\n'..table.concat(integrityErrors,'\n'))
native:close()
local first=Image{fromFile=base..'idle/frame-01.png'}
local concept=Image{fromFile=root..'/Docs/Art/EnemyVariants/Concepts/cadet-a-idle-concept-256x224.png'}
assert(first:isEqual(concept),'Standing pose no longer matches approved concept')
local i3=Image{fromFile=base..'idle/frame-03.png'}
local i7=Image{fromFile=base..'idle/frame-07.png'}
assert(not first:isEqual(i3) and not first:isEqual(i7) and not i3:isEqual(i7),'Idle breathing lost')
local globalCount=0;for _ in pairs(globalColors) do globalCount=globalCount+1 end
print('VERIFIED: Cadet A 121 native/PNG frames, 15 tags, approved idle, binary alpha, feet and weapon bounds, '..globalCount..' colors.')
