using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Presentation;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class ImpactGlowPlayModeTests
    {
        private const int TestLayer = 30;
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        [UnityTest]
        public IEnumerator ArenaBloom_IsBoundedAndLive_WithoutEditingInheritedMaterials()
        {
            var host = new GameObject("Arena Bloom Test Host");
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            var sparks = Resources.Load<Material>("LegacyArena/VFX/Graphics/cfxr stretch rectangle ray ab");
            Assert.That(sparks, Is.Not.Null);
            Color inheritedHdr = sparks.GetColor("_Color");
            var arena = LegacyArenaView.Create(host.transform, new LegacyDuelArt(), settings);
            try
            {
                Assert.That(arena.ArenaProfile.TryGet(out Bloom bloom), Is.True);
                Assert.That(bloom.clamp.overrideState, Is.True);
                Assert.That(bloom.clamp.value, Is.EqualTo(4f));
                Assert.That(bloom.active, Is.True);

                JsonUtility.FromJsonOverwrite("{\"bloomIntensity\":0.4,\"bloomThreshold\":2}", settings);
                arena.Tick(0f, 0f);
                Assert.That(bloom.intensity.value, Is.EqualTo(.4f).Within(.001f));
                Assert.That(bloom.threshold.value, Is.EqualTo(2f));
                JsonUtility.FromJsonOverwrite("{\"glowEnabled\":false}", settings);
                arena.Tick(0f, 0f);
                Assert.That(bloom.active, Is.False);
                arena.PresentHit(true, 1, 0, false, false, 1);
                // The tiny initialization simulation need not spawn rays yet;
                // advance a normal presentation step before checking the burst.
                arena.Tick(.05f, .05f);
                Assert.That(arena.ImpactGlow.ActiveCount, Is.Zero);
                Assert.That(arena.ActiveParticleCount, Is.GreaterThan(0),
                    "Turning off new light must preserve the inherited impact particles.");
                Assert.That(sparks.GetColor("_Color"), Is.EqualTo(inheritedHdr),
                    "The brightness limit belongs to runtime Bloom, never the shared legacy material.");
            }
            finally
            {
                arena.Dispose();
                Object.Destroy(settings);
                Object.Destroy(host);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShaderLoads_AndEmitCreatesDistinctHdrImpactVariants()
        {
            var host = new GameObject("Impact Glow Test Host");
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            var glow = new DuelImpactGlow(host.transform, TestLayer, settings);
            try
            {
                Shader shader = Resources.Load<Shader>("DuelVFX/ImpactGlow");
                Assert.That(shader, Is.Not.Null);
                Assert.That(shader.isSupported, Is.True);
                Assert.That(glow.HasRequiredAssets, Is.True);

                Vector3 position = new Vector3(2.5f, -1.25f, 0f);
                glow.Emit(position, false, false);
                glow.Emit(position + Vector3.right, true, false);
                glow.Emit(position + Vector3.right * 2f, false, true);

                Assert.That(glow.ActiveCount, Is.EqualTo(3));
                MeshRenderer[] renderers = host.GetComponentsInChildren<MeshRenderer>();
                Assert.That(renderers, Has.Length.EqualTo(3));
                Material shared = renderers[0].sharedMaterial;
                foreach (MeshRenderer renderer in renderers)
                {
                    Assert.That(renderer.gameObject.layer, Is.EqualTo(TestLayer));
                    Assert.That(renderer.sharedMaterial, Is.SameAs(shared));
                    Assert.That(renderer.sharedMaterial.shader, Is.SameAs(shader));
                    Assert.That(renderer.transform.localScale.x, Is.EqualTo(settings.GlowRadius * 2f).Within(.001f));
                }
                Assert.That(renderers[0].transform.position, Is.EqualTo(position));

                Color warm = GetTint(renderers[0]);
                Color guard = GetTint(renderers[1]);
                Color fatal = GetTint(renderers[2]);
                Assert.That(warm.r, Is.GreaterThan(warm.g).And.GreaterThan(warm.b));
                Assert.That(guard.g, Is.GreaterThan(guard.r));
                Assert.That(guard.b, Is.GreaterThan(guard.r));
                Assert.That(fatal.r, Is.GreaterThan(fatal.g).And.GreaterThan(fatal.b));
                Assert.That(GetIntensity(renderers[0]), Is.EqualTo(settings.GlowIntensity).Within(.001f));

                JsonUtility.FromJsonOverwrite("{\"glowIntensity\":4.5,\"glowRadius\":1.4}", settings);
                glow.Tick(0f);
                Assert.That(GetIntensity(renderers[0]), Is.EqualTo(4.5f).Within(.001f));
                Assert.That(renderers[0].transform.localScale.x, Is.EqualTo(2.8f).Within(.001f),
                    "Active flashes must follow live authoring values without rebuilding their renderers.");
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
        public IEnumerator RealTimeLifetime_CapsAtSixteen_ThenReusesThePool()
        {
            var host = new GameObject("Impact Glow Pool Test Host");
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            var glow = new DuelImpactGlow(host.transform, TestLayer, settings);
            try
            {
                for (int index = 0; index < 20; index++)
                    glow.Emit(new Vector3(index, 0f, 0f), false, false);

                Assert.That(glow.ActiveCount, Is.EqualTo(16));
                MeshRenderer[] pooled = host.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(pooled, Has.Length.EqualTo(16));
                var pooledViews = new HashSet<MeshRenderer>(pooled);

                glow.Tick(0f);
                Assert.That(glow.ActiveCount, Is.EqualTo(16));
                float initialDuration = settings.GlowDuration;
                glow.Tick(initialDuration * 0.5f);
                Assert.That(glow.ActiveCount, Is.EqualTo(16));
                JsonUtility.FromJsonOverwrite("{\"glowDuration\":0.05}", settings);
                glow.Tick(0f);
                Assert.That(glow.ActiveCount, Is.Zero,
                    "The accumulated real time must be compared against the live duration setting.");

                glow.Emit(Vector3.one, false, false);
                Assert.That(glow.ActiveCount, Is.EqualTo(1));
                MeshRenderer[] reused = host.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(reused, Has.Length.EqualTo(16));
                foreach (MeshRenderer renderer in reused)
                    Assert.That(pooledViews.Contains(renderer), Is.True,
                        "Expired flashes must be reused instead of allocating beyond the capped pool.");
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
        public IEnumerator DisabledOrZeroIntensity_ImmediatelyClearsActiveGlows()
        {
            var host = new GameObject("Impact Glow Settings Test Host");
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            var glow = new DuelImpactGlow(host.transform, TestLayer, settings);
            try
            {
                glow.Emit(Vector3.zero, false, false);
                Assert.That(glow.ActiveCount, Is.EqualTo(1));

                JsonUtility.FromJsonOverwrite("{\"glowEnabled\":false}", settings);
                glow.Tick(0f);
                Assert.That(glow.ActiveCount, Is.Zero);
                glow.Emit(Vector3.zero, false, false);
                Assert.That(glow.ActiveCount, Is.Zero);

                JsonUtility.FromJsonOverwrite("{\"glowEnabled\":true,\"glowIntensity\":3}", settings);
                glow.Emit(Vector3.zero, true, false);
                Assert.That(glow.ActiveCount, Is.EqualTo(1));
                JsonUtility.FromJsonOverwrite("{\"glowIntensity\":0}", settings);
                glow.Emit(Vector3.zero, false, true);
                Assert.That(glow.ActiveCount, Is.Zero);
            }
            finally
            {
                glow.Dispose();
                Object.Destroy(settings);
                Object.Destroy(host);
            }
            yield return null;
        }

        private static Color GetTint(MeshRenderer renderer)
        {
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            return properties.GetColor(TintId);
        }

        private static float GetIntensity(MeshRenderer renderer)
        {
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            return properties.GetFloat(IntensityId);
        }
    }
}
