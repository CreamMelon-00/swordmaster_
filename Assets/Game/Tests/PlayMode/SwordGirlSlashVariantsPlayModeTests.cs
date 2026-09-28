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
    public sealed class SwordGirlSlashVariantsPlayModeTests
    {
        [Test]
        public void ThreeSlashMotionsHaveDistinctArtAndShareTheContactPhase()
        {
            using (var set = new MobStudentAnimationSet())
            {
                Assert.That(set.HasRequiredAssets, Is.True, string.Join(", ", set.MissingResources));
                Assert.That(MobStudentAnimationSet.AttackVariationCount(LegacySkillProperty.Slash), Is.EqualTo(3));
                Assert.That(MobStudentAnimationSet.AttackVariationCount(LegacySkillProperty.Penetrate), Is.EqualTo(3));
                Assert.That(MobStudentAnimationSet.AttackVariationCount(LegacySkillProperty.Hit), Is.EqualTo(3));
                var textures = new System.Collections.Generic.HashSet<Texture2D>();
                for (int variant = 0; variant < 3; variant++)
                {
                    string key = MobStudentAnimationSet.AttackKey(LegacySkillProperty.Slash, variant);
                    Sprite contact = set.GetAttackUpper(LegacySkillProperty.Slash, variant, .5f);
                    Assert.That(contact.name, Is.EqualTo(key + "-frame-05"));
                    Assert.That(set.GetAttackUpper(LegacySkillProperty.Slash, variant, .499f).name, Is.EqualTo(key + "-frame-04"));
                    Assert.That(set.GetAttackUpper(LegacySkillProperty.Slash, variant, 1f).name, Is.EqualTo(key + "-frame-12"));
                    textures.Add(contact.texture);
                }
                Assert.That(textures.Count, Is.EqualTo(3));
            }
        }

        [UnityTest]
        public IEnumerator EachSlashHitSelectsOnceAndKeepsItsVariantAcrossClockHoldsAndRewinds()
        {
            var random = new SlashRandom(2, 0, 1, 1);
            using (var scope = new ArenaScope(random))
            {
                var a = scope.Arena;
                Configure(a, 1f, .1f);
                a.BeginSlot(Slash(3), null);
                Assert.That(random.Calls, Is.EqualTo(1));
                float cycle = LegacyArenaView.OriginalClipDuration + .1f;
                for (int hit = 0; hit < 3; hit++)
                {
                    string key = new[] { "slash-3", "slash", "slash-2" }[hit];
                    Hold(a, cycle * hit + .001f);
                    Assert.That(a.PlayerRenderer.sprite.name, Is.EqualTo(key + "-frame-01"));
                    Hold(a, cycle * hit + LegacyArenaView.OriginalImpactTime);
                    Assert.That(a.PlayerRenderer.sprite.name, Is.EqualTo(key + "-frame-05"));
                    Sprite contact = a.PlayerRenderer.sprite;
                    a.Tick(0f, .3f);
                    Assert.That(a.PlayerRenderer.sprite, Is.SameAs(contact));
                    Hold(a, cycle * hit + .001f);
                    Assert.That(a.PlayerRenderer.sprite.name, Is.EqualTo(key + "-frame-01"));
                    Assert.That(random.Calls, Is.EqualTo(hit + 1));
                }
                Hold(a, LegacyArenaView.OriginalImpactTime);
                Assert.That(a.PlayerRenderer.sprite.name, Is.EqualTo("slash-3-frame-05"));
                Assert.That(random.Calls, Is.EqualTo(3), "Rewinding to a cached hit must not reroll.");
                a.BeginSlot(Slash(1), null);
                Assert.That(a.PlayerRenderer.sprite.name, Is.EqualTo("slash-2-frame-01"));
                Assert.That(random.Calls, Is.EqualTo(4), "A new slot draws again and can repeat the previous variant.");
                a.Reset();
                Assert.That(a.PlayerRenderer.sprite.name, Is.EqualTo("idle-frame-01"));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DefenseDoesNotConsumeAttackSelections()
        {
            var random = new SlashRandom();
            using (var scope = new ArenaScope(random))
            {
                foreach (int index in new[] { 6, 7, 8 })
                {
                    scope.Arena.BeginSlot(LegacyInitialSkills.All[index], null);
                    scope.Arena.Tick(.05f, .05f);
                }
                Assert.That(random.Calls, Is.Zero);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArenaCameraRendersAllThreeSlashContacts()
        {
            yield return null;
            var controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            bool enabled = controller.enabled;
            controller.RestartMatch(); controller.enabled = false;
            var oldRoot = controller.ArenaView.PlayerRenderer.transform.parent.gameObject;
            bool active = oldRoot.activeSelf; oldRoot.SetActive(false);
            var scope = new ArenaScope(new SlashRandom(0, 1, 2));
            var camera = scope.Arena.ArenaCamera;
            var target = new RenderTexture(1600, 900, 24);
            var previous = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                for (int variant = 0; variant < 3; variant++)
                {
                    var a = scope.Arena;
                    a.Reset(); a.CloseDistance(1f); a.BeginSlot(Slash(1), null); a.Tick(0, 1f);
                    Hold(a, LegacyArenaView.OriginalImpactTime);
                    string key = MobStudentAnimationSet.AttackKey(LegacySkillProperty.Slash, variant);
                    Assert.That(a.PlayerRenderer.sprite.name, Is.EqualTo(key + "-frame-05"));
                    yield return null; yield return null;
                    string directory = Environment.GetEnvironmentVariable("SWORDGIRL_SLASH_CAPTURE_DIR");
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                        var oldTarget = RenderTexture.active;
                        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                        try
                        {
                            RenderTexture.active = target;
                            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
                            File.WriteAllBytes(Path.Combine(directory, "unity-" + key + ".png"), image.EncodeToPNG());
                        }
                        finally { RenderTexture.active = oldTarget; Object.Destroy(image); }
                    }
                }
            }
            finally
            {
                camera.targetTexture = previous; target.Release(); Object.Destroy(target);
                scope.Dispose(); oldRoot.SetActive(active); controller.RestartMatch(); controller.enabled = enabled;
            }
        }

        private static LegacySkill Slash(int count) => new LegacySkill(101, "Slash variants", 1, 1, 1,
            LegacySkillKind.Attack, LegacySkillProperty.Slash, count, 0, string.Empty);
        private static void Hold(LegacyArenaView arena, float time) => typeof(LegacyArenaView)
            .GetMethod("HoldSlotAtTime", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(arena, new object[] { time });
        private static void Configure(LegacyArenaView arena, float speed, float gap) => typeof(LegacyArenaView)
            .GetMethod("ConfigureSlotTiming", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(arena, new object[] { speed, gap });
        private sealed class ArenaScope : IDisposable
        {
            private readonly GameObject host = new GameObject("Slash Variant Test");
            public LegacyArenaView Arena { get; }
            public ArenaScope(System.Random random) { Arena = LegacyArenaView.Create(host.transform, new LegacyDuelArt(), null, null, random); }
            public void Dispose() { Arena.Dispose(); Object.Destroy(host); }
        }
        private sealed class SlashRandom : System.Random
        {
            private readonly int[] choices;
            public int Calls { get; private set; }
            public SlashRandom(params int[] choices) { this.choices = choices; }
            public override int Next(int maxValue)
            {
                Assert.That(maxValue, Is.EqualTo(3));
                Assert.That(Calls, Is.LessThan(choices.Length), "Unexpected additional random selection.");
                return choices[Calls++];
            }
        }
    }
}
