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
    public sealed class CombatTempoPlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator SlowPlayback_MovesImpactWithItsPose_AndWaitsBetweenSkills()
        {
            yield return null;
            using (var scope = new TempoScope(false, 2))
            {
                scope.Settings("{\"animationPlaybackSpeed\":0.5,\"attackInterval\":0.1,\"skillInterval\":0.6,\"hitStopDuration\":0}");
                scope.BeginSlot();
                var controller = scope.Controller;
                var slot = controller.Session.CurrentSlot;
                Assert.That(controller.PresentationSettings, Is.Not.Null);
                Assert.That(controller.ActiveSlotPlaybackSpeed, Is.EqualTo(.5f));
                Assert.That(controller.ActiveSlotDuration, Is.EqualTo(1f / 3f + .01f).Within(.0001f));
                Assert.That(controller.Session.Act, Is.EqualTo(2));
                scope.AdvanceToSlotTime(1f / 6f - .001f);
                Assert.That(slot.HitsResolved, Is.Zero);
                Assert.That(controller.Session.Player.Resistance, Is.EqualTo(1000));
                Assert.That(controller.Session.Enemy.Resistance, Is.EqualTo(1000));
                Assert.That(controller.ArenaView.PlayerRenderer.sprite.name, Does.Match("^slash(?:-[23])?-frame-04$"));
                scope.Advance(.002f);
                Assert.That(slot.HitsResolved, Is.EqualTo(1));
                // Both strike on this hit: the resistance clash keeps each contact frame.
                Assert.That(controller.ArenaView.PlayerRenderer.sprite.name, Does.Match("^slash(?:-[23])?-frame-05$"));
                Assert.That(controller.ArenaView.EnemyRenderer.sprite.name, Does.Match("^enemy-slash(?:-[23])?-frame-05$"));
                scope.Until(() => controller.IsBetweenSlots);
                Assert.That(controller.Session.CurrentSlot, Is.Null);
                Assert.That(controller.Session.LastResolvedSlot, Is.Zero);
                scope.Advance(.599f);
                Assert.That(controller.IsBetweenSlots, Is.True);
                Assert.That(controller.Session.CurrentSlot, Is.Null);
                scope.Advance(.002f);
                Assert.That(controller.IsBetweenSlots, Is.False);
                Assert.That(controller.Session.CurrentSlot.SlotIndex, Is.EqualTo(1));
                Assert.That(controller.Session.Act, Is.EqualTo(2), "Presentation waits must not spend or recover ACT.");
            }
        }

        [UnityTest]
        public IEnumerator MultiHit_UsesPlaybackAndAttackGap_ReturnsToIdle_AndFinishesAfterLastClip()
        {
            yield return null;
            using (var scope = new TempoScope(true, 1))
            {
                scope.Settings("{\"animationPlaybackSpeed\":2,\"attackInterval\":0.12,\"skillInterval\":0.8,\"hitStopDuration\":0}");
                scope.BeginSlot();
                var controller = scope.Controller;
                var slot = controller.Session.CurrentSlot;
                float impact = 1f / 24f;
                float clip = 1f / 12f;
                float cycle = clip + .12f;
                Assert.That(slot.HitCount, Is.EqualTo(3));
                Assert.That(controller.ActiveSlotDuration, Is.EqualTo(clip * 3 + .12f * 2 + .01f).Within(.0001f));
                for (int hit = 0; hit < 3; hit++)
                {
                    float eventTime = impact + cycle * hit;
                    scope.AdvanceToSlotTime(eventTime - .001f);
                    Assert.That(slot.HitsResolved, Is.EqualTo(hit));
                    scope.Advance(.002f);
                    Assert.That(slot.HitsResolved, Is.EqualTo(hit + 1));
                    // The first hit is a mutual clash and the rest land on the enemy's finished
                    // attack, so no incoming hit interrupts the player's own strikes.
                    Assert.That(controller.ArenaView.PlayerRenderer.sprite.name, Does.Match("^slash(?:-[23])?-frame-05$"));
                    if (hit < 2)
                    {
                        scope.AdvanceToSlotTime(clip + cycle * hit + .001f);
                        Assert.That(controller.ArenaView.PlayerRenderer.sprite.name, Does.StartWith("idle-frame-"),
                            "The attack gap is idle time, not a stretched attack pose.");
                        Assert.That(controller.ArenaView.EnemyRenderer.sprite.name, Does.Match("^enemy-poses-block(?:-2)?$"),
                            "The enemy's finished one-hit attack guards while the player keeps striking.");
                    }
                }
                Assert.That(controller.Session.Enemy.Resistance, Is.EqualTo(997));
                Assert.That(controller.Session.Player.Resistance, Is.EqualTo(999));
                scope.AdvanceToSlotTime(controller.ActiveSlotDuration - .001f);
                Assert.That(controller.Session.CurrentSlot, Is.SameAs(slot), "The final hit must finish its animation.");
                scope.Advance(.002f);
                Assert.That(controller.Session.CurrentSlot, Is.Null);
                Assert.That(controller.Session.LastResolvedSlot, Is.Zero);
                Assert.That(controller.IsBetweenSlots, Is.False, "There is no skill gap after the final queue slot.");
            }
        }

        [UnityTest]
        public IEnumerator EditingTempoDuringSlot_AppliesOnlyToTheNextSkill()
        {
            yield return null;
            using (var scope = new TempoScope(false, 2))
            {
                scope.Settings("{\"animationPlaybackSpeed\":0.5,\"attackInterval\":0.3,\"skillInterval\":0,\"hitStopDuration\":0}");
                scope.BeginSlot();
                var controller = scope.Controller;
                var first = controller.Session.CurrentSlot;
                float firstDuration = controller.ActiveSlotDuration;
                scope.Settings("{\"animationPlaybackSpeed\":2,\"attackInterval\":0.01}");
                Assert.That(controller.ActiveSlotPlaybackSpeed, Is.EqualTo(.5f));
                Assert.That(controller.ActiveSlotAttackInterval, Is.EqualTo(.3f));
                Assert.That(controller.ActiveSlotDuration, Is.EqualTo(firstDuration));
                scope.AdvanceToSlotTime(1f / 6f - .001f);
                Assert.That(first.HitsResolved, Is.Zero);
                scope.Advance(.002f);
                Assert.That(first.HitsResolved, Is.EqualTo(1));
                scope.Until(() => controller.Session.CurrentSlot != null && controller.Session.CurrentSlot.SlotIndex == 1);
                Assert.That(controller.ActiveSlotPlaybackSpeed, Is.EqualTo(2f));
                Assert.That(controller.ActiveSlotAttackInterval, Is.EqualTo(.01f));
                Assert.That(controller.ActiveSlotDuration, Is.EqualTo(1f / 12f + .01f).Within(.0001f));
            }
        }

        [UnityTest]
        public IEnumerator HitStop_ConsumesRealTimeExactly_AndSimultaneousHitsDoNotStack()
        {
            yield return null;
            using (var scope = new TempoScope(false, 1))
            {
                scope.Settings("{\"animationPlaybackSpeed\":1,\"attackInterval\":0,\"skillInterval\":0,\"hitStopDuration\":0.04}");
                scope.BeginSlot();
                var controller = scope.Controller;
                float globalScale = Time.timeScale;
                scope.AdvanceToSlotTime(1f / 12f + .0001f);
                Assert.That(controller.Session.CurrentSlot.HitsResolved, Is.EqualTo(1));
                Assert.That(controller.HitStopTimeRemaining, Is.EqualTo(.04f).Within(.0001f));
                Assert.That(controller.Session.Player.Resistance, Is.EqualTo(999));
                Assert.That(controller.Session.Enemy.Resistance, Is.EqualTo(999));
                float elapsed = controller.ActiveSlotElapsedTime;
                Vector3 playerPosition = controller.ArenaView.PlayerRenderer.transform.localPosition;
                Vector3 enemyPosition = controller.ArenaView.EnemyRenderer.transform.localPosition;
                scope.Advance(.025f);
                Assert.That(controller.HitStopTimeRemaining, Is.EqualTo(.015f).Within(.0001f));
                Assert.That(controller.ActiveSlotElapsedTime, Is.EqualTo(elapsed).Within(.0001f));
                Assert.That(controller.ArenaView.PlayerRenderer.transform.localPosition, Is.EqualTo(playerPosition));
                Assert.That(controller.ArenaView.EnemyRenderer.transform.localPosition, Is.EqualTo(enemyPosition));
                scope.Advance(.02f);
                Assert.That(controller.HitStopTimeRemaining, Is.Zero);
                Assert.That(controller.ActiveSlotElapsedTime, Is.EqualTo(elapsed + .005f).Within(.0001f),
                    "Only the unused remainder of the real frame advances the combat clock.");
                Assert.That(controller.ArenaView.EnemyRenderer.transform.localPosition.x, Is.GreaterThan(enemyPosition.x));
                Assert.That(Time.timeScale, Is.EqualTo(globalScale));
            }
        }

        [UnityTest]
        public IEnumerator Restart_ClearsHitStopAndSkillGap_AndRestoresPlanningImmediately()
        {
            yield return null;
            using (var scope = new TempoScope(false, 2))
            {
                scope.Settings("{\"animationPlaybackSpeed\":1,\"attackInterval\":0.1,\"skillInterval\":0.8,\"hitStopDuration\":0.04}");
                scope.BeginSlot();
                var controller = scope.Controller;
                scope.AdvanceToSlotTime(1f / 12f + .0001f);
                Assert.That(controller.HitStopTimeRemaining, Is.GreaterThan(0));
                controller.RestartMatch();
                AssertPlanningReset(controller);
                scope.Advance(.1f);
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(9.9f).Within(.0001f));
                scope.BeginSlot();
                scope.Until(() => controller.IsBetweenSlots);
                controller.RestartMatch();
                AssertPlanningReset(controller);
                scope.Advance(.1f);
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(9.9f).Within(.0001f));
            }
        }

        [UnityTest]
        public IEnumerator LongFrame_DoesNotBatchMultiHitsOrSkipTheFinalPose()
        {
            yield return null;
            using (var scope = new TempoScope(true, 1))
            {
                scope.Settings("{\"animationPlaybackSpeed\":2,\"attackInterval\":0,\"skillInterval\":0,\"hitStopDuration\":0}");
                scope.BeginSlot();
                var controller = scope.Controller;
                var slot = controller.Session.CurrentSlot;
                scope.Advance(1f);
                Assert.That(slot.HitsResolved, Is.EqualTo(1));
                Assert.That(controller.Session.CurrentSlot, Is.SameAs(slot));
                Assert.That(controller.ActiveSlotElapsedTime, Is.EqualTo(1f / 24f).Within(.0001f));
                Assert.That(controller.ArenaView.PlayerRenderer.sprite.name, Does.Match("^slash(?:-[23])?-frame-05$"));
                Assert.That(controller.ArenaView.EnemyRenderer.sprite.name, Does.Match("^enemy-slash(?:-[23])?-frame-05$"));
                scope.Advance(0f);
                Assert.That(slot.HitsResolved, Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator ShortFrameOvershootIntoGap_HoldsTheActualImpactPose()
        {
            yield return null;
            using (var scope = new TempoScope(true, 1))
            {
                scope.Settings("{\"animationPlaybackSpeed\":2,\"attackInterval\":0.12,\"skillInterval\":0,\"hitStopDuration\":0}");
                scope.BeginSlot();
                var controller = scope.Controller;
                var slot = controller.Session.CurrentSlot;
                // The frame crosses the .0833 clip end, but not the .245 next impact.
                scope.Advance(.1f);
                Assert.That(slot.HitsResolved, Is.EqualTo(1));
                Assert.That(controller.Session.Enemy.Resistance, Is.EqualTo(999));
                Assert.That(controller.ActiveSlotElapsedTime, Is.EqualTo(1f / 24f).Within(.0001f));
                Assert.That(controller.ArenaView.PlayerRenderer.sprite.name, Does.Match("^slash(?:-[23])?-frame-05$"));
                Assert.That(controller.ArenaView.EnemyRenderer.sprite.name, Does.Match("^enemy-slash(?:-[23])?-frame-05$"));
                scope.Advance(0f);
                Assert.That(slot.HitsResolved, Is.EqualTo(1));
            }
        }

        private static void AssertPlanningReset(DuelPrototypeController controller)
        {
            Assert.That(controller.CanChoose, Is.True);
            Assert.That(controller.IsBetweenSlots, Is.False);
            Assert.That(controller.HitStopTimeRemaining, Is.Zero);
            Assert.That(controller.ActiveSlotDuration, Is.Zero);
            Assert.That(controller.Session.CurrentSlot, Is.Null);
            Assert.That(controller.Session.PlayerQueue.Count, Is.Zero);
            Assert.That(controller.Session.Act, Is.EqualTo(3));
            // RestartMatch now starts a fresh campaign, not the injected tempo fixture.
            Assert.That(controller.Session.Player.Health, Is.EqualTo(100));
            Assert.That(controller.Session.Enemy.Health, Is.EqualTo(80));
            Assert.That(controller.Campaign.StageNumber, Is.EqualTo(1));
            Assert.That(controller.Campaign.Currency, Is.Zero);
            Assert.That(controller.TurnTimeRemaining, Is.EqualTo(10f));
            Assert.That(controller.ArenaView.PlayerRenderer.sprite.name, Does.StartWith("idle-frame-"));
        }

        /// <summary>Run the real presentation clock deterministically, without changing the shared settings asset.</summary>
        private sealed class TempoScope : IDisposable
        {
            private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            private readonly DuelPresentationSettings originalSettings;
            private readonly DuelPresentationSettings settingsClone;
            private readonly LegacyQueuedDuel originalSession;
            private readonly bool originalEnabled;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }

            public TempoScope(bool multiHit, int enemyActionCount)
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                Controller.enabled = false;
                originalSettings = Controller.PresentationSettings;
                originalSession = Controller.Session;
                settingsClone = Object.Instantiate(originalSettings);
                // These regressions isolate the existing animation/impact clock;
                // StepInputPlayModeTests covers the new, separate anticipation phase.
                JsonUtility.FromJsonOverwrite("{\"stepAnticipationDuration\":0}", settingsClone);
                SetField("presentationSettings", settingsClone);
                var playerSkills = new[]
                {
                    WeakSlash(1, 0, multiHit ? 3 : 1), WeakSlash(3, 1, 1), WeakSlash(5, 2, 1)
                };
                SetField("session", new LegacyQueuedDuel(1000, 1000, 1000, 1000,
                    playerSkills, new[] { WeakSlash(1, 0, 1) }, new[] { enemyActionCount }, 1));
                var method = typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance);
                Assert.That(method, Is.Not.Null);
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller, method);
                typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance)
                    .Invoke(Controller, null);
            }

            public void Settings(string json) => JsonUtility.FromJsonOverwrite(json, settingsClone);
            public void Advance(float realDelta) => advance(realDelta, null);

            public void BeginSlot()
            {
                Assert.That(Controller.QueueLane(0), Is.True);
                Controller.CommitTurn();
                Until(() => Controller.Session.CurrentSlot != null);
                Assert.That(Controller.ActiveSlotElapsedTime, Is.Zero);
            }

            public void AdvanceToSlotTime(float time)
            {
                int steps = 0;
                while (Controller.Session.CurrentSlot != null && Controller.ActiveSlotElapsedTime < time && steps++ < 5000)
                    Advance(Mathf.Min(.001f, time - Controller.ActiveSlotElapsedTime));
                Assert.That(Controller.Session.CurrentSlot, Is.Not.Null);
                Assert.That(Controller.ActiveSlotElapsedTime, Is.EqualTo(time).Within(.0001f));
            }

            public void Until(Func<bool> condition)
            {
                int steps = 0;
                while (!condition() && steps++ < 5000) Advance(.001f);
                Assert.That(condition(), Is.True, "The presentation state must make progress within five real seconds.");
            }

            private static LegacySkill WeakSlash(int id, int lane, int count) =>
                new LegacySkill(id, "Tempo Test", 1, 1, 1, LegacySkillKind.Attack,
                    LegacySkillProperty.Slash, count, lane, string.Empty, "Slash");

            private void SetField(string name, object value)
            {
                var field = typeof(DuelPrototypeController).GetField(name, PrivateInstance);
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
