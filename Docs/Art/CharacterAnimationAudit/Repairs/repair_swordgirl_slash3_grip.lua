-- Direct pixel repair in Aseprite. Only runtime slash-3 frame 06 is touched.
-- The matching native character cel is frame 66 (1-based); its effects cel is separate.
-- Run from repository root with Aseprite -b --script this-file.lua.
local root=app.params.root or 'C:/Users/User/Documents/swordmaster_'
local path=root..'/Assets/Game/Resources/SwordGirl/Animations/slash-3/frame-06.png'
local source=Image{fromFile=app.params.source or path}
assert(source.width==256 and source.height==224)
local sprite=Sprite(256,224,ColorMode.RGB)
local correction=dofile(root..'/Docs/Art/CharacterAnimationAudit/Repairs/swordgirl_slash3_grip_pixels.lua')
local im=correction.apply(source)
sprite:newCel(sprite.layers[1],1,im,Point(0,0))
sprite:saveAs(app.params.output or path)
print('Repaired SwordGirl slash-3/06: closed fist and straight 72px blade at 10 degrees')
