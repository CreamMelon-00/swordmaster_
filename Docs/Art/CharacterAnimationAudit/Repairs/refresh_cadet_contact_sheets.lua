-- Refresh only the two contact sheets from current runtime PNGs.
-- Layouts match CadetA/Tools/build_cadet_a.lua and CadetB/validate_cadet_b.lua.
local root=app.params.root or 'C:/Users/User/Documents/swordmaster_'
local pc=app.pixelColor
local specs={
 {variant='CadetA',cols=3,rows=3,scale=2,file='cadet-a-motion-contact.png',order={
  'idle/frame-01','move/frame-01','slash/frame-05',
  'slash-2/frame-05','slash-3/frame-05','pierce/frame-05',
  'blunt/frame-05','poses/block','poses/hurt',
 }},
 {variant='CadetB',cols=6,rows=3,scale=1,file='cadet-b-contact.png',order={
  'idle/frame-01','move/frame-01','poses/block','poses/block-2',
  'poses/hurt','poses/hurt-2','slash/frame-01','slash/frame-04',
  'slash/frame-07','slash-2/frame-05','pierce/frame-01',
  'pierce/frame-04','pierce/frame-07','pierce/frame-10',
  'blunt/frame-01','blunt/frame-04','blunt/frame-07','blunt/frame-10',
 }},
}
for _,s in ipairs(specs) do
 local base=root..'/Assets/Game/Resources/EnemyVariants/'..s.variant..'/Animations/'
 local sheet=Image(256*s.cols,224*s.rows,ColorMode.RGB)
 for i,name in ipairs(s.order) do
  local im=Image{fromFile=base..name..'.png'}
  assert(im.width==256 and im.height==224)
  local ox,oy=((i-1)%s.cols)*256,math.floor((i-1)/s.cols)*224
  -- The original contact sheets preserve each occupied source pixel exactly.
  for q in im:pixels() do
   if pc.rgbaA(q())>0 then sheet:drawPixel(ox+q.x,oy+q.y,q()) end
  end
 end
 if s.scale>1 then sheet:resize(sheet.width*s.scale,sheet.height*s.scale) end
 local path=root..'/Docs/Art/EnemyVariants/'..s.variant..'/'..s.file
 sheet:saveAs(path)
 print(s.variant..' contact updated: '..sheet.width..'x'..sheet.height..', '..#s.order..' poses')
end
