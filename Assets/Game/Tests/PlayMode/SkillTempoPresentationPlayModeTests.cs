using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class SkillTempoPresentationPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float Clip = LegacyArenaView.OriginalClipDuration;

        [TestCase(2, 1)]
        [TestCase(4, 2)]
        [TestCase(4, 1)]
        public void PendingSwing_ContinuesFromPreviousWindup_AndContactsOnlyInItsFinishingInterval(int duration, int segment)
        {
            int pendingTicks = duration - segment;
            float pendingEnd = Pose(Clip, 0f, 0, pendingTicks, duration, false);
            float continuingStart = Pose(0f, 0f, pendingTicks, segment, duration, true);
            Assert.That(pendingEnd, Is.EqualTo(continuingStart).Within(.000001f));
            Assert.That(pendingEnd, Is.LessThan(Clip * .5f));
            Assert.That(Pose(Clip * .5f, 0f, pendingTicks, segment, duration, true), Is.EqualTo(Clip * .5f).Within(.000001f));
            Assert.That(Pose(Clip, 0f, pendingTicks, segment, duration, true), Is.EqualTo(Clip).Within(.000001f));
        }

        [Test]
        public void UninterruptedCharge_GathersBeforeItsStrike_AndRetainsTheScheduledContact()
        {
            Assert.That(Pose(Clip * .5f, Clip, 0, 4, 4, true), Is.LessThan(Clip * .5f));
            Assert.That(Pose(Clip, Clip, 0, 4, 4, true), Is.EqualTo(Clip * .2f).Within(.000001f));
            Assert.That(Pose(Clip * 1.5f, Clip, 0, 4, 4, true), Is.EqualTo(Clip * .5f).Within(.000001f));
            Assert.That(Pose(Clip * 2f, Clip, 0, 4, 4, true), Is.EqualTo(Clip).Within(.000001f));
        }

        [UnityTest]
        public IEnumerator FastPair_KeepsTheOpponentsSingleSwingAndVariant_AcrossBothHits()
        {
            yield return null;
            var root = new GameObject("Skill Tempo Actor Test");
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            using (var art = new LegacyDuelArt())
            using (var arena = LegacyArenaView.Create(root.transform, art, settings))
            {
                try
                {
                    var fast = Attack(99871, 1);
                    var normal = Attack(99872, 2);
                    var duel = new LegacyQueuedDuel(1000, 1000, 1000, 1000,
                        new[] { fast }, new[] { normal }, new[] { normal.Id }, 7, meshPercent: 0);
                    Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(0), Is.True);
                    arena.PrepareSlotMovement(duel, 0);
                    Assert.That(arena.SlotRequiresContact, Is.True, "Planning previews still see their attacks before timing is committed.");
                    duel.Commit();
                    LegacyCurrentSlot first = duel.BeginNextSlot();
                    Begin(arena, first);
                    Hold(arena, Clip);
                    object actor = typeof(LegacyArenaView).GetField("enemy", Private).GetValue(arena);
                    float before = ActorTime(actor);
                    Sprite beforeSprite = arena.EnemyRenderer.sprite;
                    Assert.That(before, Is.LessThan(Clip * .5f));
                    duel.ResolveNextHit(); duel.CompleteCurrentSlot();
                    LegacyCurrentSlot second = duel.BeginNextSlot();
                    Begin(arena, second);
                    Assert.That(ActorTime(actor), Is.EqualTo(before).Within(.000001f), "The second fast technique cannot restart the opposing swing.");
                    Assert.That(arena.EnemyRenderer.sprite, Is.SameAs(beforeSprite), "The held swing keeps its selected animation variant.");
                    Hold(arena, Clip * .5f);
                    Assert.That(ActorTime(actor), Is.EqualTo(Clip * .5f).Within(.000001f));
                    arena.PresentHit(true, 3, 0, false, false, 0, closeUp: false, hurtReaction: true);
                    Assert.That(ActorTime(actor), Is.EqualTo(Clip * .5f).Within(.000001f));
                    Assert.That(arena.EnemyRenderer.sprite.name.ToLowerInvariant(), Does.Not.Contain("hurt"), "Damage does not interrupt the committed longer swing.");
                    arena.EndTurn();
                    arena.BeginSlot(normal, normal);
                    Hold(arena, Clip * .25f);
                    Assert.That(ActorTime(actor), Is.EqualTo(Clip * .25f).Within(.000001f), "Ordinary slots retain their original clock after a timed slot.");
                }
                finally { Object.Destroy(settings); Object.Destroy(root); }
            }
        }

        private static LegacySkill Attack(int id, int duration) => new LegacySkill(id, "시험", 1, 3, 3,
            LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, string.Empty, durationHalfUnits: duration);

        private static void Begin(LegacyArenaView arena, LegacyCurrentSlot slot)
        {
            typeof(LegacyArenaView).GetMethod("ConfigureSlotTiming", Private).Invoke(arena, new object[] { 1f, 0f });
            typeof(LegacyArenaView).GetMethod("BeginTempoSlot", Private).Invoke(arena, new object[] { slot, null, 0f });
        }

        private static void Hold(LegacyArenaView arena, float elapsed)
            => typeof(LegacyArenaView).GetMethod("HoldSlotAtTime", Private).Invoke(arena, new object[] { elapsed });

        private static float ActorTime(object actor) => (float)actor.GetType().GetField("AnimationTime").GetValue(actor);

        private static float Pose(float elapsed, float leadIn, int elapsedTicks, int segment, int duration, bool completes)
            => (float)typeof(LegacyArenaView).Assembly.GetType("TurnLimbo.Presentation.DuelSkillTempoPose")
                .GetMethod("Sample", BindingFlags.Static | BindingFlags.Public)
                .Invoke(null, new object[] { Clip, elapsed, leadIn, elapsedTicks, segment, duration, completes });
    }
}
