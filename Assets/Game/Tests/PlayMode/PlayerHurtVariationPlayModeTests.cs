using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class PlayerHurtVariationPlayModeTests
    {
        [UnityTest]
        public IEnumerator EveryIncomingHitDrawsOnce_AndHoldsTheChosenStaticPose()
        {
            yield return null;
            var host = new GameObject("Player Hurt Variations Test");
            var random = new ReactionRandom();
            using (var art = new LegacyDuelArt())
            using (var arena = LegacyArenaView.Create(host.transform, art, reactionPoseRandom: random))
            using (var set = new FinalPlayerAnimationSet())
            {
                try
                {
                    Assert.That(set.IsReady, Is.True);
                    Assert.That(set.LoadedFrameCount, Is.EqualTo(126));
                    Assert.That(set.GetHurt(0), Is.Not.SameAs(set.GetHurt(1)));
                    Assert.That(set.GetHurt(1), Is.Not.SameAs(set.GetHurt(2)));
                    foreach (int variant in new[] { 0, 1, 2 })
                    {
                        Sprite pose = set.GetHurt(variant);
                        Assert.That(pose.rect.size, Is.EqualTo(new Vector2(256, 224)));
                        Assert.That(pose.pixelsPerUnit, Is.EqualTo(36));
                        Assert.That(pose.pivot.y, Is.EqualTo(22 + 2.23f * 36).Within(.001f),
                            "All three poses must share the original hurt pose's foot baseline.");
                    }
                    CaptureComparison(arena, set);
                    arena.BeginSlot(null, null);
                    int[] expected = { 2, 0, 2, 1 };
                    for (int i = 0; i < expected.Length; i++)
                    {
                        arena.PresentHit(false, 2, 0, false, false, 0, closeUp: false, hurtReaction: true);
                        Assert.That(arena.PlayerRenderer.sprite.name, Is.EqualTo("final-hurt-frame-" + expected[i].ToString("00")));
                        Assert.That(random.Calls, Is.EqualTo(i + 1));
                        Sprite chosen = arena.PlayerRenderer.sprite;
                        arena.Tick(0, .05f);
                        arena.Tick(.01f, .01f);
                        Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(chosen));
                        Assert.That(random.Calls, Is.EqualTo(i + 1), "Sampling must not roll another pose between hits.");
                    }
                    arena.Tick(.4f, .4f);
                    Assert.That(arena.PlayerRenderer.sprite.name, Does.StartWith("final-idle"));
                    arena.SetResistanceBroken(true, false);
                    arena.PresentHit(false, 2, 0, false, false, 0, closeUp: false, hurtReaction: true);
                    Assert.That(arena.PlayerRenderer.sprite.name, Is.EqualTo("final-break-frame-00"),
                        "The separate resistance-break pose keeps priority over ordinary random flinches.");
                }
                finally { Object.Destroy(host); }
            }
            yield return null;
        }

        private static void CaptureComparison(LegacyArenaView arena, FinalPlayerAnimationSet set)
        {
            var root = new GameObject("Hurt Comparison Camera");
            var camera = root.AddComponent<Camera>();
            camera.transform.position = new Vector3(0, -.55f, -10);
            camera.orthographic = true; camera.orthographicSize = 2.25f;
            camera.cullingMask = 1 << 28;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.145f, .137f, .145f);
            var target = new RenderTexture(1440, 400, 24);
            var pixels = new Texture2D(1440, 400, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    var figure = new GameObject("Hurt " + i) { layer = 28 };
                    figure.transform.SetParent(root.transform, false);
                    figure.transform.position = new Vector3((i - 1) * 5.1f, 0, 0);
                    var renderer = figure.AddComponent<SpriteRenderer>();
                    renderer.sharedMaterial = arena.PlayerRenderer.sharedMaterial;
                    renderer.sprite = set.GetHurt(i);
                }
                camera.targetTexture = target;
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1440, 400), 0, 0); pixels.Apply();
                string folder = Path.Combine(Application.dataPath, "../Docs/Art/PlayerHurtVariants_20261025");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "unity-three-poses.png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                target.Release(); Object.Destroy(target); Object.Destroy(pixels); Object.Destroy(root);
            }
        }

        private sealed class ReactionRandom : System.Random
        {
            private readonly int[] choices = { 2, 0, 2, 1, 0 };
            public int Calls { get; private set; }
            public override int Next(int maxValue)
            {
                Assert.That(maxValue, Is.EqualTo(3));
                Assert.That(Calls, Is.LessThan(choices.Length));
                return choices[Calls++];
            }
        }
    }
}
