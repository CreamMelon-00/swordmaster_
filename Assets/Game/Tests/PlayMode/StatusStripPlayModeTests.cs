using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class StatusStripPlayModeTests
    {
        [UnityTest]
        public IEnumerator RepeatedStacks_CombineOnlyMatchingKindAndCount_WithoutChangingTheRuntimeSnapshots()
        {
            yield return null;
            var host = new GameObject("Grouped status strip test", typeof(RectTransform), typeof(Canvas));
            try
            {
                host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                LegacyStatusStrip strip = LegacyStatusStrip.Create((RectTransform)host.transform,
                    new LegacyDuelArt().UIFont, "Grouped statuses");
                var source = new List<LegacyStatusSnapshot>();
                for (int i = 0; i < 20; i++) source.Add(new LegacyStatusSnapshot(LegacyStatusKind.Protection, 25, 1));
                source.Add(new LegacyStatusSnapshot(LegacyStatusKind.PowerUp, 20, 1));
                source.Add(new LegacyStatusSnapshot(LegacyStatusKind.PowerUp, 30, 1));
                source.Add(new LegacyStatusSnapshot(LegacyStatusKind.PowerUp, 20, 2));
                IReadOnlyList<LegacyStatusSnapshot> immutable = source.AsReadOnly();
                strip.Refresh(immutable);
                Assert.That(strip.Count, Is.EqualTo(3), "Repeated protection stacks use one icon rather than widening the strip.");
                Assert.That(strip.GetStatus(0).Kind, Is.EqualTo(LegacyStatusKind.Protection));
                Assert.That(strip.GetStatus(0).Percent, Is.EqualTo(500));
                Assert.That(strip.GetStatus(0).RemainingCount, Is.EqualTo(1));
                Assert.That(strip.GetStatus(1).Percent, Is.EqualTo(50));
                Assert.That(strip.GetStatus(1).RemainingCount, Is.EqualTo(1));
                Assert.That(strip.GetStatus(2).Percent, Is.EqualTo(20));
                Assert.That(strip.GetStatus(2).RemainingCount, Is.EqualTo(2), "Different remaining counts stay separate.");
                strip.Refresh(immutable);
                Assert.That(strip.GetStatus(0).Percent, Is.EqualTo(500), "Refresh rebuilds display totals instead of accumulating them.");
                Assert.That(source.Count, Is.EqualTo(23));
                for (int i = 0; i < 20; i++)
                {
                    Assert.That(source[i].Kind, Is.EqualTo(LegacyStatusKind.Protection));
                    Assert.That(source[i].Percent, Is.EqualTo(25));
                    Assert.That(source[i].RemainingCount, Is.EqualTo(1));
                }
                Assert.That(source[20].Percent, Is.EqualTo(20));
                Assert.That(source[21].Percent, Is.EqualTo(30));
                Assert.That(source[22].RemainingCount, Is.EqualTo(2));
            }
            finally { Object.Destroy(host); }
        }

        [UnityTest]
        public IEnumerator Icons_ShowActualCountsAndSharedDefinitions_AndRemoveAnExpiredHoveredStatus()
        {
            yield return null;
            var host = new GameObject("Status strip test", typeof(RectTransform), typeof(Canvas));
            try
            {
                Canvas canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                RectTransform parent = (RectTransform)host.transform;
                LegacyStatusStrip strip = LegacyStatusStrip.Create(parent, new LegacyDuelArt().UIFont, "Test statuses");
                strip.Refresh(new[] {
                    new LegacyStatusSnapshot(LegacyStatusKind.PowerUp, 20, 2),
                    new LegacyStatusSnapshot(LegacyStatusKind.Protection, 25, 1),
                    new LegacyStatusSnapshot(LegacyStatusKind.Vulnerability, 50, 3)
                });
                Canvas.ForceUpdateCanvases();
                Assert.That(strip.Count, Is.EqualTo(3));
                for (int i = 0; i < strip.Count; i++)
                {
                    RectTransform icon = strip.GetIconRoot(i);
                    Assert.That(icon.Find("Status Remaining").GetComponent<Text>().text,
                        Is.EqualTo(strip.GetStatus(i).RemainingCount.ToString()));
                    Assert.That(icon.Find("Status Symbol").GetComponent<LegacyStatusIconGraphic>(), Is.Not.Null);
                    Assert.That(icon.GetComponent<Image>().raycastTarget, Is.True, "Only an icon catches its hover.");
                }
                strip.GetIconRoot(2).GetComponent<LegacyStatusHoverTarget>().OnPointerEnter(new PointerEventData(null));
                Assert.That(strip.TooltipRoot.gameObject.activeSelf, Is.True);
                Assert.That(strip.TooltipRoot.Find("Status Name").GetComponent<Text>().text, Does.Contain("취약").And.Contain("3회"));
                Assert.That(strip.TooltipRoot.Find("Status Definition").GetComponent<Text>().text,
                    Is.EqualTo(LegacyStatusNames.Description(LegacyStatusKind.Vulnerability, 50)));
                int nodes = host.GetComponentsInChildren<Transform>(true).Length;
                strip.Refresh(new[] { new LegacyStatusSnapshot(LegacyStatusKind.Vulnerability, 50, 1) });
                Assert.That(strip.TooltipRoot.gameObject.activeSelf, Is.False, "An expired/replaced hovered icon cannot leave a stale explanation.");
                strip.ShowTooltip(0);
                Assert.That(strip.TooltipRoot.Find("Status Name").GetComponent<Text>().text, Does.Contain("1회"));
                strip.Refresh(Array.Empty<LegacyStatusSnapshot>());
                Assert.That(strip.Root.gameObject.activeSelf, Is.False);
                Assert.That(strip.TooltipRoot.gameObject.activeSelf, Is.False);
                Assert.That(host.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(nodes), "Icon nodes are reused.");
            }
            finally { Object.Destroy(host); }
        }

        [UnityTest]
        public IEnumerator HoveredStatus_RefreshesItsValues_AndClosesWhenAnotherKindReusesTheIcon()
        {
            yield return null;
            var host = new GameObject("Status hover refresh test", typeof(RectTransform), typeof(Canvas));
            try
            {
                host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                LegacyStatusStrip strip = LegacyStatusStrip.Create((RectTransform)host.transform,
                    new LegacyDuelArt().UIFont, "Hovered statuses");
                strip.Refresh(new[] {
                    new LegacyStatusSnapshot(LegacyStatusKind.Protection, 25, 2),
                    new LegacyStatusSnapshot(LegacyStatusKind.Vulnerability, 50, 3)
                });
                strip.ShowTooltip(0);
                strip.Refresh(new[] {
                    new LegacyStatusSnapshot(LegacyStatusKind.Protection, 50, 1),
                    new LegacyStatusSnapshot(LegacyStatusKind.Vulnerability, 50, 3)
                });
                Assert.That(strip.TooltipRoot.gameObject.activeSelf, Is.True,
                    "The same status keeps its explanation open while its magnitude and remaining count update.");
                Assert.That(strip.TooltipRoot.Find("Status Name").GetComponent<Text>().text,
                    Does.Contain("보호").And.Contain("1회"));
                Assert.That(strip.TooltipRoot.Find("Status Definition").GetComponent<Text>().text,
                    Is.EqualTo(LegacyStatusNames.Description(LegacyStatusKind.Protection, 50)));
                strip.Refresh(new[] { new LegacyStatusSnapshot(LegacyStatusKind.Vulnerability, 50, 3) });
                Assert.That(strip.TooltipRoot.gameObject.activeSelf, Is.False,
                    "Reusing the hovered icon for a different status cannot silently change its explanation.");
                strip.ShowTooltip(0);
                Assert.That(strip.TooltipRoot.gameObject.activeSelf, Is.True);
                Assert.That(strip.TooltipRoot.Find("Status Name").GetComponent<Text>().text, Does.Contain("취약"));
            }
            finally { Object.Destroy(host); }
        }

        [UnityTest]
        public IEnumerator LiveIcons_RefreshOnTheGrantAndConsumptionFrame_AndDoNotTreatBreathingAsAUse()
        {
            yield return null;
            using (var scope = new Scope(false))
            {
                LegacySkill ready = Skill(10, LegacySkillKind.Attack);
                LegacySkill guard = Skill(88000, LegacySkillKind.Defence);
                var duel = new LegacyQueuedDuel(1000, 1000, 1000, 1000,
                    new[] { ready, guard }, new[] { guard, guard, guard }, new[] { 3 }, meshPercent: 0);
                Assert.That(duel.TryQueueLane(0) && duel.TryQueueBreath() && duel.TryQueueLane(0), Is.True);
                duel.Commit();
                duel.BeginNextSlot();
                scope.Refresh(duel);
                LegacyStatusStrip statuses = scope.Hud.GetStatusStrip(true);
                Assert.That(statuses.Count, Is.EqualTo(1));
                Assert.That(statuses.GetStatus(0).Percent, Is.EqualTo(50), "준비 grants the sheet's 50% power increase.");
                Assert.That(statuses.GetStatus(0).RemainingCount, Is.EqualTo(1));
                duel.ResolveNextSlot(); duel.BeginNextSlot(); scope.Refresh(duel);
                Assert.That(statuses.Count, Is.EqualTo(1), "Breathing leaves the unspent technique count visible.");
                duel.ResolveNextSlot(); duel.BeginNextSlot(); scope.Refresh(duel);
                Assert.That(statuses.Count, Is.Zero, "The following actual technique spends the count at its start.");
                Assert.That(statuses.Root.gameObject.activeSelf, Is.False);
                scope.Hud.Reset();
                Assert.That(scope.Hud.GetStatusStrip(false).Count, Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator Breakthrough_RemovesTheHoveredProtectionOnItsStartFrame_WhileVulnerabilityStaysVisible()
        {
            yield return null;
            using (var scope = new Scope(false))
            {
                LegacySkill guard = Skill(88000, LegacySkillKind.Defence);
                var duel = new LegacyQueuedDuel(1000, 1000, 1000, 1000,
                    new[] { Skill(8, LegacySkillKind.Defence), Skill(12, LegacySkillKind.Attack), Skill(60, LegacySkillKind.Attack) },
                    new[] { guard, guard, guard }, new[] { 3 }, meshPercent: 0);
                Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(0) && duel.TryQueueLane(0), Is.True);
                duel.Commit();
                duel.ResolveNextSlot();
                duel.BeginNextSlot();
                scope.Refresh(duel);
                LegacyStatusStrip statuses = scope.Hud.GetStatusStrip(true);
                Assert.That(statuses.Count, Is.EqualTo(2));
                Assert.That(statuses.GetStatus(0).Kind, Is.EqualTo(LegacyStatusKind.Protection));
                Assert.That(statuses.GetStatus(1).Kind, Is.EqualTo(LegacyStatusKind.Vulnerability));
                statuses.ShowTooltip(0);
                Assert.That(statuses.TooltipRoot.gameObject.activeSelf, Is.True);
                duel.ResolveNextSlot();
                duel.BeginNextSlot();
                scope.Refresh(duel);
                Assert.That(statuses.Count, Is.EqualTo(1));
                Assert.That(statuses.GetStatus(0).Kind, Is.EqualTo(LegacyStatusKind.Vulnerability));
                Assert.That(statuses.TooltipRoot.gameObject.activeSelf, Is.False);
                Assert.That(duel.CurrentSlot.PlayerFeedback.ProtectionSpendPowerBonus, Is.EqualTo(10));
                Assert.That(scope.Hud.GetKeywordMarkLabel(true, 2), Is.EqualTo("보호 +10"));
            }
        }

        [UnityTest]
        public IEnumerator EveryLivingOpponent_HasAnIndependentStripBelowItsActualFeetAndAboveTheQueue()
        {
            yield return null;
            using (var scope = new Scope(true))
            {
                LegacySkill advance = Skill(12, LegacySkillKind.Attack);
                LegacySkill flow = Skill(8, LegacySkillKind.Defence);
                var script = new EnemyScript(new[] { new[] { flow } });
                var duel = new LegacyQueuedDuel(1000, 1000, new[] { advance }, new[] {
                    new LegacyEnemyDefinition("첫 상대", 1000, 1000, script),
                    new LegacyEnemyDefinition("둘째 상대", 1000, 1000, script),
                    new LegacyEnemyDefinition("셋째 상대", 1000, 1000, script)
                }, meshPercent: 0);
                Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(0) && duel.TryQueueLane(0), Is.True);
                duel.Commit();
                while (!duel.IsTurnResolved) duel.ResolveNextSlot();
                scope.Refresh(duel);
                for (int index = 0; index < 3; index++)
                {
                    LegacyStatusStrip strip = scope.Hud.GetStatusStrip(false, index);
                    Assert.That(strip.Count, Is.EqualTo(1));
                    Assert.That(strip.GetStatus(0).Kind, Is.EqualTo(LegacyStatusKind.Protection));
                    Assert.That(strip.GetStatus(0).Percent, Is.EqualTo(50), "흘리기 grants the sheet's 50% protection.");
                    Assert.That(strip.Root.gameObject.activeInHierarchy, Is.True);
                    AssertBelowFeet(scope, strip, scope.Arena.GetEnemyGroundPosition(index));
                    for (int previous = 0; previous < index; previous++)
                        Assert.That(LocalRect(strip.Root).Overlaps(LocalRect(scope.Hud.GetStatusStrip(false, previous).Root)), Is.False);
                }
                AssertBelowFeet(scope, scope.Hud.GetStatusStrip(true), scope.Arena.PlayerGroundPosition);
                Assert.That(scope.Hud.GetStatusStrip(true).GetStatus(0).Kind, Is.EqualTo(LegacyStatusKind.Vulnerability));
                Assert.That(scope.Hud.GetStatusStrip(true).GetStatus(0).RemainingCount, Is.EqualTo(3));
            }
        }

        private static void AssertBelowFeet(Scope scope, LegacyStatusStrip strip, Vector3 feet)
        {
            RectTransform root = (RectTransform)scope.Hud.Root.transform;
            Vector3 screen = scope.Arena.ArenaCamera.WorldToScreenPoint(feet);
            Assert.That(RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 anchor), Is.True);
            Rect bounds = LocalRect(strip.Root);
            Assert.That(bounds.yMax, Is.LessThan(anchor.y), "The status strip follows the physical ground anchor, below the floor circle.");
            Assert.That(Mathf.Abs(bounds.center.x - anchor.x), Is.LessThan(160f), "Collision avoidance keeps the icon near its own fighter.");
            Assert.That(bounds.yMin + root.rect.height * .5f, Is.GreaterThan(scope.Hud.QueuePanelTop));
        }

        private static Rect LocalRect(RectTransform rect) => new Rect(rect.anchoredPosition - rect.sizeDelta * .5f, rect.sizeDelta);
        private static LegacySkill Skill(int id, LegacySkillKind kind)
            => new LegacySkill(id, "상태 검증", 0, 1, 1, kind,
                kind == LegacySkillKind.Attack ? LegacySkillProperty.Slash : LegacySkillProperty.Defence, 1, 0, string.Empty, iconId: 1);

        private sealed class Scope : IDisposable
        {
            private readonly GameObject host = new GameObject("Live status strip test");
            private readonly DuelPrototypeController scene;
            private readonly bool sceneEnabled;
            public readonly LegacyArenaView Arena;
            public readonly LegacyCombatHud Hud;
            public Scope(bool trio)
            {
                scene = Object.FindAnyObjectByType<DuelPrototypeController>();
                if (scene != null) { sceneEnabled = scene.enabled; scene.enabled = false; }
                var art = new LegacyDuelArt();
                Arena = LegacyArenaView.Create(host.transform, art);
                Arena.Reset();
                if (trio) Arena.ConfigureEnemyRoster(new[] { EnemyAppearance.CadetA, EnemyAppearance.CadetB, EnemyAppearance.Student });
                Hud = new LegacyCombatHud(host.transform, art, null, null, null);
                Hud.SetStatusArena(Arena);
                Hud.SetEnemyActors(Arena.EnemyActorTransforms);
                Canvas.ForceUpdateCanvases();
            }
            public void Refresh(ILegacyDuelView duel)
            {
                for (int frame = 0; frame < 16; frame++)
                {
                    Arena.SetHudFloor(Hud.QueuePanelFloor);
                    Arena.Tick(0f, .02f);
                    Hud.Refresh(duel, 10f, duel.Phase == LegacyDuelPhase.Resolving, duel.CurrentSlot?.SlotIndex ?? -1,
                        Arena.ArenaCamera, Arena.PlayerRenderer.transform, Arena.EnemyRenderer.transform, 0f, 0f);
                    Canvas.ForceUpdateCanvases();
                }
            }
            public void Dispose()
            {
                Hud.Dispose(); Arena.Dispose(); Object.Destroy(host);
                if (scene != null) scene.enabled = sceneEnabled;
            }
        }
    }
}
