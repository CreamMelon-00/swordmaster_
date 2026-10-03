using System.Collections;
using NUnit.Framework;
using TurnLimbo.Runtime.Dialogue;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class LobbyStoryLauncherPlayModeTests
    {
        [UnityTest]
        public IEnumerator Launcher_IsSingleAndVisibleOnlyOnLobbyHome()
        {
            yield return null;
            DuelPrototypeController controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            LobbyStoryLauncher launcher = Object.FindAnyObjectByType<LobbyStoryLauncher>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(launcher, Is.Not.Null);
            Assert.That(Object.FindObjectsByType<LobbyStoryLauncher>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length, Is.EqualTo(1));

            try
            {
                controller.CloseDialogue();
                controller.ReturnToLobby();
                controller.LobbyHud.ShowTab(LobbyTab.Home);
                launcher.RefreshAvailability();
                yield return null;

                Assert.That(launcher.IsVisible, Is.True);
                Assert.That(launcher.StoryButton, Is.Not.Null);
                Assert.That(launcher.StoryButton.interactable, Is.EqualTo(launcher.IsStoryReady));
                Assert.That(launcher.StoryButton.navigation.mode, Is.EqualTo(Navigation.Mode.None));
                Assert.That(launcher.Root.transform.parent, Is.SameAs(controller.LobbyHud.Root.transform));

                LobbyTab[] hiddenTabs = { LobbyTab.Stages, LobbyTab.Loadout, LobbyTab.Curriculum };
                for (int index = 0; index < hiddenTabs.Length; index++)
                {
                    controller.LobbyHud.ShowTab(hiddenTabs[index]);
                    yield return null;
                    Assert.That(launcher.IsVisible, Is.False,
                        $"Story entry must be hidden on {hiddenTabs[index]}.");
                }

                controller.LobbyHud.ShowTab(LobbyTab.Home);
                yield return null;
                Assert.That(launcher.IsVisible, Is.True);
            }
            finally
            {
                controller.CloseDialogue();
                controller.ReturnToLobby();
                controller.LobbyHud.ShowTab(LobbyTab.Home);
            }
        }

        [UnityTest]
        public IEnumerator StoryEntry_DoesNotCoverHomePanelsAtWideAndNarrowResolutions()
        {
            yield return null;
            DuelPrototypeController controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            LobbyStoryLauncher launcher = Object.FindAnyObjectByType<LobbyStoryLauncher>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(launcher, Is.Not.Null);

            int originalWidth = Screen.width;
            int originalHeight = Screen.height;
            bool originalFullScreen = Screen.fullScreen;
            LobbyTab originalTab = controller.LobbyHud.CurrentTab;
            PrologueMission originalMission = controller.LobbyHud.NextMission;
            bool originalMissionAvailable = controller.LobbyHud.IsNextMissionAvailable;
            CombatFeature originalFeatures = controller.Campaign.Features;
            int originalStageLimit = controller.Campaign.StageLimit;
            try
            {
                controller.CloseDialogue();
                controller.ReturnToLobby();
                foreach (var resolution in new[] { new Vector2Int(1600, 900), new Vector2Int(1280, 960) })
                {
                    Screen.SetResolution(resolution.x, resolution.y, false);
                    yield return null;
                    yield return null;

                    Assert.That(Screen.width, Is.EqualTo(resolution.x));
                    Assert.That(Screen.height, Is.EqualTo(resolution.y));
                    // Mission 6 waiting on stage 02 matches the real home state. Mission 9
                    // also waits after the curriculum opens, adding the header status slip.
                    for (int state = 0; state < 2; state++)
                    {
                        bool curriculumOpen = state == 1;
                        controller.Campaign.SetProgression(curriculumOpen ? CombatFeature.All :
                            CombatFeature.LaneQ | CombatFeature.LaneE | CombatFeature.Cycle,
                            curriculumOpen ? 5 : 2);
                        controller.LobbyHud.SetNextMission(LobbyMissions.All[curriculumOpen ? 4 : 1], false);
                        controller.LobbyHud.ShowTab(LobbyTab.Home);
                        launcher.RefreshAvailability();
                        yield return null;
                        Canvas.ForceUpdateCanvases();

                        Assert.That(launcher.IsVisible, Is.True);
                        RectTransform entry = launcher.Root.GetComponent<RectTransform>();
                        AssertInsideViewport(entry, resolution);
                        Rect entryBounds = ScreenBounds(entry);
                        foreach (string name in new[] { "Home Mission Border", "Home Journey", "Lobby Header" })
                        {
                            RectTransform other = FindRect(controller.LobbyHud.Root, name);
                            Assert.That(entryBounds.Overlaps(ScreenBounds(other)), Is.False,
                                $"Story entry overlaps {name} at {resolution.x}x{resolution.y} (curriculum={curriculumOpen}).");
                        }
                        if (curriculumOpen)
                            Assert.That(entryBounds.Overlaps(ScreenBounds(FindRect(controller.LobbyHud.Root, "Lobby Status Slip"))),
                                Is.False, $"Story entry overlaps the status slip at {resolution.x}x{resolution.y}.");
                    }
                }
            }
            finally
            {
                Screen.SetResolution(originalWidth, originalHeight, originalFullScreen);
                controller.Campaign.SetProgression(originalFeatures, originalStageLimit);
                controller.LobbyHud.SetNextMission(originalMission, originalMissionAvailable);
                controller.LobbyHud.ShowTab(originalTab);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator StoryButton_StartsActualDialogueAndRestoresHomeAfterCompletion()
        {
            yield return null;
            DuelPrototypeController controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            LobbyStoryLauncher launcher = Object.FindAnyObjectByType<LobbyStoryLauncher>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(launcher, Is.Not.Null);

            int stage = controller.Campaign.StageNumber;
            int currency = controller.Campaign.Currency;
            int curriculumDone = controller.Campaign.Curriculum.CompletedCount;
            string curriculumActive = controller.Campaign.Curriculum.Active?.Id;
            try
            {
                controller.CloseDialogue();
                controller.ReturnToLobby();
                controller.LobbyHud.ShowTab(LobbyTab.Home);
                launcher.RefreshAvailability();
                yield return null;
                if (!launcher.IsStoryReady)
                    Assert.Ignore("Production story is not ready: " + launcher.AvailabilityMessage);

                launcher.StoryButton.onClick.Invoke();
                yield return null;

                Assert.That(controller.IsShowingDialogue, Is.True);
                Assert.That(controller.CurrentDialogueLine, Is.Not.Null);
                Assert.That(controller.DialogueHud.IsVisible, Is.True);
                Assert.That(launcher.IsVisible, Is.False);

                controller.CloseDialogue();
                yield return null;

                Assert.That(controller.IsShowingDialogue, Is.False);
                Assert.That(controller.IsInLobby, Is.True);
                Assert.That(controller.LobbyHud.CurrentTab, Is.EqualTo(LobbyTab.Home));
                Assert.That(launcher.IsVisible, Is.True);
                Assert.That(launcher.StoryButton.interactable, Is.True);
                Assert.That(controller.Campaign.StageNumber, Is.EqualTo(stage));
                Assert.That(controller.Campaign.Currency, Is.EqualTo(currency));
                Assert.That(controller.Campaign.Curriculum.CompletedCount, Is.EqualTo(curriculumDone));
                Assert.That(controller.Campaign.Curriculum.Active?.Id, Is.EqualTo(curriculumActive));
            }
            finally
            {
                controller.CloseDialogue();
                controller.ReturnToLobby();
                controller.LobbyHud.ShowTab(LobbyTab.Home);
            }
        }

        [UnityTest]
        public IEnumerator ProductionStoryResource_IsPresentAndValid()
        {
            yield return null;
            TextAsset source = Resources.Load<TextAsset>(LobbyStoryLauncher.DialogueResourcePath);
            Assert.That(source, Is.Not.Null,
                $"Missing Resources/{LobbyStoryLauncher.DialogueResourcePath}.txt");
            Assert.That(string.IsNullOrWhiteSpace(source.text), Is.False,
                "The production story entry document is empty.");

            DialogueScript script = null;
            Assert.DoesNotThrow(() => script = DialogueScriptParser.Parse(
                LobbyStoryLauncher.DialogueResourcePath, source.text));
            Assert.That(script, Is.Not.Null);
            Assert.That(script.Lines, Is.Not.Empty);
        }

        private static RectTransform FindRect(GameObject root, string name)
        {
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>())
                if (rect.name == name) return rect;
            Assert.Fail("Missing visible rect: " + name);
            return null;
        }

        private static Rect ScreenBounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 lowerLeft = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 upperRight = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return Rect.MinMaxRect(lowerLeft.x, lowerLeft.y, upperRight.x, upperRight.y);
        }

        private static void AssertInsideViewport(RectTransform rect, Vector2Int resolution)
        {
            Rect bounds = ScreenBounds(rect);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(0f), $"{rect.name} left at {resolution}");
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(0f), $"{rect.name} bottom at {resolution}");
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(resolution.x), $"{rect.name} right at {resolution}");
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(resolution.y), $"{rect.name} top at {resolution}");
        }
    }
}
