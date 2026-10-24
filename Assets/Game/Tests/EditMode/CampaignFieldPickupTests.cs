using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Sheets;

namespace TurnLimbo.Core.Tests
{
    /// <summary>Techniques lying in the academy field (<see cref="CampaignFieldPickups"/>): where they lie and how
    /// <see cref="CampaignRun"/> grants them. Saving them is covered in <see cref="GameSaveTests"/>, the sheet check in
    /// <see cref="CampaignSheetCheckTests"/>.</summary>
    public sealed class CampaignFieldPickupTests
    {
        private const string Pierce = "forest-pierce", StrikeBack = "forest-strike-back", Sidestep = "forest-sidestep",
            Finisher = "forest-finisher";

        [Test]
        public void Pickups_LieOnTheForestPath_EachGrantingItsOwnAcquisitionRow()
        {
            Assert.That(CampaignFieldPickups.All.Select(pickup => pickup.Id),
                Is.EqualTo(new[] { Pierce, StrikeBack, Sidestep, Finisher }));
            Assert.That(CampaignFieldPickups.All.Select(pickup => pickup.SkillId), Is.EqualTo(new[] { 47, 48, 49, 50 }));
            Assert.That(CampaignFieldPickups.In(CampaignFieldArea.Forest), Is.EqualTo(CampaignFieldPickups.All));
            Assert.That(CampaignFieldPickups.In(CampaignFieldArea.Corridor), Is.Empty);
            float previousX = float.MinValue;
            foreach (CampaignFieldPickup pickup in CampaignFieldPickups.All)
            {
                Assert.That(pickup.Id, Does.Match("^[a-z]+(-[a-z]+)*$"), "A save token: " + pickup.Id);
                Assert.That(CampaignFieldPickups.Find(pickup.Id), Is.SameAs(pickup));
                Assert.That(CampaignFieldPickups.FindGranting(pickup.SkillId), Is.SameAs(pickup));
                LegacySkillDefinition definition = LegacySkillDefinitions.Find(pickup.SkillId);
                Assert.That(definition, Is.Not.Null, pickup.Id);
                Assert.That(LegacySkillDefinitions.Table.GroupOf(definition), Is.EqualTo(LegacySkillGroup.Acquisition), pickup.Id);
                Assert.That(CampaignCurriculum.Default.FindGranting(pickup.SkillId), Is.Null, pickup.Id);
                // AcademyFieldView's walking belt and the forest's path: the left wall to the fence (x about 28.3), clear
                // of the interaction range (2.8) around the door's arrival point (x about -26.8).
                Assert.That(pickup.Y, Is.InRange(-2.3f, -0.55f), pickup.Id);
                Assert.That(pickup.X, Is.InRange(-26.8f + 2.8f, 28.3f - 1f), pickup.Id);
                Assert.That(pickup.X, Is.GreaterThan(previousX + 2.8f * 2), "Left to right, each its own nearest target.");
                previousX = pickup.X;
            }
            Assert.That(CampaignFieldPickups.Find("no-such-pickup"), Is.Null);
            Assert.That(CampaignFieldPickups.Find(null), Is.Null);
            Assert.That(CampaignFieldPickups.FindGranting(1), Is.Null, "Starting skills never lie in the field.");
        }

        [Test]
        public void Collecting_OwnsTheSkillUnequipped_OnceOnly()
        {
            var run = new CampaignRun();
            Assert.That(run.CanCollectFieldPickup(Finisher), Is.True);
            Assert.That(run.TryCollectFieldPickup(Finisher, out CampaignOwnedSkill granted), Is.True);
            Assert.That(granted, Is.SameAs(run.GetOwnedSkill(50)));
            Assert.That(granted.Skill.Name, Is.EqualTo("끝내기"));
            Assert.That(granted.Experience, Is.Zero);
            Assert.That(run.OwnedSkills.Last(), Is.SameAs(granted), "Granted after the starting nine.");
            Assert.That(run.IsSkillEquipped(50) || run.IsSkillInLoadout(50), Is.False, "Never equipped on its own.");
            Assert.That(run.HasLoadoutChanges, Is.False);
            Assert.That(run.IsFieldPickupCollected(Finisher), Is.True);
            Assert.That(run.CollectedFieldPickups, Is.EqualTo(new[] { Finisher }));

            int owned = run.OwnedSkills.Count;
            Assert.That(run.CanCollectFieldPickup(Finisher), Is.False);
            Assert.That(run.TryCollectFieldPickup(Finisher, out granted), Is.False, "A pickup is taken once.");
            Assert.That(granted, Is.Null);
            Assert.That(run.OwnedSkills.Count, Is.EqualTo(owned));
            Assert.That(run.CollectedFieldPickups, Is.EqualTo(new[] { Finisher }));
            Assert.That(run.TryCollectFieldPickup("no-such-pickup"), Is.False);
            Assert.That(run.TryCollectFieldPickup(null), Is.False);
            Assert.That(run.IsFieldPickupCollected(null), Is.False);

            Assert.That(run.TryCollectFieldPickup(Pierce), Is.True);
            Assert.That(run.CollectedFieldPickups, Is.EqualTo(new[] { Finisher, Pierce }), "In the order they were taken.");
            Assert.That(run.OwnedSkills.Where(owned => !run.IsCounterSkill(owned.SkillId) && !LegacySkillDefinitions.IsPrototype(owned.Skill)).Skip(9).Select(entry => entry.SkillId), Is.EqualTo(new[] { 50, 47 }));

            // Equipped through the loadout like any other owned skill.
            Assert.That(run.TryPlaceLoadoutSkill(50, 2, 0), Is.True);
            Assert.That(run.TrySaveLoadout(), Is.True);
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.CreateDuel(1).GetLane(2)[0].Id, Is.EqualTo(50));
        }

        [Test]
        public void Collecting_WaitsForTheLobby_AndAClosedLaneStillOwnsTheSkill()
        {
            var run = new CampaignRun();
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.CanCollectFieldPickup(Sidestep), Is.False, "Not in the middle of a battle.");
            Assert.That(run.TryCollectFieldPickup(Sidestep), Is.False);
            Assert.That(run.GetOwnedSkill(49), Is.Null);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.Phase, Is.EqualTo(CampaignPhase.Maintenance));
            Assert.That(run.TryCollectFieldPickup(Sidestep), Is.True, "Between battles is fine.");
            Assert.That(run.ReturnToLobby(), Is.True);

            // The story keeps E closed: the skill is owned and waits for its lane.
            run.SetProgression(CombatFeature.LaneQ | CombatFeature.LaneW, int.MaxValue);
            Assert.That(run.IsLaneOpen(2), Is.False);
            Assert.That(run.TryCollectFieldPickup(StrikeBack, out CampaignOwnedSkill granted), Is.True);
            Assert.That(granted.Skill.LaneIndex, Is.EqualTo(2));
            Assert.That(run.TryEquipSkill(48), Is.False, "Its lane is closed.");
            run.ClearProgression();
            Assert.That(run.TryPlaceLoadoutSkill(48, 2, 1), Is.True);
        }

        [Test]
        public void CurriculumReset_KeepsFieldSkills_AndANewJourneyPutsThemBack()
        {
            var run = new CampaignRun();
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.EnemyVictory), Is.True);
            Assert.That(run.ReturnToLobby(), Is.True);
            Assert.That(run.TryLearnCurriculumNode("horizontal-cut"), Is.True);
            Assert.That(run.TryCollectFieldPickup(Pierce), Is.True);
            Assert.That(run.TryPlaceLoadoutSkill(47, 1, 0) && run.TryPlaceLoadoutSkill(14, 0, 0) && run.TrySaveLoadout(), Is.True);

            Assert.That(run.TryResetCurriculum(), Is.True);
            Assert.That(run.GetOwnedSkill(14), Is.Null, "The curriculum's skill goes.");
            Assert.That(run.GetOwnedSkill(47), Is.Not.Null, "The field's skill stays.");
            Assert.That(run.GetEquippedLane(1)[0].SkillId, Is.EqualTo(47), "And stays equipped.");
            Assert.That(run.IsFieldPickupCollected(Pierce), Is.True);

            run.Reset();
            Assert.That(run.GetOwnedSkill(47), Is.Null);
            Assert.That(run.CollectedFieldPickups, Is.Empty);
            Assert.That(run.CanCollectFieldPickup(Pierce), Is.True, "A new journey lays it out again.");
        }

        [Test]
        public void PickupWhoseSkillIsMissing_ThrowsTheSheetErrorBeforeAnythingChanges()
        {
            List<string[]> rows = CsvTable.Read(LegacySkillSheet.Write(LegacySkillDefinitions.Table))
                .Select(record => record.Fields.ToArray()).ToList();
            rows.Remove(rows.Single(row => row[0] == "48"));
            string sheet = CsvTable.Write(rows);
            try
            {
                LegacySkillDefinitions.Install(() => sheet);
                var run = new CampaignRun();
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    var error = Assert.Throws<InvalidOperationException>(() => run.TryCollectFieldPickup(StrikeBack));
                    Assert.That(error.Message, Does.Contain("ID 48 기술이 없습니다"));
                    Assert.That(run.IsFieldPickupCollected(StrikeBack), Is.False);
                    Assert.That(run.OwnedSkills.Count(owned => !run.IsCounterSkill(owned.SkillId) && !LegacySkillDefinitions.IsPrototype(owned.Skill)), Is.EqualTo(9));
                }
                Assert.That(run.TryCollectFieldPickup(Sidestep), Is.True, "The others still work.");
            }
            finally
            {
                LegacySkillDefinitions.Install(null);
            }
        }
    }
}
