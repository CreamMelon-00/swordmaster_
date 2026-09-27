using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class StepFocusIntegrationPlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator SuccessfulPressureInput_SlowsTheActualSlotClock_WithoutChangingGlobalTime()
        {
            yield return null;
            float globalScale = Time.timeScale;
            float controlAdvance;
            using (var scope = new FocusScope())
            {
                scope.BeginPlayingSlot();
                float before = scope.Controller.ActiveSlotElapsedTime;
                scope.Advance(.05f);
                controlAdvance = scope.Controller.ActiveSlotElapsedTime - before;
                Assert.That(controlAdvance, Is.EqualTo(.05f).Within(.0001f));
            }

            using (var scope = new FocusScope())
            {
                var controller = scope.Controller;
                scope.BeginPlayingSlot();
                Assert.That(controller.IsStepTimingWindow, Is.True);
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                Press(keyboard.dKey);
                yield return null;
                float before = controller.ActiveSlotElapsedTime;
                scope.Advance(.05f, keyboard);
                Release(keyboard.dKey);

                Assert.That(controller.Session.CurrentSlot.PressureSucceeded, Is.True);
                Assert.That(controller.Session.CurrentSlot.HitsResolved, Is.Zero);
                float speed = controller.ArenaView.StepPresentationSpeed;
                Assert.That(speed, Is.GreaterThan(0f).And.LessThanOrEqualTo(.25f));
                Assert.That(controller.ActiveSlotElapsedTime - before,
                    Is.EqualTo(.05f * speed).Within(.0001f));
                Assert.That(controller.ActiveSlotElapsedTime - before, Is.LessThan(controlAdvance));
                Assert.That(controller.ArenaView.StepFocusAmount, Is.GreaterThan(0f));
                Assert.That(Time.timeScale, Is.EqualTo(globalScale),
                    "Step focus belongs to the duel's custom presentation clock, not global Unity time.");
            }
            Assert.That(Time.timeScale, Is.EqualTo(globalScale));
        }

        [UnityTest]
        public IEnumerator SuccessfulStepAndHeldShift_UseTheSlowerSpeed_WithoutMultiplyingIt()
        {
            yield return null;
            using (var scope = new FocusScope())
            {
                var controller = scope.Controller;
                scope.BeginPlayingSlot();
                Assert.That(controller.TryStep(LegacyStepAction.Pressure, out bool success), Is.True);
                Assert.That(success, Is.True);
                float stepSpeed = controller.ArenaView.StepPresentationSpeed;
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                Press(keyboard.leftShiftKey);
                yield return null;
                float globalScale = Time.timeScale;
                float before = controller.ActiveSlotElapsedTime;
                scope.Advance(.05f, keyboard);
                Release(keyboard.leftShiftKey);
                Assert.That(controller.ActiveSlotElapsedTime - before,
                    Is.EqualTo(.05f * Mathf.Min(.4f, stepSpeed)).Within(.0001f),
                    "Held Shift must not multiply an already successful step's cinematic slow motion.");
                Assert.That(Time.timeScale, Is.EqualTo(globalScale));
            }
        }

        [UnityTest]
        public IEnumerator RepeatingTheSameSuccessfulAction_DoesNotRenewExpiredFocus_ButStillMovesAndPaysACTPenalty()
        {
            yield return null;
            using (var scope = new FocusScope())
            {
                var controller = scope.Controller;
                scope.BeginPlayingSlot();
                LegacyCurrentSlot slot = controller.Session.CurrentSlot;
                Assert.That(controller.TryStep(LegacyStepAction.Pressure, out bool first), Is.True);
                Assert.That(first, Is.True);
                Assert.That(controller.ArenaView.StepPresentationSpeed, Is.LessThan(1f));

                // Expire only real-time presentation feedback, leaving this slot
                // before its first impact. This isolates repeated-input behavior
                // from the later damage/range state of a different skill.
                controller.ArenaView.Tick(0f, 10f);
                Assert.That(controller.ArenaView.StepPresentationSpeed, Is.EqualTo(1f));
                Assert.That(controller.ArenaView.StepFocusAmount, Is.Zero);
                Assert.That(controller.IsStepTimingWindow, Is.True);
                Assert.That(slot.HitsResolved, Is.Zero);
                Assert.That(slot.PressureSucceeded, Is.True);
                Assert.That(controller.TryStep(LegacyStepAction.Pressure, out bool repeated), Is.True);
                Assert.That(repeated, Is.False);
                Assert.That(controller.ArenaView.StepPresentationSpeed, Is.EqualTo(1f),
                    "An already successful action cannot keep renewing slow motion by spamming its key.");
                Assert.That(controller.ArenaView.StepFocusAmount, Is.InRange(0f, .25f),
                    "An accepted repeat may give a weak movement cue, but must not restore full successful focus.");
                Assert.That(controller.ArenaView.IsStepping, Is.True,
                    "A repeated accepted attempt still performs its movement gesture.");
                Assert.That(controller.Session.UsedStepThisTurn, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator RestartAndReturnToLobby_ClearCinematicStepState()
        {
            yield return null;
            using (var scope = new FocusScope())
            {
                var controller = scope.Controller;
                scope.BeginPlayingSlot();
                Assert.That(controller.TryStep(LegacyStepAction.Pressure, out bool success), Is.True);
                Assert.That(success, Is.True);
                Assert.That(controller.ArenaView.StepFocusAmount, Is.GreaterThan(0f));
                controller.RestartMatch();
                AssertCleared(controller);
                Assert.That(controller.CanChoose, Is.True);
                Assert.That(controller.Session.UsedStepThisTurn, Is.False);
            }
            using (var scope = new FocusScope())
            {
                var controller = scope.Controller;
                scope.BeginPlayingSlot();
                Assert.That(controller.TryStep(LegacyStepAction.Dodge, out bool success), Is.True);
                Assert.That(success, Is.True);
                controller.ReturnToLobby();
                Assert.That(controller.IsInLobby, Is.True);
                AssertCleared(controller);
                Assert.That(controller.CanStep, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator DodgeAndPressurePressedTogether_CanBothSucceedBeforeTheSameImpact()
        {
            yield return null;
            using (var scope = new FocusScope())
            {
                var controller = scope.Controller;
                scope.BeginPlayingSlot();
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                // One state event represents both keys in the same player-loop
                // update. An additional manual update before yielding would
                // consume wasPressedThisFrame before the controller observes it.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A, Key.D));
                yield return null;
                Assert.That(keyboard.aKey.wasPressedThisFrame, Is.True);
                Assert.That(keyboard.dKey.wasPressedThisFrame, Is.True);
                float globalScale = Time.timeScale;
                scope.Advance(0f, keyboard);
                LegacyCurrentSlot slot = controller.Session.CurrentSlot;
                Assert.That(slot.DodgeSucceeded, Is.True);
                Assert.That(slot.PressureSucceeded, Is.True,
                    "The first accepted step must not suppress the other key's same-frame timing judgment.");
                Assert.That(slot.HitsResolved, Is.Zero);
                Assert.That(controller.Session.UsedStepThisTurn, Is.True);
                Assert.That(controller.ArenaView.StepPresentationSpeed, Is.LessThanOrEqualTo(.25f));
                Assert.That(controller.ArenaView.StepFocusAmount, Is.GreaterThan(0f));
                Assert.That(Time.timeScale, Is.EqualTo(globalScale));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
            }
        }

        [UnityTest]
        public IEnumerator EarlyMiss_StillMovesAndCostsNaturalRecovery_ButDoesNotStartSuccessfulSlowMotion()
        {
            yield return null;
            using (var scope = new FocusScope())
            {
                var controller = scope.Controller;
                scope.BeginWindup();
                Assert.That(controller.IsStepTimingWindow, Is.False);
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                Press(keyboard.aKey);
                yield return null;
                float globalScale = Time.timeScale;
                scope.Advance(0f, keyboard);
                Release(keyboard.aKey);
                Assert.That(controller.Session.CurrentSlot.DodgeSucceeded, Is.False);
                Assert.That(controller.ArenaView.IsStepping, Is.True);
                Assert.That(controller.Session.UsedStepThisTurn, Is.True);
                Assert.That(controller.ArenaView.StepPresentationSpeed, Is.EqualTo(1f));
                Assert.That(Time.timeScale, Is.EqualTo(globalScale));
            }
        }

        private static void AssertCleared(DuelPrototypeController controller)
        {
            Assert.That(controller.ArenaView.StepPresentationSpeed, Is.EqualTo(1f));
            Assert.That(controller.ArenaView.StepFocusAmount, Is.Zero);
            Assert.That(controller.ArenaView.IsStepping, Is.False);
            Assert.That(controller.ArenaView.ActiveStepAfterimageCount, Is.Zero);
        }

        /// <summary>Use the existing bootstrap/controller with an isolated deterministic duel and clock.</summary>
        private sealed class FocusScope : IDisposable
        {
            private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            private readonly DuelPresentationSettings originalSettings, settingsClone;
            private readonly LegacyQueuedDuel originalSession;
            private readonly bool originalEnabled;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }

            public FocusScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                Controller.enabled = false;
                originalSettings = Controller.PresentationSettings;
                originalSession = Controller.Session;
                settingsClone = Object.Instantiate(originalSettings);
                JsonUtility.FromJsonOverwrite("{\"animationPlaybackSpeed\":1,\"attackInterval\":0.1," +
                    "\"skillInterval\":0.28,\"hitStopDuration\":0,\"stepAnticipationDuration\":0.24,\"stepTimingWindow\":0.1}", settingsClone);
                SetField("presentationSettings", settingsClone);
                SetField("session", new LegacyQueuedDuel(1000, 1000, 1000, 1000,
                    new[] { WeakSkill(101, 0), WeakSkill(103, 1), WeakSkill(105, 2) },
                    new[] { WeakSkill(201, 0) }, new[] { 1 }, 1));
                MethodInfo method = typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance);
                Assert.That(method, Is.Not.Null);
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller, method);
                typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance).Invoke(Controller, null);
            }

            public void Advance(float realDelta, Keyboard keyboard = null) => advance(realDelta, keyboard);

            public void BeginWindup()
            {
                Assert.That(Controller.QueueLane(0), Is.True);
                Controller.CommitTurn();
                Until(() => Controller.Session.CurrentSlot != null);
                Assert.That(Controller.IsSkillWindup, Is.True);
            }

            public void BeginPlayingSlot()
            {
                BeginWindup();
                Until(() => !Controller.IsSkillWindup);
                Assert.That(Controller.ActiveSlotElapsedTime, Is.Zero);
                Assert.That(Controller.Session.CurrentSlot.HitsResolved, Is.Zero);
                Assert.That(Controller.IsStepTimingWindow, Is.True);
            }

            private void Until(Func<bool> condition)
            {
                int frames = 0;
                while (!condition() && frames++ < 10000) Advance(.001f);
                Assert.That(condition(), Is.True, "The controller must progress within ten simulated seconds.");
            }

            private static LegacySkill WeakSkill(int id, int lane)
                => new LegacySkill(id, "Step Focus Test", 1, 3, 3, LegacySkillKind.Attack,
                    LegacySkillProperty.Slash, 3, lane, string.Empty, "Slash", 1);

            private void SetField(string name, object value)
            {
                FieldInfo field = typeof(DuelPrototypeController).GetField(name, PrivateInstance);
                Assert.That(field, Is.Not.Null);
                field.SetValue(Controller, value);
            }

            public void Dispose()
            {
                SetField("presentationSettings", originalSettings);
                SetField("session", originalSession);
                Controller.RestartMatch();
                Controller.enabled = originalEnabled;
                Object.Destroy(settingsClone);
            }
        }
    }
}
