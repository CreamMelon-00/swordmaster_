using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Presentation;
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
        public void EitherSidesFastTechnique_SlowsTheWorldButPreservesTheActionClock(int playerDuration, int enemyDuration)
        {
            LegacyCurrentSlot slot = Begin(new[] { Attack(98001, playerDuration) }, new[] { Attack(98002, enemyDuration) });
            Assert.That(Speed(slot, true), Is.EqualTo(Slow));
            Assert.That((float)Helper.GetField("Scale", BindingFlags.Public | BindingFlags.Static).GetRawConstantValue(), Is.EqualTo(Slow));
            Split(.01f, 1f, slot, true, out float world, out float action);
            Assert.That(world, Is.EqualTo(.004f).Within(.000001f));
            Assert.That(action, Is.EqualTo(.01f).Within(.000001f), "The fast technique must retain the original real-time speed.");
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
        public void WorldSlowMotion_DoesNotLimitTheActionClock_AndPreservesStrongerCinematicSlow(float existing, float expected)
        {
            LegacyCurrentSlot slot = Begin(new[] { Attack(98001, 1) }, new[] { Attack(98002, 2) });
            Assert.That(Limit(existing, slot, true), Is.EqualTo(expected).Within(.000001f));
            Assert.That(Limit(existing, slot, false), Is.EqualTo(existing).Within(.000001f),
                "Leaving a fast playing interval must preserve the existing presentation speed.");
            Split(.01f, existing, slot, true, out float world, out float action);
            Assert.That(world, Is.EqualTo(.01f * expected).Within(.000001f));
            Assert.That(action, Is.EqualTo(.01f * existing).Within(.000001f),
                "Only the fast-specific slow is excluded; step, fatal focus and finale still slow the action clock.");
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

        [TestCase(false)]
        [TestCase(true)]
        public void PausedOrHitStoppedTime_DoesNotAdvanceEitherClock(bool playerFast)
        {
            LegacyCurrentSlot slot = Completing(playerFast, 1, 1);
            Split(0f, 1f, slot, true, out float world, out float action);
            Assert.That(world, Is.Zero);
            Assert.That(action, Is.Zero, "The exemption must never advance a fast actor during hit stop or a paused clock.");
        }

        [Test]
        public void NormalAndNonPlayingPhases_KeepBothClocksTogether()
        {
            LegacyCurrentSlot ordinary = Begin(new[] { Attack(98001, 2) }, new[] { Attack(98002, 2) });
            LegacyCurrentSlot fast = Begin(new[] { Attack(98001, 1) }, new[] { Attack(98002, 2) });
            Split(.01f, 1.8f, ordinary, true, out float world, out float action);
            Assert.That(world, Is.EqualTo(.018f).Within(.000001f));
            Assert.That(action, Is.EqualTo(world));
            Split(.01f, 1.8f, fast, false, out world, out action);
            Assert.That(world, Is.EqualTo(.018f).Within(.000001f));
            Assert.That(action, Is.EqualTo(world));
            Split(.01f, 1f, null, true, out world, out action);
            Assert.That(world, Is.EqualTo(.01f).Within(.000001f));
            Assert.That(action, Is.EqualTo(world));
        }

        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(4, 2)]
        [TestCase(2, 4)]
        public void BothFastOrNoFastActions_KeepTheirExistingPoseAndContactSchedule(int playerDuration, int enemyDuration)
        {
            LegacyCurrentSlot slot = Begin(new[] { Attack(98001, playerDuration) }, new[] { Attack(98002, enemyDuration) });
            Assert.That(HasSlowCompletion(slot), Is.False);
            for (int hit = 0; hit < 3; hit++)
            {
                float contact = .5f + hit * 1.1f;
                Assert.That(ActionElapsed(slot, true, contact, 1f, .1f), Is.EqualTo(contact));
                Assert.That(ActionElapsed(slot, false, contact, 1f, .1f), Is.EqualTo(contact));
                Assert.That(HitTime(slot, hit, 1f, .1f), Is.EqualTo(contact).Within(.000001f));
                Assert.That(ClipEnd(slot, hit, 1f, .1f), Is.EqualTo(1f + hit * 1.1f).Within(.000001f));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PendingOpposingAction_KeepsItsExistingContinuousWindupBeforeTheSharedContact(bool playerFast)
        {
            var fast = new[] { Attack(98001, 1), Attack(98002, 1) };
            var normal = new[] { Attack(98003, 2) };
            LegacyQueuedDuel duel = Duel(playerFast ? fast : normal, playerFast ? normal : fast);
            duel.Commit();
            LegacyCurrentSlot pending = duel.BeginNextSlot();
            Assert.That(HasSlowCompletion(pending), Is.False);
            Assert.That(ActionElapsed(pending, !playerFast, 1f, 1f, 0f), Is.EqualTo(1f),
                "Logical interval offsets already slow a pending action; scaling its input again would cause a boundary jump.");
            float pendingEnd = Pose(1f, 1f, 0, 1, 2, false);
            Finish(duel);
            LegacyCurrentSlot completing = duel.BeginNextSlot();
            Assert.That(HasSlowCompletion(completing), Is.True);
            float completingStart = Pose(1f, ActionElapsed(completing, !playerFast, 0f, 1f, 0f), 1, 1, 2, true);
            Assert.That(completingStart, Is.EqualTo(pendingEnd).Within(.000001f));
            Assert.That(pendingEnd, Is.LessThan(.5f));
        }

        [TestCase(false, 1, 1, 0f)]
        [TestCase(true, 1, 1, 0f)]
        [TestCase(false, 1, 3, 0f)]
        [TestCase(true, 1, 3, .1f)]
        [TestCase(false, 2, 3, .1f)]
        [TestCase(true, 2, 3, 0f)]
        [TestCase(false, 3, 1, .1f)]
        [TestCase(true, 3, 1, 0f)]
        [TestCase(false, 3, 2, 0f)]
        [TestCase(true, 3, 3, .1f)]
        public void SlowOpponent_MatchesEveryActualContact_WithoutSlowingTheFastActorsContacts(
            bool playerFast, int fastHits, int slowHits, float gap)
        {
            LegacyCurrentSlot slot = Completing(playerFast, fastHits, slowHits);
            Assert.That(HasSlowCompletion(slot), Is.True);
            float previousContact = -1f;
            for (int hit = 0; hit < Math.Max(fastHits, slowHits); hit++)
            {
                float originalContact = .5f + hit * (1f + gap);
                float contact = HitTime(slot, hit, 1f, gap);
                Assert.That(contact, Is.GreaterThan(previousContact));
                previousContact = contact;
                if (hit < fastHits)
                {
                    Assert.That(contact, Is.EqualTo(originalContact).Within(.000001f),
                        "Every fast strike must retain its original contact time, including later hits.");
                    Assert.That(ActionElapsed(slot, playerFast, contact, 1f, gap), Is.EqualTo(originalContact).Within(.000001f));
                }
                if (hit < slowHits)
                    Assert.That(ActionElapsed(slot, !playerFast, contact, 1f, gap), Is.EqualTo(originalContact).Within(.000001f),
                        "A slowed-only later strike must resolve when its actual contact cel is reached.");
                Assert.That(ClipEnd(slot, hit, 1f, gap), Is.GreaterThan(contact));
            }
            float anchor = .5f + (Math.Min(fastHits, slowHits) - 1) * (1f + gap);
            float slowBefore = ActionElapsed(slot, !playerFast, anchor + .1f, 1f, gap);
            float slowAfter = ActionElapsed(slot, !playerFast, anchor + .2f, 1f, gap);
            Assert.That(slowAfter - slowBefore, Is.EqualTo(.04f).Within(.000001f));
            Assert.That(ActionElapsed(slot, playerFast, anchor + .2f, 1f, gap) -
                ActionElapsed(slot, playerFast, anchor + .1f, 1f, gap), Is.EqualTo(.1f).Within(.000001f));
            int lastHit = Math.Max(fastHits, slowHits) - 1;
            float end = ClipEnd(slot, lastHit, 1f, gap);
            Assert.That(end, Is.GreaterThanOrEqualTo(fastHits + (fastHits - 1) * gap));
            Assert.That(ActionElapsed(slot, !playerFast, end, 1f, gap),
                Is.GreaterThanOrEqualTo(slowHits + (slowHits - 1) * gap - .000001f),
                "The slot cannot cut off the slow opponent's final recovery.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SlowedOpponentRecovery_OutlastsTheFinishedFastClip_WithoutAddingAnotherHit(bool playerFast)
        {
            LegacyCurrentSlot slot = Completing(playerFast, 1, 1);
            Assert.That(HitTime(slot, 0, 1f, 0f), Is.EqualTo(.5f));
            Assert.That(ClipEnd(slot, 0, 1f, 0f), Is.EqualTo(1.75f));
            Assert.That(ActionElapsed(slot, playerFast, 1f, 1f, 0f), Is.EqualTo(1f));
            Assert.That(ActionElapsed(slot, !playerFast, 1f, 1f, 0f), Is.EqualTo(.7f).Within(.000001f));
            Assert.That(ActionElapsed(slot, !playerFast, 1.75f, 1f, 0f), Is.EqualTo(1f).Within(.000001f));
            Assert.That(slot.HitsResolved, Is.Zero);
            Assert.That(slot.HitCount, Is.EqualTo(1), "A longer visual recovery never manufactures another attack.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ACounterAgainstAFastAttack_GainsItsSlowTailAfterTheDeferredChoice_WithoutMovingTheFastContact(bool playerFast)
        {
            LegacySkill fast = Attack(98001, 1);
            LegacySkill counterSkill = Attack(98002, 2, 3);
            LegacySkill[] player = playerFast ? new[] { fast } : Array.Empty<LegacySkill>();
            LegacySkill[] enemy = playerFast ? Array.Empty<LegacySkill>() : new[] { fast };
            var counter = new LegacyCounter(counterSkill);
            var duel = new LegacyQueuedDuel(10000, 10000, 10000, 10000,
                player.Length == 0 ? new[] { Attack(98099, 2) } : player, enemy, new[] { enemy.Length }, 7,
                playerCounter: playerFast ? null : counter, enemyCounter: playerFast ? counter : null,
                features: CombatFeature.All & ~CombatFeature.Dodge & ~CombatFeature.Pressure, meshPercent: 0);
            PrepareQueues(duel, player, enemy);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            float fastContact = HitTime(slot, 0, 1f, 0f);
            if (!playerFast)
            {
                Assert.That(slot.PlayerSkill, Is.Null);
                Assert.That(slot.PendingPlayerCounter, Is.SameAs(counterSkill));
                Assert.That(HasSlowCompletion(slot), Is.False, "The pending choice has not materialized a completing action yet.");
                Assert.That(duel.PrepareCurrentSlotImpact(), Is.True);
                Assert.That(slot.PlayerSkill, Is.SameAs(counterSkill));
                Assert.That(slot.PlayerCountered, Is.True);
                Assert.That(duel.PlayerCountersRemaining, Is.Zero);
            }
            else
            {
                Assert.That(slot.EnemyCountered, Is.True);
                Assert.That(duel.EnemyCountersRemaining, Is.Zero);
            }
            Assert.That(HasSlowCompletion(slot), Is.True);
            Assert.That(HitTime(slot, 0, 1f, 0f), Is.EqualTo(fastContact).Within(.000001f));
            Assert.That(fastContact, Is.EqualTo(.5f));
            Assert.That(HitTime(slot, 1, 1f, 0f), Is.EqualTo(3f).Within(.000001f),
                "The normal counter's later strike follows its slowed pose rather than the already-finished fast action.");
            Assert.That(ClipEnd(slot, 2, 1f, 0f), Is.EqualTo(6.75f).Within(.000001f));
            Assert.That(ActionElapsed(slot, !playerFast, HitTime(slot, 1, 1f, 0f), 1f, 0f),
                Is.EqualTo(1.5f).Within(.000001f));
            Assert.That(duel.PrepareCurrentSlotImpact(), Is.False, "The choice and its opening effects settle once.");
            int fastHits = 0, counterHits = 0;
            while (!slot.IsResolved)
            {
                LegacyHitResult hit = duel.ResolveNextHit();
                if (playerFast ? hit.PlayerAttacked : hit.EnemyAttacked) fastHits++;
                if (playerFast ? hit.EnemyAttacked : hit.PlayerAttacked) counterHits++;
            }
            Assert.That(fastHits, Is.EqualTo(1));
            Assert.That(counterHits, Is.EqualTo(3));
        }

        [TestCase(false, 1, 3)]
        [TestCase(true, 1, 1)]
        [TestCase(false, 3, 1)]
        [TestCase(true, 3, 3)]
        public void OvershootBoundary_UsesTheFastActorsOwnClipEnd_InsteadOfTheOpponentsLongerRecovery(
            bool playerFast, int fastHits, int slowHits)
        {
            LegacyCurrentSlot slot = Completing(playerFast, fastHits, slowHits);
            for (int hit = 0; hit < Math.Max(fastHits, slowHits); hit++)
            {
                float boundary = ContactClipEnd(slot, hit, 1f, .1f);
                Assert.That(boundary, Is.GreaterThan(HitTime(slot, hit, 1f, .1f)));
                float expected = hit < fastHits ? 1f + hit * 1.1f : ClipEnd(slot, hit, 1f, .1f);
                Assert.That(boundary, Is.EqualTo(expected).Within(.000001f),
                    "A long frame cannot present a fast strike after that actor has already left its own attack clip.");
                Assert.That(boundary, Is.LessThanOrEqualTo(ClipEnd(slot, hit, 1f, .1f) + .000001f));
            }
        }

        [UnityTest]
        public IEnumerator RealControllerClock_ExemptsBothFastIntervals_WhileTheOpponentRecoversSlowly()
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
                float clip = LegacyArenaView.OriginalClipDuration / controller.ActiveSlotPlaybackSpeed;
                scope.Until(() => duel.CurrentSlot?.SlotIndex == 2 && controller.ActiveSlotElapsedTime >= clip * .55f);
                float fastBefore = ActorTime(controller.ArenaView, true);
                float slowBefore = ActorTime(controller.ArenaView, false);
                int[] afterContact = State(duel);
                scope.Advance(.001f);
                Assert.That(ActorTime(controller.ArenaView, true) - fastBefore, Is.EqualTo(.001f).Within(.00001f));
                Assert.That(ActorTime(controller.ArenaView, false) - slowBefore, Is.EqualTo(.0004f).Within(.00001f),
                    "The opposing recovery remains slow after the shared contact while the fast actor is exempt.");
                CollectionAssert.AreEqual(afterContact, State(duel));
                scope.Until(() => duel.CurrentSlot?.SlotIndex == 2 && controller.ActiveSlotElapsedTime >= clip * 1.1f);
                Assert.That(ActorTime(controller.ArenaView, true), Is.GreaterThan(clip), "The fast attack has already finished its original clip.");
                Assert.That(ActorTime(controller.ArenaView, false), Is.LessThan(clip), "The opponent is still completing its slow recovery.");
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

        [UnityTest]
        public IEnumerator ArenaTickAndExactHolds_ExemptEitherFastActor_AndKeepTheOpponentsPoseContinuous()
        {
            yield return null;
            foreach (bool playerFast in new[] { true, false })
            {
                var root = new GameObject("Fast Exemption Actor Test");
                var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                using (var art = new LegacyDuelArt())
                using (var arena = LegacyArenaView.Create(root.transform, art, settings))
                {
                    try
                    {
                        var fast = new[] { Attack(98001, 1), Attack(98002, 1) };
                        var slow = new[] { Attack(98003, 2) };
                        LegacyQueuedDuel duel = Duel(playerFast ? fast : slow, playerFast ? slow : fast);
                        duel.Commit();
                        LegacyCurrentSlot pending = duel.BeginNextSlot();
                        BeginArena(arena, pending);
                        float clip = LegacyArenaView.OriginalClipDuration;
                        TickArena(arena, .25f * clip);
                        Assert.That(ActorTime(arena, playerFast), Is.EqualTo(.25f * clip).Within(.00001f));
                        Assert.That(ActorTime(arena, !playerFast), Is.LessThan(.25f * clip));
                        float beforeHold = ActorTime(arena, !playerFast);
                        Hold(arena, .25f * clip);
                        Assert.That(ActorTime(arena, playerFast), Is.EqualTo(.25f * clip).Within(.00001f));
                        Assert.That(ActorTime(arena, !playerFast), Is.EqualTo(beforeHold).Within(.00001f),
                            "An exact hold must sample authoritative action time, not reapply the world's slow scale.");
                        Hold(arena, clip);
                        float pendingEnd = ActorTime(arena, !playerFast);
                        Finish(duel);
                        LegacyCurrentSlot completing = duel.BeginNextSlot();
                        BeginArena(arena, completing);
                        Assert.That(ActorTime(arena, !playerFast), Is.EqualTo(pendingEnd).Within(.00001f),
                            "The unfinished normal action cannot jump when the second fast technique begins.");
                        Hold(arena, .5f * clip);
                        Assert.That(ActorTime(arena, playerFast), Is.EqualTo(.5f * clip).Within(.00001f));
                        Assert.That(ActorTime(arena, !playerFast), Is.EqualTo(.5f * clip).Within(.00001f),
                            "Simultaneous completion still meets at both contact cels.");
                        TickArena(arena, .25f * clip);
                        Assert.That(ActorTime(arena, playerFast), Is.EqualTo(.75f * clip).Within(.00001f));
                        Assert.That(ActorTime(arena, !playerFast), Is.EqualTo(.6f * clip).Within(.00001f));
                        Hold(arena, .75f * clip);
                        Assert.That(ActorTime(arena, playerFast), Is.EqualTo(.75f * clip).Within(.00001f));
                        Assert.That(ActorTime(arena, !playerFast), Is.EqualTo(.6f * clip).Within(.00001f));
                        int[] before = State(duel);
                        Hold(arena, 1.75f * clip);
                        Assert.That(ActorTime(arena, !playerFast), Is.EqualTo(clip).Within(.00001f),
                            "The slow final recovery must be shown rather than cut off when the fast clip ends.");
                        CollectionAssert.AreEqual(before, State(duel));
                        arena.EndTurn();
                        arena.BeginSlot(slow[0], slow[0]);
                        Hold(arena, .25f * clip);
                        Assert.That(ActorTime(arena, true), Is.EqualTo(.25f * clip).Within(.00001f));
                        Assert.That(ActorTime(arena, false), Is.EqualTo(.25f * clip).Within(.00001f));
                    }
                    finally { Object.Destroy(settings); Object.Destroy(root); }
                }
            }
        }

        [UnityTest]
        public IEnumerator LongFramePastTheFastClip_HoldsTheActualSharedContact_WhileKeepingTheOpponentsRecoveryTail()
        {
            yield return null;
            using (var scope = new ControllerScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.CommitTurn();
                scope.Until(() => controller.Session.CurrentSlot?.SlotIndex == 2);
                LegacyCurrentSlot slot = controller.Session.CurrentSlot;
                Assert.That(slot.HitsResolved, Is.Zero);
                float clip = LegacyArenaView.OriginalClipDuration / controller.ActiveSlotPlaybackSpeed;
                scope.Advance(1.2f * clip);
                Assert.That(slot.HitsResolved, Is.EqualTo(1));
                Assert.That(controller.ActiveSlotElapsedTime, Is.EqualTo(.5f * clip).Within(.00001f),
                    "Crossing the fast actor's clip end must rewind to its contact even though slow recovery ends at 1.75 clips.");
                Assert.That(ActorTime(controller.ArenaView, true), Is.EqualTo(.5f * clip).Within(.00001f));
                Assert.That(ActorTime(controller.ArenaView, false), Is.EqualTo(.5f * clip).Within(.00001f));
                Assert.That(controller.ActiveSlotDuration, Is.GreaterThan(1.7f * clip));
                scope.Advance(0f);
                Assert.That(slot.HitsResolved, Is.EqualTo(1), "Holding a contact cannot resolve another hit.");
            }
        }

        [UnityTest]
        public IEnumerator AnUninterruptedCharge_KeepsItsLeadInBeforeTheActualStrike_WhenHeldAtExactTimes()
        {
            yield return null;
            foreach (bool playerCharge in new[] { true, false })
            {
                var root = new GameObject("Charge Lead In Clock Test");
                var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                using (var art = new LegacyDuelArt())
                using (var arena = LegacyArenaView.Create(root.transform, art, settings))
                {
                    try
                    {
                        LegacySkill charge = Attack(98004, 4);
                        LegacyQueuedDuel duel = Duel(playerCharge ? new[] { charge } : Array.Empty<LegacySkill>(),
                            playerCharge ? Array.Empty<LegacySkill>() : new[] { charge });
                        duel.Commit();
                        LegacyCurrentSlot slot = duel.BeginNextSlot();
                        Assert.That(slot.DurationHalfTicks, Is.EqualTo(4));
                        float clip = LegacyArenaView.OriginalClipDuration;
                        BeginArena(arena, slot, clip);
                        Assert.That(ActorTime(arena, playerCharge), Is.Zero.Within(.00001f),
                            "Lead-in subtraction must not clamp a zero hold to the end of the charge preparation.");
                        Hold(arena, .5f * clip);
                        Assert.That(ActorTime(arena, playerCharge), Is.EqualTo(.1f * clip).Within(.00001f));
                        Hold(arena, clip);
                        Assert.That(ActorTime(arena, playerCharge), Is.EqualTo(.2f * clip).Within(.00001f));
                        Hold(arena, 1.5f * clip);
                        Assert.That(ActorTime(arena, playerCharge), Is.EqualTo(.5f * clip).Within(.00001f));
                        Hold(arena, 2f * clip);
                        Assert.That(ActorTime(arena, playerCharge), Is.EqualTo(clip).Within(.00001f));
                        Assert.That(slot.HitsResolved, Is.Zero, "Pose holds do not resolve the charge themselves.");
                    }
                    finally { Object.Destroy(settings); Object.Destroy(root); }
                }
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
            Assert.That(controller.ActiveSlotElapsedTime - elapsed, Is.EqualTo(.001f).Within(.00001f),
                "The action and impact clock must retain the original speed instead of inheriting the world's 0.4 scale.");
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
        private static void Split(float liveTime, float slotSpeed, LegacyCurrentSlot slot, bool playing,
            out float world, out float action)
        {
            MethodInfo method = Helper.GetMethod("SplitDelta", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "World slow motion and the fast action/impact clock must be split.");
            object[] arguments = { liveTime, slotSpeed, slot, playing, 0f, 0f };
            method.Invoke(null, arguments);
            world = (float)arguments[4];
            action = (float)arguments[5];
        }
        private static bool HasSlowCompletion(LegacyCurrentSlot slot)
            => (bool)Helper.GetMethod("HasSlowCompletion", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { slot });
        private static float ActionElapsed(LegacyCurrentSlot slot, bool player, float elapsed, float clip, float gap)
            => (float)Helper.GetMethod("ActionElapsed", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { slot, player, elapsed, clip, gap });
        private static float HitTime(LegacyCurrentSlot slot, int hit, float clip, float gap)
            => (float)Helper.GetMethod("HitTime", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { slot, hit, clip, gap });
        private static float ClipEnd(LegacyCurrentSlot slot, int hit, float clip, float gap)
            => (float)Helper.GetMethod("ClipEnd", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { slot, hit, clip, gap });
        private static float ContactClipEnd(LegacyCurrentSlot slot, int hit, float clip, float gap)
            => (float)Helper.GetMethod("ContactClipEnd", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { slot, hit, clip, gap });
        private static float Pose(float clip, float elapsed, int elapsedTicks, int segment, int duration, bool completes)
            => (float)typeof(DuelPrototypeController).Assembly.GetType("TurnLimbo.Presentation.DuelSkillTempoPose")
                .GetMethod("Sample", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { clip, elapsed, 0f, elapsedTicks, segment, duration, completes });
        private static float ActorTime(LegacyArenaView arena, bool player)
        {
            object actor = typeof(LegacyArenaView).GetField(player ? "player" : "enemy", Private).GetValue(arena);
            return (float)actor.GetType().GetField("AnimationTime").GetValue(actor);
        }
        private static void BeginArena(LegacyArenaView arena, LegacyCurrentSlot slot, float leadIn = 0f)
        {
            typeof(LegacyArenaView).GetMethod("ConfigureSlotTiming", Private).Invoke(arena, new object[] { 1f, 0f });
            typeof(LegacyArenaView).GetMethod("BeginTempoSlot", Private).Invoke(arena, new object[] { slot, null, leadIn });
        }
        private static void TickArena(LegacyArenaView arena, float actionDelta)
            => typeof(LegacyArenaView).GetMethod("TickSkillTempo", Private)
                .Invoke(arena, new object[] { actionDelta * Slow, actionDelta, actionDelta, true });
        private static void Hold(LegacyArenaView arena, float elapsed)
            => typeof(LegacyArenaView).GetMethod("HoldSlotAtTime", Private).Invoke(arena, new object[] { elapsed });
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
        private static LegacyCurrentSlot Completing(bool playerFast, int fastHits, int slowHits)
        {
            var fast = new[] { Attack(98001, 1, fastHits), Attack(98002, 1, fastHits) };
            var slow = new[] { Attack(98003, 2, slowHits) };
            LegacyQueuedDuel duel = Duel(playerFast ? fast : slow, playerFast ? slow : fast);
            duel.Commit();
            duel.BeginNextSlot();
            Finish(duel);
            return duel.BeginNextSlot();
        }
        private static LegacyQueuedDuel Duel(LegacySkill[] player, LegacySkill[] enemy)
        {
            var duel = new LegacyQueuedDuel(10000, 10000, 10000, 10000,
                player.Length == 0 ? new[] { Attack(98099, 2) } : player, enemy, new[] { enemy.Length }, 7,
                features: CombatFeature.All & ~CombatFeature.Dodge & ~CombatFeature.Pressure, meshPercent: 0);
            PrepareQueues(duel, player, enemy);
            return duel;
        }
        private static void PrepareQueues(LegacyQueuedDuel duel, LegacySkill[] player, LegacySkill[] enemy)
        {
            if (player.Length > 0 && enemy.Length > 0)
                Assert.That(duel.TrySetFixedQueues(player, enemy), Is.True);
            else
                Assert.That((bool)typeof(LegacyQueuedDuel).GetMethod("TrySetPreparedQueues", Private)
                    .Invoke(duel, new object[] { player, enemy }), Is.True,
                    "The presentation sample's prepared-queue API explicitly permits a genuinely empty side.");
        }
        private static LegacySkill Attack(int id, int duration, int hits = 1)
            => new LegacySkill(id, "고속 슬로 검증", 0, 3, 3, LegacySkillKind.Attack, LegacySkillProperty.Slash,
                hits, 0, string.Empty, durationHalfUnits: duration);
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
                        "\"combatTempoEnabled\":false,\"combatTempoStep\":0.1,\"combatTempoMaxSpeed\":1.8," +
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
