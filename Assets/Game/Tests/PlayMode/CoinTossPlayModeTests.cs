using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class CoinTossPlayModeTests
    {
        [UnityTest]
        public IEnumerator OpeningResult_WaitsForTheLanding_AndReadsFromEitherPlayersPerspective()
        {
            yield return null;
            using (var scope = new Scope())
            {
                for (int opening = 0; opening < 2; opening++)
                    for (int local = 0; local < 2; local++)
                    {
                        DuelStartCard card = scope.Card;
                        card.ShowVersusCoinToss(1f, opening, local);
                        Assert.That(card.IsCoinToss && card.IsShowing, Is.True);
                        Assert.That(card.Timeline.Seconds, Is.EqualTo(DuelStartCard.MinimumCoinTossSeconds),
                            "A positive retry duration still allows the toss and its result to finish.");
                        Assert.That(card.PlayerLabel.text, Is.EqualTo("나"));
                        Assert.That(card.OpponentLabel.text, Is.EqualTo("상대"));
                        Assert.That(card.PlayerFigure.sprite, Is.Not.Null.And.SameAs(card.OpponentFigure.sprite));
                        Assert.That(card.OpponentFigure.rectTransform.localScale.x, Is.LessThan(0f));
                        Assert.That(card.Swords.gameObject.activeInHierarchy, Is.False);
                        Assert.That(card.CoinResultRevealed, Is.False);
                        Assert.That(card.CoinResultText, Is.Empty);
                        card.Tick(card.Timeline.Seconds * .59f);
                        Assert.That(card.CoinResultRevealed, Is.False);
                        foreach (Text text in card.Root.GetComponentsInChildren<Text>(true))
                            Assert.That(text.text, Does.Not.Contain("선공").And.Not.Contain("첫 턴 예약"),
                                "Neither hidden labels nor a coin face leak the result before landing.");
                        card.Tick(DuelStartCard.MinimumCoinTossSeconds * .03f);
                        Assert.That(card.CoinResultRevealed, Is.True);
                        Assert.That(card.CoinResultText, Is.EqualTo(opening == local ? "선공 · 나" : "선공 · 상대"));
                        Assert.That(scope.NamedText("Coin Face").text, Is.EqualTo(opening == local ? "나" : "상대"));
                        Assert.That(scope.NamedText("First Turn Caption").text, Is.EqualTo("첫 턴 예약"));
                        card.Tick(DuelStartCard.MinimumCoinTossSeconds * .18f);
                        Assert.That(card.IsShowing && card.CoinResultRevealed, Is.True,
                            "The landed result remains visible through the hold and into the shared exit.");
                        Assert.That(card.Tick(DuelStartCard.MinimumCoinTossSeconds), Is.True);
                        Assert.That(card.IsShowing || card.IsCoinToss || card.CoinResultRevealed, Is.False);
                        Assert.That(card.CoinResultText, Is.Empty);
                    }
            }
        }

        [UnityTest]
        public IEnumerator ANewTossAndCampaignShow_ClearThePreviousResult_AndKeepTheCampaignDuration()
        {
            yield return null;
            using (var scope = new Scope())
            {
                DuelStartCard card = scope.Card;
                card.ShowVersusCoinToss(3f, 0, 0);
                card.Tick(2f);
                Assert.That(card.CoinResultText, Is.EqualTo("선공 · 나"));
                card.ShowVersusCoinToss(3f, 1, 0);
                Assert.That(card.CoinResultRevealed, Is.False);
                Assert.That(card.CoinResultText, Is.Empty, "A rematch starts with an unrevealed coin.");
                card.Tick(2f);
                Assert.That(card.CoinResultText, Is.EqualTo("선공 · 상대"));

                card.Show(.7f, string.Empty, DuelStartCard.PlayerSilhouette);
                Assert.That(card.IsShowing, Is.True);
                Assert.That(card.IsCoinToss || card.CoinResultRevealed, Is.False);
                Assert.That(card.Timeline.Seconds, Is.EqualTo(.7f), "Campaign cards keep their original timing.");
                Assert.That(card.Swords.gameObject.activeInHierarchy, Is.True);
                foreach (Text text in card.Root.GetComponentsInChildren<Text>(true))
                    Assert.That(text.text == DuelStartCard.PlayerName || text.text.Length == 0, Is.True,
                        "A campaign card still names only its two fighters, including inactive coin labels.");
                card.Clear();
                Assert.That(card.IsShowing || card.Letterbox.IsVisible, Is.False);
                card.ShowVersusCoinToss(0f, 0, 1);
                Assert.That(card.IsShowing || card.IsCoinToss, Is.False, "Zero remains an explicit presentation opt-out.");
            }
        }

        [UnityTest]
        public IEnumerator CoinAndResult_StayInsideWideAndNarrowFrames_WithReadableNamesAndNoOverlap()
        {
            yield return null;
            using (var scope = new Scope())
            using (var render = new CardRender(scope.Root))
            {
                foreach (Vector2Int pixels in new[] { new Vector2Int(1280, 720), new Vector2Int(960, 720) })
                {
                    render.Resize(pixels);
                    scope.Card.ShowVersusCoinToss(3f, 0, 0);
                    scope.Card.Tick(1.07f);
                    yield return null;
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    render.AssertInside(scope.Card.Root.transform.Find("Versus Coin Toss/Tossed Coin").GetComponent<RectTransform>());
                    Assert.That(scope.Card.CoinResultRevealed, Is.False);
                    render.Capture("Toss-" + pixels.x + "x" + pixels.y);

                    scope.Card.Tick(1f);
                    Canvas.ForceUpdateCanvases();
                    Assert.That(scope.Card.CoinResultText, Is.EqualTo("선공 · 나"));
                    AssertLayout(scope, render);
                    render.Capture("Own-Opening-" + pixels.x + "x" + pixels.y);

                    scope.Card.ShowVersusCoinToss(3f, 0, 1);
                    scope.Card.Tick(2.07f);
                    Canvas.ForceUpdateCanvases();
                    Assert.That(scope.Card.CoinResultText, Is.EqualTo("선공 · 상대"));
                    AssertLayout(scope, render);
                    render.Capture("Opponent-Opening-" + pixels.x + "x" + pixels.y);
                }
            }
        }

        private static void AssertLayout(Scope scope, CardRender render)
        {
            Text result = scope.NamedText("Opening Player");
            render.AssertInside(result.rectTransform);
            render.AssertInside(scope.Card.PlayerLabel.rectTransform);
            render.AssertInside(scope.Card.OpponentLabel.rectTransform);
            Assert.That(result.font, Is.SameAs(scope.Card.PlayerLabel.font), "The coin uses the game's existing pixel font.");
            Assert.That(result.fontSize, Is.GreaterThanOrEqualTo(40));
            Assert.That(render.Bounds(result.rectTransform).Overlaps(render.Bounds(scope.Card.PlayerLabel.rectTransform)), Is.False);
            Assert.That(render.Bounds(result.rectTransform).Overlaps(render.Bounds(scope.Card.OpponentLabel.rectTransform)), Is.False);
            foreach (Graphic graphic in scope.Card.Root.transform.Find("Versus Coin Toss").GetComponentsInChildren<Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False, "The coin never adds a second interactive surface.");
        }

        private sealed class Scope : IDisposable
        {
            public readonly GameObject Root = new GameObject("Coin Toss Test");
            private readonly LegacyDuelArt art = new LegacyDuelArt();
            public DuelStartCard Card { get; }
            public Scope() => Card = new DuelStartCard(Root.transform, art, () => { });
            public Text NamedText(string name)
            {
                foreach (Text text in Card.Root.GetComponentsInChildren<Text>(true))
                    if (text.name == name) return text;
                Assert.Fail("Missing coin label: " + name);
                return null;
            }
            public void Dispose() { Card.Dispose(); art.Dispose(); Object.Destroy(Root); }
        }

        private sealed class CardRender : IDisposable
        {
            private readonly GameObject cameraObject = new GameObject("Coin Toss Capture Camera");
            private readonly GameObject root;
            private readonly Camera camera;
            private RenderTexture target;
            public CardRender(GameObject root)
            {
                this.root = root;
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.transform.position = new Vector3(0f, -1000f, -10f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.13f, .105f, .08f);
                camera.cullingMask = 1 << 31;
                camera.depth = -100f;
            }
            public void Resize(Vector2Int pixels)
            {
                camera.targetTexture = null;
                if (target != null) { target.Release(); Object.Destroy(target); }
                target = new RenderTexture(pixels.x, pixels.y, 24);
                camera.targetTexture = target;
                camera.aspect = pixels.x / (float)pixels.y;
                foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                    scaler.scaleFactor = Mathf.Sqrt(pixels.x / 1920f * pixels.y / 1080f);
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 100f;
                }
            }
            public Rect Bounds(RectTransform rect)
            {
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                Vector3 lower = camera.WorldToScreenPoint(corners[0]), upper = camera.WorldToScreenPoint(corners[2]);
                return Rect.MinMaxRect(lower.x, lower.y, upper.x, upper.y);
            }
            public void AssertInside(RectTransform rect)
            {
                Rect bounds = Bounds(rect);
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(0f), rect.name);
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(target.width), rect.name);
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(0f), rect.name);
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(target.height), rect.name);
            }
            public void Capture(string name)
            {
                string directory = Environment.GetEnvironmentVariable("SWORDMASTER_COIN_CAPTURE_DIR");
                if (string.IsNullOrEmpty(directory)) return;
                Directory.CreateDirectory(directory);
                RenderTexture previous = RenderTexture.active;
                Texture2D capture = null;
                try
                {
                    camera.Render();
                    RenderTexture.active = target;
                    capture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                    capture.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
                    capture.Apply();
                    File.WriteAllBytes(Path.Combine(directory, name + ".png"), capture.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    if (capture != null) Object.Destroy(capture);
                }
            }
            public void Dispose()
            {
                camera.targetTexture = null;
                if (target != null) { target.Release(); Object.Destroy(target); }
                Object.Destroy(cameraObject);
            }
        }
    }
}
