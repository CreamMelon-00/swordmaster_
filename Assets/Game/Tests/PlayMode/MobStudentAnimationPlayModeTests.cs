using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class MobStudentAnimationPlayModeTests
    {
        [UnityTest]
        public IEnumerator SwordGirl_AllAttackCelsHaveCorrectImportAndSharedGroundOrigin()
        {
            using (var animations = new MobStudentAnimationSet())
            {
                Assert.That(animations.HasRequiredAssets, Is.True, string.Join(", ", animations.MissingResources));
                Assert.That(animations.LoadedSpriteCount, Is.EqualTo(MobStudentAnimationSet.RequiredSpriteCount));
                foreach (string key in new[] { "idle", "slash", "slash-2", "slash-3", "pierce", "pierce-2", "pierce-3", "blunt", "blunt-2", "blunt-3" })
                    for (int i = 1; i <= (key == "idle" ? 8 : 12); i++)
                    {
                        Sprite sprite = Resources.Load<Sprite>(MobStudentAnimationSet.ResourceRoot + key + "/frame-" + i.ToString("00"));
                        Assert.That(sprite, Is.Not.Null);
                        Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(256, 224)));
                        Assert.That(sprite.pivot.x, Is.EqualTo(101f).Within(.001f));
                        Assert.That(sprite.pivot.y, Is.EqualTo(22f).Within(.001f));
                        Assert.That(sprite.pixelsPerUnit, Is.EqualTo(40f));
                        Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                        Assert.That(sprite.texture.mipmapCount, Is.EqualTo(1));
                    }
                Sprite source = Resources.Load<Sprite>(MobStudentAnimationSet.ResourceRoot + "idle/frame-01");
                Sprite adapted = animations.GetIdleUpper(0f);
                Assert.That(adapted.texture, Is.SameAs(source.texture));
                Assert.That((source.pivot.y - adapted.pivot.y) / adapted.pixelsPerUnit,
                    Is.EqualTo(MobStudentAnimationSet.GroundOffset).Within(.0001f));
                Assert.That(animations.GetLower(1f, true), Is.Null, "Full-body art must not double-render old legs.");
                Assert.That(MobStudentAnimationSet.MoveFrameCount, Is.EqualTo(1));
                for (int frame = 1; frame <= MobStudentAnimationSet.MoveFrameCount; frame++)
                {
                    Sprite move = Resources.Load<Sprite>(MobStudentAnimationSet.ResourceRoot + "move/frame-" + frame.ToString("00"));
                    Assert.That(move, Is.Not.Null);
                    Assert.That(move.rect.size, Is.EqualTo(source.rect.size));
                    Assert.That(move.pivot, Is.EqualTo(source.pivot));
                    Assert.That(move.pixelsPerUnit, Is.EqualTo(source.pixelsPerUnit));
                    Assert.That(move.texture.filterMode, Is.EqualTo(FilterMode.Point));
                    Assert.That(move.texture.mipmapCount, Is.EqualTo(1));
                }
                Assert.That(Resources.Load<Sprite>(MobStudentAnimationSet.ResourceRoot + "move/frame-02"), Is.Null);
                Assert.That(animations.GetMove().name, Is.EqualTo("move-frame-01"));
                Assert.That(animations.GetMove(), Is.SameAs(animations.GetMove()), "Movement holds one pose throughout travel.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SwordGirl_AuthoredTimingLoopsAndEveryAttackContactsAtTheAuthoritativeImpact()
        {
            using (var animations = new MobStudentAnimationSet())
            {
                int[] durations = { 180, 140, 160, 140, 160, 140, 160, 180 };
                float time = 0f;
                for (int i = 0; i < durations.Length; i++)
                {
                    Assert.That(animations.GetIdleUpper(time + .00001f).name, Is.EqualTo("idle-frame-" + (i + 1).ToString("00")));
                    time += durations[i] / 1000f;
                }
                Assert.That(animations.GetIdleUpper(time + .00001f).name, Is.EqualTo("idle-frame-01"));
                foreach (LegacySkillProperty property in Types)
                for (int variant = 0; variant < MobStudentAnimationSet.AttackVariationCount(property); variant++)
                {
                    string key = MobStudentAnimationSet.AttackKey(property, variant);
                    var frames = new HashSet<Sprite>();
                    for (int sample = 0; sample <= 1000; sample++) frames.Add(animations.GetAttackUpper(property, variant, sample / 1000f));
                    Assert.That(frames.Count, Is.EqualTo(12));
                    Assert.That(animations.GetAttackUpper(property, variant, .5f - .001f).name, Is.EqualTo(key + "-frame-04"));
                    Assert.That(animations.GetAttackUpper(property, variant, .5f).name, Is.EqualTo(key + "-frame-05"));
                    Assert.That(animations.GetAttackUpper(property, variant, 1f).name, Is.EqualTo(key + "-frame-12"));
                }
                Assert.That(animations.GetAttackUpper(LegacySkillProperty.Defence, 0, .5f), Is.Null);
                animations.Dispose();
                Assert.That(animations.HasRequiredAssets, Is.False);
                Assert.That(animations.GetIdleUpper(0), Is.Null);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator BothStudents_UseRunPoseDuringApproachAndCloseDistanceThenStandAtRest()
        {
            var host = new GameObject("Movement Pose Test");
            using (var art = new LegacyDuelArt())
            using (var arena = LegacyArenaView.Create(host.transform, art, null))
            {
                arena.BeginApproach();
                arena.Tick(.015f, .015f);
                Assert.That(arena.PlayerRenderer.sprite.name, Is.EqualTo("move-frame-01"));
                Assert.That(arena.EnemyRenderer.sprite.name, Is.EqualTo("enemy-move-frame-01"));
                Sprite playerHeld = arena.PlayerRenderer.sprite;
                Sprite enemyHeld = arena.EnemyRenderer.sprite;
                arena.Tick(0f, .03f);
                Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(playerHeld), "Hit stop holds the movement pose.");
                Assert.That(arena.EnemyRenderer.sprite, Is.SameAs(enemyHeld));
                arena.Tick(.02f, .02f);
                Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(playerHeld));
                Assert.That(arena.EnemyRenderer.sprite, Is.SameAs(enemyHeld));
                arena.Tick(.02f, .02f);
                Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(playerHeld));
                Assert.That(arena.EnemyRenderer.sprite, Is.SameAs(enemyHeld));
                arena.Tick(.02f, .02f);
                Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(playerHeld));
                Assert.That(arena.EnemyRenderer.sprite, Is.SameAs(enemyHeld));
                arena.Tick(.02f, .02f);
                Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(playerHeld));
                Assert.That(arena.EnemyRenderer.sprite, Is.SameAs(enemyHeld));
                arena.Tick(0f, .2f);
                Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(playerHeld));
                Assert.That(arena.EnemyRenderer.sprite, Is.SameAs(enemyHeld));
                // With the authored 1.2 movement multiplier, the opening gap closes in about .078 s.
                arena.Tick(.01f, .01f);
                arena.Tick(.01f, .01f);
                Assert.That(arena.PlayerRenderer.sprite.name, Does.StartWith("idle-frame-"));
                Assert.That(arena.EnemyRenderer.sprite.name, Does.StartWith("enemy-idle-frame-"));

                arena.Reset();
                arena.CloseDistance(.01f);
                Assert.That(arena.PlayerRenderer.sprite.name, Is.EqualTo("move-frame-01"));
                Assert.That(arena.EnemyRenderer.sprite.name, Is.EqualTo("enemy-move-frame-01"));
                arena.CloseDistance(.02f);
                Assert.That(arena.PlayerRenderer.sprite.name, Is.EqualTo("move-frame-01"));
                Assert.That(arena.EnemyRenderer.sprite.name, Is.EqualTo("enemy-move-frame-01"));
                arena.Tick(.01f, .01f);
                Assert.That(arena.PlayerRenderer.sprite.name, Does.StartWith("idle-frame-"));
                Assert.That(arena.EnemyRenderer.sprite.name, Does.StartWith("enemy-idle-frame-"));

                arena.Reset();
                arena.BeginSlot(LegacySkillDefinitions.Skill(7), LegacySkillDefinitions.Skill(7));
                arena.CloseDistance(.02f);
                Assert.That(arena.PlayerRenderer.sprite.name, Does.Contain("block"));
                Assert.That(arena.EnemyRenderer.sprite.name, Does.Contain("block"));
            }
            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SwordGirl_StepsAndHitStopKeepWholeBodyPoseAndAfterimageFacingTheOpponent()
        {
            yield return null;
            var controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            controller.RestartMatch(); controller.enabled = false;
            try
            {
                var arena = controller.ArenaView;
                Assert.That(arena.PlayerLowerRenderer.enabled, Is.False);
                Assert.That(arena.PlayerLowerRenderer.sprite, Is.Null);
                Sprite enemyIdle = arena.EnemyRenderer.sprite;
                arena.CloseDistance(1f);
                arena.BeginSlot(Attack(LegacySkillProperty.Penetrate, 3), null);
                Hold(arena, LegacyArenaView.OriginalImpactTime);
                Sprite contact = arena.PlayerRenderer.sprite;
                arena.Tick(0, .5f);
                Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(contact));
                float start = arena.PlayerRenderer.transform.localPosition.x;
                arena.PerformStep(LegacyStepAction.Pressure);
                arena.Tick(0f, .1f);
                Assert.That(arena.PlayerRenderer.transform.localPosition.x, Is.GreaterThan(start));
                Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(contact));
                Assert.That(arena.PlayerRenderer.flipX, Is.False);
                int inspectedGhosts = 0;
                foreach (var ghost in arena.PlayerRenderer.transform.parent.GetComponentsInChildren<SpriteRenderer>(true))
                    if (ghost.name.StartsWith("Step Afterimage") && ghost.gameObject.activeSelf)
                    {
                        inspectedGhosts++;
                        Assert.That(ghost.sprite, Is.SameAs(contact));
                        Assert.That(ghost.flipX, Is.False);
                        Assert.That(ghost.transform.Find("Lower Body"), Is.Null);
                    }
                Assert.That(inspectedGhosts, Is.GreaterThan(0));
                arena.PerformStep(LegacyStepAction.Dodge); arena.Tick(0f, .15f);
                Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(contact));
                Assert.That(arena.PlayerRenderer.flipX, Is.False);
                arena.Reset();
                Assert.That(arena.PlayerRenderer.sprite.name, Is.EqualTo("idle-frame-01"));
                Assert.That(arena.PlayerLowerRenderer.enabled, Is.False);
                Assert.That(arena.EnemyRenderer.sprite, Is.SameAs(enemyIdle));
            }
            finally { controller.RestartMatch(); controller.enabled = true; }
        }

        [UnityTest]
        public IEnumerator SwordGirl_RealArenaRendersAllThreeContactPosesAndIdle()
        {
            yield return null;
            var controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            controller.RestartMatch(); controller.enabled = false;
            var arena = controller.ArenaView;
            string directory = Environment.GetEnvironmentVariable("SWORDGIRL_VALIDATION_OUTPUT");
            var target = new RenderTexture(1600, 900, 24);
            var camera = arena.ArenaCamera;
            RenderTexture previous = camera.targetTexture;
            try
            {
                arena.CloseDistance(1f);
                camera.targetTexture = target;
                foreach (var property in new[] { LegacySkillProperty.None, LegacySkillProperty.Slash, LegacySkillProperty.Penetrate, LegacySkillProperty.Hit })
                {
                    arena.BeginSlot(property == LegacySkillProperty.None ? null : Attack(property, 1), null);
                    arena.Tick(0f, 1f);
                    if (property != LegacySkillProperty.None) Hold(arena, LegacyArenaView.OriginalImpactTime);
                    string key = property == LegacySkillProperty.None ? "idle" : MobStudentAnimationSet.AttackKey(property, 0);
                    Assert.That(arena.PlayerRenderer.sprite.name, property != LegacySkillProperty.None
                        ? Does.Match("^" + key + "(?:-[23])?-frame-05$")
                        : Is.EqualTo(key + (key == "idle" ? "-frame-01" : "-frame-05")));
                    yield return null;
                    yield return null;
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                        var active = RenderTexture.active;
                        var capture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                        try
                        {
                            RenderTexture.active = target;
                            capture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); capture.Apply();
                            File.WriteAllBytes(Path.Combine(directory, "unity-" + key + ".png"), capture.EncodeToPNG());
                        }
                        finally { RenderTexture.active = active; Object.Destroy(capture); }
                    }
                }
            }
            finally
            {
                camera.targetTexture = previous; target.Release(); Object.Destroy(target);
                controller.RestartMatch(); controller.enabled = true;
            }
        }

        private static readonly LegacySkillProperty[] Types = { LegacySkillProperty.Slash, LegacySkillProperty.Penetrate, LegacySkillProperty.Hit };
        private static LegacySkill Attack(LegacySkillProperty property, int count) =>
            new LegacySkill(101, "SwordGirl check", 1, 1, 1, LegacySkillKind.Attack, property, count, 0, string.Empty);
        private static void Hold(LegacyArenaView arena, float time) =>
            typeof(LegacyArenaView).GetMethod("HoldSlotAtTime", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(arena, new object[] { time });
    }
}
