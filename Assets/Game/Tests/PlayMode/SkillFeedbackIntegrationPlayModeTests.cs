using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class SkillFeedbackIntegrationPlayModeTests : InputTestFixture
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator HeldSkillInspection_ClosesOnKeyReleaseWithoutQueuingTheInspectedSkill()
        {
            yield return null;
            using (var scope = new ControllerScope())
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                Press(keyboard.qKey);
                yield return null;
                scope.HoldReady(0);
                scope.Advance(keyboard);
                Assert.That(scope.Explanation.gameObject.activeInHierarchy, Is.True);
                Assert.That(scope.PlayerName.text, Is.EqualTo("Draw Test"));
                Assert.That(scope.Controller.Session.PlayerQueue, Is.Empty);
                Assert.That(scope.Feedback(false, 0).ConditionTarget, Is.True);
                Assert.That(scope.Feedback(false, 0).ConditionReady, Is.True);
                Assert.That(scope.Feedback(false, 1).ConditionTarget, Is.False);

                Release(keyboard.qKey);
                yield return null;
                scope.Advance(keyboard);
                Assert.That(scope.Explanation.gameObject.activeInHierarchy, Is.False,
                    "Releasing the inspected key must close its explanation and condition preview.");
                Assert.That(scope.Controller.Session.PlayerQueue, Is.Empty,
                    "A held inspection must not turn into a short-tap skill reservation.");
                Assert.That(scope.Controller.Session.Act, Is.EqualTo(3));
                Assert.That(scope.Feedback(false, 0).ConditionTarget, Is.False);
                Assert.That(scope.Feedback(false, 0).ConditionReady, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator ReleasingAnotherHeldKey_DoesNotCloseTheCurrentlyInspectedSkill()
        {
            yield return null;
            using (var scope = new ControllerScope())
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                Press(keyboard.qKey);
                yield return null;
                scope.HoldReady(0);
                scope.Advance(keyboard);
                Press(keyboard.wKey);
                yield return null;
                scope.HoldReady(1);
                scope.Advance(keyboard);
                Assert.That(scope.PlayerName.text, Is.EqualTo("Guard Test"));
                Assert.That(scope.Feedback(false, 0).ConditionTarget, Is.False);
                Assert.That(scope.Feedback(false, 1).ConditionTarget, Is.True);
                Assert.That(scope.Feedback(false, 1).ConditionReady, Is.False,
                    "A matching enemy in a later slot is a candidate, not the next reservation's opponent.");

                Release(keyboard.qKey);
                yield return null;
                scope.Advance(keyboard);
                Assert.That(scope.Explanation.gameObject.activeInHierarchy, Is.True);
                Assert.That(scope.PlayerName.text, Is.EqualTo("Guard Test"));

                Release(keyboard.wKey);
                yield return null;
                scope.Advance(keyboard);
                Assert.That(scope.Explanation.gameObject.activeInHierarchy, Is.False);
                Assert.That(scope.Controller.Session.PlayerQueue, Is.Empty);
            }
        }

        [UnityTest]
        public IEnumerator ActualController_PassesTheAppliedBuffSnapshotAndClearsParticlesOnRestart()
        {
            yield return null;
            var duel = new LegacyQueuedDuel(1000, 1000, 1000, 1000,
                new[]
                {
                    Skill(10, "Ready Test", 0, LegacySkillKind.Attack, LegacySkillProperty.Hit),
                    Skill(7, "Boosted Guard", 0, LegacySkillKind.Defence, LegacySkillProperty.Defence)
                }, new[] { Skill(900, "Enemy Guard", 0, LegacySkillKind.Defence, LegacySkillProperty.Defence) },
                new[] { 2 });
            using (var scope = new ControllerScope(duel))
            {
                Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(0), Is.True);
                duel.Commit();
                duel.ResolveNextSlot();
                typeof(DuelPrototypeController).GetMethod("BeginSlotAnimation", PrivateInstance)
                    .Invoke(scope.Controller, null);
                scope.Advance(null);
                SkillCardFeedbackGraphic feedback = scope.Feedback(true, 1);
                Assert.That(feedback.PowerBuffPercent, Is.EqualTo(30));
                Assert.That(feedback.HasBuffParticles, Is.True);
                Assert.That(feedback.ActiveSparkCount, Is.EqualTo(SkillCardFeedbackGraphic.MaximumSparks));
                Assert.That(feedback.EffectActivated, Is.False,
                    "The guard has a power buff, but its opposing guard does not trigger the ACT condition.");
                Assert.That(duel.CurrentSlot.HitsResolved, Is.Zero);
                Assert.That(scope.Controller.HitStopTimeRemaining, Is.Zero);
                Assert.That(scope.Controller.ArenaView.HasPendingPush, Is.False);

                typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance)
                    .Invoke(scope.Controller, null);
                Assert.That(feedback.HasBuffParticles, Is.False);
                Assert.That(feedback.ActiveSparkCount, Is.Zero);
                Assert.That(feedback.gameObject.activeSelf, Is.False);
            }
        }

        private static LegacySkill Skill(int id, string name, int lane, LegacySkillKind kind, LegacySkillProperty property)
            => new LegacySkill(id, name, 1, 2, 2, kind, property, 1, lane, string.Empty, iconId: 1);

        private sealed class ControllerScope : IDisposable
        {
            private readonly bool originalEnabled;
            private readonly LegacyQueuedDuel originalSession;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }
            public Transform Explanation { get; }
            public Text PlayerName { get; }

            public ControllerScope(LegacyQueuedDuel duel = null)
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                originalSession = Controller.Session;
                Controller.enabled = false;
                // A fixture duel runs outside the opening arc's missions and their coach.
                Field("mission").SetValue(Controller, null);
                Field("guide").SetValue(Controller, null);
                duel = duel ?? new LegacyQueuedDuel(1000, 1000, 1000, 1000,
                    new[]
                    {
                        Skill(42, "Draw Test", 0, LegacySkillKind.Attack, LegacySkillProperty.Slash),
                        Skill(7, "Guard Test", 1, LegacySkillKind.Defence, LegacySkillProperty.Defence),
                        Skill(101, "Plain Test", 2, LegacySkillKind.Attack, LegacySkillProperty.Slash)
                    },
                    new[]
                    {
                        Skill(900, "Enemy Guard", 0, LegacySkillKind.Defence, LegacySkillProperty.Defence),
                        Skill(901, "Enemy Hit", 0, LegacySkillKind.Attack, LegacySkillProperty.Hit),
                        Skill(902, "Enemy Slash", 0, LegacySkillKind.Attack, LegacySkillProperty.Slash)
                    }, new[] { 3 });
                Field("session").SetValue(Controller, duel);
                typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance).Invoke(Controller, null);
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                    typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance));
                foreach (Transform node in Controller.Hud.Root.GetComponentsInChildren<Transform>(true))
                    if (node.name == "Skill Explain") Explanation = node;
                Assert.That(Explanation, Is.Not.Null);
                foreach (Text text in Explanation.GetComponentsInChildren<Text>(true))
                    if (text.name == "Name") PlayerName = text;
                Assert.That(PlayerName, Is.Not.Null);
            }

            public void HoldReady(int lane) => ((float[])Field("holdTimes").GetValue(Controller))[lane] = 1.1f;
            public void Advance(Keyboard keyboard) => advance(0f, keyboard);
            public SkillCardFeedbackGraphic Feedback(bool player, int slot)
            {
                RectTransform anchor = Controller.Hud.GetQueuedSkillAnchor(player, slot);
                Assert.That(anchor, Is.Not.Null);
                var feedback = anchor.GetComponentInChildren<SkillCardFeedbackGraphic>(true);
                Assert.That(feedback, Is.Not.Null);
                return feedback;
            }
            private static FieldInfo Field(string name) => typeof(DuelPrototypeController).GetField(name, PrivateInstance);

            public void Dispose()
            {
                Field("session").SetValue(Controller, originalSession);
                Controller.RestartMatch();
                Controller.enabled = originalEnabled;
            }
        }
    }
}
