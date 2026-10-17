local root=app.params['root'] or 'C:/Users/User/Documents/swordmaster_'
local here=root..'/Docs/Art/EnemyVariants/CadetB/Rebuild/'
local draw=dofile(here..'draw_character.lua');local poses=dofile(here..'pose_library.lua')
local out=here..'Pilot';app.fs.makeAllDirectories(out)
local names={'ready','high','cut','follow','recover'}
local sheet=Image(256*#names,224,ColorMode.RGB)
local sprite=Sprite(256,224,ColorMode.RGB);sprite.layers[1].name='Cadet B - newly authored full body'
for i,name in ipairs(names) do
 local im=draw.draw(poses[name]);im:saveAs(out..'/'..name..'.png')
 sheet:drawImage(im,Point((i-1)*256,0))
 if i>1 then sprite:newEmptyFrame(i) end
 sprite.frames[i].duration=.2;sprite:newCel(sprite.layers[1],i,im,Point(0,0))
 print('NEW_BODY '..name)
end
sheet:saveAs(out..'/key-poses.png');sprite:saveAs(out..'/CadetB-Pilot.aseprite')
