using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;

namespace TurnLimbo.Core.Tests
{
    /// <summary>What the curriculum screens read from the tree and the progress: which nodes end their branch (the trunk's
    /// last step, each school's capstone) and what the last completion opened (the tree's "새" tag, the learning moment's
    /// <c>새로 열린 과정</c>).</summary>
    public sealed class CurriculumScreenTests
    {
        [Test]
        public void DefaultTree_TheTrunksLastStepAndSchoolCapstonesEndTheirBranches_ABridgeNever()
        {
            CurriculumTree tree = CampaignCurriculum.Default;
            string[] ends = tree.Nodes.Where(node => tree.EndsBranch(node.Id)).Select(node => node.Id).ToArray();
            Assert.That(ends, Is.EqualTo(new[] { "advance", "estocada", "cadence", "doublet", "blood-fury", "wound-rend", "exploit-tension", "steady-strike" }));
            string[] capstones = tree.Nodes.Where(node => tree.IsCapstone(node.Id)).Select(node => node.Id).ToArray();
            Assert.That(capstones, Is.EqualTo(new[] { "estocada", "cadence", "doublet", "blood-fury", "wound-rend", "exploit-tension", "steady-strike" }), "The trunk has no capstone.");
            Assert.That(tree.EndsBranch("battement"), Is.False, "A bridge joins two schools; it ends neither.");
            Assert.That(tree.EndsBranch("missing"), Is.False);
            Assert.That(tree.IsCapstone(null), Is.False);
            foreach (string capstone in capstones)
                Assert.That(tree.Find(capstone).StatReward.IsEmpty,
                    Is.EqualTo(tree.Find(capstone).Branch == CurriculumBranch.Bleeding || tree.Find(capstone).Branch == CurriculumBranch.Tension), capstone + ": status schools reward techniques only.");
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

            Complete(run, "battement");
            Assert.That(Ids(curriculum.OpenedBy("vital-thrust")), Is.EqualTo(new[] { "cadence" }),
                "A node learned since is no longer waiting.");
            Assert.That(curriculum.LastCompleted.Id, Is.EqualTo("battement"));
            Assert.That(run.TryResetCurriculum(), Is.True);
            Assert.That(curriculum.LastCompleted, Is.Null);
            Assert.That(curriculum.OpenedBy("vital-thrust"), Is.Empty);
        }

        // Earns each node's cost with lost battles of stage one (a point each), then learns it. Ends in the lobby.
        private static void Complete(CampaignRun run, params string[] nodeIds)
        {
            foreach (string id in nodeIds)
            {
                while (!run.IsCurriculumNodeAffordable(id))
                {
                    Assert.That(run.TryStartStage(1), Is.True);
                    Assert.That(run.TryCompleteBattle(DuelMatchOutcome.EnemyVictory), Is.True);
                    Assert.That(run.ReturnToLobby(), Is.True);
                }
                Assert.That(run.TryLearnCurriculumNode(id), Is.True, id);
            }
        }

        private static string[] Ids(System.Collections.Generic.IReadOnlyList<CurriculumNode> nodes)
            => nodes.Select(node => node.Id).ToArray();
    }
}
