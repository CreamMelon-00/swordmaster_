using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Sheets;

namespace TurnLimbo.Core.Tests
{
    public sealed class LegacySkillFeedbackTests
    {
        [TestCase(7, LegacySkillKind.Attack, LegacySkillProperty.Hit, true)]
        [TestCase(7, LegacySkillKind.Attack, LegacySkillProperty.Slash, false)]
        [TestCase(7, LegacySkillKind.Defence, LegacySkillProperty.Defence, false)]
        [TestCase(42, LegacySkillKind.Defence, LegacySkillProperty.Defence, true)]
        [TestCase(42, LegacySkillKind.Attack, LegacySkillProperty.Defence, false)]
        [TestCase(42, LegacySkillKind.Attack, LegacySkillProperty.Hit, false)]
        [TestCase(5, LegacySkillKind.Attack, LegacySkillProperty.Slash, true)]
        [TestCase(5, LegacySkillKind.Defence, LegacySkillProperty.Defence, false)]
        [TestCase(6, LegacySkillKind.Defence, LegacySkillProperty.Defence, true)]
        [TestCase(6, LegacySkillKind.Attack, LegacySkillProperty.Hit, false)]
        public void OpponentMatcher_UsesExistingPropertyAndKindRules(int id, LegacySkillKind kind,
            LegacySkillProperty property, bool matches)
        {
            LegacySkill skill = Skill(id, 1);
            LegacySkill opponent = Skill(900, 1, kind, property);
            Assert.That(LegacySkillConditions.HasOpponentCondition(skill), Is.True);
            Assert.That(LegacySkillConditions.MatchesOpponent(skill, opponent), Is.EqualTo(matches));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(12)]
        [TestCase(19)]
        [TestCase(17)]
        public void OpponentMatcher_DoesNotHighlightEnemiesForUnconditionalOrSelfConditions(int id)
        {
            LegacySkill skill = Skill(id, 1);
            Assert.That(LegacySkillConditions.HasOpponentCondition(skill), Is.False);
            Assert.That(LegacySkillConditions.MatchesOpponent(skill,
                Skill(900, 1, LegacySkillKind.Attack, LegacySkillProperty.Hit)), Is.False);
            Assert.That(LegacySkillConditions.MatchesOpponent(skill, Guard(901, 1)), Is.False);
        }

        [Test]
        public void OpponentMatcher_RejectsMissingSkillsAndWait()
        {
            Assert.That(LegacySkillConditions.HasOpponentCondition(null), Is.False);
            Assert.That(LegacySkillConditions.HasOpponentCondition(LegacyCommonActions.Breathe), Is.False);
            Assert.That(LegacySkillConditions.MatchesOpponent(null, Guard(900, 1)), Is.False);
            Assert.That(LegacySkillConditions.MatchesOpponent(Skill(7, 1), null), Is.False);
            Assert.That(LegacySkillConditions.MatchesOpponent(Skill(42, 1), LegacyCommonActions.Breathe), Is.False);
            Assert.That(LegacySkillConditions.MatchesSelfCondition(Skill(19, 1), null), Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void GuardFeedback_DistinguishesMatchedConditionFromPlayerOnlyActRecovery(bool playerGuards)
        {
            LegacySkill guard = Guard(7, 100);
            LegacySkill hit = Skill(900, 1, property: LegacySkillProperty.Hit);
            var duel = Duel(new[] { playerGuards ? guard : hit }, new[] { playerGuards ? hit : guard }, 1);
            Queue(duel);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            LegacySkillFeedback feedback = playerGuards ? slot.PlayerFeedback : slot.EnemyFeedback;

            Assert.That(feedback.ConditionMet, Is.True);
            Assert.That(feedback.EffectActivated, Is.EqualTo(playerGuards));
            Assert.That(feedback.ActGainGranted, Is.EqualTo(playerGuards ? 2 : 0));
            Assert.That(feedback.ResistanceRestored, Is.Zero);
            Assert.That(feedback.OpponentResistanceReduced, Is.Zero);
            Assert.That(duel.NextActGain, Is.EqualTo(playerGuards ? 5 : 3));
            Assert.That(feedback.HasBeneficialBuff, Is.False);
        }

        [TestCase(true, 0, true)]
        [TestCase(true, 50, true)]
        [TestCase(false, 0, false)]
        [TestCase(false, 50, true)]
        public void CounterFeedback_ReportsActualResistanceAndActChanges(bool playerDraws, int resistance,
            bool activated)
        {
            LegacySkill draw = Skill(42, 10);
            LegacySkill guard = Guard(900, 100);
            var duel = Duel(new[] { playerDraws ? draw : guard }, new[] { playerDraws ? guard : draw }, 1,
                playerResistance: playerDraws ? 50 : resistance, enemyResistance: playerDraws ? resistance : 50);
            Queue(duel);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            LegacySkillFeedback feedback = playerDraws ? slot.PlayerFeedback : slot.EnemyFeedback;

            Assert.That(feedback.ConditionMet, Is.True);
            Assert.That(feedback.EffectActivated, Is.EqualTo(activated));
            Assert.That(feedback.ActGainGranted, Is.EqualTo(playerDraws ? 3 : 0));
            Assert.That(feedback.ResistanceRestored, Is.Zero);
            Assert.That(feedback.OpponentResistanceReduced, Is.EqualTo(resistance > 20 ? 20 : resistance));
            Assert.That((playerDraws ? duel.Enemy : duel.Player).Resistance, Is.EqualTo(resistance > 20 ? resistance - 20 : 0));
            Assert.That(duel.NextActGain, Is.EqualTo(playerDraws ? 6 : 3));
            duel.ResolveNextHit();
            Assert.That(feedback.EffectActivated, Is.EqualTo(activated), "Feedback remains the initialization snapshot.");
            Assert.That(feedback.ActGainGranted, Is.EqualTo(playerDraws ? 3 : 0));
            Assert.That(feedback.OpponentResistanceReduced, Is.EqualTo(resistance > 20 ? 20 : resistance));
        }

        [Test]
        public void CounterFeedback_RemainsInactiveAgainstActualQueuedWait()
        {
            var duel = Duel(new[] { Skill(42, 10) }, new[] { LegacyCommonActions.Breathe }, 1);
            Queue(duel);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PlayerFeedback.ConditionMet, Is.False);
            Assert.That(slot.PlayerFeedback.EffectActivated, Is.False);
            Assert.That(duel.NextActGain, Is.EqualTo(3));
            Assert.That(duel.Enemy.Resistance, Is.EqualTo(50));
        }

        [TestCase(5, LegacySkillKind.Attack, true, 5)]
        [TestCase(5, LegacySkillKind.Attack, false, 5)]
        [TestCase(5, LegacySkillKind.Defence, true, 0)]
        [TestCase(6, LegacySkillKind.Defence, true, 8)]
        [TestCase(6, LegacySkillKind.Defence, false, 8)]
        [TestCase(6, LegacySkillKind.Attack, true, 0)]
        public void StarterTrickFeedback_AppliesDirectResistanceOnlyAgainstItsOpposingKind(
            int id, LegacySkillKind opposingKind, bool playerUsesTrick, int expectedReduction)
        {
            LegacySkill trick = Skill(id, 1);
            LegacySkill opponent = opposingKind == LegacySkillKind.Attack
                ? Skill(900, 1, LegacySkillKind.Attack, LegacySkillProperty.Slash)
                : Guard(901, 100);
            var duel = Duel(new[] { playerUsesTrick ? trick : opponent },
                new[] { playerUsesTrick ? opponent : trick }, 1, playerResistance: 10, enemyResistance: 10);
            Queue(duel);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            LegacySkillFeedback feedback = playerUsesTrick ? slot.PlayerFeedback : slot.EnemyFeedback;

            Assert.That(feedback.ConditionMet, Is.EqualTo(expectedReduction > 0));
            Assert.That(feedback.EffectActivated, Is.EqualTo(expectedReduction > 0));
            Assert.That(feedback.OpponentResistanceReduced, Is.EqualTo(expectedReduction));
            Assert.That((playerUsesTrick ? duel.Enemy : duel.Player).Resistance, Is.EqualTo(10 - expectedReduction));
            Assert.That((playerUsesTrick ? duel.Enemy : duel.Player).Health, Is.EqualTo(1000),
                "A direct resistance reduction never spills into health before the hit.");
            Assert.That(duel.NextActGain, Is.EqualTo(3));
            AssertNoGrantedBuff(feedback);
        }

        [TestCase(5)]
        [TestCase(6)]
        public void StarterTrickFeedback_CapsDirectReductionAtRemainingResistance(int id)
        {
            LegacySkill matchedOpponent = id == 5 ? Skill(900, 1) : Guard(901, 100);
            var duel = Duel(new[] { Skill(id, 1) }, new[] { matchedOpponent }, 1, enemyResistance: 3);
            Queue(duel);
            duel.Commit();
            LegacySkillFeedback feedback = duel.BeginNextSlot().PlayerFeedback;
            Assert.That(feedback.ConditionMet, Is.True);
            Assert.That(feedback.EffectActivated, Is.True);
            Assert.That(feedback.OpponentResistanceReduced, Is.EqualTo(3));
            Assert.That(duel.Enemy.Resistance, Is.Zero);
            Assert.That(duel.Enemy.Health, Is.EqualTo(1000));
        }

        [TestCase(50, 10, true, 45)]
        [TestCase(50, 2, true, 50)]
        [TestCase(50, 0, false, 50)]
        [TestCase(5, 3, false, 2)]
        [TestCase(0, 0, false, 0)]
        public void RecoveryFeedback_UsesActualCappedRoundedRestoration(int maximum, int missing,
            bool activated, int remaining)
        {
            LegacySkill recovery = Guard(19, 100);
            LegacySkill[] player = missing == 0 ? new[] { recovery } : new[] { Skill(100, 1), recovery };
            LegacySkill[] enemy = missing == 0 ? new[] { Guard(900, 100) }
                : new[] { Skill(900, missing), Guard(901, 100) };
            var duel = Duel(player, enemy, player.Length, playerResistance: maximum);
            Queue(duel, player.Length);
            duel.Commit();
            if (missing > 0) duel.ResolveNextSlot();

            Assert.That(LegacySkillConditions.HasSelfCondition(recovery), Is.True);
            Assert.That(LegacySkillConditions.MatchesSelfCondition(recovery, duel.Player), Is.EqualTo(activated));
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(duel.Player.Resistance, Is.EqualTo(remaining));
            Assert.That(slot.PlayerFeedback.ConditionMet, Is.EqualTo(activated));
            Assert.That(slot.PlayerFeedback.EffectActivated, Is.EqualTo(activated));
            Assert.That(slot.PlayerFeedback.ActGainGranted, Is.Zero);
            Assert.That(slot.PlayerFeedback.ResistanceRestored, Is.EqualTo(remaining - (maximum - missing)));
            Assert.That(slot.PlayerFeedback.OpponentResistanceReduced, Is.Zero);
            Assert.That(slot.PlayerFeedback.HasBeneficialBuff, Is.False, "Instant restoration is not a carried combat buff.");
            duel.ResolveNextHit();
            Assert.That(slot.PlayerFeedback.ConditionMet, Is.EqualTo(activated));
            Assert.That(slot.PlayerFeedback.ResistanceRestored, Is.EqualTo(remaining - (maximum - missing)));
        }

        [Test]
        public void PowerSnapshot_TracksAdditiveBuffsBeforeTickAndRemainsStableAfterLaterSlots()
        {
            var duel = Duel(new[] { Guard(9, 100), Guard(9, 100), Skill(10, 10), Skill(100, 20), Skill(101, 20), Skill(102, 20) },
                new[] { Skill(900, 1) }, 6, playerResistance: 1000, enemyResistance: 1000);
            Queue(duel, 6);
            duel.Commit();
            LegacyCurrentSlot original = duel.BeginNextSlot();
            Assert.That(original.PlayerFeedback.PowerBuffPercent, Is.Zero);
            Assert.That(original.PlayerFeedback.EffectActivated, Is.True);
            Assert.That(original.PlayerFeedback.ConditionMet, Is.False);
            CompleteSlot(duel);
            LegacyCurrentSlot secondParry = duel.BeginNextSlot();
            Assert.That(secondParry.PlayerFeedback.PowerBuffPercent, Is.EqualTo(20));
            CompleteSlot(duel);
            LegacyCurrentSlot ready = duel.BeginNextSlot();
            Assert.That(ready.PlayerFeedback.PowerBuffPercent, Is.EqualTo(40));
            CompleteSlot(duel);
            LegacyCurrentSlot combined = duel.BeginNextSlot();
            Assert.That(combined.PlayerFeedback.PowerBuffPercent, Is.EqualTo(50));
            Assert.That(combined.PlayerFeedback.HasPowerBuff, Is.True);
            Assert.That(combined.PlayerFeedback.HasBeneficialBuff, Is.True);
            Assert.That(combined.PlayerFeedback.EffectActivated, Is.False);
            Assert.That(CompleteSlot(duel).EnemyResistanceDamage, Is.EqualTo(30));
            Assert.That(duel.BeginNextSlot().PlayerFeedback.PowerBuffPercent, Is.Zero);
            CompleteSlot(duel);
            Assert.That(duel.BeginNextSlot().PlayerFeedback.PowerBuffPercent, Is.Zero);
            CompleteSlot(duel);
            Assert.That(combined.PlayerFeedback.PowerBuffPercent, Is.EqualTo(50));
            Assert.That(original.PlayerFeedback.PowerBuffPercent, Is.Zero);
        }

        [Test]
        public void EnemyPowerSnapshot_UsesEnemyBuffsAndDoesNotClaimPlayerRecovery()
        {
            var duel = Duel(new[] { Guard(100, 100) },
                new[] { Guard(9, 100), Guard(9, 100), Skill(10, 10), Skill(900, 20) }, 4);
            Queue(duel, 4);
            duel.Commit();
            for (int i = 0; i < 3; i++) duel.ResolveNextSlot();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.EnemyFeedback.PowerBuffPercent, Is.EqualTo(50));
            Assert.That(slot.EnemyFeedback.HasPowerBuff, Is.True);
            Assert.That(slot.PlayerFeedback.PowerBuffPercent, Is.Zero);
            Assert.That(duel.NextActGain, Is.EqualTo(3));
        }

        [Test]
        public void FollowingGuard_ReportsItsActualPowerBoost()
        {
            var duel = Duel(new[] { Skill(10, 1), Guard(100, 10) }, new[] { Skill(900, 13) }, 2);
            Queue(duel, 2);
            duel.Commit();
            duel.ResolveNextSlot();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PlayerFeedback.PowerBuffPercent, Is.EqualTo(30));
            Assert.That(slot.PlayerFeedback.HasPowerBuff, Is.True);
            Assert.That(CompleteSlot(duel).PlayerHealthDamage, Is.Zero);
        }

        [Test]
        public void ProtectionSnapshot_ReportsActualReductionOnFollowingGuard()
        {
            var duel = Duel(new[] { Guard(8, 100), Guard(100, 10) }, new[] { Skill(900, 20) }, 2);
            Queue(duel, 2);
            duel.Commit();
            LegacyCurrentSlot opening = duel.BeginNextSlot();
            Assert.That(opening.PlayerFeedback.ProtectionBuffPercent, Is.Zero);
            Assert.That(opening.PlayerFeedback.EffectActivated, Is.True);
            Assert.That(opening.PlayerFeedback.HasBeneficialBuff, Is.False);
            CompleteSlot(duel);

            LegacyCurrentSlot protectedSlot = duel.BeginNextSlot();
            Assert.That(protectedSlot.PlayerFeedback.ProtectionBuffPercent, Is.EqualTo(25));
            Assert.That(protectedSlot.PlayerFeedback.PowerBuffPercent, Is.Zero);
            Assert.That(protectedSlot.PlayerFeedback.HasBeneficialBuff, Is.True);
            Assert.That(CompleteSlot(duel).PlayerHealthDamage, Is.EqualTo(8));
        }

        [Test]
        public void ProtectionSnapshot_CapsFullProtectionAndNetsForwardVulnerability()
        {
            // The shipped W guard protects only one following slot. A temporary valid sheet with longer
            // buffs exercises the interpreter's cap and additive vulnerability without changing that design.
            var rows = CsvTable.Read(LegacySkillSheet.Write(LegacySkillDefinitions.Table))
                .Select(record => record.Fields.ToArray()).ToList();
            string[] guardRow = rows.Single(row => row[0] == "8");
            guardRow[LegacySkillSheet.Headers.ToList().IndexOf(LegacySkillSheet.Column.ProtectionBuff)] = "60%";
            guardRow[LegacySkillSheet.Headers.ToList().IndexOf(LegacySkillSheet.Column.BuffSlots)] = "3";
            string sheet = CsvTable.Write(rows);
            try
            {
                LegacySkillDefinitions.Install(() => sheet);
                LegacySkill protect = Guard(8, 100);
                var duel = Duel(new[] { protect, protect, Skill(12, 1), Skill(100, 1) },
                    new[] { Skill(900, 10) }, 4, playerResistance: 1000);
                Queue(duel, 4);
                duel.Commit();
                duel.ResolveNextSlot();
                duel.ResolveNextSlot();
                LegacyCurrentSlot fullyProtected = duel.BeginNextSlot();
                Assert.That(fullyProtected.PlayerFeedback.ProtectionBuffPercent, Is.EqualTo(100));
                Assert.That(CompleteSlot(duel).PlayerResistanceDamage, Is.Zero);
                LegacyCurrentSlot vulnerable = duel.BeginNextSlot();
                Assert.That(vulnerable.PlayerFeedback.ProtectionBuffPercent, Is.EqualTo(70));
                Assert.That(vulnerable.PlayerFeedback.HasBeneficialBuff, Is.True);
                Assert.That(CompleteSlot(duel).PlayerResistanceDamage, Is.EqualTo(3));
            }
            finally
            {
                LegacySkillDefinitions.Install(null);
            }
        }

        [Test]
        public void ForwardVulnerability_DoesNotReportABeneficialCombatBuff()
        {
            var duel = Duel(new[] { Skill(12, 1), Skill(100, 1) }, new[] { Skill(900, 2) }, 2);
            Queue(duel, 2);
            duel.Commit();
            duel.ResolveNextSlot();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PlayerFeedback.ProtectionBuffPercent, Is.Zero);
            Assert.That(slot.PlayerFeedback.PowerBuffPercent, Is.Zero);
            Assert.That(slot.PlayerFeedback.HasBeneficialBuff, Is.False);
            Assert.That(CompleteSlot(duel).PlayerResistanceDamage, Is.EqualTo(3));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void WaitAndMissingSlots_DoNotShowBuffParticlesOrActivatedEffects(bool wait)
        {
            var duel = Duel(new[] { Guard(8, 100), Skill(10, 1) }, new[] { Skill(900, 1) }, 3);
            Queue(duel, 2);
            if (wait) Assert.That(duel.TryQueueBreath(), Is.True);
            duel.Commit();
            duel.ResolveNextSlot();
            duel.ResolveNextSlot();
            LegacyCurrentSlot slot = duel.BeginNextSlot();

            Assert.That(slot.PlayerFeedback.ConditionMet, Is.False);
            Assert.That(slot.PlayerFeedback.EffectActivated, Is.False);
            Assert.That(slot.PlayerFeedback.PowerBuffPercent, Is.Zero);
            Assert.That(slot.PlayerFeedback.HasPowerBuff, Is.False);
            Assert.That(slot.PlayerFeedback.HasBeneficialBuff, Is.False);
            Assert.That(slot.PlayerFeedback.ProtectionBuffPercent, Is.Zero,
                "The shipped W guard protects only the immediately following slot.");
            AssertNoGrantedBuff(slot.PlayerFeedback);
        }

        [TestCase(8, true, 0, 25, 1)]
        [TestCase(8, false, 0, 25, 1)]
        [TestCase(9, true, 20, 0, 2)]
        [TestCase(9, false, 20, 0, 2)]
        [TestCase(10, true, 30, 0, 1)]
        [TestCase(10, false, 30, 0, 1)]
        public void GrantedBuffFeedback_ReportsActualAcquisitionThenFollowingApplicationForEitherFighter(
            int id, bool enemyGrants, int power, int protection, int slots)
        {
            LegacySkill granting = id == 8 || id == 9 ? Guard(id, 20) : Skill(id, 20);
            LegacySkill[] grantingSequence = { granting, Skill(900, 20), Skill(901, 20) };
            LegacySkill[] opposing = { Guard(800, 100) };
            var duel = Duel(enemyGrants ? opposing : grantingSequence,
                enemyGrants ? grantingSequence : opposing, 3, playerResistance: 1000, enemyResistance: 1000);
            Queue(duel, 3);
            duel.Commit();
            LegacyCurrentSlot initialSlot = duel.BeginNextSlot();
            LegacySkillFeedback granted = enemyGrants ? initialSlot.EnemyFeedback : initialSlot.PlayerFeedback;

            Assert.That(granted.EffectActivated, Is.True);
            Assert.That(granted.PowerBuffPercent, Is.Zero, "A newly granted buff does not alter its own power snapshot.");
            Assert.That(granted.ProtectionBuffPercent, Is.Zero);
            Assert.That(granted.HasBeneficialBuff, Is.False);
            Assert.That(granted.GrantedPowerBuffPercent, Is.EqualTo(power));
            Assert.That(granted.GrantedProtectionBuffPercent, Is.EqualTo(protection));
            Assert.That(granted.GrantedBuffSlots, Is.EqualTo(slots));
            Assert.That(granted.HasGrantedBeneficialBuff, Is.True);
            CompleteSlot(duel);

            LegacyCurrentSlot followingSlot = duel.BeginNextSlot();
            LegacySkillFeedback applied = enemyGrants ? followingSlot.EnemyFeedback : followingSlot.PlayerFeedback;
            Assert.That(applied.PowerBuffPercent, Is.EqualTo(power));
            Assert.That(applied.ProtectionBuffPercent, Is.EqualTo(protection));
            Assert.That(applied.HasBeneficialBuff, Is.True);
            AssertNoGrantedBuff(applied);
            CompleteSlot(duel);

            LegacyCurrentSlot lastSlot = duel.BeginNextSlot();
            LegacySkillFeedback last = enemyGrants ? lastSlot.EnemyFeedback : lastSlot.PlayerFeedback;
            Assert.That(last.PowerBuffPercent, Is.EqualTo(slots == 1 ? 0 : power));
            Assert.That(last.ProtectionBuffPercent, Is.EqualTo(slots == 1 ? 0 : protection));
            CompleteSlot(duel);
            Assert.That(granted.GrantedPowerBuffPercent, Is.EqualTo(power));
            Assert.That(granted.GrantedProtectionBuffPercent, Is.EqualTo(protection));
            Assert.That(granted.GrantedBuffSlots, Is.EqualTo(slots), "Historical grant metadata never ticks down with its live buff.");
        }

        [Test]
        public void EnemyProtectionFeedback_SeparatesAnExistingBuffFromTheNewlyGrantedStack()
        {
            var duel = Duel(new[] { Guard(100, 100) }, new[] { Guard(8, 20), Guard(8, 20), Skill(900, 1) }, 3);
            Queue(duel, 3);
            duel.Commit();
            duel.ResolveNextSlot();
            LegacyCurrentSlot second = duel.BeginNextSlot();

            Assert.That(second.EnemyFeedback.ProtectionBuffPercent, Is.EqualTo(25));
            Assert.That(second.EnemyFeedback.GrantedProtectionBuffPercent, Is.EqualTo(25));
            Assert.That(second.EnemyFeedback.HasBeneficialBuff, Is.True);
            Assert.That(second.EnemyFeedback.HasGrantedBeneficialBuff, Is.True);
            CompleteSlot(duel);
            LegacyCurrentSlot following = duel.BeginNextSlot();
            Assert.That(following.EnemyFeedback.ProtectionBuffPercent, Is.EqualTo(25));
            AssertNoGrantedBuff(following.EnemyFeedback);
            Assert.That(second.EnemyFeedback.GrantedBuffSlots, Is.EqualTo(1));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ForwardGrantFeedback_RecordsVulnerabilityWithoutCallingItABeneficialBuff(bool enemyGrants)
        {
            LegacySkill[] granting = { Skill(12, 1), Skill(900, 1) };
            LegacySkill[] opposing = { Guard(100, 100) };
            var duel = Duel(enemyGrants ? opposing : granting, enemyGrants ? granting : opposing, 2);
            Queue(duel, 2);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            LegacySkillFeedback granted = enemyGrants ? slot.EnemyFeedback : slot.PlayerFeedback;

            Assert.That(granted.EffectActivated, Is.True);
            Assert.That(granted.GrantedPowerBuffPercent, Is.Zero);
            Assert.That(granted.GrantedProtectionBuffPercent, Is.EqualTo(-50));
            Assert.That(granted.GrantedBuffSlots, Is.EqualTo(1));
            Assert.That(granted.HasGrantedBeneficialBuff, Is.False);
            CompleteSlot(duel);
            LegacyCurrentSlot following = duel.BeginNextSlot();
            LegacySkillFeedback applied = enemyGrants ? following.EnemyFeedback : following.PlayerFeedback;
            Assert.That(applied.ProtectionBuffPercent, Is.Zero);
            Assert.That(applied.HasBeneficialBuff, Is.False);
            AssertNoGrantedBuff(applied);
            Assert.That(granted.GrantedProtectionBuffPercent, Is.EqualTo(-50));
        }

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(19)]
        [TestCase(42)]
        public void NonBuffEffects_DoNotInventGrantMetadata(int id)
        {
            LegacySkill skill = id == 7 || id == 19 ? Guard(id, 100) : Skill(id, 10);
            var duel = Duel(new[] { skill }, new[] { Guard(900, 100) }, 1);
            Queue(duel);
            duel.Commit();
            AssertNoGrantedBuff(duel.BeginNextSlot().PlayerFeedback);
        }

        [Test]
        public void BasePowerIncreaseAlone_DoesNotReportCombatBuffs()
        {
            var upgraded = new LegacySkill(100, "강화 +1", 0, 22, 22, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, 1, 0, "");
            var duel = Duel(new[] { upgraded }, new[] { Skill(900, 1) }, 1);
            Queue(duel);
            duel.Commit();
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PlayerFeedback.PowerBuffPercent, Is.Zero);
            Assert.That(slot.PlayerFeedback.ProtectionBuffPercent, Is.Zero);
            Assert.That(slot.PlayerFeedback.HasBeneficialBuff, Is.False);
            Assert.That(CompleteSlot(duel).EnemyResistanceDamage, Is.EqualTo(22));
        }

        [Test]
        public void NextTurnAndReset_ClearOnlyNewSnapshotsWithoutMutatingHistoricalFeedback()
        {
            var duel = Duel(new[] { Skill(10, 1), Skill(100, 20) }, new[] { Skill(900, 1) }, 2,
                playerResistance: 1000, enemyResistance: 1000);
            Queue(duel, 2);
            duel.Commit();
            duel.ResolveNextSlot();
            LegacyCurrentSlot boosted = duel.BeginNextSlot();
            Assert.That(boosted.PlayerFeedback.PowerBuffPercent, Is.EqualTo(30));
            CompleteSlot(duel);
            duel.BeginNextTurn();
            Assert.That(duel.CurrentSlot, Is.Null);
            Queue(duel);
            duel.Commit();
            Assert.That(duel.BeginNextSlot().PlayerFeedback.PowerBuffPercent, Is.Zero);
            duel.Reset();
            Assert.That(duel.CurrentSlot, Is.Null);
            Assert.That(boosted.PlayerFeedback.PowerBuffPercent, Is.EqualTo(30));
        }

        private static LegacySkill Skill(int id, int power, LegacySkillKind kind = LegacySkillKind.Attack,
            LegacySkillProperty property = LegacySkillProperty.Slash)
            => new LegacySkill(id, "feedback test", 0, power, power, kind, property, 1, 0, "");

        private static void AssertNoGrantedBuff(LegacySkillFeedback feedback)
        {
            Assert.That(feedback.GrantedPowerBuffPercent, Is.Zero);
            Assert.That(feedback.GrantedProtectionBuffPercent, Is.Zero);
            Assert.That(feedback.GrantedBuffSlots, Is.Zero);
            Assert.That(feedback.HasGrantedBeneficialBuff, Is.False);
        }

        private static LegacySkill Guard(int id, int power)
            => Skill(id, power, LegacySkillKind.Defence, LegacySkillProperty.Defence);

        private static LegacyQueuedDuel Duel(LegacySkill[] player, LegacySkill[] enemy, int count,
            int playerResistance = 50, int enemyResistance = 50)
            => new LegacyQueuedDuel(1000, playerResistance, 1000, enemyResistance, player, enemy, new[] { count }, 1);

        private static void Queue(LegacyQueuedDuel duel, int count = 1)
        {
            for (int i = 0; i < count; i++) Assert.That(duel.TryQueueLane(0), Is.True);
        }

        private static LegacySlotResult CompleteSlot(LegacyQueuedDuel duel)
        {
            while (!duel.IsCurrentSlotResolved) duel.ResolveNextHit();
            return duel.CompleteCurrentSlot();
        }
    }
}
