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
    public sealed class EnemyStudentAnimationPlayModeTests
    {
        [Test]
        public void CompleteAssetsHaveSharedDimensionsPivotAndImpactPhase()
        {
            using (var set = new EnemyStudentAnimationSet())
            {
                Assert.That(set.HasRequiredAssets, Is.True, string.Join(", ", set.MissingResources));
                Assert.That(set.LoadedSpriteCount, Is.EqualTo(118));
                Assert.That(set.GetIdle(0).name, Is.EqualTo("enemy-idle-frame-01"));
                Assert.That(set.GetIdle(.181f).name, Is.EqualTo("enemy-idle-frame-02"));
                Assert.That(set.GetIdle(1.26f), Is.SameAs(set.GetIdle(0)));
                foreach (var property in new[] { LegacySkillProperty.Slash, LegacySkillProperty.Penetrate, LegacySkillProperty.Hit })
                {
                    Sprite contact = set.GetAttack(property, .5f);
                    Assert.That(contact.name, Does.EndWith("frame-05"));
                    Assert.That(set.GetAttack(property, .499f).name, Does.EndWith("frame-04"));
                    Assert.That(set.GetAttack(property, 1).name, Does.EndWith("frame-12"));
                    Assert.That(contact.rect.size, Is.EqualTo(new Vector2(256, 224)));
                    Assert.That(contact.pivot, Is.EqualTo(set.GetIdle(0).pivot));
                    Assert.That(contact.pixelsPerUnit, Is.EqualTo(40));
                    Assert.That(contact.texture.filterMode, Is.EqualTo(FilterMode.Point));
                }
                set.Dispose();
                Assert.That(set.HasRequiredAssets, Is.False);
                Assert.That(set.GetIdle(0), Is.Null);
            }
        }

        [UnityTest]
        public IEnumerator EnemyRepeatsContactsOnEachHitAndHoldsOnStoppedClock()
        {
            var host = new GameObject("Enemy Motion Test");
            using (var art = new LegacyDuelArt())
            using (var arena = LegacyArenaView.Create(host.transform, art, null))
            {
                Assert.That(arena.HasEnemyStudentAnimations, Is.True);
                foreach (var property in new[] { LegacySkillProperty.Slash, LegacySkillProperty.Penetrate, LegacySkillProperty.Hit })
                {
                    arena.BeginSlot(null, new LegacySkill(901, "Enemy triple", 1, 1, 1,
                        LegacySkillKind.Attack, property, 3, 0, string.Empty));
                    for (int hit = 0; hit < 3; hit++)
                    {
                        Hold(arena, hit * LegacyArenaView.OriginalClipDuration + LegacyArenaView.OriginalImpactTime);
                        Assert.That(arena.EnemyRenderer.sprite.name, Does.EndWith("frame-05"));
                        var contact = arena.EnemyRenderer.sprite;
                        arena.Tick(0, .2f);
                        Assert.That(arena.EnemyRenderer.sprite, Is.SameAs(contact));
                    }
                }
                arena.Reset();
                Assert.That(arena.EnemyRenderer.sprite.name, Is.EqualTo("enemy-idle-frame-01"));
                arena.BeginSlot(null, LegacyInitialSkills.All[6]);
                Assert.That(arena.EnemyRenderer.sprite.name, Is.EqualTo("enemy-poses-block"));
                arena.Tick(1, 0);
                Assert.That(arena.EnemyRenderer.sprite.name, Is.EqualTo("enemy-poses-block"));
                arena.EndTurn(); arena.Tick(0, 0);
                Assert.That(arena.EnemyRenderer.sprite.name, Does.StartWith("enemy-idle-frame-"));
            }
            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CameraRendersNewEnemyIdleAllAttacksAndGuardBesidePlayer()
        {
            yield return null;
            var controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            bool enabled = controller.enabled;
            controller.RestartMatch(); controller.enabled = false;
            var arena = controller.ArenaView;
            var camera = arena.ArenaCamera;
            var target = new RenderTexture(1600, 900, 24);
            var previous = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                for (int i = 0; i < 5; i++)
                {
                    arena.Reset(); arena.CloseDistance(1f);
                    string key = new[] { "idle", "slash", "pierce", "blunt", "guard" }[i];
                    LegacySkill skill = i == 0 ? null : i == 4 ? LegacyInitialSkills.All[6] :
                        new LegacySkill(902, "Enemy capture", 1, 1, 1, LegacySkillKind.Attack,
                            new[] { LegacySkillProperty.Slash, LegacySkillProperty.Penetrate, LegacySkillProperty.Hit }[i - 1], 1, 0, string.Empty);
                    arena.BeginSlot(null, skill); arena.Tick(0, 1);
                    if (i > 0 && i < 4) Hold(arena, LegacyArenaView.OriginalImpactTime);
                    Assert.That(arena.EnemyRenderer.sprite.name, Does.StartWith("enemy-"));
                    Assert.That(arena.EnemyRenderer.flipX, Is.False);
                    yield return null; yield return null;
                    string directory = Environment.GetEnvironmentVariable("ENEMY_REMAKE_CAPTURE_DIR");
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                        var oldTarget = RenderTexture.active;
                        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                        try
                        {
                            RenderTexture.active = target;
                            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
                            File.WriteAllBytes(Path.Combine(directory, "unity-enemy-" + key + ".png"), image.EncodeToPNG());
                        }
                        finally { RenderTexture.active = oldTarget; Object.Destroy(image); }
                    }
                }
            }
            finally
            {
                camera.targetTexture = previous; target.Release(); Object.Destroy(target);
                controller.RestartMatch(); controller.enabled = enabled;
            }
        }

        private static void Hold(LegacyArenaView arena, float time) => typeof(LegacyArenaView)
            .GetMethod("HoldSlotAtTime", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(arena, new object[] { time });
    }
}
