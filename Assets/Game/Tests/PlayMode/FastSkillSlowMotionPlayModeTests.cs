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
    public sealed class FastSkillSlowMotionPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float Slow = .4f;

        [TestCase(1, 2)]
        [TestCase(2, 1)]
        [TestCase(1, 1)]
        [TestCase(1, 4)]
        [TestCase(4, 1)]
        public void EitherSidesFastTechnique_RequestsFortyPercentCombatSpeed(int playerDuration, int enemyDuration)
        {
            LegacyCurrentSlot slot = Begin(new[] { Attack(98001, playerDuration) }, new[] { Attack(98002, enemyDuration) });
            Assert.That(Speed(slot, true), Is.EqualTo(Slow));
            Assert.That((float)Helper.GetField("Scale", BindingFlags.Public | BindingFlags.Static).GetRawConstantValue(), Is.EqualTo(Slow));
        }

        [TestCase(2, 2)]
        [TestCase(4, 2)]
        [TestCase(2, 4)]
        [TestCase(4, 4)]
        public void OrdinaryAndChargeTechniques_DoNotInventFastSkillSlowMotion(int playerDuration, int enemyDuration)
        {
            LegacyCurrentSlot slot = Begin(new[] { Attack(98001, playerDuration) }, new[] { Attack(98002, enemyDuration) });
            Assert.That(Speed(slot, true), Is.EqualTo(1f));
        }

        [Test]
        public void NullAndNonPlayingPhases_AreNormalSpeed_EvenWhenTheSlotContainsAFastTechnique()
        {
            LegacyCurrentSlot slot = Begin(new[] { Attack(98001, 1) }, new[] { Attack(98002, 2) });
            Assert.That(Speed(null, true), Is.EqualTo(1f));
            Assert.That(Speed(null, false), Is.EqualTo(1f));
            Assert.That(Speed(slot, false), Is.EqualTo(1f), "Planning, anticipation, approach and inter-skill travel are not slowed.");
        }

        [Test]
        public void ActualFastSkill_IsRecognizedWhenNoActionMetadataIsAvailable()
        {
            LegacyCurrentSlot slot = Begin(new[] { Attack(98001, 1) }, new[] { Attack(98002, 2) });
            typeof(LegacyCurrentSlot).GetProperty("PlayerActionSkill").SetValue(slot, null);
            Assert.That(slot.PlayerSkill.IsFast, Is.True);
            Assert.That(Speed(slot, true), Is.EqualTo(Slow));
        }

        [Test]
        public void PaddingAndTheNextOrdinaryTechnique_ImmediatelyReleaseTheFastSlowMotion()
        {
            var duel = Duel(new[] { Attack(98001, 1), Attack(98002, 2) }, new[] { Attack(98003, 4) });
            duel.Commit();
            LegacyCurrentSlot fast = duel.BeginNextSlot();
            Assert.That(Speed(fast, true), Is.EqualTo(Slow));
            Finish(duel);
            LegacyCurrentSlot padding = duel.BeginNextSlot();
            Assert.That(padding.StartHalfTick, Is.EqualTo(1));
            Assert.That(padding.PlayerActionSkill, Is.Null);
            Assert.That(Speed(padding, true), Is.EqualTo(1f));
            Finish(duel);
            LegacyCurrentSlot ordinary = duel.BeginNextSlot();
            Assert.That(ordinary.PlayerActionSkill.IsFast, Is.False);
            Assert.That(Speed(ordinary, true), Is.EqualTo(1f));
        }

        [TestCase(1.8f, .4f)]
        [TestCase(1f, .4f)]
        [TestCase(.4f, .4f)]
        [TestCase(.12f, .12f)]
        [TestCase(.08f, .08f)]
        [TestCase(.06f, .06f)]
        public void FastSlowMotion_LimitsProgressiveSpeed_WithoutOverridingDeeperCinematicSlow(float existing, float expected)
        {
            LegacyCurrentSlot slot = Begin(new[] { Attack(98001, 1) }, new[] { Attack(98002, 2) });
            Assert.That(Limit(existing, slot, true), Is.EqualTo(expected).Within(.000001f));
            Assert.That(Limit(existing, slot, false), Is.EqualTo(existing).Within(.000001f),
                "Leaving a fast playing interval must preserve the existing presentation speed.");
        }

        [Test]
        public void ReadingTheSlowMotionPolicy_DoesNotAdvanceCombatOrConsumeAnything()
        {
            var duel = Duel(new[] { Attack(98001, 1), Attack(98002, 1) }, new[] { Attack(98003, 2) });
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            int[] before = State(duel);
            for (int i = 0; i < 20; i++)
            {
                Assert.That(Speed(slot, true), Is.EqualTo(Slow));
                Assert.That(Limit(1.8f, slot, true), Is.EqualTo(Slow));
            }
            CollectionAssert.AreEqual(before, State(duel));
            Assert.That(slot.HitsResolved, Is.Zero);
        }

        [UnityTest]
        public IEnumerator RealControllerClock_SlowsBothFastIntervals_ThenRestoresTheOrdinaryClock()
        {
            yield return null;
            using (var scope = new ControllerScope())
            {
                DuelPrototypeController controller = scope.Controller;
                LegacyQueuedDuel duel = controller.Session;
                Assert.That(FastSpeed(controller), Is.EqualTo(1f));
                controller.CommitTurn();
                scope.Until(() => duel.CurrentSlot?.SlotIndex == 1 && FastSpeed(controller) < 1f);
                AssertFastClock(scope);
                scope.Until(() => duel.CurrentSlot?.SlotIndex == 2 && FastSpeed(controller) < 1f);
                AssertFastClock(scope);
                scope.Until(() => duel.CurrentSlot?.SlotIndex == 3);
                Assert.That(FastSpeed(controller), Is.EqualTo(1f));
                int[] before = State(duel);
                float elapsed = controller.ActiveSlotElapsedTime;
                float ordinarySpeed = controller.ResolutionPlaybackSpeed;
                scope.Advance(.001f);
                Assert.That(controller.ActiveSlotElapsedTime - elapsed,
                    Is.EqualTo(.001f * ordinarySpeed).Within(.00001f), "Ordinary play resumes its existing progressive tempo.");
                CollectionAssert.AreEqual(before, State(duel));
                controller.RestartMatch();
                Assert.That(FastSpeed(controller), Is.EqualTo(1f));
            }
        }

        private static void AssertFastClock(ControllerScope scope)
        {
            DuelPrototypeController controller = scope.Controller;
            LegacyQueuedDuel duel = controller.Session;
            int[] before = State(duel);
            float global = Time.timeScale;
            float elapsed = controller.ActiveSlotElapsedTime;
            Assert.That(FastSpeed(controller), Is.EqualTo(Slow));
            scope.Advance(.001f);
            Assert.That(controller.ActiveSlotElapsedTime - elapsed, Is.EqualTo(.001f * Slow).Within(.00001f),
                "The actual combat clock must slow, even when progressive resolution wants a faster speed.");
            Assert.That(Time.timeScale, Is.EqualTo(global), "Fast slow motion belongs to this battle, not Unity's global clock.");
            CollectionAssert.AreEqual(before, State(duel), "Sampling a pre-impact slow frame must not resolve an extra hit.");
        }

        private static Type Helper
        {
            get
            {
                Type helper = typeof(DuelPrototypeController).Assembly.GetType("TurnLimbo.Presentation.DuelFastSkillSlowMotion");
                Assert.That(helper, Is.Not.Null, "Fast skills must participate in the combat-clock slow-motion policy.");
                return helper;
            }
        }

        private static float Speed(LegacyCurrentSlot slot, bool playing)
            => (float)Helper.GetMethod("Speed", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { slot, playing });
        private static float Limit(float existing, LegacyCurrentSlot slot, bool playing)
            => (float)Helper.GetMethod("Limit", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { existing, slot, playing });
        private static float FastSpeed(DuelPrototypeController controller)
        {
            PropertyInfo property = typeof(DuelPrototypeController).GetProperty("FastSkillPresentationSpeed");
            Assert.That(property, Is.Not.Null);
            return (float)property.GetValue(controller);
        }

        private static LegacyCurrentSlot Begin(LegacySkill[] player, LegacySkill[] enemy)
        {
            LegacyQueuedDuel duel = Duel(player, enemy);
            duel.Commit();
            return duel.BeginNextSlot();
        }
        private static LegacyQueuedDuel Duel(LegacySkill[] player, LegacySkill[] enemy)
        {
            var duel = new LegacyQueuedDuel(10000, 10000, 10000, 10000, player, enemy, new[] { enemy.Length }, 7,
                features: CombatFeature.All & ~CombatFeature.Dodge & ~CombatFeature.Pressure, meshPercent: 0);
            Assert.That(duel.TrySetFixedQueues(player, enemy), Is.True);
            return duel;
        }
        private static LegacySkill Attack(int id, int duration)
            => new LegacySkill(id, "고속 슬로 검증", 0, 3, 3, LegacySkillKind.Attack, LegacySkillProperty.Slash,
                1, 0, string.Empty, durationHalfUnits: duration);
        private static void Finish(LegacyQueuedDuel duel)
        {
            while (!duel.CurrentSlot.IsResolved) duel.ResolveNextHit();
            duel.CompleteCurrentSlot();
        }
        private static int[] State(LegacyQueuedDuel duel)
            => new[] { duel.Player.Health, duel.Player.Resistance, duel.Enemy.Health, duel.Enemy.Resistance,
                duel.Act, duel.NextActGain, duel.PlayerQueue.Count, duel.EnemyQueue.Count,
                duel.CurrentSlot?.HitsResolved ?? -1, duel.PlayerCountersRemaining, duel.EnemyCountersRemaining };

        private sealed class ControllerScope : IDisposable
        {
            private readonly DuelPresentationSettings originalSettings, originalArenaSettings, settings;
            private readonly LegacyQueuedDuel originalSession;
            private readonly bool originalEnabled;
            private readonly FieldInfo arenaSettings;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }

            public ControllerScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                Controller.enabled = false;
                originalSettings = Controller.PresentationSettings;
                originalSession = Controller.Session;
                arenaSettings = typeof(LegacyArenaView).GetField("settings", Private);
                originalArenaSettings = (DuelPresentationSettings)arenaSettings.GetValue(Controller.ArenaView);
                settings = Object.Instantiate(originalSettings);
                try
                {
                    JsonUtility.FromJsonOverwrite("{\"animationPlaybackSpeed\":1,\"attackInterval\":0,\"skillInterval\":0," +
                        "\"hitStopDuration\":0,\"stepAnticipationDuration\":0,\"showDirectorEnabled\":true," +
                        "\"minorSlotSpeed\":1,\"majorHitStopScale\":1,\"bladeBindSeconds\":0," +
                        "\"combatTempoEnabled\":true,\"combatTempoStep\":0.1,\"combatTempoMaxSpeed\":1.8," +
                        "\"exhaustedSideSpeed\":2,\"unopposedStreakEnabled\":false,\"bladeExchangeEnabled\":false}", settings);
                    Set("presentationSettings", settings);
                    arenaSettings.SetValue(Controller.ArenaView, settings);
                    Set("mission", null);
                    Set("guide", null);
                    LegacySkill[] player = { Attack(98001, 2), Attack(98002, 1), Attack(98003, 1), Attack(98004, 2) };
                    LegacySkill[] enemy = { Attack(98011, 2), Attack(98012, 2), Attack(98013, 2) };
                    LegacyQueuedDuel duel = Duel(player, enemy);
                    Set("session", duel);
                    advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                        typeof(DuelPrototypeController).GetMethod("AdvancePresentation", Private));
                    typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", Private).Invoke(Controller, new object[] { null, null });
                    Controller.SkipStartCard();
                    Assert.That(duel.TrySetFixedQueues(player, enemy), Is.True,
                        "ResetBattlePresentation clears reservations; install the mixed queue afterwards.");
                    Advance(0f);
                }
                catch { Dispose(); throw; }
            }
            public void Advance(float realDelta) => advance(realDelta, null);
            public void Until(Func<bool> condition)
            {
                for (int i = 0; i < 20000 && !condition(); i++) Advance(.001f);
                Assert.That(condition(), Is.True, "The battle must advance through the requested timing interval.");
            }
            private void Set(string field, object value) => typeof(DuelPrototypeController).GetField(field, Private).SetValue(Controller, value);
            public void Dispose()
            {
                Set("presentationSettings", originalSettings);
                arenaSettings.SetValue(Controller.ArenaView, originalArenaSettings);
                Set("session", originalSession);
                Controller.RestartMatch();
                Controller.enabled = originalEnabled;
                Object.Destroy(settings);
            }
        }
    }
}
