using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Presentation;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class FastSkillRenderedPoseTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase(true)]
        [TestCase(false)]
        public void OnlyTheNonFastOpponent_KeepsItsSlowPoseAcrossTheFastPair(bool playerFast)
        {
            LegacySkill fast = Attack(98701, 1), slow = Attack(98702, 2);
            var duel = Duel(playerFast ? new[] { fast, fast } : new[] { slow },
                playerFast ? new[] { slow } : new[] { fast, fast });
            duel.Commit();
            LegacyCurrentSlot first = duel.BeginNextSlot();
            Assert.That(Keep(first, !playerFast, slow, false), Is.True);
            Assert.That(Keep(first, playerFast, fast, true), Is.False,
                "A fast actor must never inherit the opponent's slow-pose policy.");
            Finish(duel);
            LegacyCurrentSlot second = duel.BeginNextSlot();
            Assert.That(Keep(second, !playerFast, slow, true), Is.True);
            Assert.That(Keep(second, playerFast, fast, true), Is.False);
            Assert.That(Phase(slow, .5f, 1f, true), Is.EqualTo(.5f));
            Assert.That(Phase(fast, .5f, 1f, false), Is.EqualTo(.5f),
                "Both actual contact cels remain anchored to the existing shared impact.");
        }

        [Test]
        public void NullWaitAndFastSkills_DoNotKeepAnOldSlowPose()
        {
            LegacySkill fast = Attack(98701, 1), slow = Attack(98702, 2);
            LegacyCurrentSlot slot = Begin(new[] { fast }, new[] { slow });
            Assert.That(Keep(null, false, slow, true), Is.False);
            Assert.That(Keep(slot, false, null, true), Is.False);
            Assert.That(Keep(slot, false, LegacyCommonActions.Breathe, true), Is.False);
            Assert.That(Keep(slot, true, fast, true), Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ASlowAction_CarriesItsPoseThroughPadding_ButTheSameObjectStartingAgainClearsIt(bool playerFast)
        {
            LegacySkill fast = Attack(98701, 1), normal = Attack(98702, 2), other = Attack(98703, 2);
            var duel = Duel(playerFast ? new[] { fast, other } : new[] { normal, normal },
                playerFast ? new[] { normal, normal } : new[] { fast, other });
            duel.Commit();
            LegacyCurrentSlot first = duel.BeginNextSlot();
            Assert.That(Keep(first, !playerFast, normal, false), Is.True);
            Finish(duel);
            LegacyCurrentSlot padding = duel.BeginNextSlot();
            Assert.That(playerFast ? padding.PlayerActionSkill : padding.EnemyActionSkill, Is.Null);
            Assert.That(Keep(padding, !playerFast, normal, true), Is.True,
                "A previously slowed swing cannot jump back to the snap curve in the empty half-cell.");
            Assert.That(Keep(padding, !playerFast, normal, false), Is.False);
            Finish(duel);
            LegacyCurrentSlot next = duel.BeginNextSlot();
            // Fixed queues copy each reservation; keep passing the shared source object to the pose policy
            // so a reused skill reference cannot mask the new action's ActionStarts boundary.
            Assert.That((playerFast ? next.EnemyActionSkill : next.PlayerActionSkill).Id, Is.EqualTo(normal.Id));
            Assert.That(playerFast ? next.EnemyActionStarts : next.PlayerActionStarts, Is.True);
            Assert.That(Keep(next, !playerFast, normal, true), Is.False,
                "Reservation identity and ActionStarts, rather than the reused skill reference, end the slow pose.");
        }

        [Test]
        public void BothFastActions_KeepTheirExistingAttackRhythm()
        {
            LegacySkill left = Attack(98701, 1), right = Attack(98702, 1);
            LegacyCurrentSlot slot = Begin(new[] { left }, new[] { right });
            Assert.That(Keep(slot, true, left, true), Is.False);
            Assert.That(Keep(slot, false, right, true), Is.False);
            for (int i = 0; i <= 100; i++)
                Assert.That(Phase(left, i / 100f, 1f, false),
                    Is.EqualTo(LegacyAttackRhythm.PosePhase(left, i / 100f, 1f)).Within(.000001f));
        }

        [TestCase(0f)]
        [TestCase(.5f)]
        [TestCase(1f)]
        public void TheSlowOpponent_BypassesOnlyTheSecondarySnapCurve_WhileOrdinaryActorsRetainIt(float strength)
        {
            LegacySkill skill = Attack(98702, 2);
            foreach (float raw in new[] { 0f, .1f, .2f, .42f, .5f, .6f, .75f, 1f })
            {
                Assert.That(Phase(skill, raw, strength, true), Is.EqualTo(raw).Within(.000001f),
                    "The canonical slow pose must reach the rendered clip without another release acceleration.");
                Assert.That(Phase(skill, raw, strength, false),
                    Is.EqualTo(LegacyAttackRhythm.PosePhase(skill, raw, strength)).Within(.000001f));
            }
        }

        [Test]
        public void ProductionEnemyCelMapping_KeepsTheAuthoredContactAndClipEnds()
        {
            Assert.That(Frame(0f), Is.EqualTo(0));
            Assert.That(Frame(.499f), Is.LessThan(4));
            Assert.That(Frame(.5f), Is.EqualTo(4), "Enemy contact remains authored cel 5 at the half-cycle hit.");
            Assert.That(Frame(1f), Is.EqualTo(11));
        }

        [TestCase(.42f)]
        [TestCase(1f)]
        public void FinalRenderedMiddleCels_RemainVisibleLongerThanTheRecompressedSlowPose(float playbackSpeed)
        {
            LegacySkill fast = Attack(98701, 1), slow = Attack(98702, 2);
            var duel = Duel(new[] { fast, fast }, new[] { slow });
            duel.Commit(); duel.BeginNextSlot(); Finish(duel);
            LegacyCurrentSlot completing = duel.BeginNextSlot();
            int[] before = State(duel);
            float clip = LegacyArenaView.OriginalClipDuration / playbackSpeed;
            float end = ExistingFloat("ClipEnd", completing, 0, clip, 0f);
            float step = clip / 2048f;
            var snapped = new float[12];
            var visibleSlow = new float[12];
            for (float elapsed = 0f; elapsed < end; elapsed += step)
            {
                float mapped = ExistingFloat("ActionElapsed", completing, false, elapsed, clip, 0f);
                float raw = Pose(clip, mapped, 1, 1, 2, true) / clip;
                snapped[Frame(Phase(slow, raw, 1f, false))] += step;
                visibleSlow[Frame(Phase(slow, raw, 1f, true))] += step;
            }
            float snappedMiddle = 0f, visibleMiddle = 0f;
            for (int frame = 5; frame <= 10; frame++)
            {
                Assert.That(visibleSlow[frame], Is.GreaterThan(snapped[frame] * 1.8f),
                    "Authored cel " + (frame + 1) + " must not be squeezed back into a one-frame release.");
                snappedMiddle += snapped[frame];
                visibleMiddle += visibleSlow[frame];
            }
            Assert.That(visibleMiddle, Is.GreaterThan(snappedMiddle * 2f));
            if (playbackSpeed < .5f)
                for (int frame = 5; frame <= 10; frame++)
                    Assert.That(visibleSlow[frame], Is.GreaterThan(2f / 60f),
                        "At the project's 0.42 playback speed, each moving middle cel gets more than two 60-Hz frames.");
            CollectionAssert.AreEqual(before, State(duel), "Rendered-pose inspection never resolves another hit.");
        }

        [Test]
        public void TheSlowRecovery_DrawsItsMiddleCel_InsteadOfJumpingAheadToTheSnapCurve()
        {
            LegacySkill slow = Attack(98702, 2);
            Assert.That(Frame(Phase(slow, .6f, 1f, true)), Is.EqualTo(6), "The slow recovery should show cel 7.");
            Assert.That(Frame(Phase(slow, .6f, 1f, false)), Is.EqualTo(8),
                "The ordinary attack rhythm intentionally releases ahead to cel 9 and remains unchanged.");
        }

        [Test]
        public void ReadingTheFinalPosePolicy_DoesNotChangeTheCombatSnapshot()
        {
            LegacySkill fast = Attack(98701, 1), slow = Attack(98702, 2);
            var duel = Duel(new[] { fast, fast }, new[] { slow });
            duel.Commit(); LegacyCurrentSlot slot = duel.BeginNextSlot();
            int[] before = State(duel);
            for (int i = 0; i < 100; i++)
            {
                bool kept = Keep(slot, false, slow, false);
                Frame(Phase(slow, i / 100f, 1f, kept));
            }
            CollectionAssert.AreEqual(before, State(duel));
        }

        [UnityTest]
        public IEnumerator ActualDrawnSprites_KeepTheSlowOpponentOnCelSeven_AndTheFastActorsOriginalRhythm()
        {
            yield return null;
            var root = new GameObject("Rendered Fast Slow Pose Test");
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            JsonUtility.FromJsonOverwrite("{\"attackRhythmEnabled\":true,\"attackRhythmStrength\":1}", settings);
            using (var art = new LegacyDuelArt())
            using (var arena = LegacyArenaView.Create(root.transform, art, settings))
            using (var finalPlayer = new FinalPlayerAnimationSet())
            {
                try
                {
                    Assert.That(finalPlayer.IsReady, Is.True);
                    LegacySkill fast = Attack(98701, 1), slow = Attack(98702, 2);
                    var duel = Duel(new[] { fast, fast }, new[] { slow });
                    duel.Commit(); LegacyCurrentSlot first = duel.BeginNextSlot();
                    BeginArena(arena, first);
                    float clip = LegacyArenaView.OriginalClipDuration;
                    Hold(arena, clip); Finish(duel);
                    LegacyCurrentSlot second = duel.BeginNextSlot();
                    BeginArena(arena, second);
                    int[] before = State(duel);
                    Hold(arena, .75f * clip);
                    Assert.That(arena.EnemyRenderer.sprite.name, Does.EndWith("frame-07"),
                        "The renderer must consume the slow phase itself, not only store a slower AnimationTime.");
                    float fastPhase = LegacyAttackRhythm.PosePhase(fast, .75f, 1f);
                    Assert.That(arena.PlayerRenderer.sprite.name, Is.EqualTo(finalPlayer.GetAttack(fast, fastPhase).name),
                        "The fast actor retains exactly its original final-art cel selection.");
                    CollectionAssert.AreEqual(before, State(duel));
                    arena.EndTurn();
                    arena.BeginSlot(slow, slow);
                    Hold(arena, .6f * clip);
                    Assert.That(arena.EnemyRenderer.sprite.name, Does.EndWith("frame-09"),
                        "An ordinary exchange still keeps the authored attack snap.");

                    arena.EndTurn();
                    var padded = Duel(new[] { fast, slow }, new[] { slow, slow });
                    padded.Commit();
                    BeginArena(arena, padded.BeginNextSlot());
                    Hold(arena, clip); Finish(padded);
                    BeginArena(arena, padded.BeginNextSlot());
                    Hold(arena, .6f * clip);
                    Assert.That(arena.EnemyRenderer.sprite.name, Does.EndWith("frame-07"),
                        "An unfinished slowed action keeps the linear pose after the fast opponent leaves a padding half-cell.");
                    Finish(padded);
                    BeginArena(arena, padded.BeginNextSlot());
                    Hold(arena, .6f * clip);
                    Assert.That(arena.EnemyRenderer.sprite.name, Does.EndWith("frame-09"),
                        "A new reservation of the same normal skill restores its ordinary release curve.");
                }
                finally { Object.Destroy(settings); Object.Destroy(root); }
            }
        }

        private static Type Policy => typeof(DuelPrototypeController).Assembly.GetType("TurnLimbo.Presentation.DuelFastSkillSlowMotion");
        private static MethodInfo Method(string name)
        {
            Assert.That(Policy, Is.Not.Null);
            MethodInfo method = Policy.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Missing rendered-pose policy: " + name);
            return method;
        }
        private static bool Keep(LegacyCurrentSlot slot, bool player, LegacySkill skill, bool previous)
            => (bool)Method("KeepSlowPose").Invoke(null, new object[] { slot, player, skill, previous });
        private static float Phase(LegacySkill skill, float raw, float strength, bool slowPose)
            => (float)Method("PosePhase").Invoke(null, new object[] { skill, raw, strength, slowPose });
        private static int Frame(float phase)
        {
            MethodInfo method = typeof(EnemyStudentAnimationSet).GetMethod("AttackFrameIndex", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "The final enemy cel index must come from its production sampling function.");
            return (int)method.Invoke(null, new object[] { phase });
        }
        private static float ExistingFloat(string name, params object[] arguments) => (float)Method(name).Invoke(null, arguments);
        private static float Pose(float clip, float elapsed, int elapsedTicks, int segmentTicks, int duration, bool completes)
            => (float)typeof(DuelPrototypeController).Assembly.GetType("TurnLimbo.Presentation.DuelSkillTempoPose")
                .GetMethod("Sample", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { clip, elapsed, 0f, elapsedTicks, segmentTicks, duration, completes });
        private static LegacySkill Attack(int id, int duration)
            => new LegacySkill(id, "렌더 자세 검사", 0, 3, 3, LegacySkillKind.Attack, LegacySkillProperty.Slash,
                1, 0, string.Empty, durationHalfUnits: duration);
        private static LegacyQueuedDuel Duel(LegacySkill[] player, LegacySkill[] enemy)
        {
            var duel = new LegacyQueuedDuel(10000, 10000, 10000, 10000, player, enemy, new[] { enemy.Length }, 7,
                features: CombatFeature.All & ~CombatFeature.Dodge & ~CombatFeature.Pressure, meshPercent: 0);
            Assert.That(duel.TrySetFixedQueues(player, enemy), Is.True);
            return duel;
        }
        private static LegacyCurrentSlot Begin(LegacySkill[] player, LegacySkill[] enemy)
        {
            LegacyQueuedDuel duel = Duel(player, enemy); duel.Commit(); return duel.BeginNextSlot();
        }
        private static void Finish(LegacyQueuedDuel duel)
        {
            while (!duel.CurrentSlot.IsResolved) duel.ResolveNextHit();
            duel.CompleteCurrentSlot();
        }
        private static int[] State(LegacyQueuedDuel duel)
            => new[] { duel.Player.Health, duel.Player.Resistance, duel.Enemy.Health, duel.Enemy.Resistance,
                duel.Act, duel.PlayerQueue.Count, duel.EnemyQueue.Count, duel.CurrentSlot?.HitsResolved ?? -1 };
        private static void BeginArena(LegacyArenaView arena, LegacyCurrentSlot slot)
        {
            typeof(LegacyArenaView).GetMethod("ConfigureSlotTiming", Private).Invoke(arena, new object[] { 1f, 0f });
            typeof(LegacyArenaView).GetMethod("BeginTempoSlot", Private).Invoke(arena, new object[] { slot, null, 0f });
        }
        private static void Hold(LegacyArenaView arena, float time)
            => typeof(LegacyArenaView).GetMethod("HoldSlotAtTime", Private).Invoke(arena, new object[] { time });
    }
}
