using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.Cutscene;
using TurnLimbo.Runtime.Prologue;
using TurnLimbo.Runtime.Save;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>Cutscenes through the real controller. The production opening is only checked for being valid, so the
    /// author can rewrite it freely; the staging itself is checked with scripts written here.</summary>
    public sealed class CutscenePlayModeTests : InputTestFixture
    {
        private const string Staged = @"@fade out 0
@bars on 0
@camera -2 4 0
@actor elisa at -3 left
@actor elisa pose hurt
@actor dummy at 2
첫째
@fade in 1
@actor elisa pose idle
@actor elisa move 0 1 &
@camera reset 1
둘째
@image ForestArena/forest-far 0
@actor dummy hide
@actor knight at 3 right
@actor knight attack slash &
셋째
@image off 0";

        private const string Effects = @"@actor elisa at -5 right
@actor knight at 5 left
@flashback on 0
@ambience aura-loop 0
@sound flashback
(첫째)
@flashback off 0
@shake 0.5 1 &
@charge knight 1 hold &
@aura knight on 0
둘째
@charge knight stop
@actor senior at -8 right
@actor senior move -6 1 &
셋째
@actor senior attack slash &
넷째";

        [TestCase("forest-ominous")]
        [TestCase("flashback")]
        [TestCase("charge")]
        [TestCase("charge-cut")]
        [TestCase("aura-loop")]
        [TestCase("shake")]
        public void PlaceholderSounds_ArePresentShortAndMono(string name)
        {
            AudioClip clip = Resources.Load<AudioClip>(CutsceneStep.SoundFolder + name);
            Assert.That(clip, Is.Not.Null, "Resources/" + CutsceneStep.SoundFolder + name);
            Assert.That(clip.channels, Is.EqualTo(1));
            Assert.That(clip.length, Is.InRange(.3f, 6f));
            if (name == "aura-loop") Assert.That(clip.length, Is.EqualTo(2f).Within(.01f), "The loop is two seconds long.");
        }

        [Test]
        public void OpeningResource_IsPresentAndValid_AndItsImagesLoad()
        {
            TextAsset source = Resources.Load<TextAsset>(DuelPrototypeController.OpeningCutscene);
            Assert.That(source, Is.Not.Null, "Resources/" + DuelPrototypeController.OpeningCutscene + ".txt");
            CutsceneScript script = null;
            Assert.DoesNotThrow(() => script = CutsceneScriptParser.Parse(DuelPrototypeController.OpeningCutscene, source.text));
            Assert.That(script.Steps, Is.Not.Empty);
            foreach (CutsceneStep step in script.Steps.Where(step => step.Kind == CutsceneStepKind.Image && step.Resource != null))
                Assert.That(Resources.Load<Sprite>(step.Resource), Is.Not.Null, $"Line {step.SourceLineNumber}: {step.Resource}");
        }

        [Test]
        public void PrologueSceneResources_LoadAndParseWithTheirMissionsCast()
        {
            // The 서막's scenes have no dialogue to fall back on, so each must load and parse as the controller reads it.
            foreach (PrologueMission mission in PrologueMissions.All)
            {
                foreach (string path in new[] { mission.IntroCutscene, mission.OutroCutscene, mission.Empowerment?.Scene }.Where(path => path != null))
                {
                    TextAsset source = Resources.Load<TextAsset>(path);
                    Assert.That(source, Is.Not.Null, "Resources/" + path + ".txt");
                    Assert.DoesNotThrow(() => CutsceneScriptParser.Parse(path, source.text, mission.SceneCast), path);
                }
            }
        }

        [UnityTest]
        public IEnumerator NewGameFromTitle_PlaysTheOpeningFirst_AndEscapeSkipsToTheFirstBriefing()
        {
            yield return null;
            using (var scope = new CutsceneScope(saveToTemporaryStore: true))
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                Assert.That(controller.NewGameFromTitle(), Is.True);
                Assert.That(controller.IsPlayingCutscene, Is.True);
                Assert.That(controller.IsInTitle || controller.IsInBriefing || controller.IsInLobby, Is.False,
                    "The cutscene owns the screen.");
                Assert.That(controller.TitleHud.IsVisible || controller.BriefingHud.IsVisible, Is.False);
                Assert.That(controller.Hud.Root.activeSelf, Is.False, "No duel HUD over the cutscene.");
                Assert.That(controller.CutsceneHud.IsVisible, Is.True);
                Assert.That(controller.CutsceneHud.Root.GetComponent<Canvas>().sortingOrder, Is.EqualTo(CutsceneHud.SortingOrder));
                Assert.That(controller.StartMission(), Is.False);
                Assert.That(controller.StartCampaignStage(1), Is.False);
                Assert.That(scope.LoadSave().PrologueCleared, Is.Zero, "The new game is saved before the cutscene.");

                Press(keyboard.escapeKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Release(keyboard.escapeKey);
                Assert.That(controller.IsPlayingCutscene, Is.False, "Escape skips the whole cutscene.");
                Assert.That(controller.IsInBriefing, Is.True, "…and the story continues at the first briefing.");
                Assert.That(controller.BriefingHud.Mission.Number, Is.EqualTo(1));
                Assert.That(controller.IsShowingDialogue, Is.False);
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(controller.IsInBriefing, Is.True, "The skip key never reaches the briefing.");
                scope.AssertArenaRestored();
            }
        }

        [Test]
        public void DirectNewGame_DoesNotPlayTheOpening()
        {
            using (var scope = new CutsceneScope())
            {
                scope.Controller.StartNewGame();
                Assert.That(scope.Controller.IsPlayingCutscene, Is.False);
                Assert.That(scope.Controller.IsInBriefing, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator InjectedCutscene_StagesFiguresCameraAndScreens_ThenReturnsToTheLobby()
        {
            yield return null;
            using (var scope = new CutsceneScope())
            {
                DuelPrototypeController controller = scope.Controller;
                LegacyArenaView arena = controller.ArenaView;
                Assert.That(controller.IsInLobby, Is.True);
                Assert.That(controller.StartCutscene(CutsceneScriptParser.Parse("Cutscene/test", Staged)), Is.True);
                Assert.That(controller.IsPlayingCutscene, Is.True);
                Assert.That(controller.IsInLobby, Is.False);
                Assert.That(controller.LobbyHud.Root.activeSelf, Is.False);
                Assert.That(controller.StartCutscene(CutsceneScriptParser.Parse("Cutscene/again", "하나")), Is.False,
                    "One cutscene at a time.");

                // The instant steps before the first line are already on screen.
                Assert.That(controller.CutsceneHud.FadeAmount, Is.EqualTo(1f));
                Assert.That(controller.CutsceneHud.BarsAmount, Is.EqualTo(1f));
                Assert.That(arena.ArenaCamera.orthographicSize, Is.EqualTo(4f));
                Assert.That(arena.ArenaCamera.transform.localPosition.x, Is.EqualTo(-2f));
                Assert.That(arena.ArenaCamera.transform.localPosition.y, Is.EqualTo(-1.9f).Within(1e-4f),
                    "Zooming in lowers the camera so the feet stay put.");
                SpriteRenderer elisa = arena.PlayerRenderer, other = arena.EnemyRenderer;
                Assert.That(elisa.gameObject.activeSelf, Is.True);
                Assert.That(elisa.transform.localPosition.x, Is.EqualTo(-3f));
                Assert.That(elisa.flipX, Is.True, "Elisa is drawn facing right; facing left flips her.");
                Assert.That(elisa.sprite.name, Does.Contain("hurt"));
                Assert.That(arena.EnemyAppearance, Is.EqualTo(EnemyAppearance.TrainingDummy));
                Assert.That(other.gameObject.activeSelf, Is.True);
                Assert.That(other.transform.localPosition.x, Is.EqualTo(2f));
                Assert.That(other.flipX, Is.False);
                Assert.That(other.sprite.name, Does.StartWith("dummy-idle-frame-"));
                DialogueHud dialogue = controller.DialogueHud;
                Assert.That(dialogue.IsVisible && dialogue.IsCinematic, Is.True);
                Assert.That(dialogue.CurrentLine.Text, Is.EqualTo("첫째"));
                Assert.That(Label(dialogue.Root, "Dialogue Progress").text, Is.EqualTo("01 / 03"));
                Assert.That(Named(dialogue.Root, "Dialogue Close").GetComponentInChildren<Text>().text, Does.Contain("건너뛰기"));
                Assert.That(Named(dialogue.Root, "Dialogue Backdrop").GetComponent<Image>().color.a, Is.Zero,
                    "Lines do not dim the scene.");
                Assert.That(controller.IsShowingDialogue, Is.False, "Cutscene lines are not a dialogue session.");
                Assert.That(controller.CloseDialogue(), Is.False);
                Assert.That(dialogue.IsVisible, Is.True, "Closing dialogue leaves the cutscene's line alone.");

                scope.Advance(5f);
                Assert.That(dialogue.CurrentLine.Text, Is.EqualTo("첫째"), "Time alone never passes a line.");
                Named(dialogue.Root, "Dialogue Next").GetComponent<Button>().onClick.Invoke();
                Assert.That(dialogue.IsVisible, Is.False, "The box goes away while the scene moves.");
                Assert.That(controller.AdvanceCutscene(), Is.False, "Nothing to advance during a timed step.");
                scope.Advance(.5f);
                Assert.That(controller.CutsceneHud.FadeAmount, Is.InRange(.01f, .99f), "The fade is under way.");
                scope.Advance(.5f);
                Assert.That(controller.CutsceneHud.FadeAmount, Is.Zero);
                scope.Advance(.25f);
                Assert.That(elisa.sprite.name, Is.EqualTo("move-frame-01"), "The running pose replaces idle while she moves.");
                Sprite movingPose = elisa.sprite;
                scope.Advance(.1f);
                Assert.That(elisa.sprite, Is.SameAs(movingPose), "The pose stays fixed during travel.");
                // A little past the one-second hold, so float steps cannot leave it a hair short.
                for (int frame = 0; frame < 24; frame++) scope.Advance(.05f);
                Assert.That(dialogue.CurrentLine.Text, Is.EqualTo("둘째"));
                Assert.That(elisa.transform.localPosition.x, Is.EqualTo(0f).Within(1e-4f), "She moved to 0 while the camera reset.");
                Assert.That(elisa.sprite.name, Does.StartWith("idle-frame-"), "The first stopped frame returns to idle.");
                Assert.That(arena.ArenaCamera.orthographicSize, Is.EqualTo(6f).Within(1e-4f));

                Assert.That(controller.AdvanceCutscene(), Is.True);
                Assert.That(controller.CutsceneHud.ImageSprite, Is.Not.Null);
                Assert.That(controller.CutsceneHud.ImageAmount, Is.EqualTo(1f));
                Assert.That(arena.EnemyAppearance, Is.EqualTo(EnemyAppearance.Student), "The knight takes the dummy's place.");
                Assert.That(other.flipX, Is.True, "The knight is drawn facing left; facing right flips him.");
                Assert.That(dialogue.CurrentLine.Text, Is.EqualTo("셋째"));
                for (int frame = 0; frame < 9; frame++) scope.Advance(.05f);
                Assert.That(other.sprite.name, Does.StartWith("enemy-slash"), "The stroke plays while the line is up.");

                Assert.That(controller.AdvanceCutscene(), Is.True);
                Assert.That(controller.IsPlayingCutscene, Is.False);
                Assert.That(controller.IsInLobby, Is.True, "A cutscene started from the lobby returns to it.");
                Assert.That(controller.LobbyHud.Root.activeSelf, Is.True);
                Assert.That(controller.CutsceneHud.IsVisible, Is.False);
                Assert.That(dialogue.IsVisible || dialogue.IsCinematic, Is.False);
                Assert.That(Named(dialogue.Root, "Dialogue Close").GetComponentInChildren<Text>().text, Does.Contain("닫기"));
                scope.AssertArenaRestored();
            }
        }

        [UnityTest]
        public IEnumerator EffectCommands_DrainTheColourShakeTheCameraPlaySoundsAndPowerTheFigures_ThenCleanUp()
        {
            yield return null;
            using (var scope = new CutsceneScope())
            {
                DuelPrototypeController controller = scope.Controller;
                LegacyArenaView arena = controller.ArenaView;
                Assert.That(controller.StartCutscene(CutsceneScriptParser.Parse("Cutscene/effects", Effects)), Is.True);
                CutsceneDirector director = controller.Cutscene;
                Assert.That(arena.FlashbackAmount, Is.EqualTo(1f), "The flashback drains the colour at once.");
                Assert.That(Grade(arena).saturation.value, Is.EqualTo(-100f).Within(1e-3f));
                Assert.That(Label(controller.DialogueHud.Root, "Dialogue Body").color, Is.EqualTo(DialogueHud.MonologueColor),
                    "The box is not graded: a thought keeps its colour over the black-and-white scene.");
                Assert.That(director.Audio.LastSound.name, Is.EqualTo("flashback"));
                Assert.That(director.Audio.AmbienceClip.name, Is.EqualTo("aura-loop"));
                Assert.That(director.Audio.AmbienceVolume, Is.EqualTo(1f));
                Assert.That(director.SeniorRenderer, Is.Not.Null, "The script uses the senior knight, so he is made…");
                Assert.That(director.SeniorRenderer.gameObject.activeSelf, Is.False, "…but stays off stage until placed.");

                Assert.That(controller.AdvanceCutscene(), Is.True);
                Assert.That(controller.DialogueHud.CurrentLine.Text, Is.EqualTo("둘째"), "Effects written with & keep the line up.");
                Assert.That(arena.FlashbackAmount, Is.Zero);
                Assert.That(Grade(arena).saturation.value, Is.EqualTo(0f).Within(1e-3f));
                DuelPowerAura knight = arena.EnemyPowerAura;
                Assert.That(knight.IsCharging && knight.IsAuraOn, Is.True);
                Assert.That(knight.AuraAmount, Is.EqualTo(1f));
                var framing = new Vector3(0f, -1.5f, -10f);
                scope.Advance(.1f);
                Assert.That(director.IsShaking, Is.True);
                float offset = Vector3.Distance(arena.ArenaCamera.transform.localPosition, framing);
                Assert.That(offset, Is.GreaterThan(1e-4f), "The camera shakes…");
                Assert.That(offset, Is.LessThan(.8f), "…within its strength.");
                scope.Advance(.4f);
                Assert.That(knight.ChargeGlow, Is.GreaterThan(0f));
                Assert.That(knight.ActiveMoteCount, Is.GreaterThan(0));
                Assert.That(knight.Root.parent, Is.SameAs(arena.EnemyRenderer.transform), "The effect sits on the knight's figure.");
                for (int frame = 0; frame < 12; frame++) scope.Advance(.05f);
                Assert.That(director.IsShaking, Is.False);
                Assert.That(arena.ArenaCamera.transform.localPosition.x, Is.EqualTo(0f).Within(1e-4f), "The shake dies away to the framing.");
                Assert.That(arena.ArenaCamera.transform.localPosition.y, Is.EqualTo(-1.5f).Within(1e-4f));
                Assert.That(knight.IsCharging && knight.IsChargeHeld, Is.True, "A held charge outlasts its second…");
                Assert.That(knight.ChargeGlow, Is.EqualTo(1f), "…at full glow, waiting for the cut.");

                Assert.That(controller.AdvanceCutscene(), Is.True);
                Assert.That(knight.IsCharging, Is.False);
                Assert.That(knight.ChargeGlow, Is.Zero, "stop cuts the charge off at once…");
                Assert.That(knight.AuraAmount, Is.EqualTo(1f), "…and leaves the aura alone.");
                SpriteRenderer senior = director.SeniorRenderer;
                Assert.That(senior.gameObject.activeSelf, Is.True);
                Assert.That(senior.transform.localPosition.x, Is.EqualTo(-8f));
                Assert.That(senior.flipX, Is.False, "He is drawn facing right.");
                Assert.That(senior.sprite.name, Does.StartWith("senior-idle-upper-"));
                Assert.That(arena.PlayerRenderer.gameObject.activeSelf && arena.EnemyRenderer.gameObject.activeSelf, Is.True,
                    "He stands with both fighters.");
                scope.Advance(.3f);
                Assert.That(director.SeniorLowerRenderer.sprite.name, Does.StartWith("senior-move-lower-"), "His legs walk as he travels.");
                Assert.That(senior.transform.localPosition.x, Is.InRange(-8f, -6f));
                for (int frame = 0; frame < 16; frame++) scope.Advance(.05f);
                Assert.That(senior.transform.localPosition.x, Is.EqualTo(-6f).Within(1e-4f));
                Assert.That(director.SeniorLowerRenderer.sprite.name, Is.EqualTo("senior-idle-lower-1"));

                Assert.That(controller.AdvanceCutscene(), Is.True);
                scope.Advance(.5f);
                Assert.That(senior.sprite.name, Does.StartWith("senior-slash-1-upper-"), "His stroke plays while the line is up.");

                Assert.That(controller.AdvanceCutscene(), Is.True);
                Assert.That(controller.IsInLobby, Is.True);
                Assert.That(director.Audio.AmbienceClip == null && director.Audio.LastSound == null, Is.True,
                    "The scene's sounds stop with it.");
                Assert.That(knight.IsAuraOn, Is.False, "Back in the lobby the arena is reset, aura and all.");
                yield return null;
                Assert.That(senior == null, Is.True, "The senior knight leaves with the scene.");
                scope.AssertArenaRestored();
            }
        }

        [UnityTest]
        public IEnumerator AnAuraLitInAScene_StaysOnTheFighterAfterIt_UntilTheArenaResets()
        {
            yield return null;
            using (var scope = new CutsceneScope())
            {
                DuelPrototypeController controller = scope.Controller;
                LegacyArenaView arena = controller.ArenaView;
                // Directly on the arena, as a battle's event scene will play; the controller's own cleanup would reset it.
                var director = new CutsceneDirector(CutsceneScriptParser.Parse("Cutscene/test",
                        "@actor knight at 5 left\n@charge knight 3 &\n@aura knight on 0.5\n@flashback on 0\n하나"),
                    arena, controller.CutsceneHud, controller.DialogueHud, null);
                director.Start();
                director.Tick(.5f);
                Assert.That(arena.EnemyPowerAura.AuraAmount, Is.EqualTo(1f).Within(1e-4f));
                Assert.That(arena.EnemyPowerAura.IsCharging, Is.True);
                Assert.That(director.SeniorRenderer, Is.Null, "No senior knight is made for a scene without him.");
                director.Dispose();
                Assert.That(arena.EnemyPowerAura.IsAuraOn, Is.True, "The aura carries on into the fight…");
                Assert.That(arena.EnemyPowerAura.IsCharging, Is.False, "…but a charge belongs to the scene…");
                Assert.That(arena.FlashbackAmount, Is.Zero, "…and so does the flashback.");
                arena.Tick(.1f, .1f);
                Assert.That(arena.EnemyPowerAura.ActiveMoteCount, Is.GreaterThan(0), "The arena keeps it alive on its own clock.");
                arena.Reset();
                Assert.That(arena.EnemyPowerAura.IsAuraOn || arena.EnemyPowerAura.AuraAmount > 0f, Is.False);
                Assert.That(arena.EnemyPowerAura.ActiveMoteCount, Is.Zero);
            }
        }

        [Test]
        public void AMissingSound_IsAWarning_AndTheSceneGoesOn()
        {
            var warnings = new System.Collections.Generic.List<string>();
            Application.LogCallback capture = (message, stack, type) => { if (type == LogType.Warning) warnings.Add(message); };
            Application.logMessageReceived += capture;
            try
            {
                using (var scope = new CutsceneScope())
                {
                    DuelPrototypeController controller = scope.Controller;
                    Assert.That(controller.StartCutscene(CutsceneScriptParser.Parse("Cutscene/test",
                        "@sound nothing-here\n@ambience nor-here 1 &\n하나")), Is.True);
                    Assert.That(controller.DialogueHud.CurrentLine.Text, Is.EqualTo("하나"));
                    Assert.That(controller.Cutscene.Audio.AmbienceClip, Is.Null);
                    Assert.That(controller.SkipCutscene(), Is.True);
                }
            }
            finally { Application.logMessageReceived -= capture; }
            Assert.That(warnings.Any(message => Regex.IsMatch(message, "line 1: no sound at Resources/Sfx/nothing-here")), Is.True);
            Assert.That(warnings.Any(message => Regex.IsMatch(message, "line 2: no sound at Resources/Sfx/nor-here")), Is.True);
        }

        [Test]
        public void AFigureSteppingOnStage_StartsIdleFacingItsUsualWay_EvenWhereTheDummyStoodHurt()
        {
            using (var scope = new CutsceneScope())
            {
                DuelPrototypeController controller = scope.Controller;
                Assert.That(controller.StartCutscene(CutsceneScriptParser.Parse("Cutscene/test",
                    "@actor dummy at 2 right\n@actor dummy pose hurt\n@actor dummy hide\n@actor knight at 3\n하나")), Is.True);
                SpriteRenderer knight = controller.ArenaView.EnemyRenderer;
                Assert.That(controller.ArenaView.EnemyAppearance, Is.EqualTo(EnemyAppearance.Student));
                Assert.That(knight.sprite.name, Does.StartWith("enemy-idle-frame-"), "The dummy's hurt does not carry over.");
                Assert.That(knight.flipX, Is.False, "…nor its facing.");
                Assert.That(controller.SkipCutscene(), Is.True);
                Assert.That(controller.IsInLobby, Is.True);
            }
        }

        [Test]
        public void AResetBattle_CanStartAtTheClashWithoutInventingARetreat()
        {
            using (var scope = new CutsceneScope())
            {
                LegacyArenaView arena = scope.Controller.ArenaView;
                arena.Reset();
                Assert.Throws<ArgumentOutOfRangeException>(() => arena.SetOpeningPositions(2f, 5f),
                    "The actors cannot start inside sword contact distance.");
                Assert.Throws<ArgumentOutOfRangeException>(() => arena.SetOpeningPositions(float.NaN, 5f));
                arena.SetOpeningPositions(1f, 5f);
                Assert.That(arena.PlayerRenderer.transform.localPosition.x, Is.EqualTo(1f));
                Assert.That(arena.EnemyRenderer.transform.localPosition.x, Is.EqualTo(5f));
                Assert.That(arena.IsInRange, Is.True, "The next duel starts from the clash instead of showing a retreat.");
                Assert.That(arena.PlayerKnockbackTarget, Is.EqualTo(arena.PlayerRenderer.transform.localPosition));
                Assert.That(arena.EnemyKnockbackTarget, Is.EqualTo(arena.EnemyRenderer.transform.localPosition));
                Assert.Throws<InvalidOperationException>(() => arena.SetOpeningPositions(-5f, 5f),
                    "Opening placement is one handoff, not an in-battle teleport.");
            }
        }

        [UnityTest]
        public IEnumerator KeysOfTheOpeningFrameAreIgnored_AndTheLastEnterStaysInTheCutscene()
        {
            yield return null;
            using (var scope = new CutsceneScope())
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                DuelPrototypeController controller = scope.Controller;
                controller.LobbyHud.ShowTab(LobbyTab.Stages);
                Press(keyboard.enterKey);
                yield return null;
                Assert.That(controller.StartCutscene(CutsceneScriptParser.Parse("Cutscene/test", "하나\n둘")), Is.True);
                scope.Advance(0f, keyboard);
                Assert.That(controller.DialogueHud.CurrentLine.Text, Is.EqualTo("하나"), "The key that opened it is ignored.");
                Release(keyboard.enterKey);
                yield return null;
                Press(keyboard.spaceKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Release(keyboard.spaceKey);
                Assert.That(controller.DialogueHud.CurrentLine.Text, Is.EqualTo("둘"));
                yield return null;
                Press(keyboard.enterKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Release(keyboard.enterKey);
                Assert.That(controller.IsPlayingCutscene, Is.False);
                Assert.That(controller.IsInLobby, Is.True, "The final Enter does not also start a stage.");
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(controller.IsInLobby, Is.True);
            }
        }

        [Test]
        public void CleanupScreens_StopTheCutsceneWithoutContinuingIt()
        {
            using (var scope = new CutsceneScope())
            {
                DuelPrototypeController controller = scope.Controller;
                Assert.That(controller.StartCutscene(CutsceneScriptParser.Parse("Cutscene/test", Staged)), Is.True);
                controller.ShowTitle();
                Assert.That(controller.IsPlayingCutscene, Is.False);
                Assert.That(controller.IsInTitle, Is.True, "The title, not the lobby the cutscene would return to.");
                Assert.That(controller.CutsceneHud.IsVisible || controller.DialogueHud.IsCinematic, Is.False);
                Assert.That(controller.StopCutscene(), Is.False);
                scope.AssertArenaRestored();

                controller.RestartJourney();
                Assert.That(controller.StartCutscene(CutsceneScriptParser.Parse("Cutscene/test", Staged)), Is.True);
                controller.StartNewGame();
                Assert.That(controller.IsPlayingCutscene, Is.False);
                Assert.That(controller.IsInBriefing, Is.True);
                Assert.That(controller.SkipCutscene() || controller.AdvanceCutscene(), Is.False);
            }
        }

        [Test]
        public void DialogueHud_CinematicModeOnlyChangesItsDimAndWording()
        {
            var parent = new GameObject("Cinematic Dialogue Test");
            try
            {
                using (var hud = new DialogueHud(parent.transform, new LegacyDuelArt(), null, null))
                {
                    int nodes = hud.Root.GetComponentsInChildren<Transform>(true).Length;
                    Image backdrop = Named(hud.Root, "Dialogue Backdrop").GetComponent<Image>();
                    float alpha = backdrop.color.a;
                    hud.SetCinematic(true);
                    Assert.That(hud.IsCinematic, Is.True);
                    Assert.That(backdrop.color.a, Is.Zero);
                    Assert.That(backdrop.raycastTarget, Is.True, "A click anywhere still advances.");
                    Assert.That(backdrop.canvasRenderer.cullTransparentMesh, Is.False,
                        "A culled transparent graphic would not be hit by the raycaster.");
                    Assert.That(Label(hud.Root, "Dialogue Input Hint").text, Does.Contain("건너뛰기"));
                    hud.SetCinematic(false);
                    Assert.That(backdrop.color.a, Is.EqualTo(alpha));
                    Assert.That(Label(hud.Root, "Dialogue Input Hint").text, Does.Contain("닫기"));
                    Assert.That(hud.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(nodes));
                }
            }
            finally { Object.Destroy(parent); }
        }

        [UnityTest]
        public IEnumerator Tremble_ShiversEveryFigureAroundItsPlace_WithinItsStrength_AndEndsExactlyThere()
        {
            yield return null;
            using (var scope = new CutsceneScope())
            {
                DuelPrototypeController controller = scope.Controller;
                LegacyArenaView arena = controller.ArenaView;
                Assert.That(controller.StartCutscene(CutsceneScriptParser.Parse("Cutscene/test", string.Join("\n",
                    "@actor elisa at -3 right",
                    "@actor dummy at 3",
                    "@actor senior at -8 right",
                    "@actor elisa tremble 0.5 0.1 &",
                    "@actor senior tremble 0.5 &",
                    "@actor dummy tremble 0.5",
                    "하나"))), Is.True);
                CutsceneDirector director = controller.Cutscene;
                Transform elisa = arena.PlayerRenderer.transform, dummy = arena.EnemyRenderer.transform;
                Transform senior = director.SeniorRenderer.transform;
                Assert.That(director.IsTrembling(CutsceneActor.Elisa) && director.IsTrembling(CutsceneActor.Dummy) &&
                    director.IsTrembling(CutsceneActor.Senior), Is.True, "Every figure can tremble, the dummy and the senior knight too.");
                Assert.That(director.IsTrembling(CutsceneActor.Knight), Is.False, "The knight is not on stage.");
                Assert.That(elisa.localPosition.x, Is.EqualTo(-3f), "A tremble eases in from the figure's place.");

                float elisaLeft = 0f, elisaRight = 0f, dummyMost = 0f, seniorMost = 0f;
                for (int frame = 0; frame < 9; frame++)
                {
                    scope.Advance(.05f);
                    float offset = elisa.localPosition.x + 3f;
                    elisaLeft = Mathf.Min(elisaLeft, offset);
                    elisaRight = Mathf.Max(elisaRight, offset);
                    dummyMost = Mathf.Max(dummyMost, Mathf.Abs(dummy.localPosition.x - 3f));
                    seniorMost = Mathf.Max(seniorMost, Mathf.Abs(senior.localPosition.x + 8f));
                    Assert.That(elisa.localPosition.y, Is.EqualTo(arena.ActorGroundY).Within(1e-5f), "Only sideways.");
                }
                Assert.That(elisaLeft, Is.LessThan(-.03f), "She shivers to one side…");
                Assert.That(elisaRight, Is.GreaterThan(.03f), "…and to the other…");
                Assert.That(Mathf.Max(-elisaLeft, elisaRight), Is.LessThanOrEqualTo(.1f + 1e-4f), "…within the strength written.");
                Assert.That(dummyMost, Is.InRange(.02f, CutsceneStep.DefaultTrembleStrength + 1e-4f), "Unwritten, a tremble is slight.");
                Assert.That(seniorMost, Is.GreaterThan(.02f));
                for (int frame = 0; frame < 20 && controller.DialogueHud.CurrentLine == null; frame++) scope.Advance(.05f);
                Assert.That(controller.DialogueHud.CurrentLine?.Text, Is.EqualTo("하나"));
                // A moment on the line, so float steps cannot leave a tremble a hair short of its end.
                scope.Advance(.05f);
                Assert.That(director.IsTrembling(CutsceneActor.Elisa) || director.IsTrembling(CutsceneActor.Dummy) ||
                    director.IsTrembling(CutsceneActor.Senior), Is.False);
                Assert.That(elisa.localPosition.x, Is.EqualTo(-3f), "Each stands exactly where it trembled.");
                Assert.That(dummy.localPosition.x, Is.EqualTo(3f));
                Assert.That(senior.localPosition.x, Is.EqualTo(-8f));
                Assert.That(controller.SkipCutscene(), Is.True);

                // Cut short in the middle of a tremble, a scene that keeps its last picture keeps her at her place.
                var cut = new CutsceneDirector(CutsceneScriptParser.Parse("Cutscene/test",
                    "@actor elisa at -2 right\n@actor elisa tremble 2 0.3 &\n하나"), arena, controller.CutsceneHud, controller.DialogueHud, null);
                cut.Start();
                cut.Tick(.13f);
                Assert.That(Mathf.Abs(elisa.localPosition.x + 2f), Is.GreaterThan(1e-3f));
                cut.Dispose();
                Assert.That(elisa.localPosition.x, Is.EqualTo(-2f));
                arena.Reset();
            }
        }

        [UnityTest]
        public IEnumerator RecallAlbum_KeepsASmallSetForTheSession_DestroysWhatItDrops_AndForgetsAtTheTitleAndANewGame()
        {
            yield return null;
            using (var scope = new CutsceneScope())
            {
                DuelPrototypeController controller = scope.Controller;
                TutorialRecallAlbum album = controller.RecallAlbum;
                Assert.That(album.Screens, Is.Empty, "A new game has nothing to recall yet.");
                album.Random = new System.Random(3);
                var offered = new List<Texture2D>();
                foreach (TutorialRecallBeat beat in TutorialRecall.Beats)
                {
                    Texture2D screen = FakeScreen(beat.Key);
                    offered.Add(screen);
                    album.Offer(beat.Key, screen);
                }
                Assert.That(album.Screens.Count, Is.EqualTo(TutorialRecall.MaximumScreens), "A small set, however many lessons were shown.");
                Assert.That(album.OfferedCount, Is.EqualTo(TutorialRecall.Beats.Count));
                Assert.That(album.Screens, Is.Unique);
                Assert.That(album.Screens.All(offered.Contains), Is.True);
                Assert.That(album.Screens.Any(screen => screen.name.StartsWith("2:")), Is.True,
                    "Later lessons get their chance too, not only the first ones.");
                Texture2D again = FakeScreen("again");
                Assert.That(album.Offer(TutorialRecall.Beats[0].Key, again), Is.False, "A beat seen again (a retry) keeps what it had.");
                Assert.That(album.HasOffered(TutorialRecall.Beats[0].Key), Is.True);

                album.Random = new System.Random(9);
                Assert.That(album.TryPick(out Texture2D picked, out TutorialRecallBeat none), Is.True);
                album.Random = new System.Random(9);
                Assert.That(album.TryPick(out Texture2D pickedAgain, out _), Is.True);
                Assert.That(picked, Is.SameAs(pickedAgain), "A seed fixes which one is recalled…");
                Assert.That(album.Screens, Does.Contain(picked), "…among the kept screens…");
                Assert.That(none, Is.Null, "…and with screens there is no card to redraw.");

                yield return null;
                Assert.That(again == null, Is.True, "A screen the album does not keep is destroyed…");
                Assert.That(offered.Count(screen => screen == null), Is.EqualTo(offered.Count - TutorialRecall.MaximumScreens),
                    "…as is every one it dropped or replaced.");

                // The same seed keeps the same set.
                using (var first = new TutorialRecallAlbum(null, new System.Random(21)))
                using (var second = new TutorialRecallAlbum(null, new System.Random(21)))
                {
                    Assert.That(first.CapturesScreens, Is.False, "Without a host nothing is captured…");
                    Assert.That(first.RequestCapture("1:1", () => true), Is.False);
                    Assert.That(first.IsCapturing, Is.False);
                    foreach (TutorialRecallBeat beat in TutorialRecall.Beats)
                    {
                        first.Offer(beat.Key, FakeScreen(beat.Key));
                        second.Offer(beat.Key, FakeScreen(beat.Key));
                    }
                    Assert.That(first.Screens.Select(screen => screen.name), Is.EqualTo(second.Screens.Select(screen => screen.name)));
                }

                Texture2D[] kept = album.Screens.ToArray();
                controller.ShowTitle();
                Assert.That(album.Screens.Count + album.OfferedCount, Is.Zero, "The title forgets the session…");
                yield return null;
                Assert.That(kept.All(screen => screen == null), Is.True, "…and destroys its screens.");

                controller.StartNewGame();
                album.Offer("1:1", FakeScreen("1:1"));
                Texture2D kept2 = album.Screens[0];
                controller.StartNewGame();
                Assert.That(album.Screens, Is.Empty, "A new game forgets them too.");
                Assert.That(album.HasOffered("1:1"), Is.False, "Its lessons are captured afresh.");
                yield return null;
                Assert.That(kept2 == null, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator RecallAlbum_ABeatWhoseCaptureFails_WarnsOnce_AndIsNotTriedAgainThatSession()
        {
            yield return null;
            var warnings = new List<string>();
            Application.LogCallback capture = (message, stack, type) => { if (type == LogType.Warning) warnings.Add(message); };
            Application.logMessageReceived += capture;
            try
            {
                using (var scope = new CutsceneScope())
                {
                    DuelPrototypeController controller = scope.Controller;
                    TutorialRecallAlbum album = controller.RecallAlbum;
                    int grabs = 0;
                    Assert.That(album.CaptureNow("1:1", () => { grabs++; throw new InvalidOperationException("no readback"); }), Is.False);
                    Assert.That(album.CaptureNow("1:2", () => { grabs++; return null; }), Is.False, "Nothing back fails too.");
                    Texture2D late = FakeScreen("late");
                    Assert.That(album.CaptureNow("1:1", () => { grabs++; return late; }), Is.False, "A failed beat is not tried again…");
                    Assert.That(grabs, Is.EqualTo(2));
                    if (album.CapturesScreens)
                    {
                        Assert.That(album.RequestCapture("1:1", () => true), Is.False, "…nor asked for again after every refresh.");
                        Assert.That(album.IsCapturing, Is.False);
                    }
                    Assert.That(album.HasTried("1:1") && album.HasTried("1:2"), Is.True);
                    Assert.That(album.HasOffered("1:1") || album.HasOffered("1:2"), Is.False);
                    Assert.That(album.OfferedCount, Is.Zero, "A failure takes no place among the offered.");
                    Assert.That(album.Screens, Is.Empty);
                    Assert.That(warnings.Count(message => message.Contains("coach beat 1:1 could not be captured (no readback)")), Is.EqualTo(1));
                    Assert.That(warnings.Count(message => message.Contains("coach beat 1:2 could not be captured")), Is.EqualTo(1));

                    // A beat that does come back is made into a memory; the shot itself is destroyed.
                    Texture2D shot = FakeScreen("shot");
                    Assert.That(album.CaptureNow("1:3", () => shot), Is.True);
                    Assert.That(album.OfferedCount, Is.EqualTo(1));
                    Assert.That(album.Screens.Single().name, Is.EqualTo("Tutorial Recall 1:3"));
                    Assert.That(album.CaptureNow("1:3", () => FakeScreen("again")), Is.False, "Offered once, captured once.");
                    yield return null;
                    Assert.That(shot == null, Is.True);
                    Assert.That(album.Screens.Single() != null, Is.True);

                    controller.StartNewGame();
                    Assert.That(album.HasTried("1:1") || album.HasTried("1:3"), Is.False, "A new game tries every beat afresh.");
                    Object.DestroyImmediate(late);
                }
            }
            finally { Application.logMessageReceived -= capture; }
        }

        [UnityTest]
        public IEnumerator RecallAlbum_WaitsWhileTheScreenIsNotReady_AsLongAsItsBeatStaysUp()
        {
            yield return null;
            using (var scope = new CutsceneScope())
            {
                TutorialRecallAlbum album = scope.Controller.RecallAlbum;
                bool ready = false, shown = true;
                if (!album.CapturesScreens)
                {
                    // Batch mode or no graphics device: nothing waits, ready or not.
                    Assert.That(album.RequestCapture("1:1", () => shown, () => ready), Is.False);
                    Assert.That(album.IsCapturing, Is.False);
                    yield break;
                }
                // A speech bubble is up when the card has settled: the capture waits for it…
                Assert.That(album.RequestCapture("1:1", () => shown, () => ready), Is.True);
                yield return new WaitForSecondsRealtime(TutorialRecallAlbum.CaptureDelay + .2f);
                Assert.That(album.IsCapturing && album.PendingKey == "1:1", Is.True, "…however long it stays…");
                Assert.That(album.HasTried("1:1"), Is.False);
                ready = true;
                for (int frame = 0; frame < 10 && album.IsCapturing; frame++) yield return null;
                Assert.That(album.IsCapturing, Is.False);
                Assert.That(album.HasTried("1:1"), Is.True, "…and captures the beat once it has gone.");

                // A beat that leaves while its capture waits is not captured, nor counted as tried.
                ready = false;
                Assert.That(album.RequestCapture("1:2", () => shown, () => ready), Is.True);
                yield return new WaitForSecondsRealtime(TutorialRecallAlbum.CaptureDelay + .1f);
                Assert.That(album.PendingKey, Is.EqualTo("1:2"));
                shown = false;
                for (int frame = 0; frame < 10 && album.IsCapturing; frame++) yield return null;
                Assert.That(album.IsCapturing || album.HasTried("1:2"), Is.False);
                album.Clear();
            }
        }

        [Test]
        public void MakeMemory_ShrinksTheShotByAWholeFactor_IntoATextureTheAlbumOwns()
        {
            foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(800, 600), new Vector2Int(2560, 1440) })
            {
                var shot = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
                Texture2D memory = null;
                try
                {
                    memory = TutorialRecallAlbum.MakeMemory(shot, "Tutorial Recall test");
                    int factor = Mathf.CeilToInt(size.x / (float)TutorialRecallAlbum.MaximumWidth);
                    Assert.That(memory.width, Is.EqualTo(size.x / factor).And.LessThanOrEqualTo(TutorialRecallAlbum.MaximumWidth));
                    Assert.That(memory.height, Is.EqualTo(size.y / factor));
                    Assert.That(memory.isReadable, Is.False, "Its pixels live on the GPU only.");
                    Assert.That(memory.hideFlags, Is.EqualTo(HideFlags.HideAndDontSave));
                    Assert.That(shot.isReadable, Is.True, "The shot is left as it was, for its owner to destroy.");
                }
                finally
                {
                    Object.DestroyImmediate(shot);
                    if (memory != null) Object.DestroyImmediate(memory);
                }
            }
        }

        [UnityTest]
        public IEnumerator Recall_FlashesACapturedScreenIn_HoldsIt_AndFadesItAway()
        {
            yield return null;
            using (var scope = new CutsceneScope())
            {
                DuelPrototypeController controller = scope.Controller;
                CutsceneHud hud = controller.CutsceneHud;
                Texture2D screen = FakeScreen("1:3");
                Assert.That(controller.RecallAlbum.Offer("1:3", screen), Is.True);
                Assert.That(controller.StartCutscene(CutsceneScriptParser.Parse("Cutscene/test", "@recall\n(머릿속에서…)")), Is.True);
                Assert.That(controller.Cutscene.IsRecalling && hud.IsRecalling, Is.True);
                Assert.That(hud.RecallScreen, Is.SameAs(screen), "The one screen kept comes back…");
                Assert.That(hud.RecallBeat, Is.Null, "…not a redrawn card.");
                Assert.That(hud.RecallCard.Frame.gameObject.activeSelf, Is.False);
                Assert.That(hud.RecallFlash, Is.EqualTo(1f), "It opens on a white flash…");
                Assert.That(hud.RecallAmount, Is.Zero);
                Assert.That(controller.DialogueHud.IsVisible, Is.False);

                scope.Advance(.1f);
                Assert.That(hud.RecallAmount, Is.EqualTo(1f), "…the memory comes up under it at once…");
                Assert.That(hud.RecallFlash, Is.InRange(.01f, .99f), "…as the flash dies away…");
                scope.Advance(.2f);
                Assert.That(hud.RecallFlash, Is.Zero);
                Assert.That(hud.RecallAmount, Is.EqualTo(1f), "…and holds…");
                scope.Advance(.9f);
                Assert.That(hud.RecallAmount, Is.InRange(.01f, .99f), "…until it fades away at the end.");
                Assert.That(controller.DialogueHud.IsVisible, Is.False, "The scene holds for the whole recall.");
                scope.Advance(.35f);
                Assert.That(hud.IsRecalling || controller.Cutscene.IsRecalling, Is.False);
                Assert.That(hud.RecallScreen, Is.Null, "The HUD lets go of the screen…");
                Assert.That(controller.DialogueHud.CurrentLine?.Text, Is.EqualTo("(머릿속에서…)"));

                Assert.That(controller.AdvanceCutscene(), Is.True);
                Assert.That(controller.IsInLobby, Is.True);
                yield return null;
                Assert.That(screen != null, Is.True, "…which stays the album's, for the next recall.");
                Assert.That(controller.RecallAlbum.Screens, Does.Contain(screen));
            }
        }

        [Test]
        public void Recall_WithoutACapturedScreen_RedrawsARandomCoachedBeat_LargeOnTheCoachsCard()
        {
            using (var scope = new CutsceneScope())
            {
                DuelPrototypeController controller = scope.Controller;
                CutsceneHud hud = controller.CutsceneHud;
                Assert.That(controller.RecallAlbum.Screens, Is.Empty, "As after 이어하기: nothing was captured this session.");
                controller.RecallAlbum.Random = new System.Random(42);
                TutorialRecallBeat expected = TutorialRecall.Beats[TutorialRecall.Pick(new System.Random(42), TutorialRecall.Beats.Count)];
                Assert.That(controller.StartCutscene(CutsceneScriptParser.Parse("Cutscene/test", "@recall 2 &\n하나")), Is.True);
                Assert.That(hud.IsRecalling, Is.True);
                Assert.That(hud.RecallScreen, Is.Null);
                Assert.That(hud.RecallBeat, Is.SameAs(expected), "The seeded album chooses the beat.");

                MissionCoachCard card = hud.RecallCard;
                Assert.That(card.Frame.gameObject.activeSelf, Is.True);
                Assert.That(card.Title.text, Is.EqualTo(expected.Beat.Title));
                Assert.That(card.Description.text, Is.EqualTo(expected.Beat.Description));
                Assert.That(card.InputHint.text, Is.EqualTo(expected.Beat.InputHint));
                Assert.That(card.Counter.text, Is.EqualTo(MissionCoachCard.CounterText(
                    MissionCoachCard.MissionLabel(expected.MissionNumber), expected.StepNumber, expected.StepCount)));
                Assert.That(card.Continue.gameObject.activeSelf, Is.EqualTo(expected.Beat.Kind == MissionGuideStepKind.Info),
                    "The card has the buttons that beat had.");
                Assert.That(card.Inspect.gameObject.activeSelf, Is.EqualTo(expected.Beat.Kind == MissionGuideStepKind.Inspect));
                Assert.That(card.Frame.localScale.x, Is.EqualTo(CutsceneHud.RecallCardScale), "Larger than in battle…");
                Assert.That(card.Frame.anchorMin, Is.EqualTo(new Vector2(.5f, .5f)), "…and centred.");
                Assert.That(card.Frame.anchorMax, Is.EqualTo(new Vector2(.5f, .5f)));
                Assert.That(controller.DialogueHud.CurrentLine?.Text, Is.EqualTo("하나"), "Written with &, the line comes up over it.");

                // The coach's own card, for the look: the same frame, panel and labels.
                Transform coachBorder = Named(controller.CoachHud.Root, "Coach Border");
                Assert.That(card.Frame.sizeDelta, Is.EqualTo(((RectTransform)coachBorder).sizeDelta));
                Assert.That(card.Frame.GetComponent<Image>().color, Is.EqualTo(coachBorder.GetComponent<Image>().color));
                Assert.That(Named(card.Frame.gameObject, "Coach Card").GetComponent<Image>().color,
                    Is.EqualTo(Named(controller.CoachHud.Root, "Coach Card").GetComponent<Image>().color));
                Assert.That(card.Title.font, Is.SameAs(Label(controller.CoachHud.Root, "Coach Title").font));
                Assert.That(card.Title.fontSize, Is.EqualTo(Label(controller.CoachHud.Root, "Coach Title").fontSize));

                // A picture, not a coach: nothing on the cutscene's screen takes clicks.
                Assert.That(hud.Root.GetComponentsInChildren<Button>(true), Is.Empty);
                Assert.That(hud.Root.GetComponentsInChildren<GraphicRaycaster>(true), Is.Empty);
                foreach (Graphic graphic in hud.Root.GetComponentsInChildren<Graphic>(true))
                    Assert.That(graphic.raycastTarget, Is.False, graphic.name);

                controller.RecallAlbum.Random = new System.Random(42);
                Assert.That(controller.RecallAlbum.TryPick(out Texture2D noScreen, out TutorialRecallBeat again), Is.True);
                Assert.That(noScreen == null && again == expected, Is.True, "The same seed, the same beat.");
                Assert.That(controller.SkipCutscene(), Is.True);
                Assert.That(hud.IsRecalling || hud.IsVisible, Is.False, "Skipping takes the recall away with the scene.");
            }
        }

        [Test]
        public void Recall_WithNothingToRecall_IsAWarning_AndTheSceneHoldsItsTime()
        {
            var warnings = new List<string>();
            Application.LogCallback capture = (message, stack, type) => { if (type == LogType.Warning) warnings.Add(message); };
            Application.logMessageReceived += capture;
            try
            {
                using (var scope = new CutsceneScope())
                {
                    DuelPrototypeController controller = scope.Controller;
                    LegacyArenaView arena = controller.ArenaView;
                    // No album at all: nothing captured and no beat to redraw.
                    var director = new CutsceneDirector(CutsceneScriptParser.Parse("Cutscene/test", "@recall 0.5\n하나"),
                        arena, controller.CutsceneHud, controller.DialogueHud, null);
                    director.Start();
                    Assert.That(director.IsRecalling || controller.CutsceneHud.IsRecalling, Is.False);
                    Assert.That(director.Playback.HoldRemaining, Is.EqualTo(.5f));
                    director.Tick(.5f);
                    Assert.That(controller.DialogueHud.CurrentLine?.Text, Is.EqualTo("하나"));
                    director.Dispose();
                    arena.Reset();
                }
            }
            finally { Application.logMessageReceived -= capture; }
            Assert.That(warnings.Any(message => Regex.IsMatch(message, "line 1: no tutorial screen or coach beat to recall")), Is.True);
        }

        [Test]
        public void TheFirstMissionsCoachedBeats_AreAskedFor_AndMissionThreesNever()
        {
            using (var scope = new CutsceneScope())
            {
                DuelPrototypeController controller = scope.Controller;
                TutorialRecallAlbum album = controller.RecallAlbum;
                controller.StartNewGame();
                for (int number = 1; number <= TutorialRecall.LastMission; number++)
                    Assert.That(controller.Prologue.TryComplete(number, DuelMatchOutcome.PlayerVictory), Is.True);
                Assert.That(controller.StartMission(), Is.True);
                if (controller.IsPlayingScene) Assert.That(controller.SkipScene(), Is.True);
                Assert.That(controller.ActiveMission.Number, Is.EqualTo(TutorialRecall.LastMission + 1));
                Assert.That(controller.CoachHud.IsVisible, Is.True);
                Assert.That(album.IsCapturing, Is.False, "Mission 3 is where the lessons are recalled, not taught.");

                controller.StartNewGame();
                Assert.That(controller.StartMission(), Is.True);
                if (controller.IsPlayingScene) Assert.That(controller.SkipScene(), Is.True);
                Assert.That(controller.ActiveMission.Number, Is.EqualTo(1));
                Assert.That(controller.CoachHud.IsVisible, Is.True);
                if (!album.CapturesScreens)
                {
                    // Batch mode or no graphics device: nothing waits, and a recall will redraw a card instead.
                    Assert.That(album.IsCapturing, Is.False);
                    return;
                }
                Assert.That(album.PendingKey, Is.EqualTo(TutorialRecall.Key(1, 1)), "The first beat's screen waits for its card to settle.");
                Assert.That(controller.AdvanceGuide(), Is.True);
                Assert.That(album.PendingKey, Is.EqualTo(TutorialRecall.Key(1, 2)), "The next beat takes its place.");
                controller.StartNewGame();
                Assert.That(album.IsCapturing, Is.False, "A new game stops the capture waiting.");
            }
        }

        private static Texture2D FakeScreen(string name)
            => new Texture2D(8, 4, TextureFormat.RGB24, false) { name = name, hideFlags = HideFlags.HideAndDontSave };

        private static Transform Named(GameObject root, string name)
        {
            Transform found = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(node => node.name == name);
            Assert.That(found, Is.Not.Null, "Missing UI node: " + name);
            return found;
        }

        private static Text Label(GameObject root, string name) => Named(root, name).GetComponent<Text>();

        private static ColorAdjustments Grade(LegacyArenaView arena)
        {
            Assert.That(arena.ArenaProfile.TryGet(out ColorAdjustments adjustments), Is.True);
            return adjustments;
        }

        private sealed class CutsceneScope : IDisposable
        {
            private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
            private readonly bool originalEnabled;
            private readonly GameSaveStore originalStore;
            private readonly string directory;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }

            public CutsceneScope(bool saveToTemporaryStore = false)
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                Controller.enabled = false;
                originalStore = Controller.SaveStore;
                if (saveToTemporaryStore)
                {
                    directory = Path.Combine(Application.temporaryCachePath, "CutsceneTests-" + Guid.NewGuid().ToString("N"));
                    Controller.SaveStore = new GameSaveStore(Path.Combine(directory, GameSaveStore.FileName));
                }
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                    typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance));
                Controller.StartNewGame();
                Controller.RestartJourney();
            }

            public void Advance(float delta, Keyboard keyboard = null) => advance(delta, keyboard);

            public GameSave LoadSave()
            {
                Assert.That(Controller.SaveStore.TryLoad(out GameSave save, out string error), Is.True, error);
                return save;
            }

            /// <summary>The duel gets its arena back: both figures up, unflipped, the camera at its duel framing.</summary>
            public void AssertArenaRestored()
            {
                LegacyArenaView arena = Controller.ArenaView;
                foreach (SpriteRenderer figure in new[] { arena.PlayerRenderer, arena.EnemyRenderer })
                {
                    Assert.That(figure.gameObject.activeSelf, Is.True, figure.name);
                    Assert.That(figure.flipX, Is.False, figure.name);
                }
                Assert.That(arena.ArenaCamera.orthographicSize, Is.EqualTo(6f));
                Assert.That(arena.EnemyAppearance, Is.EqualTo(EnemyAppearance.Student));
                Assert.That(arena.FlashbackAmount, Is.Zero, "In colour again.");
                foreach (DuelPowerAura power in new[] { arena.PlayerPowerAura, arena.EnemyPowerAura })
                    Assert.That(power.IsAuraOn || power.IsCharging || power.ActiveMoteCount > 0, Is.False, "No power left on.");
            }

            public void Dispose()
            {
                Controller.ShowTitle();
                Controller.SaveStore = originalStore;
                Controller.StartNewGame();
                Controller.RestartJourney();
                Controller.enabled = originalEnabled;
                if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
