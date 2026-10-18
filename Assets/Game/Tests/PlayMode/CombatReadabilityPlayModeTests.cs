using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class CombatReadabilityPlayModeTests
    {
        [UnityTest]
        public IEnumerator CompletedFinalHitRemainsReadableWithCardMotionOff_UntilNextPlanning()
        {
            yield return null;
            using (var fixture = new HudFixture("{\"clashMotionEnabled\":false}"))
            {
                LegacyCombatHud hud = fixture.Hud;
                LegacyQueuedDuel duel = Duel(Attack(990, "가로베기", 12), Attack(991, "내려베기", 8), 1, 5);
                fixture.Refresh(duel, false, -1);
                Assert.That(hud.CombatReadout.gameObject.activeSelf, Is.False);
                Assert.That(duel.TryQueueLane(0), Is.True);
                duel.Commit();
                duel.BeginNextSlot();
                fixture.Refresh(duel, true, 0);
                Assert.That(hud.CombatReadoutPairText, Is.EqualTo("내 가로베기  ↔  상대 내려베기"));
                Assert.That(hud.CombatReadoutText, Is.EqualTo("맞붙는 중"));
                duel.ResolveNextHit();
                hud.ObserveClash(duel);
                duel.CompleteCurrentSlot();
                fixture.Refresh(duel, true, 0);
                Assert.That(duel.CurrentSlot, Is.Null);
                Assert.That(hud.ShownClash.HasValue, Is.False, "Readability is independent of disabled card motion.");
                Assert.That(hud.CombatReadoutText, Is.EqualTo("상대 저항 붕괴·체력 손실 · 내 저항 손실"));
                Assert.That(hud.CombatReadoutSlot, Is.Zero);
                Assert.That(hud.CombatReadout.gameObject.activeSelf, Is.True);
                duel.BeginNextTurn();
                fixture.Refresh(duel, false, -1);
                Assert.That(hud.CombatReadout.gameObject.activeSelf, Is.False);
                Assert.That(hud.CombatReadoutText, Is.Empty);
                Assert.That(hud.CombatReadoutSlot, Is.EqualTo(-1));
            }
        }

        [UnityTest]
        public IEnumerator VolleyUpdatesAtTheBreakingHit_ThenHealthLoss_AndTheNextSlotReplacesIt()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                LegacyCombatHud hud = fixture.Hud;
                LegacyQueuedDuel duel = Duel(Attack(990, "연격", 18, 3), Attack(991, "약공", 1), 2, 12);
                Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(0), Is.True);
                duel.Commit();
                duel.BeginNextSlot();
                fixture.Refresh(duel, true, 0);
                duel.ResolveNextHit();
                hud.ObserveClash(duel);
                Assert.That(hud.CombatReadoutText, Is.EqualTo("서로 저항 손실"));
                duel.ResolveNextHit();
                hud.ObserveClash(duel);
                Assert.That(hud.CombatReadoutText, Is.EqualTo("상대 저항 붕괴 · 내 저항 손실"));
                duel.ResolveNextHit();
                hud.ObserveClash(duel);
                duel.CompleteCurrentSlot();
                fixture.Refresh(duel, true, 0);
                Assert.That(hud.CombatReadoutText, Is.EqualTo("상대 저항 붕괴·체력 손실 · 내 저항 손실"));
                duel.BeginNextSlot();
                fixture.Refresh(duel, true, 1);
                Assert.That(hud.CombatReadoutSlot, Is.EqualTo(1));
                Assert.That(hud.CombatReadoutText, Is.EqualTo("맞붙는 중"));
                hud.Reset();
                Assert.That(hud.CombatReadout.gameObject.activeSelf, Is.False);
                Assert.That(hud.CombatReadoutText, Is.Empty);
            }
        }

        [UnityTest]
        public IEnumerator VersusNamesAndGuardResultUseEachViewersPerspective()
        {
            yield return null;
            using (var host = new HudFixture())
            using (var guest = new HudFixture())
            {
                var match = new LocalVersusMatch(new[] { Guard(990, 4) }, new[] { Attack(991, "찌르기", 10) });
                ILegacyDuelView hostView = new LocalVersusPerspective(match, 0), guestView = new LocalVersusPerspective(match, 1);
                Assert.That(match.TryQueueLane(0, 0) && match.TryCommit(0) && match.TryQueueLane(1, 0), Is.True);
                Assert.That(match.TryCommit(1), Is.True);
                match.BeginNextSlot();
                host.Refresh(hostView, true, 0);
                guest.Refresh(guestView, true, 0);
                Assert.That(host.Hud.CombatReadoutPairText, Is.EqualTo("내 막기  ↔  상대 찌르기"));
                Assert.That(guest.Hud.CombatReadoutPairText, Is.EqualTo("내 찌르기  ↔  상대 막기"));
                match.ResolveNextHit();
                host.Hud.ObserveClash(hostView);
                guest.Hud.ObserveClash(guestView);
                Assert.That(host.Hud.CombatReadoutText, Is.EqualTo("내 방어 관통"));
                Assert.That(guest.Hud.CombatReadoutText, Is.EqualTo("상대 방어 관통"));
            }
        }

        [UnityTest]
        public IEnumerator ReadoutClearsQueueDecorationsAndCoach_TakesNoInput_AndReusesItsNodes()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                LegacyCombatHud hud = fixture.Hud;
                LegacyQueuedDuel duel = Duel(Attack(990, "베기", 12), Guard(991, 4), 1, 50);
                Assert.That(duel.TryQueueLane(0), Is.True);
                duel.Commit();
                duel.BeginNextSlot();
                Canvas canvas = hud.CombatReadout.GetComponentInParent<Canvas>();
                hud.SetQueueSeamReserve(220f);
                const float coachTop = 400f;
                hud.SetExplanationFloor(coachTop * canvas.scaleFactor);
                fixture.Refresh(duel, true, 0);
                RectTransform panel = hud.CombatReadout;
                float bottom = hud.CombatReadoutTop - panel.rect.height;
                Assert.That(bottom, Is.EqualTo(Mathf.Max(hud.QueueRowTop, coachTop) + 8f).Within(.01f));
                var corners = new Vector3[4];
                panel.GetWorldCorners(corners);
                Assert.That(RectTransformUtility.WorldToScreenPoint(null, corners[0]).y,
                    Is.GreaterThan(coachTop * canvas.scaleFactor));
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                Assert.That(panel.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                foreach (Graphic graphic in panel.GetComponentsInChildren<Graphic>(true))
                    Assert.That(graphic.raycastTarget, Is.False);
                int nodes = panel.GetComponentsInChildren<Transform>(true).Length;
                for (int frame = 0; frame < 30; frame++) fixture.Refresh(duel, true, 0);
                Assert.That(panel.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(nodes));
                Assert.That(hud.QueuePanelFloor,
                    Is.GreaterThan(hud.CombatReadoutTop / canvas.GetComponent<RectTransform>().rect.height));
            }
        }

        [UnityTest]
        public IEnumerator TrioCapturesTheFallenTarget_EvenWhenFirstObservedAfterTheHit_AndKeepsQueueOwnerNumbers()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                var script = new EnemyScript(new IReadOnlyList<LegacySkill>[] { new[] { Guard(991, 1), Guard(991, 1) } });
                var duel = new LegacyQueuedDuel(1000, 50, new[] { Attack(990, "베기", 12) }, new[]
                {
                    new LegacyEnemyDefinition("선봉", 1, 50, script),
                    new LegacyEnemyDefinition("검객", 1, 50, script),
                    new LegacyEnemyDefinition("기습꾼", 1, 50, script)
                }, randomSeed: 17, meshPercent: 0);
                Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(0), Is.True);
                duel.Commit();
                duel.BeginNextSlot();
                int fallen = duel.ActiveEnemyIndex;
                duel.ResolveNextHit();
                Assert.That(duel.EnemyIndexForSlot(0), Is.Not.EqualTo(fallen));
                fixture.Hud.ObserveClash(duel);
                string captured = "내 베기  ↔  상대 " + (fallen + 1) + "(" + duel.EnemyName(fallen) + ") 막기";
                Assert.That(fixture.Hud.CombatReadoutPairText, Is.EqualTo(captured));
                duel.CompleteCurrentSlot();
                fixture.Refresh(duel, true, 0);
                Assert.That(fixture.Hud.CombatReadoutPairText, Is.EqualTo(captured));
                RectTransform ownerMark = fixture.Hud.GetEnemyQueueOwnerMark(0);
                Assert.That(ownerMark, Is.Not.Null);
                Assert.That(ownerMark.GetComponentInChildren<Text>().text, Is.EqualTo((fallen + 1).ToString()));
                duel.BeginNextSlot();
                int next = duel.ActiveEnemyIndex;
                fixture.Refresh(duel, true, 1);
                Assert.That(next, Is.Not.EqualTo(fallen));
                Assert.That(fixture.Hud.CombatReadoutPairText,
                    Is.EqualTo("내 베기  ↔  상대 " + (next + 1) + "(" + duel.EnemyName(next) + ") 막기"));
                Assert.That(fixture.Hud.CombatReadoutText, Is.EqualTo("맞붙는 중"));
            }
        }

        private static LegacyQueuedDuel Duel(LegacySkill player, LegacySkill enemy, int slots, int enemyResistance)
            => new LegacyQueuedDuel(1000, 50, 1000, enemyResistance, new[] { player }, new[] { enemy }, new[] { slots }, 1,
                meshPercent: 0);
        private static LegacySkill Attack(int id, string name, int power, int hits = 1)
            => new LegacySkill(id, name, 0, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, hits, 0, "검증");
        private static LegacySkill Guard(int id, int power)
            => new LegacySkill(id, "막기", 0, power, power, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 0, "검증");

        private sealed class HudFixture : IDisposable
        {
            private readonly GameObject parent = new GameObject("Combat Readability HUD Test");
            private readonly DuelPresentationSettings settings;
            public LegacyCombatHud Hud { get; }
            public HudFixture(string tuning = null)
            {
                settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                if (!string.IsNullOrEmpty(tuning)) JsonUtility.FromJsonOverwrite(tuning, settings);
                Hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), _ => { }, () => { }, () => { }, settings,
                    () => { }, () => { });
                Canvas.ForceUpdateCanvases();
            }
            public void Refresh(ILegacyDuelView duel, bool resolving, int slot)
            {
                Hud.Refresh(duel, 10f, resolving, slot, null, null, null);
                Canvas.ForceUpdateCanvases();
            }
            public void Dispose()
            {
                Hud.Dispose();
                Object.Destroy(parent);
                Object.Destroy(settings);
            }
        }
    }
}
