using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Presentation;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class ReadableCameraPlayModeTests
    {
        [UnityTest]
        public IEnumerator OrdinaryAndSimultaneousHits_DoNotTeleportOrZoomTheCameraAtImpact()
        {
            using (var fixture = new ArenaFixture())
            {
                Camera camera = fixture.Arena.ArenaCamera;
                Vector3 position = camera.transform.localPosition;
                Quaternion rotation = camera.transform.localRotation;
                float size = camera.orthographicSize;

                fixture.Arena.PresentHit(true, 0, 0, false, false, 0);
                AssertCamera(camera, position, rotation, size);
                fixture.Arena.PresentHit(false, 0, 0, false, false, 0);
                AssertCamera(camera, position, rotation, size);
                fixture.Arena.Tick(0f, .02f);
                Assert.That(Vector3.Distance(camera.transform.localPosition, position),
                    Is.LessThanOrEqualTo(fixture.Settings.ImpactCameraShake + .001f));
                Assert.That(camera.orthographicSize, Is.EqualTo(size).Within(.001f));
                Assert.That(fixture.Arena.ImpactGlow.ActiveCount, Is.EqualTo(2),
                    "Improved framing must not suppress either side's impact effect.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedImpacts_RemainBounded_InsteadOfAccumulatingCameraDisplacement()
        {
            using (var fixture = new ArenaFixture())
            {
                Camera camera = fixture.Arena.ArenaCamera;
                Vector3 baseline = camera.transform.localPosition;
                for (int hit = 0; hit < 40; hit++)
                {
                    fixture.Arena.PresentHit(hit % 2 == 0, 0, 0, false, false, 0);
                    fixture.Arena.Tick(0f, .012f);
                    Assert.That(Vector3.Distance(camera.transform.localPosition, baseline),
                        Is.LessThanOrEqualTo(fixture.Settings.ImpactCameraShake + .002f),
                        "A rapid or simultaneous hit must replace the micro impulse, not compound it.");
                    Assert.That(camera.orthographicSize,
                        Is.EqualTo(fixture.Settings.CombatCameraSize).Within(.001f));
                    Assert.That(Quaternion.Angle(camera.transform.localRotation, Quaternion.identity),
                        Is.LessThan(.01f));
                }
                fixture.Arena.Tick(0f, 3f);
                Assert.That(Vector3.Distance(camera.transform.localPosition, baseline), Is.LessThan(.002f));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ImpactImpulse_ExpiresOnRealTime_WhileTheCombatClockIsStopped()
        {
            using (var fixture = new ArenaFixture())
            {
                Camera camera = fixture.Arena.ArenaCamera;
                Vector3 baseline = camera.transform.localPosition;
                Vector3 player = fixture.Arena.PlayerRenderer.transform.localPosition;
                Vector3 enemy = fixture.Arena.EnemyRenderer.transform.localPosition;
                fixture.Arena.PresentHit(true, 0, 0, false, false, 0);
                fixture.Arena.Tick(0f, .02f);
                Assert.That(Vector3.Distance(camera.transform.localPosition, baseline), Is.GreaterThan(.0001f),
                    "The small directional impulse remains visible when it is enabled.");
                fixture.Arena.Tick(0f, 3f);
                Assert.That(Vector3.Distance(camera.transform.localPosition, baseline), Is.LessThan(.002f),
                    "Hit stop cannot hold a stale camera kick indefinitely.");
                Assert.That(fixture.Arena.PlayerRenderer.transform.localPosition, Is.EqualTo(player));
                Assert.That(fixture.Arena.EnemyRenderer.transform.localPosition, Is.EqualTo(enemy));
                Assert.That(fixture.Arena.ImpactGlow.ActiveCount, Is.Zero);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DeadZone_HoldsSmallCenterChanges_ButRealTimeFollowTracksContinuousTravel()
        {
            using (var fixture = new ArenaFixture())
            {
                Camera camera = fixture.Arena.ArenaCamera;
                float start = camera.transform.localPosition.x;
                fixture.TranslateDuel(fixture.Settings.CameraFollowDeadZone * .5f);
                fixture.Arena.Tick(0f, 1f);
                Assert.That(camera.transform.localPosition.x, Is.EqualTo(start).Within(.001f),
                    "Minor alternating recoil should not drag the entire background along with it.");

                fixture.TranslateDuel(4f);
                fixture.Arena.Tick(0f, .02f);
                Assert.That(camera.transform.localPosition.x, Is.GreaterThan(start + .01f));
                Assert.That(camera.transform.localPosition.x, Is.LessThan(fixture.Arena.DuelCenter.x - .01f),
                    "The follow should ease into travel rather than teleport to its new center.");
                float previous = camera.transform.localPosition.x;
                for (int frame = 0; frame < 30; frame++)
                {
                    fixture.TranslateDuel(.06f);
                    fixture.Arena.Tick(0f, 1f / 60f);
                    Assert.That(camera.transform.localPosition.x, Is.GreaterThanOrEqualTo(previous - .001f));
                    previous = camera.transform.localPosition.x;
                }
                fixture.Arena.Tick(0f, 3f);
                Assert.That(Mathf.Abs(camera.transform.localPosition.x - fixture.Arena.DuelCenter.x),
                    Is.LessThanOrEqualTo(fixture.Settings.CameraFollowDeadZone + .02f));
                Assert.That(fixture.Arena.Separation, Is.EqualTo(LegacyArenaView.ContactDistance).Within(.001f));
                Assert.That(camera.orthographicSize,
                    Is.EqualTo(fixture.Settings.CombatCameraSize).Within(.001f));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator NormalFraming_SizeStaysStableAcrossAttackPosesAndMultiHitSkills()
        {
            using (var fixture = new ArenaFixture())
            {
                var poses = new HashSet<Sprite>();
                foreach (int skillId in new[] { 1, 3, 6, 7 })
                {
                    LegacySkill skill = LegacySkillDefinitions.Skill(skillId);
                    fixture.Arena.BeginSlot(skill, skill);
                    poses.Add(fixture.Arena.PlayerRenderer.sprite);
                    for (int hit = 0; hit < skill.AttackCount; hit++)
                    {
                        fixture.Arena.Tick(.09f, .09f);
                        poses.Add(fixture.Arena.PlayerRenderer.sprite);
                        fixture.Arena.PresentHit(true, 0, 0, false, false, 0);
                        fixture.Arena.PresentHit(false, 0, 0, false, false, 0);
                        fixture.Arena.Tick(.08f, .08f);
                        Assert.That(fixture.Arena.ArenaCamera.orthographicSize,
                            Is.EqualTo(fixture.Settings.CombatCameraSize).Within(.002f),
                            "Neither changing sprite bounds nor each hit in a multi-hit skill may trigger auto zoom.");
                    }
                }
                Assert.That(poses.Count, Is.GreaterThan(3), "The test must sample genuinely different attack/guard poses.");
                Assert.That(fixture.Arena.HasPendingPush, Is.False);
                fixture.Arena.Tick(.05f, .05f);
                Assert.That(fixture.Arena.ActiveParticleCount, Is.GreaterThan(0),
                    "The camera-only change preserves the inherited collision sparks.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CameraAuthoring_DefaultsClampSafely_AndApplyWithoutChangingTempoOrLight()
        {
            using (var fixture = new ArenaFixture())
            {
                DuelPresentationSettings settings = fixture.Settings;
                Assert.That(settings.CombatCameraSize, Is.EqualTo(3.5f));
                Assert.That(settings.CameraFollowSharpness, Is.EqualTo(6f));
                Assert.That(settings.CameraFollowDeadZone, Is.EqualTo(.35f));
                Assert.That(settings.ImpactCameraShake, Is.EqualTo(.08f));

                JsonUtility.FromJsonOverwrite("{\"combatCameraSize\":99,\"cameraFollowSharpness\":0," +
                    "\"cameraFollowDeadZone\":99,\"impactCameraShake\":-1}", settings);
                Assert.That(settings.CombatCameraSize, Is.EqualTo(6f));
                Assert.That(settings.CameraFollowSharpness, Is.EqualTo(1f));
                Assert.That(settings.CameraFollowDeadZone, Is.EqualTo(1.5f));
                Assert.That(settings.ImpactCameraShake, Is.Zero);
                JsonUtility.FromJsonOverwrite("{\"combatCameraSize\":0,\"cameraFollowSharpness\":99," +
                    "\"cameraFollowDeadZone\":-1,\"impactCameraShake\":99}", settings);
                Assert.That(settings.CombatCameraSize, Is.EqualTo(2.8f));
                Assert.That(settings.CameraFollowSharpness, Is.EqualTo(20f));
                Assert.That(settings.CameraFollowDeadZone, Is.Zero);
                Assert.That(settings.ImpactCameraShake, Is.EqualTo(.35f));
                Set(settings, "combatCameraSize", float.NaN);
                Set(settings, "cameraFollowSharpness", float.PositiveInfinity);
                Set(settings, "cameraFollowDeadZone", float.NegativeInfinity);
                Set(settings, "impactCameraShake", float.NaN);
                Assert.That(settings.CombatCameraSize, Is.EqualTo(3.5f));
                Assert.That(settings.CameraFollowSharpness, Is.EqualTo(6f));
                Assert.That(settings.CameraFollowDeadZone, Is.EqualTo(.35f));
                Assert.That(settings.ImpactCameraShake, Is.EqualTo(.08f));

                JsonUtility.FromJsonOverwrite("{\"combatCameraSize\":4.4,\"impactCameraShake\":0}", settings);
                fixture.Arena.Tick(0f, 3f);
                Assert.That(fixture.Arena.ArenaCamera.orthographicSize, Is.EqualTo(4.4f).Within(.001f));
                Vector3 position = fixture.Arena.ArenaCamera.transform.localPosition;
                fixture.Arena.PresentHit(true, 0, 0, false, false, 0);
                fixture.Arena.Tick(0f, .02f);
                Assert.That(fixture.Arena.ArenaCamera.transform.localPosition, Is.EqualTo(position));
                Assert.That(settings.AnimationPlaybackSpeed, Is.EqualTo(1f));
                Assert.That(settings.AttackInterval, Is.EqualTo(.1f));
                Assert.That(settings.SkillInterval, Is.EqualTo(.28f));
                Assert.That(settings.GlowIntensity, Is.EqualTo(3f));
                Assert.That(settings.BloomIntensity, Is.EqualTo(.65f));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator PhaseEntryAndReset_ClearPendingImpactFeedback_WithoutChangingWorldTravel()
        {
            using (var reference = new ArenaFixture())
            using (var impacted = new ArenaFixture())
            {
                Action<LegacyArenaView>[] phases =
                {
                    arena => arena.BeginTurn(), arena => arena.BeginApproach(),
                    arena => arena.EndTurn(), arena => arena.Reset()
                };
                foreach (Action<LegacyArenaView> phase in phases)
                {
                    reference.PrepareCombat();
                    impacted.PrepareCombat();
                    reference.TranslateDuel(18f);
                    impacted.TranslateDuel(18f);
                    reference.Arena.Tick(0f, 3f);
                    impacted.Arena.Tick(0f, 3f);
                    Vector3 player = impacted.Arena.PlayerRenderer.transform.localPosition;
                    Vector3 enemy = impacted.Arena.EnemyRenderer.transform.localPosition;
                    impacted.Arena.PresentHit(true, 0, 0, false, false, 0);
                    phase(reference.Arena);
                    phase(impacted.Arena);
                    reference.Arena.Tick(0f, .02f);
                    impacted.Arena.Tick(0f, .02f);
                    AssertCamera(impacted.Arena.ArenaCamera,
                        reference.Arena.ArenaCamera.transform.localPosition,
                        reference.Arena.ArenaCamera.transform.localRotation,
                        reference.Arena.ArenaCamera.orthographicSize);
                    if (phase != phases[3])
                    {
                        Assert.That(impacted.Arena.PlayerRenderer.transform.localPosition, Is.EqualTo(player));
                        Assert.That(impacted.Arena.EnemyRenderer.transform.localPosition, Is.EqualTo(enemy));
                    }
                    else
                    {
                        Assert.That(impacted.Arena.ArenaCamera.orthographicSize, Is.EqualTo(6f));
                        Assert.That(impacted.Arena.ArenaCamera.transform.localPosition.x, Is.Zero);
                        Assert.That(impacted.Arena.ImpactGlow.ActiveCount, Is.Zero);
                    }
                }
            }
            yield return null;
        }

        private static void AssertCamera(Camera camera, Vector3 position, Quaternion rotation, float size)
        {
            Assert.That(Vector3.Distance(camera.transform.localPosition, position), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(camera.transform.localRotation, rotation), Is.LessThan(.01f));
            Assert.That(camera.orthographicSize, Is.EqualTo(size).Within(.0001f));
        }

        private static void Set(DuelPresentationSettings settings, string name, float value)
        {
            FieldInfo field = typeof(DuelPresentationSettings).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing camera authoring field " + name);
            field.SetValue(settings, value);
        }

        private sealed class ArenaFixture : IDisposable
        {
            private readonly GameObject host;
            public DuelPresentationSettings Settings { get; }
            public LegacyArenaView Arena { get; }

            public ArenaFixture()
            {
                host = new GameObject("Readable Camera Test Host");
                Settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                Arena = LegacyArenaView.Create(host.transform, new LegacyDuelArt(), Settings);
                Arena.ArenaCamera.enabled = false;
                PrepareCombat();
            }

            public void PrepareCombat()
            {
                Arena.Reset();
                Arena.CloseDistance(1f);
                Arena.BeginSlot(LegacySkillDefinitions.Skill(1), LegacySkillDefinitions.Skill(1));
                Arena.Tick(0f, 3f);
            }

            public void TranslateDuel(float distance)
            {
                Arena.PlayerRenderer.transform.localPosition += Vector3.right * distance;
                Arena.EnemyRenderer.transform.localPosition += Vector3.right * distance;
            }

            public void Dispose()
            {
                Arena.Dispose();
                Object.Destroy(host);
                Object.Destroy(Settings);
            }
        }
    }
}
