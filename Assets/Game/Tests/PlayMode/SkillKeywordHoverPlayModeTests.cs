using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class SkillKeywordHoverPlayModeTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly GameObject CanvasRoot;
            public readonly EventSystem Events;
            public readonly RectTransform Card;
            public readonly Image FirstBadge, SecondBadge;
            public readonly Text FirstLabel, SecondLabel, Body;
            public readonly SkillKeywordHover Hover;
            public readonly SkillKeywordHitGraphic BodyHits;

            public Fixture()
            {
                CanvasRoot = new GameObject("Keyword Hover Canvas", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster));
                CanvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var eventObject = new GameObject("Keyword Hover Events", typeof(EventSystem));
                eventObject.transform.SetParent(CanvasRoot.transform, false);
                Events = eventObject.GetComponent<EventSystem>();
                Card = Rect("Skill Card", CanvasRoot.transform, new Vector2(420f, 360f));
                FirstBadge = Image("First Keyword", Card);
                SecondBadge = Image("Second Keyword", Card);
                FirstLabel = Label("First Label", FirstBadge.transform);
                SecondLabel = Label("Second Label", SecondBadge.transform);
                Body = Label("Effect", Card);
                Body.rectTransform.sizeDelta = new Vector2(320f, 150f);
                Body.fontSize = 20;
                Body.alignment = TextAnchor.UpperLeft;
                Body.supportRichText = false;
                Hover = SkillKeywordHover.Attach(Card, FirstBadge, FirstLabel, SecondBadge,
                    SecondLabel, Body, new LegacyDuelArt().UIFont, 1f);
                BodyHits = Card.GetComponentInChildren<SkillKeywordHitGraphic>();
            }

            public void Dispose() => Object.Destroy(CanvasRoot);

            public PointerEventData Pointer(Vector2 position) => new PointerEventData(Events) { position = position };

            public Vector2 CharacterScreenPoint(int characterIndex)
            {
                var vertices = Body.cachedTextGenerator.verts;
                int renderedGlyph = 0;
                for (int i = 0; i < characterIndex; i++)
                    if (!char.IsWhiteSpace(Body.text[i])) renderedGlyph++;
                int index = renderedGlyph * 4;
                Assert.That(vertices.Count, Is.GreaterThan(index + 3), "The target word must be rendered.");
                Vector3 center = Vector3.zero;
                for (int i = 0; i < 4; i++) center += vertices[index + i].position;
                center /= 4f * Body.pixelsPerUnit;
                return RectTransformUtility.WorldToScreenPoint(null, Body.rectTransform.TransformPoint(center));
            }

            private static RectTransform Rect(string name, Transform parent, Vector2 size)
            {
                var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = size;
                return rect;
            }

            private static Image Image(string name, Transform parent)
            {
                var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
                    .GetComponent<Image>();
                image.rectTransform.SetParent(parent, false);
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = image.rectTransform.pivot = new Vector2(.5f, .5f);
                image.rectTransform.sizeDelta = new Vector2(130f, 28f);
                return image;
            }

            private static Text Label(string name, Transform parent)
            {
                var label = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text))
                    .GetComponent<Text>();
                label.rectTransform.SetParent(parent, false);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(.5f, .5f);
                label.font = new LegacyDuelArt().UIFont;
                label.fontSize = 18;
                label.color = Color.black;
                label.raycastTarget = false;
                return label;
            }
        }

        [Test]
        public void CommonTerms_ExplainMeaningWithoutSkillSpecificNumbers()
        {
            Assert.That(SkillKeywordHover.TryExplain("순환", out string cycle), Is.True);
            Assert.That(cycle, Does.Contain("시전할 때마다"));
            Assert.That(SkillKeywordHover.TryExplain("취소", out string cancel), Is.True);
            Assert.That(cancel, Does.Contain("돌려받지"));
            Assert.That(SkillKeywordHover.TryExplain("반환", out string returned), Is.True);
            Assert.That(returned, Does.Contain("돌려받습니다"));
            Assert.That(SkillKeywordHover.TryExplain("효과 +5", out _), Is.False);
        }

        [UnityTest]
        public IEnumerator BodyOnlyRaycastsOnRenderedTerm_AndBadgeOpensAfterRealTimeDelay()
        {
            using (var fixture = new Fixture())
            {
                fixture.FirstLabel.text = "순환 2/9";
                fixture.SecondLabel.text = "타격";
                fixture.Body.text = "해당 기술의 순환 횟수 1당\n기본 위력 +5";
                fixture.Hover.SetTerms(fixture.FirstLabel.text, fixture.SecondLabel.text, fixture.Body.text);
                yield return null;
                Canvas.ForceUpdateCanvases();

                int termIndex = fixture.Body.text.IndexOf("순환", StringComparison.Ordinal);
                Vector2 termPoint = fixture.CharacterScreenPoint(termIndex);
                Vector2 ordinaryPoint = fixture.CharacterScreenPoint(0);
                Assert.That(fixture.BodyHits.Raycast(termPoint, null), Is.True);
                Assert.That(fixture.BodyHits.Raycast(ordinaryPoint, null), Is.False,
                    "Ordinary effect text must not intercept the parent card.");
                Assert.That(fixture.FirstBadge.raycastTarget, Is.True);
                Assert.That(fixture.SecondBadge.raycastTarget, Is.False);

                var bodyTarget = fixture.BodyHits.GetComponent<SkillKeywordHoverTarget>();
                bodyTarget.OnPointerEnter(fixture.Pointer(termPoint));
                bodyTarget.OnPointerMove(fixture.Pointer(termPoint));
                yield return new WaitForSecondsRealtime(SkillKeywordHover.DelaySeconds + .12f);
                var popup = GameObject.Find("Skill Keyword Explanation");
                Assert.That(popup, Is.Not.Null);
                Assert.That(popup.activeSelf, Is.True);
                Assert.That(popup.transform.Find("Keyword Title").GetComponent<Text>().text, Is.EqualTo("순환"));
                bodyTarget.OnPointerMove(fixture.Pointer(ordinaryPoint));
                Assert.That(popup.activeSelf, Is.False, "Leaving the term glyphs closes the explanation.");

                var badgeTarget = fixture.FirstBadge.GetComponent<SkillKeywordHoverTarget>();
                badgeTarget.OnPointerEnter(fixture.Pointer(RectTransformUtility.WorldToScreenPoint(null,
                    fixture.FirstBadge.rectTransform.position)));
                yield return new WaitForSecondsRealtime(SkillKeywordHover.DelaySeconds + .12f);
                Assert.That(popup.activeSelf, Is.True);
                Assert.That(popup.transform.Find("Keyword Title").GetComponent<Text>().text, Is.EqualTo("순환"));
                Assert.That(popup.transform.Find("Keyword Meaning").GetComponent<Text>().text, Does.Contain("시전"));

                badgeTarget.OnPointerExit(fixture.Pointer(Vector2.zero));
                Assert.That(popup.activeSelf, Is.False);
                fixture.Hover.SetTerms("취소", string.Empty, "앞에 예약한 기술 취소");
                Assert.That(popup.activeSelf, Is.False);
                Assert.That(fixture.FirstBadge.raycastTarget, Is.True);
                fixture.Hover.Clear();
                Assert.That(fixture.FirstBadge.raycastTarget, Is.False);
                Assert.That(fixture.BodyHits.raycastTarget, Is.False);
            }
        }
        [UnityTest]
        public IEnumerator CompositeBadgesAndPrefixedWords_ResolveOnlyStandaloneTerms()
        {
            using (var fixture = new Fixture())
            {
                fixture.FirstLabel.text = "상대 붕괴 시";
                fixture.Body.text = "ACT 미반환\n반환";
                fixture.Hover.SetTerms(fixture.FirstLabel.text, string.Empty, fixture.Body.text);
                yield return null;
                Canvas.ForceUpdateCanvases();

                int prefixed = fixture.Body.text.IndexOf("반환", StringComparison.Ordinal);
                int standalone = fixture.Body.text.LastIndexOf("반환", StringComparison.Ordinal);
                Assert.That(fixture.FirstBadge.raycastTarget, Is.True, "Composite keyword badges remain hoverable.");
                Assert.That(fixture.BodyHits.Raycast(fixture.CharacterScreenPoint(prefixed), null), Is.False,
                    "미반환 must not offer a tooltip claiming ACT is returned.");
                Vector2 standalonePoint = fixture.CharacterScreenPoint(standalone);
                Assert.That(fixture.BodyHits.Raycast(standalonePoint, null), Is.True);
                var bodyTarget = fixture.BodyHits.GetComponent<SkillKeywordHoverTarget>();
                bodyTarget.OnPointerEnter(fixture.Pointer(standalonePoint));
                bodyTarget.OnPointerMove(fixture.Pointer(standalonePoint));
                yield return new WaitForSecondsRealtime(SkillKeywordHover.DelaySeconds + .12f);
                var popup = GameObject.Find("Skill Keyword Explanation");
                Assert.That(popup.activeSelf, Is.True);
                Assert.That(popup.transform.Find("Keyword Title").GetComponent<Text>().text, Is.EqualTo("반환"));
                bodyTarget.OnPointerExit(fixture.Pointer(Vector2.zero));
                Assert.That(popup.activeSelf, Is.False);

                fixture.FirstBadge.GetComponent<SkillKeywordHoverTarget>().OnPointerEnter(fixture.Pointer(
                    RectTransformUtility.WorldToScreenPoint(null, fixture.FirstBadge.rectTransform.position)));
                yield return new WaitForSecondsRealtime(SkillKeywordHover.DelaySeconds + .12f);
                Assert.That(popup.transform.Find("Keyword Title").GetComponent<Text>().text, Is.EqualTo("붕괴"));
                fixture.Hover.SetTerms("ACT 미반환", string.Empty, fixture.Body.text);
                Assert.That(popup.activeSelf, Is.False);
                Assert.That(fixture.FirstBadge.raycastTarget, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator LongMeaning_FitsAndPopupStaysInsideCanvasAtTheScreenEdge()
        {
            using (var fixture = new Fixture())
            {
                var canvas = fixture.CanvasRoot.GetComponent<RectTransform>();
                fixture.Card.anchoredPosition = new Vector2(canvas.rect.xMax - fixture.Card.rect.width / 2f - 4f,
                    canvas.rect.yMin + fixture.Card.rect.height / 2f + 4f);
                fixture.FirstLabel.text = "취소";
                fixture.Hover.SetTerms(fixture.FirstLabel.text, string.Empty, string.Empty);
                yield return null;
                Canvas.ForceUpdateCanvases();

                var badgeTarget = fixture.FirstBadge.GetComponent<SkillKeywordHoverTarget>();
                badgeTarget.OnPointerEnter(fixture.Pointer(RectTransformUtility.WorldToScreenPoint(null,
                    fixture.FirstBadge.rectTransform.position)));
                yield return new WaitForSecondsRealtime(SkillKeywordHover.DelaySeconds + .12f);
                Canvas.ForceUpdateCanvases();
                var popup = GameObject.Find("Skill Keyword Explanation").GetComponent<RectTransform>();
                Assert.That(popup.gameObject.activeSelf, Is.True);
                Text meaning = popup.Find("Keyword Meaning").GetComponent<Text>();
                Assert.That(meaning.preferredHeight, Is.LessThanOrEqualTo(meaning.rectTransform.rect.height + .5f),
                    "The explanation must fit without clipping or tiny type.");
                Assert.That(meaning.cachedTextGenerator.fontSizeUsedForBestFit, Is.GreaterThanOrEqualTo(14));
                Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas, popup);
                Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(canvas.rect.xMin - .5f));
                Assert.That(bounds.max.x, Is.LessThanOrEqualTo(canvas.rect.xMax + .5f));
                Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(canvas.rect.yMin - .5f));
                Assert.That(bounds.max.y, Is.LessThanOrEqualTo(canvas.rect.yMax + .5f));
                fixture.Card.gameObject.SetActive(false);
                Assert.That(popup.gameObject.activeSelf, Is.False, "Hiding the card dismisses its glossary.");
            }
        }

    }
}

