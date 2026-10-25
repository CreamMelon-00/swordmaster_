using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class HurtReactionArenaPlayModeTests
    {
        private static readonly LegacySkill Attack = new LegacySkill(980, "Reaction test", 1, 4, 4,
            LegacySkillKind.Attack, LegacySkillProperty.Hit, 1, 0, string.Empty);
        private static readonly EnemyAppearance[] Appearances =
        {
            EnemyAppearance.Student, EnemyAppearance.Elisa, EnemyAppearance.CadetA,
            EnemyAppearance.CadetB, EnemyAppearance.TrainingDummy
        };

        [UnityTest]
        public IEnumerator SuppressedBodyHitsKeepTheAttackAndAllOtherImpactFeedback()
        {
            using (var scope = new ArenaScope())
            {
                foreach (bool playerTarget in new[] { true, false })
                    foreach (EnemyAppearance appearance in Appearances)
                    {
                        if (playerTarget && appearance != EnemyAppearance.Student) continue;
                        var arena = scope.Arena;
                        Prepare(arena, appearance);
                        SpriteRenderer target = playerTarget ? arena.PlayerRenderer : arena.EnemyRenderer;
                        Sprite contact = target.sprite;
                        Vector3 before = target.transform.localPosition;
                        int draws = scope.Reactions.Calls;
                        arena.PresentHit(!playerTarget, 3, 2, false, false, 5,
                            LegacyArenaView.HitExchange.MutualClash, hurtReaction: false);
                        Assert.That(target.sprite, Is.SameAs(contact), target.name + " retains its own attack on HP overflow too.");
                        Assert.That(scope.Reactions.Calls, Is.EqualTo(draws), "A suppressed hurt pose does not draw an unused variant.");
                        Assert.That(arena.ImpactGlow.ActiveCount, Is.GreaterThan(0));
                        arena.Tick(.05f, .05f);
                        Assert.That(target.sprite.name, Does.Not.Contain("hurt"));
                        Assert.That(target.color.g, Is.LessThan(target.color.r), "The red body-hit flash is retained.");
                        Assert.That(arena.ActiveParticleCount, Is.GreaterThan(0), "Impact sparks are independent of flinching.");
                        if (appearance != EnemyAppearance.TrainingDummy || playerTarget)
                            Assert.That(Vector3.Distance(target.transform.localPosition, before), Is.GreaterThan(.01f),
                                "Blunt-hit knockback is retained.");
                        else Assert.That(target.transform.localPosition, Is.EqualTo(before), "The dummy retains its fixed stand.");
                    }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExplicitWeakHitCanRecoilOnResistanceDamage_AndASubsequentBalancedHitClearsIt()
        {
            using (var scope = new ArenaScope())
            {
                foreach (bool playerTarget in new[] { true, false })
                    foreach (EnemyAppearance appearance in Appearances)
                    {
                        if (playerTarget && appearance != EnemyAppearance.Student) continue;
                        foreach (LegacyArenaView.HitExchange exchange in new[]
                            { LegacyArenaView.HitExchange.MutualClash, LegacyArenaView.HitExchange.BladeBlock })
                        {
                            var arena = scope.Arena;
                            Prepare(arena, appearance);
                            SpriteRenderer target = playerTarget ? arena.PlayerRenderer : arena.EnemyRenderer;
                            Sprite contact = target.sprite;
                            arena.PresentHit(!playerTarget, 0, 4, false, false, 4, exchange, hurtReaction: true);
                            Assert.That(target.sprite.name, Does.Contain("hurt"), "A weaker attack can flinch without HP damage.");
                            int draws = scope.Reactions.Calls;
                            arena.Tick(0f, .03f);
                            Assert.That(target.sprite.name, Does.Contain("hurt"), "Hit stop holds the chosen reaction.");
                            arena.PresentHit(!playerTarget, 1, 0, false, false, 1,
                                LegacyArenaView.HitExchange.MutualClash, hurtReaction: false);
                            Assert.That(target.sprite, Is.SameAs(contact), "A prior flinch cannot leak into a balanced contact.");
                            Assert.That(scope.Reactions.Calls, Is.EqualTo(draws));
                            if (!playerTarget && appearance == EnemyAppearance.TrainingDummy)
                                Assert.That(arena.IsEnemyHurtPlaying, Is.False);
                            arena.Tick(0f, .03f);
                            Assert.That(target.sprite, Is.SameAs(contact), "The cleared reaction stays cleared during hit stop.");
                        }
                    }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SuccessfulGuardsRemainGuardsAndSuppressedDamageReturnsToTheHeldGuard()
        {
            using (var scope = new ArenaScope())
            {
                foreach (bool playerTarget in new[] { true, false })
                    foreach (EnemyAppearance appearance in Appearances)
                    {
                        if (appearance == EnemyAppearance.TrainingDummy || playerTarget && appearance != EnemyAppearance.Student) continue;
                        var arena = scope.Arena;
                        arena.SetEnemyAppearance(appearance);
                        arena.Reset();
                        LegacySkill defense = LegacySkillDefinitions.Skill(7);
                        arena.BeginSlot(playerTarget ? defense : Attack, playerTarget ? Attack : defense);
                        SpriteRenderer target = playerTarget ? arena.PlayerRenderer : arena.EnemyRenderer;
                        foreach (bool hurtReaction in new[] { true, false })
                        {
                            arena.PresentHit(!playerTarget, 0, 3, true, false, 3,
                                LegacyArenaView.HitExchange.None, hurtReaction: hurtReaction);
                            Assert.That(target.sprite.name, playerTarget ? (NUnit.Framework.Constraints.Constraint)Does.Match("^final-guard-0[1-3]-frame-00$")
                                : Does.Contain("block"), "Successful guard has its own reaction.");
                        }
                        arena.PresentHit(!playerTarget, 0, 3, true, true, 3, hurtReaction: true);
                        Assert.That(target.sprite.name, playerTarget ? (NUnit.Framework.Constraints.Constraint)Does.StartWith("final-hurt-frame-")
                            : Does.Contain("hurt"), "A broken guard can still recoil on resistance damage.");
                        arena.PresentHit(!playerTarget, 1, 0, true, false, 1, hurtReaction: false);
                        Assert.That(target.sprite.name, playerTarget ? (NUnit.Framework.Constraints.Constraint)Does.Match("^final-guard-0[1-3]-frame-00$")
                            : Does.Contain("block"), "Clearing the flinch restores the held defense.");
                    }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator BrokenStateAloneDoesNotFabricateAHurtAnimationOrAnImpact()
        {
            using (var scope = new ArenaScope())
            {
                foreach (EnemyAppearance appearance in Appearances)
                {
                    var arena = scope.Arena;
                    arena.SetEnemyAppearance(appearance);
                    arena.Reset();
                    arena.BeginSlot(null, null);
                    arena.SetResistanceBroken(true, true);
                    arena.Tick(0f, 0f);
                    Assert.That(arena.PlayerRenderer.sprite.name, Does.Not.Contain("hurt"));
                    Assert.That(arena.EnemyRenderer.sprite.name, Does.Not.Contain("hurt"));
                    Assert.That(arena.ImpactGlow.ActiveCount, Is.Zero);
                    Assert.That(arena.ActiveParticleCount, Is.Zero);
                    Assert.That(arena.IsEnemyHurtPlaying, Is.False);
                    arena.PresentHit(true, 1, 0, false, false, 1, hurtReaction: true);
                    Assert.That(arena.EnemyRenderer.sprite.name, Does.Contain("hurt"), "Only an actual incoming hit flinches the broken target.");
                }
            }
            yield return null;
        }

        private static void Prepare(LegacyArenaView arena, EnemyAppearance appearance)
        {
            arena.SetEnemyAppearance(appearance);
            arena.Reset();
            arena.BeginSlot(Attack, Attack);
            typeof(LegacyArenaView).GetMethod("HoldSlotAtTime", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(arena, new object[] { LegacyArenaView.OriginalImpactTime });
        }

        private sealed class ArenaScope : IDisposable
        {
            private readonly GameObject host = new GameObject("Hurt reaction gate test");
            private readonly LegacyDuelArt art = new LegacyDuelArt();
            private readonly DuelPresentationSettings settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            public CountingZero Reactions { get; } = new CountingZero();
            public LegacyArenaView Arena { get; }
            public ArenaScope() => Arena = LegacyArenaView.Create(host.transform, art, settings,
                reactionPoseRandom: Reactions, enemyReactionPoseRandom: Reactions);
            public void Dispose()
            {
                Arena.Dispose();
                art.Dispose();
                Object.Destroy(settings);
                Object.Destroy(host);
            }
        }

        private sealed class CountingZero : System.Random
        {
            public int Calls { get; private set; }
            public override int Next(int maxValue) { Calls++; return 0; }
        }
    }
}
