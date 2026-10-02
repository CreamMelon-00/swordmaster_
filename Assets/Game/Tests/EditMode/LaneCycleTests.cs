using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;

namespace TurnLimbo.Core.Tests
{
    public sealed class LaneCycleTests
    {
        private static int[] Lane(LegacyQueuedDuel duel, int lane) => duel.GetLane(lane).Select(skill => skill.Id).ToArray();

        [Test]
        public void Cycle_TurnsEveryOpenLaneTogetherForFreeAndKeepsTheQueue()
        {
            var duel = new LegacyQueuedDuel(100, 50, 1000, 1000, LegacyInitialSkills.All,
                new[] { LegacySkillDefinitions.Skill(1) }, new[] { 1 }, 3);
            int[] q = Lane(duel, 0), w = Lane(duel, 1), e = Lane(duel, 2);
            int act = duel.Act;
            Assert.That(duel.TryCycleLanes(), Is.True);
            Assert.That(Lane(duel, 0), Is.EqualTo(new[] { q[1], q[2], q[0] }), "The front skill goes to the back unused.");
            Assert.That(Lane(duel, 1), Is.EqualTo(new[] { w[1], w[2], w[0] }), "Every lane turns at once.");
            Assert.That(Lane(duel, 2), Is.EqualTo(new[] { e[1], e[2], e[0] }));
            Assert.That(duel.Act, Is.EqualTo(act), "넘기기 is free.");
            Assert.That(duel.PlayerQueue, Is.Empty, "Nothing is queued.");
            Assert.That(duel.LaneCyclesThisTurn, Is.EqualTo(1));
            Assert.That(duel.TryCycleLanes() && duel.TryCycleLanes(), Is.True);
            Assert.That(Lane(duel, 0), Is.EqualTo(q), "Three presses bring three-skill lanes back.");
            Assert.That(duel.LaneCyclesThisTurn, Is.EqualTo(3), "There is no limit.");
        }

        [Test]
        public void Queueing_TurnsOnlyItsOwnLane_SoCyclingAloneNeverChangesHowTheLanesLineUp()
        {
            var duel = new LegacyQueuedDuel(100, 50, 1000, 1000, LegacyInitialSkills.All,
                new[] { LegacySkillDefinitions.Skill(1) }, new[] { 1 }, 3);
            int[] w = Lane(duel, 1);
            Assert.That(duel.TryQueueLane(0), Is.True, "Using a skill turns only the Q lane.");
            Assert.That(Lane(duel, 1), Is.EqualTo(w));
            Assert.That(duel.TryCycleLanes(), Is.True);
            Assert.That(duel.PlayerQueue.Count, Is.EqualTo(1), "넘기기 never touches what is already queued.");
            Assert.That(Lane(duel, 1), Is.EqualTo(new[] { w[1], w[2], w[0] }));
        }

        [Test]
        public void Cycle_OnlyDuringPlanningWhenOpen_AndClosedOrSingleSkillLanesStayPut()
        {
            var closed = new LegacyQueuedDuel(100, 50, 1000, 1000, LegacyInitialSkills.All,
                new[] { LegacySkillDefinitions.Skill(1) }, new[] { 1 }, 3, features: CombatFeature.LaneQ);
            int[] q = Lane(closed, 0);
            Assert.That(closed.TryCycleLanes(), Is.False, "넘기기 is closed.");
            Assert.That(Lane(closed, 0), Is.EqualTo(q));
            Assert.That(closed.LaneCyclesThisTurn, Is.Zero);

            var qOnly = new LegacyQueuedDuel(100, 50, 1000, 1000, LegacyInitialSkills.All,
                new[] { LegacySkillDefinitions.Skill(1) }, new[] { 1 }, 3, features: CombatFeature.LaneQ | CombatFeature.Cycle);
            Assert.That(qOnly.TryCycleLanes(), Is.True);
            Assert.That(qOnly.GetLane(1), Is.Empty, "A closed lane has nothing to turn.");

            var single = new LegacyQueuedDuel(100, 50, 1000, 1000, new[] { LegacySkillDefinitions.Skill(1) },
                new[] { LegacySkillDefinitions.Skill(1) }, new[] { 1 }, 3);
            Assert.That(single.TryCycleLanes(), Is.False, "A one-skill lane has nothing to bring forward.");

            var duel = new LegacyQueuedDuel(100, 50, 1000, 1000, LegacyInitialSkills.All,
                new[] { LegacySkillDefinitions.Skill(1) }, new[] { 1 }, 3);
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            Assert.That(duel.TryCycleLanes(), Is.False, "Only while planning.");
        }

        [Test]
        public void CycledOrder_CarriesIntoTheNextTurn_AndTheCountRestarts()
        {
            var duel = new LegacyQueuedDuel(100, 50, 1000, 1000, LegacyInitialSkills.All,
                new[] { LegacySkillDefinitions.Skill(1) }, new[] { 1 }, 3);
            int[] q = Lane(duel, 0);
            Assert.That(duel.TryCycleLanes(), Is.True);
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            while (!duel.IsTurnResolved && !duel.IsFinished) duel.ResolveNextSlot();
            duel.BeginNextTurn();
            Assert.That(Lane(duel, 0), Is.EqualTo(new[] { q[2], q[0], q[1] }), "The lanes keep their order between turns.");
            Assert.That(duel.LaneCyclesThisTurn, Is.Zero);
            duel.Reset();
            Assert.That(Lane(duel, 0), Is.EqualTo(q), "A new match starts from the loadout order.");
        }

        [Test]
        public void Guide_CycleBeatWaitsForShiftAndLocksQueueingMeanwhile()
        {
            var guide = new MissionGuide(new[]
            {
                new MissionGuideBeat(MissionGuideStepKind.Cycle, "넘기기", "", ""),
                new MissionGuideBeat(MissionGuideStepKind.Free, "자유", "", ""),
            });
            Assert.That(guide.AllowsCycle, Is.True);
            Assert.That(guide.AllowsQueue(0) || guide.AllowsCommit || guide.AllowsBreath, Is.False);
            guide.NotifyQueued(0);
            Assert.That(guide.Kind, Is.EqualTo(MissionGuideStepKind.Cycle));
            guide.NotifyCycled();
            Assert.That(guide.IsFree && guide.AllowsCycle, Is.True);
            guide.Finish();
            Assert.That(guide.AllowsCycle, Is.False);
        }

        [Test]
        public void MissionTwo_TeachesCycleWhereItBringsSharpSlashForward()
        {
            PrologueMission mission = PrologueMissions.Get(2);
            Assert.That(mission.Features.Has(CombatFeature.Cycle), Is.True);
            Assert.That(PrologueMissions.Get(1).Features.Has(CombatFeature.Cycle), Is.False, "Mission 1 is about striking only.");
            Assert.That(mission.CreateGuide().Beats.Count(beat => beat.Kind == MissionGuideStepKind.Cycle), Is.EqualTo(1));
            LegacyQueuedDuel duel = mission.CreateDuel(1);
            Assert.That(duel.GetLane(0)[0].Id, Is.EqualTo(LegacySkillDefinitions.Skill(1).Id));
            Assert.That(duel.TryCycleLanes(), Is.True);
            Assert.That(duel.GetLane(0)[0].Id, Is.EqualTo(LegacySkillDefinitions.Skill(2).Id), "예리한 베기 comes forward.");
        }
    }
}
