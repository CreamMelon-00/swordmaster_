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
    public sealed class FinalPlayerArtPlayModeTests
    {
        [Test]
        public void AllFramesImportAtConsistentScaleAndContactPhase()
        {
            using (var set = new FinalPlayerAnimationSet())
            {
                Assert.That(set.IsReady, Is.True);
                Assert.That(set.LoadedFrameCount, Is.EqualTo(111));
                Sprite idle = set.GetIdle(0);
                Assert.That(idle.pixelsPerUnit, Is.EqualTo(36));
                Assert.That(idle.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(idle.rect.size, Is.EqualTo(new Vector2(256, 224)));
                Assert.That(idle.pivot.y, Is.EqualTo(5 + 2.23f * 36).Within(.001));
                Assert.That(idle.pivot.x, Is.EqualTo(114).Within(.001), "The two planted boots span x=71..157; their midpoint anchors the ground circle.");
                var properties = new[] { LegacySkillProperty.Slash, LegacySkillProperty.Penetrate,
                    LegacySkillProperty.Hit, LegacySkillProperty.Hit, LegacySkillProperty.Hit, LegacySkillProperty.Hit };
                int[] ids = { 1, 2, 5, 6, 10, 54 };
                string[] keys = { "slash", "pierce", "beat", "loaded", "measured", "evade" };
                int[] contacts = { 4, 5, 7, 4, 3, 9 };
                for (int i = 0; i < ids.Length; i++)
                {
                    var contact = set.GetAttack(properties[i], ids[i], .5f);
                    Assert.That(contact.name, Is.EqualTo("final-" + keys[i] + "-frame-" + contacts[i].ToString("00")));
                    Assert.That(set.GetAttack(properties[i], ids[i], .499f), Is.Not.SameAs(contact));
                    Assert.That(set.GetAttack(properties[i], ids[i], 0).pivot, Is.EqualTo(idle.pivot));
                }
            }
        }

        [TestCase(5, "beat")][TestCase(48, "beat")][TestCase(52, "beat")]
        [TestCase(6, "loaded")][TestCase(55, "loaded")][TestCase(68, "loaded")]
        [TestCase(10, "measured")][TestCase(53, "measured")][TestCase(54, "evade")]
        public void StrikeSkillsBindToAuthoredFamily(int id, string expected)
            => Assert.That(FinalPlayerAnimationSet.AttackKey(LegacySkillProperty.Hit, id), Is.EqualTo(expected));

        [UnityTest]
        public IEnumerator ArenaUsesNewArtForEveryHitWithoutChangingEnemyArt()
        {
            yield return null;
            var controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            bool wasEnabled = controller != null && controller.enabled;
            GameObject oldRoot = controller != null ? controller.ArenaView.PlayerRenderer.transform.parent.gameObject : null;
            bool wasActive = oldRoot != null && oldRoot.activeSelf;
            if (controller != null) controller.enabled = false;
            if (oldRoot != null) oldRoot.SetActive(false);
            var host = new GameObject("Final Player Art Test");
            var arena = LegacyArenaView.Create(host.transform, new LegacyDuelArt());
            try
            {
                yield return null;
                Assert.That(arena.PlayerRenderer.sprite.name, Does.StartWith("final-idle"));
                arena.SetDepthPresentationVisible(true);
                Vector3 feet = arena.PlayerRenderer.transform.TransformPoint(new Vector3(
                    (114 - arena.PlayerRenderer.sprite.pivot.x) / 36,
                    (5 - arena.PlayerRenderer.sprite.pivot.y) / 36, 0));
                Assert.That(Vector3.Distance(feet, arena.PlayerGroundCircle.position), Is.LessThan(.001f));
                Capture(arena, "unity-idle-grounding.png");
                var hold = typeof(LegacyArenaView).GetMethod("HoldSlotAtTime", BindingFlags.Instance | BindingFlags.NonPublic);
                var configure = typeof(LegacyArenaView).GetMethod("ConfigureSlotTiming", BindingFlags.Instance | BindingFlags.NonPublic);
                configure.Invoke(arena, new object[] { 1f, .1f });
                int[] ids = { 5, 6, 10, 54 };
                string[] keys = { "beat", "loaded", "measured", "evade" };
                for (int i = 0; i < ids.Length; i++)
                {
                    arena.BeginSlot(new LegacySkill(ids[i], "Art test", 1, 1, 1, LegacySkillKind.Attack,
                        LegacySkillProperty.Hit, 3, 0, ""), null);
                    for (int hit = 0; hit < 3; hit++)
                    {
                        hold.Invoke(arena, new object[] { hit * (LegacyArenaView.OriginalClipDuration + .1f) + LegacyArenaView.OriginalImpactTime });
                        Assert.That(arena.PlayerRenderer.sprite.name, Does.StartWith("final-" + keys[i]));
                    }
                    Assert.That(arena.EnemyRenderer.sprite.name, Does.Not.StartWith("final-"));
                    if (i == 0) Capture(arena, "unity-strike-contact.png");
                }
            }
            finally
            {
                arena.Dispose(); Object.Destroy(host);
                if (oldRoot != null) oldRoot.SetActive(wasActive);
                if (controller != null) controller.enabled = wasEnabled;
            }
        }

        private static void Capture(LegacyArenaView arena, string filename)
        {
            var target = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var camera = arena.ArenaCamera;
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                string directory = Path.Combine(Application.dataPath, "../Docs/Art/FinalPlayer");
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, filename), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                target.Release(); Object.Destroy(target); Object.Destroy(image);
            }
        }
    }
}
