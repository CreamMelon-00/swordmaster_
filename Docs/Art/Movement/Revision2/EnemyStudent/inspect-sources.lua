local root=assert(app.params.root):gsub('\\','/')
local paths={
  root..'/Docs/Art/EnemyStudent/Enemy_Uniform_Animations.aseprite',
  root..'/Docs/Art/EnemyStudent/Enemy_Uniform_PixelMatched.aseprite',
  root..'/Docs/Art/Movement/Revision/EnemyStudent/EnemyStudent-move-revision.aseprite'
}
for _,p in ipairs(paths) do
  local s=app.open(p)
  print(p, s.width,s.height,'frames',#s.frames,'layers',#s.layers,'tags',#s.tags)
  for _,l in ipairs(s.layers) do print('layer',l.name,'visible',l.isVisible) end
  for _,t in ipairs(s.tags) do print('tag',t.name,t.fromFrame.frameNumber,t.toFrame.frameNumber) end
end
