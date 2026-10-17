-- New whole-body key poses. Angle interpolation uses a signed authored arc;
-- the horizontal cut has one explicitly foreshortened passing drawing.
local K=dofile(app.params['root'] and app.params['root']..'/Docs/Art/EnemyVariants/CadetB/Rebuild/pose_library.lua' or 'C:/Users/User/Documents/swordmaster_/Docs/Art/EnemyVariants/CadetB/Rebuild/pose_library.lua')
local M={}
local function copy(t) local r={};for k,v in pairs(t) do if type(v)=='table' then r[k]={v[1],v[2]} else r[k]=v end end;return r end
local function pose(base,extra) local r=copy(base);r.head=nil;for k,v in pairs(extra or {}) do r[k]=v end;return r end
local function mix(a,b,t)
 local r={};local vectorKeys={'waist','guard','nearAnkle','farAnkle'}
 for _,k in ipairs(vectorKeys) do
  local av=a[k] or (k=='nearAnkle' and {80,197}) or (k=='farAnkle' and {165,197})
  local bv=b[k] or (k=='nearAnkle' and {80,197}) or (k=='farAnkle' and {165,197})
  r[k]={av[1]+(bv[1]-av[1])*t,av[2]+(bv[2]-av[2])*t}
 end
 for _,k in ipairs({'lean','headTilt','turn','depth'}) do local def=k=='depth' and 1 or 0;r[k]=(a[k] or def)+((b[k] or def)-(a[k] or def))*t end
 local delta=(b.angle-a.angle+180)%360-180;r.angle=a.angle+delta*t
 for _,k in ipairs({'farElbow','nearElbow','weaponBehind','expression'}) do r[k]=t<.5 and a[k] or b[k] end
 return r
end
local ready=K.ready
local overhead=pose(K.high,{waist={127,153},guard={123,81},angle=-48})
local passing=pose(K.cut,{waist={122,159},lean=-4,guard={79,104},angle=-150,nearAnkle={75,197},farAnkle={168,197}})
local downContact=K.cut
local downLow=pose(K.follow,{waist={119,165},guard={81,148},angle=163})
local downFollow=K.follow
local returnA=pose(ready,{waist={120,160},lean=-4,guard={110,136},angle=120,nearElbow=-1,nearAnkle={73,197},farAnkle={168,197}})
local returnB=pose(ready,{waist={121,157},guard={129,132},angle=60,nearElbow=-1,nearAnkle={76,197},farAnkle={166,197}})
local returnC=pose(ready,{waist={122,155},guard={136,150},angle=35,nearAnkle={78,197},farAnkle={165,197}})
M.slash={copy(ready),mix(ready,overhead,.55),overhead,passing,downContact,downLow,downFollow,returnA,returnB,returnC,copy(ready),copy(ready)}

local lowReady=pose(ready,{waist={125,156},lean=-2,guard={131,165},angle=18,nearElbow=1})
local risingPass=pose(K.cut,{waist={122,161},guard={99,154},angle=-160,nearAnkle={76,197},farAnkle={168,197}})
local risingHit=pose(K.cut,{waist={120,160},guard={82,125},angle=-150,nearAnkle={74,197},farAnkle={168,197}})
local risingFollow=pose(risingHit,{waist={122,157},lean=-2,guard={92,108},angle=-130,nearElbow=1})
local highFollow=pose(ready,{waist={125,154},lean=0,guard={113,91},angle=-95,nearElbow=1,farElbow=-1,weaponBehind=true,nearAnkle={82,197},farAnkle={168,197}})
M['slash-2']={copy(ready),mix(ready,lowReady,.7),lowReady,risingPass,risingHit,risingFollow,highFollow,pose(highFollow,{guard={118,90},angle=-70}),pose(highFollow,{guard={129,108},angle=-30}),returnC,copy(ready),copy(ready)}

local sideWind=pose(ready,{waist={128,155},lean=2,guard={154,128},angle=-5,depth=.85,weaponBehind=true,nearElbow=1,nearAnkle={85,197},farAnkle={168,197},turn=2})
local sidePass=pose(ready,{waist={124,158},lean=-2,guard={112,128},angle=-175,depth=.48,nearElbow=-1,nearAnkle={79,197},farAnkle={168,197}})
local sideHit=pose(K.cut,{waist={120,160},lean=-4,guard={78,134},angle=180,nearAnkle={73,197},farAnkle={169,197},headTilt=0})
local sideFollow=pose(sideHit,{waist={119,161},guard={86,141},angle=175})
M['slash-3']={copy(ready),mix(ready,sideWind,.65),sideWind,sidePass,sideHit,pose(sideHit,{guard={81,136},angle=178}),sideFollow,returnA,returnB,returnC,copy(ready),copy(ready)}

local thrustReady=pose(ready,{waist={127,154},lean=0,guard={139,137},angle=180,nearElbow=1,weaponBehind=true,nearAnkle={84,197},farAnkle={168,197}})
local thrustPass=pose(thrustReady,{waist={125,158},lean=-3,guard={106,138},nearElbow=-1,weaponBehind=false,nearAnkle={79,197},farAnkle={169,197}})
local thrustHit=pose(K.cut,{waist={122,162},lean=-6,guard={78,140},angle=180,nearAnkle={74,197},farAnkle={171,197},headTilt=-2})
local thrustReturn=pose(thrustReady,{waist={124,159},lean=-3,guard={110,140},nearElbow=-1,weaponBehind=false,nearAnkle={79,197},farAnkle={169,197}})
M.pierce={copy(ready),mix(ready,thrustReady,.7),thrustReady,thrustPass,thrustHit,pose(thrustHit,{guard={79,140},waist={121,163}}),pose(thrustHit,{guard={91,140}}),thrustReturn,pose(thrustReturn,{guard={128,142},angle=155}),returnC,copy(ready),copy(ready)}

local highThrustReady=pose(thrustReady,{guard={134,119},waist={128,153},angle=178,nearElbow=1})
local highThrustPass=pose(thrustPass,{guard={105,116},waist={125,156},angle=179})
local highThrustHit=pose(thrustHit,{guard={80,114},waist={122,159},lean=-4,angle=180,nearAnkle={76,197},farAnkle={170,197},headTilt=-2})
local highThrustReturn=pose(thrustReturn,{guard={111,118},waist={126,155},angle=178,nearAnkle={81,197}})
M['pierce-2']={copy(ready),mix(ready,highThrustReady,.7),highThrustReady,highThrustPass,highThrustHit,pose(highThrustHit,{guard={82,114}}),pose(highThrustHit,{guard={93,116}}),highThrustReturn,pose(returnB,{guard={131,129},angle=70}),returnC,copy(ready),copy(ready)}

local lowThrustReady=pose(thrustReady,{guard={142,148},waist={128,156},angle=175})
local lowThrustPass=pose(thrustPass,{guard={109,154},waist={124,162},lean=-5,angle=172})
local lowThrustHit=pose(thrustHit,{guard={88,160},waist={120,165},lean=-7,angle=170,nearAnkle={73,197},farAnkle={171,197},headTilt=-2})
M['pierce-3']={copy(ready),mix(ready,lowThrustReady,.7),lowThrustReady,lowThrustPass,lowThrustHit,pose(lowThrustHit,{guard={89,160}}),pose(lowThrustHit,{guard={99,157}}),pose(thrustReturn,{guard={117,150},waist={124,159},angle=160}),returnB,returnC,copy(ready),copy(ready)}

local pommelReady=pose(ready,{waist={126,154},lean=0,guard={145,125},angle=0,nearElbow=1,nearAnkle={83,197},farAnkle={167,197}})
local pommelPass=pose(pommelReady,{waist={124,158},lean=-2,guard={124,129},nearElbow=-1,nearAnkle={79,197},farAnkle={168,197}})
local pommelHit=pose(K.cut,{waist={121,160},lean=-4,guard={109,131},angle=0,nearAnkle={76,197},farAnkle={170,197},headTilt=-2})
M.blunt={copy(ready),mix(ready,pommelReady,.7),pommelReady,pommelPass,pommelHit,pose(pommelHit,{guard={110,131}}),pose(pommelPass,{guard={117,129}}),pommelPass,pose(pommelReady,{waist={125,155},guard={138,134}}),returnC,copy(ready),copy(ready)}

local underReady=pose(pommelReady,{guard={141,148},waist={126,157},angle=-15})
local underPass=pose(pommelPass,{guard={122,132},waist={123,160},angle=15})
local underHit=pose(pommelHit,{guard={109,115},waist={123,158},lean=-3,angle=35,nearAnkle={78,197},farAnkle={168,197},nearElbow=1})
M['blunt-2']={copy(ready),mix(ready,underReady,.7),underReady,underPass,underHit,pose(underHit,{guard={110,113},angle=38}),pose(underHit,{guard={117,117},angle=30}),pose(pommelPass,{guard={128,126},angle=15}),pommelReady,returnC,copy(ready),copy(ready)}

local hammerReady=pose(overhead,{guard={129,96},angle=30,waist={127,153},nearElbow=1,farElbow=-1})
local hammerPass=pose(pommelPass,{guard={115,120},angle=-10})
local hammerHit=pose(pommelHit,{guard={104,146},waist={120,163},lean=-5,angle=-35,nearAnkle={74,197},farAnkle={171,197}})
M['blunt-3']={copy(ready),mix(ready,hammerReady,.65),hammerReady,hammerPass,hammerHit,pose(hammerHit,{guard={105,149},waist={119,164},angle=-35}),pose(hammerHit,{guard={114,147},angle=-28}),pommelPass,pommelReady,returnC,copy(ready),copy(ready)}

M.idle={};for i=1,8 do M.idle[i]=pose(ready,{waist={123,154+(({0,0,-.5,-1,-1,-.5,0,0})[i])}}) end
M.idle[7].expression='blink'
M.poses={
 block=pose(ready,{waist={124,156},lean=-2,guard={109,137},angle=-80,nearElbow=1,farElbow=-1}),
 ['block-2']=pose(ready,{waist={124,156},lean=-1,guard={100,135},angle=-135,nearElbow=1,farElbow=-1}),
 hurt=pose(ready,{waist={125,161},lean=3,guard={135,164},angle=12,headTilt=5,expression='hurt',nearElbow=1,nearAnkle={78,197},farAnkle={166,197}}),
 ['hurt-2']=pose(ready,{waist={125,163},lean=7,guard={141,145},angle=10,headTilt=8,expression='hurt',nearElbow=1,nearAnkle={78,197},farAnkle={166,197}}),
}
M.move={pose(ready,{waist={124,155},lean=-10,guard={145,143},angle=28,nearAnkle={86,197},farAnkle={154,185},nearElbow=1,headTilt=-4})}
M.copy=copy
return M
