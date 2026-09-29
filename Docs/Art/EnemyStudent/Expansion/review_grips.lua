local out='C:/Users/user/Documents/SwordMasterStory/Aseprite_Character/Enemy_Expansion'
local s=app.open(out..'/Enemy_AllAttacks3_Hurt.aseprite')
local im=Image(512,448,ColorMode.RGB)
for p in im:pixels() do p(app.pixelColor.rgba(38,35,39,255)) end
for i,f in ipairs({95,97,98,101}) do im:drawSprite(s,f,Point((i-1)%2*256,math.floor((i-1)/2)*224)) end
local big=Image(1024,896,ColorMode.RGB)
for p in big:pixels() do p(im:getPixel(math.floor(p.x/2),math.floor(p.y/2))) end
big:saveAs(out..'/blunt-2-grip-review.png')
print('Four corrected whole-weapon poses exported')
