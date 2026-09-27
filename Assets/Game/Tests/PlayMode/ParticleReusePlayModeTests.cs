using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class ParticleReusePlayModeTests
    {
        [UnityTest]
        public IEnumerator EightySequentialImpacts_KeepEmittingAndRetiringWithoutPoolGrowth()
        {
            var host = new GameObject("Repeated Impact Test Host");
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            var arena = LegacyArenaView.Create(host.transform, new LegacyDuelArt(), settings);
            var missedHits = new List<int>();
            var retainedHits = new List<int>();
            try
            {
                for (int hit = 1; hit <= 80; hit++)
                {
                    arena.PresentHit(true, 1, 0, false, false, 1);
                    arena.Tick(.1f, .1f);
                    if (arena.ActiveParticleCount == 0) missedHits.Add(hit);
                    arena.Tick(1f, 1f);
                    foreach (var particles in host.GetComponentsInChildren<ParticleSystem>())
                        if (particles.transform.parent.name == "Legacy Duel Arena")
                        {
                            retainedHits.Add(hit);
                            break;
                        }
                }
                int pooledSystems = host.GetComponentsInChildren<ParticleSystem>(true).Length;
                Debug.Log("PARTICLE REUSE hits=80 missed=" + string.Join(",", missedHits) +
                    " retained=" + string.Join(",", retainedHits) + " pooledSystems=" + pooledSystems);
                Assert.That(missedHits, Is.Empty, "Later hits must emit after pooled reuse.");
                Assert.That(retainedHits, Is.Empty, "Finished particle hierarchies must return to the pool.");
                Assert.That(pooledSystems, Is.EqualTo(2), "Sequential impacts must reuse one sparks/ring hierarchy.");
                arena.Reset();
                Assert.That(arena.ActiveParticleCount, Is.Zero);
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
        public IEnumerator SaturatedPool_RecoversAfterSimulation_AndRealTimeAloneDoesNotRetireIt()
        {
            var host = new GameObject("Saturated Impact Test Host");
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            var arena = LegacyArenaView.Create(host.transform, new LegacyDuelArt(), settings);
            try
            {
                for (int hit = 0; hit < 30; hit++) arena.PresentHit(true, 0, 0, false, false, 0);
                Assert.That(host.GetComponentsInChildren<ParticleSystem>(true), Has.Length.EqualTo(48),
                    "An instantaneous burst must respect the original 24-effect limit.");
                arena.Tick(0f, 1f);
                Assert.That(host.GetComponentsInChildren<ParticleSystem>(), Has.Length.EqualTo(48),
                    "Real-time glow decay must not retire pending particles on the paused combat clock.");
                arena.Tick(.1f, .1f);
                Assert.That(arena.ActiveParticleCount, Is.GreaterThan(0));
                arena.Tick(1f, 1f);
                Assert.That(host.GetComponentsInChildren<ParticleSystem>(), Is.Empty,
                    "All completed sparks and rings must retire, even after pool saturation.");
                arena.PresentHit(false, 0, 0, true, false, 0);
                arena.Tick(.1f, .1f);
                Assert.That(arena.ActiveParticleCount, Is.GreaterThan(0),
                    "The next impact must reuse a returned entry rather than disappear.");
                Assert.That(host.GetComponentsInChildren<ParticleSystem>(true), Has.Length.EqualTo(48));
                arena.Reset();
                Assert.That(host.GetComponentsInChildren<ParticleSystem>(), Is.Empty);
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
        public IEnumerator ReusedParticles_FollowImpactsAfterLongWorldTravel()
        {
            var host = new GameObject("Travelling Impact Test Host");
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            var arena = LegacyArenaView.Create(host.transform, new LegacyDuelArt(), settings);
            var particleBuffer = new ParticleSystem.Particle[100];
            try
            {
                foreach (float center in new[] { 0f, 100f, -100f, 500f, -500f })
                {
                    arena.PlayerRenderer.transform.position = new Vector3(center - 2f, -.5f, 0f);
                    arena.EnemyRenderer.transform.position = new Vector3(center + 2f, -.5f, 0f);
                    arena.PresentHit(true, 0, 0, false, false, 0);
                    arena.Tick(.1f, .1f);
                    Assert.That(arena.ActiveParticleCount, Is.GreaterThan(0));
                    foreach (var system in host.GetComponentsInChildren<ParticleSystem>())
                    {
                        int count = system.GetParticles(particleBuffer);
                        for (int index = 0; index < count; index++)
                        {
                            Vector3 worldPosition = particleBuffer[index].position;
                            if (system.main.simulationSpace == ParticleSystemSimulationSpace.Local)
                                worldPosition = system.transform.TransformPoint(worldPosition);
                            Assert.That(Mathf.Abs(worldPosition.x - center), Is.LessThan(5f),
                                "Reused particles must not remain at the previous impact's world position.");
                        }
                    }
                    arena.Tick(1f, 1f);
                }
                Assert.That(host.GetComponentsInChildren<ParticleSystem>(true), Has.Length.EqualTo(2));
            }
            finally
            {
                arena.Dispose();
                Object.Destroy(settings);
                Object.Destroy(host);
            }
            yield return null;
        }
    }
}
