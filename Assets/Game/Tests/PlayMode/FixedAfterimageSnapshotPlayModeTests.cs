using System;
using System.Collections;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class FixedAfterimageSnapshotPlayModeTests
    {
        private static readonly int AttackOffsetId = Shader.PropertyToID("_AttackOffset");

        [UnityTest]
        public IEnumerator ForwardBackwardAndFlippedSnapshots_StayAtCapturedPoseAsTheSourceAndCameraChange()
        {
            using (var scope = new Scope())
            {
                foreach (float direction in new[] { -1f, 1f })
                    foreach (bool flipped in new[] { false, true })
                    {
                        scope.Pool.Reset();
                        var source = scope.Source;
                        source.sprite = scope.First;
                        source.flipX = flipped;
                        source.flipY = !flipped;
                        source.transform.SetPositionAndRotation(new Vector3(direction * 2f, -1f, 3f), Quaternion.Euler(0f, 0f, 17f));
                        source.transform.localScale = new Vector3(1.3f, .8f, 1f);
                        Vector3 offset = new Vector3(direction * .6f, .2f, 0f);
                        SetOffset(source, offset);
                        scope.Pool.Emit(source, new Color(.3f, .6f, .9f, .8f), 1f, 2);
                        SpriteRenderer ghost = scope.Upper;
                        Vector3 position = source.transform.position + source.transform.TransformVector(offset);
                        Quaternion rotation = source.transform.rotation;
                        Vector3 scale = source.transform.lossyScale;
                        AssertPose(ghost, scope.First, position, rotation, scale, flipped, !flipped);

                        source.transform.position += Vector3.right * direction * 3f;
                        source.transform.rotation = Quaternion.Euler(0f, 0f, 110f);
                        source.transform.localScale = Vector3.one * .4f;
                        source.sprite = scope.Second;
                        source.flipX = !flipped;
                        source.flipY = flipped;
                        SetOffset(source, -offset * 4f);
                        scope.Camera.transform.position = new Vector3(9f, 4f, -20f);
                        scope.Camera.orthographicSize = 2f;
                        scope.Pool.Tick(.25f);
                        AssertPose(ghost, scope.First, position, rotation, scale, flipped, !flipped);
                        Assert.That(DuelStepAfterimages.DrawnColor(ghost).a, Is.EqualTo(.6f).Within(.0001f));
                        Assert.That(scope.Pool.ActiveCount, Is.EqualTo(1), "Both travel directions keep their snapshot.");
                    }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator LayeredSnapshots_BakeEachShaderOffsetAndKeepTheirOrderingAndLowerPose()
        {
            using (var scope = new Scope())
            {
                scope.Source.gameObject.AddComponent<SortingGroup>().sortingOrder = 1;
                scope.Source.sortingOrder = 4;
                scope.Lower.sortingOrder = 3;
                scope.Source.transform.SetPositionAndRotation(new Vector3(3f, -2f, 0f), Quaternion.Euler(0f, 0f, -11f));
                scope.Source.transform.localScale = new Vector3(1.4f, .9f, 1f);
                scope.Lower.transform.localPosition = new Vector3(.15f, -.5f, .01f);
                scope.Lower.transform.localScale = new Vector3(.8f, .7f, 1f);
                scope.Lower.flipX = true;
                Vector3 upperOffset = new Vector3(.4f, .2f, 0f), lowerOffset = new Vector3(.1f, -.1f, 0f);
                SetOffset(scope.Source, upperOffset);
                SetOffset(scope.Lower, lowerOffset);
                Vector3 upperPosition = scope.Source.transform.position + scope.Source.transform.TransformVector(upperOffset);
                Vector3 lowerPosition = scope.Lower.transform.position + scope.Lower.transform.TransformVector(lowerOffset);
                Vector3 lowerScale = scope.Lower.transform.lossyScale;
                Quaternion lowerRotation = scope.Lower.transform.rotation;
                scope.Pool.Emit(scope.Source, Color.cyan, 1f, 3, scope.Lower);
                SpriteRenderer upper = scope.Upper, lower = scope.GhostLower;
                Assert.That(upper.transform.position, Is.EqualTo(upperPosition));
                AssertPose(lower, scope.Second, lowerPosition, lowerRotation, lowerScale, true, false);
                Assert.That(upper.sortingOrder, Is.EqualTo(scope.Pool.GroupedSortingCeiling));
                Assert.That(lower.sortingOrder, Is.EqualTo(upper.sortingOrder - 1));
                Assert.That(lower.sharedMaterial, Is.SameAs(scope.Lower.sharedMaterial));

                scope.Source.transform.position += Vector3.left * 7f;
                scope.Lower.transform.localPosition = Vector3.up * 5f;
                scope.Lower.transform.localRotation = Quaternion.Euler(0f, 0f, 75f);
                scope.Lower.transform.localScale = Vector3.one * 2f;
                scope.Lower.sprite = scope.First;
                scope.Lower.flipX = false;
                SetOffset(scope.Lower, Vector3.one * 10f);
                scope.Pool.Tick(.4f);
                Assert.That(upper.transform.position, Is.EqualTo(upperPosition));
                AssertPose(lower, scope.Second, lowerPosition, lowerRotation, lowerScale, true, false);
                Assert.That(lower.color.a, Is.EqualTo(.6f).Within(.0001f));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExpiredSnapshot_ReusesTheSameRendererWithAFreshPoseAndNoStaleLowerLayer()
        {
            using (var scope = new Scope())
            {
                scope.Pool.Emit(scope.Source, Color.yellow, .2f, 3, scope.Lower);
                SpriteRenderer renderer = scope.Upper;
                Assert.That(scope.GhostLower.enabled, Is.True);
                scope.Pool.Tick(.2f);
                Assert.That(scope.Pool.ActiveCount, Is.Zero);
                Assert.That(renderer.sprite, Is.Null);
                scope.Source.sprite = scope.Second;
                scope.Source.transform.position = new Vector3(-4f, -3f, 0f);
                scope.Source.flipX = true;
                scope.Pool.Emit(scope.Source, LegacyStepAction.Dodge);
                Assert.That(scope.Upper, Is.SameAs(renderer));
                Assert.That(scope.GhostLower.enabled, Is.False);
                Assert.That(renderer.sprite, Is.SameAs(scope.Second));
                Assert.That(renderer.flipX, Is.True);
                Vector3 position = renderer.transform.position;
                scope.Source.transform.position += Vector3.right * 9f;
                scope.Pool.Tick(DuelStepAfterimages.Lifetime * .5f);
                Assert.That(renderer.transform.position, Is.EqualTo(position));
                Assert.That(DuelStepAfterimages.DrawnColor(renderer).a,
                    Is.EqualTo(DuelStepAfterimages.InitialAlpha * .5f).Within(.0001f));
                scope.Pool.Tick(DuelStepAfterimages.Lifetime * .5f);
                Assert.That(scope.Pool.ActiveCount, Is.Zero);
                Assert.That(scope.Pool.Root.childCount, Is.EqualTo(1));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SilhouetteSnapshot_FadesThroughItsPropertyTintWithoutMovingOrChangingSize()
        {
            Shader shader = Shader.Find("TurnLimbo/Duel Break Silhouette");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                using (var scope = new Scope(material))
                {
                    scope.Pool.Emit(scope.Source, new Color(.8f, .4f, .1f, .6f), 1f, -3);
                    SpriteRenderer ghost = scope.Upper;
                    Vector3 position = ghost.transform.position, scale = ghost.transform.lossyScale;
                    scope.Pool.Tick(.5f);
                    Assert.That(ghost.transform.position, Is.EqualTo(position));
                    Assert.That(ghost.transform.lossyScale, Is.EqualTo(scale));
                    Assert.That(ghost.color, Is.EqualTo(Color.white));
                    Assert.That(DuelStepAfterimages.DrawnColor(ghost).a, Is.EqualTo(.3f).Within(.0001f));
                }
            }
            finally { Object.Destroy(material); }
            yield return null;
        }

        private static void SetOffset(SpriteRenderer renderer, Vector3 offset)
        {
            var block = new MaterialPropertyBlock();
            block.SetVector(AttackOffsetId, offset);
            renderer.SetPropertyBlock(block);
        }

        private static void AssertPose(SpriteRenderer renderer, Sprite sprite, Vector3 position, Quaternion rotation,
            Vector3 scale, bool flipX, bool flipY)
        {
            Assert.That(renderer.sprite, Is.SameAs(sprite));
            Assert.That(Vector3.Distance(renderer.transform.position, position), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(renderer.transform.rotation, rotation), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(renderer.transform.lossyScale, scale), Is.LessThan(.0001f));
            Assert.That(renderer.flipX, Is.EqualTo(flipX));
            Assert.That(renderer.flipY, Is.EqualTo(flipY));
        }

        private sealed class Scope : IDisposable
        {
            private readonly GameObject host = new GameObject("Fixed afterimage test");
            private readonly Texture2D texture = new Texture2D(4, 4);
            public Sprite First { get; }
            public Sprite Second { get; }
            public SpriteRenderer Source { get; }
            public SpriteRenderer Lower { get; }
            public Camera Camera { get; }
            public DuelStepAfterimages Pool { get; }
            public SpriteRenderer Upper => Pool.Root.GetChild(0).GetComponent<SpriteRenderer>();
            public SpriteRenderer GhostLower => Upper.transform.GetChild(0).GetComponent<SpriteRenderer>();
            public Scope(Material material = null)
            {
                First = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.one * .5f);
                Second = Sprite.Create(texture, new Rect(2f, 2f, 2f, 2f), Vector2.one * .5f);
                var sourceObject = new GameObject("Source");
                sourceObject.transform.SetParent(host.transform, false);
                Source = sourceObject.AddComponent<SpriteRenderer>();
                Source.sprite = First;
                var lowerObject = new GameObject("Source lower");
                lowerObject.transform.SetParent(sourceObject.transform, false);
                Lower = lowerObject.AddComponent<SpriteRenderer>();
                Lower.sprite = Second;
                var cameraObject = new GameObject("Snapshot camera");
                cameraObject.transform.SetParent(host.transform, false);
                Camera = cameraObject.AddComponent<Camera>();
                Camera.enabled = false;
                Camera.orthographic = true;
                Pool = new DuelStepAfterimages(host.transform, material, 0, true, 1, "Snapshot");
            }
            public void Dispose()
            {
                Pool.Dispose();
                Object.Destroy(host);
                Object.Destroy(First);
                Object.Destroy(Second);
                Object.Destroy(texture);
            }
        }
    }
}
