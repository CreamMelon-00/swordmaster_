using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class ClashReadoutTests
    {
        [Test]
        public void BeforeTheFirstHit_PowerAndEarlyEffectsDoNotRevealAResult()
        {
            var input = new LegacyClashInput(0, LegacyClashAction.Attack, 100, 1, LegacyClashAction.Guard, 1, 1,
                playerDodged: true, enemyBroken: true);
            Assert.That(LegacyClashReadout.Result(new LegacySlotFacts(input, enemyHealthLost: 60,
                enemyResistanceLost: 10)), Is.EqualTo("맞붙는 중"));
            Assert.That(LegacyClashReadout.Result(LegacySlotFacts.None), Is.Empty);
        }

        [Test]
        public void StrongerAttackStillTakesDamage_AndBothResistanceLossesRemainVisible()
        {
            LegacyQueuedDuel duel = Duel(Attack(990, "가로베기", 12), Attack(991, "내려베기", 8));
            Begin(duel);
            duel.ResolveNextHit();
            Assert.That(duel.CurrentSlotFacts.PlayerResistanceLost, Is.EqualTo(8));
            Assert.That(duel.CurrentSlotFacts.EnemyResistanceLost, Is.EqualTo(12));
            Assert.That(LegacyClashReadout.Result(duel.CurrentSlotFacts), Is.EqualTo("서로 저항 손실"));
            Assert.That(LegacyClashReadout.Pair(duel.CurrentSlot.PlayerSkill, duel.CurrentSlot.EnemySkill),
                Is.EqualTo("내 가로베기  ↔  상대 내려베기"));
        }

        [Test]
        public void BreakingResistanceWithNoHealthOverflowIsStillAnExplicitResult()
        {
            LegacyQueuedDuel duel = Duel(Attack(990, "베기", 10), Attack(991, "약공", 1), enemyResistance: 10);
            Begin(duel);
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.EnemyHealthDamage, Is.Zero);
            Assert.That(LegacyClashReadout.Result(duel.CurrentSlotFacts), Is.EqualTo("상대 저항 붕괴 · 내 저항 손실"));
        }

        [Test]
        public void VolleyShowsInitialResistanceLoss_TheLaterBreak_ThenHealthLoss()
        {
            LegacyQueuedDuel duel = Duel(Attack(990, "연격", 18, 3), Attack(991, "약공", 1), enemyResistance: 12);
            Begin(duel);
            duel.ResolveNextHit();
            Assert.That(LegacyClashReadout.Result(duel.CurrentSlotFacts), Is.EqualTo("서로 저항 손실"));
            duel.ResolveNextHit();
            Assert.That(duel.CurrentSlotFacts.EnemyHealthLost, Is.Zero);
            Assert.That(LegacyClashReadout.Result(duel.CurrentSlotFacts), Is.EqualTo("상대 저항 붕괴 · 내 저항 손실"));
            LegacyHitResult final = duel.ResolveNextHit();
            Assert.That(final.EnemyHealthDamage, Is.EqualTo(12));
            Assert.That(LegacyClashReadout.Result(duel.CurrentSlotFacts),
                Is.EqualTo("상대 저항 붕괴·체력 손실 · 내 저항 손실"));
        }

        [TestCase(10, 4, "상대 방어 관통", "내 방어 관통")]
        [TestCase(4, 10, "상대 방어 성공", "내 방어 성공")]
        public void GuardResultNamesWhoseGuardWasPiercedOrHeld(int attack, int guard, string expected, string mirrored)
        {
            LegacyQueuedDuel duel = Duel(Attack(990, "베기", attack), Guard(991, guard));
            Begin(duel);
            duel.ResolveNextHit();
            Assert.That(LegacyClashReadout.Result(duel.CurrentSlotFacts), Is.EqualTo(expected));
            Assert.That(LegacyClashReadout.Result(duel.CurrentSlotFacts.Mirrored()), Is.EqualTo(mirrored));
        }

        [Test]
        public void AHitWithNoActualLossDoesNotClaimToHavePassedTheGuard()
        {
            var input = new LegacyClashInput(0, LegacyClashAction.Attack, 10, 1, LegacyClashAction.Guard, 2, 1, decided: true);
            Assert.That(LegacyClashReadout.Result(new LegacySlotFacts(input)), Is.EqualTo("피해 없음"));
        }

        [Test]
        public void DodgeAndTheAttackThatStillLandsMirrorTheirOwners()
        {
            var input = new LegacyClashInput(0, LegacyClashAction.Attack, 6, 1, LegacyClashAction.Attack, 10, 1,
                playerDodged: true, decided: true);
            var facts = new LegacySlotFacts(input, enemyResistanceLost: 6);
            Assert.That(LegacyClashReadout.Result(facts), Is.EqualTo("내 회피 성공 · 상대 저항 손실"));
            Assert.That(LegacyClashReadout.Result(facts.Mirrored()), Is.EqualTo("상대 회피 성공 · 내 저항 손실"));
        }

        [Test]
        public void MixedHealthAndResistanceLossNamesBothSides()
        {
            var input = new LegacyClashInput(0, LegacyClashAction.Attack, 6, 1, LegacyClashAction.Attack, 10, 1, decided: true);
            var facts = new LegacySlotFacts(input, playerHealthLost: 20, enemyResistanceLost: 6);
            Assert.That(LegacyClashReadout.Result(facts), Is.EqualTo("내 체력 손실 · 상대 저항 손실"));
            Assert.That(LegacyClashReadout.Result(facts.Mirrored()), Is.EqualTo("내 저항 손실 · 상대 체력 손실"));
        }

        [Test]
        public void SignedResistanceRecoverySurvivesMirroringAndEquality()
        {
            var input = new LegacyClashInput(0, LegacyClashAction.Guard, 2, 1, LegacyClashAction.Guard, 2, 1, decided: true);
            var facts = new LegacySlotFacts(input, playerResistanceLost: -8);
            Assert.That(LegacyClashReadout.Result(facts), Is.EqualTo("내 저항 회복"));
            Assert.That(LegacyClashReadout.Result(facts.Mirrored()), Is.EqualTo("상대 저항 회복"));
            Assert.That(facts.Mirrored().Mirrored(), Is.EqualTo(facts));
            Assert.That(facts.Mirrored().Mirrored().GetHashCode(), Is.EqualTo(facts.GetHashCode()));
            Assert.That(facts, Is.Not.EqualTo(new LegacySlotFacts(input)));
        }

        [Test]
        public void MutualBreaksAndMutualHealthLossRemainMutual()
        {
            var broken = new LegacyClashInput(0, LegacyClashAction.Attack, 10, 1, LegacyClashAction.Attack, 10, 1,
                playerBroken: true, enemyBroken: true, decided: true);
            Assert.That(LegacyClashReadout.Result(new LegacySlotFacts(broken)), Is.EqualTo("서로 저항 붕괴"));
            Assert.That(LegacyClashReadout.Result(new LegacySlotFacts(broken, playerHealthLost: 1, enemyHealthLost: 2)),
                Is.EqualTo("서로 저항 붕괴·체력 손실"));
            var input = new LegacyClashInput(1, LegacyClashAction.Attack, 10, 1, LegacyClashAction.Attack, 10, 1, decided: true);
            Assert.That(LegacyClashReadout.Result(new LegacySlotFacts(input, playerHealthLost: 20, enemyHealthLost: 20)),
                Is.EqualTo("서로 체력 손실"));
        }

        [Test]
        public void TrioUsesTheActualCurrentOpponent_EvenWhenTheHitJustKilledThatOwner()
        {
            LegacyQueuedDuel duel = Trio(Attack(990, "베기", 12), Guard(991, 1), 1, 50);
            Begin(duel);
            int target = duel.ActiveEnemyIndex;
            string expected = "상대 " + (target + 1) + "(" + duel.EnemyName(target) + ")";
            Assert.That(LegacyClashReadout.OpponentLabel(duel), Is.EqualTo(expected));
            duel.ResolveNextHit();
            Assert.That(duel.Enemies[target].IsDefeated, Is.True);
            Assert.That(duel.EnemyIndexForSlot(0), Is.Not.EqualTo(target), "A fresh lookup already chooses a living fallback.");
            Assert.That(LegacyClashReadout.OpponentLabel(duel), Is.EqualTo(expected), "This hit still belongs to the fallen target.");
        }

        [Test]
        public void ACancelledOwnersSlotUsesTheLivingOpponentActuallySelectedByTheResolver()
        {
            LegacyQueuedDuel duel = Trio(Attack(990, "베기", 2), Attack(991, "약공", 1), 100, 2);
            Begin(duel);
            int brokenOwner = duel.ActiveEnemyIndex;
            duel.ResolveNextHit();
            duel.CompleteCurrentSlot();
            duel.ResolveNextSlot();
            duel.ResolveNextSlot();
            Assert.That(duel.IsEnemySlotCanceled(3), Is.True);
            Assert.That(duel.EnemyOwnerAtSlot(3), Is.EqualTo(brokenOwner));
            duel.BeginNextSlot();
            int actual = duel.ActiveEnemyIndex;
            Assert.That(actual, Is.Not.EqualTo(brokenOwner));
            Assert.That(duel.CurrentSlot.EnemySkill, Is.Null);
            Assert.That(LegacyClashReadout.OpponentLabel(duel),
                Is.EqualTo("상대 " + (actual + 1) + "(" + duel.EnemyName(actual) + ")"));
        }

        private static LegacyQueuedDuel Duel(LegacySkill player, LegacySkill enemy, int enemyResistance = 50)
            => new LegacyQueuedDuel(1000, 50, 1000, enemyResistance, new[] { player }, new[] { enemy }, new[] { 1 }, 1,
                meshPercent: 0);

        private static LegacyQueuedDuel Trio(LegacySkill player, LegacySkill enemy, int health, int resistance)
        {
            var script = new EnemyScript(new IReadOnlyList<LegacySkill>[] { new[] { enemy, enemy } });
            return new LegacyQueuedDuel(1000, 50, new[] { player }, new[]
            {
                new LegacyEnemyDefinition("선봉", health, resistance, script),
                new LegacyEnemyDefinition("검객", health, resistance, script),
                new LegacyEnemyDefinition("기습꾼", health, resistance, script)
            }, randomSeed: 17, meshPercent: 0);
        }

        private static void Begin(LegacyQueuedDuel duel)
        {
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.BeginNextSlot();
        }

        private static LegacySkill Attack(int id, string name, int power, int hits = 1)
            => new LegacySkill(id, name, 0, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, hits, 0, "검증");
        private static LegacySkill Guard(int id, int power)
            => new LegacySkill(id, "막기", 0, power, power, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 0, "검증");
    }
}
