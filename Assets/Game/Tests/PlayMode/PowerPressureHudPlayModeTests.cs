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
    public sealed class PowerPressureHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator PlayerNumberAndGaugeRise_EnemyStaysFixed_ThenSteamReleasesBeforeTheAttack()
        {
            yield return null;
            using (var scope = new Scope())
            {
                LegacyQueuedDuel duel = Duel();
                LegacyCurrentSlot slot = scope.Prepare(duel);
                Assert.That(scope.Hud.BeginPlayerPowerPressure(slot), Is.True);
                Assert.That(scope.Hud.PlayerClashPowerText, Is.EqualTo("0"));
                Assert.That(scope.Hud.EnemyClashPowerText, Is.EqualTo(slot.EnemyTotalPower.ToString()));
                Image gauge = scope.Find<Image>("Player Pressure Gauge"), fill = scope.Find<Image>("Pressure Fill");
                DuelPressureSteamGraphic steam = scope.Find<DuelPressureSteamGraphic>("Player Pressure Steam");
                Assert.That(fill.sprite, Is.Not.Null, "A filled Image needs a real sprite to obey fillAmount.");
                Assert.That(fill.fillAmount, Is.Zero);
                Assert.That(gauge.gameObject.activeSelf, Is.True);
                int playerHealth = duel.Player.Health, enemyHealth = duel.Enemy.Health;
                scope.Refresh(duel, true, .15f);
                Assert.That(int.Parse(scope.Hud.PlayerClashPowerText), Is.InRange(1, slot.PlayerTotalPower - 1));
                Assert.That(fill.fillAmount, Is.InRange(.01f, .99f));
                Assert.That(scope.Hud.EnemyClashPowerText, Is.EqualTo(slot.EnemyTotalPower.ToString()));
                Assert.That(duel.Player.Health, Is.EqualTo(playerHealth));
                Assert.That(duel.Enemy.Health, Is.EqualTo(enemyHealth), "Compression never resolves a hit or rolls power again.");
                scope.Refresh(duel, true, DuelPowerPressure.ChargeSeconds - .15f + .00001f);
                Assert.That(scope.Hud.PlayerClashPowerText, Is.EqualTo(slot.PlayerTotalPower.ToString()));
                Assert.That(scope.Hud.IsPlayerPowerPressurizing, Is.True, "The final number has a readable lock.");
                Assert.That(fill.fillAmount, Is.EqualTo(1f));
                scope.Refresh(duel, true, DuelPowerPressure.LockSeconds + .00001f);
                Assert.That(scope.Hud.IsPlayerPowerPressurizing, Is.False);
                Assert.That(steam.gameObject.activeSelf, Is.True, "Steam continues briefly while the attack can begin.");
                scope.AssertSteamAboveSkillNames(steam);
                scope.Refresh(duel, true, DuelPowerPressure.SteamSeconds);
                Assert.That(steam.gameObject.activeSelf, Is.False);
                Assert.That(gauge.gameObject.activeSelf, Is.False);
                Assert.That(scope.Hud.PlayerClashPowerText, Is.EqualTo(slot.PlayerTotalPower.ToString()));
                foreach (Graphic graphic in scope.Hud.Root.GetComponentsInChildren<Graphic>(true))
                    if (graphic.name == "Player Pressure Gauge" || graphic.name == "Pressure Fill" || graphic.name == "Player Pressure Steam")
                        Assert.That(graphic.raycastTarget, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator RepeatedFeedbackAndLaterHitsNeverRestartTheSameCompression()
        {
            yield return null;
            using (var scope = new Scope())
            {
                var duel = Duel(player: new[] { Attack(99811, 20, hits: 3), Attack(99812, 5) },
                    enemy: new[] { Attack(99813, 5, hits: 3), Attack(99814, 5) });
                LegacyCurrentSlot slot = scope.Prepare(duel);
                Assert.That(scope.Hud.BeginPlayerPowerPressure(slot), Is.True);
                scope.Refresh(duel, true, .2f);
                float charge = scope.Hud.PlayerPressureCharge;
                string value = scope.Hud.PlayerClashPowerText;
                scope.Hud.SetSkillFeedback(slot);
                Assert.That(scope.Hud.BeginPlayerPowerPressure(slot), Is.False);
                scope.Refresh(duel, true);
                Assert.That(scope.Hud.PlayerPressureCharge, Is.EqualTo(charge));
                Assert.That(scope.Hud.PlayerClashPowerText, Is.EqualTo(value));
                scope.Refresh(duel, true, DuelPowerPressure.HoldSeconds);
                duel.ResolveNextHit();
                scope.Hud.SetSkillFeedback(slot);
                Assert.That(scope.Hud.BeginPlayerPowerPressure(slot), Is.False);
                Assert.That(scope.Hud.IsPlayerPowerPressurizing, Is.False);
                Assert.That(slot.IsResolved, Is.False, "There are more hits in this same skill.");
            }
        }

        [UnityTest]
        public IEnumerator IneligibleLegacyDefenceDodgeAndCounterSlotsNeverStartPressure()
        {
            yield return null;
            using (var scope = new Scope())
            {
                LegacyCurrentSlot legacy = scope.Prepare(Duel(prototype: false));
                Assert.That(scope.Hud.BeginPlayerPowerPressure(legacy), Is.False);
                var guard = new LegacySkill(99821, "방어", 0, 5, 5, LegacySkillKind.Defence,
                    LegacySkillProperty.Defence, 1, 0, string.Empty);
                LegacyCurrentSlot defence = scope.Prepare(Duel(enemy: new[] { guard }));
                Assert.That(scope.Hud.BeginPlayerPowerPressure(defence), Is.False);
                var dodge = Duel(player: new[] { LegacySkillDefinitions.Skill(54) });
                LegacyCurrentSlot dodged = scope.Prepare(dodge);
                Assert.That(dodged.DodgeSucceeded, Is.True);
                Assert.That(scope.Hud.BeginPlayerPowerPressure(dodged), Is.False);
                foreach (bool playerCounter in new[] { true, false })
                {
                    LegacySkill attack = Attack(99822, 5);
                    var counter = new LegacyCounter(Attack(99823, 5));
                    var duel = Duel(player: playerCounter ? new[] { attack } : new[] { attack, attack },
                        enemy: playerCounter ? new[] { attack, attack } : new[] { attack },
                        playerCounter: playerCounter ? counter : null, enemyCounter: playerCounter ? null : counter);
                    scope.Prepare(duel);
                    Finish(duel);
                    LegacyCurrentSlot slot = duel.BeginNextSlot();
                    // A player's fallback counter settles only as its first hit begins.
                    duel.ResolveNextHit();
                    Assert.That(playerCounter ? slot.PlayerCountered : slot.EnemyCountered, Is.True);
                    scope.Hud.SetSkillFeedback(slot);
                    scope.Refresh(duel, true);
                    Assert.That(scope.Hud.BeginPlayerPowerPressure(slot), Is.False, "A fallback counter does not add another preparation hold.");
                }
            }
        }

        [UnityTest]
        public IEnumerator TempoWaitsForAnActualSimultaneousClash_WithoutInventingPendingPower()
        {
            yield return null;
            foreach (bool charge in new[] { false, true })
                using (var scope = new Scope())
                {
                    var player = charge ? new[] { Attack(99831, 20, 4), Attack(99832, 10) }
                        : new[] { Attack(99831, 10, 1), Attack(99832, 10, 1), Attack(99833, 10) };
                    var duel = Duel(player: player, enemy: new[] { Attack(99834, 10), Attack(99835, 10), Attack(99836, 10) });
                    LegacyCurrentSlot first = scope.Prepare(duel);
                    Assert.That(scope.Hud.BeginPlayerPowerPressure(first), Is.False,
                        "The earlier unilateral completion has no real clash roll to display.");
                    Finish(duel);
                    LegacyCurrentSlot simultaneous = duel.BeginNextSlot();
                    scope.Hud.SetSkillFeedback(simultaneous);
                    scope.Refresh(duel, true);
                    Assert.That(scope.Hud.BeginPlayerPowerPressure(simultaneous), Is.True);
                    Assert.That(scope.Hud.PlayerClashPowerText, Is.EqualTo("0"));
                    scope.Refresh(duel, true, DuelPowerPressure.HoldSeconds + .00001f);
                    Assert.That(scope.Hud.PlayerClashPowerText, Is.EqualTo(simultaneous.PlayerTotalPower.ToString()));
                    Assert.That(scope.Hud.IsPlayerPowerPressurizing, Is.False);
                    Assert.That(scope.Hud.BeginPlayerPowerPressure(simultaneous), Is.False);
                }
        }

        [UnityTest]
        public IEnumerator ManualDodgeHidingResetAndTurnEndClearGaugeSteamAndHold()
        {
            yield return null;
            using (var scope = new Scope())
            {
                var duel = Duel();
                LegacyCurrentSlot slot = scope.Prepare(duel);
                Assert.That(scope.Hud.BeginPlayerPowerPressure(slot), Is.True);
                Assert.That(duel.TryStep(LegacyStepAction.Dodge, true, out bool dodged) && dodged, Is.True);
                scope.Refresh(duel, true, .01f);
                scope.AssertPressureHidden();
                foreach (int boundary in new[] { 0, 1, 2 })
                {
                    duel = Duel(); slot = scope.Prepare(duel);
                    Assert.That(scope.Hud.BeginPlayerPowerPressure(slot), Is.True);
                    scope.Refresh(duel, true, .1f);
                    if (boundary == 0) scope.Hud.Root.SetActive(false);
                    else if (boundary == 1) scope.Hud.Reset();
                    else scope.Hud.EndTurn();
                    scope.AssertPressureHidden();
                    scope.Hud.Root.SetActive(true);
                }
            }
        }

        private static LegacySkill Attack(int id, int power, int duration = 2, int hits = 1)
            => new LegacySkill(id, "압력 시험", 0, power, power, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, hits, 0, string.Empty, durationHalfUnits: duration);

        private static LegacyQueuedDuel Duel(bool prototype = true, LegacySkill[] player = null, LegacySkill[] enemy = null,
            LegacyCounter playerCounter = null, LegacyCounter enemyCounter = null)
        {
            player = player ?? new[] { Attack(99841, 20), Attack(99842, 5) };
            enemy = enemy ?? new[] { Attack(99843, 5), Attack(99844, 5) };
            var duel = new LegacyQueuedDuel(1000, 50, 1000, 50, player, enemy, new[] { enemy.Length },
                randomSeed: 7, meshPercent: 0, playerCounter: playerCounter, enemyCounter: enemyCounter, usePowerBreak: prototype);
            Assert.That(duel.TrySetFixedQueues(player, enemy), Is.True);
            return duel;
        }

        private static void Finish(LegacyQueuedDuel duel)
        {
            while (!duel.CurrentSlot.IsResolved) duel.ResolveNextHit();
            duel.CompleteCurrentSlot();
        }

        private sealed class Scope : IDisposable
        {
            private readonly GameObject owner = new GameObject("Power Pressure HUD Test");
            private readonly DuelPresentationSettings settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            private readonly LegacyDuelArt art = new LegacyDuelArt();
            public LegacyCombatHud Hud { get; }
            public Scope()
            {
                JsonUtility.FromJsonOverwrite("{\"clashMotionEnabled\":false}", settings);
                Hud = new LegacyCombatHud(owner.transform, art, _ => { }, () => { }, () => { }, settings);
                Canvas.ForceUpdateCanvases();
            }
            public LegacyCurrentSlot Prepare(LegacyQueuedDuel duel)
            {
                Hud.ClearSkillFeedback();
                Refresh(duel, false);
                duel.Commit();
                LegacyCurrentSlot slot = duel.BeginNextSlot();
                Hud.SetSkillFeedback(slot);
                Refresh(duel, true);
                return slot;
            }
            public void Refresh(LegacyQueuedDuel duel, bool resolving, float realDelta = 0f)
            {
                Hud.Refresh(duel, 10f, resolving, duel.CurrentSlot?.SlotIndex ?? -1, null, null, null,
                    delta: 0f, realDelta: realDelta);
                Canvas.ForceUpdateCanvases();
            }
            public T Find<T>(string name) where T : Component
            {
                foreach (T component in Hud.Root.GetComponentsInChildren<T>(true))
                    if (component.name == name) return component;
                throw new InvalidOperationException("Missing pressure visual: " + name);
            }
            public void AssertPressureHidden()
            {
                Assert.That(Hud.IsPlayerPowerPressurizing, Is.False);
                Assert.That(Find<Image>("Player Pressure Gauge").gameObject.activeSelf, Is.False);
                Assert.That(Find<DuelPressureSteamGraphic>("Player Pressure Steam").gameObject.activeSelf, Is.False);
            }
            public void AssertSteamAboveSkillNames(DuelPressureSteamGraphic steam)
            {
                Canvas.ForceUpdateCanvases();
                Mesh mesh = steam.canvasRenderer.GetMesh();
                Assert.That(mesh, Is.Not.Null);
                Assert.That(mesh.vertexCount, Is.GreaterThan(0), "Check the actual generated release cloud.");
                RectTransform names = Find<Text>("Current Skills").rectTransform;
                float nameTop = Hud.CombatReadout.InverseTransformPoint(names.TransformPoint(
                    new Vector3(names.rect.center.x, names.rect.yMax, 0f))).y;
                float cloudBottom = float.PositiveInfinity;
                foreach (Vector3 vertex in mesh.vertices)
                    cloudBottom = Mathf.Min(cloudBottom,
                        Hud.CombatReadout.InverseTransformPoint(steam.transform.TransformPoint(vertex)).y);
                Assert.That(cloudBottom, Is.GreaterThanOrEqualTo(nameTop - .25f),
                    "The pressure cloud must not cover the existing current-skill name row.");
            }
            public void Dispose() { Hud.Dispose(); art.Dispose(); Object.Destroy(owner); Object.Destroy(settings); }
        }
    }
}
