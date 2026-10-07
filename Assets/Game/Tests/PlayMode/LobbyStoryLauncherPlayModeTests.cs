using System.Collections;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Cutscene;
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
            Assert.That(Object.FindObjectsByType<LobbyStoryLauncher>(FindObjectsInactive.Include).Length, Is.EqualTo(1));

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
                Assert.That(launcher.StoryButton.GetComponentInChildren<Text>().text, Does.Contain("다시보기"));
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

                        launcher.StoryButton.onClick.Invoke();
                        yield return null;
                        Assert.That(launcher.IsReplayListOpen, Is.True);
                        AssertInsideViewport(FindRect(controller.LobbyHud.Root, "Story Replay Ledger"), resolution);
                        launcher.CloseReplayList();
                    }
                }
            }
            finally
            {
                launcher.CloseReplayList();
                Screen.SetResolution(originalWidth, originalHeight, originalFullScreen);
                controller.Campaign.SetProgression(originalFeatures, originalStageLimit);
                controller.LobbyHud.SetNextMission(originalMission, originalMissionAvailable);
                controller.LobbyHud.ShowTab(originalTab);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReplayList_OffersOnlyCompletedProductionScenes()
        {
            yield return null;
            DuelPrototypeController controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            LobbyStoryLauncher launcher = Object.FindAnyObjectByType<LobbyStoryLauncher>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(launcher, Is.Not.Null);

            int originalCleared = controller.Prologue.ClearedCount;
            try
            {
                controller.StopCutscene();
                controller.CloseDialogue();
                controller.ReturnToLobby();
                controller.LobbyHud.ShowTab(LobbyTab.Home);
                Assert.That(controller.Prologue.TryRestore(2), Is.True);
                launcher.RefreshAvailability();
                yield return null;

                Assert.That(launcher.AvailableScenes.Select(scene => scene.ResourcePath), Is.EqualTo(new[]
                {
                    "Cutscene/opening",
                    "Cutscene/mission-01-intro", "Cutscene/mission-01-outro",
                    "Cutscene/mission-02-intro", "Cutscene/mission-02-outro",
                }));
                launcher.StoryButton.onClick.Invoke();
                yield return null;
                Assert.That(launcher.IsReplayListOpen, Is.True);
                Assert.That(controller.IsPlayingCutscene, Is.False, "Opening the list must not start a scene.");
                Assert.That(controller.IsShowingDialogue, Is.False);
                Assert.That(launcher.ReplayScene("Cutscene/mission-04-event"), Is.False,
                    "Unseen missions must not be playable from the replay list.");
                Assert.That(controller.ReplayStoryCutscene("Cutscene/mission-04-event", 4), Is.False,
                    "The playback API must also reject unseen missions.");
                Assert.That(launcher.ReplayScene("Dialogue/dialogue"), Is.False,
                    "The temporary test dialogue is not a story replay.");

                Button closeButton = controller.LobbyHud.Root.GetComponentsInChildren<Button>()
                    .FirstOrDefault(button => button.name == "Story Replay Close");
                Assert.That(closeButton, Is.Not.Null);
                closeButton.onClick.Invoke();
                Assert.That(launcher.IsReplayListOpen, Is.False);

                Assert.That(controller.Prologue.TryRestore(4), Is.True);
                launcher.RefreshAvailability();
                Assert.That(launcher.AvailableScenes.Select(scene => scene.ResourcePath), Is.EqualTo(new[]
                {
                    "Cutscene/opening",
                    "Cutscene/mission-01-intro", "Cutscene/mission-01-outro",
                    "Cutscene/mission-02-intro", "Cutscene/mission-02-outro",
                    "Cutscene/mission-03-intro", "Cutscene/mission-03-outro",
                    "Cutscene/mission-04-intro", "Cutscene/mission-04-event", "Cutscene/mission-04-outro",
                }));
                foreach (var scene in launcher.AvailableScenes)
                {
                    Assert.That(scene.Title, Is.Not.Empty);
                    int missionNumber = scene.ResourcePath == "Cutscene/opening" ? 0 :
                        int.Parse(scene.ResourcePath.Substring("Cutscene/mission-".Length, 2));
                    Assert.That(scene.MissionNumber, Is.EqualTo(missionNumber), scene.ResourcePath);
                }
            }
            finally
            {
                launcher.CloseReplayList();
                controller.StopCutscene();
                controller.CloseDialogue();
                controller.Prologue.TryRestore(originalCleared);
                controller.ReturnToLobby();
                controller.LobbyHud.ShowTab(LobbyTab.Stages);
                controller.LobbyHud.ShowTab(LobbyTab.Home);
                launcher.RefreshAvailability();
            }
        }

        [UnityTest]
        public IEnumerator StoryButton_ReplaysOpeningAndCompletedMissionAndRestoresHome()
        {
            yield return null;
            DuelPrototypeController controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            LobbyStoryLauncher launcher = Object.FindAnyObjectByType<LobbyStoryLauncher>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(launcher, Is.Not.Null);

            int originalCleared = controller.Prologue.ClearedCount;
            int stage = controller.Campaign.StageNumber;
            int stagesCleared = controller.Campaign.ClearedStageCount;
            int currency = controller.Campaign.Currency;
            int curriculumDone = controller.Campaign.Curriculum.CompletedCount;
            string curriculumActive = controller.Campaign.Curriculum.Active?.Id;
            try
            {
                controller.StopCutscene();
                controller.CloseDialogue();
                controller.ReturnToLobby();
                controller.LobbyHud.ShowTab(LobbyTab.Home);
                Assert.That(controller.Prologue.TryRestore(4), Is.True);
                launcher.RefreshAvailability();
                yield return null;

                foreach (string resourcePath in new[] { "Cutscene/opening", "Cutscene/mission-04-outro" })
                {
                    launcher.StoryButton.onClick.Invoke();
                    yield return null;
                    Assert.That(launcher.IsReplayListOpen, Is.True);
                    Assert.That(controller.IsPlayingCutscene, Is.False);

                    Button replayButton = controller.LobbyHud.Root.GetComponentsInChildren<Button>()
                        .FirstOrDefault(button => button.name == "Story Replay " + resourcePath);
                    Assert.That(replayButton, Is.Not.Null, resourcePath);
                    replayButton.onClick.Invoke();
                    Assert.That(controller.IsPlayingCutscene, Is.True, resourcePath);
                    if (resourcePath == "Cutscene/mission-04-outro")
                        Assert.That(controller.ArenaView.EnemyPowerAura.IsAuraOn, Is.True,
                            "The replayed outro begins with the aura earned in the battle.");
                    Assert.That(launcher.IsReplayListOpen, Is.False);
                    Assert.That(launcher.IsVisible, Is.False);

                    Assert.That(controller.SkipCutscene(), Is.True);
                    yield return null;
                    Assert.That(controller.IsPlayingCutscene, Is.False);
                    Assert.That(controller.IsInLobby, Is.True);
                    Assert.That(controller.LobbyHud.CurrentTab, Is.EqualTo(LobbyTab.Home));
                    Assert.That(launcher.IsVisible, Is.True);
                    Assert.That(launcher.StoryButton.interactable, Is.True);
                    Assert.That(controller.Prologue.ClearedCount, Is.EqualTo(4));
                    Assert.That(controller.Campaign.StageNumber, Is.EqualTo(stage));
                    Assert.That(controller.Campaign.ClearedStageCount, Is.EqualTo(stagesCleared));
                    Assert.That(controller.Campaign.Currency, Is.EqualTo(currency));
                    Assert.That(controller.Campaign.Curriculum.CompletedCount, Is.EqualTo(curriculumDone));
                    Assert.That(controller.Campaign.Curriculum.Active?.Id, Is.EqualTo(curriculumActive));
                }
            }
            finally
            {
                launcher.CloseReplayList();
                controller.StopCutscene();
                controller.CloseDialogue();
                controller.Prologue.TryRestore(originalCleared);
                controller.ReturnToLobby();
                controller.LobbyHud.ShowTab(LobbyTab.Home);
                launcher.RefreshAvailability();
            }
        }

        [UnityTest]
        public IEnumerator ProductionReplayCutscenes_ArePresentAndValid()
        {
            yield return null;
            foreach (string resourcePath in new[]
            {
                "Cutscene/opening",
                "Cutscene/mission-01-intro", "Cutscene/mission-01-outro",
                "Cutscene/mission-02-intro", "Cutscene/mission-02-outro",
                "Cutscene/mission-03-intro", "Cutscene/mission-03-outro",
                "Cutscene/mission-04-intro", "Cutscene/mission-04-event", "Cutscene/mission-04-outro",
            })
            {
                TextAsset source = Resources.Load<TextAsset>(resourcePath);
                Assert.That(source, Is.Not.Null, $"Missing Resources/{resourcePath}.txt");
                Assert.That(string.IsNullOrWhiteSpace(source.text), Is.False, resourcePath);
                int missionNumber = resourcePath == "Cutscene/opening" ? 0 :
                    int.Parse(resourcePath.Substring("Cutscene/mission-".Length, 2));
                CutsceneScript script = null;
                Assert.DoesNotThrow(() => script = CutsceneScriptParser.Parse(resourcePath, source.text,
                    missionNumber == 0 ? null : StoryMissions.Get(missionNumber).SceneCast), resourcePath);
                Assert.That(script.Steps, Is.Not.Empty, resourcePath);
            }
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
