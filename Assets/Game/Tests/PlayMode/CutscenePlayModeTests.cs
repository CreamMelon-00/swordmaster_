using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.Cutscene;
using TurnLimbo.Runtime.Prologue;
using TurnLimbo.Runtime.Save;
using UnityEngine;
using UnityEngine.InputSystem;
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
@actor elise at -3 left
@actor elise pose hurt
@actor dummy at 2
첫째
@fade in 1
@actor elise pose idle
@actor elise move 0 1 &
@camera reset 1
둘째
@image ForestArena/forest-far 0
@actor dummy hide
@actor knight at 3 right
@actor knight attack slash &
셋째
@image off 0";

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
                SpriteRenderer elise = arena.PlayerRenderer, other = arena.EnemyRenderer;
                Assert.That(elise.gameObject.activeSelf, Is.True);
                Assert.That(elise.transform.localPosition.x, Is.EqualTo(-3f));
                Assert.That(elise.flipX, Is.True, "Elisa is drawn facing right; facing left flips her.");
                Assert.That(elise.sprite.name, Does.Contain("hurt"));
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
                Assert.That(elise.sprite.name, Is.EqualTo("move-frame-01"), "The running pose replaces idle while she moves.");
                Sprite movingPose = elise.sprite;
                scope.Advance(.1f);
                Assert.That(elise.sprite, Is.SameAs(movingPose), "The pose stays fixed during travel.");
                // A little past the one-second hold, so float steps cannot leave it a hair short.
                for (int frame = 0; frame < 24; frame++) scope.Advance(.05f);
                Assert.That(dialogue.CurrentLine.Text, Is.EqualTo("둘째"));
                Assert.That(elise.transform.localPosition.x, Is.EqualTo(0f).Within(1e-4f), "She moved to 0 while the camera reset.");
                Assert.That(elise.sprite.name, Does.StartWith("idle-frame-"), "The first stopped frame returns to idle.");
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

        private static Transform Named(GameObject root, string name)
        {
            Transform found = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(node => node.name == name);
            Assert.That(found, Is.Not.Null, "Missing UI node: " + name);
            return found;
        }

        private static Text Label(GameObject root, string name) => Named(root, name).GetComponent<Text>();

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
