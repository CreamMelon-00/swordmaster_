using System;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;

namespace TurnLimbo.Core.Tests
{
    public sealed class PrologueMissionTests
    {
        [Test]
        public void Arc_HasFourOrderedMissionsWithBriefingsAndDialogueStubs()
        {
            Assert.That(PrologueMissions.Count, Is.EqualTo(4));
            Assert.That(PrologueMissions.All.Select(m => m.Number), Is.EqualTo(new[] { 1, 2, 3, 4 }));
            Assert.That(PrologueMissions.All.Select(m => m.Title).Distinct().Count(), Is.EqualTo(4));
            foreach (PrologueMission mission in PrologueMissions.All)
            {
                Assert.That(mission.Objectives, Is.Not.Empty);
                Assert.That(mission.Enemies, Is.Not.Empty);
                Assert.That(mission.BackgroundResource, Is.Not.Empty);
                Assert.That(mission.IntroDialogue, Is.EqualTo($"Dialogue/mission-{mission.Number:00}-intro"));
                Assert.That(mission.OutroDialogue, Is.EqualTo($"Dialogue/mission-{mission.Number:00}-outro"));
                Assert.That(PrologueMissions.Get(mission.Number), Is.SameAs(mission));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => PrologueMissions.Get(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => PrologueMissions.Get(5));
        }

        [Test]
        public void EveryMission_UsesTheQLaneOnlyWithStepsAndBreathingLocked()
        {
            foreach (PrologueMission mission in PrologueMissions.All)
            {
                Assert.That(mission.LaneCount, Is.EqualTo(1));
                Assert.That(mission.StepsEnabled, Is.False);
                Assert.That(mission.BreathEnabled, Is.False);
                LegacyQueuedDuel duel = mission.CreateDuel();
                Assert.That(duel.GetLane(0), Is.Not.Empty, mission.Title);
                Assert.That(duel.GetLane(1), Is.Empty, mission.Title);
                Assert.That(duel.GetLane(2), Is.Empty, mission.Title);
                Assert.That(duel.TryQueueLane(1), Is.False);
                Assert.That(duel.Enemy.MaxResistance, Is.GreaterThan(0), "A zero-resistance enemy would stay broken.");
            }
            Assert.That(PrologueMissions.All.Select(m => m.PlanningTimer), Is.EqualTo(new[] { false, false, false, true }));
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(42)]
        public void MissionOne_NeverAttacksAndMashingQWinsInTwoTurns(int seed)
        {
            LegacyQueuedDuel duel = PrologueMissions.Get(1).CreateDuel(seed);
            int turns = PlayByMashingQ(duel, 4);
            Assert.That(duel.Outcome, Is.EqualTo(DuelMatchOutcome.PlayerVictory));
            Assert.That(turns, Is.LessThanOrEqualTo(2));
            Assert.That(duel.Player.Health, Is.EqualTo(PrologueMission.PlayerHealth));
            Assert.That(duel.Player.Resistance, Is.EqualTo(PrologueMission.PlayerResistance));
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(42)]
        public void MissionTwo_ClashesBreakTheEnemyOnTheFirstTurn(int seed)
        {
            PrologueMission mission = PrologueMissions.Get(2);
            LegacyQueuedDuel duel = mission.CreateDuel(seed);
            // Follow the coach exactly: its first turn is what the break copy describes.
            MissionGuide guide = mission.CreateGuide();
            Assert.That(QueueGuidedTurn(guide, duel), Is.EqualTo(2), "Both enemy attacks are met by a clash.");
            duel.Commit();
            while (!duel.IsTurnResolved && !duel.IsFinished) duel.ResolveNextSlot();
            Assert.That(duel.Enemy.IsResistanceBroken, Is.True, "The guided first turn shows a break.");
            Assert.That(duel.Player.Resistance, Is.LessThan(PrologueMission.PlayerResistance), "The enemy attacks back.");
        }

        [Test]
        public void MissionThree_GuidedQueueBlocksTheDownwardSlashForTwoExtraAct()
        {
            PrologueMission mission = PrologueMissions.Get(3);
            LegacyQueuedDuel duel = mission.CreateDuel();
            Assert.That(duel.GetLane(0)[0].Name, Is.EqualTo("베기"));
            Assert.That(QueueGuidedTurn(mission.CreateGuide(), duel), Is.EqualTo(2));
            Assert.That(duel.PlayerQueue[1].Name, Is.EqualTo("막기"), "The guide's second beat asks for 막기.");
            Assert.That(duel.EnemyQueue[1].Property, Is.EqualTo(LegacySkillProperty.Hit));
            duel.Commit();
            while (!duel.IsTurnResolved) duel.ResolveNextSlot();
            duel.BeginNextTurn();
            Assert.That(duel.Act, Is.EqualTo(1 + 3 + 1 + 2), "Leftover 1, natural 3, 베기 +1, 막기 vs a Hit attack +2.");
            Assert.That(duel.Player.Health, Is.EqualTo(PrologueMission.PlayerHealth));
        }

        [Test]
        public void MissionGuide_WaitsForEachBeatsInputAndEndsFree()
        {
            MissionGuide guide = PrologueMissions.Get(1).CreateGuide();
            Assert.That(guide.IsOpening, Is.True);
            Assert.That(guide.Kind, Is.EqualTo(MissionGuideStepKind.Info));
            Assert.That(guide.CanAdvance, Is.True);
            Assert.That(guide.AllowsQueue(0), Is.False);
            Assert.That(guide.AllowsCommit, Is.False);
            Assert.That(guide.TryAdvance(), Is.True);
            Assert.That(guide.Kind, Is.EqualTo(MissionGuideStepKind.Queue));
            Assert.That(guide.ExpectedLane, Is.Zero);
            Assert.That(guide.TryAdvance(), Is.False, "Action beats never skip.");
            Assert.That(guide.AllowsQueue(1), Is.False);
            guide.NotifyQueued(1);
            Assert.That(guide.StepNumber, Is.EqualTo(2));
            guide.NotifyQueued(0);
            guide.NotifyQueued(0);
            Assert.That(guide.Kind, Is.EqualTo(MissionGuideStepKind.Commit));
            Assert.That(guide.AllowsCommit, Is.True);
            Assert.That(guide.FocusesCommit, Is.True);
            guide.NotifyCommitted();
            Assert.That(guide.Kind, Is.EqualTo(MissionGuideStepKind.WatchTurn));
            guide.NotifyTurnBegan(1);
            Assert.That(guide.Kind, Is.EqualTo(MissionGuideStepKind.WatchTurn), "Only the next turn ends the watch.");
            guide.NotifyTurnBegan(2);
            Assert.That(guide.IsFree, Is.True);
            Assert.That(guide.AllowsQueue(0), Is.True);
            Assert.That(guide.AllowsCommit, Is.True);
            Assert.That(guide.StepNumber, Is.EqualTo(guide.StepCount));
            guide.Finish();
            Assert.That(guide.IsComplete, Is.True);
            Assert.That(guide.AllowsQueue(0), Is.False);
            Assert.That(guide.AllowsCommit, Is.False);
        }

        [Test]
        public void EveryGuide_EndsFreeAndOnlyQueuesTheQLane()
        {
            foreach (PrologueMission mission in PrologueMissions.All)
            {
                MissionGuide guide = mission.CreateGuide();
                Assert.That(guide, Is.Not.Null, mission.Title);
                Assert.That(guide.Beats.All(beat => beat.Lane == -1 || beat.Lane == 0), Is.True, mission.Title);
                Assert.That(guide.Beats.Last().Kind, Is.EqualTo(MissionGuideStepKind.Free), mission.Title);
                Assert.That(ReferenceEquals(mission.CreateGuide(), guide), Is.False, "Each attempt gets a fresh coach.");
            }
            Assert.Throws<ArgumentException>(() => new MissionGuide(new[]
            {
                new MissionGuideBeat(MissionGuideStepKind.Info, "a", "", ""),
            }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MissionGuideBeat(MissionGuideStepKind.Queue, "a", "", ""));
        }

        [Test]
        public void EveryGuide_ScriptedQueuesAreAffordableAndReachTheCommitBeat()
        {
            foreach (PrologueMission mission in PrologueMissions.All)
            {
                MissionGuide guide = mission.CreateGuide();
                LegacyQueuedDuel duel = mission.CreateDuel();
                int queued = QueueGuidedTurn(guide, duel);
                Assert.That(queued, Is.EqualTo(guide.Beats.Count(beat => beat.Kind == MissionGuideStepKind.Queue)), mission.Title);
                Assert.That(guide.AllowsCommit, Is.True, mission.Title);
            }
        }

        [Test]
        public void PrologueRun_AdvancesOnlyOnInOrderVictoriesAndCompletes()
        {
            var run = new PrologueRun();
            Assert.That(run.CurrentMission.Number, Is.EqualTo(1));
            Assert.That(run.TryComplete(2, DuelMatchOutcome.PlayerVictory), Is.False);
            Assert.That(run.TryComplete(1, DuelMatchOutcome.EnemyVictory), Is.False);
            Assert.That(run.TryComplete(1, DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.CurrentMission.Number, Is.EqualTo(2));
            Assert.That(run.IsCleared(1), Is.True);
            Assert.That(run.TryComplete(1, DuelMatchOutcome.PlayerVictory), Is.False, "Replays never move progress.");
            for (int number = 2; number <= 4; number++)
                Assert.That(run.TryComplete(number, DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.IsComplete, Is.True);
            Assert.That(run.CurrentMission, Is.Null);
            Assert.That(run.TryComplete(5, DuelMatchOutcome.PlayerVictory), Is.False);
            run.Reset();
            Assert.That(run.CurrentMission.Number, Is.EqualTo(1));
            run.CompleteAll();
            Assert.That(run.IsComplete, Is.True);
        }

        /// <summary>Walks the coach up to its first commit, queueing exactly what each Queue beat asks for.</summary>
        private static int QueueGuidedTurn(MissionGuide guide, LegacyQueuedDuel duel)
        {
            int queued = 0;
            while (!guide.AllowsCommit)
            {
                if (guide.CanAdvance) Assert.That(guide.TryAdvance(), Is.True);
                else if (guide.AllowsInspect) guide.NotifyInspected();
                else
                {
                    Assert.That(guide.Kind, Is.EqualTo(MissionGuideStepKind.Queue), "Only reading, inspecting and queueing precede the commit.");
                    Assert.That(duel.TryQueueLane(guide.ExpectedLane), Is.True, guide.Title + " must be affordable.");
                    guide.NotifyQueued(guide.ExpectedLane);
                    queued++;
                }
            }
            return queued;
        }

        private static int PlayByMashingQ(LegacyQueuedDuel duel, int maximumTurns)
        {
            int turns = 0;
            while (!duel.IsFinished && turns < maximumTurns)
            {
                turns++;
                while (duel.TryQueueLane(0)) { }
                duel.Commit();
                while (!duel.IsTurnResolved && !duel.IsFinished) duel.ResolveNextSlot();
                if (!duel.IsFinished) duel.BeginNextTurn();
            }
            return turns;
        }
    }
}
