-- Explicitly drawn foreground contours for the existing combat key poses.
-- These are arm/hand silhouettes, not a rectangular head replacement mask.
-- Run once against the existing exported cels; authored deltas are recorded
-- separately for the normal generators to replay after their old cleanup.
local root = app.params.repositoryRoot or 'C:/Users/User/Documents/swordmaster_'
local pc = app.pixelColor
local sourceRoot = root..'/Assets/Game/Resources/EnemyStudent/Animations/'
local oldHair = {{57,34,42},{85,43,49},{117,55,60},{148,71,73},{178,94,88},{202,119,104},{221,146,120}}
local blackHair = {{25,21,33},{36,30,46},{51,44,61},{69,60,78},{88,78,96},{112,101,117},{143,132,145}}
local oldBlade = {{40,32,47},{56,43,62},{77,56,81},{100,75,102},{128,101,128},{155,131,154}}
local steel = {{30,31,41},{48,51,64},{70,76,91},{104,111,128},{147,155,173},{192,201,213}}
local brass = {{140,86,56},{170,106,61},{181,121,72},{199,132,71},{201,144,96},{226,157,80},{240,175,94},{246,203,125}}
local pewter = {{89,92,103},{105,111,120},{118,124,136},{134,140,152},{148,154,163},{164,173,183},{184,193,202},{210,216,216}}
local function rgba(c) return pc.rgba(c[1],c[2],c[3],255) end
local aMap = {}
for _, pair in ipairs({{oldHair,blackHair},{oldBlade,steel},{brass,pewter}}) do
  for i,c in ipairs(pair[1]) do aMap[rgba(c)] = rgba(pair[2][i]) end
end
local function mapper(kind)
  local concept = Image{fromFile=root..'/Docs/Art/EnemyVariants/Concepts/cadet-'..kind:lower()..'-idle-concept-256x224.png'}
  local palette,seen,cache={},{},{}
  for q in concept:pixels() do local c=q();if pc.rgbaA(c)>0 and not seen[c] then seen[c]=true;palette[#palette+1]=c end end
  return function(c)
    if pc.rgbaA(c)==0 then return c end
    if kind=='A' then c=aMap[c] or c end
    if cache[c] then return cache[c] end
    local r,g,b=pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c)
    local best,score=palette[1],math.huge
    for _,v in ipairs(palette) do
      local dr,dg,db=r-pc.rgbaR(v),g-pc.rgbaG(v),b-pc.rgbaB(v)
      local d=2*dr*dr+3*dg*dg+2*db*db
      if d<score then best,score=v,d end
    end
    cache[c]=best;return best
  end
end
local function inside(poly,x,y)
  local odd=false;local j=#poly
  for i=1,#poly do
    local a,b=poly[i],poly[j]
    if (a[2]>y)~=(b[2]>y) and x<(b[1]-a[1])*(y-a[2])/(b[2]-a[2])+a[1] then odd=not odd end
    j=i
  end
  return odd
end
local contours={
  slash2={
    {{128,88},{143,88},{152,108},{148,116},{139,116},{134,107},{132,101},{129,115},{120,125},{110,125},{103,118},{103,109},{116,112},{125,106}},
  },
  overhead={
    {{103,62},{114,59},{119,65},{116,75},{111,84},{104,83},{101,75}},
    {{114,61},{123,60},{128,77},{132,90},{136,103},{140,118},{136,127},{127,123},{127,111},{124,99},{121,87},{117,77},{111,70}},
  },
  risingContact={
    {{74,100},{84,101},{89,108},{97,109},{123,113},{138,114},{142,124},{138,130},{126,131},{105,125},{91,121},{83,116},{76,114}},
  },
  raisedGuard={
    {{95,116},{108,115},{116,121},{120,128},{117,137},{112,143},{104,140},{96,134}},
    {{114,129},{123,125},{134,119},{141,119},{141,129},{130,140},{121,145},{113,141}},
  },
  horizontalCut={
    {{91,123},{103,121},{115,123},{132,117},{139,118},{141,126},{137,131},{124,137},{110,139},{95,138},{90,133}},
  },
  thrustLow={
    {{82,124},{94,124},{99,127},{122,125},{132,124},{135,131},{132,138},{117,140},{92,141},{82,138}},
  },
  thrustLowReturn={
    {{94,121},{104,121},{111,125},{133,123},{140,121},{142,132},{136,139},{125,141},{100,140},{94,134}},
  },
  thrustHighReady={
    {{127,102},{143,102},{147,111},{144,119},{137,120},{129,117},{126,112}},
    {{109,111},{119,109},{126,115},{128,124},{121,133},{113,134},{108,127},{105,115}},
    {{138,112},{146,111},{150,125},{145,134},{138,132},{134,123}},
  },
  thrustHighStep={
    {{104,111},{116,110},{122,115},{123,126},{116,133},{109,130},{103,123},{100,117}},
    {{113,115},{124,113},{135,118},{143,118},{144,129},{134,135},{126,133},{118,127}},
  },
  thrustHighContact={
    {{98,110},{110,110},{116,113},{138,111},{143,116},{142,128},{131,129},{119,125},{102,125},{97,120}},
  },
  thrustHighReturn={
    {{111,109},{120,108},{128,114},{128,125},{122,133},{114,135},{109,129},{108,118}},
    {{121,110},{133,109},{138,119},{143,122},{141,134},{132,138},{125,131},{122,122}},
  },
  pommelGuard={
    {{111,115},{121,116},{126,123},{124,133},{117,140},{109,137},{108,125}},
    {{128,111},{138,112},{145,126},{146,134},{138,138},{129,132},{123,127},{124,118}},
  },
  pommelStep={
    {{93,125},{100,121},{111,119},{119,121},{125,123},{137,115},{143,117},{143,129},{133,137},{118,141},{106,138},{94,134}},
  },
  pommelContact={
    {{72,126},{80,121},{95,119},{104,123},{112,127},{138,124},{143,129},{140,140},{102,142},{81,138},{72,134}},
  },
  risingGuard={
    {{104,115},{112,115},{117,121},{126,119},{135,123},{142,132},{140,142},{130,144},{120,137},{112,140},{104,138},{101,129}},
  },
  risingPommel={
    {{75,82},{84,83},{89,90},{96,95},{100,101},{114,108},{129,115},{136,122},{136,132},{126,132},{107,118},{93,107},{85,105},{80,98},{75,95}},
  },
  hammerReady={
    {{103,58},{113,55},{121,58},{124,68},{119,75},{111,76},{104,71}},
    {{103,70},{112,68},{115,75},{110,85},{104,83},{102,78}},
    {{114,63},{123,62},{128,77},{132,90},{136,103},{140,118},{136,127},{127,123},{127,111},{124,99},{121,87},{117,77},{111,71}},
  },
  hammerWindup={
    {{104,60},{116,55},{124,58},{127,68},{120,75},{109,74},{103,69}},
    {{117,65},{126,64},{132,80},{135,96},{135,112},{132,124},{125,125},{122,117},{123,102},{120,85},{115,74}},
  },
  hammerReturn={
    {{115,120},{123,116},{131,119},{133,129},{126,138},{117,142},{111,139},{110,131}},
    {{126,113},{137,114},{143,122},{147,135},{139,140},{132,133},{126,127}},
  },
}
local ranges={
  {'slash',2,2,'slash2'},{'slash',3,4,'overhead'},
  {'slash-2',5,7,'risingContact'},{'slash-2',8,10,'raisedGuard'},
  {'slash-3',5,7,'horizontalCut'},
  {'pierce',5,6,'thrustLow'},{'pierce',7,8,'thrustLowReturn'},
  {'pierce-2',2,3,'thrustHighReady'},{'pierce-2',4,4,'thrustHighStep'},
  {'pierce-2',5,7,'thrustHighContact'},{'pierce-2',8,10,'thrustHighReturn'},
  {'blunt',2,3,'pommelGuard'},{'blunt',4,4,'pommelStep'},
  {'blunt',5,6,'pommelContact'},{'blunt',7,7,'pommelStep'},{'blunt',8,9,'pommelGuard'},
  {'blunt-2',2,3,'risingGuard'},{'blunt-2',5,7,'risingPommel'},{'blunt-2',8,10,'risingGuard'},
  {'blunt-3',2,3,'hammerReady'},{'blunt-3',4,4,'hammerWindup'},{'blunt-3',8,10,'hammerReturn'},
}
local function restore(im,source,polys,map)
  local changes=0
  for y=45,154 do for x=65,160 do
    local hit=false
    for _,poly in ipairs(polys) do if inside(poly,x+.5,y+.5) then hit=true;break end end
    local c=source:getPixel(x,y)
    if hit and pc.rgbaA(c)>0 then
      local value=map(c)
      if im:getPixel(x,y)~=value then im:drawPixel(x,y,value);changes=changes+1 end
    end
  end end
  return changes
end
local headPolygons={
 A={{112,69},{124,70},{135,76},{139,86},{145,96},{146,101},{142,105},{132,109},{126,109},{122,108},{119,111},{114,111},{111,111},{106,111},{103,114},{93,115},{87,112},{91,105},{91,99},{86,97},{86,92},{91,84},{99,76}},
 B={{111,60},{116,66},{117,72},{123,71},{129,68},{128,73},{138,78},{140,87},{146,90},{144,96},{137,98},{137,103},{132,105},{128,111},{122,112},{119,110},{117,113},{110,113},{103,111},{99,107},{96,103},{96,99},{91,102},{87,100},{88,94},{84,91},{86,85},{89,81},{88,77},{98,74},{105,72}},
}
local function clearSpuriousConnectors(im,source,kind)
  local right,top=-1,224
  for y=60,120 do for x=55,180 do
    local c=source:getPixel(x,y);local r,g,b=pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c)
    if pc.rgbaA(c)>0 and g>r*1.10 and g>b*1.1 then right=math.max(right,x);top=math.min(top,y) end
  end end
  local dx,dy=right-140,math.max(-8,math.min(15,top-77))
  local concept=Image{fromFile=root..'/Docs/Art/EnemyVariants/Concepts/cadet-'..kind:lower()..'-idle-concept-256x224.png'}
  for y=90,148 do for x=60,175 do
    if pc.rgbaA(source:getPixel(x,y))==0 and pc.rgbaA(im:getPixel(x,y))>0 then
      local sx,sy=x-dx,y-dy
      local head=sx>=0 and sx<256 and sy>=0 and sy<224 and inside(headPolygons[kind],sx+.5,sy+.5) and pc.rgbaA(concept:getPixel(sx,sy))>0
      if not head then im:drawPixel(x,y,pc.rgba(0,0,0,0)) end
    end
  end end
end
local total=0
for _,kind in ipairs({'A','B'}) do
  local target=root..'/Assets/Game/Resources/EnemyVariants/Cadet'..kind..'/Animations/'
  local map=mapper(kind)
  for _,entry in ipairs(ranges) do for n=entry[2],entry[3] do
    local relative=entry[1]..'/frame-'..string.format('%02d',n)..'.png'
    local source=Image{fromFile=sourceRoot..relative};local im=Image{fromFile=target..relative}
    local before=Image(im)
    local changes=restore(im,source,contours[entry[4]],map)
    clearSpuriousConnectors(im,source,kind)
    if kind=='A' and entry[1]=='slash' and n==2 then
      -- The old head conversion left a gray donor-hair loop on the crown.
      -- This crown patch cannot intersect the hands or the raised blade.
      local concept=Image{fromFile=root..'/Docs/Art/EnemyVariants/Concepts/cadet-a-idle-concept-256x224.png'}
      for y=74,88 do for x=110,128 do im:drawPixel(x,y,concept:getPixel(x+2,y-1)) end end
    elseif kind=='B' and entry[1]=='blunt-3' and n==4 then
      -- Remove only the old neutral sleeve outside the raised shoulder;
      -- retain the skin and the olive/gold lapel starting at x109.
      local wedge={{99,111},{107,111},{108,115},{104,117},{99,115}}
      for y=111,117 do for x=99,108 do
        local c=im:getPixel(x,y)
        local skin=pc.rgbaR(c)>=180 and pc.rgbaG(c)>=104 and pc.rgbaB(c)>=84 and pc.rgbaR(c)>pc.rgbaG(c)+17
        if inside(wedge,x+.5,y+.5) and not skin then im:drawPixel(x,y,pc.rgba(0,0,0,0)) end
      end end
    end
    if not im:isEqual(before) then im:saveAs(target..relative);total=total+1 end
    print('ARM_REPAIR '..kind..' '..relative..' '..changes)
  end end
  -- Restore only the newly authored lower leg, preserving every existing
  -- lower-body pixel and using the correct target palette.
  for n=2,3 do
    local relative='slash-2/frame-'..string.format('%02d',n)..'.png'
    local source=Image{fromFile=sourceRoot..relative};local im=Image{fromFile=target..relative};local changes=0
    for y=171,201 do for x=85,109 do
      local c=source:getPixel(x,y)
      if pc.rgbaA(c)>0 and pc.rgbaA(im:getPixel(x,y))==0 then im:drawPixel(x,y,map(c));changes=changes+1 end
    end end
    if kind=='B' then
      local hand={{112,154},{127,154},{136,160},{136,168},{128,171},{116,170},{111,165}}
      for y=154,170 do for x=111,136 do
        if inside(hand,x+.5,y+.5) then
          local c=source:getPixel(x,y)
          if pc.rgbaA(c)>0 then im:drawPixel(x,y,map(c));changes=changes+1 end
        end
      end end
    end
    im:saveAs(target..relative);print('LEG_HAND_REPAIR '..kind..' '..relative..' '..changes)
  end
end
print('CADET_ARM_REPAIR_COMPLETED '..total)
