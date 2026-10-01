using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;

namespace TurnLimbo.Core.Tests
{
    public sealed class EncounterKindTests
    {
        [Test]
        public void TheArcIsADuel_AndEveryLaterMissionAndStageIsABattle()
        {
            foreach (PrologueMission mission in StoryMissions.All)
                Assert.That(mission.Encounter, Is.EqualTo(mission.RequiredClearedStage == 0 ? EncounterKind.Duel : EncounterKind.Battle),
                    mission.Title);
            Assert.That(PrologueMissions.Get(PrologueMissions.Count).Encounter, Is.EqualTo(EncounterKind.Duel), "The 서막 ends as a 결투.");
            Assert.That(StoryMissions.All[PrologueMissions.Count].Encounter, Is.EqualTo(EncounterKind.Battle), "가르침 starts the 전투.");
            var campaign = new CampaignRun();
            for (int stage = 1; stage <= campaign.StageCount; stage++)
                Assert.That(campaign.GetStage(stage).Encounter, Is.EqualTo(EncounterKind.Battle), "Stage " + stage);
        }

        [Test]
        public void TheKindChangesNoRule_MissionDuelsAreBuiltTheSameWay()
        {
            // The classification lives only on the mission; the duel it builds carries no trace of it.
            PrologueMission duel = PrologueMissions.Get(2), battle = StoryMissions.All[PrologueMissions.Count];
            Assert.That(duel.CreateDuel(1).Features, Is.EqualTo(duel.Features));
            Assert.That(battle.CreateDuel(1).Features, Is.EqualTo(battle.Features));
        }
    }
}
