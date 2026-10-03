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
    public sealed class EnemyReactionPlayModeTests
    {
        [Test]
        public void FourDistinctReactionAssetsShareDimensionsPaletteImportAndGroundOrigin()
        {
            using (var set = new EnemyStudentAnimationSet())
            {
                Assert.That(set.HasRequiredAssets, Is.True, string.Join(", ", set.MissingResources));
                Assert.That(set.LoadedSpriteCount, Is.EqualTo(EnemyStudentAnimationSet.RequiredSpriteCount));
                var textures = new System.Collections.Generic.HashSet<Texture2D>();
                foreach (string key in new[] { "block", "block-2", "hurt", "hurt-2" })
                {
                    int variant = key.EndsWith("-2") ? 1 : 0;
                    var source = Resources.Load<Sprite>(EnemyStudentAnimationSet.ResourceRoot + "poses/" + key);
                    var adapted = key.StartsWith("block") ? set.GetGuard(variant) : set.GetHurt(variant);
                    Assert.That(source, Is.Not.Null);
                    Assert.That(source.rect.size, Is.EqualTo(new Vector2(256, 224)));
                    Assert.That(source.pivot, Is.EqualTo(new Vector2(122, 22)));
                    Assert.That(source.pixelsPerUnit, Is.EqualTo(40));
                    Assert.That(source.texture.filterMode, Is.EqualTo(FilterMode.Point));
                    Assert.That(source.texture.mipmapCount, Is.EqualTo(1));
                    Assert.That(adapted.texture, Is.SameAs(source.texture));
                    Assert.That(adapted.pivot, Is.EqualTo(set.GetIdle(0).pivot));
                    Assert.That(adapted.name, Is.EqualTo("enemy-poses-" + key));
                    textures.Add(adapted.texture);
                }
                Assert.That(textures.Count, Is.EqualTo(4));
                Assert.That(set.GetGuard(-1), Is.SameAs(set.GetGuard(0)));
                Assert.That(set.GetHurt(2), Is.SameAs(set.GetHurt(0)));
                set.Dispose();
                Assert.That(set.GetGuard(1), Is.Null);
                Assert.That(set.GetHurt(1), Is.Null);
            }
        }

        [UnityTest]
        public IEnumerator EveryIncomingHitAndGuardDrawsOnceIncludingRepeatedChoices()
        {
            var random = new Choices(0, 1, 1, 0, 0, 1, 1, 0);
            using (var scope = new Scope(random))
            {
                var a = scope.Arena;
                int calls = 0;
                foreach (bool guard in new[] { true, false })
                {
                    a.BeginSlot(null, guard ? LegacySkillDefinitions.Skill(7) : null);
                    Assert.That(random.Calls, Is.EqualTo(calls), "Slot entry does not consume an impact draw.");
                    foreach (int choice in new[] { 0, 1, 1, 0 })
                    {
                        a.PresentHit(true, guard ? 0 : 1, 0, guard, false, 1);
                        string expected = "enemy-poses-" + (guard ? "block" : "hurt") + (choice == 0 ? "" : "-2");
                        Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(expected));
                        Assert.That(random.Calls, Is.EqualTo(++calls));
                        Sprite selected = a.EnemyRenderer.sprite;
                        for (int frame = 0; frame < 6; frame++) a.Tick(0, .02f);
                        Hold(a, .001f);
                        Assert.That(a.EnemyRenderer.sprite, Is.SameAs(selected));
                        Assert.That(random.Calls, Is.EqualTo(calls), "Hit stop and clock sampling must not reroll.");
                        if (guard)
                        {
                            a.Tick(LegacyArenaView.ReactionPoseDuration + .01f, 0);
                            Assert.That(a.EnemyRenderer.sprite, Is.SameAs(selected), "Hold the last guard between impacts.");
                            Assert.That(random.Calls, Is.EqualTo(calls));
                        }
                    }
                }
                a.PresentHit(true, 0, 0, false, false, 0);
                a.PresentHit(false, 1, 0, false, false, 1);
                Assert.That(random.Calls, Is.EqualTo(8), "Misses and outgoing hits do not draw enemy reactions.");
                Assert.That(scope.PlayerReactions.Calls, Is.EqualTo(1));
                a.Reset();
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo("enemy-idle-frame-01"));
                a.BeginSlot(null, LegacySkillDefinitions.Skill(7));
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo("enemy-poses-block"));
                Assert.That(random.Calls, Is.EqualTo(8));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator BladeBlocksBodyDamageFatalHitsAndSlowClockUseTheCorrectReaction()
        {
            var random = new Choices(1, 0, 1, 0);
            using (var scope = new Scope(random))
            {
                var a = scope.Arena;
                a.BeginSlot(LegacySkillDefinitions.Skill(3), LegacySkillDefinitions.Skill(1));
                Hold(a, LegacyArenaView.OriginalImpactTime);
                var contact = a.EnemyRenderer.sprite;
                a.PresentHit(true, 0, 1, false, false, 1, LegacyArenaView.HitExchange.MutualClash);
                Assert.That(a.EnemyRenderer.sprite, Is.SameAs(contact));
                Assert.That(random.Calls, Is.Zero);
                a.PresentHit(true, 0, 1, false, false, 1, LegacyArenaView.HitExchange.BladeBlock);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo("enemy-poses-block-2"));
                a.PresentHit(true, 2, 0, true, false, 1);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo("enemy-poses-hurt"));
                a.PresentHit(true, 0, 1, true, true, 1);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo("enemy-poses-hurt-2"));
                Assert.That(random.Calls, Is.EqualTo(3));
                Assert.That(scope.PlayerAttacks.Calls, Is.EqualTo(1));
                Assert.That(scope.EnemyAttacks.Calls, Is.EqualTo(1));
                Assert.That(scope.PlayerReactions.Calls, Is.Zero);
                a.BeginSlot(null, null);
                typeof(LegacyArenaView).GetMethod("ConfigureSlotTiming", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(a, new object[] { .5f, .1f });
                float timeScale = Time.timeScale;
                a.PresentHit(true, 0, 2, false, false, 1);
                a.Tick(.3f, 0);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo("enemy-poses-hurt"));
                a.Tick(.03f, 0);
                Assert.That(a.EnemyRenderer.sprite.name, Does.StartWith("enemy-idle-frame-"));
                Assert.That(Time.timeScale, Is.EqualTo(timeScale));
                Assert.That(random.Calls, Is.EqualTo(4));
                a.EndTurn(); a.Tick(0, 0);
                Assert.That(a.EnemyRenderer.sprite.name, Does.StartWith("enemy-idle-frame-"));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CameraRendersBothGuardsAndBothHurtPosesBesidePlayer()
        {
            yield return null;
            var controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            bool enabled = controller.enabled; controller.RestartMatch(); controller.enabled = false;
            var oldRoot = controller.ArenaView.PlayerRenderer.transform.parent.gameObject;
            bool active = oldRoot.activeSelf; oldRoot.SetActive(false);
            var scope = new Scope(new Choices(0, 1, 0, 1));
            var camera = scope.Arena.ArenaCamera;
            var target = new RenderTexture(1600, 900, 24);
            var previous = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                foreach (string key in new[] { "block", "block-2", "hurt", "hurt-2" })
                {
                    bool guard = key.StartsWith("block");
                    var a = scope.Arena;
                    a.Reset(); a.CloseDistance(1); a.BeginSlot(null, guard ? LegacySkillDefinitions.Skill(7) : null); a.Tick(0, 1);
                    a.PresentHit(true, guard ? 0 : 1, 0, guard, false, 1);
                    Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo("enemy-poses-" + key));
                    Assert.That(a.EnemyRenderer.flipX, Is.False);
                    yield return null; yield return null;
                    camera.Render();
                    string directory = Environment.GetEnvironmentVariable("ENEMY_REACTIONS_CAPTURE_DIR");
                    if (string.IsNullOrEmpty(directory)) continue;
                    Directory.CreateDirectory(directory);
                    var old = RenderTexture.active;
                    var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                    try
                    {
                        RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
                        File.WriteAllBytes(Path.Combine(directory, "unity-enemy-" + key + ".png"), image.EncodeToPNG());
                    }
                    finally { RenderTexture.active = old; Object.Destroy(image); }
                }
            }
            finally
            {
                camera.targetTexture = previous; target.Release(); Object.Destroy(target); scope.Dispose();
                oldRoot.SetActive(active); controller.RestartMatch(); controller.enabled = enabled;
            }
        }

        private static void Hold(LegacyArenaView arena, float time) => typeof(LegacyArenaView)
            .GetMethod("HoldSlotAtTime", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(arena, new object[] { time });
        private sealed class Scope : IDisposable
        {
            private readonly GameObject host = new GameObject("Enemy reaction test");
            private readonly LegacyDuelArt art = new LegacyDuelArt();
            public CountingZero PlayerReactions { get; } = new CountingZero();
            public CountingZero PlayerAttacks { get; } = new CountingZero();
            public CountingZero EnemyAttacks { get; } = new CountingZero();
            public LegacyArenaView Arena { get; }
            public Scope(System.Random enemy) => Arena = LegacyArenaView.Create(host.transform, art, null,
                PlayerReactions, PlayerAttacks, EnemyAttacks, enemy);
            public void Dispose() { Arena.Dispose(); art.Dispose(); Object.Destroy(host); }
        }
        private sealed class CountingZero : System.Random
        {
            public int Calls { get; private set; }
            public override int Next(int maxValue) { Calls++; return 0; }
        }
        private sealed class Choices : System.Random
        {
            private readonly int[] values;
            public int Calls { get; private set; }
            public Choices(params int[] values) => this.values = values;
            public override int Next(int maxValue)
            {
                Assert.That(maxValue, Is.EqualTo(2)); Assert.That(Calls, Is.LessThan(values.Length));
                return values[Calls++];
            }
        }
    }
}
