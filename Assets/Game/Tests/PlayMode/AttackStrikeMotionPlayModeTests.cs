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
    public sealed class AttackStrikeMotionPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const int Width = 960, Height = 540, CaptureLayer = 31;
        private static readonly int AttackOffset = Shader.PropertyToID("_AttackOffset");

        [UnityTest]
        public IEnumerator AttackOffset_MovesTheRenderedBodyAtContact_WhileLogicalRangeAndTargetsStayFixed()
        {
            yield return null;
            using (var scope = new StrikeScope())
            {
                scope.Begin(false);
                scope.MaskPlayerOnly();
                scope.HoldPhase(.5f);
                LogicalPose logical = scope.Logical;
                Assert.That(scope.Arena.PlayerAttackVisualOffset, Is.EqualTo(Vector3.zero));
                Assert.That(scope.Arena.EnemyAttackVisualOffset, Is.EqualTo(Vector3.zero));
                yield return null;
                Color32[] off = scope.ReadPixels();
                Centroid offBody = BrightCentroid(off);

                scope.Begin(true);
                scope.MaskPlayerOnly();
                scope.HoldPhase(.5f);
                Assert.That(scope.Arena.PlayerAttackVisualOffset.x, Is.GreaterThan(.4f));
                Assert.That(scope.Arena.EnemyAttackVisualOffset.x, Is.LessThan(-.4f));
                AssertDrawProperty(scope.Arena.PlayerRenderer, scope.Arena.PlayerAttackVisualOffset);
                AssertDrawProperty(scope.Arena.EnemyRenderer, scope.Arena.EnemyAttackVisualOffset);
                AssertDrawnBoundsContained(scope.Arena.PlayerRenderer);
                AssertDrawnBoundsContained(scope.Arena.EnemyRenderer);
                Assert.That(scope.Arena.PlayerRenderer.sharedMaterial.shader.isSupported, Is.True);
                Assert.That(scope.Arena.PlayerRenderer.sharedMaterial.shader.name, Is.EqualTo("TurnLimbo/DuelAttackSprite"));
                Assert.That(scope.Logical.Player, Is.EqualTo(logical.Player));
                Assert.That(scope.Logical.Enemy, Is.EqualTo(logical.Enemy));
                Assert.That(scope.Logical.PlayerTarget, Is.EqualTo(logical.PlayerTarget));
                Assert.That(scope.Logical.EnemyTarget, Is.EqualTo(logical.EnemyTarget));
                Assert.That(scope.Logical.InRange, Is.EqualTo(logical.InRange));
                Assert.That(scope.Logical.Root, Is.EqualTo(logical.Root));
                Assert.That(scope.Logical.Camera, Is.EqualTo(logical.Camera));
                yield return null;
                Color32[] on = scope.ReadPixels();
                Centroid onBody = BrightCentroid(on);
                float pixelsPerWorldUnit = Height / (2f * scope.Arena.ArenaCamera.orthographicSize);
                float drawnWorldShift = (onBody.X - offBody.X) / pixelsPerWorldUnit;
                Assert.That(drawnWorldShift, Is.GreaterThan(.4f).And.LessThan(.8f),
                    "Read-back pixels must really move; an MPB value alone cannot prove that the active renderer supports the shader.");
                Assert.That(onBody.Count / (float)offBody.Count, Is.InRange(.95f, 1.05f), "The same native contact cel is translated, not replaced by a different silhouette.");

                string output = Environment.GetEnvironmentVariable("SWORDMASTER_STRIKE_CAPTURE_DIR");
                if (!string.IsNullOrEmpty(output))
                {
                    Directory.CreateDirectory(output);
                    SavePixels(off, Path.Combine(output, "mask-off.png"));
                    SavePixels(on, Path.Combine(output, "mask-on.png"));
                    var trace = new CaptureTrace();
                    foreach (bool enabled in new[] { false, true })
                    {
                        scope.Begin(enabled);
                        scope.RenderWholeArena();
                        // Let the render pipeline and volume settle while the manually driven attack stays at time zero.
                        yield return null;
                        yield return null;
                        for (int frame = 0; frame < 36; frame++)
                        {
                            float time = frame / 60f;
                            scope.Arena.Tick(0f, frame == 0 ? 0f : 1f / 60f);
                            scope.HoldTime(time);
                            Trail(scope.Arena, frame == 0 ? 0f : 1f / 60f, time < LegacyArenaView.OriginalClipDuration / .6f);
                            scope.FixCamera();
                            yield return null;
                            SavePixels(scope.ReadPixels(), Path.Combine(output, (enabled ? "on-" : "off-") + frame.ToString("00") + ".png"));
                            trace.frames.Add(new CaptureFrame
                            {
                                enabled = enabled, frame = frame, time = time,
                                playerLogical = scope.Arena.PlayerRenderer.transform.position,
                                enemyLogical = scope.Arena.EnemyRenderer.transform.position,
                                playerDrawOffset = scope.Arena.PlayerAttackVisualOffset,
                                enemyDrawOffset = scope.Arena.EnemyAttackVisualOffset,
                                echoes = scope.Arena.ActiveAttackEchoCount,
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
        public IEnumerator HeldAndHurtPoses_EmitNoRepeatedEchoes_AndOffResetAndRecoveryLeaveNoMotionOrPoolGrowth()
        {
            yield return null;
            using (var scope = new StrikeScope())
            {
                scope.Begin(true);
                scope.HoldPhase(.40f);
                Trail(scope.Arena, .016f, true);
                int first = scope.Arena.ActiveAttackEchoCount;
                Assert.That(first, Is.GreaterThan(0));
                Assert.That(scope.Arena.AttackEchoPoolSize, Is.EqualTo(8));
                for (int hold = 0; hold < 12; hold++)
                {
                    scope.HoldPhase(.40f);
                    Trail(scope.Arena, 0f, true);
                    scope.Arena.Tick(0f, 0f);
                }
                Assert.That(scope.Arena.ActiveAttackEchoCount, Is.EqualTo(first), "A frozen contact or paused clock cannot duplicate a stroke's echo.");
                scope.HoldPhase(.48f);
                Trail(scope.Arena, .016f, true);
                Assert.That(scope.Arena.ActiveAttackEchoCount, Is.GreaterThan(first).And.LessThanOrEqualTo(8));
                for (int hold = 0; hold < 12; hold++) Trail(scope.Arena, .016f, true);
                int held = scope.Arena.ActiveAttackEchoCount;
                Assert.That(held, Is.LessThanOrEqualTo(4), "Each side contributes only the two discrete stroke moments, even if its clock is held.");

                Vector3 playerBeforeScene = scope.Arena.PlayerAttackVisualOffset;
                Vector3 enemyBeforeScene = scope.Arena.EnemyAttackVisualOffset;
                typeof(LegacyArenaView).GetMethod("SuspendForCutscene", Private).Invoke(scope.Arena, null);
                AssertCleared(scope.Arena);
                scope.Arena.SetEnemyAppearance(EnemyAppearance.CadetA);
                Trail(scope.Arena, .016f, true);
                Assert.That(scope.Arena.ActiveAttackEchoCount, Is.Zero);
                typeof(LegacyArenaView).GetMethod("ResumeAfterCutscene", Private).Invoke(scope.Arena, null);
                Assert.That(scope.Arena.EnemyAppearance, Is.EqualTo(EnemyAppearance.Elisa));
                Assert.That(Vector3.Distance(scope.Arena.PlayerAttackVisualOffset, playerBeforeScene), Is.LessThan(.0001f));
                Assert.That(Vector3.Distance(scope.Arena.EnemyAttackVisualOffset, enemyBeforeScene), Is.LessThan(.0001f));
                Trail(scope.Arena, .016f, true);
                Assert.That(scope.Arena.ActiveAttackEchoCount, Is.Zero,
                    "Borrowing and restoring a cutscene appearance must preserve already-emitted stroke masks.");
                held = scope.Arena.ActiveAttackEchoCount;

                scope.HoldPhase(.5f);
                float beforeHurt = scope.Arena.PlayerAttackVisualOffset.magnitude;
                scope.Arena.PresentHit(false, 5, 0, false, false, 5);
                Assert.That(scope.Arena.PlayerAttackVisualOffset.magnitude, Is.EqualTo(beforeHurt).Within(.001f), "The hurt pose starts at the body's drawn contact position.");
                Trail(scope.Arena, .016f, true);
                Assert.That(scope.Arena.ActiveAttackEchoCount, Is.EqualTo(held), "A hurt actor cannot leave another attack stroke.");
                scope.Arena.Tick(.08f, .08f);
                Assert.That(scope.Arena.PlayerAttackVisualOffset.magnitude, Is.GreaterThan(0f).And.LessThan(beforeHurt), "The displaced hurt body settles smoothly.");
                scope.Arena.Tick(.2f, .2f);
                Trail(scope.Arena, .2f, true);
                AssertCleared(scope.Arena);

                scope.Begin(false);
                scope.HoldPhase(.5f);
                Trail(scope.Arena, .016f, true);
                AssertCleared(scope.Arena);
                scope.Begin(true);
                scope.HoldPhase(.5f);
                Trail(scope.Arena, .016f, true);
                Assert.That(scope.Arena.ActiveAttackEchoCount, Is.GreaterThan(0));
                scope.Arena.Reset();
                AssertCleared(scope.Arena);
                int warmedObjects = scope.Root.GetComponentsInChildren<Transform>(true).Length;
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    scope.Begin(true);
                    scope.HoldPhase(.5f);
                    Trail(scope.Arena, .016f, true);
                    scope.Arena.Reset();
                    AssertCleared(scope.Arena);
                    Assert.That(scope.Arena.AttackEchoPoolSize, Is.EqualTo(8));
                    Assert.That(scope.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(warmedObjects));
                }
            }
        }

        private static void AssertCleared(LegacyArenaView arena)
        {
            Assert.That(arena.PlayerAttackVisualOffset.magnitude + arena.EnemyAttackVisualOffset.magnitude, Is.LessThan(.0001f));
            Assert.That(arena.ActiveAttackEchoCount, Is.Zero);
        }
        private static void AssertDrawProperty(SpriteRenderer source, Vector3 expectedWorldOffset)
        {
            var properties = new MaterialPropertyBlock();
            source.GetPropertyBlock(properties);
            Vector4 local = properties.GetVector(AttackOffset);
            Vector3 world = source.transform.TransformVector(new Vector3(local.x, local.y, local.z));
            Assert.That(Vector3.Distance(world, expectedWorldOffset), Is.LessThan(.00001f));
        }
        private static void AssertDrawnBoundsContained(SpriteRenderer source)
        {
            var properties = new MaterialPropertyBlock();
            source.GetPropertyBlock(properties);
            Vector4 offset = properties.GetVector(AttackOffset);
            Bounds drawn = source.sprite.bounds;
            Vector3 center = drawn.center;
            if (source.flipX) center.x = -center.x;
            if (source.flipY) center.y = -center.y;
            drawn.center = center + new Vector3(offset.x, offset.y, offset.z);
            Bounds culling = source.localBounds;
            for (int axis = 0; axis < 3; axis++)
            {
                Assert.That(culling.min[axis], Is.LessThanOrEqualTo(drawn.min[axis] + .0001f), "The drawn flipped sprite must stay inside its culling bounds.");
                Assert.That(culling.max[axis], Is.GreaterThanOrEqualTo(drawn.max[axis] - .0001f));
            }
        }
        private static void Trail(LegacyArenaView arena, float delta, bool playing)
            => typeof(LegacyArenaView).GetMethod("TickAttackMotionTrail", Private).Invoke(arena, new object[] { delta, playing });
        private static Centroid BrightCentroid(Color32[] pixels)
        {
            double sumX = 0d;
            int count = 0;
            for (int index = 0; index < pixels.Length; index++)
            {
                Color32 pixel = pixels[index];
                if (pixel.r + pixel.g + pixel.b < 45) continue;
                sumX += index % Width;
                count++;
            }
            Assert.That(count, Is.GreaterThan(100), "The native body must actually be rendered over the black mask background.");
            return new Centroid { X = (float)(sumX / count), Count = count };
        }
        private static void SavePixels(Color32[] pixels, string path)
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            try { texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); }
            finally { Object.Destroy(texture); }
        }
        private struct Centroid { public float X; public int Count; }
        private struct LogicalPose
        {
            public Vector3 Player, Enemy, PlayerTarget, EnemyTarget, Root, Camera;
            public bool InRange;
        }
        [Serializable] private sealed class CaptureTrace { public List<CaptureFrame> frames = new List<CaptureFrame>(); }
        [Serializable] private sealed class CaptureFrame
        {
            public bool enabled;
            public int frame, echoes;
            public float time;
            public Vector3 playerLogical, enemyLogical, playerDrawOffset, enemyDrawOffset;
            public string playerCel, enemyCel;
        }
        private sealed class FirstVariant : System.Random { public override int Next(int maxValue) => 0; }

        private sealed class StrikeScope : IDisposable
        {
            private readonly LegacyDuelArt art;
            private readonly DuelPresentationSettings settings;
            private readonly RenderTexture target;
            public GameObject Root { get; }
            public LegacyArenaView Arena { get; }
            public LogicalPose Logical => new LogicalPose
            {
                Player = Arena.PlayerRenderer.transform.position, Enemy = Arena.EnemyRenderer.transform.position,
                PlayerTarget = Arena.PlayerKnockbackTarget, EnemyTarget = Arena.EnemyKnockbackTarget,
                InRange = Arena.IsInRange, Root = Root.transform.position, Camera = Arena.ArenaCamera.transform.position
            };
            public StrikeScope()
            {
                Root = new GameObject("Strike Motion Test");
                art = new LegacyDuelArt();
                settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                JsonUtility.FromJsonOverwrite("{\"attackRhythmEnabled\":true,\"attackRhythmStrength\":1,\"attackLungeDistance\":0.6}", settings);
                Arena = LegacyArenaView.Create(Root.transform, art, settings,
                    attackPoseRandom: new FirstVariant(), enemyAttackPoseRandom: new FirstVariant());
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                Arena.ArenaCamera.targetTexture = target;
            }
            public void Begin(bool enabled)
            {
                Arena.Reset();
                Arena.SetBackdrop(ArenaBackdropKind.SchoolCorridor);
                Arena.SetEnemyAppearance(EnemyAppearance.Elisa);
                Arena.PlayerRenderer.transform.localPosition = new Vector3(-2f, Arena.ActorGroundY, 0f);
                Arena.EnemyRenderer.transform.localPosition = new Vector3(2f, Arena.ActorGroundY, 0f);
                Arena.SetAttackRhythmOverride(enabled);
                typeof(LegacyArenaView).GetMethod("ConfigureSlotTiming", Private).Invoke(Arena, new object[] { .6f, .1f });
                var attack = new LegacySkill(88601, "베기 시각 검증", 0, 10, 10, LegacySkillKind.Attack,
                    LegacySkillProperty.Slash, 1, 0, string.Empty);
                Arena.BeginSlot(attack, attack);
                FixCamera();
            }
            public void HoldPhase(float phase) => HoldTime(LegacyArenaView.OriginalClipDuration / .6f * phase);
            public void HoldTime(float time)
                => typeof(LegacyArenaView).GetMethod("HoldSlotAtTime", Private).Invoke(Arena, new object[] { time });
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
            public void MaskPlayerOnly()
            {
                foreach (Transform child in Root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 30;
                Arena.PlayerRenderer.gameObject.layer = CaptureLayer;
                Arena.ArenaCamera.backgroundColor = Color.black;
                Arena.ArenaCamera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
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
