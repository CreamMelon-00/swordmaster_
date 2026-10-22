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
    public sealed class BladeExchangePosePlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator CosmeticPosePreservesBothIndependentClocks_MomentumAndHitRecoilMemory()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                scope.Begin(Attack(), Attack());
                object player = Actor(scope.Arena, "player"), enemy = Actor(scope.Arena, "enemy");
                Write(player, "AnimationTime", .02f);
                Write(enemy, "AnimationTime", .035f);
                Write(player, "AttackRecoilOffset", new Vector3(.21f, .01f, 0f));
                Write(enemy, "AttackRecoilOffset", new Vector3(-.31f, .02f, 0f));
                Write(player, "GroundVelocity", new Vector2(.7f, -.2f));
                Write(enemy, "GroundVelocity", new Vector2(-.5f, .4f));
                Vector3 playerGround = scope.Arena.PlayerGroundPosition, enemyGround = scope.Arena.EnemyGroundPosition;
                Vector3 playerTarget = scope.Arena.PlayerKnockbackTarget, enemyTarget = scope.Arena.EnemyKnockbackTarget;
                Transform camera = scope.Arena.ArenaCamera.transform;
                Vector3 cameraAt = camera.position;
                Quaternion cameraRotation = camera.rotation;
                float cameraSize = scope.Arena.ArenaCamera.orthographicSize;

                scope.Arena.PresentBladeExchangePose(LegacyArenaView.OriginalImpactTime, .8f);

                Assert.That(Read<float>(player, "AnimationTime"), Is.EqualTo(.02f));
                Assert.That(Read<float>(enemy, "AnimationTime"), Is.EqualTo(.035f));
                Assert.That(Read<Vector3>(player, "AttackRecoilOffset"), Is.EqualTo(new Vector3(.21f, .01f, 0f)));
                Assert.That(Read<Vector3>(enemy, "AttackRecoilOffset"), Is.EqualTo(new Vector3(-.31f, .02f, 0f)));
                Assert.That(Read<Vector2>(player, "GroundVelocity"), Is.EqualTo(new Vector2(.7f, -.2f)));
                Assert.That(Read<Vector2>(enemy, "GroundVelocity"), Is.EqualTo(new Vector2(-.5f, .4f)));
                Assert.That(scope.Arena.PlayerGroundPosition, Is.EqualTo(playerGround));
                Assert.That(scope.Arena.EnemyGroundPosition, Is.EqualTo(enemyGround));
                Assert.That(scope.Arena.PlayerKnockbackTarget, Is.EqualTo(playerTarget));
                Assert.That(scope.Arena.EnemyKnockbackTarget, Is.EqualTo(enemyTarget));
                Assert.That(scope.Arena.HasPendingPush || scope.Arena.IsBladeBinding || scope.Arena.IsFatalFocus, Is.False);
                Assert.That(camera.position, Is.EqualTo(cameraAt));
                Assert.That(camera.rotation, Is.EqualTo(cameraRotation));
                Assert.That(scope.Arena.ArenaCamera.orthographicSize, Is.EqualTo(cameraSize));
                Assert.That(scope.Arena.CameraShakeAmplitude, Is.Zero);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SmallDeflectionDoesNotAccumulate_AndItsZeroEndpointMatchesTheRealContactPose()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                scope.Begin(Attack(), Attack());
                Invoke(scope.Arena, "HoldSlotAtTime", LegacyArenaView.OriginalImpactTime);
                Sprite playerContact = scope.Arena.PlayerRenderer.sprite, enemyContact = scope.Arena.EnemyRenderer.sprite;
                Vector3 playerOffset = scope.Arena.PlayerAttackVisualOffset, enemyOffset = scope.Arena.EnemyAttackVisualOffset;
                Assert.That(playerOffset.x, Is.GreaterThan(0f));
                Assert.That(enemyOffset.x, Is.LessThan(0f));

                foreach (float share in new[] { 0f, .25f, .5f, 1f, .5f, .25f, 0f })
                {
                    scope.Arena.PresentBladeExchangePose(LegacyArenaView.OriginalImpactTime, share);
                    Vector3 playerDeflection = scope.Arena.PlayerAttackVisualOffset - playerOffset;
                    Vector3 enemyDeflection = scope.Arena.EnemyAttackVisualOffset - enemyOffset;
                    Assert.That(playerDeflection.x, Is.EqualTo(-.10f * share).Within(.00001f));
                    Assert.That(enemyDeflection.x, Is.EqualTo(.10f * share).Within(.00001f));
                    Assert.That(playerDeflection.y, Is.EqualTo(.02f * share).Within(.00001f));
                    Assert.That(enemyDeflection.y, Is.EqualTo(.02f * share).Within(.00001f));
                    Assert.That(playerDeflection.z, Is.Zero);
                    Assert.That(enemyDeflection.z, Is.Zero);
                    Assert.That(scope.Arena.PlayerRenderer.sprite, Is.SameAs(playerContact));
                    Assert.That(scope.Arena.EnemyRenderer.sprite, Is.SameAs(enemyContact));
                }
                Assert.That(scope.Arena.PlayerAttackVisualOffset, Is.EqualTo(playerOffset));
                Assert.That(scope.Arena.EnemyAttackVisualOffset, Is.EqualTo(enemyOffset));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator NextArenaTickRestoresTheNaturalPose_AndAdvancesFromItsOwnClock()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                scope.Begin(Attack(), Attack());
                const float naturalTime = .015f;
                Invoke(scope.Arena, "HoldSlotAtTime", naturalTime);
                Sprite playerNatural = scope.Arena.PlayerRenderer.sprite, enemyNatural = scope.Arena.EnemyRenderer.sprite;
                Vector3 playerOffset = scope.Arena.PlayerAttackVisualOffset, enemyOffset = scope.Arena.EnemyAttackVisualOffset;
                scope.Arena.PresentBladeExchangePose(LegacyArenaView.OriginalImpactTime, 1f);
                Assert.That(scope.Arena.PlayerRenderer.sprite, Is.Not.SameAs(playerNatural));

                scope.Arena.Tick(0f, 0f);
                Assert.That(scope.Arena.PlayerRenderer.sprite, Is.SameAs(playerNatural));
                Assert.That(scope.Arena.EnemyRenderer.sprite, Is.SameAs(enemyNatural));
                Assert.That(scope.Arena.PlayerAttackVisualOffset, Is.EqualTo(playerOffset));
                Assert.That(scope.Arena.EnemyAttackVisualOffset, Is.EqualTo(enemyOffset));
                scope.Arena.Tick(.01f, .01f);
                Assert.That(Read<float>(Actor(scope.Arena, "player"), "AnimationTime"), Is.EqualTo(naturalTime + .01f).Within(.00001f));
                Assert.That(Read<float>(Actor(scope.Arena, "enemy"), "AnimationTime"), Is.EqualTo(naturalTime + .01f).Within(.00001f));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator GuardCanDeflectWithoutLeavingItsStance_AndNeitherSideConsumesARealStrikeTrail()
        {
            yield return null;
            using (var scope = new ArenaScope())
            foreach (bool playerGuards in new[] { false, true })
            {
                scope.Begin(playerGuards ? Guard() : Attack(), playerGuards ? Attack() : Guard());
                SpriteRenderer guard = playerGuards ? scope.Arena.PlayerRenderer : scope.Arena.EnemyRenderer;
                Sprite guardPose = guard.sprite;
                Color playerColor = scope.Arena.PlayerRenderer.color, enemyColor = scope.Arena.EnemyRenderer.color;
                int trails = scope.Arena.WeaponTrailEmissionCount;
                for (int repeat = 0; repeat < 3; repeat++)
                    scope.Arena.PresentBladeExchangePose(LegacyArenaView.OriginalImpactTime, 1f);
                Assert.That(guard.sprite, Is.SameAs(guardPose));
                Vector3 guardOffset = playerGuards ? scope.Arena.PlayerAttackVisualOffset : scope.Arena.EnemyAttackVisualOffset;
                Assert.That(guardOffset.x, Is.EqualTo(playerGuards ? -.10f : .10f).Within(.00001f));
                Assert.That(guardOffset.y, Is.EqualTo(.02f).Within(.00001f));
                Assert.That(scope.Arena.PlayerRenderer.color, Is.EqualTo(playerColor));
                Assert.That(scope.Arena.EnemyRenderer.color, Is.EqualTo(enemyColor));
                Assert.That(scope.Arena.WeaponTrailEmissionCount, Is.EqualTo(trails));
                Assert.That(scope.Arena.ActiveAttackEchoCount, Is.Zero);
                Assert.That(scope.Arena.ImpactGlow.ActiveCount, Is.Zero);
                Assert.That(scope.Arena.ActiveParticleCount, Is.Zero);
                Invoke(scope.Arena, "PresentWeaponTrailContact", 0, !playerGuards, playerGuards);
                Assert.That(scope.Arena.WeaponTrailEmissionCount, Is.EqualTo(trails + 1));
                Invoke(scope.Arena, "PresentWeaponTrailContact", 0, !playerGuards, playerGuards);
                Assert.That(scope.Arena.WeaponTrailEmissionCount, Is.EqualTo(trails + 1), "Only the actual strike consumes its trail identity.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator InactiveSuspendedAndDefeatedFiguresDoNotReceiveThePoseOverlay()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                Sprite idle = scope.Arena.PlayerRenderer.sprite;
                scope.Arena.PresentBladeExchangePose(LegacyArenaView.OriginalImpactTime, 1f);
                Assert.That(scope.Arena.PlayerRenderer.sprite, Is.SameAs(idle));
                Assert.That(scope.Arena.PlayerAttackVisualOffset, Is.EqualTo(Vector3.zero));
                scope.Begin(Attack(), Attack());
                Sprite before = scope.Arena.PlayerRenderer.sprite;
                Invoke(scope.Arena, "SuspendForCutscene");
                scope.Arena.PresentBladeExchangePose(LegacyArenaView.OriginalImpactTime, 1f);
                Assert.That(scope.Arena.PlayerRenderer.sprite, Is.SameAs(before));
                Invoke(scope.Arena, "ResumeAfterCutscene");
                before = scope.Arena.PlayerRenderer.sprite;
                Write(Actor(scope.Arena, "player"), "Defeated", true);
                scope.Arena.PresentBladeExchangePose(LegacyArenaView.OriginalImpactTime, 1f);
                Assert.That(scope.Arena.PlayerRenderer.sprite, Is.SameAs(before));
                scope.Arena.Dispose();
                Assert.DoesNotThrow(() => scope.Arena.PresentBladeExchangePose(LegacyArenaView.OriginalImpactTime, 1f));
            }
            yield return null;
        }

        private static LegacySkill Attack() => new LegacySkill(-9311, "맞대기 동작 시험", 0, 10, 10,
            LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, string.Empty);
        private static LegacySkill Guard() => new LegacySkill(-9312, "맞대기 방어 시험", 0, 10, 10,
            LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 0, string.Empty);

        private sealed class ArenaScope : IDisposable
        {
            private readonly GameObject host = new GameObject("Blade Exchange Pose Test");
            private readonly LegacyDuelArt art = new LegacyDuelArt();
            private readonly DuelPresentationSettings settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            public readonly LegacyArenaView Arena;

            public ArenaScope()
            {
                JsonUtility.FromJsonOverwrite("{\"autoDepthEnabled\":false,\"attackRhythmEnabled\":true," +
                    "\"attackRhythmStrength\":1,\"attackLungeDistance\":0.6,\"weaponTrailEnabled\":true," +
                    "\"weaponTrailStrength\":1,\"weaponTrailSeconds\":0.12}", settings);
                Arena = LegacyArenaView.Create(host.transform, art, settings);
                Arena.ArenaCamera.enabled = false;
            }

            public void Begin(LegacySkill player, LegacySkill enemy)
            {
                Arena.Reset();
                Arena.SetEnemyAppearance(EnemyAppearance.Elisa);
                Arena.PlayerRenderer.transform.localPosition = new Vector3(-2f, Arena.ActorGroundY, 0f);
                Arena.EnemyRenderer.transform.localPosition = new Vector3(2f, Arena.ActorGroundY, 0f);
                Arena.SetWeaponTrailOverride(true);
                Invoke(Arena, "ConfigureSlotTiming", 1f, .1f);
                Arena.BeginSlot(player, enemy);
            }

            public void Dispose()
            {
                Arena.Dispose();
                art.Dispose();
                Object.Destroy(settings);
                Object.Destroy(host);
            }
        }

        private static object Actor(LegacyArenaView arena, string name) => typeof(LegacyArenaView).GetField(name, Private).GetValue(arena);
        private static T Read<T>(object actor, string name) => (T)actor.GetType().GetField(name).GetValue(actor);
        private static void Write<T>(object actor, string name, T value) => actor.GetType().GetField(name).SetValue(actor, value);
        private static void Invoke(object target, string name, params object[] arguments)
            => target.GetType().GetMethod(name, Private).Invoke(target, arguments);
    }
}
