using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    /// <summary>The queue cards' clash motion (<see cref="LegacyClashMotion"/>): how a resolving slot's two skill cards meet,
    /// read from both resolvers through the duel view, and the planning cue's next-opponent slot.</summary>
    public sealed class ClashMotionTests
    {
        private const LegacyClashAction Empty = LegacyClashAction.Empty, Breath = LegacyClashAction.Breath,
            Guard = LegacyClashAction.Guard, Attack = LegacyClashAction.Attack;
        private const float Tolerance = 1e-6f;

        [Test]
        public void AttackAgainstAttack_TheStrongerSkillPushes_TheWeakerRecoils_ByTheRelativeGap()
        {
            LegacyClash clash = Classify(Attack, 12, 1, Attack, 8, 1);
            Assert.That(clash.Kind, Is.EqualTo(LegacyClashKind.Clash));
            Assert.That(clash.Winner, Is.EqualTo(LegacyClashSide.Player));
            Assert.That((clash.PlayerMove, clash.EnemyMove), Is.EqualTo((LegacyClashMove.Push, LegacyClashMove.Recoil)));
            Assert.That(clash.PlayerStrength, Is.EqualTo(4f / 12f).Within(Tolerance), "|12 - 8| / 12");
            Assert.That(clash.EnemyStrength, Is.EqualTo(4f / 12f).Within(Tolerance));
            Assert.That(clash.PlayerAdvantage, Is.EqualTo(4f / 12f).Within(Tolerance));
            Assert.That(clash.Contact, Is.True, "The cards meet: a spark at the contact point.");

            LegacyClash lost = Classify(Attack, 8, 1, Attack, 12, 1);
            Assert.That(lost.Winner, Is.EqualTo(LegacyClashSide.Enemy));
            Assert.That((lost.PlayerMove, lost.EnemyMove), Is.EqualTo((LegacyClashMove.Recoil, LegacyClashMove.Push)));
            Assert.That(lost.PlayerAdvantage, Is.EqualTo(-4f / 12f).Within(Tolerance));
        }

        [Test]
        public void Clash_StrengthGrowsWithThePowerGap_AndStaysWithinOne()
        {
            float previous = 0f;
            for (int power = 11; power <= 60; power++)
            {
                LegacyClash clash = Classify(Attack, power, 1, Attack, 10, 1);
                Assert.That(clash.Winner, Is.EqualTo(LegacyClashSide.Player));
                Assert.That(clash.EnemyStrength, Is.GreaterThan(previous), "knock-back at " + power);
                Assert.That(clash.EnemyStrength, Is.EqualTo(clash.PlayerStrength));
                Assert.That(clash.EnemyStrength, Is.GreaterThan(0f).And.LessThan(1f));
                Assert.That(clash.PlayerAdvantage, Is.EqualTo(clash.PlayerStrength));
                previous = clash.EnemyStrength;
            }
            previous = 0f;
            for (int power = 9; power >= 1; power--)
            {
                LegacyClash clash = Classify(Attack, power, 1, Attack, 10, 1);
                Assert.That(clash.Winner, Is.EqualTo(LegacyClashSide.Enemy));
                Assert.That(clash.PlayerStrength, Is.GreaterThan(previous), "the player's knock-back at " + power);
                previous = clash.PlayerStrength;
            }
            Assert.That(Classify(Attack, 0, 1, Attack, 5, 1).PlayerStrength, Is.EqualTo(1f), "A powerless attack is wholly beaten.");
        }

        [Test]
        public void Clash_EvenPowersBounceBothEqually_AndTheTieIsSymmetric()
        {
            LegacyClash clash = Classify(Attack, 9, 1, Attack, 9, 1);
            Assert.That(clash.Kind, Is.EqualTo(LegacyClashKind.Clash));
            Assert.That(clash.Winner, Is.EqualTo(LegacyClashSide.None));
            Assert.That((clash.PlayerMove, clash.EnemyMove), Is.EqualTo((LegacyClashMove.Bounce, LegacyClashMove.Bounce)));
            Assert.That(clash.PlayerStrength, Is.EqualTo(clash.EnemyStrength));
            Assert.That(clash.PlayerAdvantage, Is.Zero);
            Assert.That(clash.Contact, Is.True);
            LegacyClashInput input = In(Attack, 9, 1, Attack, 9, 1);
            Assert.That(LegacyClashMotion.Classify(input.Mirrored()), Is.EqualTo(clash));
            Assert.That(clash.Mirrored(), Is.EqualTo(clash));
            Assert.That(clash.Mirrored().GetHashCode(), Is.EqualTo(clash.GetHashCode()));
        }

        [Test]
        public void Clash_ComparesWholeSkills_PowerPerHitTimesHits()
        {
            LegacyClash clash = Classify(Attack, 5, 3, Attack, 12, 1);
            Assert.That(clash.Winner, Is.EqualTo(LegacyClashSide.Player), "Three hits of 5 carry 15 against 12.");
            Assert.That(clash.PlayerStrength, Is.EqualTo(3f / 15f).Within(Tolerance));
            Assert.That(Classify(Attack, 3, 3, Attack, 9, 1).Winner, Is.EqualTo(LegacyClashSide.None), "3 × 3 meets 9 evenly.");
            Assert.That(In(Attack, 5, 3, Guard, 7, 1).PlayerSkillPower, Is.EqualTo(15));
            Assert.That(In(Attack, 5, 3, Guard, 7, 1).EnemySkillPower, Is.EqualTo(7));
        }

        [Test]
        public void AttackAgainstGuard_DamageGettingThroughPierces_ByTheShareOfTheAttackThatGotThrough()
        {
            LegacyClash clash = Classify(Attack, 10, 1, Guard, 6, 1);
            Assert.That(clash.Kind, Is.EqualTo(LegacyClashKind.GuardPierced));
            Assert.That(clash.Winner, Is.EqualTo(LegacyClashSide.Player));
            Assert.That((clash.PlayerMove, clash.EnemyMove), Is.EqualTo((LegacyClashMove.Thrust, LegacyClashMove.Recoil)));
            Assert.That(clash.PlayerStrength, Is.EqualTo(.4f).Within(Tolerance), "4 of 10 got through.");
            Assert.That(clash.EnemyStrength, Is.EqualTo(.4f).Within(Tolerance));
            Assert.That(clash.PlayerAdvantage, Is.EqualTo(.4f).Within(Tolerance));
            Assert.That(clash.Contact, Is.True);

            float previous = 0f;
            for (int power = 7; power <= 40; power++)
            {
                LegacyClash pierced = Classify(Attack, power, 1, Guard, 6, 1);
                Assert.That(pierced.Kind, Is.EqualTo(LegacyClashKind.GuardPierced));
                Assert.That(pierced.EnemyStrength, Is.GreaterThan(previous).And.LessThanOrEqualTo(1f), "pierce at " + power);
                previous = pierced.EnemyStrength;
            }
        }

        [Test]
        public void AttackAgainstGuard_AFullBlockRebounds_AndTheGuardBraces()
        {
            LegacyClash clash = Classify(Attack, 5, 1, Guard, 8, 1);
            Assert.That(clash.Kind, Is.EqualTo(LegacyClashKind.GuardHeld));
            Assert.That(clash.Winner, Is.EqualTo(LegacyClashSide.Enemy));
            Assert.That((clash.PlayerMove, clash.EnemyMove), Is.EqualTo((LegacyClashMove.Rebound, LegacyClashMove.Brace)));
            Assert.That(clash.PlayerStrength, Is.EqualTo(3f / 8f).Within(Tolerance), "The guard held 3 more than the hit.");
            Assert.That(clash.EnemyStrength, Is.EqualTo(3f / 8f).Within(Tolerance));
            Assert.That(clash.PlayerAdvantage, Is.EqualTo(-3f / 8f).Within(Tolerance));
            Assert.That(clash.Contact, Is.True);

            LegacyClash exact = Classify(Attack, 6, 1, Guard, 6, 1);
            Assert.That(exact.Kind, Is.EqualTo(LegacyClashKind.GuardHeld), "Nothing got through: a block.");
            Assert.That(exact.Winner, Is.EqualTo(LegacyClashSide.Enemy));
            Assert.That((exact.PlayerStrength, exact.PlayerAdvantage), Is.EqualTo((0f, 0f)));

            float previous = -1f;
            for (int power = 8; power >= 1; power--)
            {
                LegacyClash held = Classify(Attack, power, 1, Guard, 8, 1);
                Assert.That(held.PlayerStrength, Is.GreaterThan(previous), "rebound at " + power);
                previous = held.PlayerStrength;
            }

            // The enemy's attack on the player's guard reads the same from the other side.
            LegacyClash enemyBlocked = Classify(Guard, 8, 1, Attack, 5, 1);
            Assert.That(enemyBlocked.Winner, Is.EqualTo(LegacyClashSide.Player));
            Assert.That((enemyBlocked.PlayerMove, enemyBlocked.EnemyMove), Is.EqualTo((LegacyClashMove.Brace, LegacyClashMove.Rebound)));
        }

        [TestCase(4, 2, 7, true)]
        [TestCase(3, 2, 7, false)]
        [TestCase(10, 1, 6, true)]
        [TestCase(6, 1, 6, false)]
        [TestCase(2, 3, 5, true)]
        [TestCase(2, 3, 6, false)]
        [TestCase(1, 4, 3, true)]
        public void AttackAgainstGuard_SplitsTheGuardOverTheHits_AsTheRulesDo(int perHit, int hits, int guard, bool pierced)
        {
            LegacyClash clash = Classify(Attack, perHit, hits, Guard, guard, 1);
            Assert.That(clash.Kind, Is.EqualTo(pierced ? LegacyClashKind.GuardPierced : LegacyClashKind.GuardHeld));

            // The duel agrees: damage reaches the guard's health exactly when the motion says it pierced.
            var duel = new LegacyQueuedDuel(100, 50, 100, 50, new[] { AttackSkill(960, perHit * hits, hits) },
                new[] { GuardSkill(961, guard) }, new[] { 1 }, meshPercent: 0);
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.BeginNextSlot();
            LegacyClashInput read = duel.CurrentClash;
            Assert.That((read.PlayerPower, read.PlayerHits, read.EnemyPower), Is.EqualTo((perHit, hits, guard)));
            Assert.That(LegacyClashMotion.Classify(read).Kind, Is.EqualTo(clash.Kind));
            Assert.That(duel.ResolveNextSlot().EnemyHealthDamage > 0, Is.EqualTo(pierced));
        }

        [TestCase(3, 1, 6)]
        [TestCase(6, 1, 6)]
        [TestCase(2, 3, 9)]
        [TestCase(4, 2, 7)]
        public void APressedAttack_AlwaysPiercesAGuard_AsThePressureBonusReachesHealth(int perHit, int hits, int guard)
        {
            // Read from the duel: a successful 압박 before the first hit is part of the slot the cards show.
            var duel = new LegacyQueuedDuel(100, 50, 100, 50, new[] { AttackSkill(960, perHit * hits, hits) },
                new[] { GuardSkill(961, guard) }, new[] { 1 }, meshPercent: 0);
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.BeginNextSlot();
            Assert.That(duel.CurrentClash.PlayerPressured, Is.False);
            Assert.That(duel.TryStep(LegacyStepAction.Pressure, true, out bool success) && success, Is.True);
            LegacyClashInput read = duel.CurrentClash;
            Assert.That(read.PlayerPressured, Is.True);
            LegacyClash clash = LegacyClashMotion.Classify(read);
            Assert.That((clash.Kind, clash.Winner), Is.EqualTo((LegacyClashKind.GuardPierced, LegacyClashSide.Player)));
            Assert.That((clash.PlayerMove, clash.EnemyMove), Is.EqualTo((LegacyClashMove.Thrust, LegacyClashMove.Recoil)));
            Assert.That(clash.PlayerStrength, Is.InRange(.5f, 1f), "At least the 압박 bonus's half of what it carried.");
            Assert.That(clash.EnemyStrength, Is.EqualTo(clash.PlayerStrength), "The guard is knocked back as far as it was pierced.");
            // The duel agrees: the bonus reaches the guard's health, whatever the guard held.
            Assert.That(duel.ResolveNextSlot().EnemyHealthDamage, Is.GreaterThan(0));
        }

        [Test]
        public void Pressure_OnlyCountsForAnAttack_AndReadsTheSameFromEitherSide()
        {
            LegacyClashInput held = new LegacyClashInput(0, Attack, 3, 1, Guard, 8, 1);
            LegacyClashInput pressed = new LegacyClashInput(0, Attack, 3, 1, Guard, 8, 1, playerPressured: true);
            Assert.That(LegacyClashMotion.Classify(held).Kind, Is.EqualTo(LegacyClashKind.GuardHeld));
            Assert.That(LegacyClashMotion.Classify(pressed).Kind, Is.EqualTo(LegacyClashKind.GuardPierced));
            Assert.That(pressed, Is.Not.EqualTo(held));
            Assert.That(pressed.GetHashCode(), Is.Not.EqualTo(held.GetHashCode()));
            Assert.That(pressed.Mirrored().EnemyPressured, Is.True);
            Assert.That(LegacyClashMotion.Classify(pressed.Mirrored()), Is.EqualTo(LegacyClashMotion.Classify(pressed).Mirrored()));
            // A guard or a breath carries no 압박 (a pressed guard only halves what it takes, which the cards do not show).
            Assert.That(new LegacyClashInput(0, Guard, 8, 1, Attack, 3, 1, playerPressured: true).PlayerPressured, Is.False);
            Assert.That(new LegacyClashInput(0, Breath, 0, 1, Attack, 3, 1, playerPressured: true),
                Is.EqualTo(new LegacyClashInput(0, Breath, 0, 1, Attack, 3, 1)));
            // Against an attack or nothing it changes nothing.
            Assert.That(LegacyClashMotion.Classify(new LegacyClashInput(0, Attack, 3, 1, Attack, 8, 1, playerPressured: true)).Kind,
                Is.EqualTo(LegacyClashKind.Clash));
            Assert.That(LegacyClashMotion.Classify(new LegacyClashInput(0, Attack, 3, 1, Empty, 0, 1, playerPressured: true)).Kind,
                Is.EqualTo(LegacyClashKind.Unopposed));
        }

        [Test]
        public void AttackAgainstNothing_LungesUnopposed_AtAnEmptySlotOrABreath()
        {
            LegacyClash empty = Classify(Attack, 7, 1, Empty, 0, 1);
            Assert.That(empty.Kind, Is.EqualTo(LegacyClashKind.Unopposed));
            Assert.That(empty.Winner, Is.EqualTo(LegacyClashSide.Player));
            Assert.That((empty.PlayerMove, empty.EnemyMove), Is.EqualTo((LegacyClashMove.Lunge, LegacyClashMove.Rest)));
            Assert.That((empty.PlayerStrength, empty.EnemyStrength, empty.PlayerAdvantage), Is.EqualTo((1f, 0f, 1f)));
            Assert.That(empty.Contact, Is.False);

            LegacyClash breath = Classify(Breath, 0, 1, Attack, 7, 2);
            Assert.That(breath.Kind, Is.EqualTo(LegacyClashKind.Unopposed));
            Assert.That(breath.Winner, Is.EqualTo(LegacyClashSide.Enemy));
            Assert.That((breath.PlayerMove, breath.EnemyMove), Is.EqualTo((LegacyClashMove.Rest, LegacyClashMove.Lunge)));
            Assert.That(breath.PlayerAdvantage, Is.EqualTo(-1f));
        }

        [Test]
        public void ADodgedAttack_Whiffs_AndTheDodgerSideSteps()
        {
            foreach (LegacyClashAction mine in new[] { Guard, Attack, Breath, Empty })
            {
                LegacyClash clash = LegacyClashMotion.Classify(In(mine, 5, 1, Attack, 12, 1, playerDodged: true));
                Assert.That(clash.Kind, Is.EqualTo(LegacyClashKind.Dodged), mine.ToString());
                Assert.That(clash.Winner, Is.EqualTo(LegacyClashSide.Player));
                Assert.That((clash.PlayerMove, clash.EnemyMove), Is.EqualTo((LegacyClashMove.SideStep, LegacyClashMove.Whiff)));
                Assert.That((clash.PlayerStrength, clash.EnemyStrength, clash.PlayerAdvantage), Is.EqualTo((1f, 1f, 1f)));
                Assert.That(clash.Contact, Is.False);
            }
            LegacyClash enemyDodged = LegacyClashMotion.Classify(In(Attack, 9, 1, Guard, 4, 1, enemyDodged: true));
            Assert.That(enemyDodged.Winner, Is.EqualTo(LegacyClashSide.Enemy));
            Assert.That((enemyDodged.PlayerMove, enemyDodged.EnemyMove), Is.EqualTo((LegacyClashMove.Whiff, LegacyClashMove.SideStep)));

            // A dodge with no attack to dodge counts for nothing.
            Assert.That(LegacyClashMotion.Classify(In(Guard, 5, 1, Guard, 4, 1, playerDodged: true)).Kind, Is.EqualTo(LegacyClashKind.Settle));
            Assert.That(LegacyClashMotion.Classify(In(Attack, 9, 1, Guard, 4, 1, playerDodged: true)).Kind,
                Is.EqualTo(LegacyClashKind.GuardPierced));
        }

        [Test]
        public void WithoutAnAttack_PresentCardsSettle_AndNothingIsNone()
        {
            AssertSettle(In(Guard, 5, 1, Guard, 9, 1), LegacyClashMove.Settle, LegacyClashMove.Settle);
            AssertSettle(In(Guard, 5, 1, Empty, 0, 1), LegacyClashMove.Settle, LegacyClashMove.Rest);
            AssertSettle(In(Empty, 0, 1, Guard, 5, 1), LegacyClashMove.Rest, LegacyClashMove.Settle);
            AssertSettle(In(Breath, 0, 1, Guard, 5, 1), LegacyClashMove.Settle, LegacyClashMove.Settle);
            AssertSettle(In(Breath, 0, 1, Breath, 0, 1), LegacyClashMove.Settle, LegacyClashMove.Settle);
            AssertSettle(In(Breath, 0, 1, Empty, 0, 1), LegacyClashMove.Settle, LegacyClashMove.Rest);

            LegacyClash none = LegacyClashMotion.Classify(In(Empty, 0, 1, Empty, 0, 1));
            Assert.That(none.Kind, Is.EqualTo(LegacyClashKind.None));
            Assert.That((none.PlayerMove, none.EnemyMove), Is.EqualTo((LegacyClashMove.Rest, LegacyClashMove.Rest)));
            Assert.That(LegacyClashMotion.Classify(LegacyClashInput.None).Kind, Is.EqualTo(LegacyClashKind.None));
            Assert.That(LegacyClashInput.None.HasSlot, Is.False);
            Assert.That(LegacyClashInput.None.SlotIndex, Is.EqualTo(-1));
        }

        [Test]
        public void ABreakThisSlot_ShowsOnItsSide_WhateverTheKind()
        {
            foreach (LegacyClashInput input in Inputs())
            {
                foreach ((bool player, bool enemy) in new[] { (false, false), (true, false), (false, true), (true, true) })
                {
                    LegacyClashInput broken = new LegacyClashInput(input.SlotIndex, input.PlayerAction, input.PlayerPower,
                        input.PlayerHits, input.EnemyAction, input.EnemyPower, input.EnemyHits, input.PlayerDodged,
                        input.EnemyDodged, player, enemy, input.PlayerCountered, input.EnemyCountered, input.Decided,
                        input.PlayerPressured, input.EnemyPressured);
                    LegacyClash clash = LegacyClashMotion.Classify(broken);
                    Assert.That((clash.PlayerBroken, clash.EnemyBroken), Is.EqualTo((player, enemy)), broken.ToString());
                    LegacyClash whole = LegacyClashMotion.Classify(input);
                    Assert.That((clash.Kind, clash.PlayerMove, clash.EnemyMove), Is.EqualTo((whole.Kind, whole.PlayerMove, whole.EnemyMove)),
                        "A break adds its shake; it does not change the meeting.");
                }
            }
        }

        [Test]
        public void EveryKind_ReadsTheSameFromEitherSide()
        {
            var kinds = new HashSet<LegacyClashKind>();
            foreach (LegacyClashInput input in Inputs())
            {
                LegacyClash clash = LegacyClashMotion.Classify(input);
                kinds.Add(clash.Kind);
                Assert.That(input.Mirrored().Mirrored(), Is.EqualTo(input));
                Assert.That(LegacyClashMotion.Classify(input.Mirrored()), Is.EqualTo(clash.Mirrored()), input.ToString());
                Assert.That(clash.PlayerStrength, Is.InRange(0f, 1f));
                Assert.That(clash.EnemyStrength, Is.InRange(0f, 1f));
                Assert.That(clash.PlayerAdvantage, Is.InRange(-1f, 1f));
            }
            CollectionAssert.AreEquivalent(Enum.GetValues(typeof(LegacyClashKind)).Cast<LegacyClashKind>(), kinds,
                "The cases cover every kind.");
        }

        [Test]
        public void Input_KeepsOnlyWhatItsActionsUse_AndRefusesNonsense()
        {
            var input = new LegacyClashInput(2, Breath, 5, 3, Guard, 7, 4);
            Assert.That((input.PlayerPower, input.PlayerHits), Is.EqualTo((0, 1)), "A breath has no power or hits.");
            Assert.That((input.EnemyPower, input.EnemyHits), Is.EqualTo((7, 1)), "A guard keeps its value, not hits.");
            Assert.That(LegacyClashInput.FromSkills(0, null, 4, LegacyCommonActions.Breathe, 4).PlayerAction, Is.EqualTo(Empty));
            Assert.That(LegacyClashInput.FromSkills(0, null, 4, LegacyCommonActions.Breathe, 4).EnemyAction, Is.EqualTo(Breath));
            Assert.That(LegacyClashInput.FromSkills(0, AttackSkill(962, 6, 3), 2, GuardSkill(963, 5), 5).PlayerHits, Is.EqualTo(3));
            Assert.That(LegacyClashMotion.ActionOf(GuardSkill(963, 5)), Is.EqualTo(Guard));
            Assert.That(default(LegacyClashInput).PlayerHits, Is.EqualTo(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LegacyClashInput(-2, Attack, 1, 1, Empty, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LegacyClashInput(0, Attack, -1, 1, Empty, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LegacyClashInput(0, Attack, 1, 0, Empty, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LegacyClashInput(0, Attack, 1, 1, Guard, -3, 1));
        }

        [Test]
        public void CampaignDuel_CurrentClash_ReadsTheSlotAsItPlays_AndABreakingHit()
        {
            var duel = new LegacyQueuedDuel(100, 50, 100, 5, new[] { AttackSkill(960, 10) }, new[] { AttackSkill(961, 4) },
                new[] { 1 }, meshPercent: 0);
            ILegacyDuelView view = duel;
            Assert.That(view.CurrentClash, Is.EqualTo(LegacyClashInput.None), "Nothing resolves while planning.");
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.BeginNextSlot();
            LegacyClashInput start = view.CurrentClash;
            Assert.That(start, Is.EqualTo(new LegacyClashInput(0, Attack, 10, 1, Attack, 4, 1)));
            LegacyClash clash = LegacyClashMotion.Classify(start);
            Assert.That(clash.Winner, Is.EqualTo(LegacyClashSide.Player));
            Assert.That(clash.EnemyStrength, Is.EqualTo(.6f).Within(Tolerance));
            Assert.That(clash.EnemyBroken, Is.False);

            duel.ResolveNextHit();
            LegacyClashInput after = view.CurrentClash;
            Assert.That(after.Decided, Is.True);
            Assert.That((after.PlayerBroken, after.EnemyBroken), Is.EqualTo((false, true)), "10 breaks the enemy's 5 resistance.");
            Assert.That(LegacyClashMotion.Classify(after).EnemyBroken, Is.True);
            duel.CompleteCurrentSlot();
            Assert.That(view.CurrentClash, Is.EqualTo(LegacyClashInput.None));
        }

        [Test]
        public void CampaignDuel_APendingCounterAndADodge_ShowOnceTheySettle()
        {
            // The player's 반격 waits for the first hit: until then the player's side reads empty.
            LegacyQueuedDuel countered = CounterDuel();
            countered.Commit();
            countered.BeginNextSlot();
            Assert.That(countered.CurrentClash.PlayerAction, Is.EqualTo(Empty));
            Assert.That(countered.CurrentClash.Decided, Is.False);
            Assert.That(LegacyClashMotion.Classify(countered.CurrentClash).Kind, Is.EqualTo(LegacyClashKind.Unopposed));
            countered.ResolveNextHit();
            LegacyClashInput settled = countered.CurrentClash;
            Assert.That((settled.PlayerAction, settled.PlayerPower, settled.PlayerCountered), Is.EqualTo((Attack, 9, true)));
            LegacyClash clash = LegacyClashMotion.Classify(settled);
            Assert.That((clash.Kind, clash.Winner), Is.EqualTo((LegacyClashKind.Clash, LegacyClashSide.Player)));

            // A dodge withdraws the counter and shows as a side-step.
            LegacyQueuedDuel dodged = CounterDuel();
            dodged.Commit();
            dodged.BeginNextSlot();
            Assert.That(dodged.TryStep(LegacyStepAction.Dodge, true, out bool success), Is.True);
            Assert.That(success, Is.True);
            Assert.That(LegacyClashMotion.Classify(dodged.CurrentClash).Kind, Is.EqualTo(LegacyClashKind.Dodged));
            dodged.ResolveNextHit();
            LegacyClashInput read = dodged.CurrentClash;
            Assert.That((read.PlayerAction, read.PlayerDodged, read.PlayerCountered), Is.EqualTo((Empty, true, false)));
            LegacyClash dodge = LegacyClashMotion.Classify(read);
            Assert.That((dodge.Kind, dodge.Winner, dodge.PlayerMove, dodge.EnemyMove),
                Is.EqualTo((LegacyClashKind.Dodged, LegacyClashSide.Player, LegacyClashMove.SideStep, LegacyClashMove.Whiff)));
        }

        [Test]
        public void EnemyCounter_FillsItsEmptySide_AndSaysSo()
        {
            var duel = new LegacyQueuedDuel(100, 50, 100, 50, new[] { AttackSkill(960, 10) }, new[] { AttackSkill(961, 4) },
                new[] { 1 }, enemyCounter: new LegacyCounter(GuardSkill(964, 6)), meshPercent: 0);
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.ResolveNextSlot();
            duel.BeginNextSlot();
            LegacyClashInput input = duel.CurrentClash;
            Assert.That((input.SlotIndex, input.EnemyAction, input.EnemyPower, input.EnemyCountered), Is.EqualTo((1, Guard, 6, true)));
            Assert.That(LegacyClashMotion.Classify(input).Kind, Is.EqualTo(LegacyClashKind.GuardPierced));
        }

        [Test]
        public void Versus_TheGuestSeesTheHostsClashMirrored()
        {
            var match = new LocalVersusMatch(new[] { AttackSkill(970, 12) }, new[] { AttackSkill(971, 8) },
                100, 50, 100, 5, randomSeed: 17);
            ILegacyDuelView host = new LocalVersusPerspective(match, 0), guest = new LocalVersusPerspective(match, 1);
            Assert.That(match.CurrentClash(0), Is.EqualTo(LegacyClashInput.None));
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.TryQueueLane(1, 0), Is.True);
            Commit(match);
            match.BeginNextSlot();
            Assert.That(host.CurrentClash, Is.EqualTo(new LegacyClashInput(0, Attack, 12, 1, Attack, 8, 1)));
            Assert.That(guest.CurrentClash, Is.EqualTo(host.CurrentClash.Mirrored()));
            LegacyClash seenByHost = LegacyClashMotion.Classify(host.CurrentClash);
            LegacyClash seenByGuest = LegacyClashMotion.Classify(guest.CurrentClash);
            Assert.That(seenByHost.Winner, Is.EqualTo(LegacyClashSide.Player));
            Assert.That(seenByGuest.Winner, Is.EqualTo(LegacyClashSide.Enemy));
            Assert.That(seenByGuest, Is.EqualTo(seenByHost.Mirrored()));

            match.ResolveNextHit();
            Assert.That((host.CurrentClash.PlayerBroken, host.CurrentClash.EnemyBroken), Is.EqualTo((false, true)),
                "12 breaks the right side's 5 resistance.");
            Assert.That((guest.CurrentClash.PlayerBroken, guest.CurrentClash.EnemyBroken), Is.EqualTo((true, false)));
            Assert.That(guest.CurrentClash.Decided, Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => match.CurrentClash(2));
        }

        [Test]
        public void Versus_AGuardReadsFromEachViewersSide()
        {
            var match = new LocalVersusMatch(new[] { GuardSkill(972, 6) }, new[] { AttackSkill(973, 10) }, randomSeed: 17);
            var host = new LocalVersusPerspective(match, 0);
            var guest = new LocalVersusPerspective(match, 1);
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.TryQueueLane(1, 0), Is.True);
            Commit(match);
            match.BeginNextSlot();
            LegacyClash hostClash = LegacyClashMotion.Classify(host.CurrentClash);
            Assert.That((hostClash.Kind, hostClash.Winner, hostClash.PlayerMove),
                Is.EqualTo((LegacyClashKind.GuardPierced, LegacyClashSide.Enemy, LegacyClashMove.Recoil)));
            LegacyClash guestClash = LegacyClashMotion.Classify(guest.CurrentClash);
            Assert.That((guestClash.Winner, guestClash.PlayerMove), Is.EqualTo((LegacyClashSide.Player, LegacyClashMove.Thrust)));
        }

        [TestCase(0, 3, 0)]
        [TestCase(2, 3, 2)]
        [TestCase(3, 3, -1)]
        [TestCase(5, 3, -1)]
        [TestCase(0, 0, -1)]
        [TestCase(-1, 3, -1)]
        public void NextOpponentSlot_IsThePlayersNextSlot_WhenTheEnemyHasACardThere(int playerCount, int enemyCount, int expected)
            => Assert.That(LegacyClashMotion.NextOpponentSlot(playerCount, enemyCount), Is.EqualTo(expected));

        [Test]
        public void NextOpponentSlot_FollowsReservations_AnAutomaticBreathAndARemoval()
        {
            KeywordSheet.WithKeywords(() =>
            {
                LegacySkill[] enemy = Enumerable.Range(0, 4).Select(index => AttackSkill(980 + index, 3)).ToArray();
                var duel = new LegacyQueuedDuel(1000, 50, 1000, 100,
                    new[] { KeywordSheet.Skill(KeywordSheet.AutoBreathGuard), KeywordSheet.Skill(KeywordSheet.Returner) },
                    enemy, new[] { 4 }, playerActGainBonus: 7, meshPercent: 0);
                Assert.That(LegacyClashMotion.NextOpponentSlot(duel), Is.Zero);
                Assert.That(duel.TryQueueLane(0), Is.True, "숨고르기 +1 brings a breath along");
                Assert.That(LegacyClashMotion.NextOpponentSlot(duel), Is.EqualTo(2));
                Assert.That(duel.TryQueueBreath(), Is.True);
                Assert.That(LegacyClashMotion.NextOpponentSlot(duel), Is.EqualTo(3));
                Assert.That(duel.TryQueueLane(1), Is.True, "반환 takes the guard back and keeps the breaths");
                Assert.That(duel.PlayerQueue.Count, Is.EqualTo(3));
                Assert.That(LegacyClashMotion.NextOpponentSlot(duel), Is.EqualTo(3));
                Assert.That(duel.TryQueueBreath(), Is.True);
                Assert.That(LegacyClashMotion.NextOpponentSlot(duel), Is.EqualTo(-1), "The enemy has no fifth card.");
                duel.Commit();
                Assert.That(LegacyClashMotion.NextOpponentSlot(duel), Is.EqualTo(-1), "Only while planning.");
                Assert.That(LegacyClashMotion.NextOpponentSlot(null), Is.EqualTo(-1));
            });
        }

        [Test]
        public void NextOpponentSlot_InVersus_ReadsEachViewersOwnQueue()
        {
            var match = new LocalVersusMatch(new[] { AttackSkill(974, 5) }, new[] { AttackSkill(975, 5) }, randomSeed: 17);
            var host = new LocalVersusPerspective(match, 0);
            var guest = new LocalVersusPerspective(match, 1);
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(LegacyClashMotion.NextOpponentSlot(guest), Is.Zero, "The guest's first reservation meets the host's card.");
            Assert.That(LegacyClashMotion.NextOpponentSlot(host), Is.EqualTo(-1));
            Assert.That(match.TryQueueLane(1, 0), Is.True);
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(LegacyClashMotion.NextOpponentSlot(guest), Is.EqualTo(1));
            Assert.That(LegacyClashMotion.NextOpponentSlot(host), Is.EqualTo(-1));
        }

        private static LegacyClash Classify(LegacyClashAction player, int playerPower, int playerHits,
            LegacyClashAction enemy, int enemyPower, int enemyHits)
            => LegacyClashMotion.Classify(In(player, playerPower, playerHits, enemy, enemyPower, enemyHits));

        private static LegacyClashInput In(LegacyClashAction player, int playerPower, int playerHits,
            LegacyClashAction enemy, int enemyPower, int enemyHits, bool playerDodged = false, bool enemyDodged = false)
            => new LegacyClashInput(0, player, playerPower, playerHits, enemy, enemyPower, enemyHits, playerDodged, enemyDodged);

        // One input per meeting, each from both sides' points of view where it differs.
        private static IEnumerable<LegacyClashInput> Inputs()
        {
            yield return In(Attack, 12, 1, Attack, 8, 1);
            yield return In(Attack, 5, 3, Attack, 9, 2);
            yield return In(Attack, 9, 1, Attack, 9, 1);
            yield return In(Attack, 10, 1, Guard, 6, 1);
            yield return In(Attack, 4, 2, Guard, 7, 1);
            yield return In(Attack, 5, 1, Guard, 8, 1);
            yield return new LegacyClashInput(0, Attack, 3, 2, Guard, 9, 1, playerPressured: true);
            yield return In(Guard, 8, 1, Attack, 8, 1);
            yield return In(Attack, 7, 1, Empty, 0, 1);
            yield return In(Breath, 0, 1, Attack, 7, 2);
            yield return In(Guard, 5, 1, Attack, 12, 1, playerDodged: true);
            yield return In(Attack, 5, 1, Attack, 12, 1, playerDodged: true);
            yield return In(Attack, 5, 1, Attack, 12, 1, playerDodged: true, enemyDodged: true);
            yield return In(Guard, 5, 1, Guard, 9, 1, playerDodged: true);
            yield return In(Breath, 0, 1, Guard, 5, 1);
            yield return In(Empty, 0, 1, Guard, 5, 1);
            yield return In(Empty, 0, 1, Empty, 0, 1);
        }

        private static void AssertSettle(LegacyClashInput input, LegacyClashMove player, LegacyClashMove enemy)
        {
            LegacyClash clash = LegacyClashMotion.Classify(input);
            Assert.That(clash.Kind, Is.EqualTo(LegacyClashKind.Settle), input.ToString());
            Assert.That((clash.PlayerMove, clash.EnemyMove), Is.EqualTo((player, enemy)), input.ToString());
            Assert.That((clash.Winner, clash.PlayerAdvantage, clash.PlayerStrength, clash.EnemyStrength),
                Is.EqualTo((LegacyClashSide.None, 0f, 0f, 0f)));
            Assert.That(clash.Contact, Is.False);
        }

        private static LegacyQueuedDuel CounterDuel()
            => new LegacyQueuedDuel(100, 50, 100, 50, new[] { AttackSkill(960, 10) }, new[] { AttackSkill(961, 6) },
                new[] { 1 }, playerCounter: new LegacyCounter(AttackSkill(965, 9)), meshPercent: 0);

        private static LegacySkill AttackSkill(int id, int power, int hits = 1)
            => new LegacySkill(id, "clash attack", 1, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, hits, 0, "");

        private static LegacySkill GuardSkill(int id, int power)
            => new LegacySkill(id, "clash guard", 1, power, power, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 0, "");

        private static void Commit(LocalVersusMatch match)
        {
            int first = match.CurrentPlanner;
            Assert.That(match.TryPass(first), Is.True);
            Assert.That(match.TryPass(1 - first), Is.True);
        }
    }
}
