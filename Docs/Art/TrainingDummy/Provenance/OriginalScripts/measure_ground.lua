local out='C:/Users/user/Documents/SwordMasterStory/Aseprite_Character/Training_Dummy'
local pc=app.pixelColor
local report=assert(io.open(out..'/ground-check.txt','w'))
for _,path in ipairs({out..'/Frames/idle/frame-01.png','C:/Fork/swordmaster_/Assets/Game/Resources/SwordGirl/Animations/idle/frame-01.png','C:/Fork/swordmaster_/Assets/Game/Resources/EnemyStudent/Animations/idle/frame-01.png'}) do
 local im=Image{fromFile=path};local y0,y1=999,-1
 for p in im:pixels() do if pc.rgbaA(p())>0 then y0=math.min(y0,p.y);y1=math.max(y1,p.y) end end
 report:write(path..': top='..y0..' bottom='..y1..'\n')
end
report:close()
