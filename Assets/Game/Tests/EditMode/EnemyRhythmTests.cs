using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    public sealed class EnemyRhythmTests
    {
        private static LegacySkill S(int id) => LegacySkillDefinitions.Skill(id);

        private static int[] Ids(IEnumerable<LegacySkill> skills) => skills.Select(skill => skill.Id).ToArray();

        [Test]
        public void Script_PlaysItsOpeningOnceThenLoops()
        {
            var script = new EnemyScript(
                new IReadOnlyList<LegacySkill>[] { new[] { S(1) }, new[] { S(3), S(5) } },
                new IReadOnlyList<LegacySkill>[] { new[] { S(2), S(6), S(4) } });
            Assert.That(script.OpeningLength, Is.EqualTo(1));
            Assert.That(script.LoopLength, Is.EqualTo(2));
            Assert.That(Ids(script.Turn(1)), Is.EqualTo(Ids(new[] { S(2), S(6), S(4) })), "The opening comes first…");
            Assert.That(Ids(script.Turn(2)), Is.EqualTo(Ids(new[] { S(1) })), "…then the loop…");
            Assert.That(Ids(script.Turn(3)), Is.EqualTo(Ids(new[] { S(3), S(5) })));
            Assert.That(Ids(script.Turn(4)), Is.EqualTo(Ids(new[] { S(1) })), "…which repeats, never the opening again.");
            Assert.That(script.TurnSizes, Is.EqualTo(new[] { 3, 1, 2 }));
            Assert.That(script.AllSkills.Count, Is.EqualTo(6));
            Assert.Throws<ArgumentOutOfRangeException>(() => script.Turn(0));
        }

        [Test]
        public void Script_RejectsEmptyTurnsAndNullActions_AndSelectMapsEverySkill()
        {
            Assert.Throws<ArgumentException>(() => new EnemyScript(new IReadOnlyList<LegacySkill>[0]));
            Assert.Throws<ArgumentException>(() => new EnemyScript(new IReadOnlyList<LegacySkill>[] { new LegacySkill[0] }));
            Assert.Throws<ArgumentException>(() => new EnemyScript(new IReadOnlyList<LegacySkill>[] { new LegacySkill[] { null } }));
            Assert.Throws<ArgumentNullException>(() => new EnemyScript(null));
            var script = new EnemyScript(new IReadOnlyList<LegacySkill>[] { new[] { S(1), S(2) } },
                new IReadOnlyList<LegacySkill>[] { new[] { S(6) } });
            EnemyScript stronger = script.Select(skill => new LegacySkill(skill.Id, skill.Name, skill.Cost, skill.MinPower + 2,
                skill.MaxPower + 2, skill.Kind, skill.Property, skill.AttackCount, skill.LaneIndex, skill.Description));
            Assert.That(stronger.OpeningLength, Is.EqualTo(1));
            Assert.That(stronger.Turn(1)[0].MinPower, Is.EqualTo(S(6).MinPower + 2));
            Assert.That(stronger.Turn(2)[1].MaxPower, Is.EqualTo(S(2).MaxPower + 2));
        }

        [Test]
        public void ScriptedDuel_QueuesEachTurnFromTheScript()
        {
            EnemyScript script = CampaignEnemyRhythms.Script(CampaignEnemyRhythm.Opener);
            var duel = new LegacyQueuedDuel(9999, 9999, 9999, 9999, LegacyInitialSkills.All, script, 3);
            for (int round = 1; round <= 9; round++)
            {
                Assert.That(duel.RoundNumber, Is.EqualTo(round));
                Assert.That(Ids(duel.EnemyQueue), Is.EqualTo(Ids(script.Turn(round))), "Turn " + round);
                duel.Commit();
                while (!duel.IsTurnResolved && !duel.IsFinished) duel.ResolveNextSlot();
                Assert.That(duel.IsFinished, Is.False);
                duel.BeginNextTurn();
            }
            duel.Reset();
            Assert.That(Ids(duel.EnemyQueue), Is.EqualTo(Ids(script.Turn(1))), "A rematch starts the opening again.");
        }

        [Test]
        public void Stages_FollowTheirRhythms_AndStagesOneAndTwoKeepTheOriginalCycle()
        {
            Assert.That(Enumerable.Range(1, 8).Select(CampaignEnemyRhythms.ForStage), Is.EqualTo(new[]
            {
                CampaignEnemyRhythm.Basic, CampaignEnemyRhythm.Basic, CampaignEnemyRhythm.Gatekeeper, CampaignEnemyRhythm.Opener,
                CampaignEnemyRhythm.Onslaught, CampaignEnemyRhythm.Charge, CampaignEnemyRhythm.Opener, CampaignEnemyRhythm.Charge,
            }));
            Assert.That(CampaignEnemyRhythms.Script(CampaignEnemyRhythm.Basic), Is.Null, "The original cycle has no script.");
            var run = new CampaignRun();
            for (int stage = 1; stage <= run.StageCount; stage++)
                Assert.That(run.GetStage(stage).EnemyRhythm, Is.EqualTo(CampaignEnemyRhythms.ForStage(stage)));
        }

        [Test]
        public void RedesignedStarterRhythms_KeepTheHeavyTelegraphsAfterSkillCostsChange()
        {
            EnemyScript gatekeeper = CampaignEnemyRhythms.Script(CampaignEnemyRhythm.Gatekeeper);
            Assert.That(Ids(gatekeeper.Turn(4)), Is.EqualTo(new[] { 4 }),
                "The single open-slot blow still uses a costly W attack.");
            Assert.That(gatekeeper.Turn(4)[0].Cost, Is.EqualTo(3));

            EnemyScript opener = CampaignEnemyRhythms.Script(CampaignEnemyRhythm.Opener);
            Assert.That(Ids(opener.Turn(1)), Is.EqualTo(new[] { 4, 3, 2 }));
            Assert.That(Ids(opener.Turn(2)), Is.EqualTo(new[] { 4, 6, 3 }));
            Assert.That(opener.Turn(1).Count(skill => skill.LaneIndex == 1), Is.EqualTo(2));
            Assert.That(opener.Turn(2).Count(skill => skill.LaneIndex == 1), Is.EqualTo(2));

            EnemyScript charge = CampaignEnemyRhythms.Script(CampaignEnemyRhythm.Charge);
            Assert.That(Ids(charge.Turn(3)), Is.EqualTo(new[] { 3, 4 }));
            Assert.That(charge.Turn(3).All(skill => skill.LaneIndex == 1 && skill.Kind == LegacySkillKind.Attack), Is.True);
        }

        [Test]
        public void Rhythms_AskTheirQuestions()
        {
            bool IsGuard(LegacySkill skill) => skill.Kind == LegacySkillKind.Defence;
            int Power(IReadOnlyList<LegacySkill> turn) => turn.Where(skill => !IsGuard(skill)).Sum(skill => skill.MaxPower);

            // 수문장: a turn of nothing but guards, longer than the free 숨고르기 can step past, and a turn with one
            // heavy blow and the other slots open.
            EnemyScript gatekeeper = CampaignEnemyRhythms.Script(CampaignEnemyRhythm.Gatekeeper);
            IReadOnlyList<LegacySkill>[] gate = Enumerable.Range(1, gatekeeper.LoopLength).Select(gatekeeper.Turn).ToArray();
            Assert.That(gate.Any(turn => turn.All(IsGuard) && turn.Count > LegacyQueuedDuel.MaximumBreathsPerTurn), Is.True);
            Assert.That(gate.Any(turn => turn.Count == 1 && !IsGuard(turn[0]) && turn[0].Cost >= 3), Is.True);

            // 기선 제압: every opening turn hits harder than any looping turn, but the loop is only a little lighter.
            EnemyScript opener = CampaignEnemyRhythms.Script(CampaignEnemyRhythm.Opener);
            Assert.That(opener.OpeningLength, Is.EqualTo(2));
            int[] openingPower = Enumerable.Range(1, opener.OpeningLength).Select(round => Power(opener.Turn(round))).ToArray();
            int[] loopPower = Enumerable.Range(opener.OpeningLength + 1, opener.LoopLength).Select(round => Power(opener.Turn(round))).ToArray();
            Assert.That(openingPower.Min(), Is.GreaterThan(loopPower.Max()));
            Assert.That(loopPower.Average(), Is.GreaterThanOrEqualTo(.6 * openingPower.Average()), "Only a little lighter.");
            Assert.That(Enumerable.Range(opener.OpeningLength + 1, opener.LoopLength)
                .All(round => opener.Turn(round).Count(skill => !IsGuard(skill)) >= 2), Is.True);

            // 속공: at least three actions every turn.
            EnemyScript onslaught = CampaignEnemyRhythms.Script(CampaignEnemyRhythm.Onslaught);
            Assert.That(Enumerable.Range(1, onslaught.LoopLength).All(round => onslaught.Turn(round).Count >= 3), Is.True);

            // 일격: guarding turns, then a turn of two attacks heavier than any single turn before it.
            EnemyScript charge = CampaignEnemyRhythms.Script(CampaignEnemyRhythm.Charge);
            Assert.That(charge.Turn(1).All(IsGuard) && charge.Turn(2).All(IsGuard), Is.True);
            Assert.That(charge.Turn(3).Count(skill => !IsGuard(skill)), Is.EqualTo(2));

            // Every scripted rhythm keeps at least one guard, so the curriculum's defence-reading skills still work.
            foreach (EnemyScript script in new[] { gatekeeper, opener, onslaught, charge })
                Assert.That(script.AllSkills.Any(IsGuard), Is.True);
        }
    }
}
