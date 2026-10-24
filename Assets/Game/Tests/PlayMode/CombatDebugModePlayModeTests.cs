using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using TurnLimbo.Runtime.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class CombatDebugModePlayModeTests : InputTestFixture
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator F1_OpensThePreview_FreezesTheOriginalBattle_AndRestoresItsScreenAndClock()
        {
            yield return null;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            using (var scope = new DebugScope())
            {
                DuelPrototypeController controller = scope.Controller;
                scope.StartStage();
                LegacyQueuedDuel original = controller.Session;
                int health = original.Player.Health, enemyHealth = original.Enemy.Health, round = original.RoundNumber;
                float clock = controller.TurnTimeRemaining;
                var visibility = SnapshotChildren(controller);

                Press(keyboard.f1Key);
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(controller.IsCombatDebugMode, Is.True);
                Assert.That(controller.CombatDebugPreview, Is.Not.Null);
                Assert.That(controller.CombatDebugPreview.enabled, Is.False, "Only the owner advances the preview.");
                Assert.That(controller.CombatDebugPreview.Hud.Root.activeInHierarchy, Is.True);
                Assert.That(controller.Hud.Root.activeInHierarchy, Is.False);

                Release(keyboard.f1Key);
                yield return null;
                scope.Advance(3f);
                Assert.That(controller.Session, Is.SameAs(original));
                Assert.That((original.Player.Health, original.Enemy.Health, original.RoundNumber), Is.EqualTo((health, enemyHealth, round)));
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(clock), "The original planning deadline is suspended.");

                Press(keyboard.f1Key);
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(controller.IsCombatDebugMode, Is.False);
                Assert.That(controller.CombatDebugPreview.gameObject.activeInHierarchy, Is.False);
                Assert.That(controller.Session, Is.SameAs(original));
                foreach (KeyValuePair<GameObject, bool> state in visibility)
                    Assert.That(state.Key.activeSelf, Is.EqualTo(state.Value), state.Key.name + " restores its previous visibility.");
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(clock));

                Release(keyboard.f1Key);
                yield return null;
                scope.Advance(.1f);
                Assert.That(controller.TurnTimeRemaining, Is.LessThan(clock), "Closing the preview resumes the same battle.");
            }
        }

        [UnityTest]
        public IEnumerator CountsAndTheirButtons_RestartExactlyTheRequestedQueues_AndLoopWithoutDeathOrProgressWrites()
        {
            yield return null;
            using (var scope = new DebugScope())
            {
                DuelPrototypeController controller = scope.Controller;
                scope.StartStage();
                string campaignBefore = CampaignState(controller);
                string saveBefore = scope.WriteSave();
                scope.EnableAutoSave();
                Assert.That(controller.CombatDebugEnemyAttackCount, Is.EqualTo(5));
                Assert.That(controller.CombatDebugPlayerAttackCount, Is.EqualTo(5));
                Assert.That(controller.SetCombatDebugMode(true), Is.True);
                DuelPrototypeController preview = controller.CombatDebugPreview;
                Assert.That(preview.SaveStore, Is.Null, "A preview cannot write to a real save path.");
                Assert.That(preview.AutoSaveEnabled, Is.False);
                AssertQueues(preview, 5, 5);
                string previewCampaignBefore = CampaignState(preview);

                controller.SetCombatDebugEnemyAttackCount(-20);
                Assert.That(controller.CombatDebugEnemyAttackCount, Is.Zero);
                AssertQueues(preview, 5, 0);
                LegacyQueuedDuel beforeButton = preview.Session;
                ButtonNamed(controller, "Enemy Attack More").onClick.Invoke();
                Assert.That(controller.CombatDebugEnemyAttackCount, Is.EqualTo(1));
                Assert.That(preview.Session, Is.Not.SameAs(beforeButton), "Changing a UI count immediately restarts the preview.");
                AssertQueues(preview, 5, 1);

                controller.SetCombatDebugEnemyAttackCount(100);
                controller.SetCombatDebugPlayerAttackCount(100);
                Assert.That(controller.CombatDebugEnemyAttackCount, Is.EqualTo(10));
                Assert.That(controller.CombatDebugPlayerAttackCount, Is.EqualTo(10));
                AssertQueues(preview, 10, 10);
                controller.SetCombatDebugPlayerAttackCount(-20);
                Assert.That(controller.CombatDebugPlayerAttackCount, Is.Zero);
                AssertQueues(preview, 0, 10);

                LegacyQueuedDuel beforeRestart = preview.Session;
                controller.RestartCombatDebugPreview();
                Assert.That(preview.Session, Is.Not.SameAs(beforeRestart));
                AssertQueues(preview, 0, 10);
                LegacyQueuedDuel firstLoop = preview.Session;
                int loop = controller.CombatDebugLoopNumber, lowestHealth = preview.PlayerHealth;
                for (int frame = 0; frame < 2000 && controller.CombatDebugLoopNumber == loop; frame++)
                {
                    scope.Advance(.025f);
                    lowestHealth = Math.Min(lowestHealth, preview.PlayerHealth);
                    Assert.That(preview.PlayerHealth, Is.GreaterThan(0));
                    Assert.That(preview.EnemyHealth, Is.GreaterThan(0));
                }
                Assert.That(lowestHealth, Is.EqualTo(1), "Ten unanswered attacks reach the preview's nonlethal health floor.");
                Assert.That(controller.CombatDebugLoopNumber, Is.GreaterThan(loop), "A resolved sample starts another sample automatically.");
                Assert.That(preview.Session, Is.Not.SameAs(firstLoop));
                AssertQueues(preview, 0, 10);
                Assert.That(CampaignState(controller), Is.EqualTo(campaignBefore), "The real campaign's currency, skills and experience stay unchanged.");
                Assert.That(CampaignState(preview), Is.EqualTo(previewCampaignBefore), "The isolated preview also awards no skill experience.");
                Assert.That(File.ReadAllText(scope.Store.Path), Is.EqualTo(saveBefore));
            }
        }

        [UnityTest]
        public IEnumerator RepeatedToggles_ReuseOnePreview_AndRestoreCamerasWithoutAddingEventSystemsOrSceneRoots()
        {
            yield return null;
            using (var scope = new DebugScope())
            {
                DuelPrototypeController controller = scope.Controller;
                scope.StartStage();
                EventSystem originalEvents = EventSystem.current;
                int eventSystems = All<EventSystem>().Length, activeCameras = ActiveCameras();
                Assert.That(controller.SetCombatDebugMode(true), Is.True);
                DuelPrototypeController cached = controller.CombatDebugPreview;
                Assert.That(controller.SetCombatDebugMode(false), Is.True);
                yield return null;
                int controllersAfterWarmup = All<DuelPrototypeController>().Length;
                int rootsAfterWarmup = controller.GetComponentsInChildren<Transform>(true).Length;

                for (int toggle = 0; toggle < 3; toggle++)
                {
                    Assert.That(controller.SetCombatDebugMode(true), Is.True);
                    Assert.That(controller.CombatDebugPreview, Is.SameAs(cached));
                    Assert.That(cached.gameObject.activeInHierarchy, Is.True);
                    Assert.That(cached.enabled, Is.False);
                    Assert.That(EventSystem.current, Is.SameAs(originalEvents));
                    Assert.That(All<EventSystem>().Length, Is.EqualTo(eventSystems));
                    Assert.That(controller.SetCombatDebugMode(false), Is.True);
                    yield return null;
                    Assert.That(cached.gameObject.activeInHierarchy, Is.False);
                    Assert.That(All<DuelPrototypeController>().Length, Is.EqualTo(controllersAfterWarmup));
                    Assert.That(controller.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(rootsAfterWarmup),
                        "Reopening must not accumulate another arena, HUD or transient-effect root.");
                    Assert.That(ActiveCameras(), Is.EqualTo(activeCameras));
                    Assert.That(All<EventSystem>().Length, Is.EqualTo(eventSystems));
                }
            }
        }

        [UnityTest]
        public IEnumerator FeaturedSampleButton_RestartsWithDeepThrust_AndReturnsToTheOriginalSample()
        {
            yield return null;
            using (var scope = new DebugScope())
            {
                DuelPrototypeController controller = scope.Controller;
                scope.StartStage();
                LegacyQueuedDuel original = controller.Session;
                string campaignBefore = CampaignState(controller);
                float clock = controller.TurnTimeRemaining;
                Assert.That(controller.CombatDebugDeepThrustEnabled, Is.False);
                Assert.That(controller.SetCombatDebugMode(true), Is.True);
                DuelPrototypeController preview = controller.CombatDebugPreview;
                bool rhythm = controller.CombatDebugAttackRhythmEnabled;
                LegacyQueuedDuel beforeSelection = preview.Session;

                ButtonNamed(controller, "Debug Deep Thrust").onClick.Invoke();
                Assert.That(controller.CombatDebugDeepThrustEnabled && preview.CombatDebugDeepThrustEnabled, Is.True);
                Assert.That(preview.Session, Is.Not.SameAs(beforeSelection));
                AssertQueues(preview, controller.CombatDebugPlayerAttackCount, controller.CombatDebugEnemyAttackCount);
                foreach (LegacySkill skill in preview.Session.PlayerQueue)
                    Assert.That(skill.Id, Is.EqualTo(LegacyFeaturedTechnique.SkillId));
                Assert.That(preview.Session.EnemyQueue[0].Id, Is.EqualTo(-10001));
                Assert.That(controller.CombatDebugAttackRhythmEnabled, Is.EqualTo(rhythm),
                    "Choosing a technique keeps the existing attack-emphasis comparison setting.");
                controller.RestartCombatDebugPreview();
                Assert.That(preview.Session.PlayerQueue[0].Id, Is.EqualTo(LegacyFeaturedTechnique.SkillId));

                ButtonNamed(controller, "Debug Deep Thrust").onClick.Invoke();
                Assert.That(controller.CombatDebugDeepThrustEnabled || preview.CombatDebugDeepThrustEnabled, Is.False);
                Assert.That(preview.Session.PlayerQueue[0].Id, Is.EqualTo(-10001));
                Assert.That(controller.Session, Is.SameAs(original));
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(clock));
                Assert.That(CampaignState(controller), Is.EqualTo(campaignBefore));
            }
        }

        [UnityTest]
        public IEnumerator AnOnlineRoom_RefusesTheLocalPreview_AndKeepsItsOwnScreenActive()
        {
            yield return null;
            using (var scope = new DebugScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                Assert.That(controller.StartOnlineVersus(), Is.True);
                DuelPrototypeController before = controller.CombatDebugPreview;
                Assert.That(controller.IsInOnlineVersus, Is.True);
                Assert.That(controller.SetCombatDebugMode(true), Is.False);
                Assert.That(controller.IsCombatDebugMode, Is.False);
                Assert.That(controller.CombatDebugPreview, Is.SameAs(before));
                Assert.That(controller.IsInOnlineVersus, Is.True);
                Assert.That(controller.OnlineVersus.Hud.Root.activeInHierarchy, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator F1_RestoresAPlayingChildLoop_AndKeepsAnOpenStoryReplayList()
        {
            yield return null;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            using (var scope = new DebugScope())
            {
                DuelPrototypeController controller = scope.Controller;
                LobbyStoryLauncher launcher = controller.GetComponent<LobbyStoryLauncher>();
                Assert.That(launcher, Is.Not.Null);
                controller.ReturnToLobby();
                controller.LobbyHud.ShowTab(LobbyTab.Home);
                launcher.RefreshAvailability();
                typeof(LobbyStoryLauncher).GetMethod("Update", Private).Invoke(launcher, null);
                Assert.That(launcher.StoryButton.interactable, Is.True);
                launcher.StoryButton.onClick.Invoke();
                Assert.That(launcher.IsReplayListOpen, Is.True);

                var audioObject = new GameObject("Debug Restoration Loop");
                audioObject.transform.SetParent(controller.transform, false);
                AudioSource source = audioObject.AddComponent<AudioSource>();
                AudioClip clip = AudioClip.Create("Debug Restoration Test Clip", 48000, 1, 48000, false);
                source.playOnAwake = false;
                source.loop = true;
                source.volume = 0f;
                source.clip = clip;
                try
                {
                    source.Play();
                    yield return null;
                    Assert.That(source.isPlaying, Is.True, "This restoration check requires a running audio source.");
                    source.timeSamples = 12000;
                    Press(keyboard.f1Key);
                    yield return null;
                    int resumeSample = source.timeSamples;
                    scope.Advance(0f, keyboard);
                    Assert.That(controller.IsCombatDebugMode, Is.True);
                    Assert.That(audioObject.activeInHierarchy, Is.False);
                    Assert.That(source.isPlaying, Is.False);
                    typeof(LobbyStoryLauncher).GetMethod("Update", Private).Invoke(launcher, null);
                    Assert.That(launcher.IsReplayListOpen, Is.True, "The launcher's normal Update must leave the suspended list alone.");

                    Release(keyboard.f1Key);
                    yield return null;
                    scope.Advance(.1f);
                    Press(keyboard.f1Key);
                    yield return null;
                    scope.Advance(0f, keyboard);
                    Assert.That(controller.IsCombatDebugMode, Is.False);
                    Assert.That(audioObject.activeInHierarchy, Is.True);
                    Assert.That(source.isPlaying, Is.True, "A child loop stopped by deactivation must resume after F1.");
                    Assert.That(source.clip, Is.SameAs(clip));
                    Assert.That(source.timeSamples, Is.EqualTo(resumeSample).Within(2048),
                        "Resume near the paused position rather than starting the loop over; allow an audio buffer's progress.");
                    typeof(LobbyStoryLauncher).GetMethod("Update", Private).Invoke(launcher, null);
                    Assert.That(launcher.IsReplayListOpen, Is.True);
                    Assert.That(launcher.Root.activeInHierarchy, Is.True);
                    Release(keyboard.f1Key);
                }
                finally
                {
                    controller.SetCombatDebugMode(false);
                    launcher.CloseReplayList();
                    source.Stop();
                    Object.Destroy(audioObject);
                    Object.Destroy(clip);
                }
            }
        }

        private static void AssertQueues(DuelPrototypeController preview, int player, int enemy)
        {
            Assert.That(preview.Session.Phase, Is.EqualTo(LegacyDuelPhase.Resolving));
            Assert.That(preview.Session.PlayerQueue.Count, Is.EqualTo(player));
            Assert.That(preview.Session.EnemyQueue.Count, Is.EqualTo(enemy));
            Assert.That(preview.Session.LaneReservationsThisTurn(0), Is.Zero,
                "The preview prepares fixed samples without spending manual reservations.");
            Assert.That(preview.Session.RoundNumber, Is.EqualTo(1));
        }

        private static string CampaignState(DuelPrototypeController controller)
            => GameSaveCodec.Serialize(GameSave.Capture(new PrologueRun(), controller.Campaign));

        private static Dictionary<GameObject, bool> SnapshotChildren(DuelPrototypeController controller)
        {
            var result = new Dictionary<GameObject, bool>();
            foreach (Transform child in controller.transform) result.Add(child.gameObject, child.gameObject.activeSelf);
            return result;
        }

        private static Button ButtonNamed(DuelPrototypeController controller, string name)
        {
            foreach (Button button in controller.GetComponentsInChildren<Button>(true))
                if (button.name == name && button.gameObject.activeInHierarchy) return button;
            Assert.Fail("Missing active debug button: " + name);
            return null;
        }

        private static T[] All<T>() where T : Object
            => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        private static int ActiveCameras()
        {
            int count = 0;
            foreach (Camera camera in All<Camera>()) if (camera.isActiveAndEnabled) count++;
            return count;
        }

        private sealed class DebugScope : IDisposable
        {
            private readonly bool originalEnabled, originalStartCards, originalStoryProgression, originalAutoSave,
                originalDeepThrust;
            private readonly int originalEnemyCount, originalPlayerCount;
            private readonly GameSaveStore originalStore;
            private readonly string directory;
            private readonly Action<float, Keyboard> advance;
            private readonly PropertyInfo storyProgression = typeof(DuelPrototypeController).GetProperty("StoryProgressionEnabled");
            private readonly PropertyInfo autoSave = typeof(DuelPrototypeController).GetProperty("AutoSaveEnabled");
            public DuelPrototypeController Controller { get; }
            public GameSaveStore Store { get; }

            public DebugScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                originalStartCards = Controller.StartCardsEnabled;
                originalStoryProgression = Controller.StoryProgressionEnabled;
                originalAutoSave = Controller.AutoSaveEnabled;
                originalEnemyCount = Controller.CombatDebugEnemyAttackCount;
                originalPlayerCount = Controller.CombatDebugPlayerAttackCount;
                originalDeepThrust = Controller.CombatDebugDeepThrustEnabled;
                originalStore = Controller.SaveStore;
                Controller.enabled = false;
                Controller.SetCombatDebugMode(false);
                Controller.SetCombatDebugDeepThrustEnabled(false);
                Controller.StartCardsEnabled = false;
                storyProgression.SetValue(Controller, false);
                autoSave.SetValue(Controller, false);
                directory = Path.Combine(Application.temporaryCachePath, "CombatDebugTests-" + Guid.NewGuid().ToString("N"));
                Store = new GameSaveStore(Path.Combine(directory, GameSaveStore.FileName));
                Controller.SaveStore = Store;
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                    typeof(DuelPrototypeController).GetMethod("AdvancePresentation", Private));
                Controller.RestartJourney();
            }

            public void StartStage()
            {
                if (!Controller.IsInLobby) Controller.ReturnToLobby();
                Assert.That(Controller.StartCampaignStage(1), Is.True);
                Assert.That(Controller.CanChoose, Is.True);
            }

            public void Advance(float seconds, Keyboard keyboard = null) => advance(seconds, keyboard);
            public void EnableAutoSave() => autoSave.SetValue(Controller, true);

            public string WriteSave()
            {
                Assert.That(Store.TrySave(GameSave.Capture(new PrologueRun(), Controller.Campaign), out string error), Is.True, error);
                return File.ReadAllText(Store.Path);
            }

            public void Dispose()
            {
                Controller.SetCombatDebugMode(false);
                autoSave.SetValue(Controller, false);
                Controller.ShowTitle();
                Controller.SetCombatDebugEnemyAttackCount(originalEnemyCount);
                Controller.SetCombatDebugPlayerAttackCount(originalPlayerCount);
                Controller.SetCombatDebugDeepThrustEnabled(originalDeepThrust);
                Controller.RestartJourney();
                Controller.StartCardsEnabled = originalStartCards;
                storyProgression.SetValue(Controller, originalStoryProgression);
                typeof(DuelPrototypeController).GetMethod("SyncStoryProgression", Private).Invoke(Controller, null);
                Controller.SaveStore = originalStore;
                autoSave.SetValue(Controller, originalAutoSave);
                Controller.enabled = originalEnabled;
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
