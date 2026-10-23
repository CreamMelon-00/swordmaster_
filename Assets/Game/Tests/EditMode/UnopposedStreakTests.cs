using System;
using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    /// <summary>일방 공격 가속 (<see cref="LegacyUnopposedStreak"/>, 2026-10-15): the streak counts slots in a row where exactly
    /// one side attacks into nothing (whichever side), a slot where neither attacks keeps it, a slot where both act against
    /// an attack starts it over; the speed grows by the step from the start length up to the cap; a Major or Climax slot keeps
    /// its own speed while the streak counts on; the director carries it turn by turn (never banked, switched off with the
    /// director), and both versus screens and both resolvers agree.</summary>
    public sealed class UnopposedStreakTests
    {
        private const LegacyClashAction Empty = LegacyClashAction.Empty, Breath = LegacyClashAction.Breath,
            Guard = LegacyClashAction.Guard, Attack = LegacyClashAction.Attack;
        private const float Tolerance = 1e-5f;

        [Test]
        public void TheDefaults_AreTheDocumentedOnes()
        {
            LegacyStreakTuning tuning = LegacyStreakTuning.Default;
            Assert.That(tuning.Enabled && tuning.Accelerates, Is.True);
            Assert.That(tuning.StepPerSlot, Is.EqualTo(.15f).Within(Tolerance));
            Assert.That(tuning.MaximumSpeed, Is.EqualTo(1.6f).Within(Tolerance));
            Assert.That(tuning.StartLength, Is.EqualTo(2));
            Assert.That(LegacyStreakTuning.Off.Enabled || LegacyStreakTuning.Off.Accelerates, Is.False);
            var director = new LegacyShowDirector();
            Assert.That(director.Streak.Accelerates, Is.True, "A director starts with the defaults.");
            Assert.That(director.StreakEnabled, Is.True);
            Assert.That((director.StreakLength, director.StreakSpeed, director.GapSpeed), Is.EqualTo((0, 1f, 1f)));
        }

        [Test]
        public void TheTuning_KeepsItsNumbersInRange()
        {
            var wild = new LegacyStreakTuning(true, float.NaN, float.NaN, -4);
            Assert.That((wild.StepPerSlot, wild.MaximumSpeed, wild.StartLength), Is.EqualTo((0f, 1f, 1)), "Not a number: nothing faster.");
            Assert.That(wild.Accelerates, Is.False);
            var high = new LegacyStreakTuning(true, 9f, 9f, 99);
            Assert.That(high.StepPerSlot, Is.EqualTo(LegacyStreakTuning.MaximumStepPerSlot));
            Assert.That(high.MaximumSpeed, Is.EqualTo(LegacyStreakTuning.MaximumSpeedLimit));
            Assert.That(high.StartLength, Is.EqualTo(LegacyStreakTuning.MaximumStartLength));
            var low = new LegacyStreakTuning(true, -1f, .2f, 0);
            Assert.That((low.StepPerSlot, low.MaximumSpeed, low.StartLength), Is.EqualTo((0f, 1f, 1)));
            Assert.That(new LegacyStreakTuning(true, .15f, 1f, 2).Accelerates, Is.False, "A cap of 1 never speeds up.");
            Assert.That(new LegacyStreakTuning(true, 0f, 1.6f, 2).Accelerates, Is.False, "Nor does a step of 0.");
            Assert.That(new LegacyStreakTuning(false, .15f, 1.6f, 2).Accelerates, Is.False);
        }

        [Test]
        public void OnlyAnAttackIntoNothing_Extends_NoAttackKeeps_AndBothActingResets()
        {
            Assert.That(LegacyUnopposedStreak.StepOf(LegacyClashKind.Unopposed), Is.EqualTo(LegacyStreakStep.Extends));
            foreach (LegacyClashKind kind in new[] { LegacyClashKind.Settle, LegacyClashKind.None })
                Assert.That(LegacyUnopposedStreak.StepOf(kind), Is.EqualTo(LegacyStreakStep.Keeps), kind.ToString());
            foreach (LegacyClashKind kind in new[]
            {
                LegacyClashKind.Clash, LegacyClashKind.GuardPierced, LegacyClashKind.GuardHeld, LegacyClashKind.Dodged,
            })
                Assert.That(LegacyUnopposedStreak.StepOf(kind), Is.EqualTo(LegacyStreakStep.Resets), kind.ToString());
            foreach (LegacyClashKind kind in (LegacyClashKind[])Enum.GetValues(typeof(LegacyClashKind)))
                Assert.That(Enum.IsDefined(typeof(LegacyStreakStep), LegacyUnopposedStreak.StepOf(kind)), Is.True, "Every kind is told.");

            // The slots as the clash motion reads them, either side attacking.
            var extends = new[]
            {
                In(Attack, 10, 1, Empty, 0, 1), In(Attack, 10, 2, Breath, 0, 1), In(Empty, 0, 1, Attack, 10, 1),
                In(Breath, 0, 1, Attack, 10, 3),
            };
            var keeps = new[]
            {
                In(Guard, 6, 1, Guard, 9, 1), In(Guard, 6, 1, Empty, 0, 1), In(Breath, 0, 1, Breath, 0, 1),
                In(Empty, 0, 1, Breath, 0, 1), In(Empty, 0, 1, Empty, 0, 1),
            };
            var resets = new[]
            {
                In(Attack, 10, 1, Attack, 10, 1), In(Attack, 10, 1, Guard, 4, 1), In(Attack, 3, 1, Guard, 9, 1),
                In(Guard, 9, 1, Attack, 3, 1), In(Breath, 0, 1, Attack, 10, 1, playerDodged: true),
                new LegacyClashInput(0, Attack, 9, 1, Attack, 10, 1, playerCountered: true, decided: true),
            };
            foreach (LegacyClashInput input in extends)
                Assert.That(LegacyUnopposedStreak.Next(3, Kind(input)), Is.EqualTo(4), input.ToString());
            foreach (LegacyClashInput input in keeps)
                Assert.That(LegacyUnopposedStreak.Next(3, Kind(input)), Is.EqualTo(3), input.ToString());
            foreach (LegacyClashInput input in resets)
                Assert.That(LegacyUnopposedStreak.Next(3, Kind(input)), Is.Zero, input.ToString());
            Assert.That(LegacyUnopposedStreak.Next(-2, LegacyClashKind.Unopposed), Is.EqualTo(1), "Below 0 reads as 0.");
        }

        [Test]
        public void AStreak_CountsOnlyInARow_ThroughNeutralSlots_FromZero()
        {
            LegacyClashKind U = LegacyClashKind.Unopposed, S = LegacyClashKind.Settle, C = LegacyClashKind.Clash,
                G = LegacyClashKind.GuardPierced, D = LegacyClashKind.Dodged;
            Assert.That(LegacyUnopposedStreak.Count(new LegacyClashKind[0]), Is.Zero);
            Assert.That(LegacyUnopposedStreak.Count(new[] { U, U, U }), Is.EqualTo(3));
            Assert.That(LegacyUnopposedStreak.Count(new[] { U, S, U, S, S, U }), Is.EqualTo(3), "Neither side attacking keeps it.");
            Assert.That(LegacyUnopposedStreak.Count(new[] { U, U, C, U }), Is.EqualTo(1), "A 합 starts it over.");
            Assert.That(LegacyUnopposedStreak.Count(new[] { U, U, G }), Is.Zero, "So does a guard.");
            Assert.That(LegacyUnopposedStreak.Count(new[] { U, U, U, D, U, U }), Is.EqualTo(2), "And a dodge.");
            Assert.Throws<ArgumentNullException>(() => LegacyUnopposedStreak.Count(null));
        }

        [Test]
        public void TheSpeed_GrowsByItsStep_FromTheStartLength_UpToTheCap()
        {
            LegacyStreakTuning tuning = LegacyStreakTuning.Default;
            float[] expected = { 1f, 1f, 1.15f, 1.3f, 1.45f, 1.6f, 1.6f, 1.6f };
            for (int streak = 0; streak < expected.Length; streak++)
                Assert.That(LegacyUnopposedStreak.Speed(streak, tuning), Is.EqualTo(expected[streak]).Within(Tolerance), "streak " + streak);
            Assert.That(LegacyUnopposedStreak.Speed(int.MaxValue, tuning), Is.EqualTo(1.6f).Within(Tolerance));
            Assert.That(LegacyUnopposedStreak.Speed(-1, tuning), Is.EqualTo(1f));

            var fromFirst = new LegacyStreakTuning(true, .2f, 2f, 1);
            Assert.That(LegacyUnopposedStreak.Speed(1, fromFirst), Is.EqualTo(1.2f).Within(Tolerance), "From the first slot.");
            Assert.That(LegacyUnopposedStreak.Speed(5, fromFirst), Is.EqualTo(2f).Within(Tolerance));
            var late = new LegacyStreakTuning(true, .1f, 1.25f, 4);
            Assert.That(LegacyUnopposedStreak.Speed(3, late), Is.EqualTo(1f));
            Assert.That(LegacyUnopposedStreak.Speed(4, late), Is.EqualTo(1.1f).Within(Tolerance));
            Assert.That(LegacyUnopposedStreak.Speed(9, late), Is.EqualTo(1.25f).Within(Tolerance), "The cap holds.");
            foreach (LegacyStreakTuning off in new[]
            {
                LegacyStreakTuning.Off, new LegacyStreakTuning(false, .15f, 1.6f, 2), new LegacyStreakTuning(true, 0f, 1.6f, 2),
                new LegacyStreakTuning(true, .15f, 1f, 2),
            })
                Assert.That(LegacyUnopposedStreak.Speed(5, off), Is.EqualTo(1f));
        }

        [Test]
        public void AMajorOrClimaxSlot_KeepsItsSpeed_ButTheStreakCountsOnThroughIt()
        {
            Assert.That(LegacyUnopposedStreak.Accelerates(LegacyClashKind.Unopposed, LegacySlotTier.Normal), Is.True);
            Assert.That(LegacyUnopposedStreak.Accelerates(LegacyClashKind.Unopposed, LegacySlotTier.Minor), Is.True);
            Assert.That(LegacyUnopposedStreak.Accelerates(LegacyClashKind.Unopposed, LegacySlotTier.Major), Is.False);
            Assert.That(LegacyUnopposedStreak.Accelerates(LegacyClashKind.Unopposed, LegacySlotTier.Climax), Is.False);
            foreach (LegacyClashKind kind in new[] { LegacyClashKind.Settle, LegacyClashKind.Clash, LegacyClashKind.None })
                Assert.That(LegacyUnopposedStreak.Accelerates(kind, LegacySlotTier.Normal), Is.False, kind.ToString());

            // U U U(Major) U S U C U U(Climax): the Major slot plays as ever, the next one counts it.
            var turn = new[]
            {
                Rank(LegacyClashKind.Unopposed), Rank(LegacyClashKind.Unopposed),
                Rank(LegacyClashKind.Unopposed, LegacySlotTier.Major), Rank(LegacyClashKind.Unopposed),
                Rank(LegacyClashKind.Settle, LegacySlotTier.Minor), Rank(LegacyClashKind.Unopposed),
                Rank(LegacyClashKind.Clash), Rank(LegacyClashKind.Unopposed),
                Rank(LegacyClashKind.Unopposed, LegacySlotTier.Climax),
            };
            float[] speeds = LegacyUnopposedStreak.Speeds(turn, LegacyStreakTuning.Default);
            float[] expected = { 1f, 1.15f, 1f, 1.45f, 1f, 1.6f, 1f, 1f, 1f };
            Assert.That(speeds.Length, Is.EqualTo(expected.Length));
            for (int slot = 0; slot < expected.Length; slot++)
                Assert.That(speeds[slot], Is.EqualTo(expected[slot]).Within(Tolerance), "slot " + slot);
            Assert.Throws<ArgumentNullException>(() => LegacyUnopposedStreak.Speeds(null, LegacyStreakTuning.Default));
        }

        [Test]
        public void TheDirector_PlaysARunFasterAndFaster_AndTheWaitAfterEachSlotToo_WithoutBankingIt()
        {
            var director = Director();
            float[] expected = { 1f, 1.15f, 1.3f, 1.45f, 1.6f, 1.6f };
            for (int slot = 0; slot < expected.Length; slot++)
            {
                LegacySlotRank rank = director.BeginSlot(Facts(slot, Attack, 10, Empty, 0));
                Assert.That(rank.Tier, Is.EqualTo(LegacySlotTier.Normal), "10 against a maximum health of 100: no big blow.");
                Assert.That(director.StreakLength, Is.EqualTo(slot + 1));
                Assert.That(director.StreakSpeed, Is.EqualTo(expected[slot]).Within(Tolerance), "slot " + slot);
                director.Observe(Facts(slot, Attack, 10, Empty, 0, decided: true));
                Assert.That(director.StreakSpeed, Is.EqualTo(expected[slot]).Within(Tolerance));
                Assert.That(director.GapSpeed, Is.EqualTo(1f), "Within a slot there is no wait after it yet.");
                director.EndSlot();
                Assert.That(director.StreakLength, Is.EqualTo(slot + 1));
                Assert.That(director.StreakSpeed, Is.EqualTo(1f), "Between slots nothing plays.");
                Assert.That(director.GapSpeed, Is.EqualTo(expected[slot]).Within(Tolerance), "The wait after it plays as fast.");
                Assert.That(director.ShortenGap(.28f), Is.EqualTo(.28f), "An attack is no Minor slot.");
            }
            Assert.That((director.Saved, director.Spent, director.NetExtra), Is.EqualTo((0f, 0f, 0f)),
                "What the streak saves is never banked for emphasis: the turn is only shorter.");

            // A new turn (and any reset) starts the streak over.
            director.BeginTurn(LegacyShowPacingTuning.Default, LegacySlotImportanceTuning.Default);
            Assert.That((director.StreakLength, director.GapSpeed), Is.EqualTo((0, 1f)));
            Assert.That(director.Streak.Accelerates, Is.True, "The two-argument BeginTurn keeps the streak's numbers.");
            director.BeginSlot(Facts(0, Empty, 0, Attack, 10));
            director.BeginSlot(Facts(1, Breath, 0, Attack, 10));
            Assert.That(director.StreakSpeed, Is.EqualTo(1.15f).Within(Tolerance), "The enemy's run counts the same.");
            director.Clear();
            Assert.That((director.StreakLength, director.StreakSpeed, director.GapSpeed), Is.EqualTo((0, 1f, 1f)));
        }

        [Test]
        public void TheDirector_ResetsOnAMutualSlot_KeepsOnANeutralOne_AndNeverSpeedsAMajorSlot()
        {
            var director = Director();
            director.BeginSlot(Facts(0, Attack, 10, Empty, 0));
            director.BeginSlot(Facts(1, Attack, 10, Breath, 0));
            Assert.That(director.StreakSpeed, Is.EqualTo(1.15f).Within(Tolerance));
            director.BeginSlot(Facts(2, Guard, 5, Guard, 5));
            Assert.That((director.StreakLength, director.StreakSpeed), Is.EqualTo((2, 1f)), "Guard against guard keeps the run.");
            director.EndSlot();
            Assert.That(director.GapSpeed, Is.EqualTo(1f), "Nor does the wait after it play faster.");
            Assert.That(director.ShortenGap(.28f), Is.EqualTo(.14f).Within(Tolerance), "It is the director's Minor slot instead.");
            director.BeginSlot(Facts(3, Attack, 10, Empty, 0));
            Assert.That(director.StreakSpeed, Is.EqualTo(1.3f).Within(Tolerance));

            // A big unopposed blow (30 of 100) is Major: it plays as ever, but the run counts it.
            Assert.That(director.BeginSlot(Facts(4, Attack, 30, Empty, 0)).Tier, Is.EqualTo(LegacySlotTier.Major));
            Assert.That((director.StreakLength, director.StreakSpeed), Is.EqualTo((4, 1f)));
            director.EndSlot();
            Assert.That(director.GapSpeed, Is.EqualTo(1f), "Nor the wait after it.");
            director.BeginSlot(Facts(5, Attack, 10, Empty, 0));
            Assert.That(director.StreakSpeed, Is.EqualTo(1.6f).Within(Tolerance), "The fifth in a row: the cap.");

            // A slot that turns Major at a later hit plays at its own speed from then on.
            director.BeginSlot(Facts(6, Attack, 10, Empty, 0));
            Assert.That(director.StreakSpeed, Is.EqualTo(1.6f).Within(Tolerance));
            director.Observe(new LegacySlotFacts(new LegacyClashInput(6, Attack, 10, 2, Empty, 0, 1, enemyBroken: true,
                decided: true), playerMaxHealth: 100, enemyMaxHealth: 100));
            Assert.That(director.SlotRank.Tier, Is.EqualTo(LegacySlotTier.Major), "The second hit broke the enemy.");
            Assert.That(director.StreakSpeed, Is.EqualTo(1f));

            // A 합 starts it over.
            director.BeginSlot(Facts(7, Attack, 10, Attack, 9));
            Assert.That((director.StreakLength, director.StreakSpeed), Is.EqualTo((0, 1f)));
            director.BeginSlot(Facts(8, Empty, 0, Attack, 10));
            Assert.That((director.StreakLength, director.StreakSpeed), Is.EqualTo((1, 1f)));
            director.BeginSlot(Facts(9, Empty, 0, Attack, 10));
            Assert.That(director.StreakSpeed, Is.EqualTo(1.15f).Within(Tolerance));

            // The finishing blow keeps its finale.
            director.BeginSlot(new LegacySlotFacts(new LegacyClashInput(10, Attack, 10, 1, Empty, 0, 1, decided: true),
                playerMaxHealth: 100, enemyMaxHealth: 100, enemyHealthLost: 10, enemyDefeated: true));
            Assert.That(director.SlotRank.Tier, Is.EqualTo(LegacySlotTier.Climax));
            Assert.That((director.StreakLength, director.StreakSpeed), Is.EqualTo((3, 1f)));
        }

        [Test]
        public void ACounterFillingTheSlot_OrADodge_SettlesItAtTheFirstHit_AndStartsItOver()
        {
            var director = Director();
            director.BeginSlot(Facts(0, Breath, 0, Attack, 10));
            director.BeginSlot(Facts(1, Breath, 0, Attack, 10));
            // A pending 반격 reads as an empty slot until it strikes: what the start tells is a third unopposed slot…
            director.BeginSlot(Facts(2, Empty, 0, Attack, 10));
            Assert.That(director.StreakLength, Is.EqualTo(3));
            Assert.That(director.StreakSpeed, Is.EqualTo(1.3f).Within(Tolerance));
            // …which the counter's strike makes a 합: the run starts over (the controller held the speed back till now).
            director.Observe(Facts(2, Attack, 9, Attack, 10, decided: true, playerCountered: true));
            Assert.That((director.StreakLength, director.StreakSpeed), Is.EqualTo((0, 1f)));
            director.EndSlot();
            Assert.That((director.StreakLength, director.GapSpeed), Is.EqualTo((0, 1f)));

            director.BeginSlot(Facts(3, Breath, 0, Attack, 10));
            director.BeginSlot(Facts(4, Empty, 0, Attack, 10));
            Assert.That(director.StreakSpeed, Is.EqualTo(1.15f).Within(Tolerance));
            director.Observe(new LegacySlotFacts(new LegacyClashInput(4, Empty, 0, 1, Attack, 10, 1, playerDodged: true,
                decided: true), playerMaxHealth: 100, enemyMaxHealth: 100));
            Assert.That((director.StreakLength, director.StreakSpeed), Is.EqualTo((0, 1f)), "A dodge is both acting.");
        }

        [Test]
        public void SwitchedOff_WithTheDirectorOrOnItsOwn_NothingSpeedsUp_ButTheStreakCounts()
        {
            var off = new LegacyShowDirector();
            off.BeginTurn(LegacyShowPacingTuning.Off, LegacySlotImportanceTuning.Default, LegacyStreakTuning.Default);
            var own = new LegacyShowDirector();
            own.BeginTurn(LegacyShowPacingTuning.Default, LegacySlotImportanceTuning.Default, LegacyStreakTuning.Off);
            foreach (LegacyShowDirector director in new[] { off, own })
            {
                Assert.That(director.StreakEnabled, Is.False);
                for (int slot = 0; slot < 4; slot++)
                {
                    director.BeginSlot(Facts(slot, Attack, 10, Empty, 0));
                    Assert.That(director.StreakLength, Is.EqualTo(slot + 1));
                    Assert.That(director.StreakSpeed, Is.EqualTo(1f));
                    director.EndSlot();
                    Assert.That(director.GapSpeed, Is.EqualTo(1f));
                }
            }
        }

        [Test]
        public void BothVersusScreens_PlayEverySlotAtTheSameSpeed()
        {
            LegacySlotImportanceTuning symmetric = Symmetric();
            var host = new LegacyShowDirector();
            var guest = new LegacyShowDirector();
            host.BeginTurn(LegacyShowPacingTuning.Default, symmetric, LegacyStreakTuning.Default);
            guest.BeginTurn(LegacyShowPacingTuning.Default, symmetric, LegacyStreakTuning.Default);
            var slots = new[]
            {
                Facts(0, Attack, 10, Empty, 0), Facts(1, Breath, 0, Attack, 10), Facts(2, Attack, 10, Breath, 0),
                Facts(3, Guard, 5, Empty, 0), Facts(4, Empty, 0, Attack, 10),
                new LegacySlotFacts(new LegacyClashInput(5, Attack, 10, 1, Empty, 0, 1), 51, 0, 100, 100),
                Facts(6, Attack, 10, Empty, 0), Facts(7, Attack, 10, Attack, 4), Facts(8, Empty, 0, Attack, 10),
            };
            float[] expected = { 1f, 1.15f, 1.3f, 1f, 1.45f, 1f, 1.6f, 1f, 1f };
            for (int slot = 0; slot < slots.Length; slot++)
            {
                host.BeginSlot(slots[slot]);
                guest.BeginSlot(slots[slot].Mirrored());
                Assert.That(host.StreakSpeed, Is.EqualTo(expected[slot]).Within(Tolerance), "slot " + slot);
                Assert.That(guest.StreakSpeed, Is.EqualTo(host.StreakSpeed), "slot " + slot + ": the same on both screens.");
                Assert.That(guest.StreakLength, Is.EqualTo(host.StreakLength));
                host.EndSlot();
                guest.EndSlot();
                Assert.That(guest.GapSpeed, Is.EqualTo(host.GapSpeed));
            }
        }

        [Test]
        public void CampaignDuel_ARunOfAttacksIntoNothing_PlaysFaster_UntilACounterStrikes()
        {
            var duel = new LegacyQueuedDuel(1000, 1000, 1000, 1000,
                new[] { AttackSkill(960, 10), AttackSkill(970, 10, lane: 1) }, new LegacySkill[0],
                new[] { 0 }, meshPercent: 0);
            for (int index = 0; index < 4; index++)
                Assert.That(duel.TryQueueLane(index / LegacyLaneLimit.MaxReservations), Is.True);
            duel.Commit();
            Assert.That(PlayTurn(duel, Director()), Is.EqualTo(new[] { 1f, 1.15f, 1.3f, 1.45f }));

            // Two breaths against the enemy's attacks, then an empty slot a 반격 fills: the run starts over at its strike.
            var countered = new LegacyQueuedDuel(1000, 1000, 1000, 1000, new[] { AttackSkill(961, 10) },
                new[] { AttackSkill(962, 10), AttackSkill(963, 10), AttackSkill(964, 10), AttackSkill(965, 10) }, new[] { 4 },
                playerCounter: new LegacyCounter(AttackSkill(966, 9)), meshPercent: 0);
            Assert.That(countered.TryQueueBreath() && countered.TryQueueBreath(), Is.True);
            countered.Commit();
            float[] speeds = PlayTurn(countered, Director(), out int[] streaks);
            Assert.That(streaks, Is.EqualTo(new[] { 1, 2, 0, 1 }), "The counter filled the third slot (a 합); the fourth is the enemy's again.");
            Assert.That(speeds, Is.EqualTo(new[] { 1f, 1.15f, 1f, 1f }));
        }

        [Test]
        public void VersusMatch_TheHostsRun_PlaysAtTheSameSpeedOnBothScreens()
        {
            var match = new LocalVersusMatch(new[] { AttackSkill(967, 10) }, new[] { AttackSkill(968, 10) }, 1000, 1000, 1000, 1000,
                randomSeed: 5);
            ILegacyDuelView host = new LocalVersusPerspective(match, 0), guest = new LocalVersusPerspective(match, 1);
            int queued = 0;
            while (match.Phase == LegacyDuelPhase.Planning)
            {
                int planner = match.CurrentPlanner;
                if (planner == 0 && queued < 3)
                {
                    Assert.That(match.TryQueueLane(0, 0), Is.True);
                    queued++;
                }
                else Assert.That(match.TryPass(planner), Is.True);
            }
            Assert.That(queued, Is.EqualTo(3));
            LegacySlotImportanceTuning symmetric = Symmetric();
            var hostDirector = new LegacyShowDirector(LegacyShowPacingTuning.Default, symmetric);
            var guestDirector = new LegacyShowDirector(LegacyShowPacingTuning.Default, symmetric);
            hostDirector.BeginTurn(LegacyShowPacingTuning.Default, symmetric, LegacyStreakTuning.Default);
            guestDirector.BeginTurn(LegacyShowPacingTuning.Default, symmetric, LegacyStreakTuning.Default);
            var speeds = new List<float>();
            while (!match.IsTurnResolved)
            {
                match.BeginNextSlot();
                hostDirector.BeginSlot(host.CurrentSlotFacts);
                guestDirector.BeginSlot(guest.CurrentSlotFacts);
                Assert.That(guestDirector.StreakSpeed, Is.EqualTo(hostDirector.StreakSpeed), "Settled at the start: no 반격 in versus.");
                while (!match.CurrentSlot.IsResolved)
                {
                    match.ResolveNextHit();
                    hostDirector.Observe(host.CurrentSlotFacts);
                    guestDirector.Observe(guest.CurrentSlotFacts);
                }
                Assert.That(guestDirector.StreakSpeed, Is.EqualTo(hostDirector.StreakSpeed));
                speeds.Add(hostDirector.StreakSpeed);
                match.CompleteCurrentSlot();
                hostDirector.EndSlot();
                guestDirector.EndSlot();
                Assert.That(guestDirector.GapSpeed, Is.EqualTo(hostDirector.GapSpeed));
            }
            Assert.That(speeds.Count, Is.EqualTo(3));
            Assert.That(speeds[0], Is.EqualTo(1f));
            Assert.That(speeds[1], Is.EqualTo(1.15f).Within(Tolerance));
            Assert.That(speeds[2], Is.EqualTo(1.3f).Within(Tolerance));
        }

        private static float[] PlayTurn(LegacyQueuedDuel duel, LegacyShowDirector director) => PlayTurn(duel, director, out _);

        // Plays a committed turn through the director as the controller does; each slot's speed after its first hit.
        private static float[] PlayTurn(LegacyQueuedDuel duel, LegacyShowDirector director, out int[] streaks)
        {
            var speeds = new List<float>();
            var lengths = new List<int>();
            while (!duel.IsTurnResolved)
            {
                duel.BeginNextSlot();
                director.BeginSlot(duel.CurrentSlotFacts);
                while (!duel.IsCurrentSlotResolved)
                {
                    duel.ResolveNextHit();
                    director.Observe(duel.CurrentSlotFacts);
                }
                speeds.Add((float)Math.Round(director.StreakSpeed, 4));
                lengths.Add(director.StreakLength);
                duel.CompleteCurrentSlot();
                director.EndSlot();
            }
            streaks = lengths.ToArray();
            return speeds.ToArray();
        }

        private static LegacyShowDirector Director()
        {
            var director = new LegacyShowDirector();
            director.BeginTurn(LegacyShowPacingTuning.Default, LegacySlotImportanceTuning.Default, LegacyStreakTuning.Default);
            return director;
        }

        // As online versus rates: the enemy's 간파 lifts a slot as the player's does.
        private static LegacySlotImportanceTuning Symmetric()
            => new LegacySlotImportanceTuning(LegacySlotImportanceTuning.DefaultBladeBindGap,
                LegacySlotImportanceTuning.DefaultLargeClashGap, LegacySlotImportanceTuning.DefaultDeepPierce,
                LegacySlotImportanceTuning.DefaultBigUnopposedPercent, LegacyDecisiveHit.DefaultHealthDamagePercent,
                LegacySlotImportanceTuning.DefaultMeshChainMinimum, true, true, LegacySlotImportance.DefaultSignatureSkillIds);

        private static LegacyClashKind Kind(LegacyClashInput input) => LegacyClashMotion.Classify(input).Kind;

        private static LegacySlotRank Rank(LegacyClashKind kind, LegacySlotTier tier = LegacySlotTier.Normal)
            => new LegacySlotRank(0, tier, 0, LegacySlotHighlight.None, LegacyReadEvent.None, LegacyReadEvent.None, false, false,
                kind);

        private static LegacySlotFacts Facts(int slot, LegacyClashAction player, int playerPower, LegacyClashAction enemy,
            int enemyPower, bool decided = false, bool playerCountered = false)
            => new LegacySlotFacts(new LegacyClashInput(slot, player, playerPower, 1, enemy, enemyPower, 1,
                playerCountered: playerCountered, decided: decided), playerMaxHealth: 100, enemyMaxHealth: 100);

        private static LegacyClashInput In(LegacyClashAction player, int playerPower, int playerHits,
            LegacyClashAction enemy, int enemyPower, int enemyHits, bool playerDodged = false)
            => new LegacyClashInput(0, player, playerPower, playerHits, enemy, enemyPower, enemyHits, playerDodged);

        private static LegacySkill AttackSkill(int id, int power, int lane = 0)
            => new LegacySkill(id, "streak attack", 0, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, lane, "");
    }
}
