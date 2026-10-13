using System;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;

namespace TurnLimbo.Core.Tests
{
    /// <summary>What the curriculum screens read from the tree and the progress: which nodes end their branch (the trunk's
    /// last step, each school's capstone), what the last completion opened (the tree's "새" tag, the result's
    /// <c>새로 열린 과정</c>), and the result snapshot that carries both.</summary>
    public sealed class CurriculumScreenTests
    {
        [Test]
        public void DefaultTree_TheTrunksLastStepAndTheThreeCapstonesEndTheirBranches_ABridgeNever()
        {
            CurriculumTree tree = CampaignCurriculum.Default;
            string[] ends = tree.Nodes.Where(node => tree.EndsBranch(node.Id)).Select(node => node.Id).ToArray();
            Assert.That(ends, Is.EqualTo(new[] { "advance", "estocada", "cadence", "doublet" }));
            string[] capstones = tree.Nodes.Where(node => tree.IsCapstone(node.Id)).Select(node => node.Id).ToArray();
            Assert.That(capstones, Is.EqualTo(new[] { "estocada", "cadence", "doublet" }), "The trunk has no capstone.");
            Assert.That(tree.EndsBranch("battement"), Is.False, "A bridge joins two schools; it ends neither.");
            Assert.That(tree.EndsBranch("missing"), Is.False);
            Assert.That(tree.IsCapstone(null), Is.False);
            foreach (string capstone in capstones)
                Assert.That(tree.Find(capstone).StatReward.IsEmpty, Is.False, capstone + " grants the capstone's stat.");
        }

        [Test]
        public void OpenedBy_NamesOnlyTheNodesTheCompletionOpened()
        {
            var run = new CampaignRun();
            CurriculumProgress curriculum = run.Curriculum;
            Assert.That(curriculum.LastCompleted, Is.Null);
            Assert.That(curriculum.OpenedBy("horizontal-cut"), Is.Empty, "Nothing is opened by a node not yet completed.");

            Complete(run, "horizontal-cut");
            Assert.That(curriculum.LastCompleted.Id, Is.EqualTo("horizontal-cut"));
            Assert.That(Ids(curriculum.OpenedBy("horizontal-cut")), Is.EqualTo(new[] { "preparation" }));
            Complete(run, "preparation", "advance");
            Assert.That(Ids(curriculum.OpenedBy("advance")), Is.EqualTo(new[] { "quick-draw", "diagonal-cut", "suppleness" }),
                "The trunk's end opens the three schools.");
            Complete(run, "quick-draw", "fighting-spirit");
            Assert.That(Ids(curriculum.OpenedBy("fighting-spirit")), Is.EqualTo(new[] { "estocada" }));
            Complete(run, "esquive");
            Assert.That(curriculum.OpenedBy("esquive"), Is.Empty,
                "The capstone was already open through its other twist, and the bridge still needs 순환's twist.");
            Complete(run, "diagonal-cut", "vital-thrust");
            Assert.That(Ids(curriculum.OpenedBy("vital-thrust")), Is.EqualTo(new[] { "battement", "cadence" }));
            Assert.That(curriculum.LastCompleted.Id, Is.EqualTo("vital-thrust"));

            Assert.That(run.TrySelectCurriculumNode("battement"), Is.True);
            Assert.That(Ids(curriculum.OpenedBy("vital-thrust")), Is.EqualTo(new[] { "battement", "cadence" }),
                "A node now in progress was still opened by it.");
            Assert.That(run.TryResetCurriculum(), Is.True);
            Assert.That(curriculum.LastCompleted, Is.Null);
            Assert.That(curriculum.OpenedBy("vital-thrust"), Is.Empty);
        }

        [Test]
        public void BattleResult_CarriesWhatTheCompletionOpened_AndOnlyACompletionCanOpenOrFinishACourse()
        {
            CurriculumTree tree = CampaignCurriculum.Default;
            CurriculumNode advance = tree.Find("advance");
            var opened = new[] { tree.Find("quick-draw"), tree.Find("diagonal-cut"), tree.Find("suppleness") };
            var result = new BattleResult(DuelMatchOutcome.PlayerVictory, false, 1, "숲길 입구", 30, 30, 3, 90, 0, false, 0, true,
                advance, openedCurriculumNodes: opened, completedCurriculumCourse: true);
            Assert.That(result.OpenedCurriculumNodes, Is.EqualTo(opened));
            Assert.That(result.CompletedCurriculumCourse, Is.True);
            opened[0] = null;
            Assert.That(result.OpenedCurriculumNodes[0], Is.SameAs(tree.Find("quick-draw")), "The snapshot keeps its own list.");

            var plain = new BattleResult(DuelMatchOutcome.PlayerVictory, false, 1, "숲길 입구", 30, 30, 3, 90, 0, false, 0, true);
            Assert.That(plain.OpenedCurriculumNodes, Is.Empty);
            Assert.That(plain.CompletedCurriculumCourse, Is.False);
            Assert.Throws<ArgumentException>(() => new BattleResult(DuelMatchOutcome.PlayerVictory, false, 1, "숲길 입구",
                30, 30, 3, 90, 0, false, 0, true, openedCurriculumNodes: new[] { advance }));
            Assert.Throws<ArgumentException>(() => new BattleResult(DuelMatchOutcome.PlayerVictory, false, 1, "숲길 입구",
                30, 30, 3, 90, 0, false, 0, true, completedCurriculumCourse: true));
        }

        // Selects each node in turn and finishes one lost battle of stage one for it. Ends in the lobby.
        private static void Complete(CampaignRun run, params string[] nodeIds)
        {
            foreach (string id in nodeIds)
            {
                Assert.That(run.TrySelectCurriculumNode(id), Is.True, id);
                Assert.That(run.TryStartStage(1), Is.True);
                Assert.That(run.TryCompleteBattle(DuelMatchOutcome.EnemyVictory), Is.True);
                Assert.That(run.ReturnToLobby(), Is.True);
                Assert.That(run.Curriculum.IsCompleted(id), Is.True, id);
            }
        }

        private static string[] Ids(System.Collections.Generic.IReadOnlyList<CurriculumNode> nodes)
            => nodes.Select(node => node.Id).ToArray();
    }
}
