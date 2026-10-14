from pathlib import Path
import json

root = Path(r'C:\Fork\swordmaster_')
presentation = root / 'Assets/Game/Presentation'
def modify(name, changes):
    path = presentation / name
    text = path.read_text(encoding='utf-8-sig')
    for old, new in changes:
        assert old in text, (name, old[:90])
        text = text.replace(old, new)
    path.write_text(text, encoding='utf-8')

palette = {'15262e':'29231c','20373e':'382d23','1b3037':'30291f','2f5057':'51412b','68898b':'957449','d8aa6d':'c8a365','f3e9d3':'e9debf','9bb8b5':'b7aa8e','e0d0ae':'d8c8a4','d69078':'c58069','0e181e':'1e211f','89ae91':'789878','91b7c1':'91aaa7'}
modify('DuelVisualTheme.cs', [(a,b) for a,b in palette.items()] + [
    ('Cool academy slate, warm light and restrained clockwork metal.', 'Walnut, parchment and restrained warm brass.'),
    ('if (AcademyUiSkin.ApplyFrame(image)) return;', '// Functional surfaces use a fine trim; illustrated controls have their own silhouettes.'),
    ('            AcademyUiSkin.ApplyFrame(button.targetGraphic as Image, true);\n', ''),
    ('float inset = ornate ? 6f : 2f;', 'float inset = 1f;'),
    ('Outline(vertices, bounds, 0f, 2f, shade);', 'Outline(vertices, bounds, 0f, 1f, shade);')])
# Preserve the already approved title veil rather than recolouring its background treatment.
modify('AcademyUiSkin.cs', [('Color a = DuelVisualTheme.Track, b = a;', 'Color a = new Color32(14, 24, 30, 255), b = a;')])

modify('LegacyCombatHud.cs', [
    ('DockWidth = 980f, DockHeight = 200f', 'DockWidth = 1240f, DockHeight = 248f'),
    ('public const float ActTrackY = 60f;', 'public const float ActTrackY = 76f;'),
    ('            Dress(inputImage);', '            BuildCommandDesk(inputImage);'),
    ('            Image("Left Desk Join", controls, white, new Vector2(-503f, -4f), new Vector2(34f, 4f), Border);\n            Image("Right Desk Join", controls, white, new Vector2(503f, -4f), new Vector2(34f, 4f), Border);',
     '            Text("Desk Heading", controls, new Vector2(-472f, 72f), new Vector2(218f, 24f), 13, TextAnchor.MiddleCenter).text = "전투 준비";'),
    ('new Vector2(2f, 112f), dividerInk', 'new Vector2(1f, 146f), dividerInk'),
    ('new Vector2(-EndButtonX, -4f), new Vector2(96, 112)', 'new Vector2(-EndButtonX, -16f), new Vector2(96, 132)'),
    ('            var logButton = AddButton(logImage);', '            var logButton = AddButton(logImage);\n            ConfigureCommandControl(logImage, logButton, "HudActions/combat-log", 64f, new Vector2(0f, 16f), logImage.transform.Find("Icon").GetComponent<Image>());'),
    ('new Vector2(360, -4), new Vector2(136, 112)', 'new Vector2(360, -4), new Vector2(152, 172)'),
    ('new Vector2(-42, 17), Vector2.one * 36', 'new Vector2(0f, 20f), Vector2.one * 96f'),
    ('new Vector2(22, 27), new Vector2(84, 22), 17', 'new Vector2(0, -35), new Vector2(140, 24), 19'),
    ('new Vector2(22, 3), new Vector2(84, 18)', 'new Vector2(0, -59), new Vector2(140, 18)'),
    ('new Vector2(0, -18), new Vector2(124, 18)', 'new Vector2(0, -79), new Vector2(140, 18)'),
    ('new Vector2(0, -42), new Vector2(124, 18)', 'new Vector2(0, 78), new Vector2(140, 22)'),
    ('            breathButton = AddButton(breathe);', '            breathButton = AddButton(breathe);\n            ConfigureCommandControl(breathe, breathButton, "AcademyUI/breath-valve", 96f, new Vector2(0f, 20f), breathIcon);\n            BuildCommandKeycap(breathe.transform, "S");'),
    ('new Vector2(-360, -4), new Vector2(136, 112)', 'new Vector2(-360, -4), new Vector2(152, 172)'),
    ('new Vector2(0, 27), new Vector2(124, 22), 17', 'new Vector2(0, -35), new Vector2(140, 24), 19'),
    ('new Vector2(0, 3), new Vector2(124, 18)', 'new Vector2(0, -59), new Vector2(140, 18)'),
    ('            cycleButton = AddButton(cycle);', '            cycleButton = AddButton(cycle);\n            ConfigureCommandControl(cycle, cycleButton, "AcademyUI/cycle-lever", 96f, new Vector2(0f, 20f));\n            BuildCommandKeycap(cycle.transform, "Shift");'),
    ('new Vector2(EndButtonX, -4f), new Vector2(96, 112)', 'new Vector2(EndButtonX, -4f), new Vector2(108, 172)'),
    ('            commitButton = AddButton(start, true);', '            commitButton = AddButton(start, true);\n            ConfigureCommandControl(start, commitButton, "AcademyUI/commit-seal", 96f, new Vector2(0f, 18f), start.transform.Find("Icon").GetComponent<Image>());\n            commitLabel.rectTransform.anchoredPosition = new Vector2(0f, -43f);\n            commitLabel.fontSize = 19;\n            start.transform.Find("KeyHint").GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -71f);'),
    ('new Vector2(0f, 84f), new Vector2(540f, 32f)', 'new Vector2(0f, 108f), new Vector2(540f, 32f)')])
modify('LegacyCombatHud.SkillGears.cs', [
    ('GearCentreY = -96f', 'GearCentreY = -100f'),
    ('CurrentSkillSize = 52f', 'CurrentSkillSize = 60f'),
    ('SkillWindowSize = 66f', 'SkillWindowSize = 76f'),
    ('new Vector2(150f, 20f), 15', 'new Vector2(154f, 24f), 18'),
    ('resizeTextMaxSize = 15;', 'resizeTextMaxSize = 18;'),
    ('new Vector2(24f, 22f), 20', 'new Vector2(26f, 24f), 22'),
    ('                var keyLabel = Text("Key",', '                Image("Key Plate", window.transform, null, new Vector2(-18f, KeyRow), new Vector2(28f, 26f), Track);\n                var keyLabel = Text("Key",')])
modify('LegacyCombatHud.Keywords.cs', [('new Vector2(38f, -42f), new Vector2(58f, 18f)', 'new Vector2(0f, -99f), new Vector2(100f, 18f)')])
