local root=assert(app.params.root):gsub('\\','/')
local dir=root..'/Docs/Art/Movement/Revision2/EnemyStudent'
local src=Image{fromFile=dir..'/Candidate-4-v4/frame-01.png'}
local pc=app.pixelColor
local guide=Image(256,224,ColorMode.RGB)
for p in src:pixels() do
  local c=p()
  if pc.rgbaA(c)>0 then
    pcall(function()
      guide:drawPixel(p.x,p.y,pc.rgba(math.floor(pc.rgbaR(c)*.55),math.floor(pc.rgbaG(c)*.55),math.floor(pc.rgbaB(c)*.55),255))
    end)
  end
end
local function mark(x,y,r,c)
  for yy=y-r,y+r do for xx=x-r,x+r do
    if (xx-x)^2+(yy-y)^2<=r*r and xx>=0 and yy>=0 and xx<256 and yy<224 then
      guide:drawPixel(xx,yy,c)
    end
  end end
end
local function line(x0,y0,x1,y1,c)
  local n=math.max(math.abs(x1-x0),math.abs(y1-y0))
  for k=0,n do
    local x=math.floor(x0+(x1-x0)*k/n+.5)
    local y=math.floor(y0+(y1-y0)*k/n+.5)
    mark(x,y,2,c)
  end
end
local blue=pc.rgba(49,158,255,255)
local red=pc.rgba(255,62,44,255)
-- Blue: the former leading leg now trails on screen right.
line(104,157,119,174,blue)
line(119,174,137,190,blue)
line(137,190,143,200,blue)
-- Red: the formerly rear leg crosses in front and lands screen left.
line(123,157,109,174,red)
line(109,174,92,190,red)
line(92,190,84,200,red)
mark(84,200,5,red)
mark(143,200,5,blue)
local big=Image(1024,896,ColorMode.RGB)
for p in big:pixels() do p(guide:getPixel(math.floor(p.x/4),math.floor(p.y/4))) end
big:saveAs(dir..'/opposite-pose-guide.png')
