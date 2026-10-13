using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using TurnLimbo.Runtime.Save;

namespace TurnLimbo.Core.Tests
{
    public sealed class CurriculumTests
    {
        [Test]
        public void StatOnlyNodes_ApplyCumulativelyAndFollowCompletedNodesThroughSaveAndReset()
        {
            var tree = new CurriculumTree(new[]
            {
                new CurriculumNode("conditioning", "기초 체력", CurriculumBranch.Retraction, 0f, 0,
                    Array.Empty<int>(), description: "체력과 저항을 높입니다.",
                    statReward: new CurriculumStatReward(health: 20, resistance: 5)),
                new CurriculumNode("tempo", "전투 호흡", CurriculumBranch.Retraction, 0f, 1,
                    Array.Empty<int>(), requiresAll: new[] { "conditioning" },
                    description: "ACT와 턴 시간을 늘립니다.",
                    statReward: new CurriculumStatReward(actGain: 2, actCapacity: 3, planningSeconds: 4)),
            });
            var run = new CampaignRun(tree);
            Assert.That(run.CurriculumStats.IsEmpty, Is.True);

            Assert.That(run.TrySelectCurriculumNode("conditioning"), Is.True);
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.Draw), Is.True);
            Assert.That(run.CurriculumStats.Health, Is.EqualTo(20));
            Assert.That(run.CurriculumStats.Resistance, Is.EqualTo(5));
            Assert.That(run.CreateDuel().Player.MaxHealth, Is.EqualTo(120));
            Assert.That(run.CreateDuel().Player.MaxResistance, Is.EqualTo(55));
            Assert.That(run.ReturnToLobby(), Is.True);

            Assert.That(run.TrySelectCurriculumNode("tempo"), Is.True);
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.Draw), Is.True);
            Assert.That(run.CurriculumStats.ActGain, Is.EqualTo(2));
            Assert.That(run.CurriculumStats.ActCapacity, Is.EqualTo(3));
            Assert.That(run.CurriculumStats.PlanningSeconds, Is.EqualTo(4));
            LegacyQueuedDuel duel = run.CreateDuel();
            Assert.That(duel.Act, Is.EqualTo(5), "The gain bonus applies on the first turn.");
            Assert.That(duel.PlayerMaximumAct, Is.EqualTo(13));

            string text = GameSaveCodec.Serialize(GameSave.Capture(new PrologueRun(), run));
            Assert.That(GameSaveCodec.TryParse(text, out GameSave parsed, out string parseError), Is.True, parseError);
            var restored = new CampaignRun(tree);
            Assert.That(parsed.TryApply(new PrologueRun(), restored, out string restoreError), Is.True, restoreError);
            Assert.That(restored.CurriculumStats.Health, Is.EqualTo(20));
            Assert.That(restored.CurriculumStats.ActGain, Is.EqualTo(2));
            Assert.That(restored.CurriculumStats.PlanningSeconds, Is.EqualTo(4));
            Assert.That(restored.CreateDuel().Act, Is.EqualTo(5));
            Assert.That(restored.TryResetCurriculum(), Is.True);
            Assert.That(restored.CurriculumStats.IsEmpty, Is.True);
            Assert.That(restored.CreateDuel().Player.MaxHealth, Is.EqualTo(100));
            Assert.That(restored.CreateDuel().Act, Is.EqualTo(3));
        }

        [Test]
        public void ActBonuses_UseTheIncreasedCapacity()
        {
            var duel = new LegacyQueuedDuel(1000, 50, 1000, 50, LegacyInitialSkills.All,
                new[] { LegacySkillDefinitions.Skill(7) }, new[] { 1 },
                playerActGainBonus: 2, playerActCapacityBonus: 3);
            Assert.That(duel.Act, Is.EqualTo(5));
            for (int turn = 0; turn < 3; turn++)
            {
                duel.Commit();
                while (!duel.IsTurnResolved) duel.ResolveNextSlot();
                duel.BeginNextTurn();
            }
            Assert.That(duel.Act, Is.EqualTo(13));
            Assert.That(duel.NextActGain, Is.EqualTo(5));
        }

        [Test]
        public void StatReward_RejectsNegativeBonusesAndNodesWithoutAnyReward()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CurriculumStatReward(health: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CurriculumStatReward(actGain: -1));
            Assert.Throws<ArgumentException>(() => new CurriculumNode("empty", "빈 과정", CurriculumBranch.Retraction,
                0f, 0, Array.Empty<int>()));
        }

        [Test]
        public void DefaultTree_IsOrderedAndGrantsEachCurriculumSkillFromExactlyOneNode()
        {
            CurriculumTree tree = CampaignCurriculum.Default;
            var earlier = new HashSet<string>();
            var granted = new List<int>();
            foreach (CurriculumNode node in tree.Nodes)
            {
                Assert.That(node.Title, Is.Not.Empty, node.Id);
                Assert.That(node.Description, Is.Not.Empty, node.Id);
                Assert.That(node.Battles, Is.EqualTo(1), node.Id + ": the prototype pace is one battle per node.");
                Assert.That(node.SkillIds.Count, Is.EqualTo(1), node.Id + ": every node grants one technique and is titled by it.");
                Assert.That(node.ExclusiveWith, Is.Empty, node.Id + ": no node closes another, so every node can be completed.");
                foreach (string required in node.RequiresAll.Concat(node.RequiresAny))
                    Assert.That(earlier.Contains(required), Is.True, node.Id + " requires " + required);
                foreach (int skill in node.SkillIds) Assert.That(tree.FindGranting(skill), Is.SameAs(node), node.Id);
                Assert.That(earlier.Add(node.Id), Is.True, "Duplicate node " + node.Id);
                Assert.That(tree.Find(node.Id), Is.SameAs(node));
                granted.AddRange(node.SkillIds);
            }
            // The keyword curriculum with its phase 2 nodes (quick-steps, whetstone), still tree version 2: they only add
            // nodes and widen cadence's requirement.
            Assert.That(tree.Nodes.Count, Is.EqualTo(17));
            Assert.That(CampaignCurriculum.TreeVersion, Is.EqualTo(2));
            Assert.That(tree.Find("one-stroke"), Is.Null, "알티바호 is stage 7's first-clear reward now.");
            var campaign = new CampaignRun();
            var stageRewards = Enumerable.Range(1, campaign.StageCount)
                .Select(stage => campaign.GetStage(stage).FirstClearSkillId).Where(id => id != 0).ToArray();
            int[] fieldPickups = CampaignFieldPickups.All.Select(pickup => pickup.SkillId).ToArray();
            CollectionAssert.AreEquivalent(CampaignSkillCatalog.AcquisitionSkills.Select(skill => skill.Id).Except(stageRewards).Except(fieldPickups), granted);
            foreach (int id in stageRewards) Assert.That(tree.FindGranting(id), Is.Null, "Stage rewards are not curriculum rewards.");
            foreach (int id in fieldPickups) Assert.That(tree.FindGranting(id), Is.Null, "Field pickups are not curriculum rewards.");
            CollectionAssert.IsEmpty(fieldPickups.Intersect(stageRewards), "Field pickups are not stage rewards.");
            foreach (LegacySkill skill in LegacyInitialSkills.All)
                Assert.That(tree.FindGranting(skill.Id), Is.Null, "Starting skills are never granted.");
            Assert.That(campaign.Curriculum.Tree, Is.SameAs(tree));
        }

        [Test]
        public void DefaultTree_IsATrunkThreeSchoolsAndTwoBridges()
        {
            CurriculumTree tree = CampaignCurriculum.Default;
            string Ids(Func<CurriculumNode, bool> filter) => string.Join(" ", tree.Nodes.Where(filter).Select(node => node.Id));
            Assert.That(Ids(node => node.Branch == CurriculumBranch.Foundation), Is.EqualTo("horizontal-cut preparation advance"));
            Assert.That(Ids(node => node.Branch == CurriculumBranch.Collapse), Is.EqualTo("quick-draw fighting-spirit esquive estocada"));
            Assert.That(Ids(node => node.Branch == CurriculumBranch.Circulation), Is.EqualTo("diagonal-cut vital-thrust quick-steps cadence"));
            Assert.That(Ids(node => node.Branch == CurriculumBranch.Retraction), Is.EqualTo("suppleness breathing contretemps doublet"));
            Assert.That(Ids(node => node.Branch == CurriculumBranch.Bridge), Is.EqualTo("battement whetstone"));
            Assert.That(Ids(node => node.RequiresAll.Count == 0 && node.RequiresAny.Count == 0), Is.EqualTo("horizontal-cut"),
                "The trunk is the only way in.");
            foreach (string intro in new[] { "quick-draw", "diagonal-cut", "suppleness" })
                CollectionAssert.AreEqual(new[] { "advance" }, tree.Find(intro).RequiresAll, intro);

            // Each capstone needs either of its school's twists and gives a stat; a bridge needs one twist of both
            // neighbours and gives none.
            CollectionAssert.AreEqual(new[] { "fighting-spirit", "esquive" }, tree.Find("estocada").RequiresAny);
            CollectionAssert.AreEqual(new[] { "vital-thrust", "quick-steps" }, tree.Find("cadence").RequiresAny);
            CollectionAssert.AreEqual(new[] { "breathing", "contretemps" }, tree.Find("doublet").RequiresAny);
            CollectionAssert.AreEqual(new[] { "esquive", "vital-thrust" }, tree.Find("battement").RequiresAll);
            CollectionAssert.AreEqual(new[] { "quick-steps", "breathing" }, tree.Find("whetstone").RequiresAll);
            CollectionAssert.AreEqual(new[] { "diagonal-cut" }, tree.Find("quick-steps").RequiresAll);
            Assert.That((tree.Find("quick-steps").Column, tree.Find("whetstone").Column), Is.EqualTo((3f, 3.5f)));
            Assert.That(tree.Find("estocada").StatReward.Health, Is.EqualTo(5));
            Assert.That(tree.Find("cadence").StatReward.Health, Is.EqualTo(5));
            Assert.That(tree.Find("doublet").StatReward.PlanningSeconds, Is.EqualTo(2));
            Assert.That(tree.Find("doublet").StatReward.Health, Is.Zero);
            foreach (CurriculumNode node in tree.Nodes.Where(node => node.Row < 5 || node.Branch == CurriculumBranch.Bridge))
                Assert.That(node.StatReward.IsEmpty, Is.True, node.Id + ": only capstones carry a stat.");
            CollectionAssert.AreEqual(new[] { 14, 10, 12, 42, 15, 32, 19, 54, 21, 57, 17, 55, 51, 52, 53, 58, 56 },
                tree.Nodes.Select(node => node.SkillIds[0]));
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 3, 3, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5 }, tree.Nodes.Select(node => node.Row));
        }

        [Test]
        public void States_FollowPrerequisitesTheNodeInProgressAndCompletion()
        {
            var run = new CampaignRun();
            CurriculumProgress curriculum = run.Curriculum;
            foreach (CurriculumNode node in curriculum.Tree.Nodes)
            {
                bool root = node.RequiresAll.Count == 0 && node.RequiresAny.Count == 0;
                Assert.That(curriculum.GetState(node.Id),
                    Is.EqualTo(root ? CurriculumNodeState.Available : CurriculumNodeState.Locked), node.Id);
            }
            Assert.That(curriculum.Active, Is.Null);
            Assert.That(curriculum.CompletedCount, Is.Zero);

            Assert.That(run.TrySelectCurriculumNode("horizontal-cut"), Is.True);
            Assert.That(curriculum.GetState("horizontal-cut"), Is.EqualTo(CurriculumNodeState.Active));
            Assert.That(curriculum.Active.Id, Is.EqualTo("horizontal-cut"));
            Assert.That(curriculum.ActiveBattles, Is.Zero);
            Assert.That(curriculum.GetState("preparation"), Is.EqualTo(CurriculumNodeState.Locked),
                "A node in progress does not open what follows it.");

            FinishBattle(run, DuelMatchOutcome.PlayerVictory);
            Assert.That(curriculum.GetState("horizontal-cut"), Is.EqualTo(CurriculumNodeState.Completed));
            Assert.That(curriculum.GetState("preparation"), Is.EqualTo(CurriculumNodeState.Available));
            Assert.That(curriculum.GetState("advance"), Is.EqualTo(CurriculumNodeState.Locked));
            Assert.That(curriculum.Active, Is.Null);
            Assert.That(curriculum.ActiveBattles, Is.Zero);
            Assert.That(curriculum.CompletedCount, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { "horizontal-cut" }, curriculum.Completed);
            Assert.Throws<NotSupportedException>(() => ((IList<string>)curriculum.Completed).Add("advance"));

            // The trunk's end opens the three schools at once.
            Complete(run, "preparation");
            Assert.That(curriculum.GetState("advance"), Is.EqualTo(CurriculumNodeState.Available));
            foreach (string intro in new[] { "quick-draw", "diagonal-cut", "suppleness" })
                Assert.That(curriculum.GetState(intro), Is.EqualTo(CurriculumNodeState.Locked), intro);
            Complete(run, "advance");
            foreach (string intro in new[] { "quick-draw", "diagonal-cut", "suppleness" })
                Assert.That(curriculum.GetState(intro), Is.EqualTo(CurriculumNodeState.Available), intro);
            Assert.That(curriculum.GetState("fighting-spirit"), Is.EqualTo(CurriculumNodeState.Locked));
            CollectionAssert.AreEqual(new[] { "horizontal-cut", "preparation", "advance" }, curriculum.Completed);
        }

        [TestCase(CampaignPhase.Lobby, true)]
        [TestCase(CampaignPhase.Maintenance, true)]
        [TestCase(CampaignPhase.Battle, false)]
        [TestCase(CampaignPhase.Failed, false)]
        [TestCase(CampaignPhase.Completed, false)]
        public void Selection_IsOpenOnlyInTheLobbyAndMaintenance(CampaignPhase phase, bool open)
        {
            CampaignRun run = RunInPhase(phase);
            Assert.That(run.TrySelectCurriculumNode("horizontal-cut"), Is.EqualTo(open));
            Assert.That(run.Curriculum.Active?.Id, Is.EqualTo(open ? "horizontal-cut" : null));
            Assert.That(run.Curriculum.GetState("horizontal-cut"),
                Is.EqualTo(open ? CurriculumNodeState.Active : CurriculumNodeState.Available));
        }

        [Test]
        public void Selection_CanChangeWhileNoBattleHasCountedTowardIt()
        {
            // Every default node takes one battle, so a counted battle always completes the node at once;
            // the node in progress therefore never holds a counted battle that would fix the choice.
            var run = new CampaignRun();
            Complete(run, "horizontal-cut", "preparation", "advance");
            Assert.That(run.TrySelectCurriculumNode("quick-draw"), Is.True);
            Assert.That(run.TrySelectCurriculumNode("quick-draw"), Is.False, "It is already in progress.");
            Assert.That(run.Curriculum.ActiveBattles, Is.Zero);
            Assert.That(run.Curriculum.CanSelect("diagonal-cut"), Is.True);
            Assert.That(run.TrySelectCurriculumNode("diagonal-cut"), Is.True);
            Assert.That(run.Curriculum.Active.Id, Is.EqualTo("diagonal-cut"));
            Assert.That(run.Curriculum.GetState("quick-draw"), Is.EqualTo(CurriculumNodeState.Available));

            FinishBattle(run, DuelMatchOutcome.PlayerVictory);
            Assert.That(run.Curriculum.IsCompleted("diagonal-cut"), Is.True);
            Assert.That(run.Curriculum.IsCompleted("quick-draw"), Is.False);
            Assert.That(run.OwnedSkills.Any(owned => owned.SkillId == 42), Is.False);
        }

        [Test]
        public void InvalidSelections_ChangeNothing()
        {
            var run = new CampaignRun();
            Complete(run, "horizontal-cut", "preparation");
            Assert.That(run.TrySelectCurriculumNode("advance"), Is.True);
            foreach (string id in new[] { null, "", "no-such-node", "horizontal-cut", "advance", "vital-thrust", "one-stroke" })
            {
                Assert.That(run.Curriculum.CanSelect(id), Is.False, id ?? "null");
                Assert.That(run.TrySelectCurriculumNode(id), Is.False, id ?? "null");
                Assert.That(run.Curriculum.Active.Id, Is.EqualTo("advance"), id ?? "null");
                Assert.That(run.Curriculum.ActiveBattles, Is.Zero, id ?? "null");
            }
            CollectionAssert.AreEqual(new[] { "horizontal-cut", "preparation" }, run.Curriculum.Completed);
            Assert.That(run.Curriculum.IsCompleted(null), Is.False);
            Assert.That(run.Curriculum.IsExcluded(null), Is.False);
            Assert.That(run.Curriculum.ArePrerequisitesMet("no-such-node"), Is.False);
            Assert.Throws<ArgumentException>(() => run.Curriculum.GetState("no-such-node"));
            Assert.Throws<ArgumentException>(() => run.Curriculum.GetState(null));
        }

        [Test]
        public void ExclusivePair_ClosesTheOtherSideUntilTheCurriculumIsReset_AndASaveCannotHoldBoth()
        {
            // The default tree has no exclusive pair since the keyword curriculum; a supplied tree still may.
            var tree = new CurriculumTree(new[]
            {
                Node("root", 14),
                Node("left", 16, all: new[] { "root" }, exclusive: new[] { "right" }),
                Node("right", 42, all: new[] { "root" }),
                Node("other", 15, all: new[] { "root" }),
            });
            var run = new CampaignRun(tree);
            CurriculumProgress curriculum = run.Curriculum;
            Complete(run, "root");
            Assert.That(curriculum.GetState("left"), Is.EqualTo(CurriculumNodeState.Available));
            Assert.That(curriculum.GetState("right"), Is.EqualTo(CurriculumNodeState.Available));
            Assert.That(curriculum.Tree.AreExclusive("right", "left"), Is.True);

            // Choosing one side is not a commitment until it is completed.
            Assert.That(run.TrySelectCurriculumNode("left"), Is.True);
            Assert.That(curriculum.GetState("right"), Is.EqualTo(CurriculumNodeState.Available));
            Assert.That(curriculum.IsExcluded("right"), Is.False);
            FinishBattle(run, DuelMatchOutcome.PlayerVictory);

            Assert.That(curriculum.GetState("left"), Is.EqualTo(CurriculumNodeState.Completed));
            Assert.That(curriculum.IsExcluded("left"), Is.False);
            Assert.That(curriculum.GetState("right"), Is.EqualTo(CurriculumNodeState.Excluded));
            Assert.That(curriculum.IsExcluded("right"), Is.True);
            Assert.That(curriculum.ArePrerequisitesMet("right"), Is.True, "Only the exclusive pair closes it.");
            Assert.That(curriculum.CanSelect("right"), Is.False);
            Assert.That(run.TrySelectCurriculumNode("right"), Is.False);
            Assert.That(curriculum.Active, Is.Null);
            Assert.That(curriculum.GetState("other"), Is.EqualTo(CurriculumNodeState.Available), "Other nodes are untouched.");

            CampaignSave save = run.CaptureSave();
            Assert.That(new CampaignRun(tree).TryRestore(save, out string error), Is.True, error);
            var both = new CampaignSave(0, new int[0], new[] { "root", "left", "right" }, null, 0, save.Loadout);
            Assert.That(new CampaignRun(tree).TryRestore(both, out error), Is.False);
            StringAssert.Contains("right", error);
            var excludedActive = new CampaignSave(0, new int[0], new[] { "root", "left" }, "right", 0, save.Loadout);
            Assert.That(new CampaignRun(tree).TryRestore(excludedActive, out error), Is.False);
            StringAssert.Contains("right", error);

            Assert.That(run.TryResetCurriculum(), Is.True);
            Complete(run, "root", "right");
            Assert.That(curriculum.GetState("left"), Is.EqualTo(CurriculumNodeState.Excluded));
            Assert.That(run.OwnedSkills.Any(owned => owned.SkillId == 42), Is.True);
            Assert.That(run.OwnedSkills.Any(owned => owned.SkillId == 16), Is.False);
        }

        [TestCase("quick-draw fighting-spirit", "estocada", "esquive")]
        [TestCase("quick-draw esquive", "estocada", "fighting-spirit")]
        [TestCase("diagonal-cut vital-thrust", "cadence", "quick-steps")]
        [TestCase("diagonal-cut quick-steps", "cadence", "vital-thrust")]
        [TestCase("suppleness breathing", "doublet", "contretemps")]
        [TestCase("suppleness contretemps", "doublet", "breathing")]
        public void Capstone_OpensAfterEitherTwistOfItsSchool_AndGrantsItsStat(string path, string capstone, string other)
        {
            var run = new CampaignRun();
            Complete(run, "horizontal-cut", "preparation", "advance");
            string[] nodes = path.Split(' ');
            Complete(run, nodes[0]);
            Assert.That(run.Curriculum.GetState(capstone), Is.EqualTo(CurriculumNodeState.Locked));
            Complete(run, nodes[1]);
            Assert.That(run.Curriculum.ArePrerequisitesMet(capstone), Is.True);
            Assert.That(run.Curriculum.GetState(capstone), Is.EqualTo(CurriculumNodeState.Available));
            if (other.Length > 0)
                Assert.That(run.Curriculum.IsCompleted(other), Is.False, "The other twist is not needed.");
            CurriculumStatReward before = run.CurriculumStats;
            Complete(run, capstone);
            CurriculumNode node = run.Curriculum.Tree.Find(capstone);
            Assert.That(run.OwnedSkills.Any(owned => owned.SkillId == node.SkillIds[0]), Is.True);
            Assert.That(run.CurriculumStats.Health, Is.EqualTo(before.Health + node.StatReward.Health));
            Assert.That(run.CurriculumStats.PlanningSeconds, Is.EqualTo(before.PlanningSeconds + node.StatReward.PlanningSeconds));
            Assert.That(node.StatReward.IsEmpty, Is.False, "A capstone grants a stat.");
        }

        [Test]
        public void Bridge_NeedsATwistFromBothNeighbouringSchools()
        {
            var run = new CampaignRun();
            Complete(run, "horizontal-cut", "preparation", "advance", "quick-draw", "diagonal-cut", "esquive");
            Assert.That(run.Curriculum.GetState("battement"), Is.EqualTo(CurriculumNodeState.Locked), "붕괴 alone is not enough.");
            Complete(run, "vital-thrust");
            Assert.That(run.Curriculum.GetState("battement"), Is.EqualTo(CurriculumNodeState.Available));

            var other = new CampaignRun();
            Complete(other, "horizontal-cut", "preparation", "advance", "quick-draw", "diagonal-cut", "vital-thrust",
                "fighting-spirit");
            Assert.That(other.Curriculum.GetState("battement"), Is.EqualTo(CurriculumNodeState.Locked),
                "It needs 에스키브 itself, not any 붕괴 twist.");
            Complete(run, "battement");
            Assert.That(run.OwnedSkills.Any(owned => owned.SkillId == 52), Is.True);
            Assert.That(run.CurriculumStats.IsEmpty, Is.True, "A bridge grants no stat.");

            // 숫돌 bridges 순환 and 판짜기 the same way: 잔걸음 and 호흡 themselves.
            var whetstone = new CampaignRun();
            Complete(whetstone, "horizontal-cut", "preparation", "advance", "diagonal-cut", "suppleness", "quick-steps");
            Assert.That(whetstone.Curriculum.GetState("whetstone"), Is.EqualTo(CurriculumNodeState.Locked), "순환 alone is not enough.");
            Complete(whetstone, "contretemps");
            Assert.That(whetstone.Curriculum.GetState("whetstone"), Is.EqualTo(CurriculumNodeState.Locked),
                "It needs 호흡 itself, not any 판짜기 twist.");
            Complete(whetstone, "breathing");
            Assert.That(whetstone.Curriculum.GetState("whetstone"), Is.EqualTo(CurriculumNodeState.Available));
            Complete(whetstone, "whetstone");
            Assert.That(whetstone.OwnedSkills.Any(owned => owned.SkillId == 58), Is.True);
            Assert.That(whetstone.OwnedSkills.Any(owned => owned.SkillId == 59), Is.False, "갈아내기 is never owned.");
        }

        [TestCase(DuelMatchOutcome.PlayerVictory)]
        [TestCase(DuelMatchOutcome.EnemyVictory)]
        [TestCase(DuelMatchOutcome.Draw)]
        public void AnyFinishedBattle_CountsTowardTheNodeInProgress(DuelMatchOutcome outcome)
        {
            var run = new CampaignRun();
            Assert.That(run.TrySelectCurriculumNode("horizontal-cut"), Is.True);
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.LastCompletedCurriculumNode, Is.Null);
            Assert.That(run.TryCompleteBattle(outcome), Is.True);

            Assert.That(run.Curriculum.IsCompleted("horizontal-cut"), Is.True);
            Assert.That(run.Curriculum.Active, Is.Null);
            Assert.That(run.LastCompletedCurriculumNode, Is.SameAs(run.Curriculum.Tree.Find("horizontal-cut")));
            Assert.That(run.OwnedSkills.Any(owned => owned.SkillId == 14), Is.True);
            Assert.That(run.IsStageCleared(1), Is.EqualTo(outcome == DuelMatchOutcome.PlayerVictory), "Only a victory clears the stage.");

            // The note belongs to the battle that completed the node.
            Assert.That(run.ReturnToLobby(), Is.True);
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.LastCompletedCurriculumNode, Is.Null);
            Assert.That(run.TryCompleteBattle(outcome), Is.True);
            Assert.That(run.LastCompletedCurriculumNode, Is.Null, "Nothing was in progress.");
        }

        [Test]
        public void AbandonedOrUnfinishedBattle_DoesNotCount()
        {
            var run = new CampaignRun();
            Assert.That(run.TrySelectCurriculumNode("horizontal-cut"), Is.True);
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.InProgress), Is.False);
            Assert.That(run.TryCompleteBattle((DuelMatchOutcome)999), Is.False);
            Assert.That(run.TryAbandonBattle(), Is.True);
            Assert.That(run.Curriculum.Active.Id, Is.EqualTo("horizontal-cut"));
            Assert.That(run.Curriculum.ActiveBattles, Is.Zero);
            Assert.That(run.Curriculum.CompletedCount, Is.Zero);
            Assert.That(run.LastCompletedCurriculumNode, Is.Null);
            Assert.That(run.OwnedSkills.Count, Is.EqualTo(9));

            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.Draw), Is.True);
            Assert.That(run.Curriculum.IsCompleted("horizontal-cut"), Is.True);
        }

        [Test]
        public void BattleWithoutANodeInProgress_ChangesNoCurriculum()
        {
            var run = new CampaignRun();
            FinishBattle(run, DuelMatchOutcome.PlayerVictory);
            FinishBattle(run, DuelMatchOutcome.EnemyVictory);
            Assert.That(run.Curriculum.CompletedCount, Is.Zero);
            Assert.That(run.Curriculum.Active, Is.Null);
            Assert.That(run.Curriculum.ActiveBattles, Is.Zero);
            Assert.That(run.LastCompletedCurriculumNode, Is.Null);
            Assert.That(run.OwnedSkills.Count, Is.EqualTo(10), "The first stage clear grants 탐색 independently of the curriculum.");
            Assert.That(run.Curriculum.GetState("horizontal-cut"), Is.EqualTo(CurriculumNodeState.Available));
            Assert.That(run.TryResetCurriculum(), Is.False, "There is nothing to reset.");
        }

        [Test]
        public void Completion_GrantsTheCatalogSkillWithoutEquippingIt()
        {
            var run = new CampaignRun();
            Assert.That(run.TrySelectCurriculumNode("horizontal-cut"), Is.True);
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);

            Assert.That(run.OwnedSkills.Count, Is.EqualTo(11), "The curriculum skill and first-clear skill are separate rewards.");
            CampaignOwnedSkill granted = run.OwnedSkills.Single(owned => owned.SkillId == 14);
            LegacySkill basis = CampaignSkillCatalog.AcquisitionSkills.First(skill => skill.Id == 14);
            Assert.That(granted.Skill, Is.Not.SameAs(basis));
            Assert.That(granted.Skill.Id, Is.EqualTo(basis.Id));
            Assert.That(granted.Skill.Name, Is.EqualTo(basis.Name));
            Assert.That(granted.Skill.MinPower, Is.EqualTo(basis.MinPower));
            Assert.That(granted.Skill.MaxPower, Is.EqualTo(basis.MaxPower));
            Assert.That(run.IsSkillEquipped(14), Is.False);
            Assert.That(run.IsSkillInLoadout(14), Is.False);
            Assert.That(run.HasLoadoutChanges, Is.False);
            Assert.That(run.EquippedSkillCount, Is.EqualTo(9));
            CollectionAssert.AreEqual(new[] { 1, 2, 7 }, EquippedIds(run, 0));
            Assert.That(run.TryStartNextStage(), Is.True);
            CollectionAssert.AreEqual(new[] { 1, 2, 7 }, DuelIds(run.CreateDuel().GetLane(0)));
        }

        [Test]
        public void Reset_RemovesGrantedSkillsAndRefillsSavedLanesWithStartingSkills()
        {
            var run = new CampaignRun();
            Complete(run, "horizontal-cut", "preparation", "advance", "diagonal-cut");
            Assert.That(run.TryPlaceLoadoutSkill(14, 0, 0), Is.True);
            Assert.That(run.TryPlaceLoadoutSkill(15, 0, 1), Is.True);
            Assert.That(run.TryPlaceLoadoutSkill(10, 0, 2), Is.True);
            Assert.That(run.TryPlaceLoadoutSkill(12, 2, 1), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
            CollectionAssert.AreEqual(new[] { 14, 15, 10 }, EquippedIds(run, 0));
            CollectionAssert.AreEqual(new[] { 5, 12, 9 }, EquippedIds(run, 2));
            Assert.That(run.TrySelectCurriculumNode("vital-thrust"), Is.True);
            Assert.That(run.TryUnequipSkill(3), Is.True);

            Assert.That(run.TryResetCurriculum(), Is.True);
            Assert.That(run.Curriculum.CompletedCount, Is.Zero);
            Assert.That(run.Curriculum.Active, Is.Null);
            Assert.That(run.LastCompletedCurriculumNode, Is.Null);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, run.OwnedSkills.Select(owned => owned.SkillId));
            CollectionAssert.AreEqual(new[] { 1, 2, 7 }, EquippedIds(run, 0), "An emptied lane gets its starting skills back in order.");
            CollectionAssert.AreEqual(new[] { 3, 4, 8 }, EquippedIds(run, 1));
            Assert.That(run.GetLoadoutSlot(1, 0).SkillId, Is.EqualTo(3), "The unsaved edit is discarded.");
            CollectionAssert.AreEqual(new[] { 5, 9, 6 }, EquippedIds(run, 2), "Kept skills stay and the freed slot is filled at the end.");
            for (int lane = 0; lane < 3; lane++)
            {
                Assert.That(run.GetEquippedLane(lane).Count, Is.EqualTo(3));
                Assert.That(run.GetLoadoutCount(lane), Is.EqualTo(3));
            }
            Assert.That(run.EquippedSkillCount, Is.EqualTo(9));
            Assert.That(run.HasLoadoutChanges, Is.False);
            Assert.That(run.CanStartStage(1), Is.True);
            Assert.That(run.TryResetCurriculum(), Is.False, "Nothing is left to reset.");

            Assert.That(run.Curriculum.GetState("horizontal-cut"), Is.EqualTo(CurriculumNodeState.Available));
            Assert.That(run.Curriculum.GetState("preparation"), Is.EqualTo(CurriculumNodeState.Locked));
            Complete(run, "horizontal-cut");
            Assert.That(run.OwnedSkills.Any(owned => owned.SkillId == 14), Is.True, "A reset curriculum can be taken again.");
        }

        [Test]
        public void Reset_ClearsANodeInProgressEvenWithNothingCompleted()
        {
            var run = new CampaignRun();
            Assert.That(run.TryResetCurriculum(), Is.False);
            Assert.That(run.TrySelectCurriculumNode("horizontal-cut"), Is.True);
            Assert.That(run.TryResetCurriculum(), Is.True);
            Assert.That(run.Curriculum.Active, Is.Null);
            Assert.That(run.OwnedSkills.Count, Is.EqualTo(9));
        }

        [TestCase(CampaignPhase.Battle)]
        [TestCase(CampaignPhase.Failed)]
        [TestCase(CampaignPhase.Completed)]
        public void Reset_IsRejectedDuringABattleAndOnItsResult(CampaignPhase phase)
        {
            var run = new CampaignRun();
            Complete(run, "horizontal-cut");
            Assert.That(run.TryUnequipSkill(7), Is.True);
            Assert.That(run.TryEquipSkill(14), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
            Assert.That(run.TrySelectCurriculumNode("preparation"), Is.True);
            EnterPhase(run, phase);
            string[] completed = run.Curriculum.Completed.ToArray();
            string active = run.Curriculum.Active?.Id;
            int owned = run.OwnedSkills.Count;

            Assert.That(run.TryResetCurriculum(), Is.False);
            CollectionAssert.AreEqual(completed, run.Curriculum.Completed);
            Assert.That(run.Curriculum.Active?.Id, Is.EqualTo(active));
            Assert.That(run.OwnedSkills.Count, Is.EqualTo(owned));
            CollectionAssert.AreEqual(new[] { 1, 2, 14 }, EquippedIds(run, 0));
            if (phase == CampaignPhase.Battle)
            {
                Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                Assert.That(run.Curriculum.IsCompleted("preparation"), Is.True, "The battle still counts afterwards.");
            }
        }

        [Test]
        public void TreeConstructor_RejectsBrokenDefinitions()
        {
            Assert.Throws<ArgumentNullException>(() => new CurriculumTree(null));
            Assert.Throws<ArgumentException>(() => new CurriculumTree(new CurriculumNode[0]));
            Assert.Throws<ArgumentException>(() => new CurriculumTree(new[] { Node("a", 1), null }), "missing node");
            Assert.Throws<ArgumentException>(() => new CurriculumTree(new[] { Node("a", 1), Node("a", 2) }), "duplicate id");
            Assert.Throws<ArgumentException>(() => new CurriculumTree(new[] { Node("a", 1, all: new[] { "missing" }) }),
                "unknown prerequisite");
            Assert.Throws<ArgumentException>(() => new CurriculumTree(new[] { Node("a", 1, any: new[] { "missing" }) }),
                "unknown alternative prerequisite");
            Assert.Throws<ArgumentException>(() => new CurriculumTree(new[] { Node("b", 2, all: new[] { "a" }), Node("a", 1) }),
                "forward prerequisite");
            Assert.Throws<ArgumentException>(() => new CurriculumTree(new[] { Node("a", 1), Node("b", 2, any: new[] { "c" }), Node("c", 3) }),
                "forward alternative prerequisite");
            Assert.Throws<ArgumentException>(() => new CurriculumTree(new[] { Node("a", 1, all: new[] { "a" }) }), "self prerequisite");
            Assert.Throws<ArgumentException>(() => new CurriculumTree(new[] { Node("a", 1), Node("b", 1) }), "skill granted twice");
            Assert.Throws<ArgumentException>(() => new CurriculumTree(new[]
                { new CurriculumNode("a", "A", CurriculumBranch.Circulation, 0f, 0, new[] { 1, 1 }) }), "skill granted twice by one node");
            Assert.Throws<ArgumentException>(() => new CurriculumTree(new[] { Node("a", 1, exclusive: new[] { "missing" }) }),
                "unknown exclusive node");
            Assert.Throws<ArgumentException>(() => new CurriculumTree(new[] { Node("a", 1, exclusive: new[] { "a" }) }),
                "self exclusive");
        }

        [Test]
        public void NodeConstructor_RejectsBadIdsTitlesSkillsAndPace()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Node("a", 1, battles: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Node("a", 1, battles: -1));
            Assert.Throws<ArgumentException>(() => Node(null, 1));
            Assert.Throws<ArgumentException>(() => Node(" ", 1));
            Assert.Throws<ArgumentException>(() => Node("two words", 1), "Ids are single words in the save file.");
            Assert.Throws<ArgumentException>(() => Node("wide　space", 1), "The codec splits on every Unicode space.");
            Assert.Throws<ArgumentException>(() => Node("tabbed", 1));
            Assert.Throws<ArgumentException>(() => new CurriculumNode("a", " ", CurriculumBranch.Retraction, 0f, 0, new[] { 1 }));
            Assert.Throws<ArgumentException>(() => new CurriculumNode("a", null, CurriculumBranch.Retraction, 0f, 0, new[] { 1, 2 }),
                "Only a one-skill node takes its title from the skill.");
            Assert.Throws<ArgumentException>(() => new CurriculumNode("a", null, CurriculumBranch.Retraction, 0f, 0, new int[0]));
            Assert.Throws<ArgumentNullException>(() => new CurriculumNode("a", "A", CurriculumBranch.Retraction, 0f, 0, null));
            Assert.Throws<ArgumentNullException>(() => new CurriculumNode("a", null, CurriculumBranch.Retraction, 0f, 0, null));
            CurriculumNode slow = Node("a", 1, battles: 3);
            Assert.That(slow.Battles, Is.EqualTo(3));
            Assert.That(slow.Description, Is.Empty);
            Assert.That(slow.RequiresAll, Is.Empty);
            Assert.That(slow.RequiresAny, Is.Empty);
            Assert.That(slow.ExclusiveWith, Is.Empty);
        }

        [Test]
        public void EveryNode_CanBeCompleted_ThenTheCurriculumIsFinishedUntilAReset()
        {
            var run = new CampaignRun();
            CurriculumTree tree = run.Curriculum.Tree;
            Assert.That(run.Curriculum.HasSelectableNode, Is.True);
            Assert.That(run.Curriculum.IsFinished, Is.False, "Nothing is in progress yet, but plenty is left to choose.");
            // Tree order lists every prerequisite first.
            Complete(run, tree.Nodes.Take(tree.Nodes.Count - 1).Select(node => node.Id).ToArray());
            Assert.That(run.Curriculum.IsFinished, Is.False);
            Assert.That(run.TrySelectCurriculumNode(tree.Nodes.Last().Id), Is.True);
            Assert.That(run.Curriculum.IsFinished, Is.False, "A node in progress is not finished.");
            FinishBattle(run, DuelMatchOutcome.PlayerVictory);
            Assert.That(run.Curriculum.CompletedCount, Is.EqualTo(tree.Nodes.Count), "No exclusive pair closes any node.");
            Assert.That(run.Curriculum.HasSelectableNode, Is.False);
            Assert.That(run.Curriculum.IsFinished, Is.True);
            Assert.That(tree.Nodes.All(node => run.Curriculum.GetState(node.Id) == CurriculumNodeState.Completed), Is.True);
            Assert.That(run.CurriculumStats.Health, Is.EqualTo(10), "붕괴 and 순환 capstones.");
            Assert.That(run.CurriculumStats.PlanningSeconds, Is.EqualTo(2), "판짜기 capstone.");
            Assert.That(run.CreateDuel().Player.MaxHealth, Is.EqualTo(110));
            Assert.That(run.ReturnToLobby(), Is.True);
            Assert.That(run.TryResetCurriculum(), Is.True);
            Assert.That(run.Curriculum.IsFinished, Is.False);
            Assert.That(run.Curriculum.HasSelectableNode, Is.True);
            Assert.That(run.CurriculumStats.IsEmpty, Is.True);
        }

        [Test]
        public void Tree_MakesExclusivitySymmetricAndFindsGrantingNodes()
        {
            var tree = new CurriculumTree(new[]
            {
                Node("root", 1),
                Node("left", 2, all: new[] { "root" }, exclusive: new[] { "right" }),
                Node("right", 3, all: new[] { "root" }),
                Node("join", 4, any: new[] { "left", "right" }),
            });
            CollectionAssert.AreEqual(new[] { "root", "left", "right", "join" }, tree.Nodes.Select(node => node.Id));
            Assert.That(tree.AreExclusive("left", "right"), Is.True);
            Assert.That(tree.AreExclusive("right", "left"), Is.True, "Declaring one side is enough.");
            Assert.That(tree.AreExclusive("root", "left"), Is.False);
            Assert.That(tree.AreExclusive(null, "left"), Is.False);
            Assert.That(tree.AreExclusive("missing", "left"), Is.False);
            Assert.That(tree.FindGranting(3), Is.SameAs(tree.Find("right")));
            Assert.That(tree.FindGranting(99), Is.Null);
            Assert.That(tree.Find(null), Is.Null);
            Assert.That(tree.Find("missing"), Is.Null);
        }

        [Test]
        public void BattleResult_CarriesCurriculumProgressThatMissionsCannotHave()
        {
            CurriculumNode done = CampaignCurriculum.Default.Find("horizontal-cut");
            CurriculumNode next = CampaignCurriculum.Default.Find("diagonal-cut");
            var result = new BattleResult(DuelMatchOutcome.EnemyVictory, false, 1, "숲길 입구",
                0, 0, 3, 0, 20, false, 0, false, done, next, 0);
            Assert.That(result.CompletedCurriculumNode, Is.SameAs(done));
            Assert.That(result.ActiveCurriculumNode, Is.SameAs(next));
            Assert.That(result.ActiveCurriculumBattles, Is.Zero);

            var plain = new BattleResult(DuelMatchOutcome.PlayerVictory, true, 2, "맞서는 검",
                0, 0, 3, 100, 0, false, 0, true);
            Assert.That(plain.CompletedCurriculumNode, Is.Null);
            Assert.That(plain.ActiveCurriculumNode, Is.Null);
            Assert.That(plain.ActiveCurriculumBattles, Is.Zero);

            Assert.Throws<ArgumentException>(() => new BattleResult(DuelMatchOutcome.PlayerVictory, true, 2, "맞서는 검",
                0, 0, 3, 100, 0, false, 0, true, completedCurriculumNode: done));
            Assert.Throws<ArgumentException>(() => new BattleResult(DuelMatchOutcome.PlayerVictory, true, 2, "맞서는 검",
                0, 0, 3, 100, 0, false, 0, true, activeCurriculumNode: next));
            Assert.Throws<ArgumentException>(() => new BattleResult(DuelMatchOutcome.PlayerVictory, true, 2, "맞서는 검",
                0, 0, 3, 100, 0, false, 0, true, curriculumFinished: true));
            Assert.That(plain.CurriculumFinished, Is.False);
            Assert.That(new BattleResult(DuelMatchOutcome.PlayerVictory, false, 1, "숲길 입구", 30, 30, 3, 90, 0, false, 0, true,
                done, curriculumFinished: true).CurriculumFinished, Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleResult(DuelMatchOutcome.Draw, false, 1, "숲길 입구",
                0, 0, 3, 10, 10, false, 0, false, activeCurriculumBattles: 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleResult(DuelMatchOutcome.Draw, false, 1, "숲길 입구",
                0, 0, 3, 10, 10, false, 0, false, activeCurriculumNode: next, activeCurriculumBattles: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleResult(DuelMatchOutcome.Draw, false, 1, "숲길 입구",
                0, 0, 3, 10, 10, false, 0, false, activeCurriculumNode: next, activeCurriculumBattles: next.Battles));
        }

        /// <summary>Selects each node in turn and finishes one lost battle of stage one for it, so the stages and the
        /// currency stay as they were. Ends in the lobby.</summary>
        private static void Complete(CampaignRun run, params string[] nodeIds)
        {
            foreach (string id in nodeIds)
            {
                Assert.That(run.TrySelectCurriculumNode(id), Is.True, id);
                FinishBattle(run, DuelMatchOutcome.EnemyVictory);
                Assert.That(run.Curriculum.IsCompleted(id), Is.True, id);
            }
        }

        private static void FinishBattle(CampaignRun run, DuelMatchOutcome outcome)
        {
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(outcome), Is.True);
            Assert.That(run.ReturnToLobby(), Is.True);
        }

        private static CampaignRun RunInPhase(CampaignPhase phase)
        {
            var run = new CampaignRun();
            EnterPhase(run, phase);
            return run;
        }

        private static void EnterPhase(CampaignRun run, CampaignPhase phase)
        {
            switch (phase)
            {
                case CampaignPhase.Battle:
                    Assert.That(run.TryStartStage(1), Is.True);
                    break;
                case CampaignPhase.Maintenance:
                case CampaignPhase.Failed:
                    Assert.That(run.TryStartStage(1), Is.True);
                    Assert.That(run.TryCompleteBattle(phase == CampaignPhase.Failed
                        ? DuelMatchOutcome.EnemyVictory : DuelMatchOutcome.PlayerVictory), Is.True);
                    break;
                case CampaignPhase.Completed:
                    for (int stage = 1; stage <= run.StageCount; stage++)
                    {
                        Assert.That(run.TryStartStage(stage), Is.True);
                        Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                    }
                    break;
            }
            Assert.That(run.Phase, Is.EqualTo(phase));
        }

        private static CurriculumNode Node(string id, int skill, string[] all = null, string[] any = null,
            string[] exclusive = null, int battles = 1)
            => new CurriculumNode(id, id, CurriculumBranch.Circulation, 0f, 0, new[] { skill }, all, any, exclusive, battles);

        private static int[] EquippedIds(CampaignRun run, int laneIndex)
            => run.GetEquippedLane(laneIndex).Select(owned => owned.SkillId).ToArray();

        private static int[] DuelIds(IReadOnlyList<LegacySkill> lane) => lane.Select(skill => skill.Id).ToArray();
    }
}
