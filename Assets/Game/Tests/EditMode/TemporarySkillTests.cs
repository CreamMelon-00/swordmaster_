using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    /// <summary>일회성 (Docs/SkillSheet.md, 예약 효과·사용 효과): 58 숫돌's 예약 효과 <c>일회성 59 ×3</c> sets three 59 갈아내기 at the
    /// front of the Q lane once per turn; reserving one uses it up, the copies outlive their generator's removal, a new turn
    /// takes the ones left, their 순환 count is shared and lasts the battle, and versus and the forecast copy follow the same
    /// rules.</summary>
    public sealed class TemporarySkillTests
    {
        private const int Q = 0, W = 1, E = 2;
        private const int Grind = 59, Whetstone = 58, Doublet = 56, HorizontalCut = 14, OpeningE = 5;

        [Test]
        public void Whetstone_SetsThreeCopiesAtTheFrontOfTheQLane_OncePerTurn_AndReservingOneUsesItUp()
        {
            LegacyQueuedDuel duel = Duel(HorizontalCut, Whetstone, OpeningE);
            Assert.That(Ids(duel.GetLane(Q)), Is.EqualTo(new[] { HorizontalCut }));
            Assert.That(duel.Act, Is.EqualTo(10));

            Queue(duel, W);
            Assert.That(Ids(duel.GetLane(Q)), Is.EqualTo(new[] { Grind, Grind, Grind, HorizontalCut }));
            Assert.That(duel.ReserveEffectsUsedThisTurn & LegacyReserveEffect.Temporary, Is.EqualTo(LegacyReserveEffect.Temporary));
            Assert.That(duel.GetLane(Q)[0], Is.SameAs(LegacySkillDefinitions.Skill(Grind)), "The sheet's own skill.");

            Queue(duel, Q);
            Assert.That(Ids(duel.PlayerQueue), Is.EqualTo(new[] { Whetstone, Grind }));
            Assert.That(Ids(duel.GetLane(Q)), Is.EqualTo(new[] { Grind, Grind, HorizontalCut }), "Used up, never sent to the back.");
            Assert.That(duel.Act, Is.EqualTo(8), "Each copy costs its ACT.");

            Queue(duel, W);
            Assert.That(Ids(duel.GetLane(Q)), Is.EqualTo(new[] { Grind, Grind, HorizontalCut }),
                "Once per turn: 숫돌 again sets nothing.");

            Queue(duel, Q, Q);
            Assert.That(Ids(duel.PlayerQueue), Is.EqualTo(new[] { Whetstone, Grind, Whetstone, Grind, Grind }));
            Assert.That(Ids(duel.GetLane(Q)), Is.EqualTo(new[] { HorizontalCut }));
            // One 순환 count for every copy (one id): the slots read 0, 1, 2 within the cap.
            Assert.That(new[] { 1, 3, 4 }.Select(duel.CycleUsesAtSlot), Is.EqualTo(new[] { 0, 1, 2 }));

            duel.Commit();
            while (!duel.IsTurnResolved) duel.ResolveNextSlot();
            Assert.That(duel.CycleUses(Grind), Is.EqualTo(2), "The count is the battle's, capped at 2.");

            duel.BeginNextTurn();
            Assert.That(duel.ReserveEffectsUsedThisTurn, Is.EqualTo(LegacyReserveEffect.None));
            Queue(duel, W, Q);
            Assert.That(Ids(duel.GetLane(Q)), Is.EqualTo(new[] { Grind, Grind, HorizontalCut }), "A new turn sets them again.");
            Assert.That(duel.CycleUsesAtSlot(1), Is.EqualTo(2), "Today's copies start sharp: the count outlived its copies.");
            duel.Commit();
            while (!duel.IsTurnResolved) duel.ResolveNextSlot();
            duel.BeginNextTurn();
            Assert.That(Ids(duel.GetLane(Q)), Is.EqualTo(new[] { HorizontalCut }), "The two left went with the turn.");
        }

        [Test]
        public void CopiesOutliveTheirGeneratorsRemoval_AReturnedCopyIsGone_AndReturningCannotSetThemAgain()
        {
            LegacyQueuedDuel duel = Duel(HorizontalCut, Whetstone, Doublet);
            Assert.That(Ids(duel.GetLane(W)), Is.EqualTo(new[] { Whetstone, Doublet }));
            Queue(duel, W, Q);
            Assert.That(duel.Act, Is.EqualTo(8));
            Queue(duel, W);
            Assert.That(Ids(duel.PlayerQueue), Is.EqualTo(new[] { Doublet }), "두블레 returns both.");
            Assert.That(duel.Act, Is.EqualTo(8 - 3 + 2), "반환 refunds the copy's ACT too.");
            Assert.That(duel.QueuePowerRemovedSkillCount, Is.EqualTo(2));
            Assert.That(Ids(duel.GetLane(Q)), Is.EqualTo(new[] { Grind, Grind, HorizontalCut }),
                "The copies set stay (as breaths do); the returned copy does not come back.");
            Queue(duel, W);
            Assert.That(Ids(duel.PlayerQueue), Is.EqualTo(new[] { Doublet, Whetstone }));
            Assert.That(Ids(duel.GetLane(Q)), Is.EqualTo(new[] { Grind, Grind, HorizontalCut }), "The bit holds: no second set.");
        }

        [Test]
        public void AClosedLane_GetsNoCopies_AndTheTurnKeepsItsSet()
        {
            var duel = new LegacyQueuedDuel(1000, 50, 1000, 100,
                new[] { LegacySkillDefinitions.Skill(HorizontalCut), LegacySkillDefinitions.Skill(Whetstone),
                    LegacySkillDefinitions.Skill(OpeningE) }, Array.Empty<LegacySkill>(), new[] { 0 }, randomSeed: 1,
                features: CombatFeature.All & ~CombatFeature.LaneQ, playerActGainBonus: 7, meshPercent: 0);
            LegacySkill whetstone = duel.GetLane(W)[0];
            Assert.That(LegacyKeywordPreview.LaneLine(duel, whetstone), Is.Null, "No line for a lane the duel does not have.");
            Queue(duel, W);
            Assert.That(duel.GetLane(Q), Is.Empty);
            Assert.That(duel.ReserveEffectsUsedThisTurn, Is.EqualTo(LegacyReserveEffect.None));
        }

        [Test]
        public void Screens_SayWhatWillComeAndTagTheCopies()
        {
            LegacyQueuedDuel duel = Duel(HorizontalCut, Whetstone, OpeningE);
            LegacySkill whetstone = duel.GetLane(W)[0];
            Assert.That(LegacyKeywordPreview.LaneLine(duel, whetstone), Is.EqualTo("갈아내기 ×3"));
            Assert.That(LegacyKeywordPreview.Note(duel, whetstone), Is.EqualTo("일회성 · 지금 예약하면 Q 열 맨 앞에 갈아내기 3개"));
            Assert.That(LegacyKeywordPreview.TemporarySpawnCue(whetstone), Is.EqualTo("갈아내기 ×3"));
            Assert.That(LegacyKeywordPreview.TemporaryCounts(duel), Is.EqualTo(new[] { 0, 0, 0 }));
            Queue(duel, W);
            Assert.That(LegacyKeywordPreview.TemporaryCounts(duel), Is.EqualTo(new[] { 3, 0, 0 }));
            Assert.That(LegacyKeywordPreview.LaneLine(duel, whetstone), Is.EqualTo(LegacyKeywordPreview.UsedThisTurn));
            Assert.That(LegacyKeywordPreview.Note(duel, whetstone), Is.EqualTo("일회성 · 턴당 1회 · 사용함"));
            Queue(duel, Q);
            LegacyKeywordMark mark = LegacyKeywordPreview.QueueMark(duel, true, 1, true);
            Assert.That((mark.Text, mark.Kind, mark.Spent), Is.EqualTo((LegacyKeywordPreview.TemporaryTag,
                LegacyKeywordMarkKind.Temporary, false)));
            Assert.That(LegacyKeywordPreview.QueueMark(duel, true, 0, true).IsEmpty, Is.True, "숫돌's own card carries no mark.");
            Assert.That(LegacyKeywordPreview.CycleCount(duel, true, 1, true, out _, out _), Is.EqualTo("0/2"), "Its 순환 count.");
        }

        [Test]
        public void ForecastCopies_SetAndUseCopiesOfTheirOwn_AndTheLiveDuelNeverSeesThem()
        {
            LegacyQueuedDuel duel = Duel(HorizontalCut, Whetstone, OpeningE);
            string before = SlotForecastTests.Snapshot(duel);
            Assert.That(duel.ForecastIfQueued(W), Is.Not.Null);
            duel.ForecastTurn();
            Assert.That(SlotForecastTests.Snapshot(duel), Is.EqualTo(before), "The lanes and the bit stay as they were.");

            Queue(duel, W);
            before = SlotForecastTests.Snapshot(duel);
            LegacySlotForecast grind = duel.ForecastIfQueued(Q);
            Assert.That(grind.PlayerSkill.Id, Is.EqualTo(Grind));
            duel.ForecastTurn();
            Assert.That(SlotForecastTests.Snapshot(duel), Is.EqualTo(before), "The copy's use of a 갈아내기 stays in the copy.");
            Assert.That(Ids(duel.GetLane(Q)), Is.EqualTo(new[] { Grind, Grind, Grind, HorizontalCut }));
        }

        [Test]
        public void Versus_SetsUsesAndClearsCopiesAsTheCampaignDoes_AndNeverOffersThemInTheLoadout()
        {
            LegacySkill[] skills = { LegacySkillDefinitions.Skill(HorizontalCut), LegacySkillDefinitions.Skill(Whetstone),
                LegacySkillDefinitions.Skill(OpeningE) };
            var match = new LocalVersusMatch(skills, skills, randomSeed: 3);
            Assert.That(match.TryQueueLane(0, W), Is.True);
            Assert.That(Ids(match.Left.GetLane(Q)), Is.EqualTo(new[] { Grind, Grind, Grind, HorizontalCut }));
            Assert.That(Ids(match.Right.GetLane(Q)), Is.EqualTo(new[] { HorizontalCut }), "Only the reserver's lane.");
            Assert.That(match.TryPass(1), Is.True);
            Assert.That(match.TryQueueLane(0, Q), Is.True);
            Assert.That(Ids(match.Left.Queue), Is.EqualTo(new[] { Whetstone, Grind }));
            Assert.That(Ids(match.Left.GetLane(Q)), Is.EqualTo(new[] { Grind, Grind, HorizontalCut }));
            var view = new LocalVersusPerspective(match, 0);
            Assert.That(LegacyKeywordPreview.QueueMark(view, true, 1, true).Kind, Is.EqualTo(LegacyKeywordMarkKind.Temporary));
            Assert.That(LegacyKeywordPreview.QueueMark(new LocalVersusPerspective(match, 1), false, 1, true).Text,
                Is.EqualTo(LegacyKeywordPreview.TemporaryTag), "The opponent's copy is tagged too.");
            Assert.That(match.TryPass(1) && match.TryPass(0), Is.True);
            while (!match.IsTurnResolved) match.ResolveNextSlot();
            Assert.That(match.Left.CycleUses(Grind), Is.EqualTo(1));
            match.BeginNextTurn();
            Assert.That(Ids(match.Left.GetLane(Q)), Is.EqualTo(new[] { HorizontalCut }), "The two left went with the turn.");
            Assert.That(match.Left.ReserveEffectsUsedThisTurn, Is.EqualTo(LegacyReserveEffect.None));

            // The pool is the 시작 and 획득 rows: 숫돌 and 잔걸음 are in it, 갈아내기 never.
            var loadout = new VersusLoadout();
            Assert.That(loadout.TryPlaceSkill(Grind, Q, 0), Is.False);
            int[] ids = loadout.ExportIds();
            ids[0] = Grind;
            Assert.That(loadout.TryImportIds(ids), Is.False);
            Assert.That(loadout.TryPlaceSkill(57, Q, 0) && loadout.TryPlaceSkill(Whetstone, W, 0), Is.True);
        }

        private static LegacyQueuedDuel Duel(params int[] ids)
            => new LegacyQueuedDuel(1000, 50, 1000, 100, ids.Select(LegacySkillDefinitions.Skill).ToArray(),
                Array.Empty<LegacySkill>(), new[] { 0 }, randomSeed: 1, playerActGainBonus: 7, meshPercent: 0);

        private static void Queue(LegacyQueuedDuel duel, params int[] lanes)
        {
            foreach (int lane in lanes) Assert.That(duel.TryQueueLane(lane), Is.True, "lane " + lane);
        }

        private static int[] Ids(IReadOnlyList<LegacySkill> skills) => skills.Select(skill => skill.Id).ToArray();
    }
}
