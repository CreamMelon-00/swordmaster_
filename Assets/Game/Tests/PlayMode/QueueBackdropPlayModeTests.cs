using System;
using System.Collections;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class QueueBackdropPlayModeTests
    {
        [UnityTest]
        public IEnumerator ResolvingMovesTheSameSquareCardsBehindActors_AndPlanningRestoresThem()
        {
            yield return null;
            using (var scope = new Scope())
            {
                yield return null;
                var duel = Duel();
                scope.Refresh(duel, false);
                RectTransform card = scope.Hud.GetQueuedSkillAnchor(true, 0);
                Transform row = card.parent;
                Sprite original = card.Find("Icon").GetComponent<Image>().sprite;
                Vector2 size = card.sizeDelta;
                Vector3 scale = card.localScale;
                float before = PixelWidth(card);
                float beforeHeight = PixelHeight(card);
                Assert.That(row.parent, Is.SameAs(scope.Hud.Root.transform));
                scope.Refresh(duel, true);
                Assert.That(scope.Hud.GetQueuedSkillAnchor(true, 0), Is.SameAs(card), "Reuse the existing card, not a second queue.");
                Assert.That(card.sizeDelta, Is.EqualTo(size), "The normal card stays square.");
                Assert.That(card.localScale, Is.EqualTo(scale), "Enlargement is uniform on the shared parent.");
                Assert.That(card.Find("Icon").GetComponent<Image>().sprite, Is.SameAs(original));
                float enlargement = PixelWidth(card) / before;
                Assert.That(PixelHeight(card) / beforeHeight, Is.EqualTo(enlargement).Within(.02f),
                    "The original icon is enlarged equally on both axes, never stretched vertically.");
                Assert.That(enlargement, Is.EqualTo(row.parent.localScale.x).Within(.02f));
                Assert.That(enlargement, Is.LessThanOrEqualTo(LegacyCombatHud.QueueBackdropScale + .02f));
                if (((RectTransform)scope.Hud.Root.transform).rect.width >= 1800f)
                    Assert.That(enlargement, Is.EqualTo(LegacyCombatHud.QueueBackdropScale).Within(.02f));
                Assert.That(scope.Hud.QueueIsBackdrop, Is.True);
                Canvas backdrop = row.GetComponentInParent<Canvas>();
                Assert.That(backdrop.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
                Assert.That(backdrop.worldCamera, Is.SameAs(scope.Camera));
                Assert.That(backdrop.sortingOrder, Is.LessThan(-3).And.GreaterThan(-5),
                    "Between all four current campaign figures and the forest/corridor background.");
                Assert.That(backdrop.GetComponent<CanvasGroup>().alpha, Is.EqualTo(.38f).Within(.001f));
                Assert.That(backdrop.GetComponent<GraphicRaycaster>(), Is.Null);
                Assert.That(backdrop.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                Assert.That(card.gameObject.layer, Is.EqualTo(30));
                Assert.That(scope.Hud.PlayerQueuePanel.gameObject.activeInHierarchy, Is.False);
                scope.Refresh(duel, false);
                Assert.That(scope.Hud.QueueIsBackdrop, Is.False);
                Assert.That(card.parent, Is.SameAs(row));
                Assert.That(row.parent, Is.SameAs(scope.Hud.Root.transform));
                Assert.That(card.gameObject.layer, Is.EqualTo(5));
                Assert.That(scope.Hud.PlayerQueuePanel.gameObject.activeInHierarchy, Is.True);
                Assert.That(PixelWidth(card), Is.EqualTo(before).Within(.02f));
                Assert.That(duel.Player.Health, Is.EqualTo(1000));
            }
        }

        [UnityTest]
        public IEnumerator QueueMoveDoesNotMoveTheExistingReadoutOrCameraClearance()
        {
            yield return null;
            using (var moved = new Scope())
            using (var original = new Scope())
            {
                yield return null;
                var duel = Duel();
                moved.Refresh(duel, true);
                original.Refresh(duel, true, false);
                Assert.That(moved.Hud.QueueRowTop, Is.EqualTo(original.Hud.QueueRowTop).Within(.001f));
                Assert.That(moved.Hud.QueuePanelFloor, Is.EqualTo(original.Hud.QueuePanelFloor).Within(.001f));
                Assert.That(moved.Hud.PlayerQueuePanel.anchoredPosition,
                    Is.EqualTo(original.Hud.PlayerQueuePanel.anchoredPosition));
                RectTransform card = moved.Hud.GetQueuedSkillAnchor(true, 0);
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(moved.Camera, card.position);
                Assert.That(screen.y / Screen.height, Is.EqualTo(.53f).Within(.01f), "Only the actual queue moves to the centre.");
            }
        }

        [UnityTest]
        public IEnumerator FadeDisableResetAndDisposeLeaveNoIndependentQueueOnScreen()
        {
            yield return null;
            using (var scope = new Scope())
            {
                yield return null;
                var duel = Duel();
                scope.Refresh(duel, true);
                Canvas backdrop = scope.Hud.GetQueuedSkillAnchor(true, 0).GetComponentInParent<Canvas>();
                scope.Hud.SetFade(.5f);
                Assert.That(backdrop.GetComponent<CanvasGroup>().alpha, Is.EqualTo(.19f).Within(.001f));
                scope.Hud.Root.SetActive(false);
                Assert.That(backdrop.gameObject.activeSelf, Is.False);
                Assert.That(scope.Hud.QueueIsBackdrop, Is.True, "Disable hides the sibling canvas without reparenting during OnDisable.");
                scope.Hud.Root.SetActive(true);
                scope.Refresh(duel, true);
                Assert.That(scope.Hud.GetQueuedSkillAnchor(true, 0).GetComponentInParent<Canvas>(), Is.SameAs(backdrop));
                Assert.That(backdrop.gameObject.activeSelf, Is.True);
                scope.Hud.Reset();
                Assert.That(scope.Hud.QueueIsBackdrop, Is.False);
                Assert.That(backdrop.gameObject.activeSelf, Is.False);
                scope.Refresh(duel, true);
                scope.Hud.Dispose();
                yield return null;
                Assert.That(backdrop == null, Is.True, "The sibling canvas shares the HUD's lifetime.");
            }
        }

        [UnityTest]
        public IEnumerator TempoWidthsAndMirroringSurviveTheUniformBackdropTransform()
        {
            yield return null;
            using (var scope = new Scope())
            {
                yield return null;
                var duel = Duel(new[] { Skill(99701, 1), Skill(99702, 1), Skill(99703, 2), Skill(99704, 4) },
                    new[] { Skill(99711, 2), Skill(99712, 2), Skill(99713, 2), Skill(99714, 2) });
                scope.Refresh(duel, true);
                RectTransform fast = scope.Hud.GetQueuedSkillAnchor(true, 0), normal = scope.Hud.GetQueuedSkillAnchor(true, 2),
                    charge = scope.Hud.GetQueuedSkillAnchor(true, 3);
                Assert.That(PixelWidth(fast), Is.EqualTo(PixelWidth(normal) * .5f).Within(.03f));
                Assert.That(PixelWidth(charge), Is.EqualTo(PixelWidth(normal) * 2f).Within(.03f));
                RectTransform second = scope.Hud.GetQueuedSkillAnchor(true, 1), opposing = scope.Hud.GetQueuedSkillAnchor(false, 0);
                float mid = Screen.width * .5f;
                Assert.That(mid - OuterScreen(second, false), Is.EqualTo(OuterScreen(opposing, true) - mid).Within(.05f),
                    "Both fast cards end at the opposing normal card's mirrored endpoint.");
            }
        }

        private static float PixelWidth(RectTransform card)
        {
            var corners = new Vector3[4];
            card.GetWorldCorners(corners);
            Camera camera = card.GetComponentInParent<Canvas>().rootCanvas.worldCamera;
            return Vector2.Distance(RectTransformUtility.WorldToScreenPoint(camera, corners[0]),
                RectTransformUtility.WorldToScreenPoint(camera, corners[3]));
        }

        private static float PixelHeight(RectTransform card)
        {
            var corners = new Vector3[4];
            card.GetWorldCorners(corners);
            Camera camera = card.GetComponentInParent<Canvas>().rootCanvas.worldCamera;
            return Vector2.Distance(RectTransformUtility.WorldToScreenPoint(camera, corners[0]),
                RectTransformUtility.WorldToScreenPoint(camera, corners[1]));
        }

        private static float OuterScreen(RectTransform card, bool enemy)
        {
            Camera camera = card.GetComponentInParent<Canvas>().rootCanvas.worldCamera;
            return RectTransformUtility.WorldToScreenPoint(camera,
                card.TransformPoint(new Vector3(enemy ? card.rect.xMax : card.rect.xMin, 0f, 0f))).x;
        }

        private static LegacySkill Skill(int id, int duration = 2)
            => new LegacySkill(id, "시험", 0, 1, 1, LegacySkillKind.Attack, LegacySkillProperty.Slash,
                1, 0, "", durationHalfUnits: duration);

        private static LegacyQueuedDuel Duel(LegacySkill[] player = null, LegacySkill[] enemy = null)
        {
            player = player ?? new[] { Skill(99721), Skill(99722), Skill(99723) };
            enemy = enemy ?? new[] { Skill(99731), Skill(99732), Skill(99733) };
            var duel = new LegacyQueuedDuel(1000, 1000, 1000, 1000, player, enemy, new[] { enemy.Length }, meshPercent: 0);
            Assert.That(duel.TrySetFixedQueues(player, enemy), Is.True);
            return duel;
        }

        private sealed class Scope : IDisposable
        {
            private readonly GameObject parent = new GameObject("Queue Backdrop Test");
            public LegacyCombatHud Hud { get; }
            public Camera Camera { get; }
            public Scope()
            {
                var camera = new GameObject("Queue Backdrop Test Camera");
                camera.transform.SetParent(parent.transform, false);
                camera.transform.position = new Vector3(0f, 0f, -10f);
                Camera = camera.AddComponent<Camera>();
                Camera.orthographic = true;
                Camera.orthographicSize = 4f;
                Camera.cullingMask = 1 << 30;
                Hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), null, null, null);
                Canvas.ForceUpdateCanvases();
            }
            public void Refresh(LegacyQueuedDuel duel, bool resolving, bool withCamera = true)
            {
                Hud.Refresh(duel, 10f, resolving, 0, withCamera ? Camera : null, null, null, 0f, 0f);
                Canvas.ForceUpdateCanvases();
            }
            public void Dispose() { Hud.Dispose(); Object.Destroy(parent); }
        }
    }
}
