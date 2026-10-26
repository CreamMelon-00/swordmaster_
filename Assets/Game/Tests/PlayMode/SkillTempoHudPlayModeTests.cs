using System;
using System.Collections;
using NUnit.Framework;
using TurnLimbo.Presentation;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class SkillTempoHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator TimelineWidths_AlignFastPairWithNormal_AndChargeWithTwoNormalCards()
        {
            yield return null;
            using (var scope = new Scope())
            {
                var duel = Duel(new[] { Skill(901, 1), Skill(902, 1), Skill(903, 2), Skill(904, 4) },
                    new[] { Skill(911, 2), Skill(912, 2), Skill(913, 2), Skill(914, 2) });
                scope.Refresh(duel);
                var fast = scope.Card(true, 0);
                var second = scope.Card(true, 1);
                var normal = scope.Card(true, 2);
                var charge = scope.Card(true, 3);
                Assert.That(fast.rect.width, Is.EqualTo(normal.rect.width * .5f).Within(.001f));
                Assert.That(charge.rect.width, Is.EqualTo(normal.rect.width * 2f).Within(.001f));
                Assert.That(fast.rect.height, Is.EqualTo(normal.rect.height));
                Assert.That(charge.rect.height, Is.EqualTo(normal.rect.height));
                Assert.That(Outer(second), Is.EqualTo(-Outer(scope.Card(false, 0))).Within(.01f),
                    "The second fast completion meets the opposing normal completion.");
                Assert.That(Outer(charge), Is.EqualTo(-Outer(scope.Card(false, 3))).Within(.01f));
                Assert.That(duel.Player.Health, Is.EqualTo(1000), "Layout never resolves the battle.");
            }
        }

        [UnityTest]
        public IEnumerator FastThenNormal_ShowsHalfACardOfEmptyTime_AndPooledCardsRegainSquareShape()
        {
            yield return null;
            using (var scope = new Scope())
            {
                var duel = Duel(new[] { Skill(901, 1), Skill(902, 2) }, new[] { Skill(911, 2), Skill(912, 2) });
                scope.Refresh(duel);
                var fast = scope.Card(true, 0);
                var normal = scope.Card(true, 1);
                float scale = normal.localScale.x;
                float gap = Mathf.Abs(normal.anchoredPosition.x - fast.anchoredPosition.x) -
                    (normal.rect.width + fast.rect.width) * scale * .5f;
                Assert.That(gap, Is.EqualTo(normal.rect.width * scale * .5f).Within(.01f));
                var ordinary = Duel(new[] { Skill(903, 2), Skill(904, 2) }, new[] { Skill(913, 2) });
                scope.Refresh(ordinary);
                Assert.That(scope.Card(true, 0).rect.width, Is.EqualTo(scope.Card(true, 0).rect.height));
                Assert.That(scope.Card(true, 1).rect.width, Is.EqualTo(scope.Card(true, 1).rect.height));
            }
        }

        [UnityTest]
        public IEnumerator Resolving_NormalCardStaysVisibleAcrossBothFastActions_ThenDisappears()
        {
            yield return null;
            using (var scope = new Scope())
            {
                var duel = Duel(new[] { Skill(901, 1), Skill(902, 1), Skill(903, 2) },
                    new[] { Skill(911, 2), Skill(912, 2) });
                scope.Refresh(duel);
                duel.Commit();
                var first = duel.BeginNextSlot();
                scope.Refresh(duel, true, first.SlotIndex);
                Assert.That(scope.Card(false, 0), Is.Not.Null);
                while (!first.IsResolved) duel.ResolveNextHit();
                duel.CompleteCurrentSlot();
                scope.Refresh(duel, true, first.SlotIndex + 1);
                Assert.That(scope.Hud.GetQueuedSkillAnchor(true, 0), Is.Null,
                    "Consumed cards stay hidden while the next action is approaching and CurrentSlot is null.");
                Assert.That(scope.Card(false, 0), Is.Not.Null);
                var second = duel.BeginNextSlot();
                scope.Refresh(duel, true, second.SlotIndex);
                Assert.That(scope.Hud.GetQueuedSkillAnchor(true, 0), Is.Null);
                Assert.That(scope.Card(true, 1), Is.Not.Null);
                Assert.That(scope.Card(false, 0), Is.Not.Null, "The same normal action still occupies the timeline.");
                while (!second.IsResolved) duel.ResolveNextHit();
                duel.CompleteCurrentSlot();
                var third = duel.BeginNextSlot();
                scope.Refresh(duel, true, third.SlotIndex);
                Assert.That(scope.Hud.GetQueuedSkillAnchor(false, 0), Is.Null);
                Assert.That(scope.Card(false, 1), Is.Not.Null);
            }
        }

        [UnityTest]
        public IEnumerator CompletedFastPairsSlideTheShortTailForward_ByHalfACardAtATime()
        {
            yield return null;
            using (var scope = new Scope())
            {
                var duel = Duel(new[] { Skill(921, 1), Skill(922, 1), Skill(923, 2) },
                    new[] { Skill(931, 1), Skill(932, 1), Skill(933, 2) });
                scope.Refresh(duel);
                duel.Commit();
                var first = duel.BeginNextSlot();
                scope.Refresh(duel, true, first.SlotIndex);
                float inner = Near(scope.Card(true, 0));
                float before = scope.Card(true, 1).anchoredPosition.x;
                Finish(duel);
                scope.Refresh(duel, true, first.SlotIndex + 1);
                Assert.That(scope.Hud.GetQueuedSkillAnchor(true, 0), Is.Null);
                Assert.That(scope.Hud.QueueSlide, Is.EqualTo(.5f).Within(.001f));
                Assert.That(scope.Card(true, 1).anchoredPosition.x, Is.EqualTo(before).Within(.01f),
                    "The next icon starts where it stood instead of jumping to the seam.");
                scope.Refresh(duel, true, first.SlotIndex + 1);
                Assert.That(scope.Hud.QueueSlide, Is.EqualTo(.5f).Within(.001f), "A stopped battle holds the move.");
                scope.Refresh(duel, true, first.SlotIndex + 1, LegacyQueuePanelLayout.DefaultSlideSeconds * .5f);
                Assert.That(Near(scope.Card(true, 1)), Is.GreaterThan(inner));
                Assert.That(scope.Card(true, 1).anchoredPosition.x, Is.GreaterThan(before));
                scope.Refresh(duel, true, first.SlotIndex + 1, LegacyQueuePanelLayout.DefaultSlideSeconds);
                Assert.That(scope.Hud.QueueSlide, Is.Zero);
                Assert.That(Near(scope.Card(true, 1)), Is.EqualTo(inner).Within(.01f),
                    "A short queue must still bring its next skill to the use position.");
                Assert.That(scope.Card(true, 1).anchoredPosition.x,
                    Is.EqualTo(-scope.Card(false, 1).anchoredPosition.x).Within(.01f));
                scope.Refresh(duel);
                Assert.That(scope.Hud.QueueSlide, Is.Zero);
                Assert.That(scope.Hud.GetQueuedSkillAnchor(true, 0), Is.Not.Null, "Planning restores the full queue.");
            }
        }

        [UnityTest]
        public IEnumerator UnmatchedFastPaddingAndTheOpposingNormalStayAligned_UntilThatSharedTimeCompletes()
        {
            yield return null;
            using (var scope = new Scope())
            {
                var duel = Duel(new[] { Skill(941, 1), Skill(942, 2) },
                    new[] { Skill(951, 2), Skill(952, 2) });
                scope.Refresh(duel);
                duel.Commit();
                var first = duel.BeginNextSlot();
                scope.Refresh(duel, true, first.SlotIndex);
                float inner = Near(scope.Card(false, 0));
                float playerNext = scope.Card(true, 1).anchoredPosition.x;
                float enemyCurrent = scope.Card(false, 0).anchoredPosition.x;
                float width = scope.Card(false, 0).rect.width;
                Finish(duel);
                scope.Refresh(duel, true, first.SlotIndex + 1);
                Assert.That(scope.Hud.GetQueuedSkillAnchor(true, 0), Is.Null);
                Assert.That(scope.Card(true, 1).anchoredPosition.x, Is.EqualTo(playerNext).Within(.01f));
                Assert.That(scope.Card(false, 0).anchoredPosition.x, Is.EqualTo(enemyCurrent).Within(.01f));
                Assert.That(scope.Card(false, 0).rect.width, Is.EqualTo(width),
                    "The normal skill remains whole while the fast action's unmatched half-time passes.");
                var second = duel.BeginNextSlot();
                scope.Refresh(duel, true, second.SlotIndex);
                Finish(duel);
                scope.Refresh(duel, true, second.SlotIndex + 1);
                Assert.That(scope.Hud.QueueSlide, Is.EqualTo(1f).Within(.001f));
                scope.Refresh(duel, true, second.SlotIndex + 1, LegacyQueuePanelLayout.DefaultSlideSeconds);
                Assert.That(Near(scope.Card(true, 1)), Is.EqualTo(inner).Within(.01f));
                Assert.That(Near(scope.Card(false, 1)), Is.EqualTo(inner).Within(.01f));
            }
        }

        [UnityTest]
        public IEnumerator AWholeChargeStaysInPlaceWhilePreparing_ThenTheNextSkillsAdvanceTogether()
        {
            yield return null;
            using (var scope = new Scope())
            {
                var duel = Duel(new[] { Skill(961, 4), Skill(962, 2) },
                    new[] { Skill(971, 2), Skill(972, 2), Skill(973, 2) });
                scope.Refresh(duel);
                duel.Commit();
                var first = duel.BeginNextSlot();
                scope.Refresh(duel, true, first.SlotIndex);
                RectTransform charge = scope.Card(true, 0);
                float inner = Near(charge), chargePosition = charge.anchoredPosition.x, width = charge.rect.width;
                Finish(duel);
                scope.Refresh(duel, true, first.SlotIndex + 1);
                Assert.That(scope.Card(true, 0), Is.SameAs(charge));
                Assert.That(charge.rect.width, Is.EqualTo(width));
                Assert.That(charge.anchoredPosition.x, Is.EqualTo(chargePosition).Within(.01f));
                Assert.That(scope.Hud.QueueSlide, Is.Zero, "A long skill is retained as one complete card while winding up.");
                var second = duel.BeginNextSlot();
                scope.Refresh(duel, true, second.SlotIndex);
                Finish(duel);
                scope.Refresh(duel, true, second.SlotIndex + 1);
                Assert.That(scope.Hud.GetQueuedSkillAnchor(true, 0), Is.Null);
                Assert.That(scope.Hud.QueueSlide, Is.EqualTo(2f).Within(.001f));
                scope.Refresh(duel, true, second.SlotIndex + 1, LegacyQueuePanelLayout.DefaultSlideSeconds);
                Assert.That(Near(scope.Card(true, 1)), Is.EqualTo(inner).Within(.01f));
                Assert.That(Near(scope.Card(false, 2)), Is.EqualTo(inner).Within(.01f));
                Assert.That(scope.Card(true, 1).anchoredPosition.x,
                    Is.EqualTo(-scope.Card(false, 2).anchoredPosition.x).Within(.01f));
            }
        }

        [UnityTest]
        public IEnumerator TheNextWideChargeRemainsVisibleThroughoutTheEnlargedQueueSlide()
        {
            yield return null;
            foreach (int duration in new[] { 4, 8 })
                using (var scope = new Scope(duration == 4 ? 80f : 48f, backdrop: true))
                {
                    var opponents = new LegacySkill[duration];
                    for (int i = 0; i < opponents.Length; i++) opponents[i] = Skill(991, 2);
                    var duel = Duel(new[] { Skill(981, duration), Skill(982, duration) }, opponents);
                    scope.Refresh(duel);
                    duel.Commit();
                    int nextSlot = 0;
                    for (int segment = 0; segment < duration / 2; segment++)
                    {
                        var slot = duel.BeginNextSlot();
                        scope.Refresh(duel, true, slot.SlotIndex);
                        nextSlot = slot.SlotIndex + 1;
                        Finish(duel);
                        scope.Refresh(duel, true, nextSlot);
                    }
                    Assert.That(scope.Hud.QueueIsBackdrop, Is.True);
                    Assert.That(scope.Hud.GetQueuedSkillAnchor(true, 0), Is.Null);
                    Assert.That(scope.Hud.QueueSlide, Is.EqualTo(2f).Within(.001f),
                        "Fitting the displayed move does not change the original logical slide clock.");
                    for (int frame = 0; frame < 8; frame++)
                    {
                        RectTransform next = scope.Card(true, 1);
                        Assert.That(next.rect.width, Is.EqualTo(next.rect.height * duration * .5f).Within(.01f),
                            "Keep the next charge whole instead of hiding it until the slide finishes.");
                        scope.Refresh(duel, true, nextSlot, LegacyQueuePanelLayout.DefaultSlideSeconds / 7f);
                    }
                    Assert.That(scope.Hud.QueueSlide, Is.Zero);
                }
        }

        private static float Near(RectTransform card)
            => Mathf.Abs(card.anchoredPosition.x) - card.rect.width * card.localScale.x * .5f;

        private static void Finish(LegacyQueuedDuel duel)
        {
            while (!duel.CurrentSlot.IsResolved) duel.ResolveNextHit();
            duel.CompleteCurrentSlot();
        }

        private static float Outer(RectTransform card)
            => card.anchoredPosition.x + Mathf.Sign(card.anchoredPosition.x) * card.rect.width * card.localScale.x * .5f;

        private static LegacySkill Skill(int id, int duration)
            => new LegacySkill(id, "시험", 0, 1, 1, LegacySkillKind.Attack, LegacySkillProperty.Slash,
                1, 0, "", durationHalfUnits: duration);

        private static LegacyQueuedDuel Duel(LegacySkill[] player, LegacySkill[] enemy)
        {
            var duel = new LegacyQueuedDuel(1000, 1000, 1000, 1000, player, enemy, new[] { enemy.Length }, meshPercent: 0);
            Assert.That(duel.TrySetFixedQueues(player, enemy), Is.True);
            return duel;
        }

        private sealed class Scope : IDisposable
        {
            private readonly GameObject parent = new GameObject("Skill Tempo HUD Test");
            private readonly DuelPresentationSettings settings;
            private readonly Camera camera;
            public LegacyCombatHud Hud { get; }
            public Scope(float cardSize = 0f, bool backdrop = false)
            {
                if (cardSize > 0f)
                {
                    settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                    JsonUtility.FromJsonOverwrite("{\"queueCardSize\":" + cardSize.ToString(
                        System.Globalization.CultureInfo.InvariantCulture) + "}", settings);
                }
                if (backdrop)
                {
                    var cameraObject = new GameObject("Skill Tempo Backdrop Camera");
                    cameraObject.transform.SetParent(parent.transform, false);
                    cameraObject.transform.position = new Vector3(0f, 0f, -10f);
                    camera = cameraObject.AddComponent<Camera>();
                    camera.orthographic = true;
                    camera.orthographicSize = 4f;
                    camera.cullingMask = 1 << 30;
                }
                Hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), null, null, null, settings);
                Canvas.ForceUpdateCanvases();
            }
            public void Refresh(LegacyQueuedDuel duel, bool resolving = false, int slot = 0, float delta = 0f)
            {
                Hud.Refresh(duel, 10f, resolving, slot, camera, null, null, delta, delta);
                Canvas.ForceUpdateCanvases();
            }
            public RectTransform Card(bool player, int index)
            {
                var card = Hud.GetQueuedSkillAnchor(player, index);
                Assert.That(card, Is.Not.Null);
                return card;
            }
            public void Dispose() { Hud.Dispose(); Object.Destroy(parent); if (settings != null) Object.Destroy(settings); }
        }
    }
}
