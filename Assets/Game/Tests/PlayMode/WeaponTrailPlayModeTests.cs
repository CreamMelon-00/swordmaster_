using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class WeaponTrailPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const int Width = 960, Height = 540, CaptureLayer = 31;
        private const float Speed = .6f, Interval = .1f;
        private static float ClipSeconds => LegacyArenaView.OriginalClipDuration / Speed;
        private static float CycleSeconds => ClipSeconds + Interval;

        [UnityTest]
        public IEnumerator WeaponTrail_RendersNativeBladePixels_WithoutChangingTheExistingBodyMotion()
        {
            yield return null;
            using (var scope = new TrailScope())
            {
                scope.Begin(false);
                scope.HoldPhase(.5f);
                Stroke(scope.Arena, 1f / 60f, true);
                Pose before = scope.Pose;
                int oldEchoes = scope.Arena.ActiveAttackEchoCount;
                Assert.That(oldEchoes, Is.GreaterThan(0), "The existing body echoes stay enabled in both halves of this comparison.");
                Assert.That(scope.Arena.ActiveWeaponTrailCount, Is.Zero);
                scope.RenderTrailOnly();
                yield return null;
                Color32[] off = scope.ReadPixels();
                Assert.That(BrightPixels(off), Is.Zero, "The isolated weapon-trail layer contains no body, backdrop or old echoes.");

                int emissions = scope.Arena.WeaponTrailEmissionCount;
                scope.Begin(true);
                scope.HoldPhase(.5f);
                Stroke(scope.Arena, 1f / 60f, true);
                Assert.That(scope.Arena.WeaponTrailEmissionCount - emissions, Is.EqualTo(2), "Each fighter draws one blade stroke at contact.");
                Assert.That(scope.Arena.ActiveWeaponTrailCount, Is.EqualTo(2));
                Assert.That(scope.Arena.ActiveAttackEchoCount, Is.EqualTo(oldEchoes));
                AssertSamePose(scope.Pose, before);
                MeshRenderer[] meshes = scope.Arena.WeaponTrailRoot.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(meshes.Length, Is.GreaterThan(0));
                foreach (MeshRenderer mesh in meshes)
                    Assert.That(mesh.sharedMaterial.shader.isSupported, Is.True, "The actual procedural mesh shader must work on this render pipeline.");
                scope.RenderTrailOnly();
                yield return null;
                Color32[] on = scope.ReadPixels();
                Assert.That(BrightPixels(on), Is.GreaterThan(80),
                    "Read-back pixels must show the blade arc; an emission counter or enabled mesh alone cannot prove the effect is visible.");

                string output = Environment.GetEnvironmentVariable("SWORDMASTER_BLADE_TRAIL_CAPTURE_DIR");
                if (!string.IsNullOrEmpty(output))
                {
                    Directory.CreateDirectory(output);
                    SavePixels(off, Path.Combine(output, "trail-mask-off.png"));
                    SavePixels(on, Path.Combine(output, "trail-mask-on.png"));
                    var trace = new CaptureTrace();
                    var baseline = new List<Pose>();
                    foreach (bool enabled in new[] { false, true })
                    {
                        scope.Begin(enabled);
                        int firstEmission = scope.Arena.WeaponTrailEmissionCount;
                        scope.RenderWholeArena();
                        // Settle the render pipeline and volume without advancing the manually held attack.
                        yield return null;
                        yield return null;
                        for (int frame = 0; frame < 36; frame++)
                        {
                            float delta = frame == 0 ? 0f : 1f / 60f;
                            float time = frame / 60f;
                            scope.Arena.Tick(0f, delta);
                            scope.HoldTime(time);
                            Stroke(scope.Arena, delta, time < ClipSeconds);
                            scope.FixCamera();
                            if (!enabled) baseline.Add(scope.Pose);
                            else AssertSamePose(scope.Pose, baseline[frame]);
                            yield return null;
                            SavePixels(scope.ReadPixels(), Path.Combine(output, (enabled ? "on-" : "off-") + frame.ToString("00") + ".png"));
                            trace.frames.Add(new CaptureFrame
                            {
                                weaponTrail = enabled, frame = frame, time = time,
                                playerLogical = scope.Arena.PlayerRenderer.transform.position,
                                enemyLogical = scope.Arena.EnemyRenderer.transform.position,
                                playerDrawOffset = scope.Arena.PlayerAttackVisualOffset,
                                enemyDrawOffset = scope.Arena.EnemyAttackVisualOffset,
                                bodyEchoes = scope.Arena.ActiveAttackEchoCount,
                                bladeTrails = scope.Arena.ActiveWeaponTrailCount,
                                bladeEmissions = scope.Arena.WeaponTrailEmissionCount - firstEmission,
                                playerCel = scope.Arena.PlayerRenderer.sprite.name,
                                enemyCel = scope.Arena.EnemyRenderer.sprite.name
                            });
                        }
                    }
                    File.WriteAllText(Path.Combine(output, "trace.json"), JsonUtility.ToJson(trace, true));
                }
            }
        }

        [UnityTest]
        public IEnumerator WeaponTrail_EmitsOncePerActualAttackHit_AndClearsAcrossHoldsCutscenesAndReuse()
        {
            yield return null;
            Transform releasedRoot;
            using (var scope = new TrailScope())
            {
                scope.Begin(true, hits: 3);
                int first = scope.Arena.WeaponTrailEmissionCount;
                int poolSize = scope.Arena.WeaponTrailPoolSize;
                Assert.That(poolSize, Is.GreaterThanOrEqualTo(2));
                scope.HoldPhase(.40f);
                Stroke(scope.Arena, .016f, true);
                Assert.That(scope.Arena.WeaponTrailEmissionCount, Is.EqualTo(first), "A blade arc must wait until its raw strike phase.");
                scope.HoldPhase(.48f);
                Stroke(scope.Arena, 0f, true);
                Stroke(scope.Arena, .016f, false);
                Assert.That(scope.Arena.WeaponTrailEmissionCount, Is.EqualTo(first), "Paused and non-playing clocks cannot create a stroke.");
                Stroke(scope.Arena, .016f, true);
                Assert.That(scope.Arena.WeaponTrailEmissionCount - first, Is.EqualTo(2));

                scope.HoldPhase(.5f);
                scope.Arena.BeginBladeBind(.3f, .02f, .04f, 0f, 0);
                for (int hold = 0; hold < 8; hold++)
                {
                    scope.Arena.AdvanceBladeBind(.02f);
                    scope.HoldPhase(.5f);
                    Stroke(scope.Arena, .02f, true);
                }
                Assert.That(scope.Arena.WeaponTrailEmissionCount - first, Is.EqualTo(2), "A bound contact cannot redraw its blade stroke every frame.");
                scope.Arena.FinishBladeBind();
                scope.HoldPhase(.1f);
                scope.HoldPhase(.5f);
                Stroke(scope.Arena, .016f, true);
                Assert.That(scope.Arena.WeaponTrailEmissionCount - first, Is.EqualTo(2), "Re-entering the same hit after a visual rewind must not emit again.");

                Invoke(scope.Arena, "SuspendForCutscene");
                Assert.That(scope.Arena.ActiveWeaponTrailCount, Is.Zero);
                scope.Arena.SetEnemyAppearance(EnemyAppearance.CadetA);
                Stroke(scope.Arena, .016f, true);
                Assert.That(scope.Arena.WeaponTrailEmissionCount - first, Is.EqualTo(2));
                Invoke(scope.Arena, "ResumeAfterCutscene");
                Assert.That(scope.Arena.EnemyAppearance, Is.EqualTo(EnemyAppearance.Elisa));
                Stroke(scope.Arena, .016f, true);
                Assert.That(scope.Arena.ActiveWeaponTrailCount, Is.Zero);
                Assert.That(scope.Arena.WeaponTrailEmissionCount - first, Is.EqualTo(2), "Restoring a borrowed cutscene appearance preserves already-drawn hit identities.");
                for (int hit = 1; hit < 3; hit++)
                {
                    scope.Arena.Tick(0f, .2f);
                    scope.HoldTime(hit * CycleSeconds + .5f * ClipSeconds);
                    Stroke(scope.Arena, .016f, true);
                    Assert.That(scope.Arena.WeaponTrailEmissionCount - first, Is.EqualTo((hit + 1) * 2), "Every remaining multihit stroke still gets its own arc.");
                }
                scope.Arena.EndTurn();
                Assert.That(scope.Arena.ActiveWeaponTrailCount, Is.Zero);
                Stroke(scope.Arena, .016f, true);
                Assert.That(scope.Arena.WeaponTrailEmissionCount - first, Is.EqualTo(6));

                scope.Begin(true, rhythm: false);
                first = scope.Arena.WeaponTrailEmissionCount;
                scope.HoldPhase(.5f);
                Stroke(scope.Arena, .016f, true);
                Assert.That(scope.Arena.PlayerAttackVisualOffset, Is.EqualTo(Vector3.zero));
                Assert.That(scope.Arena.EnemyAttackVisualOffset, Is.EqualTo(Vector3.zero));
                Assert.That(scope.Arena.ActiveAttackEchoCount, Is.Zero);
                Assert.That(scope.Arena.WeaponTrailEmissionCount - first, Is.EqualTo(2), "The weapon trail is independent of the body-rhythm/lunge switch.");
                scope.Begin(false, rhythm: true);
                first = scope.Arena.WeaponTrailEmissionCount;
                scope.HoldPhase(.5f);
                Stroke(scope.Arena, .016f, true);
                Assert.That(scope.Arena.ActiveAttackEchoCount, Is.GreaterThan(0));
                Assert.That(scope.Arena.ActiveWeaponTrailCount, Is.Zero);
                Assert.That(scope.Arena.WeaponTrailEmissionCount, Is.EqualTo(first));

                LegacySkill guard = new LegacySkill(88612, "방어 시각 검증", 0, 5, 5,
                    LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 1, string.Empty);
                scope.Begin(true, playerSkill: guard, enemySkill: LegacyCommonActions.Breathe);
                first = scope.Arena.WeaponTrailEmissionCount;
                scope.HoldPhase(.5f);
                Stroke(scope.Arena, .016f, true);
                Assert.That(scope.Arena.WeaponTrailEmissionCount, Is.EqualTo(first), "A guard or breath cannot draw an attacking weapon trail.");
                scope.Begin(true, appearance: EnemyAppearance.TrainingDummy);
                first = scope.Arena.WeaponTrailEmissionCount;
                scope.HoldPhase(.5f);
                Stroke(scope.Arena, .016f, true);
                Assert.That(scope.Arena.WeaponTrailEmissionCount - first, Is.EqualTo(1), "Only the attacker emits; a dummy never invents a sword stroke.");

                scope.Begin(true, hits: 3);
                first = scope.Arena.WeaponTrailEmissionCount;
                scope.HoldPhase(.5f);
                scope.Arena.PresentHit(false, 5, 0, false, false, 5);
                scope.Arena.PresentHit(true, 5, 0, false, false, 5);
                Stroke(scope.Arena, .016f, true);
                Assert.That(scope.Arena.WeaponTrailEmissionCount, Is.EqualTo(first), "An interrupted hurt pose cannot create a new attack arc.");
                scope.Arena.Tick(.4f, .4f);
                scope.HoldTime(CycleSeconds + .5f * ClipSeconds);
                Stroke(scope.Arena, .016f, true);
                Assert.That(scope.Arena.WeaponTrailEmissionCount - first, Is.EqualTo(2), "The next attack may draw again once the hurt poses finish.");
                scope.Arena.Tick(0f, .3f);
                Assert.That(scope.Arena.ActiveWeaponTrailCount, Is.Zero, "Blade trails fade on real time even while the action clock is held.");

                scope.Begin(true, hits: 3);
                first = scope.Arena.WeaponTrailEmissionCount;
                // A fast frame can jump from windup past contact without ever sampling raw phase .48.
                scope.HoldPhase(.60f);
                Invoke(scope.Arena, "PresentWeaponTrailContact", 0, true, true);
                scope.Arena.PresentHit(false, 5, 0, false, false, 5);
                scope.Arena.PresentHit(true, 5, 0, false, false, 5);
                Assert.That(scope.Arena.WeaponTrailEmissionCount - first, Is.EqualTo(2), "The authoritative hit leaves both weapon strokes before hurt poses replace them.");
                Stroke(scope.Arena, .016f, true);
                Invoke(scope.Arena, "PresentWeaponTrailContact", 0, true, true);
                scope.Arena.Tick(.4f, .4f);
                scope.HoldPhase(.5f);
                Stroke(scope.Arena, .016f, true);
                Assert.That(scope.Arena.WeaponTrailEmissionCount - first, Is.EqualTo(2), "A later tick, contact hold or bind release cannot duplicate that skipped-phase hit.");

                scope.Arena.Reset();
                int objects = scope.Arena.WeaponTrailRoot.GetComponentsInChildren<Transform>(true).Length;
                int total = scope.Arena.WeaponTrailEmissionCount;
                Assert.That(scope.Arena.ActiveWeaponTrailCount, Is.Zero);
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    scope.Begin(true);
                    scope.HoldPhase(.5f);
                    Stroke(scope.Arena, .016f, true);
                    Assert.That(scope.Arena.WeaponTrailEmissionCount, Is.EqualTo(total + 2));
                    total += 2;
                    scope.Arena.Reset();
                    Assert.That(scope.Arena.ActiveWeaponTrailCount, Is.Zero);
                    Assert.That(scope.Arena.WeaponTrailEmissionCount, Is.EqualTo(total), "Reset clears drawings but retains lifetime diagnostics.");
                    Assert.That(scope.Arena.WeaponTrailPoolSize, Is.EqualTo(poolSize));
                    Assert.That(scope.Arena.WeaponTrailRoot.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
                }
                releasedRoot = scope.Arena.WeaponTrailRoot;
            }
            yield return null;
            Assert.That(releasedRoot == null, Is.True, "Disposing the arena releases its pooled weapon-trail meshes.");
        }

        private static void Stroke(LegacyArenaView arena, float delta, bool playing)
            => Invoke(arena, "TickAttackMotionTrail", delta, playing);
        private static void Invoke(LegacyArenaView arena, string method, params object[] arguments)
            => typeof(LegacyArenaView).GetMethod(method, Private).Invoke(arena, arguments);
        private static int BrightPixels(Color32[] pixels)
        {
            int count = 0;
            foreach (Color32 pixel in pixels) if (pixel.r + pixel.g + pixel.b > 36) count++;
            return count;
        }
        private static void AssertSamePose(Pose actual, Pose expected)
        {
            Assert.That(actual.Player, Is.EqualTo(expected.Player));
            Assert.That(actual.Enemy, Is.EqualTo(expected.Enemy));
            Assert.That(actual.PlayerOffset, Is.EqualTo(expected.PlayerOffset));
            Assert.That(actual.EnemyOffset, Is.EqualTo(expected.EnemyOffset));
            Assert.That(actual.PlayerTarget, Is.EqualTo(expected.PlayerTarget));
            Assert.That(actual.EnemyTarget, Is.EqualTo(expected.EnemyTarget));
            Assert.That(actual.InRange, Is.EqualTo(expected.InRange));
            Assert.That(actual.PlayerCel, Is.EqualTo(expected.PlayerCel));
            Assert.That(actual.EnemyCel, Is.EqualTo(expected.EnemyCel));
        }
        private static void SavePixels(Color32[] pixels, string path)
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            try { texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); }
            finally { Object.Destroy(texture); }
        }
        private struct Pose
        {
            public Vector3 Player, Enemy, PlayerOffset, EnemyOffset, PlayerTarget, EnemyTarget;
            public bool InRange;
            public string PlayerCel, EnemyCel;
        }
        [Serializable] private sealed class CaptureTrace { public List<CaptureFrame> frames = new List<CaptureFrame>(); }
        [Serializable] private sealed class CaptureFrame
        {
            public bool weaponTrail;
            public int frame, bodyEchoes, bladeTrails, bladeEmissions;
            public float time;
            public Vector3 playerLogical, enemyLogical, playerDrawOffset, enemyDrawOffset;
            public string playerCel, enemyCel;
        }
        private sealed class FirstVariant : System.Random { public override int Next(int maxValue) => 0; }

        private sealed class TrailScope : IDisposable
        {
            private readonly LegacyDuelArt art;
            private readonly DuelPresentationSettings settings;
            private readonly RenderTexture target;
            public GameObject Root { get; }
            public LegacyArenaView Arena { get; }
            public Pose Pose => new Pose
            {
                Player = Arena.PlayerRenderer.transform.position, Enemy = Arena.EnemyRenderer.transform.position,
                PlayerOffset = Arena.PlayerAttackVisualOffset, EnemyOffset = Arena.EnemyAttackVisualOffset,
                PlayerTarget = Arena.PlayerKnockbackTarget, EnemyTarget = Arena.EnemyKnockbackTarget,
                InRange = Arena.IsInRange, PlayerCel = Arena.PlayerRenderer.sprite.name, EnemyCel = Arena.EnemyRenderer.sprite.name
            };
            public TrailScope()
            {
                Root = new GameObject("Weapon Trail Test");
                art = new LegacyDuelArt();
                settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                JsonUtility.FromJsonOverwrite("{\"attackRhythmEnabled\":true,\"attackRhythmStrength\":1,\"attackLungeDistance\":0.6," +
                    "\"weaponTrailEnabled\":true,\"weaponTrailStrength\":1,\"weaponTrailSeconds\":0.12}", settings);
                Arena = LegacyArenaView.Create(Root.transform, art, settings,
                    attackPoseRandom: new FirstVariant(), enemyAttackPoseRandom: new FirstVariant());
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                Arena.ArenaCamera.targetTexture = target;
            }
            public void Begin(bool enabled, bool rhythm = true, int hits = 1, EnemyAppearance appearance = EnemyAppearance.Elisa,
                LegacySkill playerSkill = null, LegacySkill enemySkill = null)
            {
                Arena.Reset();
                Arena.SetBackdrop(ArenaBackdropKind.SchoolCorridor);
                Arena.SetEnemyAppearance(appearance);
                Arena.PlayerRenderer.transform.localPosition = new Vector3(-2f, Arena.ActorGroundY, 0f);
                Arena.EnemyRenderer.transform.localPosition = new Vector3(2f, Arena.ActorGroundY, 0f);
                Arena.SetAttackRhythmOverride(rhythm);
                Arena.SetWeaponTrailOverride(enabled);
                Invoke(Arena, "ConfigureSlotTiming", Speed, Interval);
                var attack = new LegacySkill(88611, "칼 궤적 시각 검증", 0, 10, 10,
                    LegacySkillKind.Attack, LegacySkillProperty.Slash, hits, 0, string.Empty);
                Arena.BeginSlot(playerSkill ?? attack, enemySkill ?? attack);
                FixCamera();
            }
            public void HoldPhase(float phase) => HoldTime(ClipSeconds * phase);
            public void HoldTime(float time) => Invoke(Arena, "HoldSlotAtTime", time);
            public void FixCamera()
            {
                Camera camera = Arena.ArenaCamera;
                camera.transform.localPosition = new Vector3(0f, -1.5f, -10f);
                camera.transform.localRotation = Quaternion.identity;
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.aspect = Width / (float)Height;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.cullingMask = 1 << CaptureLayer;
                camera.GetUniversalAdditionalCameraData().volumeLayerMask = 1 << CaptureLayer;
            }
            public void RenderTrailOnly()
            {
                foreach (Transform child in Root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 30;
                foreach (Transform child in Arena.WeaponTrailRoot.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = CaptureLayer;
                Arena.ArenaCamera.backgroundColor = Color.black;
                Arena.ArenaCamera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                FixCamera();
            }
            public void RenderWholeArena()
            {
                foreach (Transform child in Root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = CaptureLayer;
                Arena.ArenaCamera.backgroundColor = new Color(.12f, .23f, .20f, 1f);
                Arena.ArenaCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                FixCamera();
            }
            public Color32[] ReadPixels()
            {
                RenderTexture previous = RenderTexture.active;
                var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                    texture.Apply();
                    return texture.GetPixels32();
                }
                finally { RenderTexture.active = previous; Object.Destroy(texture); }
            }
            public void Dispose()
            {
                Arena.ArenaCamera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
                Arena.Dispose();
                art.Dispose();
                Object.Destroy(settings);
                Object.Destroy(Root);
            }
        }
    }
}
