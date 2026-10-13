using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Sheets;
using Column = TurnLimbo.Runtime.LegacyCombat.LegacySkillSheet.Column;

namespace TurnLimbo.Core.Tests
{
    /// <summary>A skill sheet for tests: the shipped rows plus rows written here, installed for the length of one test, so the
    /// keyword effects (예약 효과, 사용 효과) are tried on numbers that never change with the shipped sheet.</summary>
    internal static class KeywordSheet
    {
        /// <summary>A 획득 row with a fixed power (minimum = maximum) and one hit; <paramref name="cells"/> set any other column.</summary>
        internal static string[] Row(int id, string lane, string kind, int cost, int power, params (string column, string value)[] cells)
        {
            IReadOnlyList<string> headers = LegacySkillSheet.Headers;
            var row = new string[headers.Count];
            for (int index = 0; index < row.Length; index++) row[index] = string.Empty;
            void Set(string column, string value) => row[headers.ToList().IndexOf(column)] = value;
            Set(Column.Id, id.ToString(CultureInfo.InvariantCulture));
            Set(Column.Name, "시험 " + id.ToString(CultureInfo.InvariantCulture));
            Set(Column.Group, LegacySkillSheet.AcquisitionGroup);
            Set(Column.Lane, lane);
            Set(Column.Kind, kind);
            Set(Column.Property, kind == "방어" ? "방어" : "참격");
            Set(Column.Cost, cost.ToString(CultureInfo.InvariantCulture));
            Set(Column.MinPower, power.ToString(CultureInfo.InvariantCulture));
            Set(Column.MaxPower, power.ToString(CultureInfo.InvariantCulture));
            Set(Column.Hits, "1");
            Set(Column.ShortLabel, "시험");
            Set(Column.Detail, "시험");
            foreach ((string column, string value) in cells) Set(column, value);
            return row;
        }

        /// <summary>Runs <paramref name="body"/> with the shipped sheet plus <paramref name="rows"/> installed, then goes back to
        /// the shipped sheet.</summary>
        internal static void With(Action body, params string[][] rows)
        {
            List<string[]> sheet = CsvTable.Read(LegacySkillSheet.Write(LegacySkillDefinitions.Table))
                .Select(record => record.Fields.ToArray()).ToList();
            sheet.AddRange(rows);
            string csv = CsvTable.Write(sheet);
            try
            {
                LegacySkillDefinitions.Install(() => csv);
                body();
            }
            finally
            {
                LegacySkillDefinitions.Install(null);
            }
        }

        // The rows the keyword tests share (ids clear of the sheet's and of the tests' own ad hoc skills).
        internal const int CycleQ = 901, CycleW = 902, Feeder = 903, BreathPayoff = 904, ReductionPayoff = 905,
            CyclePayoff = 906, AutoBreathGuard = 907, Dodger = 908, Returner = 909, ReductionQ = 911, ReductionMissE = 912,
            SecondFeeder = 913, ReductionPayoffMaxOne = 915;

        internal static string[][] Rows => new[]
        {
            // 순환 carriers: +2 per use up to 2 (Q), +5 per use up to 2 (W).
            Row(CycleQ, "Q", "공격", 1, 5, (Column.CyclePowerPerUse, "2"), (Column.CycleMaxCount, "2")),
            Row(CycleW, "W", "공격", 1, 10, (Column.CyclePowerPerUse, "5"), (Column.CycleMaxCount, "2")),
            // 순환 +1 feeders.
            Row(Feeder, "E", "공격", 1, 6, (Column.ReserveEffect, "순환 +1")),
            Row(SecondFeeder, "W", "공격", 1, 4, (Column.ReserveEffect, "순환 +1")),
            // Count-ahead payoffs.
            Row(BreathPayoff, "E", "공격", 2, 6, (Column.UseEffect, "앞 숨고르기마다 위력 +4 (최대 4)")),
            Row(ReductionPayoff, "W", "공격", 3, 16, (Column.UseEffect, "앞 저항 감소 기술마다 위력 +5 (최대 3)")),
            Row(ReductionPayoffMaxOne, "W", "공격", 1, 16, (Column.UseEffect, "앞 저항 감소 기술마다 위력 +5 (최대 1)")),
            Row(CyclePayoff, "Q", "공격", 1, 6, (Column.UseEffect, "앞 순환 기술마다 위력 +3 (최대 3)")),
            // 숨고르기 +1 on a guard, 자동 회피 on an attack, a returning guard.
            Row(AutoBreathGuard, "Q", "방어", 1, 5, (Column.ReserveEffect, "숨고르기 +1")),
            Row(Dodger, "E", "공격", 1, 7, (Column.UseEffect, "자동 회피")),
            Row(Returner, "W", "방어", 1, 5, (Column.QueueRemovalMode, "반환")),
            // 저항 감소 carriers: one unconditional, one gated on a guard it will not meet.
            Row(ReductionQ, "Q", "공격", 1, 3, (Column.ResistanceReduction, "5")),
            Row(ReductionMissE, "E", "공격", 1, 3, (Column.ResistanceReduction, "5"), (Column.OpponentKind, "방어")),
        };

        /// <summary>Runs <paramref name="body"/> with <see cref="Rows"/> installed.</summary>
        internal static void WithKeywords(Action body) => With(body, Rows);

        internal static LegacySkill Skill(int id) => LegacySkillDefinitions.Skill(id);
    }

    /// <summary>The effects that read this turn's queue (<see cref="LegacyKeywordLinks"/>): 순환 +1 at the commit, the count-ahead
    /// power of 사용 효과, their once-per-turn rules, and the same numbers in versus.</summary>
    public sealed class KeywordLinkTests
    {
        private const int Q = 0, W = 1, E = 2;

        [Test]
        public void CycleFeed_RaisesTheCyclingSkillsAheadAtTheCommit_OncePerKind_AndThisTurnsSlotsReadIt()
        {
            KeywordSheet.WithKeywords(() =>
            {
                LegacyQueuedDuel duel = Duel(KeywordSheet.CycleQ, KeywordSheet.CycleW, KeywordSheet.Feeder);
                Queue(duel, Q, Q, W, E);
                Assert.That(Ids(duel.PlayerQueue), Is.EqualTo(new[] { 901, 901, 902, 903 }));
                Assert.That((duel.CycleUses(901), duel.CycleUses(902)), Is.EqualTo((0, 0)), "Nothing moves while planning.");
                Assert.That(Enumerable.Range(0, 4).Select(duel.CycleUsesAtSlot), Is.EqualTo(new[] { 1, 2, 1, 0 }),
                    "Each slot shows the count it will read: the feed, then one more per earlier copy, within the cap.");

                duel.Commit();
                Assert.That((duel.CycleUses(901), duel.CycleUses(902)), Is.EqualTo((1, 1)), "Two copies of 901 still gain one.");
                Assert.That(Enumerable.Range(0, 4).Select(duel.CycleUsesAtSlot), Is.EqualTo(new[] { 1, 2, 1, 0 }));
                duel.BeginNextSlot();
                Assert.That(duel.CurrentSlot.PlayerCycleUses, Is.EqualTo(1));
                Assert.That(Enumerable.Range(0, 4).Select(duel.CycleUsesAtSlot), Is.EqualTo(new[] { 1, 2, 1, 0 }),
                    "Resolving: the current slot shows what it read, the later ones what they will read.");
                Assert.That(Damage(duel), Is.EqualTo(new[] { 5 + 2, 5 + 4, 10 + 5, 6 }), "This turn's slots are already stronger.");
                Assert.That((duel.CycleUses(901), duel.CycleUses(902)), Is.EqualTo((2, 2)));

                // At the cap the feed has nothing left to give.
                duel.BeginNextTurn();
                Queue(duel, Q, E);
                var uses = new Dictionary<int, int> { [901] = 2 };
                Assert.That(LegacyKeywordLinks.ApplyCycleFeed(duel.PlayerQueue, uses), Is.Zero);
                duel.Commit();
                Assert.That(duel.CycleUses(901), Is.EqualTo(2));
                Assert.That(Damage(duel)[0], Is.EqualTo(9));
            });
        }

        [Test]
        public void CycleFeed_OnlyTheFirstCopyOfAFeederFeeds_OnlyWhatIsAheadOfIt_AndEachFeederKindAddsOne()
        {
            KeywordSheet.WithKeywords(() =>
            {
                // A second copy of the feeder adds nothing more.
                LegacyQueuedDuel duel = Duel(KeywordSheet.CycleQ, KeywordSheet.Feeder);
                Queue(duel, Q, E, E);
                duel.Commit();
                Assert.That(duel.CycleUses(901), Is.EqualTo(1));

                // The feeder's first copy stands before the target: the later copy does not count either.
                duel = Duel(KeywordSheet.CycleQ, KeywordSheet.Feeder);
                Queue(duel, E, Q, E);
                Assert.That(duel.CycleUsesAtSlot(1), Is.Zero);
                duel.Commit();
                Assert.That(duel.CycleUses(901), Is.Zero);
                Assert.That(Damage(duel)[1], Is.EqualTo(5));

                // Two kinds of feeder: one each.
                duel = Duel(KeywordSheet.CycleQ, KeywordSheet.Feeder, KeywordSheet.SecondFeeder);
                Queue(duel, Q, E, W);
                Assert.That(LegacyKeywordLinks.CycleFeedReceived(duel.PlayerQueue, 901), Is.EqualTo(2));
                var raised = new List<int>();
                Assert.That(LegacyKeywordLinks.ApplyCycleFeed(duel.PlayerQueue, new Dictionary<int, int>(), raised), Is.EqualTo(1));
                Assert.That(raised, Is.EqualTo(new[] { 901 }));
                duel.Commit();
                Assert.That(duel.CycleUses(901), Is.EqualTo(2));
                Assert.That(Damage(duel)[0], Is.EqualTo(5 + 4));
            });
        }

        [Test]
        public void CycleFeed_ASourceOrTargetTakenOffBeforeTheCommit_GivesAndGetsNothing_SoReturningCannotStackIt()
        {
            KeywordSheet.WithKeywords(() =>
            {
                LegacyQueuedDuel duel = Duel(KeywordSheet.CycleQ, KeywordSheet.Returner, KeywordSheet.Feeder);
                Queue(duel, Q, E);
                Assert.That(duel.CycleUsesAtSlot(0), Is.EqualTo(1), "The preview counts the feeder…");
                Queue(duel, W);
                Assert.That(Ids(duel.PlayerQueue), Is.EqualTo(new[] { 909 }), "…until 반환 takes both back.");
                Assert.That(duel.CycleUses(901), Is.Zero, "Nothing was applied, so nothing needs undoing.");
                // Queue both again after the returner: one feed, from the committed queue alone.
                Queue(duel, Q, E);
                duel.Commit();
                Assert.That(duel.CycleUses(901), Is.EqualTo(1), "Not 2: the returned pair never counted.");

                duel = Duel(KeywordSheet.CycleQ, KeywordSheet.Returner, KeywordSheet.Feeder);
                Queue(duel, Q, E, W, Q);
                duel.Commit();
                Assert.That(duel.CycleUses(901), Is.Zero, "The feeder was returned: the target re-queued after it gains nothing.");
            });
        }

        [Test]
        public void AheadPower_CountsBreathsByEntry_FirstCopyOnly_AndBreathsSurviveRemoval()
        {
            KeywordSheet.WithKeywords(() =>
            {
                LegacyQueuedDuel duel = Duel(KeywordSheet.CycleQ, KeywordSheet.BreathPayoff);
                Assert.That(duel.TryQueueBreath() && duel.TryQueueBreath(), Is.True);
                Queue(duel, Q);
                Assert.That(duel.TryQueueBreath(), Is.True);
                LegacySkill payoff = duel.GetLane(E)[0];
                Assert.That(LegacyKeywordLinks.AheadPowerBonus(duel.PlayerQueue, duel.PlayerQueue.Count, payoff), Is.EqualTo(12),
                    "The preview counts the live queue as if the skill were appended.");
                Queue(duel, E, E);
                Assert.That(LegacyKeywordLinks.AheadPowerCount(duel.PlayerQueue, 4, payoff), Is.EqualTo(3));
                Assert.That(LegacyKeywordLinks.AheadPowerCount(duel.PlayerQueue, 5, payoff), Is.Zero, "The second copy counts nothing.");
                duel.Commit();
                int[] damage = Damage(duel);
                Assert.That(damage[4], Is.EqualTo(6 + 3 * 4));
                Assert.That(damage[5], Is.EqualTo(6), "Once per turn: the second copy is plain.");

                // The breaths stay when 반환 takes the skills between them.
                duel = Duel(KeywordSheet.CycleQ, KeywordSheet.Returner, KeywordSheet.BreathPayoff);
                Assert.That(duel.TryQueueBreath(), Is.True);
                Queue(duel, Q);
                Assert.That(duel.TryQueueBreath(), Is.True);
                Queue(duel, W, E);
                Assert.That(Ids(duel.PlayerQueue), Is.EqualTo(new[] { -1, -1, 909, 904 }));
                duel.Commit();
                Assert.That(Damage(duel)[3], Is.EqualTo(6 + 2 * 4));
            });
        }

        [Test]
        public void AheadPower_ReachesItsCapOfFourBreaths_WithTheAutomaticOne()
        {
            KeywordSheet.WithKeywords(() =>
            {
                LegacyQueuedDuel duel = Duel(KeywordSheet.AutoBreathGuard, KeywordSheet.BreathPayoff);
                Queue(duel, Q);
                for (int count = 0; count < LegacyQueuedDuel.MaximumBreathsPerTurn; count++) Assert.That(duel.TryQueueBreath(), Is.True);
                Assert.That(duel.TryQueueBreath(), Is.False);
                Queue(duel, E);
                Assert.That(duel.PlayerQueue.Count(skill => skill.IsWait), Is.EqualTo(4));
                duel.Commit();
                Assert.That(Damage(duel)[5], Is.EqualTo(6 + 4 * 4));
            });
        }

        [Test]
        public void AheadPower_CountsReductionKindsWhetherOrNotTheirConditionHolds_UpToTheMaximum()
        {
            KeywordSheet.WithKeywords(() =>
            {
                LegacyQueuedDuel duel = Duel(KeywordSheet.ReductionQ, KeywordSheet.ReductionMissE, KeywordSheet.ReductionPayoff);
                Queue(duel, Q, Q, E, W);
                Assert.That(LegacyKeywordLinks.CountAhead(duel.PlayerQueue, 3, LegacyAheadTarget.ResistanceReduction), Is.EqualTo(2),
                    "Two copies of one reduction skill are one kind.");
                duel.Commit();
                int[] damage = Damage(duel);
                Assert.That(damage[3], Is.EqualTo(16 + 2 * 5));
                Assert.That(duel.Enemy.Resistance, Is.EqualTo(90), "The gated reduction missed and still counted.");

                duel = Duel(KeywordSheet.ReductionQ, KeywordSheet.ReductionMissE, KeywordSheet.ReductionPayoffMaxOne);
                Queue(duel, Q, E, W);
                duel.Commit();
                Assert.That(Damage(duel)[2], Is.EqualTo(16 + 5), "최대 1.");
            });
        }

        [Test]
        public void AheadPower_CountsCyclingKinds_AndOnlyWhatIsAhead()
        {
            KeywordSheet.WithKeywords(() =>
            {
                LegacyQueuedDuel duel = Duel(KeywordSheet.CycleQ, KeywordSheet.CyclePayoff, KeywordSheet.CycleW);
                Queue(duel, Q, W, Q, Q);
                Assert.That(Ids(duel.PlayerQueue), Is.EqualTo(new[] { 901, 902, 906, 901 }));
                Assert.That(LegacyKeywordLinks.CountAhead(duel.PlayerQueue, 2, LegacyAheadTarget.CycleSkill), Is.EqualTo(2));
                Assert.That(LegacyKeywordLinks.CountAhead(duel.PlayerQueue, 4, LegacyAheadTarget.CycleSkill), Is.EqualTo(2),
                    "A later copy of a counted kind adds nothing.");
                duel.Commit();
                Assert.That(Damage(duel)[2], Is.EqualTo(6 + 2 * 3));
            });
        }

        [Test]
        public void ForecastingTheNewEffects_NeverChangesTheLiveDuel()
        {
            KeywordSheet.WithKeywords(() =>
            {
                Func<LegacyQueuedDuel> make = () => new LegacyQueuedDuel(1000, 50, 1000, 100,
                    new[] { KeywordSheet.Skill(901), KeywordSheet.Skill(907), KeywordSheet.Skill(902), KeywordSheet.Skill(903) },
                    new EnemyScript(new[] { new[] { Attack(701, 9), Attack(702, 9) } }), 5, playerActGainBonus: 7);
                LegacyQueuedDuel duel = make(), untouched = make();
                for (int turn = 0; turn < 3; turn++)
                {
                    foreach (LegacyQueuedDuel side in new[] { duel, untouched }) Queue(side, Q, W, Q, E);
                    string before = SlotForecastTests.Snapshot(duel);
                    Assert.That(before, Is.EqualTo(SlotForecastTests.Snapshot(untouched)));
                    for (int look = 0; look < 3; look++)
                    {
                        duel.ForecastTurn();
                        for (int lane = 0; lane < 3; lane++) duel.ForecastIfQueued(lane);
                    }
                    Assert.That(SlotForecastTests.Snapshot(duel), Is.EqualTo(before), "Forecasting changes nothing the duel shows.");
                    duel.Commit();
                    untouched.Commit();
                    while (!duel.IsTurnResolved && !duel.IsFinished)
                        Assert.That(duel.ResolveNextSlot().EnemyHealthDamage, Is.EqualTo(untouched.ResolveNextSlot().EnemyHealthDamage));
                    Assert.That(SlotForecastTests.Snapshot(duel), Is.EqualTo(SlotForecastTests.Snapshot(untouched)));
                    duel.BeginNextTurn();
                    untouched.BeginNextTurn();
                }
            });
        }

        [Test]
        public void Versus_FeedsCyclesAndCountsAheadAsTheCampaignDoes()
        {
            KeywordSheet.WithKeywords(() =>
            {
                LegacySkill[] skills = { KeywordSheet.Skill(901), KeywordSheet.Skill(902), KeywordSheet.Skill(903) };
                var match = new LocalVersusMatch(skills, skills, randomSeed: 3);
                Assert.That(match.TryQueueLane(0, Q), Is.True);
                Assert.That(match.TryPass(1), Is.True);
                Assert.That(match.TryQueueLane(0, W), Is.True);
                Assert.That(match.TryPass(1), Is.True);
                Assert.That(match.TryQueueLane(0, E), Is.True);
                var view = new LocalVersusPerspective(match, 0);
                Assert.That(Enumerable.Range(0, 3).Select(view.CycleUsesAtSlot), Is.EqualTo(new[] { 1, 1, 0 }));
                Assert.That(new LocalVersusPerspective(match, 1).EnemyCycleUsesAtSlot(0), Is.EqualTo(1), "The opponent reads it too.");
                Assert.That(match.TryPass(1) && match.TryPass(0), Is.True);
                Assert.That(match.Phase, Is.EqualTo(LegacyDuelPhase.Resolving));
                Assert.That((match.Left.CycleUses(901), match.Left.CycleUses(902)), Is.EqualTo((1, 1)));
                var damage = new List<int>();
                while (!match.IsTurnResolved) damage.Add(match.ResolveNextSlot().RightHealthDamage);
                Assert.That(damage, Is.EqualTo(new[] { 5 + 2, 10 + 5, 6 }));

                LegacySkill[] breathers = { KeywordSheet.Skill(904) };
                match = new LocalVersusMatch(breathers, breathers, randomSeed: 3);
                Assert.That(match.TryQueueBreath(0) && match.TryPass(1) && match.TryQueueBreath(0) && match.TryPass(1), Is.True);
                Assert.That(match.TryQueueLane(0, E), Is.True);
                Assert.That(match.TryPass(1), Is.True);
                Assert.That(match.TryQueueLane(0, E), Is.False, "904 costs 2 of the opening 3 ACT: one copy only.");
                Assert.That(match.TryPass(0), Is.True, "Right passed last, so Left's pass commits.");
                Assert.That(match.Phase, Is.EqualTo(LegacyDuelPhase.Resolving));
                damage.Clear();
                while (!match.IsTurnResolved) damage.Add(match.ResolveNextSlot().RightHealthDamage);
                Assert.That(damage, Is.EqualTo(new[] { 0, 0, 6 + 2 * 4 }));
            });
        }

        [Test]
        public void Helpers_ReadOnlyTheQueueTheyAreGiven()
        {
            KeywordSheet.WithKeywords(() =>
            {
                LegacySkill cycle = KeywordSheet.Skill(901), payoff = KeywordSheet.Skill(904);
                var queue = new[] { LegacyCommonActions.Breathe, cycle, LegacyCommonActions.Breathe, payoff, payoff };
                Assert.That(LegacyKeywordLinks.CountAhead(queue, 3, LegacyAheadTarget.Breath), Is.EqualTo(2));
                Assert.That(LegacyKeywordLinks.CountAhead(queue, 99, LegacyAheadTarget.Breath), Is.EqualTo(2), "Past the end counts all.");
                Assert.That(LegacyKeywordLinks.CountAhead(queue, 0, LegacyAheadTarget.Breath), Is.Zero);
                Assert.That(LegacyKeywordLinks.CountAhead(null, 3, LegacyAheadTarget.Breath), Is.Zero);
                Assert.That(LegacyKeywordLinks.CountAhead(queue, 3, LegacyAheadTarget.None), Is.Zero);
                Assert.That(LegacyKeywordLinks.IsFirstOfKind(queue, 3), Is.True);
                Assert.That(LegacyKeywordLinks.IsFirstOfKind(queue, 4), Is.False);
                Assert.That(LegacyKeywordLinks.IsFirstOfKind(queue, 0), Is.False, "A breath is never a kind of its own.");
                Assert.That(LegacyKeywordLinks.AheadPowerBonus(queue, 1, cycle), Is.Zero, "No 사용 효과, no bonus.");
                Assert.That(LegacyKeywordLinks.PlannedCycleUses(queue, 1, 1), Is.EqualTo(1));
                Assert.That(LegacyKeywordLinks.PlannedCycleUses(queue, 3, 1), Is.Zero, "904 does not cycle.");
            });
        }

        internal static LegacyQueuedDuel Duel(params int[] ids)
            => new LegacyQueuedDuel(1000, 50, 1000, 100, ids.Select(KeywordSheet.Skill).ToArray(), Array.Empty<LegacySkill>(),
                new[] { 0 }, randomSeed: 1, playerActGainBonus: 7, meshPercent: 0);

        internal static void Queue(LegacyQueuedDuel duel, params int[] lanes)
        {
            foreach (int lane in lanes) Assert.That(duel.TryQueueLane(lane), Is.True, "lane " + lane);
        }

        // Every slot's health damage to the enemy (the passive target takes each attack whole: no guard, unbroken).
        private static int[] Damage(LegacyQueuedDuel duel)
        {
            var damage = new List<int>();
            while (!duel.IsTurnResolved && !duel.IsFinished) damage.Add(duel.ResolveNextSlot().EnemyHealthDamage);
            return damage.ToArray();
        }

        private static LegacySkill Attack(int id, int power)
            => new LegacySkill(id, "attack", 1, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "");

        private static int[] Ids(IReadOnlyList<LegacySkill> skills) => skills.Select(skill => skill.Id).ToArray();
    }
}
