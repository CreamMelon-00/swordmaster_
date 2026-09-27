using System;
using System.Collections;
using System.Collections.Generic;
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
        public IEnumerator AllFortyNineFrames_LoadWithCommonCanvasFootPivotAndPointFiltering()
        {
            using (var animations = new MobStudentAnimationSet())
            {
                Assert.That(animations.MissingResources, Is.Empty);
                Assert.That(animations.HasRequiredAssets, Is.True);
                Assert.That(animations.LoadedSpriteCount, Is.EqualTo(49));
                var textures = new HashSet<Texture2D>();
                foreach (string path in ResourcePaths())
                {
                    Sprite sprite = Resources.Load<Sprite>("MobStudent/Animations/" + path);
                    Assert.That(sprite, Is.Not.Null, path);
                    Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(1024, 768)), path);
                    Assert.That(sprite.pivot.x, Is.EqualTo(448f).Within(.001f), path);
                    Assert.That(sprite.pivot.y, Is.EqualTo(64f).Within(.001f), path);
                    Assert.That(sprite.pixelsPerUnit, Is.EqualTo(150f), path);
                    Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point), path);
                    textures.Add(sprite.texture);
                }
                Assert.That(textures.Count, Is.EqualTo(49));
                Sprite source = Resources.Load<Sprite>("MobStudent/Animations/idle/upper-1");
                Sprite upper = animations.GetIdleUpper(0f);
                Assert.That(upper.texture, Is.SameAs(source.texture), "Adapting the old arena origin must not copy textures.");
                Assert.That(upper.pivot.y - source.pivot.y,
                    Is.EqualTo(-MobStudentAnimationSet.GroundOffset * 150f).Within(.001f));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator NineAttacks_ShowFourDistinctFrames_ContactThird_AndFourthHitCyclesToFirstVariant()
        {
            using (var animations = new MobStudentAnimationSet())
            {
                Assert.That(animations.HasRequiredAssets, Is.True);
                var allFrames = new HashSet<Sprite>();
                foreach (LegacySkillProperty type in AttackTypes)
                {
                    for (int hit = 0; hit < 3; hit++)
                    {
                        for (int frame = 0; frame < 4; frame++)
                        {
                            Sprite sprite = animations.GetAttackUpper(type, hit, frame / 4f);
                            Assert.That(sprite, Is.Not.Null);
                            Assert.That(sprite.name, Is.EqualTo(MobStudentAnimationSet.AttackKey(type, hit) + "-upper-" + (frame + 1)));
                            allFrames.Add(sprite);
                        }
                        Assert.That(animations.GetAttackUpper(type, hit,
                            LegacyArenaView.OriginalImpactTime / LegacyArenaView.OriginalClipDuration).name,
                            Does.EndWith("-upper-3"));
                    }
                    Assert.That(animations.GetAttackUpper(type, 3, 0f), Is.SameAs(animations.GetAttackUpper(type, 0, 0f)));
                }
                Assert.That(allFrames.Count, Is.EqualTo(36), "All nine attacks must be actual loaded clips, not a fallback frame.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerMovesEightLowerFrames_WhileUpperRemainsAtTheHeldAttackContact()
        {
            var host = new GameObject("Mob Student Layer Test");
            using (var art = new LegacyDuelArt())
            {
                var arena = LegacyArenaView.Create(host.transform, art);
                try
                {
                    Assert.That(arena.HasMobStudentAnimations, Is.True);
                    arena.BeginSlot(Attack(LegacySkillProperty.Slash, 3), null);
                    Action<float> hold = BindHold(arena);
                    var lowerFrames = new HashSet<Sprite>();
                    Sprite enemyIdle = arena.EnemyRenderer.sprite;
                    for (int frame = 1; frame <= 8; frame++)
                    {
                        // Actual displacement supplies movement without changing the combat clock's ownership.
                        arena.PlayerRenderer.transform.localPosition += Vector3.right * .01f;
                        arena.Tick(MobStudentAnimationSet.MoveFrameDuration + .00001f, 0f);
                        hold(LegacyArenaView.OriginalImpactTime);
                        lowerFrames.Add(arena.PlayerLowerRenderer.sprite);
                        Assert.That(arena.PlayerLowerRenderer.sprite.name, Is.EqualTo("move-lower-" + (frame % 8 + 1)));
                        Assert.That(arena.PlayerRenderer.sprite.name, Is.EqualTo("slash-1-upper-3"));
                        Assert.That(arena.PlayerRenderer.flipX, Is.False);
                        Assert.That(arena.PlayerLowerRenderer.flipX, Is.False);
                        Assert.That(arena.PlayerLowerRenderer.transform.localPosition, Is.EqualTo(Vector3.zero));
                        Assert.That(arena.PlayerRenderer.sprite.pivot, Is.EqualTo(arena.PlayerLowerRenderer.sprite.pivot));
                    }
                    Assert.That(lowerFrames.Count, Is.EqualTo(8));
                    Sprite upperHeld = arena.PlayerRenderer.sprite;
                    Sprite lowerHeld = arena.PlayerLowerRenderer.sprite;
                    arena.Tick(0f, .5f);
                    Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(upperHeld));
                    Assert.That(arena.PlayerLowerRenderer.sprite, Is.SameAs(lowerHeld));
                    arena.Tick(.001f, 0f);
                    Assert.That(arena.PlayerLowerRenderer.sprite.name, Is.EqualTo("idle-lower-1"));
                    arena.Reset();
                    Assert.That(arena.PlayerRenderer.sprite.name, Is.EqualTo("idle-upper-1"));
                    Assert.That(arena.PlayerLowerRenderer.sprite.name, Is.EqualTo("idle-lower-1"));
                    Assert.That(arena.EnemyRenderer.sprite, Is.SameAs(enemyIdle));
                }
                finally { arena.Dispose(); Object.Destroy(host); }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator BriefStepAndApproach_AdvanceLowerPosesWithoutChangingTheirMovementOrAttackClock()
        {
            var host = new GameObject("Mob Student Short Motion Test");
            using (var art = new LegacyDuelArt())
            {
                var arena = LegacyArenaView.Create(host.transform, art);
                try
                {
                    var approachPoses = new HashSet<Sprite>();
                    arena.BeginApproach();
                    for (int frame = 0; frame < 6; frame++)
                    {
                        arena.Tick(.015f, .015f);
                        approachPoses.Add(arena.PlayerLowerRenderer.sprite);
                    }
                    Assert.That(approachPoses.Count, Is.GreaterThanOrEqualTo(4),
                        "A short approach must progress through footwork rather than show only its first pose.");
                    arena.Reset();
                    arena.BeginSlot(Attack(LegacySkillProperty.Slash, 3), null);
                    Sprite upper = arena.PlayerRenderer.sprite;
                    float startX = arena.PlayerRenderer.transform.localPosition.x;
                    var stepPoses = new HashSet<Sprite>();
                    arena.PerformStep(LegacyStepAction.Pressure);
                    for (int frame = 0; frame < 4; frame++)
                    {
                        arena.Tick(0f, .01f);
                        stepPoses.Add(arena.PlayerLowerRenderer.sprite);
                        Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(upper));
                    }
                    Assert.That(stepPoses.Count, Is.GreaterThanOrEqualTo(3));
                    Assert.That(arena.PlayerRenderer.transform.localPosition.x, Is.GreaterThan(startX));
                    arena.Tick(0f, .1f);
                    Assert.That(arena.IsStepping, Is.False);
                    Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(upper));
                    arena.Tick(.001f, 0f);
                    Assert.That(arena.PlayerLowerRenderer.sprite.name, Is.EqualTo("idle-lower-1"));
                }
                finally { arena.Dispose(); Object.Destroy(host); }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator PressureAfterimages_IncludeBothCurrentLayers_AndRetreatDoesNotMirrorEitherFoot()
        {
            var host = new GameObject("Mob Student Trail Test");
            using (var art = new LegacyDuelArt())
            {
                var arena = LegacyArenaView.Create(host.transform, art);
                try
                {
                    arena.CloseDistance(1f);
                    arena.BeginSlot(Attack(LegacySkillProperty.Penetrate, 3), null);
                    arena.PerformStep(LegacyStepAction.Pressure);
                    Transform ghost = host.transform.Find("Legacy Duel Arena/Duel Step Afterimages").GetChild(0);
                    Assert.That(ghost.GetComponent<SpriteRenderer>().sprite, Is.SameAs(arena.PlayerRenderer.sprite));
                    Assert.That(ghost.Find("Lower Body").GetComponent<SpriteRenderer>().sprite,
                        Is.SameAs(arena.PlayerLowerRenderer.sprite));
                    arena.Tick(0f, .1f);
                    arena.PerformStep(LegacyStepAction.Dodge);
                    arena.Tick(0f, .15f);
                    Assert.That(arena.PlayerLowerRenderer.sprite.name, Does.StartWith("move-lower-"));
                    Assert.That(arena.PlayerRenderer.flipX, Is.False);
                    Assert.That(arena.PlayerLowerRenderer.flipX, Is.False);
                    Assert.That(arena.PlayerRenderer.sprite.name, Is.EqualTo("pierce-1-upper-1"),
                        "Real-time displacement must not advance the paused combat attack clock.");
                }
                finally { arena.Dispose(); Object.Destroy(host); }
            }
            yield return null;
        }

        private static readonly LegacySkillProperty[] AttackTypes =
            { LegacySkillProperty.Slash, LegacySkillProperty.Penetrate, LegacySkillProperty.Hit };

        private static LegacySkill Attack(LegacySkillProperty type, int count) =>
            new LegacySkill(101, "Animation Test", 1, 1, 1, LegacySkillKind.Attack, type, count, 0, string.Empty);

        private static Action<float> BindHold(LegacyArenaView arena) =>
            (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), arena,
                typeof(LegacyArenaView).GetMethod("HoldSlotAtTime", BindingFlags.Instance | BindingFlags.NonPublic));

        private static IEnumerable<string> ResourcePaths()
        {
            for (int frame = 1; frame <= 4; frame++) yield return "idle/upper-" + frame;
            yield return "idle/lower-1";
            for (int frame = 1; frame <= 8; frame++) yield return "move/lower-" + frame;
            foreach (string type in new[] { "slash", "pierce", "blunt" })
                for (int hit = 1; hit <= 3; hit++)
                    for (int frame = 1; frame <= 4; frame++) yield return type + "-" + hit + "/upper-" + frame;
        }
    }
}
