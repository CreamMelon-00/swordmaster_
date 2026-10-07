local root='C:/Users/User/Documents/swordmaster_'
local base=root..'/Assets/Game/Resources/EnemyVariants/CadetB/Animations/'
local originalBase=root..'/Assets/Game/Resources/EnemyStudent/Animations/'
local p=app.pixelColor
local representative={
  {'idle','frame-01.png'},{'move','frame-01.png'},{'poses','block.png'},{'poses','block-2.png'},
  {'poses','hurt.png'},{'poses','hurt-2.png'},{'slash','frame-01.png'},{'slash','frame-04.png'},
  {'slash','frame-07.png'},{'slash-2','frame-05.png'},{'pierce','frame-01.png'},
  {'pierce','frame-04.png'},{'pierce','frame-07.png'},{'pierce','frame-10.png'},
  {'blunt','frame-01.png'},{'blunt','frame-04.png'},{'blunt','frame-07.png'},
  {'blunt','frame-10.png'},
}
local contact=Image(256*6,224*3,ColorMode.RGB)
for i,v in ipairs(representative) do
  local img=Image{fromFile=base..v[1]..'/'..v[2]}
  local ox,oy=((i-1)%6)*256,math.floor((i-1)/6)*224
  for q in img:pixels() do
    local c=q();if p.rgbaA(c)>0 then contact:drawPixel(ox+q.x,oy+q.y,c) end
  end
end
contact:saveAs(root..'/Docs/Art/EnemyVariants/CadetB/cadet-b-contact.png')
local groups={'idle','move','poses','slash','slash-2','slash-3','pierce','pierce-2','pierce-3','blunt','blunt-2','blunt-3'}
local function pixelIntegrity(img)
  local filled,seen={},{}
  for q in img:pixels() do
    if p.rgbaA(q())>0 then filled[q.y*256+q.x]=true end
  end
  local singles,small,holes,points,groups=0,0,0,{},{}
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
      groups[#groups+1]=queue
    end
  end
  -- A transparent point inside the head/neck can become a red pinhole when
  -- the break silhouette is drawn behind the actor. Four solid cardinal
  -- neighbours establish an actual enclosed one-pixel hole.
  for y=70,145 do
    for x=65,175 do
      local index=y*256+x
      if not filled[index] and filled[index-1] and filled[index+1] and
        filled[index-256] and filled[index+256] then holes=holes+1 end
    end
  end
  return singles,small,holes,table.concat(points,', '),groups,filled
end
local function largestEnclosedPocket(filled)
  local seen,biggest={},0
  for y=0,223 do
    for x=0,255 do
      local index=y*256+x
      if not filled[index] and not seen[index] then
        local queue={index};seen[index]=true
        local first,touchesEdge=1,false
        while first<=#queue do
          local current=queue[first];first=first+1
          local cx,cy=current%256,math.floor(current/256)
          if cx==0 or cy==0 or cx==255 or cy==223 then touchesEdge=true end
          for _,nextIndex in ipairs({current-256,current+256,current-1,current+1}) do
            local nx,ny=nextIndex%256,math.floor(nextIndex/256)
            if nextIndex>=0 and nextIndex<256*224 and
              math.abs(nx-cx)+math.abs(ny-cy)==1 and
              not filled[nextIndex] and not seen[nextIndex] then
              seen[nextIndex]=true;queue[#queue+1]=nextIndex
            end
          end
        end
        if not touchesEdge then biggest=math.max(biggest,#queue) end
      end
    end
  end
  return biggest
end
local total,allColors=0,{}
local integrityErrors={}
for _,group in ipairs(groups) do
  local names={}
  if group=='poses' then names={'block.png','block-2.png','hurt.png','hurt-2.png'} else
    local count=group=='idle' and 8 or group=='move' and 1 or 12
    for i=1,count do names[#names+1]=string.format('frame-%02d.png',i) end
  end
  for _,name in ipairs(names) do
    local img=Image{fromFile=base..group..'/'..name}
    local source=Image{fromFile=originalBase..group..'/'..name}
    assert(img.width==256 and img.height==224,group..'/'..name..' dimensions')
    local colors,semi={},0
    local minx,miny,maxx,maxy=256,224,-1,-1
    local sourceMinX,sourceMaxX,sourceMaxY=256,-1,-1
    for q in img:pixels() do
      local c=q();local a=p.rgbaA(c)
      if a>0 and a<255 then semi=semi+1 end
      if a>0 then
        colors[c]=true;allColors[c]=true
        minx=math.min(minx,q.x);miny=math.min(miny,q.y)
        maxx=math.max(maxx,q.x);maxy=math.max(maxy,q.y)
      end
      if p.rgbaA(source:getPixel(q.x,q.y))>0 then
        sourceMinX=math.min(sourceMinX,q.x)
        sourceMaxX=math.max(sourceMaxX,q.x)
        sourceMaxY=math.max(sourceMaxY,q.y)
      end
    end
    assert(semi==0,group..'/'..name..' has semitransparent pixels')
    assert(minx>=0 and maxx<=255 and miny>=0 and maxy<=223,group..'/'..name..' bounds')
    assert(maxy==sourceMaxY,group..'/'..name..' planted-foot baseline changed')
    assert(math.abs(minx-sourceMinX)<=5 and math.abs(maxx-sourceMaxX)<=6,
      group..'/'..name..' weapon or silhouette extent changed unexpectedly')
    local n=0;for _ in pairs(colors) do n=n+1 end
    assert(n<=80,group..'/'..name..' >80 colors: '..n)
    local singles,small,holes,points,groups,filled=pixelIntegrity(img)
    local oldSingles,oldSmall,oldHoles,_,oldGroups,oldFilled=pixelIntegrity(source)
    if singles>oldSingles or small>oldSmall or holes>oldHoles then
      integrityErrors[#integrityErrors+1]=string.format(
        '%s/%s: islands 1px %d>%d, islands <=3px %d>%d, enclosed head holes %d>%d [%s]',
        group,name,singles,oldSingles,small,oldSmall,holes,oldHoles,points)
    end
    local sourceMain,targetMain=1,1
    for i=2,#oldGroups do
      if #oldGroups[i]>#oldGroups[sourceMain] then sourceMain=i end
    end
    for i=2,#groups do
      if #groups[i]>#groups[targetMain] then targetMain=i end
    end
    local sourceMainSet={}
    for _,index in ipairs(oldGroups[sourceMain]) do sourceMainSet[index]=true end
    for i,groupPixels in ipairs(groups) do
      if i~=targetMain and #groupPixels>=4 then
        local overlap=0
        for _,index in ipairs(groupPixels) do
          if sourceMainSet[index] then overlap=overlap+1 end
        end
        if overlap>=4 then
          integrityErrors[#integrityErrors+1]=string.format(
            '%s/%s: new detached body or weapon component of %d pixels',
            group,name,#groupPixels)
        end
      end
    end
    local oldPocket=largestEnclosedPocket(oldFilled)
    local newPocket=largestEnclosedPocket(filled)
    if newPocket>math.max(32,oldPocket) then
      integrityErrors[#integrityErrors+1]=string.format(
        '%s/%s: new enclosed transparency pocket of %d pixels (source %d)',
        group,name,newPocket,oldPocket)
    end
    total=total+1
  end
end
assert(#integrityErrors==0,'New detached pixels or head holes:\n'..table.concat(integrityErrors,'\n'))
local globalColors=0;for _ in pairs(allColors) do globalColors=globalColors+1 end
local native=app.open(root..'/Docs/Art/EnemyVariants/CadetB/CadetB-AllAnimations.aseprite')
assert(#native.frames==121 and #native.tags==15,'Native source frame/tag count changed')
local nativeOrder={}
for _,v in ipairs({{'idle',8},{'slash',12},{'slash-2',12},{'slash-3',12},
  {'pierce',12},{'pierce-2',12},{'pierce-3',12},
  {'blunt',12},{'blunt-2',12},{'blunt-3',12}}) do
  for i=1,v[2] do nativeOrder[#nativeOrder+1]=v[1]..'/'..string.format('frame-%02d',i) end
end
for _,v in ipairs({'poses/block','poses/block-2','poses/hurt','poses/hurt-2','move/frame-01'}) do
  nativeOrder[#nativeOrder+1]=v
end
assert(#nativeOrder==121)
for frame,relative in ipairs(nativeOrder) do
  local render=Image(256,224,ColorMode.RGB)
  render:drawSprite(native,frame)
  local png=Image{fromFile=base..relative..'.png'}
  assert(render:isEqual(png),relative..' differs from editable source')
end
native:close()
local idle1=Image{fromFile=base..'idle/frame-01.png'}
local idle3=Image{fromFile=base..'idle/frame-03.png'}
local idle7=Image{fromFile=base..'idle/frame-07.png'}
assert(not idle1:isEqual(idle3) and not idle1:isEqual(idle7) and not idle3:isEqual(idle7),
  'Idle breathing frames lost their pose variation')
print('VERIFIED: '..total..' native/PNG frames, 15 tags, 256x224, binary alpha, <=80 colors per frame, '..globalColors..' global colors.')
