using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class BladeExchangeContactPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int TintId = Shader.PropertyToID("_Tint");

        [UnityTest]
        public IEnumerator LightContactLeavesBodiesAndCameraAlone_AndPreservesBothFirstStrikeTrails()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                scope.BeginAttack();
                LegacyArenaView arena = scope.Arena;
                Vector3 player = arena.PlayerRenderer.transform.position, enemy = arena.EnemyRenderer.transform.position;
                Vector3 playerTarget = arena.PlayerKnockbackTarget, enemyTarget = arena.EnemyKnockbackTarget;
                Vector3 playerOffset = arena.PlayerAttackVisualOffset, enemyOffset = arena.EnemyAttackVisualOffset;
                Sprite playerPose = arena.PlayerRenderer.sprite, enemyPose = arena.EnemyRenderer.sprite;
                Color playerColor = arena.PlayerRenderer.color, enemyColor = arena.EnemyRenderer.color;
                Vector3 camera = arena.ArenaCamera.transform.position;
                int trails = arena.WeaponTrailEmissionCount;
                int particles = arena.ActiveParticleCount;

                arena.PresentBladeExchangeContact(.3f);
                arena.PresentBladeExchangeContact(.3f);
                arena.Tick(0f, 0f);
                Assert.That(arena.ImpactGlow.ActiveCount, Is.EqualTo(2));
                Assert.That(arena.PlayerRenderer.transform.position, Is.EqualTo(player));
                Assert.That(arena.EnemyRenderer.transform.position, Is.EqualTo(enemy));
                Assert.That(arena.PlayerKnockbackTarget, Is.EqualTo(playerTarget));
                Assert.That(arena.EnemyKnockbackTarget, Is.EqualTo(enemyTarget));
                Assert.That(arena.HasPendingPush || arena.IsPursuing || arena.IsBladeBinding || arena.IsFatalFocus, Is.False);
                Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(playerPose), "Contact does not switch the attack into a body reaction.");
                Assert.That(arena.EnemyRenderer.sprite, Is.SameAs(enemyPose));
                Assert.That(arena.PlayerRenderer.color, Is.EqualTo(playerColor));
                Assert.That(arena.EnemyRenderer.color, Is.EqualTo(enemyColor));
                Assert.That(arena.PlayerAttackVisualOffset, Is.EqualTo(playerOffset));
                Assert.That(arena.EnemyAttackVisualOffset, Is.EqualTo(enemyOffset));
                Assert.That(arena.ArenaCamera.transform.position, Is.EqualTo(camera));
                Assert.That(arena.CameraShakeAmplitude, Is.Zero);
                Assert.That(arena.ActiveParticleCount, Is.EqualTo(particles), "The light exchange adds no full hit particles.");
                Assert.That(arena.WeaponTrailEmissionCount, Is.EqualTo(trails));

                Invoke(arena, "PresentWeaponTrailContact", 0, true, true);
                Assert.That(arena.WeaponTrailEmissionCount, Is.EqualTo(trails + 2),
                    "Both real first hits still draw their trails after the two cosmetic contacts.");
                Invoke(arena, "PresentWeaponTrailContact", 0, true, true);
                Assert.That(arena.WeaponTrailEmissionCount, Is.EqualTo(trails + 2), "The actual hit mask still suppresses duplicate real contacts.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator LightContactUsesTheBladeMidpointAndCoolGlow_AndDoesNotIncreaseAnExistingPush()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                scope.BeginAttack();
                LegacyArenaView arena = scope.Arena;
                arena.PresentHit(true, 1, 0, false, false, 20);
                Assert.That(arena.HasPendingPush, Is.True, "The fixture's blunt impact creates a real pending push.");
                Vector3 target = arena.EnemyKnockbackTarget;
                Vector3 position = arena.EnemyRenderer.transform.position;
                Vector3 midpoint = (arena.PlayerRenderer.transform.position + position) * .5f;
                arena.ImpactGlow.Reset();
                arena.PresentBladeExchangeContact(.3f);
                MeshRenderer glow = GlowRenderers(scope.Host).Single();
                Assert.That(glow.transform.position, Is.EqualTo(midpoint));
                Color tint = Properties(glow).GetColor(TintId);
                Assert.That(tint.g, Is.GreaterThan(tint.r));
                Assert.That(tint.b, Is.GreaterThan(tint.r));
                Assert.That(arena.EnemyKnockbackTarget, Is.EqualTo(target));
                Assert.That(arena.EnemyRenderer.transform.position, Is.EqualTo(position));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator WeakGlowRetainsItsOwnScaleDuringLiveTuning_AndPoolReuseRestoresFullImpacts()
        {
            yield return null;
            var host = new GameObject("Weak Blade Glow Test");
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            var glow = new DuelImpactGlow(host.transform, 30, settings);
            try
            {
                Assert.That(glow.HasRequiredAssets, Is.True);
                glow.Emit(Vector3.zero, true, false, .3f);
                glow.Emit(Vector3.right, true, false);
                MeshRenderer[] views = GlowRenderers(host);
                Assert.That(views.Length, Is.EqualTo(2));
                MeshRenderer weak = views.Single(view => view.transform.position == Vector3.zero);
                MeshRenderer full = views.Single(view => view.transform.position == Vector3.right);
                Assert.That(weak.transform.localScale.x / full.transform.localScale.x,
                    Is.EqualTo(Mathf.Sqrt(.3f)).Within(.001f));
                Assert.That(Properties(weak).GetFloat(IntensityId) / Properties(full).GetFloat(IntensityId),
                    Is.EqualTo(.3f).Within(.001f));
                Assert.That(full.transform.localScale.x, Is.EqualTo(settings.GlowRadius * 2f).Within(.001f));
                Assert.That(Properties(full).GetFloat(IntensityId), Is.EqualTo(settings.GlowIntensity).Within(.001f));

                JsonUtility.FromJsonOverwrite("{\"glowIntensity\":4.5,\"glowRadius\":1.4}", settings);
                glow.Tick(0f);
                Assert.That(weak.transform.localScale.x, Is.EqualTo(2.8f * Mathf.Sqrt(.3f)).Within(.001f));
                Assert.That(Properties(weak).GetFloat(IntensityId), Is.EqualTo(4.5f * .3f).Within(.001f));
                Assert.That(full.transform.localScale.x, Is.EqualTo(2.8f).Within(.001f));
                Assert.That(Properties(full).GetFloat(IntensityId), Is.EqualTo(4.5f).Within(.001f));
                glow.Tick(settings.GlowDuration);
                Assert.That(glow.ActiveCount, Is.Zero);
                glow.Emit(Vector3.up, false, false);
                MeshRenderer reused = GlowRenderers(host).Single();
                Assert.That(reused, Is.SameAs(weak));
                Assert.That(reused.transform.localScale.x, Is.EqualTo(2.8f).Within(.001f));
                Assert.That(Properties(reused).GetFloat(IntensityId), Is.EqualTo(4.5f).Within(.001f),
                    "A weak contact must not leave a weak multiplier on the next pooled damage impact.");
                int active = glow.ActiveCount;
                foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
                    glow.Emit(Vector3.one, true, false, invalid);
                Assert.That(glow.ActiveCount, Is.EqualTo(active));
            }
            finally
            {
                glow.Dispose();
                Object.Destroy(settings);
                Object.Destroy(host);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ContactOnlyAppearsInAnActiveSlot_AndRespectsCutsceneSuspensionAndDisposal()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                LegacyArenaView arena = scope.Arena;
                arena.PresentBladeExchangeContact(.3f);
                Assert.That(arena.ImpactGlow.ActiveCount, Is.Zero, "Planning has no exchanged blades.");
                arena.BeginApproach();
                arena.PresentBladeExchangeContact(.3f);
                Assert.That(arena.ImpactGlow.ActiveCount, Is.Zero);
                scope.BeginAttack();
                Invoke(arena, "SuspendForCutscene");
                arena.PresentBladeExchangeContact(.3f);
                Assert.That(arena.ImpactGlow.ActiveCount, Is.Zero);
                Invoke(arena, "ResumeAfterCutscene");
                arena.PresentBladeExchangeContact(.3f);
                Assert.That(arena.ImpactGlow.ActiveCount, Is.EqualTo(1));
                arena.EndTurn();
                arena.PresentBladeExchangeContact(.3f);
                Assert.That(arena.ImpactGlow.ActiveCount, Is.EqualTo(1), "Returning cannot add another contact.");
                arena.Dispose();
                Assert.DoesNotThrow(() => arena.PresentBladeExchangeContact(.3f));
            }
            yield return null;
        }

        private sealed class ArenaScope : IDisposable
        {
            public readonly GameObject Host = new GameObject("Blade Exchange Contact Test");
            public readonly DuelPresentationSettings Settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            private readonly LegacyDuelArt art = new LegacyDuelArt();
            public readonly LegacyArenaView Arena;

            public ArenaScope()
            {
                JsonUtility.FromJsonOverwrite("{\"weaponTrailEnabled\":true,\"weaponTrailStrength\":1,\"weaponTrailSeconds\":0.12}", Settings);
                Arena = LegacyArenaView.Create(Host.transform, art, Settings);
            }

            public void BeginAttack()
            {
                Arena.Reset();
                Arena.SetEnemyAppearance(EnemyAppearance.Elisa);
                Arena.PlayerRenderer.transform.localPosition = new Vector3(-2f, Arena.ActorGroundY, 0f);
                Arena.EnemyRenderer.transform.localPosition = new Vector3(2f, Arena.ActorGroundY, 0f);
                Arena.SetWeaponTrailOverride(true);
                Invoke(Arena, "ConfigureSlotTiming", 1f, .1f);
                var attack = new LegacySkill(-9301, "검 맞대기 시험", 0, 10, 10,
                    LegacySkillKind.Attack, LegacySkillProperty.Hit, 1, 0, string.Empty);
                Arena.BeginSlot(attack, attack);
                Invoke(Arena, "HoldSlotAtTime", LegacyArenaView.OriginalImpactTime);
            }

            public void Dispose()
            {
                Arena.Dispose();
                art.Dispose();
                Object.Destroy(Settings);
                Object.Destroy(Host);
            }
        }

        private static MeshRenderer[] GlowRenderers(GameObject host)
            => host.GetComponentsInChildren<MeshRenderer>().Where(renderer => renderer.gameObject.activeInHierarchy &&
                renderer.name.StartsWith("Impact Glow ", StringComparison.Ordinal)).ToArray();

        private static MaterialPropertyBlock Properties(MeshRenderer renderer)
        {
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            return properties;
        }

        private static void Invoke(object target, string name, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(name, Private);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(target, arguments);
        }
    }
}
