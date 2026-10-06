using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>넘기기 (Shift) through the real controller: the key and the HUD button turn every open lane together.</summary>
    public sealed class LaneCyclePlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator Shift_TurnsEveryLaneOncePerPress_AndTheButtonDoesTheSame()
        {
            yield return null;
            using (var scope = new CycleScope())
            {
                scope.UseSession(CombatFeature.All);
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                DuelPrototypeController controller = scope.Controller;
                LegacyQueuedDuel duel = controller.Session;
                int act = duel.Act;
                Press(keyboard.leftShiftKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(Fronts(duel), Is.EqualTo(new[] { 102, 106, 105 }), "Every lane turns at once; a one-skill lane stays.");
                Assert.That(Label(controller.Hud.Root.transform, "Current Q", "Skill Name").text, Is.EqualTo("둘"),
                    "The card shows the skill brought forward.");
                for (int frame = 0; frame < 3; frame++)
                {
                    yield return null;
                    scope.Advance(0f, keyboard);
                }
                Assert.That(duel.LaneCyclesThisTurn, Is.EqualTo(1), "Holding Shift must not repeat.");
                Release(keyboard.leftShiftKey);
                yield return null;
                Press(keyboard.rightShiftKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Release(keyboard.rightShiftKey);
                yield return null;
                Assert.That(duel.LaneCyclesThisTurn, Is.EqualTo(2), "Either Shift works.");
                Assert.That(Fronts(duel), Is.EqualTo(new[] { 104, 103, 105 }));

                Button button = CycleButton(controller);
                Assert.That(button.gameObject.activeSelf, Is.True);
                Assert.That(button.interactable, Is.True);
                Assert.That(Label(button.transform, "KeyHint").text, Is.EqualTo("Shift"));
                // The turn slide: the lanes that turned play it, a one-skill lane does not, and it settles on real time.
                LegacyCombatHud hud = controller.Hud;
                Assert.That(hud.IsLaneTurning(0) && hud.IsLaneTurning(1), Is.True, "Both multi-skill lanes turned together.");
                Assert.That(hud.IsLaneTurning(2), Is.False, "A one-skill lane has nothing to bring forward.");
                RectTransform qIcon = Named(controller.Hud.Root.transform, "Current Q").Find("Skill Image").GetComponent<RectTransform>();
                Assert.That(qIcon.anchoredPosition.y, Is.GreaterThan(10f), "The new front skill is still dropping in.");
                // A held explanation is anchored to the icon's resting place, not to the sliding icon.
                RectTransform explain = Named(controller.Hud.Root.transform, "Skill Explain").GetComponent<RectTransform>();
                hud.ShowExplanation(duel.GetLane(0)[0], false);
                Vector2 duringTurn = explain.anchoredPosition;
                scope.Advance(LegacyCombatHud.LaneTurnDuration);
                Assert.That(hud.IsLaneTurning(0) || hud.IsLaneTurning(1), Is.False);
                Assert.That(qIcon.anchoredPosition, Is.EqualTo(new Vector2(0f, 10f)));
                hud.ShowExplanation(duel.GetLane(0)[0], false);
                Assert.That(Vector2.Distance(explain.anchoredPosition, duringTurn), Is.LessThan(.01f), "The explanation did not slide.");
                hud.HideExplanation();
                Assert.That(qIcon.localScale, Is.EqualTo(Vector3.one));
                Assert.That(Named(controller.Hud.Root.transform, "Current Q").localScale, Is.EqualTo(Vector3.one));
                button.onClick.Invoke();
                Assert.That(Fronts(duel), Is.EqualTo(new[] { 101, 106, 105 }), "Three turns bring the three-skill lane back.");
                Assert.That(duel.LaneCyclesThisTurn, Is.EqualTo(3), "There is no limit.");
                Assert.That(duel.Act, Is.EqualTo(act), "넘기기 is free.");
                Assert.That(duel.PlayerQueue, Is.Empty, "Nothing is queued.");
            }
        }

        [Test]
        public void Cycle_SpendsASecondOfPlanningTime_AndNeedsMoreThanThatLeft()
        {
            using (var scope = new CycleScope())
            {
                scope.UseSession(CombatFeature.All);
                DuelPrototypeController controller = scope.Controller;
                Assert.That(controller.LaneCycleCost, Is.EqualTo(DuelPrototypeController.LaneCycleTimeCost), "A stage's clock runs.");
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(10f));
                int act = controller.Session.Act;
                Assert.That(controller.CycleLanes(), Is.True);
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(9f).Within(1e-4f), "넘기기 pays with planning time…");
                Assert.That(controller.Session.Act, Is.EqualTo(act), "…not ACT.");
                Assert.That(Label(controller.Hud.Root.transform, "CycleButton", "Time Cost").text, Is.EqualTo("1초 소모"));
                Assert.That(controller.Hud.IsShowingTimeSpent, Is.True);
                Assert.That(Label(controller.Hud.Root.transform, "Time Spent").text, Is.EqualTo("-1초"));

                scope.Advance(7.4f);
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(1.6f).Within(1e-3f));
                Assert.That(controller.Hud.IsShowingTimeSpent, Is.False, "The '-1초' fades on real time.");
                Assert.That(controller.CycleLanes(), Is.True, "It may spend a second that leaves at least half a second…");
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(.6f).Within(1e-3f));
                Assert.That(controller.CanAffordLaneCycle, Is.False);
                Assert.That(controller.CycleLanes(), Is.False, "…but never more, so it never runs the clock out itself.");
                scope.Advance(1f / 60f);
                Assert.That(controller.IsResolving, Is.False, "The skill it brought forward can still be queued.");
                scope.Advance(0f);
                Assert.That(CycleButton(controller).interactable, Is.False, "The button waits for time it cannot have.");
            }
        }

        [UnityTest]
        public IEnumerator LaneKeyHeldThroughShift_DoesNotQueueTheSkillThatCameForward()
        {
            yield return null;
            using (var scope = new CycleScope())
            {
                scope.UseSession(CombatFeature.All);
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                DuelPrototypeController controller = scope.Controller;
                LegacyQueuedDuel duel = controller.Session;
                Press(keyboard.qKey);
                yield return null;
                scope.Advance(0f, keyboard);
                yield return null;
                scope.Advance(0f, keyboard);
                Press(keyboard.leftShiftKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(duel.LaneCyclesThisTurn, Is.EqualTo(1));
                Release(keyboard.qKey);
                Release(keyboard.leftShiftKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(duel.PlayerQueue, Is.Empty, "Q was pressed for the skill that just left.");
                Assert.That(Fronts(duel)[0], Is.EqualTo(102));

                Press(keyboard.qKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Release(keyboard.qKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(duel.PlayerQueue.Count, Is.EqualTo(1), "A fresh tap queues the skill brought forward.");
                Assert.That(duel.PlayerQueue[0].Id, Is.EqualTo(102));
            }
        }

        [UnityTest]
        public IEnumerator Cycle_IsRefusedWhileInspectingOrResolving_AndHiddenWhenClosed()
        {
            yield return null;
            using (var scope = new CycleScope())
            {
                scope.UseSession(CombatFeature.All);
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                DuelPrototypeController controller = scope.Controller;
                LegacyQueuedDuel duel = controller.Session;
                Press(keyboard.tabKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(CycleButton(controller).interactable, Is.False, "Inspecting the enemy holds the lanes.");
                Assert.That(controller.CycleLanes(), Is.False);
                Press(keyboard.leftShiftKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Release(keyboard.leftShiftKey);
                Release(keyboard.tabKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(duel.LaneCyclesThisTurn, Is.Zero);
                Assert.That(Fronts(duel), Is.EqualTo(new[] { 101, 103, 105 }));
                Assert.That(CycleButton(controller).interactable, Is.True);

                Assert.That(controller.QueueLane(0), Is.True);
                Assert.That(controller.CycleLanes(), Is.True, "Queueing first is fine.");
                Assert.That(duel.PlayerQueue.Count, Is.EqualTo(1), "넘기기 never touches what is already queued.");
                Assert.That(duel.PlayerQueue[0].Id, Is.EqualTo(101));
                Assert.That(Fronts(duel), Is.EqualTo(new[] { 104, 106, 105 }));
                controller.CommitTurn();
                Assert.That(controller.IsResolving, Is.True);
                Assert.That(controller.CycleLanes(), Is.False, "Only while planning.");
                scope.Advance(0f);
                Assert.That(CycleButton(controller).interactable, Is.False);
                scope.Until(() => controller.CanChoose);
                Assert.That(Fronts(duel), Is.EqualTo(new[] { 104, 106, 105 }), "The turned order carries into the next turn.");
                Assert.That(duel.LaneCyclesThisTurn, Is.Zero);
                Assert.That(controller.CycleLanes(), Is.True);

                scope.UseSession(CombatFeature.LaneQ | CombatFeature.LaneW | CombatFeature.LaneE);
                Assert.That(CycleButton(controller).gameObject.activeSelf, Is.False, "A closed 넘기기 is not drawn.");
                Assert.That(Named(controller.Hud.Root.transform, "Cycle Note").gameObject.activeSelf, Is.False);
                Press(keyboard.leftShiftKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Release(keyboard.leftShiftKey);
                yield return null;
                Assert.That(Fronts(controller.Session), Is.EqualTo(new[] { 101, 103, 105 }));
                Assert.That(controller.CycleLanes(), Is.False);
            }
        }

        [UnityTest]
        public IEnumerator MissionOne_KeepsShiftClosed_AndMissionTwoTeachesItBeforeFreePlay()
        {
            yield return null;
            using (var scope = new CycleScope())
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                DuelPrototypeController controller = scope.Controller;
                controller.StartNewGame();
                Assert.That(controller.StartMission(), Is.True);
                scope.SkipIntro();
                Assert.That(controller.ActiveMission.Number, Is.EqualTo(1));
                Assert.That(controller.AdvanceGuide(), Is.True);
                scope.Advance(0f);
                Assert.That(CycleButton(controller).gameObject.activeSelf, Is.False, "The first mission is about striking only.");
                Assert.That(controller.CycleLanes(), Is.False);
                Press(keyboard.leftShiftKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Release(keyboard.leftShiftKey);
                yield return null;
                Assert.That(controller.Session.LaneCyclesThisTurn, Is.Zero);

                Assert.That(controller.Prologue.TryRestore(1), Is.True);
                controller.ReturnToLobby();
                Assert.That(controller.IsInBriefing, Is.True);
                Assert.That(controller.BriefingHud.Mission.Number, Is.EqualTo(2));
                Assert.That(controller.StartMission(), Is.True);
                scope.SkipIntro();
                Assert.That(controller.ActiveMission.Number, Is.EqualTo(2));
                MissionGuide guide = controller.Guide;
                LegacyQueuedDuel duel = controller.Session;
                Assert.That(duel.Features.Has(CombatFeature.Cycle), Is.True);
                scope.Advance(0f);
                Assert.That(CycleButton(controller).gameObject.activeSelf, Is.False, "The button waits for its lesson.");
                Assert.That(controller.CycleLanes(), Is.False);

                // Walk the coach to its 넘기기 beat; the duel stays in its first planning phase meanwhile.
                for (int beat = 0; beat < 20 && guide.Kind != MissionGuideStepKind.Cycle; beat++)
                {
                    switch (guide.Kind)
                    {
                        case MissionGuideStepKind.Inspect: guide.NotifyInspected(); break;
                        case MissionGuideStepKind.Queue: guide.NotifyQueued(guide.ExpectedLane); break;
                        case MissionGuideStepKind.Commit: guide.NotifyCommitted(); break;
                        case MissionGuideStepKind.WatchTurn: guide.NotifyTurnBegan(2); break;
                        default: Assert.That(guide.TryAdvance(), Is.True, guide.Title); break;
                    }
                }
                Assert.That(guide.Kind, Is.EqualTo(MissionGuideStepKind.Cycle));
                scope.Advance(0f);
                Button button = CycleButton(controller);
                Assert.That(button.gameObject.activeSelf, Is.True, "The lesson shows the button.");
                Assert.That(button.interactable, Is.True);
                Assert.That(controller.QueueLane(0), Is.False, "The beat waits for Shift.");
                controller.CommitTurn();
                Assert.That(controller.IsResolving, Is.False, "…and cannot be skipped by committing.");
                Assert.That(duel.GetLane(0)[0].Id, Is.EqualTo(LegacySkillDefinitions.Skill(1).Id));
                Press(keyboard.leftShiftKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Release(keyboard.leftShiftKey);
                yield return null;
                Assert.That(duel.GetLane(0)[0].Id, Is.EqualTo(LegacySkillDefinitions.Skill(2).Id), "연속 베기 comes forward.");
                Assert.That(guide.IsFree, Is.True, "Shift finishes the lesson.");
                Assert.That(controller.CycleLanes(), Is.True, "Free play keeps 넘기기.");
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(10f), "Mission 2 has no clock, so 넘기기 is free there.");
                Assert.That(Label(controller.Hud.Root.transform, "CycleButton", "Time Cost").text, Is.EqualTo("무료"));
                Assert.That(controller.QueueLane(0), Is.True);
            }
        }

        private static int[] Fronts(LegacyQueuedDuel duel)
            => Enumerable.Range(0, 3).Select(lane => duel.GetLane(lane).Count > 0 ? duel.GetLane(lane)[0].Id : 0).ToArray();

        private static Button CycleButton(DuelPrototypeController controller)
            => Named(controller.Hud.Root.transform, "CycleButton").GetComponent<Button>();

        private static Transform Named(Transform root, string name)
        {
            Transform found = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(node => node.name == name);
            Assert.That(found, Is.Not.Null, "Missing UI node: " + name);
            return found;
        }

        private static Text Label(Transform root, params string[] path)
        {
            Transform node = root;
            foreach (string name in path) node = Named(node, name);
            return node.GetComponent<Text>();
        }

        private sealed class CycleScope : IDisposable
        {
            private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
            private readonly DuelPresentationSettings originalSettings, originalArenaSettings, settings;
            private readonly LegacyQueuedDuel originalSession;
            private readonly bool originallyEnabled;
            private readonly FieldInfo arenaSettings;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }

            public CycleScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                Assert.That(Controller.AutoSaveEnabled, Is.False, "Tests never write the player's save.");
                originallyEnabled = Controller.enabled;
                Controller.enabled = false;
                originalSettings = Controller.PresentationSettings;
                originalSession = Controller.Session;
                arenaSettings = typeof(LegacyArenaView).GetField("settings", PrivateInstance);
                originalArenaSettings = (DuelPresentationSettings)arenaSettings.GetValue(Controller.ArenaView);
                settings = Object.Instantiate(originalSettings);
                JsonUtility.FromJsonOverwrite("{\"hitStopDuration\":0,\"skillInterval\":0}", settings);
                Set("presentationSettings", settings);
                arenaSettings.SetValue(Controller.ArenaView, settings);
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                    typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance));
            }

            /// <summary>A free duel with Q = 하나 둘 넷, W = 셋 여섯 and E = 다섯, so a one-skill lane is in the mix.</summary>
            public void UseSession(CombatFeature features)
            {
                Set("mission", null);
                Set("guide", null);
                Set("session", new LegacyQueuedDuel(1000, 1000, 1000, 1000,
                    new[] { Skill(101, "하나", 0), Skill(102, "둘", 0), Skill(104, "넷", 0), Skill(103, "셋", 1),
                        Skill(106, "여섯", 1), Skill(105, "다섯", 2) },
                    new[] { Skill(201, "적", 0) }, new[] { 1 }, 1, features: features));
                typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance).Invoke(Controller, null);
            }

            public void Advance(float delta, Keyboard keyboard = null) => advance(delta, keyboard);

            public void Until(Func<bool> condition)
            {
                for (int i = 0; i < 10000 && !condition(); i++) Advance(.001f);
                Assert.That(condition(), Is.True, "The real controller should complete the turn within ten simulated seconds.");
            }

            /// <summary>Skips a mission's intro scene (its battlefield cutscene, or its dialogue), which starts the duel.</summary>
            public void SkipIntro()
            {
                Assert.That(Controller.IsPlayingScene, Is.True, "Each mission opens with its intro scene.");
                Assert.That(Controller.SkipScene(), Is.True);
                Assert.That(Controller.IsPlayingScene, Is.False);
            }

            private static LegacySkill Skill(int id, string name, int lane)
                => new LegacySkill(id, name, 1, 1, 1, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, lane,
                    string.Empty, "Slash", 1);

            private void Set(string name, object value)
                => typeof(DuelPrototypeController).GetField(name, PrivateInstance).SetValue(Controller, value);

            public void Dispose()
            {
                Set("presentationSettings", originalSettings);
                arenaSettings.SetValue(Controller.ArenaView, originalArenaSettings);
                Set("session", originalSession);
                // Leave a fresh arc and lobby behind, as the other flow tests do.
                Controller.StartNewGame();
                Controller.RestartJourney();
                Controller.enabled = originallyEnabled;
                Object.Destroy(settings);
            }
        }
    }
}
