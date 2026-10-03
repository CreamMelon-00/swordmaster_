using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class SwordGirlReactionPlayModeTests
    {
        private static readonly LegacySkill ThreeHitThrust = new LegacySkill(903, "Three-hit thrust", 1, 6, 6,
            LegacySkillKind.Attack, LegacySkillProperty.Penetrate, 3, 1, string.Empty);

        [Test]
        public void StaticPosesShareImportSettingsAndRuntimeGroundOrigin()
        {
            using (var set = new MobStudentAnimationSet())
            {
                Assert.That(set.HasRequiredAssets, Is.True, string.Join(", ", set.MissingResources));
                Assert.That(set.LoadedSpriteCount, Is.EqualTo(MobStudentAnimationSet.RequiredSpriteCount));
                foreach (string key in new[] { "block", "block-2", "hurt", "hurt-2" })
                {
                    var source = Resources.Load<Sprite>(MobStudentAnimationSet.ResourceRoot + "poses/" + key);
                    int variant = key.EndsWith("-2") ? 1 : 0;
                    var adapted = key.StartsWith("block") ? set.GetBlockPose(variant) : set.GetHurtPose(variant);
                    Assert.That(source, Is.Not.Null);
                    Assert.That(source.rect.size, Is.EqualTo(new Vector2(256, 224)));
                    Assert.That(source.pivot, Is.EqualTo(new Vector2(101, 22)));
                    Assert.That(source.pixelsPerUnit, Is.EqualTo(40));
                    Assert.That(source.texture.filterMode, Is.EqualTo(FilterMode.Point));
                    Assert.That(source.texture.mipmapCount, Is.EqualTo(1));
                    Assert.That(adapted.texture, Is.SameAs(source.texture));
                    Assert.That((source.pivot.y - adapted.pivot.y) / adapted.pixelsPerUnit,
                        Is.EqualTo(MobStudentAnimationSet.GroundOffset).Within(.0001f));
                }
                Assert.That(set.GetBlockPose(), Is.Not.SameAs(set.GetHurtPose()));
                Assert.That(set.GetBlockPose(0), Is.Not.SameAs(set.GetBlockPose(1)));
                Assert.That(set.GetHurtPose(0), Is.Not.SameAs(set.GetHurtPose(1)));
                set.Dispose();
                Assert.That(set.GetBlockPose(), Is.Null);
                Assert.That(set.GetHurtPose(), Is.Null);
            }
        }

        [UnityTest]
        public IEnumerator DefenseHoldsBetweenEnemyHitsAndEndsWithItsSlot()
        {
            using (var scope = new ArenaScope())
            {
                var a = scope.Arena;
                a.BeginSlot(LegacySkillDefinitions.Skill(7), ThreeHitThrust);
                var block = a.PlayerRenderer.sprite;
                Assert.That(block.name, Is.EqualTo("poses-block"));
                for (int i = 0; i < 12; i++)
                {
                    a.Tick(.1f, .1f);
                    Assert.That(a.PlayerRenderer.sprite, Is.SameAs(block));
                }
                a.EndTurn(); a.Tick(0, 0);
                Assert.That(a.PlayerRenderer.sprite.name, Does.StartWith("idle-frame-"));
                Assert.That(a.PlayerLowerRenderer.enabled, Is.False);
                Assert.That(a.PlayerRenderer.flipX, Is.False);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnemyDamageReactsImmediatelyFreezesInHitStopAndRecovers()
        {
            using (var scope = new ArenaScope())
            {
                var a = scope.Arena;
                a.BeginSlot(null, LegacySkillDefinitions.Skill(1));
                a.PresentHit(false, 4, 0, false, false);
                Sprite hurt = a.PlayerRenderer.sprite;
                Assert.That(hurt.name, Does.StartWith("poses-hurt"));
                a.Tick(0, .5f);
                Assert.That(a.PlayerRenderer.sprite, Is.SameAs(hurt), "Hit stop must hold the damaged pose.");
                a.Tick(LegacyArenaView.ReactionPoseDuration - .01f, 0);
                Assert.That(a.PlayerRenderer.sprite, Is.SameAs(hurt));
                a.Tick(.02f, 0);
                Assert.That(a.PlayerRenderer.sprite.name, Does.StartWith("idle-frame-"));
                a.PresentHit(false, 0, 3, false, false);
                Assert.That(a.PlayerRenderer.sprite.name, Does.StartWith("poses-hurt"), "Unblocked resistance damage is a hit too.");
                a.Reset();
                Assert.That(a.PlayerRenderer.sprite.name, Is.EqualTo("idle-frame-01"));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SuccessfulGuardBlocksButHpPenetrationAndGuardBreakHurt()
        {
            using (var scope = new ArenaScope())
            {
                var a = scope.Arena;
                a.BeginSlot(LegacySkillDefinitions.Skill(7), LegacySkillDefinitions.Skill(1));
                a.PresentHit(false, 0, 0, true, false, 4);
                Assert.That(a.PlayerRenderer.sprite.name, Does.StartWith("poses-block"));
                a.PresentHit(false, 0, 3, true, false, 4);
                Assert.That(a.PlayerRenderer.sprite.name, Does.StartWith("poses-block"));
                a.PresentHit(false, 2, 0, true, false);
                Assert.That(a.PlayerRenderer.sprite.name, Does.StartWith("poses-hurt"));
                a.Tick(LegacyArenaView.ReactionPoseDuration + .01f, 0);
                Assert.That(a.PlayerRenderer.sprite.name, Does.StartWith("poses-block"));
                a.PresentHit(false, 0, 3, true, true);
                Assert.That(a.PlayerRenderer.sprite.name, Does.StartWith("poses-hurt"));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator OwnAttackAndZeroDamageDoNotHurtPlayer_RepeatedHitsRefreshReaction()
        {
            using (var scope = new ArenaScope())
            {
                var a = scope.Arena;
                a.BeginSlot(LegacySkillDefinitions.Skill(1), null);
                Sprite attack = a.PlayerRenderer.sprite;
                a.PresentHit(true, 4, 0, false, false);
                Assert.That(a.PlayerRenderer.sprite, Is.SameAs(attack));
                a.PresentHit(false, 0, 0, false, false, 0);
                Assert.That(a.PlayerRenderer.sprite, Is.SameAs(attack));
                a.PresentHit(false, 1, 0, false, false);
                a.Tick(.12f, 0);
                a.PresentHit(false, 1, 0, false, false);
                a.Tick(.1f, 0);
                Assert.That(a.PlayerRenderer.sprite.name, Does.StartWith("poses-hurt"));
                a.BeginSlot(LegacySkillDefinitions.Skill(1), null);
                Assert.That(a.PlayerRenderer.sprite.name, Does.Match("^slash(?:-[23])?-frame-01$"));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SlowPlaybackScalesReactionWithoutChangingGlobalTime()
        {
            using (var scope = new ArenaScope())
            {
                var a = scope.Arena;
                typeof(LegacyArenaView).GetMethod("ConfigureSlotTiming", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(a, new object[] { .5f, .1f });
                a.BeginSlot(null, LegacySkillDefinitions.Skill(1));
                float timeScale = Time.timeScale;
                a.PresentHit(false, 2, 0, false, false);
                a.Tick(.3f, 0);
                Assert.That(a.PlayerRenderer.sprite.name, Does.StartWith("poses-hurt"));
                a.Tick(.03f, 0);
                Assert.That(a.PlayerRenderer.sprite.name, Does.StartWith("idle-frame-"));
                Assert.That(Time.timeScale, Is.EqualTo(timeScale));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryIncomingHitAndGuardDrawsAgainButSamplingNeverRerolls()
        {
            var random = new PoseRandom(0, 1, 1, 0, 0, 1, 1, 0);
            using (var scope = new ArenaScope(random))
            {
                var a = scope.Arena;
                int calls = 0;
                foreach (bool guard in new[] { true, false })
                {
                    a.BeginSlot(guard ? LegacySkillDefinitions.Skill(7) : null, ThreeHitThrust);
                    Assert.That(random.Calls, Is.EqualTo(calls), "Entering the slot must not consume a hit/guard draw.");
                    foreach (int choice in new[] { 0, 1, 1, 0 })
                    {
                        a.PresentHit(false, guard ? 0 : 1, 0, guard, false, 1);
                        calls++;
                        string expected = (guard ? "poses-block" : "poses-hurt") + (choice == 0 ? "" : "-2");
                        Assert.That(random.Calls, Is.EqualTo(calls));
                        Assert.That(a.PlayerRenderer.sprite.name, Is.EqualTo(expected));
                        Sprite selected = a.PlayerRenderer.sprite;
                        for (int frame = 0; frame < 6; frame++) a.Tick(0f, .02f);
                        Assert.That(a.PlayerRenderer.sprite, Is.SameAs(selected));
                        Assert.That(random.Calls, Is.EqualTo(calls), "Hit stop / rendering must not draw again.");
                        if (guard)
                        {
                            a.Tick(LegacyArenaView.ReactionPoseDuration + .01f, 0);
                            Assert.That(a.PlayerRenderer.sprite, Is.SameAs(selected), "Retain the last guard between impacts.");
                            Assert.That(random.Calls, Is.EqualTo(calls));
                        }
                    }
                }
                a.PresentHit(true, 3, 0, false, false);
                a.PresentHit(false, 0, 0, false, false, 0);
                Assert.That(random.Calls, Is.EqualTo(8), "Outgoing hits and misses do not draw player reactions.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClashesKeepStrikes_FinishedAttacksBlock_AndBodyHitsStillHurt()
        {
            using (var scope = new ArenaScope(new PoseRandom(1, 0)))
            {
                var a = scope.Arena;
                a.BeginSlot(LegacySkillDefinitions.Skill(1), ThreeHitThrust);
                Hold(a, LegacyArenaView.OriginalImpactTime + .0001f);
                Sprite contact = a.PlayerRenderer.sprite;
                Sprite enemyContact = a.EnemyRenderer.sprite;
                a.PresentHit(false, 0, 3, false, false, 3, LegacyArenaView.HitExchange.MutualClash);
                a.PresentHit(true, 0, 4, false, false, 4, LegacyArenaView.HitExchange.MutualClash);
                Assert.That(a.PlayerRenderer.sprite, Is.SameAs(contact), "Both striking: the contact frames are the clash.");
                Assert.That(a.EnemyRenderer.sprite, Is.SameAs(enemyContact));

                Hold(a, LegacyArenaView.OriginalClipDuration + LegacyArenaView.OriginalImpactTime);
                Assert.That(a.PlayerRenderer.sprite.name, Is.EqualTo("poses-block"));
                a.PresentHit(false, 0, 3, false, false, 3, LegacyArenaView.HitExchange.BladeBlock);
                Assert.That(a.PlayerRenderer.sprite.name, Is.EqualTo("poses-block-2"),
                    "The finished attack receives a resistance-only strike on its blade.");
                a.Tick(LegacyArenaView.ReactionPoseDuration + .01f, 0);
                Assert.That(a.PlayerRenderer.sprite.name, Is.EqualTo("poses-block-2"),
                    "Retain the last guard while the opponent is still striking.");
                a.PresentHit(false, 2, 1, false, false, 3, LegacyArenaView.HitExchange.BladeBlock);
                Assert.That(a.PlayerRenderer.sprite.name, Is.EqualTo("poses-hurt"), "Resistance overflow reaches the body.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RemadeEnemyReactsWithoutConsumingPlayerCosmeticDraws()
        {
            using (var scope = new ArenaScope(new PoseRandom()))
            {
                var a = scope.Arena;
                a.BeginSlot(ThreeHitThrust, LegacySkillDefinitions.Skill(1));
                Hold(a, LegacyArenaView.OriginalImpactTime + .0001f);
                a.PresentHit(true, 0, 1, false, false, 1, LegacyArenaView.HitExchange.MutualClash);
                Assert.That(a.EnemyRenderer.sprite.name, Does.Match("^enemy-slash(?:-[23])?-frame-05$"));
                Hold(a, LegacyArenaView.OriginalClipDuration + LegacyArenaView.OriginalImpactTime);
                Assert.That(a.EnemyRenderer.sprite.name, Does.Match("^enemy-poses-block(?:-2)?$"));
                a.PresentHit(true, 0, 1, false, false, 1, LegacyArenaView.HitExchange.BladeBlock);
                a.PresentHit(true, 3, 0, false, false, 3);
                Assert.That(a.EnemyRenderer.sprite.name, Does.Match("^enemy-poses-hurt(?:-2)?$"),
                    "HP damage now displays the enemy hurt pose.");

                a.BeginSlot(LegacySkillDefinitions.Skill(1), null);
                Hold(a, LegacyArenaView.OriginalImpactTime + .0001f);
                a.PresentHit(true, 0, 0, true, false, 2);
                Assert.That(a.EnemyRenderer.sprite.name, Does.Match("^enemy-poses-block(?:-2)?$"), "A guard reaction uses the guard frame.");
                a.Tick(LegacyArenaView.ReactionPoseDuration + .01f, 0);
                Assert.That(a.EnemyRenderer.sprite.name, Does.StartWith("enemy-idle-frame-"));

                typeof(LegacyArenaView).GetMethod("ConfigureSlotTiming", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(a, new object[] { .1f, 0f });
                a.BeginSlot(ThreeHitThrust, LegacySkillDefinitions.Skill(1));
                Hold(a, LegacyArenaView.OriginalClipDuration / .1f + .01f);
                a.PresentHit(true, 0, 1, false, false, 1, LegacyArenaView.HitExchange.BladeBlock);
                a.EndTurn(); a.Tick(0, 0);
                Assert.That(a.EnemyRenderer.sprite.name, Does.StartWith("enemy-idle-frame-"),
                    "Slow playback must not carry the last block reaction into planning.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SuccessfulDodgeLeavesNoExchangeToGuard()
        {
            using (var scope = new ArenaScope())
            {
                var a = scope.Arena;
                float tail = LegacyArenaView.OriginalClipDuration + LegacyArenaView.OriginalImpactTime;
                a.BeginSlot(LegacySkillDefinitions.Skill(1), ThreeHitThrust);
                a.PerformStep(LegacyStepAction.Dodge, true);
                Hold(a, tail);
                Assert.That(a.PlayerRenderer.sprite.name, Does.StartWith("idle-frame-"),
                    "The dodge voided the opponent's remaining hits, so the finished attack has nothing to receive.");
                a.BeginSlot(LegacySkillDefinitions.Skill(1), ThreeHitThrust);
                Hold(a, tail);
                Assert.That(a.PlayerRenderer.sprite.name, Is.EqualTo("poses-block"), "A new slot starts without the previous dodge.");
            }
            yield return null;
        }

        private static void Hold(LegacyArenaView arena, float elapsed) =>
            typeof(LegacyArenaView).GetMethod("HoldSlotAtTime", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(arena, new object[] { elapsed });

        [UnityTest]
        public IEnumerator ArenaCameraRendersAllFourReactionPoses()
        {
            yield return null;
            var controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            bool enabled = controller.enabled;
            controller.RestartMatch(); controller.enabled = false;
            var existingRoot = controller.ArenaView.PlayerRenderer.transform.parent.gameObject;
            bool rootActive = existingRoot.activeSelf;
            existingRoot.SetActive(false);
            var scope = new ArenaScope(new PoseRandom(0, 1, 0, 1));
            var a = scope.Arena;
            var camera = a.ArenaCamera;
            var target = new RenderTexture(1600, 900, 24);
            var previous = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                foreach (string key in new[] { "block", "block-2", "hurt", "hurt-2" })
                {
                    bool guard = key.StartsWith("block");
                    a.Reset(); a.CloseDistance(1f);
                    a.BeginSlot(guard ? LegacySkillDefinitions.Skill(7) : null, LegacySkillDefinitions.Skill(1));
                    a.Tick(0, 1f);
                    typeof(LegacyArenaView).GetMethod("HoldSlotAtTime", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(a, new object[] { LegacyArenaView.OriginalImpactTime });
                    a.PresentHit(false, guard ? 0 : 3, 0, guard, false, 3);
                    Assert.That(a.PlayerRenderer.sprite.name, Is.EqualTo("poses-" + key));
                    yield return null; yield return null;
                    string directory = Environment.GetEnvironmentVariable("SWORDGIRL_REACTION_CAPTURE_DIR");
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                        var active = RenderTexture.active;
                        var capture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                        try
                        {
                            RenderTexture.active = target;
                            capture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); capture.Apply();
                            File.WriteAllBytes(Path.Combine(directory, "unity-" + key + ".png"), capture.EncodeToPNG());
                        }
                        finally { RenderTexture.active = active; Object.Destroy(capture); }
                    }
                }
            }
            finally
            {
                camera.targetTexture = previous; target.Release(); Object.Destroy(target);
                scope.Dispose(); existingRoot.SetActive(rootActive);
                controller.RestartMatch(); controller.enabled = enabled;
            }
        }

        private sealed class ArenaScope : IDisposable
        {
            private readonly GameObject host = new GameObject("SwordGirl Reaction Test");
            public LegacyArenaView Arena { get; }
            public ArenaScope(System.Random random = null) { Arena = LegacyArenaView.Create(host.transform, new LegacyDuelArt(), null, random); }
            public void Dispose() { Arena.Dispose(); Object.Destroy(host); }
        }

        private sealed class PoseRandom : System.Random
        {
            private readonly int[] choices;
            public int Calls { get; private set; }
            public PoseRandom(params int[] choices) { this.choices = choices; }
            public override int Next(int maxValue)
            {
                Assert.That(maxValue, Is.EqualTo(2));
                Assert.That(Calls, Is.LessThan(choices.Length), "Unexpected extra random selection.");
                return choices[Calls++];
            }
        }
    }
}
