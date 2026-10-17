using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class LowHealthHeartbeatPlayModeTests
    {
        private const float Frame = .01f, Tolerance = .0001f;

        [UnityTest]
        public IEnumerator OnlyTheLocalPlayersLowHealthStartsTheWarning_AndUnavailableOrEndedBattlesClearIt()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                scope.Read(Reading(31), 1f);
                Assert.That(scope.Arena.LowHealthHeartbeatAmount, Is.Zero, "31% is outside the warning.");
                scope.Read(new LegacyHealthReading(100, 100, 5, 100, false), 1f);
                Assert.That(scope.Arena.LowHealthHeartbeatAmount, Is.Zero, "An opponent's danger is not the local player's warning.");
                scope.Read(Reading(30), 1f);
                Assert.That(scope.Arena.LowHealthHeartbeatAmount, Is.GreaterThan(.4f), "The warning starts at 30%.");

                foreach (LegacyHealthReading absent in new[] { LegacyHealthReading.None, Reading(0),
                    new LegacyHealthReading(10, 100, 0, 100, false) })
                {
                    scope.Read(Reading(10), .6f);
                    scope.Read(absent, Frame);
                    Assert.That(scope.Arena.LowHealthHeartbeatAmount, Is.Zero, "Missing health and either fall leave no heartbeat.");
                }
                scope.Read(Reading(10), .6f);
                scope.Arena.Tick(0f, Frame);
                Assert.That(scope.Arena.LowHealthHeartbeatPulse + scope.Arena.LowHealthHeartbeatAmount, Is.Zero,
                    "Without this frame's battle request, a menu or training arena cannot carry the warning.");
                scope.Read(Reading(10), .6f);
                scope.Arena.SetTensionLighting(Reading(10), DuelTensionLook.Off);
                scope.Arena.Tick(0f, Frame);
                Assert.That(scope.Arena.LowHealthHeartbeatAmount, Is.Zero, "The lighting switch also disables the warning.");

                scope.Read(Reading(3), 1f);
                float tension = scope.Arena.TensionAmount;
                scope.Arena.ClearLowHealthHeartbeat();
                Assert.That(scope.Arena.LowHealthHeartbeatAmount + scope.Arena.LowHealthHeartbeatPulse, Is.Zero,
                    "A screen transition can clear the last warning without needing another combat frame.");
                Assert.That(scope.Arena.TensionAmount, Is.EqualTo(tension), "The held battle's other lighting is preserved.");
                Assert.That(Edges(scope.Arena).intensity.value,
                    Is.EqualTo(.4f + scope.Settings.TensionLighting.Vignette * tension).Within(Tolerance));
            }
        }

        [UnityTest]
        public IEnumerator CriticalHealthHasStrongerFasterDoubleBeats_WithARestBetweenCycles()
        {
            yield return null;
            using (var threshold = new ArenaScope())
            using (var critical = new ArenaScope())
            {
                threshold.Read(Reading(30), 1.2f);
                critical.Read(Reading(3), 1.2f);
                List<float> slow = threshold.Samples(Reading(30), 4f);
                List<float> fast = critical.Samples(Reading(3), 4f);
                Assert.That(Max(fast), Is.GreaterThan(Max(slow) * 1.8f), "The deepest danger has a visibly stronger beat.");
                Assert.That(Peaks(fast, .2f), Is.GreaterThan(Peaks(slow, .2f) + 2), "The heart also beats faster.");
                Assert.That(LongestRest(fast, .01f), Is.GreaterThan(20), "The paired beats leave a sustained quiet gap.");
                Assert.That(critical.Arena.LowHealthHeartbeatAmount, Is.LessThanOrEqualTo(1f));
                Assert.That(Edges(critical.Arena).intensity.value, Is.LessThanOrEqualTo(.82f));
            }
        }

        [UnityTest]
        public IEnumerator RealTimeKeepsTheHeartBeatingThroughSlowMotion_ZeroTimeHoldsIt_AndHealingFadesIt()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                scope.Read(Reading(10), .8f);
                float pulse = scope.Arena.LowHealthHeartbeatPulse, exposure = Grade(scope.Arena).postExposure.value;
                float edges = Edges(scope.Arena).intensity.value;
                Color ink = Edges(scope.Arena).color.value;
                for (int frame = 0; frame < 30; frame++) scope.Read(Reading(10), 0f);
                Assert.That(scope.Arena.LowHealthHeartbeatPulse, Is.EqualTo(pulse));
                Assert.That(Grade(scope.Arena).postExposure.value, Is.EqualTo(exposure));
                Assert.That(Edges(scope.Arena).intensity.value, Is.EqualTo(edges));
                Assert.That(Edges(scope.Arena).color.value, Is.EqualTo(ink));
                List<float> moving = scope.Samples(Reading(10), .8f); // Combat delta stays at zero throughout.
                Assert.That(Max(moving) - Min(moving), Is.GreaterThan(.6f));
                scope.Read(Reading(80), .1f);
                Assert.That(scope.Arena.LowHealthHeartbeatAmount, Is.GreaterThan(0f), "Healing eases the warning out.");
                scope.Read(Reading(80), .6f);
                Assert.That(scope.Arena.LowHealthHeartbeatAmount + scope.Arena.LowHealthHeartbeatPulse, Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator CutscenesHideAndFreezeTheHeartbeat_ResumeRestoresTheHeldPicture_AndResetClearsIt()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                scope.Read(Reading(5), .71f);
                float amount = scope.Arena.LowHealthHeartbeatAmount, pulse = scope.Arena.LowHealthHeartbeatPulse;
                float exposure = Grade(scope.Arena).postExposure.value, edges = Edges(scope.Arena).intensity.value;
                Color ink = Edges(scope.Arena).color.value;
                Invoke(scope.Arena, "SuspendForCutscene");
                Assert.That(scope.Arena.LowHealthHeartbeatAmount + scope.Arena.LowHealthHeartbeatPulse, Is.Zero);
                Assert.That(Grade(scope.Arena).postExposure.value, Is.EqualTo(0f).Within(Tolerance));
                Assert.That(Edges(scope.Arena).intensity.value, Is.EqualTo(.4f).Within(Tolerance));
                scope.Read(Reading(5), 1.3f);
                Assert.That(scope.Arena.LowHealthHeartbeatPulse, Is.Zero, "A cutscene's real time cannot tick the battle's heartbeat.");
                Invoke(scope.Arena, "ResumeAfterCutscene");
                Assert.That(scope.Arena.LowHealthHeartbeatAmount, Is.EqualTo(amount));
                Assert.That(scope.Arena.LowHealthHeartbeatPulse, Is.EqualTo(pulse));
                Assert.That(Grade(scope.Arena).postExposure.value, Is.EqualTo(exposure).Within(Tolerance));
                Assert.That(Edges(scope.Arena).intensity.value, Is.EqualTo(edges).Within(Tolerance));
                Assert.That(Edges(scope.Arena).color.value, Is.EqualTo(ink));
                scope.Arena.Reset();
                Assert.That(scope.Arena.LowHealthHeartbeatAmount + scope.Arena.LowHealthHeartbeatPulse, Is.Zero);
                Assert.That(Edges(scope.Arena).intensity.value, Is.EqualTo(.4f).Within(Tolerance));
            }
        }

        [UnityTest]
        public IEnumerator NativeRenderShowsRedDarkPulsingEdges_WithoutMovingTheCameraOrTheFighters()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                scope.Read(Reading(3), 2f);
                Vector3 player = scope.Arena.PlayerGroundPosition, enemy = scope.Arena.EnemyGroundPosition;
                Vector3 camera = scope.Arena.ArenaCamera.transform.localPosition;
                float size = scope.Arena.ArenaCamera.orthographicSize;
                Color32[] rest = null, peak = null;
                for (int frame = 0; frame < 150 && (rest == null || peak == null); frame++)
                {
                    scope.Read(Reading(3), Frame);
                    if (rest == null && scope.Arena.LowHealthHeartbeatPulse < .001f) rest = scope.Capture("01-heartbeat-rest");
                    if (peak == null && scope.Arena.LowHealthHeartbeatPulse > .93f) peak = scope.Capture("02-heartbeat-beat");
                }
                Assert.That(rest, Is.Not.Null); Assert.That(peak, Is.Not.Null);
                Assert.That(EdgeRedness(peak) - EdgeRedness(rest), Is.GreaterThan(1f),
                    "The actual post-processed image, not only its volume settings, visibly reddens at the beat.");
                Assert.That(ChangedPixels(rest, peak), Is.GreaterThan(5000), "A native beat changes a substantial area of the screen.");
                Assert.That(scope.Arena.PlayerGroundPosition, Is.EqualTo(player));
                Assert.That(scope.Arena.EnemyGroundPosition, Is.EqualTo(enemy));
                Assert.That(Vector3.Distance(scope.Arena.ArenaCamera.transform.localPosition, camera), Is.LessThan(.01f));
                Assert.That(scope.Arena.ArenaCamera.orthographicSize, Is.EqualTo(size).Within(.01f));
            }
        }

        private static LegacyHealthReading Reading(int player) => new LegacyHealthReading(player, 100, 100, 100, false);
        private static Vignette Edges(LegacyArenaView arena)
        {
            Assert.That(arena.ArenaProfile.TryGet(out Vignette value), Is.True); return value;
        }
        private static ColorAdjustments Grade(LegacyArenaView arena)
        {
            Assert.That(arena.ArenaProfile.TryGet(out ColorAdjustments value), Is.True); return value;
        }
        private static void Invoke(LegacyArenaView arena, string name)
            => typeof(LegacyArenaView).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(arena, null);
        private static float Max(List<float> values) { float result = 0f; foreach (float value in values) result = Mathf.Max(result, value); return result; }
        private static float Min(List<float> values) { float result = 1f; foreach (float value in values) result = Mathf.Min(result, value); return result; }
        private static int Peaks(List<float> values, float threshold)
        {
            int result = 0;
            for (int i = 1; i < values.Count - 1; i++)
                if (values[i] > threshold && values[i] >= values[i - 1] && values[i] > values[i + 1]) result++;
            return result;
        }
        private static int LongestRest(List<float> values, float threshold)
        {
            int longest = 0, current = 0;
            foreach (float value in values) { current = value <= threshold ? current + 1 : 0; longest = Mathf.Max(longest, current); }
            return longest;
        }
        private static float EdgeRedness(Color32[] pixels)
        {
            float sum = 0f; int count = 0;
            for (int y = 0; y < ArenaScope.Height; y++)
                for (int x = 0; x < ArenaScope.Width; x++)
                    if (x < ArenaScope.Width / 8 || x >= ArenaScope.Width * 7 / 8 || y < ArenaScope.Height / 8 || y >= ArenaScope.Height * 7 / 8)
                    {
                        Color32 color = pixels[y * ArenaScope.Width + x]; sum += color.r - color.g; count++;
                    }
            return sum / count;
        }
        private static int ChangedPixels(Color32[] first, Color32[] second)
        {
            int result = 0;
            for (int i = 0; i < first.Length; i++)
                if (Math.Abs(first[i].r - second[i].r) + Math.Abs(first[i].g - second[i].g) + Math.Abs(first[i].b - second[i].b) > 12) result++;
            return result;
        }

        private sealed class ArenaScope : IDisposable
        {
            public const int Width = 1280, Height = 720;
            private readonly GameObject root = new GameObject("Low Health Heartbeat Test");
            private readonly LegacyDuelArt art = new LegacyDuelArt();
            private readonly DuelPrototypeController scene;
            private readonly bool sceneEnabled;
            private RenderTexture target;
            public DuelPresentationSettings Settings { get; } = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            public LegacyArenaView Arena { get; }
            public ArenaScope()
            {
                scene = Object.FindAnyObjectByType<DuelPrototypeController>();
                if (scene != null) { sceneEnabled = scene.enabled; scene.enabled = false; }
                Arena = LegacyArenaView.Create(root.transform, art, Settings);
                Arena.ArenaCamera.aspect = Width / (float)Height;
                Arena.SetBackdrop(ArenaBackdropKind.SchoolCorridor); Arena.Reset(); Arena.SetOpeningPositions(-2f, 2f);
                Arena.Tick(0f, Frame);
            }
            public void Read(LegacyHealthReading reading, float seconds)
            {
                int frames = Mathf.Max(1, Mathf.CeilToInt(seconds / Frame));
                for (int frame = 0; frame < frames; frame++)
                {
                    Arena.SetTensionLighting(reading, Settings.TensionLighting);
                    Arena.Tick(0f, seconds / frames);
                }
            }
            public List<float> Samples(LegacyHealthReading reading, float seconds)
            {
                var result = new List<float>();
                for (int frame = 0; frame < Mathf.RoundToInt(seconds / Frame); frame++)
                { Read(reading, Frame); result.Add(Arena.LowHealthHeartbeatPulse); }
                return result;
            }
            public Color32[] Capture(string name)
            {
                Camera camera = Arena.ArenaCamera;
                if (target == null)
                {
                    target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                    camera.targetTexture = target; camera.aspect = Width / (float)Height;
                    // Volume tracks a layer change in its Update. These same-frame manual renders never run it:
                    // unregister before moving, then register on the capture layer so URP's cached list sees our grade.
                    Volume volume = root.GetComponentInChildren<Volume>();
                    Assert.That(volume, Is.Not.Null);
                    volume.enabled = false;
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                    volume.enabled = true;
                    camera.cullingMask = 1 << 31;
                    camera.GetUniversalAdditionalCameraData().volumeLayerMask = 1 << 31;
                }
                camera.Render();
                Assert.That(VolumeManager.instance.stack.GetComponent<Vignette>().intensity.value,
                    Is.EqualTo(Edges(Arena).intensity.value).Within(Tolerance), "The actual rendered camera reads this arena's grade.");
                var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); texture.Apply();
                    string output = Environment.GetEnvironmentVariable("SWORDMASTER_HEARTBEAT_CAPTURE_DIR");
                    if (!string.IsNullOrEmpty(output))
                    { Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG()); }
                    return texture.GetPixels32();
                }
                finally { RenderTexture.active = previous; Object.Destroy(texture); }
            }
            public void Dispose()
            {
                if (target != null) { Arena.ArenaCamera.targetTexture = null; target.Release(); Object.Destroy(target); }
                Arena.Dispose(); art.Dispose(); Object.Destroy(Settings); Object.Destroy(root);
                if (scene != null) scene.enabled = sceneEnabled;
            }
        }
    }
}
