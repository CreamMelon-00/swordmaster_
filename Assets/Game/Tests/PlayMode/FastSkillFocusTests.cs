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
    public sealed class FastSkillFocusTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void AFrameRequest_EntersQuicklyAndMustBeRenewedForTheNextFrame()
        {
            object envelope = NewEnvelope();
            Assert.That(Amount(envelope), Is.Zero);
            Request(envelope, true);
            Assert.That(Amount(envelope), Is.Zero, "Requesting a cue cannot advance the clock.");
            Tick(envelope, .07f, true);
            Assert.That(Amount(envelope), Is.InRange(.94f, 1f));
            Tick(envelope, .12f, true);
            Assert.That(Amount(envelope), Is.LessThan(.06f), "An omitted frame request releases the focus.");
            Tick(envelope, .5f, true);
            Assert.That(Amount(envelope), Is.Zero);
        }

        [Test]
        public void RenewedRequests_KeepOneFocusAcrossConsecutiveFastActions()
        {
            object envelope = NewEnvelope();
            Request(envelope, true);
            Tick(envelope, .07f, true);
            float first = Amount(envelope);
            Request(envelope, true);
            Tick(envelope, .07f, true);
            Assert.That(Amount(envelope), Is.GreaterThanOrEqualTo(first));
            Assert.That(Amount(envelope), Is.GreaterThan(.99f));
        }

        [TestCase(0f, .8f, .8f)]
        [TestCase(.9f, .4f, .9f)]
        [TestCase(.4f, 1f, 1f)]
        [TestCase(1f, 1f, 1f)]
        public void BackdropFocus_UsesTheStrongerCueWithoutAddingTheirDarkness(float step, float fast, float expected)
            => Assert.That((float)EnvelopeType.GetMethod("Combine").Invoke(null, new object[] { step, fast }), Is.EqualTo(expected));

        [Test]
        public void AnIneligibleScene_ClearsBothTheFocusAndItsPendingRequest()
        {
            object envelope = NewEnvelope();
            Request(envelope, true);
            Tick(envelope, .07f, true);
            Request(envelope, true);
            Tick(envelope, .01f, false);
            Assert.That(Amount(envelope), Is.Zero);
            Tick(envelope, .1f, true);
            Assert.That(Amount(envelope), Is.Zero, "A scene change cannot replay a stale request.");
        }

        [Test]
        public void ClearingBeforeATick_DropsAnUnconsumedRequest()
        {
            object envelope = NewEnvelope();
            Request(envelope, true);
            EnvelopeType.GetMethod("Clear").Invoke(envelope, null);
            Tick(envelope, .1f, true);
            Assert.That(Amount(envelope), Is.Zero);
        }

        [Test]
        public void ZeroOrInvalidElapsedTime_DoesNotAnimateTheCue()
        {
            object envelope = NewEnvelope();
            Request(envelope, true);
            Tick(envelope, .07f, true);
            float held = Amount(envelope);
            foreach (float delta in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                Request(envelope, false);
                Tick(envelope, delta, true);
                Assert.That(Amount(envelope), Is.EqualTo(held));
            }
        }

        [UnityTest]
        public IEnumerator FastFocus_DarkensTheBackdropAndClosesExistingBands_WhileFastPoseKeepsItsClock()
        {
            yield return null;
            var parent = new GameObject("Fast Focus Test");
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            JsonUtility.FromJsonOverwrite("{\"combatCameraMotionEnabled\":false,\"stepBackdropDarkening\":0.55}", settings);
            using (var art = new LegacyDuelArt())
            using (var arena = LegacyArenaView.Create(parent.transform, art, settings))
            using (var hud = new LegacyCombatHud(parent.transform, art, null, null, null))
            {
                try
                {
                    LegacySkill fast = Attack(98961, 1), normal = Attack(98962, 2);
                    var duel = new LegacyQueuedDuel(10000, 10000, 10000, 10000,
                        new[] { fast, normal }, new[] { normal }, new[] { 1 }, 7, meshPercent: 0);
                    Assert.That(duel.TrySetFixedQueues(new[] { fast, fast, normal }, new[] { normal, normal }), Is.True);
                    Color originalBackdrop = arena.ArenaCamera.backgroundColor;
                    duel.Commit();
                    LegacyCurrentSlot first = duel.BeginNextSlot();
                    Begin(arena, first);
                    arena.SetFastSkillFocus(true);
                    Frame(arena, .004f, .01f, .07f);
                    Assert.That(arena.FastSkillFocusAmount, Is.GreaterThan(.9f));
                    Assert.That(arena.SlowMotionBackdropFocus, Is.EqualTo(arena.FastSkillFocusAmount));
                    Assert.That(Brightness(arena.ArenaCamera.backgroundColor), Is.LessThan(Brightness(originalBackdrop)));
                    Assert.That(arena.StepPresentationSpeed, Is.EqualTo(1f), "A visual focus must not re-slow the fast actor's clock.");
                    Assert.That(ActorTime(arena, true), Is.EqualTo(.01f).Within(.00001f));
                    Assert.That(ActorTime(arena, false), Is.LessThan(ActorTime(arena, true)));
                    hud.BeginCombat();
                    hud.SetCombatLetterboxAccent(arena.CombatLetterboxAccent);
                    hud.Refresh(duel, 10f, true, first.SlotIndex, arena.ArenaCamera,
                        arena.PlayerRenderer.transform, arena.EnemyRenderer.transform, .01f, .15f);
                    Assert.That(hud.CombatLetterboxInset, Is.GreaterThan(0f),
                        "Fast focus reuses the existing bands even while ordinary camera motion is disabled.");

                    float focus = arena.FastSkillFocusAmount, fastPose = ActorTime(arena, true);
                    arena.SetFastSkillFocus(true);
                    Frame(arena, 0f, 0f, .03f);
                    Assert.That(arena.FastSkillFocusAmount, Is.GreaterThanOrEqualTo(focus), "Hit stop does not erase the cue.");
                    Assert.That(ActorTime(arena, true), Is.EqualTo(fastPose));
                    Finish(duel);
                    LegacyCurrentSlot second = duel.BeginNextSlot();
                    focus = arena.FastSkillFocusAmount;
                    Begin(arena, second);
                    Assert.That(arena.FastSkillFocusAmount, Is.EqualTo(focus), "Consecutive fast actions do not flash the backdrop bright.");
                    arena.SetFastSkillFocus(true);
                    Frame(arena, .004f, .01f, .01f);
                    Assert.That(arena.FastSkillFocusAmount, Is.GreaterThan(.9f));
                    Finish(duel);
                    LegacyCurrentSlot ordinary = duel.BeginNextSlot();
                    Begin(arena, ordinary);
                    arena.SetFastSkillFocus(false);
                    Frame(arena, 0f, 0f, .5f);
                    Assert.That(arena.FastSkillFocusAmount, Is.Zero);

                    arena.SetFastSkillFocus(true);
                    Frame(arena, 0f, 0f, .07f);
                    Assert.That(arena.FastSkillFocusAmount, Is.GreaterThan(0f));
                    typeof(LegacyArenaView).GetMethod("SuspendForCutscene", Private).Invoke(arena, null);
                    Assert.That(arena.FastSkillFocusAmount, Is.Zero);
                    Assert.That(Brightness(arena.ArenaCamera.backgroundColor), Is.EqualTo(Brightness(originalBackdrop)).Within(.001f));
                    arena.SetFastSkillFocus(true);
                    typeof(LegacyArenaView).GetMethod("ResumeAfterCutscene", Private).Invoke(arena, null);
                    Frame(arena, 0f, 0f, .07f);
                    Assert.That(arena.FastSkillFocusAmount, Is.Zero);
                    arena.Reset();
                    Assert.That(arena.FastSkillFocusAmount, Is.Zero);
                    arena.BeginSlot(fast, normal);
                    arena.SetFastSkillFocus(true);
                    Frame(arena, 0f, 0f, .07f);
                    arena.EndTurn();
                    Assert.That(arena.FastSkillFocusAmount, Is.Zero);
                }
                finally { Object.Destroy(settings); Object.Destroy(parent); }
            }
        }

        [UnityTest]
        public IEnumerator ExistingStepFocus_KeepsItsOwnClockAndUsesTheStrongerBackdropCue()
        {
            yield return null;
            var parent = new GameObject("Fast And Step Focus Test");
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            using (var art = new LegacyDuelArt())
            using (var arena = LegacyArenaView.Create(parent.transform, art, settings))
            {
                try
                {
                    arena.BeginSlot(Attack(98971, 1), Attack(98972, 2));
                    arena.PerformStep(LegacyStepAction.Pressure, true, 1);
                    float stepSpeed = arena.StepPresentationSpeed;
                    Assert.That(stepSpeed, Is.LessThan(1f));
                    arena.SetFastSkillFocus(true);
                    Frame(arena, .001f, .001f, .01f);
                    Assert.That(arena.FastSkillFocusAmount, Is.GreaterThan(0f));
                    Assert.That(arena.StepPresentationSpeed, Is.EqualTo(stepSpeed).Within(.0001f),
                        "The fast visual request cannot replace a successful step's existing combat clock.");
                    Assert.That(arena.SlowMotionBackdropFocus,
                        Is.EqualTo(Mathf.Max(arena.StepFocusAmount, arena.FastSkillFocusAmount)).Within(.0001f));
                    Assert.That(arena.SlowMotionBackdropFocus, Is.LessThanOrEqualTo(1f));
                    arena.EndTurn();
                    Assert.That(arena.FastSkillFocusAmount, Is.Zero);
                    Assert.That(arena.StepPresentationSpeed, Is.EqualTo(1f));
                }
                finally { Object.Destroy(settings); Object.Destroy(parent); }
            }
        }

        private static Type EnvelopeType => typeof(LegacyArenaView).Assembly.GetType("TurnLimbo.Presentation.DuelFastSkillFocusEnvelope", true);
        private static object NewEnvelope() => Activator.CreateInstance(EnvelopeType, true);
        private static float Amount(object envelope) => (float)EnvelopeType.GetProperty("Amount").GetValue(envelope);
        private static void Request(object envelope, bool active) => EnvelopeType.GetMethod("Request").Invoke(envelope, new object[] { active });
        private static void Tick(object envelope, float realDelta, bool allowed)
            => EnvelopeType.GetMethod("Tick").Invoke(envelope, new object[] { realDelta, allowed });
        private static float Brightness(Color color) => color.r + color.g + color.b;
        private static float ActorTime(LegacyArenaView arena, bool player)
        {
            object actor = typeof(LegacyArenaView).GetField(player ? "player" : "enemy", Private).GetValue(arena);
            return (float)actor.GetType().GetField("AnimationTime").GetValue(actor);
        }
        private static LegacySkill Attack(int id, int duration) => new LegacySkill(id, "Focus Test", 0, 1, 1,
            LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "", durationHalfUnits: duration);
        private static void Begin(LegacyArenaView arena, LegacyCurrentSlot slot)
        {
            typeof(LegacyArenaView).GetMethod("ConfigureSlotTiming", Private).Invoke(arena, new object[] { 1f, 0f });
            typeof(LegacyArenaView).GetMethod("BeginTempoSlot", Private).Invoke(arena, new object[] { slot, null, 0f });
        }
        private static void Frame(LegacyArenaView arena, float worldDelta, float actionDelta, float realDelta)
            => typeof(LegacyArenaView).GetMethod("TickSkillTempo", Private).Invoke(arena, new object[] { worldDelta, actionDelta, realDelta, true });
        private static void Finish(LegacyQueuedDuel duel)
        {
            while (!duel.CurrentSlot.IsResolved) duel.ResolveNextHit();
            duel.CompleteCurrentSlot();
        }
    }
}
