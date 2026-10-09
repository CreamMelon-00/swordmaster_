-- Assert every frame in the tagged Aseprite master equals its logical PNG.
local root=assert(app.params.root,'Pass --script-param root=<absolute workspace path>')
local base=root..'/Docs/Art/SkillIcons/Unified/'
local native=app.open(base..'UnifiedSkillIcons-48.aseprite')
assert(#native.frames==20 and #native.tags==20,'20 frames/tags required')
for n=1,20 do
  assert(native.tags[n].name=='role'..n,'role tag order changed at '..n)
  assert(native.tags[n].fromFrame.frameNumber==n and
         native.tags[n].toFrame.frameNumber==n,'role tag span changed at '..n)
  local expected=Image{fromFile=base..'Output/48/role'..n..'.png'}
  local rendered=Image(48,48,ColorMode.RGB)
  rendered:drawSprite(native,n)
  assert(rendered:isEqual(expected),'native/PNG mismatch: role'..n)
end
print('ICON MASTER PASS: 20 Aseprite frames match 20 logical PNGs exactly.')

