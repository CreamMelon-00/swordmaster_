using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class BreathingInputPlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator SPress_QueuesOncePerPress_AndStopsAtThreeWithoutSpendingAct()
        {
            yield return null;
            using (var scope = new BreathScope())
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                var controller = scope.Controller;
                Press(keyboard.sKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(controller.Session.BreathsQueuedThisTurn, Is.EqualTo(1));
                for (int frame = 0; frame < 3; frame++)
                {
                    yield return null;
                    scope.Advance(0f, keyboard);
                    Assert.That(controller.Session.BreathsQueuedThisTurn, Is.EqualTo(1), "Holding S must not repeat.");
                }
                Release(keyboard.sKey);
                yield return null;
                for (int press = 0; press < 3; press++)
                {
                    Press(keyboard.sKey);
                    yield return null;
                    scope.Advance(0f, keyboard);
                    Release(keyboard.sKey);
                    yield return null;
                }
                Assert.That(controller.Session.PlayerQueue.Count, Is.EqualTo(3));
                Assert.That(controller.Session.BreathsRemainingThisTurn, Is.Zero);
                Assert.That(controller.Session.Act, Is.EqualTo(3));
                Assert.That(controller.Session.GetLane(0)[0].Id, Is.EqualTo(101));
            }
        }

        [UnityTest]
        public IEnumerator ZeroAct_StillAllowsBreathing_WithoutChangingLaneRotation()
        {
            yield return null;
            using (var scope = new BreathScope())
            {
                var controller = scope.Controller;
                Assert.That(controller.QueueLane(0), Is.True);
                Assert.That(controller.QueueBreath(), Is.True);
                Assert.That(controller.QueueLane(1), Is.True);
                Assert.That(controller.QueueLane(2), Is.True);
                Assert.That(controller.Session.Act, Is.Zero);
                Assert.That(controller.QueueBreath(), Is.True);
                Assert.That(controller.Session.PlayerQueue.Count, Is.EqualTo(5));
                Assert.That(controller.Session.PlayerQueue[0].Id, Is.EqualTo(101));
                Assert.That(controller.Session.PlayerQueue[1].IsWait, Is.True);
                Assert.That(controller.Session.PlayerQueue[2].Id, Is.EqualTo(103));
                Assert.That(controller.Session.PlayerQueue[3].Id, Is.EqualTo(105));
                Assert.That(controller.Session.PlayerQueue[4].IsWait, Is.True);
                Assert.That(controller.Session.GetLane(0)[0].Id, Is.EqualTo(102));
                Assert.That(controller.Session.Act, Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator WaitSlot_StaysIdle_TakesUnguardedHits_HidesPressure_AndResetsNextTurn()
        {
            yield return null;
            using (var scope = new BreathScope())
            {
                var controller = scope.Controller;
                Assert.That(controller.QueueBreath(), Is.True);
                Assert.That(controller.QueueBreath(), Is.True);
                Assert.That(controller.QueueBreath(), Is.True);
                controller.CommitTurn();
                Assert.That(controller.QueueBreath(), Is.False);
                scope.Until(() => controller.Session.CurrentSlot != null);
                LegacyCurrentSlot slot = controller.Session.CurrentSlot;
                Assert.That(slot.PlayerSkill.IsWait, Is.True);
                PropertyInfo playerPower = typeof(LegacyCurrentSlot).GetProperty("PlayerPower",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(playerPower, Is.Not.Null);
                Assert.That(playerPower.GetValue(slot), Is.EqualTo(0));
                Assert.That(controller.ActiveSlotDuration, Is.GreaterThan(0f));
                Assert.That(controller.StepHud.DodgeRing.gameObject.activeInHierarchy, Is.True);
                Assert.That(controller.StepHud.PressureRing.gameObject.activeInHierarchy, Is.False);
                Assert.That(controller.ArenaView.IsPressureAttackTrailActive, Is.False);
                scope.Until(() => controller.Session.LastResolvedSlot == 0);
                Assert.That(slot.HitsResolved, Is.EqualTo(2), "Wait must occupy the entire opposing multi-hit slot.");
                Assert.That(controller.PlayerHealth, Is.EqualTo(998));
                Assert.That(controller.Session.Player.Resistance, Is.EqualTo(1000));
                Assert.That(controller.EnemyHealth, Is.EqualTo(1000));
                scope.Until(() => controller.CanChoose);
                Assert.That(controller.Session.RoundNumber, Is.EqualTo(2));
                Assert.That(controller.Session.Act, Is.EqualTo(6), "There is no extra recovery reward for breathing.");
                Assert.That(controller.Session.BreathsQueuedThisTurn, Is.Zero);
                Assert.That(controller.Session.BreathsRemainingThisTurn, Is.EqualTo(3));
                Assert.That(controller.Session.PlayerQueue, Is.Empty);
                Assert.That(controller.QueueBreath(), Is.True);
            }
        }

        [UnityTest]
        public IEnumerator Breathing_IsBlockedDuringInspection_LobbyAndGuidedTutorial()
        {
            yield return null;
            using (var scope = new BreathScope())
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                Press(keyboard.tabKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(scope.Controller.QueueBreath(), Is.False);
                Press(keyboard.sKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(scope.Controller.Session.PlayerQueue, Is.Empty);
                Release(keyboard.sKey);
                Release(keyboard.tabKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(scope.Controller.QueueBreath(), Is.True);
                scope.Controller.ReturnToLobby();
                Assert.That(scope.Controller.IsInLobby, Is.True);
                Assert.That(scope.Controller.QueueBreath(), Is.False);
                Assert.That(scope.Controller.StartTutorial(), Is.True);
                Assert.That(scope.Controller.QueueBreath(), Is.False);
                Assert.That(scope.Controller.Session.BreathsQueuedThisTurn, Is.Zero);
            }
        }

        private sealed class BreathScope : IDisposable
        {
            private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
            private readonly DuelPresentationSettings originalSettings, originalArenaSettings, settings;
            private readonly LegacyQueuedDuel originalSession;
            private readonly bool originallyEnabled;
            private readonly FieldInfo arenaSettings;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }

            public BreathScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
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
                Set("tutorial", null);
                Set("session", new LegacyQueuedDuel(1000, 1000, 1000, 1000,
                    new[] { Skill(101, 0), Skill(102, 0), Skill(103, 1), Skill(105, 2) },
                    new[] { Skill(201, 0, 2) }, new[] { 1 }, 1));
                var method = typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance);
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller, method);
                typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance).Invoke(Controller, null);
            }

            public void Advance(float delta, Keyboard keyboard = null) => advance(delta, keyboard);
            public void Until(Func<bool> condition)
            {
                for (int i = 0; i < 10000 && !condition(); i++) Advance(.001f);
                Assert.That(condition(), Is.True, "The real controller should complete the slot/turn within ten simulated seconds.");
            }
            private static LegacySkill Skill(int id, int lane, int hits = 1)
                => new LegacySkill(id, "Breathing Test", 1, hits, hits, LegacySkillKind.Attack,
                    LegacySkillProperty.Slash, hits, lane, string.Empty, "Slash", 1);
            private void Set(string name, object value)
                => typeof(DuelPrototypeController).GetField(name, PrivateInstance).SetValue(Controller, value);
            public void Dispose()
            {
                Set("presentationSettings", originalSettings);
                arenaSettings.SetValue(Controller.ArenaView, originalArenaSettings);
                Set("session", originalSession);
                Controller.RestartMatch();
                Controller.enabled = originallyEnabled;
                Object.Destroy(settings);
            }
        }
    }
}
