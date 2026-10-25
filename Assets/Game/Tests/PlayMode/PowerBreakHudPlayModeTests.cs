using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Presentation;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class PowerBreakHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator PrototypeShowsOnlyHealth_AndTheLegacySwitchRestoresTheExistingPanel()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                var prototype = Duel(true, Attack(99601, 20), Attack(99602, 5));
                fixture.Refresh(prototype, false);
                Transform player = fixture.Hud.Root.transform.Find("PlayerStatus");
                Assert.That(player.Find("HP Value").GetComponent<Text>().text, Is.EqualTo("1000 / 1000"));
                foreach (string name in new[] { "Resistance Label", "Resistance Track", "Resistance", "Resistance delayed", "Resistance Value" })
                    Assert.That(player.Find(name).gameObject.activeSelf, Is.False, name + " must not remain visible in the prototype.");
                Assert.That(((RectTransform)player).rect.height, Is.EqualTo(78f));
                fixture.Refresh(Duel(false, Attack(99601, 20), Attack(99602, 5)), false);
                Assert.That(player.Find("Resistance Value").gameObject.activeSelf, Is.True);
                Assert.That(player.Find("Resistance Track").gameObject.activeSelf, Is.True);
                Assert.That(((RectTransform)player).rect.height, Is.EqualTo(112f));
                Assert.That(fixture.Hud.PowerClashVisible, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator OnlyTheNextCardShatters_AndItsFastOrChargeWidthStaysAnExposedGap()
        {
            yield return null;
            foreach (int duration in new[] { 1, 4 })
                using (var fixture = new Fixture())
                {
                    LegacySkill strong = Attack(99611, 20), weak = Attack(99612, 5), after = Attack(99613, 5, duration);
                    LegacySkill later = Attack(99614, 5);
                    var duel = Duel(true, strong, weak);
                    Assert.That(duel.TrySetFixedQueues(new[] { strong, later, later, later }, new[] { weak, after, later }), Is.True);
                    fixture.Refresh(duel, false);
                    duel.Commit();
                    LegacyCurrentSlot slot = duel.BeginNextSlot();
                    fixture.Hud.SetSkillFeedback(slot);
                    fixture.Refresh(duel, true);
                    RectTransform next = fixture.Hud.GetQueuedSkillAnchor(false, 1);
                    Vector2 position = next.anchoredPosition, size = next.sizeDelta;
                    duel.ResolveNextHit();
                    Assert.That(duel.IsEnemySkillDestroyed(1), Is.True);
                    fixture.Refresh(duel, true);
                    Assert.That(fixture.Hud.BreakFragmentEmissionCount, Is.EqualTo(1));
                    Assert.That(fixture.Hud.GetQueuedSkillAnchor(false, 1), Is.SameAs(next));
                    Assert.That(next.anchoredPosition, Is.EqualTo(position));
                    Assert.That(next.sizeDelta, Is.EqualTo(size));
                    Assert.That(next.Find("Icon").GetComponent<Image>().enabled, Is.False);
                    Text gap = next.Find("Broken Skill Gap").GetComponent<Text>();
                    Assert.That(gap.gameObject.activeSelf, Is.True);
                    Assert.That(gap.text, Does.Contain("×2"));
                    Assert.That(fixture.Hud.GetQueuedSkillAnchor(false, 2), Is.Not.Null, "The later reservation must survive.");
                    fixture.Hud.PlayBreakQueueCancellation(false, 1);
                    fixture.Refresh(duel, true);
                    Assert.That(fixture.Hud.BreakFragmentEmissionCount, Is.EqualTo(1), "Repeated observations cannot shatter it again.");
                    Finish(duel);
                    LegacyCurrentSlot gapSlot = duel.BeginNextSlot();
                    Assert.That(gapSlot.EnemyBrokenGap, Is.True);
                    fixture.Hud.SetSkillFeedback(gapSlot);
                    fixture.Refresh(duel, true);
                    Assert.That(fixture.Hud.CombatReadoutPairText, Does.Contain("파괴된 빈틈 ×2"));
                    Assert.That(fixture.Hud.EnemyClashPowerText, Is.Null, "A destroyed reservation has no attack power.");
                }
        }

        [UnityTest]
        public IEnumerator FinalRolledPowersMeet_TheBrokenNumberFracturesOnce_AndRealTimeClearsItsPieces()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                LegacySkill strong = Attack(99621, 20), weak = Attack(99622, 5);
                var duel = Duel(true, strong, weak);
                Assert.That(duel.TrySetFixedQueues(new[] { strong, weak }, new[] { weak, weak }), Is.True);
                fixture.Refresh(duel, false);
                duel.Commit(); LegacyCurrentSlot slot = duel.BeginNextSlot();
                fixture.Hud.SetSkillFeedback(slot);
                fixture.Refresh(duel, true);
                Assert.That(fixture.Hud.PlayerClashPowerText, Is.EqualTo(slot.PlayerTotalPower.ToString()));
                Assert.That(fixture.Hud.EnemyClashPowerText, Is.EqualTo(slot.EnemyTotalPower.ToString()));
                Assert.That(fixture.Hud.CombatReadout.rect.height, Is.EqualTo(134f));
                int playerHealth = duel.Player.Health, enemyHealth = duel.Enemy.Health;
                duel.ResolveNextHit();
                fixture.Hud.PlayPowerClashImpact(slot);
                int playerAfterHit = duel.Player.Health, enemyAfterHit = duel.Enemy.Health;
                Assert.That(playerAfterHit, Is.LessThan(playerHealth));
                Assert.That(enemyAfterHit, Is.LessThan(enemyHealth));
                fixture.Refresh(duel, true, .08f);
                Assert.That(fixture.Hud.ActivePowerNumberFragmentCount, Is.EqualTo(6));
                fixture.Hud.PlayPowerClashImpact(slot);
                fixture.Refresh(duel, true, 0f);
                Assert.That(fixture.Hud.ActivePowerNumberFragmentCount, Is.EqualTo(6));
                Assert.That(duel.Player.Health, Is.EqualTo(playerAfterHit));
                Assert.That(duel.Enemy.Health, Is.EqualTo(enemyAfterHit), "Number animation cannot resolve damage twice.");
                fixture.Refresh(duel, true, .6f);
                Assert.That(fixture.Hud.ActivePowerNumberFragmentCount, Is.Zero);
                Assert.That(fixture.Hud.PlayerClashPowerText, Is.EqualTo("20"));
                foreach (Graphic graphic in fixture.Hud.Root.transform.Find("Power Clash").GetComponentsInChildren<Graphic>(true))
                    Assert.That(graphic.raycastTarget, Is.False);
                fixture.Hud.Reset();
                Assert.That(fixture.Hud.PowerClashVisible, Is.False);
                Assert.That(fixture.Hud.ActivePowerNumberFragmentCount, Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator DefenceAndDodgesHaveNoAttackClashNumbers_AndOriginalEmptySpaceIsNotAnExposedGap()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                LegacySkill strong = Attack(99631, 20);
                var guard = new LegacySkill(99632, "방어", 0, 1, 1, LegacySkillKind.Defence,
                    LegacySkillProperty.Defence, 1, 0, string.Empty);
                var duel = Duel(true, strong, guard);
                Assert.That(duel.TrySetFixedQueues(new[] { strong, strong }, new[] { guard }), Is.True);
                fixture.Refresh(duel, false);
                duel.Commit(); LegacyCurrentSlot slot = duel.BeginNextSlot();
                fixture.Hud.SetSkillFeedback(slot);
                fixture.Refresh(duel, true);
                Assert.That(fixture.Hud.PowerClashVisible, Is.False);
                Assert.That(fixture.Hud.EnemyClashPowerText, Is.Null);
                duel.ResolveNextHit(); fixture.Hud.PlayPowerClashImpact(slot);
                fixture.Refresh(duel, true, .1f);
                Assert.That(fixture.Hud.ActivePowerNumberFragmentCount, Is.Zero);
                Finish(duel);
                LegacyCurrentSlot empty = duel.BeginNextSlot();
                fixture.Hud.SetSkillFeedback(empty);
                fixture.Refresh(duel, true);
                Assert.That(empty.EnemyBrokenGap, Is.False);
                Assert.That(fixture.Hud.CombatReadoutPairText, Does.Contain("빈자리"));
                Assert.That(fixture.Hud.CombatReadoutPairText, Does.Not.Contain("×2"));
            }
            foreach (bool automatic in new[] { true, false })
                using (var fixture = new Fixture())
                {
                    LegacySkill player = automatic ? LegacySkillDefinitions.Skill(54) : Attack(99633, 5);
                    LegacySkill enemy = Attack(99634, 20);
                    var duel = Duel(true, player, enemy);
                    Assert.That(duel.TrySetFixedQueues(new[] { player, player }, new[] { enemy, enemy }), Is.True);
                    fixture.Refresh(duel, false);
                    duel.Commit(); LegacyCurrentSlot slot = duel.BeginNextSlot();
                    fixture.Hud.SetSkillFeedback(slot);
                    fixture.Refresh(duel, true);
                    Assert.That(fixture.Hud.PowerClashVisible, Is.EqualTo(!automatic),
                        "An already confirmed automatic dodge never advertises a clash.");
                    if (!automatic)
                        Assert.That(duel.TryStep(LegacyStepAction.Dodge, true, out bool dodged) && dodged, Is.True);
                    Assert.That(slot.DodgeSucceeded, Is.True);
                    duel.ResolveNextHit();
                    fixture.Hud.PlayPowerClashImpact(slot);
                    Assert.That(fixture.Hud.PowerClashVisible, Is.False,
                        "The hit callback closes a comparison whose manual dodge succeeded after its initial display.");
                    fixture.Refresh(duel, true, .1f);
                    Assert.That(fixture.Hud.PowerClashVisible, Is.False);
                    Assert.That(fixture.Hud.ActivePowerNumberFragmentCount, Is.Zero);
                    Assert.That(slot.PlayerBroken || slot.EnemyBroken, Is.False);
                    Assert.That(duel.IsPlayerSkillDestroyed(1), Is.False);
                }
        }

        [UnityTest]
        public IEnumerator TrioBreakAndRosterCallbacks_ShareOneShatter_AndHidingTheHudClearsPowerFragments()
        {
            yield return null;
            foreach (bool targetFalls in new[] { false, true })
            using (var fixture = new Fixture())
            {
                LegacySkill strong = Attack(99641, 20), weak = Attack(99642, 5);
                var script = new EnemyScript(new IReadOnlyList<LegacySkill>[] { new[] { weak, weak } });
                int enemyHealth = targetFalls ? 20 : 1000;
                var duel = new LegacyQueuedDuel(1000, 50, new[] { strong }, new[]
                {
                    new LegacyEnemyDefinition("검객 1", enemyHealth, 50, script),
                    new LegacyEnemyDefinition("검객 2", enemyHealth, 50, script),
                    new LegacyEnemyDefinition("검객 3", enemyHealth, 50, script)
                }, randomSeed: 7, meshPercent: 0, usePowerBreak: true);
                Assert.That(duel.TryQueueLane(0), Is.True);
                fixture.Refresh(duel, false);
                duel.Commit(); LegacyCurrentSlot slot = duel.BeginNextSlot();
                fixture.Hud.SetSkillFeedback(slot);
                fixture.Refresh(duel, true);
                duel.ResolveNextHit();
                Assert.That(slot.EnemyDestroyedQueueIndex, Is.GreaterThanOrEqualTo(0));
                fixture.Hud.PlayBreakQueueCancellation(false, slot.EnemyDestroyedQueueIndex);
                fixture.Hud.PlayEnemyQueueCancellation();
                fixture.Refresh(duel, true);
                fixture.Hud.PlayEnemyQueueCancellation();
                Assert.That(fixture.Hud.BreakFragmentEmissionCount, Is.EqualTo(1),
                    "Roster cancellation and power-break observation must not emit the same destroyed card twice.");
                int destroyed = slot.EnemyDestroyedQueueIndex;
                Assert.That(duel.IsEnemySkillDestroyed(destroyed), Is.True, "The actual destruction remains in turn history.");
                Assert.That(duel.Enemies[duel.EnemyOwnerAtSlot(destroyed)].IsDefeated, Is.EqualTo(targetFalls));
                RectTransform destroyedPlace = fixture.Hud.GetQueuedSkillAnchor(false, destroyed);
                if (targetFalls)
                    Assert.That(destroyedPlace, Is.Null, "A fallen owner's canceled reservation cannot promise a ×2 gap against its surviving ally.");
                else
                    Assert.That(destroyedPlace.Find("Broken Skill Gap").gameObject.activeSelf, Is.True);
                fixture.Hud.PlayPowerClashImpact(slot);
                fixture.Refresh(duel, true, .08f);
                Assert.That(fixture.Hud.ActivePowerNumberFragmentCount, Is.EqualTo(6));
                fixture.Hud.Root.SetActive(false);
                Assert.That(fixture.Hud.PowerClashVisible, Is.False);
                Assert.That(fixture.Hud.ActivePowerNumberFragmentCount, Is.Zero);
            }
        }

        private static LegacyQueuedDuel Duel(bool mode, LegacySkill player, LegacySkill enemy)
            => new LegacyQueuedDuel(1000, 50, 1000, 50, new[] { player }, new[] { enemy }, new[] { 3 },
                randomSeed: 7, meshPercent: 0, usePowerBreak: mode);
        private static LegacySkill Attack(int id, int power, int duration = 2)
            => new LegacySkill(id, "검증 베기", 0, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash,
                1, 0, string.Empty, durationHalfUnits: duration);
        private static void Finish(LegacyQueuedDuel duel)
        {
            while (!duel.CurrentSlot.IsResolved) duel.ResolveNextHit();
            duel.CompleteCurrentSlot();
        }
        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("Power Break HUD Test");
            private readonly DuelPresentationSettings settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            private readonly LegacyDuelArt art = new LegacyDuelArt();
            public LegacyCombatHud Hud { get; }
            public Fixture()
            {
                JsonUtility.FromJsonOverwrite("{\"clashMotionEnabled\":false}", settings);
                Hud = new LegacyCombatHud(root.transform, art, _ => { }, () => { }, () => { }, settings, () => { }, () => { });
                Canvas.ForceUpdateCanvases();
            }
            public void Refresh(LegacyQueuedDuel duel, bool resolving, float realDelta = 0f)
            {
                Hud.Refresh(duel, 10f, resolving, duel.CurrentSlot?.SlotIndex ?? -1, null, null, null,
                    delta: 0f, realDelta: realDelta);
                Canvas.ForceUpdateCanvases();
            }
            public void Dispose()
            {
                Hud.Dispose(); art.Dispose();
                Object.Destroy(root); Object.Destroy(settings);
            }
        }
    }
}
