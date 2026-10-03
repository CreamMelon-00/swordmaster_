using System;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;

namespace TurnLimbo.Core.Tests
{
    public sealed class StoryUnlockTests
    {
        private static readonly CombatFeature[] UnlockOrder =
        {
            CombatFeature.LaneE, CombatFeature.Breath, CombatFeature.Dodge, CombatFeature.LaneW, CombatFeature.Pressure,
        };

        [Test]
        public void StoryChain_IsTheArcThenOneLobbyMissionPerFeatureInTheUsersOrder()
        {
            Assert.That(StoryMissions.All.Select(mission => mission.Number), Is.EqualTo(Enumerable.Range(1, 9)));
            Assert.That(StoryMissions.All.Take(PrologueMissions.Count), Is.EqualTo(PrologueMissions.All));
            Assert.That(StoryMissions.All.Skip(PrologueMissions.Count), Is.EqualTo(LobbyMissions.All));
            foreach (PrologueMission mission in PrologueMissions.All)
            {
                Assert.That(mission.RequiredClearedStage, Is.Zero, mission.Title);
                Assert.That(mission.Chapter, Is.EqualTo("깨어남"));
                // 넘기기 opens with the first mission's win and is used from the second mission on.
                Assert.That(mission.Unlocks, Is.EqualTo(mission.Number == 1 ? CombatFeature.Cycle : CombatFeature.None), mission.Title);
                Assert.That(mission.Features, Is.EqualTo(mission.Number == 1 ? CombatFeature.LaneQ
                    : CombatFeature.LaneQ | CombatFeature.Cycle), mission.Title);
            }
            CombatFeature open = PrologueRun.BaseFeatures | CombatFeature.Cycle;
            for (int index = 0; index < LobbyMissions.Count; index++)
            {
                PrologueMission mission = LobbyMissions.All[index];
                Assert.That(mission.RequiredClearedStage, Is.EqualTo(index + 1), "Each mission waits for the next stage.");
                Assert.That(mission.Unlocks, Is.EqualTo(UnlockOrder[index]), mission.Title);
                open |= mission.Unlocks;
                Assert.That(mission.Features, Is.EqualTo(open), mission.Title + " teaches its feature with everything before it.");
                Assert.That(mission.PlanningTimer, Is.True, "Lobby missions keep the planning timer.");
                Assert.That(mission.Chapter, Is.EqualTo(LobbyMissions.Chapter));
                Assert.That(mission.UnlockText, Is.Not.Empty);
                Assert.That(mission.IntroDialogue, Is.EqualTo($"Dialogue/mission-{mission.Number:00}-intro"));
                foreach (LegacySkill skill in mission.PlayerSkills)
                    Assert.That(mission.Features.HasLane(skill.LaneIndex), Is.True, mission.Title);
            }
            Assert.That(open, Is.EqualTo(CombatFeature.All), "The chain opens every basic feature (반격 comes later).");
        }

        [Test]
        public void MissionOne_FightsTheTrainingDummy()
        {
            PrologueMission first = StoryMissions.Get(1);
            Assert.That(first.EnemyAppearance, Is.EqualTo(EnemyAppearance.TrainingDummy));
            Assert.That(first.Enemies[0].Name, Is.EqualTo("허수아비"));
            Assert.That(first.Enemies[0].SilhouetteResource, Is.EqualTo(PrologueMissions.DummySilhouette));
            foreach (PrologueMission mission in StoryMissions.All.Skip(1))
                Assert.That(mission.EnemyAppearance, Is.EqualTo(EnemyAppearance.Student), mission.Title);
        }

        [Test]
        public void EveryLessonBeat_MatchesWhatItsMissionOpens()
        {
            foreach (PrologueMission mission in StoryMissions.All)
            {
                MissionGuide guide = mission.CreateGuide();
                Assert.That(guide.Beats.Last().Kind, Is.EqualTo(MissionGuideStepKind.Free), mission.Title);
                foreach (MissionGuideBeat beat in guide.Beats)
                {
                    if (beat.Kind == MissionGuideStepKind.Queue) Assert.That(mission.Features.HasLane(beat.Lane), Is.True, mission.Title);
                    if (beat.Kind == MissionGuideStepKind.Breathe) Assert.That(mission.BreathEnabled, Is.True, mission.Title);
                    if (beat.Kind == MissionGuideStepKind.Dodge) Assert.That(mission.Features.Has(CombatFeature.Dodge), Is.True);
                    if (beat.Kind == MissionGuideStepKind.Pressure) Assert.That(mission.Features.Has(CombatFeature.Pressure), Is.True);
                }
                // The scripted planning part must be playable with the starting ACT.
                LegacyQueuedDuel duel = mission.CreateDuel();
                while (!guide.AllowsCommit)
                {
                    if (guide.CanAdvance) Assert.That(guide.TryAdvance(), Is.True);
                    else if (guide.AllowsInspect) guide.NotifyInspected();
                    else if (guide.Kind == MissionGuideStepKind.Breathe)
                    {
                        Assert.That(duel.TryQueueBreath(), Is.True, mission.Title);
                        guide.NotifyBreathed();
                    }
                    else
                    {
                        Assert.That(guide.Kind, Is.EqualTo(MissionGuideStepKind.Queue), mission.Title);
                        Assert.That(duel.TryQueueLane(guide.ExpectedLane), Is.True, mission.Title + ": " + guide.Title);
                        guide.NotifyQueued(guide.ExpectedLane);
                    }
                }
            }
            PrologueMission lanes = LobbyMissions.All[0];
            Assert.That(lanes.CreateDuel().GetLane(2), Is.Not.Empty, "The E-lane mission hands out the E skills.");
            Assert.That(lanes.CreateDuel().GetLane(1), Is.Empty, "W is still closed.");
        }

        [Test]
        public void Duel_LeavesClosedLanesOutAndRefusesClosedActionsWithoutCountingAttempts()
        {
            var duel = new LegacyQueuedDuel(100, 50, 1000, 1000, LegacyInitialSkills.All,
                new[] { LegacySkillDefinitions.Skill(1) }, new[] { 1 }, 3, features: CombatFeature.LaneQ | CombatFeature.LaneE);
            Assert.That(duel.Features, Is.EqualTo(CombatFeature.LaneQ | CombatFeature.LaneE));
            Assert.That(duel.GetLane(0).Count, Is.EqualTo(3));
            Assert.That(duel.GetLane(1), Is.Empty, "A closed lane's skills never reach the duel.");
            Assert.That(duel.GetLane(2).Count, Is.EqualTo(3));
            Assert.That(duel.TryQueueLane(1), Is.False);
            Assert.That(duel.TryQueueBreath(), Is.False, "Breathing is closed.");
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.ResolveNextSlot();
            Assert.That(duel.TryStep(LegacyStepAction.Dodge, true, out bool success), Is.False);
            Assert.That(duel.TryStep(LegacyStepAction.Pressure, true, out success), Is.False);
            Assert.That(success, Is.False);
            Assert.That(duel.StepAttemptsThisTurn, Is.Zero, "A closed step is not an attempt and costs nothing.");
            Assert.That(duel.StepMissedThisTurn, Is.False);

            var dodgeOnly = new LegacyQueuedDuel(100, 50, 1000, 1000, LegacyInitialSkills.All,
                new[] { LegacySkillDefinitions.Skill(1) }, new[] { 1 }, 3, features: CombatFeature.LaneQ | CombatFeature.Dodge);
            Assert.That(dodgeOnly.TryQueueLane(0), Is.True);
            dodgeOnly.Commit();
            Assert.That(dodgeOnly.TryStep(LegacyStepAction.Pressure, true, out _), Is.False);
            Assert.That(dodgeOnly.TryStep(LegacyStepAction.Dodge, false, out _), Is.True, "Dodge alone is open.");
            Assert.That(dodgeOnly.StepAttemptsThisTurn, Is.EqualTo(1));

            var everything = new LegacyQueuedDuel();
            Assert.That(everything.Features, Is.EqualTo(CombatFeature.All), "Duels built without a set keep everything.");
            Assert.Throws<ArgumentException>(() => new LegacyQueuedDuel(100, 50, 10, 10, LegacyInitialSkills.All,
                new[] { LegacySkillDefinitions.Skill(1) }, new[] { 1 }, 1, features: CombatFeature.Breath));
        }

        [Test]
        public void StoryProgress_UnlocksInOrderAndCapsStagesUntilEachMissionIsWon()
        {
            var story = new PrologueRun();
            var cleared = new bool[9];
            Func<int, bool> isCleared = stage => cleared[stage];
            Assert.That(story.ArcMissionCount, Is.EqualTo(PrologueMissions.Count));
            Assert.That(story.StageLimit, Is.Zero, "No stage before the 서막 ends.");
            Assert.That(story.UnlockedFeatures, Is.EqualTo(CombatFeature.LaneQ));
            for (int number = 1; number <= PrologueMissions.Count; number++)
            {
                Assert.That(story.CanPlayCurrent(isCleared), Is.True);
                Assert.That(story.TryComplete(number, DuelMatchOutcome.PlayerVictory), Is.True);
            }
            Assert.That(story.IsArcComplete, Is.True);
            Assert.That(story.UnlockedFeatures, Is.EqualTo(CombatFeature.LaneQ | CombatFeature.Cycle),
                "The lobby opens with the Q lane only, and 넘기기 from the 서막.");
            CombatFeature expected = CombatFeature.LaneQ | CombatFeature.Cycle;
            for (int index = 0; index < LobbyMissions.Count; index++)
            {
                int stage = index + 1;
                Assert.That(story.StageLimit, Is.EqualTo(stage), "Only up to the stage the next mission waits for.");
                Assert.That(story.CanPlayCurrent(isCleared), Is.False, "The mission waits for its stage.");
                cleared[stage] = true;
                Assert.That(story.CanPlayCurrent(isCleared), Is.True);
                Assert.That(story.TryComplete(PrologueMissions.Count + stage, DuelMatchOutcome.EnemyVictory), Is.False);
                Assert.That(story.TryComplete(PrologueMissions.Count + stage, DuelMatchOutcome.PlayerVictory), Is.True);
                expected |= UnlockOrder[index];
                Assert.That(story.UnlockedFeatures, Is.EqualTo(expected));
            }
            Assert.That(story.IsComplete, Is.True);
            Assert.That(story.StageLimit, Is.EqualTo(int.MaxValue));
            Assert.That(story.UnlockedFeatures, Is.EqualTo(CombatFeature.All));
            Assert.That(story.CanPlayCurrent(isCleared), Is.False, "Nothing is left to play.");
        }

        [Test]
        public void CampaignProgression_FiltersBattleLanesCapsStagesAndLocksClosedLaneEditing()
        {
            var run = new CampaignRun();
            Assert.That(run.Features, Is.EqualTo(CombatFeature.All));
            Assert.That(run.StageLimit, Is.EqualTo(int.MaxValue));
            run.SetProgression(CombatFeature.LaneQ | CombatFeature.LaneE, 1);
            Assert.That(run.IsLaneOpen(1), Is.False);
            Assert.That(run.TryStartStage(1), Is.True);
            LegacyQueuedDuel duel = run.CreateDuel(1);
            Assert.That(duel.Features, Is.EqualTo(CombatFeature.LaneQ | CombatFeature.LaneE));
            Assert.That(duel.GetLane(1), Is.Empty, "The W skills stay in the loadout but not in the battle.");
            Assert.That(duel.GetLane(2).Count, Is.EqualTo(3));
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.ReturnToLobby(), Is.True);
            Assert.That(run.HighestUnlockedStage, Is.EqualTo(2));
            Assert.That(run.CanStartStage(2), Is.False, "Stage 2 waits for its mission.");
            Assert.That(run.IsStageWaitingForMission(2), Is.True);
            Assert.That(run.IsStageWaitingForMission(1), Is.False);
            Assert.That(run.IsStageWaitingForMission(3), Is.False, "A locked stage is not waiting for a mission yet.");

            int wSkill = run.GetEquippedLane(1)[0].SkillId;
            Assert.That(run.TryUnequipSkill(wSkill), Is.False, "Closed lanes stay complete for the day they open.");
            Assert.That(run.TryMoveEquippedSkill(wSkill, 1), Is.False);
            Assert.That(run.TryPlaceLoadoutSkill(wSkill, 1, 2), Is.False);
            Assert.That(run.TryUnequipSkill(run.GetEquippedLane(2)[0].SkillId), Is.True, "Open lanes are editable.");
            Assert.That(run.TryResetLoadout(), Is.True);

            run.Reset();
            Assert.That(run.Features, Is.EqualTo(CombatFeature.LaneQ | CombatFeature.LaneE), "A new journey keeps the story.");
            run.SetProgression(CombatFeature.All, 2);
            Assert.That(run.TryStartStage(1), Is.True);
            run.ClearProgression();
            Assert.That(run.StageLimit, Is.EqualTo(int.MaxValue));
            Assert.Throws<ArgumentException>(() => run.SetProgression(CombatFeature.Breath, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => run.SetProgression(CombatFeature.LaneQ, -1));
        }

        [Test]
        public void Curriculum_OpensWithTheLastLane_WhichMissionEightsWinBrings()
        {
            Assert.That(CampaignRun.OpensCurriculum(CombatFeature.All), Is.True);
            Assert.That(CampaignRun.OpensCurriculum(CombatFeature.LaneQ | CombatFeature.LaneW | CombatFeature.LaneE), Is.True,
                "Only the lanes count.");
            Assert.That(CampaignRun.OpensCurriculum(CombatFeature.All & ~CombatFeature.LaneW), Is.False);
            Assert.That(StoryMissions.Get(8).Unlocks, Is.EqualTo(CombatFeature.LaneW));
            var story = new PrologueRun();
            for (int cleared = 0; cleared <= story.MissionCount; cleared++)
            {
                Assert.That(story.TryRestore(cleared), Is.True);
                Assert.That(CampaignRun.OpensCurriculum(story.UnlockedFeatures), Is.EqualTo(cleared >= 8),
                    cleared + " missions won");
            }
            Assert.That(new CampaignRun().IsCurriculumOpen, Is.True, "Outside the story everything is open.");

            // Mission 8 says so, in its objective and in what its win announces.
            PrologueMission eight = StoryMissions.Get(8);
            Assert.That(eight.Objectives.Last(), Is.EqualTo("완료하면 W열과 커리큘럼이 열린다"));
            Assert.That(eight.UnlockText, Does.Contain("W열").And.Contain("커리큘럼"));
            foreach (PrologueMission mission in StoryMissions.All.Where(mission => mission.Number != 8))
            {
                foreach (string objective in mission.Objectives) Assert.That(objective, Does.Not.Contain("커리큘럼"), mission.Title);
                Assert.That(mission.UnlockText ?? string.Empty, Does.Not.Contain("커리큘럼"), mission.Title);
            }
        }

        [Test]
        public void ClosedCurriculum_RefusesChoicesAndCountsNoBattle_ButKeepsSavedProgressUntilItOpens()
        {
            const CombatFeature beforeW = CombatFeature.All & ~CombatFeature.LaneW & ~CombatFeature.Pressure;
            var run = new CampaignRun();
            run.SetProgression(beforeW, int.MaxValue);
            Assert.That(run.IsCurriculumOpen, Is.False);
            Assert.That(run.Curriculum.CanSelect("horizontal-cut"), Is.True, "The tree itself is unchanged…");
            Assert.That(run.TrySelectCurriculumNode("horizontal-cut"), Is.False, "…but the run refuses every choice.");
            Assert.That(run.Curriculum.Active, Is.Null);
            Assert.That(run.TryResetCurriculum(), Is.False);
            int owned = run.OwnedSkills.Count;
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.LastCompletedCurriculumNode, Is.Null);
            Assert.That(run.Curriculum.CompletedCount, Is.Zero);
            Assert.That(run.Curriculum.ActiveBattles, Is.Zero);
            Assert.That(run.OwnedSkills.Count, Is.EqualTo(owned + 1),
                "The closed curriculum grants nothing, but stage 1 still awards 탐색.");
            Assert.That(run.Phase, Is.EqualTo(CampaignPhase.Maintenance));
            Assert.That(run.TrySelectCurriculumNode("horizontal-cut"), Is.False, "Not in maintenance either.");
            Assert.That(run.ReturnToLobby(), Is.True);

            // An older save may hold progress made before the curriculum waited for mission 8.
            var played = new CampaignRun();
            Assert.That(played.TrySelectCurriculumNode("horizontal-cut"), Is.True);
            Assert.That(played.TryStartStage(1), Is.True);
            Assert.That(played.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(played.ReturnToLobby(), Is.True);
            Assert.That(played.TrySelectCurriculumNode("diagonal-cut"), Is.True);
            CampaignSave save = played.CaptureSave();
            Assert.That(run.TryRestore(save, out string error), Is.True, error);
            Assert.That(run.IsCurriculumOpen, Is.False, "A restore leaves the story's features alone.");
            CollectionAssert.AreEqual(new[] { "horizontal-cut" }, run.Curriculum.Completed, "The saved progress is kept…");
            Assert.That(run.Curriculum.Active.Id, Is.EqualTo("diagonal-cut"));
            Assert.That(run.OwnedSkills.Any(skill => skill.SkillId == 14), Is.True, "…with the skill it already granted.");
            Assert.That(run.TrySelectCurriculumNode("advance"), Is.False);
            Assert.That(run.TryResetCurriculum(), Is.False);
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.Draw), Is.True);
            Assert.That(run.LastCompletedCurriculumNode, Is.Null);
            Assert.That(run.Curriculum.IsCompleted("diagonal-cut"), Is.False, "The node in progress waits, untouched.");
            Assert.That(run.Curriculum.ActiveBattles, Is.Zero);
            Assert.That(run.OwnedSkills.Any(skill => skill.SkillId == 15), Is.False);
            Assert.That(run.ReturnToLobby(), Is.True);
            CampaignSave again = run.CaptureSave();
            CollectionAssert.AreEqual(save.CurriculumCompleted, again.CurriculumCompleted, "Saving keeps it as it was.");
            Assert.That(again.CurriculumActive, Is.EqualTo("diagonal-cut"));
            Assert.That(again.CurriculumBattles, Is.Zero);

            // Mission 8's W lane opens it, and the waiting node counts from the next battle.
            run.SetProgression(beforeW | CombatFeature.LaneW, int.MaxValue);
            Assert.That(run.IsCurriculumOpen, Is.True);
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.LastCompletedCurriculumNode.Id, Is.EqualTo("diagonal-cut"));
            Assert.That(run.OwnedSkills.Any(skill => skill.SkillId == 15), Is.True);
            Assert.That(run.ReturnToLobby(), Is.True);
            Assert.That(run.TrySelectCurriculumNode("advance"), Is.True);
            Assert.That(run.TryResetCurriculum(), Is.True);
            Assert.That(run.Curriculum.CompletedCount, Is.Zero);
        }

        [Test]
        public void Guide_BreatheAndStepBeatsWaitForTheirInputOrTheNextTurn()
        {
            var guide = new MissionGuide(new[]
            {
                new MissionGuideBeat(MissionGuideStepKind.Breathe, "숨", "", ""),
                new MissionGuideBeat(MissionGuideStepKind.Commit, "확정", "", ""),
                new MissionGuideBeat(MissionGuideStepKind.Dodge, "회피", "", ""),
                new MissionGuideBeat(MissionGuideStepKind.Pressure, "압박", "", ""),
                new MissionGuideBeat(MissionGuideStepKind.Free, "자유", "", ""),
            });
            Assert.That(guide.AllowsBreath, Is.True);
            Assert.That(guide.AllowsQueue(0), Is.False);
            Assert.That(guide.AllowsCommit, Is.False);
            guide.NotifyQueued(0);
            Assert.That(guide.Kind, Is.EqualTo(MissionGuideStepKind.Breathe));
            guide.NotifyBreathed();
            Assert.That(guide.Kind, Is.EqualTo(MissionGuideStepKind.Commit));
            Assert.That(guide.AllowsBreath, Is.False);
            guide.NotifyCommitted();
            Assert.That(guide.Kind, Is.EqualTo(MissionGuideStepKind.Dodge));
            Assert.That(guide.AllowsStep(LegacyStepAction.Dodge), Is.True);
            Assert.That(guide.AllowsStep(LegacyStepAction.Pressure), Is.False, "Only the step being taught.");
            guide.NotifyStepped(LegacyStepAction.Pressure);
            Assert.That(guide.Kind, Is.EqualTo(MissionGuideStepKind.Dodge));
            guide.NotifyStepped(LegacyStepAction.Dodge);
            Assert.That(guide.Kind, Is.EqualTo(MissionGuideStepKind.Pressure));
            guide.NotifyTurnBegan(1);
            Assert.That(guide.Kind, Is.EqualTo(MissionGuideStepKind.Pressure));
            guide.NotifyTurnBegan(2);
            Assert.That(guide.IsFree, Is.True, "A missed step lesson never stalls the mission.");
            Assert.That(guide.AllowsBreath, Is.True);
            Assert.That(guide.AllowsStep(LegacyStepAction.Dodge) && guide.AllowsStep(LegacyStepAction.Pressure), Is.True);
            guide.Finish();
            Assert.That(guide.AllowsBreath || guide.AllowsStep(LegacyStepAction.Dodge), Is.False);
        }

        [Test]
        public void MissionModel_RejectsSkillsOutsideItsLanesAndStoriesThatPutStageMissionsFirst()
        {
            Assert.Throws<ArgumentException>(() => new PrologueRun(new[] { LobbyMissions.All[0], PrologueMissions.All[0] }));
            var arcOnly = new PrologueRun(PrologueMissions.All);
            Assert.That(arcOnly.ArcMissionCount, Is.EqualTo(PrologueMissions.Count));
            arcOnly.CompleteAll();
            Assert.That(arcOnly.IsComplete && arcOnly.IsArcComplete, Is.True);
            Assert.That(arcOnly.StageLimit, Is.EqualTo(int.MaxValue));
        }
    }
}
