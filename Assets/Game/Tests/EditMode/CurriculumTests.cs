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
                new CurriculumNode("conditioning", "기초 체력", CurriculumBranch.Guard, 0f, 0,
                    Array.Empty<int>(), description: "체력과 저항을 높입니다.",
                    statReward: new CurriculumStatReward(health: 20, resistance: 5)),
                new CurriculumNode("tempo", "전투 호흡", CurriculumBranch.Guard, 0f, 1,
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
            Assert.Throws<ArgumentException>(() => new CurriculumNode("empty", "빈 과정", CurriculumBranch.Guard,
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
                Assert.That(node.SkillIds, Is.Not.Empty, node.Id);
                foreach (string required in node.RequiresAll.Concat(node.RequiresAny))
                    Assert.That(earlier.Contains(required), Is.True, node.Id + " requires " + required);
                foreach (string other in node.ExclusiveWith)
                {
                    Assert.That(tree.AreExclusive(node.Id, other), Is.True, node.Id);
                    Assert.That(tree.AreExclusive(other, node.Id), Is.True, other);
                }
                foreach (int skill in node.SkillIds) Assert.That(tree.FindGranting(skill), Is.SameAs(node), node.Id);
                Assert.That(earlier.Add(node.Id), Is.True, "Duplicate node " + node.Id);
                Assert.That(tree.Find(node.Id), Is.SameAs(node));
                granted.AddRange(node.SkillIds);
            }
            Assert.That(tree.Nodes.Count, Is.EqualTo(10));
            var campaign = new CampaignRun();
            var stageRewards = Enumerable.Range(1, campaign.StageCount)
                .Select(stage => campaign.GetStage(stage).FirstClearSkillId).Where(id => id != 0).ToArray();
            CollectionAssert.AreEquivalent(CampaignSkillCatalog.AcquisitionSkills.Select(skill => skill.Id).Except(stageRewards), granted);
            foreach (int id in stageRewards) Assert.That(tree.FindGranting(id), Is.Null, "Stage rewards are not curriculum rewards.");
            foreach (LegacySkill skill in LegacyInitialSkills.All)
                Assert.That(tree.FindGranting(skill.Id), Is.Null, "Starting skills are never granted.");
            Assert.That(campaign.Curriculum.Tree, Is.SameAs(tree));
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
            Assert.That(curriculum.GetState("diagonal-cut"), Is.EqualTo(CurriculumNodeState.Locked),
                "A node in progress does not open what follows it.");

            FinishBattle(run, DuelMatchOutcome.PlayerVictory);
            Assert.That(curriculum.GetState("horizontal-cut"), Is.EqualTo(CurriculumNodeState.Completed));
            Assert.That(curriculum.GetState("diagonal-cut"), Is.EqualTo(CurriculumNodeState.Available));
            Assert.That(curriculum.GetState("one-stroke"), Is.EqualTo(CurriculumNodeState.Locked));
            Assert.That(curriculum.Active, Is.Null);
            Assert.That(curriculum.ActiveBattles, Is.Zero);
            Assert.That(curriculum.CompletedCount, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { "horizontal-cut" }, curriculum.Completed);
            Assert.Throws<NotSupportedException>(() => ((IList<string>)curriculum.Completed).Add("advance"));

            Complete(run, "diagonal-cut", "one-stroke");
            Assert.That(curriculum.GetState("quick-draw"), Is.EqualTo(CurriculumNodeState.Excluded));
            CollectionAssert.AreEqual(new[] { "horizontal-cut", "diagonal-cut", "one-stroke" }, curriculum.Completed);
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
            Assert.That(run.TrySelectCurriculumNode("horizontal-cut"), Is.True);
            Assert.That(run.TrySelectCurriculumNode("horizontal-cut"), Is.False, "It is already in progress.");
            Assert.That(run.Curriculum.ActiveBattles, Is.Zero);
            Assert.That(run.Curriculum.CanSelect("advance"), Is.True);
            Assert.That(run.TrySelectCurriculumNode("advance"), Is.True);
            Assert.That(run.Curriculum.Active.Id, Is.EqualTo("advance"));
            Assert.That(run.Curriculum.GetState("horizontal-cut"), Is.EqualTo(CurriculumNodeState.Available));

            FinishBattle(run, DuelMatchOutcome.PlayerVictory);
            Assert.That(run.Curriculum.IsCompleted("advance"), Is.True);
            Assert.That(run.Curriculum.IsCompleted("horizontal-cut"), Is.False);
            Assert.That(run.OwnedSkills.Any(owned => owned.SkillId == 14), Is.False);
        }

        [Test]
        public void InvalidSelections_ChangeNothing()
        {
            var run = new CampaignRun();
            Complete(run, "horizontal-cut");
            Assert.That(run.TrySelectCurriculumNode("advance"), Is.True);
            foreach (string id in new[] { null, "", "no-such-node", "horizontal-cut", "advance", "vital-thrust", "one-stroke" })
            {
                Assert.That(run.Curriculum.CanSelect(id), Is.False, id ?? "null");
                Assert.That(run.TrySelectCurriculumNode(id), Is.False, id ?? "null");
                Assert.That(run.Curriculum.Active.Id, Is.EqualTo("advance"), id ?? "null");
                Assert.That(run.Curriculum.ActiveBattles, Is.Zero, id ?? "null");
            }
            CollectionAssert.AreEqual(new[] { "horizontal-cut" }, run.Curriculum.Completed);
            Assert.That(run.Curriculum.IsCompleted(null), Is.False);
            Assert.That(run.Curriculum.IsExcluded(null), Is.False);
            Assert.That(run.Curriculum.ArePrerequisitesMet("no-such-node"), Is.False);
            Assert.Throws<ArgumentException>(() => run.Curriculum.GetState("no-such-node"));
            Assert.Throws<ArgumentException>(() => run.Curriculum.GetState(null));
        }

        [Test]
        public void CompletingOneStroke_ExcludesQuickDrawUntilTheCurriculumIsReset()
        {
            var run = new CampaignRun();
            CurriculumProgress curriculum = run.Curriculum;
            Complete(run, "horizontal-cut", "diagonal-cut");
            Assert.That(curriculum.GetState("one-stroke"), Is.EqualTo(CurriculumNodeState.Available));
            Assert.That(curriculum.GetState("quick-draw"), Is.EqualTo(CurriculumNodeState.Available));
            Assert.That(curriculum.Tree.AreExclusive("quick-draw", "one-stroke"), Is.True);

            // Choosing one side is not a commitment until it is completed.
            Assert.That(run.TrySelectCurriculumNode("one-stroke"), Is.True);
            Assert.That(curriculum.GetState("quick-draw"), Is.EqualTo(CurriculumNodeState.Available));
            Assert.That(curriculum.IsExcluded("quick-draw"), Is.False);
            FinishBattle(run, DuelMatchOutcome.PlayerVictory);

            Assert.That(curriculum.GetState("one-stroke"), Is.EqualTo(CurriculumNodeState.Completed));
            Assert.That(curriculum.IsExcluded("one-stroke"), Is.False);
            Assert.That(curriculum.GetState("quick-draw"), Is.EqualTo(CurriculumNodeState.Excluded));
            Assert.That(curriculum.IsExcluded("quick-draw"), Is.True);
            Assert.That(curriculum.ArePrerequisitesMet("quick-draw"), Is.True, "Only the exclusive pair closes it.");
            Assert.That(curriculum.CanSelect("quick-draw"), Is.False);
            Assert.That(run.TrySelectCurriculumNode("quick-draw"), Is.False);
            Assert.That(curriculum.Active, Is.Null);
            Assert.That(curriculum.GetState("suppleness"), Is.EqualTo(CurriculumNodeState.Locked), "Other pairs are untouched.");
            Assert.That(curriculum.GetState("fighting-spirit"), Is.EqualTo(CurriculumNodeState.Locked));

            Assert.That(run.TryResetCurriculum(), Is.True);
            Complete(run, "horizontal-cut", "diagonal-cut", "quick-draw");
            Assert.That(curriculum.GetState("one-stroke"), Is.EqualTo(CurriculumNodeState.Excluded));
            Assert.That(run.OwnedSkills.Any(owned => owned.SkillId == 42), Is.True);
            Assert.That(run.OwnedSkills.Any(owned => owned.SkillId == 16), Is.False);
        }

        [TestCase("horizontal-cut diagonal-cut", "vital-thrust")]
        [TestCase("advance vital-thrust", "diagonal-cut")]
        public void Preparation_OpensAfterEitherDiagonalCutOrVitalThrust(string path, string unneeded)
        {
            var run = new CampaignRun();
            string[] nodes = path.Split(' ');
            Complete(run, nodes[0]);
            Assert.That(run.Curriculum.GetState("preparation"), Is.EqualTo(CurriculumNodeState.Locked));
            Assert.That(run.Curriculum.ArePrerequisitesMet("preparation"), Is.False);
            Complete(run, nodes[1]);
            Assert.That(run.Curriculum.ArePrerequisitesMet("preparation"), Is.True);
            Assert.That(run.Curriculum.GetState("preparation"), Is.EqualTo(CurriculumNodeState.Available));
            Assert.That(run.Curriculum.GetState(unneeded), Is.EqualTo(CurriculumNodeState.Locked), "The other opener is not needed.");
            Complete(run, "preparation");
            Assert.That(run.OwnedSkills.Any(owned => owned.SkillId == 10), Is.True);
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
            Assert.That(run.TrySelectCurriculumNode("breathing"), Is.True);
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.InProgress), Is.False);
            Assert.That(run.TryCompleteBattle((DuelMatchOutcome)999), Is.False);
            Assert.That(run.TryAbandonBattle(), Is.True);
            Assert.That(run.Curriculum.Active.Id, Is.EqualTo("breathing"));
            Assert.That(run.Curriculum.ActiveBattles, Is.Zero);
            Assert.That(run.Curriculum.CompletedCount, Is.Zero);
            Assert.That(run.LastCompletedCurriculumNode, Is.Null);
            Assert.That(run.OwnedSkills.Count, Is.EqualTo(9));

            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.Draw), Is.True);
            Assert.That(run.Curriculum.IsCompleted("breathing"), Is.True);
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
            Assert.That(run.TrySelectCurriculumNode("breathing"), Is.True);
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);

            Assert.That(run.OwnedSkills.Count, Is.EqualTo(11), "The curriculum skill and first-clear skill are separate rewards.");
            CampaignOwnedSkill granted = run.OwnedSkills.Single(owned => owned.SkillId == 17);
            Assert.That(granted.Skill, Is.SameAs(CampaignSkillCatalog.AcquisitionSkills.First(skill => skill.Id == 17)));
            Assert.That(run.IsSkillEquipped(17), Is.False);
            Assert.That(run.IsSkillInLoadout(17), Is.False);
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
            Complete(run, "horizontal-cut", "diagonal-cut", "breathing", "advance");
            Assert.That(run.TryPlaceLoadoutSkill(14, 0, 0), Is.True);
            Assert.That(run.TryPlaceLoadoutSkill(15, 0, 1), Is.True);
            Assert.That(run.TryPlaceLoadoutSkill(17, 0, 2), Is.True);
            Assert.That(run.TryPlaceLoadoutSkill(12, 2, 1), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
            CollectionAssert.AreEqual(new[] { 14, 15, 17 }, EquippedIds(run, 0));
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
            Assert.That(run.Curriculum.GetState("diagonal-cut"), Is.EqualTo(CurriculumNodeState.Locked));
            Complete(run, "horizontal-cut");
            Assert.That(run.OwnedSkills.Any(owned => owned.SkillId == 14), Is.True, "A reset curriculum can be taken again.");
        }

        [Test]
        public void Reset_ClearsANodeInProgressEvenWithNothingCompleted()
        {
            var run = new CampaignRun();
            Assert.That(run.TryResetCurriculum(), Is.False);
            Assert.That(run.TrySelectCurriculumNode("advance"), Is.True);
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
            Assert.That(run.TrySelectCurriculumNode("diagonal-cut"), Is.True);
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
                Assert.That(run.Curriculum.IsCompleted("diagonal-cut"), Is.True, "The battle still counts afterwards.");
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
                { new CurriculumNode("a", "A", CurriculumBranch.Slash, 0f, 0, new[] { 1, 1 }) }), "skill granted twice by one node");
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
            Assert.Throws<ArgumentException>(() => new CurriculumNode("a", " ", CurriculumBranch.Guard, 0f, 0, new[] { 1 }));
            Assert.Throws<ArgumentException>(() => new CurriculumNode("a", null, CurriculumBranch.Guard, 0f, 0, new[] { 1, 2 }),
                "Only a one-skill node takes its title from the skill.");
            Assert.Throws<ArgumentException>(() => new CurriculumNode("a", null, CurriculumBranch.Guard, 0f, 0, new int[0]));
            Assert.Throws<ArgumentNullException>(() => new CurriculumNode("a", "A", CurriculumBranch.Guard, 0f, 0, null));
            Assert.Throws<ArgumentNullException>(() => new CurriculumNode("a", null, CurriculumBranch.Guard, 0f, 0, null));
            CurriculumNode slow = Node("a", 1, battles: 3);
            Assert.That(slow.Battles, Is.EqualTo(3));
            Assert.That(slow.Description, Is.Empty);
            Assert.That(slow.RequiresAll, Is.Empty);
            Assert.That(slow.RequiresAny, Is.Empty);
            Assert.That(slow.ExclusiveWith, Is.Empty);
        }

        [Test]
        public void EightNodes_FinishTheCurriculumUntilAReset()
        {
            var run = new CampaignRun();
            Assert.That(run.Curriculum.HasSelectableNode, Is.True);
            Assert.That(run.Curriculum.IsFinished, Is.False, "Nothing is in progress yet, but plenty is left to choose.");
            Complete(run, "horizontal-cut", "diagonal-cut", "one-stroke", "advance", "vital-thrust", "preparation",
                "breathing");
            Assert.That(run.Curriculum.IsFinished, Is.False);
            Assert.That(run.TrySelectCurriculumNode("suppleness"), Is.True);
            Assert.That(run.Curriculum.IsFinished, Is.False, "A node in progress is not finished.");
            FinishBattle(run, DuelMatchOutcome.PlayerVictory);
            Assert.That(run.Curriculum.CompletedCount, Is.EqualTo(8), "Two exclusive pairs cap one journey at eight nodes.");
            Assert.That(run.Curriculum.HasSelectableNode, Is.False);
            Assert.That(run.Curriculum.IsFinished, Is.True);
            Assert.That(run.Curriculum.GetState("quick-draw"), Is.EqualTo(CurriculumNodeState.Excluded));
            Assert.That(run.Curriculum.GetState("fighting-spirit"), Is.EqualTo(CurriculumNodeState.Excluded));
            Assert.That(run.ReturnToLobby(), Is.True);
            Assert.That(run.TryResetCurriculum(), Is.True);
            Assert.That(run.Curriculum.IsFinished, Is.False);
            Assert.That(run.Curriculum.HasSelectableNode, Is.True);
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
            => new CurriculumNode(id, id, CurriculumBranch.Slash, 0f, 0, new[] { skill }, all, any, exclusive, battles);

        private static int[] EquippedIds(CampaignRun run, int laneIndex)
            => run.GetEquippedLane(laneIndex).Select(owned => owned.SkillId).ToArray();

        private static int[] DuelIds(IReadOnlyList<LegacySkill> lane) => lane.Select(skill => skill.Id).ToArray();
    }
}
