from pathlib import Path
import re, uuid
root=Path(r'C:\Fork\swordmaster_')
p=root/'Assets/Game/Presentation'
def edit(name, changes):
    file=p/name; t=file.read_text(encoding='utf-8-sig')
    for a,b in changes:
        assert a in t,(name,a[:70]); t=t.replace(a,b)
    file.write_text(t,encoding='utf-8')
def remove_method(t, signature):
    start=t.index(signature); opening=t.index('{',start); depth=1; end=opening+1
    while depth:
        depth+= (t[end]=='{')-(t[end]=='}'); end+=1
    return t[:start]+t[end:]

edit('AcademyUiSkin.cs',[
 ('private static Sprite panel, button, crest, titleBackground;', 'private static Sprite panel, button, crest, titleBackground;\n        public static Sprite GearWheel => Resources.Load<Sprite>(ResourceFolder + "gear-wheel");\n        public static Sprite GearPinion => Resources.Load<Sprite>(ResourceFolder + "gear-pinion");\n        public static Sprite GearClock => Resources.Load<Sprite>(ResourceFolder + "gear-clock");\n        public static Sprite GaugeBezel => Resources.Load<Sprite>(ResourceFolder + "gauge-bezel");\n        public static Sprite GaugeFill => Resources.Load<Sprite>(ResourceFolder + "gauge-fill");\n        public static Sprite MenuPlaque => Resources.Load<Sprite>(ResourceFolder + "menu-plaque");')])

file=p/'LegacyCombatHud.SkillGears.cs'; t=file.read_text(encoding='utf-8-sig')
start=t.index('        // Draws the lane gear'); end=t.index('        // A socket on the rim:',start)
t=t[:start]+'''        // Imported images are shared across all lanes and HUDs. Geometry still controls placement and rotation.
        private void DrawGearPictures(GearGeometry geometry, int gearTeeth, int idlerTeeth)
        {
            wheelSprite = AcademyUiSkin.GearWheel;
            idlerSprite = AcademyUiSkin.GearPinion;
            assetsAvailable &= wheelSprite != null && idlerSprite != null;
            foreach (SkillGear gear in gears) gear.Wheel.sprite = wheelSprite;
            foreach (Image image in idlers) image.sprite = idlerSprite;
        }

        private void ReleaseGearPictures()
        {
            // Only the runtime socket is owned by this HUD; imported gears must survive HUD disposal.
            DuelGearShimmer.Release(socketSprite);
            DuelGearShimmer.Release(socketTexture);
            wheelSprite = idlerSprite = socketSprite = null;
            socketTexture = null;
        }

'''+t[end:]
t=t.replace('private Texture2D wheelTexture, idlerTexture, socketTexture;', 'private Texture2D socketTexture;')
t=t.replace('        private (float, int, float) wheelPicture, idlerPicture;\n','')
t=t.replace('private static readonly Color GearBrass = Color.Lerp(DuelVisualTheme.Border, DuelVisualTheme.Accent, .4f);','private static readonly Color GearBrass = Color.white;')
t=t.replace('private static readonly Color IdlerBrass = DuelVisualTheme.Border;', 'private static readonly Color IdlerBrass = Color.white;')
t=t.replace('            StartIdlers();','            cycleAnimation?.Play();\n            StartIdlers();')
file.write_text(t,encoding='utf-8')
edit('LegacyCombatHud.TimerGear.cs',[
 ('private Texture2D timerGearTexture, steamTexture;', 'private Texture2D steamTexture;'),
 ('            timerGearTexture = GearTexture("Planning Timer Gear", TimerGearTexels, 12, .78f, .58f, .28f, .11f, 5);\n            timerGearSprite = DuelGearShimmer.CreateSprite(timerGearTexture);','            timerGearSprite = AcademyUiSkin.GearClock;\n            assetsAvailable &= timerGearSprite != null;'),
 ('            DuelGearShimmer.Release(timerGearSprite);\n            DuelGearShimmer.Release(timerGearTexture);\n',''),
 ('timerGearTexture = steamTexture = null;', 'steamTexture = null;'),
 ('? DuelVisualTheme.Danger : GearBrass;', '? DuelVisualTheme.Danger : Color.white;')])
for name,old,new in [('DuelLoadingHud.cs','gearSprite = DuelGearShimmer.CreateSprite(gearTexture);','gearSprite = AcademyUiSkin.GearClock;'),('DuelGearShimmer.cs','gearSprite = CreateSprite(gearTexture);','gearSprite = AcademyUiSkin.GearClock;')]:
    file=p/name; t=file.read_text(encoding='utf-8-sig')
    t=t.replace('            gearTexture = CreateGearTexture();\n','').replace(old,new)
    t=t.replace('            DuelGearShimmer.Release(gearSprite);\n','').replace('            DuelGearShimmer.Release(gearTexture);\n','')
    t=t.replace('            Release(gearSprite);\n','').replace('            Release(gearTexture);\n','')
    t=t.replace('private readonly Texture2D gearTexture, glintTexture;', 'private readonly Texture2D glintTexture;')
    t=t.replace('private Texture2D gearTexture, steamTexture;', 'private Texture2D steamTexture;')
    t=remove_method(t,'        private static Texture2D CreateGearTexture()')
    t=t.replace('Color.Lerp(DuelVisualTheme.Border, DuelVisualTheme.Accent, .45f)', 'Color.white')
    t=t.replace('new Color(Brass.r, Brass.g, Brass.b, GearOpacity', 'new Color(1f, 1f, 1f, GearOpacity')
    file.write_text(t,encoding='utf-8')

edit('LegacyCombatHud.cs',[
 ('        private readonly Outline guideCommitFocus;', '        private AcademyControlAnimation cycleAnimation, breathAnimation;\n        private readonly Outline guideCommitFocus;'),
 ('            inputs = controls.gameObject.AddComponent<CanvasGroup>();', '            inputs = controls.gameObject.AddComponent<CanvasGroup>();\n            Image("Instrument Bed", controls, null, new Vector2(0, 96), new Vector2(532, 64), Track);\n            Image("Instrument Bezel", controls, AcademyUiSkin.GaugeBezel, new Vector2(0, 96), new Vector2(544, 72));'),
 ('            BuildCommandKeycap(breathe.transform, "S");', '            BuildCommandKeycap(breathe.transform, "S");\n            breathAnimation = new AcademyControlAnimation(breathIcon, "Valve", .65f);'),
 ('            BuildCommandKeycap(cycle.transform, "Shift");', '            BuildCommandKeycap(cycle.transform, "Shift");\n            cycleAnimation = new AcademyControlAnimation(cycle.transform.Find("Icon").GetComponent<Image>(), "Lever", .45f);'),
 ('new Vector2(520, 8)', 'new Vector2(520, 12)'),
 ('new Vector2(2, 8)', 'new Vector2(2, 12)'),
 ('actFill = Image("Act_Gauge", controls, white,', 'actFill = Image("Act_Gauge", controls, AcademyUiSkin.GaugeFill,'),
 ('new Vector2(520, 12), Accent);', 'new Vector2(520, 12), Color.white);'),
 ('new Vector2(520f, 6f)', 'new Vector2(520f, 10f)'),
 ('timerFill = Image("Timer", timerPanel, white,', 'timerFill = Image("Timer", timerPanel, AcademyUiSkin.GaugeFill,'),
 ('new Vector2(520f, 10f), Accent);', 'new Vector2(520f, 10f), Color.white);'),
 ('timerFill.color = timeLow ? DuelVisualTheme.Danger : Accent;', 'timerFill.color = timeLow ? DuelVisualTheme.Danger : Color.white;'),
 ('            if (shownBreathsRemaining == remaining) return;', '            if (shownBreathsRemaining == remaining) return;\n            if (shownBreathsRemaining >= 0 && remaining < shownBreathsRemaining) breathAnimation?.Play();'),
 ('            AdvanceLaneTurns(actualDelta);', '            AdvanceLaneTurns(actualDelta);\n            cycleAnimation?.Advance(actualDelta);\n            breathAnimation?.Advance(actualDelta);')])

edit('DuelLoadingHud.cs',[
 ('title.text = "기사학교 기계식 전송 장치";', 'title.text = "기사학교";'),
 ('            Panel("Loading Rail", assembly, new Vector2(38f, 4f), new Vector2(528f, 20f),\n                DuelVisualTheme.Border, false);', '            Image("Loading Rail", assembly, AcademyUiSkin.GaugeBezel, new Vector2(38f, 4f), new Vector2(544f, 40f), Color.white);'),
 ('new Vector2(520f, 12f)', 'new Vector2(500f, 16f)'),
 ('DuelVisualTheme.Accent, false);\n            // uGUI', 'Color.white, false);\n            // uGUI'),
 ('fill.sprite = Resources.Load<Sprite>("LegacyHud/white");', 'fill.sprite = AcademyUiSkin.GaugeFill;'),
 ('new Vector2(337f, 4f)', 'new Vector2(346f, 4f)')])

edit('TitleHud.cs',[
 ('primary ? DuelVisualTheme.Accent : DuelVisualTheme.RaisedSurface);\n            image.raycastTarget', 'Color.white);\n            image.sprite = AcademyUiSkin.MenuPlaque;\n            image.type = Image.Type.Sliced;\n            image.pixelsPerUnitMultiplier = .5f;\n            image.raycastTarget'),
 ('            DuelVisualTheme.StyleButton(button, primary);', '            DuelVisualTheme.StyleButton(button, primary);\n            image.transform.Find("Brass Trim").gameObject.SetActive(false);\n            var marker = Panel("Primary Marker", image.transform, new Vector2(-size.x * .5f + 26f, 0), new Vector2(4, size.y - 28), DuelVisualTheme.Accent);\n            marker.gameObject.SetActive(primary);'),
 ('primary ? DuelVisualTheme.Ink : DuelVisualTheme.Foreground).text = caption;', 'DuelVisualTheme.Foreground).text = caption;'),
 ('continueButton.targetGraphic.color = canContinue ? DuelVisualTheme.Accent : DuelVisualTheme.RaisedSurface;', 'continueButton.targetGraphic.color = Color.white;\n            continueButton.transform.Find("Primary Marker").gameObject.SetActive(canContinue);'),
 ('newGameButton.targetGraphic.color = canContinue ? DuelVisualTheme.RaisedSurface : DuelVisualTheme.Accent;', 'newGameButton.targetGraphic.color = Color.white;\n            newGameButton.transform.Find("Primary Marker").gameObject.SetActive(!canContinue);'),
 ('continueCaption.color = canContinue ? DuelVisualTheme.Ink : DuelVisualTheme.Muted;', 'continueCaption.color = canContinue ? DuelVisualTheme.Foreground : DuelVisualTheme.Muted;'),
 ('newGameCaption.color = canContinue ? DuelVisualTheme.Foreground : DuelVisualTheme.Ink;', 'newGameCaption.color = DuelVisualTheme.Foreground;')])

# Import metadata, including a sliced plaque. PNG ownership remains with Unity Resources.
base=(root/'Assets/Game/Resources/AcademyUI/academy-crest.png.meta').read_text()
for file in (root/'Assets/Game/Resources/AcademyUI').rglob('*.png'):
    if file.with_suffix('.png.meta').exists(): continue
    m=re.sub(r'guid: [a-f0-9]+','guid: '+uuid.uuid4().hex,base)
    m=m.replace('maxTextureSize: 256','maxTextureSize: 512')
    if file.stem=='menu-plaque': m=m.replace('spriteBorder: {x: 0, y: 0, z: 0, w: 0}','spriteBorder: {x: 38, y: 8, z: 30, w: 8}')
    file.with_suffix('.png.meta').write_text(m,encoding='utf-8')
