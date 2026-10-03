local root=assert(app.params.root):gsub('\\','/')
local dir=root..'/Docs/Art/Movement/RunPose/SwordGirl'
local sprite=app.open(dir..'/SwordGirl-run.aseprite')
assert(sprite.width==256 and sprite.height==224)
assert(#sprite.frames==1)
local flattened=Image(256,224,ColorMode.RGB)
flattened:drawSprite(sprite,1)
local png=Image{fromFile=dir..'/frame-01.png'}
assert(flattened:isEqual(png),'Aseprite/PNG pixel mismatch')
print('Aseprite roundtrip matches final PNG')
