using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Sheets;
using Column = TurnLimbo.Runtime.LegacyCombat.LegacySkillSheet.Column;

namespace TurnLimbo.Core.Tests
{
    /// <summary>상대 상태 조건 (the sheet's opponent-state condition) and 조건 피해 배율 (its damage multiplier) in combat,
    /// through 이아's 베네디체레 (501: a 5~10 guard that recovers a quarter of its maximum resistance while the opponent is
    /// broken) and 프레디카레 (502: one 24~32 slash, its health damage doubled on an opponent at 30% health or less), and
    /// through rows a test sheet changes.</summary>
    public sealed class OpponentStateConditionTests
    {
        private const int Laudare = 500, Benedicere = 501, Praedicare = 502, Reprise = 19, DiagonalCut = 15;

        [Test]
        public void TheMottoRows_HaveTheirStateConditions_AndOthersHaveNone()
        {
            LegacySkillEffect benedicere = LegacySkillDefinitions.Find(Benedicere).Effect;
            Assert.That((benedicere.OpponentState, benedicere.OpponentHealthPercent, benedicere.ResistanceRecoveryPercent,
                benedicere.ConditionalDamagePercent), Is.EqualTo((LegacyOpponentState.Broken, 0, 25, 0)));
            LegacySkillEffect praedicare = LegacySkillDefinitions.Find(Praedicare).Effect;
            Assert.That((praedicare.OpponentState, praedicare.OpponentHealthPercent, praedicare.ResistanceRecoveryPercent,
                praedicare.ConditionalDamagePercent), Is.EqualTo((LegacyOpponentState.HealthAtMost, 30, 0, 200)));
            Assert.That(LegacySkillConditions.HasOpponentStateCondition(Skill(Benedicere)), Is.True);
            Assert.That(LegacySkillConditions.HasOpponentStateCondition(Skill(Praedicare)), Is.True);
            foreach (LegacySkillDefinition definition in LegacySkillDefinitions.All.Where(d => d.Skill.Id < Benedicere))
            {
                Assert.That(definition.Effect.HasOpponentStateCondition, Is.False, "skill " + definition.Skill.Id);
                Assert.That(definition.Effect.ConditionalDamagePercent, Is.Zero, "skill " + definition.Skill.Id);
            }
            Assert.That(LegacySkillConditions.HasOpponentStateCondition(null), Is.False);
            Assert.That(LegacySkillConditions.HasOpponentStateCondition(LegacyCommonActions.Breathe), Is.False);
            Assert.That(benedicere.OpponentStateHolds(null), Is.False);
            Assert.That(LegacySkillEffect.None.OpponentStateHolds(new LegacyQueuedDuel().Player), Is.False, "No condition holds nothing.");
        }

        [Test]
        public void Benedicere_RecoversAQuarter_OnlyWhileTheOpponentIsBroken()
        {
            // Slot 0: 라우다레 breaks the player while the player's 20-power slash wears the enemy's resistance to 30.
            LegacyQueuedDuel broken = AfterFirstSlot(new[] { Skill(Laudare), Skill(Benedicere) }, Probe(900, 20));
            Assert.That(broken.Player.IsResistanceBroken, Is.True);
            Assert.That(broken.Enemy.Resistance, Is.EqualTo(30));
            Assert.That(LegacySkillConditions.MatchesOpponentState(Skill(Benedicere), broken.Enemy, broken.Player), Is.True);
            LegacyCurrentSlot recovery = broken.BeginNextSlot();
            Assert.That(recovery.EnemyFeedback.ResistanceRestored, Is.EqualTo(12), "A quarter of 50 is 12.5, rounded to even.");
            Assert.That(recovery.EnemyFeedback.ConditionMet && recovery.EnemyFeedback.EffectActivated, Is.True);
            Assert.That(recovery.EnemyFeedback.ConditionalDamagePercent, Is.Zero);
            Assert.That(broken.Enemy.Resistance, Is.EqualTo(42));
            Assert.That(broken.Player.IsResistanceBroken, Is.True, "Only her own resistance comes back.");

            // The same wear from a slash that breaks nothing: the player stays whole, so nothing is restored.
            LegacyQueuedDuel whole = AfterFirstSlot(new[] { Probe(901, 20), Skill(Benedicere) }, Probe(900, 20));
            Assert.That(whole.Player.IsResistanceBroken, Is.False);
            Assert.That(whole.Enemy.Resistance, Is.EqualTo(30));
            Assert.That(LegacySkillConditions.MatchesOpponentState(Skill(Benedicere), whole.Enemy, whole.Player), Is.False);
            LegacyCurrentSlot unmatched = whole.BeginNextSlot();
            Assert.That(unmatched.EnemyFeedback.ResistanceRestored, Is.Zero);
            Assert.That(unmatched.EnemyFeedback.ConditionMet || unmatched.EnemyFeedback.EffectActivated, Is.False);
            Assert.That(whole.Enemy.Resistance, Is.EqualTo(30));

            // Broken player, but her resistance is full: the condition holds and there is nothing to restore.
            LegacyQueuedDuel full = AfterFirstSlot(new[] { Skill(Laudare), Skill(Benedicere) });
            Assert.That(full.Player.IsResistanceBroken && full.Enemy.Resistance == 50, Is.True);
            Assert.That(Skill(Benedicere).Kind == LegacySkillKind.Defence &&
                LegacySkillDefinitions.Find(Benedicere).Effect.OpponentStateHolds(full.Player), Is.True);
            Assert.That(LegacySkillConditions.MatchesOpponentState(Skill(Benedicere), full.Enemy, full.Player), Is.False,
                "No effect would act, as for 르프리즈 at full resistance.");
            LegacyCurrentSlot nothing = full.BeginNextSlot();
            Assert.That(nothing.EnemyFeedback.ResistanceRestored, Is.Zero);
            Assert.That(nothing.EnemyFeedback.ConditionMet || nothing.EnemyFeedback.EffectActivated, Is.False);
        }

        [Test]
        public void Benedicere_ChecksThePlayer_AfterThePlayersOwnInitialEffects()
        {
            // Slot 1: the broken player's 르프리즈 restores 5 first, so 베네디체레 then meets a whole player.
            LegacyQueuedDuel duel = AfterFirstSlot(new[] { Skill(Laudare), Skill(Benedicere) }, Probe(900, 20), Lane0(Skill(Reprise)));
            Assert.That(duel.Player.IsResistanceBroken, Is.True);
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.PlayerFeedback.ResistanceRestored, Is.EqualTo(5));
            Assert.That(slot.EnemyFeedback.ResistanceRestored, Is.Zero);
            Assert.That(slot.EnemyFeedback.ConditionMet, Is.False);
            Assert.That(duel.Enemy.Resistance, Is.EqualTo(30));
        }

        [TestCase(300, true)]
        [TestCase(299, true)]
        [TestCase(301, false)]
        [TestCase(1000, false)]
        public void Praedicare_DoublesItsHit_OnATargetAtThirtyPercentOrLess(int health, bool doubled)
        {
            // Slot 0 lands 1000 - health on the player's body; slot 1 is 프레디카레 against an empty slot.
            LegacyQueuedDuel duel = health < 1000
                ? AfterFirstSlot(new[] { Probe(900, 1000 - health), Skill(Praedicare) })
                : Committed(new[] { Skill(Praedicare) });
            Assert.That(duel.Player.Health, Is.EqualTo(health));
            Assert.That(duel.Player.IsResistanceBroken, Is.False);
            Assert.That(LegacySkillConditions.MatchesOpponentState(Skill(Praedicare), duel.Enemy, duel.Player), Is.EqualTo(doubled));
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.EnemyFeedback.ConditionalDamagePercent, Is.EqualTo(doubled ? 200 : 0));
            Assert.That(slot.EnemyFeedback.ConditionMet, Is.EqualTo(doubled));
            Assert.That(slot.EnemyFeedback.EffectActivated, Is.EqualTo(doubled));
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.EnemyAttackConditionMet, Is.EqualTo(doubled));
            Assert.That(hit.PlayerAttackConditionMet, Is.False);
            Assert.That(hit.PlayerPushPower, Is.InRange(24, 32), "The knockback keeps the plain value.");
            Assert.That(hit.PlayerHealthDamage, Is.EqualTo(hit.PlayerPushPower * (doubled ? 2 : 1)));
        }

        [TestCase(16, true)]
        [TestCase(17, false)]
        public void HealthCondition_ComparesExactly_WithoutRoundingThePercent(int health, bool holds)
        {
            // 30% of 55 is 16.5: 16 is at or below it, 17 is not.
            var duel = new LegacyQueuedDuel(55, 10, 100, 10, new[] { Probe(999, 1) },
                new EnemyScript(new[] { new[] { Probe(900, 55 - health) } }), 1);
            duel.Commit();
            duel.ResolveNextSlot();
            Assert.That(duel.Player.Health, Is.EqualTo(health));
            Assert.That(LegacySkillDefinitions.Find(Praedicare).Effect.OpponentStateHolds(duel.Player), Is.EqualTo(holds));
        }

        [Test]
        public void TheMultiplier_ComesLast_AfterTheBreakDoublingAndTheBrokenTargetBonus()
        {
            List<string[]> rows = Rows();
            Set(rows, Praedicare, Column.BrokenTargetDamage, "25%");
            Installed(rows, () =>
            {
                // Slot 0 breaks the player in a clash (50 against 50 leaves no overflow); slot 1 lands 350, doubled on
                // the broken player, so slot 2's 프레디카레 meets a broken player at 300 of 1000.
                LegacyQueuedDuel duel = AfterFirstSlot(new[] { Probe(900, 50), Probe(901, 350), Skill(Praedicare) },
                    Probe(902, 1), null, Probe(902, 1));
                Assert.That(duel.Player.IsResistanceBroken, Is.True);
                duel.ResolveNextSlot();
                Assert.That(duel.Player.Health, Is.EqualTo(300));
                Assert.That(duel.BeginNextSlot().EnemyFeedback.ConditionalDamagePercent, Is.EqualTo(200));
                LegacyHitResult hit = duel.ResolveNextHit();
                int power = hit.PlayerPushPower;
                int bonus = (int)Math.Round(power * 2 * 1.25, MidpointRounding.ToEven);
                Assert.That(hit.PlayerHealthDamage, Is.EqualTo(bonus * 2), "×2 broken, +25%, then ×2.");
                Assert.That(hit.EnemyAttackConditionMet, Is.True);
            });
        }

        [Test]
        public void ABreakingHit_TakesTheMultiplierOnItsOverflow_WhenTheConditionHeldBeforeIt()
        {
            // Slot 0 wears the player's resistance to 10 in a clash, slot 1 lands 700 on the breathing player, and
            // slot 2's 프레디카레 clashes with the player's slash: 10 goes to resistance, the rest overflows doubled.
            LegacyQueuedDuel duel = AfterFirstSlot(new[] { Probe(900, 40), Probe(901, 700), Skill(Praedicare) },
                Probe(902, 1), null, Probe(902, 1));
            Assert.That(duel.Player.Resistance, Is.EqualTo(10));
            duel.ResolveNextSlot();
            Assert.That(duel.Player.Health, Is.EqualTo(300));
            Assert.That(duel.Player.IsResistanceBroken, Is.False);
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.EnemyFeedback.ConditionalDamagePercent, Is.Zero, "A clash against whole resistance arms nothing…");
            Assert.That(slot.EnemyFeedback.ConditionMet || slot.EnemyFeedback.EffectActivated, Is.False);
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.EnemyAttackConditionMet, Is.True, "…but the overflow is still multiplied, without a badge.");
            Assert.That(hit.PlayerResistanceDamage, Is.EqualTo(10));
            Assert.That(hit.PlayerPushPower, Is.InRange(14, 22), "The overflow, 24~32 less the 10 resistance took.");
            Assert.That(hit.PlayerHealthDamage, Is.EqualTo(2 * hit.PlayerPushPower), "Not yet broken before the hit, so only the multiplier.");
            Assert.That(duel.Player.IsResistanceBroken, Is.True);
        }

        [Test]
        public void AResistanceOnlyHit_DealsNoHealth_EvenWhenTheConditionHolds()
        {
            // A 1000-resistance player at 300 health clashes with 프레디카레: all of it goes to resistance, undoubled.
            LegacyQueuedDuel duel = AfterFirstSlot(new[] { Probe(900, 700), Skill(Praedicare) }, 1000, null, Probe(902, 1));
            Assert.That(duel.Player.Health, Is.EqualTo(300));
            Assert.That(LegacySkillConditions.MatchesOpponentState(Skill(Praedicare), duel.Enemy, duel.Player), Is.True,
                "The preview reads the condition only.");
            LegacyCurrentSlot slot = duel.BeginNextSlot();
            Assert.That(slot.EnemyFeedback.ConditionalDamagePercent, Is.Zero, "The clash reaches no health, so nothing is armed.");
            Assert.That(slot.EnemyFeedback.ConditionMet, Is.False);
            Assert.That(slot.EnemyFeedback.EffectActivated, Is.False);
            LegacyHitResult hit = duel.ResolveNextHit();
            Assert.That(hit.EnemyAttackConditionMet, Is.True, "The condition held as the hit landed…");
            Assert.That(hit.PlayerHealthDamage, Is.Zero, "…but there was no health damage to multiply.");
            Assert.That(hit.PlayerResistanceDamage, Is.InRange(24, 32));
        }

        [TestCase(28, false)]
        [TestCase(27, true)]
        public void AGuardThatAbsorbsTheStrike_ArmsNothing_WhileOneThatLetsItThrough_ArmsTheMultiplier(int guard, bool armed)
        {
            // 프레디카레 fixed at 28 against a player at 300 of 1000 who guards: the guard takes its power from the hit.
            List<string[]> rows = Rows();
            Set(rows, Praedicare, Column.MinPower, "28");
            Set(rows, Praedicare, Column.MaxPower, "28");
            Installed(rows, () =>
            {
                LegacyQueuedDuel duel = AfterFirstSlot(new[] { Probe(900, 700), Skill(Praedicare) }, null, Guard(903, guard));
                Assert.That(duel.Player.Health, Is.EqualTo(300));
                Assert.That(duel.Player.IsResistanceBroken, Is.False);
                LegacyCurrentSlot slot = duel.BeginNextSlot();
                Assert.That(slot.EnemyFeedback.ConditionalDamagePercent, Is.EqualTo(armed ? 200 : 0));
                Assert.That(slot.EnemyFeedback.ConditionMet, Is.EqualTo(armed));
                Assert.That(slot.EnemyFeedback.EffectActivated, Is.EqualTo(armed));
                LegacyHitResult hit = duel.ResolveNextHit();
                Assert.That(hit.EnemyAttackConditionMet, Is.True, "The condition held as the hit landed either way.");
                Assert.That(hit.PlayerHealthDamage, Is.EqualTo(armed ? 2 : 0), "28 less the guard, then doubled.");
            });
        }

        [Test]
        public void AMultiHitAttack_MeetsTheConditionPartway_AndOnlyLaterHitsAreMultiplied()
        {
            // 사선베기 (two hits of 7~10 / 2) made to double at 50% health or less, against a player at 501 of 1000.
            List<string[]> rows = Rows();
            Set(rows, DiagonalCut, Column.OpponentState, "체력 50% 이하");
            Set(rows, DiagonalCut, Column.ConditionalDamage, "200%");
            Installed(rows, () =>
            {
                LegacyQueuedDuel duel = AfterFirstSlot(new[] { Probe(900, 499), Skill(DiagonalCut) });
                Assert.That(duel.Player.Health, Is.EqualTo(501));
                LegacyCurrentSlot slot = duel.BeginNextSlot();
                Assert.That(slot.EnemyFeedback.ConditionalDamagePercent, Is.Zero, "Not yet at the slot's start.");
                Assert.That(slot.EnemyFeedback.ConditionMet, Is.False);
                LegacyHitResult first = duel.ResolveNextHit();
                LegacyHitResult second = duel.ResolveNextHit();
                Assert.That(first.EnemyAttackConditionMet, Is.False);
                Assert.That(first.PlayerHealthDamage, Is.EqualTo(first.PlayerPushPower));
                Assert.That(second.EnemyAttackConditionMet, Is.True, "The first hit took the player to 50% or less.");
                Assert.That(second.PlayerHealthDamage, Is.EqualTo(2 * second.PlayerPushPower));
            });
        }

        [Test]
        public void ThePlayersMultiplier_IsArmedOnlyIfItStillHolds_AfterTheEnemysInitialEffects()
        {
            // 프레디카레 made to double on a broken target, used by the player against an enemy broken in slot 0.
            List<string[]> rows = Rows();
            Set(rows, Praedicare, Column.OpponentState, "붕괴");
            Installed(rows, () =>
            {
                // The enemy's 르프리즈 restores 5 at the slot's start, so the strike meets a whole enemy.
                LegacyQueuedDuel recovered = AfterFirstSlot(new[] { Probe(900, 1), Skill(Reprise) }, Probe(901, 50), Skill(Praedicare));
                Assert.That(recovered.Enemy.IsResistanceBroken, Is.True);
                LegacyCurrentSlot slot = recovered.BeginNextSlot();
                Assert.That(slot.EnemyFeedback.ResistanceRestored, Is.EqualTo(5));
                Assert.That(slot.PlayerFeedback.ConditionalDamagePercent, Is.Zero);
                Assert.That(slot.PlayerFeedback.ConditionMet || slot.PlayerFeedback.EffectActivated, Is.False);
                Assert.That(recovered.ResolveNextHit().PlayerAttackConditionMet, Is.False);

                // A plain guard leaves the enemy broken: armed from the start, and the hit is multiplied.
                LegacyQueuedDuel stillBroken = AfterFirstSlot(new[] { Probe(900, 1), Guard(903, 4) }, Probe(901, 50), Skill(Praedicare));
                LegacyCurrentSlot armed = stillBroken.BeginNextSlot();
                Assert.That(armed.PlayerFeedback.ConditionalDamagePercent, Is.EqualTo(200));
                Assert.That(armed.PlayerFeedback.ConditionMet && armed.PlayerFeedback.EffectActivated, Is.True);
                Assert.That(armed.EnemyFeedback.ConditionalDamagePercent, Is.Zero);
                LegacyHitResult hit = stillBroken.ResolveNextHit();
                Assert.That(hit.PlayerAttackConditionMet, Is.True);
                Assert.That(hit.EnemyHealthDamage, Is.EqualTo(hit.EnemyPushPower * 4), "Broken ×2, then ×2.");
            });
        }

        private static LegacySkill Skill(int id) => LegacySkillDefinitions.Skill(id);

        private static LegacyQueuedDuel AfterFirstSlot(LegacySkill[] enemy, params LegacySkill[] player)
            => AfterFirstSlot(enemy, 50, player);

        /// <summary>A 1000-health player queuing <paramref name="player"/> in order (null breathes; the skills sit in lane Q)
        /// against a 1000/50 enemy playing <paramref name="enemy"/> every turn, committed with the first slot played.</summary>
        private static LegacyQueuedDuel AfterFirstSlot(LegacySkill[] enemy, int playerResistance, params LegacySkill[] player)
        {
            LegacyQueuedDuel duel = Committed(enemy, playerResistance, player);
            duel.ResolveNextSlot();
            return duel;
        }

        private static LegacyQueuedDuel Committed(LegacySkill[] enemy, params LegacySkill[] player) => Committed(enemy, 50, player);

        private static LegacyQueuedDuel Committed(LegacySkill[] enemy, int playerResistance, params LegacySkill[] player)
        {
            // The lane holds each skill once in queue order, so queuing its front walks the list (a lone skill repeats).
            LegacySkill[] lane = player.Where(skill => skill != null).Distinct().Select(Lane0).ToArray();
            var duel = new LegacyQueuedDuel(1000, playerResistance, 1000, 50, lane.Length > 0 ? lane : new[] { Probe(999, 1) },
                new EnemyScript(new[] { enemy }), 7, features: CombatFeature.All);
            foreach (LegacySkill skill in player)
            {
                Assert.That(skill == null ? duel.TryQueueBreath() : duel.TryQueueLane(0), Is.True);
                if (skill != null) Assert.That(duel.PlayerQueue.Last().Id, Is.EqualTo(skill.Id));
            }
            duel.Commit();
            return duel;
        }

        private static LegacySkill Lane0(LegacySkill skill) => skill.LaneIndex == 0 ? skill
            : new LegacySkill(skill.Id, skill.Name, skill.Cost, skill.MinPower, skill.MaxPower, skill.Kind, skill.Property,
                skill.AttackCount, 0, skill.Description, skill.AnimationName, skill.IconId);

        private static List<string[]> Rows()
            => CsvTable.Read(LegacySkillSheet.Write(LegacySkillDefinitions.Table)).Select(record => record.Fields.ToArray()).ToList();

        private static void Set(List<string[]> rows, int id, string header, string value)
            => rows.Single(row => row[0] == id.ToString())[LegacySkillSheet.Headers.ToList().IndexOf(header)] = value;

        private static void Installed(List<string[]> rows, Action body)
        {
            string sheet = CsvTable.Write(rows);
            try
            {
                LegacySkillDefinitions.Install(() => sheet);
                body();
            }
            finally
            {
                LegacySkillDefinitions.Install(null);
            }
        }

        private static LegacySkill Probe(int id, int power)
            => new LegacySkill(id, "probe", 0, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "");

        private static LegacySkill Guard(int id, int power)
            => new LegacySkill(id, "guard", 0, power, power, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 0, "");
    }
}
