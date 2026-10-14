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
        public IEnumerator Launcher_IsSingleAndVisibleInTheFieldMenu()
        {
            yield return null;
            DuelPrototypeController controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            LobbyStoryLauncher launcher = Object.FindAnyObjectByType<LobbyStoryLauncher>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(launcher, Is.Not.Null);
            Assert.That(Object.FindObjectsByType<LobbyStoryLauncher>(FindObjectsInactive.Include).Length, Is.EqualTo(1));

            int originalCleared = controller.Prologue.ClearedCount;
            try
            {
                controller.CloseDialogue();
                Assert.That(controller.Prologue.TryRestore(PrologueMissions.Count), Is.True);
                controller.ReturnToLobby();
                yield return null;
                Assert.That(controller.IsInAcademyField, Is.True);
                Assert.That(launcher.IsVisible, Is.False);
                Assert.That(controller.OpenFieldMenu(), Is.True);
                launcher.RefreshAvailability();
                yield return null;

                Assert.That(launcher.IsVisible, Is.True);
                Assert.That(launcher.StoryButton, Is.Not.Null);
                Assert.That(launcher.StoryButton.interactable, Is.EqualTo(launcher.IsStoryReady));
                Assert.That(launcher.StoryButton.GetComponentInChildren<Text>().text, Does.Contain("다시보기"));
                Assert.That(launcher.StoryButton.navigation.mode, Is.EqualTo(Navigation.Mode.None));
                Assert.That(launcher.Root.transform.parent, Is.SameAs(controller.LobbyHud.Root.transform));
                Canvas.ForceUpdateCanvases();
                Rect replayEntry = ScreenBounds(launcher.Root.GetComponent<RectTransform>());
                Assert.That(replayEntry.Overlaps(ScreenBounds(FindRect(controller.LobbyHud.Root, "Field Menu Close"))), Is.False,
                    "The replay entry leaves room for the field menu close button.");
                Assert.That(replayEntry.Overlaps(ScreenBounds(FindRect(controller.LobbyHud.Root, "Field Menu Header"))), Is.False);

                foreach (LobbyTab tab in new[] { LobbyTab.Loadout, LobbyTab.Curriculum })
                {
                    controller.LobbyHud.ShowTab(tab);
                    yield return null;
                    Assert.That(launcher.IsVisible, Is.True,
                        $"Story replay stays available on {tab} in the field menu.");
                }
                Assert.That(controller.CloseFieldMenu(), Is.True);
                yield return null;
                Assert.That(launcher.IsVisible, Is.False);
            }
            finally
            {
                controller.CloseDialogue();
                controller.Prologue.TryRestore(originalCleared);
                controller.ReturnToLobby();
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

            // The lobby canvas is laid out at each resolution without touching the screen: Screen.SetResolution does
            // nothing in the Editor's Play Mode, and batch mode has no Game view to resize (its screen stays 640x480).
            // For the check the canvas is drawn by its own camera into a texture of that size, so it lays out at
            // exactly that pixel size, with the scale its CanvasScaler gives a screen of that size.
            Canvas lobbyCanvas = controller.LobbyHud.Root.GetComponent<Canvas>();
            CanvasScaler lobbyScaler = controller.LobbyHud.Root.GetComponent<CanvasScaler>();
            Assert.That(lobbyCanvas, Is.Not.Null);
            Assert.That(lobbyScaler, Is.Not.Null);
            Assert.That(lobbyCanvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(lobbyScaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(lobbyScaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight));
            Camera originalCanvasCamera = lobbyCanvas.worldCamera;
            float originalPlaneDistance = lobbyCanvas.planeDistance;
            float originalScaleFactor = lobbyScaler.scaleFactor;
            var cameraObject = new GameObject("Story Entry Layout Camera");
            // Away from the arena, so the canvas this camera places in front of itself stays out of the game's view
            // (near enough that world-space float precision stays far below a pixel).
            cameraObject.transform.position = new Vector3(0f, -1000f, 0f);
            Camera layoutCamera = cameraObject.AddComponent<Camera>();
            layoutCamera.clearFlags = CameraClearFlags.SolidColor;
            layoutCamera.cullingMask = 1 << controller.LobbyHud.Root.layer;
            layoutCamera.depth = -100f;
            RenderTexture layoutTarget = null;

            int originalCleared = controller.Prologue.ClearedCount;
            LobbyTab originalTab = controller.LobbyHud.CurrentTab;
            PrologueMission originalMission = controller.LobbyHud.NextMission;
            bool originalMissionAvailable = controller.LobbyHud.IsNextMissionAvailable;
            CombatFeature originalFeatures = controller.Campaign.Features;
            int originalStageLimit = controller.Campaign.StageLimit;
            try
            {
                controller.CloseDialogue();
                Assert.That(controller.Prologue.TryRestore(0), Is.True);
                controller.ReturnToLobby();
                foreach (var resolution in new[] { new Vector2Int(1600, 900), new Vector2Int(1280, 960) })
                {
                    layoutCamera.targetTexture = null;
                    if (layoutTarget != null) { layoutTarget.Release(); Object.Destroy(layoutTarget); }
                    layoutTarget = new RenderTexture(resolution.x, resolution.y, 24);
                    layoutCamera.targetTexture = layoutTarget;
                    // The factor CanvasScaler's ScaleWithScreenSize gives this screen size, set as a constant because
                    // the scaler may read the display rather than the camera's texture.
                    float expectedScale = ScaleWithScreenSizeFactor(lobbyScaler, resolution);
                    lobbyScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                    lobbyScaler.scaleFactor = expectedScale;
                    lobbyCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                    lobbyCanvas.worldCamera = layoutCamera;
                    lobbyCanvas.planeDistance = 100f;
                    yield return null;
                    yield return null;
                    for (int frame = 0; frame < 10 && !IsLaidOutAt(lobbyCanvas, resolution, expectedScale); frame++)
                        yield return null;

                    Canvas.ForceUpdateCanvases();
                    Assert.That(layoutCamera.pixelWidth, Is.EqualTo(resolution.x), LayoutSizeFailure);
                    Assert.That(layoutCamera.pixelHeight, Is.EqualTo(resolution.y), LayoutSizeFailure);
                    Assert.That(lobbyCanvas.scaleFactor, Is.EqualTo(expectedScale).Within(.0001f), LayoutSizeFailure);
                    Rect canvasRect = ((RectTransform)lobbyCanvas.transform).rect;
                    Assert.That(canvasRect.width * expectedScale, Is.EqualTo(resolution.x).Within(1f), LayoutSizeFailure);
                    Assert.That(canvasRect.height * expectedScale, Is.EqualTo(resolution.y).Within(1f), LayoutSizeFailure);
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
                        AssertInsideViewport(entry, resolution, layoutCamera);
                        Rect entryBounds = ScreenBounds(entry, layoutCamera);
                        foreach (string name in new[] { "Home Mission Border", "Home Journey", "Lobby Header" })
                        {
                            RectTransform other = FindRect(controller.LobbyHud.Root, name);
                            Assert.That(entryBounds.Overlaps(ScreenBounds(other, layoutCamera)), Is.False,
                                $"Story entry overlaps {name} at {resolution.x}x{resolution.y} (curriculum={curriculumOpen}).");
                        }
                        if (curriculumOpen)
                            Assert.That(entryBounds.Overlaps(ScreenBounds(FindRect(controller.LobbyHud.Root, "Lobby Status Slip"),
                                layoutCamera)), Is.False, $"Story entry overlaps the status slip at {resolution.x}x{resolution.y}.");

                        launcher.StoryButton.onClick.Invoke();
                        yield return null;
                        Assert.That(launcher.IsReplayListOpen, Is.True);
                        AssertInsideViewport(FindRect(controller.LobbyHud.Root, "Story Replay Ledger"), resolution, layoutCamera);
                        launcher.CloseReplayList();
                    }
                }
            }
            finally
            {
                launcher.CloseReplayList();
                lobbyCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                lobbyCanvas.worldCamera = originalCanvasCamera;
                lobbyCanvas.planeDistance = originalPlaneDistance;
                lobbyScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                lobbyScaler.scaleFactor = originalScaleFactor;
                layoutCamera.targetTexture = null;
                Object.Destroy(cameraObject);
                if (layoutTarget != null) { layoutTarget.Release(); Object.Destroy(layoutTarget); }
                controller.Campaign.SetProgression(originalFeatures, originalStageLimit);
                controller.Prologue.TryRestore(originalCleared);
                controller.LobbyHud.SetNextMission(originalMission, originalMissionAvailable);
                controller.ReturnToLobby();
                controller.LobbyHud.ShowTab(originalTab);
            }
            yield return null;
            yield return null;
            // The pages read their size while being built, so rebuild the current one at the restored screen size.
            controller.LobbyHud.ShowTab(controller.LobbyHud.CurrentTab);
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
                Assert.That(controller.Prologue.TryRestore(2), Is.True);
                controller.ReturnToLobby();
                controller.LobbyHud.ShowTab(LobbyTab.Home);
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
                if (!controller.IsInAcademyField) controller.LobbyHud.ShowTab(LobbyTab.Home);
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
            int curriculumPoints = controller.Campaign.CurriculumPoints;
            try
            {
                controller.StopCutscene();
                controller.CloseDialogue();
                Assert.That(controller.Prologue.TryRestore(PrologueMissions.Count), Is.True);
                controller.ReturnToLobby();
                Assert.That(controller.IsInAcademyField, Is.True);
                launcher.RefreshAvailability();
                yield return null;

                foreach (string resourcePath in new[] { "Cutscene/opening", "Cutscene/mission-04-outro" })
                {
                    Assert.That(controller.OpenFieldMenu(), Is.True);
                    yield return null;
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
                    Assert.That(controller.IsInAcademyField, Is.True);
                    Assert.That(controller.LobbyHud.IsVisible, Is.False);
                    Assert.That(launcher.IsVisible, Is.False);
                    Assert.That(launcher.StoryButton.interactable, Is.True);
                    Assert.That(controller.Prologue.ClearedCount, Is.EqualTo(4));
                    Assert.That(controller.Campaign.StageNumber, Is.EqualTo(stage));
                    Assert.That(controller.Campaign.ClearedStageCount, Is.EqualTo(stagesCleared));
                    Assert.That(controller.Campaign.Currency, Is.EqualTo(currency));
                    Assert.That(controller.Campaign.Curriculum.CompletedCount, Is.EqualTo(curriculumDone));
                    Assert.That(controller.Campaign.CurriculumPoints, Is.EqualTo(curriculumPoints));
                }
            }
            finally
            {
                launcher.CloseReplayList();
                controller.StopCutscene();
                controller.CloseDialogue();
                controller.Prologue.TryRestore(originalCleared);
                controller.ReturnToLobby();
                if (!controller.IsInAcademyField) controller.LobbyHud.ShowTab(LobbyTab.Home);
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

        /// <summary>Pixel bounds of the rect on its canvas's screen: the real screen for an overlay canvas (no camera),
        /// or the camera's target texture for a canvas drawn by that camera.</summary>
        private static Rect ScreenBounds(RectTransform rect, Camera camera = null)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 lowerLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 upperRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(lowerLeft.x, lowerLeft.y, upperRight.x, upperRight.y);
        }

        private static void AssertInsideViewport(RectTransform rect, Vector2Int resolution, Camera camera)
        {
            Rect bounds = ScreenBounds(rect, camera);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(0f), $"{rect.name} left at {resolution}");
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(0f), $"{rect.name} bottom at {resolution}");
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(resolution.x), $"{rect.name} right at {resolution}");
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(resolution.y), $"{rect.name} top at {resolution}");
        }

        private const string LayoutSizeFailure = "The lobby canvas must lay out at the tested pixel size " +
            "(drawn by a camera into a texture of that size, at the scale its CanvasScaler gives that screen size).";

        /// <summary>CanvasScaler's ScaleWithScreenSize factor for a screen of this size (MatchWidthOrHeight): the
        /// weighted average of the width and height ratios to the reference resolution, taken in log2 space.</summary>
        private static float ScaleWithScreenSizeFactor(CanvasScaler scaler, Vector2Int screen)
        {
            float logWidth = Mathf.Log(screen.x / scaler.referenceResolution.x, 2f);
            float logHeight = Mathf.Log(screen.y / scaler.referenceResolution.y, 2f);
            return Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, scaler.matchWidthOrHeight));
        }

        /// <summary>Whether the canvas has taken the tested size: its scale, and a rect that covers the resolution at
        /// that scale (the layout space every page is built in).</summary>
        private static bool IsLaidOutAt(Canvas canvas, Vector2Int resolution, float scale)
        {
            Rect rect = ((RectTransform)canvas.transform).rect;
            return Mathf.Abs(canvas.scaleFactor - scale) < .0001f &&
                Mathf.Abs(rect.width * scale - resolution.x) < 1f && Mathf.Abs(rect.height * scale - resolution.y) < 1f;
        }
    }
}
