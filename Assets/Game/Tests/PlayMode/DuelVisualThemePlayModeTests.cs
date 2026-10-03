using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class DuelVisualThemePlayModeTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Parent = new GameObject("Visual Theme Test");
            public readonly CampaignRun Run = new CampaignRun();
            public readonly LegacyDuelArt Art = new LegacyDuelArt();
            public readonly CampaignLobbyHud Lobby;
            private readonly GameObject ownedEventSystem;
            private LegacyCombatHud combat;

            public Fixture()
            {
                if (EventSystem.current == null)
                    ownedEventSystem = new GameObject("Visual Theme Event System", typeof(EventSystem));
                Lobby = new CampaignLobbyHud(Parent.transform, Art, null, null, null, null, null, null, null);
                Lobby.Show(Run);
            }

            public LegacyCombatHud Combat()
            {
                Lobby.Hide();
                if (combat == null) combat = new LegacyCombatHud(Parent.transform, Art, null, null, null);
                Canvas.ForceUpdateCanvases();
                return combat;
            }

            public void Dispose()
            {
                combat?.Dispose();
                Lobby.Dispose();
                if (ownedEventSystem != null) Object.Destroy(ownedEventSystem);
                Object.Destroy(Parent);
            }
        }

        [UnityTest]
        public IEnumerator RepeatedThemeApplication_ReusesTrimAndPreservesSourceImageAndLayout()
        {
            yield return null;
            var node = new GameObject("Theme Source", typeof(RectTransform), typeof(Image), typeof(Button));
            try
            {
                var image = node.GetComponent<Image>();
                image.color = new Color(.22f, .38f, .51f, .73f);
                image.raycastTarget = true;
                var rect = image.rectTransform;
                rect.anchorMin = new Vector2(.15f, .25f);
                rect.anchorMax = new Vector2(.75f, .85f);
                rect.pivot = new Vector2(.3f, .7f);
                rect.anchoredPosition = new Vector2(47f, -31f);
                rect.sizeDelta = new Vector2(240f, 132f);
                rect.localScale = new Vector3(.9f, 1.1f, 1f);
                rect.localRotation = Quaternion.Euler(0f, 0f, 7f);
                var caption = new GameObject("Existing Caption", typeof(RectTransform));
                caption.transform.SetParent(rect, false);
                var button = node.GetComponent<Button>();
                button.targetGraphic = image;

                Color originalColor = image.color;
                Vector2 anchorsMin = rect.anchorMin, anchorsMax = rect.anchorMax, pivot = rect.pivot;
                Vector2 position = rect.anchoredPosition, size = rect.sizeDelta;
                Vector3 scale = rect.localScale;
                Quaternion rotation = rect.localRotation;

                DuelVisualTheme.Frame(image);
                var originalTrim = image.GetComponentInChildren<DuelPanelTrim>();
                for (int repeat = 0; repeat < 5; repeat++)
                {
                    DuelVisualTheme.DressPanel(image);
                    DuelVisualTheme.StyleButton(button, true);
                    DuelVisualTheme.StyleButton(button);
                    DuelVisualTheme.Frame(image);
                }

                Assert.That(image.GetComponentsInChildren<DuelPanelTrim>(true).Length, Is.EqualTo(1),
                    "Restyling one image must reuse its decoration, not stack more trims.");
                Assert.That(image.GetComponentInChildren<DuelPanelTrim>(), Is.SameAs(originalTrim));
                Assert.That(originalTrim, Is.Not.Null);
                Assert.That(originalTrim.GetComponent<CanvasRenderer>(), Is.Not.Null,
                    "Custom MaskableGraphic trim needs a renderer for clipping and show/hide.");
                Assert.That(originalTrim.raycastTarget, Is.False);
                Assert.That(originalTrim.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(originalTrim.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(originalTrim.rectTransform.sizeDelta, Is.EqualTo(Vector2.zero));
                Assert.That(originalTrim.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(originalTrim.transform.GetSiblingIndex(), Is.LessThan(caption.transform.GetSiblingIndex()),
                    "The existing caption remains above the decoration.");
                Assert.That(image.color, Is.EqualTo(originalColor));
                Assert.That(image.raycastTarget, Is.True);
                Assert.That(button.targetGraphic, Is.SameAs(image));
                Assert.That(rect.anchorMin, Is.EqualTo(anchorsMin));
                Assert.That(rect.anchorMax, Is.EqualTo(anchorsMax));
                Assert.That(rect.pivot, Is.EqualTo(pivot));
                Assert.That(rect.anchoredPosition, Is.EqualTo(position));
                Assert.That(rect.sizeDelta, Is.EqualTo(size));
                Assert.That(rect.localScale, Is.EqualTo(scale));
                Assert.That(rect.localRotation, Is.EqualTo(rotation));
            }
            finally { Object.Destroy(node); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator LobbyLoadoutCurriculumAndCombat_UseSharedSurfacesAndNonInteractiveTrim()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                AssertThemed(Find<Image>(fixture.Lobby.Root, "Lobby Header"), DuelVisualTheme.Surface);
                AssertThemed(Find<Image>(fixture.Lobby.Root, "Lobby Status Slip"), DuelVisualTheme.RaisedSurface);
                AssertThemed(Find<Image>(fixture.Lobby.Root, "Home Sidebar"), DuelVisualTheme.Surface);
                AssertThemed(Find<Image>(fixture.Lobby.Root, "Home Journey"), DuelVisualTheme.RaisedSurface);
                AssertThemed(Find<Image>(fixture.Lobby.Root, "Home Open Stages"), DuelVisualTheme.Accent);
                AssertDecorationsIgnoreInput(fixture.Lobby.Root);

                fixture.Lobby.ShowTab(LobbyTab.Stages);
                AssertThemed(Find<Image>(fixture.Lobby.Root, "Page Ledger"), DuelVisualTheme.Surface);
                AssertDecorationsIgnoreInput(fixture.Lobby.Root);

                fixture.Lobby.ShowTab(LobbyTab.Loadout);
                AssertThemed(Find<Image>(fixture.Lobby.Root, "Page Ledger"), DuelVisualTheme.Surface);
                AssertThemed(Find<Image>(fixture.Lobby.Root, "Detail Surface"), DuelVisualTheme.Paper);
                AssertButtonHasTrim(Find<Button>(fixture.Lobby.Root, "Loadout Slot Q 1"));
                AssertDecorationsIgnoreInput(fixture.Lobby.Root);

                fixture.Lobby.ShowTab(LobbyTab.Curriculum);
                AssertThemed(Find<Image>(fixture.Lobby.Root, "Page Ledger"), DuelVisualTheme.Surface);
                AssertThemed(Find<Image>(fixture.Lobby.Root, "Curriculum Detail Surface"), DuelVisualTheme.Paper);
                AssertThemed(Find<Image>(fixture.Lobby.Root, "Curriculum Tree"), DuelVisualTheme.Track);
                AssertButtonHasTrim(Find<Button>(fixture.Lobby.Root, "Curriculum Node horizontal-cut"));
                AssertButtonHasTrim(Find<Button>(fixture.Lobby.Root, "Curriculum Primary Action"));
                AssertDecorationsIgnoreInput(fixture.Lobby.Root);

                LegacyCombatHud combat = fixture.Combat();
                var attack = Find<Button>(combat.Root, "Current Q");
                var commit = Find<Button>(combat.Root, "AButton");
                AssertThemed(attack.transform.Find("Surface").GetComponent<Image>(), DuelVisualTheme.Card);
                AssertThemed(commit.transform.Find("Surface").GetComponent<Image>(), DuelVisualTheme.RaisedSurface);
                AssertButtonHasTrim(attack);
                AssertButtonHasTrim(commit);
                AssertDecorationsIgnoreInput(combat.Root);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DecorativeTrim_DoesNotInterceptButtonOrLoadoutDragAndDropRaycasts()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                // A newly created Canvas needs a render lifecycle before its graphics have
                // absolute depths; forcing a rebuild alone does not guarantee that render.
                yield return null;
                var home = Find<Button>(fixture.Lobby.Root, "Home Open Stages");
                AssertPointerRoutesToButton(fixture.Lobby.Root, home);

                Assert.That(fixture.Run.TryUnequipSkill(1), Is.True);
                fixture.Lobby.ShowTab(LobbyTab.Loadout);
                yield return WaitForPageTransition(fixture.Lobby);
                var slot = Find<Button>(fixture.Lobby.Root, "Loadout Slot Q 1");
                var owned = Find<Button>(fixture.Lobby.Root, "Loadout Owned Skill 1");
                GameObject slotHit = AssertPointerRoutesToButton(fixture.Lobby.Root, slot);
                GameObject ownedHit = AssertPointerRoutesToButton(fixture.Lobby.Root, owned);
                Assert.That(ExecuteEvents.GetEventHandler<IBeginDragHandler>(slotHit), Is.SameAs(slot.gameObject));
                Assert.That(ExecuteEvents.GetEventHandler<IDropHandler>(slotHit), Is.SameAs(slot.gameObject));
                Assert.That(ExecuteEvents.GetEventHandler<IBeginDragHandler>(ownedHit), Is.SameAs(owned.gameObject),
                    "Framing collection cards must not steal their drag source.");

                fixture.Lobby.ShowTab(LobbyTab.Curriculum);
                yield return WaitForPageTransition(fixture.Lobby);
                AssertPointerRoutesToButton(fixture.Lobby.Root, Find<Button>(fixture.Lobby.Root, "Curriculum Node horizontal-cut"));
                AssertPointerRoutesToButton(fixture.Lobby.Root, Find<Button>(fixture.Lobby.Root, "Curriculum Primary Action"));

                LegacyCombatHud combat = fixture.Combat();
                yield return null;
                AssertPointerRoutesToButton(combat.Root, Find<Button>(combat.Root, "Current Q"));
                AssertPointerRoutesToButton(combat.Root, Find<Button>(combat.Root, "AButton"));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator PublicSkillDetails_UseReadablePaperAndInkAcrossPlanningViews()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                fixture.Lobby.ShowTab(LobbyTab.Loadout);
                Find<Button>(fixture.Lobby.Root, "Loadout Slot Q 1").onClick.Invoke();
                Image loadoutPaper = Find<Image>(fixture.Lobby.Root, "Detail Surface");
                AssertPaperDetails(loadoutPaper,
                    "Detail Heading", "Loadout Detail Name", "Loadout Detail Role", "Loadout Detail Values",
                    "Loadout Detail Effect");
                AssertSharedDamageRoutingRemoved(loadoutPaper.gameObject, "Loadout Damage Hint");

                fixture.Lobby.ShowTab(LobbyTab.Curriculum);
                Image curriculumPaper = Find<Image>(fixture.Lobby.Root, "Curriculum Detail Surface");
                string[] curriculumTexts =
                {
                    "Curriculum Detail Heading", "Curriculum Detail Name", "Curriculum Detail Purpose",
                    "Curriculum Detail Values", "Curriculum Detail Effect", "Curriculum Requirement",
                    "Curriculum Availability",
                };
                AssertPaperDetails(curriculumPaper, curriculumTexts);
                AssertSharedDamageRoutingRemoved(curriculumPaper.gameObject, "Curriculum Damage Hint");
                Transform exclusive = curriculumPaper.transform.Find("Curriculum Exclusive");
                Assert.That(exclusive, Is.Not.Null);
                Assert.That(exclusive.gameObject.activeSelf, Is.False,
                    "A node without a paired choice collapses the exclusive line instead of reserving blank paper.");
                Find<Button>(fixture.Lobby.Root, "Curriculum Node one-stroke").onClick.Invoke();
                AssertPaperDetails(curriculumPaper, curriculumTexts);
                Assert.That(exclusive.gameObject.activeSelf, Is.True);
                Assert.That(Find<Text>(curriculumPaper.gameObject, "Curriculum Exclusive").text, Is.Not.Empty,
                    "A paired node names the choice it closes.");

                LegacyCombatHud combat = fixture.Combat();
                combat.ShowExplanation(fixture.Run.OwnedSkills[0].Skill, false);
                var playerDetail = Find<Image>(combat.Root, "Skill Explain");
                AssertPaperDetails(playerDetail.transform.Find("Surface").GetComponent<Image>(),
                    playerDetail.gameObject, "Name", "Power Cost Property", "Effect");
                AssertSharedDamageRoutingRemoved(playerDetail.gameObject, "Player Damage Hint");

                combat.ShowExplanation(fixture.Run.OwnedSkills[0].Skill, true);
                var enemyDetail = Find<Image>(combat.Root, "Enemy Skill Explain");
                AssertPaperDetails(enemyDetail.transform.Find("Surface").GetComponent<Image>(),
                    enemyDetail.gameObject, "Name", "Power Property", "Effect");
                AssertSharedDamageRoutingRemoved(enemyDetail.gameObject, "Enemy Damage Hint");
            }
            yield return null;
        }

        private static void AssertThemed(Image image, Color expected)
        {
            Assert.That(image, Is.Not.Null);
            Assert.That(image.color, Is.EqualTo(expected), image.name + " should use the shared theme.");
            Assert.That(image.GetComponentInChildren<DuelPanelTrim>(true), Is.Not.Null,
                image.name + " should retain a common frame.");
        }

        private static void AssertButtonHasTrim(Button button)
        {
            Assert.That(button.targetGraphic, Is.Not.Null);
            Assert.That(button.targetGraphic.GetComponentInChildren<DuelPanelTrim>(true), Is.Not.Null,
                button.name + " should frame the graphic that actually receives its visual state.");
            foreach (Graphic decoration in button.GetComponentsInChildren<Graphic>(true))
                if (decoration.gameObject != button.gameObject)
                    Assert.That(decoration.raycastTarget, Is.False, decoration.name + " must not cover " + button.name);
        }

        private static void AssertDecorationsIgnoreInput(GameObject root)
        {
            var trims = root.GetComponentsInChildren<DuelPanelTrim>();
            Assert.That(trims, Is.Not.Empty);
            foreach (DuelPanelTrim trim in trims)
                Assert.That(trim.raycastTarget, Is.False, trim.transform.parent.name + " trim must be decorative only.");
        }

        private static IEnumerator WaitForPageTransition(CampaignLobbyHud lobby)
        {
            // Page input is deliberately guarded during the fade. Validate pointer routing
            // after the real unscaled animation finishes, not by bypassing that guard.
            float deadline = Time.realtimeSinceStartup + 2f;
            while (lobby.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(lobby.IsTransitioning, Is.False, "The incoming lobby page must finish its transition before pointer use.");
            CanvasGroup group = lobby.CurrentPage.GetComponent<CanvasGroup>();
            Assert.That(group, Is.Not.Null);
            Assert.That(group.alpha, Is.EqualTo(1f));
            Assert.That(group.interactable, Is.True);
            Assert.That(group.blocksRaycasts, Is.True);
            // Let the final opacity change complete a render lifecycle as well.
            yield return null;
        }

        private static void AssertSharedDamageRoutingRemoved(GameObject detail, string hintName)
        {
            Text hint = null;
            foreach (Text text in detail.GetComponentsInChildren<Text>(true))
                if (text.name == hintName) hint = text;
            Assert.That(hint, Is.Not.Null, "The compatibility reference remains available: " + hintName);
            Assert.That(hint.text, Is.Empty, hintName + " must not repeat a shared combat rule.");
            Assert.That(hint.gameObject.activeSelf, Is.False);
            Assert.That(hint.gameObject.activeInHierarchy, Is.False);
            foreach (Transform node in detail.GetComponentsInChildren<Transform>(true))
                Assert.That(node.name, Is.Not.EqualTo("Damage Routing"));
            foreach (Text text in detail.GetComponentsInChildren<Text>())
            {
                Assert.That(text.text, Does.Not.Contain("공격과 대결 → 저항"));
                Assert.That(text.text, Does.Not.Contain("방어·빈칸 → 체력"));
            }
        }

        private static GameObject AssertPointerRoutesToButton(GameObject root, Button button)
        {
            Canvas.ForceUpdateCanvases();
            Assert.That(EventSystem.current, Is.Not.Null, "Pointer validation requires an active EventSystem.");
            var canvas = root.GetComponent<Canvas>();
            var raycaster = root.GetComponent<GraphicRaycaster>();
            var graphic = button.GetComponent<Image>();
            Assert.That(canvas, Is.Not.Null);
            Assert.That(raycaster, Is.Not.Null);
            Assert.That(graphic, Is.Not.Null);
            var rect = button.GetComponent<RectTransform>();
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(raycaster.eventCamera, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left,
                displayIndex = canvas.targetDisplay
            };
            var registered = GraphicRegistry.GetRaycastableGraphicsForCanvas(canvas);
            string diagnostics = RaycastDiagnostics(canvas, graphic, pointer.position, registered.Count);
            Assert.That(canvas.isActiveAndEnabled, Is.True, diagnostics);
            Assert.That(raycaster.isActiveAndEnabled, Is.True, diagnostics);
            Assert.That(graphic.isActiveAndEnabled, Is.True, diagnostics);
            Assert.That(graphic.canvas, Is.SameAs(canvas), "The button must register with its fixture canvas. " + diagnostics);
            bool isRegistered = false;
            for (int index = 0; index < registered.Count; index++)
                if (registered[index] == graphic) isRegistered = true;
            Assert.That(isRegistered, Is.True, "The button must be registered for raycasting. " + diagnostics);
            Assert.That(graphic.depth, Is.GreaterThanOrEqualTo(0),
                "The fixture graphics have not completed a render lifecycle. " + diagnostics);
            Assert.That(graphic.canvasRenderer.cull, Is.False, diagnostics);
            Assert.That(graphic.raycastTarget, Is.True, diagnostics);
            Assert.That(canvas.pixelRect.Contains(pointer.position), Is.True,
                "The test pointer must be inside the canvas viewport. " + diagnostics);
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(rect, pointer.position, raycaster.eventCamera),
                Is.True, diagnostics);
            Assert.That(graphic.Raycast(pointer.position, raycaster.eventCamera), Is.True,
                "A CanvasGroup or mask must not filter out this button. " + diagnostics);
            var hits = new List<RaycastResult>();
            raycaster.Raycast(pointer, hits);
            Assert.That(hits, Is.Not.Empty, button.name + " must still be reachable by a pointer. " + diagnostics);
            foreach (RaycastResult hit in hits)
                Assert.That(hit.gameObject.GetComponent<DuelPanelTrim>(), Is.Null,
                    "Decorative geometry must not participate in input raycasts.");
            GameObject firstHit = hits[0].gameObject;
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(firstHit), Is.SameAs(button.gameObject),
                "The top graphic must route input to " + button.name + ", not " + firstHit.name + ". " + diagnostics);
            return firstHit;
        }

        private static string RaycastDiagnostics(Canvas canvas, Graphic graphic, Vector2 pointer, int registeredCount)
        {
            return "Button=" + graphic.name + "; frame=" + Time.frameCount
                + "; screen=" + Screen.width + "x" + Screen.height
                + "; graphics=" + SystemInfo.graphicsDeviceType + "; batch=" + Application.isBatchMode
                + "; mode=" + canvas.renderMode + "; display=" + canvas.targetDisplay
                + "; pixelRect=" + canvas.pixelRect + "; canvasRect=" + canvas.GetComponent<RectTransform>().rect
                + "; scale=" + canvas.scaleFactor + "; pointer=" + pointer
                + "; registered=" + registeredCount + "; depth=" + graphic.depth
                + "; culled=" + graphic.canvasRenderer.cull + "; raycastTarget=" + graphic.raycastTarget
                + "; rect=" + graphic.rectTransform.rect + "; active=" + graphic.isActiveAndEnabled + ".";
        }

        private static void AssertPaperDetails(Image paper, params string[] textNames)
            => AssertPaperDetails(paper, paper.gameObject, textNames);

        private static void AssertPaperDetails(Image paper, GameObject detailRoot, params string[] textNames)
        {
            AssertThemed(paper, DuelVisualTheme.Paper);
            foreach (string name in textNames)
            {
                Text label = Find<Text>(detailRoot, name);
                Assert.That(label.color, Is.EqualTo(DuelVisualTheme.Ink), name + " must read as ink on paper.");
                Assert.That(Contrast(paper.color, label.color), Is.GreaterThanOrEqualTo(4.5f),
                    name + " must retain readable contrast after theming.");
                Assert.That(label.raycastTarget, Is.False);
            }
        }

        private static float Contrast(Color first, Color second)
        {
            Color linearFirst = first.linear, linearSecond = second.linear;
            float firstLuminance = .2126f * linearFirst.r + .7152f * linearFirst.g + .0722f * linearFirst.b;
            float secondLuminance = .2126f * linearSecond.r + .7152f * linearSecond.g + .0722f * linearSecond.b;
            return (Mathf.Max(firstLuminance, secondLuminance) + .05f)
                / (Mathf.Min(firstLuminance, secondLuminance) + .05f);
        }

        private static T Find<T>(GameObject root, string name) where T : Component
        {
            foreach (T candidate in root.GetComponentsInChildren<T>())
                if (candidate.name == name) return candidate;
            Assert.Fail("Missing visible theme node: " + name);
            return null;
        }
    }
}
