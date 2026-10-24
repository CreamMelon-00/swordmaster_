using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
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
                Assert.That(set.LoadedFrameCount, Is.EqualTo(122));
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
                int[] contacts = { 4, 5, 7, 4, 3, 8 };
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
        public IEnumerator EveryLoadedAttackUsesItsTypeAndAssignedFamilyOnEveryHit()
        {
            var host = new GameObject("All Skill Animation Audit");
            var arena = LegacyArenaView.Create(host.transform, new LegacyDuelArt());
            var report = new List<BindingAudit>();
            var expectedHits = new Dictionary<int, string>
            {
                {5,"beat"},{48,"beat"},{52,"beat"},{6,"loaded"},{55,"loaded"},{68,"loaded"},
                {10,"measured"},{53,"measured"},{54,"evade"}
            };
            var hold = typeof(LegacyArenaView).GetMethod("HoldSlotAtTime", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                typeof(LegacyArenaView).GetMethod("ConfigureSlotTiming", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(arena, new object[] { 1f, .1f });
                using (var set = new FinalPlayerAnimationSet())
                {
                    foreach (var definition in LegacySkillDefinitions.All)
                    {
                        var skill = definition.Skill;
                        if (skill.Kind != LegacySkillKind.Attack) continue;
                        string key = skill.Property == LegacySkillProperty.Slash ? "slash"
                            : skill.Property == LegacySkillProperty.Penetrate ? "pierce" : expectedHits[skill.Id];
                        Assert.That(set.ResolveAttackKey(skill), Is.EqualTo(key), skill.Name);
                        arena.BeginSlot(skill, null);
                        for (int hit = 0; hit < skill.AttackCount; hit++)
                            foreach (float phase in new[] { .01f, .25f, .5f, .75f, .99f })
                            {
                                hold.Invoke(arena, new object[] { hit * (LegacyArenaView.OriginalClipDuration + .1f)
                                    + phase * LegacyArenaView.OriginalClipDuration });
                                Assert.That(arena.PlayerRenderer.sprite.name, Does.StartWith("final-" + key + "-frame-"),
                                    skill.Id + ": " + skill.Name + " hit=" + hit + " phase=" + phase);
                            }
                        report.Add(new BindingAudit { id = skill.Id, name = skill.Name, property = skill.Property.ToString(),
                            animation = key, hits = skill.AttackCount });
                    }
                    Assert.That(report.Count, Is.EqualTo(40));
                    foreach (int id in new[] { 1, 5, 6, 54 })
                    {
                        var mislabeled = new LegacySkill(id, "Mismatched label", 0, 1, 1, LegacySkillKind.Attack,
                            LegacySkillProperty.Hit, 1, 0, "", "Slash");
                        Assert.That(set.GetAttack(mislabeled, .5f).name, Does.Not.StartWith("final-slash"),
                            "Property must override stale Slash labels or an ID from another type.");
                    }
                    CaptureStrikeComparison(arena, set);
                }
                string path = Path.Combine(Application.dataPath, "../Docs/Art/FinalPlayer/Validation/skill-animation-bindings.json");
                File.WriteAllText(path, JsonUtility.ToJson(new BindingAuditReport { entries = report.ToArray() }, true));
            }
            finally { arena.Dispose(); Object.Destroy(host); }
            yield return null;
        }

        [Serializable] private sealed class BindingAuditReport { public BindingAudit[] entries; }
        [Serializable] private sealed class BindingAudit
        {
            public int id, hits;
            public string name, property, animation;
        }

        private static void CaptureStrikeComparison(LegacyArenaView arena, FinalPlayerAnimationSet set)
        {
            var root = new GameObject("Strike Art Comparison");
            var camera = root.AddComponent<Camera>();
            camera.transform.position = new Vector3(0, -.1f, -10);
            camera.orthographic = true; camera.orthographicSize = 6.1f;
            camera.cullingMask = 1 << 28;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.145f, .137f, .145f);
            var target = new RenderTexture(1080, 760, 24);
            var pixels = new Texture2D(1080, 760, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                int[] ids = {5,6,10,54};
                for (int i = 0; i < ids.Length; i++)
                {
                    var figure = new GameObject("Strike " + i) { layer = 28 };
                    figure.transform.SetParent(root.transform, false);
                    figure.transform.position = new Vector3(i % 2 == 0 ? -3 : 3, i < 2 ? 2.8f : -2.8f, 0);
                    var renderer = figure.AddComponent<SpriteRenderer>();
                    renderer.sprite = set.GetAttack(LegacySkillDefinitions.Skill(ids[i]), .5f);
                    renderer.sharedMaterial = arena.PlayerRenderer.sharedMaterial;
                }
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1080, 760), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(Application.dataPath, "../Docs/Art/FinalPlayer/strike-four-families-with-trails.png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                target.Release(); Object.Destroy(target); Object.Destroy(pixels); Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator GuardsRollOnEachActionAndContactButHoldOneStaticPoseBetweenThem()
        {
            yield return null;
            var controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            bool enabled = controller != null && controller.enabled;
            var oldRoot = controller != null ? controller.ArenaView.PlayerRenderer.transform.parent.gameObject : null;
            bool active = oldRoot != null && oldRoot.activeSelf;
            if (controller != null) controller.enabled = false;
            if (oldRoot != null) oldRoot.SetActive(false);
            var host = new GameObject("Guard Three Poses Test");
            var random = new GuardRandom();
            var arena = LegacyArenaView.Create(host.transform, new LegacyDuelArt(), reactionPoseRandom: random);
            try
            {
                arena.SetDepthPresentationVisible(true);
                arena.BeginSlot(LegacySkillDefinitions.Skill(7), LegacySkillDefinitions.Skill(1));
                Assert.That(random.Calls, Is.EqualTo(1));
                Assert.That(arena.PlayerRenderer.sprite.name, Is.EqualTo("final-guard-01-frame-00"));
                Capture(arena, "unity-guard-01.png");
                for (int index = 1; index < 3; index++)
                {
                    Sprite before = arena.PlayerRenderer.sprite;
                    arena.Tick(0, .1f);
                    arena.Tick(0, .1f);
                    Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(before));
                    Assert.That(random.Calls, Is.EqualTo(index));
                    arena.PresentHit(false, 0, 2, true, false);
                    Assert.That(random.Calls, Is.EqualTo(index + 1));
                    Assert.That(arena.PlayerRenderer.sprite.name, Is.EqualTo("final-guard-0" + (index + 1) + "-frame-00"));
                    Capture(arena, "unity-guard-0" + (index + 1) + ".png");
                }
                arena.BeginSlot(LegacySkillDefinitions.Skill(7), null);
                Assert.That(random.Calls, Is.EqualTo(4));
                Assert.That(arena.PlayerRenderer.sprite.name, Is.EqualTo("final-guard-03-frame-00"),
                    "Independent random draws may repeat the preceding guard.");
                using (var set = new FinalPlayerAnimationSet())
                {
                    int[] floors = { 176, 191, 218 };
                    float[] centers = { 118.5f, 108.5f, 119.5f };
                    int[] heads = { 53, 64, 85 };
                    for (int i = 0; i < 3; i++)
                    {
                        var guard = set.GetGuard(i);
                        Assert.That(guard.texture.filterMode, Is.EqualTo(FilterMode.Point));
                        Assert.That(guard.pivot.x, Is.EqualTo(centers[i]).Within(.001));
                        Assert.That((224 - floors[i] - 1 - guard.pivot.y) / guard.pixelsPerUnit,
                            Is.EqualTo(MobStudentAnimationSet.GroundOffset).Within(.001));
                        Assert.That((floors[i] - heads[i] + 1) / guard.pixelsPerUnit, Is.EqualTo(125f / 36).Within(.001));
                    }
                    CaptureGuardComparison(arena, set);
                }
            }
            finally
            {
                arena.Dispose(); Object.Destroy(host);
                if (oldRoot != null) oldRoot.SetActive(active);
                if (controller != null) controller.enabled = enabled;
            }
        }

        [UnityTest]
        public IEnumerator AuthoredSlashAndPierceDoNotDoubleEmitProceduralPlayerTrails()
        {
            var host = new GameObject("Authored Trail Test");
            var arena = LegacyArenaView.Create(host.transform, new LegacyDuelArt());
            try
            {
                foreach (var property in new[] { LegacySkillProperty.Slash, LegacySkillProperty.Penetrate, LegacySkillProperty.Hit })
                {
                    arena.SetWeaponTrailOverride(true);
                    typeof(LegacyArenaView).GetMethod("ConfigureSlotTiming", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(arena, new object[] { 1f, .1f });
                    var skill = new LegacySkill(1, "Trail test", 1, 1, 1, LegacySkillKind.Attack, property, 1, 0, "");
                    arena.BeginSlot(skill, skill);
                    int before = arena.WeaponTrailEmissionCount;
                    typeof(LegacyArenaView).GetMethod("PresentWeaponTrailContact", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(arena, new object[] { 0, true, true });
                    Assert.That(arena.WeaponTrailEmissionCount - before, Is.EqualTo(1),
                        "The player has baked trails; the enemy still emits its procedural trail.");
                }
            }
            finally { arena.Dispose(); Object.Destroy(host); }
            yield return null;
        }

        private sealed class GuardRandom : System.Random
        {
            private readonly int[] choices = { 0, 1, 2, 2 };
            public int Calls { get; private set; }
            public override int Next(int maxValue)
            {
                Assert.That(maxValue, Is.EqualTo(3));
                Assert.That(Calls, Is.LessThan(choices.Length));
                return choices[Calls++];
            }
        }

        private static void CaptureGuardComparison(LegacyArenaView arena, FinalPlayerAnimationSet set)
        {
            var root = new GameObject("Guard Art Comparison");
            var camera = root.AddComponent<Camera>();
            camera.transform.position = new Vector3(0, -.1f, -10);
            camera.orthographic = true; camera.orthographicSize = 2.7f;
            camera.cullingMask = 1 << 28;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.145f, .137f, .145f);
            var target = new RenderTexture(1024, 400, 24);
            var pixels = new Texture2D(1024, 400, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    var figure = new GameObject("Guard " + i) { layer = 28 };
                    figure.transform.SetParent(root.transform, false);
                    figure.transform.position = new Vector3((i - 1) * 3.3f, 0, 0);
                    var renderer = figure.AddComponent<SpriteRenderer>();
                    renderer.sprite = set.GetGuard(i);
                    renderer.sharedMaterial = arena.PlayerRenderer.sharedMaterial;
                }
                camera.targetTexture = target;
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1024, 400), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(Application.dataPath, "../Docs/Art/FinalPlayer/guard-three-poses.png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                target.Release(); Object.Destroy(target); Object.Destroy(pixels); Object.Destroy(root);
            }
        }

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

        [UnityTest]
        public IEnumerator HurtAndBrokenPosesFollowReactionAndRecoveryWithoutReplacingAttacks()
        {
            yield return null;
            var controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            bool enabled = controller != null && controller.enabled;
            var oldRoot = controller != null ? controller.ArenaView.PlayerRenderer.transform.parent.gameObject : null;
            bool active = oldRoot != null && oldRoot.activeSelf;
            if (controller != null) controller.enabled = false;
            if (oldRoot != null) oldRoot.SetActive(false);
            var host = new GameObject("Hurt and Break Art Test");
            var arena = LegacyArenaView.Create(host.transform, new LegacyDuelArt());
            try
            {
                arena.SetDepthPresentationVisible(true);
                arena.BeginSlot(null, null);
                arena.PresentHit(false, 2, 0, false, false, 0, closeUp: false, hurtReaction: true);
                Assert.That(arena.PlayerRenderer.sprite.name, Is.EqualTo("final-hurt-frame-00"));
                Sprite hurt = arena.PlayerRenderer.sprite;
                arena.Tick(0f, .05f);
                Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(hurt), "Hit stop holds the static hurt pose.");
                Capture(arena, "unity-hurt.png");
                arena.Tick(.3f, .3f);
                Assert.That(arena.PlayerRenderer.sprite.name, Does.StartWith("final-idle"));

                arena.SetResistanceBroken(true, false);
                Assert.That(arena.PlayerRenderer.sprite.name, Is.EqualTo("final-break-frame-00"));
                Sprite broken = arena.PlayerRenderer.sprite;
                arena.Tick(.3f, .3f);
                Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(broken), "Broken idle stays after the short impact ends.");
                Capture(arena, "unity-break.png");
                Assert.That(arena.EnemyRenderer.sprite.name, Does.Not.StartWith("final-"));

                arena.PresentHit(false, 2, 0, false, false, 0, closeUp: false, hurtReaction: true);
                Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(broken), "Incoming hits use the broken reaction while broken.");
                arena.BeginSlot(LegacySkillDefinitions.Skill(1), null);
                Assert.That(arena.PlayerRenderer.sprite.name, Does.StartWith("final-slash"),
                    "The current/next permitted attack still follows combat's own clock.");
                arena.SetResistanceBroken(false, false);
                Assert.That(arena.PlayerRenderer.sprite.name, Does.StartWith("final-slash"));
                arena.BeginSlot(null, null);
                Assert.That(arena.PlayerRenderer.sprite.name, Does.StartWith("final-idle"));
                arena.SetResistanceBroken(true, false);
                arena.Reset();
                Assert.That(arena.PlayerRenderer.sprite.name, Does.StartWith("final-idle"));
                Assert.That(arena.PlayerBreakAura.IsBroken, Is.False);

                using (var set = new FinalPlayerAnimationSet())
                {
                    var poses = new[] { set.GetHurt(), set.GetBreak() };
                    float[] centers = {82f, 105f};
                    int[] floors = {201, 218};
                    for (int i = 0; i < poses.Length; i++)
                    {
                        var pose = poses[i];
                        Assert.That(pose.texture.filterMode, Is.EqualTo(FilterMode.Point));
                        Assert.That(pose.rect.size, Is.EqualTo(new Vector2(256, 224)));
                        Assert.That(pose.pixelsPerUnit, Is.EqualTo(36f));
                        Assert.That(pose.pivot.x, Is.EqualTo(centers[i]).Within(.001));
                        Assert.That((224 - floors[i] - 1 - pose.pivot.y) / pose.pixelsPerUnit,
                            Is.EqualTo(MobStudentAnimationSet.GroundOffset).Within(.001));
                    }
                }
            }
            finally
            {
                arena.Dispose(); Object.Destroy(host);
                if (oldRoot != null) oldRoot.SetActive(active);
                if (controller != null) controller.enabled = enabled;
            }
        }

        [Test]
        public void LobbyLocomotionLoopsWithSharedPhaseAndFixedImportOrigin()
        {
            using (var set = new FinalPlayerAnimationSet())
            {
                foreach (bool running in new[] { false, true })
                {
                    string key = running ? "run" : "walk";
                    Assert.That(set.LocomotionDuration(running), Is.EqualTo(running ? .56f : .8f).Within(.001f));
                    Sprite first = set.GetLocomotion(running, 0f);
                    Assert.That(first.name, Is.EqualTo("final-" + key + "-frame-00"));
                    Assert.That(set.GetLocomotion(running, 1f), Is.SameAs(first));
                    Assert.That(set.GetLocomotion(running, .125f).name, Is.EqualTo("final-" + key + "-frame-01"));
                    for (int i = 0; i < 8; i++)
                    {
                        Sprite frame = set.GetLocomotion(running, (i + .1f) / 8f);
                        Assert.That(frame.pivot, Is.EqualTo(first.pivot));
                        Assert.That(frame.pixelsPerUnit, Is.EqualTo(first.pixelsPerUnit));
                        Assert.That(frame.texture.filterMode, Is.EqualTo(FilterMode.Point));
                    }
                }
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
